using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Progression
{
    public sealed class FormalAttentionHistoryEntry
    {
        public FormalAttentionHistoryEntry(
            string reasonId,
            string stableEventKey,
            int requestedDelta,
            int appliedDelta,
            int valueAfter,
            ulong revision)
            : this(
                reasonId,
                stableEventKey,
                requestedDelta,
                appliedDelta,
                valueAfter,
                revision,
                0f,
                null)
        {
        }

        public FormalAttentionHistoryEntry(
            string reasonId,
            string stableEventKey,
            int requestedDelta,
            int appliedDelta,
            int valueAfter,
            ulong revision,
            float ruleTimeSeconds,
            string sourceInstanceId)
        {
            ReasonId = reasonId;
            StableEventKey = stableEventKey;
            RequestedDelta = requestedDelta;
            AppliedDelta = appliedDelta;
            ValueAfter = valueAfter;
            Revision = revision;
            RuleTimeSeconds = ruleTimeSeconds;
            SourceInstanceId = sourceInstanceId;
        }

        public string ReasonId { get; }
        public string StableEventKey { get; }
        public int RequestedDelta { get; }
        public int AppliedDelta { get; }
        public int ValueAfter { get; }
        public ulong Revision { get; }
        public float RuleTimeSeconds { get; }
        public string SourceInstanceId { get; }
    }

    public sealed class FormalAttentionSnapshot
    {
        private readonly ReadOnlyCollection<FormalAttentionHistoryEntry>
            history;
        private readonly ReadOnlyCollection<FormalAttentionHistoryEntry>
            recentHistory;
        private readonly ReadOnlyCollection<int> reachedThresholds;
        private readonly ReadOnlyCollection<string> committedEventKeys;
        private readonly ReadOnlyCollection<string> onceReasonIds;

        public FormalAttentionSnapshot(
            int value,
            ulong revision,
            FormalAttentionHistoryEntry[] history,
            int[] reachedThresholds,
            string[] committedEventKeys,
            string[] onceReasonIds)
        {
            Value = value;
            Revision = revision;

            FormalAttentionHistoryEntry[] historyCopy = history == null
                ? Array.Empty<FormalAttentionHistoryEntry>()
                : (FormalAttentionHistoryEntry[])history.Clone();
            int recentCount = Math.Min(
                FormalAttentionCatalog.RecentReasonCapacity,
                historyCopy.Length);
            var recentCopy = new FormalAttentionHistoryEntry[recentCount];
            if (recentCount > 0)
            {
                Array.Copy(
                    historyCopy,
                    historyCopy.Length - recentCount,
                    recentCopy,
                    0,
                    recentCount);
            }

            this.history = Array.AsReadOnly(historyCopy);
            recentHistory = Array.AsReadOnly(recentCopy);
            this.reachedThresholds = Array.AsReadOnly(
                reachedThresholds == null
                    ? Array.Empty<int>()
                    : (int[])reachedThresholds.Clone());
            this.committedEventKeys = Array.AsReadOnly(
                committedEventKeys == null
                    ? Array.Empty<string>()
                    : (string[])committedEventKeys.Clone());
            this.onceReasonIds = Array.AsReadOnly(
                onceReasonIds == null
                    ? Array.Empty<string>()
                    : (string[])onceReasonIds.Clone());
        }

        public int Value { get; }
        public ulong Revision { get; }
        public IReadOnlyList<FormalAttentionHistoryEntry> History => history;
        public IReadOnlyList<FormalAttentionHistoryEntry> RecentHistory =>
            recentHistory;
        public IReadOnlyList<int> ReachedThresholds => reachedThresholds;
        public IReadOnlyList<string> CommittedEventKeys =>
            committedEventKeys;
        public IReadOnlyList<string> CommittedStableEventKeys =>
            committedEventKeys;
        public IReadOnlyList<string> OnceReasonIds => onceReasonIds;
        public IReadOnlyList<string> AppliedOnceReasonIds => onceReasonIds;
    }

    public sealed class FormalAttentionRuntime
    {
        private readonly Queue<FormalAttentionHistoryEntry> history =
            new Queue<FormalAttentionHistoryEntry>(
                FormalAttentionCatalog.HistoryCapacity);
        private readonly HashSet<int> reachedThresholds =
            new HashSet<int>();
        private readonly HashSet<string> committedEventKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> onceReasonIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> orphanReasonIds =
            new HashSet<string>(StringComparer.Ordinal);

        private int value = FormalAttentionCatalog.InitialValue;
        private ulong revision;
        private FormalAttentionSnapshot cachedSnapshot;

        public FormalAttentionRuntime()
        {
            RebuildSnapshot();
        }

        public int Value => value;
        public ulong Revision => revision;
        public Exception LastThresholdNotificationFailure { get; private set; }

        public event Action<int> ThresholdReached;

        public bool TryApply(
            string reasonId,
            string stableEventKey,
            out string error)
        {
            if (string.IsNullOrWhiteSpace(reasonId))
            {
                error = "Attention reason ID is required.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(stableEventKey))
            {
                error = "Stable attention event key is required.";
                return false;
            }
            if (orphanReasonIds.Contains(reasonId))
            {
                error = "Restored orphan attention reasons are read-only.";
                return false;
            }

            FormalAttentionReasonDefinition reason =
                FormalAttentionCatalog.Find(reasonId);
            if (reason == null)
            {
                error = "Unknown attention reason: " + reasonId;
                return false;
            }
            if (reason.RepeatPolicy ==
                    FormalAttentionRepeatPolicy.OncePerSession &&
                onceReasonIds.Contains(reasonId))
            {
                error = "Attention reason was already applied: " + reasonId;
                return false;
            }
            if (committedEventKeys.Contains(stableEventKey))
            {
                error = "Attention event was already applied: " +
                    stableEventKey;
                return false;
            }

            int previous = value;
            value = Clamp(
                previous + reason.Delta,
                FormalAttentionCatalog.MinimumValue,
                FormalAttentionCatalog.MaximumValue);
            int appliedDelta = value - previous;
            unchecked { revision++; }

            committedEventKeys.Add(stableEventKey);
            if (reason.RepeatPolicy ==
                FormalAttentionRepeatPolicy.OncePerSession)
            {
                onceReasonIds.Add(reasonId);
            }
            FormalAttentionHistoryEntry entry =
                new FormalAttentionHistoryEntry(
                    reasonId,
                    stableEventKey,
                    reason.Delta,
                    appliedDelta,
                    value,
                    revision);
            if (history.Count == FormalAttentionCatalog.HistoryCapacity)
                history.Dequeue();
            history.Enqueue(entry);
            int newlyReachedMask = LockReachedThresholds(previous, value);
            RebuildSnapshot();
            PublishReachedThresholds(newlyReachedMask);
            error = string.Empty;
            return true;
        }

        public FormalAttentionSnapshot Capture()
        {
            return cachedSnapshot;
        }

        public bool TryRestore(
            FormalAttentionSnapshot snapshot,
            out string error)
        {
            if (!TryPrepareRestore(
                    snapshot,
                    out Queue<FormalAttentionHistoryEntry> nextHistory,
                    out HashSet<int> nextThresholds,
                    out HashSet<string> nextEvents,
                    out HashSet<string> nextOnceReasons,
                    out HashSet<string> nextOrphans,
                    out error))
            {
                return false;
            }

            value = snapshot.Value;
            revision = snapshot.Revision;
            Replace(history, nextHistory);
            Replace(reachedThresholds, nextThresholds);
            Replace(committedEventKeys, nextEvents);
            Replace(onceReasonIds, nextOnceReasons);
            Replace(orphanReasonIds, nextOrphans);
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private static bool TryPrepareRestore(
            FormalAttentionSnapshot snapshot,
            out Queue<FormalAttentionHistoryEntry> nextHistory,
            out HashSet<int> nextThresholds,
            out HashSet<string> nextEvents,
            out HashSet<string> nextOnceReasons,
            out HashSet<string> nextOrphans,
            out string error)
        {
            nextHistory = new Queue<FormalAttentionHistoryEntry>();
            nextThresholds = new HashSet<int>();
            nextEvents = new HashSet<string>(StringComparer.Ordinal);
            nextOnceReasons = new HashSet<string>(StringComparer.Ordinal);
            nextOrphans = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot == null)
            {
                error = "Attention snapshot is required.";
                return false;
            }
            if (snapshot.Value < FormalAttentionCatalog.MinimumValue ||
                snapshot.Value > FormalAttentionCatalog.MaximumValue ||
                snapshot.History.Count > FormalAttentionCatalog.HistoryCapacity)
            {
                error = "Attention snapshot values are outside formal bounds.";
                return false;
            }

            ulong previousRevision = 0;
            var historyEventKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.History.Count; index++)
            {
                FormalAttentionHistoryEntry entry = snapshot.History[index];
                if (!IsValidHistoryEntry(entry) ||
                    entry.Revision <= previousRevision ||
                    entry.Revision > snapshot.Revision ||
                    !historyEventKeys.Add(entry.StableEventKey))
                {
                    error = "Attention history is invalid or out of order.";
                    return false;
                }
                previousRevision = entry.Revision;
                nextHistory.Enqueue(entry);
                if (FormalAttentionCatalog.Find(entry.ReasonId) == null)
                    nextOrphans.Add(entry.ReasonId);
            }
            if (snapshot.History.Count > 0)
            {
                FormalAttentionHistoryEntry latest = null;
                foreach (FormalAttentionHistoryEntry entry in nextHistory)
                    latest = entry;
                if (latest == null || latest.Revision != snapshot.Revision ||
                    latest.ValueAfter != snapshot.Value)
                {
                    error = "Attention snapshot does not match latest history.";
                    return false;
                }
            }
            else if (snapshot.Revision != 0)
            {
                error = "Attention revision requires matching history.";
                return false;
            }

            if (!CopyThresholds(snapshot.ReachedThresholds, nextThresholds) ||
                !CopyStrings(snapshot.CommittedEventKeys, nextEvents) ||
                !CopyStrings(snapshot.OnceReasonIds, nextOnceReasons))
            {
                error = "Attention snapshot contains duplicate or invalid IDs.";
                return false;
            }
            foreach (string eventKey in historyEventKeys)
            {
                if (!nextEvents.Contains(eventKey))
                {
                    error = "Attention history event is not committed.";
                    return false;
                }
            }
            foreach (FormalAttentionHistoryEntry entry in nextHistory)
            {
                FormalAttentionReasonDefinition reason =
                    FormalAttentionCatalog.Find(entry.ReasonId);
                if (reason != null && reason.RepeatPolicy ==
                        FormalAttentionRepeatPolicy.OncePerSession &&
                    !nextOnceReasons.Contains(entry.ReasonId))
                {
                    error = "One-shot attention history is not locked.";
                    return false;
                }
            }
            foreach (string reasonId in nextOnceReasons)
            {
                if (FormalAttentionCatalog.Find(reasonId) == null)
                    nextOrphans.Add(reasonId);
            }
            error = string.Empty;
            return true;
        }

        private static bool IsValidHistoryEntry(
            FormalAttentionHistoryEntry entry)
        {
            return entry != null &&
                !string.IsNullOrWhiteSpace(entry.ReasonId) &&
                !string.IsNullOrWhiteSpace(entry.StableEventKey) &&
                entry.ValueAfter >= FormalAttentionCatalog.MinimumValue &&
                entry.ValueAfter <= FormalAttentionCatalog.MaximumValue &&
                entry.Revision > 0 &&
                !float.IsNaN(entry.RuleTimeSeconds) &&
                !float.IsInfinity(entry.RuleTimeSeconds) &&
                entry.RuleTimeSeconds >= 0f;
        }

        private static bool CopyThresholds(
            IReadOnlyList<int> source,
            HashSet<int> destination)
        {
            int previous = int.MinValue;
            for (var index = 0; index < source.Count; index++)
            {
                int threshold = source[index];
                if (threshold <= previous ||
                    !IsFormalThreshold(threshold) ||
                    !destination.Add(threshold))
                {
                    return false;
                }
                previous = threshold;
            }
            return true;
        }

        private static bool CopyStrings(
            IReadOnlyList<string> source,
            HashSet<string> destination)
        {
            for (var index = 0; index < source.Count; index++)
            {
                string value = source[index];
                if (string.IsNullOrWhiteSpace(value) ||
                    !destination.Add(value))
                {
                    return false;
                }
            }
            return true;
        }

        private int LockReachedThresholds(int previous, int current)
        {
            if (current <= previous)
                return 0;
            int mask = 0;
            IReadOnlyList<int> thresholds = FormalAttentionCatalog.Thresholds;
            for (var index = 0; index < thresholds.Count; index++)
            {
                int threshold = thresholds[index];
                if (previous < threshold && current >= threshold &&
                    reachedThresholds.Add(threshold))
                    mask |= 1 << index;
            }
            return mask;
        }

        private void PublishReachedThresholds(int mask)
        {
            if (mask == 0 || ThresholdReached == null) return;
            IReadOnlyList<int> thresholds = FormalAttentionCatalog.Thresholds;
            for (var thresholdIndex = 0;
                 thresholdIndex < thresholds.Count;
                 thresholdIndex++)
            {
                if ((mask & (1 << thresholdIndex)) == 0) continue;
                Delegate[] handlers = ThresholdReached.GetInvocationList();
                for (var index = 0; index < handlers.Length; index++)
                {
                    try
                    {
                        ((Action<int>)handlers[index])(
                            thresholds[thresholdIndex]);
                    }
                    catch (Exception exception)
                    {
                        LastThresholdNotificationFailure = exception;
                    }
                }
            }
        }

        private static bool IsFormalThreshold(int candidate)
        {
            IReadOnlyList<int> thresholds = FormalAttentionCatalog.Thresholds;
            for (var index = 0; index < thresholds.Count; index++)
            {
                if (thresholds[index] == candidate)
                    return true;
            }
            return false;
        }

        private void RebuildSnapshot()
        {
            var historyCopy = history.ToArray();
            int[] thresholdCopy = SortedIntegers(reachedThresholds);
            string[] eventCopy = SortedStrings(committedEventKeys);
            string[] onceCopy = SortedStrings(onceReasonIds);
            cachedSnapshot = new FormalAttentionSnapshot(
                value,
                revision,
                historyCopy,
                thresholdCopy,
                eventCopy,
                onceCopy);
        }

        private static int Clamp(int candidate, int minimum, int maximum)
        {
            return candidate < minimum
                ? minimum
                : candidate > maximum
                    ? maximum
                    : candidate;
        }

        private static int[] SortedIntegers(IEnumerable<int> source)
        {
            var result = new List<int>(source);
            result.Sort();
            return result.ToArray();
        }

        private static string[] SortedStrings(IEnumerable<string> source)
        {
            var result = new List<string>(source);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static void Replace<T>(Queue<T> target, Queue<T> source)
        {
            target.Clear();
            foreach (T value in source)
                target.Enqueue(value);
        }

        private static void Replace<T>(HashSet<T> target, HashSet<T> source)
        {
            target.Clear();
            foreach (T value in source)
                target.Add(value);
        }
    }
}
