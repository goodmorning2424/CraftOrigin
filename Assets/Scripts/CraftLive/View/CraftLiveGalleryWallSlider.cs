using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Slides the authored Pad1 wall prefabs as one horizontal carousel while
    /// aligning every wall to one shared display plane while preserving each
    /// wall's authored rotation and scale. Paintings follow automatically
    /// because they remain children of their wall prefab.
    /// </summary>
    public sealed class CraftLiveGalleryWallSlider : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CraftLiveGalleryWallView[] walls =
            new CraftLiveGalleryWallView[0];

        [Header("Horizontal Sliding")]
        [SerializeField] private bool fitSpacingToCamera = true;
        [SerializeField, Min(0.1f)] private float wallSpacing = 4f;
        [SerializeField, Min(0f)] private float spacingPadding = 0.25f;
        [SerializeField, Min(0.0001f)] private float dragSensitivity = 0.01f;
        [SerializeField, Range(0.05f, 0.95f)]
        private float changeThreshold = 0.22f;
        [SerializeField, Min(0.1f)] private float snapSpeed = 10f;

        private Transform[] slideRoots = new Transform[0];
        private Vector3 carouselAnchorPosition;
        private Vector3 slideDirection = Vector3.right;
        private float resolvedSpacing = 4f;
        private float dragOffset;
        private int selectedIndex;
        private bool dragging;
        private bool configured;

        public int SelectedIndex => selectedIndex;
        public int WallCount => slideRoots.Length;
        public float ResolvedSpacing => resolvedSpacing;
        public bool IsDragging => dragging;
        public Vector3 AnchorPosition => carouselAnchorPosition;
        public Vector3 SlideDirection => slideDirection;

        private void Start()
        {
            if (!configured)
            {
                Configure(walls, targetCamera);
            }
        }

        private void Update()
        {
            if (!configured || dragging || Mathf.Approximately(dragOffset, 0f))
            {
                return;
            }

            dragOffset = Mathf.MoveTowards(
                dragOffset,
                0f,
                snapSpeed * Time.unscaledDeltaTime);
            ApplyPositions();
        }

        public void Configure(
            IReadOnlyList<CraftLiveGalleryWallView> targetWalls,
            Camera camera)
        {
            List<CraftLiveGalleryWallView> validWalls =
                new List<CraftLiveGalleryWallView>();
            List<Transform> validRoots = new List<Transform>();
            if (targetWalls != null)
            {
                foreach (CraftLiveGalleryWallView wall in targetWalls)
                {
                    if (wall == null ||
                        wall.SlideRoot == null ||
                        validRoots.Contains(wall.SlideRoot))
                    {
                        continue;
                    }

                    validWalls.Add(wall);
                    validRoots.Add(wall.SlideRoot);
                }
            }

            targetCamera = camera != null ? camera : targetCamera;
            bool rootsChanged = !HasSameRoots(validRoots);
            walls = validWalls.ToArray();
            if (rootsChanged)
            {
                slideRoots = validRoots.ToArray();
                if (slideRoots.Length > 0)
                {
                    carouselAnchorPosition = slideRoots[0].position;
                }

                selectedIndex = Mathf.Clamp(
                    selectedIndex,
                    0,
                    Mathf.Max(0, slideRoots.Length - 1));
            }

            slideDirection = targetCamera != null
                ? targetCamera.transform.right.normalized
                : Vector3.right;
            if (slideDirection.sqrMagnitude < 0.0001f)
            {
                slideDirection = Vector3.right;
            }

            resolvedSpacing = ResolveSpacing();
            dragOffset = 0f;
            dragging = false;
            configured = slideRoots.Length > 0;
            foreach (CraftLiveGalleryWallView wall in walls)
            {
                wall.CaptureFixedHeaderPose();
                wall.ConfigureHorizontalSlider(this);
            }

            ApplyPositions();
        }

        public void BeginDrag()
        {
            if (!configured || slideRoots.Length <= 1)
            {
                return;
            }

            dragging = true;
            CraftLiveAudio.Play(CraftLiveSound.WallSlide, 0.58f);
        }

        public void Drag(float screenDeltaX)
        {
            if (!configured || slideRoots.Length <= 1)
            {
                return;
            }

            if (!dragging)
            {
                BeginDrag();
            }

            float minimumOffset = selectedIndex < slideRoots.Length - 1
                ? -resolvedSpacing
                : 0f;
            float maximumOffset = selectedIndex > 0
                ? resolvedSpacing
                : 0f;
            dragOffset = Mathf.Clamp(
                dragOffset + screenDeltaX * dragSensitivity,
                minimumOffset,
                maximumOffset);
            ApplyPositions();
        }

        public void EndDrag()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            float threshold = resolvedSpacing * changeThreshold;
            if (dragOffset <= -threshold &&
                selectedIndex < slideRoots.Length - 1)
            {
                selectedIndex++;
                dragOffset += resolvedSpacing;
            }
            else if (dragOffset >= threshold && selectedIndex > 0)
            {
                selectedIndex--;
                dragOffset -= resolvedSpacing;
            }

            ApplyPositions();
        }

        public void SetSelectedIndex(int index, bool immediate)
        {
            if (slideRoots.Length == 0)
            {
                selectedIndex = 0;
                dragOffset = 0f;
                return;
            }

            selectedIndex = Mathf.Clamp(index, 0, slideRoots.Length - 1);
            dragging = false;
            if (immediate)
            {
                dragOffset = 0f;
            }

            ApplyPositions();
        }

        public void CompleteTransitionImmediately()
        {
            dragging = false;
            dragOffset = 0f;
            ApplyPositions();
        }

        private bool HasSameRoots(IReadOnlyList<Transform> candidateRoots)
        {
            if (candidateRoots == null ||
                slideRoots.Length != candidateRoots.Count)
            {
                return false;
            }

            for (int i = 0; i < slideRoots.Length; i++)
            {
                if (slideRoots[i] != candidateRoots[i])
                {
                    return false;
                }
            }

            return true;
        }

        private float ResolveSpacing()
        {
            float spacing = Mathf.Max(0.1f, wallSpacing);
            if (!fitSpacingToCamera)
            {
                return spacing;
            }

            float largestWallWidth = 0f;
            foreach (Transform root in slideRoots)
            {
                if (root == null)
                {
                    continue;
                }

                Renderer[] renderers =
                    root.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    continue;
                }

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                largestWallWidth = Mathf.Max(
                    largestWallWidth,
                    ProjectedWidth(bounds));
            }

            spacing = Mathf.Max(
                spacing,
                largestWallWidth + spacingPadding);
            if (targetCamera != null && slideRoots.Length > 0)
            {
                float distance = Mathf.Abs(Vector3.Dot(
                    carouselAnchorPosition -
                    targetCamera.transform.position,
                    targetCamera.transform.forward));
                float viewportWidth = targetCamera.orthographic
                    ? targetCamera.orthographicSize *
                      2f * targetCamera.aspect
                    : 2f * distance *
                      Mathf.Tan(targetCamera.fieldOfView *
                                0.5f * Mathf.Deg2Rad) *
                      targetCamera.aspect;
                spacing = Mathf.Max(
                    spacing,
                    viewportWidth + spacingPadding);
            }

            return Mathf.Max(0.1f, spacing);
        }

        private float ProjectedWidth(Bounds bounds)
        {
            Vector3 direction = slideDirection.normalized;
            Vector3 extents = bounds.extents;
            return 2f * (
                Mathf.Abs(direction.x) * extents.x +
                Mathf.Abs(direction.y) * extents.y +
                Mathf.Abs(direction.z) * extents.z);
        }

        private void ApplyPositions()
        {
            if (!configured && slideRoots.Length == 0)
            {
                return;
            }

            for (int i = 0; i < slideRoots.Length; i++)
            {
                if (slideRoots[i] == null)
                {
                    continue;
                }

                float offset =
                    (i - selectedIndex) * resolvedSpacing + dragOffset;
                slideRoots[i].position =
                    carouselAnchorPosition + slideDirection * offset;
            }

            foreach (CraftLiveGalleryWallView wall in walls)
            {
                wall?.RestoreFixedHeaderPose();
            }
        }

        private void OnValidate()
        {
            wallSpacing = Mathf.Max(0.1f, wallSpacing);
            spacingPadding = Mathf.Max(0f, spacingPadding);
            dragSensitivity = Mathf.Max(0.0001f, dragSensitivity);
            snapSpeed = Mathf.Max(0.1f, snapSpeed);
        }
    }
}
