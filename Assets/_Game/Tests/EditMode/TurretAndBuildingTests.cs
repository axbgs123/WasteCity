using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;
namespace WasteCity.Tests
{
 public sealed class TurretAndBuildingTests
 {
  [Test] public void TurretConsumesOneAmmoForThreeSecondsOfFire(){var i=new ResourceInventory(100);i.Add(ResourceIds.Ammunition,1);var target=new HealthModel(200);var w=new TurretWeaponModel(20,3);Assert.That(w.Tick(1,i,target,ArmorType.Light),Is.EqualTo(20));Assert.That(w.Tick(2,i,target,ArmorType.Light),Is.EqualTo(40));Assert.That(i.Get(ResourceIds.Ammunition),Is.Zero);Assert.That(w.Tick(.1f,i,target,ArmorType.Light),Is.Zero);Assert.That(w.OutOfAmmo,Is.True);}
  [Test] public void HeavyArmorReducesTurretPhysicalDamage(){var i=new ResourceInventory(100);i.Add(ResourceIds.Ammunition,1);var target=new HealthModel(200);new TurretWeaponModel(20,3).Tick(1,i,target,ArmorType.Heavy);Assert.That(target.Current,Is.EqualTo(186));}
  [Test] public void BallisticsMultiplierRaisesRawTurretDamage(){var i=new ResourceInventory(100);i.Add(ResourceIds.Ammunition,1);var target=new HealthModel(200);new TurretWeaponModel(20,3).Tick(1,i,target,ArmorType.Light,1.15f);Assert.That(target.Current,Is.EqualTo(177));}
 }
}
