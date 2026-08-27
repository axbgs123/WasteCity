using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
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

        public bool RefreshIfChanged()
        {
            FormalFateSnapshot fs = fate.Capture();
            PocketUniverseFateSnapshot ps = pocket.Capture();
            FormalVoidDebtSnapshot ds = debt.Capture();
            FormalRewindAnchorMetadataSnapshot rs = rewind.Capture();
            if (ReferenceEquals(fs, lastFate) && ReferenceEquals(ps, lastPocket) &&
                ReferenceEquals(ds, lastDebt) && ReferenceEquals(rs, lastRewind)) return false;
            bool selected = fs.HasSelection;
            FormalFateDefinition definition = FormalFateCatalog.Find(fs.SelectedId);
            string selectedText = definition == null ? string.Empty :
                definition.DisplayName + "  Lv." + fs.Level;
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
            view.Apply(selected && view.IsOpen, selectedText,
                string.Equals(fs.SelectedId, FormalFateCatalog.PocketUniverseId, StringComparison.Ordinal) ? flagships : Array.Empty<string>(),
                string.Equals(fs.SelectedId, FormalFateCatalog.PocketUniverseId, StringComparison.Ordinal) ? collapse : string.Empty,
                string.Equals(fs.SelectedId, FormalFateCatalog.VoidDebtId, StringComparison.Ordinal) ? debts : Array.Empty<string>(),
                string.Equals(fs.SelectedId, FormalFateCatalog.VoidDebtId, StringComparison.Ordinal) ? "总债务 " + debt.TotalDebt : string.Empty,
                string.Equals(fs.SelectedId, FormalFateCatalog.VoidDebtId, StringComparison.Ordinal) ? "下次结算 " + ds.SettlementRemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒" : string.Empty,
                rewindSelected
                    ? anchors
                    : Array.Empty<GrayboxRewindAnchorSlotPresentation3D>(),
                rewindSelected);
            lastFate = fs; lastPocket = ps; lastDebt = ds; lastRewind = rs;
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
