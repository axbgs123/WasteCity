using System.Linq;
using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class VoidChestRuntimeTests
    {
        [Test]
        public void IDEA0028_StableDeathAndOrdinalYieldExactlyOnePercentWindow()
        {
            bool[] rolls = Enumerable.Range(1, 100)
                .Select(value => VoidChestRuntime.ShouldDrop(
                    "enemy-death:stable-001", (ulong)value))
                .ToArray();
            Assert.That(rolls.Count(value => value), Is.EqualTo(1));
            Assert.That(rolls, Is.EqualTo(Enumerable.Range(1, 100)
                .Select(value => VoidChestRuntime.ShouldDrop(
                    "enemy-death:stable-001", (ulong)value))));
        }

        [Test]
        public void IDEA0028_DropWindowIncludesSessionAndSelectionVersion()
        {
            bool[] first = Enumerable.Range(1, 100)
                .Select(value => VoidChestRuntime.ShouldDrop(
                    "session:first", 3, "enemy-death:stable", (ulong)value))
                .ToArray();
            bool[] repeated = Enumerable.Range(1, 100)
                .Select(value => VoidChestRuntime.ShouldDrop(
                    "session:first", 3, "enemy-death:stable", (ulong)value))
                .ToArray();
            bool[] otherSession = Enumerable.Range(1, 100)
                .Select(value => VoidChestRuntime.ShouldDrop(
                    "session:other", 3, "enemy-death:stable", (ulong)value))
                .ToArray();
            bool[] otherVersion = Enumerable.Range(1, 100)
                .Select(value => VoidChestRuntime.ShouldDrop(
                    "session:first", 4, "enemy-death:stable", (ulong)value))
                .ToArray();

            Assert.That(first.Count(value => value), Is.EqualTo(1));
            Assert.That(otherSession.Count(value => value), Is.EqualTo(1));
            Assert.That(otherVersion.Count(value => value), Is.EqualTo(1));
            Assert.That(repeated, Is.EqualTo(first));
            Assert.That(otherSession, Is.Not.EqualTo(first));
            Assert.That(otherVersion, Is.Not.EqualTo(first));
        }

        [Test]
        public void IDEA0028_EvaluationAndClaimAreBothIdempotent()
        {
            var runtime = new VoidChestRuntime();
            ulong dropOrdinal = Enumerable.Range(1, 100)
                .Select(value => (ulong)value)
                .Single(value => VoidChestRuntime.ShouldDrop(
                    "enemy-death:stable-002", value));

            Assert.That(runtime.TryEvaluateDeath(
                "enemy-death:stable-002", dropOrdinal,
                out VoidChestEvaluation first, out string error), Is.True, error);
            Assert.That(first.Dropped, Is.True);
            Assert.That(first.ChestId, Is.Not.Empty);
            Assert.That(first.ResourceId, Is.Not.Empty);
            Assert.That(first.Amount, Is.GreaterThan(0));
            Assert.That(first.NarrativeFragmentId, Is.Not.Empty);
            Assert.That(runtime.Capture().UnclaimedChestIds,
                Is.EqualTo(new[] { first.ChestId }));

            Assert.That(runtime.TryEvaluateDeath(
                "enemy-death:stable-002", dropOrdinal,
                out VoidChestEvaluation duplicate, out _), Is.False);
            Assert.That(duplicate.ChestId, Is.EqualTo(first.ChestId));
            Assert.That(runtime.TryClaim(first.ChestId, out _), Is.True);
            Assert.That(runtime.TryClaim(first.ChestId, out _), Is.False);
            Assert.That(runtime.Capture().UnclaimedChestIds, Is.Empty);
            Assert.That(runtime.Capture().ClaimedChestIds,
                Is.EqualTo(new[] { first.ChestId }));
        }

        [Test]
        public void IDEA0028_InvalidDeathDoesNotMutateState()
        {
            var runtime = new VoidChestRuntime();
            VoidChestSnapshot before = runtime.Capture();
            Assert.That(runtime.TryEvaluateDeath(
                string.Empty, 1, out _, out _), Is.False);
            Assert.That(runtime.TryEvaluateDeath(
                "enemy-death:stable", 0, out _, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }

        [Test]
        public void IDEA0028_RestoreRoundTripsEvaluationsAndClaimState()
        {
            var source = new VoidChestRuntime("session:restore", 3);
            ulong dropOrdinal = Enumerable.Range(1, 100)
                .Select(value => (ulong)value)
                .Single(value => VoidChestRuntime.ShouldDrop(
                    "session:restore", 3, "enemy-death:restore", value));
            Assert.That(source.TryEvaluateDeath(
                "enemy-death:restore", dropOrdinal,
                out VoidChestEvaluation dropped, out string error),
                Is.True, error);
            Assert.That(source.TryEvaluateDeath(
                "enemy-death:no-drop", 1,
                out _, out error), Is.True, error);
            Assert.That(source.TryClaim(dropped.ChestId, out error),
                Is.True, error);

            var restored = new VoidChestRuntime("session:restore", 3);
            Assert.That(restored.TryRestore(
                source.Capture(), out error), Is.True, error);
            Assert.That(restored.Capture().Revision,
                Is.EqualTo(source.Capture().Revision));
            Assert.That(restored.Capture().Evaluations.Select(value =>
                    (value.DeathId, value.SequenceOrdinal, value.Dropped,
                        value.ChestId, value.Claimed)),
                Is.EqualTo(source.Capture().Evaluations.Select(value =>
                    (value.DeathId, value.SequenceOrdinal, value.Dropped,
                        value.ChestId, value.Claimed))));
            Assert.That(restored.Capture().ClaimedChestIds,
                Is.EqualTo(new[] { dropped.ChestId }));
            Assert.That(restored.TryClaim(dropped.ChestId, out _), Is.False);
        }

        [Test]
        public void IDEA0028_InvalidRestoreIsAtomic()
        {
            var runtime = new VoidChestRuntime();
            VoidChestSnapshot before = runtime.Capture();
            var invalidEvaluation = new VoidChestEvaluation(
                "enemy-death:invalid",
                1,
                true,
                "void-chest:forged",
                false,
                "core.resource.iron",
                99,
                "core.narrative.void-chest.forged");
            var invalid = new VoidChestSnapshot(
                1,
                new[] { invalidEvaluation },
                new[] { invalidEvaluation.ChestId },
                new string[0]);

            Assert.That(runtime.TryRestore(invalid, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }
    }
}
