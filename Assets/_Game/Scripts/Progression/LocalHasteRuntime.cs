using System;

namespace WasteCity.Progression
{
    public sealed class LocalHasteTickProjection
    {
        internal LocalHasteTickProjection(
            string targetId,
            float consumedBudgetSeconds,
            float effectiveRuleSeconds,
            bool activeAfterTick)
        {
            TargetId = targetId ?? string.Empty;
            ConsumedBudgetSeconds = consumedBudgetSeconds;
            EffectiveRuleSeconds = effectiveRuleSeconds;
            ActiveAfterTick = activeAfterTick;
        }

        public string TargetId { get; }
        public float ConsumedBudgetSeconds { get; }
        public float EffectiveRuleSeconds { get; }
        public bool ActiveAfterTick { get; }
    }

    public sealed class LocalHasteSnapshot
    {
        public LocalHasteSnapshot(
            string targetId,
            bool active,
            float remainingBudgetSeconds,
            ulong revision)
            : this(
                targetId,
                active,
                remainingBudgetSeconds,
                revision,
                0ul)
        {
        }

        public LocalHasteSnapshot(
            string targetId,
            bool active,
            float remainingBudgetSeconds,
            ulong revision,
            ulong currentCycleOrdinal)
        {
            TargetId = targetId ?? string.Empty;
            Active = active;
            RemainingBudgetSeconds = remainingBudgetSeconds;
            Revision = revision;
            CurrentCycleOrdinal = currentCycleOrdinal;
        }

        public string TargetId { get; }
        public bool Active { get; }
        public float RemainingBudgetSeconds { get; }
        public ulong Revision { get; }
        public ulong CurrentCycleOrdinal { get; }
    }

    public sealed class LocalHasteRuntime
    {
        public const float LevelOneBudgetSeconds = 60f;
        public const float LevelOneMultiplier = 5f;
        public const string ProductionTargetId = "production";
        public const string ResearchTargetId = "research";
        public const string DefenseTargetId = "defense";

        private string targetId = string.Empty;
        private bool active;
        private float remainingBudgetSeconds = LevelOneBudgetSeconds;
        private ulong currentCycleOrdinal;
        private ulong revision;
        private LocalHasteSnapshot cachedSnapshot;

        public LocalHasteRuntime()
        {
            RebuildSnapshot();
        }

        public string TargetId => targetId;
        public bool IsActive => active;
        public float RemainingBudgetSeconds => remainingBudgetSeconds;
        public float Multiplier => LevelOneMultiplier;
        public float RuleCycleSeconds => FormalFateCatalog.RuleCycleSeconds;
        public ulong CurrentCycleOrdinal => currentCycleOrdinal;
        public ulong Revision => revision;

        public bool TryEnterCycle(ulong cycleOrdinal, out string error)
        {
            if (cycleOrdinal == 0 || cycleOrdinal <= currentCycleOrdinal)
            {
                error = "Haste cycle must advance monotonically.";
                return false;
            }
            currentCycleOrdinal = cycleOrdinal;
            remainingBudgetSeconds = LevelOneBudgetSeconds;
            active = false;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TrySelectTarget(string value, out string error)
        {
            if (active || !TryGetTargetKind(value, out int targetKind) ||
                targetKind == 0 ||
                string.Equals(targetId, value, StringComparison.Ordinal))
            {
                error = "Haste target is invalid, unchanged, or currently active.";
                return false;
            }
            targetId = value;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryStart(out string error)
        {
            if (active || remainingBudgetSeconds <= 0f ||
                string.IsNullOrEmpty(targetId))
            {
                error = "Haste requires a target and remaining budget.";
                return false;
            }
            active = true;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryStop()
        {
            if (!active) return false;
            active = false;
            unchecked { revision++; }
            RebuildSnapshot();
            return true;
        }

        public bool Tick(
            float ruleDeltaSeconds,
            bool globallyPaused,
            out LocalHasteTickProjection projection,
            out string error)
        {
            if (float.IsNaN(ruleDeltaSeconds) ||
                float.IsInfinity(ruleDeltaSeconds) ||
                ruleDeltaSeconds < 0f)
            {
                projection = null;
                error = "Haste delta must be finite and non-negative.";
                return false;
            }

            if (!active || globallyPaused || ruleDeltaSeconds == 0f)
            {
                projection = new LocalHasteTickProjection(
                    targetId,
                    0f,
                    0f,
                    active);
                error = string.Empty;
                return true;
            }

            float consumed = Math.Min(
                remainingBudgetSeconds,
                ruleDeltaSeconds);
            remainingBudgetSeconds -= consumed;
            if (remainingBudgetSeconds <= 0.00001f)
            {
                remainingBudgetSeconds = 0f;
                active = false;
            }
            unchecked { revision++; }
            RebuildSnapshot();
            projection = new LocalHasteTickProjection(
                targetId,
                consumed,
                consumed * LevelOneMultiplier,
                active);
            error = string.Empty;
            return true;
        }

        public LocalHasteSnapshot Capture() => cachedSnapshot;

        public bool TryRestore(LocalHasteSnapshot snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Local haste snapshot is required.";
                return false;
            }

            bool blankTarget = string.IsNullOrEmpty(snapshot.TargetId);
            bool invalidTarget = !TryGetTargetKind(
                snapshot.TargetId,
                out int targetKind) ||
                !blankTarget && targetKind == 0;
            bool invalidBudget =
                float.IsNaN(snapshot.RemainingBudgetSeconds) ||
                float.IsInfinity(snapshot.RemainingBudgetSeconds) ||
                snapshot.RemainingBudgetSeconds < 0f ||
                snapshot.RemainingBudgetSeconds > LevelOneBudgetSeconds;
            bool invalidActive = snapshot.Active &&
                (blankTarget || snapshot.RemainingBudgetSeconds <= 0f);
            bool unreachableBlank = blankTarget &&
                (snapshot.Active ||
                 Math.Abs(snapshot.RemainingBudgetSeconds -
                     LevelOneBudgetSeconds) > 0.00001f ||
                 snapshot.CurrentCycleOrdinal == 0 &&
                 snapshot.Revision != 0);
            bool invalidCycleRevision =
                snapshot.CurrentCycleOrdinal > 0 && snapshot.Revision == 0 ||
                snapshot.Revision == 0 &&
                (!blankTarget || snapshot.Active ||
                 snapshot.CurrentCycleOrdinal != 0);
            if (invalidTarget || invalidBudget || invalidActive ||
                unreachableBlank || invalidCycleRevision)
            {
                error = "Local haste snapshot is inconsistent.";
                return false;
            }

            targetId = snapshot.TargetId;
            active = snapshot.Active;
            remainingBudgetSeconds = snapshot.RemainingBudgetSeconds;
            currentCycleOrdinal = snapshot.CurrentCycleOrdinal;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public static bool TryGetTargetKind(string value, out int targetKind)
        {
            if (string.IsNullOrEmpty(value))
            {
                targetKind = 0;
                return true;
            }
            if (string.Equals(
                    value,
                    ProductionTargetId,
                    StringComparison.Ordinal))
            {
                targetKind = 1;
                return true;
            }
            if (string.Equals(
                    value,
                    ResearchTargetId,
                    StringComparison.Ordinal))
            {
                targetKind = 2;
                return true;
            }
            if (string.Equals(
                    value,
                    DefenseTargetId,
                    StringComparison.Ordinal))
            {
                targetKind = 3;
                return true;
            }
            targetKind = -1;
            return false;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new LocalHasteSnapshot(
                targetId,
                active,
                remainingBudgetSeconds,
                revision,
                currentCycleOrdinal);
        }
    }
}
