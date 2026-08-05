using System;
using NUnit.Framework;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class CityPathfinderTests
    {
        [Test]
        public void PathDetoursAroundCliffAndIncludesDestination()
        {
            WorldCell open = Open();
            WorldCell cliff = Blocked();
            var cells = new[,]
            {
                { open, open, open },
                { open, cliff, open },
                { open, open, open }
            };

            Assert.That(
                CityPathfinder.TryFindPath(
                    new WorldMapModel(cells),
                    0,
                    1,
                    2,
                    1,
                    out WorldGridPoint[] path),
                Is.True);
            Assert.That(path.Length, Is.EqualTo(4));
            Assert.That(path[path.Length - 1].X, Is.EqualTo(2));
            Assert.That(path[path.Length - 1].Y, Is.EqualTo(1));
            Assert.That(
                Array.Exists(path, point => point.X == 1 && point.Y == 1),
                Is.False);
        }

        [Test]
        public void UnreachableDestinationReturnsFalseAndEmptyPath()
        {
            WorldCell open = Open();
            WorldCell cliff = Blocked();
            var cells = new[,]
            {
                { open, open, open },
                { cliff, cliff, cliff },
                { open, open, open }
            };

            Assert.That(
                CityPathfinder.TryFindPath(
                    new WorldMapModel(cells),
                    0,
                    0,
                    2,
                    0,
                    out WorldGridPoint[] path),
                Is.False);
            Assert.That(path, Is.Empty);
        }

        [Test]
        public void EqualDistanceChoiceAvoidsSlowWetland()
        {
            WorldCell open = Open();
            WorldCell wetland = new WorldCell(TerrainKind.Wetland, null, 0);
            var cells = new[,]
            {
                { open, open, open },
                { open, wetland, open },
                { open, wetland, open },
                { open, wetland, open },
                { open, open, open }
            };

            Assert.That(
                CityPathfinder.TryFindPath(
                    new WorldMapModel(cells),
                    0,
                    1,
                    4,
                    1,
                    out WorldGridPoint[] path),
                Is.True);
            Assert.That(path.Length, Is.EqualTo(6));
            Assert.That(
                Array.Exists(
                    path,
                    point => point.Y == 1 && point.X > 0 && point.X < 4),
                Is.False);
        }

        [Test]
        public void InvalidOrBlockedEndpointIsRejected()
        {
            WorldCell open = Open();
            WorldCell cliff = Blocked();
            var map = new WorldMapModel(new[,]
            {
                { open, open },
                { open, cliff }
            });

            Assert.That(
                CityPathfinder.TryFindPath(
                    map,
                    -1,
                    0,
                    0,
                    0,
                    out WorldGridPoint[] outsidePath),
                Is.False);
            Assert.That(outsidePath, Is.Empty);
            Assert.That(
                CityPathfinder.TryFindPath(
                    map,
                    0,
                    0,
                    1,
                    1,
                    out WorldGridPoint[] blockedPath),
                Is.False);
            Assert.That(blockedPath, Is.Empty);
        }

        [Test]
        public void StartAlreadyAtDestinationReturnsSuccessfulEmptyPath()
        {
            var map = new WorldMapModel(new[,] { { Open() } });

            Assert.That(
                CityPathfinder.TryFindPath(
                    map,
                    0,
                    0,
                    0,
                    0,
                    out WorldGridPoint[] path),
                Is.True);
            Assert.That(path, Is.Empty);
        }

        private static WorldCell Open()
        {
            return new WorldCell(TerrainKind.Wasteland, null, 0);
        }

        private static WorldCell Blocked()
        {
            return new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Cliff);
        }
    }
}
