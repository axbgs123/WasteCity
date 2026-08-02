using NUnit.Framework;
using WasteCity.Combat;
using System.Collections.Generic;
using System.Linq;

namespace WasteCity.Tests
{
    public sealed class BossEncounterTests
    {
        [Test] public void ShieldAbsorbsDamageBeforeHealth(){var health=new HealthModel(100);health.GrantShield(60);Assert.That(health.Apply(50,DamageType.Physical,ArmorType.Light),Is.EqualTo(50));Assert.That(health.Current,Is.EqualTo(100));Assert.That(health.Shield,Is.EqualTo(10));}
        [Test] public void CrystalShieldModeOverridesPhysicalDamageToFiftyPercent(){var health=new HealthModel(200);health.SetPhysicalDamagePercent(50);health.Apply(100,DamageType.Physical,ArmorType.Heavy);Assert.That(health.Current,Is.EqualTo(150));}
        [Test] public void PhaseTwoGrantsShieldAndSummonsHowlers(){var model=new BossEncounterModel();var actions=new List<BossAction>();model.Tick(.1f,.7f,actions);Assert.That(model.Phase,Is.EqualTo(BossPhase.Two));Assert.That(actions.Any(value=>value.Type==BossActionType.GrantShield&&value.Amount==600),Is.True);Assert.That(actions.Any(value=>value.Type==BossActionType.SummonHowlers&&value.Amount==3),Is.True);}
        [Test] public void PhaseOneSlamHasWarningBeforeDamage(){var model=new BossEncounterModel();var actions=new List<BossAction>();model.Tick(8.6f,1,actions);Assert.That(actions.Any(value=>value.Type==BossActionType.GroundSlamWarning),Is.True);actions.Clear();model.Tick(1.4f,1,actions);Assert.That(actions.Any(value=>value.Type==BossActionType.GroundSlam&&value.Amount==60),Is.True);}
        [Test] public void PhaseThreeMovesFasterAndSummonsThreeGnawers(){var model=new BossEncounterModel();var actions=new List<BossAction>();model.Tick(0,.39f,actions);Assert.That(model.Phase,Is.EqualTo(BossPhase.Three));Assert.That(model.SpeedMultiplier,Is.EqualTo(1.4f));actions.Clear();model.Tick(10,.39f,actions);Assert.That(actions.Any(value=>value.Type==BossActionType.SummonGnawers&&value.Amount==3),Is.True);}
        [Test] public void BossStateRoundTrips(){var model=new BossEncounterModel();var actions=new List<BossAction>();model.Tick(0,.5f,actions);model.Tick(4,.5f,actions);var restored=new BossEncounterModel();restored.Restore(model.Capture());Assert.That(restored.Phase,Is.EqualTo(BossPhase.Two));}
    }
}
