using System.Collections.Generic;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Scene-authored Pad1 gallery layout. The wall, its frame slots, and the
    /// scrolling content stay in one hierarchy, so moving the wall also moves
    /// every painting attached to it.
    /// </summary>
    public sealed class CraftLiveGalleryWallView : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private CraftLiveMaterialCategory category;

        [Header("Preplaced Layout")]
        [SerializeField] private Transform slideRoot;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private CraftLiveGalleryColumn column;
        [SerializeField] private CraftLiveMaterialPaintingView[] frameSlots =
            new CraftLiveMaterialPaintingView[0];

        [Header("Optional Presentation")]
        [SerializeField] private TextMesh headerText;
        [SerializeField] private GameObject emptyStateRoot;
        [SerializeField] private TextMesh emptyStateText;

        [Header("Scrolling")]
        [SerializeField, Min(0.1f)] private float itemSpacing = 2.35f;
        [SerializeField] private float viewportTop = 3.25f;
        [SerializeField] private float viewportBottom = -3.25f;
        [SerializeField, Min(0.01f)]
        private float dragSensitivityMultiplier = 0.25f;
        [SerializeField, Min(0.01f)]
        private float mouseWheelSensitivityMultiplier = 0.25f;
        [SerializeField] private Renderer scrollBoundaryRenderer;
        [SerializeField, Range(0f, 0.45f)]
        private float boundaryHorizontalInset = 0.045f;

        private readonly List<CraftLiveMaterialPaintingView> validSlots =
            new List<CraftLiveMaterialPaintingView>();
        private Vector3 fixedHeaderWorldPosition;
        private Quaternion fixedHeaderWorldRotation;
        private bool hasFixedHeaderPose;

        public CraftLiveMaterialCategory Category => category;
        public Transform SlideRoot => slideRoot != null ? slideRoot : transform;
        public Transform ContentRoot => contentRoot;
        public int SlotCapacity
        {
            get
            {
                CollectValidSlots();
                return validSlots.Count;
            }
        }

        public bool HasUsableLayout
        {
            get
            {
                CollectValidSlots();
                return contentRoot != null && validSlots.Count > 0;
            }
        }

        public void ConfigureHorizontalSlider(
            CraftLiveGalleryWallSlider slider)
        {
            EnsureColumn();
            column?.SetHorizontalSlider(slider);
        }

        public void CaptureFixedHeaderPose()
        {
            if (headerText == null)
            {
                hasFixedHeaderPose = false;
                return;
            }

            Transform headerTransform = headerText.transform;
            fixedHeaderWorldPosition = headerTransform.position;
            fixedHeaderWorldRotation = headerTransform.rotation;
            hasFixedHeaderPose = true;
        }

        public void RestoreFixedHeaderPose()
        {
            if (!hasFixedHeaderPose || headerText == null)
            {
                return;
            }

            headerText.transform.SetPositionAndRotation(
                fixedHeaderWorldPosition,
                fixedHeaderWorldRotation);
        }

        public bool TryBind(
            CraftLivePad1GalleryController owner,
            CraftLiveMaterialCategory expectedCategory,
            IReadOnlyList<CraftLiveMaterialDefinition> materials,
            CraftLiveRoomState state,
            CraftLiveSession session,
            string header,
            int visibleCount,
            float dragSensitivity,
            float wheelStep,
            List<CraftLiveMaterialPaintingView> boundPaintings)
        {
            CollectValidSlots();
            int materialCount = materials != null ? materials.Count : 0;
            if (contentRoot == null ||
                validSlots.Count == 0 ||
                category != expectedCategory ||
                materialCount > validSlots.Count)
            {
                return false;
            }

            EnsureColumn();
            if (column == null)
            {
                return false;
            }

            column.SetMovementRoot(
                SlideRoot,
                owner != null ? owner.TargetCamera : Camera.main);
            if (headerText != null && owner != null)
            {
                // The heading belongs to the screen, not to the scrolling
                // authored wall. Detach it while preserving its scene pose so
                // neither item scrolling nor wall sliding can move it.
                headerText.transform.SetParent(
                    owner.transform,
                    true);
            }
            column.SetFixedHeader(
                headerText != null ? headerText.transform : null);
            column.SetRendererScrollBounds(
                ResolveScrollBoundaryRenderer(),
                ResolveWallRenderer(),
                boundaryHorizontalInset);

            if (headerText != null)
            {
                headerText.text = header ?? string.Empty;
            }

            if (emptyStateRoot != null)
            {
                emptyStateRoot.SetActive(false);
            }

            if (emptyStateText != null)
            {
                emptyStateText.text = string.Empty;
            }

            List<CraftLiveMaterialPaintingView> activeSlots =
                new List<CraftLiveMaterialPaintingView>(materialCount);
            List<CraftLiveMaterialPaintingView> layoutSlots =
                new List<CraftLiveMaterialPaintingView>(validSlots.Count);
            for (int i = 0; i < validSlots.Count; i++)
            {
                CraftLiveMaterialPaintingView slot = validSlots[i];
                slot.Unbind();
                slot.gameObject.SetActive(true);
                slot.CaptureRestingTransform();
                layoutSlots.Add(slot);
                if (i >= materialCount)
                {
                    slot.SetViewportVisible(true);
                    continue;
                }

                slot.Bind(owner, materials[i]);
                slot.Refresh(state, session);
                activeSlots.Add(slot);
                boundPaintings?.Add(slot);
            }

            column.Configure(
                contentRoot,
                layoutSlots,
                ResolveItemSpacing(layoutSlots),
                Mathf.Max(1, visibleCount),
                dragSensitivity * dragSensitivityMultiplier,
                wheelStep * mouseWheelSensitivityMultiplier);
            column.SetViewport(viewportTop, viewportBottom);
            return true;
        }

        public void ClearBindings()
        {
            CollectValidSlots();
            foreach (CraftLiveMaterialPaintingView slot in validSlots)
            {
                slot?.Unbind();
            }

            if (emptyStateRoot != null)
            {
                emptyStateRoot.SetActive(false);
            }
        }

        private void EnsureColumn()
        {
            if (column == null)
            {
                column = GetComponent<CraftLiveGalleryColumn>();
            }
        }

        private void CollectValidSlots()
        {
            validSlots.Clear();
            if (frameSlots == null)
            {
                return;
            }

            foreach (CraftLiveMaterialPaintingView slot in frameSlots)
            {
                if (slot == null ||
                    contentRoot == null ||
                    (slot.transform != contentRoot &&
                     !slot.transform.IsChildOf(contentRoot)) ||
                    validSlots.Contains(slot))
                {
                    continue;
                }

                validSlots.Add(slot);
            }
        }

        private float ResolveItemSpacing(
            IReadOnlyList<CraftLiveMaterialPaintingView> slots)
        {
            if (slots != null && slots.Count >= 2)
            {
                float measuredX = Mathf.Abs(
                    slots[0].transform.localPosition.x -
                    slots[1].transform.localPosition.x);
                if (measuredX > 0.0001f)
                {
                    return measuredX;
                }

                float measured = Mathf.Abs(
                    slots[0].transform.localPosition.y -
                    slots[1].transform.localPosition.y);
                if (measured > 0.0001f)
                {
                    return measured;
                }
            }

            return Mathf.Max(0.1f, itemSpacing);
        }

        private Renderer ResolveScrollBoundaryRenderer()
        {
            if (scrollBoundaryRenderer != null)
            {
                return scrollBoundaryRenderer;
            }

            Renderer[] renderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include);
            foreach (Renderer candidate in renderers)
            {
                if (candidate != null && candidate.gameObject.name == "Box")
                {
                    scrollBoundaryRenderer = candidate;
                    return scrollBoundaryRenderer;
                }
            }

            return null;
        }

        private Renderer ResolveWallRenderer()
        {
            Transform root = SlideRoot;
            Renderer renderer = root != null
                ? root.GetComponent<Renderer>()
                : null;
            return renderer != null
                ? renderer
                : root?.GetComponentInChildren<Renderer>(true);
        }

        private void Reset()
        {
            slideRoot = transform;
            EnsureColumn();
        }

        private void OnValidate()
        {
            itemSpacing = Mathf.Max(0.1f, itemSpacing);
            dragSensitivityMultiplier = Mathf.Max(
                0.01f,
                dragSensitivityMultiplier);
            mouseWheelSensitivityMultiplier = Mathf.Max(
                0.01f,
                mouseWheelSensitivityMultiplier);
            boundaryHorizontalInset = Mathf.Clamp(
                boundaryHorizontalInset,
                0f,
                0.45f);
            if (viewportBottom > viewportTop)
            {
                float swap = viewportBottom;
                viewportBottom = viewportTop;
                viewportTop = swap;
            }

            EnsureColumn();
        }
    }
}
