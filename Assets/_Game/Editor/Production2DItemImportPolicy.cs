using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor
{
    /// <summary>
    /// IDEA-0016 class-level import policy for formal transparent item icons.
    /// It is intentionally scoped below Production2D/Items and never touches
    /// terrain or any other texture family.
    /// </summary>
    public sealed class Production2DItemImportPolicy : AssetPostprocessor
    {
        public const string Root = "Assets/_Game/Art/Production2D/Items/";
        public const int DeliverySize = 256;

        private void OnPreprocessTexture()
        {
            if (!IsItemPng(assetPath))
                return;

            Configure((TextureImporter)assetImporter);
        }

        [MenuItem("WasteCity/Art/Production 2D/Reimport Item Icons")]
        public static void ReimportItemIcons()
        {
            if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/')))
                return;

            string[] paths = Directory
                .GetFiles(Root, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.Ordinal);
            foreach (string rawPath in paths)
            {
                string path = rawPath.Replace('\\', '/');
                if (!IsItemPng(path))
                    continue;
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public static bool IsItemPng(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                path.StartsWith(Root, StringComparison.Ordinal) &&
                string.Equals(
                    Path.GetExtension(path),
                    ".png",
                    StringComparison.OrdinalIgnoreCase);
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
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
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
