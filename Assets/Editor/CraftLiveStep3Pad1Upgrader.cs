using System.IO;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveStep3Pad1Upgrader
    {
        private const int UpgradeVersion = 3;
        private const string MenuPath =
            "Tools/Craft-live/Step 3/Upgrade Pad1 Gallery";
        private const string RequestPath =
            "Temp/CraftLiveStep3Upgrade.request";
        private const int StableEditorFramesBeforeTests = 10;
        private static int stableEditorFrameCount;

        [InitializeOnLoadMethod]
        private static void RunRequestedUpgradeAfterReload()
        {
            string requestPath = GetProjectPath(RequestPath);
            if (!File.Exists(requestPath))
            {
                return;
            }

            ScheduleRequestedUpgrade();
        }

        [MenuItem(MenuPath)]
        public static void Upgrade()
        {
            UpgradePad1Scene();
            UpgradeBootstrapScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Craft-live Step {UpgradeVersion}: " +
                "Pad1 gallery runtime is ready.");
        }

        private static void RunRequestedUpgrade()
        {
            string requestPath = GetProjectPath(RequestPath);
            if (!File.Exists(requestPath))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -=
                    HandlePlayModeStateChanged;
                EditorApplication.playModeStateChanged +=
                    HandlePlayModeStateChanged;
                return;
            }

            File.Delete(requestPath);
            Upgrade();
            CraftLiveStep0BaselineValidator.Run();
            ScheduleTestsWhenEditorIsStable();
        }

        private static void ScheduleRequestedUpgrade()
        {
            EditorApplication.delayCall -= RunRequestedUpgrade;
            EditorApplication.delayCall += RunRequestedUpgrade;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -=
                    HandlePlayModeStateChanged;
                EditorApplication.playModeStateChanged +=
                    HandlePlayModeStateChanged;
            }
        }

        private static void HandlePlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -=
                HandlePlayModeStateChanged;
            EditorApplication.delayCall += RunRequestedUpgrade;
        }

        private static void ScheduleTestsWhenEditorIsStable()
        {
            stableEditorFrameCount = 0;
            EditorApplication.update -= WaitForStableEditorThenRunTests;
            EditorApplication.update += WaitForStableEditorThenRunTests;
        }

        private static void WaitForStableEditorThenRunTests()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                stableEditorFrameCount = 0;
                return;
            }

            stableEditorFrameCount++;
            if (stableEditorFrameCount <
                StableEditorFramesBeforeTests)
            {
                return;
            }

            EditorApplication.update -= WaitForStableEditorThenRunTests;
            CraftLiveEditModeTestRunner.Run();
        }

        private static void UpgradePad1Scene()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad1ScenePath,
                scene =>
                {
                    CraftLivePad1Bindings bindings =
                        FindSingle<CraftLivePad1Bindings>(scene);
                    if (bindings == null)
                    {
                        throw new System.InvalidOperationException(
                            "Pad1 Scene has no CraftLivePad1Bindings.");
                    }

                    GameObject target = bindings.gameObject;
                    CraftLivePad1GalleryController gallery =
                        target.GetComponent<
                            CraftLivePad1GalleryController>();
                    if (gallery == null)
                    {
                        gallery = target.AddComponent<
                            CraftLivePad1GalleryController>();
                    }

                    CraftLivePad1MaterialPreview preview =
                        target.GetComponent<
                            CraftLivePad1MaterialPreview>();
                    if (preview == null)
                    {
                        preview = target.AddComponent<
                            CraftLivePad1MaterialPreview>();
                    }

                    SetObject(gallery, "bindings", bindings);
                    SetObject(preview, "bindings", bindings);
                    EditorSceneManager.MarkSceneDirty(scene);
                });
        }

        private static void UpgradeBootstrapScene()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.BootstrapScenePath,
                scene =>
                {
                    Camera camera = FindSingle<Camera>(scene);
                    if (camera == null)
                    {
                        throw new System.InvalidOperationException(
                            "Bootstrap Scene has no Camera.");
                    }

                    PhysicsRaycaster[] raycasters =
                        camera.GetComponents<PhysicsRaycaster>();
                    if (raycasters.Length == 0)
                    {
                        camera.gameObject.AddComponent<PhysicsRaycaster>();
                    }
                    else
                    {
                        for (int i = 1; i < raycasters.Length; i++)
                        {
                            Object.DestroyImmediate(raycasters[i]);
                        }
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                });
        }

        private static void WithScene(
            string scenePath,
            System.Action<Scene> action)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForUpgrade = !scene.IsValid() || !scene.isLoaded;
            if (openedForUpgrade)
            {
                scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Additive);
            }

            try
            {
                action(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedForUpgrade)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T[] matches = root.GetComponentsInChildren<T>(true);
                foreach (T match in matches)
                {
                    if (result != null)
                    {
                        throw new System.InvalidOperationException(
                            $"{scene.path} has multiple " +
                            $"{typeof(T).Name} components.");
                    }

                    result = match;
                }
            }

            return result;
        }

        private static void SetObject(
            Object target,
            string propertyName,
            Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} was not found.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetProjectPath(string relativePath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "..",
                    relativePath));
        }
    }
}
