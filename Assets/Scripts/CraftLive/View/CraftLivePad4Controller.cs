using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad4Controller :
        MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad4Bindings bindings;
        [SerializeField] private CraftLiveHologramView hologramView;
        [SerializeField] private bool createFallbackText = true;
        [SerializeField] private UnityEvent<string> onWeaponNameChanged;
        [SerializeField] private UnityEvent<string> onWeaponCodeChanged;
        [SerializeField] private UnityEvent<bool> onFinalWeaponSelected;

        private TextMesh label;
        private int observedResultSerial = -1;
        private string observedCode = string.Empty;

        private void Awake()
        {
            ResolveReferences();
            EnsureSynchronizedStartScreen();
        }

        private void EnsureSynchronizedStartScreen()
        {
            CraftLiveSynchronizedStartScreen startScreen =
                GetComponent<CraftLiveSynchronizedStartScreen>();
            if (startScreen == null)
            {
                startScreen = gameObject.AddComponent<
                    CraftLiveSynchronizedStartScreen>();
            }

            startScreen.Configure(bindings != null ? bindings.UiRoot : null);
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

        private void Start()
        {
            BuildFallback();
            Refresh(session != null ? session.State : null);
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }
        }

        public void Configure(
            CraftLivePad4Bindings targetBindings,
            CraftLiveHologramView targetHologram)
        {
            bindings = targetBindings;
            hologramView = targetHologram;
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (bindings == null)
            {
                bindings = GetComponent<CraftLivePad4Bindings>();
            }

            if (hologramView == null)
            {
                hologramView =
                    GetComponentInChildren<
                        CraftLiveHologramView>(true);
            }
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null || state.result == null)
            {
                return;
            }

            if (observedResultSerial !=
                state.result.resultSerial)
            {
                observedResultSerial =
                    state.result.resultSerial;
                onWeaponNameChanged?.Invoke(
                    state.result.weaponName);
            }

            if (observedCode != state.finalWeaponCode)
            {
                observedCode =
                    state.finalWeaponCode ?? string.Empty;
                onWeaponCodeChanged?.Invoke(observedCode);
            }

            bool finalSelected =
                state.sessionPhase ==
                CraftLiveSessionPhase.Finished;
            onFinalWeaponSelected?.Invoke(finalSelected);
            if (label != null)
            {
                label.text = finalSelected
                    ? $"{state.result.weaponName}\n" +
                      (string.IsNullOrWhiteSpace(state.finalWeaponCode)
                          ? "グループ番号 発行中…"
                          : $"グループ番号 {state.finalWeaponCode}")
                    : state.result.weaponName;
            }
        }

        private void BuildFallback()
        {
            if (!createFallbackText ||
                bindings == null ||
                bindings.UiRoot == null)
            {
                return;
            }

            GameObject textObject =
                new GameObject("Generated_Pad4Label");
            textObject.transform.SetParent(
                bindings.UiRoot,
                false);
            textObject.transform.localPosition =
                new Vector3(0f, -4.1f, -0.6f);
            label = textObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                label,
                0.072f,
                CraftLiveForgeUITheme.ParchmentText);
        }
    }
}
