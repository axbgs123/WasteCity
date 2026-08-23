using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Graybox3D;

namespace WasteCity.Editor
{
    public sealed class Production2DItemIconValidationReport
    {
        private readonly string[] errors;
        private readonly string[] warnings;

        internal Production2DItemIconValidationReport(
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            this.errors = errors.ToArray();
            this.warnings = warnings.ToArray();
        }

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool IsValid => errors.Length == 0;

        public string FormatErrors()
        {
            return string.Join(Environment.NewLine, errors);
        }
    }

    /// <summary>
    /// Deterministically maps formal resource IDs to available item Sprites.
    /// Missing formal images are intentionally omitted so the runtime catalog
    /// retains its stable procedural fallback.
    /// </summary>
    public static class Production2DItemIconCatalogBuilder
    {
        public const string ItemsRoot = Production2DItemImportPolicy.Root;
        public const string ItemMastersRoot =
            "Docs/Art/IDEA-0016/Source/Items";
        public const string CatalogAssetPath =
            "Assets/_Game/Rendering/Graybox3D/ResourceIconCatalog3D.asset";

        private const string Prefix = "item-";

        [MenuItem("WasteCity/Art/Production 2D/Build Resource Item Icon Catalog")]
        public static void BuildResourceIconCatalog()
        {
            Production2DItemIconValidationReport report =
                ValidateSourceAssets();
            if (!report.IsValid)
            {
                throw new InvalidDataException(
                    "IDEA-0016 item icon validation failed:" +
                    Environment.NewLine + report.FormatErrors());
            }

            Production2DItemImportPolicy.ReimportItemIcons();
            ResourceIconCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<ResourceIconCatalog3D>(
                    CatalogAssetPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Missing approved ResourceIconCatalog3D asset: " +
                    CatalogAssetPath);
            }

