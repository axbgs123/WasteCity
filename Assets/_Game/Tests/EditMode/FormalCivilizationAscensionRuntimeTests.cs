using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class FormalCivilizationAscensionRuntimeTests
    {
        [TestCase(false, 2, true, true)]
        [TestCase(true, 1, true, true)]
        [TestCase(true, 2, false, true)]
        [TestCase(true, 2, true, false)]
        public void IDEA0020_AllFourRequirementsAreMandatory(
            bool legacyAnalysis,
            int machineGunCount,
            bool bossDefeated,
            bool productionRunning)
        {
            var runtime = new FormalCivilizationAscensionRuntime(
                FormalFateCatalog.PocketUniverseId);
            var requirements = new FormalCivilizationAscensionRequirements(
                legacyAnalysis, machineGunCount, bossDefeated,
                productionRunning);
            Assert.That(runtime.TryAscend(requirements, out _, out _), Is.False);
            Assert.That(runtime.Capture().CivilizationLevel, Is.EqualTo(1));
        }

        [Test]
        public void IDEA0020_SuccessCommitsOnceAndEmitsStableIntegrationCommand()
        {
            var runtime = new FormalCivilizationAscensionRuntime(
                FormalFateCatalog.RewindAnchorId);
            var requirements = new FormalCivilizationAscensionRequirements(
                true, 2, true, true);
            Assert.That(runtime.TryAscend(
                requirements,
                out FormalCivilizationAscensionCommand command,
                out string error), Is.True, error);
            FormalCivilizationAscensionSnapshot snapshot = runtime.Capture();
            Assert.That(snapshot.CivilizationLevel, Is.EqualTo(2));
            Assert.That(snapshot.FateLevel, Is.EqualTo(2));
            Assert.That(command.AttentionReasonId,
                Is.EqualTo("core.attention.civilization.advanced"));
            Assert.That(command.AttentionDelta, Is.EqualTo(25));
            Assert.That(command.CheckpointReasonId,
                Is.EqualTo("first-civilization-ascension"));
            Assert.That(command.RewindAnchorCapacity, Is.EqualTo(2));
            Assert.That(runtime.TryAscend(requirements, out _, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(snapshot));
        }

        [Test]
        public void IDEA0020_PendingOwnerBindsExactlyOneFormalFate()
        {
            var runtime = new FormalCivilizationAscensionRuntime();
            FormalCivilizationAscensionSnapshot pending = runtime.Capture();
            Assert.That(pending.FateId, Is.Empty);
            Assert.That(pending.FateLevel, Is.Zero);
            Assert.That(runtime.TryBindFate(
                FormalFateCatalog.PocketUniverseId,
                out string error), Is.True, error);
            Assert.That(runtime.Capture().FateLevel, Is.EqualTo(1));
            Assert.That(runtime.TryBindFate(
                FormalFateCatalog.VoidDebtId,
                out error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void IDEA0020_FormalRestoreCanRollbackBoundOwnerToPending()
        {
            var runtime = new FormalCivilizationAscensionRuntime();
            FormalCivilizationAscensionSnapshot pending = runtime.Capture();
            Assert.That(runtime.TryBindFate(
                FormalFateCatalog.PocketUniverseId,
                out string error), Is.True, error);

            Assert.That(runtime.TryRestore(pending, out error), Is.True, error);
            Assert.That(runtime.Capture().FateId, Is.Empty);
            Assert.That(runtime.Capture().FateLevel, Is.Zero);
            Assert.That(runtime.TryBindFate(
                FormalFateCatalog.VoidDebtId,
                out error), Is.True, error);
        }

        [Test]
        public void IDEA0020_FormalRestoreCanResetAscendedOwnerToPending()
        {
            var runtime = new FormalCivilizationAscensionRuntime(
                FormalFateCatalog.RewindAnchorId);
            Assert.That(runtime.TryAscend(
                new FormalCivilizationAscensionRequirements(
                    true, 2, true, true),
                out _,
                out string error), Is.True, error);
            var pending = new FormalCivilizationAscensionSnapshot(
                1, string.Empty, 0, false, 0UL);

            Assert.That(runtime.TryRestore(pending, out error), Is.True, error);
            Assert.That(runtime.Capture().CivilizationLevel, Is.EqualTo(1));
            Assert.That(runtime.Capture().FateId, Is.Empty);
            Assert.That(runtime.Capture().FateLevel, Is.Zero);
            Assert.That(runtime.Capture().Ascended, Is.False);
        }

        [Test]
        public void IDEA0020_CaptureIsStableAndInvalidRestoreIsAtomic()
        {
            var runtime = new FormalCivilizationAscensionRuntime(
                FormalFateCatalog.VoidDebtId);
            FormalCivilizationAscensionSnapshot stable = runtime.Capture();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                if (!ReferenceEquals(runtime.Capture(), stable)) Assert.Fail();
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before,
                Is.Zero);
            var invalid = new FormalCivilizationAscensionSnapshot(
                2, FormalFateCatalog.VoidDebtId, 1, false, 4UL);
            Assert.That(runtime.TryRestore(invalid, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.Capture(), Is.SameAs(stable));
        }

        [Test]
        public void IDEA0020_RequirementsExposeStableOrderedChineseProjection()
        {
            var requirements = new FormalCivilizationAscensionRequirements(
                false, 1, false, false);
            Assert.That(requirements.Statuses.Select(value => value.StableId),
                Is.EqualTo(new[]
                {
                    "legacy-analysis", "machine-gun-turrets",
                    "crystal-broodmother", "production-running",
                }));
            Assert.That(requirements.Statuses.All(value =>
                !string.IsNullOrWhiteSpace(value.DisplayName) &&
                !string.IsNullOrWhiteSpace(value.MissingText)), Is.True);
        }

        [Test]
        public void IDEA0020_ReadOnlyPreparationProjectionOwnsRuleAndRewards()
        {
            var pending = new FormalCivilizationAscensionRuntime();
            var requirements = new FormalCivilizationAscensionRequirements(
                true, 2, true, true);
            Assert.That(pending.CanPrepareAscension(requirements), Is.False);
            Assert.That(pending.TryBindFate(
                FormalFateCatalog.PocketUniverseId,
                out string error), Is.True, error);
            Assert.That(pending.CanPrepareAscension(requirements), Is.True);
            Assert.That(pending.TargetCivilizationLevel, Is.EqualTo(2));
            Assert.That(pending.TargetFateLevel, Is.EqualTo(2));
            Assert.That(pending.AttentionReward,
                Is.EqualTo(FormalAttentionCatalog.Find(
                    pending.AttentionReasonId).Delta));

            Assert.That(pending.TryAscend(
                requirements, out _, out error), Is.True, error);
            Assert.That(pending.CanPrepareAscension(requirements), Is.False);
        }
    }
}
