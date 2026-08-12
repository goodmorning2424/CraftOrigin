using System.IO;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveStep78Upgrader
    {
        private const string RequestPath =
            "Temp/CraftLiveStep78Upgrade.request";
        private static int stableFrames;

        [InitializeOnLoadMethod]
        private static void OnReload()
        {
            if (File.Exists(GetProjectPath(RequestPath)))
            {
                Schedule();
            }
        }

        [MenuItem(
            "Tools/Craft-live/Steps 7-8/Upgrade Synthesis and Session")]
        public static void Upgrade()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.BootstrapScenePath,
                scene =>
                {
                    CraftLiveSession session =
                        FindSingle<CraftLiveSession>(scene);
                    CraftLiveSessionTimerController timer =
                        Ensure<CraftLiveSessionTimerController>(
                            session.gameObject);
                    SetObject(timer, "session", session);
                    SetObject(
                        timer,
                        "targetCamera",
                        FindSingle<Camera>(scene));
                });
            WithScene(
                CraftLiveStep2SceneGenerator.Pad2ScenePath,
                scene =>
                {
                    CraftLivePad2Bindings bindings =
                        FindSingle<CraftLivePad2Bindings>(scene);
                    SetBindings(
                        Ensure<CraftLiveLiquidFlowController>(
                            bindings.gameObject),
                        bindings);
                    CraftLiveHammerStrikePresentation strikePresentation =
                        Ensure<CraftLiveHammerStrikePresentation>(
                            bindings.gameObject);
                    SetBindings(strikePresentation, bindings);
                    SetObject(
                        strikePresentation,
                        "weaponFocusTarget",
                        bindings.CenterWeaponRoot);
                    CraftLiveHammerSynthesisController hammerController =
                        Ensure<CraftLiveHammerSynthesisController>(
                            bindings.gameObject);
                    SetBindings(hammerController, bindings);
                    SetObject(
                        hammerController,
                        "presentation",
                        strikePresentation);
                    SetBindings(
                        Ensure<CraftLivePad2ResultController>(
                            bindings.gameObject),
                        bindings);
                    CraftLivePad2TransferReceiver receiver =
                        bindings.GetComponent<
                            CraftLivePad2TransferReceiver>();
                    if (receiver != null)
                    {
                        SetBool(
                            receiver,
                            "publishStatsAfterArrival",
                            false);
                        SetFloat(
                            receiver,
                            "completionHoldSeconds",
                            1.05f);
                    }
                });
            WithScene(
                CraftLiveStep2SceneGenerator.Pad4ScenePath,
                scene =>
                {
                    CraftLivePad4Bindings bindings =
                        FindSingle<CraftLivePad4Bindings>(scene);
                    CraftLiveHologramView hologram =
                        Ensure<CraftLiveHologramView>(
                            bindings.WeaponDisplayRoot.gameObject);
                    SetObject(
                        hologram,
                        "spawnRoot",
                        bindings.WeaponDisplayRoot);
                    SetObject(
                        hologram,
                        "calibration",
                        bindings.Calibration);
                    CraftLivePad4Controller controller =
                        Ensure<CraftLivePad4Controller>(
                            bindings.gameObject);
                    SetObject(controller, "bindings", bindings);
                    SetObject(
                        controller,
                        "hologramView",
                        hologram);
                });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SetBindings(
            Object component,
            CraftLivePad2Bindings bindings)
        {
            SetObject(component, "bindings", bindings);
        }

        private static void RunRequested()
        {
            string path = GetProjectPath(RequestPath);
            if (!File.Exists(path))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.playModeStateChanged -= HandlePlayMode;
                EditorApplication.playModeStateChanged += HandlePlayMode;
                return;
            }

            File.Delete(path);
            Upgrade();
            CraftLiveStep0BaselineValidator.Run();
            stableFrames = 0;
            EditorApplication.update -= WaitThenTest;
            EditorApplication.update += WaitThenTest;
        }

        private static void Schedule()
        {
            EditorApplication.delayCall -= RunRequested;
            EditorApplication.delayCall += RunRequested;
        }

        private static void HandlePlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.playModeStateChanged -= HandlePlayMode;
                EditorApplication.delayCall += RunRequested;
            }
        }

        private static void WaitThenTest()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                stableFrames = 0;
                return;
            }

            if (++stableFrames < 10)
            {
                return;
            }

            EditorApplication.update -= WaitThenTest;
            CraftLiveEditModeTestRunner.Run();
        }

        private static void WithScene(
            string path,
            System.Action<Scene> action)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
            {
                scene = EditorSceneManager.OpenScene(
                    path,
                    OpenSceneMode.Additive);
            }

            try
            {
                action(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            T found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T match in
                         root.GetComponentsInChildren<T>(true))
                {
                    if (found != null)
                    {
                        throw new System.InvalidOperationException(
                            $"{scene.path} has multiple {typeof(T).Name}.");
                    }

                    found = match;
                }
            }

            return found != null
                ? found
                : throw new System.InvalidOperationException(
                    $"{scene.path} has no {typeof(T).Name}.");
        }

        private static T Ensure<T>(GameObject target)
            where T : Component
        {
            T value = target.GetComponent<T>();
            return value != null ? value : target.AddComponent<T>();
        }

        private static void SetObject(
            Object target,
            string name,
            Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(
            Object target,
            string name,
            bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(
            Object target,
            string name,
            float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(name).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetProjectPath(string relative)
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", relative));
        }
    }
}
