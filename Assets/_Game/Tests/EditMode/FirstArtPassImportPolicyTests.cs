using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WasteCity.Tests
{
    public sealed class FirstArtPassImportPolicyTests
    {
        private const string TerrainRoot = "Assets/_Game/Art/FirstPass/Environment/Terrain";

        private static readonly string[] TerrainTypes =
        {
            "Wasteland",
            "Rocky",
            "Wetland",
            "Crystal",
            "Ruins",
            "DeepWater",
            "Cliff",
        };

        private static readonly string[] ModelPaths =
        {
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_BoundaryEdge.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_BrokenPipe.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_CrackedFloorSlab.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_DrainageChannel.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_RebarConcreteBlock.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_RubblePile_A.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_RubblePile_B.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Ruins/Models/SM_Ruins_WornMarkingPlate.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_EndCap.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_InnerCorner.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_OuterCorner.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_Straight_A.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_Straight_B.fbx",
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Cliff/Models/SM_Cliff_TopCap.fbx",
        };

        public static IEnumerable<string> TerrainCases => TerrainTypes;

        public static IEnumerable<string> ModelCases => ModelPaths;

        [TestCaseSource(nameof(TerrainCases))]
        public void BaseColor_UsesSrgbTilingContract(string terrain)
        {
            TextureImporter importer = RequireTextureImporter(TexturePath(terrain, "BaseColor"));

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            AssertCommonTextureContract(importer);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
        }

        [TestCaseSource(nameof(TerrainCases))]
        public void Normal_UsesNormalMapLinearContract(string terrain)
        {
            TextureImporter importer = RequireTextureImporter(TexturePath(terrain, "Normal"));

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap));
            Assert.That(importer.sRGBTexture, Is.False);
            AssertCommonTextureContract(importer);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
        }

        [TestCaseSource(nameof(TerrainCases))]
        public void Mask_UsesLosslessLinearRgbaContract(string terrain)
        {
            TextureImporter importer = RequireTextureImporter(TexturePath(terrain, "Mask"));

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            AssertCommonTextureContract(importer);
        }

        [TestCaseSource(nameof(TerrainCases))]
        public void Height_UsesLosslessLinearSingleChannelContract(string terrain)
        {
            TextureImporter importer = RequireTextureImporter(TexturePath(terrain, "Height"));

            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.SingleChannel));
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            AssertCommonTextureContract(importer);
        }

        [TestCaseSource(nameof(ModelCases))]
        public void StaticTerrainModel_DoesNotImportUnusedRuntimeFeatures(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Assert.That(importer, Is.Not.Null, path);

            Assert.That(importer.globalScale, Is.EqualTo(1f));
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(importer.addCollider, Is.False);
        }

        [Test]
        public void Policy_DoesNotApplyToSameSuffixOutsideFirstPass()
        {
            const string folder = "Assets/_Game/Tests/TempFirstArtPassImportPolicy";
            const string path = folder + "/Outside_Normal.png";
            AssetDatabase.DeleteAsset(folder);
            Directory.CreateDirectory(folder);
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            try
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = RequireTextureImporter(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
                Assert.That(importer.sRGBTexture, Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static string TexturePath(string terrain, string mapName)
        {
            return $"{TerrainRoot}/{terrain}/T_Terrain_{terrain}_{mapName}.png";
        }

        private static TextureImporter RequireTextureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            return importer;
        }

        private static void AssertCommonTextureContract(TextureImporter importer)
        {
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.anisoLevel, Is.EqualTo(4));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
        }
    }
}
