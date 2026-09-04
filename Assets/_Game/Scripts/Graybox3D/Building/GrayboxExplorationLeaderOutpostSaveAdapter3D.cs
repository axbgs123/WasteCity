using System;
using WasteCity.Leader.Exploration;
using WasteCity.Persistence.ThreeD;
using WasteCity.World.Exploration;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxExplorationLeaderOutpostSaveAdapter3D :
        IFormalThreeDExplorationSaveDomain
    {
        private const float TimeEpsilon = .001f;

        private readonly WorldExplorationRuntime exploration;
        private readonly LeaderControlRuntime leaderControl;
        private readonly ManualGatherRuntime manualGather;
        private readonly CenJinDistressRuntime distress;
        private readonly OutpostAlertRuntime outpostAlerts;
        private readonly string sessionId;
        private readonly Func<double> ruleTimeSecondsProvider;
        private ulong savedExplorationRevision;
        private ulong explorationRuntimeRevisionAnchor;
        private ulong savedLeaderRevision;
        private ulong leaderRuntimeRevisionAnchor;
        private ulong savedGatherRevision;
        private ulong savedGatherCycleOrdinal;
        private ulong gatherRuntimeRevisionAnchor;
        private ulong savedDistressRevision;
        private ulong distressRuntimeRevisionAnchor;

        public GrayboxExplorationLeaderOutpostSaveAdapter3D(
            WorldExplorationRuntime exploration,
            LeaderControlRuntime leaderControl,
            ManualGatherRuntime manualGather,
            CenJinDistressRuntime distress,
            OutpostAlertRuntime outpostAlerts,
            string sessionId,
            Func<double> ruleTimeSecondsProvider)
        {
            this.exploration = exploration ??
                throw new ArgumentNullException(nameof(exploration));
            this.leaderControl = leaderControl ??
                throw new ArgumentNullException(nameof(leaderControl));
            this.manualGather = manualGather ??
                throw new ArgumentNullException(nameof(manualGather));
            this.distress = distress ??
                throw new ArgumentNullException(nameof(distress));
            this.outpostAlerts = outpostAlerts ??
                throw new ArgumentNullException(nameof(outpostAlerts));
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException(
                    "探索存档适配器需要稳定会话 ID",
                    nameof(sessionId));
            if (!string.Equals(
                    distress.SessionId,
                    sessionId,
                    StringComparison.Ordinal))
                throw new ArgumentException(
                    "求救运行时与探索存档会话不一致",
                    nameof(distress));
            this.sessionId = sessionId;
            this.ruleTimeSecondsProvider = ruleTimeSecondsProvider ??
                throw new ArgumentNullException(
                    nameof(ruleTimeSecondsProvider));
            savedExplorationRevision = exploration.Revision;
            explorationRuntimeRevisionAnchor = exploration.Revision;
            savedLeaderRevision = leaderControl.Revision;
            leaderRuntimeRevisionAnchor = leaderControl.Revision;
            savedGatherRevision = manualGather.Revision;
            savedGatherCycleOrdinal = manualGather.Revision;
            gatherRuntimeRevisionAnchor = manualGather.Revision;
            savedDistressRevision = distress.Revision;
            distressRuntimeRevisionAnchor = distress.Revision;
        }

        public GrayboxFormalSaveDomainId3D DomainId =>
            GrayboxFormalSaveDomainId3D.Exploration;

        public bool TryCapture(
            FormalThreeDSaveData destination,
            out string error)
        {
            if (destination?.world == null)
            {
                error = "探索存档捕获需要正式世界数据";
                return false;
            }
            if (!SessionMatches(destination.sessionId))
            {
                error = "探索存档捕获会话不一致";
                return false;
            }
            if (!TryGetRuleTime(out double ruleTime, out error) ||
                !TryBuildSaveData(
                    destination.world.configurationSignature,
                    ruleTime,
                    out FormalThreeDExplorationSaveData captured,
                    out error))
                return false;

            destination.exploration = captured;
            error = string.Empty;
            return true;
        }

        public bool TryApply(
            FormalThreeDSaveData source,
            out string error)
        {
            if (source?.world == null || source.exploration == null)
            {
                error = "正式探索存档不能为空";
                return false;
            }
            if (!SessionMatches(source.sessionId))
            {
                error = "探索存档恢复会话不一致";
                return false;
            }
            if (!TryGetRuleTime(out double ruleTime, out error) ||
                !TryPrepare(
                    source.exploration,
                    source.world.configurationSignature,
                    ruleTime,
                    out PreparedRestore prepared,
                    out error))
                return false;

            WorldExplorationSnapshot explorationRollback =
                exploration.Capture();
            LeaderControlMode leaderRollback = leaderControl.RequestedMode;
            ManualGatherSnapshot gatherRollback = manualGather.Capture();
            CenJinDistressSnapshot distressRollback = distress.Capture();
            OutpostAlertRuntimeSnapshot alertRollback =
                outpostAlerts.Capture();

            if (TryApplyPrepared(prepared, out error))
            {
                AnchorRestoredRevisions(source.exploration);
                return true;
            }

            string applyError = error;
            if (!exploration.TryRestore(
                    explorationRollback, out string rollbackError) ||
                !leaderControl.TryRestore(
                    leaderRollback, out rollbackError) ||
                !manualGather.TryRestore(
                    gatherRollback, out rollbackError) ||
                !distress.TryRestore(
                    distressRollback, out rollbackError) ||
                !outpostAlerts.TryRestore(
                    alertRollback, out rollbackError))
            {
                error = applyError + "；探索域内回滚失败：" +
                    rollbackError;
                return false;
            }
            error = applyError;
            return false;
        }

        private bool TryBuildSaveData(
            string worldConfigurationSignature,
            double currentRuleTime,
            out FormalThreeDExplorationSaveData data,
            out string error)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(worldConfigurationSignature))
            {
                error = "探索存档需要世界配置签名";
                return false;
            }

            WorldExplorationSnapshot explorationSnapshot =
                exploration.Capture();
            WorldExplorationScanRecord[] scans =
                explorationSnapshot.ScanRecords;
            var savedScans = new FormalThreeDScanZoneSaveData[scans.Length];
            for (var index = 0; index < scans.Length; index++)
            {
                savedScans[index] = new FormalThreeDScanZoneSaveData
                {
                    zoneId = scans[index].ZoneId,
                    committedEventKey = scans[index].StableEventKey,
                };
            }

            WorldIntelObservation[] intel = explorationSnapshot.Intel;
            var savedIntel = new FormalThreeDIntelSaveData[intel.Length];
            for (var index = 0; index < intel.Length; index++)
            {
                WorldIntelObservation item = intel[index];
                string persistenceStableId =
                    ToPersistenceStableId(item.Kind, item.StableId,
                        item.X, item.Y);
                double age = currentRuleTime -
                    item.ObservedRuleTimeSeconds;
                if (!IsFinite(age) || age < -TimeEpsilon)
                {
                    error = "探索情报观察时间不能晚于规则时间";
                    return false;
                }
                age = Math.Max(0d, age);
                float remainingFresh = (float)Math.Max(
                    0d,
                    FormalExplorationCatalog3D.IntelStaleSeconds - age);
                float remainingExpiry = (float)Math.Max(
                    0d,
                    FormalExplorationCatalog3D.IntelExpiredSeconds - age);
                bool expired = remainingExpiry <= 0f;
                bool hasMutable = !expired && item.HasMutableValue;
                int mutableValue = hasMutable ? item.MutableValue : 0;
                savedIntel[index] = new FormalThreeDIntelSaveData
                {
                    stableIntelId = persistenceStableId,
                    ownerKind = (int)item.Kind,
                    ownerStableId = persistenceStableId,
                    summary = expired ? string.Empty : item.Summary,
                    x = item.X,
                    y = item.Y,
                    remainingFreshSeconds = remainingFresh,
                    remainingExpirySeconds = remainingExpiry,
                    hasMutableValue = hasMutable,
                    mutableValue = mutableValue,
                    depleted = hasMutable && mutableValue <= 0,
                    sourceRevision = item.SourceRevision,
                };
            }

            ManualGatherSnapshot gather = manualGather.Capture();
            CenJinDistressSnapshot distressSnapshot = distress.Capture();
            OutpostAlertRuntimeSnapshot alerts = outpostAlerts.Capture();
            var savedAlerts = new FormalThreeDOutpostAlertSaveData[
                alerts.Alerts.Count];
            for (var index = 0; index < savedAlerts.Length; index++)
            {
                OutpostAlertEntry item = alerts.Alerts[index];
                savedAlerts[index] = new FormalThreeDOutpostAlertSaveData
                {
                    stableAlertId = item.StableAlertId,
                    settlementId = item.SettlementId,
                    attackFactId = item.StableAlertId,
                    severity = (int)item.Severity,
                    x = item.X,
                    y = item.Y,
                    threatSummary = item.ThreatSummary,
                    estimatedLossRiskPercent =
                        item.EstimatedLossRiskPercent,
                    estimatedSecondsToLoss = item.EstimatedSecondsToLoss,
                    firstRuleTimeSeconds = item.FirstRuleTime,
                    latestRuleTimeSeconds = item.LatestRuleTime,
                    acknowledged = item.IsAcknowledged,
                    resolved = item.IsResolved,
                    revision = alerts.Revision,
                };
            }

            data = new FormalThreeDExplorationSaveData
            {
                configurationSignature = FormalThreeDExplorationSaveData
                    .ConfigurationSignature,
                configurationVersion = FormalThreeDExplorationSaveData
                    .ConfigurationVersion,
                worldConfigurationSignature = worldConfigurationSignature,
                width = explorationSnapshot.Width,
                height = explorationSnapshot.Height,
                exploredCells = explorationSnapshot.ExploredCells,
                scanZones = savedScans,
                intel = savedIntel,
                leader = new FormalThreeDLeaderInteractionSaveData
                {
                    requestedControlMode =
                        (int)leaderControl.RequestedMode,
                    revision = ProjectRevision(
                        savedLeaderRevision,
                        leaderRuntimeRevisionAnchor,
                        leaderControl.Revision),
                    manualGather = new FormalThreeDManualGatherSaveData
                    {
                        active = gather.IsActive,
                        targetNodeId = ToPersistenceResourceNodeId(
                            gather.TargetStableId),
                        targetResourceId = gather.TargetResourceId,
                        remainingCycleSeconds = gather.RemainingSeconds,
                        cycleOrdinal = ProjectRevision(
                            savedGatherCycleOrdinal,
                            gatherRuntimeRevisionAnchor,
                            manualGather.Revision),
                        revision = ProjectRevision(
                            savedGatherRevision,
                            gatherRuntimeRevisionAnchor,
                            manualGather.Revision),
                    },
                },
                cenJinDistress = new FormalThreeDCenJinDistressSaveData
                {
                    siteId = distressSnapshot.SiteId,
                    state = (int)distressSnapshot.State,
                    elapsedSinceDiscoverySeconds =
                        distressSnapshot.ElapsedSinceDiscoverySeconds,
                    rescueRemainingSeconds =
                        distressSnapshot.RescueRemainingSeconds,
                    reservedBiomass = distressSnapshot.ReservedBiomass,
                    committedRewardKey =
                        distressSnapshot.CommittedEventKey ?? string.Empty,
                    revision = ProjectRevision(
                        savedDistressRevision,
                        distressRuntimeRevisionAnchor,
                        distress.Revision),
                },
                outpostAlerts = savedAlerts,
                revision = ProjectRevision(
                    savedExplorationRevision,
                    explorationRuntimeRevisionAnchor,
                    explorationSnapshot.Revision),
            };
            error = string.Empty;
            return true;
        }

        private bool TryPrepare(
            FormalThreeDExplorationSaveData data,
            string worldConfigurationSignature,
            double currentRuleTime,
            out PreparedRestore prepared,
            out string error)
        {
            prepared = default;
            if (!string.Equals(
                    data.configurationSignature,
                    FormalThreeDExplorationSaveData.ConfigurationSignature,
                    StringComparison.Ordinal) ||
                data.configurationVersion !=
                    FormalThreeDExplorationSaveData.ConfigurationVersion ||
                !string.Equals(
                    data.worldConfigurationSignature,
                    worldConfigurationSignature,
                    StringComparison.Ordinal) ||
                data.width != exploration.Width ||
                data.height != exploration.Height ||
                data.exploredCells == null ||
                data.scanZones == null ||
                data.intel == null ||
                data.leader?.manualGather == null ||
                data.cenJinDistress == null ||
                data.outpostAlerts == null)
            {
                error = "探索存档结构或世界身份无效";
                return false;
            }

            var scanRecords = new WorldExplorationScanRecord[
                data.scanZones.Length];
            for (var index = 0; index < scanRecords.Length; index++)
            {
                FormalThreeDScanZoneSaveData item = data.scanZones[index];
                if (item == null)
                {
                    error = "探索扫描记录不能为空";
                    return false;
                }
                scanRecords[index] = new WorldExplorationScanRecord(
                    item.zoneId,
                    item.committedEventKey);
            }

            var intel = new WorldIntelObservation[data.intel.Length];
            for (var index = 0; index < intel.Length; index++)
            {
                FormalThreeDIntelSaveData item = data.intel[index];
                if (item == null)
                {
                    error = "探索情报记录不能为空";
                    return false;
                }
                if (!string.Equals(
                        item.stableIntelId,
                        item.ownerStableId,
                        StringComparison.Ordinal) ||
                    item.summary == null ||
                    item.ownerKind < 0 || item.ownerKind > 4)
                {
                    error = "探索情报身份、类型或摘要无效";
                    return false;
                }
                if (!TryRebuildObservationTime(
                    item,
                    currentRuleTime,
                    out float observedRuleTime,
                    out error))
                    return false;
                try
                {
                    string runtimeStableId = ToRuntimeStableId(
                        (WorldIntelKind)item.ownerKind,
                        item.stableIntelId,
                        item.x,
                        item.y);
                    intel[index] = new WorldIntelObservation(
                        runtimeStableId,
                        (WorldIntelKind)item.ownerKind,
                        item.x,
                        item.y,
                        item.summary,
                        item.hasMutableValue,
                        item.mutableValue,
                        observedRuleTime,
                        item.sourceRevision);
                }
                catch (Exception exception)
                {
                    error = "探索情报存档无效：" + exception.Message;
                    return false;
                }
            }

            var explorationSnapshot = new WorldExplorationSnapshot(
                data.width,
                data.height,
                data.exploredCells,
                scanRecords,
                intel,
                data.revision);
            var explorationCandidate = new WorldExplorationRuntime(
                data.width,
                data.height,
                sessionId,
                (_, __) => false);
            if (!explorationCandidate.TryRestore(
                    explorationSnapshot, out error))
                return false;

            LeaderControlMode mode =
                (LeaderControlMode)data.leader.requestedControlMode;
            var leaderCandidate = new LeaderControlRuntime();
            if (!leaderCandidate.TryRestore(mode, out error)) return false;

            FormalThreeDManualGatherSaveData gather =
                data.leader.manualGather;
            var gatherSnapshot = new ManualGatherSnapshot(
                gather.active,
                ToRuntimeResourceNodeId(gather.targetNodeId),
                gather.targetResourceId,
                gather.remainingCycleSeconds);
            var gatherCandidate = new ManualGatherRuntime();
            if (!gatherCandidate.TryRestore(gatherSnapshot, out error))
                return false;

            var distressSnapshot = new CenJinDistressSnapshot(
                sessionId,
                data.cenJinDistress.siteId,
                (CenJinDistressState)data.cenJinDistress.state,
                data.cenJinDistress.elapsedSinceDiscoverySeconds,
                data.cenJinDistress.rescueRemainingSeconds,
                data.cenJinDistress.reservedBiomass,
                data.cenJinDistress.committedRewardKey);
            var distressCandidate = new CenJinDistressRuntime(sessionId);
            if (!distressCandidate.TryRestore(
                    distressSnapshot, out error))
                return false;

            if (!TryBuildAlertSnapshot(
                    data.outpostAlerts,
                    out OutpostAlertRuntimeSnapshot alertSnapshot,
                    out error))
                return false;
            var alertCandidate = new OutpostAlertRuntime();
            if (!alertCandidate.TryRestore(alertSnapshot, out error))
                return false;

            prepared = new PreparedRestore(
                explorationSnapshot,
                mode,
                gatherSnapshot,
                distressSnapshot,
                alertSnapshot);
            error = string.Empty;
            return true;
        }

        private static string ToPersistenceStableId(
            WorldIntelKind kind,
            string stableId,
            int x,
            int y)
        {
            return kind == WorldIntelKind.Resource
                ? GrayboxResourceNodeIdentity3D.Create(x, y)
                : stableId;
        }

        private static string ToRuntimeStableId(
            WorldIntelKind kind,
            string stableId,
            int x,
            int y)
        {
            if (kind != WorldIntelKind.Resource) return stableId;
            FormalResourceNodeSpec3D? node =
                FormalWorldGenerationCatalog3D.FindResourceNode(x, y);
            return node.HasValue ? node.Value.StableId : stableId;
        }

        private static string ToPersistenceResourceNodeId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return string.Empty;
            for (var index = 0;
                 index < FormalWorldGenerationCatalog3D.ResourceNodes.Count;
                 index++)
            {
                FormalResourceNodeSpec3D node =
                    FormalWorldGenerationCatalog3D.ResourceNodes[index];
                if (string.Equals(
                        node.StableId,
                        stableId,
                        StringComparison.Ordinal))
                    return GrayboxResourceNodeIdentity3D.Create(
                        node.X,
                        node.Y);
            }
            return stableId;
        }

        private static string ToRuntimeResourceNodeId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return string.Empty;
            for (var index = 0;
                 index < FormalWorldGenerationCatalog3D.ResourceNodes.Count;
                 index++)
            {
                FormalResourceNodeSpec3D node =
                    FormalWorldGenerationCatalog3D.ResourceNodes[index];
                if (string.Equals(
                        GrayboxResourceNodeIdentity3D.Create(node.X, node.Y),
                        stableId,
                        StringComparison.Ordinal))
                    return node.StableId;
            }
            return stableId;
        }

        private bool TryApplyPrepared(
            PreparedRestore prepared,
            out string error)
        {
            if (!exploration.TryRestore(
                    prepared.Exploration, out error) ||
                !leaderControl.TryRestore(prepared.LeaderMode, out error) ||
                !manualGather.TryRestore(
                    prepared.ManualGather, out error) ||
                !distress.TryRestore(prepared.Distress, out error) ||
                !outpostAlerts.TryRestore(
                    prepared.OutpostAlerts, out error))
                return false;
            error = string.Empty;
            return true;
        }

        private static bool TryBuildAlertSnapshot(
            FormalThreeDOutpostAlertSaveData[] values,
            out OutpostAlertRuntimeSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            var candidate = new OutpostAlertRuntime();
            ulong revision = values.Length == 0 ? 0ul : values[0].revision;
            for (var index = 0; index < values.Length; index++)
            {
                FormalThreeDOutpostAlertSaveData item = values[index];
                if (item == null || item.revision != revision ||
                    !string.Equals(
                        item.stableAlertId,
                        item.attackFactId,
                        StringComparison.Ordinal))
                {
                    error = "前哨警报身份或 revision 无效";
                    return false;
                }
                if (!candidate.TryReport(
                        item.stableAlertId,
                        item.settlementId,
                        item.x,
                        item.y,
                        (OutpostAlertSeverity)item.severity,
                        item.threatSummary,
                        item.estimatedLossRiskPercent,
                        item.estimatedSecondsToLoss,
                        item.firstRuleTimeSeconds,
                        out error))
                    return false;
                if (item.latestRuleTimeSeconds >
                        item.firstRuleTimeSeconds &&
                    !candidate.TryReport(
                        item.stableAlertId,
                        item.settlementId,
                        item.x,
                        item.y,
                        (OutpostAlertSeverity)item.severity,
                        item.threatSummary,
                        item.estimatedLossRiskPercent,
                        item.estimatedSecondsToLoss,
                        item.latestRuleTimeSeconds,
                        out error))
                    return false;
                if (item.acknowledged &&
                    !candidate.TryAcknowledge(item.stableAlertId))
                {
                    error = "前哨警报确认状态无效";
                    return false;
                }
                if (item.resolved && !candidate.TryResolve(
                        item.stableAlertId,
                        item.latestRuleTimeSeconds,
                        out error))
                    return false;
            }
            snapshot = new OutpostAlertRuntimeSnapshot(
                revision,
                candidate.Capture().Alerts);
            error = string.Empty;
            return true;
        }

        private static bool TryRebuildObservationTime(
            FormalThreeDIntelSaveData item,
            double currentRuleTime,
            out float observedRuleTime,
            out string error)
        {
            observedRuleTime = 0f;
            if (!IsFinite(item.remainingFreshSeconds) ||
                !IsFinite(item.remainingExpirySeconds) ||
                item.remainingFreshSeconds < 0f ||
                item.remainingFreshSeconds >
                    FormalExplorationCatalog3D.IntelStaleSeconds ||
                item.remainingExpirySeconds < 0f ||
                item.remainingExpirySeconds >
                    FormalExplorationCatalog3D.IntelExpiredSeconds)
            {
                error = "探索情报剩余时间无效";
                return false;
            }
            double age = FormalExplorationCatalog3D.IntelExpiredSeconds -
                item.remainingExpirySeconds;
            float expectedFresh = (float)Math.Max(
                0d,
                FormalExplorationCatalog3D.IntelStaleSeconds - age);
            if (Math.Abs(expectedFresh - item.remainingFreshSeconds) >
                    TimeEpsilon ||
                item.remainingExpirySeconds <= 0f &&
                    (!string.IsNullOrEmpty(item.summary) ||
                     item.hasMutableValue || item.mutableValue != 0 ||
                     item.depleted) ||
                !item.hasMutableValue &&
                    (item.mutableValue != 0 || item.depleted) ||
                item.depleted && item.mutableValue > 0)
            {
                error = "探索情报时效或易变字段不一致";
                return false;
            }
            double observed = currentRuleTime - age;
            if (!IsFinite(observed) || observed < -TimeEpsilon ||
                observed > float.MaxValue)
            {
                error = "探索情报规则时间无效";
                return false;
            }
            observedRuleTime = (float)Math.Max(0d, observed);
            error = string.Empty;
            return true;
        }

        private bool SessionMatches(string value)
        {
            return string.Equals(
                value,
                sessionId,
                StringComparison.Ordinal);
        }

        private void AnchorRestoredRevisions(
            FormalThreeDExplorationSaveData data)
        {
            savedExplorationRevision = data.revision;
            explorationRuntimeRevisionAnchor = exploration.Revision;
            savedLeaderRevision = data.leader.revision;
            leaderRuntimeRevisionAnchor = leaderControl.Revision;
            savedGatherRevision = data.leader.manualGather.revision;
            savedGatherCycleOrdinal = data.leader.manualGather.cycleOrdinal;
            gatherRuntimeRevisionAnchor = manualGather.Revision;
            savedDistressRevision = data.cenJinDistress.revision;
            distressRuntimeRevisionAnchor = distress.Revision;
        }

        internal void ReanchorDerivedRuntimeState()
        {
            explorationRuntimeRevisionAnchor = exploration.Revision;
        }

        private static ulong ProjectRevision(
            ulong savedRevision,
            ulong runtimeAnchor,
            ulong currentRuntimeRevision)
        {
            if (currentRuntimeRevision < runtimeAnchor)
                return currentRuntimeRevision;
            ulong delta = currentRuntimeRevision - runtimeAnchor;
            return ulong.MaxValue - savedRevision < delta
                ? ulong.MaxValue
                : savedRevision + delta;
        }

        private bool TryGetRuleTime(
            out double ruleTime,
            out string error)
        {
            ruleTime = ruleTimeSecondsProvider();
            if (!IsFinite(ruleTime) || ruleTime < 0d ||
                ruleTime > float.MaxValue)
            {
                error = "探索规则时间必须有限且非负";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private readonly struct PreparedRestore
        {
            public PreparedRestore(
                WorldExplorationSnapshot exploration,
                LeaderControlMode leaderMode,
                ManualGatherSnapshot manualGather,
                CenJinDistressSnapshot distress,
                OutpostAlertRuntimeSnapshot outpostAlerts)
            {
                Exploration = exploration;
                LeaderMode = leaderMode;
                ManualGather = manualGather;
                Distress = distress;
                OutpostAlerts = outpostAlerts;
            }

            public WorldExplorationSnapshot Exploration { get; }
            public LeaderControlMode LeaderMode { get; }
            public ManualGatherSnapshot ManualGather { get; }
            public CenJinDistressSnapshot Distress { get; }
            public OutpostAlertRuntimeSnapshot OutpostAlerts { get; }
        }
    }
}
