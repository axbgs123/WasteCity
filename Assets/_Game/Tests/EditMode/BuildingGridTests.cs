using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class BuildingGridTests
    {
        [Test]
        public void PlacementConsumesCostAndBlocksOverlap()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.Alloy, 20);
            var grid = new BuildingGrid(8, 8); var warehouse = BuildingCatalog.All[2];
            Assert.That(grid.TryPlace(warehouse, 1, 1, inventory, false, out _), Is.True);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(12));
            Assert.That(grid.TryPlace(warehouse, 2, 2, inventory, false, out _), Is.False);
        }
        [Test]
        public void MiningStationRequiresResourceCoverage()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.Iron, 20);
            var grid = new BuildingGrid(8, 8);
            Assert.That(grid.TryPlace(BuildingCatalog.All[0], 0, 0, inventory, false, out _), Is.False);
            Assert.That(grid.TryPlace(BuildingCatalog.All[0], 0, 0, inventory, true, out _), Is.True);
        }
    }
}
