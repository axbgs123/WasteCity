using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Economy;

namespace WasteCity.Progression
{
    public sealed class PocketUniverseBuildingCandidate
    {
        public PocketUniverseBuildingCandidate(
            string stableInstanceId,
            string buildingDefinitionId,
            bool isCompleted,
            bool isPlayerOwned)
        {
            StableInstanceId = stableInstanceId;
            BuildingDefinitionId = buildingDefinitionId;
            IsCompleted = isCompleted;
            IsPlayerOwned = isPlayerOwned;
        }

        public string StableInstanceId { get; }
        public string BuildingDefinitionId { get; }
        public bool IsCompleted { get; }
        public bool IsPlayerOwned { get; }
    }

    public sealed class PocketUniverseFlagshipState
    {
        public PocketUniverseFlagshipState(
            string buildingDefinitionId,
            string stableInstanceId)
        {
            BuildingDefinitionId = buildingDefinitionId;
            StableInstanceId = stableInstanceId;
        }

        public string BuildingDefinitionId { get; }
        public string StableInstanceId { get; }
    }

    public sealed class PocketUniverseCollapseCommand
    {
        internal PocketUniverseCollapseCommand(
            string stableInstanceId,
            int centerX,
            int centerY,
            int size)
        {
            StableInstanceId = stableInstanceId;
            CenterX = centerX;
            CenterY = centerY;
            Size = size;
            StableCommandId = "pocket-universe-collapse:" +
                stableInstanceId;
        }

        public string StableInstanceId { get; }
        public string StableCommandId { get; }
        public int CenterX { get; }
        public int CenterY { get; }
        public int Size { get; }
    }

    public sealed class PocketUniverseFateSnapshot
    {
        private readonly ReadOnlyCollection<PocketUniverseFlagshipState>
            flagships;
        private readonly ReadOnlyCollection<string> collapsedFlagshipIds;

        public PocketUniverseFateSnapshot(
            int level,
            ulong revision,
            PocketUniverseFlagshipState[] flagships,
            string[] collapsedFlagshipIds)
            : this(
                level,
                revision,
                flagships,
                collapsedFlagshipIds,
                null)
        {
        }

        public PocketUniverseFateSnapshot(
            int level,
            ulong revision,
            PocketUniverseFlagshipState[] flagships,
            string[] collapsedFlagshipIds,
            string firstProductionFlagshipId)
        {
            Level = level;
            Revision = revision;
            this.flagships = Array.AsReadOnly(flagships == null
                ? Array.Empty<PocketUniverseFlagshipState>()
                : (PocketUniverseFlagshipState[])flagships.Clone());
            this.collapsedFlagshipIds = Array.AsReadOnly(
                collapsedFlagshipIds == null
                    ? Array.Empty<string>()
                    : (string[])collapsedFlagshipIds.Clone());
            FirstProductionFlagshipId =
                firstProductionFlagshipId ?? string.Empty;
        }

        public int Level { get; }
        public ulong Revision { get; }
        public IReadOnlyList<PocketUniverseFlagshipState> Flagships =>
            flagships;
        public IReadOnlyList<string> CollapsedFlagshipIds =>
            collapsedFlagshipIds;
        public string FirstProductionFlagshipId { get; }
    }

