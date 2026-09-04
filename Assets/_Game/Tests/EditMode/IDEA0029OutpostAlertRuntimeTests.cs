using System.Linq;
using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029OutpostAlertRuntimeTests
    {
        [Test]
        public void NewFactCapturesStableIdentityTimesAndUnreadActiveState()
        {
            var runtime = new OutpostAlertRuntime();

            Assert.That(Report(
                runtime,
                "attack.000001",
                "core.outpost.000001",
                OutpostAlertSeverity.UnderAttack,
                risk: 60,
                ruleTime: 12d,
                out string error), Is.True, error);

            OutpostAlertEntry entry = runtime.Get("attack.000001");
            Assert.That(entry.StableAlertId, Is.EqualTo("attack.000001"));
            Assert.That(entry.SettlementId,
                Is.EqualTo("core.outpost.000001"));
            Assert.That(entry.X, Is.EqualTo(14));
            Assert.That(entry.Y, Is.EqualTo(9));
            Assert.That(entry.ThreatSummary, Is.EqualTo("啮噬者正在攻击"));
            Assert.That(entry.Severity,
                Is.EqualTo(OutpostAlertSeverity.UnderAttack));
            Assert.That(entry.EstimatedLossRiskPercent, Is.EqualTo(60));
            Assert.That(entry.FirstRuleTime, Is.EqualTo(12d));
            Assert.That(entry.LatestRuleTime, Is.EqualTo(12d));
            Assert.That(entry.IsAcknowledged, Is.False);
            Assert.That(entry.IsResolved, Is.False);
            Assert.That(runtime.ActiveAlerts, Has.Count.EqualTo(1));
            Assert.That(runtime.UnacknowledgedAlerts, Has.Count.EqualTo(1));
            Assert.That(runtime.Revision, Is.EqualTo(1UL));

            Assert.That(Report(
                runtime,
                "attack.000001",
                "core.outpost.000001",
                OutpostAlertSeverity.UnderAttack,
                60,
                12d,
                out error), Is.True, error);
            Assert.That(runtime.Revision, Is.EqualTo(1UL),
                "The same authoritative fact must be idempotent.");
        }

        [Test]
        public void SameActiveFactCanUpgradeWithoutLosingFirstTime()
        {
            var runtime = new OutpostAlertRuntime();
            Assert.That(Report(
                runtime, "attack.1", "outpost.a",
                OutpostAlertSeverity.Guard, 20, 5d,
                out string error), Is.True, error);
            Assert.That(runtime.TryAcknowledge("attack.1"), Is.True);
            Assert.That(runtime.Get("attack.1").IsAcknowledged, Is.True);

            Assert.That(Report(
                runtime, "attack.1", "outpost.a",
                OutpostAlertSeverity.UnderAttack, 55, 8d,
                out error), Is.True, error);
            OutpostAlertEntry upgraded = runtime.Get("attack.1");
            Assert.That(upgraded.Severity,
                Is.EqualTo(OutpostAlertSeverity.UnderAttack));
            Assert.That(upgraded.FirstRuleTime, Is.EqualTo(5d));
            Assert.That(upgraded.LatestRuleTime, Is.EqualTo(8d));
            Assert.That(upgraded.IsAcknowledged, Is.False,
                "A severity escalation must become unread again.");

            Assert.That(Report(
                runtime, "attack.1", "outpost.a",
                OutpostAlertSeverity.Guard, 30, 10d,
                out error), Is.True, error);
            Assert.That(runtime.Get("attack.1").Severity,
                Is.EqualTo(OutpostAlertSeverity.UnderAttack),
                "An active fact keeps its highest reported severity.");
            Assert.That(runtime.Get("attack.1").LatestRuleTime,
                Is.EqualTo(10d));
        }

        [Test]
        public void AcknowledgeRemovesOnlyUnreadEmphasis()
        {
            var runtime = new OutpostAlertRuntime();
            Assert.That(Report(runtime, "attack.a", "outpost.a",
                OutpostAlertSeverity.Guard, 20, 1d, out _), Is.True);
            Assert.That(Report(runtime, "attack.b", "outpost.b",
                OutpostAlertSeverity.Critical, 90, 2d, out _), Is.True);

            Assert.That(runtime.TryAcknowledge("attack.b"), Is.True);
            Assert.That(runtime.ActiveAlerts, Has.Count.EqualTo(2));
            Assert.That(runtime.UnacknowledgedAlerts.Select(
                value => value.StableAlertId),
                Is.EqualTo(new[] { "attack.a" }));
            Assert.That(runtime.TryAcknowledge("attack.b"), Is.True);
            Assert.That(runtime.ActiveAlerts, Has.Count.EqualTo(2));
        }

        [Test]
        public void ResolveMovesFactOutOfActiveButKeepsPersistentSnapshot()
        {
            var runtime = new OutpostAlertRuntime();
            Assert.That(Report(runtime, "attack.1", "outpost.a",
                OutpostAlertSeverity.UnderAttack, 50, 4d,
                out _), Is.True);

            Assert.That(runtime.TryResolve(
                "attack.1", 9d, out string error), Is.True, error);
            Assert.That(runtime.ActiveAlerts, Is.Empty);
            Assert.That(runtime.UnacknowledgedAlerts, Is.Empty);
            Assert.That(runtime.Get("attack.1").IsResolved, Is.True);
            Assert.That(runtime.Get("attack.1").LatestRuleTime,
                Is.EqualTo(9d));
            Assert.That(runtime.Capture().Alerts, Has.Count.EqualTo(1));

            ulong revision = runtime.Revision;
            Assert.That(Report(runtime, "attack.1", "outpost.a",
                OutpostAlertSeverity.Critical, 95, 10d,
                out error), Is.True, error);
            Assert.That(runtime.Get("attack.1").IsResolved, Is.True,
                "Late duplicate reports must not reopen a resolved fact.");
            Assert.That(runtime.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void ActiveAlertsSortBySeverityRiskThenStableAlertId()
        {
            var runtime = new OutpostAlertRuntime();
            Assert.That(Report(runtime, "attack.z", "outpost.z",
                OutpostAlertSeverity.UnderAttack, 80, 1d, out _), Is.True);
            Assert.That(Report(runtime, "attack.a", "outpost.a",
                OutpostAlertSeverity.Critical, 50, 2d, out _), Is.True);
            Assert.That(Report(runtime, "attack.b", "outpost.b",
                OutpostAlertSeverity.Critical, 70, 3d, out _), Is.True);
            Assert.That(Report(runtime, "attack.c", "outpost.c",
                OutpostAlertSeverity.Guard, 90, 4d, out _), Is.True);

            Assert.That(runtime.ActiveAlerts.Select(
                value => value.StableAlertId),
                Is.EqualTo(new[]
                {
                    "attack.b",
                    "attack.a",
                    "attack.z",
                    "attack.c",
                }));
        }

        [Test]
        public void CaptureRestoreIsAtomicAndRejectsDuplicateStableAlertIds()
        {
            var runtime = new OutpostAlertRuntime();
            Assert.That(Report(runtime, "attack.1", "outpost.a",
                OutpostAlertSeverity.UnderAttack, 45, 3d, out _), Is.True);
            Assert.That(runtime.TryAcknowledge("attack.1"), Is.True);
            OutpostAlertRuntimeSnapshot good = runtime.Capture();

            var duplicate = new OutpostAlertRuntimeSnapshot(
                99UL,
                new[] { good.Alerts[0], good.Alerts[0] });
            OutpostAlertRuntimeSnapshot before = runtime.Capture();
            Assert.That(runtime.TryRestore(
                duplicate, out string error), Is.False);
            Assert.That(error, Does.Contain("duplicate"));
            Assert.That(runtime.Capture(), Is.EqualTo(before));

            var restored = new OutpostAlertRuntime();
            Assert.That(restored.TryRestore(good, out error), Is.True, error);
            Assert.That(restored.Capture(), Is.EqualTo(good));
            Assert.That(restored.Get("attack.1").IsAcknowledged, Is.True);
        }

        [Test]
        public void InvalidTimeOrRiskDoesNotMutateExistingAlert()
        {
            var runtime = new OutpostAlertRuntime();
            Assert.That(Report(runtime, "attack.1", "outpost.a",
                OutpostAlertSeverity.Guard, 10, 2d, out _), Is.True);
            OutpostAlertRuntimeSnapshot before = runtime.Capture();

            Assert.That(runtime.TryReport(
                "attack.2", "outpost.b", 1, 2,
                OutpostAlertSeverity.UnderAttack,
                "未知威胁", 101, 5f, double.NaN,
                out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.Capture(), Is.EqualTo(before));
        }

        private static bool Report(
            OutpostAlertRuntime runtime,
            string stableAlertId,
            string settlementId,
            OutpostAlertSeverity severity,
            int risk,
            double ruleTime,
            out string error)
        {
            return runtime.TryReport(
                stableAlertId,
                settlementId,
                14,
                9,
                severity,
                "啮噬者正在攻击",
                risk,
                30f,
                ruleTime,
                out error);
        }
    }
}
