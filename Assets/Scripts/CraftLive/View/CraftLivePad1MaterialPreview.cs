using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad1MaterialPreview : MonoBehaviour
    {
        private static readonly CraftLiveSlotId[] TransferTestSlots =
        {
            CraftLiveSlotId.Top,
            CraftLiveSlotId.Left,
            CraftLiveSlotId.Right,
            CraftLiveSlotId.Bottom,
            CraftLiveSlotId.Skill,
            CraftLiveSlotId.Attribute
        };

        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad1Bindings bindings;

        [Header("Model")]
        [SerializeField] private bool useMaterialWorldPrefab = true;
        [SerializeField] private bool createPlaceholderWhenMissing = true;
        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Pad1で表示する全素材モデルの基準サイズです。")]
        private float targetModelSize = 0.9f;
        [SerializeField]
        [Tooltip("全素材モデルへ共通で加えるローカル位置補正です。")]
        private Vector3 modelPositionOffset = Vector3.zero;
        [SerializeField]
        [Tooltip("素材モデルの画面内位置。中央より少し上を既定にします。")]
        private Vector2 modelViewportPosition = new Vector2(0.5f, 0.82f);
        [SerializeField] private Vector3 modelRotation =
            new Vector3(10f, -20f, 0f);
        [SerializeField, Min(0f)] private float modelCameraApproach = 0.55f;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 18f;
        [SerializeField, Min(0.05f)] private float revealDuration = 0.28f;

        [Header("Fallback Hologram")]
        [SerializeField] private bool createFallbackHologram = true;
        [SerializeField] private Color hologramColor =
            new Color(0.08f, 0.72f, 0.82f, 1f);
        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("素材説明ホログラム背景の不透明度です。0.9でわずかに透けつつ文字を読みやすくします。")]
        private float hologramPanelOpacity = 0.9f;
        [SerializeField, Range(0.5f, 6f)]
        private float hologramBorderGlow = 2.8f;
        [SerializeField, Range(0.005f, 0.08f)]
        private float hologramBorderThickness = 0.025f;
        [SerializeField]
        [Tooltip("自動計算したホログラム板の幅・高さへ掛ける倍率です。各軸10倍まで指定できます。")]
        private Vector2 hologramPanelSizeMultiplier = Vector2.one;
        [SerializeField, Min(0f)]
        [Tooltip("絵画より手前へ配置するため、アンカーからカメラ方向へ寄せる距離です。")]
        private float hologramCameraApproach = 0.85f;
        [SerializeField]
        [Tooltip("SpriteRendererを含む透明オブジェクト間の描画順です。")]
        private int hologramSortingOrder = 20;
        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("パネルとボタンを含むホログラム全体の倍率です。")]
        private float hologramGroupScale = 1f;
        [SerializeField]
        [Tooltip("カメラ基準のホログラム全体位置補正です。zで奥行きを調整します。")]
        private Vector3 hologramGroupPositionOffset = Vector3.zero;
        [SerializeField, Range(0f, 0.2f)]
        private float hologramScreenMargin = 0.035f;

        [Header("Hologram Display Area")]
        [SerializeField]
        [Tooltip("ホログラム表示面の左上です。4点すべてを設定すると、この面が画面内の定義として優先されます。")]
        private Transform displayAreaTopLeft;
        [SerializeField]
        [Tooltip("ホログラム表示面の右上です。")]
        private Transform displayAreaTopRight;
        [SerializeField]
        [Tooltip("ホログラム表示面の右下です。")]
        private Transform displayAreaBottomRight;
        [SerializeField]
        [Tooltip("ホログラム表示面の左下です。")]
        private Transform displayAreaBottomLeft;
        [SerializeField, Range(0f, 0.2f)]
        [Tooltip("4点で囲んだ表示面の内側に確保する余白です。画面幅・高さを1とした比率で指定します。")]
        private float displayAreaPadding = 0.02f;
        [SerializeField, Range(-3f, 3f)]
        [Tooltip("4点で定義した面からの奥行き補正です。正の値でカメラ側、負の値で面の奥側へ移動します。")]
        private float displayAreaDepthOffset;
        [SerializeField]
        private Color displayAreaGizmoColor =
            new Color(0.1f, 0.9f, 1f, 0.8f);

        [Header("Hologram Text")]
        [SerializeField] private Font hologramFont;
        [SerializeField, Range(8, 256)]
        [Tooltip("生成するTextMeshへ直接設定するフォントサイズです。")]
        private int hologramGeneratedFontSize = 64;
        [FormerlySerializedAs("hologramFontSize")]
        [SerializeField, Range(0.008f, 0.05f)]
        [Tooltip("板からはみ出さない範囲で使用する最大文字サイズです。")]
        private float hologramMaxCharacterSize = 0.045f;
        [SerializeField] private Color hologramTextColor = Color.white;

        [Header("Transfer Button")]
        [SerializeField] private bool createTransferButton = true;
        [SerializeField]
        [Tooltip("Unity EditorのPlay Mode時だけ、空き枠を内部的に仮設定して転送待ちへ追加できます。発射は既存のばね操作で行います。")]
        private bool allowTransferWithoutPlacementForPlayTest;
        [SerializeField, Range(0.12f, 0.5f)]
        private float transferButtonHeight = 0.2f;
        [SerializeField] private bool createReturnButton = true;
        [SerializeField, Range(0.05f, 0.8f)]
        private float hologramButtonOpacity = 0.3f;
        [Header("Return Button")]
        [SerializeField]
        [Tooltip("説明パネルの実寸に対する戻るボタンの幅・高さ比です。")]
        private Vector2 returnButtonSize = new Vector2(0.55f, 0.18f);
        [SerializeField] private Vector3 returnButtonPositionOffset;

        [Header("Button Text")]
        [FormerlySerializedAs("transferButtonFontSize")]
        [SerializeField, Range(8, 256)]
        private int buttonFontSize = 64;
        [FormerlySerializedAs("transferButtonTextSize")]
        [SerializeField, Range(0.1f, 2f)]
        private float buttonTextSize = 0.9f;

        [Header("Dismiss")]
        [SerializeField]
        [Tooltip("ホログラム、モデル、選択中の額縁以外をタップすると選択を閉じます。")]
        private bool dismissOnOutsideTap;
        [SerializeField, Min(1f)] private float outsideTapMaxDistance = 100f;

        [Header("UI Events")]
        [SerializeField] private UnityEvent<bool> onDetailsVisible;
        [SerializeField] private UnityEvent<Sprite> onIconChanged;
        [SerializeField] private UnityEvent<string> onNameChanged;
        [SerializeField] private UnityEvent<string> onCategoryChanged;
        [SerializeField] private UnityEvent<string> onDescriptionChanged;
        [SerializeField] private UnityEvent<string> onAbilityChanged;
        [SerializeField] private UnityEvent<string> onUsageChanged;
        [SerializeField] private UnityEvent<string> onDetailTextChanged;
        [SerializeField] private UnityEvent<Color> onThemeColorChanged;

        private GameObject spawnedModel;
        private Material generatedFirePreviewMaterial;
        private GameObject fallbackHologram;
        private Transform fallbackPanel;
        private Renderer fallbackPanelRenderer;
        private readonly Transform[] fallbackBorderEdges =
            new Transform[4];
        private Material fallbackPanelMaterial;
        private Material fallbackBorderMaterial;
        private Material fallbackTransferButtonMaterial;
        private Material fallbackReturnButtonMaterial;
        private TextMesh fallbackText;
        private Color readableHologramTextColor =
            new Color(1f, 0.94f, 0.72f, 1f);
        private Transform fallbackTransferButton;
        private Renderer fallbackTransferButtonRenderer;
        private CraftLiveWorldButton fallbackTransferWorldButton;
        private TextMesh fallbackTransferButtonText;
        private Transform fallbackReturnButton;
        private Renderer fallbackReturnButtonRenderer;
        private CraftLiveWorldButton fallbackReturnWorldButton;
        private TextMesh fallbackReturnButtonText;
        private float fallbackPanelWidth = 1f;
        private float fallbackPanelHeight = 1f;
        private Transform selectionAnchor;
        private CraftLiveMaterialPaintingView suppressedPainting;
        private string selectionAnchorMaterialId = string.Empty;
        private string displayedMaterialId = string.Empty;
        private bool detailsOnlyWhileTransferWaiting;
        private Coroutine revealCoroutine;
        private Coroutine returnCoroutine;
        private Vector3 revealHiddenLocalPosition;
        private Vector3 revealHiddenScale;

        public string DisplayedMaterialId => displayedMaterialId;
        public GameObject SpawnedModel => spawnedModel;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (session != null)
            {
                session.StateChanged += Refresh;
                Refresh(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }

            StopReveal();
            StopReturn();
            RestoreSuppressedPainting();
        }

        private void OnDestroy()
        {
            DestroyRuntimeMaterial(fallbackPanelMaterial);
            DestroyRuntimeMaterial(fallbackBorderMaterial);
            DestroyRuntimeMaterial(fallbackTransferButtonMaterial);
            DestroyRuntimeMaterial(fallbackReturnButtonMaterial);
        }

        public void Refresh(CraftLiveRoomState state)
        {
            ResolveReferences();
            string materialId = state != null
                ? state.selectedMaterialId
                : string.Empty;

            if (string.IsNullOrEmpty(materialId) &&
                detailsOnlyWhileTransferWaiting &&
                CraftLivePad1GalleryController.IsWaitingForSingleTransfer(state))
            {
                CraftLiveMaterialDefinition detailsMaterial =
                    session != null && session.Catalog != null
                        ? session.Catalog.FindMaterial(displayedMaterialId)
                        : null;
                if (detailsMaterial != null)
                {
                    Publish(detailsMaterial, true);
                    return;
                }
            }

            detailsOnlyWhileTransferWaiting = false;

            CraftLiveMaterialDefinition material =
                state != null && session != null &&
                session.Catalog != null
                    ? session.Catalog.FindMaterial(
                        materialId)
                    : null;

            if (material == null)
            {
                ClearPreview();
                Publish(null, false);
                return;
            }

            if (displayedMaterialId != material.MaterialId)
            {
                SpawnPreview(material);
            }

            Publish(material, true);
        }

        public void SetSelectionAnchor(
            CraftLiveMaterialDefinition material,
            Transform target)
        {
            selectionAnchor = target;
            selectionAnchorMaterialId = material != null
                ? material.MaterialId
                : string.Empty;
        }

        public void ShowDetailsWithoutPlacement(
            CraftLiveMaterialDefinition material)
        {
            if (material == null)
            {
                return;
            }

            detailsOnlyWhileTransferWaiting = true;
            if (displayedMaterialId != material.MaterialId)
            {
                SpawnPreview(material);
            }

            Publish(material, true);
        }

        public void ClearPreview()
        {
            detailsOnlyWhileTransferWaiting = false;
            displayedMaterialId = string.Empty;
            selectionAnchor = null;
            selectionAnchorMaterialId = string.Empty;
            ClearVisuals();
        }

        private void ClearVisuals()
        {
            StopReveal();
            StopReturn();
            RestoreSuppressedPainting();
            if (spawnedModel != null)
            {
                Destroy(spawnedModel);
                spawnedModel = null;
            }

            if (generatedFirePreviewMaterial != null)
            {
                Destroy(generatedFirePreviewMaterial);
                generatedFirePreviewMaterial = null;
            }

            if (fallbackHologram != null)
            {
                fallbackHologram.SetActive(false);
            }
            if (fallbackReturnButton != null)
            {
                fallbackReturnButton.gameObject.SetActive(false);
            }
        }

        private void SuppressSelectionPainting(Transform anchor)
        {
            RestoreSuppressedPainting();
            if (anchor == null)
            {
                return;
            }

            suppressedPainting =
                anchor.GetComponentInParent<CraftLiveMaterialPaintingView>();
            if (suppressedPainting != null)
            {
                suppressedPainting.SetPreviewSuppressed(true);
            }
        }

        private void RestoreSuppressedPainting()
        {
            if (suppressedPainting == null)
            {
                return;
            }

            suppressedPainting.SetPreviewSuppressed(false);
            suppressedPainting = null;
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
        }

        private void SpawnPreview(
            CraftLiveMaterialDefinition material)
        {
            ClearVisuals();
            if (bindings == null ||
                bindings.MaterialPreviewRoot == null)
            {
                return;
            }

            Camera presentationCamera = ResolvePresentationCamera();
            Transform resolvedAnchor = ResolveSelectionAnchor(material);
            Vector3 revealStartPosition = PositionPresentationRoots(
                presentationCamera,
                resolvedAnchor);
            SuppressSelectionPainting(resolvedAnchor);

            if (useMaterialWorldPrefab &&
                material.WorldPrefab != null)
            {
                spawnedModel = new GameObject(
                    $"Preview_{material.MaterialId}");
                spawnedModel.transform.SetParent(
                    bindings.MaterialPreviewRoot,
                    false);
                GameObject content = Instantiate(
                    material.WorldPrefab,
                    spawnedModel.transform);
                CraftLiveForgeUITheme.EnsureCompatibleSurfaces(content);
                PrepareParticlePreview(content);
                bool fitted = CraftLiveRuntimeVisualUtility.FitAndCenter(
                    content.transform,
                    targetModelSize * material.Pad1PreviewScale,
                    false);
                if (!fitted || IsParticleOnlyVisual(content))
                {
                    CreateParticlePreviewCore(
                        spawnedModel.transform,
                        material.EffectColor,
                        targetModelSize * material.Pad1PreviewScale);
                }


                if (material.ElementEffect.type ==
                    CraftLiveElementType.Fire)
                {
                    CreateReliableFirePreview(
                        spawnedModel.transform,
                        material.EffectColor,
                        targetModelSize * material.Pad1PreviewScale);
                }
            }
            else if (createPlaceholderWhenMissing)
            {
                spawnedModel = new GameObject(
                    $"Preview_{material.MaterialId}");
                spawnedModel.transform.SetParent(
                    bindings.MaterialPreviewRoot,
                    false);
                GameObject content = GameObject.CreatePrimitive(
                    CraftLivePad1Presentation.GetPlaceholderPrimitive(
                        material));
                content.transform.SetParent(
                    spawnedModel.transform,
                    false);
                Collider collider =
                    content.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                ApplyColor(
                    content.GetComponent<Renderer>(),
                    material.EffectColor,
                    true);
                CraftLiveRuntimeVisualUtility.FitAndCenter(
                    content.transform,
                    targetModelSize * material.Pad1PreviewScale,
                    false);
            }

            displayedMaterialId = material.MaterialId;
            CraftLiveAudio.Play(
                material.RequiresQrUnlock
                    ? CraftLiveSound.RareReveal
                    : CraftLiveSound.Description,
                0.68f);
            if (spawnedModel == null)
            {
                RestoreSuppressedPainting();
                return;
            }

            spawnedModel.name =
                $"Preview_{material.MaterialId}";
            spawnedModel.transform.localPosition =
                modelPositionOffset + material.Pad1PreviewOffset;
            spawnedModel.transform.localRotation =
                Quaternion.Euler(modelRotation);

            CraftLivePreviewSpin spin =
                spawnedModel.GetComponent<CraftLivePreviewSpin>();
            if (spin == null)
            {
                spin = spawnedModel.AddComponent<CraftLivePreviewSpin>();
            }

            spin.Configure(spinDegreesPerSecond);
            spin.enabled = false;
            Vector3 finalScale = spawnedModel.transform.localScale;
            Vector3 finalPosition = spawnedModel.transform.localPosition;
            spawnedModel.transform.localScale = finalScale * 0.15f;
            spawnedModel.transform.position = revealStartPosition;
            revealHiddenLocalPosition = spawnedModel.transform.localPosition;
            revealHiddenScale = spawnedModel.transform.localScale;
            revealCoroutine = StartCoroutine(
                Reveal(
                    spawnedModel.transform,
                    finalPosition,
                    finalScale,
                    spin));
        }

        private void Publish(
            CraftLiveMaterialDefinition material,
            bool visible)
        {
            CraftLivePad1GalleryController gallery =
                GetComponent<CraftLivePad1GalleryController>();
            if (gallery != null)
            {
                // Gallery category signs are permanent navigation landmarks.
                // Keep all three visible while the material model and its
                // supplemental hologram are open.
                gallery.SetSpecialHeadersVisible(true);
            }

            onDetailsVisible?.Invoke(visible);
            if (material == null)
            {
                onIconChanged?.Invoke(null);
                onNameChanged?.Invoke(string.Empty);
                onCategoryChanged?.Invoke(string.Empty);
                onDescriptionChanged?.Invoke(string.Empty);
                onAbilityChanged?.Invoke(string.Empty);
                onUsageChanged?.Invoke(string.Empty);
                onDetailTextChanged?.Invoke(string.Empty);
                return;
            }

            string details =
                CraftLivePad1Presentation.BuildDetailText(material);
            onIconChanged?.Invoke(material.Icon);
            onNameChanged?.Invoke(material.DisplayName);
            onCategoryChanged?.Invoke(
                CraftLivePad1Presentation.GetCategoryLabel(
                    material.Category));
            onDescriptionChanged?.Invoke(material.Description);
            onAbilityChanged?.Invoke(material.AbilitySummary);
            onUsageChanged?.Invoke(material.UsageSummary);
            onDetailTextChanged?.Invoke(details);
            onThemeColorChanged?.Invoke(material.EffectColor);
            RefreshFallbackHologram(
                details,
                material.Pad1HologramColor,
                material);
        }

        private void RefreshFallbackHologram(
            string details,
            Color themeColor,
            CraftLiveMaterialDefinition material)
        {
            if (!createFallbackHologram ||
                bindings == null ||
                bindings.HologramInfoRoot == null)
            {
                return;
            }

            if (fallbackHologram == null)
            {
                fallbackHologram = new GameObject(
                    "Generated_FallbackHologram");
                fallbackHologram.transform.SetParent(
                    bindings.HologramInfoRoot,
                    false);
                fallbackHologram.AddComponent<
                    CraftLiveGeneratedRuntimeVisual>();

                GameObject panel = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                panel.name = "Panel";
                panel.transform.SetParent(
                    fallbackHologram.transform,
                    false);
                fallbackPanel = panel.transform;
                fallbackPanelRenderer = panel.GetComponent<Renderer>();
                ConfigureHologramRenderer(
                    fallbackPanelRenderer,
                    hologramSortingOrder);
                fallbackPanelMaterial =
                    CreateHologramPanelMaterial();
                if (fallbackPanelRenderer != null &&
                    fallbackPanelMaterial != null)
                {
                    fallbackPanelRenderer.sharedMaterial =
                        fallbackPanelMaterial;
                }
                fallbackBorderMaterial = CreateHologramMaterial();
                for (int i = 0; i < fallbackBorderEdges.Length; i++)
                {
                    fallbackBorderEdges[i] = CreateHologramBorderEdge(
                        fallbackHologram.transform,
                        $"Border_{i}");
                }

                GameObject textObject = new GameObject("Details");
                textObject.transform.SetParent(
                    fallbackHologram.transform,
                    false);
                textObject.transform.localPosition =
                    new Vector3(0f, 0f, -0.08f);
                fallbackText = textObject.AddComponent<TextMesh>();
                fallbackText.anchor = TextAnchor.MiddleCenter;
                fallbackText.alignment = TextAlignment.Center;
                CraftLiveForgeUITheme.StyleText(
                    fallbackText,
                    0.028f,
                    hologramTextColor);
                CraftLiveForgeUITheme.ApplyCrispTextMetrics(
                    fallbackText,
                    0.028f);
                SetRendererSortingOrder(
                    fallbackText.GetComponent<Renderer>(),
                    hologramSortingOrder + 2);

                if (createTransferButton)
                {
                    CreateFallbackTransferButton();
                }
                if (createReturnButton)
                {
                    CreateFallbackReturnButton();
                }
            }

            if (createTransferButton && fallbackTransferButton == null)
            {
                CreateFallbackTransferButton();
            }
            if (createReturnButton && fallbackReturnButton == null)
            {
                CreateFallbackReturnButton();
            }

            if (fallbackTransferButton != null)
            {
                fallbackTransferButton.gameObject.SetActive(
                    createTransferButton);
            }
            if (fallbackReturnButton != null)
            {
                fallbackReturnButton.gameObject.SetActive(
                    createReturnButton);
            }

            fallbackHologram.SetActive(true);
            if (fallbackReturnButton != null)
            {
                fallbackReturnButton.gameObject.SetActive(createReturnButton);
            }
            Camera presentationCamera = ResolvePresentationCamera();
            Transform resolvedAnchor = ResolveSelectionAnchorById(
                displayedMaterialId);
            PositionPresentationRoots(
                presentationCamera,
                resolvedAnchor);
            UpdateHologramColors(themeColor);
            UpdateHologramText(details);
            UpdateTransferButton(material, themeColor);
            UpdateReturnButton(themeColor);
            PositionPresentationRoots(
                presentationCamera,
                resolvedAnchor);
        }

        private Camera ResolvePresentationCamera()
        {
            Camera result = Camera.main;
            if (result == null)
            {
                result = FindAnyObjectByType<Camera>();
            }

            return result;
        }

        private Transform ResolveSelectionAnchor(
            CraftLiveMaterialDefinition material)
        {
            return ResolveSelectionAnchorById(
                material != null ? material.MaterialId : string.Empty);
        }

        private Transform ResolveSelectionAnchorById(string materialId)
        {
            if (selectionAnchor != null &&
                selectionAnchorMaterialId == materialId)
            {
                return selectionAnchor;
            }

            CraftLivePad1GalleryController gallery =
                GetComponent<CraftLivePad1GalleryController>();
            Transform resolved = gallery != null
                ? gallery.FindMaterialAnchor(materialId)
                : null;
            if (resolved != null)
            {
                selectionAnchor = resolved;
                selectionAnchorMaterialId = materialId;
            }

            return resolved;
        }

        private Vector3 PositionPresentationRoots(
            Camera presentationCamera,
            Transform resolvedAnchor)
        {
            Transform modelRoot = bindings != null
                ? bindings.MaterialPreviewRoot
                : null;
            Transform hologramRoot = bindings != null
                ? bindings.HologramInfoRoot
                : null;
            if (presentationCamera == null)
            {
                return resolvedAnchor != null
                    ? resolvedAnchor.position
                    : modelRoot != null
                        ? modelRoot.position
                        : transform.position;
            }

            float minimumDepth =
                presentationCamera.nearClipPlane + 0.35f;
            Vector3 anchorPosition;
            float anchorDepth;
            Vector3 anchorViewport;
            if (resolvedAnchor != null)
            {
                anchorPosition = resolvedAnchor.position;
                anchorViewport = presentationCamera.WorldToViewportPoint(
                    anchorPosition);
                anchorDepth = anchorViewport.z;
            }
            else
            {
                anchorDepth = 2f;
                anchorViewport = new Vector3(0.42f, 0.5f, anchorDepth);
                anchorPosition = presentationCamera.ViewportToWorldPoint(
                    anchorViewport);
            }

            if (anchorDepth < minimumDepth)
            {
                anchorDepth = Mathf.Max(2f, minimumDepth);
                anchorViewport = new Vector3(0.42f, 0.5f, anchorDepth);
                anchorPosition = presentationCamera.ViewportToWorldPoint(
                    anchorViewport);
            }

            Vector3 modelPosition = presentationCamera.ViewportToWorldPoint(
                new Vector3(
                    Mathf.Clamp(modelViewportPosition.x, 0.05f, 0.95f),
                    Mathf.Clamp(modelViewportPosition.y, 0.05f, 0.95f),
                    Mathf.Max(minimumDepth,
                        anchorDepth - modelCameraApproach)));
            if (modelRoot != null)
            {
                modelRoot.SetPositionAndRotation(
                    modelPosition,
                    presentationCamera.transform.rotation);
            }

            if (hologramRoot != null)
            {
                bool hasDisplayArea =
                    TryGetDisplayAreaViewport(
                        presentationCamera,
                        out Vector2[] displayAreaViewport,
                        out float displayAreaDepth);
                float hologramDepth = Mathf.Max(
                    minimumDepth,
                    (hasDisplayArea
                        ? displayAreaDepth - displayAreaDepthOffset
                        : anchorDepth - hologramCameraApproach) +
                    hologramGroupPositionOffset.z);
                float hologramX = modelViewportPosition.x <= 0.5f
                    ? 0.78f
                    : 0.22f;
                float hologramY = modelViewportPosition.y >= 0.5f
                    ? 0.2f
                    : 0.7f;
                Vector3 desiredPosition =
                    presentationCamera.ViewportToWorldPoint(
                        new Vector3(
                            hologramX,
                            hologramY,
                            hologramDepth));
                desiredPosition +=
                    presentationCamera.transform.right *
                    hologramGroupPositionOffset.x +
                    presentationCamera.transform.up *
                    hologramGroupPositionOffset.y;
                Vector3 desiredViewport =
                    presentationCamera.WorldToViewportPoint(
                        desiredPosition);
                desiredViewport.z = hologramDepth;
                ResizeFallbackHologram(
                    presentationCamera,
                    hologramDepth);
                // The scene's four display anchors describe the gallery, not
                // a safe detail panel region. On portrait displays they pull
                // the panel into the model, so fit against the active camera
                // viewport instead.
                float fittedGroupScale = FitHologramGroupInsideScreen(
                    presentationCamera,
                    hologramDepth,
                    ref desiredViewport);
                Vector3 hologramPosition =
                    presentationCamera.ViewportToWorldPoint(
                        desiredViewport);
                hologramRoot.SetPositionAndRotation(
                    hologramPosition,
                    presentationCamera.transform.rotation);
                if (fallbackHologram != null)
                {
                    fallbackHologram.transform.localPosition = Vector3.zero;
                    fallbackHologram.transform.localRotation =
                        Quaternion.identity;
                    fallbackHologram.transform.localScale =
                        Vector3.one * fittedGroupScale;
                    ConstrainRenderedHologramInsideVisibleArea(
                        presentationCamera,
                        hologramRoot,
                        hologramDepth,
                        null);
                    PositionReturnButton();
                }
            }

            return anchorPosition;
        }

        private void ResizeFallbackHologram(
            Camera presentationCamera,
            float depth)
        {
            if (fallbackPanel == null || presentationCamera == null)
            {
                return;
            }

            float worldHeight = presentationCamera.orthographic
                ? presentationCamera.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(
                    presentationCamera.fieldOfView *
                    0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * presentationCamera.aspect;
            float panelWidth = Mathf.Clamp(
                worldWidth * 0.4f *
                Mathf.Max(0.1f, hologramPanelSizeMultiplier.x),
                0.25f,
                10f);
            float panelHeight = Mathf.Clamp(
                worldHeight * 0.66f *
                Mathf.Max(0.1f, hologramPanelSizeMultiplier.y),
                0.25f,
                10f);
            fallbackPanel.localScale =
                new Vector3(panelWidth, panelHeight, 0.06f);
            fallbackPanelWidth = panelWidth;
            fallbackPanelHeight = panelHeight;
            float thickness = Mathf.Min(
                hologramBorderThickness,
                Mathf.Min(panelWidth, panelHeight) * 0.08f);
            SetBorderEdge(
                0,
                new Vector3(-panelWidth * 0.5f, 0f, -0.04f),
                new Vector3(thickness, panelHeight + thickness, 0.035f));
            SetBorderEdge(
                1,
                new Vector3(panelWidth * 0.5f, 0f, -0.04f),
                new Vector3(thickness, panelHeight + thickness, 0.035f));
            SetBorderEdge(
                2,
                new Vector3(0f, panelHeight * 0.5f, -0.04f),
                new Vector3(panelWidth + thickness, thickness, 0.035f));
            SetBorderEdge(
                3,
                new Vector3(0f, -panelHeight * 0.5f, -0.04f),
                new Vector3(panelWidth + thickness, thickness, 0.035f));

            ResizeTransferButton(panelWidth, panelHeight);
        }

        private float FitHologramGroupInsideScreen(
            Camera presentationCamera,
            float depth,
            ref Vector3 viewportPosition)
        {
            float worldHeight = presentationCamera.orthographic
                ? presentationCamera.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(
                    presentationCamera.fieldOfView *
                    0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * presentationCamera.aspect;
            float margin = Mathf.Clamp(
                hologramScreenMargin,
                0f,
                0.2f);
            float buttonHeight =
                createTransferButton || createReturnButton
                    ? Mathf.Min(
                        transferButtonHeight,
                        fallbackPanelHeight * 0.22f)
                    : 0f;
            float topExtent = fallbackPanelHeight * 0.5f;
            float bottomExtent =
                fallbackPanelHeight * 0.5f +
                buttonHeight * 1.22f;
            float totalHeight = topExtent + bottomExtent;
            float totalWidth = fallbackPanelWidth;
            float requestedScale = Mathf.Max(
                0.1f,
                hologramGroupScale);
            float availableWidth =
                worldWidth * Mathf.Max(0.05f, 1f - margin * 2f);
            float availableHeight =
                worldHeight * Mathf.Max(0.05f, 1f - margin * 2f);
            float fittedScale = Mathf.Min(
                requestedScale,
                availableWidth / Mathf.Max(0.0001f, totalWidth),
                availableHeight / Mathf.Max(0.0001f, totalHeight));
            fittedScale = Mathf.Max(0.05f, fittedScale);

            float halfViewportWidth =
                totalWidth * fittedScale * 0.5f /
                Mathf.Max(0.0001f, worldWidth);
            float topViewport =
                topExtent * fittedScale /
                Mathf.Max(0.0001f, worldHeight);
            float bottomViewport =
                bottomExtent * fittedScale /
                Mathf.Max(0.0001f, worldHeight);
            viewportPosition.x = Mathf.Clamp(
                viewportPosition.x,
                margin + halfViewportWidth,
                1f - margin - halfViewportWidth);
            viewportPosition.y = Mathf.Clamp(
                viewportPosition.y,
                margin + bottomViewport,
                1f - margin - topViewport);
            return fittedScale;
        }

        private bool TryGetDisplayAreaViewport(
            Camera presentationCamera,
            out Vector2[] viewportCorners,
            out float averageDepth)
        {
            viewportCorners = null;
            averageDepth = 0f;
            if (presentationCamera == null ||
                displayAreaTopLeft == null ||
                displayAreaTopRight == null ||
                displayAreaBottomRight == null ||
                displayAreaBottomLeft == null)
            {
                return false;
            }

            Transform[] corners =
            {
                displayAreaTopLeft,
                displayAreaTopRight,
                displayAreaBottomRight,
                displayAreaBottomLeft
            };
            Vector2[] projected = new Vector2[4];
            float totalDepth = 0f;
            for (int index = 0; index < corners.Length; index++)
            {
                Vector3 viewport =
                    presentationCamera.WorldToViewportPoint(
                        corners[index].position);
                if (viewport.z <= 0.001f)
                {
                    return false;
                }

                projected[index] = new Vector2(
                    viewport.x,
                    viewport.y);
                totalDepth += viewport.z;
            }

            SortDisplayAreaCorners(projected);
            if (!IsConvexDisplayArea(projected))
            {
                return false;
            }

            viewportCorners = projected;
            averageDepth = totalDepth / corners.Length;
            return true;
        }

        private static void SortDisplayAreaCorners(Vector2[] corners)
        {
            if (corners == null || corners.Length < 3)
            {
                return;
            }

            Vector2 center = Vector2.zero;
            foreach (Vector2 corner in corners)
            {
                center += corner;
            }
            center /= corners.Length;

            System.Array.Sort(
                corners,
                (first, second) =>
                {
                    float firstAngle = Mathf.Atan2(
                        first.y - center.y,
                        first.x - center.x);
                    float secondAngle = Mathf.Atan2(
                        second.y - center.y,
                        second.x - center.x);
                    return firstAngle.CompareTo(secondAngle);
                });
        }

        private float FitHologramGroupInsideDisplayArea(
            Camera presentationCamera,
            float depth,
            Vector2[] displayArea,
            ref Vector3 viewportPosition)
        {
            float worldHeight = presentationCamera.orthographic
                ? presentationCamera.orthographicSize * 2f
                : 2f * depth * Mathf.Tan(
                    presentationCamera.fieldOfView *
                    0.5f * Mathf.Deg2Rad);
            float worldWidth = worldHeight * presentationCamera.aspect;
            float requestedScale = Mathf.Max(
                0.001f,
                hologramGroupScale);

            if (TryBuildDisplayAreaCenterPolygon(
                    displayArea,
                    worldWidth,
                    worldHeight,
                    requestedScale,
                    displayAreaPadding,
                    out List<Vector2> fittedCenters))
            {
                Vector2 fittedPosition = ClosestPointInsidePolygon(
                    fittedCenters,
                    new Vector2(
                        viewportPosition.x,
                        viewportPosition.y));
                viewportPosition.x = fittedPosition.x;
                viewportPosition.y = fittedPosition.y;
                return requestedScale;
            }

            float minimumScale = 0f;
            float maximumScale = requestedScale;
            float fittedScale = 0f;
            List<Vector2> bestCenters = null;
            for (int iteration = 0; iteration < 16; iteration++)
            {
                float candidateScale =
                    (minimumScale + maximumScale) * 0.5f;
                if (TryBuildDisplayAreaCenterPolygon(
                        displayArea,
                        worldWidth,
                        worldHeight,
                        candidateScale,
                        displayAreaPadding,
                        out List<Vector2> candidateCenters))
                {
                    fittedScale = candidateScale;
                    bestCenters = candidateCenters;
                    minimumScale = candidateScale;
                }
                else
                {
                    maximumScale = candidateScale;
                }
            }

            if (bestCenters == null || bestCenters.Count < 3)
            {
                return FitHologramGroupInsideScreen(
                    presentationCamera,
                    depth,
                    ref viewportPosition);
            }

            Vector2 bestPosition = ClosestPointInsidePolygon(
                bestCenters,
                new Vector2(
                    viewportPosition.x,
                    viewportPosition.y));
            viewportPosition.x = bestPosition.x;
            viewportPosition.y = bestPosition.y;
            return Mathf.Max(0.001f, fittedScale);
        }

        private bool TryBuildDisplayAreaCenterPolygon(
            Vector2[] displayArea,
            float worldWidth,
            float worldHeight,
            float scale,
            float padding,
            out List<Vector2> centerPolygon)
        {
            if (displayArea == null || displayArea.Length != 4 ||
                worldWidth <= 0.0001f || worldHeight <= 0.0001f)
            {
                centerPolygon = new List<Vector2>();
                return false;
            }

            centerPolygon = new List<Vector2>(displayArea);

            float buttonHeight =
                createTransferButton || createReturnButton
                    ? Mathf.Min(
                        transferButtonHeight,
                        fallbackPanelHeight * 0.22f)
                    : 0f;
            float halfWidth =
                fallbackPanelWidth * scale * 0.5f /
                worldWidth;
            float top =
                fallbackPanelHeight * scale * 0.5f /
                worldHeight;
            float bottom =
                (fallbackPanelHeight * 0.5f +
                 buttonHeight * 1.22f) * scale /
                worldHeight;
            Vector2[] groupOffsets =
            {
                new Vector2(-halfWidth, top),
                new Vector2(halfWidth, top),
                new Vector2(halfWidth, -bottom),
                new Vector2(-halfWidth, -bottom)
            };

            return TryBuildCenterPolygonFromOffsets(
                displayArea,
                groupOffsets,
                1f,
                padding,
                out centerPolygon);
        }

        private void ConstrainRenderedHologramInsideVisibleArea(
            Camera presentationCamera,
            Transform hologramRoot,
            float depth,
            Vector2[] displayArea)
        {
            if (presentationCamera == null ||
                hologramRoot == null ||
                fallbackHologram == null)
            {
                return;
            }

            for (int iteration = 0; iteration < 2; iteration++)
            {
                Vector3 rootViewport =
                    presentationCamera.WorldToViewportPoint(
                        hologramRoot.position);
                List<Vector2> rendererOffsets =
                    CollectRenderedViewportOffsets(
                        presentationCamera,
                        rootViewport);
                if (rendererOffsets.Count == 0)
                {
                    return;
                }

                float scaleFactor = 1f;
                Vector2 targetCenter = new Vector2(
                    rootViewport.x,
                    rootViewport.y);
                if (displayArea != null && displayArea.Length == 4)
                {
                    if (!TryBuildCenterPolygonFromOffsets(
                            displayArea,
                            rendererOffsets,
                            1f,
                            displayAreaPadding,
                            out List<Vector2> centerPolygon))
                    {
                        float lower = 0f;
                        float upper = 1f;
                        centerPolygon = null;
                        for (int search = 0; search < 16; search++)
                        {
                            float candidate = (lower + upper) * 0.5f;
                            if (TryBuildCenterPolygonFromOffsets(
                                    displayArea,
                                    rendererOffsets,
                                    candidate,
                                    displayAreaPadding,
                                    out List<Vector2> candidatePolygon))
                            {
                                scaleFactor = candidate;
                                centerPolygon = candidatePolygon;
                                lower = candidate;
                            }
                            else
                            {
                                upper = candidate;
                            }
                        }
                    }

                    if (centerPolygon == null ||
                        centerPolygon.Count < 3)
                    {
                        return;
                    }

                    targetCenter = ClosestPointInsidePolygon(
                        centerPolygon,
                        targetCenter);
                }
                else
                {
                    FitRenderedOffsetsInsideScreen(
                        rendererOffsets,
                        targetCenter,
                        out scaleFactor,
                        out targetCenter);
                }

                if (scaleFactor < 0.9999f)
                {
                    fallbackHologram.transform.localScale *=
                        Mathf.Max(0.001f, scaleFactor);
                }

                Vector3 fittedViewport = new Vector3(
                    targetCenter.x,
                    targetCenter.y,
                    depth);
                hologramRoot.SetPositionAndRotation(
                    presentationCamera.ViewportToWorldPoint(
                        fittedViewport),
                    presentationCamera.transform.rotation);

                if (scaleFactor >= 0.9999f &&
                    Mathf.Abs(targetCenter.x - rootViewport.x) < 0.00001f &&
                    Mathf.Abs(targetCenter.y - rootViewport.y) < 0.00001f)
                {
                    break;
                }
            }
        }

        private void KeepHologramClearOfPreviewModel(
            Camera presentationCamera,
            Transform hologramRoot,
            float depth,
            Vector2[] displayArea)
        {
            if (presentationCamera == null || hologramRoot == null ||
                fallbackHologram == null || spawnedModel == null)
            {
                return;
            }

            const float gap = 0.025f;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (!TryGetViewportBounds(
                        presentationCamera,
                        spawnedModel,
                        out Rect modelBounds))
                {
                    return;
                }

                Vector3 rootViewport =
                    presentationCamera.WorldToViewportPoint(
                        hologramRoot.position);
                if (!TryGetViewportBounds(
                        presentationCamera,
                        fallbackHologram,
                        out Rect hologramBounds) ||
                    !modelBounds.Overlaps(hologramBounds))
                {
                    return;
                }

                float moveLeft = modelBounds.xMin - gap -
                    hologramBounds.xMax;
                float moveRight = modelBounds.xMax + gap -
                    hologramBounds.xMin;
                float horizontalMove = Mathf.Abs(moveLeft) <
                    Mathf.Abs(moveRight)
                    ? moveLeft
                    : moveRight;
                rootViewport.x += horizontalMove;
                rootViewport.z = depth;
                hologramRoot.SetPositionAndRotation(
                    presentationCamera.ViewportToWorldPoint(rootViewport),
                    presentationCamera.transform.rotation);
                ConstrainRenderedHologramInsideVisibleArea(
                    presentationCamera,
                    hologramRoot,
                    depth,
                    displayArea);
            }
        }

        private static bool TryGetViewportBounds(
            Camera presentationCamera,
            GameObject target,
            out Rect viewportBounds)
        {
            viewportBounds = default;
            if (presentationCamera == null || target == null)
            {
                return false;
            }

            bool hasPoint = false;
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            foreach (Renderer renderer in
                     target.GetComponentsInChildren<Renderer>(false))
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0
                            ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0
                            ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0
                            ? bounds.min.z : bounds.max.z);
                    Vector3 viewport =
                        presentationCamera.WorldToViewportPoint(point);
                    if (viewport.z <= 0.001f)
                    {
                        continue;
                    }

                    hasPoint = true;
                    minimumX = Mathf.Min(minimumX, viewport.x);
                    maximumX = Mathf.Max(maximumX, viewport.x);
                    minimumY = Mathf.Min(minimumY, viewport.y);
                    maximumY = Mathf.Max(maximumY, viewport.y);
                }
            }

            if (!hasPoint)
            {
                return false;
            }

            viewportBounds = Rect.MinMaxRect(
                minimumX,
                minimumY,
                maximumX,
                maximumY);
            return true;
        }

        private List<Vector2> CollectRenderedViewportOffsets(
            Camera presentationCamera,
            Vector3 rootViewport)
        {
            List<Vector2> offsets = new List<Vector2>();
            Renderer[] renderers =
                fallbackHologram.GetComponentsInChildren<Renderer>(false);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 minimum = bounds.min;
                Vector3 maximum = bounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 worldCorner = new Vector3(
                        (corner & 1) == 0 ? minimum.x : maximum.x,
                        (corner & 2) == 0 ? minimum.y : maximum.y,
                        (corner & 4) == 0 ? minimum.z : maximum.z);
                    Vector3 viewport =
                        presentationCamera.WorldToViewportPoint(
                            worldCorner);
                    if (viewport.z <= 0.001f)
                    {
                        continue;
                    }

                    offsets.Add(new Vector2(
                        viewport.x - rootViewport.x,
                        viewport.y - rootViewport.y));
                }
            }

            return offsets;
        }

        private void FitRenderedOffsetsInsideScreen(
            List<Vector2> offsets,
            Vector2 desiredCenter,
            out float scaleFactor,
            out Vector2 fittedCenter)
        {
            float minimumX = float.PositiveInfinity;
            float maximumX = float.NegativeInfinity;
            float minimumY = float.PositiveInfinity;
            float maximumY = float.NegativeInfinity;
            foreach (Vector2 offset in offsets)
            {
                minimumX = Mathf.Min(minimumX, offset.x);
                maximumX = Mathf.Max(maximumX, offset.x);
                minimumY = Mathf.Min(minimumY, offset.y);
                maximumY = Mathf.Max(maximumY, offset.y);
            }

            float margin = Mathf.Clamp(
                hologramScreenMargin,
                0f,
                0.2f);
            float available = Mathf.Max(0.001f, 1f - margin * 2f);
            float width = Mathf.Max(0.000001f, maximumX - minimumX);
            float height = Mathf.Max(0.000001f, maximumY - minimumY);
            scaleFactor = Mathf.Min(
                1f,
                available / width,
                available / height);

            float centerMinimumX = margin - minimumX * scaleFactor;
            float centerMaximumX =
                1f - margin - maximumX * scaleFactor;
            float centerMinimumY = margin - minimumY * scaleFactor;
            float centerMaximumY =
                1f - margin - maximumY * scaleFactor;
            fittedCenter = new Vector2(
                ClampToValidInterval(
                    desiredCenter.x,
                    centerMinimumX,
                    centerMaximumX),
                ClampToValidInterval(
                    desiredCenter.y,
                    centerMinimumY,
                    centerMaximumY));
        }

        private static float ClampToValidInterval(
            float value,
            float minimum,
            float maximum)
        {
            return minimum <= maximum
                ? Mathf.Clamp(value, minimum, maximum)
                : (minimum + maximum) * 0.5f;
        }

        private static bool TryBuildCenterPolygonFromOffsets(
            Vector2[] displayArea,
            IEnumerable<Vector2> groupOffsets,
            float offsetScale,
            float padding,
            out List<Vector2> centerPolygon)
        {
            centerPolygon = new List<Vector2>(displayArea);

            bool clockwise = SignedPolygonArea(displayArea) < 0f;
            float safePadding = Mathf.Max(0f, padding);
            for (int index = 0; index < displayArea.Length; index++)
            {
                Vector2 edgeStart = displayArea[index];
                Vector2 edgeEnd =
                    displayArea[(index + 1) % displayArea.Length];
                Vector2 edge = edgeEnd - edgeStart;
                Vector2 leftNormal = new Vector2(-edge.y, edge.x);
                Vector2 constraintNormal = clockwise
                    ? leftNormal
                    : -leftNormal;
                float maximumOffset = float.NegativeInfinity;
                foreach (Vector2 offset in groupOffsets)
                {
                    maximumOffset = Mathf.Max(
                        maximumOffset,
                        Vector2.Dot(
                            constraintNormal,
                            offset * offsetScale));
                }

                float limit =
                    Vector2.Dot(constraintNormal, edgeStart) -
                    maximumOffset -
                    constraintNormal.magnitude * safePadding;
                centerPolygon = ClipPolygonAgainstHalfPlane(
                    centerPolygon,
                    constraintNormal,
                    limit);
                if (centerPolygon.Count < 3)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<Vector2> ClipPolygonAgainstHalfPlane(
            List<Vector2> polygon,
            Vector2 normal,
            float limit)
        {
            List<Vector2> result = new List<Vector2>();
            if (polygon == null || polygon.Count == 0)
            {
                return result;
            }

            Vector2 previous = polygon[polygon.Count - 1];
            float previousDistance =
                Vector2.Dot(normal, previous) - limit;
            bool previousInside = previousDistance <= 0.00001f;
            foreach (Vector2 current in polygon)
            {
                float currentDistance =
                    Vector2.Dot(normal, current) - limit;
                bool currentInside = currentDistance <= 0.00001f;
                if (currentInside != previousInside)
                {
                    float denominator =
                        previousDistance - currentDistance;
                    float t = Mathf.Abs(denominator) > 0.000001f
                        ? previousDistance / denominator
                        : 0f;
                    result.Add(Vector2.LerpUnclamped(
                        previous,
                        current,
                        t));
                }

                if (currentInside)
                {
                    result.Add(current);
                }

                previous = current;
                previousDistance = currentDistance;
                previousInside = currentInside;
            }

            return result;
        }

        private static Vector2 ClosestPointInsidePolygon(
            List<Vector2> polygon,
            Vector2 point)
        {
            if (IsPointInsidePolygon(polygon, point))
            {
                return point;
            }

            Vector2 closest = polygon[0];
            float closestSqrDistance = float.PositiveInfinity;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 start = polygon[index];
                Vector2 end = polygon[(index + 1) % polygon.Count];
                Vector2 segment = end - start;
                float segmentSqrLength = segment.sqrMagnitude;
                float t = segmentSqrLength > 0.000001f
                    ? Mathf.Clamp01(
                        Vector2.Dot(point - start, segment) /
                        segmentSqrLength)
                    : 0f;
                Vector2 candidate = start + segment * t;
                float sqrDistance = (point - candidate).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closest = candidate;
                    closestSqrDistance = sqrDistance;
                }
            }

            return closest;
        }

        private static bool IsPointInsidePolygon(
            List<Vector2> polygon,
            Vector2 point)
        {
            bool inside = false;
            for (int index = 0, previous = polygon.Count - 1;
                 index < polygon.Count;
                 previous = index++)
            {
                Vector2 currentPoint = polygon[index];
                Vector2 previousPoint = polygon[previous];
                bool crosses =
                    (currentPoint.y > point.y) !=
                    (previousPoint.y > point.y) &&
                    point.x <
                    (previousPoint.x - currentPoint.x) *
                    (point.y - currentPoint.y) /
                    (previousPoint.y - currentPoint.y) +
                    currentPoint.x;
                if (crosses)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static bool IsConvexDisplayArea(Vector2[] polygon)
        {
            if (polygon == null || polygon.Length != 4 ||
                Mathf.Abs(SignedPolygonArea(polygon)) < 0.00001f)
            {
                return false;
            }

            float direction = 0f;
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 first = polygon[index];
                Vector2 second =
                    polygon[(index + 1) % polygon.Length];
                Vector2 third =
                    polygon[(index + 2) % polygon.Length];
                float cross = Cross2D(
                    second - first,
                    third - second);
                if (Mathf.Abs(cross) < 0.000001f)
                {
                    return false;
                }

                float currentDirection = Mathf.Sign(cross);
                if (direction == 0f)
                {
                    direction = currentDirection;
                }
                else if (direction != currentDirection)
                {
                    return false;
                }
            }

            return true;
        }

        private static float SignedPolygonArea(Vector2[] polygon)
        {
            float area = 0f;
            for (int index = 0; index < polygon.Length; index++)
            {
                Vector2 current = polygon[index];
                Vector2 next = polygon[(index + 1) % polygon.Length];
                area += current.x * next.y - next.x * current.y;
            }

            return area * 0.5f;
        }

        private static float Cross2D(Vector2 first, Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }

        private void CreateFallbackTransferButton()
        {
            if (fallbackHologram == null ||
                fallbackTransferButton != null)
            {
                return;
            }

            GameObject button = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            button.name = "TransferButton";
            button.transform.SetParent(
                fallbackHologram.transform,
                false);
            fallbackTransferButton = button.transform;
            fallbackTransferButtonRenderer =
                button.GetComponent<Renderer>();
            ConfigureHologramRenderer(
                fallbackTransferButtonRenderer,
                hologramSortingOrder + 3);
            fallbackTransferButtonMaterial = CreateHologramMaterial();
            if (fallbackTransferButtonRenderer != null &&
                fallbackTransferButtonMaterial != null)
            {
                fallbackTransferButtonRenderer.sharedMaterial =
                    fallbackTransferButtonMaterial;
            }

            fallbackTransferWorldButton =
                button.AddComponent<CraftLiveWorldButton>();
            fallbackTransferWorldButton.AddListener(
                TransferSelectedMaterial);

            GameObject label = new GameObject("Label");
            label.transform.SetParent(button.transform, false);
            label.transform.localPosition =
                new Vector3(0f, 0f, -0.56f);
            fallbackTransferButtonText = label.AddComponent<TextMesh>();
            fallbackTransferButtonText.anchor = TextAnchor.MiddleCenter;
            fallbackTransferButtonText.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                fallbackTransferButtonText,
                0.18f,
                hologramTextColor);
            SetRendererSortingOrder(
                fallbackTransferButtonText.GetComponent<Renderer>(),
                hologramSortingOrder + 4);

            ResizeTransferButton(
                fallbackPanelWidth,
                fallbackPanelHeight);
        }

        private void CreateFallbackReturnButton()
        {
            if (fallbackHologram == null ||
                fallbackReturnButton != null)
            {
                return;
            }

            GameObject button = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            button.name = "ReturnButton";
            button.transform.SetParent(
                bindings != null && bindings.HologramInfoRoot != null
                    ? bindings.HologramInfoRoot
                    : fallbackHologram.transform,
                false);
            fallbackReturnButton = button.transform;
            fallbackReturnButtonRenderer =
                button.GetComponent<Renderer>();
            ConfigureHologramRenderer(
                fallbackReturnButtonRenderer,
                hologramSortingOrder + 3);
            fallbackReturnButtonMaterial = CreateHologramMaterial();
            if (fallbackReturnButtonRenderer != null &&
                fallbackReturnButtonMaterial != null)
            {
                fallbackReturnButtonRenderer.sharedMaterial =
                    fallbackReturnButtonMaterial;
            }

            fallbackReturnWorldButton =
                button.AddComponent<CraftLiveWorldButton>();
            fallbackReturnWorldButton.AddListener(
                ReturnSelectedMaterial);

            GameObject label = new GameObject("Label");
            label.transform.SetParent(button.transform, false);
            label.transform.localPosition =
                new Vector3(0f, 0f, -0.56f);
            fallbackReturnButtonText = label.AddComponent<TextMesh>();
            fallbackReturnButtonText.anchor = TextAnchor.MiddleCenter;
            fallbackReturnButtonText.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                fallbackReturnButtonText,
                0.18f,
                hologramTextColor);
            fallbackReturnButtonText.text = "戻る";
            SetRendererSortingOrder(
                fallbackReturnButtonText.GetComponent<Renderer>(),
                hologramSortingOrder + 4);

            PositionReturnButton();
        }

        private void ResizeTransferButton(
            float panelWidth,
            float panelHeight)
        {
            if (fallbackTransferButton == null &&
                fallbackReturnButton == null)
            {
                return;
            }

            float panelScale = fallbackHologram != null
                ? Mathf.Max(0.001f, fallbackHologram.transform.lossyScale.x)
                : 1f;
            float height = Mathf.Min(
                transferButtonHeight,
                panelHeight * 0.22f);
            bool showBoth = false;
            float width = Mathf.Clamp(
                panelWidth * (showBoth ? 0.36f : 0.68f),
                panelWidth * 0.28f,
                panelWidth * 0.72f);
            float y = -panelHeight * 0.5f - height * 0.72f;
            if (fallbackTransferButton != null)
            {
                fallbackTransferButton.localPosition = new Vector3(
                    showBoth ? panelWidth * 0.21f : 0f,
                    y,
                    -0.055f);
                fallbackTransferButton.localScale =
                    new Vector3(width, height, 0.055f);
            }
            if (fallbackTransferButtonText != null)
            {
                fallbackTransferButtonText.characterSize =
                    CraftLiveForgeUITheme.ScaleCharacterSize(
                        Mathf.Max(
                            0.01f,
                            height * buttonTextSize));
            }
            if (fallbackReturnButtonText != null)
            {
                fallbackReturnButtonText.characterSize =
                    CraftLiveForgeUITheme.ScaleCharacterSize(
                        Mathf.Max(
                            0.01f,
                            fallbackPanelHeight * panelScale *
                            returnButtonSize.y * buttonTextSize));
            }
        }

        private void PositionReturnButton()
        {
            if (fallbackReturnButton == null || fallbackHologram == null ||
                fallbackPanel == null)
            {
                return;
            }

            float panelScale = Mathf.Max(
                0.001f,
                fallbackHologram.transform.lossyScale.x);
            float width = Mathf.Max(
                0.05f,
                fallbackPanelWidth * panelScale * returnButtonSize.x);
            float height = Mathf.Max(
                0.05f,
                fallbackPanelHeight * panelScale * returnButtonSize.y);
            Vector3 panelLocalPosition = new Vector3(
                0f,
                -fallbackPanelHeight * 0.5f - height * 0.72f,
                -0.08f);
            fallbackReturnButton.position =
                fallbackHologram.transform.TransformPoint(panelLocalPosition) +
                fallbackHologram.transform.TransformVector(
                    returnButtonPositionOffset);
            fallbackReturnButton.rotation = fallbackHologram.transform.rotation;
            fallbackReturnButton.localScale =
                new Vector3(width, height, 0.055f);
            if (fallbackReturnButtonText != null)
            {
                fallbackReturnButtonText.transform.localScale = new Vector3(
                    1f / width,
                    1f / height,
                    1f);
                fallbackReturnButtonText.characterSize =
                    CraftLiveForgeUITheme.ScaleCharacterSize(
                        Mathf.Max(0.01f, height * buttonTextSize));
                fallbackReturnButtonText.text = "戻る";
                FitReturnButtonTextToButtonBounds();
            }
        }

        private void FitReturnButtonTextToButtonBounds()
        {
            if (fallbackReturnButton == null ||
                fallbackReturnButtonText == null)
            {
                return;
            }

            Renderer buttonRenderer =
                fallbackReturnButton.GetComponent<Renderer>();
            Renderer textRenderer =
                fallbackReturnButtonText.GetComponent<Renderer>();
            if (buttonRenderer == null || textRenderer == null)
            {
                return;
            }

            Vector3 buttonSize = buttonRenderer.bounds.size;
            Vector3 textSize = textRenderer.bounds.size;
            if (textSize.x <= Mathf.Epsilon || textSize.y <= Mathf.Epsilon)
            {
                return;
            }

            float fit = Mathf.Min(
                1f,
                buttonSize.x * 0.78f / textSize.x,
                buttonSize.y * 0.62f / textSize.y);
            fallbackReturnButtonText.characterSize *=
                Mathf.Clamp(fit, 0.02f, 1f);
        }

        private Transform CreateHologramBorderEdge(
            Transform parent,
            string name)
        {
            GameObject edge = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            edge.name = name;
            edge.transform.SetParent(parent, false);
            Collider collider = edge.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Renderer renderer = edge.GetComponent<Renderer>();
            ConfigureHologramRenderer(
                renderer,
                hologramSortingOrder + 1);
            if (renderer != null && fallbackBorderMaterial != null)
            {
                renderer.sharedMaterial = fallbackBorderMaterial;
            }

            return edge.transform;
        }

        private void SetBorderEdge(
            int index,
            Vector3 localPosition,
            Vector3 localScale)
        {
            if (index < 0 ||
                index >= fallbackBorderEdges.Length ||
                fallbackBorderEdges[index] == null)
            {
                return;
            }

            fallbackBorderEdges[index].localPosition = localPosition;
            fallbackBorderEdges[index].localScale = localScale;
        }

        private Material CreateHologramMaterial()
        {
            Material material = CreateDedicatedHologramMaterial(
                "Generated_HologramMaterial");
            if (material == null)
            {
                return null;
            }
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            return material;
        }

        private static Material CreateHologramPanelMaterial()
        {
            Material material = CreateDedicatedHologramMaterial(
                "Generated_HologramPanelMaterial");
            if (material == null)
            {
                return null;
            }

            // Match the border's Resources-backed unlit material so the panel
            // reliably renders its tint as well as its transparency.
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat(
                "_DstBlend",
                (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetShaderPassEnabled("ShadowCaster", false);
            return material;
        }

        private static Material CreateDedicatedHologramMaterial(string name)
        {
            Shader shader = Shader.Find("CraftOrigin/HologramTransparent");
            if (shader == null)
            {
                return CraftLiveForgeUITheme.CreateCompatibleUnlitMaterial(
                    name);
            }

            return new Material(shader) { name = name };
        }

        private static void ConfigureHologramRenderer(
            Renderer renderer,
            int sortingOrder)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
        }

        private static void SetRendererSortingOrder(
            Renderer renderer,
            int sortingOrder)
        {
            if (renderer != null)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }

        private void UpdateHologramColors(Color themeColor)
        {
            // Separate the three visual roles instead of painting the whole
            // hologram with one color. A dark tinted field preserves the
            // material identity, a bright rim defines the silhouette, and a
            // warm ivory foreground stays readable over every gallery wall.
            Color panelColor = Color.Lerp(
                new Color(0.006f, 0.011f, 0.018f, 1f),
                themeColor,
                0.34f);
            // Tint strength and transparency must be independent. Multiplying
            // both by opacity attenuates the visible hue twice.
            // Use the same panel transparency for catalog and QR materials.
            panelColor.a = hologramPanelOpacity;
            SetMaterialColor(fallbackPanelMaterial, panelColor);
            ApplyRendererColor(fallbackPanelRenderer, panelColor);

            Color borderColor = Color.Lerp(
                themeColor,
                Color.white,
                0.24f);
            borderColor = new Color(
                borderColor.r * hologramBorderGlow,
                borderColor.g * hologramBorderGlow,
                borderColor.b * hologramBorderGlow,
                0.92f);
            SetMaterialColor(fallbackBorderMaterial, borderColor);
            foreach (Transform edge in fallbackBorderEdges)
            {
                if (edge != null)
                {
                    ApplyRendererColor(
                        edge.GetComponent<Renderer>(),
                        borderColor);
                }
            }


            readableHologramTextColor = new Color(
                1f,
                0.94f,
                0.72f,
                1f);
            if (fallbackText != null)
            {
                fallbackText.color = readableHologramTextColor;
            }
        }

        private void UpdateTransferButton(
            CraftLiveMaterialDefinition material,
            Color themeColor)
        {
            if (!createTransferButton ||
                fallbackTransferWorldButton == null)
            {
                return;
            }

            bool hasPlacement = HasTransferPlacement(
                session != null ? session.State : null,
                material);
            bool playTestTransfer =
                !hasPlacement &&
                IsPlayTestTransferEnabled() &&
                CanPreparePlayTestTransfer(
                    session != null ? session.State : null,
                    material);
            bool interactable = hasPlacement || playTestTransfer;

            Color normal = WithAlpha(Color.Lerp(
                themeColor,
                Color.white,
                0.12f), hologramButtonOpacity);
            Color hover = WithAlpha(Color.Lerp(
                normal,
                Color.white,
                0.25f), Mathf.Min(0.9f, hologramButtonOpacity + 0.16f));
            Color pressed = WithAlpha(Color.Lerp(
                normal,
                Color.white,
                0.48f), Mathf.Min(0.95f, hologramButtonOpacity + 0.32f));
            fallbackTransferWorldButton.Configure(
                fallbackTransferButton,
                new[] { fallbackTransferButtonRenderer },
                normal,
                hover,
                pressed);
            fallbackTransferWorldButton.SetDisabledColor(
                new Color(0.08f, 0.1f, 0.12f,
                    hologramButtonOpacity * 0.55f));
            fallbackTransferWorldButton.SetInteractable(interactable);

            if (fallbackTransferButtonText != null)
            {
                ApplyFont(fallbackTransferButtonText);
                CraftLiveForgeUITheme.ApplyCrispTextMetrics(
                    fallbackTransferButtonText,
                    0.18f);
                fallbackTransferButtonText.color = interactable
                    ? readableHologramTextColor
                    : new Color(0.42f, 0.42f, 0.42f, 1f);
                fallbackTransferButtonText.text = "転送";
            }
        }

        private void UpdateReturnButton(Color themeColor)
        {
            if (!createReturnButton ||
                fallbackReturnWorldButton == null)
            {
                return;
            }

            Color normal = WithAlpha(Color.Lerp(
                themeColor,
                Color.black,
                0.22f), hologramButtonOpacity);
            Color hover = WithAlpha(Color.Lerp(
                normal,
                Color.white,
                0.22f), Mathf.Min(0.9f, hologramButtonOpacity + 0.16f));
            Color pressed = WithAlpha(Color.Lerp(
                normal,
                Color.white,
                0.42f), Mathf.Min(0.95f, hologramButtonOpacity + 0.32f));
            fallbackReturnWorldButton.Configure(
                fallbackReturnButton,
                new[] { fallbackReturnButtonRenderer },
                normal,
                hover,
                pressed);
            fallbackReturnWorldButton.SetDisabledColor(
                new Color(0.08f, 0.1f, 0.12f,
                    hologramButtonOpacity * 0.55f));
            fallbackReturnWorldButton.SetInteractable(
                returnCoroutine == null);
            if (fallbackReturnButtonText != null)
            {
                ApplyFont(fallbackReturnButtonText);
                CraftLiveForgeUITheme.ApplyCrispTextMetrics(
                    fallbackReturnButtonText,
                    0.18f);
                fallbackReturnButtonText.color =
                    readableHologramTextColor;
                fallbackReturnButtonText.text = "戻る";
            }
        }

        public void ReturnSelectedMaterial()
        {
            if (returnCoroutine != null)
            {
                return;
            }

            StopReveal();
            if (fallbackTransferWorldButton != null)
            {
                fallbackTransferWorldButton.SetInteractable(false);
            }
            if (fallbackReturnWorldButton != null)
            {
                fallbackReturnWorldButton.SetInteractable(false);
            }
            returnCoroutine = StartCoroutine(ReturnPreview());
        }

        public void TransferSelectedMaterial()
        {
            if (session == null || session.Catalog == null)
            {
                return;
            }

            CraftLiveMaterialDefinition material =
                session.Catalog.FindMaterial(displayedMaterialId);
            if (material == null)
            {
                return;
            }

            CraftLiveRoomState state = session.State;
            if (HasTransferPlacement(state, material))
            {
                CaptureTransferMergeSource(material);
                session.ConfirmPlacement();
                return;
            }

            if (!IsPlayTestTransferEnabled())
            {
                return;
            }

            PreparePlayTestTransfer(material);
        }

        public void SetPlayTestTransferWithoutPlacement(bool value)
        {
            allowTransferWithoutPlacementForPlayTest = value;
            CraftLiveMaterialDefinition material =
                session != null && session.Catalog != null
                    ? session.Catalog.FindMaterial(displayedMaterialId)
                    : null;
            UpdateTransferButton(
                material,
                material != null
                    ? material.Pad1HologramColor
                    : hologramColor);
        }

        private bool PreparePlayTestTransfer(
            CraftLiveMaterialDefinition material)
        {
            CraftLiveRoomState state = session != null
                ? session.State
                : null;
            if (!CanPreparePlayTestTransfer(state, material))
            {
                return false;
            }

            if (state.placement.status == CraftLivePlacementStatus.Idle)
            {
                session.SelectMaterial(material);
                state = session.State;
            }

            if (!TryFindAvailableTransferSlot(
                    state,
                    material,
                    out CraftLiveSlotId slot))
            {
                return false;
            }

            session.ChoosePlacementSlot(slot);
            CaptureTransferMergeSource(material);
            session.ConfirmPlacement();
            return IsMaterialQueued(session.State, material);
        }

        private void CaptureTransferMergeSource(
            CraftLiveMaterialDefinition material)
        {
            CraftLivePad1TransferController transferController =
                GetComponent<CraftLivePad1TransferController>();
            if (transferController == null)
            {
                transferController =
                    GetComponentInParent<CraftLivePad1TransferController>();
            }
            transferController?.CaptureTransferMergeSource(
                material,
                spawnedModel);
        }

        private bool IsPlayTestTransferEnabled()
        {
#if UNITY_EDITOR
            return allowTransferWithoutPlacementForPlayTest &&
                   Application.isPlaying;
#else
            return false;
#endif
        }

        private static bool HasTransferPlacement(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material)
        {
            return state != null &&
                   material != null &&
                   state.placement != null &&
                   state.placement.status ==
                       CraftLivePlacementStatus.ConfirmingSlot &&
                   state.placement.hasCandidateSlot &&
                   state.placement.materialId == material.MaterialId;
        }

        private static bool IsMaterialQueued(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material)
        {
            if (state == null ||
                material == null ||
                state.placement == null ||
                state.placement.status != CraftLivePlacementStatus.Idle ||
                state.transferQueue == null)
            {
                return false;
            }

            foreach (CraftLiveTransferQueueEntry entry in
                     state.transferQueue)
            {
                if (entry != null &&
                    entry.materialId == material.MaterialId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanPreparePlayTestTransfer(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material)
        {
            if (state == null ||
                material == null ||
                state.placement == null)
            {
                return false;
            }

            bool usableState =
                state.placement.status == CraftLivePlacementStatus.Idle ||
                (state.placement.status ==
                     CraftLivePlacementStatus.SelectingSlot &&
                 state.placement.materialId == material.MaterialId);
            return usableState &&
                   TryFindAvailableTransferSlot(
                       state,
                       material,
                       out _);
        }

        private static bool TryFindAvailableTransferSlot(
            CraftLiveRoomState state,
            CraftLiveMaterialDefinition material,
            out CraftLiveSlotId slot)
        {
            foreach (CraftLiveSlotId candidate in TransferTestSlots)
            {
                if (material.CanUseIn(candidate) &&
                    state.CanReserveSlot(candidate))
                {
                    slot = candidate;
                    return true;
                }
            }

            slot = default;
            return false;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void SetMaterialColor(
            Material material,
            Color color)
        {
            if (material == null)
            {
                return;
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color);
            }
        }

        private static void ApplyRendererColor(
            Renderer renderer,
            Color color)
        {
            if (renderer == null)
            {
                return;
            }

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private void UpdateHologramText(string details)
        {
            if (fallbackText == null)
            {
                return;
            }

            int charactersPerLine = Mathf.Clamp(
                Mathf.RoundToInt(
                    21f * fallbackPanelWidth /
                    Mathf.Max(0.65f, fallbackPanelHeight)),
                16,
                26);
            string wrapped = WrapText(details, charactersPerLine, 7);
            string[] lines = wrapped.Split('\n');
            int longestLine = 1;
            foreach (string line in lines)
            {
                longestLine = Mathf.Max(longestLine, line.Length);
            }

            float sizeByWidth =
                fallbackPanelWidth * 0.86f /
                (longestLine * 0.62f);
            float sizeByHeight =
                fallbackPanelHeight * 0.8f /
                (Mathf.Max(1, lines.Length) * 1.18f);
            ApplyFont(fallbackText);
            fallbackText.color = readableHologramTextColor;
            CraftLiveForgeUITheme.ApplyCrispTextMetrics(
                fallbackText,
                Mathf.Clamp(
                    Mathf.Min(
                        sizeByWidth,
                        sizeByHeight,
                        hologramMaxCharacterSize),
                    0.006f,
                    hologramMaxCharacterSize));
            fallbackText.text = wrapped;
            fallbackText.transform.localPosition =
                new Vector3(0f, 0f, -0.06f);
            FitHologramTextToPanelBounds();
        }

        private void FitHologramTextToPanelBounds()
        {
            if (fallbackText == null || fallbackPanelRenderer == null)
            {
                return;
            }

            Renderer textRenderer = fallbackText.GetComponent<Renderer>();
            if (textRenderer == null)
            {
                return;
            }

            Vector3 panelSize = fallbackPanelRenderer.bounds.size;
            Vector3 textSize = textRenderer.bounds.size;
            if (textSize.x <= 0.0001f || textSize.y <= 0.0001f)
            {
                return;
            }

            float fit = Mathf.Min(
                1f,
                panelSize.x * 0.86f / textSize.x,
                panelSize.y * 0.8f / textSize.y);
            fallbackText.characterSize *= Mathf.Clamp(fit, 0.05f, 1f);
        }

        private void ApplyFont(TextMesh textMesh)
        {
            if (textMesh == null)
            {
                return;
            }

            textMesh.fontStyle = FontStyle.Bold;
            if (hologramFont == null)
            {
                if (textMesh == fallbackText)
                {
                    CraftLiveForgeUITheme.ApplyBoardFont(textMesh);
                }
                else
                {
                    CraftLiveForgeUITheme.ApplyHeadingFont(textMesh);
                }
                return;
            }

            textMesh.font = hologramFont;
            MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = hologramFont.material;
            }
        }

        private static void DestroyRuntimeMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
        }

        private IEnumerator Reveal(
            Transform target,
            Vector3 finalPosition,
            Vector3 finalScale,
            CraftLivePreviewSpin spin)
        {
            Vector3 startPosition = target.localPosition;
            Vector3 startScale = target.localScale;
            float elapsed = 0f;
            while (target != null && elapsed < revealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / revealDuration);
                t = 1f - Mathf.Pow(1f - t, 3f);
                target.localPosition = Vector3.LerpUnclamped(
                    startPosition,
                    finalPosition,
                    t);
                target.localScale = Vector3.LerpUnclamped(
                    startScale,
                    finalScale,
                    t);
                yield return null;
            }

            if (target != null)
            {
                target.localPosition = finalPosition;
                target.localScale = finalScale;
                // The model has reached its full size now; recalculate only
                // the panel position using the final rendered bounds.
                PositionPresentationRoots(
                    ResolvePresentationCamera(),
                    ResolveSelectionAnchorById(displayedMaterialId));
                CraftLiveAudio.Play(CraftLiveSound.PaintingImpact, 0.55f);
            }

            if (spin != null)
            {
                spin.enabled = true;
            }

            revealCoroutine = null;
        }

        private IEnumerator ReturnPreview()
        {
            Transform target = spawnedModel != null
                ? spawnedModel.transform
                : null;
            CraftLivePreviewSpin spin = target != null
                ? target.GetComponent<CraftLivePreviewSpin>()
                : null;
            if (spin != null)
            {
                spin.enabled = false;
            }

            if (target != null)
            {
                Vector3 startPosition = target.localPosition;
                Vector3 startScale = target.localScale;
                float elapsed = 0f;
                while (target != null && elapsed < revealDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / revealDuration);
                    t = 1f - Mathf.Pow(1f - t, 3f);
                    target.localPosition = Vector3.LerpUnclamped(
                        startPosition,
                        revealHiddenLocalPosition,
                        t);
                    target.localScale = Vector3.LerpUnclamped(
                        startScale,
                        revealHiddenScale,
                        t);
                    yield return null;
                }

                if (target != null)
                {
                    target.localPosition = revealHiddenLocalPosition;
                    target.localScale = revealHiddenScale;
                }
            }

            returnCoroutine = null;
            CraftLiveRoomState state = session != null
                ? session.State
                : null;
            if (state != null &&
                (state.placement.status ==
                     CraftLivePlacementStatus.SelectingSlot ||
                 state.placement.status ==
                     CraftLivePlacementStatus.ConfirmingSlot))
            {
                session.CancelPlacement();
            }
            else
            {
                ClearPreview();
                Publish(null, false);
            }
        }

        private void StopReveal()
        {
            if (revealCoroutine != null)
            {
                StopCoroutine(revealCoroutine);
                revealCoroutine = null;
            }
        }

        private void StopReturn()
        {
            if (returnCoroutine != null)
            {
                StopCoroutine(returnCoroutine);
                returnCoroutine = null;
            }
        }

        private static void FitModel(
            Transform model,
            float targetSize)
        {
            Renderer[] renderers =
                model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                model.localScale = Vector3.one * targetSize;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float largest = Mathf.Max(
                bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z));
            if (largest > 0.0001f)
            {
                model.localScale *= targetSize / largest;
            }
        }

        private static void ApplyColor(
            Renderer target,
            Color color,
            bool emission)
        {
            if (target == null)
            {
                return;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            block.SetColor(
                "_EmissionColor",
                emission ? color * 0.2f : Color.black);
            target.SetPropertyBlock(block);
        }

        private static void PrepareParticlePreview(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            foreach (ParticleSystem particles in
                     target.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                particles.Simulate(0.12f, true, true, true);
                particles.Play(true);
            }
        }

        private static bool IsParticleOnlyVisual(GameObject target)
        {
            return target != null &&
                   target.GetComponentInChildren<ParticleSystem>(true) != null &&
                   target.GetComponentInChildren<MeshRenderer>(true) == null &&
                   target.GetComponentInChildren<SpriteRenderer>(true) == null;
        }

        private static void CreateParticlePreviewCore(
            Transform parent,
            Color color,
            float targetSize)
        {
            GameObject core = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            core.name = "ParticlePreviewCore";
            core.transform.SetParent(parent, false);
            core.transform.localScale =
                Vector3.one * Mathf.Max(0.08f, targetSize * 0.28f);
            Collider collider = core.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            ApplyColor(core.GetComponent<Renderer>(), color, true);
        }

        private void CreateReliableFirePreview(
            Transform parent,
            Color color,
            float targetSize)
        {
            if (parent == null)
            {
                return;
            }

            GameObject effect = new GameObject("ReliableFirePreview");
            effect.transform.SetParent(parent, false);
            effect.transform.localPosition =
                new Vector3(0f, -targetSize * 0.2f, -targetSize * 0.18f);

            ParticleSystem particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 48;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                targetSize * 0.18f,
                targetSize * 0.42f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                targetSize * 0.07f,
                targetSize * 0.16f);
            Color hot = Color.Lerp(color, Color.yellow, 0.55f);
            Color warm = new Color(color.r, color.g, color.b, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(hot, warm);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 28f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 13f;
            shape.radius = targetSize * 0.08f;
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = targetSize * 0.08f;
            noise.frequency = 0.75f;

            ParticleSystemRenderer particleRenderer =
                effect.GetComponent<ParticleSystemRenderer>();
            generatedFirePreviewMaterial =
                CraftLiveForgeUITheme.CreateCompatibleParticleMaterial(
                    "Generated_Pad1FirePreviewMaterial");
            if (generatedFirePreviewMaterial != null)
            {
                particleRenderer.sharedMaterial =
                    generatedFirePreviewMaterial;
            }

            particles.Simulate(0.18f, true, true, true);
            particles.Play(true);
        }

        private static string WrapText(
            string value,
            int charactersPerLine,
            int maximumLines)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            int lineLength = 0;
            int lineCount = 1;
            foreach (char character in value)
            {
                if (character == '\r')
                {
                    continue;
                }

                if (character == '\n' ||
                    lineLength >= charactersPerLine)
                {
                    if (lineCount >= maximumLines)
                    {
                        builder.Append("...");
                        break;
                    }

                    builder.Append('\n');
                    lineCount++;
                    lineLength = 0;
                    if (character == '\n')
                    {
                        continue;
                    }
                }

                builder.Append(character);
                lineLength++;
            }

            return builder.ToString();
        }

        private void Update()
        {
            if (!dismissOnOutsideTap ||
                string.IsNullOrWhiteSpace(displayedMaterialId) ||
                fallbackHologram == null ||
                !fallbackHologram.activeInHierarchy)
            {
                return;
            }

            if (!TryGetTapPosition(out Vector2 screenPosition) ||
                IsTapInsidePreview(screenPosition))
            {
                return;
            }

            if (session != null &&
                session.State != null &&
                (session.State.placement.status ==
                     CraftLivePlacementStatus.SelectingSlot ||
                 session.State.placement.status ==
                     CraftLivePlacementStatus.ConfirmingSlot))
            {
                session.CancelPlacement();
            }
            else
            {
                ClearPreview();
            }
        }

        private static bool TryGetTapPosition(out Vector2 position)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null &&
                touchscreen.primaryTouch.press.wasPressedThisFrame)
            {
                position = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        private bool IsTapInsidePreview(Vector2 screenPosition)
        {
            Camera presentationCamera = ResolvePresentationCamera();
            if (presentationCamera == null)
            {
                return false;
            }

            Ray ray = presentationCamera.ScreenPointToRay(screenPosition);
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                outsideTapMaxDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                Transform hitTransform = hit.transform;
                if (hitTransform == null)
                {
                    continue;
                }

                if (fallbackHologram != null &&
                    hitTransform.IsChildOf(fallbackHologram.transform))
                {
                    return true;
                }
                if (fallbackTransferButton != null &&
                    (hitTransform == fallbackTransferButton ||
                     hitTransform.IsChildOf(fallbackTransferButton)))
                {
                    return true;
                }
                if (hitTransform.GetComponentInParent<
                        CraftLiveSpringDragHandle>() != null)
                {
                    return true;
                }
                if (spawnedModel != null &&
                    hitTransform.IsChildOf(spawnedModel.transform))
                {
                    return true;
                }
                if (selectionAnchor != null &&
                    (hitTransform == selectionAnchor ||
                     hitTransform.IsChildOf(selectionAnchor)))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (displayAreaTopLeft == null ||
                displayAreaTopRight == null ||
                displayAreaBottomRight == null ||
                displayAreaBottomLeft == null)
            {
                return;
            }

            Vector3 topLeft = displayAreaTopLeft.position;
            Vector3 topRight = displayAreaTopRight.position;
            Vector3 bottomRight = displayAreaBottomRight.position;
            Vector3 bottomLeft = displayAreaBottomLeft.position;
            Gizmos.color = displayAreaGizmoColor;
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
            Gizmos.DrawLine(topLeft, bottomRight);
            Gizmos.DrawLine(topRight, bottomLeft);

            float averageEdgeLength =
                ((topRight - topLeft).magnitude +
                 (bottomRight - topRight).magnitude +
                 (bottomLeft - bottomRight).magnitude +
                 (topLeft - bottomLeft).magnitude) * 0.25f;
            float pointRadius = Mathf.Clamp(
                averageEdgeLength * 0.025f,
                0.005f,
                0.08f);
            Gizmos.DrawSphere(topLeft, pointRadius);
            Gizmos.DrawSphere(topRight, pointRadius);
            Gizmos.DrawSphere(bottomRight, pointRadius);
            Gizmos.DrawSphere(bottomLeft, pointRadius);
        }

        private void OnValidate()
        {
            targetModelSize = Mathf.Clamp(targetModelSize, 0.1f, 3f);
            modelCameraApproach = Mathf.Max(0f, modelCameraApproach);
            modelViewportPosition.x = Mathf.Clamp(
                modelViewportPosition.x,
                0.05f,
                0.95f);
            modelViewportPosition.y = Mathf.Clamp(
                modelViewportPosition.y,
                0.05f,
                0.95f);
            hologramPanelOpacity = Mathf.Clamp(
                hologramPanelOpacity,
                0.05f,
                1f);
            hologramBorderGlow = Mathf.Clamp(
                hologramBorderGlow,
                0.5f,
                6f);
            hologramBorderThickness = Mathf.Clamp(
                hologramBorderThickness,
                0.005f,
                0.08f);
            hologramPanelSizeMultiplier.x = Mathf.Clamp(
                hologramPanelSizeMultiplier.x,
                0.25f,
                10f);
            hologramPanelSizeMultiplier.y = Mathf.Clamp(
                hologramPanelSizeMultiplier.y,
                0.25f,
                10f);
            hologramCameraApproach = Mathf.Max(
                0f,
                hologramCameraApproach);
            hologramGroupScale = Mathf.Clamp(
                hologramGroupScale,
                0.1f,
                2f);
            hologramScreenMargin = Mathf.Clamp(
                hologramScreenMargin,
                0f,
                0.2f);
            displayAreaPadding = Mathf.Clamp(
                displayAreaPadding,
                0f,
                0.2f);
            displayAreaDepthOffset = Mathf.Clamp(
                displayAreaDepthOffset,
                -3f,
                3f);
            hologramGeneratedFontSize = Mathf.Clamp(
                hologramGeneratedFontSize,
                8,
                256);
            hologramMaxCharacterSize = Mathf.Clamp(
                hologramMaxCharacterSize,
                0.008f,
                0.05f);
            transferButtonHeight = Mathf.Clamp(
                transferButtonHeight,
                0.12f,
                0.5f);
            hologramButtonOpacity = Mathf.Clamp(
                hologramButtonOpacity,
                0.05f,
                0.8f);
            buttonFontSize = Mathf.Clamp(
                buttonFontSize,
                8,
                256);
            buttonTextSize = Mathf.Clamp(
                buttonTextSize,
                0.1f,
                2f);
            returnButtonSize.x = Mathf.Max(0.05f, returnButtonSize.x);
            returnButtonSize.y = Mathf.Max(0.05f, returnButtonSize.y);
            ResizeTransferButton(
                fallbackPanelWidth,
                fallbackPanelHeight);
            PositionReturnButton();
            outsideTapMaxDistance = Mathf.Max(
                1f,
                outsideTapMaxDistance);
            spinDegreesPerSecond =
                Mathf.Max(0f, spinDegreesPerSecond);
            revealDuration = Mathf.Max(0.05f, revealDuration);
        }
    }

    public sealed class CraftLivePreviewSpin : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float degreesPerSecond = 18f;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.06f;
        [SerializeField, Min(0f)] private float bobSpeed = 1.6f;

        private Vector3 basePosition;

        private void OnEnable()
        {
            basePosition = transform.localPosition;
        }

        public void Configure(float speed)
        {
            degreesPerSecond = Mathf.Max(0f, speed);
            basePosition = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(
                Vector3.up,
                degreesPerSecond * Time.unscaledDeltaTime,
                Space.Self);
            Vector3 position = basePosition;
            position.y +=
                Mathf.Sin(Time.unscaledTime * bobSpeed) *
                bobAmplitude;
            transform.localPosition = position;
        }
    }
}
