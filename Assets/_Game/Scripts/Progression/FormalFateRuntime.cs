using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Progression
{
    public sealed class FormalFateSnapshot
    {
        private readonly ReadOnlyCollection<string> offeredIds;

        public FormalFateSnapshot(
            ulong revision,
            string[] offeredIds,
            string selectedId,
            int level)
            : this(revision, offeredIds, selectedId, level, 0)
        {
        }

        public FormalFateSnapshot(
            ulong revision,
            string[] offeredIds,
            string selectedId,
            int level,
            int offerSelectionVersion)
        {
            Revision = revision;
            this.offeredIds = Array.AsReadOnly(
                offeredIds == null
                    ? Array.Empty<string>()
                    : (string[])offeredIds.Clone());
            SelectedId = selectedId;
            Level = level;
            OfferSelectionVersion = offerSelectionVersion;
        }

        public ulong Revision { get; }
        public IReadOnlyList<string> OfferedIds => offeredIds;
        public string SelectedId { get; }
        public int Level { get; }
        public int OfferSelectionVersion { get; }
        public bool HasSelection => !string.IsNullOrWhiteSpace(SelectedId);
    }

    public sealed class FormalFateRuntime
    {
        private const string SelectionAttentionReasonId =
            "core.attention.fate.first-activation";
        private const string SelectionEventKey = "fate-selection-complete";

        private string[] offeredIds;
        private string selectedId;
        private int level;
        private ulong revision;
        private int offerSelectionVersion;
        private FormalFateSnapshot cachedSnapshot;

        public FormalFateRuntime()
        {
            offeredIds = BuildFixedOfferIds();
            RebuildSnapshot();
        }

        public FormalFateRuntime(
            string sessionId,
            int worldSeed,
            int offerSelectorVersion)
        {
            offeredIds = CopyOffers(FormalFateOfferSelector.Select(
                sessionId,
                worldSeed,
                offerSelectorVersion));
            offerSelectionVersion = offerSelectorVersion;
            RebuildSnapshot();
        }

        public bool EffectsReady => FormalFateCatalog.EffectsReady;

        public bool TrySelect(
            string fateId,
            out string attentionReasonId,
            out string stableEventKey,
            out string error)
        {
            attentionReasonId = string.Empty;
            stableEventKey = string.Empty;
            if (!string.IsNullOrEmpty(selectedId))
            {
                error = "A formal fate has already been selected.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(fateId) ||
                FormalFateCatalog.Find(fateId) == null ||
                !ContainsOffer(fateId))
            {
                error = "Unknown formal fate: " + (fateId ?? string.Empty);
                return false;
            }

            selectedId = fateId;
            level = 1;
            unchecked { revision++; }
            RebuildSnapshot();
            attentionReasonId = SelectionAttentionReasonId;
            stableEventKey = SelectionEventKey;
            error = string.Empty;
            return true;
        }

        public bool TryPromoteToLevelTwo(out string error)
        {
            if (string.IsNullOrEmpty(selectedId) || level != 1)
            {
                error = "只有已选择且处于一级的正式命轨可以升至二级";
                return false;
            }
            level = 2;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public FormalFateSnapshot Capture()
        {
            return cachedSnapshot;
        }

        public bool TryRestore(
            FormalFateSnapshot snapshot,
            out string error)
        {
            if (snapshot == null)
            {
                error = "Formal fate snapshot is required.";
                return false;
            }
            if (snapshot.OfferSelectionVersion < 0 ||
                snapshot.OfferSelectionVersion > 1)
            {
                error = "Formal fate offer selection version is unsupported.";
                return false;
            }
            if (!TryCopyApprovedOffers(
                    snapshot.OfferedIds,
                    out string[] restoredOffers))
            {
                error = "Formal fate offers must be three unique approved ids.";
                return false;
            }

            bool pending = !snapshot.HasSelection && snapshot.Level == 0;
            bool selected = snapshot.HasSelection &&
                (snapshot.Level == 1 || snapshot.Level == 2) &&
                FormalFateCatalog.Find(snapshot.SelectedId) != null &&
                ContainsOffer(restoredOffers, snapshot.SelectedId);
            if (!pending && !selected)
            {
                error = "Formal fate selection and level are inconsistent.";
                return false;
            }

            offeredIds = restoredOffers;
            selectedId = pending ? null : snapshot.SelectedId;
            level = snapshot.Level;
            revision = snapshot.Revision;
            offerSelectionVersion = snapshot.OfferSelectionVersion;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private static string[] BuildFixedOfferIds()
        {
            IReadOnlyList<FormalFateDefinition> offers =
                FormalFateCatalog.FixedOffers;
            var result = new string[offers.Count];
            for (var index = 0; index < offers.Count; index++)
                result[index] = offers[index].Id.Value;
            return result;
        }

        private bool ContainsOffer(string fateId)
        {
            return ContainsOffer(offeredIds, fateId);
        }

        private static bool ContainsOffer(
            IReadOnlyList<string> offers,
            string fateId)
        {
            for (var index = 0; index < offers.Count; index++)
            {
                if (string.Equals(
                        offers[index],
                        fateId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool TryCopyApprovedOffers(
            IReadOnlyList<string> candidate,
            out string[] result)
        {
            result = null;
            if (candidate == null ||
                candidate.Count != FormalFateOfferSelector.OfferCount)
                return false;

            var copy = new string[candidate.Count];
            for (var index = 0; index < candidate.Count; index++)
            {
                string id = candidate[index];
                if (string.IsNullOrWhiteSpace(id) ||
                    FormalFateCatalog.Find(id) == null)
                    return false;
                for (var prior = 0; prior < index; prior++)
                {
                    if (string.Equals(copy[prior], id, StringComparison.Ordinal))
                        return false;
                }
                copy[index] = id;
            }

            result = copy;
            return true;
        }

        private static string[] CopyOffers(IReadOnlyList<string> offers)
        {
            var copy = new string[offers.Count];
            for (var index = 0; index < offers.Count; index++)
                copy[index] = offers[index];
            return copy;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new FormalFateSnapshot(
                revision,
                offeredIds,
                selectedId,
                level,
                offerSelectionVersion);
        }
    }
}
