using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class BuildingUpgradeTests
    {
        [Test] public void HeavyTurretRequiresCivilizationAndAlloyArmor(){Assert.That(BuildingUpgradeCatalog.For(BuildingCatalog.MachineGunTurret,1,true),Is.Null);Assert.That(BuildingUpgradeCatalog.For(BuildingCatalog.MachineGunTurret,2,false),Is.Null);Assert.That(BuildingUpgradeCatalog.For(BuildingCatalog.MachineGunTurret,2,true).Target,Is.EqualTo(BuildingCatalog.HeavyMachineGunTurret));}
        [Test] public void GridUpgradePreservesCellAndBuildingCount(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Alloy,50);var grid=new BuildingGrid(4,4);grid.TryPlace(BuildingCatalog.MachineGunTurret,1,1,inventory,true,out var placed);var recipe=BuildingUpgradeCatalog.For(placed.Definition,2,true);Assert.That(grid.TryUpgrade(placed,recipe.Target,inventory,recipe.CostId,recipe.Cost,out var upgraded),Is.True);Assert.That(upgraded.Definition,Is.EqualTo(BuildingCatalog.HeavyMachineGunTurret));Assert.That(grid.Count,Is.EqualTo(1));}
        [Test] public void GridUpgradeDoesNotMutateWhenResourcesAreMissing(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Alloy,10);var grid=new BuildingGrid(4,4);grid.TryPlace(BuildingCatalog.MachineGunTurret,1,1,inventory,true,out var placed);Assert.That(grid.TryUpgrade(placed,BuildingCatalog.HeavyMachineGunTurret,inventory,ResourceIds.Alloy,20,out _),Is.False);Assert.That(grid.Count,Is.EqualTo(1));}
        [Test] public void SwordRidingPlatformRequiresCivilizationAndResearch(){Assert.That(BuildingUpgradeCatalog.For(BuildingCatalog.SwordArrayTower,1,false,true),Is.Null);Assert.That(BuildingUpgradeCatalog.For(BuildingCatalog.SwordArrayTower,2,false,false),Is.Null);var recipe=BuildingUpgradeCatalog.For(BuildingCatalog.SwordArrayTower,2,false,true);Assert.That(recipe.Target,Is.SameAs(BuildingCatalog.SwordRidingPlatform));Assert.That(recipe.CostId,Is.EqualTo(ResourceIds.SpiritIron));Assert.That(recipe.Cost,Is.EqualTo(20));}

        [Test]
        public void GridUpgradePreservesInnerCitySite()
        {
            var source = new BuildingDefinition(
                "test.building.source",
                "测试源",
                1,
                1,
                ResourceIds.Iron,
                0,
                placement: BuildingPlacement.Either,
                operation: BuildingOperation.MobileAllowed);
            var target = new BuildingDefinition(
                "test.building.target",
                "测试目标",
                1,
                1,
                ResourceIds.Iron,
                0,
                placement: BuildingPlacement.Either,
                operation: BuildingOperation.MobileAllowed);
            var grid = new BuildingGrid(2, 2);
            var inventory = new ResourceInventory(10);
            grid.TryRestore(source, 0, 0, out var placed, BuildingSite.InnerCity);

            Assert.That(
                grid.TryUpgrade(
                    placed,
                    target,
                    inventory,
                    ResourceIds.Iron,
                    0,
                    out var upgraded),
                Is.True);
            Assert.That(upgraded.Site, Is.EqualTo(BuildingSite.InnerCity));
        }

        [Test]
        public void GridUpgradeRejectsTargetThatDoesNotSupportExistingSite()
        {
            var source = new BuildingDefinition(
                "test.building.source",
                "测试源",
                1,
                1,
                ResourceIds.Iron,
                0,
                placement: BuildingPlacement.Either,
                operation: BuildingOperation.MobileAllowed);
            var groundOnlyTarget = new BuildingDefinition(
                "test.building.ground-target",
                "地面目标",
                1,
                1,
                ResourceIds.Iron,
                0);
            var grid = new BuildingGrid(2, 2);
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.Iron, 5);
            grid.TryRestore(source, 0, 0, out var placed, BuildingSite.InnerCity);

            Assert.That(
                grid.TryUpgrade(
                    placed,
                    groundOnlyTarget,
                    inventory,
                    ResourceIds.Iron,
                    2,
                    out _),
                Is.False);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(grid.Count, Is.EqualTo(1));
        }
    }
}
