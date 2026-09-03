using UnityEngine;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePlacementWatchdog : MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField, Min(2f)] private float stageTimeoutSeconds = 6f;
        [SerializeField, Min(0.5f)] private float completionTimeoutSeconds = 3f;

        private CraftLivePlacementStatus observedStatus;
        private int observedGroupGeneration = -1;
        private int observedSerial = -1;
        private float observedAt;

        private void Awake()
        {
            if (session == null)
            {
                session = GetComponent<CraftLiveSession>();
            }
        }

        private void Update()
        {
            if (session == null || session.State == null)
            {
                return;
            }

            CraftLivePlacementFlow placement = session.State.placement;
            int groupGeneration = session.State.groupGeneration;
            if (groupGeneration != observedGroupGeneration ||
                placement.status != observedStatus ||
                placement.transferSerial != observedSerial)
            {
                observedGroupGeneration = groupGeneration;
                observedStatus = placement.status;
                observedSerial = placement.transferSerial;
                observedAt = Time.realtimeSinceStartup;
                return;
            }

            float elapsed = Time.realtimeSinceStartup - observedAt;
            if (session.Role == CraftLiveRole.MaterialPad &&
                elapsed >= stageTimeoutSeconds)
            {
                if (placement.status == CraftLivePlacementStatus.Pad1Loading)
                {
                    session.MarkTransferLaunching(
                        groupGeneration,
                        placement.transferSerial);
                    return;
                }

                if (placement.status == CraftLivePlacementStatus.Pad1Launching)
                {
                    session.MarkTransferArriving(
                        groupGeneration,
                        placement.transferSerial);
                    return;
                }
            }

            if (session.Role == CraftLiveRole.WorkbenchPad)
            {
                CraftLivePad2TransferReceiver receiver =
                    FindAnyObjectByType<
                        CraftLivePad2TransferReceiver>();
                if (placement.status == CraftLivePlacementStatus.Pad2Arriving &&
                    elapsed >= stageTimeoutSeconds)
                {
                    // The arrival animation is presentation only. Give it one
                    // extra stage window, but never let a stalled WebGL frame,
                    // renderer or coroutine hold the authoritative slot
                    // reservation forever.
                    if (receiver != null &&
                        receiver.IsReceivingTransfer(
                            groupGeneration,
                            placement.transferSerial) &&
                        elapsed < stageTimeoutSeconds * 2f)
                    {
                        return;
                    }

                    session.CompleteCurrentPlacement(
                        groupGeneration,
                        placement.transferSerial);
                    return;
                }

                if (placement.status == CraftLivePlacementStatus.PlacementComplete &&
                    elapsed >= completionTimeoutSeconds)
                {
                    bool recoveryTimeoutReached = elapsed >=
                        completionTimeoutSeconds + stageTimeoutSeconds;
                    bool receiverIsStillRunning = receiver != null &&
                        receiver.IsReceivingTransfer(
                            groupGeneration,
                            placement.transferSerial);
                    if (ShouldWaitForReceiver(
                            receiverIsStillRunning,
                            recoveryTimeoutReached))
                    {
                        // The receiver owns the place -> light -> continue
                        // sequence during its grace period. After the recovery
                        // timeout it must not hold the batch forever: a stuck
                        // WebGL coroutine used to leave the next item unable
                        // to launch even though the slot was already committed.
                        return;
                    }

                    if (recoveryTimeoutReached && receiverIsStillRunning)
                    {
                        receiver.AbortStalledTransfer(
                            groupGeneration,
                            placement.transferSerial);
                    }

                    CraftLiveLiquidFlowController flow =
                        FindAnyObjectByType<
                            CraftLiveLiquidFlowController>();
                    bool flowFinished = flow == null ||
                        flow.HasCompletedFlow(
                            groupGeneration,
                            placement.transferSerial);
                    if (flowFinished || recoveryTimeoutReached)
                    {
                        session.ContinueAfterPlacement(
                            groupGeneration,
                            placement.transferSerial);
                    }
                }
            }
        }

        public static bool ShouldWaitForReceiver(
            bool receiverIsStillRunning,
            bool recoveryTimeoutReached)
        {
            return receiverIsStillRunning && !recoveryTimeoutReached;
        }
    }
}
