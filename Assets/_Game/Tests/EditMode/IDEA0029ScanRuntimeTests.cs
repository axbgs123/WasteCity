using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029ScanRuntimeTests
    {
        [Test]
        public void VisibleFormalNodeScansZoneAndPublishesStableAttentionFact()
        {
            var visibility = new WorldVisibilityRuntime(64, 48);
            visibility.UpsertSource(new WorldVisionSource(
                "core.city.000001",
                WorldVisionSourceKind.PrimaryCity,
                16,
                15,
                true));
            var calls = new List<string>();
            var runtime = new WorldScanRuntime(
                visibility,
                (reasonId, stableEventKey) =>
                {
                    calls.Add(reasonId + "|" + stableEventKey);
                    return true;
                });

            Assert.That(runtime.TryScanVisibleNode(
                    "session-a",
                    "world.deposit.safe-iron.01",
                    16,
                    15,
                    out WorldScanResult result,
                    out string error),
                Is.True,
                error);
            Assert.That(result.ZoneId,
                Is.EqualTo("core.exploration.zone.safe-mining"));
            Assert.That(result.AttentionReasonId,
                Is.EqualTo("core.attention.scan.safe-mining-zone"));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "core.attention.scan.safe-mining-zone|" +
                "exploration.scan:session-a:" +
                "core.exploration.zone.safe-mining"
            }));
            Assert.That(runtime.IsScanned(result.ZoneId), Is.True);
            Assert.That(visibility.IsExplored(23, 15), Is.True);
        }

        [Test]
        public void HiddenUnknownAndDuplicateNodesDoNotPublishAgain()
        {
            var visibility = new WorldVisibilityRuntime(64, 48);
            var callCount = 0;
            var runtime = new WorldScanRuntime(
                visibility,
                (_, __) =>
                {
                    callCount++;
                    return true;
                });

            Assert.That(runtime.TryScanVisibleNode(
                "session-a", "world.deposit.safe-iron.01", 16, 15,
                out _, out _), Is.False);
            visibility.UpsertSource(new WorldVisionSource(
                "core.city.000001", WorldVisionSourceKind.PrimaryCity,
                16, 15, true));
            Assert.That(runtime.TryScanVisibleNode(
                "session-a", "world.deposit.remote-iron.01", 16, 15,
                out _, out _), Is.False);
            Assert.That(runtime.TryScanVisibleNode(
                "session-a", "world.deposit.safe-iron.01", 16, 15,
                out WorldScanResult first, out string error), Is.True, error);
            Assert.That(runtime.TryScanVisibleNode(
                "session-a", "world.deposit.safe-stone.01", 11, 16,
                out WorldScanResult duplicate, out error), Is.False);
            Assert.That(duplicate.Status,
                Is.EqualTo(WorldScanStatus.AlreadyScanned));
            Assert.That(callCount, Is.EqualTo(1));
            Assert.That(first.ZoneId,
                Is.EqualTo("core.exploration.zone.safe-mining"));
        }

        [Test]
        public void RejectedAttentionCommitLeavesScanAndRevealUntouched()
        {
            var visibility = new WorldVisibilityRuntime(64, 48);
            visibility.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader, 32, 38, true));
            var runtime = new WorldScanRuntime(
                visibility,
                (_, __) => false);
            bool wasExplored = visibility.IsExplored(40, 38);

            Assert.That(runtime.TryScanVisibleNode(
                "session-a", "world.deposit.rift-iron.01", 32, 38,
                out WorldScanResult result, out string error), Is.False);

            Assert.That(result.Status,
                Is.EqualTo(WorldScanStatus.AttentionRejected));
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.IsScanned(
                "core.exploration.zone.crystal-rift"), Is.False);
            Assert.That(visibility.IsExplored(40, 38), Is.EqualTo(wasExplored));
        }

        [Test]
        public void RestoreScannedZoneIdsIsValidatedAndDoesNotReplayCallbacks()
        {
            var visibility = new WorldVisibilityRuntime(64, 48);
            var callCount = 0;
            var runtime = new WorldScanRuntime(
                visibility,
                (_, __) =>
                {
                    callCount++;
                    return true;
                });

            Assert.That(runtime.TryRestoreScannedZoneIds(new[]
            {
                "core.exploration.zone.crystal-rift"
            }, out string error), Is.True, error);
            Assert.That(callCount, Is.Zero);
            Assert.That(runtime.CaptureScannedZoneIds(), Is.EqualTo(new[]
            {
                "core.exploration.zone.crystal-rift"
            }));
            Assert.That(runtime.TryRestoreScannedZoneIds(new[]
            {
                "unknown.zone"
            }, out error), Is.False);
            Assert.That(runtime.CaptureScannedZoneIds(), Is.EqualTo(new[]
            {
                "core.exploration.zone.crystal-rift"
            }));
        }
    }
}
