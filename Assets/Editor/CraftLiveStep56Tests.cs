using System.Collections.Generic;
using System.Reflection;
using CraftOrigin.CraftLive;
using CraftOrigin.CraftLiveEditor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        public void AllTransfer_ProcessesQueuedItemsInOrder()
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

            CompleteActiveTransfer(session);

            Assert.That(
                session.State.slots.top,
                Is.EqualTo("oreA"));
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(
                    CraftLivePlacementStatus.Pad1Loading));
            Assert.That(
                session.State.placement.materialId,
                Is.EqualTo("oreB"));

            CompleteActiveTransfer(session);

            Assert.That(
                session.State.slots.left,
                Is.EqualTo("oreB"));
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(session.State.transferQueue, Is.Empty);
        }

        [Test]
        public void AllTransfer_FourItemsCompleteWithoutStopping()
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
        public void BatchGridOffsets_FormCenteredTwoByTwoGroup()
        {
            Vector2[] offsets = new Vector2[4];
            for (int index = 0; index < offsets.Length; index++)
            {
                offsets[index] =
                    CraftLivePad1TransferController.ResolveBatchGridOffset(
                        index,
                        4,
                        2f,
                        4f);
            }

            Assert.That(offsets[0], Is.EqualTo(new Vector2(-1f, 2f)));
            Assert.That(offsets[1], Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(offsets[2], Is.EqualTo(new Vector2(-1f, -2f)));
            Assert.That(offsets[3], Is.EqualTo(new Vector2(1f, -2f)));
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
            session.CompleteCurrentPlacement();
            int firstSerial = session.State.placement.transferSerial;

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
                session.State.placement.materialId,
                Is.EqualTo(second.MaterialId));
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
