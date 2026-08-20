using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad1TransferController :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        [Header("References")]
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad1Bindings bindings;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private GameObject fallbackTicketPrefab;
        [SerializeField]
        [Tooltip("任意のばねモデルです。未設定時は簡易形状を生成します。")]
        private GameObject springPrefab;

        [Header("Physical Launcher")]
        [SerializeField] private Transform sceneSpring;
        [SerializeField] private Transform pusherPlate;
        [SerializeField] private Transform launcherRamp;
        [SerializeField]
        [Tooltip("斜面入口を手動調整する場合に設定します。未設定時はHassyaから自動算出します。")]
        private Transform rampEntryAnchor;
        [SerializeField]
        [Tooltip("斜面の曲がり方を手動調整する場合に設定します。")]
        private Transform rampMiddleAnchor;
        [SerializeField]
        [Tooltip("斜面出口を手動調整する場合に設定します。")]
        private Transform rampExitAnchor;
        [SerializeField, Min(0.05f)] private float frameSeatDistance = 0.32f;
        [SerializeField, Min(0.1f)] private float boxExitDistance = 1.05f;
        [SerializeField, Min(0.1f)] private float rampLaunchDistance = 1.15f;
        [SerializeField, Min(0f)] private float rampLaunchRise = 0.58f;
        [SerializeField, Min(0.02f)] private float springPullWorldDistance = 0.28f;
        [SerializeField, Min(0f)]
        [Tooltip("プレートと額縁、および額縁同士の間隔です。0なら隙間なく接触します。")]
        private float physicalFrameGap;
        [SerializeField, Min(0.01f)]
        [Tooltip("Front-to-back spacing between connected frames in a batch launch.")]
        private float batchTrainSpacing = 0.24f;

        [Header("Fallback Presentation")]
        [SerializeField] private bool createFallbackVisuals = true;
        [SerializeField, Min(1)] private int queueColumns = 3;
        [SerializeField, Min(0.1f)] private float queueSpacing = 0.48f;
        [SerializeField, Min(0.1f)] private float queueTicketSize = 0.42f;
        [SerializeField]
        [Tooltip("くぼみに置く額縁のワールド幅・高さです。物理発射台使用時はこちらを優先します。")]
        private Vector2 grooveFrameSize = new Vector2(0.22f, 0.28f);
        [SerializeField, Tooltip(
            "When disabled, TransferQueueRoot and SpringLauncherRoot keep their hierarchy-authored transforms.")]
        private bool positionRootsFromCamera = true;
        [SerializeField]
        [Tooltip("x/yは画面内の位置、zはカメラからの距離です。")]
        private Vector3 transferQueueViewportPosition =
            new Vector3(0.5f, 0.38f, 1.55f);
        [SerializeField]
        [Tooltip("x/yは画面内の位置、zはカメラからの距離です。")]
        private Vector3 springLauncherViewportPosition =
            new Vector3(0.5f, 0.34f, 1.5f);
        [SerializeField] private Vector3 springPrefabLocalPosition =
            new Vector3(0f, -0.34f, 0f);
        [SerializeField] private Vector3 springPrefabScaleMultiplier =
            Vector3.one;

        [Header("Spring Input")]
        [SerializeField, Min(30f)] private float requiredPullPixels = 110f;
        [SerializeField] private bool launchAllByDefault = true;
        [SerializeField, Tooltip(
            "Always transfers every queued material when using the scene's physical launcher.")]
        private bool forceLaunchAllWithPhysicalLauncher = true;
        [SerializeField] private Vector3 pulledArmEuler =
            new Vector3(-38f, 0f, 0f);
        [SerializeField] private Vector3 compressedSpringScale =
            new Vector3(1f, 0.42f, 1f);

        [Header("Animation")]
        [SerializeField, Min(0.05f)] private float loadDuration = 0.28f;
        [SerializeField, Min(0.05f)] private float queueArrivalDuration = 0.4f;
        [SerializeField, Min(0f)] private float queueArrivalArcHeight = 0.22f;
        [SerializeField, Range(0.1f, 0.7f)]
        private float modelMergeApproachRatio = 0.35f;
        [SerializeField, Min(0.1f)]
        [Tooltip("モデルが絵へ吸収される速さの倍率です。1が標準、2で約2倍速です。")]
        private float modelAbsorptionSpeed = 1f;
        [SerializeField, Min(0.03f)] private float impactDuration = 0.12f;
        [SerializeField, Min(0.05f)] private float grooveLaunchDuration = 0.24f;
        [SerializeField, Min(0.05f)] private float launchDuration = 0.55f;
        [SerializeField, Min(0.1f)]
        [Tooltip("物理発射時の速度（ワールド単位/秒）です。直線と斜面で同じ速度を維持します。")]
        private float physicalLaunchSpeed = 2.8f;
        [SerializeField, Min(0f)] private float launchArcHeight = 1.1f;
        [SerializeField, Min(0f)]
        [Tooltip("Inspectorで設定する、発射方向へ傾く時間と元へ戻る時間（秒）です。0なら即時に切り替えます。")]
        private float cameraShiftDuration = 0.18f;
        [SerializeField, HideInInspector, Min(1f)]
        [Tooltip("旧設定です。カメラの旋回時間にはCamera Shift Durationを使用します。")]
        private float cameraRotationSpeed = 30f;
        [SerializeField, Range(-90f, 90f)]
        [Tooltip("Inspectorで設定する発射時のカメラ角度です。正の値で右へ向きます。")]
        private float cameraYawDegrees;
        [SerializeField, Tooltip(
            "When disabled, launching never changes the hierarchy-authored camera transform.")]
        private bool animateCameraDuringLaunch;
        [SerializeField, Min(0f)]
        [Tooltip("斜面を離れた後の額縁に加える落下加速度です。")]
        private float postRampGravity = 0.8f;

        [Header("Standalone Play Test")]
        [SerializeField]
        [Tooltip("Pad2受信機がないテストシーンでは、発射後に再び転送できる状態へ戻します。")]
        private bool resetAfterStandaloneLaunch = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onQueueCountChanged;
        [SerializeField] private UnityEvent<bool> onLaunchAllModeChanged;
        [SerializeField] private UnityEvent<float> onPullChanged;
        [SerializeField] private UnityEvent onLoadingStarted;
        [SerializeField] private UnityEvent onLaunched;

        private readonly Dictionary<int, GameObject> queueVisuals =
            new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Coroutine> queueArrivalRoutines =
            new Dictionary<int, Coroutine>();
        private readonly HashSet<int> prelaunchedTransferSerials =
            new HashSet<int>();
        private readonly List<GameObject> activeBatchTickets =
            new List<GameObject>();
        private Transform launcherSeat;
        private Transform launcherArm;
        private Transform springVisual;
        private Transform launchExit;
        private Transform rampPathMiddle;
        private Transform rampLaunchEnd;
        private GameObject generatedRoot;
        private GameObject activeTicket;
        private TextMesh queueText;
        private TextMesh modeText;
        private Quaternion armRestRotation;
        private Vector3 springRestScale;
        private Vector3 springRestPosition;
        private Vector3 pusherRestPosition;
        private Vector3 physicalLaunchDirection;
        private bool usingPhysicalLauncher;
        private bool launchAll;
        private bool pulling;
        private float pullStartX;
        private float pullAmount;
        private int handledTransferSerial = -1;
        private Coroutine launchRoutine;
        private Coroutine cameraTurnRoutine;
        private bool cameraTurning;
        private Vector3 cameraRestPosition;
        private Quaternion cameraRestRotation;
        private bool cameraRestCaptured;
        private string pendingMergeMaterialId = string.Empty;
        private GameObject pendingMergeModel;

        public bool LaunchAll => launchAll;
        public float PullAmount => pullAmount;

        public void CaptureTransferMergeSource(
            CraftLiveMaterialDefinition material,
            GameObject previewModel)
        {
            DestroySafely(pendingMergeModel);
            pendingMergeModel = null;
            pendingMergeMaterialId = material != null
                ? material.MaterialId
                : string.Empty;
            if (material == null || previewModel == null)
            {
                return;
            }

            pendingMergeModel = Instantiate(
                previewModel,
                previewModel.transform.position,
                previewModel.transform.rotation);
            pendingMergeModel.name =
                $"TransferModel_{material.MaterialId}";
            pendingMergeModel.transform.localScale =
                previewModel.transform.lossyScale;
            foreach (Collider targetCollider in
                     pendingMergeModel.GetComponentsInChildren<Collider>(true))
            {
                targetCollider.enabled = false;
            }
            foreach (MonoBehaviour behaviour in
                     pendingMergeModel.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }
        }

        private void Awake()
        {
            ResolveReferences();
            ResolvePhysicalLauncherReferences();
            launchAll = ShouldForceLaunchAll() || launchAllByDefault;
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
        }

        private void Start()
        {
            ResolvePhysicalLauncherReferences();
            BuildFallbackPresentation();
            PositionTransferRoots();
            Refresh(session != null ? session.State : null);
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }

            if (launchRoutine != null)
            {
                StopCoroutine(launchRoutine);
                launchRoutine = null;
            }
            if (cameraTurnRoutine != null)
            {
                StopCoroutine(cameraTurnRoutine);
                cameraTurnRoutine = null;
            }
            cameraTurning = false;

            foreach (Coroutine routine in queueArrivalRoutines.Values)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }
            queueArrivalRoutines.Clear();
            prelaunchedTransferSerials.Clear();

            foreach (GameObject ticket in activeBatchTickets)
            {
                if (ticket != activeTicket)
                {
                    DestroySafely(ticket);
                }
            }
            activeBatchTickets.Clear();

            DestroySafely(activeTicket);
            activeTicket = null;
            DestroySafely(pendingMergeModel);
            pendingMergeModel = null;
            pendingMergeMaterialId = string.Empty;
            RestoreMechanism();
            RestoreCamera();
        }

        public void Configure(CraftLivePad1Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
            PositionTransferRoots();
        }

        public void SetSingleMode()
        {
            if (ShouldForceLaunchAll())
            {
                launchAll = true;
                PublishMode();
                return;
            }

            launchAll = false;
            PublishMode();
        }

        public void SetAllMode()
        {
            launchAll = true;
            PublishMode();
        }

        public void ToggleLaunchMode()
        {
            if (ShouldForceLaunchAll())
            {
                launchAll = true;
                PublishMode();
                return;
            }

            launchAll = !launchAll;
            PublishMode();
        }

        public void LaunchSelectedMode()
        {
            TryLaunchSelectedMode();
        }

        private bool TryLaunchSelectedMode()
        {
            if (!CanOperateSpring(session != null ? session.State : null))
            {
                return false;
            }

            if (ShouldForceLaunchAll() || launchAll)
            {
                return session.BeginAllQueuedTransfers();
            }

            return session.BeginSingleTransfer();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanOperateSpring(session != null ? session.State : null))
            {
                return;
            }

            pulling = true;
            pullStartX = eventData.position.x;
            SetPull(0f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!pulling)
            {
                return;
            }

            float distance = Mathf.Max(
                0f,
                pullStartX - eventData.position.x);
            SetPull(distance / requiredPullPixels);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!pulling)
            {
                return;
            }

            pulling = false;
            bool shouldLaunch = pullAmount >= 0.98f;
            if (!shouldLaunch || !TryLaunchSelectedMode())
            {
                SetPull(0f);
            }
        }

        public static bool CanPull(CraftLiveRoomState state)
        {
            return state != null &&
                   state.placement.status ==
                       CraftLivePlacementStatus.Idle &&
                   state.transferQueue != null &&
                   state.transferQueue.Count > 0;
        }

        private bool CanOperateSpring(CraftLiveRoomState state)
        {
            return CanPull(state) && queueArrivalRoutines.Count == 0;
        }

        public void SetStandaloneResetEnabled(bool value)
        {
            resetAfterStandaloneLaunch = value;
        }

