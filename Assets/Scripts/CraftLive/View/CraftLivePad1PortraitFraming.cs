using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Frames the complete Pad 1 wooden Box while reserving a clear band above
    /// it for the comment board. No wall transform is changed; only the camera
    /// and its scene anchor are adjusted for the active screen aspect ratio.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class CraftLivePad1PortraitFraming : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Renderer boxRenderer;
        [SerializeField] private Transform cameraAnchor;

        [Header("Safe Area")]
        [SerializeField, Range(0.55f, 0.9f)]
        private float portraitBoxWidth = 0.84f;
        [SerializeField, Range(0.5f, 0.82f)]
        private float portraitBoxHeight = 0.68f;
        [SerializeField, Range(0.25f, 0.55f)]
        private float portraitBoxCenterY = 0.405f;
        [SerializeField, Range(0.5f, 0.9f)]
        private float landscapeBoxWidth = 0.72f;
        [SerializeField, Range(0.5f, 0.82f)]
        private float landscapeBoxHeight = 0.72f;
        [SerializeField, Range(0.25f, 0.6f)]
        private float landscapeBoxCenterY = 0.43f;

        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private Vector3 originalAnchorPosition;
        private Quaternion originalAnchorRotation;
        private bool capturedOriginalPose;
        private float appliedAspect = -1f;

        public Renderer BoxRenderer => boxRenderer;

        private void Start()
        {
            ApplyNow();
        }

        private void LateUpdate()
        {
            float aspect = ResolveAspect();
            if (Mathf.Abs(aspect - appliedAspect) > 0.001f)
            {
                ApplyNow();
            }
        }

        private void OnDisable()
        {
            if (!capturedOriginalPose)
            {
                return;
            }

            if (targetCamera != null)
            {
                targetCamera.transform.SetPositionAndRotation(
                    originalCameraPosition,
                    originalCameraRotation);
            }

            if (cameraAnchor != null)
            {
                cameraAnchor.SetPositionAndRotation(
                    originalAnchorPosition,
                    originalAnchorRotation);
            }
        }

        public void Configure(
            Camera camera,
            Transform anchor)
        {
            targetCamera = camera;
            cameraAnchor = anchor;
            CaptureOriginalPose();
            ResolveBoxRenderer();
            ApplyNow();
        }

        public void ApplyNow()
        {
            ResolveReferences();
            if (targetCamera == null || boxRenderer == null ||
                targetCamera.orthographic)
            {
                return;
            }

            CaptureOriginalPose();
            float aspect = ResolveAspect();
            Rect targetRect = GetTargetBoxViewportRect(
                aspect,
                portraitBoxWidth,
                portraitBoxHeight,
                portraitBoxCenterY,
                landscapeBoxWidth,
                landscapeBoxHeight,
                landscapeBoxCenterY);
            Quaternion rotation = originalCameraRotation;
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Bounds bounds = boxRenderer.bounds;

            GetCameraOrientedExtents(
                bounds,
                right,
                up,
                forward,
                out float halfWidth,
                out float halfHeight,
                out float halfDepth);
            float verticalTangent = Mathf.Tan(
                targetCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float horizontalTangent = verticalTangent * aspect;
            float distanceForWidth = halfDepth + halfWidth /
                Mathf.Max(0.0001f,
                    targetRect.width * 0.5f * horizontalTangent);
            float distanceForHeight = halfDepth + halfHeight /
                Mathf.Max(0.0001f,
                    targetRect.height * 0.5f * verticalTangent);
            float distance = Mathf.Max(
                targetCamera.nearClipPlane + halfDepth + 0.1f,
                distanceForWidth,
                distanceForHeight);

            Transform cameraTransform = targetCamera.transform;
            cameraTransform.SetPositionAndRotation(
                bounds.center - forward * distance,
                rotation);
            Vector3 targetWorld = targetCamera.ViewportToWorldPoint(
                new Vector3(
                    targetRect.center.x,
                    targetRect.center.y,
                    distance));
            cameraTransform.position += bounds.center - targetWorld;

            if (cameraAnchor != null)
            {
                cameraAnchor.SetPositionAndRotation(
                    cameraTransform.position,
                    cameraTransform.rotation);
            }

            appliedAspect = aspect;
        }

        public static Rect GetTargetBoxViewportRect(float aspect)
        {
            return GetTargetBoxViewportRect(
                aspect,
                0.84f,
                0.68f,
                0.405f,
                0.72f,
                0.72f,
                0.43f);
        }

        private static Rect GetTargetBoxViewportRect(
            float aspect,
            float portraitWidth,
            float portraitHeight,
            float portraitCenterY,
            float landscapeWidth,
            float landscapeHeight,
            float landscapeCenterY)
        {
            bool portrait = aspect < 1f;
            float width = portrait ? portraitWidth : landscapeWidth;
            float height = portrait ? portraitHeight : landscapeHeight;
            float centerY = portrait
                ? portraitCenterY
                : landscapeCenterY;
            return new Rect(
                0.5f - width * 0.5f,
                centerY - height * 0.5f,
                width,
                height);
        }

        private void ResolveReferences()
        {
            if (targetCamera == null)
            {
                CraftLivePad1GalleryController gallery =
                    GetComponent<CraftLivePad1GalleryController>();
                targetCamera = gallery != null
                    ? gallery.TargetCamera
                    : Camera.main;
            }

            if (cameraAnchor == null)
            {
                CraftLivePadSceneRoot sceneRoot =
                    GetComponent<CraftLivePadSceneRoot>();
                cameraAnchor = sceneRoot != null
                    ? sceneRoot.CameraAnchor
                    : null;
            }

            ResolveBoxRenderer();
        }

        private void ResolveBoxRenderer()
        {
            if (boxRenderer != null)
            {
                return;
            }

            Renderer[] renderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include);
            foreach (Renderer candidate in renderers)
            {
                if (candidate != null &&
                    candidate.gameObject.name == "Box" &&
                    candidate.gameObject.scene == gameObject.scene)
                {
                    boxRenderer = candidate;
                    break;
                }
            }
        }

        private void CaptureOriginalPose()
        {
            if (capturedOriginalPose || targetCamera == null)
            {
                return;
            }

            originalCameraPosition = targetCamera.transform.position;
            originalCameraRotation = targetCamera.transform.rotation;
            if (cameraAnchor != null)
            {
                originalAnchorPosition = cameraAnchor.position;
                originalAnchorRotation = cameraAnchor.rotation;
            }

            capturedOriginalPose = true;
        }

        private float ResolveAspect()
        {
            if (targetCamera == null)
            {
                return 1f;
            }

            RenderTexture texture = targetCamera.targetTexture;
            float aspect = texture != null && texture.height > 0
                ? texture.width / (float)texture.height
                : targetCamera.aspect;
            return Mathf.Max(0.1f, aspect);
        }

        private static void GetCameraOrientedExtents(
            Bounds bounds,
            Vector3 right,
            Vector3 up,
            Vector3 forward,
            out float halfWidth,
            out float halfHeight,
            out float halfDepth)
        {
            halfWidth = 0f;
            halfHeight = 0f;
            halfDepth = 0f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 offset = Vector3.Scale(
                            bounds.extents,
                            new Vector3(x, y, z));
                        halfWidth = Mathf.Max(
                            halfWidth,
                            Mathf.Abs(Vector3.Dot(offset, right)));
                        halfHeight = Mathf.Max(
                            halfHeight,
                            Mathf.Abs(Vector3.Dot(offset, up)));
                        halfDepth = Mathf.Max(
                            halfDepth,
                            Mathf.Abs(Vector3.Dot(offset, forward)));
                    }
                }
            }
        }
    }
}
