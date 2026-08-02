using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class BuildingUpgradeTests
    {
        [Test] public void HeavyTurretRequiresSecondCivilizationLevel(){Assert.That(BuildingUpgradeCatalog.For(BuildingCatalog.MachineGunTurret,1),Is.Null);Assert.That(BuildingUpgradeCatalog.For(BuildingCatalog.MachineGunTurret,2).Target,Is.EqualTo(BuildingCatalog.HeavyMachineGunTurret));}
        [Test] public void GridUpgradePreservesCellAndBuildingCount(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Alloy,50);var grid=new BuildingGrid(4,4);grid.TryPlace(BuildingCatalog.MachineGunTurret,1,1,inventory,true,out var placed);var recipe=BuildingUpgradeCatalog.For(placed.Definition,2);Assert.That(grid.TryUpgrade(placed,recipe.Target,inventory,recipe.CostId,recipe.Cost,out var upgraded),Is.True);Assert.That(upgraded.Definition,Is.EqualTo(BuildingCatalog.HeavyMachineGunTurret));Assert.That(grid.Count,Is.EqualTo(1));}
        [Test] public void GridUpgradeDoesNotMutateWhenResourcesAreMissing(){var inventory=new ResourceInventory(100);inventory.Add(ResourceIds.Alloy,10);var grid=new BuildingGrid(4,4);grid.TryPlace(BuildingCatalog.MachineGunTurret,1,1,inventory,true,out var placed);Assert.That(grid.TryUpgrade(placed,BuildingCatalog.HeavyMachineGunTurret,inventory,ResourceIds.Alloy,20,out _),Is.False);Assert.That(grid.Count,Is.EqualTo(1));}
    }
}
