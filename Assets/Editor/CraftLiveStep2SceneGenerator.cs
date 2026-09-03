using System.Collections.Generic;
using System.IO;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveStep2SceneGenerator
    {
        public const string SceneFolder = "Assets/Scenes/CraftLive";
        public const string BootstrapScenePath =
            SceneFolder + "/CraftLiveBootstrap.unity";
        public const string Pad1ScenePath =
            SceneFolder + "/Pad1_MaterialGallery.unity";
        public const string Pad2ScenePath =
            SceneFolder + "/Pad2_Workbench.unity";
        public const string Pad3ScenePath =
            SceneFolder + "/Pad3_StatusQr.unity";
        public const string Pad4ScenePath =
            SceneFolder + "/Pad4_Hologram.unity";
        public const string LaunchConfigPath =
            "Assets/CraftLiveData/DefaultCraftLiveLaunchConfig.asset";

        private const string MenuPath =
            "Tools/Craft-live/Step 2/Create Or Update Four-Pad Skeleton";
        private const string RequestPath =
            "Temp/CraftLiveStep2Generate.request";

        [InitializeOnLoadMethod]
        private static void RunRequestedGenerationAfterReload()
        {
            string requestPath = GetProjectPath(RequestPath);
            if (!File.Exists(requestPath))
            {
                return;
            }

            File.Delete(requestPath);
            EditorApplication.delayCall += RunRequestedGeneration;
        }

        private static void RunRequestedGeneration()
        {
            CreateOrUpdate();
            CraftLiveStep0BaselineValidator.Run();
            CraftLiveEditModeTestRunner.Run();
        }

        [MenuItem(MenuPath)]
        public static void CreateOrUpdate()
        {
            EnsureFolder("Assets/Scenes", "CraftLive");
            CraftLiveLaunchConfig config = LoadOrCreateLaunchConfig();
            CraftLivePad4Calibration calibration =
                AssetDatabase.LoadAssetAtPath<CraftLivePad4Calibration>(
                    "Assets/CraftLiveData/DefaultPad4Calibration.asset");

            int created = 0;
            created += CreateIfMissing(
                BootstrapScenePath,
                () => CreateBootstrapScene(config));
            created += CreateIfMissing(
                Pad1ScenePath,
                CreatePad1Scene);
            created += CreateIfMissing(
                Pad2ScenePath,
                CreatePad2Scene);
            created += CreateIfMissing(
                Pad3ScenePath,
                CreatePad3Scene);
            created += CreateIfMissing(
                Pad4ScenePath,
                () => CreatePad4Scene(calibration));

            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Craft-live Step 2: four-pad skeleton ready. " +
                $"Created scenes={created}");
        }

        private static int CreateIfMissing(
            string path,
            System.Action create)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                return 0;
            }

            create();
            return 1;
        }

        private static void CreateBootstrapScene(
            CraftLiveLaunchConfig launchConfig)
        {
            Scene scene = CreateAdditiveScene();
            GameObject root = CreateRoot(scene, "CraftLive_Bootstrap");

            CraftLiveSession session =
                root.AddComponent<CraftLiveSession>();
            CraftLiveRoomTransport transport =
                root.AddComponent<CraftLiveRoomTransport>();
            CraftLiveBootstrap bootstrap =
                root.AddComponent<CraftLiveBootstrap>();
            CraftLiveWebPresentation presentation =
                root.AddComponent<CraftLiveWebPresentation>();

            GameObject cameraObject = CreateChild(
                root.transform,
                "Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.localPosition =
                new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.03f, 0.03f, 0.03f);
            cameraObject.AddComponent<AudioListener>();

            GameObject lightObject = CreateChild(
                root.transform,
                "Shared Directional Light");
            lightObject.transform.localRotation =
                Quaternion.Euler(50f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;

            GameObject eventSystemObject = CreateChild(
                root.transform,
                "EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            CraftLiveCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CraftLiveCatalog>(
                    "Assets/CraftLiveData/DefaultCraftLiveCatalog.asset");
            CraftLiveRules rules =
                AssetDatabase.LoadAssetAtPath<CraftLiveRules>(
                    "Assets/CraftLiveData/DefaultCraftLiveRules.asset");

            SetObject(session, "catalog", catalog);
            SetObject(session, "rules", rules);
            SetEnum(session, "role", (int)CraftLiveRole.Auto);
            SetObject(transport, "session", session);
            SetBool(transport, "useFirebase", false);
            transport.enabled = false;
            SetObject(bootstrap, "session", session);
            SetObject(bootstrap, "transport", transport);
            SetObject(bootstrap, "launchConfig", launchConfig);
            SetObject(bootstrap, "targetCamera", camera);
            SetObject(presentation, "targetCamera", camera);

            SaveAndClose(scene, BootstrapScenePath);
        }

        private static void CreatePad1Scene()
        {
            Scene scene = CreateAdditiveScene();
            GameObject root = CreatePadRoot(
                scene,
                "Pad1_MaterialGallery_Root",
                CraftLiveRole.MaterialPad);
            CraftLivePad1Bindings bindings =
                root.AddComponent<CraftLivePad1Bindings>();

            Transform gallery = CreateChild(
                root.transform,
                "GalleryWalls").transform;
            SetObject(
                bindings,
                "powerUpWall",
                CreateChild(gallery, "PowerUpWall").transform);
            SetObject(
                bindings,
                "skillWall",
                CreateChild(gallery, "SkillWall").transform);
            SetObject(
                bindings,
                "typeWall",
                CreateChild(gallery, "TypeWall").transform);
            SetObject(
                bindings,
                "materialPreviewRoot",
                CreateChild(
                    root.transform,
                    "MaterialPreviewRoot").transform);
            SetObject(
                bindings,
                "hologramInfoRoot",
                CreateChild(
                    root.transform,
                    "HologramInfoRoot").transform);
            SetObject(
                bindings,
                "transferQueueRoot",
                CreateChild(
                    root.transform,
                    "TransferQueueRoot").transform);
            SetObject(
                bindings,
                "springLauncherRoot",
                CreateChild(
                    root.transform,
                    "SpringLauncherRoot").transform);
            SetObject(
                bindings,
                "railCameraAnchor",
                CreateChild(
                    root.transform,
                    "RailCameraAnchor").transform);
            SetObject(
                bindings,
                "uiRoot",
                CreateChild(root.transform, "UIRoot").transform);

            SaveAndClose(scene, Pad1ScenePath);
        }

        private static void CreatePad2Scene()
        {
            Scene scene = CreateAdditiveScene();
            GameObject root = CreatePadRoot(
                scene,
                "Pad2_Workbench_Root",
                CraftLiveRole.WorkbenchPad);
            CraftLivePad2Bindings bindings =
                root.AddComponent<CraftLivePad2Bindings>();

            SetObject(
                bindings,
                "weaponCarouselRoot",
                CreateChild(
                    root.transform,
                    "WeaponCarouselRoot").transform);
            SetObject(
                bindings,
                "centerWeaponRoot",
                CreateChild(
                    root.transform,
                    "CenterWeaponRoot",
                    new Vector3(0f, 1.15f, 0f)).transform);
            SetObject(
                bindings,
                "hammerRoot",
                CreateChild(
                    root.transform,
                    "HammerRoot",
                    new Vector3(0f, 1.15f, 0f)).transform);

            Transform slots = CreateChild(
                root.transform,
                "MaterialSlots").transform;
            SetObject(
                bindings,
                "upperLeftSlot",
                CreateChild(
                    slots,
                    "UpperLeft_BaseSlot",
                    CraftLivePad2SlotLayout.Get(
                        CraftLivePad2PhysicalSlot.UpperLeft)
                        .DefaultPosition).transform);
            SetObject(
                bindings,
                "middleLeftSlot",
                CreateChild(
                    slots,
                    "MiddleLeft_BaseSlot",
                    CraftLivePad2SlotLayout.Get(
                        CraftLivePad2PhysicalSlot.MiddleLeft)
                        .DefaultPosition).transform);
            SetObject(
                bindings,
                "upperRightSlot",
                CreateChild(
                    slots,
                    "UpperRight_BaseSlot",
                    CraftLivePad2SlotLayout.Get(
                        CraftLivePad2PhysicalSlot.UpperRight)
                        .DefaultPosition).transform);
            SetObject(
                bindings,
                "middleRightSlot",
                CreateChild(
                    slots,
                    "MiddleRight_BaseSlot",
                    CraftLivePad2SlotLayout.Get(
                        CraftLivePad2PhysicalSlot.MiddleRight)
                        .DefaultPosition).transform);
            SetObject(
                bindings,
                "lowerLeftSkillSlot",
                CreateChild(
                    slots,
                    "LowerLeft_SkillSlot",
                    CraftLivePad2SlotLayout.Get(
                        CraftLivePad2PhysicalSlot.LowerLeft)
                        .DefaultPosition).transform);
            SetObject(
                bindings,
                "lowerRightAttributeSlot",
                CreateChild(
                    slots,
                    "LowerRight_AttributeSlot",
                    CraftLivePad2SlotLayout.Get(
                        CraftLivePad2PhysicalSlot.LowerRight)
                        .DefaultPosition).transform);

            SetObject(
                bindings,
                "transferArrivalRoot",
                CreateChild(
                    root.transform,
                    "TransferArrivalRoot",
                    new Vector3(0f, 5f, 0f)).transform);
            SetObject(
                bindings,
                "liquidFlowRoot",
                CreateChild(
                    root.transform,
                    "LiquidFlowRoot").transform);
            SetObject(
                bindings,
                "resultHologramRoot",
                CreateChild(
                    root.transform,
                    "ResultHologramRoot").transform);
            SetObject(
                bindings,
                "uiRoot",
                CreateChild(root.transform, "UIRoot").transform);

            SaveAndClose(scene, Pad2ScenePath);
        }

        private static void CreatePad3Scene()
        {
            Scene scene = CreateAdditiveScene();
            GameObject root = CreatePadRoot(
                scene,
                "Pad3_StatusQr_Root",
                CraftLiveRole.QrPad);
            CraftLivePad3Bindings bindings =
                root.AddComponent<CraftLivePad3Bindings>();

            Transform tubes = CreateChild(
                root.transform,
                "StatusTubes").transform;
            SetObject(
                bindings,
                "attackTubeRoot",
                CreateChild(
                    tubes,
                    "AttackTubeRoot",
                    new Vector3(-2f, 1.5f, 0f)).transform);
            SetObject(
                bindings,
                "defenseTubeRoot",
                CreateChild(
                    tubes,
                    "DefenseTubeRoot",
                    new Vector3(0f, 1.5f, 0f)).transform);
            SetObject(
                bindings,
                "evasionTubeRoot",
                CreateChild(
                    tubes,
                    "EvasionTubeRoot",
                    new Vector3(2f, 1.5f, 0f)).transform);
            SetObject(
                bindings,
                "qrReadButtonRoot",
                CreateChild(
                    root.transform,
                    "QrReadButtonRoot",
                    new Vector3(0f, -3f, 0f)).transform);
            SetObject(
                bindings,
                "qrFeedbackRoot",
                CreateChild(
                    root.transform,
                    "QrFeedbackRoot",
                    new Vector3(0f, -1.5f, 0f)).transform);
            SetObject(
                bindings,
                "uiRoot",
                CreateChild(root.transform, "UIRoot").transform);

            SaveAndClose(scene, Pad3ScenePath);
        }

        private static void CreatePad4Scene(
            CraftLivePad4Calibration calibration)
        {
            Scene scene = CreateAdditiveScene();
            GameObject root = CreatePadRoot(
                scene,
                "Pad4_Hologram_Root",
                CraftLiveRole.HologramPad);
            SetColor(
                root.GetComponent<CraftLivePadSceneRoot>(),
                "backgroundColor",
                Color.black);
            SetBool(
                root.GetComponent<CraftLivePadSceneRoot>(),
                "mirrorHorizontally",
                true);
            CraftLivePad4Bindings bindings =
                root.AddComponent<CraftLivePad4Bindings>();

            SetObject(
                bindings,
                "weaponDisplayRoot",
                CreateChild(
                    root.transform,
                    "WeaponDisplayRoot").transform);
            SetObject(
                bindings,
                "effectRoot",
                CreateChild(root.transform, "EffectRoot").transform);
            SetObject(
                bindings,
                "uiRoot",
                CreateChild(root.transform, "UIRoot").transform);
            SetObject(bindings, "calibration", calibration);

            SaveAndClose(scene, Pad4ScenePath);
        }

        private static Scene CreateAdditiveScene()
        {
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (!loadedScene.isLoaded ||
                    !string.IsNullOrEmpty(loadedScene.path))
                {
                    continue;
                }

                if (loadedScene.rootCount > 0)
                {
                    string backupPath =
                        AssetDatabase.GenerateUniqueAssetPath(
                            SceneFolder +
                            "/PreStep2_Untitled_Backup.unity");
                    if (!EditorSceneManager.SaveScene(
                            loadedScene,
                            backupPath))
                    {
                        throw new System.InvalidOperationException(
                            "Craft-live Step 2 could not preserve the " +
                            "untitled Scene before generation.");
                    }

                    Debug.LogWarning(
                        "Craft-live Step 2 preserved the untitled Scene at " +
                        $"{backupPath} before generating new Scenes.");
                    continue;
                }

                if (SceneManager.sceneCount == 1)
                {
                    return EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);
                }

                EditorSceneManager.CloseScene(loadedScene, true);
            }

            return EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
        }

        private static GameObject CreatePadRoot(
            Scene scene,
            string name,
            CraftLiveRole role)
        {
            GameObject root = CreateRoot(scene, name);
            CraftLivePadSceneRoot padRoot =
                root.AddComponent<CraftLivePadSceneRoot>();
            Transform cameraAnchor = CreateChild(
                root.transform,
                "CameraAnchor",
                new Vector3(0f, 0f, -10f)).transform;
            SetEnum(padRoot, "role", (int)role);
            SetObject(padRoot, "cameraAnchor", cameraAnchor);
            return root;
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            GameObject root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static GameObject CreateChild(
            Transform parent,
            string name,
            Vector3? localPosition = null)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition =
                localPosition ?? Vector3.zero;
            return child;
        }

        private static void SaveAndClose(Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);
            if (SceneManager.sceneCount > 1)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static CraftLiveLaunchConfig LoadOrCreateLaunchConfig()
        {
            CraftLiveLaunchConfig config =
                AssetDatabase.LoadAssetAtPath<CraftLiveLaunchConfig>(
                    LaunchConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<CraftLiveLaunchConfig>();
            AssetDatabase.CreateAsset(config, LaunchConfigPath);
            return config;
        }

        private static void UpdateBuildSettings()
        {
            string[] requiredPaths =
            {
                BootstrapScenePath,
                Pad1ScenePath,
                Pad2ScenePath,
                Pad3ScenePath,
                Pad4ScenePath
            };
            List<EditorBuildSettingsScene> scenes =
                new List<EditorBuildSettingsScene>();
            foreach (string path in requiredPaths)
            {
                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            HashSet<string> required =
                new HashSet<string>(requiredPaths);
            foreach (EditorBuildSettingsScene existing in
                     EditorBuildSettings.scenes)
            {
                if (required.Contains(existing.path))
                {
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(
                    existing.path,
                    false));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void SetObject(
            Object target,
            string propertyName,
            Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(
            Object target,
            string propertyName,
            bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(
            Object target,
            string propertyName,
            Color value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).colorValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(
            Object target,
            string propertyName,
            int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetProjectPath(string relativePath)
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
