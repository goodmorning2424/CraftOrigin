using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePadSceneRoot : MonoBehaviour
    {
        [SerializeField] private CraftLiveRole role;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private bool orthographic;
        [SerializeField, Min(0.01f)] private float orthographicSize = 5f;
        [SerializeField, Range(1f, 179f)] private float fieldOfView = 60f;
        [SerializeField] private Color backgroundColor =
            new Color(0.03f, 0.03f, 0.03f, 1f);

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

        private void Awake()
        {
            ApplySceneFont();
        }

        private void Start()
        {
            ApplySceneFont();
        }

        private void LateUpdate()
        {
            if (!applyFontToAllTextMeshes || sceneFont == null ||
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
        }

        [ContextMenu("Apply Scene Font Now")]
        public void ApplySceneFont()
        {
            if (!applyFontToAllTextMeshes || sceneFont == null ||
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

            if (text.font != sceneFont)
            {
                text.font = sceneFont;
            }

            Renderer textRenderer = text.GetComponent<Renderer>();
            if (textRenderer != null && sceneFont.material != null &&
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
