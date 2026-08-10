using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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

    }
}
