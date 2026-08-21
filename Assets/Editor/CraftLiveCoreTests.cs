using System.Collections.Generic;
using System.Reflection;
using CraftOrigin.CraftLive;
using NUnit.Framework;
using UnityEngine;

namespace CraftOrigin.CraftLiveTests
{
    public sealed class CraftLiveCoreTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void BaseMaterial_AddsThreeStatModifiersWithoutElementBoost()
        {
            CraftLiveMaterialDefinition material = CreateMaterial(
                "ore",
                CraftLiveMaterialCategory.Upgrade);
            SetField(material, "statModifiers", new CraftLiveStats
            {
                attackRate = 30f,
                defenseRate = 4f,
                evasionRate = 2f
            });

            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { material },
                new List<CraftLiveWeaponDefinition>());
            CraftLiveRoomState state = CraftLiveRoomState.Create(catalog);
            state.slots.top = "ore";

            CraftLiveStats stats = CraftLiveCalculator.CalculateStats(
                state,
                catalog,
                null,
                5f);

            Assert.That(stats.attackRate, Is.EqualTo(35f));
            Assert.That(stats.defenseRate, Is.EqualTo(9f));
            Assert.That(stats.evasionRate, Is.EqualTo(7f));
            Assert.That(stats.elementBoost, Is.Zero);
        }

        [Test]
        public void Calculation_IncludesWeaponBaseStats()
        {
            CraftLiveWeaponDefinition weapon = CreateWeapon("sword");
            SetField(weapon, "baseStats", new CraftLiveStats
            {
                attackRate = 12f,
                defenseRate = 8f,
                evasionRate = 3f
            });
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition>(),
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveRoomState state = CraftLiveRoomState.Create(catalog);

            CraftLiveStats stats = CraftLiveCalculator.CalculateStats(
                state,
                catalog,
                null,
                0f);

            Assert.That(stats.attackRate, Is.EqualTo(12f));
            Assert.That(stats.defenseRate, Is.EqualTo(8f));
            Assert.That(stats.evasionRate, Is.EqualTo(3f));
        }

        [Test]
        public void Synthesis_RequiresConfirmedWeaponAttributeAndSkill()
        {
            CraftLiveMaterialDefinition attribute = CreateMaterial(
                "fire",
                CraftLiveMaterialCategory.Attribute);
            CraftLiveMaterialDefinition skill = CreateMaterial(
                "luck",
                CraftLiveMaterialCategory.Skill);
            CraftLiveWeaponDefinition weapon = CreateWeapon("sword");
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { attribute, skill },
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveRoomState state = CraftLiveRoomState.Create(catalog);

            Assert.That(
                CraftLiveCalculator.ValidateSynthesis(state, catalog),
                Is.Not.Empty);

            state.weaponSelectionConfirmed = true;
            state.slots.attribute = "fire";
            state.slots.skill = "luck";

            Assert.That(
                CraftLiveCalculator.ValidateSynthesis(state, catalog),
                Is.Empty);
        }

        [Test]
        public void Synthesis_CanRequireAllFourBaseSlotsFromRules()
        {
            CraftLiveMaterialDefinition attribute = CreateMaterial(
                "fire",
                CraftLiveMaterialCategory.Attribute);
            CraftLiveMaterialDefinition skill = CreateMaterial(
                "luck",
                CraftLiveMaterialCategory.Skill);
            CraftLiveMaterialDefinition ore = CreateMaterial(
                "ore",
                CraftLiveMaterialCategory.Upgrade);
            CraftLiveWeaponDefinition weapon = CreateWeapon("sword");
            CraftLiveRules rules = CreateAsset<CraftLiveRules>();
            SetField(rules, "requireAllFourBaseSlots", true);
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition>
                {
                    attribute,
                    skill,
                    ore
                },
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveRoomState state = CraftLiveRoomState.Create(catalog);
            state.weaponSelectionConfirmed = true;
            state.slots.attribute = "fire";
            state.slots.skill = "luck";

            Assert.That(
                CraftLiveCalculator.ValidateSynthesis(
                    state,
                    catalog,
                    rules),
                Is.Not.Empty);

            state.slots.top = "ore";
            state.slots.right = "ore";
            state.slots.left = "ore";
            state.slots.bottom = "ore";

            Assert.That(
                CraftLiveCalculator.ValidateSynthesis(
                    state,
                    catalog,
                    rules),
                Is.Empty);
        }

        [Test]
        public void BuildResult_SnapshotsAttributeAndSkillEffects()
        {
            CraftLiveMaterialDefinition attribute = CreateMaterial(
                "fire",
                CraftLiveMaterialCategory.Attribute);
            SetField(attribute, "attributeId", "fire");
            SetField(attribute, "attributeDisplayName", "炎");
            SetField(attribute, "elementEffect", new CraftLiveElementEffect
            {
                type = CraftLiveElementType.Fire,
                activationChancePercent = 25f,
                effectAmount = 6f,
                durationSeconds = 3f
            });
            CraftLiveMaterialDefinition skill = CreateMaterial(
                "luck",
                CraftLiveMaterialCategory.Skill);
            SetField(skill, "skillId", "luck");
            SetField(skill, "skillDisplayName", "幸運");
            SetField(skill, "skillEffect", new CraftLiveSkillEffect
            {
                type = CraftLiveSkillType.Luck,
                activationChancePercent = 20f,
                primaryValue = 15f
            });
            CraftLiveWeaponDefinition weapon = CreateWeapon("sword");
            SetField(weapon, "displayName", "鍛鉄の剣");

            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { attribute, skill },
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveRoomState state = CraftLiveRoomState.Create(catalog);
            state.selectedWeaponId = "sword";
            state.weaponSelectionConfirmed = true;
            state.slots.attribute = "fire";
            state.slots.skill = "luck";

            CraftLiveResultState result = CraftLiveCalculator.BuildResult(
                state,
                catalog,
                null,
                1000);

            Assert.That(result.weaponName, Is.EqualTo("炎の鍛鉄の剣"));
            Assert.That(result.skillName, Is.EqualTo("幸運"));
            Assert.That(
                result.elementEffect.type,
                Is.EqualTo(CraftLiveElementType.Fire));
            Assert.That(
                result.skillEffect.type,
                Is.EqualTo(CraftLiveSkillType.Luck));
        }

        [TestCase("craftlive:material:fireCrystal", "fireCrystal")]
        [TestCase("{\"materialId\":\"waterStone\"}", "waterStone")]
        [TestCase(
            "https://example.invalid/material?material=darkStone",
            "darkStone")]
        public void QrPayload_IsParsed(string payload, string expected)
        {
            Assert.That(
                CraftLiveQrScanner.ParseMaterialId(payload),
                Is.EqualTo(expected));
        }

        [Test]
        public void QrRegistration_IsPermanentAndDoesNotIncrement()
        {
            CraftLiveMaterialDefinition material = CreateMaterial(
                "fire",
                CraftLiveMaterialCategory.Attribute);
            SetField(material, "requiresQrUnlock", true);
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { material },
                new List<CraftLiveWeaponDefinition>());
            CraftLiveSession session = CreateSession(catalog);

            session.UnlockMaterialId("fire");
            session.UnlockMaterialId("fire");

            Assert.That(session.State.GetInventoryCount("fire"), Is.EqualTo(1));
            Assert.That(
                session.State.registeredMaterialIds.Count,
                Is.EqualTo(1));
            Assert.That(session.State.registrationSerial, Is.EqualTo(2));
            Assert.That(session.State.lastRegistrationDelta, Is.Zero);
        }

        [Test]
        public void Placement_ChangesSlotOnlyAfterArrivalAndDoesNotConsume()
        {
            CraftLiveMaterialDefinition material = CreateMaterial(
                "fire",
                CraftLiveMaterialCategory.Attribute);
            SetField(material, "requiresQrUnlock", true);
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { material },
                new List<CraftLiveWeaponDefinition>());
            CraftLiveSession session = CreateSession(catalog);
            session.UnlockMaterialId("fire");

            session.SelectMaterial(material);
            session.ChoosePlacementSlot(CraftLiveSlotId.Attribute);

            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.ConfirmingSlot));
            Assert.That(session.State.slots.attribute, Is.Empty);

            session.ConfirmPlacement();
            session.MarkTransferLaunching();
            session.MarkTransferArriving();
            session.CompleteCurrentPlacement();

            Assert.That(session.State.slots.attribute, Is.EqualTo("fire"));
            Assert.That(session.State.GetInventoryCount("fire"), Is.EqualTo(1));
            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.PlacementComplete));
        }

        [Test]
        public void RemovingPlacedMaterial_DoesNotChangeRegistration()
        {
            CraftLiveMaterialDefinition material = CreateMaterial(
                "fire",
                CraftLiveMaterialCategory.Attribute);
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { material },
                new List<CraftLiveWeaponDefinition>());
            CraftLiveSession session = CreateSession(catalog);
            CraftLiveRoomState state = CraftLiveRoomState.Create(catalog);
            state.RegisterMaterial("fire");
            state.slots.attribute = "fire";
            session.ApplyRemoteState(state);

            session.RemoveSlot(CraftLiveSlotId.Attribute);

            Assert.That(session.State.slots.attribute, Is.Empty);
            Assert.That(session.State.HasMaterialRegistered("fire"), Is.True);
        }

        [Test]
        public void ResetRoomForNextGroup_ClearsQrRegistrations()
        {
            CraftLiveMaterialDefinition material = CreateMaterial(
                "fire",
                CraftLiveMaterialCategory.Attribute);
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { material },
                new List<CraftLiveWeaponDefinition>());
            CraftLiveSession session = CreateSession(catalog);
            session.UnlockMaterialId("fire");

            session.ResetRoomForNextGroup();

            Assert.That(session.State.HasMaterialRegistered("fire"), Is.False);
            Assert.That(session.State.registeredMaterialIds, Is.Empty);
            Assert.That(
                session.State.schemaVersion,
                Is.EqualTo(CraftLiveRoomState.CurrentSchemaVersion));
        }

        [Test]
        public void SelectingMaterialBeforeWeapon_IsRejectedWithoutLockingFlow()
        {
            CraftLiveMaterialDefinition material = CreateMaterial(
                "ore",
                CraftLiveMaterialCategory.Upgrade);
            CraftLiveWeaponDefinition weapon = CreateWeapon("sword");
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { material },
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveSession session = CreateSession(catalog);

            session.SelectMaterial(material);

            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(session.State.selectedMaterialId, Is.Empty);
            StringAssert.Contains("先にパッド2で武器", session.State.message);
            Assert.That(
                CraftLivePad2WeaponCarousel.CanChangeWeapon(session.State),
                Is.True);
        }

        [Test]
        public void SelectingWeapon_RecoversLegacyUnconfirmedMaterialState()
        {
            CraftLiveMaterialDefinition material = CreateMaterial(
                "ore",
                CraftLiveMaterialCategory.Upgrade);
            CraftLiveWeaponDefinition weapon = CreateWeapon("sword");
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { material },
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveSession session = CreateSession(catalog);
            CraftLiveRoomState legacyState =
                CraftLiveRoomState.Create(catalog);
            legacyState.selectedMaterialId = material.MaterialId;
            legacyState.placement.materialId = material.MaterialId;
            legacyState.placement.status =
                CraftLivePlacementStatus.SelectingSlot;
            session.ApplyRemoteState(legacyState);

            Assert.That(
                CraftLivePad2WeaponCarousel.CanChangeWeapon(session.State),
                Is.True);
            session.SelectWeapon(weapon);

            Assert.That(
                session.State.placement.status,
                Is.EqualTo(CraftLivePlacementStatus.Idle));
            Assert.That(session.State.selectedMaterialId, Is.Empty);
            Assert.That(session.State.selectedWeaponId, Is.EqualTo("sword"));
        }

        [Test]
        public void ExpiredEmptyRemoteRoom_RestartsWithoutBlockingPad2()
        {
            CraftLiveWeaponDefinition weapon = CreateWeapon("sword");
            CraftLiveMaterialDefinition registered = CreateMaterial(
                "registered",
                CraftLiveMaterialCategory.Attribute);
            CraftLiveCatalog catalog = CreateCatalog(
                new List<CraftLiveMaterialDefinition> { registered },
                new List<CraftLiveWeaponDefinition> { weapon });
            CraftLiveSession session = CreateSession(catalog);
            CraftLiveRoomState expired = CraftLiveRoomState.Create(catalog);
            expired.RegisterMaterial("registered");
            expired.sessionStartedAtUnixMs = 1;
            expired.sessionEndsAtUnixMs = 2;
            expired.sessionPhase = CraftLiveSessionPhase.FinalSelection;
            expired.weaponSelectionConfirmed = false;
            expired.revision = 12;

            session.ApplyRemoteState(expired);

            Assert.That(
                session.State.sessionPhase,
                Is.EqualTo(CraftLiveSessionPhase.Playing));
            Assert.That(
                session.State.sessionEndsAtUnixMs,
                Is.GreaterThan(CraftLiveSession.UnixNowMs()));
            Assert.That(session.State.revision, Is.EqualTo(13));
            Assert.That(
                session.State.HasMaterialRegistered("registered"),
                Is.True);
            Assert.That(
                CraftLivePad2WeaponCarousel.CanChangeWeapon(session.State),
                Is.True);
        }

        private CraftLiveMaterialDefinition CreateMaterial(
            string id,
            CraftLiveMaterialCategory category)
        {
            CraftLiveMaterialDefinition material =
                CreateAsset<CraftLiveMaterialDefinition>();
            SetField(material, "materialId", id);
            SetField(material, "category", category);
            return material;
        }

        private CraftLiveWeaponDefinition CreateWeapon(string id)
        {
            CraftLiveWeaponDefinition weapon =
                CreateAsset<CraftLiveWeaponDefinition>();
            SetField(weapon, "weaponId", id);
            return weapon;
        }

        private CraftLiveCatalog CreateCatalog(
            List<CraftLiveMaterialDefinition> materials,
            List<CraftLiveWeaponDefinition> weapons)
        {
            CraftLiveCatalog catalog = CreateAsset<CraftLiveCatalog>();
            SetField(catalog, "materials", materials);
            SetField(catalog, "weapons", weapons);
            return catalog;
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(asset);
            return asset;
        }

        private CraftLiveSession CreateSession(CraftLiveCatalog catalog)
        {
            GameObject gameObject = new GameObject("CraftLiveSessionTest");
            createdObjects.Add(gameObject);
            CraftLiveSession session =
                gameObject.AddComponent<CraftLiveSession>();
            SetField(session, "catalog", catalog);
            MethodInfo awake = typeof(CraftLiveSession).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null);
            awake.Invoke(session, null);
            return session;
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}