#if UNITY_EDITOR
        [ContextMenu("Debug/Queue First Available Material")]
        private void DebugQueueFirstAvailableMaterial()
        {
            ResolveReferences();
            if (!Application.isPlaying ||
                session == null ||
                session.Catalog == null ||
                session.State.placement.status !=
                    CraftLivePlacementStatus.Idle)
            {
                Debug.LogWarning(
                    "Craft-live: Play Modeの待機中に実行してください。",
                    this);
                return;
            }

            CraftLiveSlotId[] slots =
            {
                CraftLiveSlotId.Top,
                CraftLiveSlotId.Left,
                CraftLiveSlotId.Right,
                CraftLiveSlotId.Bottom,
                CraftLiveSlotId.Skill,
                CraftLiveSlotId.Attribute
            };
            foreach (CraftLiveMaterialDefinition material in
                     session.Catalog.Materials)
            {
                if (material == null)
                {
                    continue;
                }

                foreach (CraftLiveSlotId slot in slots)
                {
                    if (!material.CanUseIn(slot) ||
                        !session.State.CanReserveSlot(slot))
                    {
                        continue;
                    }

                    if (!session.IsMaterialUnlocked(material))
                    {
                        session.UnlockMaterialId(
                            material.MaterialId);
                    }

                    session.SelectMaterial(material);
                    session.ChoosePlacementSlot(slot);
                    session.ConfirmPlacement();
                    return;
                }
            }

            Debug.LogWarning(
                "Craft-live: 空いている配置枠がありません。",
                this);
        }
