using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePlacementSlotView :
        MonoBehaviour,
        IPointerClickHandler
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveSlotId slot;
        [SerializeField] private Transform previewAnchor;
        [SerializeField] private Renderer[] highlightRenderers =
            new Renderer[0];
        [SerializeField] private Color idleColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color availableColor = new Color(0.25f, 0.8f, 1f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float availableEmission = 1.2f;
        [SerializeField, Min(0f)] private float selectedEmission = 2.4f;
        [SerializeField] private GameObject fallbackPreviewPrefab;
        [SerializeField] private bool requireConfirmedWeapon;
        [SerializeField] private UnityEvent<bool> onAvailableChanged;
        [SerializeField] private UnityEvent<bool> onSelectedChanged;

        private GameObject previewObject;
        private string previewMaterialId;
        private bool available;
        private bool subscribed;
        private MaterialPropertyBlock highlightBlock;

        public CraftLiveSlotId Slot => slot;
        public bool Available => available;
        public string PreviewMaterialId => previewMaterialId;

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();

            ClearPreview();
        }

        public void SelectPlacement()
        {
            if (available)
            {
                session?.ChoosePlacementSlot(slot);
            }
        }

        public void Configure(
            CraftLiveSession targetSession,
            CraftLiveSlotId targetSlot,
            Transform targetPreviewAnchor,
            Renderer[] targetHighlightRenderers,
            GameObject targetFallbackPreviewPrefab,
            bool targetRequireConfirmedWeapon)
        {
            Unsubscribe();
            session = targetSession;
            slot = targetSlot;
            previewAnchor = targetPreviewAnchor;
            highlightRenderers =
                targetHighlightRenderers ?? new Renderer[0];
            fallbackPreviewPrefab = targetFallbackPreviewPrefab;
            requireConfirmedWeapon =
                targetRequireConfirmedWeapon;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void RefreshNow()
        {
            if (session != null)
            {
                Refresh(session.State);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            SelectPlacement();
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null || session == null)
            {
                return;
            }

            CraftLiveMaterialDefinition selectedMaterial =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(state.selectedMaterialId)
                    : null;
            available = IsAvailable(
                state,
                selectedMaterial,
                slot,
                requireConfirmedWeapon);
            bool selected = available &&
                            state.placement.hasCandidateSlot &&
                            state.placement.candidateSlot == slot;

            Color color = selected
                ? selectedColor
                : available
                    ? availableColor
                    : idleColor;
            float emission = selected
                ? selectedEmission
                : available
                    ? availableEmission
                    : 0f;
            ApplyHighlight(color, emission);
            onAvailableChanged?.Invoke(available);
            onSelectedChanged?.Invoke(selected);

            if (selected && selectedMaterial != null)
            {
                ShowPreview(selectedMaterial);
            }
            else
            {
                ClearPreview();
            }
        }

        private void ShowPreview(CraftLiveMaterialDefinition material)
        {
            if (previewMaterialId == material.MaterialId && previewObject != null)
            {
                return;
            }

            ClearPreview();
            if (previewAnchor == null)
            {
                return;
            }

            previewObject = new GameObject(
                $"Preview_{material.MaterialId}");
            previewObject.transform.SetParent(previewAnchor, false);
            float previewSize = 0.58f;
            CraftLivePad2Bindings padBindings =
                previewAnchor.GetComponentInParent<
                    CraftLivePad2Bindings>();
            if (padBindings != null &&
                CraftLivePad2AlignmentGuide.TryResolveLocalPose(
                    padBindings.transform,
                    CraftLivePad2AlignmentGuideKind.Material,
                    slot,
                    out CraftLivePad2GuidePose guidePose))
            {
                previewObject.transform.SetPositionAndRotation(
                    padBindings.transform.TransformPoint(
                        guidePose.LocalPosition),
                    padBindings.transform.rotation *
                    guidePose.LocalRotation);
                previewSize = Mathf.Max(
                    0.05f,
                    Mathf.Max(
                        guidePose.LocalScale.x,
                        guidePose.LocalScale.y));
            }

            GameObject contentObject = new GameObject("VisualContent");
            contentObject.transform.SetParent(
                previewObject.transform,
                false);
            Transform content = contentObject.transform;

            GameObject prefab = material.WorldPrefab != null
                ? material.WorldPrefab
                : fallbackPreviewPrefab;
            if (prefab != null)
            {
                Instantiate(prefab, content, false);
            }
            else
            {
                GameObject fallback =
                    GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fallback.transform.SetParent(content, false);
                if (fallback.TryGetComponent(out Collider previewCollider))
                {
                    Destroy(previewCollider);
                }
            }

            CraftLiveRuntimeVisualUtility.FitAndCenter(
                content,
                previewSize,
                true,
                material.Pad2PreviewRollDegrees,
                preferUpright: true,
                restAuthoredBottomOnSurface: true);
            foreach (Collider collider in
                     previewObject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            previewMaterialId = material.MaterialId;
            ApplyMaterialColor(previewObject, material.EffectColor);
        }

        private void ClearPreview()
        {
            if (previewObject != null)
            {
                Destroy(previewObject);
            }

            previewObject = null;
            previewMaterialId = string.Empty;
        }

        private void ApplyHighlight(Color color, float emission)
        {
            if (highlightRenderers == null)
            {
                return;
            }

            if (highlightBlock == null)
            {
                highlightBlock = new MaterialPropertyBlock();
            }

            foreach (Renderer targetRenderer in highlightRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(highlightBlock);
                highlightBlock.SetColor("_BaseColor", color);
                highlightBlock.SetColor("_Color", color);
                highlightBlock.SetColor(
                    "_EmissionColor",
                    color * emission);
                targetRenderer.SetPropertyBlock(highlightBlock);
            }
        }

        public static bool IsAvailable(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material,
            CraftLiveSlotId targetSlot,
            bool requiresConfirmedWeapon)
        {
            if (state == null || material == null)
            {
                return false;
            }

            bool selectionStage =
                state.placement.status ==
                CraftLivePlacementStatus.SelectingSlot ||
                state.placement.status ==
                CraftLivePlacementStatus.ConfirmingSlot;
            return selectionStage &&
                   (!requiresConfirmedWeapon ||
                    state.weaponSelectionConfirmed) &&
                   state.CanReserveSlot(targetSlot) &&
                   material.CanUseIn(targetSlot);
        }

        private void Subscribe()
        {
            if (subscribed || session == null)
            {
                return;
            }

            session.StateChanged += Refresh;
            subscribed = true;
            Refresh(session.State);
        }

        private void Unsubscribe()
        {
            if (!subscribed || session == null)
            {
                subscribed = false;
                return;
            }

            session.StateChanged -= Refresh;
            subscribed = false;
        }

        private static void ApplyMaterialColor(GameObject target, Color color)
        {
            if (target == null)
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>())
            {
                targetRenderer.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                targetRenderer.SetPropertyBlock(block);
            }
        }
    }
}
