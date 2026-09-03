using System;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxFateSelectionController3D : IDisposable
    {
        private readonly FormalFateRuntime fate;
        private readonly GrayboxProgressionEventRouter3D router;
        private readonly GrayboxFateSelectionView3D view;
        private readonly Func<bool> effectsReady;
        private FormalFateSnapshot renderedSnapshot;
        private bool renderedReady;

        public event Action<string> SelectionCommitted;

        public GrayboxFateSelectionController3D(
            FormalFateRuntime fate,
            GrayboxProgressionEventRouter3D router,
            GrayboxFateSelectionView3D view,
            Func<bool> effectsReady)
        {
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.router = router ?? throw new ArgumentNullException(nameof(router));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.effectsReady = effectsReady ??
                throw new ArgumentNullException(nameof(effectsReady));
            view.CardSelected += HandleCardSelected;
            view.ConfirmRequested += HandleConfirmRequested;
            view.CancelRequested += CancelConfirmation;
        }

        public bool RefreshIfChanged()
        {
            FormalFateSnapshot snapshot = fate.Capture();
            bool ready = effectsReady();
            if (ReferenceEquals(renderedSnapshot, snapshot) &&
                renderedReady == ready)
            {
                return false;
            }
            view.ApplyCards(ResolveOfferedDefinitions(snapshot));
            if (snapshot.HasSelection)
            {
                view.SetSelectedStatus(SelectedStatus(snapshot));
                view.Close();
            }
            else if (ready)
            {
                view.SetSelectedStatus(string.Empty);
                view.Open();
            }
            else
            {
                view.Close();
            }
            renderedSnapshot = snapshot;
            renderedReady = ready;
            return true;
        }

        public bool TrySelectCard(string fateId, out string error)
        {
            FormalFateSnapshot snapshot = fate.Capture();
            if (!effectsReady() || snapshot.HasSelection ||
                FormalFateCatalog.Find(fateId) == null ||
                !ContainsOffer(snapshot, fateId))
            {
                error = "命轨选择尚未开放或候选无效";
                return false;
            }
            view.ShowConfirmation(fateId);
            error = string.Empty;
            return true;
        }

        public void CancelConfirmation()
        {
            view.CancelConfirmation();
        }

        public bool TryConfirmSelection(out string error)
        {
            string fateId = view.PendingFateId;
            if (!view.IsConfirmationOpen || string.IsNullOrWhiteSpace(fateId))
            {
                error = "尚未选择待确认命轨";
                return false;
            }
            if (!router.TrySelectFate(fateId, out error)) return false;
            FormalFateSnapshot snapshot = fate.Capture();
            view.SetSelectedStatus(SelectedStatus(snapshot));
            view.Close();
            renderedSnapshot = snapshot;
            renderedReady = effectsReady();
            SelectionCommitted?.Invoke(snapshot.SelectedId);
            return true;
        }

        public void Dispose()
        {
            view.CardSelected -= HandleCardSelected;
            view.ConfirmRequested -= HandleConfirmRequested;
            view.CancelRequested -= CancelConfirmation;
            SelectionCommitted = null;
        }

        private static string SelectedStatus(FormalFateSnapshot snapshot)
        {
            FormalFateDefinition definition =
                FormalFateCatalog.Find(snapshot.SelectedId);
            return definition == null
                ? string.Empty
                : "已选择：" + definition.DisplayName + "  Lv." +
                  snapshot.Level;
        }

        private static FormalFateDefinition[] ResolveOfferedDefinitions(
            FormalFateSnapshot snapshot)
        {
            var definitions =
                new FormalFateDefinition[snapshot.OfferedIds.Count];
            for (var index = 0; index < definitions.Length; index++)
            {
                definitions[index] =
                    FormalFateCatalog.Find(snapshot.OfferedIds[index]);
            }
            return definitions;
        }

        private static bool ContainsOffer(
            FormalFateSnapshot snapshot,
            string fateId)
        {
            for (var index = 0; index < snapshot.OfferedIds.Count; index++)
            {
                if (string.Equals(
                        snapshot.OfferedIds[index],
                        fateId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void HandleCardSelected(string fateId)
        {
            TrySelectCard(fateId, out _);
        }

        private void HandleConfirmRequested()
        {
            TryConfirmSelection(out _);
        }
    }
}
