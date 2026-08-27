using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.Research;

namespace WasteCity.Editor
{
    public sealed class Production2DTechnologyIconValidationReport
    {
        private readonly string[] errors;
        private readonly string[] warnings;

        internal Production2DTechnologyIconValidationReport(
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            this.errors = errors.ToArray();
            this.warnings = warnings.ToArray();
        }

        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;
        public bool IsValid => errors.Length == 0;
        public string FormatErrors() => string.Join(Environment.NewLine, errors);
    }

    /// <summary>
    /// Validates and deterministically maps IDEA-0016 formal research IDs to
    /// transparent technology emblem Sprites. Missing art is omitted so the
    /// runtime catalog retains its stable route/tier fallback.
    /// </summary>
    public static class Production2DTechnologyIconCatalogBuilder
    {
        public const string TechnologyRoot =
            Production2DTechnologyImportPolicy.Root;
        public const string TechnologyMastersRoot =
            "Docs/Art/IDEA-0016/Source/Technologies/";
        public const string CatalogAssetPath =
            "Assets/_Game/Resources/Production2D/ResearchIconCatalog3D.asset";

        private const string Prefix = "tech-";
        private const string MasterSuffix = "-master-v1.png";
        private const string LegacyAnalysisId =
            "core.research.legacy-analysis";
        private const string LegacyAnalysisPlaceholderSourceId =
            "core.research.artifact-crafting";

        [MenuItem("WasteCity/Art/Production 2D/Build Technology Icon Catalog")]
        public static void BuildTechnologyIconCatalog()
        {
            EnsureLegacyAnalysisPlaceholderAssets();
            Production2DTechnologyIconValidationReport report =
                ValidateSourceAssets();
            if (!report.IsValid)
            {
                throw new InvalidDataException(
                    "IDEA-0016 technology icon validation failed:" +
                    Environment.NewLine + report.FormatErrors());
            }

            Production2DTechnologyImportPolicy.ReimportTechnologyIcons();
            EnsureFolder("Assets/_Game/Resources/Production2D");
            ResearchIconCatalog3D catalog =
                AssetDatabase.LoadAssetAtPath<ResearchIconCatalog3D>(
                    CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ResearchIconCatalog3D>();
                catalog.name = "ResearchIconCatalog3D";
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            ResearchIconOverride3D[] next = ResearchCatalog.All
                .OrderBy(definition => definition.Id.Value,
                    StringComparer.Ordinal)
                .Select(CreateOverrideIfPresent)
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
                    "IDEA-0016 technology catalog retained formal fallbacks:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, report.Warnings));
            }
        }

        public static void BuildTechnologyIconCatalogForBatch()
        {
            BuildTechnologyIconCatalog();
        }

        public static void EnsureLegacyAnalysisPlaceholderAssetsForBatch()
        {
            EnsureLegacyAnalysisPlaceholderAssets();
        }

