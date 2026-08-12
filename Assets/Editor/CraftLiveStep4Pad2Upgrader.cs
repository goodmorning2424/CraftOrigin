using System.IO;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveStep4Pad2Upgrader
    {
        private const int UpgradeVersion = 4;
        private const string MenuPath =
            "Tools/Craft-live/Step 4/Upgrade Pad2 Workbench";
        private const string RequestPath =
            "Temp/CraftLiveStep4Upgrade.request";
        private const int StableEditorFramesBeforeTests = 10;
        private static int stableEditorFrameCount;

        [InitializeOnLoadMethod]
        private static void RunRequestedUpgradeAfterReload()
        {
            if (File.Exists(GetProjectPath(RequestPath)))
            {
                ScheduleRequestedUpgrade();
            }
        }

        [MenuItem(MenuPath)]
        public static void Upgrade()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad2ScenePath,
                scene =>
                {
                    CraftLivePad2Bindings bindings =
                        FindSingle<CraftLivePad2Bindings>(scene);
                    if (bindings == null)
                    {
                        throw new System.InvalidOperationException(
                            "Pad2 Scene has no CraftLivePad2Bindings.");
                    }

                    CraftLivePad2PlacementController placement =
                        bindings.GetComponent<
                            CraftLivePad2PlacementController>();
                    if (placement == null)
                    {
                        placement = bindings.gameObject.AddComponent<
                            CraftLivePad2PlacementController>();
                    }

                    CraftLivePad2WeaponCarousel carousel =
                        bindings.WeaponCarouselRoot.GetComponent<
                            CraftLivePad2WeaponCarousel>();
                    if (carousel == null)
                    {
                        carousel =
                            bindings.WeaponCarouselRoot.gameObject
                                .AddComponent<
                                    CraftLivePad2WeaponCarousel>();
                    }

                    SetObject(placement, "bindings", bindings);
                    SetObject(carousel, "bindings", bindings);
                    EditorSceneManager.MarkSceneDirty(scene);
                });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Craft-live Step {UpgradeVersion}: " +
                "Pad2 workbench runtime is ready.");
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
                foreach (T match in
                         root.GetComponentsInChildren<T>(true))
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
