using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveSessionTimerController :
        MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private bool createFallbackText = true;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;
        [SerializeField] private UnityEvent<float> onRemainingSeconds;
        [SerializeField] private UnityEvent<string> onTimerTextChanged;
        [SerializeField] private UnityEvent<CraftLiveSessionPhase>
            onSessionPhaseChanged;

        private TextMesh timerText;
        private float nextRefreshTime;
        private CraftLiveSessionPhase observedPhase =
            (CraftLiveSessionPhase)(-1);

        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<CraftLiveSession>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void Start()
        {
            session?.EnsureSessionStarted();
            BuildFallback();
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime =
                Time.unscaledTime + refreshInterval;
            Refresh();
        }

        public static string FormatTime(float seconds)
        {
            int total = Mathf.Max(
                0,
                Mathf.CeilToInt(seconds));
            return $"{total / 60:00}:{total % 60:00}";
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Expire Session Now")]
        private void DebugExpireSessionNow()
        {
            if (!Application.isPlaying || session == null)
            {
                Debug.LogWarning(
                    "Craft-live: Play Modeで実行してください。",
                    this);
                return;
            }

            session.ExpireSession();
        }
#endif

        private void Refresh()
        {
            if (session == null || session.State == null)
            {
                return;
            }

            CraftLiveRoomState state = session.State;
            float remaining =
                session.GetRemainingSessionSeconds();
            if (state.sessionPhase ==
                    CraftLiveSessionPhase.Playing &&
                remaining <= 0f)
            {
                session.ExpireSession();
                state = session.State;
            }

            string value = state.sessionPhase ==
                CraftLiveSessionPhase.Playing
                    ? FormatTime(remaining)
                    : state.sessionPhase ==
                      CraftLiveSessionPhase.FinalSelection
                        ? "武器を選択"
                        : "完成";
            if (timerText != null)
            {
                timerText.text = value;
            }

            onRemainingSeconds?.Invoke(remaining);
            onTimerTextChanged?.Invoke(value);
            if (observedPhase != state.sessionPhase)
            {
                observedPhase = state.sessionPhase;
                onSessionPhaseChanged?.Invoke(observedPhase);
            }
        }

        private void BuildFallback()
        {
            if (!createFallbackText ||
                targetCamera == null)
            {
                return;
            }

            GameObject timerObject =
                new GameObject("Generated_SessionTimer");
            timerObject.transform.SetParent(
                targetCamera.transform,
                false);
            timerObject.transform.localPosition =
                new Vector3(0f, 4.35f, 1f);
            timerText = timerObject.AddComponent<TextMesh>();
            timerText.anchor = TextAnchor.MiddleCenter;
            timerText.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                timerText,
                0.055f,
                CraftLiveForgeUITheme.ParchmentText);
        }
    }
}
