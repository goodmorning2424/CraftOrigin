using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad1GalleryController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad1Bindings bindings;
        [SerializeField] private CraftLiveMaterialPaintingView paintingPrefab;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CraftLiveGalleryWallSlider wallSlider;

        [Header("Camera")]
        [SerializeField, Tooltip(
            "When disabled, the camera keeps the position authored in the scene hierarchy.")]
        private bool useAutomaticPortraitFraming;

        [Header("Registration")]
        [SerializeField] private bool showLockedMaterials;

        [Header("Automatic Layout")]
        [SerializeField] private bool applyDefaultLayout = true;
        [SerializeField, Tooltip(
            "Keeps scene-authored wall galleries in place instead of replacing them with generated fallback columns.")]
        private bool preservePreplacedHierarchy = true;
        [SerializeField, Min(1f)] private float columnSpacing = 2.75f;
        [SerializeField] private float columnVerticalPosition;
        [SerializeField, Min(0.5f)] private float paintingSpacing = 2.35f;
        [SerializeField, Min(1)] private int visiblePaintings = 2;
        [SerializeField, Min(0.001f)] private float dragSensitivity = 0.012f;
        [SerializeField, Min(0.01f)] private float mouseWheelStep = 0.8f;

        [Header("Fallback Painting")]
        [SerializeField] private Vector2 paintingSize =
            new Vector2(2.15f, 1.72f);
        [Header("Wall Fit")]
        [SerializeField] private bool fitToWallBounds = true;
        [SerializeField, Min(0f)] private float wallHorizontalPadding = 0.15f;
        [SerializeField, Min(0f)] private float wallVerticalPadding = 0.3f;
        [SerializeField, Min(0f)] private float paintingGap = 0.18f;
        [SerializeField, Min(0f)] private float wallFrontOffset = 0.03f;
        [SerializeField] private bool createFallbackWallBackdrop = true;
        [SerializeField] private Color frameColor =
            new Color(0.18f, 0.09f, 0.035f, 1f);
        [SerializeField] private Color wallColor =
            new Color(0.085f, 0.06f, 0.045f, 1f);

        private readonly List<CraftLiveMaterialPaintingView> paintings =
            new List<CraftLiveMaterialPaintingView>();
        private readonly Dictionary<CraftLiveMaterialCategory, int>
            categoryCounts =
                new Dictionary<CraftLiveMaterialCategory, int>();
        private bool built;
        private int handledRegistrationSerial = -1;

        private struct ColumnLayout
        {
            public Vector2 paintingSize;
            public float paintingSpacing;
            public float viewportTop;
            public float viewportBottom;
            public float firstPaintingY;
            public Vector3 localCenter;
            public bool hasWallBounds;
        }

        public int GeneratedPaintingCount => paintings.Count;
        public bool ShowLockedMaterials => showLockedMaterials;
        public CraftLiveGalleryWallSlider WallSlider => wallSlider;
        public Camera TargetCamera => targetCamera;

        public void SetSpecialHeadersVisible(bool visible)
        {
            CraftLiveGalleryWallView[] walls =
                FindObjectsByType<CraftLiveGalleryWallView>(
                    FindObjectsInactive.Include);
            foreach (CraftLiveGalleryWallView wall in walls)
            {
                if (wall == null ||
                    (wall.Category != CraftLiveMaterialCategory.Skill &&
                     wall.Category != CraftLiveMaterialCategory.Attribute))
                {
                    continue;
                }

                wall.SetHeaderVisible(visible);
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ConfigureCameraFraming();
            EnsureCommentBoard();
            EnsureEventSystem();
            DisableLegacyWallCarousel();
            EnsureCameraRaycaster();
            ConfigureWallInputSurfaces();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (session != null)
            {
                session.StateChanged += HandleStateChanged;
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
                session.StateChanged -= HandleStateChanged;
            }

            SetSpecialHeadersVisible(true);
        }

        public void Rebuild()
        {
            ResolveReferences();
            if (session == null ||
                session.Catalog == null ||
                bindings == null)
            {
                Debug.LogWarning(
                    "CraftLivePad1GalleryController: Session, Catalog, or " +
                    "Bindings is missing.",
                    this);
                return;
            }

            ClearPreplacedWallBindings();
            ClearGeneratedColumns();
            paintings.Clear();
            categoryCounts.Clear();

            bool preserveSceneWallPositions = HasAnyWallBounds();
            if (applyDefaultLayout && !preserveSceneWallPositions)
            {
                ApplyLayout();
            }

            PreserveAuthoredGalleryLayout();
            ConfigureWallInputSurfaces();

            BuildColumn(
                bindings.PowerUpWall,
                CraftLiveMaterialCategory.Upgrade,
                "パワーアップ");
            BuildColumn(
                bindings.SkillWall,
                CraftLiveMaterialCategory.Skill,
                "スキル");
            BuildColumn(
                bindings.TypeWall,
                CraftLiveMaterialCategory.Attribute,
                "タイプ");

            built = true;
            CraftLiveRoomState state = session.State;
            handledRegistrationSerial =
                state != null ? state.registrationSerial : -1;
            RefreshPaintings(state);
        }

        public void SetShowLockedMaterials(bool value)
        {
            if (showLockedMaterials == value)
            {
                return;
            }

            showLockedMaterials = value;
            Rebuild();
        }

        public int GetGeneratedCount(
            CraftLiveMaterialCategory category)
        {
            return categoryCounts.TryGetValue(category, out int count)
                ? count
                : 0;
        }

        public void SelectMaterial(
            CraftLiveMaterialDefinition material)
        {
            SelectMaterial(material, null);
        }

        public void SelectMaterial(
            CraftLiveMaterialDefinition material,
            Transform selectionAnchor)
        {
            if (session == null || material == null)
            {
                return;
            }

            if (IsMaterialSelected(material))
            {
                session.CancelPlacement();
                return;
            }

            CraftLivePad1MaterialPreview preview =
                GetComponent<CraftLivePad1MaterialPreview>();
            preview?.SetSelectionAnchor(material, selectionAnchor);

            if (IsWaitingForSingleTransfer(session.State))
            {
                // Let Pad1 show the material details, but keep the shared
                // placement state Idle so Pad2 does not light a second guide.
                session.ShowSingleTransferWarning();
                preview?.ShowDetailsWithoutPlacement(material);
                return;
            }

            if (CanBeginMaterialPlacement(session.State, material))
            {
                session.SelectMaterial(material);
                return;
            }

            // Never expand a details hologram without entering the exact
            // placement state that makes Pad 2 guides visible. Previously
            // this preview-only path made the hologram appear while the
            // authoritative state stayed Idle, so no placement guide could
            // be selected.
            preview?.ClearPreview();
        }

        public static bool CanBeginMaterialPlacement(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material)
        {
            return state != null &&
                   material != null &&
                   state.sessionPhase == CraftLiveSessionPhase.Playing &&
                   state.weaponSelectionConfirmed &&
                   state.placement != null &&
                   state.placement.status == CraftLivePlacementStatus.Idle &&
                   !IsWaitingForSingleTransfer(state) &&
                   HasCompatibleOpenSlot(state, material);
        }

        public static bool IsWaitingForSingleTransfer(
            CraftLiveRoomState state)
        {
            return !CraftLiveSession.MultiMaterialTransferEnabled &&
                   state != null &&
                   state.placement != null &&
                   state.placement.status == CraftLivePlacementStatus.Idle &&
                   state.transferQueue != null &&
                   state.transferQueue.Count > 0;
        }

        private static bool HasCompatibleOpenSlot(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material)
        {
            return CanUseOpenSlot(state, material, CraftLiveSlotId.Top) ||
                   CanUseOpenSlot(state, material, CraftLiveSlotId.Left) ||
                   CanUseOpenSlot(state, material, CraftLiveSlotId.Right) ||
                   CanUseOpenSlot(state, material, CraftLiveSlotId.Bottom) ||
                   CanUseOpenSlot(state, material, CraftLiveSlotId.Skill) ||
                   CanUseOpenSlot(
                       state,
                       material,
                       CraftLiveSlotId.Attribute);
        }

        private static bool CanUseOpenSlot(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material,
            CraftLiveSlotId slot)
        {
            return material.CanUseIn(slot) && state.CanReserveSlot(slot);
        }

        public Transform FindMaterialAnchor(string materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return null;
            }

            foreach (CraftLiveMaterialPaintingView painting in paintings)
            {
                if (painting != null &&
                    painting.Material != null &&
                    painting.Material.MaterialId == materialId)
                {
                    return painting.PresentationAnchor;
                }
            }

            return null;
        }

        public bool IsMaterialSelected(
            CraftLiveMaterialDefinition material)
        {
            return session != null &&
                   session.State != null &&
                   material != null &&
                   session.State.selectedMaterialId == material.MaterialId;
        }

        public static bool ShouldDisplayMaterial(
            CraftLiveMaterialDefinition material,
            CraftLiveRoomState state,
            bool includeLocked)
        {
            if (material == null)
            {
                return false;
            }

            return includeLocked ||
                   !material.RequiresQrUnlock ||
                   (state != null &&
                    state.HasMaterialRegistered(material.MaterialId));
        }

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (bindings == null)
            {
                bindings = GetComponent<CraftLivePad1Bindings>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (wallSlider == null)
            {
                wallSlider = GetComponent<CraftLiveGalleryWallSlider>();
            }
        }

        private void EnsureCommentBoard()
        {
            CraftLiveWoodCommentBoard board =
                GetComponent<CraftLiveWoodCommentBoard>();
            if (board == null)
            {
                board = gameObject.AddComponent<
                    CraftLiveWoodCommentBoard>();
            }

            board.Configure(session, targetCamera);
        }

        private void ConfigureCameraFraming()
        {
            CraftLivePad1PortraitFraming framing =
                GetComponent<CraftLivePad1PortraitFraming>();
            if (!useAutomaticPortraitFraming)
            {
                if (framing != null)
                {
                    framing.enabled = false;
                }

                return;
            }

            if (framing == null)
            {
                framing = gameObject.AddComponent<
                    CraftLivePad1PortraitFraming>();
            }

            framing.enabled = true;

            CraftLivePadSceneRoot sceneRoot =
                GetComponent<CraftLivePadSceneRoot>();
            framing.Configure(
                targetCamera,
                sceneRoot != null ? sceneRoot.CameraAnchor : null);
        }

        private void EnsureCameraRaycaster()
        {
            if (targetCamera != null &&
                targetCamera.GetComponent<PhysicsRaycaster>() == null)
            {
                targetCamera.gameObject.AddComponent<PhysicsRaycaster>();
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject(
                "Generated_Pad1EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            InputSystemUIInputModule inputModule =
                eventSystemObject.AddComponent<
                    InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private void ConfigureWallInputSurfaces()
        {
            if (bindings == null)
            {
                return;
            }

            ConfigureWallInputSurface(bindings.PowerUpWall);
            ConfigureWallInputSurface(bindings.SkillWall);
            ConfigureWallInputSurface(bindings.TypeWall);
        }

        private static void ConfigureWallInputSurface(Transform wallRoot)
        {
            if (wallRoot == null)
            {
                return;
            }

            CraftLiveGalleryColumn column =
                wallRoot.GetComponentInChildren<
                    CraftLiveGalleryColumn>(true);
            if (column == null)
            {
                return;
            }

            foreach (Collider targetCollider in
                     wallRoot.GetComponentsInChildren<Collider>(true))
            {
                if (targetCollider == null ||
                    targetCollider.GetComponentInParent<
                        CraftLiveMaterialPaintingView>() != null)
                {
                    continue;
                }

                targetCollider.enabled = true;
                CraftLiveGalleryInputSurface inputSurface =
                    targetCollider.GetComponent<
                        CraftLiveGalleryInputSurface>();
                if (inputSurface == null)
                {
                    inputSurface = targetCollider.gameObject.AddComponent<
                        CraftLiveGalleryInputSurface>();
                }

                inputSurface.Configure(column, wallRoot);
            }
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            if (built &&
                state.registrationSerial != handledRegistrationSerial)
            {
                bool playArrival = ShouldPlayRegistrationArrival(
                    built,
                    state.registrationSerial,
                    handledRegistrationSerial,
                    state.lastRegisteredMaterialId);
                string registeredMaterialId =
                    state.lastRegisteredMaterialId;
                Rebuild();
                if (playArrival)
                {
                    FindPainting(registeredMaterialId)?
                        .PlayRegistrationArrival();
                }
                return;
            }

            RefreshPaintings(state);
        }

        public static bool ShouldPlayRegistrationArrival(
            bool galleryBuilt,
            int registrationSerial,
            int handledSerial,
            string registeredMaterialId)
        {
            return galleryBuilt &&
                   registrationSerial > 0 &&
                   registrationSerial != handledSerial &&
                   !string.IsNullOrWhiteSpace(registeredMaterialId);
        }

        private CraftLiveMaterialPaintingView FindPainting(
            string materialId)
        {
            if (string.IsNullOrWhiteSpace(materialId))
            {
                return null;
            }

            foreach (CraftLiveMaterialPaintingView painting in paintings)
            {
                if (painting != null &&
                    painting.Material != null &&
                    painting.Material.MaterialId == materialId)
                {
                    return painting;
                }
            }

            return null;
        }

        private void RefreshPaintings(CraftLiveRoomState state)
        {
            if (state == null || session == null)
            {
                return;
            }

            foreach (CraftLiveMaterialPaintingView painting in paintings)
            {
                painting?.Refresh(state, session);
            }
        }

        private void ApplyLayout()
        {
            SetLocalPosition(
                bindings.PowerUpWall,
                new Vector3(
                    -columnSpacing,
                    columnVerticalPosition,
                    0f));
            SetLocalPosition(
                bindings.SkillWall,
                new Vector3(
                    0f,
                    columnVerticalPosition,
                    0f));
            SetLocalPosition(
                bindings.TypeWall,
                new Vector3(
                    columnSpacing,
                    columnVerticalPosition,
                    0f));
            SetLocalPosition(
                bindings.MaterialPreviewRoot,
                new Vector3(0f, 0f, -2.15f));
            SetLocalPosition(
                bindings.HologramInfoRoot,
                new Vector3(2.25f, -0.35f, -2.45f));
        }

        private void BuildColumn(
            Transform wallRoot,
            CraftLiveMaterialCategory category,
            string header)
        {
            if (wallRoot == null)
            {
                return;
            }

            List<CraftLiveMaterialDefinition> displayedMaterials =
                new List<CraftLiveMaterialDefinition>();
            foreach (CraftLiveMaterialDefinition material in
                     session.Catalog.Materials)
            {
                if (material == null ||
                    material.Category != category ||
                    !ShouldDisplayMaterial(
                        material,
                        session.State,
                        showLockedMaterials))
                {
                    continue;
                }

                displayedMaterials.Add(material);
            }

            categoryCounts[category] = displayedMaterials.Count;
            CraftLiveGalleryWallView preplacedWall =
                FindPreplacedWall(wallRoot);

            if (preplacedWall != null &&
                preplacedWall.TryBind(
                    this,
                    category,
                    displayedMaterials,
                    session.State,
                    session,
                    header,
                    visiblePaintings,
                    dragSensitivity,
                    mouseWheelStep,
                    paintings))
            {
                return;
            }

            if (preplacedWall != null && preplacedWall.HasUsableLayout)
            {
                Debug.LogError(
                    $"Craft-live: preplaced {category} wall could not " +
                    "display the current materials. Check its category and frame " +
                    "slot capacity.",
                    preplacedWall);
                if (preservePreplacedHierarchy)
                {
                    return;
                }
            }

            ColumnLayout layout = ResolveColumnLayout(wallRoot);

            GameObject generated = new GameObject(
                $"Generated_{category}_Column");
            generated.transform.SetParent(wallRoot, false);
            generated.transform.localPosition = layout.localCenter;
            generated.AddComponent<CraftLiveGeneratedRuntimeVisual>();

            GameObject backdrop = null;
            if (createFallbackWallBackdrop && !layout.hasWallBounds)
            {
                backdrop = CreateCube(
                    "WallBackdrop",
                    generated.transform,
                    new Vector3(0f, -0.05f, 0.55f),
                    new Vector3(
                        paintingSize.x + 0.35f,
                        8.2f,
                        0.16f),
                    wallColor,
                    true);
            }

            CraftLiveGalleryColumn column =
                generated.AddComponent<CraftLiveGalleryColumn>();
            GameObject contentObject = new GameObject("ScrollContent");
            contentObject.transform.SetParent(generated.transform, false);

            CreateText(
                generated.transform,
                "Header",
                header,
                new Vector3(
                    0f,
                    layout.viewportTop + 0.35f,
                    -0.05f),
                0.055f,
                Color.white);

            List<CraftLiveMaterialPaintingView> columnPaintings =
                new List<CraftLiveMaterialPaintingView>();
            int index = 0;
            foreach (CraftLiveMaterialDefinition material in
                     displayedMaterials)
            {
                CraftLiveMaterialPaintingView painting =
                    CreatePainting(
                        contentObject.transform,
                        material,
                        index,
                        layout);
                columnPaintings.Add(painting);
                paintings.Add(painting);
                index++;
            }

            column.Configure(
                contentObject.transform,
                columnPaintings,
                layout.paintingSpacing,
                visiblePaintings,
                dragSensitivity,
                mouseWheelStep);
            column.SetViewport(
                layout.viewportTop,
                layout.viewportBottom);

            // Keep a reference so the backdrop collider remains part of
            // the drag hierarchy even if the column is empty.
            if (backdrop != null &&
                backdrop.GetComponent<Collider>() == null)
            {
                backdrop.AddComponent<BoxCollider>();
            }
        }

        private CraftLiveMaterialPaintingView CreatePainting(
            Transform contentRoot,
            CraftLiveMaterialDefinition material,
            int index,
            ColumnLayout layout)
        {
            CraftLiveMaterialPaintingView painting;
            if (paintingPrefab != null)
            {
                painting = Instantiate(paintingPrefab, contentRoot);
                painting.name = $"Painting_{material.MaterialId}";
            }
            else
            {
                painting = CreateFallbackPainting(
                    contentRoot,
                    material,
                    layout.paintingSize);
            }

            painting.transform.localPosition =
                new Vector3(
                    0f,
                    layout.firstPaintingY -
                    index * layout.paintingSpacing,
                    0f);
            painting.transform.localRotation = Quaternion.identity;
            painting.CaptureRestingTransform();
            // QR-unlocked paintings can come from differently configured
            // prefabs. Keep every painting on the authored wall plane, just
            // like the base materials, instead of moving it toward the camera.
            painting.UseFixedGalleryPosition();
            painting.Bind(this, material);
            return painting;
        }

        private CraftLiveMaterialPaintingView CreateFallbackPainting(
            Transform parent,
            CraftLiveMaterialDefinition material,
            Vector2 resolvedPaintingSize)
        {
            GameObject root = new GameObject(
                $"Painting_{material.MaterialId}");
            root.transform.SetParent(parent, false);
            CraftLiveMaterialPaintingView painting =
                root.AddComponent<CraftLiveMaterialPaintingView>();

            float nameTextY = -resolvedPaintingSize.y * 0.34f;
            float stateTextY = resolvedPaintingSize.y * 0.38f;
            float nameCharacterSize = Mathf.Clamp(
                resolvedPaintingSize.y * 0.02f,
                0.018f,
                0.035f);
            float stateCharacterSize = Mathf.Clamp(
                resolvedPaintingSize.y * 0.016f,
                0.014f,
                0.028f);

            GameObject frame = CreateCube(
                "Frame",
                root.transform,
                Vector3.zero,
                new Vector3(
                    resolvedPaintingSize.x,
                    resolvedPaintingSize.y,
                    0.18f),
                frameColor,
                false);
            GameObject art = CreateCube(
                "Art",
                root.transform,
                new Vector3(0f, 0.12f, -0.12f),
                new Vector3(
                    Mathf.Max(0.05f, resolvedPaintingSize.x - 0.28f),
                    Mathf.Max(0.05f, resolvedPaintingSize.y - 0.48f),
                    0.08f),
                material.EffectColor,
                false);

            SpriteRenderer iconRenderer = null;
            if (material.Icon != null)
            {
                GameObject iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(root.transform, false);
                iconObject.transform.localPosition =
                    new Vector3(0f, 0.15f, -0.2f);
                iconObject.transform.localScale =
                    Vector3.one * 0.9f;
                iconRenderer =
                    iconObject.AddComponent<SpriteRenderer>();
                iconRenderer.sprite = material.Icon;
                iconRenderer.color = Color.white;
            }

            TextMesh nameText = CreateText(
                root.transform,
                "MaterialName",
                material.DisplayName,
                new Vector3(0f, nameTextY, -0.23f),
                nameCharacterSize,
                Color.white);
            TextMesh stateText = CreateText(
                root.transform,
                "MaterialState",
                string.Empty,
                new Vector3(0f, stateTextY, -0.23f),
                stateCharacterSize,
                Color.white);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                resolvedPaintingSize.x,
                resolvedPaintingSize.y,
                0.3f);
            painting.ConfigureFallbackVisuals(
                root.transform,
                new[]
                {
                    frame.GetComponent<Renderer>(),
                    art.GetComponent<Renderer>()
                },
                new Collider[] { collider },
                nameText,
                stateText);
            return painting;
        }

        private ColumnLayout ResolveColumnLayout(Transform wallRoot)
        {
            if (fitToWallBounds &&
                TryGetWallBounds(wallRoot, out Bounds wallBounds))
            {
                float availableWidth = Mathf.Max(
                    0.1f,
                    wallBounds.size.x -
                    wallHorizontalPadding * 2f);
                float availableHeight = Mathf.Max(
                    0.1f,
                    wallBounds.size.y -
                    wallVerticalPadding * 2f);
                int count = Mathf.Max(1, visiblePaintings);
                float gap = Mathf.Min(
                    paintingGap,
                    availableHeight / count);
                float height = Mathf.Max(
                    0.1f,
                    (availableHeight - gap * (count - 1)) /
                    count);
                float top = wallBounds.size.y * 0.5f -
                            wallVerticalPadding;
                float cameraLocalZ = targetCamera != null
                    ? wallRoot.InverseTransformPoint(
                        targetCamera.transform.position).z
                    : wallBounds.center.z - wallBounds.size.z;
                float frontDirection = cameraLocalZ >= wallBounds.center.z
                    ? 1f
                    : -1f;
                Vector3 localCenter = wallBounds.center;
                localCenter.z +=
                    frontDirection *
                    (wallBounds.size.z * 0.5f + wallFrontOffset);

                return new ColumnLayout
                {
                    paintingSize = new Vector2(
                        availableWidth,
                        height),
                    paintingSpacing = height + gap,
                    viewportTop = top,
                    viewportBottom =
                        -wallBounds.size.y * 0.5f +
                        wallVerticalPadding,
                    firstPaintingY = top - height * 0.5f,
                    localCenter = localCenter,
                    hasWallBounds = true
                };
            }

            return new ColumnLayout
            {
                paintingSize = paintingSize,
                paintingSpacing = paintingSpacing,
                viewportTop = 3.25f,
                viewportBottom = -3.25f,
                firstPaintingY = 2.35f,
                localCenter = Vector3.zero,
                hasWallBounds = false
            };
        }

        private bool HasAnyWallBounds()
        {
            return TryGetWallBounds(bindings.PowerUpWall, out _) ||
                   TryGetWallBounds(bindings.SkillWall, out _) ||
                   TryGetWallBounds(bindings.TypeWall, out _);
        }

        private static bool TryGetWallBounds(
            Transform wallRoot,
            out Bounds localBounds)
        {
            localBounds = default;
            if (wallRoot == null)
            {
                return false;
            }

            Bounds worldBounds = default;
            bool hasBounds = false;
            foreach (Renderer target in
                     wallRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (target == null ||
                    target.GetComponentInParent<
                        CraftLiveGeneratedRuntimeVisual>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    worldBounds = target.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(target.bounds);
                }
            }

            foreach (Collider target in
                     wallRoot.GetComponentsInChildren<Collider>(true))
            {
                if (target == null ||
                    target.GetComponentInParent<
                        CraftLiveGeneratedRuntimeVisual>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    worldBounds = target.bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(target.bounds);
                }
            }

            if (!hasBounds ||
                worldBounds.size.x <= Mathf.Epsilon ||
                worldBounds.size.y <= Mathf.Epsilon)
            {
                return false;
            }

            Vector3 worldMin = worldBounds.min;
            Vector3 worldMax = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(worldMin.x, worldMin.y, worldMin.z),
                new Vector3(worldMin.x, worldMin.y, worldMax.z),
                new Vector3(worldMin.x, worldMax.y, worldMin.z),
                new Vector3(worldMin.x, worldMax.y, worldMax.z),
                new Vector3(worldMax.x, worldMin.y, worldMin.z),
                new Vector3(worldMax.x, worldMin.y, worldMax.z),
                new Vector3(worldMax.x, worldMax.y, worldMin.z),
                new Vector3(worldMax.x, worldMax.y, worldMax.z)
            };

            localBounds = new Bounds(
                wallRoot.InverseTransformPoint(corners[0]),
                Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                localBounds.Encapsulate(
                    wallRoot.InverseTransformPoint(corners[i]));
            }

            return localBounds.size.x > Mathf.Epsilon &&
                   localBounds.size.y > Mathf.Epsilon;
        }

        private void ClearGeneratedColumns()
        {
            if (bindings == null)
            {
                return;
            }

            ClearGeneratedChildren(bindings.PowerUpWall);
            ClearGeneratedChildren(bindings.SkillWall);
            ClearGeneratedChildren(bindings.TypeWall);
        }

        private void ClearPreplacedWallBindings()
        {
            if (bindings == null)
            {
                return;
            }

            ClearPreplacedWall(bindings.PowerUpWall);
            ClearPreplacedWall(bindings.SkillWall);
            ClearPreplacedWall(bindings.TypeWall);
        }

        private static void ClearPreplacedWall(Transform root)
        {
            FindPreplacedWall(root)?.ClearBindings();
        }

        private void PreserveAuthoredGalleryLayout()
        {
            DisableLegacyWallCarousel();
        }

        private void DisableLegacyWallCarousel()
        {
            if (wallSlider == null)
            {
                return;
            }

            wallSlider.enabled = false;
        }

        private static CraftLiveGalleryWallView FindPreplacedWall(
            Transform root)
        {
            if (root == null)
            {
                return null;
            }

            CraftLiveGalleryWallView wall =
                root.GetComponent<CraftLiveGalleryWallView>();
            return wall != null
                ? wall
                : root.GetComponentInChildren<
                    CraftLiveGalleryWallView>(true);
        }

        private static void ClearGeneratedChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            CraftLiveGeneratedRuntimeVisual[] generated =
                root.GetComponentsInChildren<
                    CraftLiveGeneratedRuntimeVisual>(true);
            foreach (CraftLiveGeneratedRuntimeVisual visual in generated)
            {
                if (visual == null || visual.transform.parent != root)
                {
                    continue;
                }

                DestroySafely(visual.gameObject);
            }
        }

        private static GameObject CreateCube(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            bool keepCollider)
        {
            GameObject cube = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = localScale;
            ApplyRendererColor(cube.GetComponent<Renderer>(), color);
            if (!keepCollider)
            {
                Collider collider = cube.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroySafely(collider);
                }
            }

            return cube;
        }

        private static TextMesh CreateText(
            Transform parent,
            string name,
            string value,
            Vector3 localPosition,
            float characterSize,
            Color color)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = localPosition;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                text,
                characterSize,
                color);
            return text;
        }

        private static void ApplyRendererColor(
            Renderer target,
            Color color)
        {
            if (target == null)
            {
                return;
            }

            CraftLiveForgeUITheme.ApplyForgeSurface(target, color);
        }

        private static void SetLocalPosition(
            Transform target,
            Vector3 position)
        {
            if (target != null)
            {
                target.localPosition = position;
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
            columnSpacing = Mathf.Max(1f, columnSpacing);
            paintingSpacing = Mathf.Max(0.5f, paintingSpacing);
            visiblePaintings = Mathf.Max(1, visiblePaintings);
            dragSensitivity = Mathf.Max(0.001f, dragSensitivity);
            mouseWheelStep = Mathf.Max(0.01f, mouseWheelStep);
            paintingSize.x = Mathf.Max(0.5f, paintingSize.x);
            paintingSize.y = Mathf.Max(0.5f, paintingSize.y);
            wallHorizontalPadding = Mathf.Max(
                0f,
                wallHorizontalPadding);
            wallVerticalPadding = Mathf.Max(
                0f,
                wallVerticalPadding);
            paintingGap = Mathf.Max(0f, paintingGap);
            wallFrontOffset = Mathf.Max(0f, wallFrontOffset);
        }
    }
}
