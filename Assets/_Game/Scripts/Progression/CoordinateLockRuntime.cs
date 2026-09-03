using System;

namespace WasteCity.Progression
{
    public static class CoordinateLockCatalog
    {
        public const int TargetAttention = 90;
        public const int RequiredCompletedPressureThreshold = 60;
        public const string AttentionReasonId =
            "core.attention.world.coordinate-locked";
        public const string StableEventKey = "world-coordinate-lock";

        public static bool IsSixthActEquivalent(
            AdvancementSequenceStage stage)
        {
            return stage == AdvancementSequenceStage.Continued;
        }
    }

    public sealed class CoordinateLockSnapshot
    {
        public CoordinateLockSnapshot(bool committed, ulong revision)
        {
            Committed = committed;
            Revision = revision;
        }

        public bool Committed { get; }
        public ulong Revision { get; }
    }

    public sealed class CoordinateLockRuntime
    {
        private readonly FormalAttentionRuntime attention;
        private readonly AttentionPressureRuntime pressure;
        private bool committed;
        private ulong revision;
        private CoordinateLockSnapshot cachedSnapshot;

        public CoordinateLockRuntime(
            FormalAttentionRuntime attention,
            AttentionPressureRuntime pressure)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.pressure = pressure ??
                throw new ArgumentNullException(nameof(pressure));
            RebuildSnapshot();
        }

        public bool TryCommit(
            bool legacyAnalysisCompleted,
            bool sixthActReached,
            out string error)
        {
            if (committed)
            {
                error = "坐标锁定已经提交";
                return false;
            }
            if (!legacyAnalysisCompleted || !sixthActReached ||
                !HasCompletedPressure(
                    CoordinateLockCatalog.RequiredCompletedPressureThreshold))
            {
                error = "坐标锁定的遗产解析、高危压力或流程条件未满足";
                return false;
            }

            FormalAttentionSnapshot attentionBefore = attention.Capture();
            AttentionPressureSnapshot pressureBefore = pressure.Capture();
            if (!attention.TryRaiseToAtLeast(
                    CoordinateLockCatalog.AttentionReasonId,
                    CoordinateLockCatalog.StableEventKey,
                    CoordinateLockCatalog.TargetAttention,
                    out error))
            {
                return false;
            }

            if (!HasPressure(CoordinateLockCatalog.TargetAttention) &&
                !pressure.TryQueueThreshold(
                    CoordinateLockCatalog.TargetAttention,
                    out error))
            {
                attention.TryRestore(attentionBefore, out _);
                pressure.TryRestore(pressureBefore, out _);
                return false;
            }

            committed = true;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public CoordinateLockSnapshot Capture() => cachedSnapshot;

        public bool TryRestore(
            CoordinateLockSnapshot snapshot,
            out string error)
        {
            bool clean = snapshot != null && !snapshot.Committed &&
                snapshot.Revision == 0ul;
            bool completed = snapshot != null && snapshot.Committed &&
                snapshot.Revision > 0ul;
            if (!clean && !completed)
            {
                error = "坐标锁定快照无效";
                return false;
            }
            committed = snapshot.Committed;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private bool HasCompletedPressure(int threshold)
        {
            var entries = pressure.Capture().Entries;
            for (var index = 0; index < entries.Count; index++)
            {
                AttentionPressureEntrySnapshot entry = entries[index];
                if (entry.Threshold == threshold)
                    return entry.State == AttentionPressureState.Completed;
            }
            return false;
        }

        private bool HasPressure(int threshold)
        {
            var entries = pressure.Capture().Entries;
            for (var index = 0; index < entries.Count; index++)
                if (entries[index].Threshold == threshold) return true;
            return false;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new CoordinateLockSnapshot(committed, revision);
        }
    }
}
