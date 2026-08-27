using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class CrystalBroodmotherPerformanceTests
    {
        private const long FixedStepSnapshotBudgetBytes = 64000L;

        [Test]
        public void EnemyPoolAndBossFixedStepSnapshotStayWithinBudget()
        {
            FieldInfo pool = typeof(GrayboxDefenseWorldView3D).GetField(
                "FormalEnemyPoolCapacity",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(pool, Is.Not.Null);
            Assert.That(pool.GetRawConstantValue(), Is.EqualTo(46));
            Assert.That(SingleCityDefenseCampaignModel.FormalFixedStepSeconds,
                Is.EqualTo(.1f));
            Assert.That(CrystalBroodmotherCatalog.FixedStepSeconds,
                Is.EqualTo(.1f));

            var encounter = new CrystalBroodmotherEncounter(
                "boss.performance.snapshot");
            encounter.Tick(.1f, false, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                encounter.Tick(.1f, false, 0);
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated,
                Is.LessThanOrEqualTo(FixedStepSnapshotBudgetBytes));
            Assert.That(encounter.Capture().Defeated, Is.False);
        }

        [Test]
        public void BossCampaignChunksAndSaveBreakpointProduceSameResult()
        {
            SingleCityDefenseCampaignModel whole = CreateBossCampaign();
            SingleCityDefenseCampaignModel split = CreateBossCampaign();
            SingleCityDefenseCampaignModel checkpointed =
                CreateBossCampaign();

            whole.Advance(5.7f, 1);
            for (var index = 0; index < 57; index++)
                split.Advance(.1f, 1);
            for (var index = 0; index < 23; index++)
                checkpointed.Advance(.1f, 1);

            SingleCityDefenseCampaignPersistenceState saved =
                checkpointed.CaptureForPersistence();
            var resumed = new SingleCityDefenseCampaignModel(
                0f, 0f, AttentionPressureCampaignCatalog.Boss);
            Assert.That(resumed.TryPrepareRestore(
                saved,
                out SingleCityDefenseCampaignRestorePlan plan,
                out string error), Is.True, error);
            Assert.That(resumed.TryCommitRestore(plan, out error),
                Is.True, error);
            for (var index = 0; index < 34; index++)
                resumed.Advance(.1f, 1);

            AssertCampaignEquivalent(whole, split);
            AssertCampaignEquivalent(whole, resumed);
            Assert.That(whole.TryInjectReinforcements(
                "boss.performance.phase-70",
                new[] { new WaveEntry(EnemyArchetype.CrystalBeast, 4) }),
                Is.False,
                "The same stable event cannot grow the campaign later.");
        }

        private static SingleCityDefenseCampaignModel CreateBossCampaign()
        {
            var model = new SingleCityDefenseCampaignModel(
                0f, 0f, AttentionPressureCampaignCatalog.Boss);
            Assert.That(model.TryStartAfterExternalWarning(), Is.True);
            Assert.That(model.TryInjectReinforcements(
                "boss.performance.phase-70",
                new[] { new WaveEntry(EnemyArchetype.CrystalBeast, 4) }),
                Is.True);
            Assert.That(model.TryInjectReinforcements(
                "boss.performance.phase-70",
                new[] { new WaveEntry(EnemyArchetype.CrystalBeast, 4) }),
                Is.False);
            return model;
        }

        private static void AssertCampaignEquivalent(
            SingleCityDefenseCampaignModel expected,
            SingleCityDefenseCampaignModel actual)
        {
            SingleCityDefenseCampaignSnapshot left = expected.Snapshot;
            SingleCityDefenseCampaignSnapshot right = actual.Snapshot;
            Assert.That(right.Phase, Is.EqualTo(left.Phase));
            Assert.That(right.CurrentWaveNumber,
                Is.EqualTo(left.CurrentWaveNumber));
            Assert.That(right.PlannedEnemyCount,
                Is.EqualTo(left.PlannedEnemyCount));
            Assert.That(right.SpawnedEnemyCount,
                Is.EqualTo(left.SpawnedEnemyCount));
            Assert.That(right.AliveEnemyCount,
                Is.EqualTo(left.AliveEnemyCount));
            Assert.That(right.CoreCurrentHealth,
                Is.EqualTo(left.CoreCurrentHealth));
            Assert.That(right.Enemies.Select(value => value.StableId),
                Is.EqualTo(left.Enemies.Select(value => value.StableId)));
            Assert.That(right.Enemies.Select(value => value.CurrentHealth),
                Is.EqualTo(left.Enemies.Select(value => value.CurrentHealth)));
            SingleCityDefenseCampaignPersistenceState leftSaved =
                expected.CaptureForPersistence();
            SingleCityDefenseCampaignPersistenceState rightSaved =
                actual.CaptureForPersistence();
            Assert.That(rightSaved.FixedStepAccumulatorSeconds,
                Is.EqualTo(leftSaved.FixedStepAccumulatorSeconds)
                    .Within(.0001f));
            Assert.That(rightSaved.InjectedReinforcements,
                Has.Count.EqualTo(leftSaved.InjectedReinforcements.Count));
        }
    }
}
