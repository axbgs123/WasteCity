using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using WasteCity.Editor;

namespace WasteCity.Tests
{
    [Category("TerrainAssetDeep")]
    public sealed class FirstArtTerrainAssetBuilderTests
    {
        private const string TerrainRoot =
            "Assets/_Game/Art/FirstPass/Environment/Terrain";
        private const long CompressedPayloadCeiling = 128L * 1024L * 1024L;
        private const long EditorNativeMemoryCeiling = 256L * 1024L * 1024L;
        private const long EditorDuplicateTolerance = 64L * 1024L;
        private const long ExpectedCompressedPayloadBytes = 127227779L;

        private static readonly string[] TerrainNames =
        {
            "Wasteland",
            "Rocky",
            "Wetland",
            "Crystal",
            "Ruins",
            "DeepWater",
            "Cliff",
        };

        private static readonly string[] Channels =
        {
            "BaseColor",
            "Normal",
            "Mask",
            "Height",
        };

        private static readonly string[] ArrayPaths =
        {
            FirstArtTerrainAssetBuilder.BaseColorArrayPath,
            FirstArtTerrainAssetBuilder.NormalArrayPath,
            FirstArtTerrainAssetBuilder.MaskArrayPath,
            FirstArtTerrainAssetBuilder.HeightArrayPath,
        };

