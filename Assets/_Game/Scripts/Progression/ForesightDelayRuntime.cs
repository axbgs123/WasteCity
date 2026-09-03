using System;
using System.Collections.Generic;

namespace WasteCity.Progression
{
    public sealed class ForesightAuthoritativePlan
    {
        public ForesightAuthoritativePlan(
            string eventId,
            float occurrenceRuleTimeSeconds,
            string summaryKey)
        {
            EventId = eventId ?? string.Empty;
            OccurrenceRuleTimeSeconds = occurrenceRuleTimeSeconds;
            SummaryKey = summaryKey ?? string.Empty;
        }

        public string EventId { get; }
        public float OccurrenceRuleTimeSeconds { get; }
        public string SummaryKey { get; }
    }

    public sealed class ForesightProjection
    {
        public ForesightProjection(
            string eventId,
            string summaryKey,
            float occurrenceRuleTimeSeconds,
            float secondsUntilEvent)
        {
            EventId = eventId;
            SummaryKey = summaryKey;
            OccurrenceRuleTimeSeconds = occurrenceRuleTimeSeconds;
            SecondsUntilEvent = secondsUntilEvent;
        }

        public string EventId { get; }
        public string SummaryKey { get; }
        public float OccurrenceRuleTimeSeconds { get; }
        public float SecondsUntilEvent { get; }
    }

    public sealed class ForesightDelaySnapshot
    {
        public ForesightDelaySnapshot(
            ulong lastConsumedCycleOrdinal,
            ForesightProjection lastProjection,
            ulong revision)
            : this(
                lastConsumedCycleOrdinal,
                lastConsumedCycleOrdinal,
                lastProjection,
                lastProjection == null
                    ? 0f
                    : FormalFateCatalog.ForesightDisplaySeconds,
                revision)
        {
        }

        public ForesightDelaySnapshot(
            ulong currentCycleOrdinal,
            ulong lastConsumedCycleOrdinal,
            ForesightProjection lastProjection,
            ulong revision)
            : this(
                currentCycleOrdinal,
                lastConsumedCycleOrdinal,
                lastProjection,
                lastProjection == null
                    ? 0f
                    : FormalFateCatalog.ForesightDisplaySeconds,
                revision)
        {
        }

        public ForesightDelaySnapshot(
            ulong currentCycleOrdinal,
            ulong lastConsumedCycleOrdinal,
            ForesightProjection lastProjection,
            float displayRemainingSeconds,
            ulong revision)
        {
            CurrentCycleOrdinal = currentCycleOrdinal;
            LastConsumedCycleOrdinal = lastConsumedCycleOrdinal;
            LastProjection = lastProjection == null
                ? null
                : new ForesightProjection(
                    lastProjection.EventId,
                    lastProjection.SummaryKey,
                    lastProjection.OccurrenceRuleTimeSeconds,
                    lastProjection.SecondsUntilEvent);
            DisplayRemainingSeconds = displayRemainingSeconds;
            Revision = revision;
        }

        public ulong LastConsumedCycleOrdinal { get; }
        public ulong CurrentCycleOrdinal { get; }
        public ForesightProjection LastProjection { get; }
        public float DisplayRemainingSeconds { get; }
        public ulong Revision { get; }
    }

    public sealed class ForesightDelayRuntime
    {
        private ulong lastConsumedCycleOrdinal;
        private ulong currentCycleOrdinal;
        private ForesightProjection lastProjection;
        private float displayRemainingSeconds;
        private ulong revision;
        private ForesightDelaySnapshot cachedSnapshot;

        public ForesightDelayRuntime()
        {
            RebuildSnapshot();
        }

        public ulong Revision => revision;
        public ulong CurrentCycleOrdinal => currentCycleOrdinal;

