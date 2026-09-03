using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxFateOperationPresentation3D
    {
        public GrayboxFateOperationPresentation3D(
            string fateId,
            string titleText,
            string ruleText,
            string costText,
            string levelText,
            string statusText,
            string actionText,
            bool genericActionAvailable)
        {
            FateId = fateId ?? string.Empty;
            TitleText = titleText ?? string.Empty;
            RuleText = ruleText ?? string.Empty;
            CostText = costText ?? string.Empty;
            LevelText = levelText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            GenericActionAvailable = genericActionAvailable;
        }

        public string FateId { get; }
        public string TitleText { get; }
        public string RuleText { get; }
        public string CostText { get; }
        public string LevelText { get; }
        public string StatusText { get; }
        public string ActionText { get; }
        public bool GenericActionAvailable { get; }
    }

    public sealed class GrayboxRewindAnchorSlotPresentation3D
    {
        public GrayboxRewindAnchorSlotPresentation3D(
            int slotNumber,
            string anchorId,
            FormalRewindAnchorMetadata metadata)
        {
            SlotNumber = slotNumber;
            AnchorId = anchorId ?? string.Empty;
            IsOccupied = metadata != null;
            CreationOrdinal = metadata?.CreationOrdinal ?? 0L;
            CheckpointReasonId = metadata?.CheckpointReasonId ?? string.Empty;
            DisplayText = IsOccupied
                ? "槽位 " + slotNumber + "  已创建  创建序号 " +
                  CreationOrdinal + "  " + CheckpointReasonId
                : "槽位 " + slotNumber + "  空";
        }

        public int SlotNumber { get; }
        public string AnchorId { get; }
        public bool IsOccupied { get; }
        public long CreationOrdinal { get; }
        public string CheckpointReasonId { get; }
        public string DisplayText { get; }
    }

    public sealed class GrayboxFateOperationsController3D
    {
        private readonly FormalFateRuntime fate;
        private readonly PocketUniverseFateEffect pocket;
        private readonly FormalVoidDebtRuntime debt;
        private readonly FormalRewindAnchorMetadataRuntime rewind;
        private readonly GrayboxFateOperationsView3D view;
        private FormalFateSnapshot lastFate;
        private PocketUniverseFateSnapshot lastPocket;
        private FormalVoidDebtSnapshot lastDebt;
        private FormalRewindAnchorMetadataSnapshot lastRewind;
        private Func<string, string> statusProvider;
        private string lastExternalStatus = string.Empty;

        public GrayboxFateOperationsController3D(FormalFateRuntime fate,
            PocketUniverseFateEffect pocket, FormalVoidDebtRuntime debt,
            FormalRewindAnchorMetadataRuntime rewind,
            GrayboxFateOperationsView3D view)
        {
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.pocket = pocket ?? throw new ArgumentNullException(nameof(pocket));
            this.debt = debt ?? throw new ArgumentNullException(nameof(debt));
            this.rewind = rewind ?? throw new ArgumentNullException(nameof(rewind));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void ConfigureStatusProvider(Func<string, string> provider)
        {
            statusProvider = provider;
            lastFate = null;
            lastExternalStatus = string.Empty;
        }

        public bool RefreshIfChanged()
        {
            FormalFateSnapshot fs = fate.Capture();
            PocketUniverseFateSnapshot ps = pocket.Capture();
            FormalVoidDebtSnapshot ds = debt.Capture();
            FormalRewindAnchorMetadataSnapshot rs = rewind.Capture();
            string externalStatus = statusProvider?.Invoke(
                fs.SelectedId ?? string.Empty) ?? string.Empty;
            if (ReferenceEquals(fs, lastFate) && ReferenceEquals(ps, lastPocket) &&
                ReferenceEquals(ds, lastDebt) && ReferenceEquals(rs, lastRewind) &&
                string.Equals(
                    externalStatus,
                    lastExternalStatus,
                    StringComparison.Ordinal)) return false;
            bool selected = fs.HasSelection;
            FormalFateDefinition definition = FormalFateCatalog.Find(fs.SelectedId);
            var flagships = new List<string>();
            for (var i = 0; i < ps.Flagships.Count; i++)
                flagships.Add(ps.Flagships[i].BuildingDefinitionId + "  " + ps.Flagships[i].StableInstanceId);
            string collapse = ps.CollapsedFlagshipIds.Count > 0 ?
                "已坍缩 " + ps.CollapsedFlagshipIds.Count : "尚未坍缩";
            var debts = new List<string>();
            for (var i = 0; i < ds.Debts.Count; i++)
            {
                FormalVoidDebtEntry entry = ds.Debts[i];
                string name = ResourceDefinitionCatalog.TryGet(entry.ResourceId, out ResourceDefinition resource)
                    ? resource.ChineseName : entry.ResourceId;
                debts.Add(name + " " + entry.Amount);
            }
            bool rewindSelected = string.Equals(fs.SelectedId,
                FormalFateCatalog.RewindAnchorId, StringComparison.Ordinal);
            IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D> anchors =
                rewindSelected
                    ? RewindSlots(fs, rs)
                    : Array.Empty<GrayboxRewindAnchorSlotPresentation3D>();
            GrayboxFateOperationPresentation3D presentation =
                BuildPresentation(
                    fs,
                    definition,
                    flagships,
                    collapse,
                    debts,
                    ds,
                    anchors);
            view.Apply(
                selected && view.IsOpen,
                presentation,
                rewindSelected
                    ? anchors
                    : Array.Empty<GrayboxRewindAnchorSlotPresentation3D>(),
                rewindSelected);
            lastFate = fs; lastPocket = ps; lastDebt = ds; lastRewind = rs;
            lastExternalStatus = externalStatus;
            return true;
        }

        public bool TryOpen()
        {
            if (!fate.Capture().HasSelection) return false;
            view.Open();
            RefreshIfChanged();
            return true;
        }

        public void Close()
        {
            view.Close();
        }

        public void CancelReadConfirmation()
        {
            view.CancelReadConfirmation();
        }

        public bool TryRequestReadAnchor()
        {
            if (!view.RewindCommandsVisible ||
                string.IsNullOrEmpty(view.SelectedRewindAnchorId))
                return false;
            view.BeginReadConfirmation();
            return true;
        }

        public bool TrySelectRewindAnchor(string anchorId)
        {
            return view.TrySelectRewindAnchor(anchorId);
        }

        public bool TryConfirmReadAnchor()
        {
            if (!view.IsReadConfirmationOpen) return false;
            view.ConfirmRead();
            return true;
        }

        public bool TryRequestFateAction()
        {
            return view.IsOpen && view.RequestFateAction();
        }

        public bool TryConfirmFateAction()
        {
            if (!view.IsFateActionConfirmationOpen) return false;
            view.ConfirmFateAction();
            return true;
        }

        public void ReportFateActionResult(bool succeeded, string feedback)
        {
            view.ReportActionResult(succeeded, feedback);
        }

        private GrayboxFateOperationPresentation3D BuildPresentation(
            FormalFateSnapshot fateState,
            FormalFateDefinition definition,
            IReadOnlyList<string> flagships,
            string collapse,
            IReadOnlyList<string> debts,
            FormalVoidDebtSnapshot debtState,
            IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D> anchors)
        {
            if (definition == null)
            {
                return new GrayboxFateOperationPresentation3D(
                    string.Empty,
                    string.Empty,
                    "尚未选择命轨",
                    "无",
                    "未激活",
                    "当前没有可显示的命轨状态",
                    "无可用动作",
                    false);
            }

            string id = definition.Id.Value;
            return new GrayboxFateOperationPresentation3D(
                id,
                definition.DisplayName + "  Lv." + fateState.Level,
                fateState.Level >= 2
                    ? definition.LevelTwoSummary
                    : definition.LevelOneSummary,
                definition.CostSummary,
                "Lv." + fateState.Level,
                BuildStatusText(
                    id,
                    flagships,
                    collapse,
                    debts,
                    debtState,
                    anchors),
                ActionText(id),
                HasGenericAction(id));
        }

        private string BuildStatusText(
            string fateId,
            IReadOnlyList<string> flagships,
            string collapse,
            IReadOnlyList<string> debts,
            FormalVoidDebtSnapshot debtState,
            IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D> anchors)
        {
            string ownerStatus = statusProvider?.Invoke(fateId) ??
                string.Empty;
            if (!string.IsNullOrWhiteSpace(ownerStatus)) return ownerStatus;
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.PocketUniverseId,
                    StringComparison.Ordinal))
            {
                var lines = new List<string>();
                if (flagships.Count == 0)
                    lines.Add("尚未建立生产旗舰");
                else
                    for (var index = 0; index < flagships.Count; index++)
                        lines.Add("旗舰 " + flagships[index]);
                lines.Add(collapse);
                return string.Join("；", lines);
            }
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.VoidDebtId,
                    StringComparison.Ordinal))
            {
                var lines = new List<string>();
                if (debts.Count == 0) lines.Add("当前没有未偿债务");
                else lines.AddRange(debts);
                lines.Add("总债务 " + debt.TotalDebt);
                lines.Add("下次结算 " +
                    debtState.SettlementRemainingSeconds.ToString(
                        "0.0", CultureInfo.InvariantCulture) + " 秒");
                return string.Join("；", lines);
            }
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.RewindAnchorId,
                    StringComparison.Ordinal))
            {
                int occupied = 0;
                for (var index = 0; index < anchors.Count; index++)
                    if (anchors[index].IsOccupied) occupied++;
                return "锚点槽 " + occupied + "/" + anchors.Count +
                    "，请选择槽位后创建、读取或清除";
            }
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.QuantumEntanglementId,
                    StringComparison.Ordinal))
                return "能力已激活；基础资源共享状态由聚落通信网络确认";
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.SpatialTemplateId,
                    StringComparison.Ordinal))
                return "能力已激活；可记录并部署一个 3×3 建筑模板";
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.LocalHasteId,
                    StringComparison.Ordinal))
                return "能力已激活；每个规则周期拥有 60 秒预算，倍率 ×5";
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.ForesightDelayId,
                    StringComparison.Ordinal))
                return "能力已激活；每个规则周期可查看一次 3 秒命运片段";
            if (string.Equals(
                    fateId,
                    FormalFateCatalog.CausalTransparencyId,
                    StringComparison.Ordinal))
                return "能力已激活；完整关注度历史与阈值因果链可查看";
            return "能力已激活；普通敌人死亡时进行 1% 灰烬宝箱判定";
        }

        private static bool HasGenericAction(string fateId)
        {
            return string.Equals(fateId,
                       FormalFateCatalog.QuantumEntanglementId,
                       StringComparison.Ordinal) ||
                string.Equals(fateId,
                    FormalFateCatalog.SpatialTemplateId,
                    StringComparison.Ordinal) ||
                string.Equals(fateId,
                    FormalFateCatalog.LocalHasteId,
                    StringComparison.Ordinal) ||
                string.Equals(fateId,
                    FormalFateCatalog.ForesightDelayId,
                    StringComparison.Ordinal) ||
                string.Equals(fateId,
                    FormalFateCatalog.CausalTransparencyId,
                    StringComparison.Ordinal) ||
                string.Equals(fateId,
                    FormalFateCatalog.VoidChestId,
                    StringComparison.Ordinal);
        }

        private static string ActionText(string fateId)
        {
            if (string.Equals(fateId,
                    FormalFateCatalog.QuantumEntanglementId,
                    StringComparison.Ordinal)) return "查看共享网络";
            if (string.Equals(fateId,
                    FormalFateCatalog.SpatialTemplateId,
                    StringComparison.Ordinal)) return "管理空间模板";
            if (string.Equals(fateId,
                    FormalFateCatalog.LocalHasteId,
                    StringComparison.Ordinal)) return "选择加速目标";
            if (string.Equals(fateId,
                    FormalFateCatalog.ForesightDelayId,
                    StringComparison.Ordinal)) return "查看命运片段";
            if (string.Equals(fateId,
                    FormalFateCatalog.CausalTransparencyId,
                    StringComparison.Ordinal)) return "展开完整因果链";
            if (string.Equals(fateId,
                    FormalFateCatalog.VoidChestId,
                    StringComparison.Ordinal)) return "查看灰烬宝箱";
            if (string.Equals(fateId,
                    FormalFateCatalog.RewindAnchorId,
                    StringComparison.Ordinal)) return "使用下方锚点操作";
            return "自动生效，无需主动操作";
        }

        private IReadOnlyList<GrayboxRewindAnchorSlotPresentation3D>
            RewindSlots(
                FormalFateSnapshot fateState,
                FormalRewindAnchorMetadataSnapshot snapshot)
        {
            var byId = new Dictionary<string, FormalRewindAnchorMetadata>(
                StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Entries.Count; index++)
                byId[snapshot.Entries[index].AnchorId] =
                    snapshot.Entries[index];
            int capacity = fateState.Level == 2 &&
                rewind.MaximumAnchors == 2 ? 2 : 1;
            var result = new GrayboxRewindAnchorSlotPresentation3D[capacity];
            string firstId = GrayboxRewindAnchorService3D.StableAnchorId;
            byId.TryGetValue(firstId, out FormalRewindAnchorMetadata first);
            result[0] = new GrayboxRewindAnchorSlotPresentation3D(
                1, firstId, first);
            if (capacity == 2)
            {
                string secondId =
                    GrayboxRewindAnchorService3D.SecondStableAnchorId;
                byId.TryGetValue(
                    secondId,
                    out FormalRewindAnchorMetadata second);
                result[1] = new GrayboxRewindAnchorSlotPresentation3D(
                    2, secondId, second);
            }
            return Array.AsReadOnly(result);
        }
    }
}
