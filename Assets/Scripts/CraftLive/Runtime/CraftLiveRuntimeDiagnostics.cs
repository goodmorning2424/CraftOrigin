using System;
using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLiveRuntimeDiagnostics : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLiveRoomTransport transport;
        [SerializeField, Min(2f)] private float staleConnectionSeconds = 15f;
        [SerializeField] private UnityEvent<string> onSummaryChanged;
        [SerializeField] private UnityEvent<bool> onHealthyChanged;

        private bool hasHealthState;
        private bool previousHealthy;
        private string previousSummary = string.Empty;

        public bool IsHealthy { get; private set; }
        public string Summary { get; private set; } = string.Empty;

        public event Action<string> SummaryChanged;
        public event Action<bool> HealthyChanged;

        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<CraftLiveSession>();
            }

            if (transport == null)
            {
                transport = GetComponent<CraftLiveRoomTransport>();
            }
        }

        private void OnEnable()
        {
            if (session != null)
            {
                session.StateChanged += HandleStateChanged;
            }

            if (transport != null)
            {
                transport.ConnectionChanged += HandleConnectionChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= HandleStateChanged;
            }

            if (transport != null)
            {
                transport.ConnectionChanged -= HandleConnectionChanged;
            }
        }

        private void Update()
        {
            Refresh();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            Refresh();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            Refresh();
        }

        private void HandleStateChanged(CraftLiveRoomState unused)
        {
            Refresh();
        }

        private void HandleConnectionChanged(
            CraftLiveConnectionState unusedState,
            string unusedMessage)
        {
            Refresh();
        }

        private void Refresh()
        {
            CraftLiveRoomState roomState =
                session != null ? session.State : null;
            bool localMode = transport == null || !transport.IsRemoteMode;
            bool stale = false;
            if (!localMode &&
                transport.LastSuccessfulRequestUnixMs > 0)
            {
                long elapsedMs =
                    CraftLiveSession.UnixNowMs() -
                    transport.LastSuccessfulRequestUnixMs;
                stale = elapsedMs >
                        Mathf.Max(2f, staleConnectionSeconds) * 1000f;
            }

            IsHealthy = localMode ||
                        (transport != null &&
                         transport.InitialSyncComplete &&
                         transport.ConnectionState !=
                             CraftLiveConnectionState.Offline &&
                         !stale);

            string role = session != null
                ? session.Role.ToString()
                : "Unknown";
            string room = session != null
                ? session.RoomId
                : "-";
            long revision = roomState != null ? roomState.revision : -1;
            string connection = transport != null
                ? transport.ConnectionState.ToString()
                : "NoTransport";
            string pending = transport != null &&
                             transport.HasPendingPublish
                ? " pending"
                : string.Empty;
            string staleLabel = stale ? " stale" : string.Empty;
            Summary =
                $"{connection}{pending}{staleLabel} | " +
                $"room={room} role={role} rev={revision}";

            if (Summary != previousSummary)
            {
                previousSummary = Summary;
                onSummaryChanged?.Invoke(Summary);
                SummaryChanged?.Invoke(Summary);
            }

            if (!hasHealthState || IsHealthy != previousHealthy)
            {
                hasHealthState = true;
                previousHealthy = IsHealthy;
                onHealthyChanged?.Invoke(IsHealthy);
                HealthyChanged?.Invoke(IsHealthy);
            }
        }
    }
}
