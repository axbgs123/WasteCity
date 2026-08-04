using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class PsionicResonanceTests
    {
        [Test]
        public void ApplyStartsFiveSecondMarkAndApplyAgainRefreshesIt()
        {
            var model = new PsionicResonanceModel();

            model.Apply();
            model.Tick(2f);
            model.Apply();

            Assert.That(model.Active, Is.True);
            Assert.That(model.Remaining, Is.EqualTo(5f));
        }

        [Test]
        public void MarkExpiresAndNegativeTimeDoesNotAdvanceIt()
        {
            var model = new PsionicResonanceModel();
            model.Apply();

            model.Tick(-3f);
            Assert.That(model.Remaining, Is.EqualTo(5f));
            model.Tick(5f);

            Assert.That(model.Active, Is.False);
            Assert.That(model.Remaining, Is.Zero);
        }

        [Test]
        public void RestoreClampsRemainingDuration()
        {
            var model = new PsionicResonanceModel();

            model.Restore(9f);
            Assert.That(model.Remaining, Is.EqualTo(5f));
            model.Restore(-1f);
            Assert.That(model.Remaining, Is.Zero);
        }

        [Test]
        public void MarkerCapAllowsRefreshButRejectsEleventhTarget()
        {
            Assert.That(PsionicResonanceRules.CanMark(false, 9), Is.True);
            Assert.That(PsionicResonanceRules.CanMark(false, 10), Is.False);
            Assert.That(PsionicResonanceRules.CanMark(true, 10), Is.True);
        }

        [TestCase(10, 3)]
        [TestCase(1, 1)]
        [TestCase(0, 0)]
        [TestCase(-3, 0)]
        public void SynchronizedDamageUsesThirtyPercentWithPositiveMinimum(
            int primaryAppliedDamage,
            int expected)
        {
            Assert.That(
                PsionicResonanceRules.SynchronizedRawDamage(primaryAppliedDamage),
                Is.EqualTo(expected));
        }
    }
}
