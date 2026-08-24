using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using WasteCity.ArtIntegration3D;

[assembly: InternalsVisibleTo("WasteCity.EditModeTests")]

namespace WasteCity.Editor
{
    public static class FirstArtTerrainAssetBuilder
    {
        public const string BaseColorArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_BaseColor.asset";
        public const string NormalArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Normal.asset";
        public const string MaskArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Mask.asset";
        public const string HeightArrayPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Generated/TA_Terrain_Height.asset";
        public const string MaterialPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Materials/MAT_Terrain_FirstPass.mat";
        public const string ProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset";
        public const string ShaderPath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Shaders/WasteCityFirstPassTerrain.shader";

        private const string TerrainRoot =
            "Assets/_Game/Art/FirstPass/Environment/Terrain";
        private const string CartographicConceptRoot =
            "ArtSource/FirstPass/Environment/Terrain";
        private const string RuntimeFolder = TerrainRoot + "/Runtime";
        private const string GeneratedFolder = RuntimeFolder + "/Generated";
        private const string MaterialsFolder = RuntimeFolder + "/Materials";
        private const string ProfilesFolder = RuntimeFolder + "/Profiles";
        private const string ShadersFolder = RuntimeFolder + "/Shaders";
        private const int SourceTextureSize = 2048;
        private const int CartographicConceptTextureSize = 1254;
        private const int CartographicSeamBlendWidth = 96;
        private const int HeightTextureSize = 1024;

        internal static Action<string> HeightSourceReadableCheckpoint;
        internal static Action<string> MaskCompressionCheckpoint;
        internal static Action<int, string> DestinationPersistCheckpoint;
        internal static Action<string> DestinationRollbackCheckpoint;

        [MenuItem("WasteCity/Art/Build First Terrain Runtime Assets")]
        public static void BuildRuntimeAssets()
        {
            BuildTextureArrays();
            EnsureRuntimeAssetFolders();

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !string.Equals(
                    shader.name,
                    FirstArtTerrainProfile3D.RequiredShaderName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Required terrain Shader is missing or has the wrong name: {ShaderPath}");
            }

            Texture2DArray baseColor = LoadRequiredArray(BaseColorArrayPath);
            Texture2DArray normal = LoadRequiredArray(NormalArrayPath);
            Texture2DArray mask = LoadRequiredArray(MaskArrayPath);
            Texture2DArray height = LoadRequiredArray(HeightArrayPath);
            Material material = LoadOrCreateMaterial(shader);
            material.SetTexture("_BaseColorArray", baseColor);
            material.SetTexture("_NormalArray", normal);
            material.SetTexture("_MaskArray", mask);
            material.SetTexture("_HeightArray", height);
            ApplyCartographicVisualStyle(material);
            EditorUtility.SetDirty(material);

            FirstArtTerrainProfile3D profile = LoadOrCreateProfile();
            profile.Configure(material, baseColor, normal, mask, height);
            if (!profile.TryValidate(out string validationError))
                throw new InvalidOperationException(validationError);
            EditorUtility.SetDirty(profile);

            AssetDatabase.SaveAssets();
            ReimportOutput(MaterialPath);
            ReimportOutput(ProfilePath);

            Material reloadedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            FirstArtTerrainProfile3D reloadedProfile =
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(ProfilePath);
            if (reloadedMaterial == null || reloadedProfile == null)
                throw new InvalidOperationException("Generated terrain Material or Profile could not be reloaded.");
            if (!reloadedProfile.TryValidate(out validationError))
                throw new InvalidOperationException(validationError);
        }

        [MenuItem("WasteCity/Art/Rebuild IDEA-0018 Terrain Source Channels")]
        public static void RebuildCartographicSourceChannels()
        {
            var originalFiles = new Dictionary<string, byte[]>(
                StringComparer.Ordinal);
            var originalMetaFiles = new Dictionary<string, byte[]>(
                StringComparer.Ordinal);
            var originalGuids = new Dictionary<string, string>(
                StringComparer.Ordinal);
            IReadOnlyDictionary<string, byte[]> generated =
                GenerateCartographicSourceFilesFromConcepts();

            for (int layer = 0;
                 layer < FirstArtTerrainCatalog3D.LayerCount;
                 layer++)
            {
                var terrainLayer = (FirstArtTerrainLayer3D)layer;
                string terrainName = TerrainName(terrainLayer);
                AddGeneratedSource(
                    originalFiles,
                    originalMetaFiles,
                    originalGuids,
                    SourcePath(terrainName, SourceChannel.BaseColor));
                AddGeneratedSource(
                    originalFiles,
                    originalMetaFiles,
                    originalGuids,
                    SourcePath(terrainName, SourceChannel.Height));
                AddGeneratedSource(
                    originalFiles,
                    originalMetaFiles,
                    originalGuids,
                    SourcePath(terrainName, SourceChannel.Normal));
                AddGeneratedSource(
                    originalFiles,
                    originalMetaFiles,
                    originalGuids,
                    SourcePath(terrainName, SourceChannel.Mask));
            }

            try
            {
                foreach (KeyValuePair<string, byte[]> pair in generated)
                    File.WriteAllBytes(AbsoluteProjectPath(pair.Key), pair.Value);
                ReimportGeneratedSources(generated.Keys);
                ValidatePreservedSourceIdentity(
                    originalMetaFiles,
                    originalGuids);
                BuildTextureArrays();
            }
            catch
            {
                foreach (KeyValuePair<string, byte[]> pair in originalFiles)
                    File.WriteAllBytes(AbsoluteProjectPath(pair.Key), pair.Value);
                ReimportGeneratedSources(originalFiles.Keys);
                throw;
            }
        }

        private static void AddGeneratedSource(
            IDictionary<string, byte[]> originals,
            IDictionary<string, byte[]> originalMetaFiles,
            IDictionary<string, string> originalGuids,
            string path)
        {
            string absolutePath = AbsoluteProjectPath(path);
            string absoluteMetaPath = absolutePath + ".meta";
            if (!File.Exists(absolutePath) || !File.Exists(absoluteMetaPath))
            {
                throw new FileNotFoundException(
                    "IDEA-0018 source channel or meta is missing: " + path,
                    path);
            }
            originals.Add(path, File.ReadAllBytes(absolutePath));
            originalMetaFiles.Add(path, File.ReadAllBytes(absoluteMetaPath));
            originalGuids.Add(path, AssetDatabase.AssetPathToGUID(path));
        }

