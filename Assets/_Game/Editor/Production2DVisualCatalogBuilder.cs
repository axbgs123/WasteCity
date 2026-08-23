using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Research;

namespace WasteCity.Editor
{
    [Serializable]
    internal sealed class UnifiedPresentationManifest
    {
        public UnifiedPresentationManifestEntry[] entries;
    }

    [Serializable]
    internal sealed class UnifiedPresentationManifestEntry
    {
        public string contentId;
        public string visualClass;
        public string unitySpritePath;
    }

    public static class Production2DVisualCatalogBuilder
    {
        public const string CatalogAssetPath =
            "Assets/_Game/Resources/Production2D/" +
            "Production2DVisualCatalog3D.asset";
        public const string PresentationManifestPath =
            "Docs/Art/IDEA-0016/Manifests/" +
            "idea-0016-ui-character-marker-visual-assets.json";
        public const int ExpectedVisualCount = 114;
        public const int ExpectedRecipeCount = 30;

        [MenuItem("WasteCity/Art/Production 2D/Build Unified Visual Catalog")]
        public static void BuildVisualCatalog()
        {
            Production2DVisualEntry3D[] visualEntries =
                CreateExpectedVisualEntries();
            Production2DRecipeVisualEntry3D[] recipeEntries =
                CreateExpectedRecipeEntries(visualEntries);
            if (visualEntries.Length != ExpectedVisualCount)
                throw new InvalidDataException(
                    "IDEA-0016 unified visual catalog requires exactly " +
                    ExpectedVisualCount + " visual entries.");
            if (recipeEntries.Length != ExpectedRecipeCount)
                throw new InvalidDataException(
                    "IDEA-0016 unified visual catalog requires exactly " +
                    ExpectedRecipeCount + " recipe projections.");

            EnsureFolder("Assets/_Game/Resources");
            EnsureFolder("Assets/_Game/Resources/Production2D");
            Production2DVisualCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<Production2DVisualCatalog3D>(
                    CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    Production2DVisualCatalog3D>();
                catalog.name = "Production2DVisualCatalog3D";
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            if (!ContentsMatch(catalog, visualEntries, recipeEntries))
            {
                catalog.Configure(visualEntries, recipeEntries);
                if (!catalog.TryValidate(out string error))
                    throw new InvalidOperationException(error);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }
        }

        public static void BuildVisualCatalogForBatch()
        {
            Production2DSpriteAtlasBuilder.BuildAtlases();
            BuildVisualCatalog();
        }

        public static Production2DVisualEntry3D[]
            CreateExpectedVisualEntries()
        {
            var result = new List<Production2DVisualEntry3D>(
                ExpectedVisualCount);
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                result.Add(CreateRequiredEntry(
                    Production2DVisualClass.Item,
                    definition.Id,
                    Production2DItemIconCatalogBuilder.ExpectedAssetPath(
                        definition.Id)));
            }
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                result.Add(CreateRequiredEntry(
                    Production2DVisualClass.Technology,
                    definition.Id.Value,
                    Production2DTechnologyIconCatalogBuilder.ExpectedAssetPath(
                        definition.Id.Value)));
            }
            foreach (BuildingDefinition definition in BuildingCatalog.All)
            {
                result.Add(CreateRequiredEntry(
                    Production2DVisualClass.Building,
                    definition.Id.Value,
                    Production2DBuildingIconCatalogBuilder.ExpectedAssetPath(
                        definition.Id.Value)));
            }

            UnifiedPresentationManifestEntry[] presentation =
                ReadPresentationManifest();
            foreach (UnifiedPresentationManifestEntry entry in presentation)
            {
                result.Add(CreateRequiredEntry(
                    ParseVisualClass(entry.visualClass),
                    entry.contentId,
                    entry.unitySpritePath));
            }

