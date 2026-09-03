using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Progression
{
    public sealed class CausalThresholdExplanation
    {
        internal CausalThresholdExplanation(
            int threshold,
            string stageName,
            string stageLocalizationKey,
            bool wasReached,
            int remainingAttention)
        {
            Threshold = threshold;
            StageName = stageName;
            StageLocalizationKey = stageLocalizationKey;
            WasReached = wasReached;
            RemainingAttention = remainingAttention;
        }

        public int Threshold { get; }
        public string StageName { get; }
        public string StageLocalizationKey { get; }
        public bool WasReached { get; }
        public int RemainingAttention { get; }
    }

    public sealed class CausalTransparencyProjection
    {
        private readonly ReadOnlyCollection<FormalAttentionHistoryEntry>
            fullHistory;
        private readonly ReadOnlyCollection<CausalThresholdExplanation>
            thresholds;

        internal CausalTransparencyProjection(
            int value,
            FormalAttentionHistoryEntry[] fullHistory,
            CausalThresholdExplanation[] thresholds)
        {
            Value = value;
            this.fullHistory = Array.AsReadOnly(
                (FormalAttentionHistoryEntry[])fullHistory.Clone());
            this.thresholds = Array.AsReadOnly(
                (CausalThresholdExplanation[])thresholds.Clone());
        }

        public int Value { get; }
        public IReadOnlyList<FormalAttentionHistoryEntry> FullHistory =>
            fullHistory;
        public IReadOnlyList<CausalThresholdExplanation> Thresholds =>
            thresholds;
    }

    public sealed class CausalTransparencySnapshot
    {
        public CausalTransparencySnapshot(
            bool fullReasonAccess,
            ulong revision)
        {
            FullReasonAccess = fullReasonAccess;
            Revision = revision;
        }

        public bool FullReasonAccess { get; }
        public ulong Revision { get; }
    }

    public sealed class CausalTransparencyRuntime
    {
        private bool fullReasonAccess;
        private ulong revision;
        private CausalTransparencySnapshot cachedSnapshot;

        public CausalTransparencyRuntime()
        {
            RebuildSnapshot();
        }

        public bool FullReasonAccess => fullReasonAccess;
        public ulong Revision => revision;

        public bool TrySetFullReasonAccess(bool value)
        {
            if (fullReasonAccess == value) return false;
            fullReasonAccess = value;
            unchecked { revision++; }
            RebuildSnapshot();
            return true;
        }

        public CausalTransparencySnapshot Capture() => cachedSnapshot;

        public bool TryRestore(
            CausalTransparencySnapshot snapshot,
            out string error)
        {
            if (snapshot == null ||
                (snapshot.FullReasonAccess && snapshot.Revision == 0))
            {
                error = "Causal transparency snapshot is inconsistent.";
                return false;
            }

            fullReasonAccess = snapshot.FullReasonAccess;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryProject(
            FormalAttentionSnapshot source,
            out CausalTransparencyProjection projection,
            out string error)
        {
            projection = null;
            if (!fullReasonAccess)
            {
                error = "Full attention history access is not granted.";
                return false;
            }
            if (source == null)
            {
                error = "Attention source is required.";
                return false;
            }

            var history = new FormalAttentionHistoryEntry[source.History.Count];
            for (var index = 0; index < source.History.Count; index++)
                history[index] = source.History[index];
            var explanations = new CausalThresholdExplanation[
                FormalAttentionCatalog.Thresholds.Count];
            var reached = new HashSet<int>(source.ReachedThresholds);
            for (var index = 0;
                 index < FormalAttentionCatalog.Thresholds.Count;
                 index++)
            {
                int threshold = FormalAttentionCatalog.Thresholds[index];
                FormalAttentionStageDefinition stage = FindStage(threshold);
                bool wasReached = reached.Contains(threshold);
                explanations[index] = new CausalThresholdExplanation(
                    threshold,
                    stage?.DisplayName ?? string.Empty,
                    stage?.LocalizationKey ?? string.Empty,
                    wasReached,
                    wasReached ? 0 : Math.Max(0, threshold - source.Value));
            }
            projection = new CausalTransparencyProjection(
                source.Value,
                history,
                explanations);
            error = string.Empty;
            return true;
        }

        private static FormalAttentionStageDefinition FindStage(int threshold)
        {
            for (var index = 0;
                 index < FormalAttentionCatalog.Stages.Count;
                 index++)
            {
                FormalAttentionStageDefinition stage =
                    FormalAttentionCatalog.Stages[index];
                if (stage.MinimumInclusive == threshold) return stage;
            }
            return null;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new CausalTransparencySnapshot(
                fullReasonAccess,
                revision);
        }
    }
}
