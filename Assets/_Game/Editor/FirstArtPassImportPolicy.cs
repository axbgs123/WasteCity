using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Editor
{
    public sealed class FirstArtPassImportPolicy : AssetPostprocessor
    {
        public const string Root = "Assets/_Game/Art/FirstPass/";

        private const string BaseColorSuffix = "_BaseColor.png";
        private const string NormalSuffix = "_Normal.png";
        private const string MaskSuffix = "_Mask.png";
        private const string HeightSuffix = "_Height.png";

        private void OnPreprocessTexture()
        {
            if (!IsFirstPassAsset(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            if (assetPath.EndsWith(BaseColorSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            if (assetPath.EndsWith(NormalSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer);
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            if (assetPath.EndsWith(MaskSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }

            if (assetPath.EndsWith(HeightSuffix, StringComparison.Ordinal))
            {
                ConfigureCommonTexture(importer);
                importer.textureType = TextureImporterType.SingleChannel;
                importer.sRGBTexture = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }

        private void OnPreprocessModel()
        {
            if (!IsFirstPassAsset(assetPath) ||
                !assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
        }

        [MenuItem("WasteCity/Art/Reimport First Art Pass")]
        public static void ReimportAll()
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { Root.TrimEnd('/') });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string extension = Path.GetExtension(path);
                if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.SaveAssets();
        }

        private static bool IsFirstPassAsset(string path)
        {
            return path.StartsWith(Root, StringComparison.Ordinal);
        }

        private static void ConfigureCommonTexture(TextureImporter importer)
        {
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
        }
    }
}
