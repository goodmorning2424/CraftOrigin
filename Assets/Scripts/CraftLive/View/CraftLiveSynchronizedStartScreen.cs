using System.Collections;
using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Pad 1/3/4 companion for the Pad 2 start screen. Every pad observes the
    /// same room phase, so pressing the single Pad 2 start button dismisses
    /// all start screens through the normal synchronized session update.
    /// </summary>
    public sealed class CraftLiveSynchronizedStartScreen : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Transform displayRoot;
        [SerializeField, Min(0.1f)] private float slideDuration = 0.85f;
        [SerializeField, Min(1f)] private float slideDistance = 12f;

        private GameObject generatedScreen;
        private Coroutine slideRoutine;
        private CraftLiveSessionPhase displayedPhase =
            (CraftLiveSessionPhase)(-1);

        public void Configure(Transform targetDisplayRoot)
        {
            displayRoot = targetDisplayRoot;
            ResolveReferences();
            Refresh(session != null ? session.State : null, true);
        }

        public static bool ShouldShow(CraftLiveRoomState state)
        {
            return state != null &&
                   state.sessionPhase == CraftLiveSessionPhase.StartScreen;
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
                session.StateChanged += HandleStateChanged;
                Refresh(session.State, true);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }
        }

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (displayRoot != null)
            {
                return;
            }

            CraftLivePad1Bindings pad1 =
                GetComponent<CraftLivePad1Bindings>();
            if (pad1 != null)
            {
                displayRoot = pad1.UiRoot;
                return;
            }

            CraftLivePad3Bindings pad3 =
                GetComponent<CraftLivePad3Bindings>();
            if (pad3 != null)
            {
                displayRoot = pad3.UiRoot;
            }
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            Refresh(state, false);
        }

        private void Refresh(CraftLiveRoomState state, bool force)
        {
            CraftLiveSessionPhase phase = state != null
                ? state.sessionPhase
                : (CraftLiveSessionPhase)(-1);
            if (!force && displayedPhase == phase)
            {
                return;
            }

            bool wasShowing = generatedScreen != null &&
                displayedPhase == CraftLiveSessionPhase.StartScreen;
            displayedPhase = phase;
            if (!ShouldShow(state))
            {
                if (wasShowing && isActiveAndEnabled)
                {
                    BeginSlideOut();
                }
                else if (slideRoutine == null)
                {
                    DestroySafely(generatedScreen);
                    generatedScreen = null;
                }
                return;
            }

            ResolveReferences();
            if (displayRoot == null)
            {
                return;
            }

            BuildScreen();
        }

        private void BuildScreen()
        {
            if (slideRoutine != null)
            {
                StopCoroutine(slideRoutine);
                slideRoutine = null;
            }

            DestroySafely(generatedScreen);
            generatedScreen = new GameObject("Generated_SynchronizedStartScreen");
            generatedScreen.transform.SetParent(displayRoot, false);
            generatedScreen.transform.localPosition = Vector3.zero;
            generatedScreen.transform.localRotation = Quaternion.identity;
            generatedScreen.transform.localScale = Vector3.one * 0.72f;
            generatedScreen.AddComponent<CraftLiveGeneratedRuntimeVisual>();

            Color wood = new Color(0.44f, 0.19f, 0.065f);
            Color inset = new Color(0.19f, 0.065f, 0.018f);
            Color highlight = new Color(0.62f, 0.31f, 0.11f);

            // Match the Pad 2 start panel frame so all four devices present a
            // single continuous forge wall before the authoritative button is
            // pressed on Pad 2.
            CreatePart(
                "CastIronShadow",
                new Vector3(0.08f, -0.1f, 0.08f),
                new Vector3(7.48f, 7.86f, 0.22f),
                CraftLiveForgeUITheme.DeepIron);
            CreatePart(
                "WalnutBacking",
                Vector3.zero,
                new Vector3(7.24f, 7.62f, 0.2f),
                new Color(0.25f, 0.115f, 0.045f));
            CreatePart(
                "ForgedIronFace",
                new Vector3(0f, 0f, -0.13f),
                new Vector3(6.82f, 7.18f, 0.11f),
                CraftLiveForgeUITheme.DeepIron);
            CreatePart(
                "TopBrassRail",
                new Vector3(0f, 3.54f, -0.35f),
                new Vector3(6.72f, 0.12f, 0.13f),
                CraftLiveForgeUITheme.Brass);
            CreatePart(
                "BottomBrassRail",
                new Vector3(0f, -3.54f, -0.35f),
                new Vector3(6.72f, 0.12f, 0.13f),
                CraftLiveForgeUITheme.Brass);
            CreatePart(
                "LeftIronRail",
                new Vector3(-3.34f, 0f, -0.33f),
                new Vector3(0.14f, 7.08f, 0.13f),
                CraftLiveForgeUITheme.Iron);
            CreatePart(
                "RightIronRail",
                new Vector3(3.34f, 0f, -0.33f),
                new Vector3(0.14f, 7.08f, 0.13f),
                CraftLiveForgeUITheme.Iron);
            CreatePart(
                "CarvedWoodPlaqueShadow",
                new Vector3(0.07f, 0.38f, -0.25f),
                new Vector3(5.35f, 4.15f, 0.16f),
                new Color(0.10f, 0.03f, 0.01f));
            CreatePart(
                "CarvedWoodPlaque",
                new Vector3(0f, 0.45f, -0.38f),
                new Vector3(5.15f, 3.95f, 0.13f),
                wood);

            for (int i = 0; i < 5; i++)
            {
                float y = -0.95f + i * 0.68f;
                CreatePart(
                    $"CarvedWoodGrain_{i}",
                    new Vector3(
                        i % 2 == 0 ? -0.18f : 0.22f,
                        y + 0.45f,
                        -0.5f),
                    new Vector3(4.35f - i * 0.13f, 0.055f, 0.025f),
                    inset);
            }

            GameObject handleGroove = CreatePart(
                "CarvedHammerHandleGroove",
                new Vector3(0.36f, 0.05f, -0.63f),
                new Vector3(0.58f, 2.95f, 0.055f),
                inset);
            handleGroove.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);
            GameObject handleHighlight = CreatePart(
                "CarvedHammerHandleHighlight",
                new Vector3(0.29f, 0.10f, -0.67f),
                new Vector3(0.24f, 2.55f, 0.035f),
                highlight);
            handleHighlight.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);
            GameObject headGroove = CreatePart(
                "CarvedHammerHeadGroove",
                new Vector3(-0.58f, 1.24f, -0.64f),
                new Vector3(2.25f, 0.82f, 0.06f),
                inset);
            headGroove.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);
            GameObject headHighlight = CreatePart(
                "CarvedHammerHeadHighlight",
                new Vector3(-0.62f, 1.20f, -0.68f),
                new Vector3(1.88f, 0.43f, 0.035f),
                highlight);
            headHighlight.transform.localRotation =
                Quaternion.Euler(0f, 0f, 37f);

            // Pads 1, 3 and 4 are synchronized waiting displays only. The
            // single authoritative start button stays on Pad 2, preventing
            // staff from starting the group from an unintended station.
        }

        private void BeginSlideOut()
        {
            if (generatedScreen == null || slideRoutine != null)
            {
                return;
            }

            slideRoutine = StartCoroutine(SlideOut());
        }

        private IEnumerator SlideOut()
        {
            GameObject screen = generatedScreen;
            Vector3 start = screen.transform.localPosition;
            Vector3 end = start + Vector3.up * slideDistance;
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, slideDuration);
            while (screen != null && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                screen.transform.localPosition =
                    Vector3.LerpUnclamped(start, end, eased);
                yield return null;
            }

            DestroySafely(screen);
            if (generatedScreen == screen)
            {
                generatedScreen = null;
            }

            slideRoutine = null;
        }

        private GameObject CreatePart(
            string name,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(generatedScreen.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            DestroySafely(part.GetComponent<Collider>());

            part.AddComponent<CraftLiveGeneratedRuntimeVisual>();
            CraftLiveForgeUITheme.ApplyForgeSurface(
                part.GetComponent<Renderer>(),
                color,
                0.02f,
                0.16f,
                0.25f);
            return part;
        }

        private static void DestroySafely(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
