using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests.EditMode
{
    public sealed class CoordinateLockRuntimeTests
    {
        [TestCase(AdvancementSequenceStage.None, false)]
        [TestCase(AdvancementSequenceStage.Scanning, false)]
        [TestCase(AdvancementSequenceStage.Results, false)]
        [TestCase(AdvancementSequenceStage.Continued, true)]
        public void SixthActEquivalent_RequiresAuthoritativeContinuedStage(
            AdvancementSequenceStage stage,
            bool expected)
        {
            Assert.That(CoordinateLockCatalog.IsSixthActEquivalent(
                stage), Is.EqualTo(expected));
        }

        [TestCase(false, true, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void Commit_RequiresEveryAuthoritativeFact(
            bool legacyAnalysisCompleted,
            bool highRiskCompleted,
            bool sixthActReached)
        {
            var attention = new FormalAttentionRuntime();
            var pressure = PressureWithCompletedHighRisk(highRiskCompleted);
            var runtime = new CoordinateLockRuntime(attention, pressure);

            Assert.That(runtime.TryCommit(
                legacyAnalysisCompleted,
                sixthActReached,
                out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(attention.Value, Is.EqualTo(10));
            Assert.That(runtime.Capture().Committed, Is.False);
        }

        [Test]
        public void Commit_RaisesAttentionToNinetyAndQueuesBossOnce()
        {
            var attention = new FormalAttentionRuntime();
            var pressure = PressureWithCompletedHighRisk(true);
            var runtime = new CoordinateLockRuntime(attention, pressure);

            Assert.That(runtime.TryCommit(true, true, out string error),
                Is.True, error);
            Assert.That(attention.Value, Is.EqualTo(90));
            Assert.That(attention.Capture().ReachedThresholds,
                Does.Contain(90));
            Assert.That(pressure.Capture().Entries.Count, Is.EqualTo(3));
            Assert.That(pressure.Capture().Entries[2].Threshold,
                Is.EqualTo(90));
            Assert.That(pressure.Capture().Entries[2].State,
                Is.EqualTo(AttentionPressureState.Queued));
            Assert.That(runtime.Capture().Committed, Is.True);

            Assert.That(runtime.TryCommit(true, true, out error), Is.False);
            Assert.That(attention.Capture().History.Count, Is.EqualTo(1));
            Assert.That(pressure.Capture().Entries.Count, Is.EqualTo(3));
        }

        [Test]
        public void Restore_RejectsInvalidSnapshotWithoutMutation()
        {
            var runtime = new CoordinateLockRuntime(
                new FormalAttentionRuntime(),
                PressureWithCompletedHighRisk(true));
            CoordinateLockSnapshot before = runtime.Capture();

            Assert.That(runtime.TryRestore(
                new CoordinateLockSnapshot(true, 0ul), out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }

        private static AttentionPressureRuntime PressureWithCompletedHighRisk(
            bool completed)
        {
            var runtime = new AttentionPressureRuntime();
            var entries = completed
                ? new[]
                {
                    new AttentionPressureEntrySnapshot(
                        30, AttentionPressureState.Completed, 0f),
                    new AttentionPressureEntrySnapshot(
                        60, AttentionPressureState.Completed, 0f),
                }
                : new[]
                {
                    new AttentionPressureEntrySnapshot(
                        30, AttentionPressureState.Completed, 0f),
                    new AttentionPressureEntrySnapshot(
                        60, AttentionPressureState.Queued, 0f),
                };
            Assert.That(runtime.TryRestore(
                new AttentionPressureSnapshot(2ul, entries),
                out string error), Is.True, error);
            return runtime;
        }
    }
}
