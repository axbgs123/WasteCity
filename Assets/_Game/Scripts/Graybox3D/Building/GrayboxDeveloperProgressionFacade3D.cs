#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;
using WasteCity.World.Exploration;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDeveloperExplorationDiagnostics3D
    {
        private GrayboxDeveloperExplorationDiagnostics3D(
            string[] visionSources,
            string cellVisibility,
            string[] scanZones,
            string[] intel,
            string[] outpostAlerts)
        {
            VisionSources = Array.AsReadOnly(
                visionSources ?? Array.Empty<string>());
            CellVisibility = cellVisibility ?? string.Empty;
            ScanZones = Array.AsReadOnly(scanZones ?? Array.Empty<string>());
            Intel = Array.AsReadOnly(intel ?? Array.Empty<string>());
            OutpostAlerts = Array.AsReadOnly(
                outpostAlerts ?? Array.Empty<string>());
        }

        public IReadOnlyList<string> VisionSources { get; }
        public string CellVisibility { get; }
        public IReadOnlyList<string> ScanZones { get; }
        public IReadOnlyList<string> Intel { get; }
        public IReadOnlyList<string> OutpostAlerts { get; }

        public static GrayboxDeveloperExplorationDiagnostics3D Capture(
            WorldExplorationRuntime exploration,
            OutpostAlertRuntime outpostAlerts,
            float currentRuleTimeSeconds,
            int cellX,
            int cellY)
        {
            if (exploration == null)
            {
                return new GrayboxDeveloperExplorationDiagnostics3D(
                    new[] { "探索运行时未连接" },
                    "探索运行时未连接",
                    new[] { "探索运行时未连接" },
                    new[] { "探索运行时未连接" },
                    BuildAlertLines(outpostAlerts));
            }

            WorldVisionSource[] sources = exploration.CaptureSources();
            var sourceLines = new string[sources.Length];
            for (var index = 0; index < sources.Length; index++)
            {
                WorldVisionSource source = sources[index];
                sourceLines[index] = source.StableId + "｜" +
                    VisionSourceKindText(source.Kind) + "｜格位 (" +
                    source.X + "," + source.Y + ")｜半径 " +
                    source.Radius + "｜" +
                    (source.Active ? "启用" : "停用");
            }

            var scanLines = new string[
                FormalExplorationCatalog3D.ScanZones.Count];
            for (var index = 0; index < scanLines.Length; index++)
            {
                ExplorationScanZoneDefinition zone =
                    FormalExplorationCatalog3D.ScanZones[index];
                scanLines[index] = zone.StableId + "｜" +
                    ScanZoneName(zone.StableId) + "｜" +
                    (exploration.IsScanned(zone.StableId)
                        ? "已扫描"
                        : "未扫描");
            }

            WorldIntelObservation[] observations = exploration.Capture().Intel;
            var intelLines = new string[observations.Length];
            for (var index = 0; index < observations.Length; index++)
            {
                WorldIntelObservation observation = observations[index];
                if (!exploration.TryGetIntel(
                        observation.StableId,
                        currentRuleTimeSeconds,
                        out WorldIntelSnapshot snapshot))
                {
                    intelLines[index] = observation.StableId +
                        "｜情报时刻不可用";
                    continue;
                }
                intelLines[index] = snapshot.StableId + "｜" +
                    IntelKindText(snapshot.Kind) + "｜格位 (" +
                    snapshot.X + "," + snapshot.Y + ")｜" +
                    IntelStateText(snapshot.State) + "｜年龄 " +
                    FormatNumber(snapshot.AgeSeconds) + " 秒" +
                    (string.IsNullOrWhiteSpace(snapshot.Summary)
                        ? string.Empty
                        : "｜" + snapshot.Summary);
            }

            string cell = cellX < 0 || cellY < 0 ||
                cellX >= exploration.Width || cellY >= exploration.Height
                    ? "格位 (" + cellX + "," + cellY + ")：超出地图范围"
                    : "格位 (" + cellX + "," + cellY + ")：" +
                        VisibilityStateText(exploration.GetState(
                            cellX, cellY));
            return new GrayboxDeveloperExplorationDiagnostics3D(
                sourceLines,
                cell,
                scanLines,
                intelLines,
                BuildAlertLines(outpostAlerts));
        }

        private static string[] BuildAlertLines(OutpostAlertRuntime runtime)
        {
            if (runtime == null) return new[] { "前哨警报运行时未连接" };
            IReadOnlyList<OutpostAlertEntry> alerts = runtime.Capture().Alerts;
            var result = new string[alerts.Count];
            for (var index = 0; index < result.Length; index++)
            {
                OutpostAlertEntry alert = alerts[index];
                OutpostAlertDefinition definition =
                    OutpostAlertCatalog.ForSeverity(alert.Severity);
                result[index] = alert.StableAlertId + "｜前哨 " +
                    alert.SettlementId + "｜格位 (" + alert.X + "," +
                    alert.Y + ")｜" +
                    (definition?.ChineseName ?? "无警报") + "｜" +
                    (alert.IsResolved
                        ? "已解除"
                        : alert.IsAcknowledged ? "已确认" : "未确认") +
                    "｜风险 " + alert.EstimatedLossRiskPercent + "%｜" +
                    alert.ThreatSummary;
            }
            return result;
        }

        private static string VisionSourceKindText(WorldVisionSourceKind kind)
        {
            switch (kind)
            {
                case WorldVisionSourceKind.PrimaryCity: return "主城";
                case WorldVisionSourceKind.SecondaryCity: return "副城";
                case WorldVisionSourceKind.Leader: return "领袖";
                case WorldVisionSourceKind.Outpost: return "前哨";
                case WorldVisionSourceKind.ScoutDrone: return "侦察无人机";
                default: return "未知来源";
            }
        }

        private static string VisibilityStateText(WorldVisibilityState state)
        {
            switch (state)
            {
                case WorldVisibilityState.Visible: return "当前可见";
                case WorldVisibilityState.Explored: return "已探索";
                default: return "未探索";
            }
        }

        private static string IntelKindText(WorldIntelKind kind)
        {
            switch (kind)
            {
                case WorldIntelKind.Resource: return "资源";
                case WorldIntelKind.Building: return "建筑";
                case WorldIntelKind.Settlement: return "聚落";
                case WorldIntelKind.Character: return "人物";
                case WorldIntelKind.Enemy: return "敌人";
                default: return "未知情报";
            }
        }

        private static string IntelStateText(WorldIntelState state)
        {
            switch (state)
            {
                case WorldIntelState.Fresh: return "新鲜";
                case WorldIntelState.Stale: return "陈旧";
                case WorldIntelState.Expired: return "过期";
                default: return "未知状态";
            }
        }

        private static string ScanZoneName(string stableId)
        {
            switch (stableId)
            {
                case "core.exploration.zone.safe-mining":
                    return "安全矿区";
                case "core.exploration.zone.crystal-rift":
                    return "晶隙矿区";
                default:
                    return "探索区";
            }
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    public sealed class GrayboxDeveloperProgressionQuery3D
    {
        internal GrayboxDeveloperProgressionQuery3D(
            int attention,
            string fateId,
            int fateLevel,
            int civilizationLevel,
            string[] committedIds,
            int[] configuredThresholds,
            int[] reachedThresholds,
            string[] pressureQueue,
            string configurationSignature,
            string[] fateDomainStates)
        {
            Attention = attention;
            FateId = fateId ?? string.Empty;
            FateLevel = fateLevel;
            CivilizationLevel = civilizationLevel;
            CommittedIds = Array.AsReadOnly(
                committedIds ?? Array.Empty<string>());
            ConfiguredThresholds = Array.AsReadOnly(
                configuredThresholds ?? Array.Empty<int>());
            ReachedThresholds = Array.AsReadOnly(
                reachedThresholds ?? Array.Empty<int>());
            PressureQueue = Array.AsReadOnly(
                pressureQueue ?? Array.Empty<string>());
            ConfigurationSignature = configurationSignature ?? string.Empty;
            FateDomainStates = Array.AsReadOnly(
                fateDomainStates ?? Array.Empty<string>());
        }

        public int Attention { get; }
        public string FateId { get; }
        public int FateLevel { get; }
        public int CivilizationLevel { get; }
        public IReadOnlyList<string> CommittedIds { get; }
        public IReadOnlyList<int> ConfiguredThresholds { get; }
        public IReadOnlyList<int> ReachedThresholds { get; }
        public IReadOnlyList<string> PressureQueue { get; }
        public string ConfigurationSignature { get; }
        public IReadOnlyList<string> FateDomainStates { get; }
    }

    public sealed class GrayboxDeveloperProgressionFacade3D
    {
        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;
        private readonly FormalCivilizationAscensionRuntime civilization;
        private readonly PocketUniverseFateEffect pocket;
        private readonly FormalVoidDebtRuntime debt;
        private readonly FormalRewindAnchorMetadataRuntime rewind;
        private readonly AttentionPressureRuntime pressure;
        private readonly AdvancementSequenceModel sequence;
        private readonly QuantumEntanglementRuntime quantum;
        private readonly SpatialTemplateRuntime spatial;
        private readonly LocalHasteRuntime haste;
        private readonly ForesightDelayRuntime foresight;
        private readonly CausalTransparencyRuntime causal;
        private readonly VoidChestRuntime chest;
        private readonly CoordinateLockRuntime coordinate;
        private readonly Func<bool> createAnchor;
        private readonly Func<string, bool> readAnchor;
        private readonly Func<bool> clearAnchors;
        private readonly Func<bool> ascend;
        private readonly Func<int, bool> completePressure;
        private readonly Func<bool> resetPressure;
        private readonly Func<bool, bool> setBossDefeated;
        private readonly Func<bool> satisfyAscensionRequirements;
        private readonly Func<bool> clearAscensionRequirements;
        private readonly Func<string, bool> executeExploration;
        private readonly Func<int, int,
            GrayboxDeveloperExplorationDiagnostics3D> queryExploration;
        private readonly GrayboxProgressionEventRouter3D fateRouter;

        public GrayboxDeveloperProgressionFacade3D(
            GrayboxFormalSaveRuntimeHost3D host)
            : this(
                host?.AttentionRuntime,
                host?.FateRuntime,
                host?.Civilization,
                host?.PocketUniverseEffect,
                host?.VoidDebtRuntime,
                host?.RewindAnchorMetadata,
                host?.AttentionPressureRuntime,
                host?.Sequence,
                () => CreateAnchor(host),
                anchorId => host?.RewindAnchorService?.Read(anchorId)
                    ?.Success == true,
                () => host?.RewindAnchorService?.Clear()?.Success == true,
                () => host?.TryAdvanceCivilization()?.Success == true,
                host?.ProgressionEventRouter,
                threshold => host?.CompletePressureFixtureForDevelopment(
                    threshold) == true,
                () => host?.ResetPressureFixtureForDevelopment() == true,
                defeated => host?.SetBossDefeatedForDevelopment(defeated) ==
                    true,
                () => host?.SatisfyAscensionRequirementsForDevelopment() ==
                    true,
                () => host?.ClearAscensionRequirementsForDevelopment() == true,
                host?.QuantumEntanglementRuntime,
                host?.SpatialTemplateRuntime,
                host?.LocalHasteRuntime,
                host?.ForesightDelayRuntime,
                host?.CausalTransparencyRuntime,
                host?.VoidChestRuntime,
                host?.CoordinateLockRuntime,
                actionId => host?.ExecuteExplorationFixtureForDevelopment(
                    actionId) == true,
                (x, y) => GrayboxDeveloperExplorationDiagnostics3D.Capture(
                    host?.ExplorationController?.Exploration,
                    host?.ExplorationController?.OutpostAlerts,
                    host?.CurrentRuleTimeSeconds ?? 0f,
                    x,
                    y))
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
        }

        public GrayboxDeveloperProgressionFacade3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            FormalCivilizationAscensionRuntime civilization,
            PocketUniverseFateEffect pocket,
            FormalVoidDebtRuntime debt,
            FormalRewindAnchorMetadataRuntime rewind,
            AttentionPressureRuntime pressure,
            AdvancementSequenceModel sequence,
            Func<bool> createAnchor = null,
            Func<string, bool> readAnchor = null,
            Func<bool> clearAnchors = null,
            Func<bool> ascend = null,
            GrayboxProgressionEventRouter3D fateRouter = null,
            Func<int, bool> completePressure = null,
            Func<bool> resetPressure = null,
            Func<bool, bool> setBossDefeated = null,
            Func<bool> satisfyAscensionRequirements = null,
            Func<bool> clearAscensionRequirements = null,
            QuantumEntanglementRuntime quantum = null,
            SpatialTemplateRuntime spatial = null,
            LocalHasteRuntime haste = null,
            ForesightDelayRuntime foresight = null,
            CausalTransparencyRuntime causal = null,
            VoidChestRuntime chest = null,
            CoordinateLockRuntime coordinate = null,
            Func<string, bool> executeExploration = null,
            Func<int, int, GrayboxDeveloperExplorationDiagnostics3D>
                queryExploration = null)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.civilization = civilization ??
                throw new ArgumentNullException(nameof(civilization));
            this.pocket = pocket ??
                throw new ArgumentNullException(nameof(pocket));
            this.debt = debt ?? throw new ArgumentNullException(nameof(debt));
            this.rewind = rewind ??
                throw new ArgumentNullException(nameof(rewind));
            this.pressure = pressure ??
                throw new ArgumentNullException(nameof(pressure));
            this.sequence = sequence ??
                throw new ArgumentNullException(nameof(sequence));
            this.createAnchor = createAnchor;
            this.readAnchor = readAnchor;
            this.clearAnchors = clearAnchors;
            this.ascend = ascend;
            this.fateRouter = fateRouter ??
                new GrayboxProgressionEventRouter3D(attention, fate);
            this.completePressure = completePressure;
            this.resetPressure = resetPressure;
            this.setBossDefeated = setBossDefeated;
            this.satisfyAscensionRequirements = satisfyAscensionRequirements;
            this.clearAscensionRequirements = clearAscensionRequirements;
            this.quantum = quantum;
            this.spatial = spatial;
            this.haste = haste;
            this.foresight = foresight;
            this.causal = causal;
            this.chest = chest;
            this.coordinate = coordinate;
            this.executeExploration = executeExploration;
            this.queryExploration = queryExploration;
        }

        public bool IncreaseAttention(int amount)
        {
            if (amount <= 0 || attention.Value >=
                    FormalAttentionCatalog.MaximumValue) return false;
            int before = attention.Value;
            for (var index = 0;
                 index < amount && attention.Value <
                    FormalAttentionCatalog.MaximumValue;
                 index++)
            {
                if (!attention.TryApply(
                        "core.attention.fate.void-debt-periodic",
                        EventKey("attention-increase", index),
                        out _)) break;
            }
            return attention.Value != before;
        }

        public bool DecreaseAttention(int amount)
        {
            if (amount <= 0 || amount % 5 != 0 || attention.Value <= 0)
                return false;
            int before = attention.Value;
            for (var index = 0;
                 index < amount / 5 && attention.Value > 0;
                 index++)
            {
                if (!attention.TryApply(
                        "core.attention.ruins.optional-interference",
                        EventKey("attention-decrease", index),
                        out _)) break;
            }
            return attention.Value != before;
        }

        public bool SetAttentionFixture(int value)
        {
            if (value < FormalAttentionCatalog.MinimumValue ||
                value > FormalAttentionCatalog.MaximumValue ||
                value == attention.Value) return false;
            FormalAttentionSnapshot before = attention.Capture();
            var ordinal = 0;
            while (attention.Value > value)
            {
                if (!attention.TryApply(
                        "core.attention.ruins.optional-interference",
                        EventKey("attention-set-down", ordinal++),
                        out _))
                {
                    attention.TryRestore(before, out _);
                    return false;
                }
            }
            while (attention.Value < value)
            {
                if (!attention.TryApply(
                        "core.attention.fate.void-debt-periodic",
                        EventKey("attention-set-up", ordinal++),
                        out _))
                {
                    attention.TryRestore(before, out _);
                    return false;
                }
            }
            return attention.Value == value;
        }

        public bool SelectFate(string fateId)
        {
            FormalFateSnapshot fateBefore = fate.Capture();
            if (fateBefore.HasSelection ||
                FormalFateCatalog.Find(fateId) == null) return false;
            FormalAttentionSnapshot attentionBefore = attention.Capture();
            FormalCivilizationAscensionSnapshot civilizationBefore =
                civilization.Capture();
            if (!Contains(fateBefore.OfferedIds, fateId))
            {
                var candidate = new FormalFateSnapshot(
                    fateBefore.Revision + 1UL,
                    BuildDevelopmentOffers(fateBefore.OfferedIds, fateId),
                    null,
                    0,
                    fateBefore.OfferSelectionVersion);
                if (!fate.TryRestore(candidate, out _)) return false;
            }
            if (fateRouter.TrySelectFate(fateId, out _) &&
                civilization.TryBindFate(fateId, out _)) return true;
            fate.TryRestore(fateBefore, out _);
            attention.TryRestore(attentionBefore, out _);
            civilization.TryRestore(civilizationBefore, out _);
            return false;
        }

        private static string[] BuildDevelopmentOffers(
            IReadOnlyList<string> current,
            string selectedFateId)
        {
            var result = new string[FormalFateOfferSelector.OfferCount];
            result[0] = selectedFateId;
            var count = 1;
            for (var index = 0;
                 index < current.Count && count < result.Length;
                 index++)
            {
                if (Contains(result, count, current[index])) continue;
                result[count++] = current[index];
            }
            for (var index = 0;
                 index < FormalFateCatalog.All.Count && count < result.Length;
                 index++)
            {
                string fateId = FormalFateCatalog.All[index].Id.Value;
                if (Contains(result, count, fateId)) continue;
                result[count++] = fateId;
            }
            return result;
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string value)
        {
            return Contains(values, values.Count, value);
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            int count,
            string value)
        {
            for (var index = 0; index < count; index++)
            {
                if (string.Equals(
                        values[index], value, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        public bool UpgradeSelectedFateToLevelTwo()
        {
            if (fate.Capture().Level != 1) return false;
            return ascend != null ? ascend() : AscendFixture();
        }

        public bool CreateRewindAnchor() => IsSelected(
                FormalFateCatalog.RewindAnchorId) &&
            createAnchor?.Invoke() == true;
        public bool ReadRewindAnchor(string anchorId) =>
            IsSelected(FormalFateCatalog.RewindAnchorId) &&
            readAnchor?.Invoke(anchorId) == true;
        public bool ClearRewindAnchors() => IsSelected(
                FormalFateCatalog.RewindAnchorId) &&
            clearAnchors?.Invoke() == true;

        public bool AddVoidDebt(string resourceId, int amount)
        {
            if (!IsSelected(FormalFateCatalog.VoidDebtId)) return false;
            int before = debt.GetDebt(resourceId);
            return debt.TryBorrowConstruction(resourceId, amount, out _) &&
                debt.GetDebt(resourceId) != before;
        }

        public bool RepayVoidDebt(string resourceId, int amount)
        {
            if (!IsSelected(FormalFateCatalog.VoidDebtId)) return false;
            int before = debt.GetDebt(resourceId);
            return debt.Repay(
                    resourceId,
                    amount,
                    out _,
                    out _,
                    out _) &&
                debt.GetDebt(resourceId) != before;
        }

        public bool TriggerPressure(int threshold)
        {
            return pressure.TryQueueThreshold(threshold, out _);
        }

        public bool CompletePressureFixture(int threshold)
        {
            return completePressure != null
                ? completePressure(threshold)
                : RestorePressureFixture(threshold, bossDefeated: false);
        }

        public bool ResetPressureFixture()
        {
            if (resetPressure != null) return resetPressure();
            if (pressure.Capture().Entries.Count == 0) return false;
            return pressure.TryRestore(new AttentionPressureSnapshot(
                pressure.Revision + 1UL,
                Array.Empty<AttentionPressureEntrySnapshot>()), out _);
        }

        public bool SetBossDefeatedFixture(bool defeated)
        {
            if (setBossDefeated != null) return setBossDefeated(defeated);
            if (pressure.Capture().CrystalBroodmotherDefeated == defeated)
                return false;
            return defeated
                ? RestorePressureFixture(90, bossDefeated: true)
                : RestorePressureWithoutBoss();
        }

        public bool ExecuteFirstCivilizationAscension()
        {
            return UpgradeSelectedFateToLevelTwo();
        }

        public bool SatisfyAscensionRequirementsFixture()
        {
            return satisfyAscensionRequirements?.Invoke() == true;
        }

        public bool ClearAscensionRequirementsFixture()
        {
            return clearAscensionRequirements?.Invoke() == true;
        }

        public bool ExecuteExplorationFixture(string actionId)
        {
            return !string.IsNullOrWhiteSpace(actionId) &&
                executeExploration?.Invoke(actionId) == true;
        }

        public GrayboxDeveloperExplorationDiagnostics3D QueryExploration(
            int cellX,
            int cellY)
        {
            return queryExploration?.Invoke(cellX, cellY) ??
                GrayboxDeveloperExplorationDiagnostics3D.Capture(
                    null,
                    null,
                    0f,
                    cellX,
                    cellY);
        }

        public GrayboxDeveloperProgressionQuery3D Query()
        {
            FormalAttentionSnapshot attentionState = attention.Capture();
            FormalFateSnapshot fateState = fate.Capture();
            FormalCivilizationAscensionSnapshot civilizationState =
                civilization.Capture();
            AttentionPressureSnapshot pressureState = pressure.Capture();
            var committedSet = new HashSet<string>(
                attentionState.CommittedStableEventKeys,
                StringComparer.Ordinal);
            if (civilizationState.Ascended)
                committedSet.Add(
                    FormalThreeDCivilizationSaveData.FirstAscensionId);
            var committed = new List<string>(committedSet);
            committed.Sort(StringComparer.Ordinal);
            var queue = new string[pressureState.Entries.Count];
            for (var index = 0; index < queue.Length; index++)
            {
                AttentionPressureEntrySnapshot entry =
                    pressureState.Entries[index];
                queue[index] = entry.Threshold + "：" +
                    PressureStateText(entry.State);
            }
            return new GrayboxDeveloperProgressionQuery3D(
                attentionState.Value,
                fateState.SelectedId,
                fateState.Level,
                civilizationState.CivilizationLevel,
                committed.ToArray(),
                Copy(FormalAttentionCatalog.Thresholds),
                Copy(attentionState.ReachedThresholds),
                queue,
                FormalThreeDProgressionSaveData.ConfigurationSignature,
                BuildFateDomainStates());
        }

        private string[] BuildFateDomainStates()
        {
            return new[]
            {
                QuantumStateText(),
                SpatialStateText(),
                HasteStateText(),
                ForesightStateText(),
                CausalStateText(),
                ChestStateText(),
                CoordinateStateText(),
            };
        }

        private string QuantumStateText()
        {
            if (quantum == null) return "量子纠缠：未连接运行时";
            QuantumEntanglementSnapshot state = quantum.Capture();
            return "量子纠缠：" + (state.Connected ? "已连接" : "已断开") +
                "，共享资源 " + state.SharedResourceIds.Count +
                "，同步记录 " + state.CommittedSynchronizationKeys.Count;
        }

        private string SpatialStateText()
        {
            if (spatial == null) return "空间模板：未连接运行时";
            SpatialTemplateSnapshot state = spatial.Capture();
            var cells = 0;
            for (var index = 0; index < state.Templates.Count; index++)
                cells += state.Templates[index].Cells.Count;
            return "空间模板：模板 " + state.Templates.Count +
                "，格位 " + cells;
        }

        private string HasteStateText()
        {
            if (haste == null) return "局部时加：未连接运行时";
            LocalHasteSnapshot state = haste.Capture();
            return "局部时加：" + (state.Active ? "运行中" : "未启动") +
                "，目标 " + (string.IsNullOrEmpty(state.TargetId)
                    ? "无"
                    : HasteTargetText(state.TargetId)) +
                "，剩余 " + state.RemainingBudgetSeconds.ToString("0.##") +
                " 秒，周期 " + state.CurrentCycleOrdinal;
        }

        private static string HasteTargetText(string targetId)
        {
            switch (targetId)
            {
                case "production": return "生产";
                case "research": return "研究";
                case "defense": return "防御";
                default: return "未知目标";
            }
        }

        private string ForesightStateText()
        {
            if (foresight == null) return "预知迟滞：未连接运行时";
            ForesightDelaySnapshot state = foresight.Capture();
            return "预知迟滞：周期 " + state.CurrentCycleOrdinal + "，" +
                (state.LastProjection == null
                    ? "无预告"
                    : "预告 " + state.LastProjection.SummaryKey +
                      "，显示剩余 " +
                      state.DisplayRemainingSeconds.ToString("0.##") + " 秒");
        }

        private string CausalStateText()
        {
            if (causal == null) return "因果透明：未连接运行时";
            CausalTransparencySnapshot state = causal.Capture();
            return "因果透明：完整原因" +
                (state.FullReasonAccess ? "已开放" : "未开放");
        }

        private string ChestStateText()
        {
            if (chest == null) return "虚空宝箱：未连接运行时";
            VoidChestSnapshot state = chest.Capture();
            return "虚空宝箱：评估 " + state.Evaluations.Count +
                "，待领取 " + state.UnclaimedChestIds.Count +
                "，已领取 " + state.ClaimedChestIds.Count;
        }

        private string CoordinateStateText()
        {
            if (coordinate == null) return "坐标锁定：未连接运行时";
            return "坐标锁定：" +
                (coordinate.Capture().Committed ? "已锁定" : "未锁定");
        }

        private bool AscendFixture()
        {
            var controller = new GrayboxCivilizationAdvancementController3D(
                civilization,
                fate,
                attention,
                pocket,
                debt,
                rewind);
            if (!sequence.Start()) return false;
            GrayboxCivilizationAdvancementResult3D result = controller.Execute(
                new FormalCivilizationAscensionRequirements(
                    true, 2, true, true));
            if (result.Success) return true;
            sequence.Restore((int)AdvancementSequenceStage.None, 0f);
            return false;
        }

        private bool RestorePressureFixture(
            int threshold,
            bool bossDefeated)
        {
            if (AttentionPressureCatalog.FindByThreshold(threshold) == null)
                return false;
            AttentionPressureSnapshot before = pressure.Capture();
            var entries = new List<AttentionPressureEntrySnapshot>();
            for (var index = 0;
                 index < AttentionPressureCatalog.All.Count;
                 index++)
            {
                AttentionPressureDefinition definition =
                    AttentionPressureCatalog.All[index];
                if (definition.Threshold > threshold) break;
                entries.Add(new AttentionPressureEntrySnapshot(
                    definition.Threshold,
                    AttentionPressureState.Completed,
                    0f));
            }
            if (bossDefeated && threshold != 90) return false;
            var candidate = new AttentionPressureSnapshot(
                before.Revision + 1UL,
                entries.ToArray());
            if (SamePressure(before, candidate)) return false;
            return pressure.TryRestore(candidate, out _);
        }

        private bool RestorePressureWithoutBoss()
        {
            AttentionPressureSnapshot before = pressure.Capture();
            var entries = new List<AttentionPressureEntrySnapshot>();
            for (var index = 0; index < before.Entries.Count; index++)
            {
                AttentionPressureEntrySnapshot entry = before.Entries[index];
                if (entry.Threshold < 90) entries.Add(entry);
            }
            return pressure.TryRestore(new AttentionPressureSnapshot(
                before.Revision + 1UL,
                entries.ToArray()), out _);
        }

        private static bool SamePressure(
            AttentionPressureSnapshot left,
            AttentionPressureSnapshot right)
        {
            if (left.Entries.Count != right.Entries.Count) return false;
            for (var index = 0; index < left.Entries.Count; index++)
                if (left.Entries[index].Threshold != right.Entries[index].Threshold ||
                    left.Entries[index].State != right.Entries[index].State)
                    return false;
            return true;
        }

        private string EventKey(string action, int ordinal)
        {
            return "developer:" + action + ":" +
                (attention.Revision + 1UL) + ":" + ordinal;
        }

        private bool IsSelected(string fateId)
        {
            return string.Equals(
                fate.Capture().SelectedId,
                fateId,
                StringComparison.Ordinal);
        }

        private static string PressureStateText(AttentionPressureState state)
        {
            switch (state)
            {
                case AttentionPressureState.Queued: return "排队";
                case AttentionPressureState.Warning: return "预警";
                case AttentionPressureState.Active: return "进行中";
                case AttentionPressureState.Completed: return "已完成";
                default: return "未知";
            }
        }

        private static bool CreateAnchor(GrayboxFormalSaveRuntimeHost3D host)
        {
            if (host?.RewindAnchorService == null) return false;
            return host.RewindAnchorService.Create(
                "developer-modifier",
                new[] { "builtin:wastecity@developer-modifier" },
                new FormalSaveCheckpointMetadata
                {
                    sequence = (long)host.RewindAnchorMetadata.Revision + 1L,
                    reasonId = FormalSaveCheckpointReasonIds.RewindAnchorCreated,
                    ruleTimeSeconds = 0f,
                    completedMilestoneIds = Array.Empty<string>(),
                },
                DateTime.UtcNow).Success;
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }

        private static int[] Copy(IReadOnlyList<int> source)
        {
            var result = new int[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }
    }
}
#endif
