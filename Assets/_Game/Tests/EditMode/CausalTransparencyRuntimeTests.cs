using System.Linq;
using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class CausalTransparencyRuntimeTests
    {
        [Test]
        public void IDEA0028_FullHistoryRequiresPermissionAndExplainsThresholds()
        {
            var history = new[]
            {
                Entry(1, 12), Entry(2, 18), Entry(3, 24), Entry(4, 35),
            };
            var source = new FormalAttentionSnapshot(
                35, 4, history, new[] { 30 },
                new[] { "event-1", "event-2", "event-3", "event-4" },
                new string[0]);
            var runtime = new CausalTransparencyRuntime();

            Assert.That(runtime.TryProject(source, out _, out _), Is.False);
            Assert.That(runtime.TrySetFullReasonAccess(true), Is.True);
            Assert.That(runtime.TryProject(
                source, out CausalTransparencyProjection projection,
                out string error), Is.True, error);
            Assert.That(projection.FullHistory, Has.Count.EqualTo(4),
                "The fate projection is not limited to the ordinary recent three.");

            CausalThresholdExplanation reached = projection.Thresholds
                .Single(value => value.Threshold == 30);
            CausalThresholdExplanation pending = projection.Thresholds
                .Single(value => value.Threshold == 60);
            Assert.That(reached.WasReached, Is.True);
            Assert.That(reached.RemainingAttention, Is.Zero);
            Assert.That(pending.WasReached, Is.False);
            Assert.That(pending.RemainingAttention, Is.EqualTo(25));
        }

        [Test]
        public void IDEA0028_NullSourceAndNoOpPermissionDoNotChangeRevision()
        {
            var runtime = new CausalTransparencyRuntime();
            Assert.That(runtime.TrySetFullReasonAccess(false), Is.False);
            Assert.That(runtime.Revision, Is.Zero);
            Assert.That(runtime.TrySetFullReasonAccess(true), Is.True);
            ulong revision = runtime.Revision;
            Assert.That(runtime.TryProject(null, out _, out _), Is.False);
            Assert.That(runtime.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void IDEA0028_RestoreRoundTripsAccessAndProjectionPermission()
        {
            var source = new CausalTransparencyRuntime();
            Assert.That(source.TrySetFullReasonAccess(true), Is.True);

            var restored = new CausalTransparencyRuntime();
            Assert.That(restored.TryRestore(
                source.Capture(), out string error), Is.True, error);
            Assert.That(restored.FullReasonAccess, Is.True);
            Assert.That(restored.Revision, Is.EqualTo(source.Revision));

            var attention = new FormalAttentionSnapshot(
                0, 0, new FormalAttentionHistoryEntry[0], new int[0],
                new string[0], new string[0]);
            Assert.That(restored.TryProject(
                attention, out _, out error), Is.True, error);
        }

        [Test]
        public void IDEA0028_InvalidRestoreIsAtomic()
        {
            var runtime = new CausalTransparencyRuntime();
            CausalTransparencySnapshot before = runtime.Capture();
            var invalid = new CausalTransparencySnapshot(true, 0);

            Assert.That(runtime.TryRestore(invalid, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }

        private static FormalAttentionHistoryEntry Entry(ulong revision, int value)
        {
            return new FormalAttentionHistoryEntry(
                "core.attention.test." + revision,
                "event-" + revision,
                1,
                1,
                value,
                revision);
        }
    }
}
