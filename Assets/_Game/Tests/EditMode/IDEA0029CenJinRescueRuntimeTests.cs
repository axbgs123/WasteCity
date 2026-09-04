using NUnit.Framework;
using WasteCity.Leader.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029CenJinRescueRuntimeTests
    {
        [Test]
        public void DistressRemainsHiddenUntilSiteIsVisibleAndDiscoveryIsIdempotent()
        {
            var runtime = new CenJinDistressRuntime("session-001");

            Assert.That(runtime.TryDiscover(siteVisible: false), Is.False);
            Assert.That(runtime.State, Is.EqualTo(CenJinDistressState.Undiscovered));
            Assert.That(runtime.TryDiscover(siteVisible: true), Is.True);
            Assert.That(runtime.TryDiscover(siteVisible: true), Is.False);
            Assert.That(runtime.State, Is.EqualTo(CenJinDistressState.Discovered));
            Assert.That(runtime.ElapsedSinceDiscoverySeconds, Is.Zero);
        }

        [Test]
        public void TimelyRescueReservesTenRunsTwelveSecondsAndCommitsRewardsOnce()
        {
            var runtime = Discovered();
            var transaction = new FakeTransaction();

            Assert.That(
                runtime.TryBeginRescue(
                    cityDistance: 3f,
                    canOperate: true,
                    transaction,
                    out string error),
                Is.True,
                error);
            Assert.That(transaction.Reserved, Is.EqualTo(10));
            Assert.That(runtime.RescueRemainingSeconds, Is.EqualTo(12f));

            CenJinDistressTickResult before = runtime.Tick(
                11.99f,
                paused: false,
                cityDistance: 3f,
                canOperate: true,
                transaction);
            CenJinDistressTickResult completed = runtime.Tick(
                .01f,
                paused: false,
                cityDistance: 3f,
                canOperate: true,
                transaction);

            Assert.That(before.Kind, Is.EqualTo(CenJinDistressTickKind.Progressed));
            Assert.That(completed.Kind, Is.EqualTo(CenJinDistressTickKind.Completed));
            Assert.That(completed.Outcome, Is.EqualTo(CenJinRescueOutcome.Timely));
            Assert.That(runtime.State, Is.EqualTo(CenJinDistressState.RescuedTimely));
            Assert.That(transaction.CommitCount, Is.EqualTo(1));
            Assert.That(transaction.LastRequest.Injured, Is.False);
            Assert.That(transaction.LastRequest.PopulationReward, Is.EqualTo(40));
            Assert.That(
                transaction.LastRequest.AttentionReasonId,
                Is.EqualTo("core.attention.rescue.cen-jin"));
            Assert.That(
                transaction.LastRequest.StableEventKey,
                Is.EqualTo("core.exploration.site.cen-jin-distress:session-001"));

            Assert.That(
                runtime.Tick(30f, false, 0f, true, transaction).Kind,
                Is.EqualTo(CenJinDistressTickKind.None));
            Assert.That(transaction.CommitCount, Is.EqualTo(1));
        }

        [Test]
        public void CompletionAtNinetySecondsIsDelayedAndCriticalNeverDeletesEvent()
        {
            var delayed = Discovered();
            var delayedTransaction = new FakeTransaction();
            delayed.Tick(
                78f,
                paused: false,
                cityDistance: 0f,
                canOperate: true,
                delayedTransaction);
            delayed.TryBeginRescue(0f, true, delayedTransaction, out _);
            CenJinDistressTickResult delayedResult = delayed.Tick(
                12f,
                false,
                0f,
                true,
                delayedTransaction);

            Assert.That(delayedResult.Outcome, Is.EqualTo(CenJinRescueOutcome.Delayed));
            Assert.That(delayedTransaction.LastRequest.Injured, Is.True);

            var critical = Discovered();
            var criticalTransaction = new FakeTransaction();
            critical.Tick(180f, false, 0f, true, criticalTransaction);
            Assert.That(critical.IsCritical, Is.True);
            Assert.That(critical.State, Is.EqualTo(CenJinDistressState.Discovered));
            Assert.That(
                critical.TryBeginRescue(0f, true, criticalTransaction, out _),
                Is.True);
            Assert.That(
                critical.Tick(12f, false, 0f, true, criticalTransaction).Outcome,
                Is.EqualTo(CenJinRescueOutcome.Delayed));
        }

        [Test]
        public void PauseFreezesAndLeavingRangeRefundsReservation()
        {
            var runtime = Discovered();
            var transaction = new FakeTransaction();
            runtime.TryBeginRescue(0f, true, transaction, out _);

            CenJinDistressTickResult paused = runtime.Tick(
                20f,
                paused: true,
                cityDistance: 0f,
                canOperate: true,
                transaction);
            CenJinDistressTickResult cancelled = runtime.Tick(
                1f,
                paused: false,
                cityDistance: 3.01f,
                canOperate: true,
                transaction);

            Assert.That(paused.Kind, Is.EqualTo(CenJinDistressTickKind.Paused));
            Assert.That(runtime.State, Is.EqualTo(CenJinDistressState.Discovered));
            Assert.That(cancelled.Kind, Is.EqualTo(CenJinDistressTickKind.Cancelled));
            Assert.That(transaction.Released, Is.EqualTo(10));
            Assert.That(runtime.ReservedBiomass, Is.Zero);
        }

        [Test]
        public void ReserveAndCommitFailuresDoNotPartiallyChangeEvent()
        {
            var runtime = Discovered();
            var reserveFailure = new FakeTransaction { ReserveSucceeds = false };
            Assert.That(
                runtime.TryBeginRescue(0f, true, reserveFailure, out _),
                Is.False);
            Assert.That(runtime.State, Is.EqualTo(CenJinDistressState.Discovered));
            Assert.That(runtime.ReservedBiomass, Is.Zero);

            var commitFailure = new FakeTransaction { CommitSucceeds = false };
            Assert.That(runtime.TryBeginRescue(0f, true, commitFailure, out _), Is.True);
            CenJinDistressTickResult failed = runtime.Tick(
                12f,
                false,
                0f,
                true,
                commitFailure);
            Assert.That(failed.Kind, Is.EqualTo(CenJinDistressTickKind.CommitFailed));
            Assert.That(runtime.State, Is.EqualTo(CenJinDistressState.Rescuing));
            Assert.That(runtime.ReservedBiomass, Is.EqualTo(10));

            commitFailure.CommitSucceeds = true;
            CenJinDistressTickResult retried = runtime.Tick(
                0f,
                false,
                0f,
                true,
                commitFailure);
            Assert.That(retried.Kind, Is.EqualTo(CenJinDistressTickKind.Completed));
            Assert.That(commitFailure.CommitCount, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotRoundTripAndLegacyStatePreserveOneTimeOutcome()
        {
            var source = Discovered("session-persist");
            var transaction = new FakeTransaction();
            source.Tick(100f, false, 0f, true, transaction);
            source.TryBeginRescue(0f, true, transaction, out _);
            source.Tick(4f, false, 0f, true, transaction);

            CenJinDistressSnapshot snapshot = source.Capture();
            var restored = new CenJinDistressRuntime("session-persist");
            Assert.That(restored.TryRestore(snapshot, out string error), Is.True, error);
            Assert.That(restored.State, Is.EqualTo(CenJinDistressState.Rescuing));
            Assert.That(restored.RescueRemainingSeconds, Is.EqualTo(8f));
            Assert.That(restored.ReservedBiomass, Is.EqualTo(10));

            CenJinDistressRuntime legacy =
                CenJinDistressRuntime.CreateLegacyRescued("legacy-session");
            Assert.That(legacy.State, Is.EqualTo(CenJinDistressState.RescuedLegacy));
            Assert.That(legacy.IsCompleted, Is.True);
            Assert.That(legacy.TryDiscover(true), Is.False);
        }

        private static CenJinDistressRuntime Discovered(
            string sessionId = "session-001")
        {
            var runtime = new CenJinDistressRuntime(sessionId);
            Assert.That(runtime.TryDiscover(true), Is.True);
            return runtime;
        }

        private sealed class FakeTransaction : ICenJinDistressTransaction
        {
            public bool ReserveSucceeds { get; set; } = true;
            public bool ReleaseSucceeds { get; set; } = true;
            public bool CommitSucceeds { get; set; } = true;
            public int Reserved { get; private set; }
            public int Released { get; private set; }
            public int CommitCount { get; private set; }
            public CenJinRescueCommitRequest LastRequest { get; private set; }

            public bool TryReserveBiomass(int amount, out string error)
            {
                if (!ReserveSucceeds)
                {
                    error = "生物质不足";
                    return false;
                }
                Reserved += amount;
                error = string.Empty;
                return true;
            }

            public bool TryReleaseBiomass(int amount, out string error)
            {
                if (!ReleaseSucceeds)
                {
                    error = "返还失败";
                    return false;
                }
                Released += amount;
                error = string.Empty;
                return true;
            }

            public bool TryCommit(
                CenJinRescueCommitRequest request,
                out string error)
            {
                CommitCount++;
                LastRequest = request;
                if (!CommitSucceeds)
                {
                    error = "组合事务失败";
                    return false;
                }
                error = string.Empty;
                return true;
            }
        }
    }
}
