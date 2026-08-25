using System;
using System.Collections.Generic;
using System.Reflection;
using CraftOrigin.CraftLive;
using CraftOrigin.CraftLiveEditor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CraftOrigin.CraftLiveTests
{
    public sealed class CraftLiveStep78Tests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in created)
            {
                if (value != null)
                {
                    Object.DestroyImmediate(value);
                }
            }
            created.Clear();
        }

        [Test]
        public void V4State_MigratesToV5()
        {
            CraftLiveRoomState state =
                CraftLiveRoomState.FromJson("{\"schemaVersion\":4}");
            Assert.That(state.schemaVersion, Is.EqualTo(5));
            Assert.That(state.completedWeapons, Is.Empty);
        }

        [Test]
        public void WeaponCode_IsStable()
        {
            CraftLiveResultState result = new CraftLiveResultState
            {
                weaponId = "weapon_bigsword_sword",
                elementEffect = new CraftLiveElementEffect
                {
                    type = CraftLiveElementType.Fire
                },
                skillEffect = new CraftLiveSkillEffect
                {
                    type = CraftLiveSkillType.DoubleStrike
                },
                attackMaterialCount = 2,
                defenseMaterialCount = 1,
                evasionMaterialCount = 1
            };
            string first = CraftLiveWeaponCode.Generate(result);
            Assert.That(
                first,
                Is.EqualTo("2FD211"));
            Assert.That(first, Is.EqualTo(
                CraftLiveWeaponCode.Generate(result)));
        }

        [TestCase("2NN400", "weapon_bigsword_sword")]
        [TestCase("3NN040", "weapon_fude_staff")]
        [TestCase("4NN004", "weapon_katate_sword")]
        [TestCase("5FN000", "weapon_kaziki")]
        [TestCase("6CL000", "weapon_kobushi")]
        [TestCase("7TD000", "weapon_pikopiko_sword")]
        [TestCase("8NH121", "weapon_staff")]
        [TestCase("9NB211", "weapon_rapier")]
        public void WeaponCode_FirstCharacterIdentifiesWeapon(
            string code,
            string expectedWeaponId)
        {
            Assert.That(
                CraftLiveWeaponCode.TryGetWeaponId(
                    code,
                    out string weaponId),
                Is.True);
            Assert.That(weaponId, Is.EqualTo(expectedWeaponId));
        }

        [TestCase("")]
        [TestCase("2FN40")]
        [TestCase("2ZN400")]
        [TestCase("2FX400")]
        [TestCase("2FN500")]
        [TestCase("2FN441")]
        [TestCase("5FN100")]
        [TestCase("XFN400")]
        public void WeaponCode_RejectsInvalidCode(string code)
        {
            Assert.That(
                CraftLiveWeaponCode.TryGetWeaponId(
                    code,
                    out string weaponId),
                Is.False);
            Assert.That(weaponId, Is.Empty);
        }

        [Test]
        public void WeaponCode_DecodesAllCompositionData()
        {
            Assert.That(
                CraftLiveWeaponCode.TryDecode(
                    "2FD211",
                    out CraftLiveWeaponCodeData data),
                Is.True);
            Assert.That(data.weaponId,
                Is.EqualTo("weapon_bigsword_sword"));
            Assert.That(data.attribute,
                Is.EqualTo(CraftLiveElementType.Fire));
            Assert.That(data.skill,
                Is.EqualTo(CraftLiveSkillType.DoubleStrike));
            Assert.That(data.attackMaterialCount, Is.EqualTo(2));
            Assert.That(data.defenseMaterialCount, Is.EqualTo(1));
            Assert.That(data.evasionMaterialCount, Is.EqualTo(1));
        }

        [Test]
        public void WeaponCode_SecretWeaponForcesUpgradeCountsToZero()
        {
            CraftLiveResultState result = new CraftLiveResultState
            {
                weaponId = CraftLiveCalculator.SecretPikopikoWeaponId,
                attackMaterialCount = 4
            };
            Assert.That(
                CraftLiveWeaponCode.Generate(result),
                Is.EqualTo("7NN000"));
        }

        [TestCase(0f, "00:00")]
        [TestCase(61f, "01:01")]
        [TestCase(299.1f, "05:00")]
        public void Timer_Formats(float seconds, string expected)
        {
            Assert.That(
                CraftLiveSessionTimerController.FormatTime(seconds),
                Is.EqualTo(expected));
        }

        [Test]
        public void HammerPasses_AddCompletedWeapon()
        {
            TestSetup setup = CreateValidSetup();
            Assert.That(setup.session.StartSynthesis(), Is.True);
            for (int i = 0;
                 i < setup.rules.RequiredHammerPasses;
                 i++)
            {
                setup.session.RegisterHammerPass();
            }

            Assert.That(
                setup.session.State.craft.status,
                Is.EqualTo(CraftLiveCraftStatus.Complete));
            Assert.That(
                setup.session.State.completedWeapons,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void ExpireSession_DuringSynthesisCompletesWeaponOnce()
        {
            TestSetup setup = CreateValidSetup();
            Assert.That(setup.session.StartSynthesis(), Is.True);
            setup.session.RegisterHammerPass(1f, false);

            setup.session.ExpireSession();

            Assert.That(
                setup.session.State.sessionPhase,
                Is.EqualTo(CraftLiveSessionPhase.FinalSelection));
            Assert.That(
                setup.session.State.craft.status,
                Is.EqualTo(CraftLiveCraftStatus.Complete));
            Assert.That(
                setup.session.State.craft.completionPresentationReady,
                Is.True);
            Assert.That(
                setup.session.State.completedWeapons,
                Has.Count.EqualTo(1));
            Assert.That(
                setup.session.RegisterHammerPass(1f, false),
                Is.False);

            setup.session.CompleteSynthesis();
            Assert.That(
                setup.session.State.completedWeapons,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void ExpireSession_DuringCompletionFlashRevealsExistingResult()
        {
            TestSetup setup = CreateValidSetup();
            Assert.That(setup.session.StartSynthesis(), Is.True);
            setup.session.CompleteSynthesis(true);
            Assert.That(
                setup.session.State.craft.completionPresentationReady,
                Is.False);

            setup.session.ExpireSession();

            Assert.That(
                setup.session.State.sessionPhase,
                Is.EqualTo(CraftLiveSessionPhase.FinalSelection));
            Assert.That(
                setup.session.State.craft.completionPresentationReady,
                Is.True);
            Assert.That(
                setup.session.State.completedWeapons,
                Has.Count.EqualTo(1));

            setup.session.RevealCompletionPresentation();
            Assert.That(
                setup.session.State.completedWeapons,
                Has.Count.EqualTo(1));
        }

        [Test]
        public void NextWeapon_PreservesHistory()
        {
            TestSetup setup = CreateValidSetup();
            setup.session.StartSynthesis();
            setup.session.CompleteSynthesis();
            setup.session.BeginNextWeapon();
            Assert.That(
                setup.session.State.completedWeapons,
                Has.Count.EqualTo(1));
            Assert.That(
                setup.session.State.HasAnyPlacedMaterial(),
                Is.False);
            Assert.That(
                setup.session.State.weaponSelectionConfirmed,
                Is.False);
        }

        [Test]
        public void FinalSelection_IssuesCode()
        {
            TestSetup setup = CreateValidSetup();
            setup.session.StartSynthesis();
            setup.session.CompleteSynthesis();
            int serial = setup.session.State.result.resultSerial;
            setup.session.ExpireSession();
            Assert.That(
                setup.session.SelectFinalWeapon(serial),
                Is.True);
            Assert.That(
                setup.session.State.sessionPhase,
                Is.EqualTo(CraftLiveSessionPhase.Finished));
            Assert.That(
                setup.session.State.finalWeaponCode,
                Does.Match("^[2-9][NFCT][NLDHB][0-4]{3}$"));
        }

        [Test]
        public void LiquidPath_HasEndpoints()
        {
            Vector3 start = new Vector3(-2f, 1f, 0f);
            Vector3 end = new Vector3(3f, -1f, 0f);
            Assert.That(
                CraftLiveLiquidFlowController.EvaluatePath(
                    start, end, 0f, 1f),
                Is.EqualTo(start));
            Assert.That(
                Vector3.Distance(
                    CraftLiveLiquidFlowController.EvaluatePath(
                        start, end, 1f, 1f),
                    end),
                Is.LessThan(0.0001f));
        }

        [Test]
        public void LiquidPath_UsesWorkbenchSurfaceNormal()
        {
            Vector3 point =
                CraftLiveLiquidFlowController.EvaluatePath(
                    Vector3.zero,
                    Vector3.right,
                    0.25f,
                    1f,
                    Vector3.forward);
            Assert.That(point.z, Is.GreaterThan(0.7f));
            Assert.That(point.y, Is.EqualTo(0f).Within(0.0001f));
        }

        [TestCase(40, 0f, 0)]
        [TestCase(40, 0.5f, 20)]
        [TestCase(40, 1f, 40)]
        public void LiquidTrail_RemainsFilledBehindFlow(
            int samples,
            float progress,
            int expectedVisible)
        {
            Assert.That(
                CraftLiveLiquidFlowController.VisibleTrailSegments(
                    samples,
                    progress),
                Is.EqualTo(expectedVisible));
        }

        [Test]
        public void LiquidTrailDimensions_ApplyPadScaleExactlyOnce()
        {
            Vector2 dimensions =
                CraftLiveLiquidFlowController.ScaleTrailDimensionsToWorld(
                    0.2f,
                    0.05f,
                    0.22f);

            Assert.That(dimensions.x, Is.EqualTo(0.044f).Within(0.0001f));
            Assert.That(dimensions.y, Is.EqualTo(0.011f).Within(0.0001f));
        }

        [TestCase(0f, -0.5f, 0f)]
        [TestCase(0.5f, -0.25f, 0.5f)]
        [TestCase(1f, 0f, 1f)]
        public void AuthoredLiquidGuide_RevealsFromStartWithoutChangingPose(
            float progress,
            float expectedCenterOffset,
            float expectedLength)
        {
            Vector2 reveal =
                CraftLiveLiquidFlowController.ResolveGuideChannelReveal(
                    progress);

            Assert.That(
                reveal.x,
                Is.EqualTo(expectedCenterOffset).Within(0.0001f));
            Assert.That(
                reveal.y,
                Is.EqualTo(expectedLength).Within(0.0001f));
        }

        [TestCase(true, 4, 3, "upgrade_attack", true)]
        [TestCase(true, 4, 4, "upgrade_attack", false)]
        [TestCase(false, 4, 3, "upgrade_attack", false)]
        [TestCase(true, 0, -1, "upgrade_attack", false)]
        [TestCase(true, 4, 3, "", false)]
        public void Pad1RegistrationArrival_PlaysOnceAfterNewRegistration(
            bool built,
            int registrationSerial,
            int handledSerial,
            string materialId,
            bool expected)
        {
            Assert.That(
                CraftLivePad1GalleryController.
                    ShouldPlayRegistrationArrival(
                        built,
                        registrationSerial,
                        handledSerial,
                        materialId),
                Is.EqualTo(expected));
        }

        [Test]
        public void Pad2ReceiverExit_ReleasesOnlyMatchingCompletedPlacement()
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                groupGeneration = 7,
                placement = new CraftLivePlacementFlow
                {
                    transferSerial = 12,
                    status = CraftLivePlacementStatus.PlacementComplete
                }
            };

            Assert.That(
                CraftLivePad2TransferReceiver.
                    ShouldReleaseCompletedPlacement(state, 7, 12),
                Is.True);
            Assert.That(
                CraftLivePad2TransferReceiver.
                    ShouldReleaseCompletedPlacement(state, 7, 11),
                Is.False);
            Assert.That(
                CraftLivePad2TransferReceiver.
                    ShouldReleaseCompletedPlacement(state, 6, 12),
                Is.False);

            state.placement.status =
                CraftLivePlacementStatus.Pad2Arriving;
            Assert.That(
                CraftLivePad2TransferReceiver.
                    ShouldReleaseCompletedPlacement(state, 7, 12),
                Is.False);
        }

        [Test]
        public void ExternallySequencedFlow_CannotAutoStartCompetingRoutine()
        {
            Assert.That(
                CraftLiveLiquidFlowController.ShouldStartAutomaticFlow(
                    true,
                    CraftLivePlacementStatus.PlacementComplete,
                    3,
                    12,
                    2,
                    11,
                    false),
                Is.False);
            Assert.That(
                CraftLiveLiquidFlowController.ShouldStartAutomaticFlow(
                    false,
                    CraftLivePlacementStatus.PlacementComplete,
                    3,
                    12,
                    2,
                    11,
                    false),
                Is.True);
            Assert.That(
                CraftLiveLiquidFlowController.ShouldStartAutomaticFlow(
                    false,
                    CraftLivePlacementStatus.PlacementComplete,
                    3,
                    12,
                    3,
                    12,
                    false),
                Is.False);
        }

        [Test]
        public void LocalPad2_QueuedMaterialAutoStarts()
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                sessionPhase = CraftLiveSessionPhase.Playing,
                transferQueue = new List<CraftLiveTransferQueueEntry>
                {
                    new CraftLiveTransferQueueEntry(
                        1,
                        "ore",
                        CraftLiveSlotId.Top)
                }
            };
            state.placement.Clear();

            Assert.That(
                CraftLivePad2TransferReceiver
                    .CanAutoStartLocalArrival(
                        state,
                        CraftLiveRole.WorkbenchPad),
                Is.True);
            Assert.That(
                CraftLivePad2TransferReceiver
                    .CanAutoStartLocalArrival(
                        state,
                        CraftLiveRole.MaterialPad),
                Is.False);
        }

        [Test]
        public void ArrivalStart_RemainsAboveWorkbench()
        {
            Vector3 arrival = new Vector3(0f, 5f, 0f);
            Vector3 result =
                CraftLivePad2TransferReceiver
                    .ResolveArrivalLocalPosition(
                        arrival,
                        new Vector3(1.47f, -2.12f, 0f));

            Assert.That(result.y, Is.EqualTo(5f));
            Assert.That(result.z, Is.EqualTo(0f));
            Assert.That(result.x, Is.GreaterThan(0f));
            Assert.That(result.x, Is.LessThanOrEqualTo(0.55f));
        }

        [Test]
        public void PlacedMaterial_IsLiftedTowardWorkbenchCamera()
        {
            Vector3 result =
                CraftLivePad2TransferReceiver
                    .ResolveDisplayLocalPosition(
                        new Vector3(-1.47f, 2.12f, 0f),
                        0.45f);

            Assert.That(result.x, Is.EqualTo(-1.47f));
            Assert.That(result.y, Is.EqualTo(2.12f));
            Assert.That(result.z, Is.EqualTo(-0.45f));
        }

        [Test]
        public void PlacedMaterialGuide_DefinesExactPositionAndDepth()
        {
            Vector3 guidePosition =
                new Vector3(-1.47f, -2.12f, 0.37f);
            Vector3 result =
                CraftLivePad2TransferReceiver
                    .ResolveDisplayLocalPosition(
                        guidePosition,
                        0.45f,
                        true);

            Assert.That(result, Is.EqualTo(guidePosition));
        }

        [Test]
        public void Pad2Material_RestsAuthoredBottomOnWorkbenchSurface()
        {
            GameObject visual = new GameObject("Visual");
            created.Add(visual);
            GameObject contentObject = new GameObject("VisualContent");
            contentObject.transform.SetParent(visual.transform, false);
            GameObject model = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            model.transform.SetParent(contentObject.transform, false);

            Type utilityType = typeof(CraftLivePad2TransferReceiver)
                .Assembly.GetType(
                    "CraftOrigin.CraftLive.CraftLiveRuntimeVisualUtility");
            Assert.That(utilityType, Is.Not.Null);
            MethodInfo fit = utilityType.GetMethod(
                "FitAndCenter",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(fit, Is.Not.Null);

            bool fitted = (bool)fit.Invoke(
                null,
                new object[]
                {
                    contentObject.transform,
                    1f,
                    true,
                    0f,
                    true,
                    true
                });

            Assert.That(fitted, Is.True);
            Vector3 placedUp =
                contentObject.transform.TransformDirection(Vector3.up);
            Assert.That(
                Vector3.Dot(placedUp.normalized, Vector3.back),
                Is.GreaterThan(0.999f));
        }

        [TestCase(0, 6, 6)]
        [TestCase(3, 6, 3)]
        [TestCase(7, 6, 0)]
        public void HammerRemaining_Clamps(
            int completed, int required, int expected)
        {
            Assert.That(
                CraftLiveHammerSynthesisController.PassesRemaining(
                    completed, required),
                Is.EqualTo(expected));
        }

        [Test]
        public void HammerStrikeInput_OnlyAdvancesAlongGuideDirection()
        {
            Assert.That(
                CraftLiveHammerStrikePresentation.ProjectInputDelta(
                    new Vector2(0f, -50f),
                    Vector2.down),
                Is.EqualTo(50f).Within(0.0001f));
            Assert.That(
                CraftLiveHammerStrikePresentation.ProjectInputDelta(
                    new Vector2(0f, 50f),
                    Vector2.down),
                Is.EqualTo(-50f).Within(0.0001f));
            Assert.That(
                CraftLiveHammerStrikePresentation.ProjectInputDelta(
                    new Vector2(50f, 0f),
                    Vector2.down),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [TestCase(CraftLiveCraftStatus.Editing,
            CraftLivePlacementStatus.Idle, false)]
        [TestCase(CraftLiveCraftStatus.Mixing,
            CraftLivePlacementStatus.Idle, true)]
        [TestCase(CraftLiveCraftStatus.Mixing,
            CraftLivePlacementStatus.SelectingSlot, false)]
        [TestCase(CraftLiveCraftStatus.Mixing,
            CraftLivePlacementStatus.ConfirmingSlot, false)]
        public void HammerInput_NeverCoversMaterialPlacement(
            CraftLiveCraftStatus craftStatus,
            CraftLivePlacementStatus placementStatus,
            bool expected)
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                sessionPhase = CraftLiveSessionPhase.Playing
            };
            state.craft.status = craftStatus;
            state.placement.status = placementStatus;

            Assert.That(
                CraftLiveHammerSynthesisController
                    .ShouldShowHammerInput(state),
                Is.EqualTo(expected));
        }

        [Test]
        public void Scenes_HaveStep78Components()
        {
            AssertSceneCount<CraftLiveSessionTimerController>(
                CraftLiveStep2SceneGenerator.BootstrapScenePath, 1);
            AssertSceneCount<CraftLiveLiquidFlowController>(
                CraftLiveStep2SceneGenerator.Pad2ScenePath, 1);
            AssertSceneCount<CraftLiveHammerSynthesisController>(
                CraftLiveStep2SceneGenerator.Pad2ScenePath, 1);
            AssertSceneCount<CraftLiveHammerStrikePresentation>(
                CraftLiveStep2SceneGenerator.Pad2ScenePath, 1);
            AssertSceneCount<CraftLivePad2ResultController>(
                CraftLiveStep2SceneGenerator.Pad2ScenePath, 1);
            AssertSceneCount<CraftLiveHologramView>(
                CraftLiveStep2SceneGenerator.Pad4ScenePath, 1);
            AssertSceneCount<CraftLivePad4Controller>(
                CraftLiveStep2SceneGenerator.Pad4ScenePath, 1);
        }

        private TestSetup CreateValidSetup()
        {
            CraftLiveMaterialDefinition attribute =
                CreateMaterial(
                    "fire", CraftLiveMaterialCategory.Attribute);
            SetField(attribute, "attributeId", "fire");
            CraftLiveMaterialDefinition skill =
                CreateMaterial(
                    "luck", CraftLiveMaterialCategory.Skill);
            SetField(skill, "skillId", "luck");
            CraftLiveWeaponDefinition weapon =
                ScriptableObject.CreateInstance<
                    CraftLiveWeaponDefinition>();
            created.Add(weapon);
            SetField(weapon, "weaponId", "sword");
            CraftLiveCatalog catalog =
                ScriptableObject.CreateInstance<CraftLiveCatalog>();
            created.Add(catalog);
            SetField(catalog, "materials",
                new List<CraftLiveMaterialDefinition>
                {
                    attribute, skill
                });
            SetField(catalog, "weapons",
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveRules rules =
                ScriptableObject.CreateInstance<CraftLiveRules>();
            created.Add(rules);
            GameObject root = new GameObject("Step78Session");
            created.Add(root);
            CraftLiveSession session =
                root.AddComponent<CraftLiveSession>();
            SetField(session, "catalog", catalog);
            SetField(session, "rules", rules);
            InvokeAwake(session);
            CraftLiveRoomState state =
                CraftLiveRoomState.Create(catalog);
            state.weaponSelectionConfirmed = true;
            state.slots.attribute = "fire";
            state.slots.skill = "luck";
            session.ApplyRemoteState(state);
            return new TestSetup(session, rules);
        }

        private CraftLiveMaterialDefinition CreateMaterial(
            string id, CraftLiveMaterialCategory category)
        {
            CraftLiveMaterialDefinition value =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            created.Add(value);
            SetField(value, "materialId", id);
            SetField(value, "category", category);
            return value;
        }

        private static void InvokeAwake(CraftLiveSession session)
        {
            MethodInfo method = typeof(CraftLiveSession).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(session, null);
        }

        private static void SetField(
            object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static void AssertSceneCount<T>(
            string path, int expected) where T : Component
        {
            SceneSetup[] setup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    path, OpenSceneMode.Single);
                int count = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    count += root.GetComponentsInChildren<T>(true).Length;
                }
                Assert.That(count, Is.EqualTo(expected));
            }
            finally
            {
                bool restore = false;
                foreach (SceneSetup item in setup)
                {
                    restore |= item.isLoaded && item.isActive;
                }
                if (restore)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }
            }
        }

        private readonly struct TestSetup
        {
            public CraftLiveSession session { get; }
            public CraftLiveRules rules { get; }
            public TestSetup(
                CraftLiveSession session,
                CraftLiveRules rules)
            {
                this.session = session;
                this.rules = rules;
            }
        }
    }
}
