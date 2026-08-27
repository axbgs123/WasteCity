using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Graybox3D;

namespace WasteCity.Editor
{
    [Serializable]
    internal sealed class BuildingVisualManifest
    {
        public BuildingVisualManifestEntry[] entries;
    }

    [Serializable]
    internal sealed class BuildingVisualManifestEntry
    {
        public string contentId;
        public string displayNameZh;
        public string catalogEntry;
        public string sourceSummaryZh;
        public string useSummaryZh;
        public string unlockSummaryZh;
        public string loreBriefZh;
        public string visualKeywordsZh;
        public string forbiddenElementsZh;
        public string[] runtimeUsages;
        public string filename;
        public string masterPath;
        public string reviewState;
    }

    public sealed class Production2DBuildingValidationReport
    {
        internal Production2DBuildingValidationReport(
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            Errors = errors.ToArray();
            Warnings = warnings.ToArray();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool IsValid => Errors.Count == 0;
        public string FormatErrors() => string.Join(
            Environment.NewLine,
            Errors);
    }

    public static class Production2DBuildingIconCatalogBuilder
    {
        public const string BuildingsRoot =
            Production2DBuildingImportPolicy.Root;
        public const string BuildingMastersRoot =
            "Docs/Art/IDEA-0016/Source/Buildings/";
        public const int MasterSize = 1024;
        public const int MasterSafeBorder = 103;
        public const string ManifestPath =
            "Docs/Art/IDEA-0016/Manifests/" +
            "idea-0016-building-visual-assets.json";
        public const string ExtensionManifestPath =
            "Docs/Art/IDEA-0021/Manifests/" +
            "idea-0021-building-visual-assets.json";
        public const string CatalogAssetPath =
            "Assets/_Game/Resources/Production2D/" +
            "BuildingIconCatalog3D.asset";

        [MenuItem("WasteCity/Art/Production 2D/Build Building Icon Catalog")]
        public static void BuildBuildingIconCatalog()
        {
            Production2DBuildingValidationReport report =
                ValidateSourceAssets();
            if (!report.IsValid)
            {
                throw new InvalidDataException(
                    "IDEA-0016 building icon validation failed:" +
                    Environment.NewLine + report.FormatErrors());
            }

            Production2DBuildingImportPolicy.ReimportBuildingIcons();
            EnsureAssetFolder("Assets/_Game/Resources");
            EnsureAssetFolder("Assets/_Game/Resources/Production2D");
            BuildingIconCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<BuildingIconCatalog3D>(
                    CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    BuildingIconCatalog3D>();
                catalog.name = "BuildingIconCatalog3D";
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            BuildingIconOverride3D[] next = ReadManifestEntries()
                .OrderBy(item => item.contentId, StringComparer.Ordinal)
                .Select(item => new
                {
                    item.contentId,
                    Sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                        BuildingsRoot + item.filename)
                })
                .Where(item => item.Sprite != null)
                .Select(item => new BuildingIconOverride3D(
                    item.contentId,
                    item.Sprite))
                .ToArray();
            if (!OverridesMatch(catalog, next))
            {
                catalog.ConfigureOverrides(next);
                if (!catalog.TryValidate(out string error))
                    throw new InvalidOperationException(error);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssetIfDirty(catalog);
            }

            if (report.Warnings.Count > 0)
            {
                Debug.LogWarning(
                    "IDEA-0016 building icon catalog retained formal " +
                    "fallbacks:" + Environment.NewLine +
                    string.Join(Environment.NewLine, report.Warnings));
            }
        }

        public static void BuildBuildingIconCatalogForBatch()
        {
            BuildBuildingIconCatalog();
        }

        public static string ExpectedAssetPath(string buildingId)
        {
            BuildingVisualManifestEntry entry = ReadManifestEntries()
                .FirstOrDefault(item => string.Equals(
                    item.contentId,
                    buildingId,
                    StringComparison.Ordinal));
            return entry == null
                ? string.Empty
                : BuildingsRoot + entry.filename;
        }

        public static string ExpectedMasterPath(string buildingId)
        {
            BuildingVisualManifestEntry entry = ReadManifestEntries()
                .FirstOrDefault(item => string.Equals(
                    item.contentId,
                    buildingId,
                    StringComparison.Ordinal));
            return entry == null
                ? string.Empty
                : ResolveMasterPath(entry);
        }

