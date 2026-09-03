using System.IO;
using System.Reflection;
using CraftOrigin.CraftLive;
using NUnit.Framework;
using UnityEngine;

namespace CraftOrigin.CraftLiveEditor.Tests
{
    public sealed class CraftLiveStep9Tests
    {
        [Test]
        public void CompareVersion_PrefersGenerationThenRevisionThenTimestamp()
        {
            CraftLiveRoomState older = new CraftLiveRoomState
            {
                revision = 4,
                updatedAtUnixMs = 200
            };
            CraftLiveRoomState newerRevision = new CraftLiveRoomState
            {
                revision = 5,
                updatedAtUnixMs = 100
            };
            CraftLiveRoomState newerTimestamp = new CraftLiveRoomState
            {
                revision = 4,
                updatedAtUnixMs = 300
            };
            CraftLiveRoomState nextGeneration = new CraftLiveRoomState
            {
                groupGeneration = 1,
                revision = 0,
                updatedAtUnixMs = 1
            };
            CraftLiveRoomState delayedPreviousGeneration =
                new CraftLiveRoomState
                {
                    groupGeneration = 0,
                    revision = 999,
                    updatedAtUnixMs = 999
                };

            Assert.That(
                CraftLiveRoomTransport.CompareVersion(
                    newerRevision,
                    older),
                Is.GreaterThan(0));
            Assert.That(
                CraftLiveRoomTransport.CompareVersion(
                    newerTimestamp,
                    older),
                Is.GreaterThan(0));
            Assert.That(
                CraftLiveRoomTransport.CompareVersion(
                    nextGeneration,
                    delayedPreviousGeneration),
                Is.GreaterThan(0));
            Assert.That(
                CraftLiveRoomTransport.CompareVersion(null, older),
                Is.LessThan(0));
        }

        [Test]
        public void TransferReconciliation_PreservesArrivalAcrossNewerSnapshot()
        {
            CraftLiveRoomState local = CreateTransferState(
                3,
                7,
                CraftLivePlacementStatus.Pad2Arriving,
                10);
            CraftLiveRoomState remote = CreateTransferState(
                3,
                7,
                CraftLivePlacementStatus.Pad1Launching,
                11);
            remote.placement.Clear();

            Assert.That(
                CraftLiveRoomTransport.TryBuildTransferReconciledState(
                    remote,
                    local,
                    out CraftLiveRoomState reconciled),
                Is.True);
            Assert.That(reconciled.revision, Is.EqualTo(12));
            Assert.That(
                reconciled.transferSignal.status,
                Is.EqualTo(CraftLivePlacementStatus.Pad2Arriving));
            Assert.That(
                reconciled.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Pad2Arriving));
            Assert.That(reconciled.placement.transferSerial, Is.EqualTo(7));
            Assert.That(reconciled.placement.materialId, Is.EqualTo("ore"));
        }

