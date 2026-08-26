using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProgressionHudController3D
    {
        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;
        private readonly GrayboxProgressionHudView3D view;
        private FormalAttentionSnapshot renderedAttention;
        private FormalFateSnapshot renderedFate;

        public GrayboxProgressionHudController3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            GrayboxProgressionHudView3D view)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public bool EffectsReady =>
            FormalFateCatalog.EffectsReady && fate.EffectsReady;

        public bool RefreshIfChanged()
        {
            FormalAttentionSnapshot attentionSnapshot = attention.Capture();
            FormalFateSnapshot fateSnapshot = fate.Capture();
            if (ReferenceEquals(renderedAttention, attentionSnapshot) &&
                ReferenceEquals(renderedFate, fateSnapshot))
            {
                return false;
            }

            view.Apply(
                "关注度 " + attentionSnapshot.Value.ToString(
                    CultureInfo.InvariantCulture) + "/" +
                FormalAttentionCatalog.MaximumValue.ToString(
                    CultureInfo.InvariantCulture),
                StageName(attentionSnapshot.Value),
                NextThreshold(attentionSnapshot),
                RecentReasons(attentionSnapshot.RecentHistory),
                Copy(fateSnapshot.OfferedIds),
                EffectsReady);
            renderedAttention = attentionSnapshot;
            renderedFate = fateSnapshot;
            return true;
        }

        private static string StageName(int value)
        {
            FormalAttentionStageDefinition stage =
                FormalAttentionCatalog.StageFor(value);
            return stage?.DisplayName ?? "未知阶段";
        }

        private static string NextThreshold(
            FormalAttentionSnapshot snapshot)
        {
            if (FormalAttentionCatalog.TryGetNextUnreachedThreshold(
                    snapshot.Value,
                    snapshot.ReachedThresholds,
                    out int threshold,
                    out int distance))
            {
                return "下一阈值 " + threshold.ToString(
                    CultureInfo.InvariantCulture) + "（还差 " +
                    distance.ToString(CultureInfo.InvariantCulture) + "）";
            }
            return "所有压力阈值已锁存";
        }

        private static IReadOnlyList<string> RecentReasons(
            IReadOnlyList<FormalAttentionHistoryEntry> history)
        {
            if (history == null || history.Count == 0)
                return Array.Empty<string>();
            var result = new string[history.Count];
            for (var index = 0; index < history.Count; index++)
            {
                FormalAttentionHistoryEntry entry = history[index];
                string display = FormalAttentionCatalog
                    .DisplayNameForReason(entry.ReasonId);
                string sign = entry.AppliedDelta >= 0 ? "+" : string.Empty;
                result[index] = display + " " + sign +
                    entry.AppliedDelta.ToString(CultureInfo.InvariantCulture);
            }
            return Array.AsReadOnly(result);
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }
    }
}
