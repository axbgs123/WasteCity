using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Editor;
using WasteCity.Graybox3D;

namespace WasteCity.Tests
{
    public sealed class Production2DItemIconPipelineTests
    {
        private const string TempRoot =
            "Assets/_Game/Tests/TempProduction2DItemIcons";

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempRoot);
        }

        [Test]
        public void IDEA0016_ProductionItemPngsUseOneTransparentSpriteImportContract()
        {
            foreach (string path in ProductionItemPngs())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.textureType,
                    Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spriteImportMode,
                    Is.EqualTo(SpriteImportMode.Single), path);
                Assert.That(importer.alphaSource,
                    Is.EqualTo(TextureImporterAlphaSource.FromInput), path);
                Assert.That(importer.alphaIsTransparency, Is.True, path);
                Assert.That(importer.sRGBTexture, Is.True, path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.wrapMode,
                    Is.EqualTo(TextureWrapMode.Clamp), path);
                Assert.That(importer.filterMode,
                    Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.maxTextureSize, Is.EqualTo(256), path);
                Assert.That(importer.textureCompression,
                    Is.EqualTo(TextureImporterCompression.CompressedHQ), path);
                Assert.That(importer.crunchedCompression, Is.False, path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteMeshType,
                    Is.EqualTo(SpriteMeshType.FullRect), path);
                Assert.That(importer.spritePivot, Is.EqualTo(Vector2.one * .5f), path);
            }
        }

        [Test]
        public void IDEA0016_ItemMastersAreTrueAlphaAndRespectFormalSafeArea()
        {
            Production2DItemIconValidationReport report =
                Production2DItemIconCatalogBuilder.ValidateMasterAssets();

            Assert.That(report.IsValid, Is.True, report.FormatErrors());
            Assert.That(report.Warnings, Is.Empty);
            Assert.That(
                Directory.GetFiles(
                    Production2DItemIconCatalogBuilder.ItemMastersRoot,
                    "*-chroma-*.png",
                    SearchOption.TopDirectoryOnly),
                Is.Empty,
                "Color-key intermediates must not be stored as formal masters.");
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                string path = Production2DItemIconCatalogBuilder
                    .ExpectedMasterPath(definition.Id);
                Assert.That(File.Exists(path), Is.True, definition.Id);
                Assert.That(path, Does.EndWith("-master-v1.png"));
            }
        }

        [Test]
        public void IDEA0016_StableResourceIdsMapToDeterministicLowercaseFilenames()
        {
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                string path = Production2DItemIconCatalogBuilder
                    .ExpectedAssetPath(definition.Id);
                Assert.That(path,
                    Does.StartWith(
                        Production2DItemIconCatalogBuilder.ItemsRoot + "item-"),
                    definition.Id);
                Assert.That(path, Does.EndWith(".png"), definition.Id);
                string filename = Path.GetFileName(path);
                Assert.That(filename,
                    Is.EqualTo(filename.ToLowerInvariant()), definition.Id);
            }
            Assert.That(
                Production2DItemIconCatalogBuilder.ExpectedAssetPath(
                    ResourceIds.BiologicalWeapon),
                Does.EndWith("item-biological-weapon.png"));
            Assert.That(
                Production2DItemIconCatalogBuilder.ExpectedAssetPath(
                    ResourceIds.PsionicAmplifier),
                Does.EndWith("item-psionic-amplifier.png"));
            Assert.That(
                Production2DItemIconCatalogBuilder.ExpectedAssetPath(
                    "unknown.resource"),
                Is.Empty);
        }

        [Test]
        public void IDEA0016_ValidationAcceptsPresentIconsAndReportsMissingAsFallbacks()
        {
            Production2DItemIconValidationReport report =
                Production2DItemIconCatalogBuilder.ValidateSourceAssets();

            Assert.That(report.IsValid, Is.True, report.FormatErrors());
            Assert.That(
                report.Warnings.Count,
                Is.EqualTo(ResourceDefinitionCatalog.All.Count -
                    ProductionItemPngs().Length));
            Assert.That(report.Warnings,
                Has.All.Contains("deterministic runtime fallback"));
        }

        [Test]
        public void IDEA0016_ValidationRejectsUnknownAndDuplicateMappings()
        {
            string root = TempRoot + "/";
            string iron = root + "item-iron.png";
            string unknown = root + "item-imaginary.png";
            Production2DItemIconValidationReport report =
                Production2DItemIconCatalogBuilder.ValidateAssetPaths(
                    root,
                    new[] { iron, iron, unknown },
                    false);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors,
                Has.Some.Contains("Duplicate item icon for resource"));
            Assert.That(report.Errors,
                Has.Some.Contains("Unknown item icon filename"));
        }

        [Test]
        public void IDEA0016_ValidationRejectsWrongDimensionsAndNoAlphaChannel()
        {
            Directory.CreateDirectory(TempRoot);
            string wrongSize = TempRoot + "/item-iron.png";
            string noAlpha = TempRoot + "/item-stone.png";
            WritePng(wrongSize, 32, 32, TextureFormat.RGBA32, true);
            WritePng(noAlpha, 256, 256, TextureFormat.RGB24, false);

            Production2DItemIconValidationReport report =
                Production2DItemIconCatalogBuilder.ValidateAssetPaths(
                    TempRoot + "/",
                    new[] { wrongSize, noAlpha },
                    true);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("exactly 256x256"));
            Assert.That(report.Errors, Has.Some.Contains("alpha channel"));
        }

        [Test]
        public void IDEA0016_BuilderWiresEveryPresentSpriteAndKeepsMissingFallback()
        {
            Production2DItemIconCatalogBuilder.BuildResourceIconCatalog();
            ResourceIconCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<ResourceIconCatalog3D>(
                    Production2DItemIconCatalogBuilder.CatalogAssetPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string error), Is.True, error);
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                string path = Production2DItemIconCatalogBuilder
                    .ExpectedAssetPath(definition.Id);
                Sprite resolved = catalog.ResolveIcon(definition.Id);
                if (File.Exists(path))
                {
                    Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.That(expected, Is.Not.Null, definition.Id);
                    AssertAssetIdentity(resolved, expected, definition.Id);
                }
                else
                {
                    Assert.That(resolved,
                        Is.SameAs(ResourceIconCatalog3D.Resolve(definition.Id)),
                        definition.Id);
                }
            }
        }

        [Test]
        public void IDEA0016_TwoBuildsPreserveCatalogGuidAndMetaBytes()
        {
            string metaPath =
                Production2DItemIconCatalogBuilder.CatalogAssetPath + ".meta";
            string guidBefore = AssetDatabase.AssetPathToGUID(
                Production2DItemIconCatalogBuilder.CatalogAssetPath);
            byte[] metaBefore = File.ReadAllBytes(metaPath);

            Production2DItemIconCatalogBuilder.BuildResourceIconCatalog();
            Production2DItemIconCatalogBuilder.BuildResourceIconCatalog();

            Assert.That(
                AssetDatabase.AssetPathToGUID(
                    Production2DItemIconCatalogBuilder.CatalogAssetPath),
                Is.EqualTo(guidBefore));
            Assert.That(File.ReadAllBytes(metaPath), Is.EqualTo(metaBefore));
        }

        [Test]
        public void IDEA0016_TwoCanonicalReimportsPreserveEverySpriteGuidAndMeta()
        {
            Production2DItemImportPolicy.ReimportItemIcons();
            string[] paths = ProductionItemPngs();
            var guids = paths.ToDictionary(
                path => path,
                AssetDatabase.AssetPathToGUID,
                StringComparer.Ordinal);
            var metaBytes = paths.ToDictionary(
                path => path,
                path => File.ReadAllBytes(path + ".meta"),
                StringComparer.Ordinal);

            Production2DItemImportPolicy.ReimportItemIcons();
            Production2DItemImportPolicy.ReimportItemIcons();

            foreach (string path in paths)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(path),
                    Is.EqualTo(guids[path]), path);
                Assert.That(File.ReadAllBytes(path + ".meta"),
                    Is.EqualTo(metaBytes[path]), path);
            }
        }

        [Test]
        public void IDEA0016_PipelineExposesMenuAndBatchEntrypoints()
        {
            MethodInfo menu = typeof(Production2DItemIconCatalogBuilder)
                .GetMethod(
                    nameof(Production2DItemIconCatalogBuilder
                        .BuildResourceIconCatalog),
                    BindingFlags.Public | BindingFlags.Static);
            MethodInfo batch = typeof(Production2DItemIconCatalogBuilder)
                .GetMethod(
                    nameof(Production2DItemIconCatalogBuilder
                        .BuildResourceIconCatalogForBatch),
                    BindingFlags.Public | BindingFlags.Static);
            MethodInfo reimport = typeof(Production2DItemImportPolicy)
                .GetMethod(
                    nameof(Production2DItemImportPolicy.ReimportItemIcons),
                    BindingFlags.Public | BindingFlags.Static);

            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.GetCustomAttributes(typeof(MenuItem), false),
                Is.Not.Empty);
            Assert.That(batch, Is.Not.Null);
            Assert.That(reimport, Is.Not.Null);
            Assert.That(reimport.GetCustomAttributes(typeof(MenuItem), false),
                Is.Not.Empty);
        }

        [Test]
        public void IDEA0016_ImportPolicyCannotClaimSimilarPngOutsideApprovedRoot()
        {
            Assert.That(
                Production2DItemImportPolicy.IsItemPng(
                    "Assets/_Game/Art/Production2D/Items/item-iron.png"),
                Is.True);
            Assert.That(
                Production2DItemImportPolicy.IsItemPng(
                    "Assets/_Game/Art/FirstPass/item-iron.png"),
                Is.False);
            Assert.That(
                Production2DItemImportPolicy.IsItemPng(
                    "Assets/_Game/Art/Production2D/Items/item-iron.jpg"),
                Is.False);
        }

        private static string[] ProductionItemPngs()
        {
            return Directory
                .GetFiles(
                    Production2DItemIconCatalogBuilder.ItemsRoot,
                    "*.png",
                    SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void WritePng(
            string path,
            int width,
            int height,
            TextureFormat format,
            bool transparent)
        {
            var texture = new Texture2D(width, height, format, false);
            Color color = transparent ? new Color(1f, 0f, 0f, 0f) : Color.red;
            var pixels = Enumerable.Repeat(color, width * height).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static void AssertAssetIdentity(
            UnityEngine.Object actual,
            UnityEngine.Object expected,
            string context)
        {
            Assert.That(actual, Is.Not.Null, context);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    actual,
                    out string actualGuid,
                    out long actualFileId),
                Is.True,
                context);
            Assert.That(
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    expected,
                    out string expectedGuid,
                    out long expectedFileId),
                Is.True,
                context);
            Assert.That(actualGuid, Is.EqualTo(expectedGuid), context);
            Assert.That(actualFileId, Is.EqualTo(expectedFileId), context);
        }
    }
}
