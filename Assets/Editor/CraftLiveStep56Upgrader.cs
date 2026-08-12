using System.IO;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveStep56Upgrader
    {
        private const string MenuPath =
            "Tools/Craft-live/Steps 5-6/Upgrade Transfer and Pad3";
        private const string RequestPath =
            "Temp/CraftLiveStep56Upgrade.request";
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
            UpgradePad1();
            UpgradePad2();
            UpgradePad3();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Craft-live Steps 5-6: transfer, arrival, QR, " +
                "and status tubes are ready.");
        }

        private static void UpgradePad1()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad1ScenePath,
                scene =>
                {
                    CraftLivePad1Bindings bindings =
                        FindSingle<CraftLivePad1Bindings>(scene);
                    CraftLivePad1TransferController controller =
                        bindings.GetComponent<
                            CraftLivePad1TransferController>();
                    if (controller == null)
                    {
                        controller = bindings.gameObject.AddComponent<
                            CraftLivePad1TransferController>();
                    }

                    SetObject(controller, "bindings", bindings);
                    if (bindings.RailCameraAnchor != null &&
                        bindings.RailCameraAnchor.localPosition ==
                            Vector3.zero)
                    {
                        bindings.RailCameraAnchor.localPosition =
                            new Vector3(0.8f, 0f, -10f);
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                });
        }

        private static void UpgradePad2()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad2ScenePath,
                scene =>
                {
                    CraftLivePad2Bindings bindings =
                        FindSingle<CraftLivePad2Bindings>(scene);
                    CraftLivePad2TransferReceiver receiver =
                        bindings.GetComponent<
                            CraftLivePad2TransferReceiver>();
                    if (receiver == null)
                    {
                        receiver = bindings.gameObject.AddComponent<
                            CraftLivePad2TransferReceiver>();
                    }

                    SetObject(receiver, "bindings", bindings);
                    EditorSceneManager.MarkSceneDirty(scene);
                });
        }

        private static void UpgradePad3()
        {
            WithScene(
                CraftLiveStep2SceneGenerator.Pad3ScenePath,
                scene =>
                {
                    CraftLivePad3Bindings bindings =
                        FindSingle<CraftLivePad3Bindings>(scene);
                    CraftLiveQrScanner scanner =
                        bindings.QrReadButtonRoot.GetComponent<
                            CraftLiveQrScanner>();
                    if (scanner == null)
                    {
                        scanner =
                            bindings.QrReadButtonRoot.gameObject
                                .AddComponent<CraftLiveQrScanner>();
                    }

                    EnsureTube(
                        bindings.AttackTubeRoot,
                        CraftLiveStatType.AttackRate);
                    EnsureTube(
                        bindings.DefenseTubeRoot,
                        CraftLiveStatType.DefenseRate);
                    EnsureTube(
                        bindings.EvasionTubeRoot,
                        CraftLiveStatType.EvasionRate);

                    CraftLivePad3Controller controller =
                        bindings.GetComponent<CraftLivePad3Controller>();
                    if (controller == null)
                    {
                        controller = bindings.gameObject.AddComponent<
                            CraftLivePad3Controller>();
                    }

                    SetObject(controller, "bindings", bindings);
                    SetObject(controller, "qrScanner", scanner);
                    EditorSceneManager.MarkSceneDirty(scene);
                });
        }

        private static void EnsureTube(
            Transform root,
            CraftLiveStatType statType)
        {
            if (root == null)
            {
                return;
            }

            CraftLiveStatusTubeView tube =
                root.GetComponent<CraftLiveStatusTubeView>();
            if (tube == null)
            {
                tube = root.gameObject.AddComponent<
                    CraftLiveStatusTubeView>();
            }

            SetEnum(tube, "statType", (int)statType);
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
            EditorApplication.update -= WaitThenRunTests;
            EditorApplication.update += WaitThenRunTests;
        }

        private static void WaitThenRunTests()
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

            EditorApplication.update -= WaitThenRunTests;
            CraftLiveEditModeTestRunner.Run();
        }

        private static void WithScene(
            string scenePath,
            System.Action<Scene> action)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
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
                if (opened)
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

            if (result == null)
            {
                throw new System.InvalidOperationException(
                    $"{scene.path} has no {typeof(T).Name}.");
            }

            return result;
        }

        private static void SetObject(
            Object target,
            string propertyName,
            Object value)
        {
            SerializedObject serialized =
                new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new System.InvalidOperationException(
                    $"{target.GetType().Name}.{propertyName} missing.");
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(
            Object target,
            string propertyName,
            int value)
        {
            SerializedObject serialized =
                new SerializedObject(target);
            SerializedProperty property =
                serialized.FindProperty(propertyName);
            property.enumValueIndex = value;
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
