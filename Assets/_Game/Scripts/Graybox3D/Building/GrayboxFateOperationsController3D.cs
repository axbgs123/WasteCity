using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
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
            var anchors = new List<string>();
            for (var i = 0; i < rs.Entries.Count; i++)
                anchors.Add("锚点 " + (i + 1) + "  " + rs.Entries[i].CheckpointReasonId);
            bool rewindSelected = string.Equals(fs.SelectedId,
                FormalFateCatalog.RewindAnchorId, StringComparison.Ordinal);
            view.Apply(selected && view.IsOpen, selectedText,
                string.Equals(fs.SelectedId, FormalFateCatalog.PocketUniverseId, StringComparison.Ordinal) ? flagships : Array.Empty<string>(),
                string.Equals(fs.SelectedId, FormalFateCatalog.PocketUniverseId, StringComparison.Ordinal) ? collapse : string.Empty,
                string.Equals(fs.SelectedId, FormalFateCatalog.VoidDebtId, StringComparison.Ordinal) ? debts : Array.Empty<string>(),
                string.Equals(fs.SelectedId, FormalFateCatalog.VoidDebtId, StringComparison.Ordinal) ? "总债务 " + debt.TotalDebt : string.Empty,
                string.Equals(fs.SelectedId, FormalFateCatalog.VoidDebtId, StringComparison.Ordinal) ? "下次结算 " + ds.SettlementRemainingSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒" : string.Empty,
                rewindSelected ? anchors : Array.Empty<string>(), rewindSelected);
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
            if (!view.RewindCommandsVisible) return false;
            view.BeginReadConfirmation();
            return true;
        }

        public bool TryConfirmReadAnchor()
        {
            if (!view.IsReadConfirmationOpen) return false;
            view.ConfirmRead();
            return true;
        }
    }
}
