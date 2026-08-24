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
                    if (receiver != null &&
                        receiver.IsReceivingTransfer(
                            groupGeneration,
                            placement.transferSerial))
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
                    if (receiver != null &&
                        receiver.IsReceivingTransfer(
                            groupGeneration,
                            placement.transferSerial))
                    {
                        // The receiver owns the place -> light -> continue
                        // sequence. Advancing here at the same frame as the
                        // light completion used to swallow the next arrival.
                        return;
                    }

                    CraftLiveLiquidFlowController flow =
                        FindAnyObjectByType<
                            CraftLiveLiquidFlowController>();
                    bool flowFinished = flow == null ||
                        flow.HasCompletedFlow(
                            groupGeneration,
                            placement.transferSerial);
                    bool recoveryTimeoutReached = elapsed >=
                        completionTimeoutSeconds + stageTimeoutSeconds;
                    if (flowFinished || recoveryTimeoutReached)
                    {
                        session.ContinueAfterPlacement(
                            groupGeneration,
                            placement.transferSerial);
                    }
                }
            }
        }
    }
}