        public static Production2DBuildingValidationReport
            ValidateSourceAssets()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            BuildingVisualManifestEntry[] entries;
            try
            {
                entries = ReadManifestEntries();
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
                return new Production2DBuildingValidationReport(
                    errors,
                    warnings);
            }

            var definitions = BuildingCatalog.All.ToDictionary(
                item => item.Id.Value,
                StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (BuildingVisualManifestEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.contentId))
                {
                    errors.Add("Building manifest contains an empty contentId.");
                    continue;
                }
                if (!seen.Add(entry.contentId))
                {
                    errors.Add("Duplicate building manifest ID: " + entry.contentId);
                    continue;
                }
                if (!definitions.TryGetValue(
                        entry.contentId,
                        out BuildingDefinition definition))
                {
                    errors.Add("Unknown building manifest ID: " + entry.contentId);
                    continue;
                }
                if (!string.Equals(
                        entry.displayNameZh,
                        definition.Name,
                        StringComparison.Ordinal))
                    errors.Add("Building Chinese name does not match catalog: " +
                        entry.contentId);
                if (string.IsNullOrWhiteSpace(entry.sourceSummaryZh) ||
                    string.IsNullOrWhiteSpace(entry.useSummaryZh) ||
                    string.IsNullOrWhiteSpace(entry.unlockSummaryZh) ||
                    string.IsNullOrWhiteSpace(entry.loreBriefZh) ||
                    string.IsNullOrWhiteSpace(entry.visualKeywordsZh) ||
                    string.IsNullOrWhiteSpace(entry.forbiddenElementsZh) ||
                    entry.runtimeUsages == null ||
                    entry.runtimeUsages.Length == 0 ||
                    entry.runtimeUsages.Any(string.IsNullOrWhiteSpace))
                    errors.Add("Building brief is incomplete: " + entry.contentId);
                if (!string.Equals(
                        entry.reviewState,
                        "art-ready",
                        StringComparison.Ordinal) &&
                    !string.Equals(entry.reviewState, "art-produced",
                        StringComparison.Ordinal) &&
                    !string.Equals(entry.reviewState, "imported",
                        StringComparison.Ordinal) &&
                    !string.Equals(entry.reviewState, "integrated",
                        StringComparison.Ordinal) &&
                    !string.Equals(entry.reviewState, "verified",
                        StringComparison.Ordinal))
                    errors.Add("Building is not approved for art production: " +
                        entry.contentId);
                if (string.IsNullOrWhiteSpace(entry.filename) ||
                    !entry.filename.StartsWith(
                        "building-",
                        StringComparison.Ordinal) ||
                    !entry.filename.EndsWith(
                        ".png",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        entry.filename,
                        entry.filename.ToLowerInvariant(),
                        StringComparison.Ordinal))
                    errors.Add("Invalid building delivery filename: " +
                        entry.contentId);

                string masterPath = ResolveMasterPath(entry);
                if (!File.Exists(masterPath))
                {
                    errors.Add("Missing formal building alpha master: " +
                        entry.contentId);
                }
                else
                {
                    ValidatePng(
                        masterPath,
                        entry.contentId,
                        MasterSize,
                        true,
                        errors);
                }

                string path = BuildingsRoot + entry.filename;
                if (!File.Exists(path))
                {
                    warnings.Add(
                        "Missing formal building icon for '" + entry.contentId +
                        "'; deterministic runtime fallback remains active.");
                    continue;
                }
                ValidatePng(
                    path,
                    entry.contentId,
                    Production2DBuildingImportPolicy.DeliverySize,
                    false,
                    errors);
            }
            foreach (string id in definitions.Keys)
            {
                if (!seen.Contains(id))
                    errors.Add("Missing building manifest ID: " + id);
            }
            if (entries.Length != BuildingCatalog.All.Length)
                errors.Add("Building manifest must contain exactly " +
                    BuildingCatalog.All.Length + " entries.");

