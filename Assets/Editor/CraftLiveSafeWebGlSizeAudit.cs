using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CraftOrigin.EditorTools
{
    /// <summary>
    /// Builds the currently configured WebGL player without regenerating scenes,
    /// prefabs, catalogs, or player settings. This is intentionally separate from
    /// the production upgrader so size checks cannot alter gameplay content.
    /// </summary>
    public static class CraftLiveSafeWebGlSizeAudit
    {
        private const ulong MaximumSafeBuildBytes = 100UL * 1024UL * 1024UL;
        private const string MenuPath =
            "Tools/Craft Origin/Build WebGL Size Audit (No Scene Changes)";
        private const string RequestPath =
            "Temp/CraftLiveSafeWebGlSizeAudit.request";
        private const string ComparisonRequestPath =
            "Temp/CraftLiveMeshCompressionComparison.request";
        private const string OutputPath = "Builds/CraftLiveWebGL";
        private const string ReportPath =
            "Library/CraftLiveReports/WebGLSizeAudit_latest.md";
        private const string ComparisonReportPath =
            "Library/CraftLiveReports/MeshCompressionComparison_latest.md";

        private static readonly string[] HighDensityModelPaths =
        {
            "Assets/Meshy_AI_Emerald_Spiral_Decant_0813115423_texture.obj",
            "Assets/Meshy_AI_Crimson_Jewel_Pyramid_0813120040_texture.obj",
            "Assets/Meshy_AI_Azure_Quill_on_Glass_0813114807_texture.obj",
            "Assets/Meshy_AI_Test_Tube_0811162953_texture.obj"
        };

        [InitializeOnLoadMethod]
        private static void RunRequestedAuditAfterReload()
        {
            string request = ProjectPath(RequestPath);
            if (!File.Exists(request))
            {
                string comparisonRequest = ProjectPath(ComparisonRequestPath);
                if (!File.Exists(comparisonRequest))
                    return;

                File.Delete(comparisonRequest);
                EditorApplication.delayCall += CompareMeshCompression;
                return;
            }

            File.Delete(request);
            EditorApplication.delayCall += Build;
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                throw new InvalidOperationException(
                    "WebGL must be the active build target before a size audit.");
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled build scenes found.");

            string output = ProjectPath(OutputPath);
            Directory.CreateDirectory(output);
            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None
                });

            WriteReport(report);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Safe WebGL size audit failed: {report.summary.result}");
            }

            if (report.summary.totalSize > MaximumSafeBuildBytes)
            {
                throw new InvalidOperationException(
                    $"WebGL build is {report.summary.totalSize / (1024f * 1024f):0.00} MiB; " +
                    $"the mobile-safe limit is {MaximumSafeBuildBytes / (1024f * 1024f):0} MiB. " +
                    "Check newly referenced high-density meshes and textures before publishing.");
            }

            Debug.Log(
                $"[CraftLive] Safe WebGL size audit completed: " +
                $"{report.summary.totalSize / (1024f * 1024f):0.00} MiB");
        }

        [MenuItem("Tools/Craft Origin/Compare High Density Mesh Compression")]
        public static void CompareMeshCompression()
        {
            string[] scenes = GetEnabledScenes();
            var importers = HighDensityModelPaths
                .Select(path => AssetImporter.GetAtPath(path) as ModelImporter)
                .ToArray();
            if (importers.Any(importer => importer == null))
                throw new InvalidOperationException("A high-density model importer is missing.");

            ModelImporterMeshCompression[] original = importers
                .Select(importer => importer.meshCompression)
                .ToArray();
            BuildReport uncompressedReport = null;
            try
            {
                for (int index = 0; index < importers.Length; index++)
                {
                    importers[index].meshCompression = ModelImporterMeshCompression.Off;
                    importers[index].SaveAndReimport();
                }

                string output = ProjectPath(
                    "Temp/CraftLiveSizeAudit/UncompressedWebGL");
                Directory.CreateDirectory(output);
                uncompressedReport = BuildPipeline.BuildPlayer(
                    new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = output,
                        target = BuildTarget.WebGL,
                        options = BuildOptions.None
                    });
            }
            finally
            {
                for (int index = 0; index < importers.Length; index++)
                {
                    importers[index].meshCompression = original[index];
                    importers[index].SaveAndReimport();
                }
            }

            if (uncompressedReport == null ||
                uncompressedReport.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "The uncompressed comparison build did not succeed.");
            }

            ulong optimizedBytes = (ulong)Directory
                .EnumerateFiles(ProjectPath(OutputPath), "*", SearchOption.AllDirectories)
                .Sum(path => new FileInfo(path).Length);
            ulong uncompressedBytes = uncompressedReport.summary.totalSize;
            ulong reductionBytes = uncompressedBytes > optimizedBytes
                ? uncompressedBytes - optimizedBytes
                : 0;
            string reportFile = ProjectPath(ComparisonReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportFile));
            File.WriteAllText(
                reportFile,
                "# Mesh Compression A/B Comparison\n\n" +
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"Uncompressed: {uncompressedBytes / (1024f * 1024f):0.00} MiB\n" +
                $"Low compression: {optimizedBytes / (1024f * 1024f):0.00} MiB\n" +
                $"Reduction: {reductionBytes / (1024f * 1024f):0.00} MiB\n" +
                $"Errors: {uncompressedReport.summary.totalErrors}\n" +
                $"Warnings: {uncompressedReport.summary.totalWarnings}\n" +
                "Final importer state: restored to Low compression.\n");
        }

        private static string[] GetEnabledScenes()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled build scenes found.");
            return scenes;
        }

        private static void WriteReport(BuildReport report)
        {
            string reportFile = ProjectPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportFile));
            File.WriteAllText(
                reportFile,
                "# CraftLive Safe WebGL Size Audit\n\n" +
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                $"Result: {report.summary.result}\n" +
                $"Errors: {report.summary.totalErrors}\n" +
                $"Warnings: {report.summary.totalWarnings}\n" +
                $"Size: {report.summary.totalSize / (1024f * 1024f):0.00} MiB\n" +
                $"Duration: {report.summary.totalTime.TotalSeconds:0.00} seconds\n" +
                $"Output: {OutputPath}\n");
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", relativePath));
        }
    }
}
