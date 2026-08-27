using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Persistence;

namespace WasteCity.Progression
{
    public sealed class FormalRewindAnchorMetadata
    {
        private readonly ReadOnlyCollection<string> completedMilestoneIds;

        public FormalRewindAnchorMetadata(
            string anchorId,
            string internalKey,
            string sessionId,
            string payloadHashSha256,
            FormalSaveCheckpointMetadata checkpoint,
            long creationOrdinal)
        {
            AnchorId = Require(anchorId, nameof(anchorId));
            InternalKey = Require(internalKey, nameof(internalKey));
            SessionId = Require(sessionId, nameof(sessionId));
            PayloadHashSha256 = Require(
                payloadHashSha256,
                nameof(payloadHashSha256));
            if (!IsValidCheckpoint(checkpoint) || creationOrdinal <= 0)
            {
                throw new ArgumentException(
                    "回溯锚点检查点或创建序号无效");
            }
            CheckpointSequence = checkpoint.sequence;
            CheckpointReasonId = checkpoint.reasonId;
            CheckpointRuleTimeSeconds = checkpoint.ruleTimeSeconds;
            completedMilestoneIds = Array.AsReadOnly(
                CopyAndSort(checkpoint.completedMilestoneIds));
            CreationOrdinal = creationOrdinal;
        }

        public string AnchorId { get; }
        public string InternalKey { get; }
        public string SessionId { get; }
        public string PayloadHashSha256 { get; }
        public long CheckpointSequence { get; }
        public string CheckpointReasonId { get; }
        public float CheckpointRuleTimeSeconds { get; }
        public IReadOnlyList<string> CompletedMilestoneIds =>
            completedMilestoneIds;
        public long CreationOrdinal { get; }

        internal FormalRewindAnchorMetadata Copy()
        {
            return new FormalRewindAnchorMetadata(
                AnchorId,
                InternalKey,
                SessionId,
                PayloadHashSha256,
                new FormalSaveCheckpointMetadata
                {
                    sequence = CheckpointSequence,
                    reasonId = CheckpointReasonId,
                    ruleTimeSeconds = CheckpointRuleTimeSeconds,
                    completedMilestoneIds = ToArray(completedMilestoneIds),
                },
                CreationOrdinal);
        }

        internal static bool IsValid(FormalRewindAnchorMetadata value)
        {
            return value != null &&
                !string.IsNullOrWhiteSpace(value.AnchorId) &&
                !string.IsNullOrWhiteSpace(value.InternalKey) &&
                !string.IsNullOrWhiteSpace(value.SessionId) &&
                !string.IsNullOrWhiteSpace(value.PayloadHashSha256) &&
                value.CheckpointSequence >= 0 &&
                !string.IsNullOrWhiteSpace(value.CheckpointReasonId) &&
                IsFiniteNonNegative(value.CheckpointRuleTimeSeconds) &&
                value.CompletedMilestoneIds != null &&
                value.CreationOrdinal > 0 &&
                IsStrictlyOrdered(value.CompletedMilestoneIds);
        }

        private static string Require(string value, string parameter)
        {
            return !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException(
                    "回溯锚点稳定文本不能为空",
                    parameter);
        }

        private static bool IsValidCheckpoint(
            FormalSaveCheckpointMetadata checkpoint)
        {
            return checkpoint != null && checkpoint.sequence >= 0 &&
                !string.IsNullOrWhiteSpace(checkpoint.reasonId) &&
                IsFiniteNonNegative(checkpoint.ruleTimeSeconds) &&
                checkpoint.completedMilestoneIds != null;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= 0f;
        }

        private static bool IsStrictlyOrdered(IReadOnlyList<string> source)
        {
            string previous = null;
            for (var index = 0; index < source.Count; index++)
            {
                string value = source[index];
                if (string.IsNullOrWhiteSpace(value) ||
                    previous != null &&
                    string.CompareOrdinal(previous, value) >= 0)
                {
                    return false;
                }
                previous = value;
            }
            return true;
        }

