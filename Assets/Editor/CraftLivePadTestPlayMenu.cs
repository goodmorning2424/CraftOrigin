using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLivePadTestPlayMenu
    {
        private const string MenuRoot = "Tools/Craft-live/Test Play/";
        private const string BootstrapScenePath =
            "Assets/Scenes/CraftLive/CraftLiveBootstrap.unity";
        private const string LaunchConfigPath =
            "Assets/CraftLiveData/DefaultCraftLiveLaunchConfig.asset";

        [MenuItem(MenuRoot + "Pad 1 - Material Gallery", false, 1)]
        public static void PlayPad1()
        {
            Play(CraftLiveRole.MaterialPad);
        }

        [MenuItem(MenuRoot + "Pad 2 - Weapon Craft", false, 2)]
        public static void PlayPad2()
        {
            Play(CraftLiveRole.WorkbenchPad);
        }

        [MenuItem(MenuRoot + "Pad 3 - Status QR", false, 3)]
        public static void PlayPad3()
        {
            Play(CraftLiveRole.QrPad);
        }

        [MenuItem(MenuRoot + "Pad 4 - Hologram", false, 4)]
        public static void PlayPad4()
        {
            Play(CraftLiveRole.HologramPad);
        }

        public static void Play(CraftLiveRole role)
        {
            if (!CraftLiveBootstrap.IsTestablePadRole(role))
            {
                Debug.LogError($"Pad test play does not support {role}.");
                return;
            }

            if (EditorApplication.isPlaying)
            {
                CraftLiveBootstrap bootstrap =
                    Object.FindAnyObjectByType<CraftLiveBootstrap>();
                if (bootstrap == null)
                {
                    Debug.LogError(
                        "Craft-live: Bootstrap was not found in Play Mode. " +
                        "Stop Play Mode, then select the pad again.");
                    return;
                }

                bootstrap.SetIntegratedEditorTestWorkflow(true);
                bootstrap.SwitchPad(role);
                return;
            }

            CraftLiveLaunchConfig config =
                AssetDatabase.LoadAssetAtPath<CraftLiveLaunchConfig>(
                    LaunchConfigPath);
            if (config == null)
            {
                Debug.LogError(
                    $"Craft-live: Launch Config is missing at " +
                    $"'{LaunchConfigPath}'.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            SerializedObject serializedConfig = new SerializedObject(config);
            SerializedProperty editorRole =
                serializedConfig.FindProperty("editorRole");
            if (editorRole == null)
            {
                Debug.LogError(
                    "Craft-live: Launch Config has no editorRole setting.");
                return;
            }

            editorRole.enumValueIndex = (int)role;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);

            EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            SessionState.SetBool(
                CraftLiveBootstrap.IntegratedEditorTestSessionKey,
                true);
            EditorApplication.isPlaying = true;
        }
    }
}
