using System;
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
        public void LegacyState_MigratesToCurrentSchema()
        {
            CraftLiveRoomState state =
                CraftLiveRoomState.FromJson("{\"schemaVersion\":4}");
            Assert.That(
                state.schemaVersion,
                Is.EqualTo(CraftLiveRoomState.CurrentSchemaVersion));
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

        [TestCase(401L)]
        [TestCase(403L)]
        public void GroupPublishing_StopsCandidateScanOnRulesDenial(
            long status)
        {
            Assert.That(
                CraftLiveRoomTransport.IsFirebaseAuthorizationFailure(status),
                Is.True);
            Assert.That(
                CraftLiveRoomTransport.DescribeGroupPublishFailure(
                    status,
                    "Unauthorized"),
                Does.Contain("Database Rules"));
        }

        [Test]
        public void GroupIssuePanel_ExplainsRulesFailure()
        {
            Assert.That(
                CraftLivePad2ResultController.BuildGroupIssueStatus(
                    "Firebase Database Rulesが読み書きを拒否しています。"),
                Does.Contain("権限エラー"));
        }

        [TestCase(CraftLiveRole.Auto, false)]
        [TestCase(CraftLiveRole.MaterialPad, false)]
        [TestCase(CraftLiveRole.WorkbenchPad, true)]
        [TestCase(CraftLiveRole.QrPad, false)]
        [TestCase(CraftLiveRole.HologramPad, false)]
        public void StartButton_IsAuthoritativeOnlyOnPad2(
            CraftLiveRole role,
            bool expected)
        {
            Assert.That(
                CraftLivePad2ResultController.IsAuthoritativeStartRole(role),
                Is.EqualTo(expected));
        }

        [Test]
        public void DefaultRules_UseRealRpgMaximumStat()
        {
            CraftLiveRules rules =
                ScriptableObject.CreateInstance<CraftLiveRules>();
            created.Add(rules);
            Assert.That(rules.MaximumStat, Is.EqualTo(1000f));
            Assert.That(
                rules.SessionDurationSeconds,
                Is.EqualTo(270f));
            Assert.That(
                CraftLiveStatusTubeView.NormalizeValue(500f, rules.MaximumStat),
                Is.EqualTo(0.5f));
        }

        [TestCase("KatateSword.asset", 500f, 300f, 500f)]
        [TestCase("bigswordSword.asset", 700f, 400f, 300f)]
        [TestCase("PikopikoSword 1.asset", 300f, 300f, 800f)]
        [TestCase("YariThrust.asset", 500f, 200f, 600f)]
        [TestCase("KobushiThrust 1.asset", 200f, 200f, 200f)]
        [TestCase("BareHands.asset", 200f, 200f, 200f)]
        [TestCase("KazikiThrust 1.asset", 1000f, 100f, 100f)]
        [TestCase("TueStaff.asset", 400f, 400f, 400f)]
        [TestCase("FudeThrust 1.asset", 450f, 300f, 500f)]
        public void WeaponBaseStats_MatchRealRpgDefinitions(
            string assetName,
            float attack,
            float defense,
            float evasion)
        {
            CraftLiveWeaponDefinition weapon =
                AssetDatabase.LoadAssetAtPath<CraftLiveWeaponDefinition>(
                    $"Assets/Buki/Weapons/{assetName}");
            Assert.That(weapon, Is.Not.Null);
            Assert.That(weapon.BaseStats.attackRate, Is.EqualTo(attack));
            Assert.That(weapon.BaseStats.defenseRate, Is.EqualTo(defense));
            Assert.That(weapon.BaseStats.evasionRate, Is.EqualTo(evasion));
        }

        [Test]
        public void DefaultCatalog_SeparatesFistFromSecretBareHands()
        {
            CraftLiveCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CraftLiveCatalog>(
                    "Assets/CraftLiveData/DefaultCraftLiveCatalog.asset");
            Assert.That(catalog, Is.Not.Null);

            CraftLiveWeaponDefinition fist =
                catalog.FindWeapon("weapon_kobushi");
            Assert.That(fist, Is.Not.Null);
            Assert.That(fist.DisplayName, Is.EqualTo("こぶし"));
            Assert.That(fist.VisibleInSelection, Is.True);
            Assert.That(fist.HidePresentationModel, Is.False);
            Assert.That(fist.WorkbenchPrefab, Is.Not.Null);
            Assert.That(
                CraftLiveCalculator.IsSecretWeaponId(fist.WeaponId),
                Is.False);

            CraftLiveWeaponDefinition bareHands =
                catalog.FindWeapon(
                    CraftLiveCalculator.SecretBareHandsWeaponId);
            Assert.That(bareHands, Is.Not.Null);
            Assert.That(bareHands.DisplayName, Is.EqualTo("素手"));
            Assert.That(bareHands.VisibleInSelection, Is.False);
            Assert.That(bareHands.HidePresentationModel, Is.True);
            Assert.That(bareHands.WorkbenchPrefab, Is.Null);
        }

        [TestCase("attack.asset", 50f, 0f, 0f, "最大1000")]
        [TestCase("defence.asset", 0f, 50f, 0f, "最大1000")]
        [TestCase("Kaihi.asset", 0f, 0f, 50f, "最大1000")]
        public void UpgradeMaterials_MatchRealRpgFiftyPointBonus(
            string assetName,
            float attack,
            float defense,
            float evasion,
            string descriptionPart)
        {
            CraftLiveMaterialDefinition material =
                AssetDatabase.LoadAssetAtPath<CraftLiveMaterialDefinition>(
                    $"Assets/CraftLiveData/Sozai/{assetName}");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.StatModifiers.attackRate, Is.EqualTo(attack));
            Assert.That(material.StatModifiers.defenseRate, Is.EqualTo(defense));
            Assert.That(material.StatModifiers.evasionRate, Is.EqualTo(evasion));
            Assert.That(material.Description, Does.Contain(descriptionPart));
        }

        [TestCase("fire.asset", "10%", 100f)]
        [TestCase("freeze.asset", "50%", 50f)]
        [TestCase("lighting.asset", "50%", 50f)]
        public void AttributeDescriptions_MatchRealRpgRules(
            string assetName,
            string descriptionPart,
            float activationChance)
        {
            CraftLiveMaterialDefinition material =
                AssetDatabase.LoadAssetAtPath<CraftLiveMaterialDefinition>(
                    $"Assets/CraftLiveData/Sozai/{assetName}");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.Description, Does.Contain(descriptionPart));
            Assert.That(
                material.ElementEffect.activationChancePercent,
                Is.EqualTo(activationChance));
        }

        [TestCase("Heal.asset", 100f, 5f)]
        [TestCase("DoubleStrike.asset", 30f, 0f)]
        [TestCase("luck.asset", 25f, 0f)]
        [TestCase("Inochi.asset", 100f, 100f)]
        public void SkillDescriptions_MatchRealRpgRules(
            string assetName,
            float activationChance,
            float primaryValue)
        {
            CraftLiveMaterialDefinition material =
                AssetDatabase.LoadAssetAtPath<CraftLiveMaterialDefinition>(
                    $"Assets/CraftLiveData/Sozai/{assetName}");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.Description, Is.Not.Empty);
            Assert.That(
                material.SkillEffect.activationChancePercent,
                Is.EqualTo(activationChance));
            Assert.That(
                material.SkillEffect.primaryValue,
                Is.EqualTo(primaryValue));
        }

        [TestCase("2NN400", "weapon_bigsword_sword")]
        [TestCase("1NN000", "weapon_bare_hands")]
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

        [Test]
        public void WeaponCode_BareHandsHasOwnSecretCode()
        {
            CraftLiveResultState result = new CraftLiveResultState
            {
                weaponId = CraftLiveCalculator.SecretBareHandsWeaponId,
                attackMaterialCount = 4
            };
            Assert.That(
                CraftLiveWeaponCode.Generate(result),
                Is.EqualTo("1NN000"));
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
        public void ExpireSession_WithoutCompletedWeapon_AddsDeterministicEmergencyBigSword()
        {
            CraftLiveCatalog catalog = AssetDatabase.LoadAssetAtPath<
                CraftLiveCatalog>(
                "Assets/CraftLiveData/DefaultCraftLiveCatalog.asset");
            CraftLiveRules rules = AssetDatabase.LoadAssetAtPath<
                CraftLiveRules>(
                "Assets/CraftLiveData/DefaultCraftLiveRules.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(rules, Is.Not.Null);

            CraftLiveResultState first = ExpireEmptyEmergencySession(
                catalog,
                rules,
                "rescue-room",
                42,
                4102444800000L);
            CraftLiveResultState second = ExpireEmptyEmergencySession(
                catalog,
                rules,
                "rescue-room",
                42,
                4102444800000L);

            Assert.That(first.weaponId,
                Is.EqualTo(CraftLiveSession.EmergencyWeaponId));
            Assert.That(first.attributeId, Is.Not.Empty);
            Assert.That(first.elementEffect.type,
                Is.Not.EqualTo(CraftLiveElementType.None));
            Assert.That(first.skillId, Is.Empty);
            Assert.That(first.skillEffect.type,
                Is.EqualTo(CraftLiveSkillType.None));
            Assert.That(first.rank, Is.EqualTo("通常成功"));
            Assert.That(
                first.attackMaterialCount +
                first.defenseMaterialCount +
                first.evasionMaterialCount,
                Is.EqualTo(4));
            Assert.That(first.attackMaterialCount, Is.LessThan(4));
            Assert.That(first.defenseMaterialCount, Is.LessThan(4));
            Assert.That(first.evasionMaterialCount, Is.LessThan(4));
            Assert.That(
                CraftLiveWeaponCode.Generate(second),
                Is.EqualTo(CraftLiveWeaponCode.Generate(first)));
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
        public void FinalSelection_WaitsForFirebaseGroupNumber()
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
                Is.Empty);
            Assert.That(
                setup.session.ApplyIssuedGroupNumber(
                    setup.session.State.groupGeneration,
                    serial,
                    "07"),
                Is.True);
            Assert.That(setup.session.State.finalWeaponCode, Is.EqualTo("07"));
        }

        [Test]
        public void FinalSelection_CannotReplaceIssuedGroupNumber()
        {
            TestSetup setup = CreateValidSetup();
            setup.session.StartSynthesis();
            setup.session.CompleteSynthesis();
            int serial = setup.session.State.result.resultSerial;
            setup.session.ExpireSession();
            Assert.That(setup.session.SelectFinalWeapon(serial), Is.True);
            Assert.That(
                setup.session.ApplyIssuedGroupNumber(
                    setup.session.State.groupGeneration,
                    serial,
                    "07"),
                Is.True);
            Assert.That(
                setup.session.ApplyIssuedGroupNumber(
                    setup.session.State.groupGeneration,
                    serial,
                    "08"),
                Is.False);

            Assert.That(setup.session.SelectFinalWeapon(serial), Is.False);
            Assert.That(setup.session.State.finalWeaponCode, Is.EqualTo("07"));
            Assert.That(
                setup.session.State.sessionPhase,
                Is.EqualTo(CraftLiveSessionPhase.Finished));
        }

        [Test]
        public void FinalSelection_AcceptsThreeDigitProductionGroupNumber()
        {
            TestSetup setup = CreateValidSetup();
            setup.session.StartSynthesis();
            setup.session.CompleteSynthesis();
            int serial = setup.session.State.result.resultSerial;
            setup.session.ExpireSession();
            Assert.That(setup.session.SelectFinalWeapon(serial), Is.True);

            Assert.That(
                setup.session.ApplyIssuedGroupNumber(
                    setup.session.State.groupGeneration,
                    serial,
                    "123"),
                Is.True);
            Assert.That(
                setup.session.State.finalWeaponCode,
                Is.EqualTo("123"));
        }

        [Test]
        public void GroupCandidates_UseAllTwoDigitsBeforeThreeDigits()
        {
            HashSet<string> candidates = new HashSet<string>();
            for (int offset = 0;
                 offset < CraftLiveRoomTransport.ProductionGroupNumberCount;
                 offset++)
            {
                string candidate =
                    CraftLiveRoomTransport.CreateGroupCandidate(
                        "001",
                        1,
                        1,
                        offset);
                Assert.That(candidates.Add(candidate), Is.True, candidate);
                Assert.That(
                    candidate.Length,
                    Is.EqualTo(offset < 99 ? 2 : 3),
                    candidate);
            }

            Assert.That(candidates, Has.Count.EqualTo(999));
        }

        [TestCase("01", true)]
        [TestCase("99", true)]
        [TestCase("100", true)]
        [TestCase("999", true)]
        [TestCase("1", false)]
        [TestCase("001", false)]
        [TestCase("1000", false)]
        public void ProductionGroupNumber_ValidatesExpectedRange(
            string value,
            bool expected)
        {
            Assert.That(
                CraftLiveRoomTransport.IsProductionGroupNumber(value),
                Is.EqualTo(expected));
        }

        [Test]
        public void OccupiedGroupSnapshot_ParsesProductionAndDebugNumbers()
        {
            HashSet<string> occupied =
                CraftLiveRoomTransport.ParseOccupiedGroupNumbers(
                    "{\"01\":true,\"99\":true,\"100\":true," +
                    "\"999\":true,\"54321\":true,\"bad\":true}");

            Assert.That(occupied,
                Is.EquivalentTo(new[]
                {
                    "01", "99", "100", "999", "54321"
                }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("null")]
        [TestCase("{}")]
        public void OccupiedGroupSnapshot_EmptyPayloadHasNoNumbers(
            string json)
        {
            Assert.That(
                CraftLiveRoomTransport.ParseOccupiedGroupNumbers(json),
                Is.Empty);
        }

        [Test]
        public void DefaultRules_RequireThreeHammerStrikes()
        {
            CraftLiveRules rules =
                AssetDatabase.LoadAssetAtPath<CraftLiveRules>(
                    "Assets/CraftLiveData/DefaultCraftLiveRules.asset");
            Assert.That(rules, Is.Not.Null);
            Assert.That(rules.RequiredHammerPasses, Is.EqualTo(3));
        }

        [Test]
        public void Simulator_CanIssueFiveDigitDebugGroupNumber()
        {
            TestSetup setup = CreateValidSetup();
            setup.session.StartSynthesis();
            setup.session.CompleteSynthesis();
            int serial = setup.session.State.result.resultSerial;
            setup.session.ExpireSession();
            Assert.That(setup.session.SelectFinalWeapon(serial), Is.True);

            Assert.That(
                setup.session.ApplyIssuedGroupNumber(
                    setup.session.State.groupGeneration,
                    serial,
                    "54321",
                    true),
                Is.True);
            Assert.That(
                setup.session.State.finalWeaponCode,
                Is.EqualTo("54321"));
        }

        [TestCase("00001")]
        [TestCase("1234")]
        [TestCase("100000")]
        [TestCase("abcde")]
        public void Simulator_RejectsInvalidFiveDigitGroupNumber(string value)
        {
            Assert.That(
                CraftLiveRoomTransport.IsFiveDigitGroupNumber(value),
                Is.False);
        }

        [TestCase("10000")]
        [TestCase("54321")]
        [TestCase("99999")]
        public void Simulator_AcceptsFiveDigitGroupNumber(string value)
        {
            Assert.That(
                CraftLiveRoomTransport.IsFiveDigitGroupNumber(value),
                Is.True);
        }

        [Test]
        public void WeaponGroupSlot_RequiresAnExplicitElapsedExpiry()
        {
            CraftLiveWeaponGroupRecord existing =
                new CraftLiveWeaponGroupRecord
                {
                    sourceRoomId = "other-room",
                    sourceGroupGeneration = 3,
                    sourceResultSerial = 8,
                    expiresAtUnixMs = 0
                };

            Assert.That(
                CraftLiveRoomTransport.IsWeaponGroupSlotAvailable(
                    existing,
                    "001",
                    4,
                    9,
                    1000),
                Is.False);
            existing.expiresAtUnixMs = 999;
            Assert.That(
                CraftLiveRoomTransport.IsWeaponGroupSlotAvailable(
                    existing,
                    "001",
                    4,
                    9,
                    1000),
                Is.True);
        }

        [TestCase(CraftLiveElementType.Fire, "Fire")]
        [TestCase(CraftLiveElementType.Freeze, "Ice")]
        [TestCase(CraftLiveElementType.Lightning, "Thunder")]
        public void SharedAttribute_FallsBackToTypedElement(
            CraftLiveElementType elementType,
            string expected)
        {
            Assert.That(
                CraftLiveRoomTransport.MapAttribute(
                    string.Empty,
                    elementType),
                Is.EqualTo(expected));
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

        [TestCase(true, 4, 3, "upgrade_attack", true, true)]
        [TestCase(true, 4, 4, "upgrade_attack", true, false)]
        [TestCase(false, 4, 3, "upgrade_attack", true, false)]
        [TestCase(true, 0, -1, "upgrade_attack", true, false)]
        [TestCase(true, 4, 3, "", true, false)]
        [TestCase(true, 4, 3, "upgrade_attack", false, false)]
        public void Pad1RegistrationArrival_PlaysOnceAfterNewRegistration(
            bool built,
            int registrationSerial,
            int handledSerial,
            string materialId,
            bool newlyRegistered,
            bool expected)
        {
            Assert.That(
                CraftLivePad1GalleryController.
                    ShouldPlayRegistrationArrival(
                        built,
                        registrationSerial,
                        handledSerial,
                        materialId,
                        newlyRegistered),
                Is.EqualTo(expected));
        }

        [TestCase("命の玉", true, "命の玉が追加されました")]
        [TestCase("命の玉", false, "命の玉はすでに読み込んでいます")]
        [TestCase(" ", true, "素材が追加されました")]
        public void Pad1RegistrationPopup_UsesRegistrationResult(
            string materialName,
            bool newlyRegistered,
            string expected)
        {
            Assert.That(
                CraftLivePad1GalleryController.BuildRegistrationPopupMessage(
                    materialName,
                    newlyRegistered),
                Is.EqualTo(expected));
        }

        [TestCase(61f, false, false, 0)]
        [TestCase(60f, false, false, 60)]
        [TestCase(45f, true, false, 0)]
        [TestCase(30f, true, false, 30)]
        [TestCase(20f, false, false, 30)]
        [TestCase(20f, true, true, 0)]
        [TestCase(0f, true, false, 0)]
        public void Pad2TimeWarning_FiresEachThresholdOnce(
            float remaining,
            bool minuteShown,
            bool thirtyShown,
            int expected)
        {
            Assert.That(
                CraftLivePad2ResultController.ResolveTimeWarningSecond(
                    remaining,
                    minuteShown,
                    thirtyShown),
                Is.EqualTo(expected));
        }

        [TestCase(60, "残り60秒")]
        [TestCase(30, "残り30秒")]
        [TestCase(-1, "残り0秒")]
        public void Pad2TimeWarning_UsesSecondsMessage(
            int remainingSeconds,
            string expected)
        {
            Assert.That(
                CraftLivePad2ResultController.BuildTimeWarningMessage(
                    remainingSeconds),
                Is.EqualTo(expected));
        }

        [TestCase(CraftLiveSessionPhase.Playing, 60f, true)]
        [TestCase(CraftLiveSessionPhase.Playing, 30f, true)]
        [TestCase(CraftLiveSessionPhase.Playing, 60.1f, false)]
        [TestCase(CraftLiveSessionPhase.Playing, 0f, false)]
        [TestCase(CraftLiveSessionPhase.FinalSelection, 30f, false)]
        public void Pad3Timer_TurnsRedOnlyDuringFinalMinute(
            CraftLiveSessionPhase phase,
            float remaining,
            bool expected)
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                sessionPhase = phase
            };
            Assert.That(
                CraftLivePad3Controller.ShouldUseUrgentTimerColor(
                    state,
                    remaining),
                Is.EqualTo(expected));
        }

        [TestCase(CraftLiveCalculator.SecretBareHandsWeaponId, true)]
        [TestCase(CraftLiveCalculator.SecretPikopikoWeaponId, false)]
        [TestCase(CraftLiveCalculator.SecretKazikiWeaponId, false)]
        [TestCase("weapon_sword", false)]
        public void SecretResult_SmokeIsExclusiveToBareHands(
            string weaponId,
            bool expected)
        {
            Assert.That(
                CraftLivePad2ResultController.ShouldBuildBareHandsSmoke(
                    weaponId),
                Is.EqualTo(expected));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void ResultPanel_WaitsForCompletionPresentation(
            bool presentationReady,
            bool expected)
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                sessionPhase = CraftLiveSessionPhase.Playing
            };
            state.craft.status = CraftLiveCraftStatus.Complete;
            state.craft.completionPresentationReady = presentationReady;

            Assert.That(
                CraftLivePad2ResultController.ShouldShowResult(state),
                Is.EqualTo(expected));
        }

        [Test]
        public void SecretResult_BuildsRedBurstAndBareHandsSmoke()
        {
            GameObject host = new GameObject("SecretResultVisualTest");
            created.Add(host);
            MethodInfo build = typeof(CraftLivePad2ResultController)
                .GetMethod(
                    "BuildSecretResultEffect",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(build, Is.Not.Null);

            build.Invoke(
                null,
                new object[]
                {
                    host.transform,
                    CraftLiveCalculator.SecretBareHandsWeaponId
                });

            Assert.That(
                host.transform.Find("SecretRedRay_0"),
                Is.Not.Null);
            Transform smoke = host.transform.Find("BareHandsSmoke_0");
            Assert.That(smoke, Is.Not.Null);
            Assert.That(
                smoke.GetComponent<CraftLiveSecretSmokeEffect>(),
                Is.Not.Null);
        }

        [Test]
        public void LifeOrbMaterial_IsFullyOpaque()
        {
            const string path =
                "Assets/Materials/" +
                "Meshy_AI_Crimson_Jewel_Pyramid_0813120040_texture.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.GetFloat("_Surface"), Is.EqualTo(0f));
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1f));
            Assert.That(material.GetColor("_BaseColor").a, Is.EqualTo(1f));
            Assert.That(
                material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"),
                Is.False);
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
        public void LandingArc_UsesWorkbenchNormalInsteadOfWorldHeight()
        {
            Vector3 start = new Vector3(2f, 3f, 4f);
            Vector3 result = CraftLivePad2TransferReceiver
                .OffsetAlongSurfaceNormal(
                    start,
                    Vector3.back,
                    0.75f);

            Assert.That(result.x, Is.EqualTo(start.x));
            Assert.That(result.y, Is.EqualTo(start.y));
            Assert.That(result.z, Is.EqualTo(3.25f));
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

        private CraftLiveResultState ExpireEmptyEmergencySession(
            CraftLiveCatalog catalog,
            CraftLiveRules rules,
            string roomId,
            int groupGeneration,
            long sessionEndsAtUnixMs)
        {
            GameObject root = new GameObject("EmergencySession");
            created.Add(root);
            CraftLiveSession session = root.AddComponent<CraftLiveSession>();
            SetField(session, "catalog", catalog);
            SetField(session, "rules", rules);
            InvokeAwake(session);
            session.Configure(roomId, CraftLiveRole.WorkbenchPad);

            CraftLiveRoomState state = CraftLiveRoomState.Create(catalog);
            state.sessionPhase = CraftLiveSessionPhase.Playing;
            state.groupGeneration = groupGeneration;
            state.sessionStartedAtUnixMs = sessionEndsAtUnixMs - 270000L;
            state.sessionEndsAtUnixMs = sessionEndsAtUnixMs;
            session.ApplyRemoteState(state);
            session.ExpireSession();

            Assert.That(session.State.sessionPhase,
                Is.EqualTo(CraftLiveSessionPhase.FinalSelection));
            Assert.That(session.State.completedWeapons,
                Has.Count.EqualTo(1));
            return session.State.completedWeapons[0];
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

        private static void WithScene(
            string path,
            Action<Scene> action)
        {
            SceneSetup[] setup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    path,
                    OpenSceneMode.Single);
                action(scene);
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

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            List<T> found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }

            Assert.That(found, Has.Count.EqualTo(1));
            return found[0];
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
