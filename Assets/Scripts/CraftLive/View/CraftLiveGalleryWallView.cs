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
        private bool hasAppliedHeaderDepthOffset;
        private GameObject generatedHeaderNameplate;

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

        public void SetHeaderVisible(bool visible)
        {
            if (headerText == null)
            {
                return;
            }

            Renderer renderer = headerText.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = visible;
            }

            if (generatedHeaderNameplate != null)
            {
                generatedHeaderNameplate.SetActive(visible);
            }
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
                Camera targetCamera =
                    owner != null ? owner.TargetCamera : Camera.main;
                MoveHeaderInFrontOfWall(targetCamera);
                EnsureHeaderNameplate(targetCamera);
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

        private void EnsureHeaderNameplate(Camera targetCamera)
        {
            // All three gallery tiers use the same independent nameplate.
            if (headerText == null)
            {
                if (generatedHeaderNameplate != null)
                {
                    generatedHeaderNameplate.SetActive(false);
                }
                return;
            }

            Renderer textRenderer = headerText.GetComponent<Renderer>();
            if (textRenderer == null)
            {
                return;
            }

            Bounds textBounds = textRenderer.bounds;
            float width = Mathf.Max(
                textBounds.size.x * 1.16f,
                textBounds.size.y * 2.8f);
            float height = Mathf.Max(
                textBounds.size.y * 1.5f,
                0.08f);
            width = Mathf.Min(
                width,
                ResolveMaximumNameplateWidth(
                    targetCamera,
                    textBounds.center,
                    width));

            if (generatedHeaderNameplate == null)
            {
                generatedHeaderNameplate = new GameObject(
                    "Generated_WoodHeaderNameplate");
                generatedHeaderNameplate.AddComponent<
                    CraftLiveGeneratedRuntimeVisual>();
            }

            Transform root = generatedHeaderNameplate.transform;
            // Do not inherit the large authored TextMesh scale. Build the
            // plate as a sibling with a world scale of one, centered on the
            // renderer's actual world bounds. This keeps all three tiers the
            // same visual construction without oversized geometry.
            root.SetParent(null, true);
            root.localScale = Vector3.one;
            root.rotation = headerText.transform.rotation;
            Vector3 towardCamera = ResolveTowardCamera(
                targetCamera,
                textBounds.center);
            float textClearance = ResolveDepthClearance(
                targetCamera,
                textBounds.center,
                0.0012f,
                0.035f,
                0.22f);
            root.position =
                textBounds.center - towardCamera * textClearance;
            root.SetParent(headerText.transform.parent, true);
            ClearNameplateParts(root);

            Color wood = new Color(0.43f, 0.20f, 0.075f);
            Color innerWood = new Color(0.32f, 0.12f, 0.035f);
            Color darkWood = new Color(0.12f, 0.04f, 0.012f);
            Color brass = CraftLiveForgeUITheme.Brass;
            CreateNameplatePart(
                root,
                "WoodShadow",
                new Vector3(width * 0.012f, -height * 0.08f, -0.025f),
                new Vector3(width, height, 0.07f),
                darkWood);
            CreateNameplatePart(
                root,
                "WoodFace",
                new Vector3(0f, 0f, 0f),
                new Vector3(width * 0.96f, height * 0.9f, 0.055f),
                wood);
            CreateNameplatePart(
                root,
                "InsetWoodFace",
                new Vector3(0f, 0f, 0.035f),
                new Vector3(width * 0.86f, height * 0.66f, 0.018f),
                innerWood);
            float railWidth = width * 0.82f;
            CreateNameplatePart(
                root,
                "TopBrassInlay",
                new Vector3(0f, height * 0.31f, 0.052f),
                new Vector3(railWidth, height * 0.055f, 0.012f),
                brass);
            CreateNameplatePart(
                root,
                "BottomBrassInlay",
                new Vector3(0f, -height * 0.31f, 0.052f),
                new Vector3(railWidth, height * 0.055f, 0.012f),
                brass);
            CreateNameplatePart(
                root,
                "LeftBrassCap",
                new Vector3(-width * 0.42f, 0f, 0.052f),
                new Vector3(width * 0.018f, height * 0.56f, 0.012f),
                brass);
            CreateNameplatePart(
                root,
                "RightBrassCap",
                new Vector3(width * 0.42f, 0f, 0.052f),
                new Vector3(width * 0.018f, height * 0.56f, 0.012f),
                brass);
            headerText.color = Color.white;
            generatedHeaderNameplate.SetActive(true);
        }

        private void MoveHeaderInFrontOfWall(Camera targetCamera)
        {
            if (headerText == null ||
                targetCamera == null ||
                hasAppliedHeaderDepthOffset)
            {
                return;
            }

            // The authored labels share almost the same depth as the shelf.
            // Move the complete text/nameplate pair toward the camera once so
            // the plate cannot z-fight with or intersect the wooden shelf.
            Vector3 towardCamera = ResolveTowardCamera(
                targetCamera,
                headerText.transform.position);
            if (towardCamera.sqrMagnitude > 0.0001f)
            {
                float forwardDistance = ResolveDepthClearance(
                    targetCamera,
                    headerText.transform.position,
                    0.012f,
                    0.3f,
                    1.8f);
                headerText.transform.position +=
                    towardCamera * forwardDistance;
                hasAppliedHeaderDepthOffset = true;
            }
        }

        private static Vector3 ResolveTowardCamera(
            Camera targetCamera,
            Vector3 position)
        {
            if (targetCamera == null)
            {
                return Vector3.zero;
            }

            Vector3 direction =
                targetCamera.transform.position - position;
            return direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : -targetCamera.transform.forward;
        }

        private static float ResolveDepthClearance(
            Camera targetCamera,
            Vector3 position,
            float distanceRatio,
            float minimum,
            float maximum)
        {
            float distance = targetCamera != null
                ? Vector3.Distance(targetCamera.transform.position, position)
                : minimum;
            return Mathf.Clamp(
                distance * distanceRatio,
                minimum,
                maximum);
        }

        private static float ResolveMaximumNameplateWidth(
            Camera targetCamera,
            Vector3 position,
            float fallback)
        {
            if (targetCamera == null)
            {
                return fallback;
            }

            float depth = targetCamera.WorldToScreenPoint(position).z;
            if (depth <= 0f)
            {
                return fallback;
            }

            Vector3 left = targetCamera.ViewportToWorldPoint(
                new Vector3(0.06f, 0.5f, depth));
            Vector3 right = targetCamera.ViewportToWorldPoint(
                new Vector3(0.94f, 0.5f, depth));
            return Vector3.Distance(left, right);
        }

        private static void ClearNameplateParts(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private static void CreateNameplatePart(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }
            CraftLiveForgeUITheme.ApplyForgeSurface(
                part.GetComponent<Renderer>(),
                color,
                0.01f,
                0.18f,
                0.25f);
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
