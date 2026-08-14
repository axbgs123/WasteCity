using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtRuinsCliffLayoutTests
    {
        private const float CalibrationTolerance = 0.0000002f;
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
                    entry.RootScale) *
                ExpectedSourceImportMatrix();

            AssertMatrix(Read<Matrix4x4>(placement, "WorldMatrix"), expected);
        }

        [Test]
        public void IDEA0004_UnityRawMeshTruthCorrectsOffsetsAndRejectsImportMatrixRegressions()
        {
            var evidence = new CalibrationEvidence
            {
                requirement = "IDEA-0004",
                unity = Application.unityVersion,
                head = ReadHead(),
                tolerance = CalibrationTolerance,
                sourceImportQuaternion = new[]
                {
                    -0.7071068f, 0f, 0f, 0.7071067f,
                },
                sourceImportMatrixRowMajor = MatrixRowMajor(
                    ExpectedSourceImportMatrix()),
                entries = new CalibrationEntryEvidence[
                    FirstArtRuinsCliffCatalog3D.Entries.Count],
            };

            bool allPass = true;
            for (int index = 0;
                 index < FirstArtRuinsCliffCatalog3D.Entries.Count;
                 index++)
            {
                FirstArtRuinsCliffCatalogEntry3D entry =
                    FirstArtRuinsCliffCatalog3D.Entries[index];
                GameObject importedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(
                    entry.FbxPath);
                Assert.That(importedRoot, Is.Not.Null, entry.StableId);
                MeshFilter[] filters = importedRoot.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.EqualTo(1), entry.StableId);
                Mesh rawMesh = filters[0].sharedMesh;
                Assert.That(rawMesh, Is.Not.Null, entry.StableId);

                Bounds importedAndScaled = TransformBounds(
                    Matrix4x4.Scale(entry.RootScale) *
                    ExpectedSourceImportMatrix(),
                    rawMesh.bounds);
                Vector3 derivedOffset = new Vector3(
                    -importedAndScaled.center.x,
                    -importedAndScaled.min.y,
                    -importedAndScaled.center.z);
                Matrix4x4 correctedMatrix =
                    Matrix4x4.Translate(derivedOffset) *
                    Matrix4x4.Scale(entry.RootScale) *
                    ExpectedSourceImportMatrix();
                Bounds correctedBounds = TransformBounds(
                    correctedMatrix,
                    rawMesh.bounds);
                Bounds catalogBounds = TransformBounds(
                    Matrix4x4.Translate(entry.ChildOffset) *
                    Matrix4x4.Scale(entry.RootScale) *
                    ExpectedSourceImportMatrix(),
                    rawMesh.bounds);

                Vector3 oldSignOffset = new Vector3(
                    -derivedOffset.x,
                    derivedOffset.y,
                    -derivedOffset.z);
                Bounds oldSignBounds = TransformBounds(
                    Matrix4x4.Translate(oldSignOffset) *
                    Matrix4x4.Scale(entry.RootScale) *
                    ExpectedSourceImportMatrix(),
                    rawMesh.bounds);
                Bounds missingImportBounds = TransformBounds(
                    Matrix4x4.Translate(derivedOffset) *
                    Matrix4x4.Scale(entry.RootScale),
                    rawMesh.bounds);
                Bounds doubledImportBounds = TransformBounds(
                    correctedMatrix * ExpectedSourceImportMatrix(),
                    rawMesh.bounds);

                bool finite = IsFinite(rawMesh.bounds.center) &&
                              IsFinite(rawMesh.bounds.size) &&
                              IsFinite(derivedOffset) &&
                              IsFinite(correctedBounds.center) &&
                              IsFinite(correctedBounds.size);
                bool derivedPass = finite && PassCalibration(
                    correctedBounds,
                    entry.CalibratedBounds);
                bool catalogOffsetPass = VectorWithin(
                    entry.ChildOffset,
                    derivedOffset,
                    CalibrationTolerance);
                bool catalogPass = finite &&
                                   catalogOffsetPass &&
                                   PassCalibration(
                                       catalogBounds,
                                       entry.CalibratedBounds);
                bool oldSignRejected = !PassCalibration(
                    oldSignBounds,
                    entry.CalibratedBounds);
                bool missingImportRejected = !PassCalibration(
                    missingImportBounds,
                    entry.CalibratedBounds);
                bool doubledImportRejected = !PassCalibration(
                    doubledImportBounds,
                    entry.CalibratedBounds);
                bool entryPass = derivedPass &&
                                 catalogPass &&
                                 oldSignRejected &&
                                 missingImportRejected &&
                                 doubledImportRejected;
                allPass &= entryPass;

                evidence.entries[index] = new CalibrationEntryEvidence
                {
                    stableId = entry.StableId,
                    rawCenter = Values(rawMesh.bounds.center),
                    rawSize = Values(rawMesh.bounds.size),
                    rootScale = Values(entry.RootScale),
                    derivedChildOffset = Values(derivedOffset),
                    catalogChildOffset = Values(entry.ChildOffset),
                    finalCenter = Values(catalogBounds.center),
                    finalMinY = catalogBounds.min.y,
                    finalSize = Values(catalogBounds.size),
                    expectedSize = Values(entry.CalibratedBounds),
                    derivedPass = derivedPass,
                    catalogOffsetPass = catalogOffsetPass,
                    oldSignRejected = oldSignRejected,
                    missingImportRejected = missingImportRejected,
                    doubledImportRejected = doubledImportRejected,
                    pass = entryPass,
                };
            }

            evidence.allPass = allPass;
            WriteEvidenceIfRequested(evidence);
            Assert.That(evidence.entries.Length, Is.EqualTo(14));
            foreach (CalibrationEntryEvidence entry in evidence.entries)
            {
                Assert.That(entry.derivedPass, Is.True,
                    entry.stableId + " corrected raw bounds failed.");
                Assert.That(entry.catalogOffsetPass, Is.True,
                    entry.stableId + " Catalog offset differs from raw Mesh truth.");
                Assert.That(entry.oldSignRejected, Is.True,
                    entry.stableId + " accepted the obsolete horizontal signs.");
                Assert.That(entry.missingImportRejected, Is.True,
                    entry.stableId + " accepted a missing import matrix.");
                Assert.That(entry.doubledImportRejected, Is.True,
                    entry.stableId + " accepted a doubled import matrix.");
                Assert.That(entry.pass, Is.True,
                    entry.stableId + " correction evidence failed.");
            }
            Assert.That(allPass, Is.True,
                "Unity raw Mesh correction evidence contains failed entries.");
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
            IList placements = (IList)result;
            var mapper = new PlanarCoordinateMapper3D(map.Width, map.Height);
            foreach (object placement in placements)
            {
                int cellX = Read<int>(placement, "CellX");
                int cellY = Read<int>(placement, "CellY");
                int quarterTurns = Read<int>(placement, "QuarterTurns");
                FirstArtRuinsCliffCatalogEntry3D entry =
                    FirstArtRuinsCliffCatalog3D.Entries[
                        Read<int>(placement, "CatalogIndex")];
                Assert.That(
                    mapper.TryCellToWorld(cellX, cellY, 0f, out Vector3 world),
                    Is.True);
                Matrix4x4 expected =
                    Matrix4x4.TRS(
                        world,
                        Quaternion.Euler(0f, quarterTurns * 90f, 0f),
                        Vector3.one) *
                    Matrix4x4.Translate(entry.ChildOffset) *
                    Matrix4x4.Scale(entry.RootScale) *
                    Read<Matrix4x4>(entry, "SourceImportMatrix");
                AssertMatrix(
                    Read<Matrix4x4>(placement, "WorldMatrix"),
                    expected,
                    CalibrationTolerance,
                    entry.StableId);
            }
            return placements;
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
            AssertMatrix(actual, expected, 0.000001f, "Placement");
        }

        private static void AssertMatrix(
            Matrix4x4 actual,
            Matrix4x4 expected,
            float tolerance,
            string context)
        {
            for (int index = 0; index < 16; index++)
            {
                Assert.That(actual[index], Is.EqualTo(expected[index]).Within(tolerance),
                    context + " matrix element " + index + " differs.");
            }
        }

        private static Matrix4x4 ExpectedSourceImportMatrix()
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = 1f;
            matrix.m01 = 0f;
            matrix.m02 = 0f;
            matrix.m03 = 0f;
            matrix.m10 = 0f;
            matrix.m11 = -0.00000011920929f;
            matrix.m12 = 0.99999994f;
            matrix.m13 = 0f;
            matrix.m20 = 0f;
            matrix.m21 = -0.99999994f;
            matrix.m22 = -0.00000011920929f;
            matrix.m23 = 0f;
            matrix.m30 = 0f;
            matrix.m31 = 0f;
            matrix.m32 = 0f;
            matrix.m33 = 1f;
            return matrix;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds source)
        {
            Vector3 sourceMin = source.min;
            Vector3 sourceMax = source.max;
            Vector3 first = matrix.MultiplyPoint3x4(sourceMin);
            var result = new Bounds(first, Vector3.zero);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
            for (int z = 0; z < 2; z++)
            {
                result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                    x == 0 ? sourceMin.x : sourceMax.x,
                    y == 0 ? sourceMin.y : sourceMax.y,
                    z == 0 ? sourceMin.z : sourceMax.z)));
            }
            return result;
        }

        private static bool PassCalibration(Bounds actual, Vector3 expectedSize)
        {
            return Mathf.Abs(actual.center.x) <= CalibrationTolerance &&
                   Mathf.Abs(actual.center.z) <= CalibrationTolerance &&
                   Mathf.Abs(actual.min.y) <= CalibrationTolerance &&
                   VectorWithin(actual.size, expectedSize, CalibrationTolerance);
        }

        private static bool VectorWithin(
            Vector3 actual,
            Vector3 expected,
            float tolerance)
        {
            return Mathf.Abs(actual.x - expected.x) <= tolerance &&
                   Mathf.Abs(actual.y - expected.y) <= tolerance &&
                   Mathf.Abs(actual.z - expected.z) <= tolerance;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float[] Values(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private static float[] MatrixRowMajor(Matrix4x4 matrix)
        {
            return new[]
            {
                matrix.m00, matrix.m01, matrix.m02, matrix.m03,
                matrix.m10, matrix.m11, matrix.m12, matrix.m13,
                matrix.m20, matrix.m21, matrix.m22, matrix.m23,
                matrix.m30, matrix.m31, matrix.m32, matrix.m33,
            };
        }

        private static void WriteEvidenceIfRequested(CalibrationEvidence evidence)
        {
            string output = Environment.GetEnvironmentVariable(
                "WASTECITY_RUINS_CLIFF_CORRECTED_CALIBRATION");
            if (string.IsNullOrWhiteSpace(output))
                return;

            string directory = Path.GetDirectoryName(output);
            Assert.That(directory, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(directory);
            string temporary = output + ".tmp." + Guid.NewGuid().ToString("N");
            string backup = output + ".bak." + Guid.NewGuid().ToString("N");
            try
            {
                string json = JsonUtility.ToJson(evidence, true) + "\n";
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(output))
                {
                    File.Replace(temporary, output, backup);
                    if (File.Exists(backup))
                        File.Delete(backup);
                }
                else
                {
                    File.Move(temporary, output);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
                if (File.Exists(backup))
                    File.Delete(backup);
            }
        }

        private static string ReadHead()
        {
            string repository = Directory.GetParent(Application.dataPath).FullName;
            string gitDirectory = Path.Combine(repository, ".git");
            if (File.Exists(gitDirectory))
            {
                string marker = File.ReadAllText(gitDirectory).Trim();
                const string gitDirectoryPrefix = "gitdir: ";
                if (marker.StartsWith(gitDirectoryPrefix, StringComparison.Ordinal))
                {
                    string value = marker.Substring(gitDirectoryPrefix.Length);
                    gitDirectory = Path.GetFullPath(Path.IsPathRooted(value)
                        ? value
                        : Path.Combine(repository, value));
                }
            }
            string headPath = Path.Combine(gitDirectory, "HEAD");
            if (!File.Exists(headPath))
                return "unknown";
            string head = File.ReadAllText(headPath).Trim();
            const string prefix = "ref: ";
            if (!head.StartsWith(prefix, StringComparison.Ordinal))
                return head;
            string reference = head.Substring(prefix.Length);
            string referencePath = Path.Combine(
                gitDirectory,
                reference.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(referencePath))
                return File.ReadAllText(referencePath).Trim();

            string commonDirectoryPath = Path.Combine(gitDirectory, "commondir");
            if (File.Exists(commonDirectoryPath))
            {
                string commonValue = File.ReadAllText(commonDirectoryPath).Trim();
                string commonDirectory = Path.GetFullPath(Path.IsPathRooted(commonValue)
                    ? commonValue
                    : Path.Combine(gitDirectory, commonValue));
                referencePath = Path.Combine(
                    commonDirectory,
                    reference.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(referencePath))
                    return File.ReadAllText(referencePath).Trim();
            }
            return head;
        }

        [Serializable]
        private sealed class CalibrationEvidence
        {
            public string requirement;
            public string unity;
            public string head;
            public float tolerance;
            public float[] sourceImportQuaternion;
            public float[] sourceImportMatrixRowMajor;
            public CalibrationEntryEvidence[] entries;
            public bool allPass;
        }

        [Serializable]
        private sealed class CalibrationEntryEvidence
        {
            public string stableId;
            public float[] rawCenter;
            public float[] rawSize;
            public float[] rootScale;
            public float[] derivedChildOffset;
            public float[] catalogChildOffset;
            public float[] finalCenter;
            public float finalMinY;
            public float[] finalSize;
            public float[] expectedSize;
            public bool derivedPass;
            public bool catalogOffsetPass;
            public bool oldSignRejected;
            public bool missingImportRejected;
            public bool doubledImportRejected;
            public bool pass;
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