#endif

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (bindings == null)
            {
                bindings = GetComponentInParent<CraftLivePad1Bindings>();
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

        }

        private void ResolvePhysicalLauncherReferences()
        {
            if (sceneSpring == null)
            {
                GameObject found = GameObject.Find("Bane");
                sceneSpring = found != null ? found.transform : null;
            }

            if (pusherPlate == null)
            {
                GameObject found = GameObject.Find("Plate");
                pusherPlate = found != null ? found.transform : null;
            }

            if (launcherRamp == null)
            {
                GameObject found = GameObject.Find("Hassya");
                launcherRamp = found != null ? found.transform : null;
            }

            usingPhysicalLauncher =
                sceneSpring != null &&
                pusherPlate != null &&
                launcherRamp != null;
            physicalLaunchDirection = targetCamera != null
                ? targetCamera.transform.right.normalized
                : Vector3.right;
        }

        private void PositionTransferRoots()
        {
            if (bindings == null)
            {
                return;
            }

            if (!positionRootsFromCamera)
            {
                return;
            }

            if (usingPhysicalLauncher)
            {
                if (launcherSeat != null &&
                    bindings.TransferQueueRoot != null)
                {
                    Quaternion rotation = targetCamera != null
                        ? targetCamera.transform.rotation
                        : Quaternion.identity;
                    bindings.TransferQueueRoot.SetPositionAndRotation(
                        launcherSeat.position,
                        rotation);
                }
                return;
            }

            if (!positionRootsFromCamera || targetCamera == null)
            {
                return;
            }

            PositionRootFromViewport(
                bindings.TransferQueueRoot,
                transferQueueViewportPosition);
            PositionRootFromViewport(
                bindings.SpringLauncherRoot,
                springLauncherViewportPosition);
        }

        private void PositionRootFromViewport(
            Transform target,
            Vector3 viewportPosition)
        {
            if (target == null)
            {
                return;
            }

            float depth = Mathf.Max(
                targetCamera.nearClipPlane + 0.25f,
                viewportPosition.z);
            Vector3 position = targetCamera.ViewportToWorldPoint(
                new Vector3(
                    Mathf.Clamp01(viewportPosition.x),
                    Mathf.Clamp01(viewportPosition.y),
                    depth));
            target.SetPositionAndRotation(
                position,
                targetCamera.transform.rotation);
        }

        private void Subscribe()
        {
            if (session == null)
            {
                return;
            }

            session.StateChanged -= Refresh;
            session.StateChanged += Refresh;
            Refresh(session.State);
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            if (state.placement.status == CraftLivePlacementStatus.Idle &&
                launchRoutine == null)
            {
                PositionTransferRoots();
            }

            bool shouldStartLaunch =
                state.placement.status ==
                    CraftLivePlacementStatus.Pad1Loading &&
                state.placement.transferSerial !=
                    handledTransferSerial &&
                launchRoutine == null;
            if (shouldStartLaunch)
            {
                ClaimQueuedTicket(state.placement.transferSerial);
            }

            RefreshQueueVisuals(state);
            int queueCount = state.transferQueue != null
                ? state.transferQueue.Count
                : 0;
            if (queueText != null)
            {
                queueText.text = $"転送待ち {queueCount}";
            }

            onQueueCountChanged?.Invoke(queueCount);
            if (shouldStartLaunch)
            {
                handledTransferSerial =
                    state.placement.transferSerial;
                bool alreadyLaunched =
                    prelaunchedTransferSerials.Remove(
                        state.placement.transferSerial);
                launchRoutine = StartCoroutine(
                    alreadyLaunched
                        ? CompletePrelaunchedTransfer()
                        : Launch(state.Clone()));
            }
        }

        private IEnumerator CompletePrelaunchedTransfer()
        {
            session.MarkTransferLaunching();
            yield return null;
            session.MarkTransferArriving();
            if (resetAfterStandaloneLaunch &&
                FindAnyObjectByType<CraftLivePad2TransferReceiver>() == null)
            {
                session.CompleteTransferPreviewWithoutPlacement();
            }

            FinishLaunchRoutine();
        }

        private IEnumerator Launch(CraftLiveRoomState snapshot)
        {
            if (usingPhysicalLauncher &&
                snapshot != null &&
                snapshot.transferBatchRemaining > 0)
            {
                yield return LaunchPhysicalBatch(snapshot);
                yield break;
            }

            CraftLiveMaterialDefinition material =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(
                        snapshot.placement.materialId)
                    : null;
            if (activeTicket == null)
            {
                Vector3 createPosition = bindings != null &&
                                         bindings.TransferQueueRoot != null
                    ? bindings.TransferQueueRoot.position
                    : transform.position;
                activeTicket = CreateTicket(material, createPosition);
            }

            Vector3 start = activeTicket.transform.position;
            Vector3 seat = usingPhysicalLauncher
                ? start
                : launcherSeat != null
                    ? launcherSeat.position
                    : transform.position;
            Vector3 end = launchExit != null
                ? launchExit.position +
                  (usingPhysicalLauncher
                      ? Vector3.zero
                      : ResolveSlotExitOffset(
                          snapshot.placement.confirmedSlot))
                : seat + Vector3.right * 3f;

            onLoadingStarted?.Invoke();
            if (Vector3.Distance(start, seat) > 0.01f)
            {
                yield return AnimateLoad(
                    activeTicket.transform,
                    start,
                    seat);
            }
            else
            {
                activeTicket.transform.position = seat;
            }

            session.MarkTransferLaunching();
            if (usingPhysicalLauncher)
            {
                yield return AnimatePhysicalImpact();
                PlayLaunchSound();
                onLaunched?.Invoke();
                yield return AnimateGrooveLaunch(
                    activeTicket.transform,
                    seat,
                    end);
                cameraTurnRoutine = StartCoroutine(
                    AnimateCamera(true));
                Vector3 rampEnd = rampLaunchEnd != null
                    ? rampLaunchEnd.position
                    : end +
                      physicalLaunchDirection * rampLaunchDistance;
                yield return AnimateRampLaunch(
                    activeTicket.transform,
                    end,
                    rampEnd);
                if (cameraTurning)
                {
                    Vector3 rampControl = rampPathMiddle != null
                        ? rampPathMiddle.position
                        : Vector3.Lerp(end, rampEnd, 0.55f);
                    Vector3 exitDirection =
                        (rampEnd - rampControl).normalized;
                    yield return AnimatePostRampFlight(
                        activeTicket.transform,
                        exitDirection);
                }
                if (cameraTurnRoutine != null)
                {
                    yield return cameraTurnRoutine;
                    cameraTurnRoutine = null;
                }
            }
            else
            {
                PlayLaunchSound();
                onLaunched?.Invoke();
                yield return AnimateCamera(true);
                yield return AnimateLaunch(
                    activeTicket.transform,
                    seat,
                    end);
            }

            DestroySafely(activeTicket);
            activeTicket = null;
            RestoreMechanism();
            session.MarkTransferArriving();
            yield return AnimateCamera(false);
            if (resetAfterStandaloneLaunch &&
                FindAnyObjectByType<CraftLivePad2TransferReceiver>() == null)
            {
                session.CompleteTransferPreviewWithoutPlacement();
            }

            FinishLaunchRoutine();
        }

        private IEnumerator LaunchPhysicalBatch(
            CraftLiveRoomState snapshot)
        {
            CraftLiveMaterialDefinition firstMaterial =
                session.Catalog != null
                    ? session.Catalog.FindMaterial(
                        snapshot.placement.materialId)
                    : null;
            if (activeTicket == null)
            {
                Vector3 createPosition = bindings != null &&
                                         bindings.TransferQueueRoot != null
                    ? bindings.TransferQueueRoot.position
                    : transform.position;
                activeTicket = CreateTicket(
                    firstMaterial,
                    createPosition);
            }

            List<GameObject> batchObjects =
                CollectPhysicalBatchTickets(snapshot);
            activeBatchTickets.Clear();
            activeBatchTickets.AddRange(batchObjects);
            int count = batchObjects.Count;
            Transform[] tickets = new Transform[count];
            Vector3[] starts = new Vector3[count];
            Vector3[] seats = new Vector3[count];
            Vector3[] grooveEnds = new Vector3[count];
            Vector3[] rampControls = new Vector3[count];
            Vector3[] rampEnds = new Vector3[count];
            Vector3 railUp = ResolveCameraUp().normalized;
            Vector3 baseSeat = launcherSeat != null
                ? launcherSeat.position
                : transform.position;
            Vector3 baseGrooveEnd = launchExit != null
                ? launchExit.position
                : baseSeat + physicalLaunchDirection * 3f;
            baseGrooveEnd += railUp * (
                Vector3.Dot(baseSeat, railUp) -
                Vector3.Dot(baseGrooveEnd, railUp));
            Vector3 baseRampControl = rampPathMiddle != null
                ? rampPathMiddle.position
                : Vector3.Lerp(
                    baseGrooveEnd,
                    rampLaunchEnd != null
                        ? rampLaunchEnd.position
                        : baseGrooveEnd,
                    0.55f);
            Vector3 baseRampEnd = rampLaunchEnd != null
                ? rampLaunchEnd.position
                : baseGrooveEnd +
                  physicalLaunchDirection * rampLaunchDistance;
            for (int index = 0; index < count; index++)
            {
                tickets[index] = batchObjects[index].transform;
                starts[index] = tickets[index].position;
                Vector3 trainOffset =
                    -physicalLaunchDirection *
                    (index * batchTrainSpacing);
                seats[index] = baseSeat + trainOffset;
                grooveEnds[index] = baseGrooveEnd + trainOffset;
                rampControls[index] = baseRampControl + trainOffset;
                rampEnds[index] = baseRampEnd + trainOffset;
            }

            onLoadingStarted?.Invoke();
            yield return AnimateBatchLoad(tickets, starts, seats);
            session.MarkTransferLaunching();
            yield return AnimatePhysicalImpact();
            PlayLaunchSound();
            onLaunched?.Invoke();
            yield return AnimateBatchLinear(
                tickets,
                seats,
                grooveEnds);
            cameraTurnRoutine = StartCoroutine(
                AnimateCamera(true));
            yield return AnimateBatchRamp(
                tickets,
                grooveEnds,
                rampControls,
                rampEnds);
            if (cameraTurning)
            {
                Vector3[] exitDirections =
                    new Vector3[tickets.Length];
                for (int index = 0; index < tickets.Length; index++)
                {
                    Vector3 direction =
                        rampEnds[index] - rampControls[index];
                    exitDirections[index] =
                        direction.sqrMagnitude > 0.0001f
                            ? direction.normalized
                            : physicalLaunchDirection;
                }

                yield return AnimateBatchPostRampFlight(
                    tickets,
                    exitDirections);
            }
            if (cameraTurnRoutine != null)
            {
                yield return cameraTurnRoutine;
                cameraTurnRoutine = null;
            }

            foreach (GameObject ticket in batchObjects)
            {
                DestroySafely(ticket);
            }

            activeBatchTickets.Clear();
            activeTicket = null;
            RestoreMechanism();
            cameraTurning = false;
            session.MarkTransferArriving();
            yield return AnimateCamera(false);
            if (resetAfterStandaloneLaunch &&
                FindAnyObjectByType<CraftLivePad2TransferReceiver>() == null)
            {
                session.CompleteTransferPreviewWithoutPlacement();
            }

            FinishLaunchRoutine();
        }

        private void FinishLaunchRoutine()
        {
            launchRoutine = null;

            // Pad2 can finish placing the first item while this controller is
            // still restoring the camera. In that case the next batch item is
            // already Pad1Loading, but its StateChanged notification arrived
            // while launchRoutine was occupied. Re-evaluate the latest state
            // after releasing the routine so that notification is not lost.
            if (isActiveAndEnabled && session != null)
            {
                Refresh(session.State);
            }
        }

        private static void PlayLaunchSound()
        {
            CraftLiveAudio.Play(
                CraftLiveSound.TransferWhoosh,
                0.95f);
        }

        private List<GameObject> CollectPhysicalBatchTickets(
            CraftLiveRoomState snapshot)
        {
            List<GameObject> tickets = new List<GameObject>();
            if (activeTicket != null)
            {
                tickets.Add(activeTicket);
            }

            int additionalCount = Mathf.Min(
                snapshot.transferBatchRemaining,
                snapshot.transferQueue != null
                    ? snapshot.transferQueue.Count
                    : 0);
            for (int index = 0; index < additionalCount; index++)
            {
                CraftLiveTransferQueueEntry entry =
                    snapshot.transferQueue[index];
                if (entry == null)
                {
                    continue;
                }

                if (queueArrivalRoutines.TryGetValue(
                        entry.serial,
                        out Coroutine arrivalRoutine) &&
                    arrivalRoutine != null)
                {
                    StopCoroutine(arrivalRoutine);
                }
                queueArrivalRoutines.Remove(entry.serial);

                queueVisuals.TryGetValue(
                    entry.serial,
                    out GameObject ticket);
                queueVisuals.Remove(entry.serial);
                if (ticket == null)
                {
                    CraftLiveMaterialDefinition material =
                        session.Catalog != null
                            ? session.Catalog.FindMaterial(
                                entry.materialId)
                            : null;
                    Vector3 position = bindings != null &&
                                       bindings.TransferQueueRoot != null
                        ? bindings.TransferQueueRoot.position
                        : transform.position;
                    ticket = CreateTicket(material, position);
                }

                ticket.transform.SetParent(null, true);
                tickets.Add(ticket);
                prelaunchedTransferSerials.Add(entry.serial);
            }

            return tickets;
        }

        private IEnumerator AnimateBatchLoad(
            Transform[] tickets,
            Vector3[] starts,
            Vector3[] ends)
        {
            float elapsed = 0f;
            while (elapsed < loadDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(
                    Mathf.Clamp01(elapsed / loadDuration));
                for (int index = 0; index < tickets.Length; index++)
                {
                    if (tickets[index] != null)
                    {
                        tickets[index].position =
                            Vector3.LerpUnclamped(
                                starts[index],
                                ends[index],
                                t);
                    }
                }
                SetPull(Mathf.Max(pullAmount, t));
                yield return null;
            }

            for (int index = 0; index < tickets.Length; index++)
            {
                if (tickets[index] != null)
                {
                    tickets[index].position = ends[index];
                }
            }
        }

        private IEnumerator AnimateBatchLinear(
            Transform[] tickets,
            Vector3[] starts,
            Vector3[] ends)
        {
            float speed = Mathf.Max(0.1f, physicalLaunchSpeed);
            float duration = 0f;
            for (int index = 0; index < tickets.Length; index++)
            {
                duration = Mathf.Max(
                    duration,
                    Vector3.Distance(starts[index], ends[index]) /
                    speed);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                for (int index = 0; index < tickets.Length; index++)
                {
                    if (tickets[index] == null)
                    {
                        continue;
                    }

                    float distance =
                        Vector3.Distance(starts[index], ends[index]);
                    float t = distance > 0.0001f
                        ? Mathf.Clamp01(elapsed * speed / distance)
                        : 1f;
                    tickets[index].position = Vector3.LerpUnclamped(
                        starts[index],
                        ends[index],
                        t);
                }
                yield return null;
            }
        }

        private IEnumerator AnimateBatchRamp(
            Transform[] tickets,
            Vector3[] starts,
            Vector3[] controls,
            Vector3[] ends)
        {
            float speed = Mathf.Max(0.1f, physicalLaunchSpeed);
            float[] pathLengths = new float[tickets.Length];
            Quaternion[] rotations = new Quaternion[tickets.Length];
            float duration = 0f;
            for (int index = 0; index < tickets.Length; index++)
            {
                pathLengths[index] =
                    Vector3.Distance(starts[index], controls[index]) +
                    Vector3.Distance(controls[index], ends[index]);
                duration = Mathf.Max(
                    duration,
                    pathLengths[index] / speed);
                rotations[index] = tickets[index] != null
                    ? tickets[index].rotation
                    : Quaternion.identity;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                for (int index = 0; index < tickets.Length; index++)
                {
                    if (tickets[index] == null)
                    {
                        continue;
                    }

                    float t = pathLengths[index] > 0.0001f
                        ? Mathf.Clamp01(
                            elapsed * speed / pathLengths[index])
                        : 1f;
                    tickets[index].position =
                        EvaluateQuadraticBezier(
                            starts[index],
                            controls[index],
                            ends[index],
                            t);
                    tickets[index].rotation = Quaternion.Slerp(
                        rotations[index],
                        rotations[index] *
                        Quaternion.Euler(0f, 0f, -18f),
                        t);
                }
                yield return null;
            }

            for (int index = 0; index < tickets.Length; index++)
            {
                if (tickets[index] != null)
                {
                    tickets[index].position = ends[index];
                }
            }
        }

        private void ClaimQueuedTicket(int serial)
        {
            if (!queueVisuals.TryGetValue(serial, out GameObject visual) ||
                visual == null)
            {
                return;
            }

            if (queueArrivalRoutines.TryGetValue(
                    serial,
                    out Coroutine arrivalRoutine) &&
                arrivalRoutine != null)
            {
                StopCoroutine(arrivalRoutine);
            }
            queueArrivalRoutines.Remove(serial);
            queueVisuals.Remove(serial);
            visual.transform.SetParent(null, true);
            activeTicket = visual;
        }

        private IEnumerator AnimatePhysicalImpact()
        {
            float startPull = pullAmount;
            float elapsed = 0f;
            while (elapsed < impactDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(Mathf.Clamp01(
                    elapsed / impactDuration));
                SetPull(Mathf.Lerp(startPull, 0f, t));
                yield return null;
            }

            SetPull(0f);
        }

        private IEnumerator AnimateGrooveLaunch(
            Transform ticket,
            Vector3 start,
            Vector3 end)
        {
            float distance = Vector3.Distance(start, end);
            if (distance <= 0.0001f)
            {
                ticket.position = end;
                yield break;
            }

            float duration = distance /
                Mathf.Max(0.1f, physicalLaunchSpeed);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(
                    elapsed * physicalLaunchSpeed / distance);
                ticket.position = Vector3.LerpUnclamped(start, end, t);
                yield return null;
            }

            ticket.position = end;
        }

        private IEnumerator AnimateRampLaunch(
            Transform ticket,
            Vector3 start,
            Vector3 end)
        {
            Vector3 control = rampPathMiddle != null
                ? rampPathMiddle.position
                : Vector3.Lerp(start, end, 0.55f);
            Quaternion startRotation = ticket.rotation;
            const int sampleCount = 32;
            float[] cumulativeLengths = new float[sampleCount + 1];
            Vector3 previous = start;
            for (int i = 1; i <= sampleCount; i++)
            {
                float sampleT = i / (float)sampleCount;
                Vector3 point = EvaluateQuadraticBezier(
                    start,
                    control,
                    end,
                    sampleT);
                cumulativeLengths[i] =
                    cumulativeLengths[i - 1] +
                    Vector3.Distance(previous, point);
                previous = point;
            }

            float pathLength = cumulativeLengths[sampleCount];
            if (pathLength <= 0.0001f)
            {
                ticket.position = end;
                yield break;
            }

            float duration = pathLength /
                Mathf.Max(0.1f, physicalLaunchSpeed);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float travelled = Mathf.Min(
                    pathLength,
                    elapsed * physicalLaunchSpeed);
                float pathProgress = travelled / pathLength;
                float curveT = ResolveBezierParameterByDistance(
                    cumulativeLengths,
                    travelled);
                ticket.position = EvaluateQuadraticBezier(
                    start,
                    control,
                    end,
                    curveT);
                ticket.rotation = Quaternion.Slerp(
                    startRotation,
                    startRotation * Quaternion.Euler(0f, 0f, -18f),
                    pathProgress);
                yield return null;
            }

            ticket.position = end;
        }

        private static Vector3 EvaluateQuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * start +
                   2f * oneMinusT * t * control +
                   t * t * end;
        }

        private static float ResolveBezierParameterByDistance(
            float[] cumulativeLengths,
            float targetDistance)
        {
            int sampleCount = cumulativeLengths.Length - 1;
            for (int i = 1; i <= sampleCount; i++)
            {
                if (cumulativeLengths[i] < targetDistance)
                {
                    continue;
                }

                float segmentLength =
                    cumulativeLengths[i] - cumulativeLengths[i - 1];
                float segmentT = segmentLength > 0.0001f
                    ? (targetDistance - cumulativeLengths[i - 1]) /
                      segmentLength
                    : 0f;
                return (i - 1 + segmentT) / sampleCount;
            }
            return 1f;
        }

        private IEnumerator AnimatePostRampFlight(
            Transform ticket,
            Vector3 exitDirection)
        {
            if (ticket == null)
            {
                yield break;
            }

            Vector3 direction = exitDirection.sqrMagnitude > 0.0001f
                ? exitDirection.normalized
                : physicalLaunchDirection;
            Vector3 velocity =
                direction * Mathf.Max(0.1f, physicalLaunchSpeed);
            Vector3 gravity =
                -ResolveCameraUp().normalized * postRampGravity;
            while (ticket != null && cameraTurning)
            {
                float deltaTime = Mathf.Max(0f, Time.deltaTime);
                velocity += gravity * deltaTime;
                ticket.position += velocity * deltaTime;
                ticket.Rotate(
                    0f,
                    0f,
                    -90f * deltaTime,
                    Space.Self);
                yield return null;
            }
        }

        private IEnumerator AnimateBatchPostRampFlight(
            Transform[] tickets,
            Vector3[] exitDirections)
        {
            Vector3[] velocities = new Vector3[tickets.Length];
            float speed = Mathf.Max(0.1f, physicalLaunchSpeed);
            for (int index = 0; index < tickets.Length; index++)
            {
                Vector3 direction =
                    index < exitDirections.Length
                        ? exitDirections[index]
                        : physicalLaunchDirection;
                velocities[index] = direction.normalized * speed;
            }

            Vector3 gravity =
                -ResolveCameraUp().normalized * postRampGravity;
            while (cameraTurning)
            {
                float deltaTime = Mathf.Max(0f, Time.deltaTime);
                for (int index = 0; index < tickets.Length; index++)
                {
                    Transform ticket = tickets[index];
                    if (ticket == null)
                    {
                        continue;
                    }

                    velocities[index] += gravity * deltaTime;
                    ticket.position += velocities[index] * deltaTime;
                    ticket.Rotate(
                        0f,
                        0f,
                        -90f * deltaTime,
                        Space.Self);
                }
                yield return null;
            }
        }

        private IEnumerator AnimateLoad(
            Transform ticket,
            Vector3 start,
            Vector3 end)
        {
            float elapsed = 0f;
            while (elapsed < loadDuration)
            {
                elapsed += Time.deltaTime;
                float t = EaseOut(
                    Mathf.Clamp01(elapsed / loadDuration));
                ticket.position =
                    Vector3.LerpUnclamped(start, end, t);
                SetPull(Mathf.Max(pullAmount, t));
                yield return null;
            }
        }

        private IEnumerator AnimateLaunch(
            Transform ticket,
            Vector3 start,
            Vector3 end)
        {
            float elapsed = 0f;
            Quaternion rotation = ticket.rotation;
            while (elapsed < launchDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(
                    elapsed / launchDuration);
                Vector3 position =
                    Vector3.LerpUnclamped(start, end, t);
                position.y +=
                    Mathf.Sin(t * Mathf.PI) *
                    launchArcHeight;
                ticket.position = position;
                ticket.rotation =
                    rotation *
                    Quaternion.Euler(0f, 0f, t * 260f);
                ApplyMechanism(1f - EaseOut(t));
                yield return null;
            }
        }

        private IEnumerator AnimateCamera(bool towardRail)
        {
            if (!animateCameraDuringLaunch || targetCamera == null)
            {
                cameraTurning = false;
                yield break;
            }

            Transform cameraTransform = targetCamera.transform;
            Vector3 startPosition = cameraTransform.position;
            Quaternion startRotation = cameraTransform.rotation;
            if (!cameraRestCaptured)
            {
                cameraRestPosition = startPosition;
                cameraRestRotation = startRotation;
                cameraRestCaptured = true;
            }

            Vector3 railPosition = cameraRestPosition;
            Quaternion railRotation =
                cameraRestRotation *
                Quaternion.Euler(0f, cameraYawDegrees, 0f);
            Vector3 endPosition = towardRail
                ? railPosition
                : cameraRestPosition;
            Quaternion endRotation = towardRail
                ? railRotation
                : cameraRestRotation;
            float rotationDuration =
                Mathf.Max(0f, cameraShiftDuration);
            if (rotationDuration <= 0.0001f)
            {
                cameraTransform.SetPositionAndRotation(
                    endPosition,
                    endRotation);
                cameraTurning = false;
                yield break;
            }

            cameraTurning = true;
            float elapsed = 0f;
            while (elapsed < rotationDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(
                    elapsed / rotationDuration);
                cameraTransform.position =
                    Vector3.Lerp(
                        startPosition,
                        endPosition,
                        t);
                cameraTransform.rotation =
                    Quaternion.Slerp(
                        startRotation,
                        endRotation,
                        t);
                yield return null;
            }
            cameraTransform.SetPositionAndRotation(
                endPosition,
                endRotation);
            cameraTurning = false;
        }

        private void BuildFallbackPresentation()
        {
            if (bindings == null ||
                bindings.SpringLauncherRoot == null)
            {
                return;
            }

            DestroySafely(generatedRoot);
            if (usingPhysicalLauncher)
            {
                BuildPhysicalLauncher();
                return;
            }

            if (!createFallbackVisuals)
            {
                return;
            }

            generatedRoot = new GameObject(
                "Generated_TransferLauncher");
            generatedRoot.transform.SetParent(
                bindings.SpringLauncherRoot,
                false);
            generatedRoot.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();

            launcherSeat = CreateAnchor(
                generatedRoot.transform,
                "LauncherSeat",
                new Vector3(0f, 0.08f, -0.05f));
            launchExit = CreateAnchor(
                generatedRoot.transform,
                "LaunchExit",
                new Vector3(1.55f, 0.28f, -0.05f));
            launcherArm = CreateCube(
                generatedRoot.transform,
                "LauncherArm",
                new Vector3(0f, -0.09f, 0f),
                new Vector3(0.09f, 0.42f, 0.09f),
                new Color(0.38f, 0.25f, 0.12f));
            springVisual = CreateSpringVisual(generatedRoot.transform);
            armRestRotation = launcherArm.localRotation;
            springRestScale = springVisual.localScale;

            GameObject pullHandle = CreateCube(
                generatedRoot.transform,
                "PullAndRelease",
                new Vector3(0f, -0.68f, -0.08f),
                new Vector3(1.2f, 0.22f, 0.12f),
                new Color(0.18f, 0.55f, 0.64f)).gameObject;
            pullHandle.AddComponent<CraftLiveSpringDragHandle>()
                .Configure(this);
            CreateText(
                pullHandle.transform,
                "Label",
                "ばねを下へ引いて離す",
                new Vector3(0f, 0f, -0.56f),
                0.16f);

            GameObject modeButton = CreateCube(
                generatedRoot.transform,
                "ModeButton",
                new Vector3(0.82f, -0.68f, -0.08f),
                new Vector3(0.35f, 0.22f, 0.12f),
                new Color(0.48f, 0.4f, 0.18f)).gameObject;
            CraftLiveWorldButton worldButton =
                modeButton.AddComponent<CraftLiveWorldButton>();
            Renderer modeRenderer =
                modeButton.GetComponent<Renderer>();
            worldButton.Configure(
                modeButton.transform,
                new[] { modeRenderer },
                new Color(0.48f, 0.4f, 0.18f),
                new Color(0.65f, 0.57f, 0.3f),
                new Color(0.8f, 0.72f, 0.4f));
            worldButton.AddListener(ToggleLaunchMode);
            modeText = CreateText(
                modeButton.transform,
                "Label",
                string.Empty,
                new Vector3(0f, 0f, -0.56f),
                0.16f);
            queueText = CreateText(
                generatedRoot.transform,
                "QueueCount",
                string.Empty,
                new Vector3(0f, 0.48f, -0.08f),
                0.045f);
            PublishMode();
        }

        private void BuildPhysicalLauncher()
        {
            generatedRoot = new GameObject("PhysicalLauncherRuntime");
            generatedRoot.transform.SetParent(
                bindings.SpringLauncherRoot,
                false);
            generatedRoot.AddComponent<CraftLiveGeneratedRuntimeVisual>();

            physicalLaunchDirection = targetCamera != null
                ? targetCamera.transform.right.normalized
                : Vector3.right;
            Vector3 cameraUp = targetCamera != null
                ? targetCamera.transform.up.normalized
                : Vector3.up;
            springVisual = sceneSpring;
            springRestScale = springVisual.localScale;
            springRestPosition = springVisual.position;
            pusherRestPosition = pusherPlate.position;
            launcherArm = null;

            Vector3 seatPosition =
                pusherRestPosition +
                physicalLaunchDirection * frameSeatDistance;
            ResolveRampPath(
                seatPosition,
                cameraUp,
                out Vector3 boxExitPosition,
                out Vector3 rampMiddlePosition,
                out Vector3 rampEndPosition);
            float seatHeight = Vector3.Dot(
                seatPosition,
                cameraUp);
            boxExitPosition += cameraUp * (
                seatHeight -
                Vector3.Dot(boxExitPosition, cameraUp));
            float middleHeight = Vector3.Dot(
                rampMiddlePosition,
                cameraUp);
            if (middleHeight < seatHeight)
            {
                rampMiddlePosition +=
                    cameraUp * (seatHeight - middleHeight);
            }
            launcherSeat = CreateWorldAnchor(
                generatedRoot.transform,
                "FrameSeat",
                seatPosition);
            launchExit = CreateWorldAnchor(
                generatedRoot.transform,
                "BoxExit",
                boxExitPosition);
            rampPathMiddle = CreateWorldAnchor(
                generatedRoot.transform,
                "RampPathMiddle",
                rampMiddlePosition);
            rampLaunchEnd = CreateWorldAnchor(
                generatedRoot.transform,
                "RampLaunchEnd",
                rampEndPosition);
            CreatePhysicalDragArea();
        }

        private void ResolveRampPath(
            Vector3 seatPosition,
            Vector3 cameraUp,
            out Vector3 entry,
            out Vector3 middle,
            out Vector3 exit)
        {
            entry =
                seatPosition +
                physicalLaunchDirection * boxExitDistance;
            middle =
                entry +
                physicalLaunchDirection *
                (rampLaunchDistance * 0.58f);
            exit =
                entry +
                physicalLaunchDirection * rampLaunchDistance +
                cameraUp * rampLaunchRise;

            Renderer[] rampRenderers = launcherRamp != null
                ? launcherRamp.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            if (rampRenderers.Length > 0)
            {
                Bounds bounds = rampRenderers[0].bounds;
                for (int i = 1; i < rampRenderers.Length; i++)
                {
                    bounds.Encapsulate(rampRenderers[i].bounds);
                }

                float rightExtent = ProjectBoundsExtent(
                    bounds.extents,
                    physicalLaunchDirection);
                float upExtent = ProjectBoundsExtent(
                    bounds.extents,
                    cameraUp);
                entry =
                    bounds.center -
                    physicalLaunchDirection * rightExtent -
                    cameraUp * (upExtent * 0.42f);
                middle =
                    bounds.center +
                    physicalLaunchDirection * (rightExtent * 0.08f) -
                    cameraUp * (upExtent * 0.36f);
                exit =
                    bounds.center +
                    physicalLaunchDirection * rightExtent +
                    cameraUp * (upExtent * 0.82f);
            }

            if (rampEntryAnchor != null)
            {
                entry = rampEntryAnchor.position;
            }
            if (rampMiddleAnchor != null)
            {
                middle = rampMiddleAnchor.position;
            }
            if (rampExitAnchor != null)
            {
                exit = rampExitAnchor.position;
            }
        }

        private static float ProjectBoundsExtent(
            Vector3 extents,
            Vector3 axis)
        {
            axis = new Vector3(
                Mathf.Abs(axis.x),
                Mathf.Abs(axis.y),
                Mathf.Abs(axis.z));
            return Vector3.Dot(extents, axis);
        }

        private void CreatePhysicalDragArea()
        {
            GameObject hitArea = new GameObject("SpringDragArea");
            hitArea.transform.SetParent(generatedRoot.transform, true);
            hitArea.transform.position = Vector3.Lerp(
                sceneSpring.position,
                pusherPlate.position,
                0.5f);
            hitArea.transform.rotation = targetCamera != null
                ? targetCamera.transform.rotation
                : Quaternion.identity;
            hitArea.transform.localScale =
                new Vector3(0.62f, 0.34f, 0.22f);
            hitArea.AddComponent<BoxCollider>();
            hitArea.AddComponent<CraftLiveSpringDragHandle>()
                .Configure(this);
        }

        private void RefreshQueueVisuals(CraftLiveRoomState state)
        {
            if (bindings == null ||
                bindings.TransferQueueRoot == null ||
                state.transferQueue == null)
            {
                return;
            }

            HashSet<int> activeSerials = new HashSet<int>();
            float physicalQueueFront = usingPhysicalLauncher
                ? ResolvePusherFrontProjection()
                : 0f;
            for (int i = 0; i < state.transferQueue.Count; i++)
            {
                CraftLiveTransferQueueEntry entry =
                    state.transferQueue[i];
                if (entry == null)
                {
                    continue;
                }

                activeSerials.Add(entry.serial);
                if (prelaunchedTransferSerials.Contains(entry.serial))
                {
                    continue;
                }

                bool created = false;
                if (!queueVisuals.TryGetValue(
                        entry.serial,
                        out GameObject visual) ||
                    visual == null)
                {
                    CraftLiveMaterialDefinition material =
                        session.Catalog != null
                            ? session.Catalog.FindMaterial(
                                entry.materialId)
                            : null;
                    visual = CreateTicket(
                        material,
                        bindings.TransferQueueRoot.position);
                    visual.name =
                        $"Queued_{entry.serial}_{entry.materialId}";
                    visual.transform.SetParent(
                        bindings.TransferQueueRoot,
                        true);
                    queueVisuals[entry.serial] = visual;
                    created = true;
                }

                Vector3 targetLocalPosition;
                Vector3 targetWorldPosition;
                if (usingPhysicalLauncher)
                {
                    targetWorldPosition = ResolvePackedQueueWorldPosition(
                        visual,
                        ref physicalQueueFront);
                    targetLocalPosition =
                        bindings.TransferQueueRoot.InverseTransformPoint(
                            targetWorldPosition);
                }
                else
                {
                    targetLocalPosition = ResolveQueueLocalPosition(
                        i,
                        state.transferQueue.Count);
                    targetWorldPosition =
                        bindings.TransferQueueRoot.TransformPoint(
                            targetLocalPosition);
                }

                if (created)
                {
                    Transform source = ResolvePaintingSource(
                        entry.materialId);
                    GameObject mergeModel =
                        ConsumePendingMergeModel(entry.materialId);
                    if ((source != null || mergeModel != null) &&
                        queueArrivalDuration > 0f)
                    {
                        visual.transform.position = source != null
                            ? source.position
                            : ResolveRendererCenter(mergeModel);
                        queueArrivalRoutines[entry.serial] =
                            StartCoroutine(AnimateQueueArrival(
                                entry.serial,
                                visual.transform,
                                targetWorldPosition,
                                mergeModel));
                    }
                    else
                    {
                        visual.transform.position = targetWorldPosition;
                        if (mergeModel != null)
                        {
                            SetTransferredArtworkVisible(
                                visual.transform,
                                true);
                            DestroySafely(mergeModel);
                        }
                    }
                }
                else if (!queueArrivalRoutines.ContainsKey(entry.serial))
                {
                    visual.transform.position = targetWorldPosition;
                }
            }

            List<int> removed = new List<int>();
            foreach (KeyValuePair<int, GameObject> pair in queueVisuals)
            {
                if (!activeSerials.Contains(pair.Key))
                {
                    DestroySafely(pair.Value);
                    removed.Add(pair.Key);
                }
            }

            foreach (int serial in removed)
            {
                if (queueArrivalRoutines.TryGetValue(
                        serial,
                        out Coroutine routine) &&
                    routine != null)
                {
                    StopCoroutine(routine);
                }
                queueArrivalRoutines.Remove(serial);
                queueVisuals.Remove(serial);
            }
        }

        private Vector3 ResolveQueueLocalPosition(
            int index,
            int totalCount)
        {
            if (usingPhysicalLauncher)
            {
                return Vector3.zero;
            }

            int columns = Mathf.Max(1, queueColumns);
            int row = index / columns;
            int column = index % columns;
            int itemsInRow = Mathf.Min(
                columns,
                totalCount - row * columns);
            float center = (itemsInRow - 1) * 0.5f;
            return new Vector3(
                (column - center) * queueSpacing,
                row * queueSpacing * 0.78f,
                -0.04f);
        }

        private float ResolvePusherFrontProjection()
        {
            float front = Vector3.Dot(
                pusherRestPosition,
                physicalLaunchDirection);
            if (pusherPlate != null &&
                TryGetRendererBounds(pusherPlate.gameObject, out Bounds bounds))
            {
                front = Vector3.Dot(
                            bounds.center,
                            physicalLaunchDirection) +
                        ProjectBoundsExtent(
                            bounds.extents,
                            physicalLaunchDirection);
            }
            return front;
        }

        private Vector3 ResolvePackedQueueWorldPosition(
            GameObject visual,
            ref float occupiedFront)
        {
            float halfExtent = ResolveProjectedExtent(
                visual,
                physicalLaunchDirection,
                grooveFrameSize.x * 0.5f);
            float centerProjection =
                occupiedFront + physicalFrameGap + halfExtent;
            occupiedFront = centerProjection + halfExtent;

            Vector3 reference = launcherSeat != null
                ? launcherSeat.position
                : pusherRestPosition;
            return reference +
                   physicalLaunchDirection *
                   (centerProjection - Vector3.Dot(
                       reference,
                       physicalLaunchDirection));
        }

        private static float ResolveProjectedExtent(
            GameObject target,
            Vector3 axis,
            float fallback)
        {
            return target != null &&
                   TryGetRendererBounds(target, out Bounds bounds)
                ? Mathf.Max(
                    0.001f,
                    ProjectBoundsExtent(bounds.extents, axis))
                : Mathf.Max(0.001f, fallback);
        }

        private GameObject ConsumePendingMergeModel(string materialId)
        {
            if (pendingMergeModel == null ||
                pendingMergeMaterialId != materialId)
            {
                return null;
            }

            GameObject result = pendingMergeModel;
            pendingMergeModel = null;
            pendingMergeMaterialId = string.Empty;
            return result;
        }

        private static Vector3 ResolveRendererCenter(GameObject target)
        {
            return target != null &&
                   TryGetRendererBounds(target, out Bounds bounds)
                ? bounds.center
                : target != null
                    ? target.transform.position
                    : Vector3.zero;
        }

        private static bool TryGetRendererBounds(
            GameObject target,
            out Bounds bounds)
        {
            Renderer[] renderers = target != null
                ? target.GetComponentsInChildren<Renderer>(true)
                : new Renderer[0];
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }

        private Transform ResolvePaintingSource(string materialId)
        {
            CraftLivePad1GalleryController gallery =
                GetComponent<CraftLivePad1GalleryController>();
            return gallery != null
                ? gallery.FindMaterialAnchor(materialId)
                : null;
        }

        private IEnumerator AnimateQueueArrival(
            int serial,
            Transform visual,
            Vector3 targetPosition,
            GameObject mergeModel)
        {
            Vector3 startPosition = visual.position;
            Vector3 finalScale = visual.localScale;
            visual.localScale = finalScale * 0.78f;
            if (mergeModel == null)
            {
                float directElapsed = 0f;
                while (visual != null &&
                       directElapsed < queueArrivalDuration)
                {
                    directElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(
                        directElapsed / queueArrivalDuration);
                    float eased = EaseOut(t);
                    Vector3 position = Vector3.LerpUnclamped(
                        startPosition,
                        targetPosition,
                        eased);
                    position += ResolveCameraUp() *
                        (Mathf.Sin(t * Mathf.PI) *
                         queueArrivalArcHeight);
                    visual.position = position;
                    visual.localScale = Vector3.Lerp(
                        finalScale * 0.78f,
                        finalScale,
                        eased);
                    yield return null;
                }

                if (visual != null)
                {
                    visual.position = targetPosition;
                    visual.localScale = finalScale;
                }
                queueArrivalRoutines.Remove(serial);
                yield break;
            }

            Transform model = mergeModel.transform;
            Vector3 modelCenter = ResolveRendererCenter(mergeModel);
            Vector3 mergePosition = modelCenter;
            if (targetCamera != null)
            {
                mergePosition -= targetCamera.transform.forward * 0.06f;
            }
            Vector3 modelStartPosition = model.position;
            Quaternion modelStartRotation = model.rotation;
            Vector3 modelStartScale = model.localScale;
            Vector3 modelTargetPosition =
                ResolveTicketAbsorptionPoint(visual, mergePosition);
            Quaternion modelTargetRotation = targetCamera != null
                ? targetCamera.transform.rotation
                : model.rotation;
            float approachDuration = Mathf.Max(
                0.03f,
                queueArrivalDuration * modelMergeApproachRatio);
            float absorptionDuration = Mathf.Max(
                0.03f,
                0.22f / Mathf.Max(0.1f, modelAbsorptionSpeed));
            float moveDuration = Mathf.Max(
                0.03f,
                queueArrivalDuration * (1f - modelMergeApproachRatio));
            float totalDuration =
                approachDuration + absorptionDuration + moveDuration;
            bool absorbed = false;
            SetTransferredArtworkVisible(visual, false);
            float elapsed = 0f;
            while (visual != null && elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                if (elapsed < approachDuration)
                {
                    float phase = EaseOut(Mathf.Clamp01(
                        elapsed / approachDuration));
                    visual.position = Vector3.LerpUnclamped(
                        startPosition,
                        mergePosition,
                        phase);
                    visual.localScale = Vector3.Lerp(
                        finalScale * 0.78f,
                        finalScale,
                        phase);
                }
                else if (elapsed <
                         approachDuration + absorptionDuration)
                {
                    visual.position = mergePosition;
                    visual.localScale = finalScale;
                    float phase = EaseOut(Mathf.Clamp01(
                        (elapsed - approachDuration) /
                        absorptionDuration));
                    if (model != null)
                    {
                        model.position = Vector3.Lerp(
                            modelStartPosition,
                            modelTargetPosition,
                            phase);
                        model.rotation = Quaternion.Slerp(
                            modelStartRotation,
                            modelTargetRotation,
                            phase);
                        model.localScale = Vector3.Lerp(
                            modelStartScale,
                            Vector3.zero,
                            phase);
                    }
                }
                else
                {
                    if (!absorbed)
                    {
                        SetTransferredArtworkVisible(visual, true);
                        DestroySafely(mergeModel);
                        model = null;
                        absorbed = true;
                    }
                    float phase = EaseOut(Mathf.Clamp01(
                        (elapsed - approachDuration -
                         absorptionDuration) / moveDuration));
                    Vector3 position = Vector3.LerpUnclamped(
                        mergePosition,
                        targetPosition,
                        phase);
                    position += ResolveCameraUp() *
                        (Mathf.Sin(phase * Mathf.PI) *
                         queueArrivalArcHeight);
                    visual.position = position;
                }
                yield return null;
            }

            if (visual != null)
            {
                SetTransferredArtworkVisible(visual, true);
                visual.position = targetPosition;
                visual.localScale = finalScale;
            }
            if (!absorbed)
            {
                DestroySafely(mergeModel);
            }
            queueArrivalRoutines.Remove(serial);
        }

        private Vector3 ResolveCameraUp()
        {
            return targetCamera != null
                ? targetCamera.transform.up
                : Vector3.up;
        }

        private Vector3 ResolveTicketAbsorptionPoint(
            Transform ticket,
            Vector3 ticketPosition)
        {
            Vector3 ticketCenterOffset = Vector3.zero;
            if (TryGetRendererBounds(ticket.gameObject, out Bounds ticketBounds))
            {
                ticketCenterOffset = ticketBounds.center - ticket.position;
            }

            Vector3 targetPosition = ticketPosition + ticketCenterOffset;
            if (targetCamera != null)
            {
                targetPosition -= targetCamera.transform.forward * 0.03f;
            }
            return targetPosition;
        }

        private static void SetTransferredArtworkVisible(
            Transform ticket,
            bool visible)
        {
            if (ticket == null)
            {
                return;
            }

            foreach (SpriteRenderer spriteRenderer in
                     ticket.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (spriteRenderer.gameObject.name == "TransferredArtwork")
                {
                    spriteRenderer.enabled = visible;
                }
            }
        }

        private GameObject CreateTicket(
            CraftLiveMaterialDefinition material,
            Vector3 position)
        {
            GameObject prefab =
                material != null &&
                material.TransferTicketPrefab != null
                    ? material.TransferTicketPrefab
                    : fallbackTicketPrefab;
            GameObject ticket;
            if (prefab != null)
            {
                ticket = Instantiate(
                    prefab,
                    position,
                    Quaternion.identity);
            }
            else
            {
                ticket = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                ticket.transform.SetPositionAndRotation(
                    position,
                    Quaternion.identity);
                ticket.transform.localScale =
                    new Vector3(0.48f, 0.65f, 0.08f);
                DestroySafely(ticket.GetComponent<Collider>());
            }

            if (usingPhysicalLauncher)
            {
                FitTicket(ticket, grooveFrameSize);
            }
            else
            {
                FitTicket(ticket, queueTicketSize);
            }
            AddTicketArtwork(ticket, material);
            foreach (Collider targetCollider in
                     ticket.GetComponentsInChildren<Collider>(true))
            {
                targetCollider.enabled = false;
            }

            return ticket;
        }

        private void AddTicketArtwork(
            GameObject ticket,
            CraftLiveMaterialDefinition material)
        {
            if (ticket == null || material == null || material.Icon == null)
            {
                return;
            }

            Renderer[] renderers =
                ticket.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0
                ? renderers[0].bounds
                : new Bounds(ticket.transform.position, Vector3.one);
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            GameObject artwork = new GameObject("TransferredArtwork");
            SpriteRenderer spriteRenderer =
                artwork.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = material.Icon;
            spriteRenderer.color = Color.white;
            spriteRenderer.sortingOrder = 6;
            artwork.transform.position = bounds.center;
            if (targetCamera != null)
            {
                artwork.transform.rotation =
                    targetCamera.transform.rotation;
                artwork.transform.position -=
                    targetCamera.transform.forward * 0.018f;
            }

            Vector2 spriteSize = material.Icon.bounds.size;
            float desiredWidth = Mathf.Max(0.05f, bounds.size.x * 0.68f);
            float desiredHeight = Mathf.Max(0.05f, bounds.size.y * 0.68f);
            float artworkScale = Mathf.Min(
                desiredWidth / Mathf.Max(0.0001f, spriteSize.x),
                desiredHeight / Mathf.Max(0.0001f, spriteSize.y));
            artwork.transform.localScale = Vector3.one * artworkScale;
            artwork.transform.SetParent(ticket.transform, true);
        }

        private static void FitTicket(GameObject ticket, float targetSize)
        {
            if (ticket == null)
            {
                return;
            }

            Renderer[] renderers =
                ticket.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float largest = Mathf.Max(bounds.size.x, bounds.size.y);
            if (largest > 0.0001f)
            {
                ticket.transform.localScale *= targetSize / largest;
            }
        }

        private static void FitTicket(
            GameObject ticket,
            Vector2 targetSize)
        {
            if (ticket == null)
            {
                return;
            }

            Renderer[] renderers =
                ticket.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float scaleByWidth =
                Mathf.Max(0.01f, targetSize.x) /
                Mathf.Max(0.0001f, bounds.size.x);
            float scaleByHeight =
                Mathf.Max(0.01f, targetSize.y) /
                Mathf.Max(0.0001f, bounds.size.y);
            ticket.transform.localScale *= Mathf.Min(
                scaleByWidth,
                scaleByHeight);
        }

        private void SetPull(float value)
        {
            pullAmount = Mathf.Clamp01(value);
            ApplyMechanism(pullAmount);
            onPullChanged?.Invoke(pullAmount);
        }

        private void ApplyMechanism(float amount)
        {
            if (usingPhysicalLauncher &&
                springVisual != null &&
                pusherPlate != null)
            {
                Vector3 compressedScale = Vector3.Scale(
                    springRestScale,
                    compressedSpringScale);
                springVisual.localScale = Vector3.Lerp(
                    springRestScale,
                    compressedScale,
                    amount);
                float pullDistance =
                    springPullWorldDistance * amount;
                springVisual.position =
                    springRestPosition -
                    physicalLaunchDirection * (pullDistance * 0.5f);
                pusherPlate.position =
                    pusherRestPosition -
                    physicalLaunchDirection * pullDistance;
                return;
            }

            if (launcherArm != null)
            {
                launcherArm.localRotation =
                    Quaternion.Slerp(
                        armRestRotation,
                        Quaternion.Euler(pulledArmEuler) *
                        armRestRotation,
                        amount);
            }

            if (springVisual != null)
            {
                Vector3 compressedScale = Vector3.Scale(
                    springRestScale,
                    compressedSpringScale);
                springVisual.localScale =
                    Vector3.Lerp(
                        springRestScale,
                        compressedScale,
                        amount);
            }
        }

        private void RestoreMechanism()
        {
            pullAmount = 0f;
            ApplyMechanism(0f);
        }

        private void RestoreCamera()
        {
            if (targetCamera == null || !cameraRestCaptured)
            {
                return;
            }

            targetCamera.transform.SetPositionAndRotation(
                cameraRestPosition,
                cameraRestRotation);
            cameraRestCaptured = false;
        }

        private void PublishMode()
        {
            if (ShouldForceLaunchAll())
            {
                launchAll = true;
            }

            if (modeText != null)
            {
                modeText.text = launchAll
                    ? "全部発射"
                    : "1個発射";
            }

            onLaunchAllModeChanged?.Invoke(launchAll);
        }

        private bool ShouldForceLaunchAll()
        {
            return usingPhysicalLauncher &&
                   forceLaunchAllWithPhysicalLauncher;
        }

        private static Vector3 ResolveSlotExitOffset(
            CraftLiveSlotId slot)
        {
            switch (slot)
            {
                case CraftLiveSlotId.Top:
                case CraftLiveSlotId.Left:
                case CraftLiveSlotId.Skill:
                    return Vector3.up * 0.55f;
                case CraftLiveSlotId.Right:
                case CraftLiveSlotId.Bottom:
                case CraftLiveSlotId.Attribute:
                    return Vector3.down * 0.55f;
                default:
                    return Vector3.zero;
            }
        }

        private static Transform CreateAnchor(
            Transform parent,
            string name,
            Vector3 position)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = position;
            return anchor.transform;
        }

        private static Transform CreateWorldAnchor(
            Transform parent,
            string name,
            Vector3 position)
        {
            Transform anchor = CreateAnchor(
                parent,
                name,
                Vector3.zero);
            anchor.position = position;
            return anchor;
        }

        private Transform CreateSpringVisual(Transform parent)
        {
            GameObject pivotObject = new GameObject("SpringPivot");
            Transform pivot = pivotObject.transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = springPrefabLocalPosition;
            pivot.localScale = springPrefabScaleMultiplier;

            BoxCollider interactionCollider =
                pivotObject.AddComponent<BoxCollider>();
            interactionCollider.center = Vector3.zero;
            interactionCollider.size =
                new Vector3(0.62f, 0.55f, 0.32f);
            pivotObject.AddComponent<CraftLiveSpringDragHandle>()
                .Configure(this);

            if (springPrefab != null)
            {
                GameObject instance = Instantiate(
                    springPrefab,
                    pivot,
                    false);
                instance.name = "SpringModel";
                instance.transform.localPosition = Vector3.zero;
            }
            else
            {
                CreateCube(
                    pivot,
                    "SpringModel",
                    Vector3.zero,
                    new Vector3(0.32f, 0.36f, 0.18f),
                    new Color(0.55f, 0.58f, 0.62f));
            }

            return pivot;
        }

        private static Transform CreateCube(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            GameObject cube = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            ApplyColor(cube, color);
            return cube.transform;
        }

        private static TextMesh CreateText(
            Transform parent,
            string name,
            string value,
            Vector3 position,
            float characterSize)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                text,
                characterSize,
                CraftLiveForgeUITheme.ParchmentText);
            return text;
        }

        private static void ApplyColor(
            GameObject target,
            Color color)
        {
            if (target == null)
            {
                return;
            }

            foreach (Renderer renderer in
                     target.GetComponentsInChildren<Renderer>())
            {
                CraftLiveForgeUITheme.ApplyForgeSurface(renderer, color);
            }
        }

        private static float EaseOut(float value)
        {
            return 1f - Mathf.Pow(1f - value, 3f);
        }

        private void OnValidate()
        {
            queueColumns = Mathf.Max(1, queueColumns);
            queueSpacing = Mathf.Max(0.1f, queueSpacing);
            queueTicketSize = Mathf.Max(0.1f, queueTicketSize);
            grooveFrameSize.x = Mathf.Max(0.05f, grooveFrameSize.x);
            grooveFrameSize.y = Mathf.Max(0.05f, grooveFrameSize.y);
            frameSeatDistance = Mathf.Max(0.05f, frameSeatDistance);
            boxExitDistance = Mathf.Max(0.1f, boxExitDistance);
            rampLaunchDistance = Mathf.Max(0.1f, rampLaunchDistance);
            rampLaunchRise = Mathf.Max(0f, rampLaunchRise);
            springPullWorldDistance = Mathf.Max(
                0.02f,
                springPullWorldDistance);
            physicalFrameGap = Mathf.Max(0f, physicalFrameGap);
            batchTrainSpacing = Mathf.Max(0.01f, batchTrainSpacing);
            transferQueueViewportPosition.z = Mathf.Max(
                0.55f,
                transferQueueViewportPosition.z);
            springLauncherViewportPosition.z = Mathf.Max(
                0.55f,
                springLauncherViewportPosition.z);
            springPrefabScaleMultiplier.x = Mathf.Max(
                0.01f,
                springPrefabScaleMultiplier.x);
            springPrefabScaleMultiplier.y = Mathf.Max(
                0.01f,
                springPrefabScaleMultiplier.y);
            springPrefabScaleMultiplier.z = Mathf.Max(
                0.01f,
                springPrefabScaleMultiplier.z);
            compressedSpringScale.x = Mathf.Max(
                0.01f,
                compressedSpringScale.x);
            compressedSpringScale.y = Mathf.Clamp(
                compressedSpringScale.y,
                0.05f,
                1f);
            compressedSpringScale.z = Mathf.Max(
                0.01f,
                compressedSpringScale.z);
            requiredPullPixels = Mathf.Max(30f, requiredPullPixels);
            queueArrivalDuration = Mathf.Max(
                0.05f,
                queueArrivalDuration);
            queueArrivalArcHeight = Mathf.Max(
                0f,
                queueArrivalArcHeight);
            modelMergeApproachRatio = Mathf.Clamp(
                modelMergeApproachRatio,
                0.1f,
                0.7f);
            modelAbsorptionSpeed = Mathf.Max(
                0.1f,
                modelAbsorptionSpeed);
            impactDuration = Mathf.Max(0.03f, impactDuration);
            grooveLaunchDuration = Mathf.Max(
                0.05f,
                grooveLaunchDuration);
            loadDuration = Mathf.Max(0.05f, loadDuration);
            launchDuration = Mathf.Max(0.05f, launchDuration);
            physicalLaunchSpeed = Mathf.Max(
                0.1f,
                physicalLaunchSpeed);
            launchArcHeight = Mathf.Max(0f, launchArcHeight);
            cameraShiftDuration = Mathf.Max(
                0f,
                cameraShiftDuration);
            cameraRotationSpeed = Mathf.Max(
                1f,
                cameraRotationSpeed);
            cameraYawDegrees = Mathf.Clamp(
                cameraYawDegrees,
                -90f,
                90f);
            postRampGravity = Mathf.Max(0f, postRampGravity);
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

    public sealed class CraftLiveSpringDragHandle :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private CraftLivePad1TransferController controller;

        public void Configure(
            CraftLivePad1TransferController targetController)
        {
            controller = targetController;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            controller?.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            controller?.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            controller?.OnEndDrag(eventData);
        }
    }
}
