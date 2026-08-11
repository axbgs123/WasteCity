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
        private const string RuntimeFolder = TerrainRoot + "/Runtime";
        private const string GeneratedFolder = RuntimeFolder + "/Generated";
        private const string MaterialsFolder = RuntimeFolder + "/Materials";
        private const string ProfilesFolder = RuntimeFolder + "/Profiles";
        private const string ShadersFolder = RuntimeFolder + "/Shaders";
        private const int SourceTextureSize = 2048;
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
