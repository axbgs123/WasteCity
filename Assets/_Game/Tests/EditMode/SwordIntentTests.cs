using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class SwordIntentTests
    {
        [Test]
        public void TwentiethHitExecutesForMaximumHealthAndClears()
        {
            var model = new SwordIntentModel();
            for (int index = 0; index < 19; index++)
                Assert.That(model.AddHit(500).Executed, Is.False);

            SwordIntentHitResult result = model.AddHit(500);

            Assert.That(result.Executed, Is.True);
            Assert.That(result.TrueDamage, Is.EqualTo(500));
            Assert.That(model.Stacks, Is.Zero);
        }

        [Test]
        public void RestoreClampsBelowExecutionThreshold()
        {
            var model = new SwordIntentModel();

            model.Restore(50);
            Assert.That(model.Stacks, Is.EqualTo(19));
            model.Restore(-5);
            Assert.That(model.Stacks, Is.Zero);
        }

        [Test]
        public void ClearRemovesStacks()
        {
            var model = new SwordIntentModel();
            model.AddHit(100);
            model.Clear();
            Assert.That(model.Stacks, Is.Zero);
        }

        [Test]
        public void InvalidMaximumHealthDoesNotExecute()
        {
            var model = new SwordIntentModel();
            for (int index = 0; index < 20; index++) model.AddHit(0);
            Assert.That(model.Stacks, Is.Zero);
        }

        [Test]
        public void EmitterAddsFirstActualHitImmediatelyThenOncePerSecond()
        {
            var emitter = new SwordIntentEmitterModel();

            Assert.That(emitter.Tick(0f, true), Is.True);
            Assert.That(emitter.Tick(.5f, true), Is.False);
            Assert.That(emitter.Tick(.5f, true), Is.True);
        }

        [Test]
        public void EmitterIgnoresFramesWithoutDamageAndNegativeTime()
        {
            var emitter = new SwordIntentEmitterModel();

            Assert.That(emitter.Tick(-5f, false), Is.False);
            Assert.That(emitter.Tick(2f, false), Is.False);
            Assert.That(emitter.Tick(0f, true), Is.True);
        }
    }
}
