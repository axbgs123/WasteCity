using UnityEditor;
using UnityEngine;
using WasteCity.Graybox3D.Production;

namespace WasteCity.EditorTools
{
    public static class GrayboxResourceIconImporter3D
    {
        [MenuItem("WasteCity/Art/Configure 3D Resource Icons")]
        public static void Configure()
        {
            for (int index = 0;
                 index < GrayboxResourcePresentationCatalog3D
                     .ApprovedResourceIds.Length;
                 index++)
                ConfigurePath(
                    GrayboxResourcePresentationCatalog3D.IconAssetPath(
                        GrayboxResourcePresentationCatalog3D
                            .ApprovedResourceIds[index]));
            ConfigurePath(
                GrayboxResourcePresentationCatalog3D.FallbackIconAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigurePath(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                throw new UnityException(
                    "Missing resource icon texture: " + assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }
    }
}