        internal static IReadOnlyDictionary<string, byte[]>
            GenerateCartographicSourceFilesFromConcepts()
        {
            var generated = new Dictionary<string, byte[]>(
                FirstArtTerrainCatalog3D.LayerCount * 4,
                StringComparer.Ordinal);
            for (int layer = 0;
                 layer < FirstArtTerrainCatalog3D.LayerCount;
                 layer++)
            {
                var terrainLayer = (FirstArtTerrainLayer3D)layer;
                string terrainName = TerrainName(terrainLayer);
                CartographicSourceChannels channels =
                    GenerateCartographicSourceChannels(
                        ConceptPath(terrainName),
                        terrainLayer);
                generated.Add(
                    SourcePath(terrainName, SourceChannel.BaseColor),
                    channels.BaseColorPng);
                generated.Add(
                    SourcePath(terrainName, SourceChannel.Height),
                    channels.HeightPng);
                generated.Add(
                    SourcePath(terrainName, SourceChannel.Normal),
                    channels.NormalPng);
                generated.Add(
                    SourcePath(terrainName, SourceChannel.Mask),
                    channels.MaskPng);
            }
            return generated;
        }

        private static CartographicSourceChannels
            GenerateCartographicSourceChannels(
                string conceptPath,
                FirstArtTerrainLayer3D layer)
        {
            var concept = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                false,
                false);
            try
            {
                if (!concept.LoadImage(
                        File.ReadAllBytes(
                            AbsoluteProjectPath(conceptPath)),
                        false) ||
                    concept.width != CartographicConceptTextureSize ||
                    concept.height != CartographicConceptTextureSize)
                {
                    throw new InvalidOperationException(
                        "IDEA-0018 immutable concept must decode as " +
                        CartographicConceptTextureSize + "x" +
                        CartographicConceptTextureSize + ": " + conceptPath);
                }

                Color32[] basePixels = BlendOppositeEdgeBands(
                    ResampleBilinearCpu(
                        concept.GetPixels32(),
                        concept.width,
                        concept.height,
                        SourceTextureSize,
                        SourceTextureSize),
                    SourceTextureSize,
                    CartographicSeamBlendWidth);
                int pixelCount = basePixels.Length;
                var luminance = new float[pixelCount];
                for (int index = 0; index < pixelCount; index++)
                {
                    Color32 color = basePixels[index];
                    luminance[index] =
                        (color.r * .2126f +
                         color.g * .7152f +
                         color.b * .0722f) / 255f;
                }

                float[] broad = PeriodicBoxBlur(
                    luminance,
                    SourceTextureSize,
                    SourceTextureSize,
                    16);
                float[] local = PeriodicBoxBlur(
                    luminance,
                    SourceTextureSize,
                    SourceTextureSize,
                    3);
                ushort[] height = BuildCartographicHeight(broad, layer);
                Color32[] normal = BuildCartographicNormal(height, layer);
                Color32[] mask = BuildCartographicMask(
                    luminance,
                    local,
                    height,
                    layer);
                return new CartographicSourceChannels(
                    EncodeColorPng(basePixels, TextureFormat.RGB24),
                    EncodeHeightPng(height),
                    EncodeColorPng(normal, TextureFormat.RGB24),
                    EncodeColorPng(mask, TextureFormat.RGBA32));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(concept);
            }
        }

        private static Color32[] ResampleBilinearCpu(
            Color32[] source,
            int sourceWidth,
            int sourceHeight,
            int destinationWidth,
            int destinationHeight)
        {
            if (source == null ||
                sourceWidth <= 0 ||
                sourceHeight <= 0 ||
                source.Length != sourceWidth * sourceHeight)
            {
                throw new ArgumentException(
                    "Cartographic concept pixel count does not match size.",
                    nameof(source));
            }
            if (destinationWidth <= 0 || destinationHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(destinationWidth));

            var horizontal = new Color32[destinationWidth * sourceHeight];
            var xSample = BuildResampleAxis(sourceWidth, destinationWidth);
            for (int y = 0; y < sourceHeight; y++)
            {
                int sourceRow = y * sourceWidth;
                int destinationRow = y * destinationWidth;
                for (int x = 0; x < destinationWidth; x++)
                {
                    ResampleCoordinate sample = xSample[x];
                    horizontal[destinationRow + x] = InterpolateColor(
                        source[sourceRow + sample.Lower],
                        source[sourceRow + sample.Upper],
                        sample.Fraction);
                }
            }

            var output = new Color32[destinationWidth * destinationHeight];
            var ySample = BuildResampleAxis(sourceHeight, destinationHeight);
            for (int y = 0; y < destinationHeight; y++)
            {
                ResampleCoordinate sample = ySample[y];
                int lowerRow = sample.Lower * destinationWidth;
                int upperRow = sample.Upper * destinationWidth;
                int destinationRow = y * destinationWidth;
                for (int x = 0; x < destinationWidth; x++)
                {
                    output[destinationRow + x] = InterpolateColor(
                        horizontal[lowerRow + x],
                        horizontal[upperRow + x],
                        sample.Fraction);
                }
            }
            return output;
        }

        private static ResampleCoordinate[] BuildResampleAxis(
            int sourceSize,
            int destinationSize)
        {
            const int fractionScale = 65536;
            var samples = new ResampleCoordinate[destinationSize];
            long divisor = 2L * destinationSize;
            long maximum = (long)(sourceSize - 1) * fractionScale;
            for (int destination = 0;
                 destination < destinationSize;
                 destination++)
            {
                long numerator =
                    ((2L * destination + 1L) * sourceSize - destinationSize) *
                    fractionScale;
                long fixedCoordinate = FloorDivide(numerator, divisor);
                fixedCoordinate = Math.Max(0L, Math.Min(maximum, fixedCoordinate));
                int lower = (int)(fixedCoordinate / fractionScale);
                int fraction = (int)(fixedCoordinate % fractionScale);
                samples[destination] = new ResampleCoordinate(
                    lower,
                    Math.Min(lower + 1, sourceSize - 1),
                    fraction);
            }
            return samples;
        }

        private static long FloorDivide(long numerator, long denominator)
        {
            long quotient = numerator / denominator;
            long remainder = numerator % denominator;
            return remainder < 0L ? quotient - 1L : quotient;
        }

        private static Color32 InterpolateColor(
            Color32 lower,
            Color32 upper,
            int fraction)
        {
            const int fractionScale = 65536;
            int inverse = fractionScale - fraction;
            return new Color32(
                InterpolateByte(lower.r, upper.r, inverse, fraction),
                InterpolateByte(lower.g, upper.g, inverse, fraction),
                InterpolateByte(lower.b, upper.b, inverse, fraction),
                InterpolateByte(lower.a, upper.a, inverse, fraction));
        }

        private static byte InterpolateByte(
            byte lower,
            byte upper,
            int inverse,
            int fraction)
        {
            return (byte)((lower * inverse + upper * fraction + 32768) >> 16);
        }

