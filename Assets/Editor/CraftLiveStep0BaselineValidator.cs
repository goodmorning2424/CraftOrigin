using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveStep0BaselineValidator
    {
        private const string MenuPath =
            "Tools/Craft-live/Validate Current Project";
        private const string LegacyMenuPath =
            "Tools/Craft-live/Step 0/Run Baseline Validation";
        private const string ReportDirectory = "Library/CraftLiveReports";
        private const string ReportPath =
            ReportDirectory + "/CurrentValidation_latest.md";
        private const string CraftScenePath = "Assets/Scenes/Craft.unity";
        private static readonly string[] Step2ScenePaths =
        {
            CraftLiveStep2SceneGenerator.BootstrapScenePath,
            CraftLiveStep2SceneGenerator.Pad1ScenePath,
            CraftLiveStep2SceneGenerator.Pad2ScenePath,
            CraftLiveStep2SceneGenerator.Pad3ScenePath,
            CraftLiveStep2SceneGenerator.Pad4ScenePath
        };

        [MenuItem(MenuPath)]
        public static void Run()
        {
            BaselineReport report = new BaselineReport();
            report.Info($"Unity version: {Application.unityVersion}");
            report.Info($"Active build target: {EditorUserBuildSettings.activeBuildTarget}");

            ValidateEnumContracts(report);
            ValidateBuildScenes(report);
            ValidateDataAssets(report);
            ValidateLoadedScenes(report);
            ValidateStep2SceneAssets(report);

            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllText(ReportPath, report.ToMarkdown(), Encoding.UTF8);

            string summary =
                $"Craft-live validation: errors={report.ErrorCount}, " +
                $"warnings={report.WarningCount}, report={ReportPath}";
            if (report.ErrorCount > 0)
            {
                Debug.LogError(summary);
            }
            else if (report.WarningCount > 0)
            {
                Debug.LogWarning(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        [MenuItem(LegacyMenuPath)]
        public static void RunFromLegacyMenu()
        {
            Run();
        }

        public static void RunBatch()
        {
            string scenePath =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    CraftLiveStep2SceneGenerator.BootstrapScenePath) != null
                    ? CraftLiveStep2SceneGenerator.BootstrapScenePath
                    : CraftScenePath;
            EditorSceneManager.OpenScene(
                scenePath,
                OpenSceneMode.Single);
            Run();
        }

        private static void ValidateEnumContracts(BaselineReport report)
        {
            CheckEnum(report, CraftLiveSlotId.Attribute, 0);
            CheckEnum(report, CraftLiveSlotId.Skill, 1);
            CheckEnum(report, CraftLiveSlotId.Top, 2);
            CheckEnum(report, CraftLiveSlotId.Right, 3);
            CheckEnum(report, CraftLiveSlotId.Left, 4);
            CheckEnum(report, CraftLiveSlotId.Bottom, 5);

            CheckEnum(report, CraftLiveStatType.None, 0);
            CheckEnum(report, CraftLiveStatType.AttackRate, 1);
            CheckEnum(report, CraftLiveStatType.DefenseRate, 2);
            CheckEnum(report, CraftLiveStatType.EvasionRate, 3);
            CheckEnum(report, CraftLiveStatType.ElementBoost, 4);
            CheckEnum(report, CraftLiveElementType.None, 0);
            CheckEnum(report, CraftLiveElementType.Fire, 1);
            CheckEnum(report, CraftLiveElementType.Freeze, 2);
            CheckEnum(report, CraftLiveElementType.Lightning, 3);
            CheckEnum(report, CraftLiveSkillType.None, 0);
            CheckEnum(report, CraftLiveSkillType.Luck, 1);
            CheckEnum(report, CraftLiveSkillType.DoubleStrike, 2);
            CheckEnum(report, CraftLiveSkillType.AutoHeal, 3);
            CheckEnum(report, CraftLiveSkillType.LifeOrb, 4);

            CraftLiveRoomState state = new CraftLiveRoomState();
            if (state.schemaVersion != CraftLiveRoomState.CurrentSchemaVersion)
            {
                report.Error(
                    $"Current RoomState schema must be " +
                    $"{CraftLiveRoomState.CurrentSchemaVersion}, " +
                    $"but was {state.schemaVersion}.");
            }
            else
            {
                report.Pass(
                    $"RoomState schema is " +
                    $"{CraftLiveRoomState.CurrentSchemaVersion}.");
            }
        }

        private static void ValidateBuildScenes(BaselineReport report)
        {
            int enabledSceneCount = 0;
            Dictionary<string, int> enabledSceneIndexes =
                new Dictionary<string, int>();
            int buildIndex = 0;
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }

                enabledSceneIndexes[scene.path] = buildIndex;
                enabledSceneCount++;
                buildIndex++;
            }

            if (enabledSceneCount == 0)
            {
                report.Error("No enabled Scene exists in Build Settings.");
            }
            else
            {
                report.Pass($"Enabled Build Scenes: {enabledSceneCount}.");
            }

            foreach (string scenePath in Step2ScenePaths)
            {
                if (!enabledSceneIndexes.ContainsKey(scenePath))
                {
                    report.Error(
                        $"{scenePath} is not enabled in Build Settings.");
                }
            }

            if (enabledSceneIndexes.TryGetValue(
                    CraftLiveStep2SceneGenerator.BootstrapScenePath,
                    out int bootstrapIndex) &&
                bootstrapIndex == 0)
            {
                report.Pass(
                    "CraftLiveBootstrap.unity is Build Index 0.");
            }
            else
            {
                report.Error(
                    "CraftLiveBootstrap.unity must be Build Index 0.");
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                report.Warning(
                    "Active build target is not WebGL. This is acceptable while " +
                    "editing, but WebGL must be selected before device tests.");
            }
        }

        private static void ValidateDataAssets(BaselineReport report)
        {
            string[] catalogGuids = AssetDatabase.FindAssets("t:CraftLiveCatalog");
            string[] rulesGuids = AssetDatabase.FindAssets("t:CraftLiveRules");
            string[] calibrationGuids =
                AssetDatabase.FindAssets("t:CraftLivePad4Calibration");
            string[] launchConfigGuids =
                AssetDatabase.FindAssets("t:CraftLiveLaunchConfig");
            if (catalogGuids.Length == 0)
            {
                report.Error("No CraftLiveCatalog asset was found.");
                return;
            }

            if (catalogGuids.Length > 1)
            {
                report.Warning(
                    $"Multiple CraftLiveCatalog assets were found: {catalogGuids.Length}.");
            }

            if (rulesGuids.Length == 0)
            {
                report.Error("No CraftLiveRules asset was found.");
            }
            else
            {
                report.Pass($"CraftLiveRules assets: {rulesGuids.Length}.");
                foreach (string guid in rulesGuids)
                {
                    CraftLiveRules rules =
                        AssetDatabase.LoadAssetAtPath<CraftLiveRules>(
                            AssetDatabase.GUIDToAssetPath(guid));
                    if (rules != null && rules.SessionDurationSeconds > 0f)
                    {
                        report.Pass(
                            $"Session duration: " +
                            $"{rules.SessionDurationSeconds:0.##} seconds.");
                    }
                }
            }

            if (calibrationGuids.Length == 0)
            {
                report.Warning(
                    "No CraftLivePad4Calibration asset was found.");
            }
            else
            {
                report.Pass(
                    $"Pad4 calibration assets: {calibrationGuids.Length}.");
            }

            if (launchConfigGuids.Length == 0)
            {
                report.Error("No CraftLiveLaunchConfig asset was found.");
            }
            else
            {
                report.Pass(
                    $"Launch configuration assets: " +
                    $"{launchConfigGuids.Length}.");
                if (launchConfigGuids.Length > 1)
                {
                    report.Warning(
                        "Multiple CraftLiveLaunchConfig assets were found. " +
                        "Confirm which one Bootstrap uses.");
                }
            }

            foreach (string guid in catalogGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CraftLiveCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<CraftLiveCatalog>(path);
                ValidateCatalog(report, catalog, path);
            }
        }

        private static void ValidateCatalog(
            BaselineReport report,
            CraftLiveCatalog catalog,
            string path)
        {
            if (catalog == null)
            {
                report.Error($"Catalog could not be loaded: {path}");
                return;
            }

            HashSet<string> materialIds = new HashSet<string>();
            HashSet<string> weaponIds = new HashSet<string>();
            int missingVisualCount = 0;
            int missingGameplayValueCount = 0;

            foreach (CraftLiveMaterialDefinition material in catalog.Materials)
            {
                if (material == null)
                {
                    report.Error($"{path} contains a null Material reference.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(material.MaterialId))
                {
                    report.Error($"Material has an empty ID: {material.name}");
                }
                else if (!materialIds.Add(material.MaterialId))
                {
                    report.Error($"Duplicate Material ID: {material.MaterialId}");
                }

                if (material.Icon == null ||
                    material.WorldPrefab == null ||
                    material.TransferTicketPrefab == null)
                {
                    missingVisualCount++;
                }

                if (material.Category == CraftLiveMaterialCategory.Upgrade &&
                    !material.StatModifiers.HasAnyValue)
                {
                    missingGameplayValueCount++;
                }
                else if (
                    material.Category == CraftLiveMaterialCategory.Attribute &&
                    (material.ElementEffect.type == CraftLiveElementType.None ||
                     material.ElementEffect.activationChancePercent <= 0f))
                {
                    missingGameplayValueCount++;
                }
                else if (
                    material.Category == CraftLiveMaterialCategory.Skill &&
                    material.SkillEffect.type == CraftLiveSkillType.None)
                {
                    missingGameplayValueCount++;
                }
            }

            foreach (CraftLiveWeaponDefinition weapon in catalog.Weapons)
            {
                if (weapon == null)
                {
                    report.Error($"{path} contains a null Weapon reference.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(weapon.WeaponId))
                {
                    report.Error($"Weapon has an empty ID: {weapon.name}");
                }
                else if (!weaponIds.Add(weapon.WeaponId))
                {
                    report.Error($"Duplicate Weapon ID: {weapon.WeaponId}");
                }

                if (weapon.Icon == null || weapon.WorkbenchPrefab == null)
                {
                    missingVisualCount++;
                }

                if (!weapon.BaseStats.HasAnyValue)
                {
                    missingGameplayValueCount++;
                }
            }

            if (catalog.Materials.Count == 0)
            {
                report.Error($"{path} has no Materials.");
            }
            else
            {
                report.Pass($"{path}: Materials={catalog.Materials.Count}.");
            }

            if (catalog.Weapons.Count == 0)
            {
                report.Error($"{path} has no Weapons.");
            }
            else
            {
                report.Pass($"{path}: Weapons={catalog.Weapons.Count}.");
            }

            if (missingVisualCount > 0)
            {
                report.Warning(
                    $"{missingVisualCount} definitions still need one or more " +
                    "Icon/Prefab assignments.");
            }

            if (missingGameplayValueCount > 0)
            {
                report.Warning(
                    $"{missingGameplayValueCount} definitions still need " +
                    "gameplay values or a supported effect type.");
            }
        }

        private static void ValidateLoadedScenes(BaselineReport report)
        {
            int loadedSceneCount = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                loadedSceneCount++;
                ValidateScene(report, scene);
            }

            if (loadedSceneCount == 0)
            {
                report.Warning("No loaded Scene was available for Scene validation.");
            }
        }

        private static void ValidateStep2SceneAssets(BaselineReport report)
        {
            foreach (string scenePath in Step2ScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    report.Error($"Required Scene is missing: {scenePath}");
                    continue;
                }

                Scene scene = SceneManager.GetSceneByPath(scenePath);
                bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Additive);
                }

                try
                {
                    if (openedForValidation)
                    {
                        ValidateScene(report, scene);
                    }

                    ValidateStep2Scene(report, scene, scenePath);
                }
                finally
                {
                    if (openedForValidation)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
        }

        private static void ValidateStep2Scene(
            BaselineReport report,
            Scene scene,
            string scenePath)
        {
            if (scenePath == CraftLiveStep2SceneGenerator.BootstrapScenePath)
            {
                ValidateBootstrapScene(report, scene);
                return;
            }

            CraftLiveRole expectedRole;
            Type expectedBindingsType;
            if (scenePath == CraftLiveStep2SceneGenerator.Pad1ScenePath)
            {
                expectedRole = CraftLiveRole.MaterialPad;
                expectedBindingsType = typeof(CraftLivePad1Bindings);
            }
            else if (scenePath == CraftLiveStep2SceneGenerator.Pad2ScenePath)
            {
                expectedRole = CraftLiveRole.WorkbenchPad;
                expectedBindingsType = typeof(CraftLivePad2Bindings);
            }
            else if (scenePath == CraftLiveStep2SceneGenerator.Pad3ScenePath)
            {
                expectedRole = CraftLiveRole.QrPad;
                expectedBindingsType = typeof(CraftLivePad3Bindings);
            }
            else
            {
                expectedRole = CraftLiveRole.HologramPad;
                expectedBindingsType = typeof(CraftLivePad4Bindings);
            }

            List<CraftLivePadSceneRoot> roots =
                FindInScene<CraftLivePadSceneRoot>(scene);
            if (roots.Count != 1)
            {
                report.Error(
                    $"{scenePath}: expected exactly one Pad Scene Root, " +
                    $"found {roots.Count}.");
                return;
            }

            CraftLivePadSceneRoot padRoot = roots[0];
            if (padRoot.Role != expectedRole)
            {
                report.Error(
                    $"{scenePath}: role is {padRoot.Role}, expected " +
                    $"{expectedRole}.");
            }

            if (padRoot.CameraAnchor == null)
            {
                report.Error($"{scenePath}: Camera Anchor is missing.");
            }

            List<Component> bindings = FindInScene(
                scene,
                expectedBindingsType);
            if (bindings.Count != 1)
            {
                report.Error(
                    $"{scenePath}: expected exactly one " +
                    $"{expectedBindingsType.Name}, found {bindings.Count}.");
                return;
            }

            ValidateBindings(report, scenePath, bindings[0]);
            if (expectedRole == CraftLiveRole.MaterialPad)
            {
                ValidatePad1Step3(report, scene);
                ValidatePad1Step56(report, scene);
            }
            else if (expectedRole == CraftLiveRole.WorkbenchPad)
            {
                ValidatePad2Step4(report, scene);
                ValidatePad2Step56(report, scene);
                ValidatePad2Step78(report, scene);
            }
            else if (expectedRole == CraftLiveRole.QrPad)
            {
                ValidatePad3Step56(report, scene);
            }
            else if (expectedRole == CraftLiveRole.HologramPad)
            {
                ValidatePad4Step78(report, scene);
            }
        }

        private static void ValidateBootstrapScene(
            BaselineReport report,
            Scene scene)
        {
            string scenePath = scene.path;
            List<CraftLiveSession> sessions =
                FindInScene<CraftLiveSession>(scene);
            List<CraftLiveBootstrap> bootstraps =
                FindInScene<CraftLiveBootstrap>(scene);
            List<CraftLiveRoomTransport> transports =
                FindInScene<CraftLiveRoomTransport>(scene);
            List<Camera> cameras = FindInScene<Camera>(scene);
            List<PhysicsRaycaster> physicsRaycasters =
                FindInScene<PhysicsRaycaster>(scene);
            List<CraftLiveSessionTimerController> timers =
                FindInScene<CraftLiveSessionTimerController>(scene);
            List<CraftLiveWebPresentation> presentations =
                FindInScene<CraftLiveWebPresentation>(scene);
            List<CraftLiveRuntimeDiagnostics> diagnostics =
                FindInScene<CraftLiveRuntimeDiagnostics>(scene);

            ValidateExactCount(
                report,
                scenePath,
                nameof(CraftLiveSession),
                sessions.Count);
            ValidateExactCount(
                report,
                scenePath,
                nameof(CraftLiveBootstrap),
                bootstraps.Count);
            ValidateExactCount(
                report,
                scenePath,
                nameof(CraftLiveRoomTransport),
                transports.Count);
            ValidateExactCount(
                report,
                scenePath,
                nameof(Camera),
                cameras.Count);
            ValidateExactCount(
                report,
                scenePath,
                nameof(PhysicsRaycaster),
                physicsRaycasters.Count);
            ValidateExactCount(
                report,
                scenePath,
                nameof(CraftLiveSessionTimerController),
                timers.Count);
            ValidateExactCount(
                report,
                scenePath,
                nameof(CraftLiveWebPresentation),
                presentations.Count);
            ValidateExactCount(
                report,
                scenePath,
                nameof(CraftLiveRuntimeDiagnostics),
                diagnostics.Count);

            if (bootstraps.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    bootstraps[0],
                    "session",
                    "transport",
                    "launchConfig",
                    "targetCamera");
            }

            if (transports.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    transports[0],
                    "session");
            }

            if (sessions.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    sessions[0],
                    "catalog",
                    "rules");
            }

            if (timers.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    timers[0],
                    "session",
                    "targetCamera");
            }

            if (diagnostics.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    diagnostics[0],
                    "session",
                    "transport");
            }
        }

        private static void ValidatePad1Step3(
            BaselineReport report,
            Scene scene)
        {
            List<CraftLivePad1GalleryController> galleries =
                FindInScene<CraftLivePad1GalleryController>(scene);
            List<CraftLivePad1MaterialPreview> previews =
                FindInScene<CraftLivePad1MaterialPreview>(scene);

            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad1GalleryController),
                galleries.Count);
            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad1MaterialPreview),
                previews.Count);

            if (galleries.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    galleries[0],
                    "bindings");
            }

            if (previews.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    previews[0],
                    "bindings");
            }
        }

        private static void ValidatePad2Step4(
            BaselineReport report,
            Scene scene)
        {
            List<CraftLivePad2WeaponCarousel> carousels =
                FindInScene<CraftLivePad2WeaponCarousel>(scene);
            List<CraftLivePad2PlacementController> placements =
                FindInScene<CraftLivePad2PlacementController>(scene);

            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad2WeaponCarousel),
                carousels.Count);
            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad2PlacementController),
                placements.Count);

            if (carousels.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    carousels[0],
                    "bindings");
            }

            if (placements.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    placements[0],
                    "bindings");
            }
        }

        private static void ValidatePad1Step56(
            BaselineReport report,
            Scene scene)
        {
            List<CraftLivePad1TransferController> controllers =
                FindInScene<CraftLivePad1TransferController>(scene);
            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad1TransferController),
                controllers.Count);
            if (controllers.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    controllers[0],
                    "bindings");
            }
        }

        private static void ValidatePad2Step56(
            BaselineReport report,
            Scene scene)
        {
            List<CraftLivePad2TransferReceiver> receivers =
                FindInScene<CraftLivePad2TransferReceiver>(scene);
            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad2TransferReceiver),
                receivers.Count);
            if (receivers.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    receivers[0],
                    "bindings");
            }
        }

        private static void ValidatePad3Step56(
            BaselineReport report,
            Scene scene)
        {
            List<CraftLivePad3Controller> controllers =
                FindInScene<CraftLivePad3Controller>(scene);
            List<CraftLiveQrScanner> scanners =
                FindInScene<CraftLiveQrScanner>(scene);
            List<CraftLiveStatusTubeView> tubes =
                FindInScene<CraftLiveStatusTubeView>(scene);

            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad3Controller),
                controllers.Count);
            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLiveQrScanner),
                scanners.Count);
            if (tubes.Count == 3)
            {
                report.Pass(
                    $"{scene.path}: exactly three " +
                    $"{nameof(CraftLiveStatusTubeView)} components.");
            }
            else
            {
                report.Error(
                    $"{scene.path}: expected three " +
                    $"{nameof(CraftLiveStatusTubeView)}, " +
                    $"found {tubes.Count}.");
            }

            if (controllers.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    controllers[0],
                    "bindings",
                    "qrScanner");
            }
        }

        private static void ValidatePad2Step78(
            BaselineReport report,
            Scene scene)
        {
            ValidateSingleBinding<
                CraftLiveLiquidFlowController>(report, scene);
            ValidateSingleBinding<
                CraftLiveHammerSynthesisController>(report, scene);
            ValidateSingleBinding<
                CraftLivePad2ResultController>(report, scene);
        }

        private static void ValidatePad4Step78(
            BaselineReport report,
            Scene scene)
        {
            List<CraftLiveHologramView> holograms =
                FindInScene<CraftLiveHologramView>(scene);
            List<CraftLivePad4Controller> controllers =
                FindInScene<CraftLivePad4Controller>(scene);
            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLiveHologramView),
                holograms.Count);
            ValidateExactCount(
                report,
                scene.path,
                nameof(CraftLivePad4Controller),
                controllers.Count);
            if (holograms.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    holograms[0],
                    "spawnRoot",
                    "calibration");
            }

            if (controllers.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    controllers[0],
                    "bindings",
                    "hologramView");
            }
        }

        private static void ValidateSingleBinding<T>(
            BaselineReport report,
            Scene scene)
            where T : Component
        {
            List<T> values = FindInScene<T>(scene);
            ValidateExactCount(
                report,
                scene.path,
                typeof(T).Name,
                values.Count);
            if (values.Count == 1)
            {
                ValidateRequiredReferences(
                    report,
                    scene.path,
                    values[0],
                    "bindings");
            }
        }

        private static void ValidateBindings(
            BaselineReport report,
            string scenePath,
            Component bindings)
        {
            if (bindings is CraftLivePad1Bindings)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    bindings,
                    "powerUpWall",
                    "skillWall",
                    "typeWall",
                    "materialPreviewRoot",
                    "hologramInfoRoot",
                    "transferQueueRoot",
                    "springLauncherRoot",
                    "railCameraAnchor",
                    "uiRoot");
            }
            else if (bindings is CraftLivePad2Bindings)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    bindings,
                    "weaponCarouselRoot",
                    "centerWeaponRoot",
                    "hammerRoot",
                    "upperLeftSlot",
                    "middleLeftSlot",
                    "upperRightSlot",
                    "middleRightSlot",
                    "lowerLeftSkillSlot",
                    "lowerRightAttributeSlot",
                    "transferArrivalRoot",
                    "liquidFlowRoot",
                    "resultHologramRoot",
                    "uiRoot");
            }
            else if (bindings is CraftLivePad3Bindings)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    bindings,
                    "attackTubeRoot",
                    "defenseTubeRoot",
                    "evasionTubeRoot",
                    "qrReadButtonRoot",
                    "qrFeedbackRoot",
                    "uiRoot");
            }
            else if (bindings is CraftLivePad4Bindings)
            {
                ValidateRequiredReferences(
                    report,
                    scenePath,
                    bindings,
                    "weaponDisplayRoot",
                    "effectRoot",
                    "uiRoot",
                    "calibration");
            }
        }

        private static void ValidateRequiredReferences(
            BaselineReport report,
            string scenePath,
            Component component,
            params string[] propertyNames)
        {
            SerializedObject serialized = new SerializedObject(component);
            int assignedCount = 0;
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property =
                    serialized.FindProperty(propertyName);
                if (property == null ||
                    property.propertyType !=
                    SerializedPropertyType.ObjectReference ||
                    property.objectReferenceValue == null)
                {
                    report.Error(
                        $"{scenePath}: {component.GetType().Name}." +
                        $"{propertyName} is missing.");
                    continue;
                }

                assignedCount++;
            }

            if (assignedCount == propertyNames.Length)
            {
                report.Pass(
                    $"{scenePath}: {component.GetType().Name} has all " +
                    $"{assignedCount} required references.");
            }
        }

        private static void ValidateExactCount(
            BaselineReport report,
            string scenePath,
            string componentName,
            int count)
        {
            if (count == 1)
            {
                report.Pass(
                    $"{scenePath}: exactly one {componentName}.");
            }
            else
            {
                report.Error(
                    $"{scenePath}: expected exactly one {componentName}, " +
                    $"found {count}.");
            }
        }

        private static void ValidateScene(BaselineReport report, Scene scene)
        {
            List<CraftLiveSession> sessions = FindInScene<CraftLiveSession>(scene);
            List<CraftLiveWorkbenchView> workbenches =
                FindInScene<CraftLiveWorkbenchView>(scene);

            if (sessions.Count == 0)
            {
                report.Info($"{scene.path}: no CraftLiveSession.");
            }
            else if (sessions.Count > 1)
            {
                report.Error(
                    $"{scene.path}: multiple CraftLiveSession components: " +
                    $"{sessions.Count}.");
            }
            else
            {
                CraftLiveSession session = sessions[0];
                if (session.Catalog == null)
                {
                    report.Error($"{scene.path}: Session Catalog is missing.");
                }

                if (session.Rules == null)
                {
                    report.Error($"{scene.path}: Session Rules are missing.");
                }
            }

            foreach (CraftLiveWorkbenchView workbench in workbenches)
            {
                ValidateWorkbench(report, scene.path, workbench);
            }
        }

        private static void ValidateWorkbench(
            BaselineReport report,
            string scenePath,
            CraftLiveWorkbenchView workbench)
        {
            SerializedObject serialized = new SerializedObject(workbench);
            SerializedProperty anchors = serialized.FindProperty("slotAnchors");
            if (anchors == null || anchors.arraySize != 6)
            {
                report.Error(
                    $"{scenePath}: Workbench must have exactly 6 slot anchors.");
                return;
            }

            HashSet<int> slotValues = new HashSet<int>();
            HashSet<Object> anchorObjects = new HashSet<Object>();
            int missingArrivalEntries = 0;
            for (int i = 0; i < anchors.arraySize; i++)
            {
                SerializedProperty element = anchors.GetArrayElementAtIndex(i);
                SerializedProperty slot = element.FindPropertyRelative("slot");
                SerializedProperty anchor = element.FindPropertyRelative("anchor");
                SerializedProperty arrival =
                    element.FindPropertyRelative("arrivalEntry");

                if (!slotValues.Add(slot.enumValueIndex))
                {
                    report.Error(
                        $"{scenePath}: duplicate slot entry at index {i}.");
                }

                Object anchorObject = anchor.objectReferenceValue;
                if (anchorObject == null)
                {
                    report.Error(
                        $"{scenePath}: slot anchor is missing at index {i}.");
                }
                else if (!anchorObjects.Add(anchorObject))
                {
                    report.Error(
                        $"{scenePath}: multiple slots share anchor " +
                        $"{anchorObject.name}.");
                }

                if (arrival == null || arrival.objectReferenceValue == null)
                {
                    missingArrivalEntries++;
                }
            }

            if (slotValues.Count == 6 && anchorObjects.Count == 6)
            {
                report.Pass($"{scenePath}: 6 unique Workbench slot anchors.");
            }

            if (missingArrivalEntries > 0)
            {
                report.Warning(
                    $"{scenePath}: {missingArrivalEntries} arrival entries are " +
                    "not assigned. TransferSpawn fallback remains active.");
            }
        }

        private static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results;
        }

        private static List<Component> FindInScene(
            Scene scene,
            Type componentType)
        {
            List<Component> results = new List<Component>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(
                    root.GetComponentsInChildren(componentType, true));
            }

            return results;
        }

        private static void CheckEnum<T>(
            BaselineReport report,
            T value,
            int expected)
            where T : Enum
        {
            int actual = Convert.ToInt32(value);
            if (actual != expected)
            {
                report.Error(
                    $"{typeof(T).Name}.{value} changed from {expected} to {actual}.");
            }
        }

        private sealed class BaselineReport
        {
            private readonly List<string> entries = new List<string>();

            public int ErrorCount { get; private set; }
            public int WarningCount { get; private set; }

            public void Pass(string message)
            {
                entries.Add($"- PASS: {message}");
            }

            public void Info(string message)
            {
                entries.Add($"- INFO: {message}");
            }

            public void Warning(string message)
            {
                WarningCount++;
                entries.Add($"- WARNING: {message}");
            }

            public void Error(string message)
            {
                ErrorCount++;
                entries.Add($"- ERROR: {message}");
            }

            public string ToMarkdown()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# Craft-live Current Project Validation");
                builder.AppendLine();
                builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine($"Errors: {ErrorCount}");
                builder.AppendLine($"Warnings: {WarningCount}");
                builder.AppendLine();
                foreach (string entry in entries)
                {
                    builder.AppendLine(entry);
                }

                return builder.ToString();
            }
        }
    }
}
