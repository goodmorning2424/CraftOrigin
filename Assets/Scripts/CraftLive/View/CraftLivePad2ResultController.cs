using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad2ResultController :
        MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad2Bindings bindings;
        [SerializeField] private bool createFallbackVisuals = true;
        [SerializeField, Min(0f)] private float staffRestartDelaySeconds = 12f;
        [Header("Start Screen Transition")]
        [SerializeField, Min(0.05f)] private float startScreenSlideDuration = 0.85f;
        [SerializeField, Min(0.1f)] private float startScreenSlideDistance = 12f;
        [Header("Start Tutorial Video")]
        [SerializeField]
        [Tooltip("Pad2のクラフト開始後に再生する説明動画です。未設定の場合は動画を待たず開始します。")]
        private VideoClip startTutorialVideo;
        [SerializeField, Min(1f)]
        [Tooltip("動画の準備に失敗して進行不能になることを防ぐ待機上限です。")]
        private float tutorialPrepareTimeoutSeconds = 10f;
        [Header("Text Readability")]
        [SerializeField, Range(0.026f, 0.08f)]
        [Tooltip("完成結果に表示する攻撃・防御・回避ラベルの文字サイズです。")]
        private float resultStatLabelSize = 0.04f;
        [SerializeField, Range(0.035f, 0.09f)]
        [Tooltip("完成結果に表示する属性・技能行の文字サイズです。")]
        private float resultTraitTextSize = 0.05f;
        [Header("Remaining Time Warnings")]
        [SerializeField, Min(0.5f)] private float timeWarningPopupDuration = 3f;
        [SerializeField] private UnityEvent<bool> onResultVisible;
        [SerializeField] private UnityEvent<string> onWeaponNameChanged;
        [SerializeField] private UnityEvent<string> onRankChanged;
        [SerializeField] private UnityEvent<float> onAttackChanged;
        [SerializeField] private UnityEvent<float> onDefenseChanged;
        [SerializeField] private UnityEvent<float> onEvasionChanged;
        [SerializeField] private UnityEvent<string> onAttributeChanged;
        [SerializeField] private UnityEvent<string> onSkillChanged;
        [SerializeField] private UnityEvent<int> onHistoryCountChanged;
        [SerializeField] private UnityEvent<string> onWeaponCodeChanged;

        private GameObject generatedPanel;
        private int displayedResultSerial = -1;
        private int displayedHistoryCount = -1;
        private string displayedGroupNumber = string.Empty;
        private bool displayedCompletionReady;
        private CraftLiveSessionPhase displayedPhase =
            (CraftLiveSessionPhase)(-1);
        private Coroutine staffRestartRoutine;
        private Coroutine startScreenSlideRoutine;
        private Coroutine tutorialPlaybackRoutine;
        private GameObject departingStartPanel;
        private GameObject tutorialPlaceholder;
        private TextMesh tutorialPlaceholderLabel;
        private CraftLiveWorldButton tutorialStartButton;
        private CraftLiveTutorialVideoTapSurface tutorialTapSurface;
        private VideoPlayer tutorialVideoPlayer;
        private RenderTexture tutorialRenderTexture;
        private bool tutorialPrepared;
        private bool tutorialFinished;
        private bool tutorialFailed;
        private bool tutorialAwaitingTap;
        private int warningGroupGeneration = -1;
        private bool minuteWarningShown;
        private bool thirtySecondWarningShown;
        private string timeWarningMessage = string.Empty;
        private float timeWarningStartedAt = float.NegativeInfinity;
        private GUIStyle timeWarningStyle;

        public bool TutorialAwaitingTap => tutorialAwaitingTap;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            RefreshTimeWarning();
        }

        private void OnGUI()
        {
            if (string.IsNullOrWhiteSpace(timeWarningMessage))
            {
                return;
            }

            float elapsed = Time.realtimeSinceStartup - timeWarningStartedAt;
            float duration = Mathf.Max(0.5f, timeWarningPopupDuration);
            if (elapsed >= duration)
            {
                timeWarningMessage = string.Empty;
                return;
            }

            if (timeWarningStyle == null)
            {
                timeWarningStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Clamp(
                        Mathf.RoundToInt(Screen.height * 0.052f),
                        32,
                        64),
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                timeWarningStyle.normal.textColor =
                    new Color(1f, 0.91f, 0.58f);
            }

            float fade = Mathf.Min(
                Mathf.Clamp01(elapsed / 0.18f),
                Mathf.Clamp01((duration - elapsed) / 0.35f));
            float width = Mathf.Min(Screen.width * 0.82f, 760f);
            float height = Mathf.Clamp(Screen.height * 0.13f, 96f, 150f);
            float slide = (1f - Mathf.Clamp01(elapsed / 0.22f)) * -height;
            Rect popup = new Rect(
                (Screen.width - width) * 0.5f,
                Mathf.Max(18f, Screen.height * 0.035f) + slide,
                width,
                height);

            Color previousColor = GUI.color;
            Color previousBackground = GUI.backgroundColor;
            int previousDepth = GUI.depth;
            GUI.depth = -1100;
            GUI.color = new Color(1f, 1f, 1f, fade);
            GUI.backgroundColor = new Color(0.1f, 0.055f, 0.018f, 0.96f);
            GUI.Box(popup, timeWarningMessage, timeWarningStyle);
            GUI.color = previousColor;
            GUI.backgroundColor = previousBackground;
            GUI.depth = previousDepth;
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (session != null)
            {
                session.StateChanged -= Refresh;
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

            if (staffRestartRoutine != null)
            {
                StopCoroutine(staffRestartRoutine);
                staffRestartRoutine = null;
            }

            if (startScreenSlideRoutine != null)
            {
                StopCoroutine(startScreenSlideRoutine);
                startScreenSlideRoutine = null;
            }

            CancelTutorialPlayback();
            timeWarningMessage = string.Empty;

            DestroySafely(departingStartPanel);
            departingStartPanel = null;
        }

        private void RefreshTimeWarning()
        {
            if (session == null || session.State == null)
            {
                return;
            }

            CraftLiveRoomState state = session.State;
            if (warningGroupGeneration != state.groupGeneration)
            {
                warningGroupGeneration = state.groupGeneration;
                minuteWarningShown = false;
                thirtySecondWarningShown = false;
                timeWarningMessage = string.Empty;
            }

            bool timedPhase =
                state.sessionEndsAtUnixMs > 0 &&
                (state.sessionPhase == CraftLiveSessionPhase.StartScreen ||
                 state.sessionPhase == CraftLiveSessionPhase.Playing);
            if (!timedPhase)
            {
                return;
            }

            int warning = ResolveTimeWarningSecond(
                session.GetRemainingSessionSeconds(),
                minuteWarningShown,
                thirtySecondWarningShown);
            if (warning == 60)
            {
                minuteWarningShown = true;
                ShowTimeWarning(BuildTimeWarningMessage(warning));
            }
            else if (warning == 30)
            {
                minuteWarningShown = true;
                thirtySecondWarningShown = true;
                ShowTimeWarning(BuildTimeWarningMessage(warning));
            }
        }

        public static string BuildTimeWarningMessage(int remainingSeconds)
        {
            return $"残り{Mathf.Max(0, remainingSeconds)}秒";
        }

        public static int ResolveTimeWarningSecond(
            float remainingSeconds,
            bool minuteShown,
            bool thirtySecondsShown)
        {
            if (remainingSeconds <= 0f)
            {
                return 0;
            }

            if (remainingSeconds <= 30f && !thirtySecondsShown)
            {
                return 30;
            }

            if (remainingSeconds <= 60f && !minuteShown)
            {
                return 60;
            }

            return 0;
        }

        private void ShowTimeWarning(string message)
        {
            timeWarningMessage = message;
            timeWarningStartedAt = Time.realtimeSinceStartup;
            CraftLiveAudio.Play(CraftLiveSound.HeartbeatWarning, 0.62f);
        }

        public void Configure(CraftLivePad2Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
        }

        public void BeginNextWeapon()
        {
            session?.BeginNextWeapon();
        }

        public void SelectFinalWeapon(int resultSerial)
        {
            session?.SelectFinalWeapon(resultSerial);
        }

        public void RestartForNextGroup()
        {
            session?.ResetRoomForNextGroup();
        }

        public void StartGroup()
        {
            if (session == null ||
                !IsAuthoritativeStartRole(session.Role) ||
                session.State == null ||
                session.State.sessionPhase != CraftLiveSessionPhase.StartScreen ||
                tutorialPlaybackRoutine != null)
            {
                return;
            }

            if (startTutorialVideo == null)
            {
                session.StartGroup();
                return;
            }

            tutorialAwaitingTap = true;
            tutorialStartButton?.SetInteractable(false);
            if (tutorialStartButton != null)
            {
                tutorialStartButton.gameObject.SetActive(false);
            }
            tutorialTapSurface?.SetInteractable(true);
            if (tutorialPlaceholderLabel != null)
            {
                tutorialPlaceholderLabel.text = "動画をタップして再生";
            }
        }

        public void StartTutorialFromTap()
        {
            if (!tutorialAwaitingTap ||
                tutorialPlaybackRoutine != null ||
                tutorialVideoPlayer == null)
            {
                return;
            }

            tutorialAwaitingTap = false;
            tutorialTapSurface?.SetInteractable(false);
            if (tutorialPlaceholderLabel != null)
            {
                tutorialPlaceholderLabel.text = "動画を準備しています…";
            }
            tutorialPlaybackRoutine = StartCoroutine(
                PlayTutorialThenStart());
        }

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
            if (state == null)
            {
                return;
            }

            int historyCount = state.completedWeapons != null
                ? state.completedWeapons.Count
                : 0;
            bool changed =
                displayedPhase != state.sessionPhase ||
                displayedResultSerial !=
                    state.result.resultSerial ||
                displayedHistoryCount != historyCount ||
                displayedGroupNumber != (state.finalWeaponCode ?? string.Empty) ||
                displayedCompletionReady !=
                    state.craft.completionPresentationReady;
            CraftLiveSessionPhase previousPhase = displayedPhase;
            displayedPhase = state.sessionPhase;
            displayedResultSerial =
                state.result.resultSerial;
            displayedHistoryCount = historyCount;
            displayedGroupNumber = state.finalWeaponCode ?? string.Empty;
            displayedCompletionReady =
                state.craft.completionPresentationReady;

            bool visible = ShouldShowResult(state);
            onResultVisible?.Invoke(visible);
            PublishResult(state.result);
            onHistoryCountChanged?.Invoke(historyCount);
            onWeaponCodeChanged?.Invoke(
                state.finalWeaponCode ?? string.Empty);
            if (changed && createFallbackVisuals)
            {
                if (previousPhase == CraftLiveSessionPhase.StartScreen &&
                    state.sessionPhase != CraftLiveSessionPhase.StartScreen)
                {
                    BeginStartScreenSlide();
                }

                RebuildFallback(state);
            }
        }

        private void BeginStartScreenSlide()
        {
            if (generatedPanel == null ||
                generatedPanel.name != "Generated_StartScreen")
            {
                return;
            }


            CancelTutorialPlayback(false);

            if (startScreenSlideRoutine != null)
            {
                StopCoroutine(startScreenSlideRoutine);
            }

            DestroySafely(departingStartPanel);
            departingStartPanel = generatedPanel;
            generatedPanel = null;
            startScreenSlideRoutine = StartCoroutine(
                SlideStartScreenOut(departingStartPanel));
        }

        private IEnumerator SlideStartScreenOut(GameObject panel)
        {
            if (panel == null)
            {
                startScreenSlideRoutine = null;
                yield break;
            }

            Vector3 start = panel.transform.localPosition;
            Vector3 end = start + Vector3.up * startScreenSlideDistance;
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, startScreenSlideDuration);
            while (panel != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - normalized, 3f);
                panel.transform.localPosition = Vector3.LerpUnclamped(
                    start,
                    end,
                    eased);
                yield return null;
            }

            DestroySafely(panel);
            if (departingStartPanel == panel)
            {
                departingStartPanel = null;
            }

            startScreenSlideRoutine = null;
            ReleaseTutorialRenderTexture();
        }

        private IEnumerator PlayTutorialThenStart()
        {
            if (tutorialVideoPlayer == null)
            {
                CompleteTutorialAndStart();
                yield break;
            }

            tutorialPrepared = false;
            tutorialFinished = false;
            tutorialFailed = false;
            tutorialVideoPlayer.prepareCompleted += HandleTutorialPrepared;
            tutorialVideoPlayer.started += HandleTutorialStarted;
            tutorialVideoPlayer.loopPointReached += HandleTutorialFinished;
            tutorialVideoPlayer.errorReceived += HandleTutorialError;
            tutorialVideoPlayer.Prepare();

            float prepareElapsed = 0f;
            while (!tutorialPrepared && !tutorialFailed &&
                   prepareElapsed < tutorialPrepareTimeoutSeconds)
            {
                prepareElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!tutorialPrepared || tutorialFailed)
            {
                CompleteTutorialAndStart();
                yield break;
            }

            if (tutorialPlaceholder != null)
            {
                tutorialPlaceholder.SetActive(false);
            }

            tutorialVideoPlayer.Play();
            double clipLength = startTutorialVideo != null
                ? startTutorialVideo.length
                : 0d;
            float playbackTimeout = Mathf.Max(
                tutorialPrepareTimeoutSeconds,
                (float)clipLength + 5f);
            float playbackElapsed = 0f;
            while (!tutorialFinished && !tutorialFailed &&
                   playbackElapsed < playbackTimeout)
            {
                playbackElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            CompleteTutorialAndStart();
        }

        private void HandleTutorialPrepared(VideoPlayer source)
        {
            tutorialPrepared = true;
        }

        private void HandleTutorialStarted(VideoPlayer source)
        {
            session?.BeginTimedIntroduction();
        }

        private void HandleTutorialFinished(VideoPlayer source)
        {
            tutorialFinished = true;
        }

        private void HandleTutorialError(VideoPlayer source, string message)
        {
            tutorialFailed = true;
            Debug.LogWarning(
                $"Craft-live start tutorial could not play: {message}",
                this);
        }

        private void CompleteTutorialAndStart()
        {
            UnsubscribeTutorialPlayer();
            tutorialPlaybackRoutine = null;
            if (session != null &&
                IsAuthoritativeStartRole(session.Role) &&
                session.State != null &&
                session.State.sessionPhase == CraftLiveSessionPhase.StartScreen)
            {
                session.StartGroup();
            }
        }

        public static bool IsAuthoritativeStartRole(CraftLiveRole role)
        {
            return role == CraftLiveRole.WorkbenchPad;
        }

        private void CancelTutorialPlayback(bool releaseTexture = true)
        {
            if (tutorialPlaybackRoutine != null)
            {
                StopCoroutine(tutorialPlaybackRoutine);
                tutorialPlaybackRoutine = null;
            }

            UnsubscribeTutorialPlayer();
            if (tutorialVideoPlayer != null)
            {
                tutorialVideoPlayer.Stop();
                tutorialVideoPlayer = null;
            }

            tutorialStartButton = null;
            tutorialTapSurface = null;
            tutorialPlaceholder = null;
            tutorialPlaceholderLabel = null;
            tutorialAwaitingTap = false;
            if (releaseTexture)
            {
                ReleaseTutorialRenderTexture();
            }
        }

        private void UnsubscribeTutorialPlayer()
        {
            if (tutorialVideoPlayer == null)
            {
                return;
            }

            tutorialVideoPlayer.prepareCompleted -= HandleTutorialPrepared;
            tutorialVideoPlayer.started -= HandleTutorialStarted;
            tutorialVideoPlayer.loopPointReached -= HandleTutorialFinished;
            tutorialVideoPlayer.errorReceived -= HandleTutorialError;
        }

        private void ReleaseTutorialRenderTexture()
        {
            if (tutorialRenderTexture == null)
            {
                return;
            }

            tutorialRenderTexture.Release();
            DestroySafely(tutorialRenderTexture);
            tutorialRenderTexture = null;
        }

        private void PublishResult(CraftLiveResultState result)
        {
            if (result == null)
            {
                return;
            }

            onWeaponNameChanged?.Invoke(result.weaponName);
            onRankChanged?.Invoke(result.rank);
            onAttackChanged?.Invoke(result.stats.attackRate);
            onDefenseChanged?.Invoke(result.stats.defenseRate);
            onEvasionChanged?.Invoke(result.stats.evasionRate);
            onAttributeChanged?.Invoke(result.attributeName);
            onSkillChanged?.Invoke(result.skillName);
        }

        private void RebuildFallback(CraftLiveRoomState state)
        {
            if (staffRestartRoutine != null)
            {
                StopCoroutine(staffRestartRoutine);
                staffRestartRoutine = null;
            }

            DestroySafely(generatedPanel);
            generatedPanel = null;
            if (bindings == null ||
                bindings.ResultHologramRoot == null)
            {
                return;
            }

            if (state.sessionPhase ==
                CraftLiveSessionPhase.StartScreen)
            {
                BuildStartScreen();
                return;
            }

            if (state.sessionPhase ==
                CraftLiveSessionPhase.FinalSelection)
            {
                BuildFinalSelection(state);
                return;
            }

            if (state.sessionPhase ==
                CraftLiveSessionPhase.Finished)
            {
                BuildCodePanel(state);
                return;
            }

            if (state.craft.status ==
                    CraftLiveCraftStatus.Complete &&
                state.craft.completionPresentationReady)
            {
                BuildResultPanel(state);
            }
        }

        public static bool ShouldShowResult(CraftLiveRoomState state)
        {
            return state != null &&
                   ((state.craft.status == CraftLiveCraftStatus.Complete &&
                     state.craft.completionPresentationReady) ||
                    state.sessionPhase != CraftLiveSessionPhase.Playing);
        }

        private void BuildStartScreen()
        {
            generatedPanel = CreatePanel("Generated_StartScreen");

            Color carvedWood = new Color(0.44f, 0.19f, 0.065f);
            Color carvedInset = new Color(0.19f, 0.065f, 0.018f);
            Color carvedHighlight = new Color(0.62f, 0.31f, 0.11f);
            CreateDecorativePart(
                generatedPanel.transform,
                "CarvedWoodPlaqueShadow",
                new Vector3(0.06f, 0.35f, -0.39f),
                new Vector3(5.35f, 4.15f, 0.16f),
                new Color(0.10f, 0.03f, 0.01f),
                0f,
                0.08f,
                0.18f);
            CreateDecorativePart(
                generatedPanel.transform,
                "CarvedWoodPlaque",
                new Vector3(0f, 0.42f, -0.5f),
                new Vector3(5.15f, 3.95f, 0.13f),
                carvedWood,
                0.015f,
                0.1f,
                0.27f);

            for (int i = 0; i < 5; i++)
            {
                float y = -0.95f + i * 0.68f;
                float xOffset = i % 2 == 0 ? -0.18f : 0.22f;
                CreateDecorativePart(
                    generatedPanel.transform,
                    $"CarvedWoodGrain_{i}",
                    new Vector3(xOffset, y + 0.42f, -0.58f),
                    new Vector3(4.35f - i * 0.13f, 0.055f, 0.025f),
                    carvedInset,
                    0f,
                    0.05f,
                    0.16f);
            }

            GameObject hammerHandleGroove = CreateDecorativePart(
                generatedPanel.transform,
                "CarvedHammerHandleGroove",
                new Vector3(0.36f, 0.05f, -0.63f),
                new Vector3(0.58f, 2.95f, 0.055f),
                carvedInset,
                0f,
                0.04f,
                0.12f);
            hammerHandleGroove.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);
            GameObject hammerHandleCut = CreateDecorativePart(
                generatedPanel.transform,
                "CarvedHammerHandleHighlight",
                new Vector3(0.29f, 0.10f, -0.67f),
                new Vector3(0.24f, 2.55f, 0.035f),
                carvedHighlight,
                0f,
                0.04f,
                0.18f);
            hammerHandleCut.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);
            GameObject hammerHeadGroove = CreateDecorativePart(
                generatedPanel.transform,
                "CarvedHammerHeadGroove",
                new Vector3(-0.58f, 1.24f, -0.64f),
                new Vector3(2.25f, 0.82f, 0.06f),
                carvedInset,
                0f,
                0.05f,
                0.12f);
            hammerHeadGroove.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);
            GameObject hammerHeadCut = CreateDecorativePart(
                generatedPanel.transform,
                "CarvedHammerHeadHighlight",
                new Vector3(-0.62f, 1.20f, -0.68f),
                new Vector3(1.88f, 0.43f, 0.035f),
                carvedHighlight,
                0f,
                0.05f,
                0.18f);
            hammerHeadCut.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);

            BuildTutorialVideoFrame(generatedPanel.transform);

            GameObject start = CreateButton(
                generatedPanel.transform,
                "Start",
                "クラフト開始",
                new Vector3(0f, -2.62f, -0.7f),
                CraftLiveForgeUITheme.Ember,
                new Vector3(3.85f, 0.92f, 0.25f));
            start.GetComponent<CraftLiveWorldButton>()
                .AddListener(StartGroup);
            tutorialStartButton = start.GetComponent<CraftLiveWorldButton>();
        }

        private void BuildTutorialVideoFrame(Transform parent)
        {
            CreateDecorativePart(
                parent,
                "TutorialVideoFrameShadow",
                new Vector3(0.07f, 0.46f, -0.72f),
                new Vector3(5.72f, 3.42f, 0.13f),
                new Color(0.04f, 0.025f, 0.02f),
                0f,
                0.06f,
                0.15f);
            CreateDecorativePart(
                parent,
                "TutorialVideoFrame",
                new Vector3(0f, 0.52f, -0.77f),
                new Vector3(5.62f, 3.32f, 0.12f),
                CraftLiveForgeUITheme.Brass,
                0.22f,
                0.72f,
                0.55f);
            GameObject surface = CreateDecorativePart(
                parent,
                "TutorialVideoSurface",
                new Vector3(0f, 0.52f, -0.86f),
                new Vector3(5.32f, 2.99f, 0.045f),
                new Color(0.012f, 0.016f, 0.02f),
                0f,
                0.05f,
                0.08f);
            surface.AddComponent<BoxCollider>();
            tutorialTapSurface =
                surface.AddComponent<CraftLiveTutorialVideoTapSurface>();
            tutorialTapSurface.Configure(this, false);

            tutorialPlaceholder = new GameObject("TutorialVideoPlaceholder");
            tutorialPlaceholder.transform.SetParent(parent, false);
            tutorialPlaceholder.transform.localPosition =
                new Vector3(0f, 0.52f, -0.91f);
            tutorialPlaceholder.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            tutorialPlaceholderLabel = CreateText(
                tutorialPlaceholder.transform,
                "TutorialVideoLabel",
                startTutorialVideo != null
                    ? "操作説明動画"
                    : "操作説明動画\n（動画を割り当ててください）",
                Vector3.zero,
                0.038f,
                new Color(0.82f, 0.77f, 0.67f));

            if (startTutorialVideo == null)
            {
                return;
            }

            tutorialRenderTexture = new RenderTexture(
                640,
                360,
                0,
                RenderTextureFormat.ARGB32)
            {
                name = "Generated_StartTutorialTexture"
            };
            tutorialRenderTexture.Create();
            Renderer surfaceRenderer = surface.GetComponent<Renderer>();
            if (surfaceRenderer != null)
            {
                Material material = surfaceRenderer.material;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", tutorialRenderTexture);
                }
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", tutorialRenderTexture);
                }
            }

            tutorialVideoPlayer = generatedPanel.AddComponent<VideoPlayer>();
            tutorialVideoPlayer.playOnAwake = false;
            tutorialVideoPlayer.isLooping = false;
            tutorialVideoPlayer.waitForFirstFrame = true;
            tutorialVideoPlayer.skipOnDrop = true;
            tutorialVideoPlayer.source = VideoSource.VideoClip;
            tutorialVideoPlayer.clip = startTutorialVideo;
            tutorialVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            tutorialVideoPlayer.targetTexture = tutorialRenderTexture;
            tutorialVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        }

        private void BuildResultPanel(CraftLiveRoomState state)
        {
            generatedPanel = CreatePanel("Generated_ResultPanel");
            CraftLiveResultState result = state.result;
            CreateText(
                generatedPanel.transform,
                "CompletionKicker",
                "—  MASTER FORGE  —",
                new Vector3(0f, 3.05f, -0.72f),
                0.027f,
                CraftLiveForgeUITheme.Brass);
            CreateText(
                generatedPanel.transform,
                "CompletionTitle",
                "鍛造完了",
                new Vector3(0f, 2.53f, -0.72f),
                0.069f);
            TextMesh completedWeaponName = CreateText(
                generatedPanel.transform,
                "WeaponName",
                EmptyFallback(result.weaponName),
                new Vector3(-0.25f, 1.65f, -0.72f),
                0.058f);
            CraftLiveForgeUITheme.ApplyWeaponFont(completedWeaponName);

            if (CraftLiveCalculator.IsSecretWeaponId(result.weaponId))
            {
                BuildSecretResultEffect(
                    generatedPanel.transform,
                    result.weaponId);
            }

            CreateRankBadge(
                generatedPanel.transform,
                result.rank,
                new Vector3(2.72f, 1.67f, -0.52f));
            CreateStatPlate(
                generatedPanel.transform,
                "AttackStat",
                "攻撃",
                result.stats.attackRate,
                new Vector3(-2.08f, 0.35f, -0.5f),
                CraftLiveForgeUITheme.Ember);
            CreateStatPlate(
                generatedPanel.transform,
                "DefenseStat",
                "防御",
                result.stats.defenseRate,
                new Vector3(0f, 0.35f, -0.5f),
                new Color(0.28f, 0.48f, 0.62f));
            CreateStatPlate(
                generatedPanel.transform,
                "EvasionStat",
                "回避",
                result.stats.evasionRate,
                new Vector3(2.08f, 0.35f, -0.5f),
                new Color(0.38f, 0.57f, 0.34f));

            GameObject traits = CreateInsetPlate(
                generatedPanel.transform,
                "ForgedTraits",
                new Vector3(0f, -1.05f, -0.47f),
                new Vector3(6.15f, 1.05f, 0.15f),
                CraftLiveForgeUITheme.Iron,
                CraftLiveForgeUITheme.Brass);
            CreateText(
                traits.transform,
                "TraitText",
                $"属性  {EmptyFallback(result.attributeName)}     ◆     " +
                $"技能  {EmptyFallback(result.skillName)}",
                new Vector3(0f, 0f, -0.14f),
                resultTraitTextSize);
            GameObject next = CreateButton(
                generatedPanel.transform,
                "NextWeapon",
                "次の武器を作る",
                new Vector3(0f, -2.5f, -0.68f),
                CraftLiveForgeUITheme.Ember,
                new Vector3(3.55f, 0.78f, 0.24f));
            next.GetComponent<CraftLiveWorldButton>()
                .AddListener(BeginNextWeapon);
        }

        private void BuildFinalSelection(CraftLiveRoomState state)
        {
            generatedPanel = CreatePanel(
                "Generated_FinalSelection");
            CreateText(
                generatedPanel.transform,
                "Title",
                "完成武器を1つ選ぶ",
                new Vector3(0f, 3.25f, -0.7f),
                0.06f);
            int count = state.completedWeapons != null
                ? state.completedWeapons.Count
                : 0;
            if (count == 0)
            {
                CreateText(
                    generatedPanel.transform,
                    "Empty",
                    "完成した武器がありません",
                    Vector3.zero,
                    0.055f);
                return;
            }

            int visibleCount = Mathf.Min(12, count);
            int start = count - visibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                CraftLiveResultState result =
                    state.completedWeapons[start + i];
                int serial = result.resultSerial;
                int column = i % 2;
                int row = i / 2;
                GameObject button = CreateButton(
                    generatedPanel.transform,
                    $"Result_{serial}",
                    $"{result.weaponName}\n" +
                    $"{result.stats.attackRate:0}/" +
                    $"{result.stats.defenseRate:0}/" +
                    $"{result.stats.evasionRate:0}",
                    new Vector3(
                        column == 0 ? -1.75f : 1.75f,
                        2.35f - row * 0.9f,
                        -0.7f),
                    new Color(0.14f, 0.48f, 0.62f),
                    new Vector3(3.1f, 0.72f, 0.24f));
                button.GetComponent<CraftLiveWorldButton>()
                    .AddListener(
                        () => SelectFinalWeapon(serial));
            }
        }

        private void BuildCodePanel(CraftLiveRoomState state)
        {
            generatedPanel = new GameObject("Generated_NextRoomPanel");
            generatedPanel.transform.SetParent(
                bindings.ResultHologramRoot,
                false);
            generatedPanel.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            GameObject backdrop = CreateDecorativePart(
                generatedPanel.transform,
                "FullScreenBlackBackground",
                new Vector3(0f, 0f, 0.2f),
                new Vector3(12f, 16f, 0.35f),
                Color.black,
                0f,
                0f,
                0f);
            Material blackMaterial =
                CraftLiveForgeUITheme.CreateCompatibleUnlitMaterial(
                    "Generated_NextRoomBlack");
            if (blackMaterial != null)
            {
                blackMaterial.SetColor("_BaseColor", Color.black);
                blackMaterial.SetColor("_Color", Color.black);
                backdrop.GetComponent<Renderer>().sharedMaterial =
                    blackMaterial;
            }
            CreateText(
                generatedPanel.transform,
                "Title",
                string.IsNullOrWhiteSpace(state.finalWeaponCode)
                    ? "グループ番号を\n発行しています"
                    : "グループ番号",
                new Vector3(0f, 1.4f, -0.7f),
                0.06f,
                Color.white);
            TextMesh finalWeaponName = CreateText(
                generatedPanel.transform,
                "Weapon",
                state.result.weaponName,
                new Vector3(0f, 0.15f, -0.7f),
                0.052f,
                Color.white);
            CraftLiveForgeUITheme.ApplyWeaponFont(finalWeaponName);
            CreateText(
                generatedPanel.transform,
                "Code",
                string.IsNullOrWhiteSpace(state.finalWeaponCode)
                    ? "通信中…"
                    : state.finalWeaponCode,
                new Vector3(0f, -0.7f, -0.7f),
                0.065f,
                Color.white);
            if (string.IsNullOrWhiteSpace(state.finalWeaponCode) &&
                !string.IsNullOrWhiteSpace(state.message))
            {
                CreateText(
                    generatedPanel.transform,
                    "IssueStatus",
                    BuildGroupIssueStatus(state.message),
                    new Vector3(0f, -1.52f, -0.7f),
                    0.031f,
                    Color.white);
            }

            GameObject restart = CreateButton(
                generatedPanel.transform,
                "StaffRestart",
                "スタッフ専用\n次のグループを開始",
                new Vector3(0f, -2.45f, -0.68f),
                new Color(0.12f, 0.12f, 0.12f),
                new Vector3(4.45f, 1.02f, 0.24f));
            restart.GetComponent<CraftLiveWorldButton>()
                .AddListener(RestartForNextGroup);

            double elapsedSeconds = System.Math.Max(
                0d,
                (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() -
                 state.updatedAtUnixMs) / 1000d);
            float remaining = Mathf.Max(
                0f,
                staffRestartDelaySeconds - (float)elapsedSeconds);
            restart.SetActive(remaining <= 0f);
            if (remaining > 0f)
            {
                staffRestartRoutine = StartCoroutine(
                    RevealStaffRestart(restart, remaining));
            }
        }

        public static string BuildGroupIssueStatus(string message)
        {
            string value = (message ?? string.Empty).Trim();
            if (value.Contains("Database Rules"))
            {
                return "Firebase権限エラー\nDatabase Rulesを確認";
            }

            if (value.Contains("再接続") || value.Contains("再試行"))
            {
                return "Firebaseへ再接続中";
            }

            return value.Length <= 22
                ? value
                : value.Substring(0, 22) + "…";
        }

        private static void BuildSecretResultEffect(
            Transform parent,
            string weaponId)
        {
            Color secretRed = new Color(1f, 0.035f, 0.015f);
            Color accent = Color.Lerp(
                secretRed,
                Color.white,
                weaponId == CraftLiveCalculator.SecretBareHandsWeaponId
                    ? 0.08f
                    : 0.18f);

            BuildSecretRedBurst(parent, secretRed);
            CreateText(
                parent,
                "SecretLabel",
                "SECRET RECIPE",
                new Vector3(0f, 2.05f, -0.74f),
                0.027f,
                accent);
            CreateText(
                parent,
                "SecretSuccessLabel",
                "隠し武器合成成功！！",
                new Vector3(-0.25f, 1.13f, -0.74f),
                0.034f,
                accent);

            Vector3[] sparklePositions =
            {
                new Vector3(-2.75f, 2.62f, -0.73f),
                new Vector3(-1.95f, 2.30f, -0.73f),
                new Vector3(-0.95f, 2.78f, -0.73f),
                new Vector3(0.72f, 2.72f, -0.73f),
                new Vector3(1.72f, 2.35f, -0.73f),
                new Vector3(2.68f, 2.68f, -0.73f),
                new Vector3(-2.92f, 1.45f, -0.73f),
                new Vector3(2.9f, 1.36f, -0.73f),
                new Vector3(-2.86f, 0.62f, -0.73f),
                new Vector3(2.82f, 0.55f, -0.73f),
                new Vector3(-2.72f, -0.48f, -0.73f),
                new Vector3(2.7f, -0.38f, -0.73f),
                new Vector3(-2.55f, -1.65f, -0.73f),
                new Vector3(2.48f, -1.58f, -0.73f)
            };
            for (int index = 0; index < sparklePositions.Length; index++)
            {
                float size = index % 3 == 0 ? 0.14f : 0.09f;
                GameObject sparkle = CreateDecorativePart(
                    parent,
                    $"SecretSparkle_{index}",
                    sparklePositions[index],
                    new Vector3(size, size, 0.035f),
                    Color.Lerp(accent, Color.white, 0.55f),
                    1.8f,
                    0.12f,
                    0.82f);
                sparkle.transform.localRotation =
                    Quaternion.Euler(0f, 0f, 45f);
                sparkle.AddComponent<CraftLiveSecretResultEffect>();
            }

            if (ShouldBuildBareHandsSmoke(weaponId))
            {
                BuildBareHandsSmoke(parent);
            }
        }

        public static bool ShouldBuildBareHandsSmoke(string weaponId)
        {
            return weaponId == CraftLiveCalculator.SecretBareHandsWeaponId;
        }

        private static void BuildSecretRedBurst(
            Transform parent,
            Color red)
        {
            const int rayCount = 16;
            for (int index = 0; index < rayCount; index++)
            {
                float angle = index * (360f / rayCount);
                float radians = angle * Mathf.Deg2Rad;
                Vector3 direction = new Vector3(
                    Mathf.Cos(radians),
                    Mathf.Sin(radians),
                    0f);
                float length = index % 2 == 0 ? 1.15f : 0.72f;
                GameObject ray = CreateDecorativePart(
                    parent,
                    $"SecretRedRay_{index}",
                    direction * 2.45f + new Vector3(0f, 0.65f, -0.67f),
                    new Vector3(length, 0.045f, 0.025f),
                    Color.Lerp(red, Color.white, index % 3 == 0 ? 0.32f : 0.08f),
                    2.4f,
                    0.18f,
                    0.9f);
                ray.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                ray.AddComponent<CraftLiveSecretResultEffect>();
            }
        }

        private static void BuildBareHandsSmoke(Transform parent)
        {
            Vector3[] positions =
            {
                new Vector3(-0.9f, 0.95f, -0.78f),
                new Vector3(-0.55f, 1.05f, -0.79f),
                new Vector3(-0.18f, 0.9f, -0.8f),
                new Vector3(0.2f, 1.02f, -0.8f),
                new Vector3(0.58f, 0.92f, -0.79f),
                new Vector3(0.92f, 1.08f, -0.78f),
                new Vector3(-0.42f, 1.3f, -0.81f),
                new Vector3(0f, 1.24f, -0.82f),
                new Vector3(0.44f, 1.34f, -0.81f)
            };
            for (int index = 0; index < positions.Length; index++)
            {
                GameObject puff = GameObject.CreatePrimitive(
                    PrimitiveType.Sphere);
                puff.name = $"BareHandsSmoke_{index}";
                puff.transform.SetParent(parent, false);
                puff.transform.localPosition = positions[index];
                float size = index % 3 == 0 ? 0.42f : 0.3f;
                puff.transform.localScale = new Vector3(size, size, 0.08f);
                DestroySafely(puff.GetComponent<Collider>());
                Color smoke = index % 2 == 0
                    ? new Color(0.34f, 0.35f, 0.36f)
                    : new Color(0.5f, 0.49f, 0.47f);
                CraftLiveForgeUITheme.ApplyForgeSurface(
                    puff.GetComponent<Renderer>(),
                    smoke,
                    0f,
                    0f,
                    0.05f);
                CraftLiveSecretSmokeEffect effect =
                    puff.AddComponent<CraftLiveSecretSmokeEffect>();
                effect.Configure(index);
            }
        }

        private IEnumerator RevealStaffRestart(
            GameObject restart,
            float delaySeconds)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            if (restart != null &&
                displayedPhase == CraftLiveSessionPhase.Finished)
            {
                restart.SetActive(true);
            }

            staffRestartRoutine = null;
        }

        private GameObject CreatePanel(string name)
        {
            GameObject panel = new GameObject(name);
            panel.name = name;
            panel.transform.SetParent(
                bindings.ResultHologramRoot,
                false);
            panel.transform.localScale = Vector3.one * 0.72f;
            panel.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();

            CreateDecorativePart(
                panel.transform,
                "CastIronShadow",
                new Vector3(0.08f, -0.1f, 0.08f),
                new Vector3(7.48f, 7.86f, 0.22f),
                CraftLiveForgeUITheme.DeepIron,
                0.01f,
                0.84f,
                0.2f);
            CreateDecorativePart(
                panel.transform,
                "WalnutBacking",
                Vector3.zero,
                new Vector3(7.24f, 7.62f, 0.2f),
                new Color(0.25f, 0.115f, 0.045f),
                0.015f,
                0.18f,
                0.24f);
            CreateDecorativePart(
                panel.transform,
                "ForgedIronFace",
                new Vector3(0f, 0f, -0.13f),
                new Vector3(6.82f, 7.18f, 0.11f),
                CraftLiveForgeUITheme.DeepIron,
                0.025f,
                0.8f,
                0.28f);

            Color warmBrass = Color.Lerp(
                CraftLiveForgeUITheme.Brass,
                CraftLiveForgeUITheme.Iron,
                0.16f);
            CreateDecorativePart(
                panel.transform,
                "TopBrassRail",
                new Vector3(0f, 3.54f, -0.35f),
                new Vector3(6.72f, 0.12f, 0.13f),
                warmBrass,
                0.08f,
                0.88f,
                0.44f);
            CreateDecorativePart(
                panel.transform,
                "BottomBrassRail",
                new Vector3(0f, -3.54f, -0.35f),
                new Vector3(6.72f, 0.12f, 0.13f),
                warmBrass,
                0.05f,
                0.88f,
                0.38f);
            CreateDecorativePart(
                panel.transform,
                "LeftIronRail",
                new Vector3(-3.34f, 0f, -0.33f),
                new Vector3(0.14f, 7.08f, 0.13f),
                CraftLiveForgeUITheme.Iron,
                0.025f,
                0.9f,
                0.3f);
            CreateDecorativePart(
                panel.transform,
                "RightIronRail",
                new Vector3(3.34f, 0f, -0.33f),
                new Vector3(0.14f, 7.08f, 0.13f),
                CraftLiveForgeUITheme.Iron,
                0.025f,
                0.9f,
                0.3f);
            CreateDecorativePart(
                panel.transform,
                "HeaderDivider",
                new Vector3(0f, 2.14f, -0.4f),
                new Vector3(5.9f, 0.045f, 0.08f),
                CraftLiveForgeUITheme.Brass,
                0.08f,
                0.85f,
                0.45f);

            CreatePanelRivet(panel.transform, -3.1f, 3.3f);
            CreatePanelRivet(panel.transform, 3.1f, 3.3f);
            CreatePanelRivet(panel.transform, -3.1f, -3.3f);
            CreatePanelRivet(panel.transform, 3.1f, -3.3f);
            return panel;
        }

        private static GameObject CreateButton(
            Transform parent,
            string name,
            string label,
            Vector3 position,
            Color color,
            Vector3? scale = null)
        {
            GameObject button = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent, false);
            button.transform.localPosition = position;
            button.transform.localScale =
                scale ?? new Vector3(3f, 0.75f, 0.24f);
            Renderer renderer = button.GetComponent<Renderer>();
            CraftLiveForgeUITheme.GetButtonPalette(
                color,
                out Color normal,
                out Color hover,
                out Color pressed,
                out _,
                out _);
            ApplyColor(renderer, normal);
            CraftLiveWorldButton worldButton =
                button.AddComponent<CraftLiveWorldButton>();
            worldButton.Configure(
                button.transform,
                new[] { renderer },
                normal,
                hover,
                pressed);
            CraftLiveForgeUITheme.BuildButtonFrame(
                button.transform,
                color);
            CreateText(
                button.transform,
                "Label",
                label,
                new Vector3(0f, 0f, -0.62f),
                0.042f);
            return button;
        }

        private static TextMesh CreateText(
            Transform parent,
            string name,
            string value,
            Vector3 position,
            float size,
            Color? color = null)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value ?? string.Empty;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                text,
                size,
                color ?? CraftLiveForgeUITheme.ParchmentText);
            return text;
        }

        private void CreateStatPlate(
            Transform parent,
            string name,
            string label,
            float value,
            Vector3 position,
            Color accent)
        {
            GameObject plate = CreateInsetPlate(
                parent,
                name,
                position,
                new Vector3(1.78f, 1.04f, 0.15f),
                CraftLiveForgeUITheme.Iron,
                accent);
            CreateText(
                plate.transform,
                "Label",
                label,
                new Vector3(0f, 0.2f, -0.14f),
                resultStatLabelSize,
                CraftLiveForgeUITheme.MutedText);
            CreateText(
                plate.transform,
                "Value",
                value.ToString("0.#"),
                new Vector3(0f, -0.18f, -0.145f),
                0.058f,
                accent);
        }

        private static void CreateRankBadge(
            Transform parent,
            string rank,
            Vector3 position)
        {
            GameObject badge = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            badge.name = "RankBadge";
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = position;
            badge.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            badge.transform.localScale =
                new Vector3(0.52f, 0.09f, 0.52f);
            DestroySafely(badge.GetComponent<Collider>());
            CraftLiveForgeUITheme.ApplyForgeSurface(
                badge.GetComponent<Renderer>(),
                CraftLiveForgeUITheme.Brass,
                0.12f,
                0.92f,
                0.5f);
            CreateText(
                parent,
                "Rank",
                $"RANK\n{EmptyFallback(rank)}",
                new Vector3(
                    position.x,
                    position.y,
                    position.z - 0.17f),
                0.037f,
                CraftLiveForgeUITheme.DeepIron);
        }

        private static GameObject CreateInsetPlate(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color surface,
            Color accent)
        {
            GameObject plate = new GameObject(name);
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = position;
            CreateDecorativePart(
                plate.transform,
                "InsetSurface",
                Vector3.zero,
                scale,
                surface,
                0.025f,
                0.82f,
                0.3f);
            CreateDecorativePart(
                plate.transform,
                "AccentEdge",
                new Vector3(0f, scale.y * 0.43f, -scale.z * 0.58f),
                new Vector3(scale.x * 0.86f, scale.y * 0.055f, 0.06f),
                accent,
                0.08f,
                0.86f,
                0.44f);
            return plate;
        }

        private static GameObject CreateDecorativePart(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color,
            float emission,
            float metallic,
            float smoothness)
        {
            GameObject part = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            DestroySafely(part.GetComponent<Collider>());
            CraftLiveForgeUITheme.ApplyForgeSurface(
                part.GetComponent<Renderer>(),
                color,
                emission,
                metallic,
                smoothness);
            return part;
        }

        private static void CreatePanelRivet(
            Transform parent,
            float x,
            float y)
        {
            GameObject rivet = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            rivet.name = "HandHammeredRivet";
            rivet.transform.SetParent(parent, false);
            rivet.transform.localPosition =
                new Vector3(x, y, -0.47f);
            rivet.transform.localScale =
                new Vector3(0.16f, 0.16f, 0.07f);
            DestroySafely(rivet.GetComponent<Collider>());
            CraftLiveForgeUITheme.ApplyForgeSurface(
                rivet.GetComponent<Renderer>(),
                CraftLiveForgeUITheme.Brass,
                0.06f,
                0.92f,
                0.46f);
        }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "なし"
                : value;
        }

        private static void ApplyColor(
            Renderer renderer,
            Color color)
        {
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

    public sealed class CraftLiveSecretResultEffect : MonoBehaviour
    {
        private Vector3 baseScale;
        private float phase;

        private void Awake()
        {
            baseScale = transform.localScale;
            phase = Mathf.Abs(gameObject.name.GetHashCode() % 17) *
                    0.37f;
        }

        private void Update()
        {
            float pulse = 0.58f +
                          (Mathf.Sin(
                               Time.unscaledTime * 5.2f + phase) + 1f) *
                          0.31f;
            transform.localScale = baseScale * pulse;
        }
    }

    public sealed class CraftLiveSecretSmokeEffect : MonoBehaviour
    {
        private Vector3 basePosition;
        private Vector3 baseScale;
        private float startedAt;
        private float delay;
        private float driftDirection;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            startedAt = Time.unscaledTime;
            transform.localScale = Vector3.zero;
        }

        public void Configure(int index)
        {
            delay = index * 0.055f;
            driftDirection = index % 2 == 0 ? -1f : 1f;
        }

        private void Update()
        {
            const float lifetime = 1.65f;
            float elapsed = Time.unscaledTime - startedAt - delay;
            if (elapsed < 0f)
            {
                return;
            }

            float normalized = Mathf.Clamp01(elapsed / lifetime);
            float appear = Mathf.Clamp01(normalized / 0.16f);
            float disappear = Mathf.Clamp01((1f - normalized) / 0.24f);
            float scale = Mathf.Lerp(0.35f, 1.9f, normalized) *
                          Mathf.Min(appear, disappear);
            transform.localScale = baseScale * scale;
            transform.localPosition = basePosition + new Vector3(
                driftDirection * 0.22f * normalized,
                0.82f * normalized,
                0f);
            transform.localRotation = Quaternion.Euler(
                0f,
                0f,
                driftDirection * normalized * 24f);

            if (normalized >= 1f)
            {
                Destroy(gameObject);
            }
        }
    }
}