        private static string[] CopyAndSort(string[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<string>();
            var copy = (string[])source.Clone();
            Array.Sort(copy, StringComparer.Ordinal);
            for (var index = 0; index < copy.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(copy[index]) ||
                    index > 0 && string.Equals(
                        copy[index - 1],
                        copy[index],
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "回溯锚点检查点里程碑无效或重复");
                }
            }
            return copy;
        }

        private static string[] ToArray(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = source[index];
            return result;
        }
    }

    public sealed class FormalRewindAnchorMetadataSnapshot
    {
        private readonly ReadOnlyCollection<FormalRewindAnchorMetadata> entries;

        public FormalRewindAnchorMetadataSnapshot(
            ulong revision,
            long nextCreationOrdinal,
            FormalRewindAnchorMetadata[] entries)
        {
            Revision = revision;
            NextCreationOrdinal = nextCreationOrdinal;
            this.entries = Array.AsReadOnly(entries == null
                ? Array.Empty<FormalRewindAnchorMetadata>()
                : (FormalRewindAnchorMetadata[])entries.Clone());
        }

        public ulong Revision { get; }
        public long NextCreationOrdinal { get; }
        public IReadOnlyList<FormalRewindAnchorMetadata> Entries => entries;
    }

    public sealed class FormalRewindAnchorMetadataUpsertPlan
    {
        internal FormalRewindAnchorMetadataUpsertPlan(
            FormalRewindAnchorMetadataRuntime owner,
            FormalRewindAnchorMetadataSnapshot expected,
            FormalRewindAnchorMetadata candidate)
        {
            Owner = owner;
            Expected = expected;
            Candidate = candidate;
        }

