using System;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainControlMapTests
    {
        private FirstArtTerrainProfile3D profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(profile);
        }

        [Test]
        public void Generate_UsesFourPixelsPerCellAndFrozenChannels()
        {
            WorldMapModel map = CreateSevenStripeMap();
            using (FirstArtTerrainControlMap3D result =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                Assert.That(result.Width, Is.EqualTo(map.Width * 4));
                Assert.That(result.Height, Is.EqualTo(map.Height * 4));
                Assert.That(result.ControlA.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(result.ControlB.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(result.ControlA.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(result.ControlB.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(result.ControlA.format, Is.EqualTo(TextureFormat.RGBA32));
                Assert.That(result.ControlB.format, Is.EqualTo(TextureFormat.RGBA32));
            }
        }

        [Test]
        public void Generate_NormalizesAndKeepsAtMostThreeWeights()
        {
            using (FirstArtTerrainControlMap3D result = GenerateThreeWayJunction())
            {
                for (int y = 0; y < result.Height; y++)
                for (int x = 0; x < result.Width; x++)
                {
                    TerrainControlWeights3D weights = result.GetWeights(x, y);
                    Assert.That(weights.Sum, Is.EqualTo(1f).Within(2f / 255f));
                    Assert.That(weights.NonZeroCount, Is.LessThanOrEqualTo(3));
                    int offset = (y * result.Width + x) * 4;
                    Assert.That(
                        result.ControlABytes[offset] + result.ControlABytes[offset + 1] +
                        result.ControlABytes[offset + 2] + result.ControlABytes[offset + 3] +
                        result.ControlBBytes[offset] + result.ControlBBytes[offset + 1] +
                        result.ControlBBytes[offset + 2],
                        Is.EqualTo(255));
                    Assert.That(result.ControlBBytes[offset + 3], Is.Zero);
                }
            }
        }

        [Test]
        public void Generate_SameMapProducesSameEncodedBytes()
        {
            using (FirstArtTerrainControlMap3D first = GenerateSeed8128())
            using (FirstArtTerrainControlMap3D second = GenerateSeed8128())
            {
                CollectionAssert.AreEqual(first.ControlABytes, second.ControlABytes);
                CollectionAssert.AreEqual(first.ControlBBytes, second.ControlBBytes);
            }
        }

        [TestCase(TerrainKind.Wasteland, WorldTraversalKind.Open, FirstArtTerrainLayer3D.Wasteland)]
        [TestCase(TerrainKind.Rocky, WorldTraversalKind.Open, FirstArtTerrainLayer3D.Rocky)]
        [TestCase(TerrainKind.Wetland, WorldTraversalKind.Open, FirstArtTerrainLayer3D.Wetland)]
        [TestCase(TerrainKind.Crystal, WorldTraversalKind.Open, FirstArtTerrainLayer3D.Crystal)]
        [TestCase(TerrainKind.Crystal, WorldTraversalKind.Ruins, FirstArtTerrainLayer3D.Ruins)]
        [TestCase(TerrainKind.Rocky, WorldTraversalKind.DeepWater, FirstArtTerrainLayer3D.DeepWater)]
        [TestCase(TerrainKind.Wetland, WorldTraversalKind.Cliff, FirstArtTerrainLayer3D.Cliff)]
        public void Generate_OneCellCenterKeepsDeclaredLayerHighest(
            TerrainKind terrain,
            WorldTraversalKind traversal,
            FirstArtTerrainLayer3D expected)
        {
            WorldMapModel map = MapOf(new WorldCell(terrain, null, 0, traversal));

            using (FirstArtTerrainControlMap3D result =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                TerrainControlWeights3D weights = result.GetWeights(1, 1);
                Assert.That(WeightOf(weights, expected), Is.GreaterThan(0.5f));
                Assert.That(WeightOf(weights, expected), Is.EqualTo(weights.Sum));
            }
        }

        [TestCase(FirstArtTerrainLayer3D.Rocky, 13, 85)]
        [TestCase(FirstArtTerrainLayer3D.Wetland, 13, 71)]
        [TestCase(FirstArtTerrainLayer3D.Crystal, 12, 18)]
        public void Generate_BaseTransitionsMatchConfiguredBoundaryBytes(
            FirstArtTerrainLayer3D neighbor,
            int insideBoundaryX,
            byte expectedInsideByte)
        {
            TerrainKind terrain = TerrainFor(neighbor);
            WorldMapModel map = MapOf(
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(terrain, null, 0));

            using (FirstArtTerrainControlMap3D result =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                Assert.That(
                    EncodedWeightOf(result, insideBoundaryX, 1, neighbor),
                    Is.EqualTo(expectedInsideByte));
                Assert.That(EncodedWeightOf(result, 10, 1, neighbor), Is.Zero);
            }
        }

        [TestCase(WorldTraversalKind.Ruins, FirstArtTerrainLayer3D.Ruins, 13, 35, 12)]
        [TestCase(WorldTraversalKind.DeepWater, FirstArtTerrainLayer3D.DeepWater, 15, 102, 13)]
        [TestCase(WorldTraversalKind.Cliff, FirstArtTerrainLayer3D.Cliff, 15, 114, 13)]
        public void Generate_SpecialTransitionsMatchConfiguredBoundaryBytes(
            WorldTraversalKind traversal,
            FirstArtTerrainLayer3D special,
            int insideBoundaryX,
            byte expectedInsideByte,
            int beyondBoundaryX)
        {
            WorldMapModel map = MapOf(
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Crystal, null, 0, traversal));

            using (FirstArtTerrainControlMap3D result =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                Assert.That(
                    EncodedWeightOf(result, insideBoundaryX, 1, special),
                    Is.EqualTo(expectedInsideByte));
                Assert.That(EncodedWeightOf(result, beyondBoundaryX, 1, special), Is.Zero);
            }
        }

        [Test]
        public void Generate_MapBordersNeverSampleOppositeEdge()
        {
            WorldMapModel map = MapOf(
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Rocky, null, 0),
                new WorldCell(TerrainKind.Crystal, null, 0));

            using (FirstArtTerrainControlMap3D result =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                Assert.That(WeightOf(result.GetWeights(0, 1), FirstArtTerrainLayer3D.Crystal), Is.EqualTo(0f));
                Assert.That(WeightOf(result.GetWeights(result.Width - 1, 1), FirstArtTerrainLayer3D.Wasteland), Is.EqualTo(0f));
            }
        }

        [Test]
        public void Constructor_WhenPerInstanceFaultOccursAfterControlAAllocation_DestroysPartialTexture()
        {
            ConstructorInfo faultConstructor =
                typeof(FirstArtTerrainControlMap3D).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(byte[]),
                        typeof(byte[]),
                        typeof(Action),
                    },
                    null);
            Assert.That(faultConstructor, Is.Not.Null);
            int textureCountBefore = RuntimeControlTextureCount();
            var injectedFailure = new Action(
                () => throw new InvalidOperationException(
                    "Injected after Control A allocation."));
            TargetInvocationException invocationException =
                Assert.Throws<TargetInvocationException>(
                    () => faultConstructor.Invoke(
                        new object[]
                        {
                            1,
                            1,
                            new byte[4],
                            new byte[4],
                            injectedFailure,
                        }));

            Assert.That(
                invocationException.InnerException,
                Is.TypeOf<InvalidOperationException>());
            Assert.That(
                invocationException.InnerException.Message,
                Is.EqualTo("Injected after Control A allocation."));

            Assert.That(
                RuntimeControlTextureCount(),
                Is.EqualTo(textureCountBefore));
        }

        [Test]
        public void ExpandedSeed8128_PreservesFrozenBytesWithinAllocationBudget()
        {
            const string expectedDigest =
                "de9d52dcb0e37180b47bc3f55a79c1e47151699cd53ef2743a3c5d2314f90d47";
            var map = new WorldMapModel(96, 64, new WorldSeed(8128));
            using (FirstArtTerrainControlMap3D first =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            using (FirstArtTerrainControlMap3D second =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                Assert.That(
                    CombinedDigest(
                        first.ControlABytes,
                        first.ControlBBytes),
                    Is.EqualTo(expectedDigest));
                Assert.That(
                    CombinedDigest(
                        second.ControlABytes,
                        second.ControlBBytes),
                    Is.EqualTo(expectedDigest));
                CollectionAssert.AreEqual(
                    first.ControlABytes,
                    second.ControlABytes);
                CollectionAssert.AreEqual(
                    first.ControlBBytes,
                    second.ControlBBytes);
            }

            GenerationMeasurement expanded = MeasureGeneration(map);
            TestContext.WriteLine(
                "FirstTerrainControl96x64ThreadBytes=" +
                expanded.CurrentThreadBytes);
            TestContext.WriteLine(
                "FirstTerrainControl96x64ProfileSamples=" +
                expanded.ProfileSamples);
            TestContext.WriteLine(
                "FirstTerrainControl96x64ProfileBytes=" +
                expanded.ProfileBytes);
            TestContext.WriteLine(
                "FirstTerrainControl96x64Digest=" + expanded.Digest);
            Assert.That(expanded.Digest, Is.EqualTo(expectedDigest));
            Assert.That(
                expanded.CurrentThreadBytes,
                Is.LessThanOrEqualTo(1200000));
            Assert.That(
                expanded.ProfileSamples,
                Is.LessThanOrEqualTo(64));
            Assert.That(
                expanded.ProfileBytes,
                Is.LessThanOrEqualTo(1200000));
        }

        [Test]
        public void AllocationProfilerPositiveControlCapturesManagedObjects()
        {
            var retained = new object[64];
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                256,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            int checksum = AllocateAndRetain(retained);
            GC.KeepAlive(retained);
            recorder.Stop();
            int samples = recorder.Count;
            long bytes = 0;
            for (int index = 0; index < recorder.Count; index++)
            {
                ProfilerRecorderSample sample = recorder.GetSample(index);
                bytes += sample.Value * sample.Count;
            }
            recorder.Dispose();

            TestContext.WriteLine(
                "FirstTerrainControlPositiveSamples=" + samples);
            TestContext.WriteLine(
                "FirstTerrainControlPositiveBytes=" + bytes);
            Assert.That(checksum, Is.EqualTo(69632));
            Assert.That(samples, Is.GreaterThan(0));
            Assert.That(bytes, Is.GreaterThan(0));
        }

        [Test]
        public void SameLayerCandidates_PreserveFrozenControlBytes()
        {
            const string expectedControlA =
                "827D0000906F0000906F0000827D0000" +
                "827D0000906F0000916E0000827D0000" +
                "827D0000916E0000916E0000827D0000" +
                "827D0000916E0000916E0000827D0000";
            const string expectedControlB =
                "00000000000000000000000000000000" +
                "00000000000000000000000000000000" +
                "00000000000000000000000000000000" +
                "00000000000000000000000000000000";
            WorldMapModel map = CreateSameLayerCandidateMap();
            using (FirstArtTerrainControlMap3D result =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                Assert.That(
                    CellBytesHex(
                        result.ControlABytes,
                        result.Width,
                        2,
                        2),
                    Is.EqualTo(expectedControlA));
                Assert.That(
                    CellBytesHex(
                        result.ControlBBytes,
                        result.Width,
                        2,
                        2),
                    Is.EqualTo(expectedControlB));
            }
        }

        private FirstArtTerrainControlMap3D GenerateThreeWayJunction()
        {
            return FirstArtTerrainControlMapGenerator3D.Generate(
                MapOf(
                    new WorldCell(TerrainKind.Wasteland, null, 0),
                    new WorldCell(TerrainKind.Rocky, null, 0),
                    new WorldCell(TerrainKind.Wetland, null, 0),
                    new WorldCell(TerrainKind.Crystal, null, 0)),
                profile);
        }

        private FirstArtTerrainControlMap3D GenerateSeed8128()
        {
            return FirstArtTerrainControlMapGenerator3D.Generate(
                new WorldMapModel(16, 12, new WorldSeed(8128)),
                profile);
        }

        private static WorldMapModel CreateSevenStripeMap()
        {
            return MapOf(
                new WorldCell(TerrainKind.Wasteland, null, 0),
                new WorldCell(TerrainKind.Rocky, null, 0),
                new WorldCell(TerrainKind.Wetland, null, 0),
                new WorldCell(TerrainKind.Crystal, null, 0),
                new WorldCell(TerrainKind.Crystal, null, 0, WorldTraversalKind.Ruins),
                new WorldCell(TerrainKind.Rocky, null, 0, WorldTraversalKind.DeepWater),
                new WorldCell(TerrainKind.Wetland, null, 0, WorldTraversalKind.Cliff));
        }

        private static WorldMapModel CreateSameLayerCandidateMap()
        {
            var cells = new WorldCell[5, 5];
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 5; x++)
                cells[x, y] = new WorldCell(TerrainKind.Wasteland, null, 0);

            cells[1, 2] = new WorldCell(TerrainKind.Rocky, null, 0);
            cells[3, 2] = new WorldCell(TerrainKind.Rocky, null, 0);
            cells[2, 4] = new WorldCell(TerrainKind.Rocky, null, 0);
            return new WorldMapModel(cells);
        }

        private static WorldMapModel MapOf(params WorldCell[] cells)
        {
            var source = new WorldCell[cells.Length, 1];
            for (int x = 0; x < cells.Length; x++)
                source[x, 0] = cells[x];
            return new WorldMapModel(source);
        }

        private static TerrainKind TerrainFor(FirstArtTerrainLayer3D layer)
        {
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Rocky:
                    return TerrainKind.Rocky;
                case FirstArtTerrainLayer3D.Wetland:
                    return TerrainKind.Wetland;
                case FirstArtTerrainLayer3D.Crystal:
                    return TerrainKind.Crystal;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(layer));
            }
        }

        private static float WeightOf(
            TerrainControlWeights3D weights,
            FirstArtTerrainLayer3D layer)
        {
            int index = (int)layer;
            return index < 4 ? weights.Base[index] : weights.Special[index - 4];
        }

        private static byte EncodedWeightOf(
            FirstArtTerrainControlMap3D result,
            int x,
            int y,
            FirstArtTerrainLayer3D layer)
        {
            int offset = (y * result.Width + x) * 4;
            int layerIndex = (int)layer;
            return layerIndex < 4
                ? result.ControlABytes[offset + layerIndex]
                : result.ControlBBytes[offset + layerIndex - 4];
        }

        private static int RuntimeControlTextureCount()
        {
            return Resources.FindObjectsOfTypeAll<Texture2D>()
                .Count(texture =>
                    texture != null &&
                    (texture.name == "FirstArtTerrainControlA" ||
                     texture.name == "FirstArtTerrainControlB"));
        }

        private GenerationMeasurement MeasureGeneration(WorldMapModel map)
        {
            using (FirstArtTerrainControlMap3D warmup =
                   FirstArtTerrainControlMapGenerator3D.Generate(map, profile))
            {
                Assert.That(warmup.Width, Is.EqualTo(map.Width * 4));
            }

            ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                400000,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            FirstArtTerrainControlMap3D result = null;
            long currentThreadBytes;
            try
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                result = FirstArtTerrainControlMapGenerator3D.Generate(
                    map,
                    profile);
                currentThreadBytes =
                    GC.GetAllocatedBytesForCurrentThread() - before;
                recorder.Stop();
                int samples = recorder.Count;
                long profiledBytes = 0;
                for (int index = 0; index < recorder.Count; index++)
                {
                    ProfilerRecorderSample sample =
                        recorder.GetSample(index);
                    profiledBytes += sample.Value * sample.Count;
                }
                return new GenerationMeasurement(
                    currentThreadBytes,
                    samples,
                    profiledBytes,
                    CombinedDigest(
                        result.ControlABytes,
                        result.ControlBBytes));
            }
            finally
            {
                recorder.Dispose();
                result?.Dispose();
            }
        }

        private static string CombinedDigest(byte[] controlA, byte[] controlB)
        {
            var combined = new byte[controlA.Length + controlB.Length];
            Buffer.BlockCopy(
                controlA,
                0,
                combined,
                0,
                controlA.Length);
            Buffer.BlockCopy(
                controlB,
                0,
                combined,
                controlA.Length,
                controlB.Length);
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(combined))
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static string CellBytesHex(
            byte[] source,
            int controlWidth,
            int cellX,
            int cellY)
        {
            var bytes = new byte[4 * 4 * 4];
            int destination = 0;
            for (int localY = 0; localY < 4; localY++)
            for (int localX = 0; localX < 4; localX++)
            {
                int sourceOffset =
                    (((cellY * 4 + localY) * controlWidth) +
                     cellX * 4 + localX) * 4;
                Buffer.BlockCopy(
                    source,
                    sourceOffset,
                    bytes,
                    destination,
                    4);
                destination += 4;
            }
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }

        [System.Runtime.CompilerServices.MethodImpl(
            System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static int AllocateAndRetain(object[] retained)
        {
            int checksum = 0;
            for (int index = 0; index < retained.Length; index++)
            {
                var allocation = new byte[1024 + index];
                allocation[0] = (byte)(index + 1);
                retained[index] = allocation;
                checksum += allocation.Length + allocation[0];
            }
            return checksum;
        }

        private readonly struct GenerationMeasurement
        {
            public GenerationMeasurement(
                long currentThreadBytes,
                int profileSamples,
                long profileBytes,
                string digest)
            {
                CurrentThreadBytes = currentThreadBytes;
                ProfileSamples = profileSamples;
                ProfileBytes = profileBytes;
                Digest = digest;
            }

            public long CurrentThreadBytes { get; }
            public int ProfileSamples { get; }
            public long ProfileBytes { get; }
            public string Digest { get; }
        }

    }
}
