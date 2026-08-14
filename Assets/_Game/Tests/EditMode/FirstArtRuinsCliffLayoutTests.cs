using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtRuinsCliffLayoutTests
    {
        private const string LayoutTypeName =
            "WasteCity.ArtIntegration3D.FirstArtRuinsCliffLayout3D, WasteCity.ArtIntegration3D";

        [Test]
        public void IDEA0004_ProjectScansYThenXAndReturnsAReadOnlyProjection()
        {
            WorldMapModel map = Map(
                3,
                3,
                (2, 0, WorldTraversalKind.Ruins),
                (0, 1, WorldTraversalKind.Cliff),
                (1, 1, WorldTraversalKind.Ruins),
                (0, 2, WorldTraversalKind.Ruins));

            IList placements = Project(map);

            Assert.That(placements.IsReadOnly, Is.True);
            CollectionAssert.AreEqual(
                new[] { "2,0", "0,1", "1,1", "0,2" },
                placements.Cast<object>().Select(CellKey));
        }

        [Test]
        public void IDEA0004_RuinsPlacesEveryCellAndReachesAllEightApprovedVariants()
        {
            WorldMapModel map = FilledMap(16, 16, WorldTraversalKind.Ruins);

            IList placements = Project(map);

            Assert.That(placements.Count, Is.EqualTo(16 * 16));
            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, 8),
                placements.Cast<object>()
                    .Select(value => Read<int>(value, "CatalogIndex"))
                    .Distinct());
            Assert.That(
                placements.Cast<object>().All(value =>
                    Read<object>(value, "Family").ToString() == "Ruins" &&
                    Read<int>(value, "NeighborMask") == 0 &&
                    Read<int>(value, "QuarterTurns") >= 0 &&
                    Read<int>(value, "QuarterTurns") <= 3),
                Is.True);

            object first = placements[0];
            Assert.That(Read<int>(first, "CatalogIndex"), Is.EqualTo(3));
            Assert.That(Read<int>(first, "QuarterTurns"), Is.EqualTo(3));
        }

        [Test]
        public void IDEA0004_CliffSelectsIsolatedOneOppositeThreeAndFourNeighborModules()
        {
            AssertCliff(
                Map(5, 5, (2, 2, WorldTraversalKind.Cliff)),
                0,
                FirstArtRuinsCliffModule3D.TopCap,
                0);
            AssertCliff(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (1, 2, WorldTraversalKind.Cliff)),
                FirstArtRuinsCliffCatalog3D.WestConnection,
                FirstArtRuinsCliffModule3D.EndCap,
                0);
            AssertCliff(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (2, 3, WorldTraversalKind.Cliff)),
                FirstArtRuinsCliffCatalog3D.NorthConnection,
                FirstArtRuinsCliffModule3D.EndCap,
                1);
            AssertCliff(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (3, 2, WorldTraversalKind.Cliff)),
                FirstArtRuinsCliffCatalog3D.EastConnection,
                FirstArtRuinsCliffModule3D.EndCap,
                2);
            AssertCliff(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (2, 1, WorldTraversalKind.Cliff)),
                FirstArtRuinsCliffCatalog3D.SouthConnection,
                FirstArtRuinsCliffModule3D.EndCap,
                3);
            AssertStraight(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (1, 2, WorldTraversalKind.Cliff),
                    (3, 2, WorldTraversalKind.Cliff)),
                FirstArtRuinsCliffCatalog3D.EastConnection |
                FirstArtRuinsCliffCatalog3D.WestConnection,
                0);
            AssertStraight(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (2, 1, WorldTraversalKind.Cliff),
                    (2, 3, WorldTraversalKind.Cliff)),
                FirstArtRuinsCliffCatalog3D.NorthConnection |
                FirstArtRuinsCliffCatalog3D.SouthConnection,
                1);
            AssertCliff(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (2, 3, WorldTraversalKind.Cliff),
                    (3, 2, WorldTraversalKind.Cliff),
                    (2, 1, WorldTraversalKind.Cliff)),
                7,
                FirstArtRuinsCliffModule3D.TopCap,
                0);
            AssertCliff(
                Map(5, 5,
                    (2, 2, WorldTraversalKind.Cliff),
                    (2, 3, WorldTraversalKind.Cliff),
                    (3, 2, WorldTraversalKind.Cliff),
                    (2, 1, WorldTraversalKind.Cliff),
                    (1, 2, WorldTraversalKind.Cliff)),
                15,
                FirstArtRuinsCliffModule3D.TopCap,
                0);

            var straightCells = new List<(int, int, WorldTraversalKind)>();
            for (int x = 0; x < 16; x++)
                straightCells.Add((x, 1, WorldTraversalKind.Cliff));
            FirstArtRuinsCliffModule3D[] straightModules = Project(
                    Map(16, 3, straightCells.ToArray()))
                .Cast<object>()
                .Where(value =>
                    Read<int>(value, "NeighborMask") ==
                    (FirstArtRuinsCliffCatalog3D.EastConnection |
                     FirstArtRuinsCliffCatalog3D.WestConnection))
                .Select(value => FirstArtRuinsCliffCatalog3D.Entries[
                    Read<int>(value, "CatalogIndex")].Module)
                .Distinct()
                .ToArray();
            CollectionAssert.AreEquivalent(
                new[]
                {
                    FirstArtRuinsCliffModule3D.StraightA,
                    FirstArtRuinsCliffModule3D.StraightB,
                },
                straightModules);
        }

        [TestCase(9, 0, -1, 1)]
        [TestCase(3, 1, 1, 1)]
        [TestCase(6, 2, 1, -1)]
        [TestCase(12, 3, -1, -1)]
        public void IDEA0004_CliffAdjacentArmsUseTheExistingDiagonalTruth(
            int mask,
            int quarterTurns,
            int diagonalX,
            int diagonalY)
        {
            List<(int x, int y, WorldTraversalKind traversal)> arms =
                CliffArms(mask);
            arms.Add((2, 2, WorldTraversalKind.Cliff));

            AssertCliff(
                Map(5, 5, arms.ToArray()),
                mask,
                FirstArtRuinsCliffModule3D.InnerCorner,
                quarterTurns);

            arms.Add((2 + diagonalX, 2 + diagonalY, WorldTraversalKind.Cliff));
            AssertCliff(
                Map(5, 5, arms.ToArray()),
                mask,
                FirstArtRuinsCliffModule3D.OuterCorner,
                quarterTurns);
        }

        [Test]
        public void IDEA0004_ProjectIsDeterministicAndChangesWithTheRuleMap()
        {
            WorldMapModel first = Map(
                6,
                5,
                (1, 1, WorldTraversalKind.Ruins),
                (2, 1, WorldTraversalKind.Cliff),
                (3, 1, WorldTraversalKind.Cliff),
                (3, 2, WorldTraversalKind.Cliff));
            WorldMapModel identical = Map(
                6,
                5,
                (1, 1, WorldTraversalKind.Ruins),
                (2, 1, WorldTraversalKind.Cliff),
                (3, 1, WorldTraversalKind.Cliff),
                (3, 2, WorldTraversalKind.Cliff));
            WorldMapModel changed = Map(
                6,
                5,
                (1, 1, WorldTraversalKind.Ruins),
                (2, 1, WorldTraversalKind.Cliff),
                (3, 1, WorldTraversalKind.Cliff),
                (4, 1, WorldTraversalKind.Cliff));

            string firstProjection = Fingerprint(Project(first));
            Assert.That(Fingerprint(Project(first)), Is.EqualTo(firstProjection));
            Assert.That(Fingerprint(Project(identical)), Is.EqualTo(firstProjection));
            Assert.That(Fingerprint(Project(changed)), Is.Not.EqualTo(firstProjection));
        }

        [Test]
        public void IDEA0004_ProjectCombinesMapperRotationAndApprovedCalibration()
        {
            WorldMapModel map = FilledMap(4, 4, WorldTraversalKind.Ruins);
            object placement = Project(map)[0];
            FirstArtRuinsCliffCatalogEntry3D entry =
                FirstArtRuinsCliffCatalog3D.Entries[
                    Read<int>(placement, "CatalogIndex")];
            var mapper = new PlanarCoordinateMapper3D(4, 4);
            Assert.That(mapper.TryCellToWorld(0, 0, 0f, out Vector3 world), Is.True);
            Matrix4x4 expected =
                Matrix4x4.TRS(
                    world,
                    Quaternion.Euler(0f, 90f, 0f),
                    Vector3.one) *
                Matrix4x4.TRS(
                    entry.ChildOffset,
                    Quaternion.identity,
                    entry.RootScale);

            AssertMatrix(Read<Matrix4x4>(placement, "WorldMatrix"), expected);
        }

        [Test]
        public void IDEA0004_ProjectDoesNotMutateAnyWorldCellField()
        {
            WorldMapModel map = GrayboxWorldLayout3D.CreateDefault();
            CellSnapshot[] before = Snapshot(map);

            IList placements = Project(map);

            Assert.That(placements.Count, Is.GreaterThan(0));
            CollectionAssert.AreEqual(before, Snapshot(map));
        }

        [Test]
        public void IDEA0004_DefaultSeed8128HasOnePlacementPerRuinsAndCliffRuleCell()
        {
            WorldMapModel map = GrayboxWorldLayout3D.CreateDefault();
            IList placements = Project(map);

            Assert.That(placements.Count, Is.EqualTo(108));
            Assert.That(
                placements.Cast<object>().Count(value =>
                    Read<object>(value, "Family").ToString() == "Ruins"),
                Is.EqualTo(76));
            Assert.That(
                placements.Cast<object>().Count(value =>
                    Read<object>(value, "Family").ToString() == "Cliff"),
                Is.EqualTo(32));
        }

        [Test]
        public void IDEA0004_ApprovedCalibrationKeepsEveryPlacementInsideTheCellFitGate()
        {
            IList placements = Project(
                FilledMap(16, 16, WorldTraversalKind.Ruins));

            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, 8),
                placements.Cast<object>()
                    .Select(value => Read<int>(value, "CatalogIndex"))
                    .Distinct());
            foreach (FirstArtRuinsCliffCatalogEntry3D entry in
                     FirstArtRuinsCliffCatalog3D.Entries)
            {
                Assert.That(entry.CalibratedBounds.x, Is.LessThanOrEqualTo(.900001f),
                    entry.StableId + " exceeds the X cell-fit gate.");
                Assert.That(entry.CalibratedBounds.z, Is.LessThanOrEqualTo(.900001f),
                    entry.StableId + " exceeds the Z cell-fit gate.");
            }
        }

        private static IList Project(WorldMapModel map)
        {
            Type layoutType = Type.GetType(LayoutTypeName, false);
            Assert.That(layoutType, Is.Not.Null,
                "IDEA-0004 deterministic layout type must exist before projection.");
            MethodInfo project = layoutType.GetMethod(
                "Project",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(WorldMapModel), typeof(PlanarCoordinateMapper3D) },
                null);
            Assert.That(project, Is.Not.Null);
            object result = project.Invoke(
                null,
                new object[]
                {
                    map,
                    new PlanarCoordinateMapper3D(map.Width, map.Height),
                });
            Assert.That(result, Is.InstanceOf<IList>());
            return (IList)result;
        }

        private static void AssertCliff(
            WorldMapModel map,
            int expectedMask,
            FirstArtRuinsCliffModule3D expectedModule,
            int expectedQuarterTurns)
        {
            object target = Project(map).Cast<object>().Single(value =>
                Read<int>(value, "CellX") == 2 &&
                Read<int>(value, "CellY") == 2);
            int catalogIndex = Read<int>(target, "CatalogIndex");
            Assert.That(Read<object>(target, "Family").ToString(), Is.EqualTo("Cliff"));
            Assert.That(Read<int>(target, "NeighborMask"), Is.EqualTo(expectedMask));
            Assert.That(Read<int>(target, "QuarterTurns"), Is.EqualTo(expectedQuarterTurns));
            Assert.That(
                FirstArtRuinsCliffCatalog3D.Entries[catalogIndex].Module,
                Is.EqualTo(expectedModule));
        }

        private static void AssertStraight(
            WorldMapModel map,
            int expectedMask,
            int expectedQuarterTurns)
        {
            object target = Project(map).Cast<object>().Single(value =>
                Read<int>(value, "CellX") == 2 &&
                Read<int>(value, "CellY") == 2);
            FirstArtRuinsCliffModule3D module =
                FirstArtRuinsCliffCatalog3D.Entries[
                    Read<int>(target, "CatalogIndex")].Module;
            Assert.That(
                module,
                Is.EqualTo(FirstArtRuinsCliffModule3D.StraightA)
                    .Or.EqualTo(FirstArtRuinsCliffModule3D.StraightB));
            Assert.That(Read<int>(target, "NeighborMask"), Is.EqualTo(expectedMask));
            Assert.That(Read<int>(target, "QuarterTurns"), Is.EqualTo(expectedQuarterTurns));
        }

        private static List<(int x, int y, WorldTraversalKind traversal)> CliffArms(
            int mask)
        {
            var result = new List<(int, int, WorldTraversalKind)>();
            if ((mask & FirstArtRuinsCliffCatalog3D.NorthConnection) != 0)
                result.Add((2, 3, WorldTraversalKind.Cliff));
            if ((mask & FirstArtRuinsCliffCatalog3D.EastConnection) != 0)
                result.Add((3, 2, WorldTraversalKind.Cliff));
            if ((mask & FirstArtRuinsCliffCatalog3D.SouthConnection) != 0)
                result.Add((2, 1, WorldTraversalKind.Cliff));
            if ((mask & FirstArtRuinsCliffCatalog3D.WestConnection) != 0)
                result.Add((1, 2, WorldTraversalKind.Cliff));
            return result;
        }

        private static WorldMapModel FilledMap(
            int width,
            int height,
            WorldTraversalKind traversal)
        {
            var cells = new WorldCell[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = Cell(traversal);
            return new WorldMapModel(cells);
        }

        private static WorldMapModel Map(
            int width,
            int height,
            params (int x, int y, WorldTraversalKind traversal)[] overrides)
        {
            var cells = new WorldCell[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = Cell(WorldTraversalKind.Open);
            foreach ((int x, int y, WorldTraversalKind traversal) item in overrides)
                cells[item.x, item.y] = Cell(item.traversal);
            return new WorldMapModel(cells);
        }

        private static WorldCell Cell(WorldTraversalKind traversal)
        {
            return new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                traversal);
        }

        private static string CellKey(object placement)
        {
            return Read<int>(placement, "CellX") + "," +
                   Read<int>(placement, "CellY");
        }

        private static string Fingerprint(IList placements)
        {
            return string.Join(
                "|",
                placements.Cast<object>().Select(value =>
                {
                    Matrix4x4 matrix = Read<Matrix4x4>(value, "WorldMatrix");
                    string matrixFingerprint = string.Join(
                        "/",
                        Enumerable.Range(0, 16).Select(index =>
                            matrix[index].ToString("R", CultureInfo.InvariantCulture)));
                    return string.Join(
                        ",",
                        Read<object>(value, "Family"),
                        Read<int>(value, "CatalogIndex"),
                        Read<int>(value, "CellX"),
                        Read<int>(value, "CellY"),
                        Read<int>(value, "NeighborMask"),
                        Read<int>(value, "QuarterTurns"),
                        matrixFingerprint);
                }));
        }

        private static CellSnapshot[] Snapshot(WorldMapModel map)
        {
            var result = new CellSnapshot[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                WorldCell cell = map.Get(x, y);
                result[y * map.Width + x] = new CellSnapshot(
                    cell.Terrain,
                    cell.ResourceId,
                    cell.ResourceAmount,
                    cell.Traversal);
            }
            return result;
        }

        private static T Read<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName + " property is missing.");
            return (T)property.GetValue(instance);
        }

        private static void AssertMatrix(Matrix4x4 actual, Matrix4x4 expected)
        {
            for (int index = 0; index < 16; index++)
                Assert.That(actual[index], Is.EqualTo(expected[index]).Within(0.000001f),
                    "Matrix element " + index + " differs.");
        }

        private readonly struct CellSnapshot
        {
            public CellSnapshot(
                TerrainKind terrain,
                string resourceId,
                int resourceAmount,
                WorldTraversalKind traversal)
            {
                Terrain = terrain;
                ResourceId = resourceId;
                ResourceAmount = resourceAmount;
                Traversal = traversal;
            }

            private TerrainKind Terrain { get; }
            private string ResourceId { get; }
            private int ResourceAmount { get; }
            private WorldTraversalKind Traversal { get; }

            public override bool Equals(object obj)
            {
                return obj is CellSnapshot other &&
                       Terrain == other.Terrain &&
                       string.Equals(ResourceId, other.ResourceId, StringComparison.Ordinal) &&
                       ResourceAmount == other.ResourceAmount &&
                       Traversal == other.Traversal;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)Terrain;
                    hash = (hash * 397) ^ (ResourceId == null ? 0 : ResourceId.GetHashCode());
                    hash = (hash * 397) ^ ResourceAmount;
                    hash = (hash * 397) ^ (int)Traversal;
                    return hash;
                }
            }
        }
    }
}
