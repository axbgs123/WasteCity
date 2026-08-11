using System;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxWorldLayout3DTests
    {
        [Test]
        public void Constants_FreezeApprovedDimensionsOffsetsAndSeed()
        {
            Assert.That(GrayboxWorldLayout3D.DefaultSeed, Is.EqualTo(8128));
            Assert.That(GrayboxWorldLayout3D.LegacyWidth, Is.EqualTo(32));
            Assert.That(GrayboxWorldLayout3D.LegacyHeight, Is.EqualTo(24));
            Assert.That(GrayboxWorldLayout3D.WorldWidth, Is.EqualTo(64));
            Assert.That(GrayboxWorldLayout3D.WorldHeight, Is.EqualTo(48));
            Assert.That(GrayboxWorldLayout3D.LegacyOffsetX, Is.EqualTo(16));
            Assert.That(GrayboxWorldLayout3D.LegacyOffsetY, Is.EqualTo(12));
        }

        [Test]
        public void CreateDefault_PreservesEveryLegacyCellAndLeavesSparseOuterRing()
        {
            var legacy = new WorldMapModel(32, 24, new WorldSeed(8128));

            WorldMapModel expanded = GrayboxWorldLayout3D.CreateDefault();

            Assert.That(expanded.Width, Is.EqualTo(64));
            Assert.That(expanded.Height, Is.EqualTo(48));
            var central = 0;
            var outer = 0;
            for (var x = 0; x < expanded.Width; x++)
            for (var y = 0; y < expanded.Height; y++)
            {
                bool isCentral = x >= 16 && x < 48 && y >= 12 && y < 36;
                WorldCell actual = expanded.Get(x, y);
                if (isCentral)
                {
                    AssertCellEquals(legacy.Get(x - 16, y - 12), actual);
                    central++;
                }
                else
                {
                    Assert.That(actual.Terrain, Is.EqualTo(TerrainKind.Wasteland));
                    Assert.That(actual.Traversal, Is.EqualTo(WorldTraversalKind.Open));
                    Assert.That(actual.ResourceId, Is.Null);
                    Assert.That(actual.ResourceAmount, Is.Zero);
                    outer++;
                }
            }

            Assert.That(central, Is.EqualTo(768));
            Assert.That(outer, Is.EqualTo(2304));
            Assert.That(
                expanded.ResourceNodeCount,
                Is.EqualTo(legacy.ResourceNodeCount));
        }

        [Test]
        public void Create_WithSameSeed_IsCellForCellDeterministic()
        {
            WorldMapModel first = GrayboxWorldLayout3D.Create(37);
            WorldMapModel second = GrayboxWorldLayout3D.Create(37);

            AssertMapsEqual(first, second);
        }

        [Test]
        public void ExpandedLegacyCells_KeepExactWorldPositions()
        {
            var oldMapper = new PlanarCoordinateMapper3D(32, 24);
            var newMapper = new PlanarCoordinateMapper3D(64, 48);
            for (var x = 0; x < 32; x++)
            for (var y = 0; y < 24; y++)
            {
                Assert.That(
                    oldMapper.TryCellToWorld(x, y, 0f, out Vector3 before),
                    Is.True);
                Assert.That(
                    newMapper.TryCellToWorld(
                        GrayboxWorldLayout3D.ToExpandedX(x),
                        GrayboxWorldLayout3D.ToExpandedY(y),
                        0f,
                        out Vector3 after),
                    Is.True);
                Assert.That(after, Is.EqualTo(before));
            }
        }

        [Test]
        public void ApprovedCityCell_MapsToExpandedCell()
        {
            Assert.That(GrayboxWorldLayout3D.ToExpandedX(7), Is.EqualTo(23));
            Assert.That(GrayboxWorldLayout3D.ToExpandedY(8), Is.EqualTo(20));
        }

        [TestCase(-1)]
        [TestCase(32)]
        public void ToExpandedX_RejectsCellsOutsideLegacyWorld(int x)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GrayboxWorldLayout3D.ToExpandedX(x));
        }

        [TestCase(-1)]
        [TestCase(24)]
        public void ToExpandedY_RejectsCellsOutsideLegacyWorld(int y)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GrayboxWorldLayout3D.ToExpandedY(y));
        }

        private static void AssertMapsEqual(
            WorldMapModel expected,
            WorldMapModel actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            Assert.That(
                actual.ResourceNodeCount,
                Is.EqualTo(expected.ResourceNodeCount));
            for (var x = 0; x < expected.Width; x++)
            for (var y = 0; y < expected.Height; y++)
                AssertCellEquals(expected.Get(x, y), actual.Get(x, y));
        }

        private static void AssertCellEquals(
            WorldCell expected,
            WorldCell actual)
        {
            Assert.That(actual.Terrain, Is.EqualTo(expected.Terrain));
            Assert.That(actual.ResourceId, Is.EqualTo(expected.ResourceId));
            Assert.That(
                actual.ResourceAmount,
                Is.EqualTo(expected.ResourceAmount));
            Assert.That(actual.Traversal, Is.EqualTo(expected.Traversal));
        }
    }
}
