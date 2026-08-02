using NUnit.Framework;
using WasteCity.Leader;

namespace WasteCity.Tests
{
    public sealed class LeaderTests
    {
        [Test] public void ImmediateRescueProvidesFullOverload(){var m=new LeaderModel();m.Recruit(true);Assert.That(m.Overload.TryActivate(),Is.True);Assert.That(m.Overload.FireRateMultiplier,Is.EqualTo(1.75f));Assert.That(m.AssemblerEfficiency,Is.EqualTo(1.25f));}
        [Test] public void DelayedRescueProvidesReducedOverload(){var m=new LeaderModel();m.Recruit(false);m.Overload.TryActivate();Assert.That(m.Injured,Is.True);Assert.That(m.Overload.FireRateMultiplier,Is.EqualTo(1.35f));}
        [Test] public void OverloadBoostThenLocksTurretsBeforeCooldown(){var m=new LeaderModel();m.Recruit(true);m.Overload.TryActivate();m.Tick(5f);Assert.That(m.Overload.FireRateMultiplier,Is.Zero);m.Tick(3f);Assert.That(m.Overload.FireRateMultiplier,Is.EqualTo(1f));Assert.That(m.Overload.TryActivate(),Is.False);m.Tick(22f);Assert.That(m.Overload.TryActivate(),Is.True);}
        [Test] public void LeaderStateCanBeRestored(){var m=new LeaderModel();m.Restore(true,true,12f,0f,0f);Assert.That(m.Recruited,Is.True);Assert.That(m.Injured,Is.True);Assert.That(m.Overload.CooldownRemaining,Is.EqualTo(12f));}
    }
}
