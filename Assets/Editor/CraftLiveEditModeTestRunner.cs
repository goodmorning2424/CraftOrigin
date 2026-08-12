using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace CraftOrigin.CraftLiveEditor
{
    public static class CraftLiveEditModeTestRunner
    {
        private const string MenuPath =
            "Tools/Craft-live/Run EditMode Tests";
        private const string RequestPath =
            "Temp/CraftLiveRunEditModeTests.request";
        private const string ReportDirectory = "Library/CraftLiveReports";
        private const string ReportPath =
            ReportDirectory + "/EditModeTests_latest.md";

        private static TestRunnerApi runner;
        private static ResultCallbacks callbacks;

        [InitializeOnLoadMethod]
        private static void RunRequestedTestsAfterReload()
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
                EditorApplication.isPlaying = false;
                return;
            }

            File.Delete(requestPath);
            EditorApplication.delayCall += Run;
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
            string requestPath = GetProjectPath(RequestPath);
            if (!File.Exists(requestPath))
            {
                return;
            }

            File.Delete(requestPath);
            EditorApplication.delayCall += Run;
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                string requestPath = GetProjectPath(RequestPath);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(requestPath));
                File.WriteAllText(
                    requestPath,
                    "queued-until-edit-mode",
                    Encoding.UTF8);
                EditorApplication.playModeStateChanged -=
                    HandlePlayModeStateChanged;
                EditorApplication.playModeStateChanged +=
                    HandlePlayModeStateChanged;
                EditorApplication.isPlaying = false;
                Debug.Log(
                    "Craft-live EditMode tests queued until Play Mode stops.");
                return;
            }

            if (runner != null)
            {
                Debug.LogWarning(
                    "Craft-live EditMode tests are already running.");
                return;
            }

            runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            callbacks = new ResultCallbacks(HandleFinished);
            runner.RegisterCallbacks(callbacks);
            ExecutionSettings settings = new ExecutionSettings(
                new Filter
                {
                    testMode = TestMode.EditMode
                });
            runner.Execute(settings);
            Debug.Log("Craft-live EditMode tests started.");
        }

        private static void HandleFinished(TestRunSummary summary)
        {
            string reportDirectory = GetProjectPath(ReportDirectory);
            string reportPath = GetProjectPath(ReportPath);
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllText(
                reportPath,
                summary.ToMarkdown(),
                Encoding.UTF8);

            string message =
                $"Craft-live EditMode tests: total={summary.Total}, " +
                $"passed={summary.Passed}, failed={summary.Failed}, " +
                $"skipped={summary.Skipped}, report={ReportPath}";
            if (summary.Failed > 0)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }

            if (runner != null)
            {
                ScriptableObject.DestroyImmediate(runner);
            }

            runner = null;
            callbacks = null;
        }

        private static string GetProjectPath(string relativePath)
        {
            return Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", relativePath));
        }

        private sealed class ResultCallbacks : ICallbacks
        {
            private readonly Action<TestRunSummary> onFinished;
            private readonly List<string> failures = new List<string>();

            public ResultCallbacks(Action<TestRunSummary> onFinished)
            {
                this.onFinished = onFinished;
            }

            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                onFinished?.Invoke(new TestRunSummary
                {
                    Total = result.PassCount +
                            result.FailCount +
                            result.SkipCount +
                            result.InconclusiveCount,
                    Passed = result.PassCount,
                    Failed = result.FailCount,
                    Skipped = result.SkipCount,
                    Inconclusive = result.InconclusiveCount,
                    DurationSeconds = result.Duration,
                    Failures = new List<string>(failures)
                });
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite &&
                    result.ResultState.StartsWith(
                        "Failed",
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        $"{result.FullName}: {result.Message}\n" +
                        result.StackTrace);
                }
            }
        }

        private sealed class TestRunSummary
        {
            public int Total;
            public int Passed;
            public int Failed;
            public int Skipped;
            public int Inconclusive;
            public double DurationSeconds;
            public List<string> Failures;

            public string ToMarkdown()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("# Craft-live EditMode Tests");
                builder.AppendLine();
                builder.AppendLine(
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                builder.AppendLine($"Total: {Total}");
                builder.AppendLine($"Passed: {Passed}");
                builder.AppendLine($"Failed: {Failed}");
                builder.AppendLine($"Skipped: {Skipped}");
                builder.AppendLine($"Inconclusive: {Inconclusive}");
                builder.AppendLine(
                    $"Duration: {DurationSeconds:0.###} seconds");

                if (Failures != null && Failures.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("## Failures");
                    foreach (string failure in Failures)
                    {
                        builder.AppendLine();
                        builder.AppendLine("```text");
                        builder.AppendLine(failure);
                        builder.AppendLine("```");
                    }
                }

                return builder.ToString();
            }
        }
    }
}
