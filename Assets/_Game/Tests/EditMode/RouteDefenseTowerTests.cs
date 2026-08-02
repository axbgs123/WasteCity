using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class RouteDefenseTowerTests
    {
        [Test] public void EachFormalRouteHasDistinctDefenseTower(){Assert.That(DefenseTowerCatalog.For(BuildingCatalog.SwordArrayTower.Id.Value).DamageType,Is.EqualTo(DamageType.TrueEssence));Assert.That(DefenseTowerCatalog.For(BuildingCatalog.SporeTower.Id.Value).DamageType,Is.EqualTo(DamageType.Biological));Assert.That(DefenseTowerCatalog.For(BuildingCatalog.MindSpire.Id.Value).DamageType,Is.EqualTo(DamageType.Psionic));}
        [Test] public void RouteTowerConsumesConfiguredResource(){var profile=DefenseTowerCatalog.For(BuildingCatalog.SporeTower.Id.Value);var inventory=new ResourceInventory(100);inventory.Add(profile.ConsumableId,1);var target=new HealthModel(200);var weapon=new TurretWeaponModel(profile.DamagePerSecond,profile.SecondsPerConsumable,profile.DamageType,profile.ConsumableId);weapon.Tick(1,inventory,target,ArmorType.Light);Assert.That(inventory.Get(ResourceIds.Biomass),Is.Zero);Assert.That(target.Current,Is.LessThan(200));}
        [Test] public void BuildMenuExcludesUpgradeOnlyHeavyTurret(){Assert.That(BuildingCatalog.BuildMenu,Has.Member(BuildingCatalog.SwordArrayTower));Assert.That(BuildingCatalog.BuildMenu,Has.No.Member(BuildingCatalog.HeavyMachineGunTurret));}
    }
}
