using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CraftOrigin.EditorTools
{
    /// <summary>
    /// Applies conservative WebGL-only texture limits. Source images and the
    /// default/Standalone import settings are deliberately left untouched.
    /// </summary>
    public static class CraftLiveWebTextureOptimizer
    {
        private const string WebPlatform = "WebGL";
        [MenuItem("Tools/Craft Origin/Optimize Textures for WebGL (Safe)")]
        public static void Optimize()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            var changed = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int index = 0; index < guids.Length; index++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                    int maxSize = GetWebMaxSize(path);
                    if (maxSize <= 0)
                        continue;

                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                        continue;

                    TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(WebPlatform);
                    if (settings.overridden &&
                        settings.maxTextureSize == maxSize &&
                        settings.format == TextureImporterFormat.Automatic &&
                        settings.textureCompression == TextureImporterCompression.Compressed &&
                        settings.compressionQuality == 50 &&
                        !settings.crunchedCompression)
                    {
                        continue;
                    }

                    settings.name = WebPlatform;
                    settings.overridden = true;
                    settings.maxTextureSize = maxSize;
                    settings.resizeAlgorithm = TextureResizeAlgorithm.Mitchell;
                    settings.format = TextureImporterFormat.Automatic;
                    settings.textureCompression = TextureImporterCompression.Compressed;
                    settings.compressionQuality = 50;
                    settings.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(settings);
                    changed.Add(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CraftLive] WebGL texture optimization complete. Updated {changed.Count} texture(s).\n" +
                      string.Join("\n", changed));
        }

        private static int GetWebMaxSize(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');

            // Imported editor/plugin icons do not enter the player and are left alone.
            if (normalized.StartsWith("Assets/ai.meshy/", StringComparison.OrdinalIgnoreCase))
                return 0;

            // Small effect sprites gain the most memory reduction and are already
            // blurred/soft by design. This only affects the WebGL build.
            if (normalized.StartsWith("Assets/GabrielAguiarProductions/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/UnityTechnologies/ParticlePack/EffectExamples/", StringComparison.OrdinalIgnoreCase))
                return 512;

            // Preserve UI legibility and model appearance while halving each 2K
            // texture's dimensions (roughly one quarter of its GPU memory).
            if (normalized.StartsWith("Assets/Buki/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/Pad1/Texture/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/CraftLiveData/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/MeshyImports/", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("Assets/Meshy_AI_", StringComparison.OrdinalIgnoreCase))
                return 1024;

            return 0;
        }
    }
}
