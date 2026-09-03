using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class ForesightDelayRuntimeTests
    {
        [Test]
        public void IDEA0028_RevealConsumesOneAuthoritativePlanPerCycle()
        {
            var runtime = new ForesightDelayRuntime();
            Assert.That(runtime.TryEnterCycle(1, out string cycleError),
                Is.True, cycleError);
            var plans = new[]
            {
                new ForesightAuthoritativePlan(
                    "event.late", 140f, "event.late.summary"),
                new ForesightAuthoritativePlan(
                    "event.next", 120f, "event.next.summary"),
            };

            Assert.That(runtime.TryReveal(
                1, 100f, plans, out ForesightProjection projection,
                out string error), Is.True, error);
            Assert.That(projection.EventId, Is.EqualTo("event.next"));
            Assert.That(projection.SecondsUntilEvent,
                Is.EqualTo(20f).Within(.0001f));
            Assert.That(runtime.Capture().DisplayRemainingSeconds,
                Is.EqualTo(3f).Within(.0001f));
            Assert.That(runtime.TryReveal(1, 101f, plans, out _, out _), Is.False,
                "A cycle can reveal only once.");
            Assert.That(runtime.Capture().LastConsumedCycleOrdinal,
                Is.EqualTo(1ul));
        }

        [Test]
        public void IDEA0028_RuntimeNeverInventsOrRepairsInvalidPlans()
        {
            var runtime = new ForesightDelayRuntime();
            Assert.That(runtime.TryEnterCycle(1, out _), Is.True);
            ForesightDelaySnapshot before = runtime.Capture();
            var invalid = new[]
            {
                new ForesightAuthoritativePlan(
                    "event.duplicate", 120f, "summary.a"),
                new ForesightAuthoritativePlan(
                    "event.duplicate", 130f, "summary.b"),
            };
            Assert.That(runtime.TryReveal(
                1, 100f, invalid, out _, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));

            var pastOnly = new[]
            {
                new ForesightAuthoritativePlan("event.past", 90f, "summary"),
            };
            Assert.That(runtime.TryReveal(
                1, 100f, pastOnly, out _, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }

        [Test]
        public void IDEA0028_RestoreRoundTripsLastProjectionAndCycleBoundary()
        {
            var source = new ForesightDelayRuntime();
            Assert.That(source.TryEnterCycle(4, out _), Is.True);
            var plans = new[]
            {
                new ForesightAuthoritativePlan(
                    "event.saved", 140f, "event.saved.summary"),
            };
            Assert.That(source.TryReveal(
                4, 100f, plans, out _, out string error), Is.True, error);

            var restored = new ForesightDelayRuntime();
            Assert.That(restored.TryRestore(
                source.Capture(), out error), Is.True, error);
            ForesightDelaySnapshot snapshot = restored.Capture();
            Assert.That(snapshot.LastConsumedCycleOrdinal, Is.EqualTo(4ul));
            Assert.That(snapshot.CurrentCycleOrdinal, Is.EqualTo(4ul));
            Assert.That(snapshot.LastProjection.EventId,
                Is.EqualTo("event.saved"));
            Assert.That(snapshot.LastProjection.SecondsUntilEvent,
                Is.EqualTo(40f).Within(.0001f));
            Assert.That(snapshot.Revision,
                Is.EqualTo(source.Capture().Revision));
            Assert.That(restored.TryReveal(
                4, 101f, plans, out _, out _), Is.False);
        }

        [Test]
        public void IDEA0028_OnlyCurrentAuthoritativeCycleCanRevealOnce()
        {
            var runtime = new ForesightDelayRuntime();
            var plans = new[]
            {
                new ForesightAuthoritativePlan(
                    "event.authoritative", 120f, "event.summary"),
            };
            Assert.That(runtime.TryEnterCycle(7, out string error),
                Is.True, error);
            Assert.That(runtime.TryReveal(
                6, 100f, plans, out _, out _), Is.False,
                "The runtime cannot accept a caller-invented past cycle.");
            Assert.That(runtime.TryReveal(
                8, 100f, plans, out _, out _), Is.False,
                "The runtime cannot accept a caller-invented next cycle.");
            Assert.That(runtime.TryReveal(
                7, 100f, plans, out _, out error), Is.True, error);
            Assert.That(runtime.TryReveal(
                7, 100f, plans, out _, out _), Is.False);

            Assert.That(runtime.TryEnterCycle(7, out _), Is.False);
            Assert.That(runtime.TryEnterCycle(6, out _), Is.False);
            Assert.That(runtime.TryEnterCycle(8, out error), Is.True, error);
            Assert.That(runtime.Capture().CurrentCycleOrdinal, Is.EqualTo(8ul));
            Assert.That(runtime.Capture().LastConsumedCycleOrdinal,
                Is.EqualTo(7ul));
            Assert.That(runtime.Capture().LastProjection, Is.Null,
                "A prior-cycle preview must not remain current.");
        }

        [Test]
        public void IDEA0028_DisplayBudgetPausesThenClearsWithoutRearmingCycle()
        {
            var runtime = new ForesightDelayRuntime();
            Assert.That(runtime.TryEnterCycle(1, out _), Is.True);
            var plans = new[]
            {
                new ForesightAuthoritativePlan(
                    "event.display", 140f, "event.display.summary"),
            };
            Assert.That(runtime.TryReveal(
                1, 100f, plans, out _, out string error), Is.True, error);
            ForesightDelaySnapshot visible = runtime.Capture();
            Assert.That(visible.DisplayRemainingSeconds,
                Is.EqualTo(FormalFateCatalog.ForesightDisplaySeconds));

            Assert.That(runtime.TickDisplay(1f, true, out error),
                Is.True, error);
            Assert.That(runtime.Capture(), Is.SameAs(visible));
            Assert.That(runtime.TickDisplay(1f, false, out error),
                Is.True, error);
            Assert.That(runtime.Capture().DisplayRemainingSeconds,
                Is.EqualTo(2f).Within(.0001f));
            Assert.That(runtime.Capture().LastProjection, Is.Not.Null);
            Assert.That(runtime.TickDisplay(2f, false, out error),
                Is.True, error);
            Assert.That(runtime.Capture().DisplayRemainingSeconds, Is.Zero);
            Assert.That(runtime.Capture().LastProjection, Is.Null);
            Assert.That(runtime.Capture().LastConsumedCycleOrdinal,
                Is.EqualTo(1ul));
            Assert.That(runtime.TryReveal(
                1, 101f, plans, out _, out _), Is.False,
                "Closing the fragment cannot re-arm the consumed cycle.");
        }

        [Test]
        public void IDEA0028_InvalidRestoreIsAtomic()
        {
            var runtime = new ForesightDelayRuntime();
            ForesightDelaySnapshot before = runtime.Capture();
            var invalid = new ForesightDelaySnapshot(
                2,
                new ForesightProjection(" ", "summary", 120f, 20f),
                1);

            Assert.That(runtime.TryRestore(invalid, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }
    }
}
