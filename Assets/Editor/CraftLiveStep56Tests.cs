using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using CraftOrigin.CraftLive;
using CraftOrigin.CraftLiveEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CraftOrigin.CraftLiveTests
{
    public sealed class CraftLiveStep56Tests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in createdObjects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void V3State_MigratesToCurrentSchemaWithEmptyQueue()
        {
            CraftLiveRoomState state =
                CraftLiveRoomState.FromJson(
                    "{\"schemaVersion\":3," +
                    "\"selectedMaterialId\":\"ore\"}");

            Assert.That(
                state.schemaVersion,
                Is.EqualTo(
                    CraftLiveRoomState.CurrentSchemaVersion));
            Assert.That(state.transferQueue, Is.Empty);
        }

        [Test]
        public void ConfirmPlacement_QueuesWithoutChangingSlot()
        {
            CraftLiveMaterialDefinition ore =
                CreateMaterial(
                    "ore",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session =
                CreateSession(CreateCatalog(ore));

            QueueMaterial(
                session,
                ore,
                CraftLiveSlotId.Top);

            Assert.That(
                session.State.transferQueue,
                Has.Count.EqualTo(1));
            Assert.That(session.State.slots.top, Is.Empty);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(
                session.State.IsSlotReserved(
                    CraftLiveSlotId.Top),
                Is.True);
        }

        [Test]
        public void QueuedTransfer_AllowsAnotherMaterialOnAnOpenSlot()
        {
            CraftLiveMaterialDefinition oreA =
                CreateMaterial(
                    "oreA",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveMaterialDefinition oreB =
                CreateMaterial(
                    "oreB",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session =
                CreateSession(CreateCatalog(oreA, oreB));
            QueueMaterial(session, oreA, CraftLiveSlotId.Top);

            Assert.That(
                CraftLivePad1GalleryController.IsWaitingForSingleTransfer(
                    session.State),
                Is.False);
            Assert.That(
                CraftLivePad1GalleryController.CanBeginMaterialPlacement(
                    session.State,
                    oreB),
                Is.True);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
        }

        [Test]
        public void ReservedSlot_CannotBeSelectedAgain()
        {
            CraftLiveMaterialDefinition oreA =
                CreateMaterial(
                    "oreA",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveMaterialDefinition oreB =
                CreateMaterial(
                    "oreB",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session =
                CreateSession(CreateCatalog(oreA, oreB));
            QueueMaterial(
                session,
                oreA,
                CraftLiveSlotId.Top);

            session.SelectMaterial(oreB);
            session.ChoosePlacementSlot(CraftLiveSlotId.Top);

            Assert.That(
                session.State.placement.status,
                Is.EqualTo(
                    CraftLivePlacementStatus.SelectingSlot));
            Assert.That(
                session.State.placement.hasCandidateSlot,
                Is.False);
        }

        [Test]
        public void AllTransferEntryPoint_ProcessesWholeBatchSequentially()
        {
            CraftLiveMaterialDefinition oreA =
                CreateMaterial(
                    "oreA",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveMaterialDefinition oreB =
                CreateMaterial(
                    "oreB",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session =
                CreateSession(CreateCatalog(oreA, oreB));
            QueueMaterial(
                session,
                oreA,
                CraftLiveSlotId.Top);
            QueueMaterial(
                session,
                oreB,
                CraftLiveSlotId.Left);

            Assert.That(
                session.BeginAllQueuedTransfers(),
                Is.True);
            Assert.That(
                session.State.placement.materialId,
                Is.EqualTo("oreA"));
            Assert.That(
                session.State.transferBatchRemaining,
                Is.EqualTo(1));
            Assert.That(session.State.transferQueue, Has.Count.EqualTo(1));
            Assert.That(
                session.State.transferSignal.status,
                Is.EqualTo(CraftLivePlacementStatus.Pad1Loading));
            Assert.That(
                session.State.transferSignal.transferSerial,
                Is.EqualTo(session.State.placement.transferSerial));

            CompleteActiveTransfer(session);

            Assert.That(
                session.State.slots.top,
                Is.EqualTo("oreA"));
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Pad1Loading));
            Assert.That(
                session.State.placement.materialId,
                Is.EqualTo("oreB"));
            Assert.That(session.State.transferQueue, Is.Empty);
            Assert.That(session.State.transferBatchRemaining, Is.Zero);
            Assert.That(
                session.State.transferSignal.status,
                Is.EqualTo(CraftLivePlacementStatus.Pad1Loading));
            Assert.That(
                session.State.transferSignal.transferSerial,
                Is.EqualTo(session.State.placement.transferSerial));

            CompleteActiveTransfer(session);

            Assert.That(
                session.State.slots.left,
                Is.EqualTo("oreB"));
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(session.State.transferQueue, Is.Empty);
            Assert.That(
                session.State.transferSignal.status,
                Is.EqualTo(CraftLivePlacementStatus.PlacementComplete));
            Assert.That(
                session.State.lastCompletedTransferSerial,
                Is.EqualTo(session.State.transferSignal.transferSerial));
        }

        [Test]
        public void AllTransfer_FourItemsCompleteInOrderWithoutOverlap()
        {
            CraftLiveMaterialDefinition[] materials =
            {
                CreateMaterial("oreA", CraftLiveMaterialCategory.Upgrade),
                CreateMaterial("oreB", CraftLiveMaterialCategory.Upgrade),
                CreateMaterial("oreC", CraftLiveMaterialCategory.Upgrade),
                CreateMaterial("oreD", CraftLiveMaterialCategory.Upgrade)
            };
            CraftLiveSlotId[] slots =
            {
                CraftLiveSlotId.Top,
                CraftLiveSlotId.Left,
                CraftLiveSlotId.Right,
                CraftLiveSlotId.Bottom
            };
            CraftLiveSession session = CreateSession(
                CreateCatalog(materials));
            for (int index = 0; index < materials.Length; index++)
            {
                QueueMaterial(session, materials[index], slots[index]);
            }

            Assert.That(session.BeginAllQueuedTransfers(), Is.True);
            for (int index = 0; index < materials.Length; index++)
            {
                Assert.That(
                    session.State.placement.status,
                    Is.EqualTo(CraftLivePlacementStatus.Pad1Loading));
                Assert.That(
                    session.State.placement.materialId,
                    Is.EqualTo(materials[index].MaterialId));
                CompleteActiveTransfer(session);
                Assert.That(
                    session.State.slots.Get(slots[index]),
                    Is.EqualTo(materials[index].MaterialId));
                if (index + 1 < materials.Length)
                {
                    Assert.That(
                        session.State.placement.status,
                        Is.EqualTo(
                            CraftLivePlacementStatus.Pad1Loading));
                    Assert.That(
                        session.State.placement.materialId,
                        Is.EqualTo(materials[index + 1].MaterialId));
                }
            }

            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(session.State.transferQueue, Is.Empty);
            Assert.That(session.State.transferBatchRemaining, Is.Zero);
        }

        [Test]
        public void BatchTrainOffsets_KeepEveryFrameSeparatedInFront()
        {
            float[] halfExtents = { 0.3f, 0.4f, 0.25f, 0.5f };
            float[] offsets =
                CraftLivePad1TransferController.ResolveBatchTrainOffsets(
                    halfExtents,
                    0.05f);

            Assert.That(offsets[0], Is.Zero);
            for (int index = 1; index < offsets.Length; index++)
            {
                float previousFront =
                    offsets[index - 1] + halfExtents[index - 1];
                float currentBack = offsets[index] - halfExtents[index];
                Assert.That(currentBack, Is.GreaterThanOrEqualTo(
                    previousFront + 0.05f - 0.0001f));
                Assert.That(offsets[index], Is.GreaterThan(0f));
            }
        }

        [Test]
        public void PhysicalBatchPresentation_RequiresCompleteMultiItemFormation()
        {
            Assert.That(
                CraftLivePad1TransferController.
                    ShouldUsePhysicalBatchPresentation(true, 3, true),
                Is.True);
            Assert.That(
                CraftLivePad1TransferController.
                    ShouldUsePhysicalBatchPresentation(false, 3, true),
                Is.False);
            Assert.That(
                CraftLivePad1TransferController.
                    ShouldUsePhysicalBatchPresentation(true, 0, true),
                Is.False);
            Assert.That(
                CraftLivePad1TransferController.
                    ShouldUsePhysicalBatchPresentation(true, 3, false),
                Is.False);
        }

        [Test]
        public void CaptureQueuedFormation_IncludesEveryFrameForBatchLaunch()
        {
            GameObject controllerObject =
                new GameObject("BatchCaptureController");
            createdObjects.Add(controllerObject);
            CraftLivePad1TransferController controller =
                controllerObject.AddComponent<
                    CraftLivePad1TransferController>();
            CraftLiveRoomState state = new CraftLiveRoomState();

            FieldInfo visualsField =
                typeof(CraftLivePad1TransferController).GetField(
                    "queueVisuals",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(visualsField, Is.Not.Null);
            Dictionary<int, GameObject> visuals =
                visualsField.GetValue(controller) as
                    Dictionary<int, GameObject>;
            Assert.That(visuals, Is.Not.Null);

            for (int index = 0; index < 4; index++)
            {
                int serial = index + 1;
                state.transferQueue.Add(
                    new CraftLiveTransferQueueEntry(
                        serial,
                        $"ore{serial}",
                        CraftLiveSlotId.Top));
                GameObject frame = new GameObject($"Frame{serial}");
                createdObjects.Add(frame);
                frame.transform.position =
                    new Vector3(index * 0.5f, index * 0.1f, 0f);
                visuals.Add(serial, frame);
            }

            MethodInfo capture =
                typeof(CraftLivePad1TransferController).GetMethod(
                    "CaptureQueuedFormation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null);
            Assert.That(capture.Invoke(controller, new object[] { state }),
                Is.True);

            FieldInfo capturedField =
                typeof(CraftLivePad1TransferController).GetField(
                    "capturedLaunchPoses",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(capturedField, Is.Not.Null);
            object captured = capturedField.GetValue(controller);
            PropertyInfo countProperty =
                captured.GetType().GetProperty("Count");
            Assert.That(countProperty, Is.Not.Null);
            Assert.That(countProperty.GetValue(captured), Is.EqualTo(4));
        }

        [Test]
        public void ParentBatchPreservingWorldPose_KeepsFourFrameFormation()
        {
            GameObject parentObject = new GameObject("BatchRoot");
            createdObjects.Add(parentObject);
            parentObject.transform.SetPositionAndRotation(
                new Vector3(3.4f, -1.2f, 5.6f),
                Quaternion.Euler(17f, 39f, -11f));
            parentObject.transform.localScale = Vector3.one * 1.35f;

            List<GameObject> frames = new List<GameObject>();
            Vector3[] positions =
            {
                new Vector3(-1.8f, 0.4f, 2.1f),
                new Vector3(-0.5f, 0.7f, 2.3f),
                new Vector3(0.8f, 0.2f, 2.5f),
                new Vector3(2.0f, 0.9f, 2.7f)
            };
            Quaternion[] rotations = new Quaternion[positions.Length];
            Vector3[] scales = new Vector3[positions.Length];
            for (int index = 0; index < positions.Length; index++)
            {
                GameObject frame = new GameObject($"Frame{index + 1}");
                createdObjects.Add(frame);
                frame.transform.position = positions[index];
                frame.transform.rotation = Quaternion.Euler(
                    index * 7f,
                    index * 13f,
                    index * -5f);
                frame.transform.localScale = new Vector3(
                    0.7f + index * 0.1f,
                    0.9f + index * 0.08f,
                    1.1f + index * 0.06f);
                rotations[index] = frame.transform.rotation;
                scales[index] = frame.transform.lossyScale;
                frames.Add(frame);
            }

            CraftLivePad1TransferController.ParentBatchPreservingWorldPose(
                parentObject.transform,
                frames);

            for (int index = 0; index < frames.Count; index++)
            {
                Assert.That(
                    Vector3.Distance(
                        frames[index].transform.position,
                        positions[index]),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        frames[index].transform.rotation,
                        rotations[index]),
                    Is.LessThan(0.01f));
                Assert.That(
                    Vector3.Distance(
                        frames[index].transform.lossyScale,
                        scales[index]),
                    Is.LessThan(0.0001f));
                Assert.That(
                    frames[index].transform.parent,
                    Is.SameAs(parentObject.transform));
            }

            for (int index = 1; index < frames.Count; index++)
            {
                float expectedDistance = Vector3.Distance(
                    positions[0],
                    positions[index]);
                float actualDistance = Vector3.Distance(
                    frames[0].transform.position,
                    frames[index].transform.position);
                Assert.That(
                    actualDistance,
                    Is.EqualTo(expectedDistance).Within(0.0001f));
            }
        }

        [Test]
        public void ResetRoomForNextGroup_AdvancesTransferIdentity()
        {
            CraftLiveMaterialDefinition material =
                CreateMaterial("ore", CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session = CreateSession(
                CreateCatalog(material));
            QueueMaterial(session, material, CraftLiveSlotId.Top);
            QueueMaterial(session, material, CraftLiveSlotId.Right);
            Assert.That(session.BeginSingleTransfer(), Is.True);
            int previousGeneration = session.State.groupGeneration;
            int previousQueueSerial = session.State.transferQueueSerial;
            int previousBatchSerial = session.State.transferBatchSerial;

            session.ResetRoomForNextGroup();

            Assert.That(
                session.State.groupGeneration,
                Is.EqualTo(previousGeneration + 1));
            Assert.That(
                session.State.transferQueueSerial,
                Is.EqualTo(previousQueueSerial));
            Assert.That(
                session.State.transferBatchSerial,
                Is.EqualTo(previousBatchSerial));

            session.StartGroup();
            session.State.weaponSelectionConfirmed = true;
            QueueMaterial(session, material, CraftLiveSlotId.Top);
            Assert.That(
                session.State.transferQueue[0].serial,
                Is.GreaterThan(previousQueueSerial));
            Assert.That(session.BeginSingleTransfer(), Is.True);
            Assert.That(
                session.State.transferBatchSerial,
                Is.GreaterThan(previousBatchSerial));
        }

        [Test]
        public void CheckedTransferTransitions_RejectStaleIdentity()
        {
            CraftLiveMaterialDefinition material =
                CreateMaterial("ore", CraftLiveMaterialCategory.Upgrade);
            CraftLiveMaterialDefinition queuedMaterial =
                CreateMaterial("ore-next", CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session = CreateSession(
                CreateCatalog(material, queuedMaterial));
            QueueMaterial(session, material, CraftLiveSlotId.Top);
            QueueMaterial(
                session,
                queuedMaterial,
                CraftLiveSlotId.Left);
            Assert.That(session.BeginSingleTransfer(), Is.True);
            int generation = session.State.groupGeneration;
            int serial = session.State.placement.transferSerial;

            long revision = session.State.revision;
            Assert.That(
                session.MarkTransferLaunching(generation + 1, serial),
                Is.False);
            Assert.That(
                session.MarkTransferLaunching(generation, serial + 1),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(
                session.MarkTransferLaunching(generation, serial),
                Is.True);

            revision = session.State.revision;
            Assert.That(
                session.MarkTransferArriving(generation + 1, serial),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(
                session.MarkTransferArriving(generation, serial),
                Is.True);

            revision = session.State.revision;
            Assert.That(
                session.CompleteCurrentPlacement(generation, serial + 1),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(
                session.CompleteCurrentPlacement(generation, serial),
                Is.True);

            revision = session.State.revision;
            Assert.That(
                session.PublishCurrentStatsToPad3(
                    generation + 1,
                    serial),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(
                session.PublishCurrentStatsToPad3(generation, serial),
                Is.True);

            revision = session.State.revision;
            Assert.That(
                session.ContinueAfterPlacement(generation, serial + 1),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(
                session.ContinueAfterPlacement(generation, serial),
                Is.True);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(session.State.placement.transferSerial, Is.Zero);
            Assert.That(session.State.slots.top, Is.EqualTo("ore"));
            Assert.That(session.State.transferQueue, Has.Count.EqualTo(1));

            revision = session.State.revision;
            Assert.That(
                session.MarkTransferLaunching(generation, serial),
                Is.False);
            Assert.That(
                session.PublishCurrentStatsToPad3(generation, serial),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(session.State.transferQueue, Has.Count.EqualTo(1));
        }

        [Test]
        public void PresentationFailure_CannotBlockAuthoritativePublish()
        {
            CraftLiveSession session = CreateSession(
                CreateCatalog(
                    CreateMaterial(
                        "ore",
                        CraftLiveMaterialCategory.Upgrade)));
            bool localStatePublished = false;
            session.StateChanged += _ =>
                throw new InvalidOperationException(
                    "intentional presentation failure");
            session.LocalStateChanged += _ =>
                localStatePublished = true;
            LogAssert.Expect(
                LogType.Error,
                "CraftLiveSession: local state presentation callback failed; " +
                "remaining callbacks will continue.");
            LogAssert.Expect(
                LogType.Exception,
                new Regex(
                    "InvalidOperationException: intentional presentation failure"));

            session.ShowSingleTransferWarning();

            Assert.That(localStatePublished, Is.True);
        }

        [Test]
        public void Pad2Arrival_CommitsBeforePresentationAndOnlyOnce()
        {
            CraftLiveMaterialDefinition material =
                CreateMaterial(
                    "ore",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session = CreateSession(
                CreateCatalog(material));
            QueueMaterial(
                session,
                material,
                CraftLiveSlotId.Top);
            Assert.That(session.BeginSingleTransfer(), Is.True);
            int generation = session.State.groupGeneration;
            int serial = session.State.placement.transferSerial;
            CraftLiveRoomState arrival = null;
            session.StateChanged += next =>
            {
                if (next.placement.status ==
                    CraftLivePlacementStatus.Pad2Arriving)
                {
                    arrival = next.Clone();
                }
            };

            GameObject receiverObject =
                new GameObject("ImmediateCommitReceiver");
            createdObjects.Add(receiverObject);
            CraftLivePad2TransferReceiver receiver =
                receiverObject.AddComponent<
                    CraftLivePad2TransferReceiver>();
            SetField(receiver, "session", session);
            MethodInfo onEnable =
                typeof(CraftLivePad2TransferReceiver).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(onEnable, Is.Not.Null);
            onEnable.Invoke(receiver, null);

            Assert.That(
                session.MarkTransferLaunching(generation, serial),
                Is.True);
            Assert.That(
                session.MarkTransferArriving(generation, serial),
                Is.True);

            Assert.That(arrival, Is.Not.Null);
            int transferSerial = arrival.placement.transferSerial;
            Assert.That(session.State.slots.top, Is.EqualTo("ore"));
            Assert.That(
                session.State.lastCompletedTransferSerial,
                Is.EqualTo(transferSerial));
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));

            long committedRevision = session.State.revision;
            Assert.That(
                receiver.TryCommitArrivalBeforePresentation(arrival),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(committedRevision));
            Assert.That(session.State.slots.top, Is.EqualTo("ore"));
        }

        [Test]
        public void Bootstrap_ProvisionsOnePlacementWatchdog()
        {
            GameObject system = new GameObject("CraftLiveSystem");
            createdObjects.Add(system);
            system.AddComponent<CraftLiveSession>();
            CraftLiveBootstrap bootstrap =
                system.AddComponent<CraftLiveBootstrap>();
            MethodInfo awake = typeof(CraftLiveBootstrap).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);

            awake.Invoke(bootstrap, null);
            awake.Invoke(bootstrap, null);

            Assert.That(
                system.GetComponents<CraftLivePlacementWatchdog>(),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void PlacementWatchdog_StopsWaitingAfterRecoveryTimeout()
        {
            Assert.That(
                CraftLivePlacementWatchdog.ShouldWaitForReceiver(
                    true,
                    false),
                Is.True);
            Assert.That(
                CraftLivePlacementWatchdog.ShouldWaitForReceiver(
                    true,
                    true),
                Is.False);
        }

        [Test]
        public void ApplyRemoteState_RejectsOldGenerationAndKeepsCounters()
        {
            CraftLiveMaterialDefinition material =
                CreateMaterial("ore", CraftLiveMaterialCategory.Upgrade);
            CraftLiveCatalog catalog = CreateCatalog(material);
            CraftLiveSession session = CreateSession(catalog);
            QueueMaterial(session, material, CraftLiveSlotId.Top);
            Assert.That(session.BeginSingleTransfer(), Is.True);
            session.ResetRoomForNextGroup();

            int generation = session.State.groupGeneration;
            int queueSerial = session.State.transferQueueSerial;
            int batchSerial = session.State.transferBatchSerial;
            long revision = session.State.revision;

            CraftLiveRoomState oldGroup = CraftLiveRoomState.Create(catalog);
            oldGroup.groupGeneration = generation - 1;
            oldGroup.revision = revision + 100;
            oldGroup.transferQueueSerial = 0;
            oldGroup.transferBatchSerial = 0;
            session.ApplyRemoteState(oldGroup);

            Assert.That(session.State.groupGeneration, Is.EqualTo(generation));
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(session.State.transferQueueSerial, Is.EqualTo(queueSerial));
            Assert.That(session.State.transferBatchSerial, Is.EqualTo(batchSerial));

            CraftLiveRoomState currentGroup = session.State.Clone();
            currentGroup.revision = revision + 1;
            currentGroup.transferQueueSerial = 0;
            currentGroup.transferBatchSerial = 0;
            session.ApplyRemoteState(currentGroup);

            Assert.That(session.State.revision, Is.EqualTo(revision + 1));
            Assert.That(session.State.transferQueueSerial, Is.EqualTo(queueSerial));
            Assert.That(session.State.transferBatchSerial, Is.EqualTo(batchSerial));
        }

        [Test]
        public void CheckedPreviewCompletion_RejectsStaleIdentity()
        {
            CraftLiveMaterialDefinition material =
                CreateMaterial("ore", CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session = CreateSession(
                CreateCatalog(material));
            QueueMaterial(session, material, CraftLiveSlotId.Top);
            Assert.That(session.BeginSingleTransfer(), Is.True);
            int generation = session.State.groupGeneration;
            int serial = session.State.placement.transferSerial;
            Assert.That(
                session.MarkTransferLaunching(generation, serial),
                Is.True);
            Assert.That(
                session.MarkTransferArriving(generation, serial),
                Is.True);

            long revision = session.State.revision;
            Assert.That(
                session.CompleteTransferPreviewWithoutPlacement(
                    generation + 1,
                    serial),
                Is.False);
            Assert.That(session.State.revision, Is.EqualTo(revision));
            Assert.That(
                session.CompleteTransferPreviewWithoutPlacement(
                    generation,
                    serial),
                Is.True);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(session.State.slots.top, Is.Empty);
        }

        [Test]
        public void ContinueAfterPlacement_RejectsStaleTransferSerial()
        {
            CraftLiveMaterialDefinition first =
                CreateMaterial("first", CraftLiveMaterialCategory.Upgrade);
            CraftLiveMaterialDefinition second =
                CreateMaterial("second", CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session = CreateSession(
                CreateCatalog(new[] { first, second }));
            QueueMaterial(session, first, CraftLiveSlotId.Top);
            QueueMaterial(session, second, CraftLiveSlotId.Left);

            Assert.That(session.BeginAllQueuedTransfers(), Is.True);
            session.MarkTransferLaunching();
            session.MarkTransferArriving();
            long slotsRevisionBefore = session.State.slotsRevision;
            session.CompleteCurrentPlacement();
            int firstSerial = session.State.placement.transferSerial;

            Assert.That(
                session.State.slotsRevision,
                Is.GreaterThan(slotsRevisionBefore));
            Assert.That(
                session.State.slots.top,
                Is.EqualTo(first.MaterialId));

            Assert.That(
                session.ContinueAfterPlacement(firstSerial + 1),
                Is.False);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.PlacementComplete));
            Assert.That(
                session.ContinueAfterPlacement(firstSerial),
                Is.True);
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Pad1Loading));
            Assert.That(
                session.State.transferQueue,
                Is.Empty);
            Assert.That(
                session.State.placement.materialId,
                Is.EqualTo(second.MaterialId));

            int secondSerial = session.State.placement.transferSerial;
            long secondRevision = session.State.revision;
            Assert.That(
                session.ContinueAfterPlacement(firstSerial),
                Is.False);
            Assert.That(
                session.State.placement.transferSerial,
                Is.EqualTo(secondSerial));
            Assert.That(session.State.revision, Is.EqualTo(secondRevision));
        }

        [Test]
        public void ClearTransferQueue_ReleasesReservedSlots()
        {
            CraftLiveMaterialDefinition ore =
                CreateMaterial(
                    "ore",
                    CraftLiveMaterialCategory.Upgrade);
            CraftLiveSession session =
                CreateSession(CreateCatalog(ore));
            QueueMaterial(
                session,
                ore,
                CraftLiveSlotId.Bottom);

            session.ClearTransferQueue();

            Assert.That(session.State.transferQueue, Is.Empty);
            Assert.That(
                session.State.CanReserveSlot(
                    CraftLiveSlotId.Bottom),
                Is.True);
        }

        [Test]
        public void Pad3Stats_ChangeOnlyWhenPublished()
        {
            CraftLiveMaterialDefinition ore =
                CreateMaterial(
                    "ore",
                    CraftLiveMaterialCategory.Upgrade);
            SetField(
                ore,
                "statModifiers",
                new CraftLiveStats
                {
                    attackRate = 25f
                });
            CraftLiveSession session =
                CreateSession(CreateCatalog(ore));
            QueueMaterial(
                session,
                ore,
                CraftLiveSlotId.Top);
            session.BeginSingleTransfer();
            session.MarkTransferLaunching();
            session.MarkTransferArriving();
            session.CompleteCurrentPlacement();

            Assert.That(
                session.State.displayedStats.attackRate,
                Is.Zero);

            session.PublishCurrentStatsToPad3();

            Assert.That(
                session.State.displayedStats.attackRate,
                Is.EqualTo(25f));
            Assert.That(
                session.State.statusDisplaySerial,
                Is.EqualTo(1));
        }

        [Test]
        public void Pull_IsAvailableOnlyForIdleNonEmptyQueue()
        {
            CraftLiveRoomState state =
                new CraftLiveRoomState();
            state.Normalize(null);
            Assert.That(
                CraftLivePad1TransferController.CanPull(state),
                Is.False);

            state.transferQueue.Add(
                new CraftLiveTransferQueueEntry(
                    1,
                    "ore",
                    CraftLiveSlotId.Top));
            Assert.That(
                CraftLivePad1TransferController.CanPull(state),
                Is.True);

            state.placement.status =
                CraftLivePlacementStatus.Pad1Loading;
            Assert.That(
                CraftLivePad1TransferController.CanPull(state),
                Is.False);
        }

        [Test]
        public void ReleasePull_UsesEndPositionWhenDragCallbackWasSkipped()
        {
            Assert.That(
                CraftLivePad1TransferController.ResolveReleasePull(
                    0f,
                    130f,
                    110f),
                Is.EqualTo(1f));
            Assert.That(
                CraftLivePad1TransferController.ResolveReleasePull(
                    0.7f,
                    20f,
                    110f),
                Is.EqualTo(0.7f).Within(0.0001f));
        }

        [TestCase(0f, 100f, 0f)]
        [TestCase(25f, 100f, 0.25f)]
        [TestCase(120f, 100f, 1f)]
        [TestCase(50f, 0f, 0f)]
        public void StatusTube_NormalizesAndClamps(
            float value,
            float maximum,
            float expected)
        {
            Assert.That(
                CraftLiveStatusTubeView.NormalizeValue(
                    value,
                    maximum),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void Step56Scenes_HaveRequiredComponents()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad1ScenePath,
                scene => Assert.That(
                    FindAll<
                        CraftLivePad1TransferController>(scene),
                    Has.Count.EqualTo(1)));
            WithScene(
                CraftLiveStep2SceneGenerator.Pad2ScenePath,
                scene => Assert.That(
                    FindAll<
                        CraftLivePad2TransferReceiver>(scene),
                    Has.Count.EqualTo(1)));
            WithScene(
                CraftLiveStep2SceneGenerator.Pad3ScenePath,
                scene =>
                {
                    Assert.That(
                        FindAll<CraftLivePad3Controller>(scene),
                        Has.Count.EqualTo(1));
                    Assert.That(
                        FindAll<CraftLiveQrScanner>(scene),
                        Has.Count.EqualTo(1));
                    Assert.That(
                        FindAll<CraftLiveStatusTubeView>(scene),
                        Has.Count.EqualTo(3));
                });
        }

        private void QueueMaterial(
            CraftLiveSession session,
            CraftLiveMaterialDefinition material,
            CraftLiveSlotId slot)
        {
            session.SelectMaterial(material);
            session.ChoosePlacementSlot(slot);
            session.ConfirmPlacement();
        }

        private static void CompleteActiveTransfer(
            CraftLiveSession session)
        {
            session.MarkTransferLaunching();
            session.MarkTransferArriving();
            session.CompleteCurrentPlacement();
            session.ContinueAfterPlacement();
        }

        private CraftLiveMaterialDefinition CreateMaterial(
            string id,
            CraftLiveMaterialCategory category)
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(material, "materialId", id);
            SetField(material, "category", category);
            return material;
        }

        private CraftLiveCatalog CreateCatalog(
            params CraftLiveMaterialDefinition[] materials)
        {
            CraftLiveCatalog catalog =
                ScriptableObject.CreateInstance<
                    CraftLiveCatalog>();
            createdObjects.Add(catalog);
            SetField(
                catalog,
                "materials",
                new List<CraftLiveMaterialDefinition>(
                    materials));
            SetField(
                catalog,
                "weapons",
                new List<CraftLiveWeaponDefinition>());
            return catalog;
        }

        private CraftLiveSession CreateSession(
            CraftLiveCatalog catalog)
        {
            GameObject gameObject =
                new GameObject("Step56Session");
            createdObjects.Add(gameObject);
            CraftLiveSession session =
                gameObject.AddComponent<CraftLiveSession>();
            SetField(session, "catalog", catalog);
            MethodInfo awake =
                typeof(CraftLiveSession).GetMethod(
                    "Awake",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(session, null);
            session.State.weaponSelectionConfirmed = true;
            return session;
        }

        [Test]
        public void StatusTube_SelfPrefabReferenceDoesNotRecurse()
        {
            GameObject tubeObject = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            createdObjects.Add(tubeObject);
            CraftLiveStatusTubeView tube =
                tubeObject.AddComponent<CraftLiveStatusTubeView>();
            SetField(tube, "glassTubePrefab", tubeObject);

            MethodInfo ensureVisual =
                typeof(CraftLiveStatusTubeView).GetMethod(
                    "EnsureVisual",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(ensureVisual, Is.Not.Null);
            Assert.DoesNotThrow(() => ensureVisual.Invoke(tube, null));

            FieldInfo prefabField =
                typeof(CraftLiveStatusTubeView).GetField(
                    "glassTubePrefab",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);
            Assert.That(prefabField.GetValue(tube), Is.Null);
            Assert.That(tubeObject.transform.childCount, Is.LessThan(6));
        }

        [Test]
        public void Pad3Scene_UsesPlacedCameraGlassAndWoodReferences()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad3ScenePath,
                scene =>
                {
                    CraftLivePad3Bindings bindings =
                        FindAll<CraftLivePad3Bindings>(scene)[0];
                    Assert.That(bindings.ReferenceCamera, Is.Not.Null);
                    Assert.That(bindings.WoodPanel, Is.Not.Null);
                    Assert.That(bindings.NoticeBoard, Is.Not.Null);
                    Assert.That(
                        bindings.AttackTubeRoot.GetComponent<Renderer>(),
                        Is.Not.Null);
                    Assert.That(
                        bindings.DefenseTubeRoot.GetComponent<Renderer>(),
                        Is.Not.Null);
                    Assert.That(
                        bindings.EvasionTubeRoot.GetComponent<Renderer>(),
                        Is.Not.Null);

                    foreach (CraftLiveStatusTubeView tube in
                             new[]
                             {
                                 bindings.AttackTubeRoot.GetComponent<
                                     CraftLiveStatusTubeView>(),
                                 bindings.DefenseTubeRoot.GetComponent<
                                     CraftLiveStatusTubeView>(),
                                 bindings.EvasionTubeRoot.GetComponent<
                                     CraftLiveStatusTubeView>()
                             })
                    {
                        FieldInfo prefabField =
                            typeof(CraftLiveStatusTubeView).GetField(
                                "glassTubePrefab",
                                BindingFlags.Instance |
                                BindingFlags.NonPublic);
                        Assert.That(
                            prefabField.GetValue(tube),
                            Is.Not.SameAs(tube.gameObject));
                    }
                });
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static List<T> FindAll<T>(Scene scene)
            where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in
                     scene.GetRootGameObjects())
            {
                results.AddRange(
                    root.GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static void WithScene(
            string scenePath,
            System.Action<Scene> action)
        {
            SceneSetup[] setup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
                action(scene);
            }
            finally
            {
                bool canRestore = false;
                foreach (SceneSetup sceneSetup in setup)
                {
                    if (sceneSetup.isLoaded &&
                        sceneSetup.isActive)
                    {
                        canRestore = true;
                        break;
                    }
                }

                if (canRestore)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(
                        setup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }
    }
}
