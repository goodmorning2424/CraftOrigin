using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveWebPresentation : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CraftLiveSession session;
        [SerializeField, Min(1)] private int targetFrameRate = 30;
        [SerializeField, Range(2, 4)] private int idleRenderFrameInterval = 2;
        [SerializeField] private Vector2 targetAspect = new Vector2(3f, 4f);
        [SerializeField] private bool letterboxCamera = true;
        [SerializeField] private bool respectSafeArea = true;
        [SerializeField] private UnityEvent<bool> onPortraitChanged;
        [SerializeField] private UnityEvent<Rect> onSafeAreaChanged;

        private int previousWidth;
        private int previousHeight;
        private bool previousPortrait;
        private bool hasOrientation;
        private Rect previousSafeArea;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            QualitySettings.vSyncCount = 0;
            Application.runInBackground = false;
            Application.targetFrameRate = targetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Refresh();
        }

        private void OnEnable()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
                session.StateChanged += HandleStateChanged;
                ApplyEnergyProfile(session.State);
            }
            else
            {
                OnDemandRendering.renderFrameInterval = 1;
            }
        }

        private void Update()
        {
            if (Screen.width != previousWidth || Screen.height != previousHeight)
            {
                Refresh();
                return;
            }

            if (respectSafeArea && Screen.safeArea != previousSafeArea)
            {
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }

            OnDemandRendering.renderFrameInterval = 1;
            if (targetCamera != null)
            {
                targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            ApplyEnergyProfile(state);
        }

        private void ApplyEnergyProfile(CraftLiveRoomState state)
        {
            CraftLiveSessionPhase phase = state != null
                ? state.sessionPhase
                : CraftLiveSessionPhase.Playing;
            OnDemandRendering.renderFrameInterval =
                CalculateRenderFrameInterval(
                    phase,
                    idleRenderFrameInterval);
        }

        public static int CalculateRenderFrameInterval(
            CraftLiveSessionPhase phase,
            int idleInterval = 2)
        {
            return phase == CraftLiveSessionPhase.StartScreen ||
                   phase == CraftLiveSessionPhase.Finished
                ? Mathf.Clamp(idleInterval, 2, 4)
                : 1;
        }

        public void Refresh()
        {
            previousWidth = Mathf.Max(1, Screen.width);
            previousHeight = Mathf.Max(1, Screen.height);
            Rect safeArea = respectSafeArea
                ? ClampSafeArea(
                    Screen.safeArea,
                    previousWidth,
                    previousHeight)
                : new Rect(
                    0f,
                    0f,
                    previousWidth,
                    previousHeight);
            if (safeArea != previousSafeArea)
            {
                previousSafeArea = safeArea;
                onSafeAreaChanged?.Invoke(safeArea);
            }

            bool portrait = previousHeight >= previousWidth;
            if (!hasOrientation || portrait != previousPortrait)
            {
                hasOrientation = true;
                previousPortrait = portrait;
                onPortraitChanged?.Invoke(portrait);
            }

            if (!letterboxCamera || targetCamera == null)
            {
                return;
            }

            targetCamera.rect = CalculateCameraViewport(
                safeArea,
                new Vector2Int(previousWidth, previousHeight),
                targetAspect);
        }

        public static Rect CalculateCameraViewport(
            Rect safeAreaPixels,
            Vector2Int screenSize,
            Vector2 aspect)
        {
            int width = Mathf.Max(1, screenSize.x);
            int height = Mathf.Max(1, screenSize.y);
            Rect safe = ClampSafeArea(safeAreaPixels, width, height);
            float desired = Mathf.Max(0.01f, aspect.x) /
                            Mathf.Max(0.01f, aspect.y);
            float actual = safe.width / Mathf.Max(1f, safe.height);
            Rect content = safe;
            if (actual > desired)
            {
                float contentWidth = safe.height * desired;
                content.x += (safe.width - contentWidth) * 0.5f;
                content.width = contentWidth;
            }
            else
            {
                float contentHeight = safe.width / desired;
                content.y += (safe.height - contentHeight) * 0.5f;
                content.height = contentHeight;
            }

            return new Rect(
                content.x / width,
                content.y / height,
                content.width / width,
                content.height / height);
        }

        private static Rect ClampSafeArea(
            Rect safeArea,
            int screenWidth,
            int screenHeight)
        {
            float xMin = Mathf.Clamp(safeArea.xMin, 0f, screenWidth);
            float yMin = Mathf.Clamp(safeArea.yMin, 0f, screenHeight);
            float xMax = Mathf.Clamp(safeArea.xMax, xMin, screenWidth);
            float yMax = Mathf.Clamp(safeArea.yMax, yMin, screenHeight);
            if (xMax - xMin < 1f || yMax - yMin < 1f)
            {
                return new Rect(0f, 0f, screenWidth, screenHeight);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