        [Test]
        public void TransferReconciliation_DoesNotResurrectAcknowledgedArrival()
        {
            CraftLiveRoomState completed = CreateTransferState(
                2,
                9,
                CraftLivePlacementStatus.PlacementComplete,
                21);
            completed.lastCompletedTransferSerial = 9;
            completed.placement.Clear();
            CraftLiveRoomState delayedArrival = CreateTransferState(
                2,
                9,
                CraftLivePlacementStatus.Pad2Arriving,
                20);

            Assert.That(
                CraftLiveRoomTransport.TryBuildTransferReconciledState(
                    completed,
                    delayedArrival,
                    out _),
                Is.False);
            Assert.That(
                completed.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
        }

        [Test]
        public void TransferReconciliation_PreservesPad2SlotWhenPad1PreviewAckSharesSerial()
        {
            // Pad 2 has already committed the material to the authoritative
            // slot. This is the state produced by CompleteCurrentPlacement.
            CraftLiveRoomState pad2Committed = CreateTransferState(
                4,
                12,
                CraftLivePlacementStatus.PlacementComplete,
                30);
            pad2Committed.lastCompletedTransferSerial = 12;
            pad2Committed.slots.top = "ore";
            pad2Committed.slotsRevision = 30;
            pad2Committed.placement.Clear();

            // A separate Pad 1 process cannot find Pad 2's receiver locally,
            // so the standalone fallback can acknowledge the same serial via
            // CompleteTransferPreviewWithoutPlacement. Its newer whole-room
            // snapshot has the acknowledgement but no committed slot.
            CraftLiveRoomState pad1PreviewAck = CreateTransferState(
                4,
                12,
                CraftLivePlacementStatus.PlacementComplete,
                31);
            pad1PreviewAck.lastCompletedTransferSerial = 12;
            pad1PreviewAck.slots.top = string.Empty;
            pad1PreviewAck.slotsRevision = 0;
            pad1PreviewAck.placement.Clear();

            Assert.That(
                CraftLiveRoomTransport.TryBuildTransferReconciledState(
                    pad1PreviewAck,
                    pad2Committed,
                    out CraftLiveRoomState reconciled),
                Is.True,
                "Pad 1's preview acknowledgement must not discard Pad 2's " +
                "authoritative slot commit for the same transfer serial.");
            Assert.That(reconciled.slots.top, Is.EqualTo("ore"));
            Assert.That(
                reconciled.lastCompletedTransferSerial,
                Is.EqualTo(12));
            Assert.That(reconciled.slotsRevision, Is.EqualTo(30));
        }

        [Test]
        public void TransferReconciliation_DoesNotResurrectSlotAfterNewerRemoval()
        {
            CraftLiveRoomState committed = CreateTransferState(
                4,
                12,
                CraftLivePlacementStatus.PlacementComplete,
                30);
            committed.lastCompletedTransferSerial = 12;
            committed.slots.top = "ore";
            committed.slotsRevision = 30;
            committed.placement.Clear();

            CraftLiveRoomState removed = committed.Clone();
            removed.revision = 31;
            removed.updatedAtUnixMs = 3100;
            removed.slots.top = string.Empty;
            removed.slotsRevision = 31;

            Assert.That(
                CraftLiveRoomTransport.TryBuildTransferReconciledState(
                    removed,
                    committed,
                    out _),
                Is.False,
                "An older placement snapshot must not undo a newer explicit " +
                "slot removal.");
            Assert.That(removed.slots.top, Is.Empty);
            Assert.That(removed.slotsRevision, Is.EqualTo(31));
        }

        private static CraftLiveRoomState CreateTransferState(
            int generation,
            int serial,
            CraftLivePlacementStatus status,
            long revision)
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                groupGeneration = generation,
                revision = revision,
                updatedAtUnixMs = revision * 100,
                transferQueueSerial = serial,
                placement = new CraftLivePlacementFlow
                {
                    transferSerial = serial,
                    status = status,
                    materialId = "ore",
                    hasConfirmedSlot = true,
                    confirmedSlot = CraftLiveSlotId.Top,
                    statusChangedAtUnixMs = revision * 100
                }
            };
            state.transferSignal.Capture(state.placement);
            return state;
        }

        [Test]
        public void RetryDelay_UsesBoundedExponentialBackoff()
        {
            Assert.That(
                CraftLiveRoomTransport.CalculateRetryDelay(0, 0.75f, 8f),
                Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(
                CraftLiveRoomTransport.CalculateRetryDelay(3, 0.75f, 8f),
                Is.EqualTo(6f).Within(0.001f));
            Assert.That(
                CraftLiveRoomTransport.CalculateRetryDelay(8, 0.75f, 8f),
                Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void SafeAreaViewport_FitsThreeByFourInsideSafeArea()
        {
            Rect viewport =
                CraftLiveWebPresentation.CalculateCameraViewport(
                    new Rect(0f, 20f, 768f, 984f),
                    new Vector2Int(768, 1024),
                    new Vector2(3f, 4f));

            Assert.That(viewport.x, Is.EqualTo(0.0195f).Within(0.001f));
            Assert.That(viewport.y, Is.EqualTo(20f / 1024f).Within(0.001f));
            Assert.That(viewport.width, Is.EqualTo(0.9609f).Within(0.001f));
            Assert.That(viewport.height, Is.EqualTo(984f / 1024f).Within(0.001f));
        }

        [Test]
        public void SafeAreaViewport_FallsBackWhenAreaIsInvalid()
        {
            Rect viewport =
                CraftLiveWebPresentation.CalculateCameraViewport(
                    Rect.zero,
                    new Vector2Int(768, 1024),
                    new Vector2(3f, 4f));

            Assert.That(viewport, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }

        [TestCase(CraftLiveSessionPhase.StartScreen, 2)]
        [TestCase(CraftLiveSessionPhase.Finished, 2)]
        [TestCase(CraftLiveSessionPhase.Playing, 1)]
        [TestCase(CraftLiveSessionPhase.FinalSelection, 1)]
        public void PowerProfile_ReducesRenderingOnlyOutsideInteractivePlay(
            CraftLiveSessionPhase phase,
            int expectedInterval)
        {
            Assert.That(
                CraftLiveWebPresentation.CalculateRenderFrameInterval(
                    phase,
                    2),
                Is.EqualTo(expectedInterval));
        }

        [Test]
        public void MobilePipeline_UsesBalancedBatterySettings()
        {
            string root = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string mobilePipeline = File.ReadAllText(
                Path.Combine(
                    root,
                    "Assets/Settings/Mobile_RPAsset.asset"));
            string projectSettings = File.ReadAllText(
                Path.Combine(
                    root,
                    "ProjectSettings/ProjectSettings.asset"));

            StringAssert.Contains("m_MSAA: 2", mobilePipeline);
            StringAssert.Contains("m_ShadowDistance: 25", mobilePipeline);
            StringAssert.Contains("runInBackground: 0", projectSettings);
        }

        [Test]
        public void LaunchConfig_ProvidesValidRecoveryDefaults()
        {
            CraftLiveLaunchConfig config =
                ScriptableObject.CreateInstance<CraftLiveLaunchConfig>();
            try
            {
                Assert.That(
                    config.InitialRetryDelaySeconds,
                    Is.GreaterThanOrEqualTo(0.25f));
                Assert.That(
                    config.MaximumRetryDelaySeconds,
                    Is.GreaterThanOrEqualTo(
                        config.InitialRetryDelaySeconds));
                Assert.That(config.CachePendingState, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void QrParser_AcceptsCaseInsensitiveUrlParameter()
        {
            string result = CraftLiveQrScanner.ParseMaterialId(
                "https://example.test/read?MaterialId=ore_attack");
            Assert.That(result, Is.EqualTo("ore_attack"));
        }

        [Test]
        public void ProductionTemplateAndQrBridge_ContainMobileGuards()
        {
            string root = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string template = File.ReadAllText(
                Path.Combine(
                    root,
                    "Assets/WebGLTemplates/CraftLive/index.html"));
            string bridge = File.ReadAllText(
                Path.Combine(
                    root,
                    "Assets/Plugins/WebGL/CraftLiveWebGL.jslib"));

            StringAssert.Contains("viewport-fit=cover", template);
            StringAssert.Contains("devicePixelRatio", template);
            StringAssert.Contains("isIPad ? 1.35 : 2", template);
            StringAssert.Contains("recoverFromStaleBuild", template);
            StringAssert.Contains("unknown data format", template);
            StringAssert.Contains(
                "type === \"error\" && recoverFromStaleBuild(message)",
                template);
            StringAssert.Contains(
                "searchParams.delete(recoveryParameter)",
                template);
            StringAssert.Contains("isSecureContext", bridge);
            StringAssert.Contains("BarcodeDetector", bridge);
        }

        [Test]
        public void ProductionPad1Scene_DisablesStandalonePreviewCompletion()
        {
            string root = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string scene = File.ReadAllText(
                Path.Combine(
                    root,
                    "Assets/Scenes/CraftLive/Pad1_MaterialGallery.unity"));

            StringAssert.Contains(
                "resetAfterStandaloneLaunch: 0",
                scene);
            StringAssert.DoesNotContain(
                "resetAfterStandaloneLaunch: 1",
                scene);
        }

        [Test]
        public void ThreePadSimulator_UsesVisibleClientsAndFirebaseReadback()
        {
            string root = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string simulator = File.ReadAllText(
                Path.Combine(
                    root,
                    "Assets/WebGLTemplates/CraftLive/simulator.html"));

            Assert.That(
                simulator.Split(new[] { "<iframe" },
                    System.StringSplitOptions.None).Length - 1,
                Is.EqualTo(3));
            StringAssert.Contains("simulator", simulator);
            StringAssert.Contains("debugGroup", simulator);
            StringAssert.Contains("/weaponGroups/", simulator);
            StringAssert.Contains("^[1-9][0-9]{4}$", simulator);
        }

        [Test]
        public void RuntimeDiagnostics_HasInspectorEvents()
        {
            BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(
                typeof(CraftLiveRuntimeDiagnostics).GetField(
                    "onSummaryChanged",
                    flags),
                Is.Not.Null);
            Assert.That(
                typeof(CraftLiveRuntimeDiagnostics).GetField(
                    "onHealthyChanged",
                    flags),
                Is.Not.Null);
        }
    }
}
