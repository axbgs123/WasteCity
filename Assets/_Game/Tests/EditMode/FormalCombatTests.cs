using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class FormalCombatTests
    {
        [Test] public void TypedDamageAppliesArmorMatrixAndDeathOnce(){var h=new HealthModel(100);int deaths=0;h.Died+=()=>deaths++;Assert.That(h.Apply(100,DamageType.Physical,ArmorType.Heavy),Is.EqualTo(70));h.Apply(100,DamageType.Energy,ArmorType.Heavy);h.Apply(10,DamageType.Energy,ArmorType.Heavy);Assert.That(h.IsDead,Is.True);Assert.That(deaths,Is.EqualTo(1));}
        [Test] public void TrueDamageBypassesShieldAndMitigationButRaisesEvents(){var h=new HealthModel(100);h.GrantShield(200);h.SetPhysicalDamagePercent(0);int damage=0,deaths=0;h.Damaged+=value=>damage+=value;h.Died+=()=>deaths++;Assert.That(h.ApplyTrueDamage(100),Is.EqualTo(100));Assert.That(h.Current,Is.Zero);Assert.That(h.Shield,Is.EqualTo(200));Assert.That(damage,Is.EqualTo(100));Assert.That(deaths,Is.EqualTo(1));Assert.That(h.ApplyTrueDamage(10),Is.Zero);Assert.That(deaths,Is.EqualTo(1));}
    }
}
