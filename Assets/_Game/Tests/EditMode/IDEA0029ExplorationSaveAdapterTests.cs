using System;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Leader.Exploration;
using WasteCity.Persistence.ThreeD;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029ExplorationSaveAdapterTests
    {
        [Test]
        public void AdapterAndLosslessDtoContractExist()
        {
            Type adapter = Type.GetType(
                "WasteCity.Graybox3D.Building." +
                "GrayboxExplorationLeaderOutpostSaveAdapter3D, " +
                "WasteCity.Graybox3D.Building",
                false);
            Type intel = Type.GetType(
                "WasteCity.Persistence.ThreeD.FormalThreeDIntelSaveData, " +
                "WasteCity.Game",
                true);
            Type gather = Type.GetType(
                "WasteCity.Persistence.ThreeD." +
                "FormalThreeDManualGatherSaveData, WasteCity.Game",
                true);

            Assert.That(adapter, Is.Not.Null);
            Assert.That(intel.GetField("summary"), Is.Not.Null);
            Assert.That(intel.GetField("sourceRevision"), Is.Not.Null);
            Assert.That(gather.GetField("targetResourceId"), Is.Not.Null);
        }

        [Test]
        public void CapturesAndRestoresAllFiveRuntimeOwnersLosslessly()
        {
            RuntimeSet source = CreateRuntimeSet();
            SeedNonTrivialState(source);
            var destination = CreatePayload();

            Assert.That(source.Adapter.TryCapture(
                destination, out string error), Is.True, error);

            FormalThreeDExplorationSaveData saved = destination.exploration;
            Assert.That(saved.intel, Has.Length.EqualTo(1));
            Assert.That(saved.intel[0].summary, Is.EqualTo("铁矿 240"));
            Assert.That(saved.intel[0].sourceRevision, Is.EqualTo(9ul));
            Assert.That(saved.intel[0].remainingFreshSeconds,
                Is.EqualTo(50f).Within(.001f));
            Assert.That(saved.intel[0].remainingExpirySeconds,
                Is.EqualTo(170f).Within(.001f));
            Assert.That(saved.leader.manualGather.targetResourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(saved.cenJinDistress.state,
                Is.EqualTo((int)CenJinDistressState.Discovered));
            Assert.That(saved.outpostAlerts, Has.Length.EqualTo(1));
            Assert.That(saved.outpostAlerts[0].acknowledged, Is.True);

            RuntimeSet restored = CreateRuntimeSet();
            Assert.That(restored.Adapter.TryApply(
                destination, out error), Is.True, error);

            Assert.That(restored.Exploration.SourceCount, Is.Zero);
            Assert.That(restored.Exploration.GetState(16, 15),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(restored.Exploration.TryGetIntel(
                ResourceNodeId, 20f, out WorldIntelSnapshot intel), Is.True);
            Assert.That(intel.Summary, Is.EqualTo("铁矿 240"));
            Assert.That(intel.SourceRevision, Is.EqualTo(9ul));
            Assert.That(intel.ObservedRuleTimeSeconds,
                Is.EqualTo(10f).Within(.001f));
            Assert.That(restored.Leader.RequestedMode,
                Is.EqualTo(LeaderControlMode.Manual));
            Assert.That(restored.Gather.IsActive, Is.True);
            Assert.That(restored.Gather.TargetResourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(restored.Gather.RemainingSeconds,
                Is.EqualTo(4f).Within(.001f));
            Assert.That(restored.Distress.State,
                Is.EqualTo(CenJinDistressState.Discovered));
            Assert.That(restored.Distress.ElapsedSinceDiscoverySeconds,
                Is.EqualTo(30f).Within(.001f));
            OutpostAlertEntry alert = restored.Alerts.Get("attack.000001");
            Assert.That(alert, Is.Not.Null);
            Assert.That(alert.FirstRuleTime, Is.EqualTo(12d));
            Assert.That(alert.LatestRuleTime, Is.EqualTo(15d));
            Assert.That(alert.IsAcknowledged, Is.True);
            Assert.That(restored.Alerts.Capture().Revision,
                Is.EqualTo(saved.outpostAlerts[0].revision));

            var recaptured = CreatePayload();
            Assert.That(restored.Adapter.TryCapture(
                recaptured, out error), Is.True, error);
            Assert.That(recaptured.exploration.leader.revision,
                Is.EqualTo(saved.leader.revision));
            Assert.That(recaptured.exploration.leader.manualGather.revision,
                Is.EqualTo(saved.leader.manualGather.revision));
            Assert.That(recaptured.exploration.leader.manualGather
                .cycleOrdinal,
                Is.EqualTo(saved.leader.manualGather.cycleOrdinal));
            Assert.That(recaptured.exploration.cenJinDistress.revision,
                Is.EqualTo(saved.cenJinDistress.revision));
        }

        [Test]
        public void ExpiredIntelStripsMutableFieldsButKeepsIdentityAndRevision()
        {
            RuntimeSet set = CreateRuntimeSet(currentRuleTime: 200d);
            set.Exploration.UpsertSource(new WorldVisionSource(
                "core.city.000001", WorldVisionSourceKind.PrimaryCity,
                16, 15, true));
            Assert.That(set.Exploration.TryObserveVisibleResource(
                new WorldIntelObservation(
                    ResourceNodeId,
                    WorldIntelKind.Resource,
                    16,
                    15,
                    "铁矿 18",
                    true,
                    18,
                    0f,
                    42ul),
                out _, out string error), Is.True, error);
            var payload = CreatePayload();

            Assert.That(set.Adapter.TryCapture(payload, out error),
                Is.True,
                error);

            FormalThreeDIntelSaveData saved = payload.exploration.intel[0];
            Assert.That(saved.summary, Is.Empty);
            Assert.That(saved.hasMutableValue, Is.False);
            Assert.That(saved.mutableValue, Is.Zero);
            Assert.That(saved.remainingFreshSeconds, Is.Zero);
            Assert.That(saved.remainingExpirySeconds, Is.Zero);
            Assert.That(saved.sourceRevision, Is.EqualTo(42ul));

            RuntimeSet restored = CreateRuntimeSet(currentRuleTime: 200d);
            Assert.That(restored.Adapter.TryApply(payload, out error),
                Is.True,
                error);
            Assert.That(restored.Exploration.TryGetIntel(
                ResourceNodeId, 200f, out WorldIntelSnapshot intel), Is.True);
            Assert.That(intel.State, Is.EqualTo(WorldIntelState.Expired));
            Assert.That(intel.SourceRevision, Is.EqualTo(42ul));
        }

        [Test]
        public void InvalidLateDomainDataIsRejectedBeforeAnyOwnerMutates()
        {
            RuntimeSet source = CreateRuntimeSet();
            SeedNonTrivialState(source);
            var payload = CreatePayload();
            Assert.That(source.Adapter.TryCapture(
                payload, out string error), Is.True, error);
            payload.exploration.leader.manualGather.targetResourceId =
                "unknown.resource";

            RuntimeSet target = CreateRuntimeSet();
            ulong explorationRevision = target.Exploration.Revision;
            LeaderControlMode leaderMode = target.Leader.RequestedMode;
            ManualGatherSnapshot gather = target.Gather.Capture();
            CenJinDistressSnapshot distress = target.Distress.Capture();
            OutpostAlertRuntimeSnapshot alerts = target.Alerts.Capture();

            Assert.That(target.Adapter.TryApply(payload, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(target.Exploration.Revision,
                Is.EqualTo(explorationRevision));
            Assert.That(target.Leader.RequestedMode, Is.EqualTo(leaderMode));
            Assert.That(target.Gather.Capture().IsActive,
                Is.EqualTo(gather.IsActive));
            Assert.That(target.Distress.Capture().State,
                Is.EqualTo(distress.State));
            Assert.That(target.Alerts.Capture(), Is.EqualTo(alerts));
        }

        [Test]
        public void FutureObservationFailsCaptureWithoutReplacingDestination()
        {
            RuntimeSet set = CreateRuntimeSet(currentRuleTime: 20d);
            set.Exploration.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader,
                16, 15, true));
            Assert.That(set.Exploration.TryObserveVisibleIntel(
                new WorldIntelObservation(
                    "building.future",
                    WorldIntelKind.Building,
                    16,
                    15,
                    "未来情报",
                    false,
                    0,
                    30f), out string error), Is.True, error);
            var payload = CreatePayload();
            FormalThreeDExplorationSaveData sentinel = payload.exploration;

            Assert.That(set.Adapter.TryCapture(payload, out error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(payload.exploration, Is.SameAs(sentinel));
        }

        private const string SessionId = "formal.session.default";
        private const string WorldSignature =
            "core.world.formal-3d.v2.64x48";
        private const string ResourceNodeId =
            "world.deposit.safe-iron.01";

        private static RuntimeSet CreateRuntimeSet(
            double currentRuleTime = 20d)
        {
            var exploration = new WorldExplorationRuntime(
                64, 48, SessionId, (_, __) => true);
            var leader = new LeaderControlRuntime();
            var gather = new ManualGatherRuntime();
            var distress = new CenJinDistressRuntime(SessionId);
            var alerts = new OutpostAlertRuntime();
            var adapter = new
                GrayboxExplorationLeaderOutpostSaveAdapter3D(
                    exploration,
                    leader,
                    gather,
                    distress,
                    alerts,
                    SessionId,
                    () => currentRuleTime);
            return new RuntimeSet(
                exploration, leader, gather, distress, alerts, adapter);
        }

        private static void SeedNonTrivialState(RuntimeSet set)
        {
            set.Exploration.UpsertSource(new WorldVisionSource(
                "core.city.000001", WorldVisionSourceKind.PrimaryCity,
                16, 15, true));
            Assert.That(set.Exploration.TryObserveVisibleResource(
                new WorldIntelObservation(
                    ResourceNodeId,
                    WorldIntelKind.Resource,
                    16,
                    15,
                    "铁矿 240",
                    true,
                    240,
                    10f,
                    9ul),
                out _, out string scanError), Is.True, scanError);
            Assert.That(set.Leader.TryRequest(
                LeaderControlMode.Manual, out string leaderError),
                Is.True,
                leaderError);
            var context = new ManualGatherContext(
                true,
                CharacterLifeState.Active,
                LeaderControlMode.Manual,
                false,
                true,
                ResourceNodeId,
                ResourceIds.Iron,
                240,
                1f);
            Assert.That(set.Gather.TryStart(context, out string gatherError),
                Is.True,
                gatherError);
            set.Gather.Tick(
                2f,
                false,
                context,
                _ => default);
            Assert.That(set.Distress.TryDiscover(true), Is.True);
            set.Distress.Tick(30f, false, 0f, true, null);
            Assert.That(set.Alerts.TryReport(
                "attack.000001",
                "core.outpost.000001",
                30,
                20,
                OutpostAlertSeverity.UnderAttack,
                "掠夺者正在攻击",
                60,
                35f,
                12d,
                out string alertError), Is.True, alertError);
            Assert.That(set.Alerts.TryReport(
                "attack.000001",
                "core.outpost.000001",
                30,
                20,
                OutpostAlertSeverity.UnderAttack,
                "掠夺者正在攻击",
                60,
                35f,
                15d,
                out alertError), Is.True, alertError);
            Assert.That(set.Alerts.TryAcknowledge("attack.000001"), Is.True);
        }

        private static FormalThreeDSaveData CreatePayload()
        {
            return new FormalThreeDSaveData
            {
                sessionId = SessionId,
                world = new FormalThreeDWorldSaveData
                {
                    configurationSignature = WorldSignature,
                    width = 64,
                    height = 48,
                    resourceNodes = new[]
                    {
                        new FormalThreeDResourceNodeSaveData
                        {
                            stableNodeId = ResourceNodeId,
                            resourceId = ResourceIds.Iron,
                            x = 16,
                            y = 15,
                            remainingAmount = 240,
                        },
                    },
                },
            };
        }

        private sealed class RuntimeSet
        {
            public RuntimeSet(
                WorldExplorationRuntime exploration,
                LeaderControlRuntime leader,
                ManualGatherRuntime gather,
                CenJinDistressRuntime distress,
                OutpostAlertRuntime alerts,
                GrayboxExplorationLeaderOutpostSaveAdapter3D adapter)
            {
                Exploration = exploration;
                Leader = leader;
                Gather = gather;
                Distress = distress;
                Alerts = alerts;
                Adapter = adapter;
            }

            public WorldExplorationRuntime Exploration { get; }
            public LeaderControlRuntime Leader { get; }
            public ManualGatherRuntime Gather { get; }
            public CenJinDistressRuntime Distress { get; }
            public OutpostAlertRuntime Alerts { get; }
            public GrayboxExplorationLeaderOutpostSaveAdapter3D Adapter
            {
                get;
            }
        }
    }
}