        private static void EnsureLegacyAnalysisPlaceholderAssets()
        {
            string delivery = ExpectedAssetPath(LegacyAnalysisId);
            string master = ExpectedMasterPath(LegacyAnalysisId);
            string sourceDelivery = ExpectedAssetPath(
                LegacyAnalysisPlaceholderSourceId);
            string sourceMaster = ExpectedMasterPath(
                LegacyAnalysisPlaceholderSourceId);
            if (string.IsNullOrEmpty(delivery) || string.IsNullOrEmpty(master) ||
                !File.Exists(sourceDelivery) || !File.Exists(sourceMaster))
            {
                throw new FileNotFoundException(
                    "Legacy analysis placeholder source is unavailable.");
            }
            Directory.CreateDirectory(TechnologyMastersRoot);
            if (!File.Exists(master)) File.Copy(sourceMaster, master, false);
            if (!File.Exists(delivery))
            {
                File.Copy(sourceDelivery, delivery, false);
                AssetDatabase.ImportAsset(
                    delivery,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        public static string ExpectedAssetPath(string researchId)
        {
            ResearchDefinition definition = ResearchCatalog.Find(researchId);
            if (definition == null || definition.CatalogOrder < 0)
                return string.Empty;
            return TechnologyRoot + Prefix + FilenameSlug(researchId) + ".png";
        }

        public static string ExpectedMasterPath(string researchId)
        {
            ResearchDefinition definition = ResearchCatalog.Find(researchId);
            if (definition == null || definition.CatalogOrder < 0)
                return string.Empty;
            return TechnologyMastersRoot + Prefix + FilenameSlug(researchId) +
                MasterSuffix;
        }

        public static Production2DTechnologyIconValidationReport
            ValidateSourceAssets()
        {
            IEnumerable<string> paths = Directory.Exists(TechnologyRoot)
                ? Directory.GetFiles(
                        TechnologyRoot,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(path => string.Equals(
                        Path.GetExtension(path),
                        ".png",
                        StringComparison.OrdinalIgnoreCase))
                : Array.Empty<string>();
            Production2DTechnologyIconValidationReport delivery =
                ValidateAssetPaths(TechnologyRoot, paths, true);
            Production2DTechnologyIconValidationReport masters =
                ValidateMasterSourceAssets();
            return new Production2DTechnologyIconValidationReport(
                delivery.Errors.Concat(masters.Errors),
                delivery.Warnings.Concat(masters.Warnings));
        }

        public static Production2DTechnologyIconValidationReport
            ValidateMasterSourceAssets()
        {
            IEnumerable<string> paths = Directory.Exists(TechnologyMastersRoot)
                ? Directory.GetFiles(
                        TechnologyMastersRoot,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .Where(path => string.Equals(
                        Path.GetExtension(path),
                        ".png",
                        StringComparison.OrdinalIgnoreCase))
                : Array.Empty<string>();
            return ValidateMasterAssetPaths(
                TechnologyMastersRoot,
                paths,
                inspectPngContent: true);
        }

        public static Production2DTechnologyIconValidationReport
            ValidateMasterAssetPaths(
                string root,
                IEnumerable<string> assetPaths,
                bool inspectPngContent)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException(
                    "A project-relative root is required.", nameof(root));
            if (assetPaths == null)
                throw new ArgumentNullException(nameof(assetPaths));

            string normalizedRoot = NormalizeRoot(root);
            var errors = new List<string>();
            var warnings = new List<string>();
            var expectedByFile = new Dictionary<string, ResearchDefinition>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                string file = Path.GetFileName(ExpectedMasterPathForRoot(
                    normalizedRoot, definition.Id.Value));
                if (!expectedByFile.TryAdd(file, definition))
                {
                    errors.Add(
                        "Duplicate formal master filename mapping for research '" +
                        definition.Id.Value + "': " + file);
                }
            }

            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string path in assetPaths
                         .Select(NormalizePath)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!path.StartsWith(normalizedRoot, StringComparison.Ordinal))
                {
                    errors.Add(
                        "Technology master path is outside the approved root: " +
                        path);
                    continue;
                }
                string file = Path.GetFileName(path);
                if (!expectedByFile.TryGetValue(
                        file, out ResearchDefinition definition))
                {
                    errors.Add("Unknown technology master filename: " + file);
                    continue;
                }
                string expectedFile = Path.GetFileName(ExpectedMasterPathForRoot(
                    normalizedRoot, definition.Id.Value));
                if (!string.Equals(file, expectedFile, StringComparison.Ordinal))
                {
                    errors.Add(
                        "Technology master filename must use exact lowercase " +
                        "spelling '" + expectedFile + "': " + file);
                }
                if (seen.TryGetValue(definition.Id.Value, out string firstPath))
                {
                    errors.Add(
                        "Duplicate technology master for research '" +
                        definition.Id.Value + "': " + firstPath + " and " + path);
                    continue;
                }
                seen.Add(definition.Id.Value, path);
                if (inspectPngContent)
                {
                    ValidatePng(
                        path,
                        definition.Id.Value,
                        1024,
                        128,
                        errors,
                        "Technology master");
                }
            }

            foreach (ResearchDefinition definition in ResearchCatalog.All
                         .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                if (!seen.ContainsKey(definition.Id.Value))
                {
                    warnings.Add(
                        "Missing formal technology master for '" +
                        definition.Id.Value + "'.");
                }
            }
            return new Production2DTechnologyIconValidationReport(
                errors, warnings);
        }

        public static Production2DTechnologyIconValidationReport
            ValidateAssetPaths(
                string root,
                IEnumerable<string> assetPaths,
                bool inspectPngContent)
        {
            if (string.IsNullOrWhiteSpace(root))
                throw new ArgumentException(
                    "A project-relative root is required.", nameof(root));
            if (assetPaths == null)
                throw new ArgumentNullException(nameof(assetPaths));

            string normalizedRoot = NormalizeRoot(root);
            var errors = new List<string>();
            var warnings = new List<string>();
            var expectedByFile = new Dictionary<string, ResearchDefinition>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ResearchDefinition definition in ResearchCatalog.All)
            {
                string file = Path.GetFileName(ExpectedPath(
                    normalizedRoot, definition.Id.Value));
                if (!expectedByFile.TryAdd(file, definition))
                {
                    errors.Add(
                        "Duplicate formal filename mapping for research '" +
                        definition.Id.Value + "': " + file);
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
                    errors.Add(
                        "Technology icon path is outside the approved root: " +
                        path);
                    continue;
                }
                string file = Path.GetFileName(path);
                if (!expectedByFile.TryGetValue(
                        file, out ResearchDefinition definition))
                {
                    errors.Add(
                        "Unknown technology icon filename: " + file);
                    continue;
                }
                string expectedFile = Path.GetFileName(ExpectedPath(
                    normalizedRoot, definition.Id.Value));
                if (!string.Equals(file, expectedFile, StringComparison.Ordinal))
                {
                    errors.Add(
                        "Technology icon filename must use exact lowercase " +
                        "spelling '" + expectedFile + "': " + file);
                }
                if (seen.TryGetValue(definition.Id.Value, out string firstPath))
                {
                    errors.Add(
                        "Duplicate technology icon for research '" +
                        definition.Id.Value + "': " + firstPath + " and " +
                        path);
                    continue;
                }
                seen.Add(definition.Id.Value, path);
                if (inspectPngContent)
                    ValidatePng(
                        path,
                        definition.Id.Value,
                        Production2DTechnologyImportPolicy.DeliverySize,
                        32,
                        errors,
                        "Technology icon");
            }

            foreach (ResearchDefinition definition in ResearchCatalog.All
                         .OrderBy(value => value.Id.Value,
                             StringComparer.Ordinal))
            {
                if (!seen.ContainsKey(definition.Id.Value))
                {
                    warnings.Add(
                        "Missing formal technology icon for '" +
                        definition.Id.Value +
                        "'; deterministic runtime fallback remains active.");
                }
            }
            return new Production2DTechnologyIconValidationReport(
                errors, warnings);
        }

