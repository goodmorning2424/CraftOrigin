using System.IO;
using System.Reflection;
using CraftOrigin.CraftLive;
using NUnit.Framework;
using UnityEngine;

namespace CraftOrigin.CraftLiveEditor.Tests
{
    public sealed class CraftLiveStep9Tests
    {
        [Test]
        public void CompareVersion_PrefersGenerationThenRevisionThenTimestamp()
        {
            CraftLiveRoomState older = new CraftLiveRoomState
            {
                revision = 4,
                updatedAtUnixMs = 200
            };
            CraftLiveRoomState newerRevision = new CraftLiveRoomState
            {
                revision = 5,
                updatedAtUnixMs = 100
            };
            CraftLiveRoomState newerTimestamp = new CraftLiveRoomState
            {
                revision = 4,
                updatedAtUnixMs = 300
            };
            CraftLiveRoomState nextGeneration = new CraftLiveRoomState
            {
                groupGeneration = 1,
                revision = 0,
                updatedAtUnixMs = 1
            };
            CraftLiveRoomState delayedPreviousGeneration =
                new CraftLiveRoomState
                {
                    groupGeneration = 0,
                    revision = 999,
                    updatedAtUnixMs = 999
                };

            Assert.That(
                CraftLiveRoomTransport.CompareVersion(
                    newerRevision,
                    older),
                Is.GreaterThan(0));
            Assert.That(
                CraftLiveRoomTransport.CompareVersion(
                    newerTimestamp,
                    older),
                Is.GreaterThan(0));
            Assert.That(
                CraftLiveRoomTransport.CompareVersion(
                    nextGeneration,
                    delayedPreviousGeneration),
                Is.GreaterThan(0));
            Assert.That(
                CraftLiveRoomTransport.CompareVersion(null, older),
                Is.LessThan(0));
        }

        [Test]
        public void RetryDelay_UsesBoundedExponentialBackoff()
        {
            Assert.That(
                CraftLiveRoomTransport.CalculateRetryDelay(0, 0.75f, 8f),
                Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(
                CraftLiveRoomTransport.CalculateRetryDelay(3, 0.75f, 8f),
                Is.EqualTo(6f).Within(0.001f));
            Assert.That(
                CraftLiveRoomTransport.CalculateRetryDelay(8, 0.75f, 8f),
                Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void SafeAreaViewport_FitsThreeByFourInsideSafeArea()
        {
            Rect viewport =
                CraftLiveWebPresentation.CalculateCameraViewport(
                    new Rect(0f, 20f, 768f, 984f),
                    new Vector2Int(768, 1024),
                    new Vector2(3f, 4f));

            Assert.That(viewport.x, Is.EqualTo(0.0195f).Within(0.001f));
            Assert.That(viewport.y, Is.EqualTo(20f / 1024f).Within(0.001f));
            Assert.That(viewport.width, Is.EqualTo(0.9609f).Within(0.001f));
            Assert.That(viewport.height, Is.EqualTo(984f / 1024f).Within(0.001f));
        }

        [Test]
        public void SafeAreaViewport_FallsBackWhenAreaIsInvalid()
        {
            Rect viewport =
                CraftLiveWebPresentation.CalculateCameraViewport(
                    Rect.zero,
                    new Vector2Int(768, 1024),
                    new Vector2(3f, 4f));

            Assert.That(viewport, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }

        [Test]
        public void LaunchConfig_ProvidesValidRecoveryDefaults()
        {
            CraftLiveLaunchConfig config =
                ScriptableObject.CreateInstance<CraftLiveLaunchConfig>();
            try
            {
                Assert.That(
                    config.InitialRetryDelaySeconds,
                    Is.GreaterThanOrEqualTo(0.25f));
                Assert.That(
                    config.MaximumRetryDelaySeconds,
                    Is.GreaterThanOrEqualTo(
                        config.InitialRetryDelaySeconds));
                Assert.That(config.CachePendingState, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void QrParser_AcceptsCaseInsensitiveUrlParameter()
        {
            string result = CraftLiveQrScanner.ParseMaterialId(
                "https://example.test/read?MaterialId=ore_attack");
            Assert.That(result, Is.EqualTo("ore_attack"));
        }

        [Test]
        public void ProductionTemplateAndQrBridge_ContainMobileGuards()
        {
            string root = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string template = File.ReadAllText(
                Path.Combine(
                    root,
                    "Assets/WebGLTemplates/CraftLive/index.html"));
            string bridge = File.ReadAllText(
                Path.Combine(
                    root,
                    "Assets/Plugins/WebGL/CraftLiveWebGL.jslib"));

            StringAssert.Contains("viewport-fit=cover", template);
            StringAssert.Contains("devicePixelRatio", template);
            StringAssert.Contains("isIPad ? 1.5 : 2", template);
            StringAssert.Contains("recoverFromStaleBuild", template);
            StringAssert.Contains("unknown data format", template);
            StringAssert.Contains(
                "type === \"error\" && recoverFromStaleBuild(message)",
                template);
            StringAssert.Contains(
                "searchParams.delete(recoveryParameter)",
                template);
            StringAssert.Contains("isSecureContext", bridge);
            StringAssert.Contains("BarcodeDetector", bridge);
        }

        [Test]
        public void RuntimeDiagnostics_HasInspectorEvents()
        {
            BindingFlags flags =
                BindingFlags.Instance | BindingFlags.NonPublic;
            Assert.That(
                typeof(CraftLiveRuntimeDiagnostics).GetField(
                    "onSummaryChanged",
                    flags),
                Is.Not.Null);
            Assert.That(
                typeof(CraftLiveRuntimeDiagnostics).GetField(
                    "onHealthyChanged",
                    flags),
                Is.Not.Null);
        }
    }
}
