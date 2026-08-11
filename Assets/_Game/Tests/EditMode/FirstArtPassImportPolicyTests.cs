using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.Editor;

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

        public static IEnumerable<TestCaseData> TemporaryReadableCases
        {
            get
            {
                foreach (string terrain in TerrainTypes)
                {
                    yield return new TestCaseData(terrain, "BaseColor");
                    yield return new TestCaseData(terrain, "Normal");
                    yield return new TestCaseData(terrain, "Height");
                }
            }
        }

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
            UnityEngine.Object.DestroyImmediate(texture);

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

        [TestCaseSource(nameof(TemporaryReadableCases))]
        public void TemporaryReadability_AcceptsOnlyApprovedSourceAndRestoresExactState(
            string terrain,
            string channel)
        {
            string path = TexturePath(terrain, channel);
            ImportState before = CaptureImportState(path);

            using (FirstArtPassImportPolicy.AllowTemporaryReadability(path))
            {
                Reimport(path);
                TextureImporter importer = RequireTextureImporter(path);
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(importer.isReadable, Is.True, path);
                Assert.That(source, Is.Not.Null, path);
                Assert.That(source.isReadable, Is.True, path);
                if (string.Equals(channel, "Height", StringComparison.Ordinal))
                    Assert.That(source.format, Is.EqualTo(TextureFormat.R16), path);
                else
                    Assert.That(source.format, Is.EqualTo(TextureFormat.BC7), path);
            }

            AssertImportStateEquals(CaptureImportState(path), before, path);
        }

        [TestCase("Wasteland", "Mask")]
        [TestCase("Outside", "BaseColor")]
        public void TemporaryReadability_RejectsEveryPathOutsideExactApprovedSet(
            string terrain,
            string channel)
        {
            string path = string.Equals(terrain, "Outside", StringComparison.Ordinal)
                ? "Assets/_Game/Tests/EditMode/FirstArtPassImportPolicyTests.cs"
                : TexturePath(terrain, channel);

            Assert.That(
                () => FirstArtPassImportPolicy.AllowTemporaryReadability(path),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void TemporaryReadability_RejectsDuplicateActiveScopeAndRestoresState()
        {
            string path = TexturePath("Wasteland", "BaseColor");
            ImportState before = CaptureImportState(path);

            using (FirstArtPassImportPolicy.AllowTemporaryReadability(path))
            {
                Assert.That(
                    () => FirstArtPassImportPolicy.AllowTemporaryReadability(path),
                    Throws.TypeOf<InvalidOperationException>());
            }

            AssertImportStateEquals(CaptureImportState(path), before, path);
        }

        [Test]
        public void TemporaryReadability_OperationFailureStillRestoresExactState()
        {
            string path = TexturePath("Rocky", "Normal");
            ImportState before = CaptureImportState(path);
            var injected = new InvalidOperationException("injected source operation failure");

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            {
                using (FirstArtPassImportPolicy.AllowTemporaryReadability(path))
                {
                    Reimport(path);
                    Assert.That(RequireTextureImporter(path).isReadable, Is.True, path);
                    throw injected;
                }
            });

            Assert.That(thrown, Is.SameAs(injected));
            AssertImportStateEquals(CaptureImportState(path), before, path);
        }

        [Test]
        public void TemporaryReadability_CleanupFailureStillRestoresExactState()
        {
            string path = TexturePath("Wetland", "Height");
            ImportState before = CaptureImportState(path);
            var injected = new InvalidOperationException("injected source cleanup failure");
            FirstArtPassImportPolicy.TemporaryPlatformRestoreCheckpoint = observedPath =>
            {
                if (string.Equals(observedPath, path, StringComparison.Ordinal))
                    throw injected;
            };

            try
            {
                InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                {
                    using (FirstArtPassImportPolicy.AllowTemporaryReadability(path))
                        Reimport(path);
                });
                Assert.That(thrown, Is.SameAs(injected));
            }
            finally
            {
                FirstArtPassImportPolicy.TemporaryPlatformRestoreCheckpoint = null;
            }

            AssertImportStateEquals(CaptureImportState(path), before, path);
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

        private static void Reimport(string path)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static ImportState CaptureImportState(string path)
        {
            TextureImporter importer = RequireTextureImporter(path);
            string platformName = BuildPipeline
                .GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)
                .ToString();
            return new ImportState(
                File.ReadAllBytes(path),
                File.ReadAllBytes(path + ".meta"),
                AssetDatabase.AssetPathToGUID(path),
                AssetDatabase.GetAssetDependencyHash(path),
                importer.isReadable,
                importer.GetPlatformTextureSettings(platformName));
        }

        private static void AssertImportStateEquals(
            ImportState actual,
            ImportState expected,
            string context)
        {
            Assert.That(actual.AssetBytes, Is.EqualTo(expected.AssetBytes), context + " bytes");
            Assert.That(actual.MetaBytes, Is.EqualTo(expected.MetaBytes), context + " meta");
            Assert.That(actual.Guid, Is.EqualTo(expected.Guid), context + " GUID");
            Assert.That(
                actual.DependencyHash,
                Is.EqualTo(expected.DependencyHash),
                context + " dependency hash");
            Assert.That(actual.IsReadable, Is.EqualTo(expected.IsReadable), context + " readability");
            AssertPlatformSettingsEqual(actual.Platform, expected.Platform, context);
        }

        private static void AssertPlatformSettingsEqual(
            TextureImporterPlatformSettings actual,
            TextureImporterPlatformSettings expected,
            string context)
        {
            Assert.That(actual.name, Is.EqualTo(expected.name), context + " platform name");
            Assert.That(actual.overridden, Is.EqualTo(expected.overridden), context + " overridden");
            Assert.That(actual.maxTextureSize, Is.EqualTo(expected.maxTextureSize), context + " max size");
            Assert.That(actual.format, Is.EqualTo(expected.format), context + " format");
            Assert.That(
                actual.textureCompression,
                Is.EqualTo(expected.textureCompression),
                context + " compression");
            Assert.That(actual.compressionQuality, Is.EqualTo(expected.compressionQuality), context + " quality");
            Assert.That(actual.crunchedCompression, Is.EqualTo(expected.crunchedCompression), context + " crunch");
        }

        private static void AssertCommonTextureContract(TextureImporter importer)
        {
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.anisoLevel, Is.EqualTo(4));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
        }

        private sealed class ImportState
        {
            public ImportState(
                byte[] assetBytes,
                byte[] metaBytes,
                string guid,
                Hash128 dependencyHash,
                bool isReadable,
                TextureImporterPlatformSettings platform)
            {
                AssetBytes = assetBytes;
                MetaBytes = metaBytes;
                Guid = guid;
                DependencyHash = dependencyHash;
                IsReadable = isReadable;
                Platform = platform;
            }

            public byte[] AssetBytes { get; }
            public byte[] MetaBytes { get; }
            public string Guid { get; }
            public Hash128 DependencyHash { get; }
            public bool IsReadable { get; }
            public TextureImporterPlatformSettings Platform { get; }
        }
    }
}
