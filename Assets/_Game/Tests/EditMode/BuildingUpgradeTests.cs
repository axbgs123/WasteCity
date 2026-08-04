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
    }
}
