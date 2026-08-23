using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Editor;
using WasteCity.Graybox3D;

namespace WasteCity.Tests
{
    public sealed class Production2DBuildingIconPipelineTests
    {
        private const string Root =
            "Assets/_Game/Art/Production2D/Buildings/";
        private const string ManifestPath =
            "Docs/Art/IDEA-0016/Manifests/" +
            "idea-0016-building-visual-assets.json";

        [Test]
        public void IDEA0016_BuildingManifestCoversAllThirtyStableIds()
        {
            Assert.That(File.Exists(ManifestPath), Is.True);
            string json = File.ReadAllText(ManifestPath);
            foreach (BuildingDefinition definition in BuildingCatalog.All)
            {
                Assert.That(json, Does.Contain(
                    "\"contentId\": \"" + definition.Id.Value + "\""),
                    definition.Id.Value);
                Assert.That(json, Does.Contain(
                    "\"displayNameZh\": \"" + definition.Name + "\""),
                    definition.Id.Value);
            }
            Assert.That(Count(json, "\"contentId\":"),
                Is.EqualTo(BuildingCatalog.All.Length));
            Assert.That(json, Does.Contain(
                "\"contentId\": \"core.building.heavy-machine-gun-turret\""));
            Assert.That(json, Does.Contain(
                "\"contentId\": \"cultivation.building.sword-riding-platform\""));
            Assert.That(Count(json, "\"catalogEntry\": \"upgrade-only\""),
                Is.EqualTo(2));
        }

        [Test]
        public void IDEA0016_BuildingPipelineTypesExistBeforeFormalImport()
        {
            Assert.That(typeof(Production2DBuildingImportPolicy), Is.Not.Null);
            Assert.That(typeof(Production2DBuildingIconCatalogBuilder),
                Is.Not.Null);
            Assert.That(typeof(BuildingIconCatalog3D), Is.Not.Null);
        }

        [Test]
        public void IDEA0016_AllThirtyBuildingSpritesAreTransparent256Singles()
        {
            Assert.That(Directory.Exists(Root), Is.True);
            string[] paths = Directory.GetFiles(
                    Root,
                    "building-*.png",
                    SearchOption.TopDirectoryOnly)
                .Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Assert.That(paths, Has.Length.EqualTo(BuildingCatalog.All.Length));
            foreach (string path in paths)
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
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.wrapMode,
                    Is.EqualTo(TextureWrapMode.Clamp), path);
                Assert.That(importer.maxTextureSize, Is.EqualTo(256), path);
            }
        }

        [Test]
        public void IDEA0016_AllThirtyMastersPassAlphaAndSafeAreaValidation()
        {
            Production2DBuildingValidationReport report =
                Production2DBuildingIconCatalogBuilder.ValidateSourceAssets();

            Assert.That(report.Errors, Is.Empty,
                string.Join("\n", report.Errors));
            Assert.That(report.Warnings, Is.Empty,
                string.Join("\n", report.Warnings));
            foreach (BuildingDefinition definition in BuildingCatalog.All)
            {
                string path = Production2DBuildingIconCatalogBuilder
                    .ExpectedMasterPath(definition.Id.Value);
                Assert.That(path, Does.EndWith("-master-v1.png"),
                    definition.Id.Value);
                Assert.That(File.Exists(path), Is.True, path);
            }
            Assert.That(Production2DBuildingIconCatalogBuilder
                .ExpectedMasterPath("unknown.building"), Is.Empty);
        }

        [Test]
        public void IDEA0016_BuilderMapsEveryBuildingAndPreservesCatalogGuid()
        {
            Production2DBuildingIconCatalogBuilder.BuildBuildingIconCatalog();
            string path = Production2DBuildingIconCatalogBuilder
                .CatalogAssetPath;
            string firstGuid = AssetDatabase.AssetPathToGUID(path);
            Assert.That(firstGuid, Is.Not.Empty);
            byte[] firstAssetBytes = File.ReadAllBytes(path);
            byte[] firstMetaBytes = File.ReadAllBytes(path + ".meta");

            var catalog = AssetDatabase.LoadAssetAtPath<BuildingIconCatalog3D>(
                path);
            Assert.That(catalog, Is.Not.Null);
            foreach (BuildingDefinition definition in BuildingCatalog.All)
            {
                Sprite sprite = catalog.ResolveIcon(definition.Id.Value);
                Assert.That(sprite, Is.Not.Null, definition.Id.Value);
                Assert.That(AssetDatabase.GetAssetPath(sprite),
                    Is.EqualTo(Production2DBuildingIconCatalogBuilder
                        .ExpectedAssetPath(definition.Id.Value)),
                    definition.Id.Value);
            }

            EditorUtility.ClearDirty(catalog);
            Production2DBuildingIconCatalogBuilder.BuildBuildingIconCatalog();
            Assert.That(AssetDatabase.AssetPathToGUID(path),
                Is.EqualTo(firstGuid));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(firstAssetBytes));
            Assert.That(File.ReadAllBytes(path + ".meta"),
                Is.EqualTo(firstMetaBytes));
            Assert.That(EditorUtility.IsDirty(catalog), Is.False);
            Assert.That(catalog.ResolveIcon("unknown.building"), Is.Null);
        }

        [Test]
        public void IDEA0016_RuntimeFallbackIsDeterministicForKnownBuildings()
        {
            string id = BuildingCatalog.MiningStation.Id.Value;
            Sprite first = BuildingIconCatalog3D.ResolveFallback(id);
            Sprite second = BuildingIconCatalog3D.ResolveFallback(id);

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.SameAs(first));
            Assert.That(BuildingIconCatalog3D.ResolveFallback(
                "unknown.building"), Is.Null);
        }

        private static int Count(string text, string needle)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(
                       needle,
                       offset,
                       StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += needle.Length;
            }
            return count;
        }
    }
}
