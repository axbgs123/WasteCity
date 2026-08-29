using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Editor;
using WasteCity.Graybox3D;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class Production2DVisualCatalogAtlasTests
    {
        private static readonly string[] UiIds =
        {
            "core.ui.frame.primary-panel",
            "core.ui.frame.secondary-card",
            "core.ui.control.primary-button",
            "core.ui.frame.technology-node",
            "core.ui.divider.terminal-horizontal",
            "core.ui.connector.technology-branch",
            "core.ui.icon.search",
            "core.ui.tab.army",
            "core.ui.tab.world",
            "core.ui.tab.politics",
            "core.ui.status.guard",
            "core.ui.status.follow",
            "core.ui.status.expedition",
            "core.ui.status.retreat",
            "core.ui.status.transport",
            "core.ui.status.communication",
            "core.ui.status.loyalty",
            "core.ui.status.rescue",
            "core.ui.background.research-tree",
        };

        [Test]
        public void IDEA0016_IDEA0023_UnifiedCatalogAndSevenAtlasAssetsExistAfterBuild()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<
                    Production2DVisualCatalog3D>(
                    Production2DVisualCatalogBuilder.CatalogAssetPath),
                Is.Not.Null,
                Production2DVisualCatalogBuilder.CatalogAssetPath);
            Assert.That(Production2DSpriteAtlasBuilder.Definitions.Count,
                Is.EqualTo(7));
            foreach (Production2DAtlasDefinition definition in
                     Production2DSpriteAtlasBuilder.Definitions)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(
                        definition.AssetPath),
                    Is.Not.Null,
                    definition.AssetPath);
            }
        }

        [Test]
        public void IDEA0016_UnifiedCatalogUsesEveryFormalDefaultVisualKeyOnce()
        {
            Production2DVisualCatalog3D catalog = LoadCatalog();
            Assert.That(catalog.Entries.Count,
                Is.EqualTo(
                    Production2DVisualCatalogBuilder.ExpectedVisualCount));
            AssertClass(catalog, Production2DVisualClass.Item,
                ResourceDefinitionCatalog.All.Select(value => value.Id));
            AssertClass(catalog, Production2DVisualClass.Technology,
                ResearchCatalog.All.Select(value => value.Id.Value));
            AssertClass(catalog, Production2DVisualClass.Building,
                BuildingCatalog.All.Select(value => value.Id.Value));
            AssertClass(catalog, Production2DVisualClass.Ui, UiIds);
            AssertClass(catalog, Production2DVisualClass.Character,
                new[]
                {
                    "core.character.cen-jin",
                    "core.character.lin-xi",
                    "core.character.han-gu",
                });
            AssertClass(catalog, Production2DVisualClass.WorldMarker,
                new[]
                {
                    "core.world-marker.resource-node",
                    "core.world-marker.selection-reticle",
                    "core.world-marker.secondary-city",
                    "core.world-marker.outpost",
                    "core.world-marker.convoy",
                });
            AssertClass(catalog, Production2DVisualClass.Unit,
                new[]
                {
                    ArmyUnitCatalog.CombatPuppetId,
                    ArmyUnitCatalog.BredBehemothId,
                    ArmyUnitCatalog.PsionicMechId,
                    ArmyUnitCatalog.BioMechanicalBehemothId,
                });

            string[] keys = catalog.Entries.Select(value =>
                    value.VisualClass + "|" + value.ContentId + "|" +
                    value.Variant)
                .ToArray();
            Assert.That(keys.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(keys.Length));
            Assert.That(catalog.Entries.All(value => string.Equals(
                    value.Variant,
                    Production2DVisualCatalog3D.DefaultVariant,
                    StringComparison.Ordinal)), Is.True);
            Assert.That(catalog.TryValidate(out string error),
                Is.True, error);
        }

        [Test]
        public void IDEA0016_EveryRecipeKeepsFormalOutputAndBadgeProjection()
        {
            Production2DVisualCatalog3D catalog = LoadCatalog();
            Assert.That(catalog.RecipeEntries.Count,
                Is.EqualTo(
                    Production2DVisualCatalogBuilder.ExpectedRecipeCount));
            CollectionAssert.AreEquivalent(
                ResourceRecipeCatalog.All.Select(value => value.Id),
                catalog.RecipeEntries.Select(value => value.RecipeId));
            foreach (ResourceRecipeDefinition definition in
                     ResourceRecipeCatalog.All)
            {
                Production2DRecipeVisualEntry3D entry = catalog.RecipeEntries
                    .Single(value => string.Equals(
                        value.RecipeId,
                        definition.Id,
                        StringComparison.Ordinal));
                Production2DRecipeBadgeRule expectedBadge =
                    definition.Kind == ResourceRecipeKind.ManualCrafting
                        ? Production2DRecipeBadgeRule.Manual
                        : Production2DRecipeBadgeRule.Machine;
                Assert.That(entry.BadgeRule, Is.EqualTo(expectedBadge),
                    definition.Id);
                Assert.That(entry.UsesBoundResourceVisual,
                    Is.EqualTo(definition.UsesBoundResourceNode),
                    definition.Id);
                if (definition.UsesBoundResourceNode)
                {
                    Assert.That(entry.PrimaryOutputContentId, Is.Empty,
                        definition.Id);
                    Assert.That(entry.PrimaryOutputSprite, Is.Null,
                        definition.Id);
                }
                else
                {
                    string outputId = definition.Outputs[0].ResourceId;
                    Assert.That(entry.PrimaryOutputContentId,
                        Is.EqualTo(outputId), definition.Id);
                    Assert.That(AssetDatabase.GetAssetPath(
                            entry.PrimaryOutputSprite),
                        Is.EqualTo(Production2DItemIconCatalogBuilder
                            .ExpectedAssetPath(outputId)),
                        definition.Id);
                }
            }
        }

        [Test]
        public void IDEA0016_AtlasesOwnSortedFullRectPackablesAndSafeSettings()
        {
            foreach (Production2DAtlasDefinition definition in
                     Production2DSpriteAtlasBuilder.Definitions)
            {
                SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(
                    definition.AssetPath);
                Assert.That(atlas, Is.Not.Null, definition.AssetPath);
                Assert.That(SpriteAtlasExtensions.IsIncludeInBuild(atlas),
                    Is.True,
                    definition.AssetPath);
                SpriteAtlasPackingSettings packing =
                    SpriteAtlasExtensions.GetPackingSettings(atlas);
                Assert.That(packing.padding,
                    Is.EqualTo(Production2DSpriteAtlasBuilder.Padding));
                Assert.That(packing.enableRotation, Is.False);
                Assert.That(packing.enableTightPacking, Is.False);
                SpriteAtlasTextureSettings texture =
                    SpriteAtlasExtensions.GetTextureSettings(atlas);
                Assert.That(texture.generateMipMaps, Is.False);
                Assert.That(texture.readable, Is.False);

                UnityEngine.Object[] expected =
                    Production2DSpriteAtlasBuilder.ExpectedPackables(definition);
                UnityEngine.Object[] actual =
                    SpriteAtlasExtensions.GetPackables(atlas);
                Assert.That(actual, Has.Length.EqualTo(
                    definition.ExpectedPackableCount));
                CollectionAssert.AreEqual(expected, actual,
                    definition.AssetPath);
                foreach (UnityEngine.Object packable in actual)
                {
                    string path = AssetDatabase.GetAssetPath(packable);
                    Assert.That(path, Does.StartWith(definition.SourceRoot));
                    var importer = AssetImporter.GetAtPath(path) as
                        TextureImporter;
                    Assert.That(importer, Is.Not.Null, path);
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    Assert.That(settings.spriteMeshType,
                        Is.EqualTo(SpriteMeshType.FullRect), path);
                    Assert.That(importer.mipmapEnabled, Is.False, path);
                }
            }
        }

        [Test]
        public void IDEA0016_MissingCatalogAndUnknownIdsKeepCategoryFallbacks()
        {
            foreach (Production2DVisualClass visualClass in
                     Enum.GetValues(typeof(Production2DVisualClass)))
            {
                Sprite fallback = Production2DVisualCatalog3D
                    .ResolveFromCatalogOrFallback(
                        null,
                        visualClass,
                        "unknown.visual.for." + visualClass);
                Assert.That(fallback, Is.Not.Null, visualClass.ToString());
            }
            Assert.That(Production2DVisualCatalog3D
                .ResolveFromCatalogOrFallback(
                    null,
                    Production2DVisualClass.Item,
                    ResourceIds.Iron), Is.Not.Null);
            Assert.That(Production2DVisualCatalog3D
                .ResolveFromCatalogOrFallback(
                    null,
                    Production2DVisualClass.Building,
                    BuildingCatalog.MiningStation.Id.Value), Is.Not.Null);
            Assert.That(Production2DVisualCatalog3D
                .ResolveFromCatalogOrFallback(
                    null,
                    Production2DVisualClass.Ui,
                    string.Empty), Is.Null);
        }

        [Test]
        public void IDEA0016_RebuildIsByteStableAndDoesNotDirtyAssets()
        {
            Production2DVisualCatalogBuilder.BuildVisualCatalogForBatch();
            string[] paths = new[]
                {
                    Production2DVisualCatalogBuilder.CatalogAssetPath,
                }
                .Concat(Production2DSpriteAtlasBuilder.Definitions.Select(
                    value => value.AssetPath))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var guids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                bytes.Add(path, File.ReadAllBytes(path));
                bytes.Add(path + ".meta", File.ReadAllBytes(path + ".meta"));
                guids.Add(path, AssetDatabase.AssetPathToGUID(path));
            }

            Production2DVisualCatalog3D catalog = LoadCatalog();
            EditorUtility.ClearDirty(catalog);
            Production2DVisualCatalogBuilder.BuildVisualCatalogForBatch();
            foreach (string path in paths)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(path),
                    Is.EqualTo(guids[path]), path);
                Assert.That(File.ReadAllBytes(path),
                    Is.EqualTo(bytes[path]), path);
                Assert.That(File.ReadAllBytes(path + ".meta"),
                    Is.EqualTo(bytes[path + ".meta"]), path + ".meta");
            }
            Assert.That(EditorUtility.IsDirty(catalog), Is.False);
        }

        private static Production2DVisualCatalog3D LoadCatalog()
        {
            Production2DVisualCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<Production2DVisualCatalog3D>(
                    Production2DVisualCatalogBuilder.CatalogAssetPath);
            Assert.That(catalog, Is.Not.Null,
                Production2DVisualCatalogBuilder.CatalogAssetPath);
            return catalog;
        }

        private static void AssertClass(
            Production2DVisualCatalog3D catalog,
            Production2DVisualClass visualClass,
            IEnumerable<string> expectedIds)
        {
            string[] expected = expectedIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] actual = catalog.Entries
                .Where(value => value.VisualClass == visualClass)
                .Select(value => value.ContentId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(expected, actual, visualClass.ToString());
        }
    }
}