            ResourceIconOverride3D[] next = ResourceDefinitionCatalog.All
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .Select(definition => CreateOverrideIfPresent(definition))
                .Where(item => item != null)
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
                    "IDEA-0016 item icon catalog retained formal fallbacks:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, report.Warnings));
            }
        }

        /// <summary>Safe -executeMethod entry for CI and local batch work.</summary>
        public static void BuildResourceIconCatalogForBatch()
        {
            BuildResourceIconCatalog();
        }

        public static string ExpectedAssetPath(string resourceId)
        {
            ResourceDefinition definition = ResourceDefinitionCatalog.All
                .FirstOrDefault(item => string.Equals(
                    item.Id,
                    resourceId,
                    StringComparison.Ordinal));
            if (definition == null)
                return string.Empty;
            string slug = FilenameSlug(definition.Id);
            return ItemsRoot + Prefix + slug + ".png";
        }

        public static string ExpectedMasterPath(string resourceId)
        {
            ResourceDefinition definition = ResourceDefinitionCatalog.All
                .FirstOrDefault(item => string.Equals(
                    item.Id,
                    resourceId,
                    StringComparison.Ordinal));
            if (definition == null)
                return string.Empty;
            return ItemMastersRoot + "/" + Prefix +
                FilenameSlug(definition.Id) + "-master-v1.png";
        }

        public static Production2DItemIconValidationReport
            ValidateMasterAssets()
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var expectedByFile = new Dictionary<string, ResourceDefinition>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                expectedByFile.Add(
                    Path.GetFileName(ExpectedMasterPath(definition.Id)),
                    definition);
            }

            string[] paths = Directory.Exists(ItemMastersRoot)
                ? Directory.GetFiles(
                        ItemMastersRoot,
                        "*.png",
                        SearchOption.TopDirectoryOnly)
                    .Select(NormalizePath)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                string file = Path.GetFileName(path);
                if (!expectedByFile.TryGetValue(
                        file,
                        out ResourceDefinition definition))
                {
                    errors.Add("Unknown or intermediate item master: " + path);
                    continue;
                }
                if (!seen.Add(definition.Id))
                {
                    errors.Add(
                        "Duplicate item master for resource '" +
                        definition.Id + "': " + path);
                    continue;
                }
                ValidatePng(
                    path,
                    definition.Id,
                    1024,
                    128,
                    errors);
            }

            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                if (!seen.Contains(definition.Id))
                    warnings.Add(
                        "Missing formal true-alpha master for '" +
                        definition.Id + "'.");
            }
            return new Production2DItemIconValidationReport(errors, warnings);
        }

        public static Production2DItemIconValidationReport
            ValidateSourceAssets()
        {
            IEnumerable<string> paths = Directory.Exists(ItemsRoot)
                ? Directory.GetFiles(ItemsRoot, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => string.Equals(
                        Path.GetExtension(path),
                        ".png",
                        StringComparison.OrdinalIgnoreCase))
                : Array.Empty<string>();
            return ValidateAssetPaths(ItemsRoot, paths, true);
        }

        public static Production2DItemIconValidationReport ValidateAssetPaths(
            string root,
            IEnumerable<string> assetPaths,
            bool inspectPngContent)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException("A project-relative root is required.", nameof(root));
            if (assetPaths == null)
                throw new ArgumentNullException(nameof(assetPaths));

            string normalizedRoot = NormalizeRoot(root);
            var errors = new List<string>();
            var warnings = new List<string>();
            var expectedByFile = new Dictionary<string, ResourceDefinition>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All)
            {
                string path = ExpectedPath(normalizedRoot, definition.Id);
                string file = Path.GetFileName(path);
                if (!expectedByFile.TryAdd(file, definition))
                {
                    errors.Add(
                        "Duplicate formal filename mapping for resource '" +
                        definition.Id + "': " + file);
                }
            }

            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] orderedPaths = assetPaths
                .Select(NormalizePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (string path in orderedPaths)
            {
                if (!path.StartsWith(normalizedRoot, StringComparison.Ordinal))
                {
                    errors.Add("Item icon path is outside the approved root: " + path);
                    continue;
                }
                string file = Path.GetFileName(path);
                if (!expectedByFile.TryGetValue(file, out ResourceDefinition definition))
                {
                    errors.Add("Unknown item icon filename: " + file);
                    continue;
                }
                string expectedFile = Path.GetFileName(
                    ExpectedPath(normalizedRoot, definition.Id));
                if (!string.Equals(file, expectedFile, StringComparison.Ordinal))
                {
                    errors.Add(
                        "Item icon filename must use exact lowercase spelling '" +
                        expectedFile + "': " + file);
                }
                if (seen.TryGetValue(definition.Id, out string firstPath))
                {
                    errors.Add(
                        "Duplicate item icon for resource '" + definition.Id +
                        "': " + firstPath + " and " + path);
                    continue;
                }
                seen.Add(definition.Id, path);
                if (inspectPngContent)
                    ValidatePng(path, definition.Id, 256, 32, errors);
            }

            foreach (ResourceDefinition definition in ResourceDefinitionCatalog.All
                         .OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                if (!seen.ContainsKey(definition.Id))
                {
                    warnings.Add(
                        "Missing formal item icon for '" + definition.Id +
                        "'; deterministic runtime fallback remains active.");
                }
            }
            return new Production2DItemIconValidationReport(errors, warnings);
        }

        private static ResourceIconOverride3D CreateOverrideIfPresent(
            ResourceDefinition definition)
        {
            string path = ExpectedAssetPath(definition.Id);
            if (!File.Exists(path))
                return null;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "Validated item PNG did not import as a single Sprite: " +
                    path);
            }
            return new ResourceIconOverride3D(definition.Id, sprite);
        }

        private static bool OverridesMatch(
            ResourceIconCatalog3D catalog,
            IReadOnlyList<ResourceIconOverride3D> expected)
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty values = serialized.FindProperty("overrides");
            if (values == null || !values.isArray ||
                values.arraySize != expected.Count)
                return false;
            for (var index = 0; index < expected.Count; index++)
            {
                SerializedProperty item = values.GetArrayElementAtIndex(index);
                string resourceId = item.FindPropertyRelative("resourceId")
                    .stringValue;
                UnityEngine.Object sprite = item.FindPropertyRelative("sprite")
                    .objectReferenceValue;
                if (!string.Equals(
                        resourceId,
                        expected[index].ResourceId,
                        StringComparison.Ordinal) ||
                    sprite != expected[index].Sprite)
                    return false;
            }
            return true;
        }

        private static void ValidatePng(
            string path,
            string resourceId,
            int expectedSize,
            int safeMargin,
            ICollection<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("Missing item PNG file for '" + resourceId + "': " + path);
                return;
            }
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                errors.Add("Cannot read item PNG '" + path + "': " + exception.Message);
                return;
            }
            if (!TryReadPngHeader(bytes, out int width, out int height, out bool hasAlpha))
            {
                errors.Add("Item icon is not a decodable PNG: " + path);
                return;
            }
            if (width != expectedSize || height != expectedSize)
            {
                errors.Add(
                    "Item icon must be exactly " + expectedSize + "x" +
                    expectedSize + " pixels: " + path +
                    " is " + width + "x" + height + ".");
            }
            if (!hasAlpha)
            {
                errors.Add("Item icon PNG must include an alpha channel: " + path);
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    errors.Add("Item icon is not a decodable PNG: " + path);
                    return;
                }
                Color32[] pixels = texture.GetPixels32();
                bool hasTransparentPixel = pixels
                    .Any(pixel => pixel.a < byte.MaxValue);
                if (!hasTransparentPixel)
                {
                    errors.Add(
                        "Item icon alpha channel contains no transparent pixels: " +
                        path);
                }
                ValidateSafeArea(
                    pixels,
                    texture.width,
                    texture.height,
                    safeMargin,
                    path,
                    errors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void ValidateSafeArea(
            IReadOnlyList<Color32> pixels,
            int width,
            int height,
            int safeMargin,
            string path,
            ICollection<string> errors)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a < 16) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
            if (maxX < 0)
            {
                errors.Add("Item icon contains no visible subject: " + path);
                return;
            }
            if (minX < safeMargin || minY < safeMargin ||
                maxX >= width - safeMargin ||
                maxY >= height - safeMargin)
            {
                errors.Add(
                    "Item icon subject leaves less than the formal " +
                    safeMargin + "px safe margin: " + path + ".");
            }
        }

        private static bool TryReadPngHeader(
            IReadOnlyList<byte> bytes,
            out int width,
            out int height,
            out bool hasAlpha)
        {
            width = 0;
            height = 0;
            hasAlpha = false;
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (bytes == null || bytes.Count < 26)
                return false;
            for (var index = 0; index < signature.Length; index++)
            {
                if (bytes[index] != signature[index])
                    return false;
            }
            if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' ||
                bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
                return false;
            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            byte colorType = bytes[25];
            hasAlpha = colorType == 4 || colorType == 6;
            return width > 0 && height > 0;
        }

        private static int ReadBigEndianInt32(IReadOnlyList<byte> bytes, int index)
        {
            return bytes[index] << 24 |
                bytes[index + 1] << 16 |
                bytes[index + 2] << 8 |
                bytes[index + 3];
        }

        private static string ExpectedPath(string normalizedRoot, string resourceId)
        {
            string slug = FilenameSlug(resourceId);
            return normalizedRoot + Prefix + slug + ".png";
        }

        private static string FilenameSlug(string resourceId)
        {
            // These two formal IDs intentionally use short gameplay IDs while
            // their delivery names retain the category word players see.
            if (string.Equals(
                    resourceId,
                    ResourceIds.BiologicalWeapon,
                    StringComparison.Ordinal))
                return "biological-weapon";
            if (string.Equals(
                    resourceId,
                    ResourceIds.PsionicAmplifier,
                    StringComparison.Ordinal))
                return "psionic-amplifier";
            return resourceId.Substring(resourceId.LastIndexOf('.') + 1);
        }

        private static string NormalizeRoot(string root)
        {
            string normalized = NormalizePath(root).TrimEnd('/');
            return normalized + "/";
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
