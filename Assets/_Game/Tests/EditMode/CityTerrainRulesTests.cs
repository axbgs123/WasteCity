using NUnit.Framework;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class CityTerrainRulesTests
    {
        [TestCase(WorldTraversalKind.DeepWater)]
        [TestCase(WorldTraversalKind.Cliff)]
        public void DeepWaterAndCliffBlockCity(WorldTraversalKind traversal)
        {
            var cell = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                traversal);

            Assert.That(CityTerrainRules.IsPassable(cell), Is.False);
            Assert.That(CityTerrainRules.SpeedMultiplier(cell), Is.Zero);
            Assert.That(CityTerrainRules.SupportsDeployment(cell), Is.False);
        }

        [Test]
        public void WetlandAndRuinsUseApprovedSlowMultipliers()
        {
            var wetland = new WorldCell(TerrainKind.Wetland, null, 0);
            var ruins = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Ruins);

            Assert.That(CityTerrainRules.SpeedMultiplier(wetland), Is.EqualTo(.55f));
            Assert.That(CityTerrainRules.SpeedMultiplier(ruins), Is.EqualTo(.65f));
            Assert.That(CityTerrainRules.SupportsDeployment(wetland), Is.False);
            Assert.That(CityTerrainRules.SupportsDeployment(ruins), Is.False);
        }

        [Test]
        public void RockyGroundSlowsTravelButStillSupportsDeployment()
        {
            var rocky = new WorldCell(TerrainKind.Rocky, null, 0);

            Assert.That(CityTerrainRules.IsPassable(rocky), Is.True);
            Assert.That(CityTerrainRules.SpeedMultiplier(rocky), Is.EqualTo(.8f));
            Assert.That(CityTerrainRules.SupportsDeployment(rocky), Is.True);
        }

        [Test]
        public void WastelandAndCrystalKeepFullTravelSpeed()
        {
            Assert.That(
                CityTerrainRules.SpeedMultiplier(
                    new WorldCell(TerrainKind.Wasteland, null, 0)),
                Is.EqualTo(1f));
            Assert.That(
                CityTerrainRules.SpeedMultiplier(
                    new WorldCell(TerrainKind.Crystal, null, 0)),
                Is.EqualTo(1f));
        }

        [Test]
        public void GeneratedResourceNodesAlwaysRemainOpen()
        {
            var map = new WorldMapModel(32, 24, new WorldSeed(8128));

            for (int x = 0; x < map.Width; x++)
                for (int y = 0; y < map.Height; y++)
                    if (map.Get(x, y).HasResource)
                        Assert.That(
                            map.Get(x, y).Traversal,
                            Is.EqualTo(WorldTraversalKind.Open),
                            $"resource cell {x},{y}");
        }

        [Test]
        public void ExistingTerrainNumericValuesStaySaveCompatible()
        {
            Assert.That((int)TerrainKind.Wasteland, Is.Zero);
            Assert.That((int)TerrainKind.Rocky, Is.EqualTo(1));
            Assert.That((int)TerrainKind.Crystal, Is.EqualTo(2));
            Assert.That((int)TerrainKind.Wetland, Is.EqualTo(3));
        }
    }
}