    public sealed class PocketUniverseFateEffect :
        IFormalProductionOutputModifier
    {
        private static readonly ReadOnlyCollection<string>
            eligibleBuildingDefinitionIds = BuildEligibleDefinitionIds();

        private readonly SortedDictionary<string, string>
            flagshipByDefinition = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
        private readonly HashSet<string> collapsedFlagshipIds =
            new HashSet<string>(StringComparer.Ordinal);
        private int level = 1;
        private string firstProductionFlagshipId;
        private ulong revision;
        private PocketUniverseFateSnapshot cachedSnapshot;

        public PocketUniverseFateEffect()
        {
            RebuildSnapshot();
        }

        public IReadOnlyList<string> EligibleBuildingDefinitionIds =>
            eligibleBuildingDefinitionIds;
        public int Level => level;
        public ulong Revision => revision;

        public int SelectFlagships(
            PocketUniverseBuildingCandidate[] candidates)
        {
            if (candidates == null || candidates.Length == 0)
                return 0;

            var bestByDefinition = new Dictionary<string,
                PocketUniverseBuildingCandidate>(StringComparer.Ordinal);
            for (var index = 0; index < candidates.Length; index++)
            {
                PocketUniverseBuildingCandidate candidate = candidates[index];
                if (!IsEligibleCandidate(candidate) ||
                    flagshipByDefinition.ContainsKey(
                        candidate.BuildingDefinitionId))
                {
                    continue;
                }
                if (!bestByDefinition.TryGetValue(
                        candidate.BuildingDefinitionId,
                        out PocketUniverseBuildingCandidate best) ||
                    string.CompareOrdinal(
                        candidate.StableInstanceId,
                        best.StableInstanceId) < 0)
                {
                    bestByDefinition[candidate.BuildingDefinitionId] =
                        candidate;
                }
            }

            var usedStableIds = new HashSet<string>(
                flagshipByDefinition.Values,
                StringComparer.Ordinal);
            var selectedCount = 0;
            for (var index = 0;
                 index < eligibleBuildingDefinitionIds.Count;
                 index++)
            {
                string definitionId = eligibleBuildingDefinitionIds[index];
                if (flagshipByDefinition.ContainsKey(definitionId) ||
                    !bestByDefinition.TryGetValue(
                        definitionId,
                        out PocketUniverseBuildingCandidate candidate) ||
                    !usedStableIds.Add(candidate.StableInstanceId))
                {
                    continue;
                }
                flagshipByDefinition.Add(
                    definitionId,
                    candidate.StableInstanceId);
                selectedCount++;
            }

            if (selectedCount > 0)
            {
                unchecked { revision++; }
                RebuildSnapshot();
            }
            return selectedCount;
        }

        public int OutputMultiplier(string stableInstanceId)
        {
            return IsFlagship(stableInstanceId)
                ? level == 2 ? 4 : 2
                : 1;
        }

        public bool TrySetLevel(int nextLevel, out string error)
        {
            if (nextLevel < 1 || nextLevel > 2)
            {
                error = "袖珍宇宙当前只支持命轨等级一或二";
                return false;
            }
            if (level == nextLevel)
            {
                error = string.Empty;
                return true;
            }
            level = nextLevel;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryCreateCollapseCommand(
            string stableInstanceId,
            int centerX,
            int centerY,
            out PocketUniverseCollapseCommand command)
        {
            command = null;
            if (!IsFlagship(stableInstanceId) ||
                !collapsedFlagshipIds.Add(stableInstanceId))
            {
                return false;
            }
            command = new PocketUniverseCollapseCommand(
                stableInstanceId,
                centerX,
                centerY,
                level == 2 ? 4 : 3);
            unchecked { revision++; }
            RebuildSnapshot();
            return true;
        }

        public bool TryCommitFirstProduction(
            string stableInstanceId,
            out string stableEventKey)
        {
            stableEventKey = string.Empty;
            if (!string.IsNullOrEmpty(firstProductionFlagshipId) ||
                !IsFlagship(stableInstanceId))
            {
                return false;
            }
            firstProductionFlagshipId = stableInstanceId;
            stableEventKey = "pocket-universe-first-production:" +
                stableInstanceId;
            unchecked { revision++; }
            RebuildSnapshot();
            return true;
        }

        public PocketUniverseFateSnapshot Capture()
        {
            return cachedSnapshot;
        }

        public bool TryRestore(
            PocketUniverseFateSnapshot snapshot,
            out string error)
        {
            if (!TryPrepareRestore(
                    snapshot,
                    out SortedDictionary<string, string> nextFlagships,
                    out HashSet<string> nextCollapsed,
                    out string nextFirstProduction,
                    out error))
            {
                return false;
            }

            level = snapshot.Level;
            revision = snapshot.Revision;
            flagshipByDefinition.Clear();
            foreach (KeyValuePair<string, string> item in nextFlagships)
                flagshipByDefinition.Add(item.Key, item.Value);
            collapsedFlagshipIds.Clear();
            foreach (string stableId in nextCollapsed)
                collapsedFlagshipIds.Add(stableId);
            firstProductionFlagshipId = nextFirstProduction;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private static bool TryPrepareRestore(
            PocketUniverseFateSnapshot snapshot,
            out SortedDictionary<string, string> flagships,
            out HashSet<string> collapsed,
            out string firstProduction,
            out string error)
        {
            flagships = new SortedDictionary<string, string>(
                StringComparer.Ordinal);
            collapsed = new HashSet<string>(StringComparer.Ordinal);
            firstProduction = null;
            if (snapshot == null || snapshot.Level < 1 || snapshot.Level > 2)
            {
                error = "袖珍宇宙快照为空或等级无效";
                return false;
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            string previousDefinitionId = null;
            for (var index = 0; index < snapshot.Flagships.Count; index++)
            {
                PocketUniverseFlagshipState flagship =
                    snapshot.Flagships[index];
                if (flagship == null ||
                    string.IsNullOrWhiteSpace(flagship.BuildingDefinitionId) ||
                    string.IsNullOrWhiteSpace(flagship.StableInstanceId) ||
                    !IsEligibleDefinition(flagship.BuildingDefinitionId) ||
                    (previousDefinitionId != null && string.CompareOrdinal(
                        previousDefinitionId,
                        flagship.BuildingDefinitionId) >= 0) ||
                    !stableIds.Add(flagship.StableInstanceId))
                {
                    error = "袖珍宇宙旗舰记录为空、重复、乱序或不属于正式生产目录";
                    return false;
                }
                flagships.Add(
                    flagship.BuildingDefinitionId,
                    flagship.StableInstanceId);
                previousDefinitionId = flagship.BuildingDefinitionId;
            }

            string previousCollapsed = null;
            for (var index = 0;
                 index < snapshot.CollapsedFlagshipIds.Count;
                 index++)
            {
                string stableId = snapshot.CollapsedFlagshipIds[index];
                if (string.IsNullOrWhiteSpace(stableId) ||
                    !stableIds.Contains(stableId) ||
                    (previousCollapsed != null && string.CompareOrdinal(
                        previousCollapsed,
                        stableId) >= 0) ||
                    !collapsed.Add(stableId))
                {
                    error = "袖珍宇宙坍缩记录必须是唯一且有序的既有旗舰";
                    return false;
                }
                previousCollapsed = stableId;
            }
            if (!string.IsNullOrEmpty(snapshot.FirstProductionFlagshipId))
            {
                if (!stableIds.Contains(snapshot.FirstProductionFlagshipId))
                {
                    error = "袖珍宇宙首次生产记录必须引用既有旗舰";
                    return false;
                }
                firstProduction = snapshot.FirstProductionFlagshipId;
            }
            error = string.Empty;
            return true;
        }

        private bool IsFlagship(string stableInstanceId)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId)) return false;
            foreach (string selectedId in flagshipByDefinition.Values)
            {
                if (string.Equals(
                        selectedId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsEligibleCandidate(
            PocketUniverseBuildingCandidate candidate)
        {
            return candidate != null && candidate.IsCompleted &&
                candidate.IsPlayerOwned &&
                !string.IsNullOrWhiteSpace(candidate.StableInstanceId) &&
                IsEligibleDefinition(candidate.BuildingDefinitionId);
        }

        private static bool IsEligibleDefinition(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return false;
            for (var index = 0;
                 index < eligibleBuildingDefinitionIds.Count;
                 index++)
            {
                if (string.Equals(
                        eligibleBuildingDefinitionIds[index],
                        definitionId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private void RebuildSnapshot()
        {
            var flagships = new PocketUniverseFlagshipState[
                flagshipByDefinition.Count];
            var index = 0;
            foreach (KeyValuePair<string, string> item in
                     flagshipByDefinition)
            {
                flagships[index++] = new PocketUniverseFlagshipState(
                    item.Key,
                    item.Value);
            }
            var collapsed = new List<string>(collapsedFlagshipIds);
            collapsed.Sort(StringComparer.Ordinal);
            cachedSnapshot = new PocketUniverseFateSnapshot(
                level,
                revision,
                flagships,
                collapsed.ToArray(),
                firstProductionFlagshipId);
        }

        private static ReadOnlyCollection<string>
            BuildEligibleDefinitionIds()
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            IReadOnlyList<FormalProductionDefinition> definitions =
                FormalProductionDefinitionCatalog.All;
            for (var index = 0; index < definitions.Count; index++)
            {
                string buildingId = definitions[index]?.BuildingId;
                if (!string.IsNullOrWhiteSpace(buildingId)) ids.Add(buildingId);
            }
            var result = new string[ids.Count];
            ids.CopyTo(result);
            return Array.AsReadOnly(result);
        }
    }
}
