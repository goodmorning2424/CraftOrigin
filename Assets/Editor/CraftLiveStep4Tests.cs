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
    public sealed class CraftLiveStep4Tests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();

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

        [TestCase(
            CraftLivePad2PhysicalSlot.UpperLeft,
            CraftLiveSlotId.Top)]
        [TestCase(
            CraftLivePad2PhysicalSlot.MiddleLeft,
            CraftLiveSlotId.Left)]
        [TestCase(
            CraftLivePad2PhysicalSlot.UpperRight,
            CraftLiveSlotId.Right)]
        [TestCase(
            CraftLivePad2PhysicalSlot.MiddleRight,
            CraftLiveSlotId.Bottom)]
        [TestCase(
            CraftLivePad2PhysicalSlot.LowerLeft,
            CraftLiveSlotId.Skill)]
        [TestCase(
            CraftLivePad2PhysicalSlot.LowerRight,
            CraftLiveSlotId.Attribute)]
        public void PhysicalSlotMapping_MatchesReferenceLayout(
            CraftLivePad2PhysicalSlot physicalSlot,
            CraftLiveSlotId expected)
        {
            Assert.That(
                CraftLivePad2SlotLayout.GetSlotId(physicalSlot),
                Is.EqualTo(expected));
        }

        [Test]
        public void SlotLayout_MatchesWorkbenchRecesses()
        {
            CraftLivePad2SlotSpec upperLeft =
                CraftLivePad2SlotLayout.Get(
                    CraftLivePad2PhysicalSlot.UpperLeft);
            CraftLivePad2SlotSpec middleLeft =
                CraftLivePad2SlotLayout.Get(
                    CraftLivePad2PhysicalSlot.MiddleLeft);
            CraftLivePad2SlotSpec lowerLeft =
                CraftLivePad2SlotLayout.Get(
                    CraftLivePad2PhysicalSlot.LowerLeft);

            Assert.That(upperLeft.DefaultPosition.x, Is.EqualTo(-1.47f));
            Assert.That(upperLeft.DefaultPosition.y, Is.EqualTo(2.12f));
            Assert.That(middleLeft.DefaultPosition.y, Is.EqualTo(0f));
            Assert.That(lowerLeft.DefaultPosition.y, Is.EqualTo(-2.12f));
        }

        [Test]
        public void LiquidFlowTargets_FollowSixWorkbenchChannels()
        {
            CraftLivePad2SlotSpec upperLeft =
                CraftLivePad2SlotLayout.Get(CraftLiveSlotId.Top);
            CraftLivePad2SlotSpec upperRight =
                CraftLivePad2SlotLayout.Get(CraftLiveSlotId.Right);
            CraftLivePad2SlotSpec lowerLeft =
                CraftLivePad2SlotLayout.Get(CraftLiveSlotId.Skill);
            CraftLivePad2SlotSpec lowerRight =
                CraftLivePad2SlotLayout.Get(CraftLiveSlotId.Attribute);

            Assert.That(upperLeft.FlowEndPosition.x, Is.LessThan(0f));
            Assert.That(upperRight.FlowEndPosition.x, Is.GreaterThan(0f));
            Assert.That(lowerLeft.FlowEndPosition.x, Is.LessThan(0f));
            Assert.That(lowerRight.FlowEndPosition.x, Is.GreaterThan(0f));
            Assert.That(lowerLeft.FlowEndPosition.y,
                Is.EqualTo(lowerRight.FlowEndPosition.y));
        }

        [TestCase(-1, 3, 2)]
        [TestCase(0, 3, 0)]
        [TestCase(3, 3, 0)]
        [TestCase(4, 3, 1)]
        public void WeaponCarousel_WrapsIndex(
            int index,
            int count,
            int expected)
        {
            Assert.That(
                CraftLivePad2WeaponCarousel.WrapIndex(index, count),
                Is.EqualTo(expected));
        }

        [Test]
        public void SlotAvailability_RequiresConfirmedWeapon()
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);
            SetField(
                material,
                "category",
                CraftLiveMaterialCategory.Upgrade);
            CraftLiveRoomState state = new CraftLiveRoomState();
            state.placement.status =
                CraftLivePlacementStatus.SelectingSlot;
            state.Normalize(null);

            Assert.That(
                CraftLivePlacementSlotView.IsAvailable(
                    state,
                    material,
                    CraftLiveSlotId.Top,
                    true),
                Is.False);

            state.weaponSelectionConfirmed = true;

            Assert.That(
                CraftLivePlacementSlotView.IsAvailable(
                    state,
                    material,
                    CraftLiveSlotId.Top,
                    true),
                Is.True);
            Assert.That(
                CraftLivePlacementSlotView.IsAvailable(
                    state,
                    material,
                    CraftLiveSlotId.Skill,
                    true),
                Is.False);
        }

        [Test]
        public void Confirmation_RequiresWeaponAndCandidate()
        {
            CraftLiveRoomState state = new CraftLiveRoomState();
            state.placement.status =
                CraftLivePlacementStatus.ConfirmingSlot;
            state.placement.hasCandidateSlot = true;
            state.Normalize(null);

            Assert.That(
                CraftLivePad2PlacementController
                    .CanConfirmPlacement(state),
                Is.False);

            state.weaponSelectionConfirmed = true;

            Assert.That(
                CraftLivePad2PlacementController
                    .CanConfirmPlacement(state),
                Is.True);
        }

        [Test]
        public void WeaponChange_IsBlockedDuringPlacement()
        {
            CraftLiveRoomState state = new CraftLiveRoomState();
            state.Normalize(null);
            Assert.That(
                CraftLivePad2WeaponCarousel.CanChangeWeapon(state),
                Is.True);

            state.placement.status =
                CraftLivePlacementStatus.SelectingSlot;

            // The player may return to weapon selection until a weapon has
            // actually been confirmed. Once confirmed, placement locks it.
            Assert.That(
                CraftLivePad2WeaponCarousel.CanChangeWeapon(state),
                Is.True);

            state.weaponSelectionConfirmed = true;

            Assert.That(
                CraftLivePad2WeaponCarousel.CanChangeWeapon(state),
                Is.False);
        }

        [Test]
        public void Pad2Scene_HasOneCarouselAndPlacementController()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad2ScenePath,
                scene =>
                {
                    Assert.That(
                        FindAll<
                            CraftLivePad2WeaponCarousel>(scene),
                        Has.Count.EqualTo(1));
                    Assert.That(
                        FindAll<
                            CraftLivePad2PlacementController>(scene),
                        Has.Count.EqualTo(1));
                });
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static List<T> FindAll<T>(Scene scene)
            where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
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
