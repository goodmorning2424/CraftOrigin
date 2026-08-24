using System.Collections.Generic;
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
    public sealed class CraftLiveStep2Tests
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

        [TestCase("1", CraftLiveRole.MaterialPad)]
        [TestCase("items", CraftLiveRole.MaterialPad)]
        [TestCase("materials", CraftLiveRole.MaterialPad)]
        [TestCase("pad1", CraftLiveRole.MaterialPad)]
        [TestCase("2", CraftLiveRole.WorkbenchPad)]
        [TestCase("craft", CraftLiveRole.WorkbenchPad)]
        [TestCase("workbench", CraftLiveRole.WorkbenchPad)]
        [TestCase("pad2", CraftLiveRole.WorkbenchPad)]
        [TestCase("3", CraftLiveRole.QrPad)]
        [TestCase("status", CraftLiveRole.QrPad)]
        [TestCase("qr", CraftLiveRole.QrPad)]
        [TestCase("pad3", CraftLiveRole.QrPad)]
        [TestCase("4", CraftLiveRole.HologramPad)]
        [TestCase("hologram", CraftLiveRole.HologramPad)]
        [TestCase("pad4", CraftLiveRole.HologramPad)]
        [TestCase("", CraftLiveRole.Auto)]
        [TestCase("unknown", CraftLiveRole.Auto)]
        public void LaunchQuery_ParsesSupportedScreenAliases(
            string value,
            CraftLiveRole expected)
        {
            Assert.That(
                CraftLiveLaunchQuery.ParseRole(value),
                Is.EqualTo(expected));
        }

        [TestCase(CraftLiveRole.MaterialPad, "1", true)]
        [TestCase(CraftLiveRole.MaterialPad, "true", true)]
        [TestCase(CraftLiveRole.MaterialPad, "0", false)]
        [TestCase(CraftLiveRole.MaterialPad, "off", false)]
        [TestCase(CraftLiveRole.WorkbenchPad, "1", false)]
        [TestCase(CraftLiveRole.QrPad, "1", false)]
        [TestCase(CraftLiveRole.HologramPad, "1", false)]
        public void LaunchQuery_ResetIsOwnedByPad1Only(
            CraftLiveRole role,
            string value,
            bool expected)
        {
            Assert.That(
                CraftLiveLaunchQuery.ShouldResetRoomOnLaunch(
                    role,
                    value),
                Is.EqualTo(expected));
        }

        [TestCase(
            CraftLiveRole.MaterialPad,
            "Pad1_MaterialGallery")]
        [TestCase(
            CraftLiveRole.WorkbenchPad,
            "Pad2_Workbench")]
        [TestCase(
            CraftLiveRole.QrPad,
            "Pad3_StatusQr")]
        [TestCase(
            CraftLiveRole.HologramPad,
            "Pad4_Hologram")]
        public void LaunchConfig_DefaultSceneMappingIsStable(
            CraftLiveRole role,
            string expected)
        {
            CraftLiveLaunchConfig config =
                ScriptableObject.CreateInstance<CraftLiveLaunchConfig>();
            createdObjects.Add(config);

            Assert.That(config.GetSceneName(role), Is.EqualTo(expected));
        }

        [TestCase(CraftLiveRole.MaterialPad, true)]
        [TestCase(CraftLiveRole.WorkbenchPad, true)]
        [TestCase(CraftLiveRole.QrPad, true)]
        [TestCase(CraftLiveRole.HologramPad, true)]
        [TestCase(CraftLiveRole.Auto, false)]
        public void Bootstrap_TestSwitcherAcceptsAllFourPadsOnly(
            CraftLiveRole role,
            bool expected)
        {
            Assert.That(
                CraftLiveBootstrap.IsTestablePadRole(role),
                Is.EqualTo(expected));
        }

        [Test]
        public void RoomTransport_ConfigureSanitizesRemoteSettings()
        {
            GameObject gameObject = new GameObject("TransportTest");
            createdObjects.Add(gameObject);
            CraftLiveRoomTransport transport =
                gameObject.AddComponent<CraftLiveRoomTransport>();
            transport.enabled = false;

            transport.Configure(
                true,
                " https://example.firebaseio.com/// ",
                -10f,
                -10f);

            Assert.That(transport.IsRemoteMode, Is.True);
            Assert.That(
                transport.FirebaseDatabaseUrl,
                Is.EqualTo("https://example.firebaseio.com"));
        }

        [Test]
        public void BuildSettings_StartWithFiveEnabledStep2Scenes()
        {
            string[] expected =
            {
                CraftLiveStep2SceneGenerator.BootstrapScenePath,
                CraftLiveStep2SceneGenerator.Pad1ScenePath,
                CraftLiveStep2SceneGenerator.Pad2ScenePath,
                CraftLiveStep2SceneGenerator.Pad3ScenePath,
                CraftLiveStep2SceneGenerator.Pad4ScenePath
            };
            List<string> enabled = new List<string>();
            foreach (EditorBuildSettingsScene scene in
                     EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    enabled.Add(scene.path);
                }
            }

            CollectionAssert.AreEqual(expected, enabled);
        }

        [Test]
        public void BootstrapScene_HasRequiredRuntimeReferences()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.BootstrapScenePath,
                scene =>
                {
                    CraftLiveSession session =
                        FindSingle<CraftLiveSession>(scene);
                    CraftLiveBootstrap bootstrap =
                        FindSingle<CraftLiveBootstrap>(scene);
                    CraftLiveRoomTransport transport =
                        FindSingle<CraftLiveRoomTransport>(scene);

                    Assert.That(session.Catalog, Is.Not.Null);
                    Assert.That(session.Rules, Is.Not.Null);
                    Assert.That(transport.enabled, Is.False);
                    AssertObjectReference(bootstrap, "session");
                    AssertObjectReference(bootstrap, "transport");
                    AssertObjectReference(bootstrap, "launchConfig");
                    AssertObjectReference(bootstrap, "targetCamera");
                    SerializedObject serializedBootstrap =
                        new SerializedObject(bootstrap);
                    Assert.That(
                        serializedBootstrap
                            .FindProperty("showEditorPadSwitcher")
                            .boolValue,
                        Is.True);
                    AssertObjectReference(transport, "session");
                });
        }

        [TestCase(
            CraftLiveStep2SceneGenerator.Pad1ScenePath,
            CraftLiveRole.MaterialPad,
            typeof(CraftLivePad1Bindings))]
        [TestCase(
            CraftLiveStep2SceneGenerator.Pad2ScenePath,
            CraftLiveRole.WorkbenchPad,
            typeof(CraftLivePad2Bindings))]
        [TestCase(
            CraftLiveStep2SceneGenerator.Pad3ScenePath,
            CraftLiveRole.QrPad,
            typeof(CraftLivePad3Bindings))]
        [TestCase(
            CraftLiveStep2SceneGenerator.Pad4ScenePath,
            CraftLiveRole.HologramPad,
            typeof(CraftLivePad4Bindings))]
        public void PadScene_HasExpectedRoleAndBindings(
            string scenePath,
            CraftLiveRole expectedRole,
            System.Type bindingsType)
        {
            WithScene(
                scenePath,
                scene =>
                {
                    CraftLivePadSceneRoot root =
                        FindSingle<CraftLivePadSceneRoot>(scene);
                    Assert.That(root.Role, Is.EqualTo(expectedRole));
                    Assert.That(root.CameraAnchor, Is.Not.Null);

                    Component[] bindings =
                        FindComponents(scene, bindingsType);
                    Assert.That(bindings, Has.Length.EqualTo(1));
                    AssertAllObjectReferencesAssigned(bindings[0]);
                });
        }

        private static void AssertObjectReference(
            Object target,
            string propertyName)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            Assert.That(
                property.objectReferenceValue,
                Is.Not.Null,
                propertyName);
        }

        private static void AssertAllObjectReferencesAssigned(
            Component component)
        {
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyPath == "m_Script" ||
                    iterator.propertyType !=
                    SerializedPropertyType.ObjectReference)
                {
                    continue;
                }

                Assert.That(
                    iterator.objectReferenceValue,
                    Is.Not.Null,
                    $"{component.GetType().Name}.{iterator.propertyPath}");
            }
        }

        private static T FindSingle<T>(Scene scene) where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            Assert.That(
                results,
                Has.Count.EqualTo(1),
                $"{scene.path}: {typeof(T).Name}");
            return results[0];
        }

        private static Component[] FindComponents(
            Scene scene,
            System.Type type)
        {
            List<Component> results = new List<Component>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(
                    root.GetComponentsInChildren(type, true));
            }

            return results.ToArray();
        }

        private static void WithScene(
            string scenePath,
            System.Action<Scene> action)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
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
