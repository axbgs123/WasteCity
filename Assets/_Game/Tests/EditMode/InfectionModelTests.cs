using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class InfectionModelTests
    {
        [Test]
        public void FirstValidBiologicalDamageAppliesImmediatelyThenWaitsOneSecond()
        {
            var emitter = new InfectionEmitterModel();

            Assert.That(emitter.Tick(.1f, true), Is.True);
            Assert.That(emitter.Tick(.9f, true), Is.False);
            Assert.That(emitter.Tick(.1f, true), Is.True);
        }

        [Test]
        public void FramesWithoutDamageNeverProduceEmitterEvents()
        {
            var emitter = new InfectionEmitterModel();

            Assert.That(emitter.Tick(2f, false), Is.False);
            Assert.That(emitter.Tick(.1f, true), Is.True);
            Assert.That(emitter.Tick(2f, false), Is.False);
        }

        [Test]
        public void TenLayersBurstAndResetTheStatus()
        {
            var infection = new InfectionModel();

            Assert.That(infection.AddStacks(9), Is.False);
            Assert.That(infection.AddStacks(1), Is.True);

            Assert.That(infection.Stacks, Is.Zero);
            Assert.That(infection.Elapsed, Is.Zero);
        }

        [Test]
        public void InfectionDealsTwoPercentMaximumHealthEachSecond()
        {
            var infection = new InfectionModel();
            infection.AddStacks(1);

            Assert.That(infection.Tick(.99f, 250), Is.Zero);
            Assert.That(infection.Tick(.01f, 250), Is.EqualTo(5));
            Assert.That(infection.Tick(2f, 250), Is.EqualTo(10));
        }

        [Test]
        public void ActiveInfectionAlwaysDealsAtLeastOneDamagePerTick()
        {
            var infection = new InfectionModel();
            infection.AddStacks(1);

            Assert.That(infection.Tick(1f, 1), Is.EqualTo(1));
        }

        [Test]
        public void NoStacksOrNonPositiveDeltaDealNoDamage()
        {
            var infection = new InfectionModel();

            Assert.That(infection.Tick(2f, 250), Is.Zero);
            infection.AddStacks(1);
            Assert.That(infection.Tick(0f, 250), Is.Zero);
            Assert.That(infection.Tick(-1f, 250), Is.Zero);
        }

        [Test]
        public void RestoreNormalizesStacksAndElapsed()
        {
            var infection = new InfectionModel();

            infection.Restore(-1, -2f);
            Assert.That(infection.Stacks, Is.Zero);
            Assert.That(infection.Elapsed, Is.Zero);

            infection.Restore(20, 1.25f);
            Assert.That(infection.Stacks, Is.EqualTo(9));
            Assert.That(infection.Elapsed, Is.EqualTo(.25f).Within(.0001f));
        }

        [Test]
        public void ClearRemovesStacksAndPartialTickProgress()
        {
            var infection = new InfectionModel();
            infection.AddStacks(3);
            infection.Tick(.6f, 100);

            infection.Clear();

            Assert.That(infection.Stacks, Is.Zero);
            Assert.That(infection.Elapsed, Is.Zero);
        }
    }
}
