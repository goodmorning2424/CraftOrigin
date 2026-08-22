using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveHammerSynthesisController :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad2Bindings bindings;
        [SerializeField] private CraftLiveHammerStrikePresentation presentation;
        [SerializeField] private bool createFallbackVisuals = true;
        [SerializeField, Min(1f)] private float railHalfWidth = 2.6f;
        [SerializeField, Min(20f)] private float strokePixelsOverride;
        [Header("Strike Input")]
        [SerializeField] private Vector2 strikeInputDirection =
            new Vector2(-0.91f, -0.4f);
        [SerializeField, Range(0f, 1f)]
        private float wrongDirectionPenalty = 0.72f;
        [SerializeField] private UnityEvent<bool> onHammerVisible;
        [SerializeField] private UnityEvent<int> onPassCountChanged;
        [SerializeField] private UnityEvent<int> onPassesRemainingChanged;
        [SerializeField] private UnityEvent<float> onRailProgress;
        [SerializeField] private UnityEvent onHammerStrike;
        [SerializeField] private UnityEvent<string> onStartRejected;
        [Header("Completion Flash")]
        [SerializeField, Min(0.1f)] private float completionRevealDelay = 2f;
        [SerializeField, Range(0.05f, 1f)] private float completionFadeDuration = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float completionFadeInDuration = 0.4f;
        [SerializeField, Range(0.1f, 0.9f)] private float completionFlashOpacity = 0.7f;

        private GameObject generatedRoot;
        private GameObject synthesisButton;
        private bool dragging;
        private bool strikeInProgress;
        private int activePointerId = int.MinValue;
        private Vector2 previousPointerPosition;
        private float directedTravel;
        private bool completionFlashVisible;
        private float completionFlashAlpha;

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
            BuildFallback();
            Refresh(session != null ? session.State : null);
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            dragging = false;
            strikeInProgress = false;
            activePointerId = int.MinValue;
            completionFlashVisible = false;
            completionFlashAlpha = 0f;
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }
        }

        private void OnGUI()
        {
            if (!completionFlashVisible || completionFlashAlpha <= 0f)
            {
                return;
            }

            Color previous = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.color = new Color(1f, 1f, 1f, completionFlashAlpha);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = previous;
            GUI.depth = previousDepth;
        }

        public void Configure(CraftLivePad2Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
        }

        public void StartSynthesis()
        {
            if (session == null)
            {
                return;
            }

            string error = CraftLiveCalculator.ValidateSynthesis(
                session.State,
                session.Catalog,
                session.Rules);
            if (!string.IsNullOrEmpty(error))
            {
                onStartRejected?.Invoke(error);
                return;
            }

            session.StartSynthesis();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsMixing())
            {
                return;
            }

            if (dragging && activePointerId != eventData.pointerId)
            {
                return;
            }

            dragging = true;
            activePointerId = eventData.pointerId;
            previousPointerPosition = eventData.position;
            directedTravel = 0f;
            MoveHammer(0f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || activePointerId != eventData.pointerId ||
                !IsMixing())
            {
                return;
            }

            Vector2 pointerDelta =
                eventData.position - previousPointerPosition;
            previousPointerPosition = eventData.position;
            if (strikeInProgress)
            {
                return;
            }

            float projected =
                CraftLiveHammerStrikePresentation.ProjectInputDelta(
                    pointerDelta,
                    strikeInputDirection);
            if (projected > 0f)
            {
                directedTravel += projected;
            }
            else
            {
                directedTravel = Mathf.Max(
                    0f,
                    directedTravel + projected * wrongDirectionPenalty);
            }

            float required = GetStrokePixels();
            float normalized = Mathf.Clamp01(
                directedTravel / required);
            MoveHammer(normalized);
            onRailProgress?.Invoke(normalized);
            if (directedTravel < required)
            {
                return;
            }

            directedTravel = 0f;
            strikeInProgress = true;
            StartCoroutine(CommitStrike());
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (activePointerId != eventData.pointerId)
            {
                return;
            }

            dragging = false;
            activePointerId = int.MinValue;
            directedTravel = 0f;
            if (!strikeInProgress)
            {
                MoveHammer(0f);
            }
            onRailProgress?.Invoke(0f);
        }

        public static int PassesRemaining(
            int completed,
            int required)
        {
            return Mathf.Max(
                0,
                Mathf.Max(1, required) -
                Mathf.Max(0, completed));
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Prepare Materials and Start Synthesis")]
        private void DebugPrepareAndStart()
        {
            ResolveReferences();
            if (!Application.isPlaying ||
                session == null ||
                session.Catalog == null)
            {
                Debug.LogWarning(
                    "Craft-live: Play Modeで実行してください。",
                    this);
                return;
            }

            CraftLiveRoomState next = session.State.Clone();
            next.sessionPhase = CraftLiveSessionPhase.Playing;
            next.sessionEndsAtUnixMs =
                CraftLiveSession.UnixNowMs() +
                300000L;
            next.placement.Clear();
            next.transferQueue.Clear();
            next.transferBatchRemaining = 0;
            next.slots.Clear();
            next.craft = new CraftLiveCraftState();
            next.result = new CraftLiveResultState();
            CraftLiveWeaponDefinition weapon =
                session.Catalog.FirstWeapon();
            next.selectedWeaponId = weapon != null
                ? weapon.WeaponId
                : string.Empty;
            next.weaponSelectionConfirmed = weapon != null;

            CraftLiveSlotId[] baseSlots =
            {
                CraftLiveSlotId.Top,
                CraftLiveSlotId.Left,
                CraftLiveSlotId.Right,
                CraftLiveSlotId.Bottom
            };
            int baseIndex = 0;
            foreach (CraftLiveMaterialDefinition material in
                     session.Catalog.Materials)
            {
                if (material == null)
                {
                    continue;
                }

                next.RegisterMaterial(material.MaterialId);
                if (material.Category ==
                        CraftLiveMaterialCategory.Attribute &&
                    string.IsNullOrWhiteSpace(
                        next.slots.attribute))
                {
                    next.slots.attribute = material.MaterialId;
                }
                else if (material.Category ==
                             CraftLiveMaterialCategory.Skill &&
                         string.IsNullOrWhiteSpace(
                             next.slots.skill))
                {
                    next.slots.skill = material.MaterialId;
                }
                else if (material.Category ==
                             CraftLiveMaterialCategory.Upgrade &&
                         baseIndex < baseSlots.Length)
                {
                    next.slots.Set(
                        baseSlots[baseIndex++],
                        material.MaterialId);
                }
            }

            session.ApplyRemoteState(next);
            StartSynthesis();
        }
#endif

        private bool IsMixing()
        {
            return session != null &&
                   session.State != null &&
                   session.State.craft.status ==
                       CraftLiveCraftStatus.Mixing;
        }

        private float GetStrokePixels()
        {
            if (strokePixelsOverride > 0f)
            {
                return strokePixelsOverride;
            }

            return session != null &&
                   session.Rules != null
                ? session.Rules.HammerStrokePixels
                : 120f;
        }

        private int GetRequiredPasses()
        {
            return session != null &&
                   session.Rules != null
                ? session.Rules.RequiredHammerPasses
                : 6;
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

            if (presentation == null)
            {
                presentation = GetComponent<
                    CraftLiveHammerStrikePresentation>();
            }

            if (presentation == null && Application.isPlaying)
            {
                presentation = gameObject.AddComponent<
                    CraftLiveHammerStrikePresentation>();
            }

            if (presentation != null)
            {
                presentation.Configure(bindings);
            }
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            bool mixing =
                state.craft.status ==
                CraftLiveCraftStatus.Mixing;
            if (generatedRoot != null)
            {
                generatedRoot.SetActive(mixing);
            }

            if (synthesisButton != null)
            {
                bool canStart =
                    state.sessionPhase ==
                        CraftLiveSessionPhase.Playing &&
                    state.craft.status ==
                        CraftLiveCraftStatus.Editing &&
                    state.placement.status ==
                        CraftLivePlacementStatus.Idle &&
                    string.IsNullOrEmpty(
                        CraftLiveCalculator.ValidateSynthesis(
                            state,
                            session != null ? session.Catalog : null,
                            session != null ? session.Rules : null));
                synthesisButton.SetActive(
                    canStart);
            }

            int count = state.craft.hammerPassCount;
            int required = GetRequiredPasses();
            int remaining = PassesRemaining(
                count,
                required);
            presentation?.SetMixing(
                mixing,
                count / (float)Mathf.Max(1, required),
                count,
                required);

            onHammerVisible?.Invoke(mixing);
            onPassCountChanged?.Invoke(count);
            onPassesRemainingChanged?.Invoke(remaining);
        }

        private void BuildFallback()
        {
            if (!createFallbackVisuals ||
                bindings == null ||
                bindings.HammerRoot == null)
            {
                return;
            }

            generatedRoot = new GameObject(
                "Generated_HammerInput");
            generatedRoot.transform.SetParent(
                bindings.HammerRoot,
                false);
            generatedRoot.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();

            GameObject inputSurface = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            inputSurface.name = "StrikeInputSurface";
            inputSurface.transform.SetParent(
                generatedRoot.transform,
                false);
            inputSurface.transform.localPosition =
                new Vector3(0f, 0.35f, 0.28f);
            inputSurface.transform.localScale =
                new Vector3(
                    railHalfWidth * 2f,
                    3.8f,
                    0.04f);
            Renderer inputRenderer = inputSurface.GetComponent<Renderer>();
            if (inputRenderer != null)
            {
                inputRenderer.enabled = false;
            }

            synthesisButton =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
            synthesisButton.name =
                "Generated_StartSynthesis";
            synthesisButton.transform.SetParent(
                bindings.UiRoot,
                false);
            synthesisButton.transform.localPosition =
                new Vector3(0f, -2.62f, -0.7f);
            synthesisButton.transform.localScale =
                new Vector3(1.9f, 0.5f, 0.24f);
            Renderer buttonRenderer =
                synthesisButton.GetComponent<Renderer>();
            ApplyColor(
                buttonRenderer,
                CraftLiveForgeUITheme.Ember);
            CraftLiveWorldButton button =
                synthesisButton.AddComponent<
                    CraftLiveWorldButton>();
            button.Configure(
                synthesisButton.transform,
                new[] { buttonRenderer },
                CraftLiveForgeUITheme.Ember,
                Color.Lerp(
                    CraftLiveForgeUITheme.Ember,
                    CraftLiveForgeUITheme.Brass,
                    0.42f),
                CraftLiveForgeUITheme.Brass);
            CraftLiveForgeUITheme.BuildButtonFrame(
                synthesisButton.transform,
                CraftLiveForgeUITheme.Brass);
            button.AddListener(StartSynthesis);
            CreateText(
                synthesisButton.transform,
                "Label",
                "鍛造開始",
                new Vector3(0f, 0f, -0.62f),
                0.06f);
        }

        private IEnumerator CommitStrike()
        {
            bool completed = false;
            if (presentation != null)
            {
                yield return presentation.PlayStrikeSequence(() =>
                {
                    completed = session != null &&
                                session.RegisterHammerPass(1f, false);
                    onHammerStrike?.Invoke();
                });
            }
            else
            {
                completed = session != null &&
                            session.RegisterHammerPass(1f, false);
                onHammerStrike?.Invoke();
            }

            MoveHammer(0f);
            if (completed)
            {
                dragging = false;
                activePointerId = int.MinValue;
                yield return PlayCompletionFlash();
                session?.RevealCompletionPresentation();
            }

            strikeInProgress = false;
        }

        private IEnumerator PlayCompletionFlash()
        {
            float duration = Mathf.Max(0.1f, completionRevealDelay);
            float fade = Mathf.Min(
                duration,
                Mathf.Max(0.05f, completionFadeDuration));
            float fadeIn = Mathf.Min(
                duration - fade,
                Mathf.Max(0.05f, completionFadeInDuration));
            float fadeStart = duration - fade;
            completionFlashVisible = true;
            completionFlashAlpha = 0f;
            bool completionSwitched = false;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (elapsed < fadeIn)
                {
                    completionFlashAlpha = completionFlashOpacity *
                        Mathf.Clamp01(elapsed / fadeIn);
                }
                else if (elapsed <= fadeStart)
                {
                    completionFlashAlpha = completionFlashOpacity;
                }
                else
                {
                    completionFlashAlpha = completionFlashOpacity *
                        (1f - Mathf.Clamp01(
                            (elapsed - fadeStart) / fade));
                }

                if (!completionSwitched && elapsed >= fadeIn)
                {
                    completionSwitched = true;
                    session?.CompleteSynthesis(true);
                }
                yield return null;
            }

            if (!completionSwitched)
            {
                session?.CompleteSynthesis(true);
            }

            completionFlashAlpha = 0f;
            completionFlashVisible = false;
            // Let the white overlay disappear before publishing completion.
            yield return null;
        }

        private void MoveHammer(float normalized)
        {
            presentation?.PreviewStrike(Mathf.Clamp01(normalized));
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
            TextMesh text =
                textObject.AddComponent<TextMesh>();
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
}
