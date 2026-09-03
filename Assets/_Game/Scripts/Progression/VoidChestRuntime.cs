using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace WasteCity.Progression
{
    public sealed class VoidChestEvaluation
    {
        public VoidChestEvaluation(
            string deathId,
            ulong sequenceOrdinal,
            bool dropped,
            string chestId,
            bool claimed)
            : this(
                deathId,
                sequenceOrdinal,
                dropped,
                chestId,
                claimed,
                string.Empty,
                0,
                string.Empty)
        {
        }

        public VoidChestEvaluation(
            string deathId,
            ulong sequenceOrdinal,
            bool dropped,
            string chestId,
            bool claimed,
            string resourceId,
            int amount,
            string narrativeFragmentId)
        {
            DeathId = deathId ?? string.Empty;
            SequenceOrdinal = sequenceOrdinal;
            Dropped = dropped;
            ChestId = chestId ?? string.Empty;
            Claimed = claimed;
            ResourceId = resourceId ?? string.Empty;
            Amount = amount;
            NarrativeFragmentId = narrativeFragmentId ?? string.Empty;
        }

        public string DeathId { get; }
        public ulong SequenceOrdinal { get; }
        public bool Dropped { get; }
        public string ChestId { get; }
        public bool Claimed { get; }
        public string ResourceId { get; }
        public int Amount { get; }
        public string NarrativeFragmentId { get; }

        internal string StableEvaluationKey =>
            DeathId + "\n" + SequenceOrdinal.ToString(
                CultureInfo.InvariantCulture);

        internal VoidChestEvaluation WithClaimed()
        {
            return new VoidChestEvaluation(
                DeathId,
                SequenceOrdinal,
                Dropped,
                ChestId,
                true,
                ResourceId,
                Amount,
                NarrativeFragmentId);
        }
    }

    public sealed class VoidChestSnapshot
    {
        private readonly ReadOnlyCollection<VoidChestEvaluation> evaluations;
        private readonly ReadOnlyCollection<string> unclaimedChestIds;
        private readonly ReadOnlyCollection<string> claimedChestIds;

        public VoidChestSnapshot(
            ulong revision,
            VoidChestEvaluation[] evaluations,
            string[] unclaimedChestIds,
            string[] claimedChestIds)
        {
            Revision = revision;
            evaluations = evaluations ?? Array.Empty<VoidChestEvaluation>();
            unclaimedChestIds = unclaimedChestIds ?? Array.Empty<string>();
            claimedChestIds = claimedChestIds ?? Array.Empty<string>();
            this.evaluations = Array.AsReadOnly(
                (VoidChestEvaluation[])evaluations.Clone());
            this.unclaimedChestIds = Array.AsReadOnly(
                (string[])unclaimedChestIds.Clone());
            this.claimedChestIds = Array.AsReadOnly(
                (string[])claimedChestIds.Clone());
        }

        public ulong Revision { get; }
        public IReadOnlyList<VoidChestEvaluation> Evaluations => evaluations;
        public IReadOnlyList<string> UnclaimedChestIds => unclaimedChestIds;
        public IReadOnlyList<string> ClaimedChestIds => claimedChestIds;
    }

    public sealed class VoidChestRuntime
    {
        public const string DefaultSessionId = "compat.default-session";
        public const int DefaultSelectionVersion = 1;

        private static readonly string[] RewardResourceIds =
        {
            "core.resource.iron",
            "core.resource.stone",
            "core.resource.biomass",
            "core.resource.water",
        };

        private static readonly string[] NarrativeFragmentIds =
        {
            "core.narrative.void-chest.ash-01",
            "core.narrative.void-chest.ash-02",
            "core.narrative.void-chest.ash-03",
            "core.narrative.void-chest.ash-04",
        };

        private readonly Dictionary<string, VoidChestEvaluation> byEvaluation =
            new Dictionary<string, VoidChestEvaluation>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> evaluationKeyByChestId =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly string sessionId;
        private readonly int selectionVersion;
        private ulong revision;
        private VoidChestSnapshot cachedSnapshot;

        public VoidChestRuntime()
            : this(DefaultSessionId, DefaultSelectionVersion)
        {
        }

        public VoidChestRuntime(string sessionId, int selectionVersion)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException(
                    "A non-blank session ID is required.",
                    nameof(sessionId));
            if (selectionVersion <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(selectionVersion),
                    "Selection version must be positive.");
            this.sessionId = sessionId;
            this.selectionVersion = selectionVersion;
            RebuildSnapshot();
        }

        public string SessionId => sessionId;
        public int SelectionVersion => selectionVersion;
        public ulong Revision => revision;

        public static bool ShouldDrop(
            string stableDeathId,
            ulong sequenceOrdinal)
        {
            return ShouldDrop(
                DefaultSessionId,
                DefaultSelectionVersion,
                stableDeathId,
                sequenceOrdinal);
        }

        public static bool ShouldDrop(
            string sessionId,
            int selectionVersion,
            string stableDeathId,
            ulong sequenceOrdinal)
        {
            if (string.IsNullOrWhiteSpace(sessionId) ||
                selectionVersion <= 0 ||
                string.IsNullOrWhiteSpace(stableDeathId) ||
                sequenceOrdinal == 0)
                return false;
            uint offset = StableHash(StableContextKey(
                sessionId,
                selectionVersion,
                stableDeathId)) % 100u;
            ulong windowPosition = (sequenceOrdinal - 1ul) % 100ul;
            return (offset + windowPosition) % 100ul == 0ul;
        }

        public bool TryEvaluateDeath(
            string stableDeathId,
            ulong sequenceOrdinal,
            out VoidChestEvaluation evaluation,
            out string error)
        {
            evaluation = null;
            if (string.IsNullOrWhiteSpace(stableDeathId) ||
                sequenceOrdinal == 0)
            {
                error = "Stable death ID and positive sequence are required.";
                return false;
            }

            string key = EvaluationKey(stableDeathId, sequenceOrdinal);
            if (byEvaluation.TryGetValue(key, out evaluation))
            {
                error = "Death was already evaluated.";
                return false;
            }
            bool dropped = ShouldDrop(
                sessionId,
                selectionVersion,
                stableDeathId,
                sequenceOrdinal);
            string chestId = dropped
                ? BuildChestId(stableDeathId, sequenceOrdinal)
                : string.Empty;
            string resourceId = string.Empty;
            int amount = 0;
            string narrativeFragmentId = string.Empty;
            if (dropped)
            {
                uint rewardHash = RewardHash(stableDeathId, sequenceOrdinal);
                resourceId = RewardResourceIds[
                    (int)(rewardHash % (uint)RewardResourceIds.Length)];
                amount = 3 + (int)((rewardHash >> 8) % 6u);
                narrativeFragmentId = NarrativeFragmentIds[
                    (int)((rewardHash >> 16) %
                        (uint)NarrativeFragmentIds.Length)];
            }
            evaluation = new VoidChestEvaluation(
                stableDeathId,
                sequenceOrdinal,
                dropped,
                chestId,
                false,
                resourceId,
                amount,
                narrativeFragmentId);
            byEvaluation.Add(key, evaluation);
            if (dropped) evaluationKeyByChestId.Add(chestId, key);
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryClaim(string chestId, out string error)
        {
            if (string.IsNullOrWhiteSpace(chestId) ||
                !evaluationKeyByChestId.TryGetValue(
                    chestId,
                    out string evaluationKey) ||
                !byEvaluation.TryGetValue(
                    evaluationKey,
                    out VoidChestEvaluation evaluation) ||
                evaluation.Claimed)
            {
                error = "Chest is missing or already claimed.";
                return false;
            }
            byEvaluation[evaluationKey] = evaluation.WithClaimed();
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public VoidChestSnapshot Capture() => cachedSnapshot;

        public bool TryRestore(VoidChestSnapshot snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Void chest snapshot is required.";
                return false;
            }

            var restoredByEvaluation =
                new Dictionary<string, VoidChestEvaluation>(
                    StringComparer.Ordinal);
            var restoredByChest = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var expectedUnclaimed = new HashSet<string>(StringComparer.Ordinal);
            var expectedClaimed = new HashSet<string>(StringComparer.Ordinal);
            ulong expectedRevision = 0;
            for (var index = 0; index < snapshot.Evaluations.Count; index++)
            {
                VoidChestEvaluation evaluation = snapshot.Evaluations[index];
                if (!TryValidateEvaluation(evaluation, out string key))
                {
                    error = "Void chest evaluation is inconsistent.";
                    return false;
                }
                if (!restoredByEvaluation.TryAdd(key, evaluation))
                {
                    error = "Void chest evaluations must be unique.";
                    return false;
                }
                expectedRevision++;
                if (!evaluation.Dropped) continue;
                if (!restoredByChest.TryAdd(evaluation.ChestId, key))
                {
                    error = "Void chest IDs must be unique.";
                    return false;
                }
                if (evaluation.Claimed)
                {
                    expectedClaimed.Add(evaluation.ChestId);
                    expectedRevision++;
                }
                else
                {
                    expectedUnclaimed.Add(evaluation.ChestId);
                }
            }

            if (snapshot.Revision != expectedRevision ||
                !MatchesUniqueSet(
                    snapshot.UnclaimedChestIds,
                    expectedUnclaimed) ||
                !MatchesUniqueSet(
                    snapshot.ClaimedChestIds,
                    expectedClaimed))
            {
                error = "Void chest revision or claim indexes are inconsistent.";
                return false;
            }

            byEvaluation.Clear();
            evaluationKeyByChestId.Clear();
            foreach (KeyValuePair<string, VoidChestEvaluation> pair in
                     restoredByEvaluation)
            {
                VoidChestEvaluation value = pair.Value;
                byEvaluation.Add(pair.Key, new VoidChestEvaluation(
                    value.DeathId,
                    value.SequenceOrdinal,
                    value.Dropped,
                    value.ChestId,
                    value.Claimed,
                    value.ResourceId,
                    value.Amount,
                    value.NarrativeFragmentId));
            }
            foreach (KeyValuePair<string, string> pair in restoredByChest)
                evaluationKeyByChestId.Add(pair.Key, pair.Value);
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private void RebuildSnapshot()
        {
            var evaluations = new List<VoidChestEvaluation>(
                byEvaluation.Values);
            evaluations.Sort((left, right) => string.CompareOrdinal(
                left.StableEvaluationKey,
                right.StableEvaluationKey));
            var unclaimed = new List<string>();
            var claimed = new List<string>();
            for (var index = 0; index < evaluations.Count; index++)
            {
                VoidChestEvaluation evaluation = evaluations[index];
                if (!evaluation.Dropped) continue;
                (evaluation.Claimed ? claimed : unclaimed).Add(
                    evaluation.ChestId);
            }
            unclaimed.Sort(StringComparer.Ordinal);
            claimed.Sort(StringComparer.Ordinal);
            cachedSnapshot = new VoidChestSnapshot(
                revision,
                evaluations.ToArray(),
                unclaimed.ToArray(),
                claimed.ToArray());
        }

        private static string EvaluationKey(
            string stableDeathId,
            ulong sequenceOrdinal)
        {
            return stableDeathId + "\n" + sequenceOrdinal.ToString(
                CultureInfo.InvariantCulture);
        }

        private string BuildChestId(
            string stableDeathId,
            ulong sequenceOrdinal)
        {
            return "void-chest:" + StableHash(StableContextKey(
                sessionId,
                selectionVersion,
                stableDeathId)).ToString(
                "x8",
                CultureInfo.InvariantCulture) + ":" +
                sequenceOrdinal.ToString("D6", CultureInfo.InvariantCulture);
        }

        private uint RewardHash(
            string stableDeathId,
            ulong sequenceOrdinal)
        {
            return StableHash(
                StableContextKey(
                    sessionId,
                    selectionVersion,
                    stableDeathId) + "\nreward\n" +
                sequenceOrdinal.ToString(CultureInfo.InvariantCulture));
        }

        private bool TryValidateEvaluation(
            VoidChestEvaluation evaluation,
            out string key)
        {
            key = string.Empty;
            if (evaluation == null ||
                string.IsNullOrWhiteSpace(evaluation.DeathId) ||
                evaluation.SequenceOrdinal == 0)
                return false;

            bool expectedDrop = ShouldDrop(
                sessionId,
                selectionVersion,
                evaluation.DeathId,
                evaluation.SequenceOrdinal);
            if (evaluation.Dropped != expectedDrop ||
                evaluation.Claimed && !expectedDrop)
                return false;

            if (!expectedDrop)
            {
                if (!string.IsNullOrEmpty(evaluation.ChestId) ||
                    !string.IsNullOrEmpty(evaluation.ResourceId) ||
                    evaluation.Amount != 0 ||
                    !string.IsNullOrEmpty(evaluation.NarrativeFragmentId))
                    return false;
            }
            else
            {
                uint rewardHash = RewardHash(
                    evaluation.DeathId,
                    evaluation.SequenceOrdinal);
                string expectedResource = RewardResourceIds[
                    (int)(rewardHash % (uint)RewardResourceIds.Length)];
                int expectedAmount = 3 + (int)((rewardHash >> 8) % 6u);
                string expectedNarrative = NarrativeFragmentIds[
                    (int)((rewardHash >> 16) %
                        (uint)NarrativeFragmentIds.Length)];
                if (!string.Equals(
                        evaluation.ChestId,
                        BuildChestId(
                            evaluation.DeathId,
                            evaluation.SequenceOrdinal),
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        evaluation.ResourceId,
                        expectedResource,
                        StringComparison.Ordinal) ||
                    evaluation.Amount != expectedAmount ||
                    !string.Equals(
                        evaluation.NarrativeFragmentId,
                        expectedNarrative,
                        StringComparison.Ordinal))
                    return false;
            }

            key = EvaluationKey(
                evaluation.DeathId,
                evaluation.SequenceOrdinal);
            return true;
        }

        private static bool MatchesUniqueSet(
            IReadOnlyList<string> actual,
            HashSet<string> expected)
        {
            if (actual == null || actual.Count != expected.Count) return false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < actual.Count; index++)
            {
                string value = actual[index];
                if (string.IsNullOrWhiteSpace(value) ||
                    !expected.Contains(value) ||
                    !seen.Add(value))
                    return false;
            }
            return true;
        }

        private static string StableContextKey(
            string sessionId,
            int selectionVersion,
            string stableDeathId)
        {
            return sessionId + "\n" + selectionVersion.ToString(
                CultureInfo.InvariantCulture) + "\n" + stableDeathId;
        }

        private static uint StableHash(string value)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash ^= (byte)(character & 0xff);
                hash *= prime;
                hash ^= (byte)(character >> 8);
                hash *= prime;
            }
            return hash;
        }
    }
}