            Production2DVisualEntry3D[] ordered = result
                .OrderBy(value => value.VisualClass)
                .ThenBy(value => value.ContentId, StringComparer.Ordinal)
                .ThenBy(value => value.Variant, StringComparer.Ordinal)
                .ToArray();
            ValidateUniqueVisualKeys(ordered);
            ValidateClassCount(ordered, Production2DVisualClass.Item, 31);
            ValidateClassCount(ordered, Production2DVisualClass.Technology, 43);
            ValidateClassCount(ordered, Production2DVisualClass.Building, 30);
            ValidateClassCount(ordered, Production2DVisualClass.Ui, 7);
            ValidateClassCount(ordered, Production2DVisualClass.Character, 1);
            ValidateClassCount(ordered, Production2DVisualClass.WorldMarker, 2);
            return ordered;
        }

        public static Production2DRecipeVisualEntry3D[]
            CreateExpectedRecipeEntries(
                IReadOnlyList<Production2DVisualEntry3D> visuals)
        {
            if (visuals == null)
                throw new ArgumentNullException(nameof(visuals));
            var itemSprites = new Dictionary<string, Sprite>(
                StringComparer.Ordinal);
            for (var index = 0; index < visuals.Count; index++)
            {
                Production2DVisualEntry3D candidate = visuals[index];
                if (candidate.VisualClass == Production2DVisualClass.Item &&
                    string.Equals(
                        candidate.Variant,
                        Production2DVisualCatalog3D.DefaultVariant,
                        StringComparison.Ordinal))
                    itemSprites.Add(candidate.ContentId, candidate.Sprite);
            }

            var result = new List<Production2DRecipeVisualEntry3D>(
                ResourceRecipeCatalog.All.Count);
            foreach (ResourceRecipeDefinition definition in
                     ResourceRecipeCatalog.All)
            {
                Production2DRecipeBadgeRule badge =
                    definition.Kind == ResourceRecipeKind.ManualCrafting
                        ? Production2DRecipeBadgeRule.Manual
                        : Production2DRecipeBadgeRule.Machine;
                string badgeToken = badge == Production2DRecipeBadgeRule.Manual
                    ? "manual"
                    : "machine";
                string outputId = string.Empty;
                Sprite outputSprite = null;
                string expectedProjection;
                if (definition.UsesBoundResourceNode)
                {
                    if (definition.Outputs.Count != 0)
                        throw new InvalidDataException(
                            "Bound-resource recipe must not freeze one item " +
                            "output visual: " + definition.Id);
                    expectedProjection =
                        "bound-resource|badge:" + badgeToken;
                }
                else
                {
                    if (definition.Outputs.Count == 0)
                        throw new InvalidDataException(
                            "Recipe requires a primary output visual: " +
                            definition.Id);
                    outputId = definition.Outputs[0].ResourceId;
                    if (!itemSprites.TryGetValue(outputId, out outputSprite))
                        throw new InvalidDataException(
                            "Recipe primary output lacks an item visual: " +
                            definition.Id + " -> " + outputId);
                    expectedProjection = "item:" + outputId +
                        "|badge:" + badgeToken;
                }

                if (!string.Equals(
                        definition.IconProjection,
                        expectedProjection,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "Recipe icon projection must explicitly match its " +
                        "formal primary output and badge: " + definition.Id);
                result.Add(new Production2DRecipeVisualEntry3D(
                    definition.Id,
                    outputId,
                    definition.UsesBoundResourceNode,
                    badge,
                    outputSprite));
            }

            return result
                .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
                .ToArray();
        }

        private static Production2DVisualEntry3D CreateRequiredEntry(
            Production2DVisualClass visualClass,
            string contentId,
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(contentId) ||
                string.IsNullOrWhiteSpace(assetPath))
                throw new InvalidDataException(
                    "Unified visual mapping requires a stable content ID " +
                    "and project-relative Sprite path.");
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
                throw new FileNotFoundException(
                    "Missing imported Sprite for unified visual key '" +
                    visualClass + "|" + contentId + "|default'.",
                    assetPath);
            return new Production2DVisualEntry3D(
                visualClass,
                contentId,
                Production2DVisualCatalog3D.DefaultVariant,
                sprite);
        }