        private static Color32[] BlendOppositeEdgeBands(
            Color32[] source,
            int size,
            int blendWidth)
        {
            if (source == null || source.Length != size * size)
                throw new ArgumentException(
                    "Cartographic BaseColor pixel count does not match size.",
                    nameof(source));
            if (blendWidth < 2 || blendWidth * 2 >= size)
                throw new ArgumentOutOfRangeException(nameof(blendWidth));

            var horizontal = (Color32[])source.Clone();
            for (int y = 0; y < size; y++)
            {
                int row = y * size;
                Color32 seam = Average(source[row], source[row + size - 1]);
                for (int distance = 0; distance < blendWidth; distance++)
                {
                    float normalized = distance / (float)(blendWidth - 1);
                    float weight = 1f - Mathf.SmoothStep(0f, 1f, normalized);
                    int left = row + distance;
                    int right = row + size - 1 - distance;
                    horizontal[left] = BlendColor(source[left], seam, weight);
                    horizontal[right] = BlendColor(source[right], seam, weight);
                }
            }

            var output = (Color32[])horizontal.Clone();
            for (int x = 0; x < size; x++)
            {
                Color32 seam = Average(
                    horizontal[x],
                    horizontal[(size - 1) * size + x]);
                for (int distance = 0; distance < blendWidth; distance++)
                {
                    float normalized = distance / (float)(blendWidth - 1);
                    float weight = 1f - Mathf.SmoothStep(0f, 1f, normalized);
                    int bottom = distance * size + x;
                    int top = (size - 1 - distance) * size + x;
                    output[bottom] = BlendColor(
                        horizontal[bottom],
                        seam,
                        weight);
                    output[top] = BlendColor(horizontal[top], seam, weight);
                }
            }
            return output;
        }

