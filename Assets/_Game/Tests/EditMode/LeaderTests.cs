using NUnit.Framework;
using WasteCity.Leader;

namespace WasteCity.Tests
{
    public sealed class LeaderTests
    {
        [Test]
        public void ImmediateRescueProvidesFullOverload()
        {
            var model = new LeaderModel();
            model.Recruit(true);
            Assert.That(model.Overload.TryActivate(), Is.True);
            Assert.That(model.Overload.FireRateMultiplier, Is.EqualTo(1.75f));
            Assert.That(model.AssemblerEfficiency, Is.EqualTo(1.25f));
        }

        [Test]
        public void DelayedRescueProvidesReducedOverload()
        {
            var model = new LeaderModel();
            model.Recruit(false);
            model.Overload.TryActivate();
            Assert.That(model.Injured, Is.True);
            Assert.That(model.Overload.FireRateMultiplier, Is.EqualTo(1.35f));
        }

        [Test]
        public void OverloadBoostThenLocksTurretsBeforeCooldown()
        {
            var model = new LeaderModel();
            model.Recruit(true);
            model.Overload.TryActivate();
            model.Tick(5f);
            Assert.That(model.Overload.FireRateMultiplier, Is.Zero);
            model.Tick(3f);
            Assert.That(model.Overload.FireRateMultiplier, Is.EqualTo(1f));
            Assert.That(model.Overload.TryActivate(), Is.False);
            model.Tick(22f);
            Assert.That(model.Overload.TryActivate(), Is.True);
        }

        [Test]
        public void LeaderStateCanBeRestored()
        {
            var model = new LeaderModel();
            model.Restore(true, true, 12f, 0f, 0f);
            Assert.That(model.Recruited, Is.True);
            Assert.That(model.Injured, Is.True);
            Assert.That(model.Overload.CooldownRemaining, Is.EqualTo(12f));
        }

    }
}
