using NUnit.Framework;
using UnityEngine;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class WorldMapTests
    {
        [Test]
        public void SameSeedCreatesSameTerrainAndResources()
        {
            var a = new WorldMapModel(20, 20, new WorldSeed(99)); var b = new WorldMapModel(20, 20, new WorldSeed(99));
            Assert.That(a.Get(7, 13).Terrain, Is.EqualTo(b.Get(7, 13).Terrain));
            Assert.That(a.Get(7, 13).ResourceId, Is.EqualTo(b.Get(7, 13).ResourceId));
            Assert.That(a.ResourceNodeCount, Is.EqualTo(b.ResourceNodeCount));
        }

        [Test]
        public void FogRevealIsCircularAndIdempotent()
        {
            var map = new WorldMapModel(20, 20, new WorldSeed(1));
            Assert.That(map.Reveal(10, 10, 3), Is.GreaterThan(0));
            Assert.That(map.IsRevealed(10, 10), Is.True);
            Assert.That(map.IsRevealed(14, 10), Is.False);
            Assert.That(map.Reveal(10, 10, 3), Is.Zero);
        }

        [Test]
        public void WorldAndCellCoordinatesKeepExistingCenteredGridConvention()
        {
            var worldObject = new GameObject("CoordinateWorld");
            try
            {
                var view = worldObject.AddComponent<PlaceholderWorldView>();
                view.Generate(new WorldSeed(8128));

                Assert.That(
                    view.TryWorldToCell(
                        new Vector2(-8f, -5f),
                        out int x,
                        out int y),
                    Is.True);
                Assert.That(x, Is.EqualTo(8));
                Assert.That(y, Is.EqualTo(7));
                Assert.That(
                    view.CellToWorld(8, 7),
                    Is.EqualTo(new Vector2(-8f, -5f)));
            }
            finally
            {
                Object.DestroyImmediate(worldObject);
            }
        }
    }
}
