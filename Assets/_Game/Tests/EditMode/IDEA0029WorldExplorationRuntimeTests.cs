using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029WorldExplorationRuntimeTests
    {
        [Test]
        public void SourceObservationScansOnceAndCapturesFreshIntel()
        {
            var attentionCalls = new List<string>();
            var runtime = CreateRuntime(attentionCalls, true);
            var source = new WorldVisionSource(
                "core.city.000001",
                WorldVisionSourceKind.PrimaryCity,
                16,
                15,
                true,
                1ul);
            var observation = new WorldIntelObservation(
                "world.deposit.safe-iron.01",
                WorldIntelKind.Resource,
                16,
                15,
                "铁矿 240",
                true,
                240,
                10f,
                1ul);

            Assert.That(runtime.UpsertSource(source), Is.True);
            Assert.That(runtime.TryObserveVisibleResource(
                observation,
                out WorldScanResult scan,
                out string error), Is.True, error);

            Assert.That(scan.Status, Is.EqualTo(WorldScanStatus.Committed));
            Assert.That(runtime.IsScanned(scan.ZoneId), Is.True);
            Assert.That(attentionCalls, Is.EqualTo(new[]
            {
                "core.attention.scan.safe-mining-zone|" +
                "exploration.scan:session-a:" +
                "core.exploration.zone.safe-mining"
            }));
            Assert.That(runtime.TryGetScanEventKey(
                scan.ZoneId, out string eventKey), Is.True);
            Assert.That(eventKey, Is.EqualTo(scan.StableEventKey));
            Assert.That(runtime.TryGetIntel(
                observation.StableId,
                10f,
                out WorldIntelSnapshot intel), Is.True);
            Assert.That(intel.MutableValue, Is.EqualTo(240));

            ulong revision = runtime.Revision;
            Assert.That(runtime.UpsertSource(source), Is.False);
            Assert.That(runtime.TryObserveVisibleResource(
                observation, out WorldScanResult duplicate, out error),
                Is.True,
                error);
            Assert.That(duplicate.Status,
                Is.EqualTo(WorldScanStatus.AlreadyScanned));
            Assert.That(attentionCalls, Has.Count.EqualTo(1));
            Assert.That(runtime.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void HiddenAndRejectedResourceFactsDoNotPartiallyCommit()
        {
            var attentionCalls = new List<string>();
            var runtime = CreateRuntime(attentionCalls, false);
            var observation = new WorldIntelObservation(
                "world.deposit.rift-iron.01",
                WorldIntelKind.Resource,
                32,
                38,
                "裂谷铁矿 120",
                true,
                120,
                5f);

            Assert.That(runtime.TryObserveVisibleResource(
                observation, out _, out _), Is.False);
            Assert.That(runtime.IntelCount, Is.Zero);
            Assert.That(attentionCalls, Is.Empty);

            runtime.UpsertSource(new WorldVisionSource(
                "leader.1",
                WorldVisionSourceKind.Leader,
                32,
                38,
                true));
            bool farCellBefore = runtime.IsExplored(40, 38);
            ulong revisionBefore = runtime.Revision;

            Assert.That(runtime.TryObserveVisibleResource(
                observation,
                out WorldScanResult rejected,
                out string error), Is.False);
            Assert.That(rejected.Status,
                Is.EqualTo(WorldScanStatus.AttentionRejected));
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.IntelCount, Is.Zero);
            Assert.That(runtime.IsScanned(
                "core.exploration.zone.crystal-rift"), Is.False);
            Assert.That(runtime.IsExplored(40, 38),
                Is.EqualTo(farCellBefore));
            Assert.That(runtime.Revision, Is.EqualTo(revisionBefore));
            Assert.That(attentionCalls, Has.Count.EqualTo(1));
        }

        [Test]
        public void ThrowingAttentionBoundaryRollsBackInternalIntelAndScan()
        {
            var runtime = new WorldExplorationRuntime(
                64,
                48,
                "session-a",
                (_, __) => throw new System.InvalidOperationException(
                    "attention unavailable"));
            runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader,
                32, 38, true));
            ulong revision = runtime.Revision;

            Assert.That(runtime.TryObserveVisibleResource(
                new WorldIntelObservation(
                    "world.deposit.rift-iron.01",
                    WorldIntelKind.Resource,
                    32,
                    38,
                    "裂谷铁矿 120",
                    true,
                    120,
                    2f),
                out WorldScanResult result,
                out string error), Is.False);

            Assert.That(result.Status,
                Is.EqualTo(WorldScanStatus.AttentionRejected));
            Assert.That(error, Does.Contain("attention unavailable"));
            Assert.That(runtime.IntelCount, Is.Zero);
            Assert.That(runtime.ScannedZoneCount, Is.Zero);
            Assert.That(runtime.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void SourceChangesAreLocalAndReentryRefreshesWithoutNewReward()
        {
            var attentionCalls = new List<string>();
            var runtime = CreateRuntime(attentionCalls, true);
            var observation = new WorldIntelObservation(
                "world.deposit.safe-stone.01",
                WorldIntelKind.Resource,
                6,
                6,
                "石料 80",
                true,
                80,
                1f,
                1ul);
            runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader,
                6, 6, true, 1ul));
            Assert.That(runtime.TryObserveVisibleResource(
                observation, out _, out string error), Is.True, error);

            Assert.That(runtime.RemoveSource("leader.1"), Is.True);
            Assert.That(runtime.GetState(6, 6),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader,
                6, 6, true, 2ul)), Is.True);
            Assert.That(runtime.TryObserveVisibleResource(
                new WorldIntelObservation(
                    observation.StableId,
                    WorldIntelKind.Resource,
                    6,
                    6,
                    "石料 79",
                    true,
                    79,
                    20f,
                    2ul),
                out WorldScanResult scan,
                out error), Is.True, error);

            Assert.That(scan.Status,
                Is.EqualTo(WorldScanStatus.AlreadyScanned));
            Assert.That(attentionCalls, Has.Count.EqualTo(1));
            Assert.That(runtime.TryGetIntel(
                observation.StableId,
                20f,
                out WorldIntelSnapshot intel), Is.True);
            Assert.That(intel.MutableValue, Is.EqualTo(79));
        }

        [Test]
        public void NonResourceIntelAlsoRequiresCurrentVision()
        {
            var runtime = CreateRuntime(new List<string>(), true);
            var building = new WorldIntelObservation(
                "building.1", WorldIntelKind.Building,
                8, 8, "冶炼厂运行中", false, 0, 3f);

            Assert.That(runtime.TryObserveVisibleIntel(
                building, out _), Is.False);
            runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader,
                8, 8, true));
            Assert.That(runtime.TryObserveVisibleIntel(
                building, out string error), Is.True, error);
            Assert.That(runtime.TryGetIntel(
                building.StableId, 3f, out WorldIntelSnapshot intel),
                Is.True);
            Assert.That(intel.Kind, Is.EqualTo(WorldIntelKind.Building));
        }

        [Test]
        public void CaptureRestorePersistsHistoryScansAndIntelButNotSources()
        {
            var runtime = CreateRuntime(new List<string>(), true);
            runtime.UpsertSource(new WorldVisionSource(
                "core.city.000001",
                WorldVisionSourceKind.PrimaryCity,
                16,
                15,
                true));
            Assert.That(runtime.TryObserveVisibleResource(
                new WorldIntelObservation(
                    "world.deposit.safe-iron.01",
                    WorldIntelKind.Resource,
                    16,
                    15,
                    "铁矿 240",
                    true,
                    240,
                    10f),
                out _, out string error), Is.True, error);
            WorldExplorationSnapshot snapshot = runtime.Capture();

            bool originalFirstCell = snapshot.ExploredCells[0];
            bool[] exposed = snapshot.ExploredCells;
            exposed[0] = !exposed[0];
            Assert.That(snapshot.ExploredCells[0],
                Is.EqualTo(originalFirstCell),
                "Snapshot arrays must be defensive copies.");

            var restored = CreateRuntime(new List<string>(), true);
            Assert.That(restored.TryRestore(snapshot, out error),
                Is.True,
                error);
            Assert.That(restored.SourceCount, Is.Zero,
                "Live sight sources are derived and must not be restored.");
            Assert.That(restored.GetState(16, 15),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(restored.IsScanned(
                "core.exploration.zone.safe-mining"), Is.True);
            Assert.That(restored.TryGetScanEventKey(
                "core.exploration.zone.safe-mining", out _), Is.True);
            Assert.That(restored.TryGetIntel(
                "world.deposit.safe-iron.01", 10f, out _), Is.True);
            Assert.That(restored.Revision,
                Is.GreaterThanOrEqualTo(snapshot.Revision));
        }

        [Test]
        public void InvalidRestoreDoesNotMutateAggregate()
        {
            var runtime = CreateRuntime(new List<string>(), true);
            runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader,
                4, 4, true));
            WorldExplorationSnapshot before = runtime.Capture();
            ulong revision = runtime.Revision;
            var invalid = new WorldExplorationSnapshot(
                64,
                48,
                before.ExploredCells,
                new[]
                {
                    new WorldExplorationScanRecord(
                        "core.exploration.zone.safe-mining",
                        "exploration.scan:old:" +
                        "core.exploration.zone.safe-mining"),
                    new WorldExplorationScanRecord(
                        "core.exploration.zone.safe-mining",
                        "exploration.scan:duplicate:" +
                        "core.exploration.zone.safe-mining"),
                },
                before.Intel,
                before.Revision);

            Assert.That(runtime.TryRestore(invalid, out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.SourceCount, Is.EqualTo(1));
            Assert.That(runtime.GetState(4, 4),
                Is.EqualTo(WorldVisibilityState.Visible));
            Assert.That(runtime.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void RestoreRejectsScanEventKeyFromAnotherSession()
        {
            var runtime = CreateRuntime(new List<string>(), true);
            var wrongSession = new WorldExplorationSnapshot(
                64,
                48,
                new bool[64 * 48],
                new[]
                {
                    new WorldExplorationScanRecord(
                        "core.exploration.zone.safe-mining",
                        "exploration.scan:other-session:" +
                        "core.exploration.zone.safe-mining"),
                },
                new WorldIntelObservation[0],
                7ul);

            Assert.That(runtime.TryRestore(
                wrongSession, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.Revision, Is.Zero);
            Assert.That(runtime.ScannedZoneCount, Is.Zero);
        }

        [Test]
        public void RestoreRejectsOutOfBoundsIntelWithoutMutation()
        {
            var runtime = CreateRuntime(new List<string>(), true);
            var invalid = new WorldExplorationSnapshot(
                64,
                48,
                new bool[64 * 48],
                new WorldExplorationScanRecord[0],
                new[]
                {
                    new WorldIntelObservation(
                        "building.outside",
                        WorldIntelKind.Building,
                        64,
                        12,
                        "越界建筑",
                        false,
                        0,
                        1f),
                },
                2ul);

            Assert.That(runtime.TryRestore(invalid, out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(runtime.Revision, Is.Zero);
            Assert.That(runtime.IntelCount, Is.Zero);
        }

        private static WorldExplorationRuntime CreateRuntime(
            ICollection<string> attentionCalls,
            bool acceptAttention)
        {
            return new WorldExplorationRuntime(
                64,
                48,
                "session-a",
                (reasonId, eventKey) =>
                {
                    attentionCalls.Add(reasonId + "|" + eventKey);
                    return acceptAttention;
                });
        }
    }
}
