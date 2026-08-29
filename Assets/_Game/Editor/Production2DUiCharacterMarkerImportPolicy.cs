using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor
{
    /// <summary>
    /// IDEA-0016 import policy owned only by the formal UI, character and
    /// world-marker transparent sprite directories. It never scans or changes
    /// items, technologies, buildings, terrain or other texture families.
    /// </summary>
    public sealed class Production2DUiCharacterMarkerImportPolicy : AssetPostprocessor
    {
        public const string UiRoot = "Assets/_Game/Art/Production2D/UI/";
        public const string CharacterRoot = "Assets/_Game/Art/Production2D/Characters/";
        public const string WorldMarkerRoot = "Assets/_Game/Art/Production2D/WorldMarkers/";

        private static readonly string[] Roots =
        {
            UiRoot,
            CharacterRoot,
            WorldMarkerRoot
        };

        private static readonly IReadOnlyDictionary<string, Vector4> Borders =
            new Dictionary<string, Vector4>(StringComparer.Ordinal)
            {
                { UiRoot + "ui-primary-panel.png", new Vector4(32f, 32f, 32f, 32f) },
                { UiRoot + "ui-secondary-card.png", new Vector4(24f, 24f, 24f, 24f) },
                { UiRoot + "ui-primary-button.png", new Vector4(28f, 28f, 28f, 28f) },
                { UiRoot + "ui-technology-node.png", new Vector4(24f, 24f, 24f, 24f) },
                { UiRoot + "ui-terminal-divider.png", new Vector4(24f, 12f, 24f, 12f) }
            };

        private void OnPreprocessTexture()
        {
            if (!IsOwnedPng(assetPath))
                return;

            Configure((TextureImporter)assetImporter, assetPath);
        }

        [MenuItem("WasteCity/Art/Production 2D/Reimport UI Character And World Marker Sprites")]
        public static void ReimportOwnedAssets()
        {
            foreach (string root in Roots)
            {
                if (!AssetDatabase.IsValidFolder(root.TrimEnd('/')))
                    continue;

                string[] rawPaths = Directory.GetFiles(
                    root,
                    "*",
                    SearchOption.TopDirectoryOnly);
                Array.Sort(rawPaths, StringComparer.Ordinal);
                foreach (string rawPath in rawPaths)
                {
                    string path = rawPath.Replace('\\', '/');
                    if (!IsOwnedPng(path))
                        continue;

                    AssetDatabase.ImportAsset(
                        path,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }
            }
        }

        public static void ReimportOwnedAssetsForBatch()
        {
            ReimportOwnedAssets();
            AssetDatabase.SaveAssets();
        }

        public static bool IsOwnedPng(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (string root in Roots)
            {
                if (path.StartsWith(root, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        public static Vector4 ExpectedBorderForAsset(string path)
        {
            return path != null && Borders.TryGetValue(path, out Vector4 border)
                ? border
                : Vector4.zero;
        }

        private static void Configure(TextureImporter importer, string path)
        {
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = path.EndsWith(
                    "ui-research-tree-background.png",
                    StringComparison.Ordinal)
                ? 2048
                : path.StartsWith(CharacterRoot, StringComparison.Ordinal) ||
                  path.EndsWith("world-marker-resource-node.png",
                      StringComparison.Ordinal)
                    ? 512
                    : 256;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.isReadable = false;
            importer.spritePixelsPerUnit = 100f;
            importer.spritePivot = Vector2.one * .5f;
            importer.spriteBorder = ExpectedBorderForAsset(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
