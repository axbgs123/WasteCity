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

        private const string TerrainRoot =
            "Assets/_Game/Art/FirstPass/Environment/Terrain";
        private const string RuntimeFolder = TerrainRoot + "/Runtime";
        private const string GeneratedFolder = RuntimeFolder + "/Generated";
        private const int SourceTextureSize = 2048;
        private const int HeightTextureSize = 1024;

        internal static Action<string> HeightSourceReadableCheckpoint;

        [MenuItem("WasteCity/Art/Build First Terrain Texture Arrays")]
        public static void BuildTextureArrays()
        {
            SourceAsset[,] sources = ResolveAndValidateSources();
            var temporaryArrays = new List<Texture2DArray>(4);

            try
            {
                Texture2DArray baseColor = CreateCopiedArray(
                    sources,
                    SourceChannel.BaseColor,
                    false,
                    "TA_Terrain_BaseColor");
                temporaryArrays.Add(baseColor);

                Texture2DArray normal = CreateCopiedArray(
                    sources,
                    SourceChannel.Normal,
                    true,
                    "TA_Terrain_Normal");
                temporaryArrays.Add(normal);

                Texture2DArray mask = CreateCopiedArray(
                    sources,
                    SourceChannel.Mask,
                    true,
                    "TA_Terrain_Mask");
                temporaryArrays.Add(mask);

                Texture2DArray height = CreateHeightArray(sources);
                temporaryArrays.Add(height);

                ValidateRestoredSources(sources);
                EnsureOutputFolders();
                PersistArray(baseColor, BaseColorArrayPath);
                PersistArray(normal, NormalArrayPath);
                PersistArray(mask, MaskArrayPath);
                PersistArray(height, HeightArrayPath);
                AssetDatabase.SaveAssets();

                ReimportOutput(BaseColorArrayPath);
                ReimportOutput(NormalArrayPath);
                ReimportOutput(MaskArrayPath);
                ReimportOutput(HeightArrayPath);
                ValidatePersistentArrays(sources);
            }
            finally
            {
                foreach (Texture2DArray array in temporaryArrays)
                {
                    if (array != null)
                        UnityEngine.Object.DestroyImmediate(array);
                }
            }
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

        private static Texture2DArray CreateCopiedArray(
            SourceAsset[,] sources,
            SourceChannel channel,
            bool linear,
            string name)
        {
            Texture2D first = sources[0, (int)channel].Texture;
            var array = new Texture2DArray(
                first.width,
                first.height,
                FirstArtTerrainCatalog3D.LayerCount,
                first.format,
                first.mipmapCount > 1,
                linear)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = first.filterMode,
                anisoLevel = first.anisoLevel,
            };

            try
            {
                for (int layer = 0; layer < FirstArtTerrainCatalog3D.LayerCount; layer++)
                {
                    Texture2D source = sources[layer, (int)channel].Texture;
                    for (int mip = 0; mip < source.mipmapCount; mip++)
                        Graphics.CopyTexture(source, 0, mip, array, layer, mip);
                }

                return array;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(array);
                throw;
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
                            Graphics.CopyTexture(heightSlice, 0, mip, array, layer, mip);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(heightSlice);
                    }
                }

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
                sources[0, (int)SourceChannel.BaseColor].Texture.format,
                sources[0, (int)SourceChannel.BaseColor].Texture.mipmapCount);
            ValidatePersistentArray(
                NormalArrayPath,
                SourceTextureSize,
                sources[0, (int)SourceChannel.Normal].Texture.format,
                sources[0, (int)SourceChannel.Normal].Texture.mipmapCount);
            ValidatePersistentArray(
                MaskArrayPath,
                SourceTextureSize,
                sources[0, (int)SourceChannel.Mask].Texture.format,
                sources[0, (int)SourceChannel.Mask].Texture.mipmapCount);
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
                array.wrapMode != TextureWrapMode.Repeat)
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
            }

            public string Path { get; }

            public string Guid { get; }

            public Hash128 DependencyHash { get; }

            public bool WasReadable { get; }

            public Texture2D Texture { get; }
        }
    }
}
