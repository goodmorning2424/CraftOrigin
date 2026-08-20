using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad3Controller :
        MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad3Bindings bindings;
        [SerializeField] private CraftLiveQrScanner qrScanner;

        [Header("Fallback UI")]
        [SerializeField] private bool createFallbackVisuals = true;
        [SerializeField] private Color attackColor =
            new Color(0.88f, 0.17f, 0.12f, 1f);
        [SerializeField] private Color defenseColor =
            new Color(0.12f, 0.48f, 0.88f, 1f);
        [SerializeField] private Color evasionColor =
            new Color(0.18f, 0.82f, 0.48f, 1f);
        [SerializeField] private bool overrideTubeColors;
        [SerializeField] private Vector3 tubeLabelLocalPosition =
            new Vector3(-2.85f, 0f, -0.5f);
        [SerializeField, Range(0.03f, 0.12f)]
        [Tooltip("攻撃力・防御力・回避率ラベルの文字サイズです。")]
        private float tubeLabelCharacterSize = 0.065f;
        [Header("QR Button Layout")]
        [SerializeField] private Color qrButtonColor =
            new Color(0.9f, 0.27f, 0.075f, 1f);
        [Tooltip("生成される下部ボタンの幅・高さ・厚みです。")]
        [SerializeField] private Vector3 qrButtonSize =
            new Vector3(3.6f, 0.9f, 0.3f);
        [Tooltip("木板中央からの位置です。x/yは板の半幅・半高さに対する割合です。")]
        [SerializeField] private Vector2 qrButtonPanelPosition =
            new Vector2(0.27f, -0.3f);
        [Tooltip("右・上・カメラ方向への追加移動量です。")]
        [SerializeField] private Vector3 qrButtonPositionOffset = Vector3.zero;
        [SerializeField, Min(0f)] private float qrButtonCameraOffset = 1.05f;
        [SerializeField, Min(0f)] private float qrFeedbackCameraOffset = 0.82f;

        [Header("Events")]
        [SerializeField] private UnityEvent<bool>
            onScanningChanged;
        [SerializeField] private UnityEvent<string>
            onFeedbackChanged;
        [SerializeField] private UnityEvent<string>
            onRegisteredMaterialChanged;
        [SerializeField] private UnityEvent<bool>
            onNewRegistration;

        private TextMesh feedbackText;
        private GameObject generatedQrVisual;
        private GameObject generatedQrHousing;
        private int observedRegistrationSerial = -1;

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
            ApplyPhysicalLayout();
            BuildTubes();
            BuildQrFallback();
            Refresh(session != null ? session.State : null);
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }

            if (qrScanner != null)
            {
                qrScanner.ScanCompleted -= HandleScanCompleted;
                qrScanner.ScanFailed -= HandleScanFailed;
                qrScanner.ScanCancelled -= HandleScanCancelled;
            }
        }

        public void Configure(
            CraftLivePad3Bindings targetBindings,
            CraftLiveQrScanner targetScanner)
        {
            bindings = targetBindings;
            qrScanner = targetScanner;
            ResolveReferences();
        }

        public void StartQrScan()
        {
            if (qrScanner == null)
            {
                SetFeedback("QR Scannerが設定されていません。");
                return;
            }

            SetFeedback("QRコードをカメラへ向けてください");
            onScanningChanged?.Invoke(true);
            qrScanner.StartScan();
            if (!qrScanner.IsScanning)
            {
                onScanningChanged?.Invoke(false);
            }
        }

        public void StopQrScan()
        {
            qrScanner?.StopScan();
            onScanningChanged?.Invoke(false);
            SetFeedback("読み取りを停止しました");
        }

        public void SubmitQrPayload(string payload)
        {
            qrScanner?.OnQrScanResult(payload);
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Register First QR Material")]
        private void DebugRegisterFirstQrMaterial()
        {
            ResolveReferences();
            if (!Application.isPlaying ||
                session == null ||
                session.Catalog == null ||
                qrScanner == null)
            {
                Debug.LogWarning(
                    "Craft-live: Play Modeで実行してください。",
                    this);
                return;
            }

            foreach (CraftLiveMaterialDefinition material in
                     session.Catalog.Materials)
            {
                if (material != null &&
                    material.RequiresQrUnlock)
                {
                    qrScanner.OnQrScanResult(
                        $"craftlive:material:{material.MaterialId}");
                    return;
                }
            }
        }

        [ContextMenu("Debug/Preview Tube Values")]
        private void DebugPreviewTubeValues()
        {
            if (!Application.isPlaying || bindings == null)
            {
                Debug.LogWarning(
                    "Craft-live: Play Modeで実行してください。",
                    this);
                return;
            }

            PreviewTube(bindings.AttackTubeRoot, 75f);
            PreviewTube(bindings.DefenseTubeRoot, 50f);
            PreviewTube(bindings.EvasionTubeRoot, 30f);
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
                bindings = GetComponent<CraftLivePad3Bindings>();
            }

            if (qrScanner == null && bindings != null &&
                bindings.QrReadButtonRoot != null)
            {
                qrScanner =
                    bindings.QrReadButtonRoot.GetComponent<
                        CraftLiveQrScanner>();
            }

            qrScanner?.Configure(session);
        }

        private void Subscribe()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
                session.StateChanged += Refresh;
            }

            if (qrScanner != null)
            {
                qrScanner.ScanCompleted -= HandleScanCompleted;
                qrScanner.ScanCompleted += HandleScanCompleted;
                qrScanner.ScanFailed -= HandleScanFailed;
                qrScanner.ScanFailed += HandleScanFailed;
                qrScanner.ScanCancelled -= HandleScanCancelled;
                qrScanner.ScanCancelled += HandleScanCancelled;
            }
        }

        private void BuildTubes()
        {
            if (bindings == null)
            {
                return;
            }

            EnsureTube(
                bindings.AttackTubeRoot,
                CraftLiveStatType.AttackRate,
                attackColor,
                "攻撃力");
            EnsureTube(
                bindings.DefenseTubeRoot,
                CraftLiveStatType.DefenseRate,
                defenseColor,
                "防御力");
            EnsureTube(
                bindings.EvasionTubeRoot,
                CraftLiveStatType.EvasionRate,
                evasionColor,
                "回避率");
        }

        private void ApplyPhysicalLayout()
        {
            if (bindings == null)
            {
                return;
            }

            Camera camera = bindings.ReferenceCamera != null
                ? bindings.ReferenceCamera
                : Camera.main;
            Renderer wood = bindings.WoodPanel;
            if (bindings.NoticeBoard != null)
            {
                bindings.NoticeBoard.Configure(
                    session,
                    camera,
                    wood,
                    bindings.WoodPanelLayers);
            }

            if (camera == null || wood == null)
            {
                return;
            }

            Bounds bounds = wood.bounds;
            Vector3 cameraDirection =
                (camera.transform.position - bounds.center).normalized;
            float frontDistance = FindCameraFacingSurfaceDistance(
                bounds.center,
                cameraDirection,
                bindings.WoodPanelLayers,
                wood);
            Vector3 frontCenter = bounds.center +
                                  cameraDirection * frontDistance;

            PositionRootOnPanel(
                bindings.QrReadButtonRoot,
                frontCenter + camera.transform.right *
                    (bounds.extents.x * qrButtonPanelPosition.x +
                     qrButtonPositionOffset.x) +
                camera.transform.up *
                    (bounds.extents.y * qrButtonPanelPosition.y +
                     qrButtonPositionOffset.y) +
                cameraDirection *
                    (qrButtonCameraOffset + qrButtonPositionOffset.z),
                camera);
            PositionRootOnPanel(
                bindings.QrFeedbackRoot,
                frontCenter + camera.transform.right *
                    (bounds.extents.x * 0.27f) -
                camera.transform.up * (bounds.extents.y * 0.43f) +
                cameraDirection * qrFeedbackCameraOffset,
                camera);
        }

        private static float FindCameraFacingSurfaceDistance(
            Vector3 origin,
            Vector3 cameraDirection,
            Renderer[] layers,
            Renderer fallback)
        {
            float maximum = GetFacingExtent(
                origin,
                cameraDirection,
                fallback != null ? fallback.bounds : new Bounds(origin, Vector3.zero));
            if (layers == null)
            {
                return maximum;
            }

            foreach (Renderer layer in layers)
            {
                if (layer != null && layer.enabled)
                {
                    maximum = Mathf.Max(
                        maximum,
                        GetFacingExtent(
                            origin,
                            cameraDirection,
                            layer.bounds));
                }
            }

            return maximum;
        }

        private static float GetFacingExtent(
            Vector3 origin,
            Vector3 direction,
            Bounds bounds)
        {
            return Vector3.Dot(bounds.center - origin, direction) +
                   Mathf.Abs(direction.x) * bounds.extents.x +
                   Mathf.Abs(direction.y) * bounds.extents.y +
                   Mathf.Abs(direction.z) * bounds.extents.z;
        }

        private static void PositionRootOnPanel(
            Transform root,
            Vector3 position,
            Camera camera)
        {
            if (root == null || camera == null)
            {
                return;
            }

            root.SetPositionAndRotation(
                position,
                camera.transform.rotation);
        }

        private void EnsureTube(
            Transform root,
            CraftLiveStatType statType,
            Color color,
            string label)
        {
            if (root == null)
            {
                return;
            }

            CraftLiveStatusTubeView tube =
                root.GetComponent<CraftLiveStatusTubeView>();
            if (tube == null)
            {
                tube = root.gameObject.AddComponent<
                    CraftLiveStatusTubeView>();
            }

            if (overrideTubeColors)
            {
                tube.Configure(session, statType, color);
            }
            else
            {
                tube.Configure(session, statType);
            }
            if (createFallbackVisuals &&
                root.Find("Generated_TubeLabel") == null)
            {
                Transform labelParent = bindings.UiRoot != null
                    ? bindings.UiRoot
                    : root;
                TextMesh tubeLabel = CreateText(
                    labelParent,
                    "Generated_TubeLabel",
                    label,
                    Vector3.zero,
                    tubeLabelCharacterSize);
                PositionTubeLabel(tubeLabel, root);
            }
        }

        private void PositionTubeLabel(
            TextMesh label,
            Transform tubeRoot)
        {
            if (label == null || tubeRoot == null)
            {
                return;
            }

            Camera camera = bindings != null &&
                            bindings.ReferenceCamera != null
                ? bindings.ReferenceCamera
                : Camera.main;
            Renderer tubeRenderer =
                tubeRoot.GetComponentInChildren<Renderer>();
            if (camera == null || tubeRenderer == null)
            {
                label.transform.localPosition =
                    tubeLabelLocalPosition;
                return;
            }

            Bounds bounds = tubeRenderer.bounds;
            Vector3 position = bounds.center -
                               camera.transform.right *
                               (bounds.extents.x + 0.38f) +
                               (camera.transform.position - bounds.center)
                                   .normalized * 0.06f;
            label.transform.SetPositionAndRotation(
                position,
                camera.transform.rotation);
        }

        private void BuildQrFallback()
        {
            if (!createFallbackVisuals ||
                bindings == null ||
                bindings.QrReadButtonRoot == null)
            {
                return;
            }

            DestroySafely(generatedQrVisual);
            DestroySafely(generatedQrHousing);
            generatedQrHousing = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            generatedQrHousing.name = "Generated_QrButtonHousing";
            generatedQrHousing.transform.SetParent(
                bindings.QrReadButtonRoot,
                false);
            generatedQrHousing.transform.localPosition =
                new Vector3(0f, 0f, 0.14f);
            generatedQrHousing.transform.localScale = new Vector3(
                qrButtonSize.x * 1.2f,
                qrButtonSize.y * 1.52f,
                Mathf.Max(0.42f, qrButtonSize.z * 1.35f));
            DestroySafely(
                generatedQrHousing.GetComponent<Collider>());
            generatedQrHousing.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();
            CraftLiveForgeUITheme.ApplyForgeSurface(
                generatedQrHousing.GetComponent<Renderer>(),
                new Color(0.035f, 0.055f, 0.07f, 1f),
                0.12f,
                0.9f,
                0.58f);

            generatedQrVisual = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            generatedQrVisual.name = "Generated_QrReadButton";
            generatedQrVisual.transform.SetParent(
                bindings.QrReadButtonRoot,
                false);
            generatedQrVisual.transform.localPosition =
                new Vector3(0f, 0f, -0.18f);
            generatedQrVisual.transform.localScale =
                qrButtonSize;
            generatedQrVisual.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();
            Renderer renderer =
                generatedQrVisual.GetComponent<Renderer>();
            ApplyColor(
                renderer,
                qrButtonColor);
            CraftLiveWorldButton button =
                generatedQrVisual.AddComponent<
                    CraftLiveWorldButton>();
            button.Configure(
                generatedQrVisual.transform,
                new[] { renderer },
                qrButtonColor,
                Color.Lerp(qrButtonColor, CraftLiveForgeUITheme.Brass, 0.42f),
                Color.Lerp(qrButtonColor, CraftLiveForgeUITheme.ParchmentText, 0.35f));
            button.AddListener(StartQrScan);
            TextMesh qrLabel = CreateText(
                generatedQrVisual.transform,
                "Label",
                "読み取り開始",
                new Vector3(0f, 0f, -0.62f),
                0.052f);
            qrLabel.text = "QR 読み取り開始";
            TextMesh scannerKicker = CreateText(
                bindings.QrReadButtonRoot,
                "Generated_QrScannerKicker",
                "QR SCANNER",
                new Vector3(0f, qrButtonSize.y * 0.88f, -0.28f),
                0.028f);
            scannerKicker.color = Color.Lerp(
                qrButtonColor,
                Color.white,
                0.38f);

            if (bindings.QrFeedbackRoot != null)
            {
                feedbackText = CreateText(
                    bindings.QrFeedbackRoot,
                    "Generated_QrFeedback",
                    "QRコードを読み取って素材を登録",
                    new Vector3(0f, 0f, -0.5f),
                    0.045f);
            }
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null ||
                state.registrationSerial ==
                    observedRegistrationSerial)
            {
                return;
            }

            observedRegistrationSerial =
                state.registrationSerial;
            if (state.registrationSerial <= 0 ||
                string.IsNullOrWhiteSpace(
                    state.lastRegisteredMaterialId))
            {
                return;
            }

            CraftLiveMaterialDefinition material =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(
                        state.lastRegisteredMaterialId)
                    : null;
            string displayName = material != null
                ? material.DisplayName
                : state.lastRegisteredMaterialId;
            bool newlyRegistered =
                state.lastRegistrationDelta > 0;
            SetFeedback(
                newlyRegistered
                    ? $"{displayName}を登録しました"
                    : $"{displayName}は登録済みです");
            onRegisteredMaterialChanged?.Invoke(displayName);
            onNewRegistration?.Invoke(newlyRegistered);
        }

        private void HandleScanCompleted(
            string materialId,
            bool newlyRegistered)
        {
            onScanningChanged?.Invoke(false);
            CraftLiveAudio.Play(CraftLiveSound.RareReveal, 0.52f);
            if (session == null)
            {
                return;
            }

            CraftLiveMaterialDefinition material =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(materialId)
                    : null;
            string displayName = material != null
                ? material.DisplayName
                : materialId;
            SetFeedback(
                newlyRegistered
                    ? $"{displayName}を登録しました"
                    : $"{displayName}は登録済みです");
        }

        private static void PreviewTube(
            Transform root,
            float value)
        {
            CraftLiveStatusTubeView tube =
                root != null
                    ? root.GetComponent<CraftLiveStatusTubeView>()
                    : null;
            tube?.PreviewValue(value);
        }

        private void HandleScanFailed(string message)
        {
            onScanningChanged?.Invoke(false);
            CraftLiveAudio.Play(CraftLiveSound.Cancel, 0.72f);
            SetFeedback(message);
        }

        private void HandleScanCancelled()
        {
            onScanningChanged?.Invoke(false);
            SetFeedback("読み取りをキャンセルしました");
        }

        private void SetFeedback(string value)
        {
            value = value ?? string.Empty;
            if (feedbackText != null)
            {
                feedbackText.text = value;
            }

            onFeedbackChanged?.Invoke(value);
        }

        private static TextMesh CreateText(
            Transform parent,
            string name,
            string value,
            Vector3 position,
            float characterSize)
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
                characterSize,
                CraftLiveForgeUITheme.ParchmentText);
            return text;
        }

        private static void ApplyColor(
            Renderer renderer,
            Color color)
        {
            if (renderer == null)
            {
                return;
            }

            CraftLiveForgeUITheme.ApplyForgeSurface(renderer, color);
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
    }
}