        private static ResearchIconOverride3D CreateOverrideIfPresent(
            ResearchDefinition definition)
        {
            string path = ExpectedAssetPath(definition.Id.Value);
            if (!File.Exists(path)) return null;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "Validated technology PNG did not import as a single " +
                    "Sprite: " + path);
            }
            return new ResearchIconOverride3D(definition.Id.Value, sprite);
        }

        private static bool OverridesMatch(
            ResearchIconCatalog3D catalog,
            IReadOnlyList<ResearchIconOverride3D> expected)
        {
            var serialized = new SerializedObject(catalog);
            SerializedProperty values = serialized.FindProperty("overrides");
            if (values == null || !values.isArray ||
                values.arraySize != expected.Count)
                return false;
            for (var index = 0; index < expected.Count; index++)
            {
                SerializedProperty item = values.GetArrayElementAtIndex(index);
                string id = item.FindPropertyRelative("researchId").stringValue;
                UnityEngine.Object sprite = item.FindPropertyRelative("sprite")
                    .objectReferenceValue;
                if (!string.Equals(id, expected[index].ResearchId,
                        StringComparison.Ordinal) ||
                    sprite != expected[index].Sprite)
                    return false;
            }
            return true;
        }

        private static void ValidatePng(
            string path,
            string researchId,
            int expectedSize,
            int safeAreaInset,
            ICollection<string> errors,
            string assetKind)
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                errors.Add(
                    "Cannot read " + assetKind.ToLowerInvariant() + " PNG '" +
                    path + "': " +
                    exception.Message);
                return;
            }
            if (!TryReadPngHeader(
                    bytes, out int width, out int height, out bool hasAlpha))
            {
                errors.Add(assetKind + " is not a decodable PNG: " + path);
                return;
            }
            if (width != expectedSize || height != expectedSize)
            {
                errors.Add(
                    assetKind + " must be exactly " + expectedSize + "x" +
                    expectedSize + " pixels: " +
                    path + " is " + width + "x" + height + ".");
            }
            if (!hasAlpha)
            {
                errors.Add(
                    assetKind + " PNG must include an alpha channel: " +
                    path);
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false))
                {
                    errors.Add(
                        assetKind + " is not a decodable PNG: " + path);
                    return;
                }
                Color32[] pixels = texture.GetPixels32();
                int[] corners =
                {
                    0,
                    width - 1,
                    (height - 1) * width,
                    height * width - 1
                };
                if (corners.Any(index => pixels[index].a != 0))
                {
                    errors.Add(
                        assetKind + " corners must be fully transparent: " +
                        path);
                }
                int visible = pixels.Count(pixel => pixel.a >= 16);
                float ratio = visible / (float)pixels.Length;
                if (ratio < .08f || ratio > .64f)
                {
                    errors.Add(
                        assetKind + " subject coverage must stay between " +
                        "8% and 64%: " + path + " is " +
                        ratio.ToString("P1") + ".");
                }
                if (visible > 0)
                {
                    int minX = width;
                    int minY = height;
                    int maxX = -1;
                    int maxY = -1;
                    var chromaPixels = 0;
                    for (var y = 0; y < height; y++)
                    for (var x = 0; x < width; x++)
                    {
                        Color32 pixel = pixels[y * width + x];
                        if (pixel.a < 16) continue;
                        minX = Mathf.Min(minX, x);
                        minY = Mathf.Min(minY, y);
                        maxX = Mathf.Max(maxX, x);
                        maxY = Mathf.Max(maxY, y);
                        if (IsPureChroma(pixel)) chromaPixels++;
                    }
                    if (minX < safeAreaInset || minY < safeAreaInset ||
                        maxX >= width - safeAreaInset ||
                        maxY >= height - safeAreaInset)
                    {
                        errors.Add(
                            assetKind + " visible pixels must stay inside the " +
                            safeAreaInset + "px safe area: " + path +
                            " has bounds [" + minX + "," + minY + "]-[" +
                            maxX + "," + maxY + "].");
                    }
                    if (chromaPixels > 0)
                    {
                        errors.Add(
                            assetKind + " contains " + chromaPixels +
                            " visible pure chroma-key pixels: " + path);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static bool IsPureChroma(Color32 pixel)
        {
            return pixel.r >= 250 && pixel.g <= 5 && pixel.b >= 250 ||
                pixel.g >= 250 && pixel.r <= 5 && pixel.b <= 5 ||
                pixel.b >= 250 && pixel.r <= 5 && pixel.g <= 5;
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
            if (bytes == null || bytes.Count < 26) return false;
            for (var index = 0; index < signature.Length; index++)
                if (bytes[index] != signature[index]) return false;
            if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' ||
                bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
                return false;
            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            byte colorType = bytes[25];
            hasAlpha = colorType == 4 || colorType == 6;
            return width > 0 && height > 0;
        }

        private static int ReadBigEndianInt32(
            IReadOnlyList<byte> bytes, int index)
        {
            return bytes[index] << 24 |
                bytes[index + 1] << 16 |
                bytes[index + 2] << 8 |
                bytes[index + 3];
        }

        private static string ExpectedPath(string root, string researchId)
        {
            return root + Prefix + FilenameSlug(researchId) + ".png";
        }

        private static string ExpectedMasterPathForRoot(
            string root,
            string researchId)
        {
            return root + Prefix + FilenameSlug(researchId) + MasterSuffix;
        }

        private static string FilenameSlug(string researchId)
        {
            const string prefix = "core.research.";
            const string bridgePrefix = "bridge.";
            string slug = researchId.StartsWith(prefix, StringComparison.Ordinal)
                ? researchId.Substring(prefix.Length)
                : researchId;
            return slug.StartsWith(bridgePrefix, StringComparison.Ordinal)
                ? "bridge-" + slug.Substring(bridgePrefix.Length)
                : slug;
        }

        private static string NormalizeRoot(string root)
        {
            return NormalizePath(root).TrimEnd('/') + "/";
        }

        private static string NormalizePath(string path) =>
            path.Replace('\\', '/');

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
