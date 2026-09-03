using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class LocalHasteRuntimeTests
    {
        [Test]
        public void IDEA0028_RuntimeAcceptsOnlyFormalHasteDomains()
        {
            var runtime = new LocalHasteRuntime();

            Assert.That(runtime.TrySelectTarget(
                "building:smelter-01", out _), Is.False);
            Assert.That(runtime.TrySelectTarget("production", out _), Is.True);
            Assert.That(runtime.TrySelectTarget("research", out _), Is.True);
            Assert.That(runtime.TrySelectTarget("defense", out _), Is.True);
            Assert.That(runtime.Capture().TargetId, Is.EqualTo("defense"));
        }

        [Test]
        public void IDEA0028_LevelOneConsumesSixtySecondBudgetAtFiveTimesSpeed()
        {
            var runtime = new LocalHasteRuntime();
            Assert.That(runtime.RuleCycleSeconds, Is.EqualTo(600f));
            Assert.That(runtime.TryEnterCycle(1, out string cycleError),
                Is.True, cycleError);
            Assert.That(runtime.RemainingBudgetSeconds,
                Is.EqualTo(60f).Within(.0001f));
            Assert.That(runtime.Multiplier, Is.EqualTo(5f));
            Assert.That(runtime.TrySelectTarget("production", out _),
                Is.True);
            Assert.That(runtime.TryStart(out _), Is.True);

            Assert.That(runtime.Tick(
                10f, globallyPaused: false, out LocalHasteTickProjection tick,
                out string error), Is.True, error);
            Assert.That(tick.TargetId, Is.EqualTo("production"));
            Assert.That(tick.ConsumedBudgetSeconds,
                Is.EqualTo(10f).Within(.0001f));
            Assert.That(tick.EffectiveRuleSeconds,
                Is.EqualTo(50f).Within(.0001f));
            Assert.That(runtime.RemainingBudgetSeconds,
                Is.EqualTo(50f).Within(.0001f));
        }

        [Test]
        public void IDEA0028_PauseFreezesBudgetAndActiveTargetCannotChange()
        {
            var runtime = new LocalHasteRuntime();
            Assert.That(runtime.TryEnterCycle(1, out _), Is.True);
            Assert.That(runtime.TrySelectTarget("production", out _), Is.True);
            Assert.That(runtime.TryStart(out _), Is.True);
            Assert.That(runtime.TrySelectTarget("research", out _), Is.False);

            Assert.That(runtime.Tick(
                20f, globallyPaused: true, out LocalHasteTickProjection paused,
                out _), Is.True);
            Assert.That(paused.ConsumedBudgetSeconds, Is.Zero);
            Assert.That(paused.EffectiveRuleSeconds, Is.Zero);
            Assert.That(runtime.RemainingBudgetSeconds, Is.EqualTo(60f));

            Assert.That(runtime.Tick(
                80f, globallyPaused: false, out LocalHasteTickProjection final,
                out _), Is.True);
            Assert.That(final.ConsumedBudgetSeconds, Is.EqualTo(60f));
            Assert.That(final.EffectiveRuleSeconds, Is.EqualTo(300f));
            Assert.That(runtime.IsActive, Is.False);
            Assert.That(runtime.RemainingBudgetSeconds, Is.Zero);
        }

        [Test]
        public void IDEA0028_RestoreRoundTripsActiveBudgetAndContinuesTicking()
        {
            var source = new LocalHasteRuntime();
            Assert.That(source.TryEnterCycle(3, out _), Is.True);
            Assert.That(source.TrySelectTarget("production", out _),
                Is.True);
            Assert.That(source.TryStart(out _), Is.True);
            Assert.That(source.Tick(12f, false, out _, out _), Is.True);

            var restored = new LocalHasteRuntime();
            Assert.That(restored.TryRestore(
                source.Capture(), out string error), Is.True, error);
            Assert.That(restored.Capture().TargetId,
                Is.EqualTo(source.Capture().TargetId));
            Assert.That(restored.Capture().Active, Is.True);
            Assert.That(restored.Capture().RemainingBudgetSeconds,
                Is.EqualTo(48f).Within(.0001f));
            Assert.That(restored.Capture().Revision,
                Is.EqualTo(source.Capture().Revision));
            Assert.That(restored.Capture().CurrentCycleOrdinal,
                Is.EqualTo(3ul));

            Assert.That(restored.Tick(
                8f, false, out LocalHasteTickProjection tick,
                out error), Is.True, error);
            Assert.That(tick.EffectiveRuleSeconds, Is.EqualTo(40f));
            Assert.That(restored.RemainingBudgetSeconds, Is.EqualTo(40f));
        }

        [Test]
        public void IDEA0028_HigherCycleRefillsButSameOrPastCycleCannot()
        {
            var runtime = new LocalHasteRuntime();
            Assert.That(runtime.TryEnterCycle(1, out string error),
                Is.True, error);
            Assert.That(runtime.TrySelectTarget("production", out error),
                Is.True, error);
            Assert.That(runtime.TryStart(out error), Is.True, error);
            Assert.That(runtime.Tick(60f, false, out _, out error),
                Is.True, error);
            Assert.That(runtime.RemainingBudgetSeconds, Is.Zero);

            LocalHasteSnapshot exhausted = runtime.Capture();
            Assert.That(runtime.TryEnterCycle(1, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(exhausted));
            Assert.That(runtime.TryEnterCycle(0, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(exhausted));

            Assert.That(runtime.TryEnterCycle(2, out error), Is.True, error);
            Assert.That(runtime.Capture().CurrentCycleOrdinal, Is.EqualTo(2ul));
            Assert.That(runtime.TargetId, Is.EqualTo("production"));
            Assert.That(runtime.IsActive, Is.False,
                "A new cycle replenishes budget but cannot auto-start work.");
            Assert.That(runtime.RemainingBudgetSeconds,
                Is.EqualTo(60f).Within(.0001f));
        }

        [Test]
        public void IDEA0028_InvalidRestoreIsAtomic()
        {
            var runtime = new LocalHasteRuntime();
            Assert.That(runtime.TrySelectTarget("production", out _),
                Is.True);
            LocalHasteSnapshot before = runtime.Capture();
            var invalid = new LocalHasteSnapshot(
                string.Empty,
                true,
                float.NaN,
                9);

            Assert.That(runtime.TryRestore(invalid, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }
    }
}
