using UnityEngine;
using UnityEngine.EventSystems;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveMixInput :
        MonoBehaviour,
        IPointerDownHandler,
        IPointerMoveHandler,
        IPointerUpHandler,
        IPointerExitHandler
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private RectTransform mixingArea;
        [SerializeField, Min(0.03f)] private float publishInterval = 0.12f;

        private bool mixing;
        private int pointerId;
        private float previousAngle;
        private float pendingPower;
        private float lastPublishTime;
        private int activeMixRevision = -1;

        private void Awake()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (mixingArea == null)
            {
                mixingArea = transform as RectTransform;
            }
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.StateChanged += HandleStateChanged;
                HandleStateChanged(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }
        }

        private void Update()
        {
            CraftLiveRoomState state = session != null ? session.State : null;
            if (state == null || state.craft.status != CraftLiveCraftStatus.Mixing)
            {
                return;
            }

            float duration = session.Rules != null
                ? session.Rules.MixingDurationSeconds
                : 5f;
            long elapsedMs = CraftLiveSession.UnixNowMs() - state.craft.startedAtUnixMs;
            if (elapsedMs >= duration * 1000f)
            {
                session.CompleteSynthesis();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (session == null ||
                session.State.craft.status != CraftLiveCraftStatus.Mixing ||
                mixingArea == null)
            {
                return;
            }

            if (!TryGetAngle(eventData, out float angle))
            {
                return;
            }

            mixing = true;
            pointerId = eventData.pointerId;
            previousAngle = angle;
            pendingPower = session.State.craft.mixPower;
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!mixing || eventData.pointerId != pointerId)
            {
                return;
            }

            if (!TryGetAngle(eventData, out float angle))
            {
                return;
            }

            float difference = Mathf.DeltaAngle(
                previousAngle * Mathf.Rad2Deg,
                angle * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            previousAngle = angle;
            float powerPerRadian = session.Rules != null
                ? session.Rules.PowerPerRadian
                : 7.5f;
            pendingPower = Mathf.Clamp(
                pendingPower + Mathf.Abs(difference) * powerPerRadian,
                0f,
                100f);

            if (Time.unscaledTime - lastPublishTime >= publishInterval ||
                pendingPower >= 100f)
            {
                lastPublishTime = Time.unscaledTime;
                session.SetMixPower(pendingPower);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EndPointer(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId && !eventData.dragging)
            {
                EndPointer(eventData);
            }
        }

        private void HandleStateChanged(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.craft.status != CraftLiveCraftStatus.Mixing)
            {
                mixing = false;
                activeMixRevision = -1;
                return;
            }

            if (activeMixRevision != state.craft.startedAtUnixMs.GetHashCode())
            {
                activeMixRevision = state.craft.startedAtUnixMs.GetHashCode();
                pendingPower = state.craft.mixPower;
            }
        }

        private bool TryGetAngle(PointerEventData eventData, out float angle)
        {
            angle = 0f;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    mixingArea,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            angle = Mathf.Atan2(localPoint.y, localPoint.x);
            return true;
        }

        private void EndPointer(PointerEventData eventData)
        {
            if (!mixing || eventData.pointerId != pointerId)
            {
                return;
            }

            mixing = false;
            session.SetMixPower(pendingPower);
        }
    }
}
