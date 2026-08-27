using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxAttentionPressurePresentationController3D
    {
        private readonly AttentionPressureRuntime runtime;
        private readonly GrayboxProgressionHudView3D view;
        private AttentionPressureSnapshot rendered;

        public GrayboxAttentionPressurePresentationController3D(
            AttentionPressureRuntime runtime,
            GrayboxProgressionHudView3D view)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public bool RefreshIfChanged()
        {
            AttentionPressureSnapshot snapshot = runtime.Capture();
            if (ReferenceEquals(rendered, snapshot)) return false;
            var lines = new List<string>();
            string bossStatus = "晶壳母体：未触发";
            string bossPhase = "阶段：未开始";
            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                AttentionPressureEntrySnapshot entry = snapshot.Entries[index];
                AttentionPressureDefinition definition =
                    AttentionPressureCatalog.FindByThreshold(entry.Threshold);
                string state = StateText(entry);
                lines.Add((definition?.DisplayName ?? entry.EncounterId) +
                    "：" + state);
                if (entry.Threshold == 90)
                {
                    bossStatus = "晶壳母体：" + state;
                    bossPhase = entry.State == AttentionPressureState.Completed
                        ? "阶段：已击败"
                        : entry.State == AttentionPressureState.Active
                            ? "阶段一"
                            : "阶段：待命";
                }
            }
            view.ApplyPressure(
                lines.Count == 0 ? "暂无压力事件" : string.Join("  ", lines),
                bossStatus,
                bossPhase);
            rendered = snapshot;
            return true;
        }

        private static string StateText(AttentionPressureEntrySnapshot entry)
        {
            switch (entry.State)
            {
                case AttentionPressureState.Queued:
                    return "排队";
                case AttentionPressureState.Warning:
                    return "预警 " + entry.WarningRemainingSeconds.ToString(
                        "0", CultureInfo.InvariantCulture) + " 秒";
                case AttentionPressureState.Active:
                    return "进行中";
                case AttentionPressureState.Completed:
                    return "已完成";
                default:
                    return "未知";
            }
        }
    }
}
