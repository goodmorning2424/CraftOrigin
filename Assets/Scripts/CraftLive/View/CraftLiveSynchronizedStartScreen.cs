using UnityEngine;

namespace CraftOrigin.CraftLive
{
    /// <summary>
    /// Pad 1/3 companion for the Pad 2 start screen. Every pad observes the
    /// same room phase, so pressing the single Pad 2 start button dismisses
    /// all start screens through the normal synchronized session update.
    /// </summary>
    public sealed class CraftLiveSynchronizedStartScreen : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Transform displayRoot;

        private GameObject generatedScreen;
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

            displayedPhase = phase;
            if (!ShouldShow(state))
            {
                DestroySafely(generatedScreen);
                generatedScreen = null;
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

            CreatePart(
                "FullScreenBackdrop",
                new Vector3(0f, 0f, 0.18f),
                new Vector3(7.5f, 10.4f, 0.2f),
                CraftLiveForgeUITheme.DeepIron);
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

            GameObject handle = CreatePart(
                "CarvedHammerHandle",
                new Vector3(0.34f, 0.08f, -0.58f),
                new Vector3(0.5f, 2.85f, 0.05f),
                highlight);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 37f);
            GameObject head = CreatePart(
                "CarvedHammerHead",
                new Vector3(-0.6f, 1.24f, -0.59f),
                new Vector3(2.18f, 0.72f, 0.055f),
                highlight);
            head.transform.localRotation = Quaternion.Euler(0f, 0f, 37f);

            // Pad 1 and Pad 3 are synchronized waiting displays only. The
            // single authoritative start button stays on Pad 2, preventing
            // staff from starting the group from an unintended station.
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