        private static Color32 BlendColor(
            Color32 source,
            Color32 target,
            float weight)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(source.r, target.r, weight)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(source.g, target.g, weight)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(source.b, target.b, weight)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(source.a, target.a, weight)));
        }

        private static float[] PeriodicBoxBlur(
            float[] source,
            int width,
            int height,
            int radius)
        {
            int diameter = radius * 2 + 1;
            var horizontal = new float[source.Length];
            var output = new float[source.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                double sum = 0d;
                for (int offset = -radius; offset <= radius; offset++)
                    sum += source[row + PositiveModulo(offset, width)];
                for (int x = 0; x < width; x++)
                {
                    horizontal[row + x] = (float)(sum / diameter);
                    sum -= source[row + PositiveModulo(x - radius, width)];
                    sum += source[row + PositiveModulo(
                        x + radius + 1,
                        width)];
                }
            }
            for (int x = 0; x < width; x++)
            {
                double sum = 0d;
                for (int offset = -radius; offset <= radius; offset++)
                {
                    sum += horizontal[
                        PositiveModulo(offset, height) * width + x];
                }
                for (int y = 0; y < height; y++)
                {
                    output[y * width + x] = (float)(sum / diameter);
                    sum -= horizontal[
                        PositiveModulo(y - radius, height) * width + x];
                    sum += horizontal[
                        PositiveModulo(y + radius + 1, height) * width + x];
                }
            }
            return output;
        }

        private static ushort[] BuildCartographicHeight(
            float[] broad,
            FirstArtTerrainLayer3D layer)
        {
            double mean = 0d;
            for (int index = 0; index < broad.Length; index++)
                mean += broad[index];
            mean /= broad.Length;
            double variance = 0d;
            for (int index = 0; index < broad.Length; index++)
            {
                double difference = broad[index] - mean;
                variance += difference * difference;
            }
            float deviation = (float)Math.Sqrt(variance / broad.Length);
            deviation = Mathf.Max(deviation, .02f);
            float amplitude = layer == FirstArtTerrainLayer3D.DeepWater
                ? .035f
                : .06f;
            var height = new ushort[broad.Length];
            for (int index = 0; index < height.Length; index++)
            {
                float value = Mathf.Clamp(
                    .5f + (broad[index] - (float)mean) /
                    deviation * amplitude,
                    .36f,
                    .64f);
                height[index] = (ushort)Mathf.RoundToInt(value * 65535f);
            }
            AverageOppositeEdges(height, SourceTextureSize);
            return height;
        }

        private static Color32[] BuildCartographicNormal(
            ushort[] height,
            FirstArtTerrainLayer3D layer)
        {
            float strength = layer == FirstArtTerrainLayer3D.DeepWater
                ? 8f
                : 12f;
            var normal = new Color32[height.Length];
            for (int y = 0; y < SourceTextureSize; y++)
            for (int x = 0; x < SourceTextureSize; x++)
            {
                float left = HeightAt(height, x - 1, y);
                float right = HeightAt(height, x + 1, y);
                float down = HeightAt(height, x, y - 1);
                float up = HeightAt(height, x, y + 1);
                Vector3 value = new Vector3(
                    -(right - left) * strength,
                    -(up - down) * strength,
                    1f).normalized;
                normal[y * SourceTextureSize + x] = new Color32(
                    QuantizeUnit(value.x * .5f + .5f),
                    QuantizeUnit(value.y * .5f + .5f),
                    QuantizeUnit(value.z * .5f + .5f),
                    255);
            }
            AverageOppositeEdges(normal, SourceTextureSize);
            return normal;
        }

        private static Color32[] BuildCartographicMask(
            float[] luminance,
            float[] local,
            ushort[] height,
            FirstArtTerrainLayer3D layer)
        {
            double detailMean = 0d;
            for (int index = 0; index < luminance.Length; index++)
                detailMean += Math.Abs(luminance[index] - local[index]);
            detailMean = Math.Max(detailMean / luminance.Length, .002d);
            var mask = new Color32[luminance.Length];
            for (int index = 0; index < mask.Length; index++)
            {
                float detail = Mathf.Clamp(
                    .32f + (float)(
                        Math.Abs(luminance[index] - local[index]) /
                        detailMean - 1d) * .16f,
                    .12f,
                    .82f);
                float normalizedHeight = height[index] / 65535f;
                float ao = Mathf.Clamp(
                    .9f + (normalizedHeight - .5f) * .55f,
                    .78f,
                    1f);
                float metallic = 0f;
                if (layer == FirstArtTerrainLayer3D.Ruins)
                {
                    metallic = Mathf.Clamp01(Mathf.Max(
                        0f,
                        luminance[index] - local[index]) * 5f);
                }
                float smoothness = SmoothnessFor(
                    layer,
                    detail,
                    luminance[index]);
                mask[index] = new Color32(
                    QuantizeUnit(metallic),
                    QuantizeUnit(ao),
                    QuantizeUnit(detail),
                    QuantizeUnit(smoothness));
            }
            AverageOppositeEdges(mask, SourceTextureSize);
            return mask;
        }

        private static float SmoothnessFor(
            FirstArtTerrainLayer3D layer,
            float detail,
            float luminance)
        {
            float inverseDetail = 1f - detail;
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Wetland:
                    return Mathf.Lerp(.24f, .56f, inverseDetail);
                case FirstArtTerrainLayer3D.Crystal:
                    return Mathf.Lerp(.22f, .62f, inverseDetail);
                case FirstArtTerrainLayer3D.Ruins:
                    return Mathf.Lerp(.12f, .3f, inverseDetail);
                case FirstArtTerrainLayer3D.DeepWater:
                    return Mathf.Lerp(.78f, .92f,
                        Mathf.Clamp01(inverseDetail * .75f + luminance * .25f));
                case FirstArtTerrainLayer3D.Rocky:
                case FirstArtTerrainLayer3D.Cliff:
                    return Mathf.Lerp(.07f, .17f, inverseDetail);
                default:
                    return Mathf.Lerp(.08f, .2f, inverseDetail);
            }
        }

        private static byte[] EncodeHeightPng(ushort[] pixels)
        {
            var texture = new Texture2D(
                SourceTextureSize,
                SourceTextureSize,
                TextureFormat.R16,
                false,
                true);
            try
            {
                texture.SetPixelData(pixels, 0);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static byte[] EncodeColorPng(
            Color32[] pixels,
            TextureFormat format)
        {
            var texture = new Texture2D(
                SourceTextureSize,
                SourceTextureSize,
                format,
                false,
                true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static float HeightAt(ushort[] height, int x, int y)
        {
            int wrappedX = PositiveModulo(x, SourceTextureSize);
            int wrappedY = PositiveModulo(y, SourceTextureSize);
            return height[wrappedY * SourceTextureSize + wrappedX] /
                65535f;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int remainder = value % modulus;
            return remainder < 0 ? remainder + modulus : remainder;
        }

        private static byte QuantizeUnit(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static void AverageOppositeEdges(
            ushort[] values,
            int size)
        {
            for (int coordinate = 0; coordinate < size; coordinate++)
            {
                int horizontalA = coordinate * size;
                int horizontalB = horizontalA + size - 1;
                ushort horizontal = (ushort)(
                    (values[horizontalA] + values[horizontalB] + 1) / 2);
                values[horizontalA] = horizontal;
                values[horizontalB] = horizontal;

                int verticalA = coordinate;
                int verticalB = (size - 1) * size + coordinate;
                ushort vertical = (ushort)(
                    (values[verticalA] + values[verticalB] + 1) / 2);
                values[verticalA] = vertical;
                values[verticalB] = vertical;
            }
        }

        private static void AverageOppositeEdges(
            Color32[] values,
            int size)
        {
            for (int coordinate = 0; coordinate < size; coordinate++)
            {
                int horizontalA = coordinate * size;
                int horizontalB = horizontalA + size - 1;
                Color32 horizontal = Average(
                    values[horizontalA],
                    values[horizontalB]);
                values[horizontalA] = horizontal;
                values[horizontalB] = horizontal;

                int verticalA = coordinate;
                int verticalB = (size - 1) * size + coordinate;
                Color32 vertical = Average(
                    values[verticalA],
                    values[verticalB]);
                values[verticalA] = vertical;
                values[verticalB] = vertical;
            }
        }

        private static Color32 Average(Color32 left, Color32 right)
        {
            return new Color32(
                (byte)((left.r + right.r + 1) / 2),
                (byte)((left.g + right.g + 1) / 2),
                (byte)((left.b + right.b + 1) / 2),
                (byte)((left.a + right.a + 1) / 2));
        }

        private static void ReimportGeneratedSources(
            IEnumerable<string> paths)
        {
            foreach (string path in paths)
            {
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void ValidatePreservedSourceIdentity(
            IReadOnlyDictionary<string, byte[]> originalMetaFiles,
            IReadOnlyDictionary<string, string> originalGuids)
        {
            foreach (KeyValuePair<string, byte[]> pair in originalMetaFiles)
            {
                string path = pair.Key;
                if (!string.Equals(
                        AssetDatabase.AssetPathToGUID(path),
                        originalGuids[path],
                        StringComparison.Ordinal) ||
                    !ByteArraysEqual(
                        File.ReadAllBytes(AbsoluteProjectPath(path) + ".meta"),
                        pair.Value))
                {
                    throw new InvalidOperationException(
                        "IDEA-0018 source GUID/meta changed: " + path);
                }
            }
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
                if (left[index] != right[index]) return false;
            return true;
        }

        [MenuItem("WasteCity/Art/Build First Terrain Texture Arrays")]
        public static void BuildTextureArrays()
        {
            SourceAsset[,] sources = ResolveAndValidateSources();
            var temporaryArrays = new List<Texture2DArray>(4);
            ArrayDestinationTransaction destinationTransaction = null;

            try
            {
                Texture2DArray baseColor = CreateCpuPopulatedSourceArray(
                    sources,
                    SourceChannel.BaseColor,
                    false,
                    "TA_Terrain_BaseColor");
                temporaryArrays.Add(baseColor);

                Texture2DArray normal = CreateCpuPopulatedSourceArray(
                    sources,
                    SourceChannel.Normal,
                    true,
                    "TA_Terrain_Normal");
                temporaryArrays.Add(normal);

                Texture2DArray mask = CreateCompressedMaskArray(sources);
                temporaryArrays.Add(mask);

                Texture2DArray height = CreateHeightArray(sources);
                temporaryArrays.Add(height);

                ValidateRestoredSources(sources);
                EnsureOutputFolders();
                destinationTransaction = new ArrayDestinationTransaction(
                    BaseColorArrayPath,
                    NormalArrayPath,
                    MaskArrayPath,
                    HeightArrayPath);
                try
                {
                    PersistDestination(baseColor, BaseColorArrayPath, 1);
                    PersistDestination(normal, NormalArrayPath, 2);
                    PersistDestination(mask, MaskArrayPath, 3);
                    PersistDestination(height, HeightArrayPath, 4);
                    AssetDatabase.SaveAssets();

                    ReimportOutput(BaseColorArrayPath);
                    ReimportOutput(NormalArrayPath);
                    ReimportOutput(MaskArrayPath);
                    ReimportOutput(HeightArrayPath);
                    ValidatePersistentArrays(sources);
                    destinationTransaction.Complete();
                }
                catch (Exception operationFailure)
                {
                    List<Exception> rollbackFailures = destinationTransaction.Rollback();
                    if (rollbackFailures.Count == 0)
                    {
                        ExceptionDispatchInfo.Capture(operationFailure).Throw();
                        throw;
                    }

                    var failures = new List<Exception>(rollbackFailures.Count + 1)
                    {
                        operationFailure,
                    };
                    failures.AddRange(rollbackFailures);
                    throw new AggregateException(
                        "Terrain array persistence and rollback both failed.",
                        failures);
                }
            }
            finally
            {
                try
                {
                    destinationTransaction?.Dispose();
                }
                finally
                {
                    DestinationPersistCheckpoint = null;
                    DestinationRollbackCheckpoint = null;
                    foreach (Texture2DArray array in temporaryArrays)
                    {
                        if (array != null)
                            UnityEngine.Object.DestroyImmediate(array);
                    }
                }
            }
        }

        private static void PersistDestination(
            Texture2DArray temporary,
            string path,
            int destinationIndex)
        {
            PersistArray(temporary, path);
            AssetDatabase.SaveAssets();
            DestinationPersistCheckpoint?.Invoke(destinationIndex, path);
        }

        internal static byte QuantizeHeightBlock(ushort a, ushort b, ushort c, ushort d)
        {
            uint average = ((uint)a + b + c + d + 2u) / 4u;
            return (byte)((average + 128u) / 257u);
        }

        private static SourceAsset[,] ResolveAndValidateSources()
        {
            int layerCount = FirstArtTerrainCatalog3D.LayerCount;
            int channelCount = Enum.GetValues(typeof(SourceChannel)).Length;
            var sources = new SourceAsset[layerCount, channelCount];

            for (int layer = 0; layer < layerCount; layer++)
            {
                string terrainName = TerrainName((FirstArtTerrainLayer3D)layer);
                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
                {
                    var channel = (SourceChannel)channelIndex;
                    string path = SourcePath(terrainName, channel);
                    string absolutePath = AbsoluteProjectPath(path);
                    if (!File.Exists(absolutePath))
                        throw new FileNotFoundException($"Required terrain source is missing: {path}", path);

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                        throw new InvalidOperationException($"Terrain source has no TextureImporter: {path}");
                    if (importer.isReadable)
                    {
                        throw new InvalidOperationException(
                            $"Terrain source must start non-readable before array generation: {path}");
                    }

                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (texture == null)
                        throw new InvalidOperationException($"Terrain source did not import as Texture2D: {path}");

                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException($"Terrain source has no stable GUID: {path}");

                    sources[layer, channelIndex] = new SourceAsset(
                        path,
                        guid,
                        AssetDatabase.GetAssetDependencyHash(path),
                        importer.isReadable,
                        texture);
                }
            }

            ValidateSharedChannel(sources, SourceChannel.BaseColor, SourceTextureSize, null);
            ValidateSharedChannel(sources, SourceChannel.Normal, SourceTextureSize, null);
            ValidateSharedChannel(sources, SourceChannel.Mask, SourceTextureSize, null);
            ValidateSharedChannel(sources, SourceChannel.Height, SourceTextureSize, null);
            return sources;
        }

        private static void ValidateSharedChannel(
            SourceAsset[,] sources,
            SourceChannel channel,
            int expectedSize,
            TextureFormat? requiredFormat)
        {
            Texture2D first = sources[0, (int)channel].Texture;
            if (first.width != expectedSize || first.height != expectedSize)
            {
                throw new InvalidOperationException(
                    $"{channel} sources must be {expectedSize}x{expectedSize}; " +
                    $"'{sources[0, (int)channel].Path}' is {first.width}x{first.height}.");
            }

            if (requiredFormat.HasValue && first.format != requiredFormat.Value)
            {
                throw new InvalidOperationException(
                    $"{channel} sources must import as {requiredFormat.Value}; " +
                    $"'{sources[0, (int)channel].Path}' is {first.format}.");
            }

            for (int layer = 1; layer < FirstArtTerrainCatalog3D.LayerCount; layer++)
            {
                SourceAsset source = sources[layer, (int)channel];
                Texture2D texture = source.Texture;
                if (texture.width != first.width ||
                    texture.height != first.height ||
                    texture.format != first.format ||
                    texture.mipmapCount != first.mipmapCount)
                {
                    throw new InvalidOperationException(
                        $"{channel} source '{source.Path}' does not match the first source's " +
                        "width, height, format, and mip count.");
                }
            }
        }

        private static Texture2DArray CreateCpuPopulatedSourceArray(
            SourceAsset[,] sources,
            SourceChannel channel,
            bool linear,
            string name)
        {
            SourceAsset first = sources[0, (int)channel];
            var array = new Texture2DArray(
                first.Width,
                first.Height,
                FirstArtTerrainCatalog3D.LayerCount,
                first.Format,
                first.MipmapCount > 1,
                linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = first.FilterMode,
                anisoLevel = first.AnisoLevel,
            };

            try
            {
                for (int layer = 0; layer < FirstArtTerrainCatalog3D.LayerCount; layer++)
                {
                    SourceAsset sourceAsset = sources[layer, (int)channel];
                    using (FirstArtPassImportPolicy.AllowTemporaryReadability(sourceAsset.Path))
                    {
                        ReimportSource(sourceAsset.Path);
                        Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAsset.Path);
                        if (source == null || !source.isReadable)
                        {
                            throw new InvalidOperationException(
                                $"{channel} source did not become readable: {sourceAsset.Path}");
                        }
                        if (source.width != first.Width ||
                            source.height != first.Height ||
                            source.format != first.Format ||
                            source.mipmapCount != first.MipmapCount)
                        {
                            throw new InvalidOperationException(
                                $"{channel} source import contract changed while staging: {sourceAsset.Path}");
                        }

                        for (int mip = 0; mip < source.mipmapCount; mip++)
                        {
                            array.SetPixelData(
                                source.GetPixelData<byte>(mip),
                                mip,
                                layer);
                        }
                    }
                }

                array.Apply(false, true);
                return array;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(array);
                throw;
            }
        }

        private static Texture2DArray CreateCompressedMaskArray(SourceAsset[,] sources)
        {
            var array = new Texture2DArray(
                SourceTextureSize,
                SourceTextureSize,
                FirstArtTerrainCatalog3D.LayerCount,
                TextureFormat.BC7,
                true,
                true)
            {
                name = "TA_Terrain_Mask",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };

            try
            {
                for (int layer = 0; layer < FirstArtTerrainCatalog3D.LayerCount; layer++)
                {
                    SourceAsset source = sources[layer, (int)SourceChannel.Mask];
                    Texture2D staging = CreateCompressedMaskSlice(source);
                    try
                    {
                        MaskCompressionCheckpoint?.Invoke(source.Path);
                        for (int mip = 0; mip < staging.mipmapCount; mip++)
                        {
                            array.SetPixelData(
                                staging.GetPixelData<byte>(mip),
                                mip,
                                layer);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(staging);
                    }
                }

                array.Apply(false, true);
                return array;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(array);
                throw;
            }
        }

        private static Texture2D CreateCompressedMaskSlice(SourceAsset sourceAsset)
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAsset.Path);
            if (source == null || source.width != SourceTextureSize || source.height != SourceTextureSize)
                throw new InvalidOperationException($"Mask source failed staging validation: {sourceAsset.Path}");

            RenderTexture renderTexture = RenderTexture.GetTemporary(
                SourceTextureSize,
                SourceTextureSize,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var staging = new Texture2D(
                SourceTextureSize,
                SourceTextureSize,
                TextureFormat.RGBA32,
                true,
                true)
            {
                name = $"{Path.GetFileNameWithoutExtension(sourceAsset.Path)}_BC7Staging",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                staging.ReadPixels(
                    new Rect(0f, 0f, SourceTextureSize, SourceTextureSize),
                    0,
                    0,
                    false);
                staging.Apply(true, false);
                EditorUtility.CompressTexture(
                    staging,
                    TextureFormat.BC7,
                    TextureCompressionQuality.Best);
                if (staging.format != TextureFormat.BC7 ||
                    staging.mipmapCount != MipCount(SourceTextureSize) ||
                    !staging.isReadable)
                {
                    throw new InvalidOperationException(
                        $"Mask source did not produce a readable BC7 mip chain: {sourceAsset.Path}");
                }

                return staging;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(staging);
                throw;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        private static Texture2DArray CreateHeightArray(SourceAsset[,] sources)
        {
            var array = new Texture2DArray(
                HeightTextureSize,
                HeightTextureSize,
                FirstArtTerrainCatalog3D.LayerCount,
                TextureFormat.R8,
                true,
                true)
            {
                name = "TA_Terrain_Height",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 4,
            };

            try
            {
                for (int layer = 0; layer < FirstArtTerrainCatalog3D.LayerCount; layer++)
                {
                    SourceAsset source = sources[layer, (int)SourceChannel.Height];
                    Texture2D heightSlice = CreateHeightSlice(source.Path);
                    try
                    {
                        for (int mip = 0; mip < heightSlice.mipmapCount; mip++)
                        {
                            array.SetPixelData(
                                heightSlice.GetPixelData<byte>(mip),
                                mip,
                                layer);
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(heightSlice);
                    }
                }

                array.Apply(false, true);
                return array;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(array);
                throw;
            }
        }

        private static Texture2D CreateHeightSlice(string path)
        {
            IDisposable readabilityScope = null;
            Texture2D slice = null;
            Exception operationFailure = null;
            try
            {
                readabilityScope = FirstArtPassImportPolicy.AllowTemporaryReadability(path);
                ReimportSource(path);

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (importer == null || source == null || !importer.isReadable || !source.isReadable)
                    throw new InvalidOperationException($"Height source did not become readable: {path}");
                if (source.format != TextureFormat.R16)
                {
                    throw new InvalidOperationException(
                        $"Height source '{path}' must import as R16, not {source.format}.");
                }

                HeightSourceReadableCheckpoint?.Invoke(path);
                NativeArray<ushort> pixels = source.GetPixelData<ushort>(0);
                if (pixels.Length != SourceTextureSize * SourceTextureSize)
                    throw new InvalidOperationException($"Height source has unexpected pixel data length: {path}");

                var output = new byte[HeightTextureSize * HeightTextureSize];
                for (int y = 0; y < HeightTextureSize; y++)
                {
                    int sourceRow = y * 2 * SourceTextureSize;
                    int nextSourceRow = sourceRow + SourceTextureSize;
                    int outputRow = y * HeightTextureSize;
                    for (int x = 0; x < HeightTextureSize; x++)
                    {
                        int sourceIndex = sourceRow + x * 2;
                        output[outputRow + x] = QuantizeHeightBlock(
                            pixels[sourceIndex],
                            pixels[sourceIndex + 1],
                            pixels[nextSourceRow + x * 2],
                            pixels[nextSourceRow + x * 2 + 1]);
                    }
                }

                slice = new Texture2D(
                    HeightTextureSize,
                    HeightTextureSize,
                    TextureFormat.R8,
                    true,
                    true)
                {
                    name = $"{Path.GetFileNameWithoutExtension(path)}_Downsampled",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 4,
                };
                slice.SetPixelData(output, 0);
                slice.Apply(true, false);
            }
            catch (Exception exception)
            {
                operationFailure = exception;
            }

            List<Exception> cleanupFailures = RestoreHeightSource(path, readabilityScope);
            if (operationFailure != null || cleanupFailures.Count > 0)
            {
                if (slice != null)
                {
                    UnityEngine.Object.DestroyImmediate(slice);
                    slice = null;
                }

                ThrowOperationAndCleanupFailures(operationFailure, cleanupFailures, path);
            }

            return slice;
        }

        private static List<Exception> RestoreHeightSource(
            string path,
            IDisposable readabilityScope)
        {
            var failures = new List<Exception>(3);
            try
            {
                try
                {
                    readabilityScope?.Dispose();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
                finally
                {
                    try
                    {
                        ReimportSource(path);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }
                    finally
                    {
                        try
                        {
                            var restoredImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                            if (restoredImporter == null || restoredImporter.isReadable)
                            {
                                throw new InvalidOperationException(
                                    $"Height source readability was not restored: {path}");
                            }
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }

            return failures;
        }

        private static void ThrowOperationAndCleanupFailures(
            Exception operationFailure,
            List<Exception> cleanupFailures,
            string path)
        {
            if (operationFailure == null && cleanupFailures.Count == 0)
                return;

            if (operationFailure != null && cleanupFailures.Count == 0)
            {
                ExceptionDispatchInfo.Capture(operationFailure).Throw();
                return;
            }

            if (operationFailure == null && cleanupFailures.Count == 1)
            {
                ExceptionDispatchInfo.Capture(cleanupFailures[0]).Throw();
                return;
            }

            var failures = new List<Exception>(cleanupFailures.Count + 1);
            if (operationFailure != null)
                failures.Add(operationFailure);
            failures.AddRange(cleanupFailures);
            throw new AggregateException(
                $"Height array generation and cleanup failed for '{path}'.",
                failures);
        }

        private static void ValidateRestoredSources(SourceAsset[,] sources)
        {
            for (int layer = 0; layer < FirstArtTerrainCatalog3D.LayerCount; layer++)
            {
                for (int channel = 0; channel < Enum.GetValues(typeof(SourceChannel)).Length; channel++)
                {
                    SourceAsset captured = sources[layer, channel];
                    var importer = AssetImporter.GetAtPath(captured.Path) as TextureImporter;
                    string guid = AssetDatabase.AssetPathToGUID(captured.Path);
                    Hash128 dependencyHash = AssetDatabase.GetAssetDependencyHash(captured.Path);
                    if (importer == null ||
                        importer.isReadable != captured.WasReadable ||
                        !string.Equals(guid, captured.Guid, StringComparison.Ordinal) ||
                        dependencyHash != captured.DependencyHash)
                    {
                        throw new InvalidOperationException(
                            $"Terrain source identity or importer state changed while building arrays: {captured.Path}");
                    }
                }
            }
        }

        private static void EnsureOutputFolders()
        {
            if (!AssetDatabase.IsValidFolder(RuntimeFolder))
                AssetDatabase.CreateFolder(TerrainRoot, "Runtime");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder))
                AssetDatabase.CreateFolder(RuntimeFolder, "Generated");
        }

        private static void EnsureRuntimeAssetFolders()
        {
            if (!AssetDatabase.IsValidFolder(RuntimeFolder))
                AssetDatabase.CreateFolder(TerrainRoot, "Runtime");
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder(RuntimeFolder, "Materials");
            if (!AssetDatabase.IsValidFolder(ProfilesFolder))
                AssetDatabase.CreateFolder(RuntimeFolder, "Profiles");
            if (!AssetDatabase.IsValidFolder(ShadersFolder))
                AssetDatabase.CreateFolder(RuntimeFolder, "Shaders");
        }

        private static Texture2DArray LoadRequiredArray(string assetPath)
        {
            Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(assetPath);
            if (array == null)
                throw new InvalidOperationException($"Required terrain Texture2DArray is missing: {assetPath}");
            return array;
        }

        private static Material LoadOrCreateMaterial(Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                material.shader = shader;
                return material;
            }

            var created = new Material(shader)
            {
                name = "MAT_Terrain_FirstPass",
            };
            try
            {
                AssetDatabase.CreateAsset(created, MaterialPath);
                return created;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(created);
                throw;
            }
        }

        private static void ApplyCartographicVisualStyle(Material material)
        {
            material.SetFloat("_MacroVariation", .08f);
            material.SetColor("_WastelandTint", TerrainTint(FirstArtTerrainLayer3D.Wasteland));
            material.SetColor("_RockyTint", TerrainTint(FirstArtTerrainLayer3D.Rocky));
            material.SetColor("_WetlandTint", TerrainTint(FirstArtTerrainLayer3D.Wetland));
            material.SetColor("_CrystalTint", TerrainTint(FirstArtTerrainLayer3D.Crystal));
            material.SetColor("_RuinsTint", TerrainTint(FirstArtTerrainLayer3D.Ruins));
            material.SetColor("_DeepWaterTint", TerrainTint(FirstArtTerrainLayer3D.DeepWater));
            material.SetColor("_CliffTint", TerrainTint(FirstArtTerrainLayer3D.Cliff));
        }

        private static Color TerrainTint(FirstArtTerrainLayer3D layer)
        {
            return FirstArtTerrainVisualStyleCatalog3D.MaterialTintOf(layer);
        }

        private static FirstArtTerrainProfile3D LoadOrCreateProfile()
        {
            FirstArtTerrainProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(ProfilePath);
            if (profile != null)
                return profile;

            FirstArtTerrainProfile3D created =
                ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();
            created.name = "FirstArtTerrainProfile3D";
            try
            {
                AssetDatabase.CreateAsset(created, ProfilePath);
                return created;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(created);
                throw;
            }
        }

        private static void PersistArray(Texture2DArray temporary, string path)
        {
            Texture2DArray existing = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(UnityEngine.Object.Instantiate(temporary), path);
                return;
            }

            EditorUtility.CopySerialized(temporary, existing);
            EditorUtility.SetDirty(existing);
        }

        private static void ValidatePersistentArrays(SourceAsset[,] sources)
        {
            ValidatePersistentArray(
                BaseColorArrayPath,
                SourceTextureSize,
                sources[0, (int)SourceChannel.BaseColor].Format,
                sources[0, (int)SourceChannel.BaseColor].MipmapCount);
            ValidatePersistentArray(
                NormalArrayPath,
                SourceTextureSize,
                sources[0, (int)SourceChannel.Normal].Format,
                sources[0, (int)SourceChannel.Normal].MipmapCount);
            ValidatePersistentArray(
                MaskArrayPath,
                SourceTextureSize,
                TextureFormat.BC7,
                MipCount(SourceTextureSize));
            ValidatePersistentArray(
                HeightArrayPath,
                HeightTextureSize,
                TextureFormat.R8,
                MipCount(HeightTextureSize));
        }

        private static void ValidatePersistentArray(
            string path,
            int expectedSize,
            TextureFormat expectedFormat,
            int expectedMipCount)
        {
            Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            if (array == null ||
                array.width != expectedSize ||
                array.height != expectedSize ||
                array.depth != FirstArtTerrainCatalog3D.LayerCount ||
                array.format != expectedFormat ||
                array.mipmapCount != expectedMipCount ||
                array.wrapMode != TextureWrapMode.Repeat ||
                array.isReadable)
            {
                throw new InvalidOperationException($"Generated terrain array failed validation: {path}");
            }
        }

        private static int MipCount(int size)
        {
            int count = 1;
            while (size > 1)
            {
                size >>= 1;
                count++;
            }

            return count;
        }

        private static void ReimportSource(string path)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ReimportOutput(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static string AbsoluteProjectPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string SourcePath(string terrainName, SourceChannel channel)
        {
            return $"{TerrainRoot}/{terrainName}/T_Terrain_{terrainName}_{channel}.png";
        }

        private static string ConceptPath(string terrainName)
        {
            return $"{CartographicConceptRoot}/{terrainName}/References/" +
                $"{terrainName}_IDEA0018_Cartographic_Concept_v001.png";
        }

        private static string TerrainName(FirstArtTerrainLayer3D layer)
        {
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Wasteland:
                    return "Wasteland";
                case FirstArtTerrainLayer3D.Rocky:
                    return "Rocky";
                case FirstArtTerrainLayer3D.Wetland:
                    return "Wetland";
                case FirstArtTerrainLayer3D.Crystal:
                    return "Crystal";
                case FirstArtTerrainLayer3D.Ruins:
                    return "Ruins";
                case FirstArtTerrainLayer3D.DeepWater:
                    return "DeepWater";
                case FirstArtTerrainLayer3D.Cliff:
                    return "Cliff";
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown terrain layer.");
            }
        }

        private enum SourceChannel
        {
            BaseColor = 0,
            Normal = 1,
            Mask = 2,
            Height = 3,
        }

        private readonly struct ResampleCoordinate
        {
            public ResampleCoordinate(int lower, int upper, int fraction)
            {
                Lower = lower;
                Upper = upper;
                Fraction = fraction;
            }

            public int Lower { get; }
            public int Upper { get; }
            public int Fraction { get; }
        }

        private sealed class ArrayDestinationTransaction : IDisposable
        {
            private readonly string backupRoot;
            private readonly DestinationBackup[] backups;
            private bool completed;
            private bool cleaned;

            public ArrayDestinationTransaction(params string[] paths)
            {
                backupRoot = Path.Combine(
                    Path.GetTempPath(),
                    "wastecity-first-terrain-destination-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backupRoot);
                backups = new DestinationBackup[paths.Length];
                try
                {
                    for (int index = 0; index < paths.Length; index++)
                    {
                        string path = paths[index];
                        string absolutePath = AbsoluteProjectPath(path);
                        string absoluteMetaPath = absolutePath + ".meta";
                        bool existed = File.Exists(absolutePath);
                        string assetBackupPath = Path.Combine(backupRoot, index + ".asset");
                        string metaBackupPath = Path.Combine(backupRoot, index + ".meta");
                        if (existed)
                        {
                            File.Copy(absolutePath, assetBackupPath, true);
                            File.Copy(absoluteMetaPath, metaBackupPath, true);
                        }

                        backups[index] = new DestinationBackup(
                            path,
                            absolutePath,
                            absoluteMetaPath,
                            existed,
                            existed ? AssetDatabase.AssetPathToGUID(path) : string.Empty,
                            assetBackupPath,
                            metaBackupPath);
                    }
                }
                catch
                {
                    CleanupBackupDirectory();
                    throw;
                }
            }

            public void Complete()
            {
                completed = true;
            }

            public List<Exception> Rollback()
            {
                var failures = new List<Exception>();
                try
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        foreach (DestinationBackup backup in backups)
                        {
                            try
                            {
                                DestinationRollbackCheckpoint?.Invoke(backup.Path);
                            }
                            catch (Exception exception)
                            {
                                failures.Add(exception);
                            }

                            try
                            {
                                if (backup.Existed)
                                {
                                    File.Copy(
                                        backup.AssetBackupPath,
                                        backup.AbsolutePath,
                                        true);
                                    File.Copy(
                                        backup.MetaBackupPath,
                                        backup.AbsoluteMetaPath,
                                        true);
                                }
                                else
                                {
                                    if (File.Exists(backup.AbsolutePath))
                                        File.Delete(backup.AbsolutePath);
                                    if (File.Exists(backup.AbsoluteMetaPath))
                                        File.Delete(backup.AbsoluteMetaPath);
                                }
                            }
                            catch (Exception exception)
                            {
                                failures.Add(exception);
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            AssetDatabase.StopAssetEditing();
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }

                    try
                    {
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                    catch (Exception exception)
                    {
                        failures.Add(exception);
                    }

                    foreach (DestinationBackup backup in backups)
                    {
                        try
                        {
                            if (backup.Existed)
                            {
                                AssetDatabase.ImportAsset(
                                    backup.Path,
                                    ImportAssetOptions.ForceUpdate |
                                    ImportAssetOptions.ForceSynchronousImport);
                                string restoredGuid = AssetDatabase.AssetPathToGUID(backup.Path);
                                if (!string.Equals(
                                        restoredGuid,
                                        backup.Guid,
                                        StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException(
                                        $"Terrain destination GUID rollback failed: {backup.Path}");
                                }
                            }
                            else if (File.Exists(backup.AbsolutePath) ||
                                     File.Exists(backup.AbsoluteMetaPath) ||
                                     AssetDatabase.LoadMainAssetAtPath(backup.Path) != null)
                            {
                                throw new InvalidOperationException(
                                    $"New terrain destination survived rollback: {backup.Path}");
                            }
                        }
                        catch (Exception exception)
                        {
                            failures.Add(exception);
                        }
                    }
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }

                try
                {
                    CleanupBackupDirectory();
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
                return failures;
            }

            public void Dispose()
            {
                if (!completed && !cleaned)
                    return;
                CleanupBackupDirectory();
            }

            private void CleanupBackupDirectory()
            {
                if (cleaned)
                    return;
                if (Directory.Exists(backupRoot))
                    Directory.Delete(backupRoot, true);
                cleaned = true;
            }

            private sealed class DestinationBackup
            {
                public DestinationBackup(
                    string path,
                    string absolutePath,
                    string absoluteMetaPath,
                    bool existed,
                    string guid,
                    string assetBackupPath,
                    string metaBackupPath)
                {
                    Path = path;
                    AbsolutePath = absolutePath;
                    AbsoluteMetaPath = absoluteMetaPath;
                    Existed = existed;
                    Guid = guid;
                    AssetBackupPath = assetBackupPath;
                    MetaBackupPath = metaBackupPath;
                }

                public string Path { get; }
                public string AbsolutePath { get; }
                public string AbsoluteMetaPath { get; }
                public bool Existed { get; }
                public string Guid { get; }
                public string AssetBackupPath { get; }
                public string MetaBackupPath { get; }
            }
        }

        private sealed class CartographicSourceChannels
        {
            public CartographicSourceChannels(
                byte[] baseColorPng,
                byte[] heightPng,
                byte[] normalPng,
                byte[] maskPng)
            {
                BaseColorPng = baseColorPng ??
                    throw new ArgumentNullException(nameof(baseColorPng));
                HeightPng = heightPng ??
                    throw new ArgumentNullException(nameof(heightPng));
                NormalPng = normalPng ??
                    throw new ArgumentNullException(nameof(normalPng));
                MaskPng = maskPng ??
                    throw new ArgumentNullException(nameof(maskPng));
            }

            public byte[] BaseColorPng { get; }
            public byte[] HeightPng { get; }
            public byte[] NormalPng { get; }
            public byte[] MaskPng { get; }
        }

        private sealed class SourceAsset
        {
            public SourceAsset(
                string path,
                string guid,
                Hash128 dependencyHash,
                bool wasReadable,
                Texture2D texture)
            {
                Path = path;
                Guid = guid;
                DependencyHash = dependencyHash;
                WasReadable = wasReadable;
                Texture = texture;
                Width = texture.width;
                Height = texture.height;
                Format = texture.format;
                MipmapCount = texture.mipmapCount;
                FilterMode = texture.filterMode;
                AnisoLevel = texture.anisoLevel;
            }

            public string Path { get; }

            public string Guid { get; }

            public Hash128 DependencyHash { get; }

            public bool WasReadable { get; }

            public Texture2D Texture { get; }

            public int Width { get; }

            public int Height { get; }

            public TextureFormat Format { get; }

            public int MipmapCount { get; }

            public FilterMode FilterMode { get; }

            public int AnisoLevel { get; }
        }
    }
}
