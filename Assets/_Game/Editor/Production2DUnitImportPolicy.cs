using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor
{
    /// <summary>
    /// IDEA-0023 import contract for the four transparent army-unit
    /// billboards. The policy owns only top-level PNG files in Units and does
    /// not scan characters, world markers, terrain, or nested work folders.
    /// </summary>
    public sealed class Production2DUnitImportPolicy : AssetPostprocessor
    {
        public const string Root = "Assets/_Game/Art/Production2D/Units/";
        public const int DeliverySize = 512;

        private void OnPreprocessTexture()
        {
            if (!IsOwnedPng(assetPath)) return;
            Configure((TextureImporter)assetImporter);
        }

        [MenuItem("WasteCity/Art/Production 2D/Reimport Unit Billboards")]
        public static void ReimportOwnedAssets()
        {
            string directory = Root.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(directory)) return;
            string[] rawPaths = Directory.GetFiles(
                directory,
                "*",
                SearchOption.TopDirectoryOnly);
            Array.Sort(rawPaths, StringComparer.Ordinal);
            foreach (string rawPath in rawPaths)
            {
                string path = rawPath.Replace('\\', '/');
                if (!IsOwnedPng(path)) continue;
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
        }

        public static bool IsOwnedPng(string path)
        {
            if (string.IsNullOrEmpty(path) ||
                !string.Equals(
                    Path.GetExtension(path),
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string normalized = path.Replace('\\', '/');
            string parent = Path.GetDirectoryName(normalized)
                ?.Replace('\\', '/')
                .TrimEnd('/');
            return string.Equals(
                parent,
                Root.TrimEnd('/'),
                StringComparison.Ordinal);
        }

        private static void Configure(TextureImporter importer)
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
            importer.maxTextureSize = DeliverySize;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.isReadable = false;
            importer.spritePixelsPerUnit = 100f;
            importer.spritePivot = Vector2.one * .5f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
