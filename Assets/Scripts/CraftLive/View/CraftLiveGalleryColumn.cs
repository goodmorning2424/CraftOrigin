using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveGalleryColumn :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        [SerializeField] private Transform contentRoot;
        [SerializeField, Min(0.1f)] private float itemSpacing = 2.35f;
        [SerializeField, Min(1)] private int visibleItemCount = 3;
        [SerializeField, Min(0.001f)] private float dragSensitivity = 0.012f;
        [SerializeField, Min(0.01f)] private float wheelStep = 0.8f;

        private readonly List<CraftLiveMaterialPaintingView> items =
            new List<CraftLiveMaterialPaintingView>();
        private float scrollOffset;
        private float minimumOffset;
        private float maximumOffset;
        private bool dragging;
        private Vector3 restingContentPosition;
        private Transform movementRoot;
        private Camera movementCamera;
        private Vector3 restingMovementWorldPosition;
        private Vector3 movementDirection = Vector3.right;
        private float movementUnitsPerItemUnit = 1f;
        private Renderer scrollBoundaryRenderer;
        private Renderer scrollWallRenderer;
        private Transform fixedHeader;
        private Vector3 fixedHeaderWorldPosition;
        private Quaternion fixedHeaderWorldRotation;
        private float scrollBoundaryInsetNormalized;
        private float viewportLeft;
        private float viewportRight;

        public float ScrollOffset => scrollOffset;
        public float MinimumOffset => minimumOffset;
        public float MaximumOffset => maximumOffset;
        public bool IsDragging => dragging;
        public int ItemCount => items.Count;

        public void Configure(
            Transform targetContentRoot,
            IReadOnlyList<CraftLiveMaterialPaintingView> targetItems,
            float spacing,
            int visibleCount,
            float sensitivity,
            float targetWheelStep)
        {
            contentRoot = targetContentRoot;
            itemSpacing = Mathf.Max(0.1f, spacing);
            visibleItemCount = Mathf.Max(1, visibleCount);
            dragSensitivity = Mathf.Max(0.001f, sensitivity);
            wheelStep = Mathf.Max(0.01f, targetWheelStep);

            if (contentRoot != null)
            {
                restingContentPosition = contentRoot.localPosition;
                if (movementRoot == null)
                {
                    restingContentPosition.x += scrollOffset;
                }
            }

            UpdateMovementBasis();

            items.Clear();
            if (targetItems != null)
            {
                foreach (CraftLiveMaterialPaintingView item in targetItems)
                {
                    if (item != null)
                    {
                        items.Add(item);
                    }
                }
            }

            CaptureAuthoredItemLayout();
            SetScrollOffset(0f);
        }

        public void SetScrollOffset(float value)
        {
            scrollOffset = Mathf.Clamp(
                value,
                minimumOffset,
                maximumOffset);
            if (movementRoot != null)
            {
                movementRoot.position =
                    restingMovementWorldPosition +
                    movementDirection *
                    (scrollOffset * movementUnitsPerItemUnit);

                RestoreFixedHeaderPose();

                if (contentRoot != null)
                {
                    contentRoot.localPosition = restingContentPosition;
                }
            }
            else if (contentRoot != null)
            {
                Vector3 position = contentRoot.localPosition;
                position.x = restingContentPosition.x + scrollOffset;
                position.y = restingContentPosition.y;
                contentRoot.localPosition = position;
            }

            RefreshVisibility();
        }

        public void SetMovementRoot(
            Transform targetMovementRoot,
            Camera targetCamera)
        {
            if (movementRoot != null)
            {
                movementRoot.position = restingMovementWorldPosition;
            }

            movementRoot = targetMovementRoot;
            movementCamera = targetCamera;
            if (movementRoot != null)
            {
                restingMovementWorldPosition = movementRoot.position;
            }

            UpdateMovementBasis();
        }

        public void SetRendererScrollBounds(
            Renderer boundaryRenderer,
            Renderer wallRenderer,
            float horizontalInsetNormalized)
        {
            scrollBoundaryRenderer = boundaryRenderer;
            scrollWallRenderer = wallRenderer;
            scrollBoundaryInsetNormalized = Mathf.Clamp(
                horizontalInsetNormalized,
                0f,
                0.45f);
        }

        public void SetNormalizedPosition(float value)
        {
            SetScrollOffset(Mathf.Lerp(
                minimumOffset,
                maximumOffset,
                Mathf.Clamp01(value)));
        }

        public void SetHorizontalSlider(
            CraftLiveGalleryWallSlider slider)
        {
        }

        public void SetFixedHeader(Transform targetHeader)
        {
            fixedHeader = targetHeader;
            if (fixedHeader == null)
            {
                return;
            }

            fixedHeaderWorldPosition = fixedHeader.position;
            fixedHeaderWorldRotation = fixedHeader.rotation;
            RestoreFixedHeaderPose();
        }

        private void RestoreFixedHeaderPose()
        {
            if (fixedHeader != null)
            {
                fixedHeader.SetPositionAndRotation(
                    fixedHeaderWorldPosition,
                    fixedHeaderWorldRotation);
            }
        }

        public void SetViewport(float top, float bottom)
        {
            RefreshVisibility();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            SetScrollOffset(
                scrollOffset - eventData.delta.x * dragSensitivity);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            SetScrollOffset(
                scrollOffset - eventData.scrollDelta.y * wheelStep);
        }

        private void RefreshVisibility()
        {
            foreach (CraftLiveMaterialPaintingView item in items)
            {
                if (item == null)
                {
                    continue;
                }

                item.SetViewportVisible(true);
            }
        }

        private void CaptureAuthoredItemLayout()
        {
            if (items.Count == 0)
            {
                viewportLeft = 0f;
                viewportRight = 0f;
                minimumOffset = 0f;
                maximumOffset = 0f;
                return;
            }

            int visible = Mathf.Min(visibleItemCount, items.Count);
            viewportLeft = float.PositiveInfinity;
            viewportRight = float.NegativeInfinity;
            for (int i = 0; i < items.Count; i++)
            {
                items[i].CaptureRestingTransform();
                if (i < visible)
                {
                    float x = items[i].transform.localPosition.x;
                    viewportLeft = Mathf.Min(viewportLeft, x);
                    viewportRight = Mathf.Max(viewportRight, x);
                }
            }

            float visibleCenterLeft = viewportLeft;
            float visibleCenterRight = viewportRight;
            viewportLeft -= itemSpacing * 0.5f;
            viewportRight += itemSpacing * 0.5f;

            float allLeft = float.PositiveInfinity;
            float allRight = float.NegativeInfinity;
            foreach (CraftLiveMaterialPaintingView item in items)
            {
                float x = item.transform.localPosition.x;
                allLeft = Mathf.Min(allLeft, x);
                allRight = Mathf.Max(allRight, x);
            }

            float forwardMinimum = Mathf.Min(
                0f,
                allLeft - visibleCenterLeft);
            float forwardMaximum = Mathf.Max(
                0f,
                allRight - visibleCenterRight);
            minimumOffset = -forwardMaximum;
            maximumOffset = -forwardMinimum;
            TryApplyRendererScrollRange();
        }

        private void TryApplyRendererScrollRange()
        {
            if (movementCamera == null ||
                scrollBoundaryRenderer == null ||
                scrollWallRenderer == null ||
                movementUnitsPerItemUnit <= Mathf.Epsilon ||
                !TryGetScreenHorizontalBounds(
                    scrollBoundaryRenderer,
                    movementCamera,
                    out float boundaryLeft,
                    out float boundaryRight) ||
                !TryGetScreenHorizontalBounds(
                    scrollWallRenderer,
                    movementCamera,
                    out float wallLeft,
                    out float wallRight))
            {
                return;
            }

            float boundaryWidth = boundaryRight - boundaryLeft;
            if (boundaryWidth <= Mathf.Epsilon)
            {
                return;
            }

            float inset = boundaryWidth * scrollBoundaryInsetNormalized;
            boundaryLeft += inset;
            boundaryRight -= inset;
            if (boundaryRight <= boundaryLeft)
            {
                return;
            }

            Vector3 reference = scrollWallRenderer.bounds.center;
            float referenceScreenX =
                movementCamera.WorldToScreenPoint(reference).x;
            float movedScreenX = movementCamera.WorldToScreenPoint(
                reference + movementDirection).x;
            float pixelsPerWorldUnit = movedScreenX - referenceScreenX;
            if (Mathf.Abs(pixelsPerWorldUnit) <= Mathf.Epsilon)
            {
                return;
            }

            float alignLeftOffset =
                (boundaryLeft - wallLeft) /
                pixelsPerWorldUnit /
                movementUnitsPerItemUnit;
            float alignRightOffset =
                (boundaryRight - wallRight) /
                pixelsPerWorldUnit /
                movementUnitsPerItemUnit;
            minimumOffset = Mathf.Min(
                alignLeftOffset,
                alignRightOffset);
            maximumOffset = Mathf.Max(
                alignLeftOffset,
                alignRightOffset);
        }

        private static bool TryGetScreenHorizontalBounds(
            Renderer targetRenderer,
            Camera targetCamera,
            out float left,
            out float right)
        {
            left = float.PositiveInfinity;
            right = float.NegativeInfinity;
            Bounds bounds = targetRenderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            bool foundPointInFront = false;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        Vector3 screen =
                            targetCamera.WorldToScreenPoint(corner);
                        if (screen.z <= 0f)
                        {
                            continue;
                        }

                        foundPointInFront = true;
                        left = Mathf.Min(left, screen.x);
                        right = Mathf.Max(right, screen.x);
                    }
                }
            }

            return foundPointInFront && right > left;
        }

        private void UpdateMovementBasis()
        {
            movementDirection = movementCamera != null
                ? movementCamera.transform.right.normalized
                : Vector3.right;
            movementUnitsPerItemUnit = 1f;

            if (contentRoot == null)
            {
                return;
            }

            Vector3 itemAxis = contentRoot.TransformVector(Vector3.right);
            float itemAxisLength = itemAxis.magnitude;
            if (itemAxisLength <= Mathf.Epsilon)
            {
                return;
            }

            movementUnitsPerItemUnit = itemAxisLength;
            if (movementCamera != null &&
                Vector3.Dot(itemAxis, movementDirection) < 0f)
            {
                movementDirection = -movementDirection;
            }
        }

        private void OnValidate()
        {
            itemSpacing = Mathf.Max(0.1f, itemSpacing);
            visibleItemCount = Mathf.Max(1, visibleItemCount);
            dragSensitivity = Mathf.Max(0.001f, dragSensitivity);
            wheelStep = Mathf.Max(0.01f, wheelStep);
        }
    }

    /// <summary>
    /// Keeps the authored wall collider usable as a wide drag surface. If the
    /// wall collider is in front of a frame, clicks are forwarded to the frame
    /// found by a full physics raycast without changing scene placement.
    /// </summary>
    public sealed class CraftLiveGalleryInputSurface :
        MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        private CraftLiveGalleryColumn column;
        private Transform wallRoot;

        public void Configure(
            CraftLiveGalleryColumn targetColumn,
            Transform targetWallRoot)
        {
            column = targetColumn;
            wallRoot = targetWallRoot;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Camera eventCamera = eventData.pressEventCamera != null
                ? eventData.pressEventCamera
                : Camera.main;
            if (eventCamera == null)
            {
                return;
            }

            Ray ray = eventCamera.ScreenPointToRay(eventData.position);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                eventCamera.farClipPlane,
                eventCamera.cullingMask,
                QueryTriggerInteraction.Collide);
            System.Array.Sort(
                hits,
                (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                CraftLiveMaterialPaintingView painting =
                    hit.collider.GetComponentInParent<
                        CraftLiveMaterialPaintingView>();
                if (painting == null ||
                    wallRoot == null ||
                    (painting.transform != wallRoot &&
                     !painting.transform.IsChildOf(wallRoot)))
                {
                    continue;
                }

                painting.Select();
                return;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            column?.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            column?.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            column?.OnEndDrag(eventData);
        }

        public void OnScroll(PointerEventData eventData)
        {
            column?.OnScroll(eventData);
        }
    }
}
