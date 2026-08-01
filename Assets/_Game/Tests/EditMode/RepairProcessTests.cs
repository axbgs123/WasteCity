using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class RepairProcessTests
    {
        [Test] public void RepairUsesTwoBaseSecondsAndRestoresFiftyHealth()
        { var repair=new RepairProcess();Assert.That(repair.Tick(1.9f,1f),Is.False);Assert.That(repair.Tick(.11f,1f),Is.True);var health=new HealthModel(300);health.Apply(100,DamageType.Physical,ArmorType.Light);Assert.That(health.Heal(repair.HealAmount),Is.EqualTo(50));Assert.That(health.Current,Is.EqualTo(250)); }
        [Test] public void ProductivityAcceleratesRepair()
        { var repair=new RepairProcess();Assert.That(repair.Tick(1f,2f),Is.True); }
        [Test] public void HealingCannotExceedMaximum()
        { var health=new HealthModel(100);health.Apply(10,DamageType.Physical,ArmorType.Light);Assert.That(health.Heal(50),Is.EqualTo(10));Assert.That(health.Current,Is.EqualTo(100)); }
        [Test] public void RepairRemainingTimeCanBeRestored()
        { var repair=new RepairProcess();repair.Restore(.75f);Assert.That(repair.Remaining,Is.EqualTo(.75f)); }
    }
}
