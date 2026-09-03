using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace CraftOrigin.CraftLive
{
    [Serializable]
    public sealed class CraftLivePadPresence
    {
        public string role = string.Empty;
        public long lastSeenUnixMs;
    }

    [Serializable]
    public sealed class CraftLiveRoomPresence
    {
        public CraftLivePadPresence pad1;
        public CraftLivePadPresence pad2;
        public CraftLivePadPresence pad3;
        public CraftLivePadPresence pad4;

        public CraftLivePadPresence Get(CraftLiveRole role)
        {
            switch (role)
            {
                case CraftLiveRole.MaterialPad:
                    return pad1;
                case CraftLiveRole.WorkbenchPad:
                    return pad2;
                case CraftLiveRole.QrPad:
                    return pad3;
                case CraftLiveRole.HologramPad:
                    return pad4;
                default:
                    return null;
            }
        }

        public void Set(CraftLiveRole role, CraftLivePadPresence value)
        {
            switch (role)
            {
                case CraftLiveRole.MaterialPad:
                    pad1 = value;
                    break;
                case CraftLiveRole.WorkbenchPad:
                    pad2 = value;
                    break;
                case CraftLiveRole.QrPad:
                    pad3 = value;
                    break;
                case CraftLiveRole.HologramPad:
                    pad4 = value;
                    break;
            }
        }
    }

    [Serializable]
    public sealed class CraftLiveSharedWeaponDto
    {
        public string weaponId;
        public string teamName;
        public string weaponName;
        public string weaponImageId;
        public int attack;
        public int defense;
        public float evasion;
        public string attribute;
        public string skillAId;
        public string skillBId;
        public string ultimateSkillId;
        public string motionType;
    }

    [Serializable]
    public sealed class CraftLiveWeaponGroupRecord
    {
        public int schemaVersion = 1;
        public string groupNumber;
        public string status = "ready";
        public long createdAtUnixMs;
        public long expiresAtUnixMs;
        public string sourceRoomId;
        public int sourceGroupGeneration;
        public int sourceResultSerial;
        public CraftLiveSharedWeaponDto weapon;
        public CraftLiveResultState craftResult;
    }

    public enum CraftLiveConnectionState
    {
        Local,
        Connecting,
        Online,
        Degraded,
        Offline,
        Conflict
    }

    [DefaultExecutionOrder(-100)]
    public sealed class CraftLiveRoomTransport : MonoBehaviour
    {
        public const int ProductionGroupNumberCount = 999;
        private const int TwoDigitGroupNumberCount = 99;
        private const int ThreeDigitGroupNumberCount = 900;

        private static readonly Dictionary<string, CraftLiveRoomState>
            LocalRooms = new Dictionary<string, CraftLiveRoomState>();
        private static readonly List<CraftLiveRoomTransport> LocalClients =
            new List<CraftLiveRoomTransport>();
        private static readonly Dictionary<string, CraftLiveRoomPresence>
            LocalPresence =
                new Dictionary<string, CraftLiveRoomPresence>();

        [Header("References")]
        [SerializeField] private CraftLiveSession session;

        [Header("Firebase Realtime Database")]
        [SerializeField] private bool useFirebase = true;
        [SerializeField] private string firebaseDatabaseUrl =
            "https://craft-live-default-rtdb.firebaseio.com";
        [SerializeField, Min(0.2f)] private float pollIntervalSeconds = 0.5f;
        [SerializeField, Min(2f)] private float requestTimeoutSeconds = 10f;

        [Header("Recovery")]
        [SerializeField, Min(0.25f)] private float initialRetryDelaySeconds =
            0.75f;
        [SerializeField, Min(1f)] private float maximumRetryDelaySeconds =
            8f;
        [SerializeField] private bool cachePendingState = true;

        [Header("Pad Presence")]
        [SerializeField, Min(0.5f)] private float presenceHeartbeatSeconds =
            1.5f;
        [SerializeField, Min(2f)] private float presenceTimeoutSeconds = 6f;

        [Header("Events")]
        [SerializeField] private UnityEvent<string> onConnectionStatusChanged;
        [SerializeField] private UnityEvent<bool> onOnlineChanged;

        private bool initialSyncComplete;
        private CraftLiveRoomState pendingPublish;
        private Coroutine pollingCoroutine;
        private Coroutine publishingCoroutine;
        private Coroutine presenceCoroutine;
        private Coroutine groupPublishCoroutine;
        private CraftLiveRoomPresence roomPresence =
            new CraftLiveRoomPresence();
        private string remoteEtag = string.Empty;
        private int consecutiveFailures;
        private long lastSuccessfulRequestUnixMs;
        private CraftLiveConnectionState connectionState =
            CraftLiveConnectionState.Connecting;
        private string connectionMessage = string.Empty;
        private bool presencePublishingEnabled = true;
        private bool hasEnabled;
        private string simulatorGroupNumber = string.Empty;

        public bool IsRemoteMode =>
            useFirebase && !string.IsNullOrWhiteSpace(firebaseDatabaseUrl);
        public bool IsOnline =>
            !IsRemoteMode ||
            connectionState == CraftLiveConnectionState.Online;
        public bool HasPendingPublish => pendingPublish != null;
        public bool InitialSyncComplete => initialSyncComplete;
        public string FirebaseDatabaseUrl => firebaseDatabaseUrl;
        public CraftLiveConnectionState ConnectionState => connectionState;
        public string ConnectionMessage => connectionMessage;
        public int ConsecutiveFailures => consecutiveFailures;
        public long LastSuccessfulRequestUnixMs =>
            lastSuccessfulRequestUnixMs;
        public CraftLiveRoomPresence RoomPresence => roomPresence;
        public bool IsPresencePublishing =>
            presencePublishingEnabled && presenceCoroutine != null;
        public string SimulatorGroupNumber => simulatorGroupNumber;

        public event Action<
            CraftLiveConnectionState,
            string> ConnectionChanged;
        public event Action PresenceChanged;

        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<CraftLiveSession>();
            }
        }

        public void SetSimulatorGroupNumber(string value)
        {
            simulatorGroupNumber = IsFiveDigitGroupNumber(value)
                ? value.Trim()
                : string.Empty;
        }

        private void OnEnable()
        {
            hasEnabled = true;
            if (session == null)
            {
                return;
            }

            session.LocalStateChanged += HandleLocalStateChanged;
            if (IsRemoteMode)
            {
                LoadCachedStates();
                SetConnectionState(
                    CraftLiveConnectionState.Connecting,
                    "Firebaseへ接続中...");
                pollingCoroutine = StartCoroutine(PollRemoteRoom());
            }
            else
            {
                ConnectLocalRoom();
            }

            StartPresencePublishingIfNeeded();
        }

        private void OnDisable()
        {
            hasEnabled = false;
            if (session != null)
            {
                session.LocalStateChanged -= HandleLocalStateChanged;
            }

            if (pollingCoroutine != null)
            {
                StopCoroutine(pollingCoroutine);
                pollingCoroutine = null;
            }

            if (publishingCoroutine != null)
            {
                StopCoroutine(publishingCoroutine);
                publishingCoroutine = null;
            }

            if (presenceCoroutine != null)
            {
                StopCoroutine(presenceCoroutine);
                presenceCoroutine = null;
            }

            if (groupPublishCoroutine != null)
            {
                StopCoroutine(groupPublishCoroutine);
                groupPublishCoroutine = null;
            }

            LocalClients.Remove(this);
        }

        public bool IsRoleConnected(CraftLiveRole targetRole)
        {
            CraftLivePadPresence record = roomPresence?.Get(targetRole);
            if (record == null || record.lastSeenUnixMs <= 0)
            {
                return false;
            }

            long maximumAgeMs = Mathf.RoundToInt(
                Mathf.Max(2f, presenceTimeoutSeconds) * 1000f);
            return CraftLiveSession.UnixNowMs() - record.lastSeenUnixMs <=
                   maximumAgeMs;
        }

        public bool AreAllPadsConnected()
        {
            return IsRoleConnected(CraftLiveRole.MaterialPad) &&
                   IsRoleConnected(CraftLiveRole.WorkbenchPad) &&
                   IsRoleConnected(CraftLiveRole.QrPad) &&
                   IsRoleConnected(CraftLiveRole.HologramPad);
        }

        public void SetPresencePublishingEnabled(bool value)
        {
            presencePublishingEnabled = value;
            if (!value)
            {
                if (presenceCoroutine != null)
                {
                    StopCoroutine(presenceCoroutine);
                    presenceCoroutine = null;
                }

                return;
            }

            StartPresencePublishingIfNeeded();
        }

        public void BeginPresencePublishing()
        {
            SetPresencePublishingEnabled(true);
        }

        private void StartPresencePublishingIfNeeded()
        {
            if (!presencePublishingEnabled || presenceCoroutine != null ||
                !hasEnabled || !isActiveAndEnabled || session == null)
            {
                return;
            }

            presenceCoroutine = StartCoroutine(UpdatePadPresence());
        }

        private IEnumerator UpdatePadPresence()
        {
            while (enabled && presencePublishingEnabled && session != null)
            {
                CraftLivePadPresence own = new CraftLivePadPresence
                {
                    role = PresenceKey(session.Role),
                    lastSeenUnixMs = CraftLiveSession.UnixNowMs()
                };

                if (IsRemoteMode)
                {
                    using (UnityWebRequest write =
                           UnityWebRequest.Put(
                               BuildPresenceRoleUrl(session.Role),
                               JsonUtility.ToJson(own)))
                    {
                        write.SetRequestHeader(
                            "Content-Type",
                            "application/json");
                        write.timeout = Mathf.CeilToInt(
                            requestTimeoutSeconds);
                        yield return write.SendWebRequest();
                    }

                    using (UnityWebRequest read =
                           UnityWebRequest.Get(BuildPresenceRoomUrl()))
                    {
                        read.timeout = Mathf.CeilToInt(
                            requestTimeoutSeconds);
                        yield return read.SendWebRequest();
                        if (read.result == UnityWebRequest.Result.Success &&
                            !string.IsNullOrWhiteSpace(
                                read.downloadHandler.text) &&
                            read.downloadHandler.text != "null")
                        {
                            try
                            {
                                roomPresence = JsonUtility.FromJson<
                                    CraftLiveRoomPresence>(
                                    read.downloadHandler.text) ??
                                    new CraftLiveRoomPresence();
                            }
                            catch (Exception)
                            {
                                roomPresence = new CraftLiveRoomPresence();
                            }
                        }
                    }
                }
                else
                {
                    if (!LocalPresence.TryGetValue(
                            session.RoomId,
                            out CraftLiveRoomPresence local))
                    {
                        local = new CraftLiveRoomPresence();
                        LocalPresence[session.RoomId] = local;
                    }

                    local.Set(session.Role, own);
                    roomPresence = local;
                }

                PresenceChanged?.Invoke();
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(0.5f, presenceHeartbeatSeconds));
            }
        }

        public void Configure(
            bool remoteEnabled,
            string databaseUrl,
            float pollInterval,
            float requestTimeout)
        {
            Configure(
                remoteEnabled,
                databaseUrl,
                pollInterval,
                requestTimeout,
                initialRetryDelaySeconds,
                maximumRetryDelaySeconds,
                cachePendingState);
        }

        public void Configure(
            bool remoteEnabled,
            string databaseUrl,
            float pollInterval,
            float requestTimeout,
            float initialRetryDelay,
            float maximumRetryDelay,
            bool enablePendingStateCache)
        {
            useFirebase = remoteEnabled;
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                firebaseDatabaseUrl = databaseUrl.Trim().TrimEnd('/');
            }

            pollIntervalSeconds = Mathf.Max(0.2f, pollInterval);
            requestTimeoutSeconds = Mathf.Max(2f, requestTimeout);
            initialRetryDelaySeconds = Mathf.Max(0.25f, initialRetryDelay);
            maximumRetryDelaySeconds = Mathf.Max(
                initialRetryDelaySeconds,
                maximumRetryDelay);
            cachePendingState = enablePendingStateCache;
        }

        private void ConnectLocalRoom()
        {
            if (!LocalClients.Contains(this))
            {
                LocalClients.Add(this);
            }

            if (!LocalRooms.TryGetValue(
                    session.RoomId,
                    out CraftLiveRoomState localState))
            {
                localState = session.State.Clone();
                LocalRooms[session.RoomId] = localState;
            }

            session.ApplyRemoteState(localState.Clone());
            initialSyncComplete = true;
            SetConnectionState(
                CraftLiveConnectionState.Local,
                $"Local room {session.RoomId}");
        }

        private void HandleLocalStateChanged(CraftLiveRoomState nextState)
        {
            if (nextState == null)
            {
                return;
            }

            if (IsRemoteMode)
            {
                pendingPublish = nextState.Clone();
                SaveCachedState(PendingCacheKey, pendingPublish);
                StartPublisherIfReady();
                EnsureGroupNumberPublish(nextState);
                return;
            }

            LocalRooms[session.RoomId] = nextState.Clone();
            foreach (CraftLiveRoomTransport client in LocalClients.ToArray())
            {
                if (client != null &&
                    client.session != null &&
                    client.session.RoomId == session.RoomId &&
                    client != this)
                {
                    client.session.ApplyRemoteState(nextState.Clone());
                }
            }
            EnsureGroupNumberPublish(nextState);
        }

        private IEnumerator PollRemoteRoom()
        {
            while (enabled && session != null)
            {
                using (UnityWebRequest request =
                       UnityWebRequest.Get(BuildRoomUrl()))
                {
                    request.SetRequestHeader("X-Firebase-ETag", "true");
                    request.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
                    yield return request.SendWebRequest();

                    if (request.result ==
                        UnityWebRequest.Result.Success)
                    {
                        HandleSuccessfulPoll(request);
                    }
                    else
                    {
                        RecordFailure(
                            $"Firebase受信失敗: {request.error}");
                    }
                }

                StartPublisherIfReady();
                yield return new WaitForSecondsRealtime(
                    pollIntervalSeconds);
            }
        }

        private void HandleSuccessfulPoll(UnityWebRequest request)
        {
            MarkRequestSuccessful();
            UpdateRemoteEtag(request);
            string json = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                initialSyncComplete = true;
                if (pendingPublish == null)
                {
                    pendingPublish = session.State.Clone();
                    SaveCachedState(PendingCacheKey, pendingPublish);
                }

                SetConnectionState(
                    CraftLiveConnectionState.Online,
                    $"Firebase room {session.RoomId}");
                return;
            }

            CraftLiveRoomState remote;
            try
            {
                remote = CraftLiveRoomState.FromJson(json);
            }
            catch (Exception exception)
            {
                RecordFailure(
                    $"Firebaseデータ解析失敗: {exception.Message}");
                return;
            }

            initialSyncComplete = true;
            CraftLiveRoomState localCandidate =
                pendingPublish ?? session.State;
            int comparison = CompareVersion(remote, localCandidate);
            if (comparison > 0)
            {
                if (TryBuildTransferReconciledState(
                        remote,
                        localCandidate,
                        out CraftLiveRoomState reconciled))
                {
                    session.ApplyRemoteState(reconciled);
                    pendingPublish = reconciled.Clone();
                    SaveCachedState(PendingCacheKey, pendingPublish);
                    StartPublisherIfReady();
                }
                else
                {
                    session.ApplyRemoteState(remote);
                    pendingPublish = null;
                    DeleteCachedState(PendingCacheKey);
                }
            }
            else if (pendingPublish == null && comparison >= 0)
            {
                session.ApplyRemoteState(remote);
            }

            SaveCachedState(ConfirmedCacheKey, remote);
            SetConnectionState(
                CraftLiveConnectionState.Online,
                $"Firebase room {session.RoomId}");
            EnsureGroupNumberPublish(session.State);
        }

        private void EnsureGroupNumberPublish(CraftLiveRoomState candidate)
        {
            if (candidate == null || session == null ||
                session.Role != CraftLiveRole.WorkbenchPad ||
                candidate.sessionPhase != CraftLiveSessionPhase.Finished ||
                candidate.result == null ||
                candidate.selectedFinalResultSerial <= 0 ||
                !string.IsNullOrWhiteSpace(candidate.finalWeaponCode) ||
                groupPublishCoroutine != null)
            {
                return;
            }

            int generation = candidate.groupGeneration;
            int serial = candidate.selectedFinalResultSerial;
            CraftLiveResultState result = candidate.result.Clone();
            if (!IsRemoteMode)
            {
                string localNumber = CreateGroupCandidate(
                    session.RoomId,
                    generation,
                    serial,
                    0);
                session.ApplyIssuedGroupNumber(generation, serial, localNumber);
                return;
            }

            groupPublishCoroutine = StartCoroutine(
                PublishWeaponGroup(generation, serial, result));
        }

        private IEnumerator PublishWeaponGroup(
            int generation,
            int resultSerial,
            CraftLiveResultState result)
        {
            HashSet<string> occupiedNumbers = null;
            bool useOccupancySnapshot =
                string.IsNullOrWhiteSpace(simulatorGroupNumber);
            if (useOccupancySnapshot)
            {
                using (UnityWebRequest snapshot = UnityWebRequest.Get(
                           BuildWeaponGroupsRootUrl() + "?shallow=true"))
                {
                    snapshot.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
                    yield return snapshot.SendWebRequest();
                    if (snapshot.result == UnityWebRequest.Result.Success)
                    {
                        occupiedNumbers = ParseOccupiedGroupNumbers(
                            snapshot.downloadHandler.text);
                    }
                }
            }

            while (enabled && IsGroupIssuePending(generation, resultSerial))
            {
                bool issued = false;
                bool stopCandidateScan = false;
                string publishFailure = string.Empty;
                int candidateCount = string.IsNullOrWhiteSpace(
                    simulatorGroupNumber)
                    ? ProductionGroupNumberCount
                    : 1;
                for (int offset = 0; offset < candidateCount && !issued; offset++)
                {
                    string number = string.IsNullOrWhiteSpace(
                        simulatorGroupNumber)
                        ? CreateGroupCandidate(
                            session.RoomId,
                            generation,
                            resultSerial,
                            offset)
                        : simulatorGroupNumber;
                    if (useOccupancySnapshot &&
                        occupiedNumbers != null &&
                        occupiedNumbers.Contains(number))
                    {
                        continue;
                    }

                    string url = BuildWeaponGroupUrl(number);
                    string etag = string.Empty;
                    bool available = false;

                    using (UnityWebRequest get = UnityWebRequest.Get(url))
                    {
                        get.SetRequestHeader("X-Firebase-ETag", "true");
                        get.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
                        yield return get.SendWebRequest();
                        if (get.result != UnityWebRequest.Result.Success)
                        {
                            publishFailure = DescribeGroupPublishFailure(
                                get.responseCode,
                                get.error);
                            stopCandidateScan =
                                IsFirebaseAuthorizationFailure(
                                    get.responseCode);
                            if (stopCandidateScan)
                                break;
                            continue;
                        }

                        etag = get.GetResponseHeader("ETag") ?? string.Empty;
                        // Without an ETag the following PUT cannot be made
                        // conditional. Retrying is safer than overwriting a
                        // group that another room may have reserved meanwhile.
                        if (string.IsNullOrWhiteSpace(etag))
                        {
                            publishFailure =
                                "Firebase応答にETagがありません。";
                            continue;
                        }

                        string json = get.downloadHandler.text;
                        if (string.IsNullOrWhiteSpace(json) || json == "null")
                        {
                            available = true;
                        }
                        else
                        {
                            try
                            {
                                CraftLiveWeaponGroupRecord existing =
                                    JsonUtility.FromJson<CraftLiveWeaponGroupRecord>(json);
                                available = IsWeaponGroupSlotAvailable(
                                    existing,
                                    session.RoomId,
                                    generation,
                                    resultSerial,
                                    CraftLiveSession.UnixNowMs());
                            }
                            catch (Exception)
                            {
                                available = false;
                            }
                        }
                    }

                    if (stopCandidateScan)
                        break;

                    if (!available || !IsGroupIssuePending(generation, resultSerial))
                        continue;

                    CraftLiveWeaponGroupRecord record = BuildWeaponGroupRecord(
                        number,
                        generation,
                        resultSerial,
                        result);
                    byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(record));
                    using (UnityWebRequest put = new UnityWebRequest(url, "PUT"))
                    {
                        put.uploadHandler = new UploadHandlerRaw(body);
                        put.downloadHandler = new DownloadHandlerBuffer();
                        put.SetRequestHeader("Content-Type", "application/json");
                        put.SetRequestHeader("If-Match", etag);
                        put.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
                        yield return put.SendWebRequest();
                        if (put.result == UnityWebRequest.Result.Success)
                        {
                            issued = session.ApplyIssuedGroupNumber(
                                generation,
                                resultSerial,
                                number,
                                IsFiveDigitGroupNumber(number));
                        }
                        else
                        {
                            publishFailure = DescribeGroupPublishFailure(
                                put.responseCode,
                                put.error);
                            stopCandidateScan =
                                IsFirebaseAuthorizationFailure(
                                    put.responseCode);
                        }
                    }

                    if (stopCandidateScan)
                        break;
                }

                if (issued)
                    break;
                // If the snapshot was stale, or every number existed only as
                // an expired record, the retry performs the original guarded
                // per-number scan so reuse semantics are preserved.
                useOccupancySnapshot = false;
                session.ReportGroupNumberRetry(
                    generation,
                    resultSerial,
                    publishFailure);
                yield return new WaitForSecondsRealtime(3f);
            }
            groupPublishCoroutine = null;
        }

        public static bool IsFirebaseAuthorizationFailure(long responseCode)
        {
            return responseCode == 401L || responseCode == 403L;
        }

        public static string DescribeGroupPublishFailure(
            long responseCode,
            string error)
        {
            if (IsFirebaseAuthorizationFailure(responseCode))
            {
                return "Firebase Database Rulesが読み書きを拒否しています。";
            }

            if (responseCode > 0L)
            {
                return $"Firebase通信エラー ({responseCode})。再試行します。";
            }

            return string.IsNullOrWhiteSpace(error)
                ? "Firebaseへ接続できません。再試行します。"
                : $"Firebase通信失敗: {error}";
        }

        private bool IsGroupIssuePending(int generation, int resultSerial)
        {
            CraftLiveRoomState current = session != null ? session.State : null;
            return current != null &&
                current.groupGeneration == generation &&
                current.sessionPhase == CraftLiveSessionPhase.Finished &&
                current.selectedFinalResultSerial == resultSerial &&
                string.IsNullOrWhiteSpace(current.finalWeaponCode);
        }

        public static string CreateGroupCandidate(
            string roomId,
            int generation,
            int resultSerial,
            int offset)
        {
            unchecked
            {
                uint hash = 2166136261;
                string source = $"{roomId}|{generation}|{resultSerial}";
                foreach (char value in source)
                {
                    hash ^= value;
                    hash *= 16777619;
                }
                int normalizedOffset = Mathf.Max(0, offset);
                if (normalizedOffset < TwoDigitGroupNumberCount)
                {
                    int number = (int)(
                        (hash + (uint)normalizedOffset) %
                        TwoDigitGroupNumberCount) + 1;
                    return number.ToString("00");
                }

                int threeDigitOffset =
                    (normalizedOffset - TwoDigitGroupNumberCount) %
                    ThreeDigitGroupNumberCount;
                int threeDigitNumber = (int)(
                    (hash + (uint)threeDigitOffset) %
                    ThreeDigitGroupNumberCount) + 100;
                return threeDigitNumber.ToString("000");
            }
        }

        public static bool IsProductionGroupNumber(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (!int.TryParse(normalized, out int numeric))
            {
                return false;
            }

            return (normalized.Length == 2 &&
                    numeric >= 1 && numeric <= 99) ||
                   (normalized.Length == 3 &&
                    numeric >= 100 && numeric <= 999);
        }

        public static HashSet<string> ParseOccupiedGroupNumbers(string json)
        {
            HashSet<string> numbers = new HashSet<string>();
            if (string.IsNullOrWhiteSpace(json) ||
                string.Equals(json.Trim(), "null",
                    StringComparison.OrdinalIgnoreCase))
            {
                return numbers;
            }

            foreach (Match match in Regex.Matches(
                         json,
                         "\\\"(?<number>[0-9]{2,5})\\\"\\s*:"))
            {
                string number = match.Groups["number"].Value;
                if (IsProductionGroupNumber(number) ||
                    IsFiveDigitGroupNumber(number))
                {
                    numbers.Add(number);
                }
            }

            return numbers;
        }

        public static bool IsFiveDigitGroupNumber(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            return normalized.Length == 5 &&
                   int.TryParse(normalized, out int numeric) &&
                   numeric >= 10000 && numeric <= 99999;
        }

        public static bool IsWeaponGroupSlotAvailable(
            CraftLiveWeaponGroupRecord existing,
            string roomId,
            int generation,
            int resultSerial,
            long nowUnixMs)
        {
            if (existing == null)
                return true;

            if (existing.sourceRoomId == (roomId ?? string.Empty) &&
                existing.sourceGroupGeneration == generation &&
                existing.sourceResultSerial == resultSerial)
            {
                return true;
            }

            // A missing expiry belongs to an unknown/legacy record. Treat it as
            // occupied instead of overwriting data that RealRPG still considers
            // valid. Only an explicit, elapsed expiry makes a number reusable.
            return existing.expiresAtUnixMs > 0 &&
                   existing.expiresAtUnixMs <= nowUnixMs;
        }

        private CraftLiveWeaponGroupRecord BuildWeaponGroupRecord(
            string number,
            int generation,
            int resultSerial,
            CraftLiveResultState result)
        {
            long now = CraftLiveSession.UnixNowMs();
            return new CraftLiveWeaponGroupRecord
            {
                groupNumber = number,
                status = "ready",
                createdAtUnixMs = now,
                expiresAtUnixMs = now + 30L * 60L * 1000L,
                sourceRoomId = session.RoomId,
                sourceGroupGeneration = generation,
                sourceResultSerial = resultSerial,
                weapon = BuildSharedWeapon(result),
                craftResult = result.Clone()
            };
        }

        private static CraftLiveSharedWeaponDto BuildSharedWeapon(
            CraftLiveResultState result)
        {
            string attribute = MapAttribute(
                result.attributeId,
                result.elementEffect.type);
            return new CraftLiveSharedWeaponDto
            {
                weaponId = result.weaponId,
                teamName = "文化祭パーティ",
                weaponName = result.weaponName,
                weaponImageId = result.weaponId,
                attack = Mathf.Max(1, Mathf.RoundToInt(result.stats.attackRate)),
                defense = Mathf.Max(0, Mathf.RoundToInt(result.stats.defenseRate)),
                evasion = Mathf.Clamp01(result.stats.evasionRate / 100f),
                attribute = attribute,
                skillAId = "common_a",
                skillBId = attribute == "Thunder" ? "starfall" : "festival_flare",
                ultimateSkillId = "ultimate_festival",
                motionType = MapMotion(result.weaponType)
            };
        }

        public static string MapAttribute(
            string attributeId,
            CraftLiveElementType elementType)
        {
            string value = (attributeId ?? string.Empty).Trim().ToLowerInvariant();
            if (value == "fire") return "Fire";
            if (value == "freeze" || value == "ice" || value == "water") return "Ice";
            if (value == "lightning" || value == "thunder") return "Thunder";
            switch (elementType)
            {
                case CraftLiveElementType.Fire: return "Fire";
                case CraftLiveElementType.Freeze: return "Ice";
                case CraftLiveElementType.Lightning: return "Thunder";
                default: return "None";
            }
        }

        private static string MapMotion(CraftLiveWeaponType type)
        {
            switch (type)
            {
                case CraftLiveWeaponType.Thrust: return "Thrust";
                case CraftLiveWeaponType.Staff: return "StaffSwing";
                default: return "SwordSlash";
            }
        }

        private void StartPublisherIfReady()
        {
            if (!IsRemoteMode ||
                !initialSyncComplete ||
                pendingPublish == null ||
                publishingCoroutine != null)
            {
                return;
            }

            publishingCoroutine =
                StartCoroutine(PublishPendingRemoteState());
        }

        private IEnumerator PublishPendingRemoteState()
        {
            int retryAttempt = 0;
            while (enabled &&
                   initialSyncComplete &&
                   pendingPublish != null)
            {
                CraftLiveRoomState snapshot = pendingPublish.Clone();
                byte[] body = Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(snapshot));

                using (UnityWebRequest request =
                       new UnityWebRequest(BuildRoomUrl(), "PUT"))
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader(
                        "Content-Type",
                        "application/json");
                    request.SetRequestHeader("X-Firebase-ETag", "true");
                    if (!string.IsNullOrWhiteSpace(remoteEtag))
                    {
                        request.SetRequestHeader("If-Match", remoteEtag);
                    }

                    request.timeout = Mathf.CeilToInt(requestTimeoutSeconds);
                    yield return request.SendWebRequest();

                    if (request.result ==
                        UnityWebRequest.Result.Success)
                    {
                        retryAttempt = 0;
                        MarkRequestSuccessful();
                        UpdateRemoteEtag(request);
                        SaveCachedState(ConfirmedCacheKey, snapshot);
                        if (CompareVersion(pendingPublish, snapshot) <= 0)
                        {
                            pendingPublish = null;
                            DeleteCachedState(PendingCacheKey);
                        }

                        SetConnectionState(
                            CraftLiveConnectionState.Online,
                            $"Firebase room {session.RoomId}");
                        continue;
                    }

                    if (request.responseCode == 412)
                    {
                        HandlePublishConflict(request, snapshot);
                    }
                    else
                    {
                        RecordFailure(
                            $"Firebase送信失敗: {request.error}");
                    }
                }

                if (pendingPublish != null)
                {
                    float retryDelay = CalculateRetryDelay(
                        retryAttempt++,
                        initialRetryDelaySeconds,
                        maximumRetryDelaySeconds);
                    yield return new WaitForSecondsRealtime(retryDelay);
                }
            }

            publishingCoroutine = null;
        }

        private void HandlePublishConflict(
            UnityWebRequest request,
            CraftLiveRoomState attempted)
        {
            UpdateRemoteEtag(request);
            SetConnectionState(
                CraftLiveConnectionState.Conflict,
                "別のPadの更新を検出し、状態を再同期しています。");

            string json = request.downloadHandler != null
                ? request.downloadHandler.text
                : string.Empty;
            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return;
            }

            try
            {
                CraftLiveRoomState remote =
                    CraftLiveRoomState.FromJson(json);
                SaveCachedState(ConfirmedCacheKey, remote);
                if (CompareVersion(remote, attempted) >= 0)
                {
                    CraftLiveRoomState localCandidate =
                        pendingPublish ?? attempted;
                    if (TryBuildTransferReconciledState(
                            remote,
                            localCandidate,
                            out CraftLiveRoomState reconciled))
                    {
                        session.ApplyRemoteState(reconciled);
                        pendingPublish = reconciled.Clone();
                        SaveCachedState(
                            PendingCacheKey,
                            pendingPublish);
                    }
                    else
                    {
                        session.ApplyRemoteState(remote);
                        if (pendingPublish == null ||
                            CompareVersion(
                                pendingPublish,
                                remote) <= 0)
                        {
                            pendingPublish = null;
                            DeleteCachedState(PendingCacheKey);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                RecordFailure(
                    $"競合データ解析失敗: {exception.Message}");
            }
        }

        private void MarkRequestSuccessful()
        {
            consecutiveFailures = 0;
            lastSuccessfulRequestUnixMs = CraftLiveSession.UnixNowMs();
        }

        private void RecordFailure(string message)
        {
            consecutiveFailures++;
            SetConnectionState(
                consecutiveFailures >= 3
                    ? CraftLiveConnectionState.Offline
                    : CraftLiveConnectionState.Degraded,
                message);
        }

        private void SetConnectionState(
            CraftLiveConnectionState nextState,
            string message)
        {
            message = message ?? string.Empty;
            bool stateChanged = connectionState != nextState;
            bool messageChanged = connectionMessage != message;
            connectionState = nextState;
            connectionMessage = message;
            if (!stateChanged && !messageChanged)
            {
                return;
            }

            onConnectionStatusChanged?.Invoke(message);
            if (stateChanged)
            {
                onOnlineChanged?.Invoke(IsOnline);
            }

            ConnectionChanged?.Invoke(nextState, message);
        }

        private void LoadCachedStates()
        {
            if (!cachePendingState)
            {
                return;
            }

            CraftLiveRoomState confirmed =
                LoadCachedState(ConfirmedCacheKey);
            if (confirmed != null &&
                IsRemoteNewer(confirmed, session.State))
            {
                session.ApplyRemoteState(confirmed);
            }

            CraftLiveRoomState pending =
                LoadCachedState(PendingCacheKey);
            if (pending != null &&
                IsRemoteNewer(pending, session.State))
            {
                pendingPublish = pending;
                session.ApplyRemoteState(pending.Clone());
            }
        }

        private void SaveCachedState(
            string key,
            CraftLiveRoomState value)
        {
            if (!cachePendingState || value == null)
            {
                return;
            }

            PlayerPrefs.SetString(key, JsonUtility.ToJson(value));
            PlayerPrefs.Save();
        }

        private CraftLiveRoomState LoadCachedState(string key)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                return null;
            }

            try
            {
                return CraftLiveRoomState.FromJson(
                    PlayerPrefs.GetString(key));
            }
            catch (Exception)
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                return null;
            }
        }

        private void DeleteCachedState(string key)
        {
            if (!cachePendingState || !PlayerPrefs.HasKey(key))
            {
                return;
            }

            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private string PendingCacheKey =>
            BuildCacheKey("pending");
        private string ConfirmedCacheKey =>
            BuildCacheKey("confirmed");

        private string BuildCacheKey(string suffix)
        {
            string source =
                $"{firebaseDatabaseUrl}|{session.RoomId}|{suffix}";
            uint hash = 2166136261;
            foreach (char character in source)
            {
                hash ^= character;
                hash *= 16777619;
            }

            // V2 intentionally leaves pre-event browser caches behind. Old
            // room snapshots must not be restored after the production reset.
            return $"CraftLive.Room.V2.{hash:X8}.{suffix}";
        }

        private string BuildRoomUrl()
        {
            string baseUrl = firebaseDatabaseUrl.Trim().TrimEnd('/');
            return $"{baseUrl}/rooms/" +
                   $"{UnityWebRequest.EscapeURL(session.RoomId)}.json";
        }

        private string BuildWeaponGroupUrl(string groupNumber)
        {
            string baseUrl = firebaseDatabaseUrl.Trim().TrimEnd('/');
            return $"{baseUrl}/weaponGroups/" +
                   $"{UnityWebRequest.EscapeURL(groupNumber)}.json";
        }

        private string BuildWeaponGroupsRootUrl()
        {
            string baseUrl = firebaseDatabaseUrl.Trim().TrimEnd('/');
            return $"{baseUrl}/weaponGroups.json";
        }

        private string BuildPresenceRoomUrl()
        {
            string baseUrl = firebaseDatabaseUrl.Trim().TrimEnd('/');
            return $"{baseUrl}/presence/" +
                   $"{UnityWebRequest.EscapeURL(session.RoomId)}.json";
        }

        private string BuildPresenceRoleUrl(CraftLiveRole targetRole)
        {
            string baseUrl = firebaseDatabaseUrl.Trim().TrimEnd('/');
            return $"{baseUrl}/presence/" +
                   $"{UnityWebRequest.EscapeURL(session.RoomId)}/" +
                   $"{PresenceKey(targetRole)}.json";
        }

        private static string PresenceKey(CraftLiveRole targetRole)
        {
            switch (targetRole)
            {
                case CraftLiveRole.MaterialPad:
                    return "pad1";
                case CraftLiveRole.WorkbenchPad:
                    return "pad2";
                case CraftLiveRole.QrPad:
                    return "pad3";
                case CraftLiveRole.HologramPad:
                    return "pad4";
                default:
                    return "unknown";
            }
        }

        private void UpdateRemoteEtag(UnityWebRequest request)
        {
            string etag = request.GetResponseHeader("ETag");
            if (!string.IsNullOrWhiteSpace(etag))
            {
                remoteEtag = etag;
            }
        }

        public static int CompareVersion(
            CraftLiveRoomState left,
            CraftLiveRoomState right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            // A group reset is a hard lifecycle boundary. Its generation must
            // outrank revision/timestamp so a delayed write from the previous
            // group cannot restore old transfer IDs or visuals.
            int generationComparison =
                left.groupGeneration.CompareTo(right.groupGeneration);
            if (generationComparison != 0)
            {
                return generationComparison;
            }

            int revisionComparison =
                left.revision.CompareTo(right.revision);
            return revisionComparison != 0
                ? revisionComparison
                : left.updatedAtUnixMs.CompareTo(
                    right.updatedAtUnixMs);
        }

        public static bool TryBuildTransferReconciledState(
            CraftLiveRoomState remote,
            CraftLiveRoomState local,
            out CraftLiveRoomState reconciled)
        {
            reconciled = remote != null ? remote.Clone() : null;
            if (reconciled == null || local == null ||
                reconciled.groupGeneration != local.groupGeneration ||
                !reconciled.MergeTransferReliabilityFrom(local))
            {
                return false;
            }

            reconciled.revision = Math.Max(
                remote.revision,
                local.revision) + 1;
            reconciled.updatedAtUnixMs = CraftLiveSession.UnixNowMs();
            return true;
        }

        public static bool IsRemoteNewer(
            CraftLiveRoomState remote,
            CraftLiveRoomState current)
        {
            return CompareVersion(remote, current) > 0;
        }

        public static float CalculateRetryDelay(
            int retryAttempt,
            float initialDelay,
            float maximumDelay)
        {
            float minimum = Mathf.Max(0.25f, initialDelay);
            float maximum = Mathf.Max(minimum, maximumDelay);
            int exponent = Mathf.Clamp(retryAttempt, 0, 16);
            return Mathf.Min(maximum, minimum * Mathf.Pow(2f, exponent));
        }
    }
}
