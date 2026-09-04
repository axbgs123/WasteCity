using System;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Leader.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029ManualGatherRuntimeTests
    {
        [Test]
        public void StartRequiresQualifiedManualLeaderVisibleNodeAndDistance()
        {
            var runtime = new ManualGatherRuntime();

            Assert.That(runtime.TryStart(Context(), out string error), Is.True, error);
            Assert.That(runtime.IsActive, Is.True);
            Assert.That(runtime.TargetStableId, Is.EqualTo("world.deposit.safe-001"));

            runtime.Cancel();
            Assert.That(
                runtime.TryStart(Context(distance: 1.5001f), out error),
                Is.False);
            Assert.That(error, Does.Contain("距离"));
            Assert.That(
                runtime.TryStart(Context(targetVisible: false), out error),
                Is.False);
            Assert.That(error, Does.Contain("视野"));
            Assert.That(
                runtime.TryStart(Context(mode: LeaderControlMode.AI), out error),
                Is.False);
            Assert.That(error, Does.Contain("手动"));
            Assert.That(
                runtime.TryStart(Context(resourceId: ResourceIds.Alloy), out error),
                Is.False);
            Assert.That(error, Does.Contain("资源"));
        }

        [Test]
        public void SixRuleSecondsCommitsExactlyOneUnitAndPauseFreezes()
        {
            var runtime = new ManualGatherRuntime();
            runtime.TryStart(Context(), out _);
            int commits = 0;
            Func<string, WorldHarvestTransactionResult> commit = _ =>
            {
                commits++;
                return WorldHarvestTransactionResult.Completed(ResourceIds.Iron);
            };

            ManualGatherTickResult paused = runtime.Tick(
                5f,
                paused: true,
                Context(),
                commit);
            ManualGatherTickResult before = runtime.Tick(
                5.99f,
                paused: false,
                Context(),
                commit);
            ManualGatherTickResult completed = runtime.Tick(
                .01f,
                paused: false,
                Context(),
                commit);

            Assert.That(paused.Status, Is.EqualTo(ManualGatherStatus.Paused));
            Assert.That(before.UnitsGathered, Is.Zero);
            Assert.That(completed.UnitsGathered, Is.EqualTo(1));
            Assert.That(completed.Status, Is.EqualTo(ManualGatherStatus.Gathered));
            Assert.That(commits, Is.EqualTo(1));
            Assert.That(runtime.ElapsedSeconds, Is.EqualTo(0f).Within(.0001f));
        }

        [Test]
        public void RepeatingStartForSameTargetDoesNotResetProgress()
        {
            var runtime = new ManualGatherRuntime();
            runtime.TryStart(Context(), out _);
            runtime.Tick(
                3f,
                false,
                Context(),
                _ => throw new AssertionException("cycle should not commit"));
            ulong revision = runtime.Revision;

            Assert.That(runtime.TryStart(Context(), out string error), Is.True, error);
            Assert.That(runtime.ElapsedSeconds, Is.EqualTo(3f).Within(.0001f));
            Assert.That(runtime.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void LargeDeltaRevalidatesEachSingleUnitAndStopsAtDepletion()
        {
            var runtime = new ManualGatherRuntime();
            runtime.TryStart(Context(nodeAmount: 2), out _);
            int commits = 0;

            ManualGatherTickResult result = runtime.Tick(
                30f,
                paused: false,
                Context(nodeAmount: 2),
                _ =>
                {
                    commits++;
                    return commits <= 2
                        ? WorldHarvestTransactionResult.Completed(ResourceIds.Iron)
                        : WorldHarvestTransactionResult.Failed(
                            WorldHarvestTransactionStatus.NodeDepleted,
                            "矿脉已枯竭");
                });

            Assert.That(result.UnitsGathered, Is.EqualTo(2));
            Assert.That(result.Status, Is.EqualTo(ManualGatherStatus.NodeDepleted));
            Assert.That(commits, Is.EqualTo(3));
            Assert.That(runtime.IsActive, Is.False);
        }

        [Test]
        public void BackpackFullKeepsReadyProgressAndCanRetryWithoutMiningAgain()
        {
            var runtime = new ManualGatherRuntime();
            runtime.TryStart(Context(), out _);
            int attempts = 0;

            ManualGatherTickResult blocked = runtime.Tick(
                6f,
                false,
                Context(),
                _ =>
                {
                    attempts++;
                    return WorldHarvestTransactionResult.Failed(
                        WorldHarvestTransactionStatus.BackpackFull,
                        "背包已满");
                });
            ManualGatherTickResult retried = runtime.Tick(
                0f,
                false,
                Context(),
                _ =>
                {
                    attempts++;
                    return WorldHarvestTransactionResult.Completed(ResourceIds.Iron);
                });

            Assert.That(blocked.Status, Is.EqualTo(ManualGatherStatus.BackpackFull));
            Assert.That(runtime.IsActive, Is.True);
            Assert.That(retried.UnitsGathered, Is.EqualTo(1));
            Assert.That(attempts, Is.EqualTo(2));
        }

        [TestCase(false, 1f, LeaderControlMode.Manual, CharacterLifeState.Active)]
        [TestCase(true, 1.6f, LeaderControlMode.Manual, CharacterLifeState.Active)]
        [TestCase(true, 1f, LeaderControlMode.AI, CharacterLifeState.Active)]
        [TestCase(true, 1f, LeaderControlMode.Manual, CharacterLifeState.Downed)]
        public void LosingEligibilityCancelsUnfinishedCycle(
            bool visible,
            float distance,
            LeaderControlMode mode,
            CharacterLifeState state)
        {
            var runtime = new ManualGatherRuntime();
            runtime.TryStart(Context(), out _);
            runtime.Tick(3f, false, Context(), _ =>
                throw new AssertionException("cycle should not commit"));

            ManualGatherTickResult result = runtime.Tick(
                1f,
                false,
                Context(
                    targetVisible: visible,
                    distance: distance,
                    mode: mode,
                    state: state),
                _ => throw new AssertionException("invalid cycle committed"));

            Assert.That(result.Status, Is.EqualTo(ManualGatherStatus.Interrupted));
            Assert.That(runtime.IsActive, Is.False);
            Assert.That(runtime.ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void WorldTransactionChecksCapacityBeforeHarvestAndRollsBackAddFailure()
        {
            int harvested = 0;
            int rolledBack = 0;
            WorldHarvestTransactionResult full = WorldHarvestTransaction.TryCommitOne(
                ResourceIds.Iron,
                canAccept: (_, __) => false,
                harvest: (_, __) => { harvested++; return true; },
                addToBackpack: (_, __) => true,
                rollbackHarvest: (_, amount) => rolledBack += amount);

            Assert.That(full.Status, Is.EqualTo(WorldHarvestTransactionStatus.BackpackFull));
            Assert.That(harvested, Is.Zero);

            WorldHarvestTransactionResult failed = WorldHarvestTransaction.TryCommitOne(
                ResourceIds.Iron,
                canAccept: (_, __) => true,
                harvest: (_, __) => { harvested++; return true; },
                addToBackpack: (_, __) => false,
                rollbackHarvest: (_, amount) => rolledBack += amount);

            Assert.That(failed.Status, Is.EqualTo(WorldHarvestTransactionStatus.CommitFailed));
            Assert.That(harvested, Is.EqualTo(1));
            Assert.That(rolledBack, Is.EqualTo(1));
        }

        [Test]
        public void WorldTransactionDoesNotRollbackWhenFailurePrecedesHarvest()
        {
            int rolledBack = 0;

            WorldHarvestTransactionResult result =
                WorldHarvestTransaction.TryCommitOne(
                    ResourceIds.Iron,
                    canAccept: (_, __) => throw new InvalidOperationException(),
                    harvest: (_, __) => true,
                    addToBackpack: (_, __) => true,
                    rollbackHarvest: (_, amount) => rolledBack += amount);

            Assert.That(
                result.Status,
                Is.EqualTo(WorldHarvestTransactionStatus.CommitFailed));
            Assert.That(rolledBack, Is.Zero);
        }

        [Test]
        public void SnapshotRestoresTargetAndRemainingCycleAtomically()
        {
            var source = new ManualGatherRuntime();
            source.TryStart(Context(), out _);
            source.Tick(
                2f,
                false,
                Context(),
                _ => throw new AssertionException("cycle should not commit"));

            ManualGatherSnapshot snapshot = source.Capture();
            var restored = new ManualGatherRuntime();

            Assert.That(restored.TryRestore(snapshot, out string error), Is.True, error);
            Assert.That(restored.IsActive, Is.True);
            Assert.That(restored.TargetStableId, Is.EqualTo("world.deposit.safe-001"));
            Assert.That(restored.TargetResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(restored.RemainingSeconds, Is.EqualTo(4f).Within(.0001f));

            var invalid = new ManualGatherSnapshot(
                true,
                string.Empty,
                ResourceIds.Iron,
                3f);
            Assert.That(restored.TryRestore(invalid, out error), Is.False);
            Assert.That(restored.TargetStableId, Is.EqualTo("world.deposit.safe-001"));
            Assert.That(restored.RemainingSeconds, Is.EqualTo(4f).Within(.0001f));
        }

        private static ManualGatherContext Context(
            bool leaderRecruited = true,
            CharacterLifeState state = CharacterLifeState.Active,
            LeaderControlMode mode = LeaderControlMode.Manual,
            bool modalBlocked = false,
            bool targetVisible = true,
            string targetId = "world.deposit.safe-001",
            string resourceId = ResourceIds.Iron,
            int nodeAmount = 10,
            float distance = 1.5f)
        {
            return new ManualGatherContext(
                leaderRecruited,
                state,
                mode,
                modalBlocked,
                targetVisible,
                targetId,
                resourceId,
                nodeAmount,
                distance);
        }
    }
}
