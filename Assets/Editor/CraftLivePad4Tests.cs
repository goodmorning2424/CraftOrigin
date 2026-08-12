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
    public sealed class CraftLivePad4Tests
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

        [Test]
        public void Pad4Scene_UsesBlackBackgroundAndEffectRoot()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad4ScenePath,
                scene =>
                {
                    CraftLivePadSceneRoot sceneRoot =
                        FindSingle<CraftLivePadSceneRoot>(scene);
                    CraftLiveHologramView hologram =
                        FindSingle<CraftLiveHologramView>(scene);

                    Assert.That(sceneRoot, Is.Not.Null);
                    Assert.That(hologram, Is.Not.Null);

                    SerializedObject rootProperties =
                        new SerializedObject(sceneRoot);
                    Assert.That(
                        rootProperties.FindProperty("backgroundColor")
                            .colorValue,
                        Is.EqualTo(Color.black));

                    SerializedObject hologramProperties =
                        new SerializedObject(hologram);
                    Assert.That(
                        hologramProperties.FindProperty("effectRoot")
                            .objectReferenceValue,
                        Is.Not.Null);
                    Assert.That(
                        hologramProperties.FindProperty("rotate")
                            .boolValue,
                        Is.True);
                    Assert.That(
                        hologramProperties
                            .FindProperty(
                                "createFallbackAttributeParticles")
                            .boolValue,
                        Is.True);
                });
        }

        [Test]
        public void AttributeMaterial_HasConfigurablePad4ParticleDefaults()
        {
            CraftLiveMaterialDefinition material =
                ScriptableObject.CreateInstance<
                    CraftLiveMaterialDefinition>();
            createdObjects.Add(material);

            Assert.That(material.Pad4ParticlePrefab, Is.Null);
            Assert.That(material.Pad4ParticleLocalPosition,
                Is.EqualTo(Vector3.zero));
            Assert.That(material.Pad4ParticleLocalRotation,
                Is.EqualTo(Quaternion.identity));
            Assert.That(material.Pad4ParticleLocalScale,
                Is.EqualTo(Vector3.one));
            Assert.That(material.TintPad4Particles, Is.True);
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T match in
                         root.GetComponentsInChildren<T>(true))
                {
                    Assert.That(
                        result,
                        Is.Null,
                        $"Multiple {typeof(T).Name} components found.");
                    result = match;
                }
            }

            return result;
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
