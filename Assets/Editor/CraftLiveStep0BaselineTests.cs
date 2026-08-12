using System.Collections.Generic;
using CraftOrigin.CraftLive;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveTests
{
    public sealed class CraftLiveStep0BaselineTests
    {
        [TestCase(CraftLiveSlotId.Attribute, 0)]
        [TestCase(CraftLiveSlotId.Skill, 1)]
        [TestCase(CraftLiveSlotId.Top, 2)]
        [TestCase(CraftLiveSlotId.Right, 3)]
        [TestCase(CraftLiveSlotId.Left, 4)]
        [TestCase(CraftLiveSlotId.Bottom, 5)]
        public void SlotEnum_NumericValueRemainsStable(
            CraftLiveSlotId value,
            int expected)
        {
            Assert.That((int)value, Is.EqualTo(expected));
        }

        [Test]
        public void RoomState_V3RoundTripPreservesBaselineData()
        {
            CraftLiveRoomState state = new CraftLiveRoomState
            {
                revision = 12,
                selectedMaterialId = "fireCrystal",
                selectedWeaponId = "weapon_iron_sword"
            };
            state.RegisterMaterial("fireCrystal");
            state.slots.attribute = "fireCrystal";
            state.slots.skill = "reviveFeather";

            CraftLiveRoomState clone = state.Clone();

            Assert.That(
                clone.schemaVersion,
                Is.EqualTo(CraftLiveRoomState.CurrentSchemaVersion));
            Assert.That(clone.revision, Is.EqualTo(12));
            Assert.That(clone.HasMaterialRegistered("fireCrystal"), Is.True);
            Assert.That(clone.slots.attribute, Is.EqualTo("fireCrystal"));
            Assert.That(clone.slots.skill, Is.EqualTo("reviveFeather"));
        }

        [Test]
        public void LegacyQrList_MigratesToPermanentRegistration()
        {
            const string json =
                "{\"schemaVersion\":1,\"qrUnlockedMaterialIds\":" +
                "[\"fireCrystal\",\"fireCrystal\",\"reviveFeather\"]}";

            CraftLiveRoomState state = CraftLiveRoomState.FromJson(json);

            Assert.That(
                state.schemaVersion,
                Is.EqualTo(CraftLiveRoomState.CurrentSchemaVersion));
            Assert.That(state.HasMaterialRegistered("fireCrystal"), Is.True);
            Assert.That(state.HasMaterialRegistered("reviveFeather"), Is.True);
            Assert.That(state.qrUnlockedMaterialIds, Is.Empty);
        }

        [Test]
        public void V2Inventory_MigratesZeroCountEntryToPermanentRegistration()
        {
            const string json =
                "{\"schemaVersion\":2,\"inventory\":[" +
                "{\"materialId\":\"fireCrystal\",\"count\":0}," +
                "{\"materialId\":\"reviveFeather\",\"count\":4}]}";

            CraftLiveRoomState state = CraftLiveRoomState.FromJson(json);

            Assert.That(
                state.schemaVersion,
                Is.EqualTo(CraftLiveRoomState.CurrentSchemaVersion));
            Assert.That(state.HasMaterialRegistered("fireCrystal"), Is.True);
            Assert.That(state.HasMaterialRegistered("reviveFeather"), Is.True);
            Assert.That(state.inventory, Is.Empty);
        }

        [TestCase(CraftLiveSlotId.Attribute, CraftLiveMaterialCategory.Attribute)]
        [TestCase(CraftLiveSlotId.Skill, CraftLiveMaterialCategory.Skill)]
        [TestCase(CraftLiveSlotId.Top, CraftLiveMaterialCategory.Upgrade)]
        [TestCase(CraftLiveSlotId.Right, CraftLiveMaterialCategory.Upgrade)]
        [TestCase(CraftLiveSlotId.Left, CraftLiveMaterialCategory.Upgrade)]
        [TestCase(CraftLiveSlotId.Bottom, CraftLiveMaterialCategory.Upgrade)]
        public void SlotCategoryContract_RemainsStable(
            CraftLiveSlotId slot,
            CraftLiveMaterialCategory expected)
        {
            Assert.That(CraftLiveSlot.RequiredCategory(slot), Is.EqualTo(expected));
        }

        [Test]
        public void DefaultCatalog_HasUniqueMaterialAndWeaponIds()
        {
            CraftLiveCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CraftLiveCatalog>(
                    "Assets/CraftLiveData/DefaultCraftLiveCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Materials.Count, Is.GreaterThan(0));
            Assert.That(catalog.Weapons.Count, Is.GreaterThanOrEqualTo(3));

            HashSet<string> materialIds = new HashSet<string>();
            foreach (CraftLiveMaterialDefinition material in catalog.Materials)
            {
                Assert.That(material, Is.Not.Null);
                Assert.That(materialIds.Add(material.MaterialId), Is.True);
            }

            HashSet<string> weaponIds = new HashSet<string>();
            foreach (CraftLiveWeaponDefinition weapon in catalog.Weapons)
            {
                Assert.That(weapon, Is.Not.Null);
                Assert.That(weaponIds.Add(weapon.WeaponId), Is.True);
            }
        }

        [Test]
        public void CraftScene_HasSixUniqueWorkbenchAnchors()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(
                    "Assets/Scenes/Craft.unity",
                    OpenSceneMode.Single);
                CraftLiveWorkbenchView workbench =
                    Object.FindAnyObjectByType<CraftLiveWorkbenchView>();
                Assert.That(workbench, Is.Not.Null);

                SerializedObject serialized = new SerializedObject(workbench);
                SerializedProperty anchors = serialized.FindProperty("slotAnchors");
                Assert.That(anchors, Is.Not.Null);
                Assert.That(anchors.arraySize, Is.EqualTo(6));

                HashSet<int> slotValues = new HashSet<int>();
                HashSet<Object> anchorObjects = new HashSet<Object>();
                for (int i = 0; i < anchors.arraySize; i++)
                {
                    SerializedProperty element =
                        anchors.GetArrayElementAtIndex(i);
                    SerializedProperty slot =
                        element.FindPropertyRelative("slot");
                    SerializedProperty anchor =
                        element.FindPropertyRelative("anchor");
                    Assert.That(slotValues.Add(slot.enumValueIndex), Is.True);
                    Assert.That(anchor.objectReferenceValue, Is.Not.Null);
                    Assert.That(
                        anchorObjects.Add(anchor.objectReferenceValue),
                        Is.True);
                }
            }
            finally
            {
                bool canRestore = false;
                foreach (SceneSetup sceneSetup in setup)
                {
                    if (sceneSetup.isLoaded && sceneSetup.isActive)
                    {
                        canRestore = true;
                        break;
                    }
                }

                if (canRestore)
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
    }
}