            int upgradeOnly = entries.Count(item => item != null &&
                string.Equals(item.catalogEntry, "upgrade-only",
                    StringComparison.Ordinal));
            if (upgradeOnly != 2 ||
                !IsUpgradeOnly(entries,
                    BuildingCatalog.HeavyMachineGunTurret.Id.Value) ||
                !IsUpgradeOnly(entries,
                    BuildingCatalog.SwordRidingPlatform.Id.Value))
                errors.Add("Only the two formal upgrade targets may be " +
                    "marked upgrade-only.");
            return new Production2DBuildingValidationReport(errors, warnings);
        }

        private static bool IsUpgradeOnly(
            IEnumerable<BuildingVisualManifestEntry> entries,
            string id)
        {
            return entries.Any(item => item != null &&
                string.Equals(item.contentId, id, StringComparison.Ordinal) &&
                string.Equals(item.catalogEntry, "upgrade-only",
                    StringComparison.Ordinal));
        }

        private static BuildingVisualManifestEntry[] ReadManifestEntries()
        {
            return ReadManifest(ManifestPath)
                .Concat(ReadManifest(ExtensionManifestPath))
                .ToArray();
        }

        private static BuildingVisualManifestEntry[] ReadManifest(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "Missing formal building visual manifest.",
                    path);
            BuildingVisualManifest manifest = JsonUtility.FromJson<
                BuildingVisualManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.entries == null)
                throw new InvalidDataException(
                    "Building visual manifest is invalid JSON: " + path);
            return manifest.entries;
        }

        private static string ResolveMasterPath(
            BuildingVisualManifestEntry entry)
        {
            return string.IsNullOrWhiteSpace(entry.masterPath)
                ? BuildingMastersRoot +
                  Path.GetFileNameWithoutExtension(entry.filename) +
                  "-master-v1.png"
                : entry.masterPath.Replace('\\', '/');
        }

        private static void ValidatePng(
            string path,
            string buildingId,
            int expectedSize,
            bool requireMasterSafeBorder,
            ICollection<string> errors)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    errors.Add("Building icon is not a decodable PNG: " + path);
                    return;
                }
                if (texture.width != expectedSize ||
                    texture.height != expectedSize)
                {
                    errors.Add("Building image must be exactly " +
                        expectedSize + "x" + expectedSize + ": " + path);
                    return;
                }
                Color32[] pixels = texture.GetPixels32();
                if (!pixels.Any(pixel => pixel.a < byte.MaxValue))
                    errors.Add("Building icon requires transparent pixels: " +
                        buildingId);
                if (pixels[0].a != 0 ||
                    pixels[texture.width - 1].a != 0 ||
                    pixels[(texture.height - 1) * texture.width].a != 0 ||
                    pixels[pixels.Length - 1].a != 0)
                    errors.Add("Building icon requires fully transparent corners: " +
                        buildingId);
                if (requireMasterSafeBorder &&
                    HasVisiblePixelInSafeBorder(
                        pixels,
                        texture.width,
                        texture.height,
                        MasterSafeBorder))
                    errors.Add("Building master must keep a fully transparent " +
                        "10% safe border: " + buildingId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool HasVisiblePixelInSafeBorder(
            IReadOnlyList<Color32> pixels,
            int width,
            int height,
            int border)
        {
            int right = width - border;
            int top = height - border;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (x >= border && x < right && y >= border && y < top)
                    continue;
                if (pixels[y * width + x].a != 0) return true;
            }
            return false;
        }

        private static bool OverridesMatch(
            BuildingIconCatalog3D catalog,
            IReadOnlyList<BuildingIconOverride3D> expected)
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty values = serialized.FindProperty("overrides");
            if (values == null || !values.isArray ||
                values.arraySize != expected.Count)
                return false;
            for (var index = 0; index < expected.Count; index++)
            {
                SerializedProperty item = values.GetArrayElementAtIndex(index);
                string buildingId = item.FindPropertyRelative("buildingId")
                    .stringValue;
                UnityEngine.Object sprite = item.FindPropertyRelative("sprite")
                    .objectReferenceValue;
                if (!string.Equals(
                        buildingId,
                        expected[index].BuildingId,
                        StringComparison.Ordinal) ||
                    sprite != expected[index].Sprite)
                    return false;
            }
            return true;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException(
                    "Invalid asset folder path: " + path);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
