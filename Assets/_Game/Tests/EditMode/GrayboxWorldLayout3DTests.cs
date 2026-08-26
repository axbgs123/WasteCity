using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxWorldLayout3DTests
    {
        private const int StartX = 10;
        private const int StartY = 9;
        private const int RiftX = 32;
        private const int RiftY = 35;
        private const int EasternChokeX = 52;
        private const int EasternChokeY = 25;

        [Test]
        public void IDEA0019_Seed8128V2WorldMatchesStableGoldenHash()
        {
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();

            Assert.That(
                StableContentHash(world),
                Is.EqualTo(
                    "2f0ecd374ad3a1bf6fd50564d949741618c7ce1b72bc6619f67acda632b1e6fd"));
        }

        [Test]
        public void IDEA0019_ConstantsKeepApprovedDimensionsAndSeed()
        {
            Assert.That(GrayboxWorldLayout3D.DefaultSeed, Is.EqualTo(8128));
            Assert.That(GrayboxWorldLayout3D.WorldWidth, Is.EqualTo(64));
            Assert.That(GrayboxWorldLayout3D.WorldHeight, Is.EqualTo(48));
        }
        [Test]
        public void IDEA0019_DefaultWorldCoversAllQuadrantsAndRetiresSparseOuterRing()
        {
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            Assert.That(world.Width, Is.EqualTo(64));
            Assert.That(world.Height, Is.EqualTo(48));
            Assert.That(world.Width * world.Height, Is.EqualTo(3072));
            for (var quadrantX = 0; quadrantX < 2; quadrantX++)
            for (var quadrantY = 0; quadrantY < 2; quadrantY++)
            {
                int minimumX = quadrantX * world.Width / 2;
                int maximumX = (quadrantX + 1) * world.Width / 2;
                int minimumY = quadrantY * world.Height / 2;
                int maximumY = (quadrantY + 1) * world.Height / 2;
                Assert.That(
                    CountCells(
                        world,
                        minimumX,
                        maximumX,
                        minimumY,
                        maximumY,
                        cell => cell.HasResource),
                    Is.GreaterThan(0),
                    $"Quadrant {quadrantX},{quadrantY} has no resource region.");
            }
            AssertOuterBandHasContent(world, 0, 16, 0, world.Height, "west");
            AssertOuterBandHasContent(world, 48, 64, 0, world.Height, "east");
            AssertOuterBandHasContent(world, 0, world.Width, 0, 12, "south");
            AssertOuterBandHasContent(world, 0, world.Width, 36, 48, "north");
        }
        [Test]
        public void IDEA0019_DefaultWorldHasApprovedResourceQuotasAndSafeDeposits()
        {
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var safeIron = 0;
            var safeStone = 0;
            var riftIron = 0;
            for (var x = 0; x < world.Width; x++)
            for (var y = 0; y < world.Height; y++)
            {
                WorldCell cell = world.Get(x, y);
                if (!cell.HasResource)
                    continue;
                counts.TryGetValue(cell.ResourceId, out int count);
                counts[cell.ResourceId] = count + 1;
                Assert.That(cell.Traversal, Is.EqualTo(WorldTraversalKind.Open),
                    $"Resource {cell.ResourceId} at {x},{y} is not Open.");
                Assert.That(cell.ResourceAmount, Is.GreaterThan(0));
                int startDistance = ChebyshevDistance(x, y, StartX, StartY);
                if (startDistance <= 8 && cell.ResourceId == ResourceIds.Iron &&
                    cell.ResourceAmount == 240)
                    safeIron++;
                if (startDistance <= 8 && cell.ResourceId == ResourceIds.Stone &&
                    cell.ResourceAmount == 240)
                    safeStone++;
                if (ChebyshevDistance(x, y, RiftX, RiftY) <= 8 &&
                    cell.ResourceId == ResourceIds.Iron &&
                    cell.ResourceAmount == 480)
                    riftIron++;
            }
            Assert.That(world.ResourceNodeCount, Is.EqualTo(24));
            AssertResourceCount(counts, ResourceIds.Iron, 8);
            AssertResourceCount(counts, ResourceIds.Stone, 4);
            AssertResourceCount(counts, ResourceIds.EnergyCrystal, 4);
            AssertResourceCount(counts, ResourceIds.Water, 4);
            AssertResourceCount(counts, ResourceIds.Biomass, 4);
            Assert.That(counts.Count, Is.EqualTo(5));
            Assert.That(safeIron, Is.EqualTo(2));
            Assert.That(safeStone, Is.EqualTo(1));
            Assert.That(riftIron, Is.EqualTo(3));
        }
        [Test]
        public void IDEA0019_StartProtectionIsNineByNineOpenAndDeployable()
        {
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            for (var x = StartX - 4; x <= StartX + 4; x++)
            for (var y = StartY - 4; y <= StartY + 4; y++)
            {
                WorldCell cell = world.Get(x, y);
                Assert.That(cell.Traversal, Is.EqualTo(WorldTraversalKind.Open),
                    $"Start protection is blocked at {x},{y}.");
                Assert.That(cell.HasResource, Is.False,
                    $"Start protection contains a resource at {x},{y}.");
                Assert.That(CityTerrainRules.SupportsDeployment(cell), Is.True,
                    $"Start protection is not deployable at {x},{y}.");
            }
            Assert.That(
                CityDeploymentRules.Validate(world, StartX, StartY),
                Is.EqualTo(CityDeploymentFailure.None));
        }
        [Test]
        public void IDEA0019_TerrainFormsMacroRegionsWithinApprovedCoverage()
        {
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            int total = world.Width * world.Height;
            AssertTerrainCoverage(world, TerrainKind.Wasteland, total,
                .50f, .60f, minimumLargestComponent: 512);
            AssertTerrainCoverage(world, TerrainKind.Rocky, total,
                .18f, .26f, minimumLargestComponent: 128);
            AssertTerrainCoverage(world, TerrainKind.Crystal, total,
                .08f, .15f, minimumLargestComponent: 128);
            AssertTerrainCoverage(world, TerrainKind.Wetland, total,
                .08f, .15f, minimumLargestComponent: 128);
            int isolatedSpecialCells =
                CountComponentsOfSize(world, TerrainKind.Rocky, 1) +
                CountComponentsOfSize(world, TerrainKind.Crystal, 1) +
                CountComponentsOfSize(world, TerrainKind.Wetland, 1);
            Assert.That(isolatedSpecialCells, Is.LessThanOrEqualTo(2),
                "Macro terrain still contains salt-and-pepper single cells.");
        }
        [Test]
        public void IDEA0019_TraversalFormsLimitedContinuousRegions()
        {
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            AssertTraversalComponents(world, WorldTraversalKind.DeepWater,
                minimumCount: 1, maximumCount: 2, minimumSize: 12);
            AssertTraversalComponents(world, WorldTraversalKind.Ruins,
                minimumCount: 3, maximumCount: 5, minimumSize: 6);
            AssertTraversalComponents(world, WorldTraversalKind.Cliff,
                minimumCount: 2, maximumCount: 4, minimumSize: 6);
        }
        [Test]
        public void IDEA0019_ApprovedCorridorsAndCriticalDestinationsArePassable()
        {
            WorldMapModel world = GrayboxWorldLayout3D.CreateDefault();
            var southernRoute = new[]
            {
                new GridCell(StartX, StartY),
                new GridCell(31, StartY),
                new GridCell(31, RiftY),
                new GridCell(RiftX, RiftY),
            };
            var northwesternRoute = new[]
            {
                new GridCell(StartX, StartY),
                new GridCell(StartX, 26),
                new GridCell(34, 26),
                new GridCell(34, RiftY),
                new GridCell(RiftX, RiftY),
            };
            AssertThreeWideCorridor(world, southernRoute, "southern route");
            AssertThreeWideCorridor(
                world,
                northwesternRoute,
                "northwestern route");
            AssertPathfinderReaches(world, RiftX, RiftY, "crystal rift");
            AssertPathfinderReaches(
                world,
                EasternChokeX,
                EasternChokeY,
                "eastern choke");
        }
        [Test]
        public void Create_WithSameSeed_IsCellForCellDeterministic()
        {
            WorldMapModel first = GrayboxWorldLayout3D.Create(37);
            WorldMapModel second = GrayboxWorldLayout3D.Create(37);
            AssertMapsEqual(first, second);
        }
        private static void AssertThreeWideCorridor(
            WorldMapModel world,
            IReadOnlyList<GridCell> points,
            string name)
        {
            for (var segment = 1; segment < points.Count; segment++)
            {
                GridCell from = points[segment - 1];
                GridCell to = points[segment];
                Assert.That(from.X == to.X || from.Y == to.Y, Is.True,
                    $"{name} segment {segment} is not orthogonal.");
                if (from.Y == to.Y)
                {
                    for (int x = Math.Min(from.X, to.X);
                         x <= Math.Max(from.X, to.X); x++)
                    for (int y = from.Y - 1; y <= from.Y + 1; y++)
                        AssertPassable(world, x, y, name);
                }
                else
                {
                    for (int y = Math.Min(from.Y, to.Y);
                         y <= Math.Max(from.Y, to.Y); y++)
                    for (int x = from.X - 1; x <= from.X + 1; x++)
                        AssertPassable(world, x, y, name);
                }
            }
        }

        private static string StableContentHash(WorldMapModel world)
        {
            var canonical = new StringBuilder(
                world.Width * world.Height * 32);
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
            {
                WorldCell cell = world.Get(x, y);
                canonical.Append(y.ToString(CultureInfo.InvariantCulture));
                canonical.Append('/');
                canonical.Append(x.ToString(CultureInfo.InvariantCulture));
                canonical.Append('|');
                canonical.Append(((int)cell.Terrain).ToString(
                    CultureInfo.InvariantCulture));
                canonical.Append('|');
                canonical.Append(((int)cell.Traversal).ToString(
                    CultureInfo.InvariantCulture));
                canonical.Append('|');
                canonical.Append(cell.ResourceId ?? string.Empty);
                canonical.Append('|');
                canonical.Append(cell.ResourceAmount.ToString(
                    CultureInfo.InvariantCulture));
                canonical.Append('\n');
            }
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical.ToString()));
                var result = new StringBuilder(digest.Length * 2);
                for (var index = 0; index < digest.Length; index++)
                {
                    result.Append(digest[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return result.ToString();
            }
        }
        private static void AssertPassable(
            WorldMapModel world,
            int x,
            int y,
            string routeName)
        {
            Assert.That(x, Is.InRange(0, world.Width - 1));
            Assert.That(y, Is.InRange(0, world.Height - 1));
            Assert.That(CityTerrainRules.IsPassable(world.Get(x, y)), Is.True,
                $"{routeName} is blocked at {x},{y}.");
        }
        private static void AssertPathfinderReaches(
            WorldMapModel world,
            int destinationX,
            int destinationY,
            string destinationName)
        {
            Assert.That(
                CityPathfinder.TryFindPath(world, StartX, StartY,
                    destinationX, destinationY, out WorldGridPoint[] path),
                Is.True,
                $"The {destinationName} is not reachable.");
            Assert.That(path, Is.Not.Empty);
        }
        private static void AssertTerrainCoverage(
            WorldMapModel world,
            TerrainKind terrain,
            int total,
            float minimumFraction,
            float maximumFraction,
            int minimumLargestComponent)
        {
            int count = CountCells(world, 0, world.Width, 0, world.Height,
                cell => cell.Terrain == terrain);
            Assert.That(count, Is.InRange(
                (int)Math.Ceiling(total * minimumFraction),
                (int)Math.Floor(total * maximumFraction)),
                $"{terrain} is outside approved macro coverage.");
            List<int> components = ComponentSizes(
                world,
                cell => cell.Terrain == terrain);
            Assert.That(components, Is.Not.Empty);
            Assert.That(Maximum(components),
                Is.GreaterThanOrEqualTo(minimumLargestComponent),
                $"{terrain} lacks a readable macro region.");
        }
        private static void AssertTraversalComponents(
            WorldMapModel world,
            WorldTraversalKind traversal,
            int minimumCount,
            int maximumCount,
            int minimumSize)
        {
            List<int> components = ComponentSizes(
                world,
                cell => cell.Traversal == traversal);
            Assert.That(components.Count, Is.InRange(minimumCount, maximumCount),
                $"{traversal} has an invalid number of regions.");
            foreach (int size in components)
                Assert.That(size, Is.GreaterThanOrEqualTo(minimumSize),
                    $"{traversal} contains a scattered fragment.");
        }
        private static List<int> ComponentSizes(
            WorldMapModel world,
            Predicate<WorldCell> matches)
        {
            var visited = new bool[world.Width, world.Height];
            var result = new List<int>();
            var queue = new Queue<GridCell>();
            int[] offsetX = { 1, -1, 0, 0 };
            int[] offsetY = { 0, 0, 1, -1 };
            for (var startX = 0; startX < world.Width; startX++)
            for (var startY = 0; startY < world.Height; startY++)
            {
                if (visited[startX, startY] ||
                    !matches(world.Get(startX, startY)))
                    continue;
                visited[startX, startY] = true;
                queue.Enqueue(new GridCell(startX, startY));
                var size = 0;
                while (queue.Count > 0)
                {
                    GridCell current = queue.Dequeue();
                    size++;
                    for (var direction = 0; direction < 4; direction++)
                    {
                        int x = current.X + offsetX[direction];
                        int y = current.Y + offsetY[direction];
                        if (x < 0 || y < 0 || x >= world.Width ||
                            y >= world.Height || visited[x, y] ||
                            !matches(world.Get(x, y)))
                            continue;
                        visited[x, y] = true;
                        queue.Enqueue(new GridCell(x, y));
                    }
                }
                result.Add(size);
            }
            return result;
        }
        private static int CountComponentsOfSize(
            WorldMapModel world,
            TerrainKind terrain,
            int expectedSize)
        {
            List<int> components = ComponentSizes(
                world,
                cell => cell.Terrain == terrain);
            var count = 0;
            foreach (int size in components)
            {
                if (size == expectedSize)
                    count++;
            }
            return count;
        }
        private static void AssertOuterBandHasContent(
            WorldMapModel world,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY,
            string bandName)
        {
            Assert.That(
                CountCells(world, minimumX, maximumX, minimumY, maximumY,
                    cell => cell.Terrain != TerrainKind.Wasteland ||
                            cell.Traversal != WorldTraversalKind.Open ||
                            cell.HasResource),
                Is.GreaterThan(0),
                $"The {bandName} band still behaves like the legacy ring.");
        }
        private static int CountCells(
            WorldMapModel world,
            int minimumX,
            int maximumX,
            int minimumY,
            int maximumY,
            Predicate<WorldCell> matches)
        {
            var count = 0;
            for (var x = minimumX; x < maximumX; x++)
            for (var y = minimumY; y < maximumY; y++)
            {
                if (matches(world.Get(x, y)))
                    count++;
            }
            return count;
        }
        private static int ChebyshevDistance(int x, int y, int otherX, int otherY)
        {
            return Math.Max(Math.Abs(x - otherX), Math.Abs(y - otherY));
        }
        private static int Maximum(IReadOnlyList<int> values)
        {
            var maximum = int.MinValue;
            for (var index = 0; index < values.Count; index++)
                maximum = Math.Max(maximum, values[index]);
            return maximum;
        }
        private static void AssertResourceCount(
            IReadOnlyDictionary<string, int> counts,
            string resourceId,
            int expected)
        {
            counts.TryGetValue(resourceId, out int actual);
            Assert.That(actual, Is.EqualTo(expected),
                $"Unexpected quota for {resourceId}.");
        }
        private static void AssertMapsEqual(
            WorldMapModel expected,
            WorldMapModel actual)
        {
            Assert.That(actual.Width, Is.EqualTo(expected.Width));
            Assert.That(actual.Height, Is.EqualTo(expected.Height));
            Assert.That(actual.ResourceNodeCount,
                Is.EqualTo(expected.ResourceNodeCount));
            for (var x = 0; x < expected.Width; x++)
            for (var y = 0; y < expected.Height; y++)
            {
                WorldCell expectedCell = expected.Get(x, y);
                WorldCell actualCell = actual.Get(x, y);
                Assert.That(actualCell.Terrain, Is.EqualTo(expectedCell.Terrain));
                Assert.That(actualCell.ResourceId, Is.EqualTo(expectedCell.ResourceId));
                Assert.That(actualCell.ResourceAmount,
                    Is.EqualTo(expectedCell.ResourceAmount));
                Assert.That(actualCell.Traversal, Is.EqualTo(expectedCell.Traversal));
            }
        }
        private readonly struct GridCell
        {
            public GridCell(int x, int y) { X = x; Y = y; }
            public int X { get; }
            public int Y { get; }
        }
    }
}
