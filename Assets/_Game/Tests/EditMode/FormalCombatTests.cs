using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class FormalCombatTests
    {
        [Test] public void TypedDamageAppliesArmorMatrixAndDeathOnce(){var h=new HealthModel(100);int deaths=0;h.Died+=()=>deaths++;Assert.That(h.Apply(100,DamageType.Physical,ArmorType.Heavy),Is.EqualTo(70));h.Apply(100,DamageType.Energy,ArmorType.Heavy);h.Apply(10,DamageType.Energy,ArmorType.Heavy);Assert.That(h.IsDead,Is.True);Assert.That(deaths,Is.EqualTo(1));}
    }
}
