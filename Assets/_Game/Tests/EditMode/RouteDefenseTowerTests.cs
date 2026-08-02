using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class RouteDefenseTowerTests
    {
        [Test] public void EachFormalRouteHasDistinctDefenseTower(){Assert.That(DefenseTowerCatalog.For(BuildingCatalog.SwordArrayTower.Id.Value).DamageType,Is.EqualTo(DamageType.TrueEssence));Assert.That(DefenseTowerCatalog.For(BuildingCatalog.SporeTower.Id.Value).DamageType,Is.EqualTo(DamageType.Biological));Assert.That(DefenseTowerCatalog.For(BuildingCatalog.MindSpire.Id.Value).DamageType,Is.EqualTo(DamageType.Psionic));}
        [Test] public void RouteTowerConsumesConfiguredResource(){var profile=DefenseTowerCatalog.For(BuildingCatalog.SporeTower.Id.Value);var inventory=new ResourceInventory(100);inventory.Add(profile.ConsumableId,1);var target=new HealthModel(200);var weapon=new TurretWeaponModel(profile.DamagePerSecond,profile.SecondsPerConsumable,profile.DamageType,profile.ConsumableId);weapon.Tick(1,inventory,target,ArmorType.Light);Assert.That(inventory.Get(ResourceIds.BiologicalWeapon),Is.Zero);Assert.That(target.Current,Is.LessThan(200));}
        [Test] public void BuildMenuExcludesUpgradeOnlyHeavyTurret(){Assert.That(BuildingCatalog.BuildMenu,Has.Member(BuildingCatalog.SwordArrayTower));Assert.That(BuildingCatalog.BuildMenu,Has.No.Member(BuildingCatalog.HeavyMachineGunTurret));}
        [Test] public void TierThreeRoutesExposeLaserAndAcidTowers(){Assert.That(DefenseTowerCatalog.For(BuildingCatalog.LaserTower.Id.Value).DamageType,Is.EqualTo(DamageType.Energy));Assert.That(DefenseTowerCatalog.For(BuildingCatalog.AcidTower.Id.Value).DamageType,Is.EqualTo(DamageType.Biological));Assert.That(BuildingCatalog.BuildMenu,Has.Member(BuildingCatalog.LaserTower));Assert.That(BuildingCatalog.BuildMenu,Has.Member(BuildingCatalog.AcidTower));}
        [Test] public void ShieldPulseUsesCadenceAndCapsShield(){var pulse=new ShieldPulseModel(8f);Assert.That(pulse.Tick(7.9f),Is.False);Assert.That(pulse.Tick(.1f),Is.True);var health=new HealthModel(100);Assert.That(health.GrantShield(70,100),Is.EqualTo(70));Assert.That(health.GrantShield(50,100),Is.EqualTo(30));Assert.That(health.Shield,Is.EqualTo(100));}
        [Test] public void AutomatedRepairPulseHealsDamagedBuilding(){var pulse=new AutomatedRepairModel(6f,20);var health=new HealthModel(100);health.Restore(60);Assert.That(pulse.Tick(5.9f),Is.False);Assert.That(pulse.Tick(.1f),Is.True);Assert.That(pulse.Repair(health),Is.EqualTo(20));Assert.That(health.Current,Is.EqualTo(80));}
    }
}