        internal FormalRewindAnchorMetadataRuntime Owner { get; }
        internal FormalRewindAnchorMetadataSnapshot Expected { get; }
        internal FormalRewindAnchorMetadata Candidate { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class FormalRewindAnchorMetadataClearPlan
    {
        internal FormalRewindAnchorMetadataClearPlan(
            FormalRewindAnchorMetadataRuntime owner,
            FormalRewindAnchorMetadataSnapshot expected,
            bool hadEntry)
        {
            Owner = owner;
            Expected = expected;
            HadEntry = hadEntry;
        }

        internal FormalRewindAnchorMetadataRuntime Owner { get; }
        internal FormalRewindAnchorMetadataSnapshot Expected { get; }
        internal bool HadEntry { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class FormalRewindAnchorMetadataRuntime
    {
        public const int MaximumAnchorsAtLevelOne = 1;
        public const int MaximumAnchorsAtLevelTwo = 2;

        private readonly List<FormalRewindAnchorMetadata> entries =
            new List<FormalRewindAnchorMetadata>(MaximumAnchorsAtLevelTwo);
        private int maximumAnchors;
        private ulong revision;
        private long nextCreationOrdinal = 1;
        private FormalRewindAnchorMetadataSnapshot cachedSnapshot;

        public FormalRewindAnchorMetadataRuntime()
            : this(1)
        {
        }

        public FormalRewindAnchorMetadataRuntime(int fateLevel)
        {
            if (fateLevel != 1 && fateLevel != 2)
                throw new ArgumentOutOfRangeException(nameof(fateLevel));
            maximumAnchors = fateLevel == 2
                ? MaximumAnchorsAtLevelTwo
                : MaximumAnchorsAtLevelOne;
            RebuildSnapshot();
        }

        public ulong Revision => revision;
        public int MaximumAnchors => maximumAnchors;

        public bool TrySetFateLevel(int fateLevel, out string error)
        {
            if (fateLevel != 1 && fateLevel != 2)
            {
                error = "回溯锚点仅支持命轨等级一或二";
                return false;
            }
            int next = fateLevel == 2
                ? MaximumAnchorsAtLevelTwo
                : MaximumAnchorsAtLevelOne;
            if (entries.Count > next)
            {
                error = "当前锚点数量超过目标命轨等级容量";
                return false;
            }
            maximumAnchors = next;
            error = string.Empty;
            return true;
        }

        public FormalRewindAnchorMetadataSnapshot Capture() => cachedSnapshot;

        public bool TryPrepareUpsert(
            string anchorId,
            string internalKey,
            string sessionId,
            string payloadHashSha256,
            FormalSaveCheckpointMetadata checkpoint,
            out FormalRewindAnchorMetadataUpsertPlan plan,
            out string error)
        {
            plan = null;
            try
            {
                var candidate = new FormalRewindAnchorMetadata(
                    anchorId,
                    internalKey,
                    sessionId,
                    payloadHashSha256,
                    checkpoint,
                    nextCreationOrdinal);
                plan = new FormalRewindAnchorMetadataUpsertPlan(
                    this,
                    cachedSnapshot,
                    candidate);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryCommitUpsert(
            FormalRewindAnchorMetadataUpsertPlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "回溯锚点元数据计划不属于当前运行时";
                return false;
            }
            if (plan.Consumed)
            {
                error = "回溯锚点元数据计划已提交";
                return false;
            }
            if (!ReferenceEquals(plan.Expected, cachedSnapshot))
            {
                error = "回溯锚点元数据计划已过期";
                return false;
            }

            int replaceIndex = -1;
            for (var index = 0; index < entries.Count; index++)
                if (string.Equals(entries[index].AnchorId,
                        plan.Candidate.AnchorId, StringComparison.Ordinal))
                    replaceIndex = index;
            if (replaceIndex >= 0) entries.RemoveAt(replaceIndex);
            else if (entries.Count >= maximumAnchors) entries.RemoveAt(0);
            FormalRewindAnchorMetadata committed = plan.Candidate.Copy();
            entries.Add(committed);
            entries.Sort((left, right) =>
                left.CreationOrdinal.CompareTo(right.CreationOrdinal));
            nextCreationOrdinal = committed.CreationOrdinal + 1;
            unchecked { revision++; }
            plan.Consumed = true;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryPrepareClear(
            out FormalRewindAnchorMetadataClearPlan plan,
            out string error)
        {
            plan = new FormalRewindAnchorMetadataClearPlan(
                this,
                cachedSnapshot,
                entries.Count != 0);
            error = string.Empty;
            return true;
        }

        public bool TryCommitClear(
            FormalRewindAnchorMetadataClearPlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "回溯锚点清理计划不属于当前运行时";
                return false;
            }
            if (plan.Consumed)
            {
                error = "回溯锚点清理计划已提交";
                return false;
            }
            if (!ReferenceEquals(plan.Expected, cachedSnapshot))
            {
                error = "回溯锚点清理计划已过期";
                return false;
            }

            plan.Consumed = true;
            if (!plan.HadEntry)
            {
                error = string.Empty;
                return true;
            }
            entries.Clear();
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryRestore(
            FormalRewindAnchorMetadataSnapshot snapshot,
            out string error)
        {
            if (snapshot == null || snapshot.Entries == null ||
                snapshot.Entries.Count > maximumAnchors ||
                snapshot.NextCreationOrdinal <= 0)
            {
                error = "回溯锚点元数据快照无效或超过一槽上限";
                return false;
            }

            var candidates = new List<FormalRewindAnchorMetadata>(
                snapshot.Entries.Count);
            long previousOrdinal = 0;
            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                FormalRewindAnchorMetadata source = snapshot.Entries[index];
                if (!FormalRewindAnchorMetadata.IsValid(source) ||
                    source.CreationOrdinal <= previousOrdinal ||
                    source.CreationOrdinal >= snapshot.NextCreationOrdinal ||
                    !anchorIds.Add(source.AnchorId))
                {
                    error = "回溯锚点元数据记录无效";
                    return false;
                }
                candidates.Add(source.Copy());
                previousOrdinal = source.CreationOrdinal;
            }

            entries.Clear();
            entries.AddRange(candidates);
            revision = snapshot.Revision;
            nextCreationOrdinal = snapshot.NextCreationOrdinal;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new FormalRewindAnchorMetadataSnapshot(
                revision,
                nextCreationOrdinal,
                CopyEntries());
        }

        private FormalRewindAnchorMetadata[] CopyEntries()
        {
            var result = new FormalRewindAnchorMetadata[entries.Count];
            for (var index = 0; index < result.Length; index++)
                result[index] = entries[index].Copy();
            return result;
        }
    }
}
