using NUnit.Framework;
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
    }
}
