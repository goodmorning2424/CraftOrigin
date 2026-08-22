using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad2WeaponCarousel :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad2Bindings bindings;

        [Header("Carousel")]
        [SerializeField] private bool createFallbackVisuals = true;
        [SerializeField, Min(20f)] private float swipeThresholdPixels = 70f;
        [SerializeField, Min(0.5f)] private float cardSpacing = 2.65f;
        [SerializeField, Range(0.2f, 1f)] private float neighborScale = 0.62f;
        [SerializeField, Min(0.2f)] private float selectedModelSize = 1.65f;
        [SerializeField, Min(0.2f)] private float centerModelSize = 2.4f;
        [SerializeField, Min(0.05f)] private float slideDuration = 0.26f;
        [SerializeField, Min(0.0001f)] private float dragPositionScale =
            0.006f;
        [SerializeField] private Color cardColor =
            new Color(0.08f, 0.38f, 0.46f, 1f);

        [Header("UI Events")]
        [SerializeField] private UnityEvent<bool> onSelectionVisible;
        [SerializeField] private UnityEvent<string> onWeaponNameChanged;
        [SerializeField] private UnityEvent<string> onWeaponTypeChanged;
        [SerializeField] private UnityEvent<float> onAttackChanged;
        [SerializeField] private UnityEvent<float> onDefenseChanged;
        [SerializeField] private UnityEvent<float> onEvasionChanged;
        [SerializeField] private UnityEvent<bool> onWeaponConfirmed;

        private readonly List<CraftLiveWeaponDefinition> weapons =
            new List<CraftLiveWeaponDefinition>();
        private GameObject generatedCarousel;
        private GameObject carouselContent;
        private GameObject centerWeapon;
        private GameObject changeWeaponButton;
        private int selectedIndex;
        private float dragStartX;
        private float dragVisualOffset;
        private bool dragging;
        private Coroutine slideRoutine;
        private string displayedWeaponId = string.Empty;
        private bool displayedConfirmed;

        public int SelectedIndex => selectedIndex;
        public int WeaponCount => weapons.Count;
        public bool IsDragging => dragging;
        public CraftLiveWeaponDefinition SelectedWeapon =>
            weapons.Count > 0 &&
            selectedIndex >= 0 &&
            selectedIndex < weapons.Count
                ? weapons[selectedIndex]
                : null;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
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

            if (slideRoutine != null)
            {
                StopCoroutine(slideRoutine);
                slideRoutine = null;
            }
        }

        public void Rebuild()
        {
            ResolveReferences();
            if (session == null ||
                session.Catalog == null ||
                bindings == null)
            {
                return;
            }

            weapons.Clear();
            foreach (CraftLiveWeaponDefinition weapon in
                     session.Catalog.Weapons)
            {
                if (weapon != null && weapon.VisibleInSelection)
                {
                    weapons.Add(weapon);
                }
            }

            weapons.Sort(CompareWeaponsByType);

            selectedIndex = FindWeaponIndex(
                session.State != null
                    ? session.State.selectedWeaponId
                    : string.Empty);
            displayedWeaponId = session.State != null
                ? session.State.selectedWeaponId
                : string.Empty;
            displayedConfirmed = session.State != null &&
                                 session.State.weaponSelectionConfirmed;
            BuildCarouselVisuals();
            BuildCenterWeapon();
            EnsureChangeWeaponButton();
            Refresh(session.State);
        }

        public void SelectNext()
        {
            BeginSelectionSlide(selectedIndex + 1, -1f);
        }

        public void SelectPrevious()
        {
            BeginSelectionSlide(selectedIndex - 1, 1f);
        }

        public void SelectIndex(int index)
        {
            if (weapons.Count == 0 ||
                session == null ||
                !CanChangeWeapon(session.State))
            {
                return;
            }

            selectedIndex = WrapIndex(index, weapons.Count);
            session.SelectWeapon(weapons[selectedIndex]);
        }

        public void ConfirmSelected()
        {
            if (session == null ||
                SelectedWeapon == null ||
                !CanChangeWeapon(session.State))
            {
                return;
            }

            session.ConfirmWeapon(SelectedWeapon);
        }

        public void OpenSelection()
        {
            if (session == null ||
                SelectedWeapon == null ||
                !CanChangeWeapon(session.State))
            {
                return;
            }

            session.SelectWeapon(SelectedWeapon);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (session == null ||
                session.State == null ||
                session.State.weaponSelectionConfirmed ||
                slideRoutine != null)
            {
                return;
            }

            dragging = true;
            dragStartX = eventData.position.x;
            dragVisualOffset = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragVisualOffset =
                eventData.position.x - dragStartX;
            if (carouselContent != null)
            {
                Vector3 position =
                    carouselContent.transform.localPosition;
                position.x = Mathf.Clamp(
                    dragVisualOffset * dragPositionScale,
                    -cardSpacing * 0.65f,
                    cardSpacing * 0.65f);
                carouselContent.transform.localPosition = position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            float delta = eventData.position.x - dragStartX;
            if (delta <= -swipeThresholdPixels)
            {
                BeginSelectionSlide(selectedIndex + 1, -1f);
            }
            else if (delta >= swipeThresholdPixels)
            {
                BeginSelectionSlide(selectedIndex - 1, 1f);
            }
            else
            {
                BeginSnapBack();
            }
        }

        private void BeginSelectionSlide(
            int requestedIndex,
            float direction)
        {
            if (weapons.Count == 0 ||
                session == null ||
                !CanChangeWeapon(session.State) ||
                slideRoutine != null)
            {
                return;
            }

            int targetIndex = WrapIndex(
                requestedIndex,
                weapons.Count);
            float start = carouselContent != null
                ? carouselContent.transform.localPosition.x
                : 0f;
            slideRoutine = StartCoroutine(
                AnimateContentSlide(
                    start,
                    direction * cardSpacing,
                    targetIndex,
                    true));
        }

        private void BeginSnapBack()
        {
            if (slideRoutine != null)
            {
                return;
            }

            float start = carouselContent != null
                ? carouselContent.transform.localPosition.x
                : 0f;
            slideRoutine = StartCoroutine(
                AnimateContentSlide(
                    start,
                    0f,
                    selectedIndex,
                    false));
        }

        private System.Collections.IEnumerator AnimateContentSlide(
            float start,
            float end,
            int targetIndex,
            bool applySelection)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, slideDuration);
            while (elapsed < duration && carouselContent != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                Vector3 position =
                    carouselContent.transform.localPosition;
                position.x = Mathf.LerpUnclamped(start, end, t);
                carouselContent.transform.localPosition = position;
                yield return null;
            }

            if (carouselContent != null)
            {
                Vector3 position =
                    carouselContent.transform.localPosition;
                position.x = applySelection ? end : 0f;
                carouselContent.transform.localPosition = position;
            }

            slideRoutine = null;
            if (applySelection &&
                session != null &&
                targetIndex >= 0 &&
                targetIndex < weapons.Count)
            {
                selectedIndex = targetIndex;
                session.SelectWeapon(weapons[targetIndex]);
            }
        }

        public static int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        public static bool CanChangeWeapon(CraftLiveRoomState state)
        {
            bool recoverableUnconfirmedMaterialSelection =
                state != null &&
                !state.weaponSelectionConfirmed &&
                (state.placement.status ==
                     CraftLivePlacementStatus.SelectingSlot ||
                 state.placement.status ==
                     CraftLivePlacementStatus.ConfirmingSlot);
            return state != null &&
                   state.sessionPhase == CraftLiveSessionPhase.Playing &&
                   (state.placement.status ==
                        CraftLivePlacementStatus.Idle ||
                    recoverableUnconfirmedMaterialSelection) &&
                   state.transferQueue != null &&
                   state.transferQueue.Count == 0 &&
                   !state.HasAnyPlacedMaterial() &&
                   state.craft.status ==
                   CraftLiveCraftStatus.Editing;
        }

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (bindings == null)
            {
                bindings =
                    GetComponentInParent<CraftLivePad2Bindings>();
            }
        }

        private void Subscribe()
        {
            if (session == null)
            {
                return;
            }

            session.StateChanged -= Refresh;
            session.StateChanged += Refresh;
            Refresh(session.State);
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null || weapons.Count == 0)
            {
                return;
            }

            int nextIndex = FindWeaponIndex(state.selectedWeaponId);
            bool selectionChanged =
                nextIndex != selectedIndex ||
                displayedWeaponId != state.selectedWeaponId;
            bool confirmationChanged =
                displayedConfirmed != state.weaponSelectionConfirmed;
            selectedIndex = nextIndex;
            displayedWeaponId = state.selectedWeaponId;
            displayedConfirmed = state.weaponSelectionConfirmed;

            if (selectionChanged)
            {
                BuildCarouselVisuals();
                BuildCenterWeapon();
            }
            else if (centerWeapon == null)
            {
                BuildCenterWeapon();
            }

            if (generatedCarousel != null)
            {
                generatedCarousel.SetActive(
                    state.sessionPhase == CraftLiveSessionPhase.Playing &&
                    !state.weaponSelectionConfirmed);
            }

            if (centerWeapon != null)
            {
                centerWeapon.SetActive(ShouldShowCenterWeapon(state));
            }

            if (changeWeaponButton != null)
            {
                changeWeaponButton.SetActive(
                    state.weaponSelectionConfirmed &&
                    CanChangeWeapon(state));
            }

            if (confirmationChanged)
            {
                ResetDragPosition();
            }

            Publish(SelectedWeapon, state.weaponSelectionConfirmed);
        }

        private void Publish(
            CraftLiveWeaponDefinition weapon,
            bool confirmed)
        {
            bool selectionVisible = session != null &&
                                    session.State != null &&
                                    session.State.sessionPhase ==
                                    CraftLiveSessionPhase.Playing &&
                                    !confirmed;
            onSelectionVisible?.Invoke(selectionVisible);
            onWeaponConfirmed?.Invoke(confirmed);
            if (weapon == null)
            {
                return;
            }

            CraftLiveStats stats = weapon.BaseStats;
            onWeaponNameChanged?.Invoke(weapon.DisplayName);
            onWeaponTypeChanged?.Invoke(
                GetWeaponTypeLabel(weapon.WeaponType));
            onAttackChanged?.Invoke(stats.attackRate);
            onDefenseChanged?.Invoke(stats.defenseRate);
            onEvasionChanged?.Invoke(stats.evasionRate);
        }

        private int FindWeaponIndex(string weaponId)
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (weapons[i].WeaponId == weaponId)
                {
                    return i;
                }
            }

            return 0;
        }

        private void BuildCarouselVisuals()
        {
            if (bindings == null ||
                bindings.WeaponCarouselRoot == null ||
                !createFallbackVisuals)
            {
                return;
            }

            DestroySafely(generatedCarousel);
            generatedCarousel = new GameObject(
                "Generated_WeaponCarousel");
            generatedCarousel.transform.SetParent(
                bindings.WeaponCarouselRoot,
                false);
            generatedCarousel.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();
            carouselContent = new GameObject("SlidingCards");
            carouselContent.transform.SetParent(
                generatedCarousel.transform,
                false);

            GameObject background = CreateCube(
                "CarouselBackground",
                generatedCarousel.transform,
                new Vector3(0f, 0f, 0.45f),
                new Vector3(8f, 6.8f, 0.12f),
                new Color(0.025f, 0.045f, 0.055f, 1f),
                false);

            GameObject swipeSurface = CreateCube(
                "SwipeSurface",
                generatedCarousel.transform,
                new Vector3(0f, 0f, -0.3f),
                new Vector3(7.8f, 6.4f, 0.05f),
                Color.clear,
                true);
            Renderer swipeRenderer =
                swipeSurface.GetComponent<Renderer>();
            if (swipeRenderer != null)
            {
                swipeRenderer.enabled = false;
            }

            if (weapons.Count == 0)
            {
                return;
            }

            for (int relative = -1; relative <= 1; relative++)
            {
                int index = WrapIndex(
                    selectedIndex + relative,
                    weapons.Count);
                CreateCard(
                    weapons[index],
                    relative,
                    relative == 0);
            }

            CreateButton(
                generatedCarousel.transform,
                "PreviousButton",
                "<",
                new Vector3(-2.15f, 0f, -0.4f),
                new Color(0.2f, 0.55f, 0.65f),
                SelectPrevious);
            CreateButton(
                generatedCarousel.transform,
                "NextButton",
                ">",
                new Vector3(2.15f, 0f, -0.4f),
                new Color(0.2f, 0.55f, 0.65f),
                SelectNext);
            CreateButton(
                generatedCarousel.transform,
                "ConfirmWeaponButton",
                "この武器にする",
                new Vector3(0f, -2.65f, -0.4f),
                new Color(0.22f, 0.72f, 0.42f),
                ConfirmSelected,
                new Vector3(2.4f, 0.7f, 0.22f));
        }

        private void CreateCard(
            CraftLiveWeaponDefinition weapon,
            int relativeIndex,
            bool selected)
        {
            GameObject card = new GameObject(
                $"Card_{weapon.WeaponId}_{relativeIndex}");
            card.transform.SetParent(
                carouselContent.transform,
                false);
            card.transform.localPosition =
                new Vector3(
                    relativeIndex * cardSpacing,
                    0.3f,
                    selected ? -0.45f : 0f);
            card.transform.localScale =
                Vector3.one * (selected ? 1f : neighborScale);

            Color panelColor = selected
                ? Color.Lerp(cardColor, Color.white, 0.22f)
                : cardColor * 0.45f;
            panelColor.a = 1f;
            CreateCube(
                "CardPanel",
                card.transform,
                Vector3.zero,
                new Vector3(2.25f, 4.35f, 0.14f),
                panelColor,
                false);

            GameObject model = CreateWeaponVisual(
                weapon,
                card.transform,
                selected ? selectedModelSize : selectedModelSize * 0.8f);
            model.transform.localPosition =
                new Vector3(0f, 0.35f, -0.22f);
            CreateText(
                card.transform,
                "WeaponName",
                weapon.DisplayName,
                new Vector3(0f, -1.55f, -0.22f),
                selected ? 0.04f : 0.034f);
            CreateText(
                card.transform,
                "WeaponType",
                GetWeaponTypeLabel(weapon.WeaponType),
                new Vector3(0f, 1.7f, -0.22f),
                0.032f);
        }

        private void BuildCenterWeapon()
        {
            if (bindings == null ||
                bindings.CenterWeaponRoot == null)
            {
                return;
            }

            DestroySafely(centerWeapon);
            if (SelectedWeapon == null)
            {
                return;
            }

            centerWeapon = CreateWeaponVisual(
                SelectedWeapon,
                bindings.CenterWeaponRoot,
                centerModelSize);
            centerWeapon.name =
                $"Generated_CenterWeapon_{SelectedWeapon.WeaponId}";
            centerWeapon.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();
            centerWeapon.transform.localPosition = Vector3.zero;
            centerWeapon.SetActive(
                session != null &&
                ShouldShowCenterWeapon(session.State));
        }

        private static bool ShouldShowCenterWeapon(
            CraftLiveRoomState state)
        {
            return state != null &&
                   state.sessionPhase == CraftLiveSessionPhase.Playing &&
                   state.craft.status != CraftLiveCraftStatus.Complete &&
                   state.weaponSelectionConfirmed;
        }

        private void EnsureChangeWeaponButton()
        {
            if (!createFallbackVisuals ||
                bindings == null ||
                bindings.UiRoot == null ||
                changeWeaponButton != null)
            {
                return;
            }

            changeWeaponButton = CreateButton(
                bindings.UiRoot,
                "Generated_ChangeWeaponButton",
                "武器を選び直す",
                new Vector3(0f, 2.72f, -0.7f),
                new Color(0.2f, 0.55f, 0.65f),
                OpenSelection,
                new Vector3(2.8f, 0.6f, 0.2f));
            changeWeaponButton.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();
        }

        private GameObject CreateWeaponVisual(
            CraftLiveWeaponDefinition weapon,
            Transform parent,
            float targetSize)
        {
            GameObject result = new GameObject(
                $"Weapon_{weapon.WeaponId}");
            result.transform.SetParent(parent, false);
            GameObject contentObject = new GameObject("VisualContent");
            contentObject.transform.SetParent(result.transform, false);
            Transform content = contentObject.transform;

            if (weapon.WorkbenchPrefab != null)
            {
                Instantiate(
                    weapon.WorkbenchPrefab,
                    content,
                    false);
            }
            else
            {
                PrimitiveType primitive =
                    weapon.WeaponType ==
                    CraftLiveWeaponType.Thrust
                        ? PrimitiveType.Capsule
                        : weapon.WeaponType ==
                          CraftLiveWeaponType.Staff
                            ? PrimitiveType.Cylinder
                            : PrimitiveType.Cube;
                GameObject fallback =
                    GameObject.CreatePrimitive(primitive);
                fallback.transform.SetParent(content, false);
                switch (weapon.WeaponType)
                {
                    case CraftLiveWeaponType.Sword:
                        fallback.transform.localScale =
                            new Vector3(0.25f, 2.4f, 0.15f);
                        break;
                    case CraftLiveWeaponType.Thrust:
                        fallback.transform.localScale =
                            new Vector3(0.3f, 1.7f, 0.3f);
                        break;
                    default:
                        fallback.transform.localScale =
                            new Vector3(0.18f, 2.2f, 0.18f);
                        break;
                }

                ApplyColor(
                    fallback.GetComponent<Renderer>(),
                    GetWeaponTypeColor(weapon.WeaponType));
            }

            CraftLiveForgeUITheme.ApplyMaterialOverride(
                result,
                weapon.PresentationMaterialOverride);

            result.transform.localPosition = Vector3.zero;
            CraftLiveRuntimeVisualUtility.FitAndCenter(
                content,
                targetSize * weapon.SelectionPreviewScale,
                true,
                -18f);
            content.localScale =
                Vector3.Scale(
                    content.localScale,
                    weapon.PreviewScale);
            // Imported prefabs often have an authored pivot away from their
            // visible bounds. PreviewScale must be applied before the final
            // centering pass or changing size also moves the weapon onscreen.
            CraftLiveRuntimeVisualUtility.CenterInParent(content);
            DisableColliders(result);
            return result;
        }

        private GameObject CreateButton(
            Transform parent,
            string name,
            string label,
            Vector3 position,
            Color color,
            UnityAction action,
            Vector3? scale = null)
        {
            GameObject button = CreateCube(
                name,
                parent,
                position,
                scale ?? new Vector3(0.72f, 0.72f, 0.2f),
                color,
                true);
            CraftLiveWorldButton worldButton =
                button.AddComponent<CraftLiveWorldButton>();
            Renderer renderer = button.GetComponent<Renderer>();
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
                (scale ?? Vector3.one).x > 2.2f
                    ? 0.06f
                    : (scale ?? Vector3.one).x > 1f
                    ? 0.032f
                    : 0.06f);
            return button;
        }

        private void ResetDragPosition()
        {
            dragVisualOffset = 0f;
            if (carouselContent != null)
            {
                Vector3 position =
                    carouselContent.transform.localPosition;
                position.x = 0f;
                carouselContent.transform.localPosition = position;
            }
        }

        private static string GetWeaponTypeLabel(
            CraftLiveWeaponType type)
        {
            switch (type)
            {
                case CraftLiveWeaponType.Sword:
                    return "剣タイプ";
                case CraftLiveWeaponType.Thrust:
                    return "突きタイプ";
                default:
                    return "杖タイプ";
            }
        }

        private static int CompareWeaponsByType(
            CraftLiveWeaponDefinition left,
            CraftLiveWeaponDefinition right)
        {
            int typeOrder = left.WeaponType.CompareTo(right.WeaponType);
            return typeOrder != 0
                ? typeOrder
                : string.CompareOrdinal(left.DisplayName, right.DisplayName);
        }

        private static Color GetWeaponTypeColor(
            CraftLiveWeaponType type)
        {
            switch (type)
            {
                case CraftLiveWeaponType.Sword:
                    return new Color(0.72f, 0.75f, 0.82f);
                case CraftLiveWeaponType.Thrust:
                    return new Color(0.65f, 0.82f, 0.9f);
                default:
                    return new Color(0.55f, 0.38f, 0.82f);
            }
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            ApplyColor(cube.GetComponent<Renderer>(), color);
            if (!keepCollider)
            {
                Collider collider = cube.GetComponent<Collider>();
                DestroySafely(collider);
            }

            return cube;
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

        private static void DisableColliders(GameObject target)
        {
            foreach (Collider collider in
                     target.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
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
            swipeThresholdPixels =
                Mathf.Max(20f, swipeThresholdPixels);
            cardSpacing = Mathf.Max(0.5f, cardSpacing);
            selectedModelSize =
                Mathf.Max(0.2f, selectedModelSize);
            centerModelSize =
                Mathf.Max(0.2f, centerModelSize);
            slideDuration = Mathf.Max(0.05f, slideDuration);
            dragPositionScale = Mathf.Max(
                0.0001f,
                dragPositionScale);
        }
    }
}

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Fits imported runtime models without discarding the position, rotation,
    /// and scale authored on their prefab roots.
    /// </summary>
    internal static class CraftLiveRuntimeVisualUtility
    {
        private static readonly Quaternion[] SurfaceOrientations =
        {
            Quaternion.identity,
            Quaternion.Euler(90f, 0f, 0f),
            Quaternion.Euler(-90f, 0f, 0f),
            Quaternion.Euler(0f, 90f, 0f),
            Quaternion.Euler(0f, -90f, 0f),
            Quaternion.Euler(180f, 0f, 0f)
        };

        public static bool FitAndCenter(
            Transform contentRoot,
            float targetProjectedSize,
            bool faceLargestSurface,
            float rollDegrees = 0f,
            bool preferUpright = false)
        {
            if (contentRoot == null || contentRoot.parent == null)
            {
                return false;
            }

            contentRoot.localPosition = Vector3.zero;
            contentRoot.localRotation = Quaternion.identity;
            contentRoot.localScale = Vector3.one;

            Renderer orientationRenderer =
                contentRoot.GetComponentInChildren<Renderer>(true);
            Vector3 authoredUp = orientationRenderer != null
                ? contentRoot.InverseTransformDirection(
                    orientationRenderer.transform.up)
                : Vector3.up;

            Quaternion roll = Quaternion.Euler(0f, 0f, rollDegrees);
            Quaternion bestRotation = roll;
            if (faceLargestSurface)
            {
                float bestScore = float.NegativeInfinity;
                foreach (Quaternion orientation in SurfaceOrientations)
                {
                    contentRoot.localRotation = roll * orientation;
                    if (!TryGetBounds(
                            contentRoot,
                            contentRoot.parent,
                            out Bounds candidateBounds))
                    {
                        continue;
                    }

                    Vector3 size = candidateBounds.size;
                    float projectedArea =
                        Mathf.Max(0.000001f, size.x * size.y);
                    float depthPenalty =
                        Mathf.Max(0.0001f, size.z) +
                        Mathf.Max(size.x, size.y) * 0.02f;
                    float score = projectedArea / depthPenalty;
                    if (preferUpright)
                    {
                        Vector3 candidateUp =
                            contentRoot.parent.InverseTransformDirection(
                                contentRoot.TransformDirection(
                                    authoredUp));
                        float upright = candidateUp.sqrMagnitude >
                                        0.0001f
                            ? Vector3.Dot(
                                candidateUp.normalized,
                                Vector3.up)
                            : 0f;
                        score *= Mathf.Lerp(
                            0.94f,
                            1.06f,
                            (upright + 1f) * 0.5f);
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestRotation = contentRoot.localRotation;
                    }
                }
            }

            contentRoot.localRotation = bestRotation;
            if (!TryGetBounds(
                    contentRoot,
                    contentRoot.parent,
                    out Bounds bounds))
            {
                return false;
            }

            float projectedSize = Mathf.Max(
                bounds.size.x,
                bounds.size.y);
            if (projectedSize <= 0.0001f)
            {
                return false;
            }

            contentRoot.localScale *=
                Mathf.Max(0.0001f, targetProjectedSize) /
                projectedSize;
            CenterInParent(contentRoot);

            return true;
        }

        public static bool CenterInParent(Transform contentRoot)
        {
            if (contentRoot == null || contentRoot.parent == null ||
                !TryGetBounds(
                    contentRoot,
                    contentRoot.parent,
                    out Bounds bounds))
            {
                return false;
            }

            contentRoot.localPosition -= bounds.center;
            return true;
        }

        private static bool TryGetBounds(
            Transform visualRoot,
            Transform targetSpace,
            out Bounds bounds)
        {
            bounds = default;
            bool hasPoint = false;
            Renderer[] renderers =
                visualRoot.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Bounds localBounds = renderer.localBounds;
                Vector3 center = localBounds.center;
                Vector3 extents = localBounds.extents;
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Vector3 localPoint = center +
                                Vector3.Scale(
                                    extents,
                                    new Vector3(x, y, z));
                            Vector3 worldPoint =
                                renderer.transform.TransformPoint(
                                    localPoint);
                            Vector3 point =
                                targetSpace.InverseTransformPoint(
                                    worldPoint);
                            if (!hasPoint)
                            {
                                bounds = new Bounds(
                                    point,
                                    Vector3.zero);
                                hasPoint = true;
                            }
                            else
                            {
                                bounds.Encapsulate(point);
                            }
                        }
                    }
                }
            }

            return hasPoint;
        }
    }
}