        private static UnifiedPresentationManifestEntry[]
            ReadPresentationManifest()
        {
            if (!File.Exists(PresentationManifestPath))
                throw new FileNotFoundException(
                    "Missing IDEA-0016 UI/character/world-marker manifest.",
                    PresentationManifestPath);
            UnifiedPresentationManifest manifest = JsonUtility.FromJson<
                UnifiedPresentationManifest>(
                File.ReadAllText(PresentationManifestPath));
            if (manifest == null || manifest.entries == null)
                throw new InvalidDataException(
                    "IDEA-0016 UI/character/world-marker manifest is invalid.");
            return manifest.entries;
        }

        private static Production2DVisualClass ParseVisualClass(string value)
        {
            switch (value)
            {
                case "ui":
                    return Production2DVisualClass.Ui;
                case "character":
                    return Production2DVisualClass.Character;
                case "world-marker":
                    return Production2DVisualClass.WorldMarker;
                default:
                    throw new InvalidDataException(
                        "Unsupported presentation visualClass: " + value);
            }
        }

        private static void ValidateUniqueVisualKeys(
            IEnumerable<Production2DVisualEntry3D> entries)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Production2DVisualEntry3D entry in entries)
            {
                string key = entry.VisualClass + "|" + entry.ContentId +
                    "|" + entry.Variant;
                if (!keys.Add(key))
                    throw new InvalidDataException(
                        "Duplicate unified production 2D visual key: " + key);
            }
        }

        private static void ValidateClassCount(
            IEnumerable<Production2DVisualEntry3D> entries,
            Production2DVisualClass visualClass,
            int expected)
        {
            int actual = entries.Count(value =>
                value.VisualClass == visualClass);
            if (actual != expected)
                throw new InvalidDataException(
                    "IDEA-0016 visual class " + visualClass +
                    " requires " + expected + " entries, got " + actual + ".");
        }

        private static bool ContentsMatch(
            Production2DVisualCatalog3D catalog,
            IReadOnlyList<Production2DVisualEntry3D> expectedVisuals,
            IReadOnlyList<Production2DRecipeVisualEntry3D> expectedRecipes)
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty visuals = serialized.FindProperty("entries");
            SerializedProperty recipes = serialized.FindProperty("recipes");
            if (visuals == null || recipes == null ||
                !visuals.isArray || !recipes.isArray ||
                visuals.arraySize != expectedVisuals.Count ||
                recipes.arraySize != expectedRecipes.Count)
                return false;
            for (var index = 0; index < expectedVisuals.Count; index++)
            {
                SerializedProperty item = visuals.GetArrayElementAtIndex(index);
                Production2DVisualEntry3D expected = expectedVisuals[index];
                if (item.FindPropertyRelative("visualClass").enumValueIndex !=
                        (int)expected.VisualClass ||
                    !string.Equals(
                        item.FindPropertyRelative("contentId").stringValue,
                        expected.ContentId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        item.FindPropertyRelative("variant").stringValue,
                        expected.Variant,
                        StringComparison.Ordinal) ||
                    item.FindPropertyRelative("sprite").objectReferenceValue !=
                        expected.Sprite)
                    return false;
            }
            for (var index = 0; index < expectedRecipes.Count; index++)
            {
                SerializedProperty item = recipes.GetArrayElementAtIndex(index);
                Production2DRecipeVisualEntry3D expected = expectedRecipes[index];
                if (!string.Equals(
                        item.FindPropertyRelative("recipeId").stringValue,
                        expected.RecipeId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        item.FindPropertyRelative("primaryOutputContentId")
                            .stringValue,
                        expected.PrimaryOutputContentId,
                        StringComparison.Ordinal) ||
                    item.FindPropertyRelative("usesBoundResourceVisual")
                        .boolValue != expected.UsesBoundResourceVisual ||
                    item.FindPropertyRelative("badgeRule").enumValueIndex !=
                        (int)expected.BadgeRule ||
                    item.FindPropertyRelative("primaryOutputSprite")
                        .objectReferenceValue != expected.PrimaryOutputSprite)
                    return false;
            }
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException(
                    "Invalid asset folder path: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