        [Test]
        public void BuildTextureArrays_UsesFrozenLayerOrderAndFormats()
        {
            FirstArtTerrainAssetBuilder.BuildTextureArrays();

            Texture2DArray baseColor = LoadArray(
                FirstArtTerrainAssetBuilder.BaseColorArrayPath);
            Texture2DArray normal = LoadArray(
                FirstArtTerrainAssetBuilder.NormalArrayPath);
            Texture2DArray mask = LoadArray(
                FirstArtTerrainAssetBuilder.MaskArrayPath);
            Texture2DArray height = LoadArray(
                FirstArtTerrainAssetBuilder.HeightArrayPath);

            AssertArrayContract(baseColor, 2048, TexturePath("Wasteland", "BaseColor"), true);
            AssertArrayContract(normal, 2048, TexturePath("Wasteland", "Normal"), false);
            AssertArrayContract(mask, 2048, null, false);
            AssertArrayContract(height, 1024, null, false);
            Assert.That(mask.format, Is.EqualTo(TextureFormat.BC7));
            Assert.That(mask.mipmapCount, Is.EqualTo(12));
            Assert.That(height.format, Is.EqualTo(TextureFormat.R8));
            Assert.That(height.graphicsFormat, Is.EqualTo(GraphicsFormat.R8_UNorm));

            for (int slice = 0; slice < TerrainNames.Length; slice++)
            {
                string sourcePath = TexturePath(TerrainNames[slice], "BaseColor");
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                Assert.That(source, Is.Not.Null, sourcePath);

                Color32 expected = ReadCenterPixel(source);
                Color32 actual = ReadCenterPixel(baseColor, slice);
                AssertColorWithinOneByte(actual, expected, sourcePath);
            }

            AssertSlicePixelsMatch(normal, "Normal", 731, 913, true);
            AssertMaskSamplesWithinCompressionError(mask);

            AssertHeightBlockMatchesSource(height, 0, "Wasteland", 317, 743);

            foreach (string path in SourcePaths())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.isReadable, Is.False, path);
            }
        }

        [Test]
        public void BuildTextureArrays_CompressesMaskWithinPayloadAndEditorMemoryBudgets()
        {
            FirstArtTerrainAssetBuilder.BuildTextureArrays();

            Texture2DArray baseColor = LoadArray(
                FirstArtTerrainAssetBuilder.BaseColorArrayPath);
            Texture2DArray normal = LoadArray(
                FirstArtTerrainAssetBuilder.NormalArrayPath);
            Texture2DArray mask = LoadArray(
                FirstArtTerrainAssetBuilder.MaskArrayPath);
            Texture2DArray height = LoadArray(
                FirstArtTerrainAssetBuilder.HeightArrayPath);
            long baseColorBytes = Profiler.GetRuntimeMemorySizeLong(baseColor);
            long normalBytes = Profiler.GetRuntimeMemorySizeLong(normal);
            long maskBytes = Profiler.GetRuntimeMemorySizeLong(mask);
            long heightBytes = Profiler.GetRuntimeMemorySizeLong(height);
            long totalBytes =
                baseColorBytes + normalBytes + maskBytes + heightBytes;
            long baseColorPayload = CalculateCompressedPayloadBytes(baseColor);
            long normalPayload = CalculateCompressedPayloadBytes(normal);
            long maskPayload = CalculateCompressedPayloadBytes(mask);
            long heightPayload = CalculateCompressedPayloadBytes(height);
            long payloadBytes =
                baseColorPayload + normalPayload + maskPayload + heightPayload;
            long editorDuplicateDifference = Math.Abs(totalBytes - payloadBytes * 2L);
            double editorNativeRatio = totalBytes / (double)payloadBytes;

            TestContext.WriteLine("FirstTerrainBaseColorPayloadBytes=" + baseColorPayload);
            TestContext.WriteLine("FirstTerrainNormalPayloadBytes=" + normalPayload);
            TestContext.WriteLine("FirstTerrainMaskPayloadBytes=" + maskPayload);
            TestContext.WriteLine("FirstTerrainHeightPayloadBytes=" + heightPayload);
            TestContext.WriteLine("FirstTerrainCompressedPayloadBytes=" + payloadBytes);
            TestContext.WriteLine("FirstTerrainBaseColorRuntimeBytes=" + baseColorBytes);
            TestContext.WriteLine("FirstTerrainNormalRuntimeBytes=" + normalBytes);
            TestContext.WriteLine("FirstTerrainMaskRuntimeBytes=" + maskBytes);
            TestContext.WriteLine("FirstTerrainHeightRuntimeBytes=" + heightBytes);
            TestContext.WriteLine("FirstTerrainEditorNativeBytes=" + totalBytes);
            TestContext.WriteLine(
                "FirstTerrainEditorDuplicateDifferenceBytes=" + editorDuplicateDifference);
            TestContext.WriteLine(
                "FirstTerrainEditorNativeToPayloadRatio=" + editorNativeRatio.ToString("F6"));
            Assert.That(payloadBytes, Is.EqualTo(ExpectedCompressedPayloadBytes));
            Assert.That(payloadBytes, Is.LessThanOrEqualTo(CompressedPayloadCeiling));
            Assert.That(totalBytes, Is.LessThanOrEqualTo(EditorNativeMemoryCeiling));
            Assert.That(editorDuplicateDifference, Is.LessThanOrEqualTo(EditorDuplicateTolerance));
            Assert.That(mask.width, Is.EqualTo(2048));
            Assert.That(mask.height, Is.EqualTo(2048));
            Assert.That(mask.depth, Is.EqualTo(7));
            Assert.That(mask.mipmapCount, Is.EqualTo(12));
            Assert.That(mask.format, Is.EqualTo(TextureFormat.BC7));
            Assert.That(
                GraphicsFormatUtility.IsSRGBFormat(mask.graphicsFormat),
                Is.False);
            Assert.That(mask.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            AssertMaskSamplesWithinCompressionError(mask);
        }

        private static long CalculateCompressedPayloadBytes(Texture2DArray array)
        {
            long bytesPerSlice = 0L;
            int width = array.width;
            int height = array.height;
            for (int mip = 0; mip < array.mipmapCount; mip++)
            {
                if (array.format == TextureFormat.BC7)
                {
                    long blocksWide = (width + 3L) / 4L;
                    long blocksHigh = (height + 3L) / 4L;
                    bytesPerSlice += blocksWide * blocksHigh * 16L;
                }
                else if (array.format == TextureFormat.R8)
                {
                    bytesPerSlice += (long)width * height;
                }
                else
                {
                    Assert.Fail(
                        $"Unsupported first-terrain runtime format for payload calculation: {array.format}");
                }

                width = Math.Max(1, width >> 1);
                height = Math.Max(1, height >> 1);
            }

            return bytesPerSlice * array.depth;
        }

        [Test]
        public void BuildTextureArrays_TwoBuildsPreserveSourceAndGeneratedIdentity()
        {
            Dictionary<string, AssetState> sourceBefore = CaptureSourceStates();

            FirstArtTerrainAssetBuilder.BuildTextureArrays();
            Dictionary<string, AssetState> sourceAfterFirst = CaptureSourceStates();
            Dictionary<string, AssetState> arraysAfterFirst = CaptureArrayStates();

            FirstArtTerrainAssetBuilder.BuildTextureArrays();
            Dictionary<string, AssetState> sourceAfterSecond = CaptureSourceStates();
            Dictionary<string, AssetState> arraysAfterSecond = CaptureArrayStates();

            AssertStatesEqual(sourceAfterFirst, sourceBefore, "first source build");
            AssertStatesEqual(sourceAfterSecond, sourceBefore, "second source build");
            AssertStatesEqual(arraysAfterSecond, arraysAfterFirst, "generated array rerun");
        }

        [Test]
        public void BuildTextureArrays_ExistingR8AndBc7SentinelsReturnToStableGoldenContent()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string backupRoot = Path.Combine(
                Path.GetTempPath(),
                "wastecity-first-terrain-array-backup-" + Guid.NewGuid().ToString("N"));
            Dictionary<string, ExternalAssetBackup> backups =
                CreateExternalArrayBackups(backupRoot);
            Dictionary<string, AssetState> sourceBefore = CaptureSourceStates();

            try
            {
                FirstArtTerrainAssetBuilder.BuildTextureArrays();
                Dictionary<string, ArrayRuntimeState> golden = CaptureArrayRuntimeStates();

                OverwriteDestinationWithSentinel(
                    FirstArtTerrainAssetBuilder.HeightArrayPath,
                    CreateHeightSentinel());
                OverwriteDestinationWithSentinel(
                    FirstArtTerrainAssetBuilder.MaskArrayPath,
                    CreateMaskSentinel());

                Dictionary<string, ArrayRuntimeState> sentinel = CaptureArrayRuntimeStates();
                Assert.That(
                    sentinel[FirstArtTerrainAssetBuilder.HeightArrayPath].GpuDigest,
                    Is.Not.EqualTo(golden[FirstArtTerrainAssetBuilder.HeightArrayPath].GpuDigest),
                    "R8 Height sentinel must visibly differ before the rebuild");
                Assert.That(
                    sentinel[FirstArtTerrainAssetBuilder.MaskArrayPath].GpuDigest,
                    Is.Not.EqualTo(golden[FirstArtTerrainAssetBuilder.MaskArrayPath].GpuDigest),
                    "BC7 Mask sentinel must visibly differ before the rebuild");

                FirstArtTerrainAssetBuilder.BuildTextureArrays();
                Dictionary<string, ArrayRuntimeState> recovered = CaptureArrayRuntimeStates();
                AssertArrayRuntimeStatesEqual(recovered, golden, "sentinel recovery");

                FirstArtTerrainAssetBuilder.BuildTextureArrays();
                Dictionary<string, ArrayRuntimeState> repeated = CaptureArrayRuntimeStates();
                AssertArrayRuntimeStatesEqual(repeated, recovered, "consecutive rebuild");
                Assert.That(
                    Math.Abs(SumNativeBytes(repeated) - SumNativeBytes(recovered)),
                    Is.LessThanOrEqualTo(EditorDuplicateTolerance));
                AssertStatesEqual(CaptureSourceStates(), sourceBefore, "sentinel source preservation");
            }
            finally
            {
                RestoreExternalArrayBackups(backups, backupRoot);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void BuildTextureArrays_PersistFailureRestoresAllExistingDestinations(
            int failingDestination)
        {
            string backupRoot = Path.Combine(
                Path.GetTempPath(),
                "wastecity-first-terrain-transaction-existing-" + Guid.NewGuid().ToString("N"));
            Dictionary<string, ExternalAssetBackup> outerBackups =
                CreateExternalArrayBackups(backupRoot);
            try
            {
                FirstArtTerrainAssetBuilder.BuildTextureArrays();
                Dictionary<string, AssetState> before = CaptureArrayStates();
                var failure = new InvalidOperationException(
                    "injected destination persist failure " + failingDestination);

                FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint =
                    (destinationIndex, path) =>
                    {
                        if (destinationIndex != failingDestination)
                            return;
                        AssetDatabase.SaveAssets();
                        throw failure;
                    };
                try
                {
                    InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                        () => FirstArtTerrainAssetBuilder.BuildTextureArrays());
                    Assert.That(thrown, Is.SameAs(failure));
                }
                finally
                {
                    FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint = null;
                }

                AssertStatesEqual(
                    CaptureArrayStates(),
                    before,
                    "destination rollback " + failingDestination);
            }
            finally
            {
                FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint = null;
                FirstArtTerrainAssetBuilder.DestinationRollbackCheckpoint = null;
                RestoreExternalArrayBackups(outerBackups, backupRoot);
            }
        }

        [Test]
        public void BuildTextureArrays_PersistFailureDeletesOriginallyMissingDestination()
        {
            string backupRoot = Path.Combine(
                Path.GetTempPath(),
                "wastecity-first-terrain-transaction-missing-" + Guid.NewGuid().ToString("N"));
            Dictionary<string, ExternalAssetBackup> outerBackups =
                CreateExternalArrayBackups(backupRoot);
            try
            {
                FirstArtTerrainAssetBuilder.BuildTextureArrays();
                Assert.That(
                    AssetDatabase.DeleteAsset(FirstArtTerrainAssetBuilder.HeightArrayPath),
                    Is.True);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Dictionary<string, FileAssetState> before = CaptureFileArrayStatesAllowMissing();
                Assert.That(
                    before[FirstArtTerrainAssetBuilder.HeightArrayPath].Existed,
                    Is.False);
                var failure = new InvalidOperationException("injected fourth destination failure");

                FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint =
                    (destinationIndex, path) =>
                    {
                        if (destinationIndex != 4)
                            return;
                        AssetDatabase.SaveAssets();
                        throw failure;
                    };
                try
                {
                    InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                        () => FirstArtTerrainAssetBuilder.BuildTextureArrays());
                    Assert.That(thrown, Is.SameAs(failure));
                }
                finally
                {
                    FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint = null;
                }

                AssertFileArrayStatesEqual(
                    CaptureFileArrayStatesAllowMissing(),
                    before,
                    "originally missing rollback");
            }
            finally
            {
                FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint = null;
                FirstArtTerrainAssetBuilder.DestinationRollbackCheckpoint = null;
                RestoreExternalArrayBackups(outerBackups, backupRoot);
            }
        }

        [Test]
        public void BuildTextureArrays_RollbackFailurePreservesOperationAndCleanupExceptions()
        {
            string backupRoot = Path.Combine(
                Path.GetTempPath(),
                "wastecity-first-terrain-transaction-aggregate-" + Guid.NewGuid().ToString("N"));
            Dictionary<string, ExternalAssetBackup> outerBackups =
                CreateExternalArrayBackups(backupRoot);
            try
            {
                FirstArtTerrainAssetBuilder.BuildTextureArrays();
                Dictionary<string, AssetState> before = CaptureArrayStates();
                var operationFailure = new InvalidOperationException("injected persist failure");
                var rollbackFailure = new IOException("injected rollback failure");

                FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint =
                    (destinationIndex, path) =>
                    {
                        if (destinationIndex != 1)
                            return;
                        AssetDatabase.SaveAssets();
                        throw operationFailure;
                    };
                FirstArtTerrainAssetBuilder.DestinationRollbackCheckpoint = path =>
                {
                    if (string.Equals(
                            path,
                            FirstArtTerrainAssetBuilder.BaseColorArrayPath,
                            StringComparison.Ordinal))
                    {
                        throw rollbackFailure;
                    }
                };
                try
                {
                    AggregateException thrown = Assert.Throws<AggregateException>(
                        () => FirstArtTerrainAssetBuilder.BuildTextureArrays());
                    CollectionAssert.Contains(thrown.Flatten().InnerExceptions, operationFailure);
                    CollectionAssert.Contains(thrown.Flatten().InnerExceptions, rollbackFailure);
                }
                finally
                {
                    FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint = null;
                    FirstArtTerrainAssetBuilder.DestinationRollbackCheckpoint = null;
                }

                AssertStatesEqual(
                    CaptureArrayStates(),
                    before,
                    "aggregate rollback still restores destinations");
            }
            finally
            {
                FirstArtTerrainAssetBuilder.DestinationPersistCheckpoint = null;
                FirstArtTerrainAssetBuilder.DestinationRollbackCheckpoint = null;
                RestoreExternalArrayBackups(outerBackups, backupRoot);
            }
        }

        [Test]
        public void BuildTextureArrays_MissingSourceLeavesGeneratedAssetsUntouched()
        {
            FirstArtTerrainAssetBuilder.BuildTextureArrays();
            Dictionary<string, AssetState> before = CaptureArrayStates();
            string sourcePath = TexturePath("Cliff", "Height");
            string backupPath = sourcePath + ".task4-missing";
            Assert.That(File.Exists(backupPath), Is.False, backupPath);

            File.Move(sourcePath, backupPath);
            try
            {
                Assert.That(
                    () => FirstArtTerrainAssetBuilder.BuildTextureArrays(),
                    Throws.TypeOf<FileNotFoundException>());
                AssertStatesEqual(CaptureArrayStates(), before, "missing-source abort");
            }
            finally
            {
                if (File.Exists(backupPath))
                    File.Move(backupPath, sourcePath);
            }
        }

        [Test]
        public void BuildTextureArrays_MaskCompressionFailureLeavesSourcesAndGeneratedArraysUntouched()
        {
            FirstArtTerrainAssetBuilder.BuildTextureArrays();
            Dictionary<string, AssetState> sourcesBefore = CaptureSourceStates();
            Dictionary<string, AssetState> arraysBefore = CaptureArrayStates();
            string failingPath = TexturePath("Wasteland", "Mask");
            var failure = new InvalidOperationException("injected Mask compression failure");
            bool checkpointReached = false;

            FirstArtTerrainAssetBuilder.MaskCompressionCheckpoint = path =>
            {
                if (!string.Equals(path, failingPath, StringComparison.Ordinal))
                    return;

                checkpointReached = true;
                throw failure;
            };

            try
            {
                InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                    () => FirstArtTerrainAssetBuilder.BuildTextureArrays());
                Assert.That(thrown, Is.SameAs(failure));
            }
            finally
            {
                FirstArtTerrainAssetBuilder.MaskCompressionCheckpoint = null;
            }

            Assert.That(checkpointReached, Is.True);
            AssertStatesEqual(CaptureSourceStates(), sourcesBefore, "Mask compression source rollback");
            AssertStatesEqual(CaptureArrayStates(), arraysBefore, "Mask compression array rollback");
        }

        [Test]
        public void BuildTextureArrays_HeightOperationAndCleanupFailuresStillRestoreSource()
        {
            string path = TexturePath("Wasteland", "Height");
            string guidBefore = AssetDatabase.AssetPathToGUID(path);
            Hash128 dependencyHashBefore = AssetDatabase.GetAssetDependencyHash(path);
            byte[] metaBefore = File.ReadAllBytes(path + ".meta");
            TextureImporter importerBefore = RequireTextureImporter(path);
            string platformName = BuildPipeline
                .GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)
                .ToString();
            TextureImporterPlatformSettings platformBefore =
                importerBefore.GetPlatformTextureSettings(platformName);
            var operationFailure = new InvalidOperationException("injected Height operation failure");
            var cleanupFailure = new InvalidOperationException("injected Height cleanup failure");
            bool operationCheckpointReached = false;
            bool cleanupCheckpointReached = false;

            FirstArtTerrainAssetBuilder.HeightSourceReadableCheckpoint = observedPath =>
            {
                if (!string.Equals(observedPath, path, StringComparison.Ordinal))
                    return;

                operationCheckpointReached = true;
                TextureImporter readableImporter = RequireTextureImporter(path);
                Texture2D readableSource = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(readableImporter.isReadable, Is.True, path);
                Assert.That(readableSource, Is.Not.Null, path);
                Assert.That(readableSource.format, Is.EqualTo(TextureFormat.R16), path);
                throw operationFailure;
            };
            FirstArtPassImportPolicy.TemporaryPlatformRestoreCheckpoint = observedPath =>
            {
                if (!string.Equals(observedPath, path, StringComparison.Ordinal))
                    return;

                cleanupCheckpointReached = true;
                throw cleanupFailure;
            };

            try
            {
                AggregateException thrown = Assert.Throws<AggregateException>(
                    () => FirstArtTerrainAssetBuilder.BuildTextureArrays());
                CollectionAssert.Contains(thrown.Flatten().InnerExceptions, operationFailure);
                CollectionAssert.Contains(thrown.Flatten().InnerExceptions, cleanupFailure);
            }
            finally
            {
                FirstArtTerrainAssetBuilder.HeightSourceReadableCheckpoint = null;
                FirstArtPassImportPolicy.TemporaryPlatformRestoreCheckpoint = null;
            }

            Assert.That(operationCheckpointReached, Is.True);
            Assert.That(cleanupCheckpointReached, Is.True);
            TextureImporter importerAfter = RequireTextureImporter(path);
            TextureImporterPlatformSettings platformAfter =
                importerAfter.GetPlatformTextureSettings(platformName);
            Assert.That(importerAfter.isReadable, Is.False, path);
            AssertPlatformSettingsEqual(platformAfter, platformBefore, path);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guidBefore), path);
            Assert.That(AssetDatabase.GetAssetDependencyHash(path), Is.EqualTo(dependencyHashBefore), path);
            Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metaBefore), path);

            using (FirstArtPassImportPolicy.AllowTemporaryReadability(path))
            {
            }

            Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metaBefore), path);
        }

        [TestCase(0, 0, 0, 0, 0)]
        [TestCase(65535, 65535, 65535, 65535, 255)]
        [TestCase(0, 256, 514, 65535, 64)]
        [TestCase(128, 129, 128, 129, 1)]
        public void QuantizeHeightBlock_UsesRoundedAverageAndUshortToByteRounding(
            int a,
            int b,
            int c,
            int d,
            int expected)
        {
            byte actual = FirstArtTerrainAssetBuilder.QuantizeHeightBlock(
                (ushort)a,
                (ushort)b,
                (ushort)c,
                (ushort)d);

            Assert.That(actual, Is.EqualTo((byte)expected));
        }

        private static void AssertArrayContract(
            Texture2DArray array,
            int expectedSize,
            string formatSourcePath,
            bool expectedSourceSrgb)
        {
            Assert.That(array.depth, Is.EqualTo(7));
            Assert.That(array.width, Is.EqualTo(expectedSize));
            Assert.That(array.height, Is.EqualTo(expectedSize));
            Assert.That(array.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(array.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(array.anisoLevel, Is.EqualTo(4));
            Assert.That(array.mipmapCount, Is.GreaterThan(1));
            Assert.That(array.isReadable, Is.False);

            if (formatSourcePath == null)
            {
                Assert.That(GraphicsFormatUtility.IsSRGBFormat(array.graphicsFormat), Is.False);
                return;
            }

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(formatSourcePath);
            Assert.That(source, Is.Not.Null, formatSourcePath);
            TextureImporter importer = RequireTextureImporter(formatSourcePath);
            Assert.That(importer.sRGBTexture, Is.EqualTo(expectedSourceSrgb), formatSourcePath);
            Assert.That(array.format, Is.EqualTo(source.format), formatSourcePath);
            Assert.That(array.graphicsFormat, Is.EqualTo(source.graphicsFormat), formatSourcePath);
            Assert.That(
                GraphicsFormatUtility.IsSRGBFormat(array.graphicsFormat),
                Is.EqualTo(GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat)),
                formatSourcePath);
            Assert.That(array.mipmapCount, Is.EqualTo(source.mipmapCount), formatSourcePath);
        }

        private static void AssertSlicePixelsMatch(
            Texture2DArray array,
            string channel,
            int x,
            int y,
            bool linear)
        {
            for (int slice = 0; slice < TerrainNames.Length; slice++)
            {
                string sourcePath = TexturePath(TerrainNames[slice], channel);
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                Assert.That(source, Is.Not.Null, sourcePath);

                Color32 expected = ReadPixel(source, x, y);
                Color32 actual = ReadPixel(array, slice, x, y, linear);
                AssertColorWithinOneByte(actual, expected, sourcePath);
            }
        }

        private static void AssertMaskSamplesWithinCompressionError(
            Texture2DArray mask)
        {
            long totalAbsoluteError = 0;
            int comparedChannels = 0;
            for (int slice = 0; slice < TerrainNames.Length; slice++)
            {
                string sourcePath = TexturePath(TerrainNames[slice], "Mask");
                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                Assert.That(source, Is.Not.Null, sourcePath);
                Color32[] expectedPixels = ReadAllPixels(source, source.width, source.height);
                Color32[] actualPixels = ReadArraySlicePixels(mask, slice);

                for (int sample = 0; sample < 64; sample++)
                {
                    int x = (17 + sample * 271) % mask.width;
                    int y = (31 + sample * 421) % mask.height;
                    int pixelIndex = y * mask.width + x;
                    Color32 expected = expectedPixels[pixelIndex];
                    Color32 actual = actualPixels[pixelIndex];
                    int[] errors =
                    {
                        Mathf.Abs(actual.r - expected.r),
                        Mathf.Abs(actual.g - expected.g),
                        Mathf.Abs(actual.b - expected.b),
                        Mathf.Abs(actual.a - expected.a),
                    };
                    for (int channel = 0; channel < errors.Length; channel++)
                    {
                        Assert.That(
                            errors[channel],
                            Is.LessThanOrEqualTo(16),
                            $"{sourcePath} ({x},{y}) channel {channel}");
                        totalAbsoluteError += errors[channel];
                        comparedChannels++;
                    }
                }
            }

            double meanAbsoluteError =
                comparedChannels == 0
                    ? 0d
                    : totalAbsoluteError / (double)comparedChannels;
            TestContext.WriteLine(
                "FirstTerrainMaskMeanAbsoluteChannelError=" +
                meanAbsoluteError.ToString("F6"));
            Assert.That(meanAbsoluteError, Is.LessThanOrEqualTo(4d));
        }

        private static Color32[] ReadArraySlicePixels(
            Texture2DArray array,
            int slice)
        {
            var sliceTexture = new Texture2D(
                array.width,
                array.height,
                array.format,
                false,
                true);
            try
            {
                Graphics.CopyTexture(array, slice, 0, sliceTexture, 0, 0);
                return ReadAllPixels(sliceTexture, array.width, array.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sliceTexture);
            }
        }

        private static Color32[] ReadAllPixels(
            Texture source,
            int width,
            int height)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                readback.ReadPixels(
                    new Rect(0f, 0f, width, height),
                    0,
                    0,
                    false);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        private static Texture2DArray LoadArray(string path)
        {
            Texture2DArray array = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            Assert.That(array, Is.Not.Null, path);
            return array;
        }

        private static string TexturePath(string terrain, string channel)
        {
            return $"{TerrainRoot}/{terrain}/T_Terrain_{terrain}_{channel}.png";
        }

        private static IEnumerable<string> SourcePaths()
        {
            foreach (string terrain in TerrainNames)
            foreach (string channel in Channels)
                yield return TexturePath(terrain, channel);
        }

        private static Dictionary<string, AssetState> CaptureSourceStates()
        {
            var states = new Dictionary<string, AssetState>(StringComparer.Ordinal);
            string platformName = BuildPipeline
                .GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget)
                .ToString();
            foreach (string path in SourcePaths())
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                states.Add(
                    path,
                    new AssetState(
                        AssetDatabase.AssetPathToGUID(path),
                        AssetDatabase.GetAssetDependencyHash(path),
                        importer.isReadable,
                        File.ReadAllBytes(path),
                        File.ReadAllBytes(path + ".meta"),
                        importer.GetPlatformTextureSettings(platformName)));
            }

            return states;
        }

        private static Dictionary<string, AssetState> CaptureArrayStates()
        {
            var states = new Dictionary<string, AssetState>(StringComparer.Ordinal);
            foreach (string path in ArrayPaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2DArray>(path), Is.Not.Null, path);
                states.Add(
                    path,
                    new AssetState(
                        AssetDatabase.AssetPathToGUID(path),
                        AssetDatabase.GetAssetDependencyHash(path),
                        false,
                        File.ReadAllBytes(path),
                        File.ReadAllBytes(path + ".meta"),
                        null));
            }

            return states;
        }

        private static void AssertStatesEqual(
            Dictionary<string, AssetState> actual,
            Dictionary<string, AssetState> expected,
            string context)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), context);
            foreach (KeyValuePair<string, AssetState> pair in expected)
            {
                AssetState value = actual[pair.Key];
                Assert.That(value.Guid, Is.EqualTo(pair.Value.Guid), $"{context}: {pair.Key} GUID");
                Assert.That(value.DependencyHash, Is.EqualTo(pair.Value.DependencyHash),
                    $"{context}: {pair.Key} dependency hash");
                Assert.That(value.IsReadable, Is.EqualTo(pair.Value.IsReadable),
                    $"{context}: {pair.Key} readability");
                Assert.That(value.ContentBytes, Is.EqualTo(pair.Value.ContentBytes),
                    $"{context}: {pair.Key} content bytes");
                Assert.That(value.MetaBytes, Is.EqualTo(pair.Value.MetaBytes),
                    $"{context}: {pair.Key} meta bytes");
                if (pair.Value.PlatformSettings != null)
                {
                    Assert.That(value.PlatformSettings, Is.Not.Null,
                        $"{context}: {pair.Key} platform settings");
                    AssertPlatformSettingsEqual(
                        value.PlatformSettings,
                        pair.Value.PlatformSettings,
                        $"{context}: {pair.Key}");
                }
            }
        }

        private static Color32 ReadCenterPixel(Texture2D source)
        {
            return ReadPixel(source, source.width / 2, source.height / 2);
        }

        private static Color32 ReadPixel(Texture2D source, int x, int y)
        {
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, renderTexture);
                RenderTexture.active = renderTexture;
                pixel.ReadPixels(
                    new Rect(x, y, 1, 1),
                    0,
                    0,
                    false);
                pixel.Apply(false, false);
                return pixel.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.DestroyImmediate(pixel);
            }
        }

        private static Color32 ReadCenterPixel(Texture2DArray array, int slice)
        {
            return ReadPixel(array, slice, array.width / 2, array.height / 2, false);
        }

        private static Color32 ReadPixel(
            Texture2DArray array,
            int slice,
            int x,
            int y,
            bool linear)
        {
            var texture = new Texture2D(
                array.width,
                array.height,
                array.format,
                false,
                linear);
            try
            {
                Graphics.CopyTexture(array, slice, 0, texture, 0, 0);
                return ReadPixel(texture, x, y);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void AssertColorWithinOneByte(
            Color32 actual,
            Color32 expected,
            string context)
        {
            Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThanOrEqualTo(1), context + " R");
            Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThanOrEqualTo(1), context + " G");
            Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThanOrEqualTo(1), context + " B");
            Assert.That(Mathf.Abs(actual.a - expected.a), Is.LessThanOrEqualTo(1), context + " A");
        }

        private static void AssertHeightBlockMatchesSource(
            Texture2DArray height,
            int slice,
            string terrain,
            int outputX,
            int outputY)
        {
            string path = TexturePath(terrain, "Height");
            byte[] metaBefore = File.ReadAllBytes(path + ".meta");
            IDisposable readabilityScope = null;
            try
            {
                readabilityScope = FirstArtPassImportPolicy.AllowTemporaryReadability(path);
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);

                Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(source, Is.Not.Null, path);
                Assert.That(source.format, Is.EqualTo(TextureFormat.R16), path);
                NativeArray<ushort> pixels = source.GetPixelData<ushort>(0);
                int sourceX = outputX * 2;
                int sourceY = outputY * 2;
                int first = sourceY * source.width + sourceX;
                uint average = (
                    (uint)pixels[first] +
                    pixels[first + 1] +
                    pixels[first + source.width] +
                    pixels[first + source.width + 1] +
                    2u) / 4u;
                byte expected = (byte)((average + 128u) / 257u);

                Color32 actual = ReadPixel(
                    height,
                    slice,
                    outputX,
                    outputY,
                    true);
                Assert.That(actual.r, Is.EqualTo(expected), path);
            }
            finally
            {
                readabilityScope?.Dispose();
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metaBefore), path);
            }
        }

        private static TextureImporter RequireTextureImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            return importer;
        }

        private static void AssertPlatformSettingsEqual(
            TextureImporterPlatformSettings actual,
            TextureImporterPlatformSettings expected,
            string context)
        {
            Assert.That(actual.name, Is.EqualTo(expected.name), context + " platform name");
            Assert.That(actual.overridden, Is.EqualTo(expected.overridden), context + " overridden");
            Assert.That(actual.maxTextureSize, Is.EqualTo(expected.maxTextureSize), context + " max size");
            Assert.That(actual.format, Is.EqualTo(expected.format), context + " format");
            Assert.That(
                actual.textureCompression,
                Is.EqualTo(expected.textureCompression),
                context + " compression");
            Assert.That(
                actual.compressionQuality,
                Is.EqualTo(expected.compressionQuality),
                context + " quality");
            Assert.That(
                actual.crunchedCompression,
                Is.EqualTo(expected.crunchedCompression),
                context + " crunch");
        }

        private static Dictionary<string, ExternalAssetBackup> CreateExternalArrayBackups(
            string backupRoot)
        {
            Directory.CreateDirectory(backupRoot);
            var backups = new Dictionary<string, ExternalAssetBackup>(StringComparer.Ordinal);
            for (int index = 0; index < ArrayPaths.Length; index++)
            {
                string path = ArrayPaths[index];
                string absolutePath = Path.GetFullPath(path);
                string absoluteMetaPath = absolutePath + ".meta";
                bool existed = File.Exists(absolutePath);
                string assetBackup = Path.Combine(backupRoot, index + ".asset");
                string metaBackup = Path.Combine(backupRoot, index + ".meta");
                if (existed)
                {
                    File.Copy(absolutePath, assetBackup, true);
                    File.Copy(absoluteMetaPath, metaBackup, true);
                }

                backups.Add(
                    path,
                    new ExternalAssetBackup(
                        existed,
                        assetBackup,
                        metaBackup,
                        existed ? AssetDatabase.AssetPathToGUID(path) : string.Empty));
            }

            return backups;
        }

        private static Dictionary<string, FileAssetState> CaptureFileArrayStatesAllowMissing()
        {
            var states = new Dictionary<string, FileAssetState>(StringComparer.Ordinal);
            foreach (string path in ArrayPaths)
            {
                string absolutePath = Path.GetFullPath(path);
                bool existed = File.Exists(absolutePath);
                states.Add(
                    path,
                    new FileAssetState(
                        existed,
                        existed ? AssetDatabase.AssetPathToGUID(path) : string.Empty,
                        existed ? File.ReadAllBytes(absolutePath) : null,
                        existed ? File.ReadAllBytes(absolutePath + ".meta") : null));
            }
            return states;
        }

        private static void AssertFileArrayStatesEqual(
            Dictionary<string, FileAssetState> actual,
            Dictionary<string, FileAssetState> expected,
            string context)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), context);
            foreach (KeyValuePair<string, FileAssetState> pair in expected)
            {
                FileAssetState value = actual[pair.Key];
                Assert.That(value.Existed, Is.EqualTo(pair.Value.Existed), context + " exists " + pair.Key);
                Assert.That(value.Guid, Is.EqualTo(pair.Value.Guid), context + " GUID " + pair.Key);
                Assert.That(value.ContentBytes, Is.EqualTo(pair.Value.ContentBytes), context + " bytes " + pair.Key);
                Assert.That(value.MetaBytes, Is.EqualTo(pair.Value.MetaBytes), context + " meta " + pair.Key);
            }
        }

        private static void RestoreExternalArrayBackups(
            Dictionary<string, ExternalAssetBackup> backups,
            string backupRoot)
        {
            try
            {
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (KeyValuePair<string, ExternalAssetBackup> pair in backups)
                    {
                        string absolutePath = Path.GetFullPath(pair.Key);
                        string absoluteMetaPath = absolutePath + ".meta";
                        if (pair.Value.Existed)
                        {
                            File.Copy(pair.Value.AssetBackupPath, absolutePath, true);
                            File.Copy(pair.Value.MetaBackupPath, absoluteMetaPath, true);
                        }
                        else
                        {
                            if (File.Exists(absolutePath))
                                File.Delete(absolutePath);
                            if (File.Exists(absoluteMetaPath))
                                File.Delete(absoluteMetaPath);
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (KeyValuePair<string, ExternalAssetBackup> pair in backups)
                {
                    if (!pair.Value.Existed)
                        continue;
                    AssetDatabase.ImportAsset(
                        pair.Key,
                        ImportAssetOptions.ForceUpdate |
                        ImportAssetOptions.ForceSynchronousImport);
                    Assert.That(
                        AssetDatabase.AssetPathToGUID(pair.Key),
                        Is.EqualTo(pair.Value.Guid),
                        pair.Key);
                }
            }
            finally
            {
                if (Directory.Exists(backupRoot))
                    Directory.Delete(backupRoot, true);
            }
        }

        private static Texture2DArray CreateHeightSentinel()
        {
            var sentinel = new Texture2DArray(
                1024,
                1024,
                TerrainNames.Length,
                TextureFormat.R8,
                true,
                true);
            try
            {
                var pixels = new byte[1024 * 1024];
                for (int layer = 0; layer < TerrainNames.Length; layer++)
                {
                    byte value = (byte)(17 + layer * 23);
                    for (int index = 0; index < pixels.Length; index++)
                        pixels[index] = value;
                    sentinel.SetPixelData(pixels, 0, layer);
                }
                sentinel.Apply(true, false);
                return sentinel;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(sentinel);
                throw;
            }
        }

        private static Texture2DArray CreateMaskSentinel()
        {
            const int size = 2048;
            var sentinel = new Texture2DArray(
                size,
                size,
                TerrainNames.Length,
                TextureFormat.BC7,
                true,
                true);
            var pixels = new Color32[size * size];
            try
            {
                for (int layer = 0; layer < TerrainNames.Length; layer++)
                {
                    Color32 color = layer == 2
                        ? new Color32(18, 224, 242, 255)
                        : new Color32((byte)(31 + layer * 19), 11, 7, 255);
                    for (int index = 0; index < pixels.Length; index++)
                        pixels[index] = color;

                    var staging = new Texture2D(
                        size,
                        size,
                        TextureFormat.RGBA32,
                        true,
                        true);
                    try
                    {
                        staging.SetPixels32(pixels);
                        staging.Apply(true, false);
                        EditorUtility.CompressTexture(
                            staging,
                            TextureFormat.BC7,
                            TextureCompressionQuality.Best);
                        Assert.That(staging.format, Is.EqualTo(TextureFormat.BC7));
                        for (int mip = 0; mip < staging.mipmapCount; mip++)
                        {
                            sentinel.SetPixelData(
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

                sentinel.Apply(false, false);
                Color32 ordinary = ReadArrayMipPixel(sentinel, 1, 3);
                Color32 namedDistinct = ReadArrayMipPixel(sentinel, 2, 3);
                Assert.That(namedDistinct, Is.Not.EqualTo(ordinary));
                return sentinel;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(sentinel);
                throw;
            }
        }

        private static void OverwriteDestinationWithSentinel(
            string path,
            Texture2DArray sentinel)
        {
            try
            {
                Texture2DArray destination = LoadArray(path);
                string expectedName = Path.GetFileNameWithoutExtension(path);
                EditorUtility.CopySerialized(sentinel, destination);
                destination.name = expectedName;
                Assert.That(destination.name, Is.EqualTo(expectedName), path);
                destination.Apply(false, true);
                EditorUtility.SetDirty(destination);
                Assert.That(destination.name, Is.EqualTo(expectedName), path);
                AssetDatabase.SaveAssets();
                Assert.That(destination.name, Is.EqualTo(expectedName), path);
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sentinel);
            }
        }

        private static Dictionary<string, ArrayRuntimeState> CaptureArrayRuntimeStates()
        {
            var states = new Dictionary<string, ArrayRuntimeState>(StringComparer.Ordinal);
            foreach (string path in ArrayPaths)
            {
                Texture2DArray array = LoadArray(path);
                states.Add(
                    path,
                    new ArrayRuntimeState(
                        AssetDatabase.AssetPathToGUID(path),
                        array.width,
                        array.height,
                        array.depth,
                        array.format,
                        array.mipmapCount,
                        array.wrapMode,
                        array.filterMode,
                        array.anisoLevel,
                        array.isReadable,
                        CalculateCompressedPayloadBytes(array),
                        Profiler.GetRuntimeMemorySizeLong(array),
                        CalculateGpuDigest(array)));
            }
            return states;
        }

        private static string CalculateGpuDigest(Texture2DArray array)
        {
            using (SHA256 sha = SHA256.Create())
            {
                var bytes = new List<byte>(array.depth * array.mipmapCount * 4);
                for (int layer = 0; layer < array.depth; layer++)
                for (int mip = 0; mip < array.mipmapCount; mip++)
                {
                    Color32 pixel = ReadArrayMipPixel(array, layer, mip);
                    bytes.Add(pixel.r);
                    bytes.Add(pixel.g);
                    bytes.Add(pixel.b);
                    bytes.Add(pixel.a);
                }
                byte[] hash = sha.ComputeHash(bytes.ToArray());
                var builder = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                    builder.Append(value.ToString("x2"));
                return builder.ToString();
            }
        }

        private static Color32 ReadArrayMipPixel(Texture2DArray array, int layer, int mip)
        {
            int width = Math.Max(1, array.width >> mip);
            int height = Math.Max(1, array.height >> mip);
            int copyWidth = array.format == TextureFormat.BC7
                ? Math.Max(4, width)
                : width;
            int copyHeight = array.format == TextureFormat.BC7
                ? Math.Max(4, height)
                : height;
            bool needsDestinationMips = copyWidth != width || copyHeight != height;
            int destinationMip = needsDestinationMips
                ? (width == 1 ? 2 : 1)
                : 0;
            var slice = new Texture2D(
                copyWidth,
                copyHeight,
                array.format,
                needsDestinationMips,
                true);
            RenderTexture renderTexture = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.CopyTexture(array, layer, mip, slice, 0, destinationMip);
                Graphics.Blit(slice, renderTexture);
                RenderTexture.active = renderTexture;
                readback.ReadPixels(
                    new Rect(0f, 0f, width, height),
                    0,
                    0,
                    false);
                readback.Apply(false, false);
                return readback.GetPixel(width / 2, height / 2);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(slice);
            }
        }

        private static long SumNativeBytes(Dictionary<string, ArrayRuntimeState> states)
        {
            long sum = 0L;
            foreach (ArrayRuntimeState state in states.Values)
                sum += state.NativeBytes;
            return sum;
        }

        private static void AssertArrayRuntimeStatesEqual(
            Dictionary<string, ArrayRuntimeState> actual,
            Dictionary<string, ArrayRuntimeState> expected,
            string context)
        {
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys), context);
            foreach (KeyValuePair<string, ArrayRuntimeState> pair in expected)
            {
                ArrayRuntimeState value = actual[pair.Key];
                Assert.That(value.Guid, Is.EqualTo(pair.Value.Guid), context + " GUID " + pair.Key);
                Assert.That(value.Width, Is.EqualTo(pair.Value.Width), context + " width " + pair.Key);
                Assert.That(value.Height, Is.EqualTo(pair.Value.Height), context + " height " + pair.Key);
                Assert.That(value.Depth, Is.EqualTo(pair.Value.Depth), context + " depth " + pair.Key);
                Assert.That(value.Format, Is.EqualTo(pair.Value.Format), context + " format " + pair.Key);
                Assert.That(value.MipCount, Is.EqualTo(pair.Value.MipCount), context + " mips " + pair.Key);
                Assert.That(value.WrapMode, Is.EqualTo(pair.Value.WrapMode), context + " wrap " + pair.Key);
                Assert.That(value.FilterMode, Is.EqualTo(pair.Value.FilterMode), context + " filter " + pair.Key);
                Assert.That(value.AnisoLevel, Is.EqualTo(pair.Value.AnisoLevel), context + " aniso " + pair.Key);
                Assert.That(value.IsReadable, Is.False, context + " readable " + pair.Key);
                Assert.That(value.PayloadBytes, Is.EqualTo(pair.Value.PayloadBytes), context + " payload " + pair.Key);
                Assert.That(value.GpuDigest, Is.EqualTo(pair.Value.GpuDigest), context + " content " + pair.Key);
            }
        }

        private sealed class ExternalAssetBackup
        {
            public ExternalAssetBackup(
                bool existed,
                string assetBackupPath,
                string metaBackupPath,
                string guid)
            {
                Existed = existed;
                AssetBackupPath = assetBackupPath;
                MetaBackupPath = metaBackupPath;
                Guid = guid;
            }

            public bool Existed { get; }
            public string AssetBackupPath { get; }
            public string MetaBackupPath { get; }
            public string Guid { get; }
        }

        private sealed class FileAssetState
        {
            public FileAssetState(
                bool existed,
                string guid,
                byte[] contentBytes,
                byte[] metaBytes)
            {
                Existed = existed;
                Guid = guid;
                ContentBytes = contentBytes;
                MetaBytes = metaBytes;
            }

            public bool Existed { get; }
            public string Guid { get; }
            public byte[] ContentBytes { get; }
            public byte[] MetaBytes { get; }
        }

        private sealed class ArrayRuntimeState
        {
            public ArrayRuntimeState(
                string guid,
                int width,
                int height,
                int depth,
                TextureFormat format,
                int mipCount,
                TextureWrapMode wrapMode,
                FilterMode filterMode,
                int anisoLevel,
                bool isReadable,
                long payloadBytes,
                long nativeBytes,
                string gpuDigest)
            {
                Guid = guid;
                Width = width;
                Height = height;
                Depth = depth;
                Format = format;
                MipCount = mipCount;
                WrapMode = wrapMode;
                FilterMode = filterMode;
                AnisoLevel = anisoLevel;
                IsReadable = isReadable;
                PayloadBytes = payloadBytes;
                NativeBytes = nativeBytes;
                GpuDigest = gpuDigest;
            }

            public string Guid { get; }
            public int Width { get; }
            public int Height { get; }
            public int Depth { get; }
            public TextureFormat Format { get; }
            public int MipCount { get; }
            public TextureWrapMode WrapMode { get; }
            public FilterMode FilterMode { get; }
            public int AnisoLevel { get; }
            public bool IsReadable { get; }
            public long PayloadBytes { get; }
            public long NativeBytes { get; }
            public string GpuDigest { get; }
        }

        private sealed class AssetState
        {
            public AssetState(
                string guid,
                Hash128 dependencyHash,
                bool isReadable,
                byte[] contentBytes,
                byte[] metaBytes,
                TextureImporterPlatformSettings platformSettings)
            {
                Guid = guid;
                DependencyHash = dependencyHash;
                IsReadable = isReadable;
                ContentBytes = contentBytes;
                MetaBytes = metaBytes;
                PlatformSettings = platformSettings;
            }

            public string Guid { get; }

            public Hash128 DependencyHash { get; }

            public bool IsReadable { get; }

            public byte[] ContentBytes { get; }

            public byte[] MetaBytes { get; }

            public TextureImporterPlatformSettings PlatformSettings { get; }
        }
    }
}
