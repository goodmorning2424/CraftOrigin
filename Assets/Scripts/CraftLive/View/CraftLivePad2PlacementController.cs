using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad2PlacementController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad2Bindings bindings;
        [SerializeField] private GameObject fallbackMaterialPreviewPrefab;

        [Header("Automatic Slot Setup")]
        [SerializeField] private bool createFallbackSlots = true;
        [SerializeField]
        [Tooltip("Enable only when generated fallback anchors should replace the hierarchy-authored Pad2 slot positions.")]
        private bool applyReferenceLayout;
        [SerializeField, Min(0.4f)] private float slotDiameter = 0.86f;
        [SerializeField] private Color baseSlotColor =
            new Color(0.24f, 0.27f, 0.3f, 1f);
        [SerializeField] private Color skillSlotColor =
            new Color(0.5f, 0.28f, 0.62f, 1f);
        [SerializeField] private Color attributeSlotColor =
            new Color(0.65f, 0.28f, 0.18f, 1f);

        [Header("Fallback Confirmation Controls")]
        [SerializeField] private bool createFallbackControls = true;

        [Header("UI Events")]
        [SerializeField] private UnityEvent<string> onInstructionChanged;
        [SerializeField] private UnityEvent<bool> onConfirmVisible;
        [SerializeField] private UnityEvent<bool> onChangeVisible;
        [SerializeField] private UnityEvent<bool> onCancelVisible;
        [SerializeField] private UnityEvent<CraftLiveSlotId>
            onCandidateSlotChanged;

        private readonly List<CraftLivePlacementSlotView> slotViews =
            new List<CraftLivePlacementSlotView>();
        private GameObject generatedControls;
        private GameObject confirmButton;
        private GameObject changeButton;
        private GameObject cancelButton;
        private TextMesh instructionText;

        public IReadOnlyList<CraftLivePlacementSlotView> SlotViews =>
            slotViews;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (session != null)
            {
                session.StateChanged -= Refresh;
                session.StateChanged += Refresh;
            }
        }

        private void Start()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }
        }

        public void Rebuild()
        {
            ResolveReferences();
            if (session == null || bindings == null)
            {
                return;
            }

            slotViews.Clear();
            foreach (CraftLivePad2SlotSpec spec in
                     CraftLivePad2SlotLayout.All)
            {
                Transform anchor = GetAnchor(spec.PhysicalSlot);
                if (anchor == null)
                {
                    continue;
                }

                if (applyReferenceLayout)
                {
                    anchor.localPosition = spec.DefaultPosition;
                }

                RemoveGeneratedSlot(anchor);
                CraftLivePlacementSlotView existing =
                    FindCustomSlotView(anchor);
                if (existing != null)
                {
                    slotViews.Add(existing);
                    existing.RefreshNow();
                    continue;
                }

                if (createFallbackSlots)
                {
                    slotViews.Add(
                        CreateFallbackSlot(anchor, spec));
                }
            }

            BuildFallbackControls();
            Refresh(session.State);
        }

        public void ConfirmCandidate()
        {
            if (!CanConfirmPlacement(session != null
                    ? session.State
                    : null))
            {
                return;
            }

            session.ConfirmPlacement();
        }

        public void ChangeCandidate()
        {
            CraftLiveRoomState state =
                session != null ? session.State : null;
            if (state == null ||
                state.placement.status !=
                CraftLivePlacementStatus.ConfirmingSlot)
            {
                return;
            }

            session.ClearPlacementChoice();
        }

        public void CancelPlacement()
        {
            CraftLiveRoomState state =
                session != null ? session.State : null;
            if (state == null)
            {
                return;
            }

            if (state.placement.status ==
                    CraftLivePlacementStatus.SelectingSlot ||
                state.placement.status ==
                    CraftLivePlacementStatus.ConfirmingSlot)
            {
                session.CancelPlacement();
            }
        }

        public static bool CanConfirmPlacement(
            CraftLiveRoomState state)
        {
            return state != null &&
                   state.weaponSelectionConfirmed &&
                   state.placement.status ==
                   CraftLivePlacementStatus.ConfirmingSlot &&
                   state.placement.hasCandidateSlot;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Select First Base Material")]
        private void DebugSelectFirstBaseMaterial()
        {
            DebugSelectFirstMaterial(
                CraftLiveMaterialCategory.Upgrade);
        }

        [ContextMenu("Debug/Select First Skill Material")]
        private void DebugSelectFirstSkillMaterial()
        {
            DebugSelectFirstMaterial(
                CraftLiveMaterialCategory.Skill);
        }

        [ContextMenu("Debug/Select First Attribute Material")]
        private void DebugSelectFirstAttributeMaterial()
        {
            DebugSelectFirstMaterial(
                CraftLiveMaterialCategory.Attribute);
        }

        private void DebugSelectFirstMaterial(
            CraftLiveMaterialCategory category)
        {
            ResolveReferences();
            if (!Application.isPlaying ||
                session == null ||
                session.Catalog == null)
            {
                Debug.LogWarning(
                    "Craft-live: start Play Mode before using " +
                    "the Pad2 material debug command.",
                    this);
                return;
            }

            foreach (CraftLiveMaterialDefinition material in
                     session.Catalog.Materials)
            {
                if (material == null ||
                    material.Category != category)
                {
                    continue;
                }

                if (!session.IsMaterialUnlocked(material))
                {
                    session.UnlockMaterialId(material.MaterialId);
                }

                session.SelectMaterial(material);
                return;
            }

            Debug.LogWarning(
                $"Craft-live: no {category} material exists.",
                this);
        }
#endif

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (bindings == null)
            {
                bindings = GetComponent<CraftLivePad2Bindings>();
            }
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null || session == null)
            {
                return;
            }

            bool selecting =
                state.placement.status ==
                CraftLivePlacementStatus.SelectingSlot;
            bool confirming =
                state.placement.status ==
                CraftLivePlacementStatus.ConfirmingSlot;
            bool showPlacementSlots =
                state.weaponSelectionConfirmed &&
                (selecting || confirming);
            foreach (CraftLivePlacementSlotView slotView in slotViews)
            {
                if (slotView == null)
                {
                    continue;
                }

                if (slotView.gameObject.activeSelf != showPlacementSlots)
                {
                    slotView.gameObject.SetActive(showPlacementSlots);
                }
                else if (showPlacementSlots)
                {
                    slotView.RefreshNow();
                }
            }

            bool canConfirm = CanConfirmPlacement(state);
            bool showChange =
                confirming && state.weaponSelectionConfirmed;
            bool showCancel = selecting || confirming;

            SetActive(confirmButton, canConfirm);
            SetActive(changeButton, showChange);
            SetActive(cancelButton, showCancel);
            onConfirmVisible?.Invoke(canConfirm);
            onChangeVisible?.Invoke(showChange);
            onCancelVisible?.Invoke(showCancel);
            if (state.placement.hasCandidateSlot)
            {
                onCandidateSlotChanged?.Invoke(
                    state.placement.candidateSlot);
            }

            string instruction = ResolveInstruction(state);
            if (instructionText != null)
            {
                instructionText.text = instruction;
            }

            onInstructionChanged?.Invoke(instruction);
        }

        private string ResolveInstruction(
            CraftLiveRoomState state)
        {
            bool selectionStage =
                state.placement.status ==
                    CraftLivePlacementStatus.SelectingSlot ||
                state.placement.status ==
                    CraftLivePlacementStatus.ConfirmingSlot;
            if (!state.weaponSelectionConfirmed)
            {
                return selectionStage
                    ? "先に武器を確定"
                    : "武器を選んで確定";
            }

            return session.GetInstruction(
                CraftLiveRole.WorkbenchPad);
        }

        private CraftLivePlacementSlotView CreateFallbackSlot(
            Transform anchor,
            CraftLivePad2SlotSpec spec)
        {
            GameObject root = new GameObject(
                $"Generated_{spec.PhysicalSlot}_{spec.SlotId}");
            root.transform.SetParent(anchor, false);
            root.AddComponent<CraftLiveGeneratedRuntimeVisual>();

            GameObject disc = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            disc.name = "HighlightDisc";
            disc.transform.SetParent(root.transform, false);
            disc.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            disc.transform.localScale =
                new Vector3(
                    slotDiameter * 0.5f,
                    0.06f,
                    slotDiameter * 0.5f);
            Renderer renderer = disc.GetComponent<Renderer>();
            ApplyColor(renderer, GetSlotColor(spec.SlotId));
            Collider discCollider = disc.GetComponent<Collider>();
            DestroySafely(discCollider);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size =
                new Vector3(slotDiameter, slotDiameter, 0.35f);

            GameObject previewObject =
                new GameObject("PreviewAnchor");
            previewObject.transform.SetParent(root.transform, false);
            previewObject.transform.localPosition =
                new Vector3(0f, 0f, -0.45f);

            CreateText(
                root.transform,
                "SlotLabel",
                spec.Label,
                new Vector3(0f, 0f, -0.22f),
                0.04f);

            CraftLivePlacementSlotView view =
                root.AddComponent<CraftLivePlacementSlotView>();
            view.Configure(
                session,
                spec.SlotId,
                previewObject.transform,
                new[] { renderer },
                fallbackMaterialPreviewPrefab,
                true);
            return view;
        }

        private void BuildFallbackControls()
        {
            DestroySafely(generatedControls);
            generatedControls = null;
            confirmButton = null;
            changeButton = null;
            cancelButton = null;
            instructionText = null;

            if (!createFallbackControls ||
                bindings == null ||
                bindings.UiRoot == null)
            {
                return;
            }

            generatedControls = new GameObject(
                "Generated_PlacementControls");
            generatedControls.transform.SetParent(
                bindings.UiRoot,
                false);
            generatedControls.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();

            instructionText = CreateText(
                generatedControls.transform,
                "Instruction",
                string.Empty,
                new Vector3(0f, 3.45f, -0.8f),
                0.055f);
            confirmButton = CreateButton(
                generatedControls.transform,
                "ConfirmPlacementButton",
                "この場所に置く",
                new Vector3(0f, -2.68f, -0.8f),
                new Color(0.2f, 0.72f, 0.4f),
                ConfirmCandidate);
            changeButton = CreateButton(
                generatedControls.transform,
                "ChangePlacementButton",
                "選び直す",
                new Vector3(-1.52f, -2.68f, -0.8f),
                new Color(0.68f, 0.55f, 0.2f),
                ChangeCandidate);
            cancelButton = CreateButton(
                generatedControls.transform,
                "CancelPlacementButton",
                "キャンセル",
                new Vector3(1.52f, -2.68f, -0.8f),
                new Color(0.68f, 0.24f, 0.22f),
                CancelPlacement);
        }

        private GameObject CreateButton(
            Transform parent,
            string name,
            string label,
            Vector3 position,
            Color color,
            UnityAction action)
        {
            GameObject button = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent, false);
            button.transform.localPosition = position;
            button.transform.localScale =
                new Vector3(1.45f, 0.56f, 0.22f);
            Renderer renderer = button.GetComponent<Renderer>();
            ApplyColor(renderer, color);
            CraftLiveWorldButton worldButton =
                button.AddComponent<CraftLiveWorldButton>();
            worldButton.Configure(
                button.transform,
                new[] { renderer },
                color,
                Color.Lerp(color, Color.white, 0.3f),
                Color.Lerp(color, Color.white, 0.55f));
            worldButton.AddListener(action);
            CreateText(
                button.transform,
                "Label",
                label,
                new Vector3(0f, 0f, -0.62f),
                0.038f);
            return button;
        }

        private Transform GetAnchor(
            CraftLivePad2PhysicalSlot physicalSlot)
        {
            switch (physicalSlot)
            {
                case CraftLivePad2PhysicalSlot.UpperLeft:
                    return bindings.UpperLeftSlot;
                case CraftLivePad2PhysicalSlot.MiddleLeft:
                    return bindings.MiddleLeftSlot;
                case CraftLivePad2PhysicalSlot.UpperRight:
                    return bindings.UpperRightSlot;
                case CraftLivePad2PhysicalSlot.MiddleRight:
                    return bindings.MiddleRightSlot;
                case CraftLivePad2PhysicalSlot.LowerLeft:
                    return bindings.LowerLeftSkillSlot;
                default:
                    return bindings.LowerRightAttributeSlot;
            }
        }

        private static void RemoveGeneratedSlot(Transform anchor)
        {
            CraftLiveGeneratedRuntimeVisual[] generated =
                anchor.GetComponentsInChildren<
                    CraftLiveGeneratedRuntimeVisual>(true);
            foreach (CraftLiveGeneratedRuntimeVisual visual in generated)
            {
                if (visual != null &&
                    visual.transform.parent == anchor)
                {
                    DestroySafely(visual.gameObject);
                }
            }
        }

        private static CraftLivePlacementSlotView FindCustomSlotView(
            Transform anchor)
        {
            CraftLivePlacementSlotView[] candidates =
                anchor.GetComponentsInChildren<
                    CraftLivePlacementSlotView>(true);
            foreach (CraftLivePlacementSlotView candidate in candidates)
            {
                if (candidate != null &&
                    candidate.GetComponentInParent<
                        CraftLiveGeneratedRuntimeVisual>() == null)
                {
                    return candidate;
                }
            }

            return null;
        }

        private Color GetSlotColor(CraftLiveSlotId slot)
        {
            if (slot == CraftLiveSlotId.Skill)
            {
                return skillSlotColor;
            }

            if (slot == CraftLiveSlotId.Attribute)
            {
                return attributeSlotColor;
            }

            return baseSlotColor;
        }

        private static TextMesh CreateText(
            Transform parent,
            string name,
            string value,
            Vector3 position,
            float size)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                text,
                size,
                CraftLiveForgeUITheme.ParchmentText);
            return text;
        }

        private static void ApplyColor(
            Renderer target,
            Color color)
        {
            if (target == null)
            {
                return;
            }

            CraftLiveForgeUITheme.ApplyForgeSurface(target, color);
        }

        private static void SetActive(
            GameObject target,
            bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }

        private static void DestroySafely(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnValidate()
        {
            slotDiameter = Mathf.Max(0.4f, slotDiameter);
        }
    }
}
