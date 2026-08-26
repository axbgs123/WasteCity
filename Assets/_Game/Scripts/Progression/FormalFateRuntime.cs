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
        {
            Revision = revision;
            this.offeredIds = Array.AsReadOnly(
                offeredIds == null
                    ? Array.Empty<string>()
                    : (string[])offeredIds.Clone());
            SelectedId = selectedId;
            Level = level;
        }

        public ulong Revision { get; }
        public IReadOnlyList<string> OfferedIds => offeredIds;
        public string SelectedId { get; }
        public int Level { get; }
        public bool HasSelection => !string.IsNullOrWhiteSpace(SelectedId);
    }

    public sealed class FormalFateRuntime
    {
        private const string SelectionAttentionReasonId =
            "core.attention.fate.first-activation";
        private const string SelectionEventKey = "fate-selection-complete";

        private readonly string[] offeredIds;
        private string selectedId;
        private int level;
        private ulong revision;
        private FormalFateSnapshot cachedSnapshot;

        public FormalFateRuntime()
        {
            offeredIds = BuildFixedOfferIds();
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
            if (!MatchesFixedOffers(snapshot.OfferedIds))
            {
                error = "Formal fate offers do not match the fixed catalog.";
                return false;
            }

            bool pending = !snapshot.HasSelection && snapshot.Level == 0;
            bool selected = snapshot.HasSelection && snapshot.Level == 1 &&
                FormalFateCatalog.Find(snapshot.SelectedId) != null &&
                ContainsOffer(snapshot.SelectedId);
            if (!pending && !selected)
            {
                error = "Formal fate selection and level are inconsistent.";
                return false;
            }

            selectedId = pending ? null : snapshot.SelectedId;
            level = snapshot.Level;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private static string[] BuildFixedOfferIds()
        {
            IReadOnlyList<FormalFateDefinition> fixedOffers =
                FormalFateCatalog.FixedOffers;
            var result = new string[fixedOffers.Count];
            for (var index = 0; index < fixedOffers.Count; index++)
                result[index] = fixedOffers[index].Id.Value;
            return result;
        }

        private bool ContainsOffer(string fateId)
        {
            for (var index = 0; index < offeredIds.Length; index++)
            {
                if (string.Equals(
                        offeredIds[index],
                        fateId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private bool MatchesFixedOffers(IReadOnlyList<string> candidate)
        {
            if (candidate == null || candidate.Count != offeredIds.Length)
                return false;
            for (var index = 0; index < offeredIds.Length; index++)
            {
                if (!string.Equals(
                        candidate[index],
                        offeredIds[index],
                        StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new FormalFateSnapshot(
                revision,
                offeredIds,
                selectedId,
                level);
        }
    }
}
