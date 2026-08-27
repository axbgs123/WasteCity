using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Editor;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class Production2DTechnologyIconPipelineTests
    {
        [Test]
        public void IDEA0016_FormalTechnologyPngsUseTransparentSpriteImportContract()
        {
            foreach (string path in TechnologyPngs())
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
                Assert.That(importer.spritePivot,
                    Is.EqualTo(Vector2.one * .5f), path);
            }
        }

        [Test]
        public void IDEA0016_AllStableResearchIdsMapToDeterministicTechnologyPaths()
        {
            Assert.That(ResearchCatalog.All, Has.Length.EqualTo(44));
            string[] paths = ResearchCatalog.All
                .Select(definition =>
                    Production2DTechnologyIconCatalogBuilder
                        .ExpectedAssetPath(definition.Id.Value))
                .ToArray();
            Assert.That(paths, Has.All.StartsWith(
                Production2DTechnologyImportPolicy.Root + "tech-"));
            Assert.That(paths, Is.Unique);
            Assert.That(paths, Has.All.EndsWith(".png"));
            Assert.That(paths.All(path =>
                string.Equals(
                    Path.GetFileName(path),
                    Path.GetFileName(path).ToLowerInvariant(),
                    StringComparison.Ordinal)), Is.True);
            Assert.That(
                Production2DTechnologyIconCatalogBuilder.ExpectedAssetPath(
                    "core.research.bridge.psionic-mech"),
                Is.EqualTo(
                    Production2DTechnologyImportPolicy.Root +
                    "tech-bridge-psionic-mech.png"));
            Assert.That(
                Production2DTechnologyIconCatalogBuilder.ExpectedAssetPath(
                    "unknown.research"), Is.Empty);

            string[] masterPaths = ResearchCatalog.All
                .Select(definition =>
                    Production2DTechnologyIconCatalogBuilder
                        .ExpectedMasterPath(definition.Id.Value))
                .ToArray();
            Assert.That(masterPaths, Has.All.StartsWith(
                Production2DTechnologyIconCatalogBuilder
                    .TechnologyMastersRoot + "tech-"));
            Assert.That(masterPaths, Is.Unique);
            Assert.That(masterPaths, Has.All.EndsWith("-master-v1.png"));
            Assert.That(
                Production2DTechnologyIconCatalogBuilder.ExpectedMasterPath(
                    "core.research.bridge.psionic-mech"),
                Is.EqualTo(
                    Production2DTechnologyIconCatalogBuilder
                        .TechnologyMastersRoot +
                    "tech-bridge-psionic-mech-master-v1.png"));
            Assert.That(
                Production2DTechnologyIconCatalogBuilder.ExpectedMasterPath(
                    "unknown.research"), Is.Empty);
        }

        [Test]
        public void IDEA0016_ValidationRejectsUnknownAndDuplicateResearchIcons()
        {
            string root = "Assets/TestTechnologyIcons/";
            Production2DTechnologyIconValidationReport unknown =
                Production2DTechnologyIconCatalogBuilder.ValidateAssetPaths(
                    root,
                    new[] { root + "tech-not-in-catalog.png" },
                    inspectPngContent: false);
            Assert.That(unknown.IsValid, Is.False);
            Assert.That(unknown.Errors.Any(error =>
                error.Contains("Unknown technology icon filename")), Is.True);

            string expected = root + "tech-scrap-processing.png";
            Production2DTechnologyIconValidationReport duplicate =
                Production2DTechnologyIconCatalogBuilder.ValidateAssetPaths(
                    root,
                    new[] { expected, expected },
                    inspectPngContent: false);
            Assert.That(duplicate.IsValid, Is.False);
            Assert.That(duplicate.Errors.Any(error =>
                error.Contains("Duplicate technology icon")), Is.True);
        }

        [Test]
        public void IDEA0016_FormalTechnologyBatchIsCompleteAndBuildsStableCatalog()
        {
            Production2DTechnologyIconValidationReport report =
                Production2DTechnologyIconCatalogBuilder.ValidateSourceAssets();
            Assert.That(report.IsValid, Is.True, report.FormatErrors());
            Assert.That(report.Warnings, Is.Empty);
            Assert.That(TechnologyPngs(), Has.Length.EqualTo(44));
            Assert.That(TechnologyMasterPngs(), Has.Length.EqualTo(44));

            Production2DTechnologyIconCatalogBuilder
                .BuildTechnologyIconCatalog();
            ResearchIconCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<ResearchIconCatalog3D>(
                    Production2DTechnologyIconCatalogBuilder.CatalogAssetPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string error),
                Is.True, error);
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                string path = Production2DTechnologyIconCatalogBuilder
                    .ExpectedAssetPath(definition.Id.Value);
                Sprite expected = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(expected, Is.Not.Null, path);
                Assert.That(catalog.ResolveIcon(definition.Id.Value),
                    Is.SameAs(expected), definition.Id.Value);
            }
        }

        [Test]
        public void IDEA0016_AllFormalTechnologyMastersAreTrueAlphaAndSafeAreaCompliant()
        {
            Production2DTechnologyIconValidationReport report =
                Production2DTechnologyIconCatalogBuilder
                    .ValidateMasterSourceAssets();
            Assert.That(report.IsValid, Is.True, report.FormatErrors());
            Assert.That(report.Warnings, Is.Empty);
            Assert.That(TechnologyMasterPngs(), Has.Length.EqualTo(44));
        }

        [Test]
        public void IDEA0020_LegacyAnalysisIconIsIndependentAndByteStable()
        {
            const string researchId = "core.research.legacy-analysis";
            Production2DTechnologyIconCatalogBuilder
                .BuildTechnologyIconCatalogForBatch();
            string delivery = Production2DTechnologyIconCatalogBuilder
                .ExpectedAssetPath(researchId);
            string master = Production2DTechnologyIconCatalogBuilder
                .ExpectedMasterPath(researchId);
            Assert.That(delivery, Does.EndWith("tech-legacy-analysis.png"));
            Assert.That(master,
                Does.EndWith("tech-legacy-analysis-master-v1.png"));
            Assert.That(File.Exists(delivery), Is.True);
            Assert.That(File.Exists(delivery + ".meta"), Is.True);
            Assert.That(File.Exists(master), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(delivery),
                Is.Not.Null);
            byte[] deliveryBytes = File.ReadAllBytes(delivery);
            byte[] metaBytes = File.ReadAllBytes(delivery + ".meta");
            byte[] masterBytes = File.ReadAllBytes(master);

            Production2DTechnologyIconCatalogBuilder
                .BuildTechnologyIconCatalogForBatch();

            Assert.That(File.ReadAllBytes(delivery), Is.EqualTo(deliveryBytes));
            Assert.That(File.ReadAllBytes(delivery + ".meta"),
                Is.EqualTo(metaBytes));
            Assert.That(File.ReadAllBytes(master), Is.EqualTo(masterBytes));
        }

        [Test]
        public void IDEA0016_ResearchIconCatalogHasDeterministicFallbacks()
        {
            var catalog = ScriptableObject.CreateInstance<ResearchIconCatalog3D>();
            try
            {
                foreach (ResearchDefinition definition in ResearchCatalog.All)
                {
                    Sprite first = catalog.ResolveIcon(definition.Id.Value);
                    Sprite second = catalog.ResolveIcon(definition.Id.Value);
                    Assert.That(first, Is.Not.Null, definition.Id.Value);
                    Assert.That(second, Is.SameAs(first), definition.Id.Value);
                }
                Assert.That(catalog.ResolveIcon("unknown.research"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void IDEA0016_FormalTechnologyIconsAreVisibleOnRuntimeTreeNodes()
        {
            Production2DTechnologyIconCatalogBuilder
                .BuildTechnologyIconCatalog();
            var root = new GameObject(
                "ResearchTreeRuntimeVisibilityTest",
                typeof(RectTransform),
                typeof(Canvas));
            try
            {
                var panel = root.GetComponent<RectTransform>();
                panel.sizeDelta = new Vector2(1500f, 850f);
                var view = root.AddComponent<GrayboxResearchTreeView3D>();
                view.Initialize(
                    panel,
                    _ => null,
                    _ => { },
                    () => { },
                    () => { },
                    () => { });

                Image[] images = root.GetComponentsInChildren<Image>(true);
                foreach (ResearchDefinition definition in ResearchCatalog.All)
                {
                    string expectedName = "Research.Node." +
                        definition.Id.Value + ".Icon";
                    Image image = images.SingleOrDefault(candidate =>
                        string.Equals(
                            candidate.gameObject.name,
                            expectedName,
                            StringComparison.Ordinal));
                    Assert.That(image, Is.Not.Null, expectedName);
                    Assert.That(image.gameObject.activeSelf, Is.True,
                        expectedName);
                    Assert.That(
                        AssetDatabase.GetAssetPath(image.sprite),
                        Is.EqualTo(
                            Production2DTechnologyIconCatalogBuilder
                                .ExpectedAssetPath(definition.Id.Value)),
                        expectedName);
                    Assert.That(image.rectTransform.sizeDelta,
                        Is.EqualTo(new Vector2(48f, 48f)), expectedName);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static string[] TechnologyPngs()
        {
            if (!Directory.Exists(Production2DTechnologyImportPolicy.Root))
                return Array.Empty<string>();
            return Directory.GetFiles(
                    Production2DTechnologyImportPolicy.Root,
                    "*.png",
                    SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] TechnologyMasterPngs()
        {
            string root = Production2DTechnologyIconCatalogBuilder
                .TechnologyMastersRoot;
            if (!Directory.Exists(root)) return Array.Empty<string>();
            return Directory.GetFiles(
                    root,
                    "tech-*-master-v1.png",
                    SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
