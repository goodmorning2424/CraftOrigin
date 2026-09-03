using UnityEngine;
using UnityEngine.Rendering;

namespace CraftOrigin.CraftLive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CraftLiveCameraMirror : MonoBehaviour
    {
        [SerializeField] private bool mirrorHorizontally = true;

        private Camera targetCamera;

        public bool MirrorHorizontally => mirrorHorizontally;

        public void Configure(bool horizontal)
        {
            mirrorHorizontally = horizontal;
            enabled = horizontal;
            if (!horizontal)
            {
                ResolveCamera()?.ResetProjectionMatrix();
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -=
                HandleBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering +=
                HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -=
                HandleEndCameraRendering;
            RenderPipelineManager.endCameraRendering +=
                HandleEndCameraRendering;
        }

        private void OnPreCull()
        {
            ApplyMirror(ResolveCamera());
            GL.invertCulling = mirrorHorizontally;
        }

        private void OnPostRender()
        {
            GL.invertCulling = false;
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (camera != ResolveCamera())
            {
                return;
            }

            ApplyMirror(camera);
            GL.invertCulling = mirrorHorizontally;
        }

        private void HandleEndCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            if (camera == ResolveCamera())
            {
                GL.invertCulling = false;
            }
        }

        private void ApplyMirror(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.ResetProjectionMatrix();
            if (!mirrorHorizontally)
            {
                return;
            }

            Matrix4x4 projection = camera.projectionMatrix;
            projection.m00 = -projection.m00;
            camera.projectionMatrix = projection;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -=
                HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -=
                HandleEndCameraRendering;
            GL.invertCulling = false;
            ResolveCamera()?.ResetProjectionMatrix();
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            return targetCamera;
        }
    }

    [ExecuteAlways]
    public sealed class CraftLivePadSceneRoot : MonoBehaviour
    {
        [SerializeField] private CraftLiveRole role;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private bool orthographic;
        [SerializeField, Min(0.01f)] private float orthographicSize = 5f;
        [SerializeField, Range(1f, 179f)] private float fieldOfView = 60f;
        [SerializeField] private Color backgroundColor =
            new Color(0.03f, 0.03f, 0.03f, 1f);
        [SerializeField, Tooltip(
            "Mirrors the complete pad output for reflected displays such as Pad4's acrylic plate.")]
        private bool mirrorHorizontally;

        [Header("Scene Font")]
        [SerializeField, Tooltip(
            "Font used by every TextMesh in this pad scene. Leave empty to keep each text's current font.")]
        private Font sceneFont;
        [SerializeField, Tooltip(
            "Also applies the scene font to TextMesh objects created while the scene is running.")]
        private bool applyFontToAllTextMeshes = true;
        [SerializeField, Min(0.1f), Tooltip(
            "How often the scene checks for newly-created TextMesh objects.")]
        private float fontRefreshInterval = 0.5f;

        private float nextFontRefreshTime;

        public CraftLiveRole Role => role;
        public Transform CameraAnchor => cameraAnchor;
        public Font SceneFont => sceneFont;
        public bool MirrorHorizontally => mirrorHorizontally;

        private void Awake()
        {
            ApplySceneFont();
        }

        private void Start()
        {
            ApplySceneFont();
            CraftLiveAudio.StartBackground(role);
        }

        private void LateUpdate()
        {
            if (!applyFontToAllTextMeshes ||
                Time.unscaledTime < nextFontRefreshTime)
            {
                return;
            }

            nextFontRefreshTime = Time.unscaledTime + fontRefreshInterval;
            ApplySceneFont();
        }

        public void ApplyCamera(Camera targetCamera)
        {
            if (targetCamera == null)
            {
                return;
            }

            if (cameraAnchor != null)
            {
                targetCamera.transform.SetPositionAndRotation(
                    cameraAnchor.position,
                    cameraAnchor.rotation);
            }

            targetCamera.orthographic = orthographic;
            targetCamera.orthographicSize =
                Mathf.Max(0.01f, orthographicSize);
            targetCamera.fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
            targetCamera.backgroundColor = backgroundColor;

            CraftLiveCameraMirror mirror =
                targetCamera.GetComponent<CraftLiveCameraMirror>();
            if (mirrorHorizontally && mirror == null)
            {
                mirror = targetCamera.gameObject.AddComponent<
                    CraftLiveCameraMirror>();
            }

            if (mirror != null)
            {
                mirror.Configure(mirrorHorizontally);
            }
        }

        [ContextMenu("Apply Scene Font Now")]
        public void ApplySceneFont()
        {
            if (!applyFontToAllTextMeshes ||
                !gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return;
            }

            GameObject[] sceneRoots = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < sceneRoots.Length; rootIndex++)
            {
                TextMesh[] texts = sceneRoots[rootIndex]
                    .GetComponentsInChildren<TextMesh>(true);
                for (int textIndex = 0; textIndex < texts.Length; textIndex++)
                {
                    ApplyFont(texts[textIndex]);
                }
            }
        }

        public static bool TryApplyConfiguredFont(TextMesh text)
        {
            if (text == null || !text.gameObject.scene.IsValid() ||
                !text.gameObject.scene.isLoaded)
            {
                return false;
            }

            GameObject[] sceneRoots = text.gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < sceneRoots.Length; index++)
            {
                CraftLivePadSceneRoot padRoot = sceneRoots[index]
                    .GetComponentInChildren<CraftLivePadSceneRoot>(true);
                if (padRoot == null || !padRoot.applyFontToAllTextMeshes ||
                    padRoot.sceneFont == null)
                {
                    continue;
                }

                padRoot.ApplyFont(text);
                return true;
            }

            return false;
        }

        private void ApplyFont(TextMesh text)
        {
            if (text == null)
            {
                return;
            }

            text.fontStyle = FontStyle.Bold;

            if (sceneFont != null && text.font != sceneFont)
            {
                text.font = sceneFont;
            }
            else if (sceneFont == null)
            {
                CraftLiveForgeUITheme.ApplyBundledFont(text);
            }

            Renderer textRenderer = text.GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                textRenderer.receiveShadows = false;
            }

            if (textRenderer != null && sceneFont != null &&
                sceneFont.material != null &&
                textRenderer.sharedMaterial != sceneFont.material)
            {
                textRenderer.sharedMaterial = sceneFont.material;
            }
        }

        private void OnValidate()
        {
            orthographicSize = Mathf.Max(0.01f, orthographicSize);
            fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
            fontRefreshInterval = Mathf.Max(0.1f, fontRefreshInterval);

            if (!Application.isPlaying)
            {
                ApplySceneFont();
            }
        }
    }
}
