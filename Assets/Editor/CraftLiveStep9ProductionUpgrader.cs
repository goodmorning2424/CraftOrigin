using System;
using System.IO;
using System.Text;
using CraftOrigin.CraftLive;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CraftOrigin.CraftLiveEditor
{
    // Applies the repeatable production profile without replacing pad scenes.
    public static class CraftLiveStep9ProductionUpgrader
    {
        public const string BuildDirectory = "Builds/CraftLiveWebGL";
        public const string ProductionReportPath =
            "Library/CraftLiveReports/ProductionReadiness_latest.md";

        private const string UpgradeMenuPath =
            "Tools/Craft-live/Step 9/Apply WebGL Production Settings";
        private const string ValidateMenuPath =
            "Tools/Craft-live/Step 9/Validate Production Readiness";
        private const string BuildMenuPath =
            "Tools/Craft-live/Step 9/Build WebGL";
        private const string UpgradeRequestPath =
            "Temp/CraftLiveStep9Upgrade.request";
        private const string BuildRequestPath =
            "Temp/CraftLiveStep9Build.request";
        private const int StableEditorFramesBeforeTests = 10;

        private static int stableEditorFrameCount;

        [InitializeOnLoadMethod]
        private static void RunRequestedWorkAfterReload()
        {
            if (File.Exists(GetProjectPath(UpgradeRequestPath)))
            {
                EditorApplication.delayCall += RunRequestedUpgrade;
            }

            if (File.Exists(GetProjectPath(BuildRequestPath)))
            {
                EditorApplication.delayCall += RunRequestedBuild;
            }
        }

        [MenuItem(UpgradeMenuPath)]
        public static void ApplyProductionSettings()
        {
            ConfigurePlayerSettings();
            UpgradeBootstrapScene();
            CraftLiveStep2SceneGenerator.CreateOrUpdate();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateProductionReadiness();
            Debug.Log(
                "Craft-live Step 9: WebGL production settings are ready.");
        }

        [MenuItem(ValidateMenuPath)]
        public static int ValidateProductionReadiness()
        {
            ProductionReport report = new ProductionReport();
            report.Info($"Unity version: {Application.unityVersion}");
            report.Check(
                EditorUserBuildSettings.activeBuildTarget ==
                    BuildTarget.WebGL,
                "Active build target is WebGL.");
            report.Check(
                !PlayerSettings.runInBackground,
                "Run In Background is disabled for mobile power saving.");
            report.Check(
                PlayerSettings.defaultWebScreenWidth == 768 &&
                PlayerSettings.defaultWebScreenHeight == 1024,
                "Default WebGL size is 768 x 1024 (3:4 portrait).");
            report.Check(
                PlayerSettings.WebGL.template == "PROJECT:CraftLive",
                "CraftLive custom WebGL template is selected.");
            report.Check(
                PlayerSettings.WebGL.dataCaching,
                "WebGL data caching is enabled.");
            report.Check(
                PlayerSettings.WebGL.decompressionFallback,
                "WebGL decompression fallback is enabled.");
            report.Check(
                PlayerSettings.WebGL.compressionFormat ==
                    WebGLCompressionFormat.Brotli,
                "WebGL compression is Brotli.");
            report.Check(
                PlayerSettings.WebGL.nameFilesAsHashes,
                "WebGL build files use content hashes to prevent stale cache mixing.");
            report.Check(
                PlayerSettings.WebGL.initialMemorySize >= 256,
                "Initial WebGL heap is at least 256 MB for iPad startup.");
            report.Check(
                PlayerSettings.WebGL.maximumMemorySize <= 1024,
                "Maximum WebGL heap stays within the mobile-safe 1 GB limit.");

            string templatePath =
                "Assets/WebGLTemplates/CraftLive/index.html";
            string stylePath =
                "Assets/WebGLTemplates/CraftLive/TemplateData/style.css";
            string simulatorPath =
                "Assets/WebGLTemplates/CraftLive/simulator.html";
            string bridgePath =
                "Assets/Plugins/WebGL/CraftLiveWebGL.jslib";
            report.Check(
                File.Exists(GetProjectPath(templatePath)),
                "Custom WebGL index template exists.");
            report.Check(
                File.Exists(GetProjectPath(stylePath)),
                "Custom WebGL stylesheet exists.");
            report.Check(
                File.Exists(GetProjectPath(simulatorPath)),
                "Three-pad Firebase simulator exists.");
            report.Check(
                File.Exists(GetProjectPath(bridgePath)),
                "WebGL QR bridge exists.");

            CraftLiveLaunchConfig launchConfig =
                AssetDatabase.LoadAssetAtPath<CraftLiveLaunchConfig>(
                    CraftLiveStep2SceneGenerator.LaunchConfigPath);
            report.Check(
                launchConfig != null,
                "Launch Config exists.");
            if (launchConfig != null)
            {
                report.Check(
                    launchConfig.FirebaseDatabaseUrl.StartsWith(
                        "https://",
                        StringComparison.OrdinalIgnoreCase),
                    "Firebase URL uses HTTPS.");
                report.Check(
                    launchConfig.CachePendingState,
                    "Pending room state cache is enabled.");
                report.Info(
                    $"Firebase poll={launchConfig.PollIntervalSeconds:0.##}s, " +
                    $"timeout={launchConfig.RequestTimeoutSeconds:0.##}s, " +
                    $"retry={launchConfig.InitialRetryDelaySeconds:0.##}-" +
                    $"{launchConfig.MaximumRetryDelaySeconds:0.##}s.");
            }

            ValidateBootstrap(report);
            report.Write(GetProjectPath(ProductionReportPath));
            if (report.Errors > 0)
            {
                Debug.LogError(
                    $"Craft-live production validation failed: " +
                    $"{report.Errors} error(s). See {ProductionReportPath}");
            }
            else
            {
                Debug.Log(
                    $"Craft-live production validation passed. " +
                    $"See {ProductionReportPath}");
            }

            return report.Errors;
        }

        [MenuItem(BuildMenuPath)]
        public static void BuildWebGl()
        {
            ApplyProductionSettings();
            if (ValidateProductionReadiness() > 0)
            {
                throw new BuildFailedException(
                    "Craft-live production validation failed.");
            }

            Directory.CreateDirectory(GetProjectPath(BuildDirectory));
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = GetEnabledBuildScenes(),
                locationPathName = GetProjectPath(BuildDirectory),
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteBuildReport(report);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"WebGL build failed: {report.summary.result}");
            }

            Debug.Log(
                $"Craft-live WebGL build completed: {BuildDirectory}");
        }

        private static void ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "goodmorning2424";
            PlayerSettings.productName = "Craft-live";
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultWebScreenWidth = 768;
            PlayerSettings.defaultWebScreenHeight = 1024;
            PlayerSettings.WebGL.template = "PROJECT:CraftLive";
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.compressionFormat =
                WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.memorySize = 256;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 1024;
            PlayerSettings.WebGL.memoryGrowthMode =
                WebGLMemoryGrowthMode.Geometric;
        }

        private static void UpgradeBootstrapScene()
        {
            string path =
                CraftLiveStep2SceneGenerator.BootstrapScenePath;
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
                CraftLiveSession session = FindSingle<CraftLiveSession>(scene);
                CraftLiveRoomTransport transport =
                    FindSingle<CraftLiveRoomTransport>(scene);
                CraftLiveWebPresentation presentation =
                    FindSingle<CraftLiveWebPresentation>(scene);
                CraftLiveRuntimeDiagnostics diagnostics =
                    FindOptional<CraftLiveRuntimeDiagnostics>(scene);
                if (diagnostics == null)
                {
                    diagnostics =
                        session.gameObject.AddComponent<
                            CraftLiveRuntimeDiagnostics>();
                }

                SetObject(diagnostics, "session", session);
                SetObject(diagnostics, "transport", transport);
                SetBool(presentation, "respectSafeArea", true);
                SetInt(presentation, "targetFrameRate", 30);
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

        private static void ValidateBootstrap(ProductionReport report)
        {
            string path =
                CraftLiveStep2SceneGenerator.BootstrapScenePath;
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
                report.Check(
                    Count<CraftLiveWebPresentation>(scene) == 1,
                    "Bootstrap has exactly one Web Presentation.");
                report.Check(
                    Count<CraftLiveRuntimeDiagnostics>(scene) == 1,
                    "Bootstrap has exactly one Runtime Diagnostics.");
                report.Check(
                    Count<CraftLiveRoomTransport>(scene) == 1,
                    "Bootstrap has exactly one Room Transport.");
            }
            finally
            {
                if (opened)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void RunRequestedUpgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunRequestedUpgrade;
                return;
            }

            File.Delete(GetProjectPath(UpgradeRequestPath));
            ApplyProductionSettings();
            CraftLiveStep0BaselineValidator.Run();
            ScheduleTestsWhenEditorIsStable();
        }

        private static void RunRequestedBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RunRequestedBuild;
                return;
            }

            File.Delete(GetProjectPath(BuildRequestPath));
            BuildWebGl();
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

        private static string[] GetEnabledBuildScenes()
        {
            EditorBuildSettingsScene[] enabled = Array.FindAll(
                    EditorBuildSettings.scenes,
                    scene => scene.enabled);
            return Array.ConvertAll(enabled, scene => scene.path);
        }

        private static void WriteBuildReport(BuildReport report)
        {
            string path = GetProjectPath(
                "Library/CraftLiveReports/WebGLBuild_latest.md");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildSummary summary = report.summary;
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Craft-live WebGL Build");
            builder.AppendLine();
            builder.AppendLine(
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Result: {summary.result}");
            builder.AppendLine($"Errors: {summary.totalErrors}");
            builder.AppendLine($"Warnings: {summary.totalWarnings}");
            builder.AppendLine(
                $"Size: {summary.totalSize / (1024f * 1024f):0.##} MiB");
            builder.AppendLine(
                $"Duration: {summary.totalTime.TotalSeconds:0.##} seconds");
            builder.AppendLine($"Output: {BuildDirectory}");
            File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
        }

        private static T FindSingle<T>(Scene scene)
            where T : Component
        {
            T result = FindOptional<T>(scene);
            if (result == null)
            {
                throw new InvalidOperationException(
                    $"{scene.path} has no {typeof(T).Name}.");
            }

            if (Count<T>(scene) != 1)
            {
                throw new InvalidOperationException(
                    $"{scene.path} has multiple {typeof(T).Name} components.");
            }

            return result;
        }

        private static T FindOptional<T>(Scene scene)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private static int Count<T>(Scene scene)
            where T : Component
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private static void SetObject(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(
            UnityEngine.Object target,
            string propertyName,
            bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(
            UnityEngine.Object target,
            string propertyName,
            int value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetProjectPath(string relativePath)
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", relativePath));
        }

        private sealed class ProductionReport
        {
            private readonly StringBuilder body = new StringBuilder();

            public int Errors { get; private set; }

            public void Info(string message)
            {
                body.AppendLine($"- INFO: {message}");
            }

            public void Check(bool condition, string message)
            {
                if (!condition)
                {
                    Errors++;
                }

                body.AppendLine(
                    $"- {(condition ? "PASS" : "ERROR")}: {message}");
            }

            public void Write(string path)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                StringBuilder output = new StringBuilder();
                output.AppendLine(
                    "# Craft-live Production Readiness");
                output.AppendLine();
                output.AppendLine(
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                output.AppendLine($"Errors: {Errors}");
                output.AppendLine();
                output.Append(body);
                File.WriteAllText(path, output.ToString(), Encoding.UTF8);
            }
        }
    }

}