        public bool TryEnterCycle(ulong cycleOrdinal, out string error)
        {
            if (cycleOrdinal == 0 || cycleOrdinal <= currentCycleOrdinal)
            {
                error = "Foresight cycle must advance monotonically.";
                return false;
            }
            currentCycleOrdinal = cycleOrdinal;
            lastProjection = null;
            displayRemainingSeconds = 0f;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryReveal(
            ulong cycleOrdinal,
            float currentRuleTimeSeconds,
            IEnumerable<ForesightAuthoritativePlan> authoritativePlans,
            out ForesightProjection projection,
            out string error)
        {
            projection = null;
            if (cycleOrdinal == 0 ||
                cycleOrdinal != currentCycleOrdinal ||
                cycleOrdinal == lastConsumedCycleOrdinal ||
                float.IsNaN(currentRuleTimeSeconds) ||
                float.IsInfinity(currentRuleTimeSeconds) ||
                authoritativePlans == null)
            {
                error = "Foresight cycle, time, or plans are invalid.";
                return false;
            }

            var plans = new List<ForesightAuthoritativePlan>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ForesightAuthoritativePlan plan in authoritativePlans)
            {
                if (plan == null || string.IsNullOrWhiteSpace(plan.EventId) ||
                    string.IsNullOrWhiteSpace(plan.SummaryKey) ||
                    float.IsNaN(plan.OccurrenceRuleTimeSeconds) ||
                    float.IsInfinity(plan.OccurrenceRuleTimeSeconds) ||
                    !eventIds.Add(plan.EventId))
                {
                    error = "Authoritative foresight plans are invalid.";
                    return false;
                }
                if (plan.OccurrenceRuleTimeSeconds >= currentRuleTimeSeconds)
                    plans.Add(plan);
            }
            if (plans.Count == 0)
            {
                error = "No future authoritative plan is available.";
                return false;
            }
            plans.Sort(ComparePlans);
            ForesightAuthoritativePlan selected = plans[0];
            projection = new ForesightProjection(
                selected.EventId,
                selected.SummaryKey,
                selected.OccurrenceRuleTimeSeconds,
                selected.OccurrenceRuleTimeSeconds - currentRuleTimeSeconds);
            lastConsumedCycleOrdinal = cycleOrdinal;
            lastProjection = projection;
            displayRemainingSeconds = FormalFateCatalog.ForesightDisplaySeconds;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TickDisplay(
            float deltaRuleSeconds,
            bool paused,
            out string error)
        {
            if (float.IsNaN(deltaRuleSeconds) ||
                float.IsInfinity(deltaRuleSeconds) ||
                deltaRuleSeconds < 0f)
            {
                error = "Foresight display delta is invalid.";
                return false;
            }
            if (paused || deltaRuleSeconds == 0f || lastProjection == null)
            {
                error = string.Empty;
                return true;
            }

            displayRemainingSeconds = Math.Max(
                0f,
                displayRemainingSeconds - deltaRuleSeconds);
            if (displayRemainingSeconds == 0f)
                lastProjection = null;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public ForesightDelaySnapshot Capture() => cachedSnapshot;

        public bool TryRestore(
            ForesightDelaySnapshot snapshot,
            out string error)
        {
            if (snapshot == null)
            {
                error = "Foresight delay snapshot is required.";
                return false;
            }

            ForesightProjection projection = snapshot.LastProjection;
            bool displayIsValid =
                !float.IsNaN(snapshot.DisplayRemainingSeconds) &&
                !float.IsInfinity(snapshot.DisplayRemainingSeconds) &&
                snapshot.DisplayRemainingSeconds >= 0f &&
                snapshot.DisplayRemainingSeconds <=
                    FormalFateCatalog.ForesightDisplaySeconds;
            bool defaultState = snapshot.CurrentCycleOrdinal == 0 &&
                snapshot.LastConsumedCycleOrdinal == 0 &&
                projection == null && snapshot.DisplayRemainingSeconds == 0f &&
                snapshot.Revision == 0;
            bool enteredState = snapshot.CurrentCycleOrdinal > 0 &&
                snapshot.LastConsumedCycleOrdinal <
                    snapshot.CurrentCycleOrdinal &&
                projection == null && snapshot.DisplayRemainingSeconds == 0f &&
                snapshot.Revision > 0;
            bool visibleConsumedState = snapshot.CurrentCycleOrdinal > 0 &&
                snapshot.LastConsumedCycleOrdinal ==
                    snapshot.CurrentCycleOrdinal &&
                snapshot.DisplayRemainingSeconds > 0f &&
                snapshot.Revision > 0 && IsValidProjection(projection);
            bool hiddenConsumedState = snapshot.CurrentCycleOrdinal > 0 &&
                snapshot.LastConsumedCycleOrdinal ==
                    snapshot.CurrentCycleOrdinal &&
                projection == null && snapshot.DisplayRemainingSeconds == 0f &&
                snapshot.Revision > 0;
            if (!displayIsValid ||
                (!defaultState && !enteredState &&
                 !visibleConsumedState && !hiddenConsumedState))
            {
                error = "Foresight delay snapshot is inconsistent.";
                return false;
            }

            currentCycleOrdinal = snapshot.CurrentCycleOrdinal;
            lastConsumedCycleOrdinal = snapshot.LastConsumedCycleOrdinal;
            lastProjection = projection == null
                ? null
                : new ForesightProjection(
                    projection.EventId,
                    projection.SummaryKey,
                    projection.OccurrenceRuleTimeSeconds,
                    projection.SecondsUntilEvent);
            displayRemainingSeconds = snapshot.DisplayRemainingSeconds;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new ForesightDelaySnapshot(
                currentCycleOrdinal,
                lastConsumedCycleOrdinal,
                lastProjection,
                displayRemainingSeconds,
                revision);
        }

        private static int ComparePlans(
            ForesightAuthoritativePlan left,
            ForesightAuthoritativePlan right)
        {
            int byTime = left.OccurrenceRuleTimeSeconds.CompareTo(
                right.OccurrenceRuleTimeSeconds);
            return byTime != 0
                ? byTime
                : string.CompareOrdinal(left.EventId, right.EventId);
        }

        private static bool IsValidProjection(ForesightProjection projection)
        {
            return projection != null &&
                !string.IsNullOrWhiteSpace(projection.EventId) &&
                !string.IsNullOrWhiteSpace(projection.SummaryKey) &&
                !float.IsNaN(projection.OccurrenceRuleTimeSeconds) &&
                !float.IsInfinity(projection.OccurrenceRuleTimeSeconds) &&
                !float.IsNaN(projection.SecondsUntilEvent) &&
                !float.IsInfinity(projection.SecondsUntilEvent) &&
                projection.SecondsUntilEvent >= 0f;
        }
    }
}
