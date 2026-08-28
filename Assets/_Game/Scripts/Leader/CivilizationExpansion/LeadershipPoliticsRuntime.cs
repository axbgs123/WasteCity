using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Leader.CivilizationExpansion
{
    public sealed class InternalFactionDefinition
    {
        internal InternalFactionDefinition(
            string id,
            string displayName,
            int initialInfluence,
            int initialLoyalty)
        {
            Id = new StableId(id);
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException("派系名称不能为空", nameof(displayName))
                : displayName;
            InitialInfluence = Clamp(initialInfluence);
            InitialLoyalty = Clamp(initialLoyalty);
        }

        public StableId Id { get; }
        public string DisplayName { get; }
        public int InitialInfluence { get; }
        public int InitialLoyalty { get; }

        private static int Clamp(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }

    public static class InternalFactionCatalog
    {
        public static InternalFactionDefinition EngineeringCouncil { get; } =
            new InternalFactionDefinition(
                "core.faction.engineering-council",
                "工程议会",
                45,
                65);

        public static InternalFactionDefinition GarrisonCorps { get; } =
            new InternalFactionDefinition(
                "core.faction.garrison-corps",
                "守备团",
                35,
                60);

        public static InternalFactionDefinition MigrantMilitia { get; } =
            new InternalFactionDefinition(
                "core.faction.migrant-militia",
                "迁徙民团",
                20,
                70);

        private static readonly ReadOnlyCollection<InternalFactionDefinition>
            all = Array.AsReadOnly(new[]
            {
                EngineeringCouncil,
                GarrisonCorps,
                MigrantMilitia,
            });

        public static IReadOnlyList<InternalFactionDefinition> All => all;
    }

    public sealed class InternalFactionSnapshot
    {
        internal InternalFactionSnapshot(
            InternalFactionDefinition definition,
            int influence,
            int loyalty)
        {
            Definition = definition;
            Influence = influence;
            Loyalty = loyalty;
        }

        public InternalFactionDefinition Definition { get; }
        public int Influence { get; }
        public int Loyalty { get; }
    }

    public sealed class CandidateSupportProjection
    {
        internal CandidateSupportProjection(
            string characterId,
            float prestigeContribution,
            float loyaltyContribution,
            float assignmentContribution,
            float factionContribution,
            float designationContribution)
        {
            CharacterId = characterId;
            PrestigeContribution = prestigeContribution;
            LoyaltyContribution = loyaltyContribution;
            AssignmentContribution = assignmentContribution;
            FactionContribution = factionContribution;
            DesignationContribution = designationContribution;
            Total = Math.Max(
                0f,
                Math.Min(
                    100f,
                    prestigeContribution + loyaltyContribution +
                    assignmentContribution + factionContribution +
                    designationContribution));
        }

        public string CharacterId { get; }
        public float PrestigeContribution { get; }
        public float LoyaltyContribution { get; }
        public float AssignmentContribution { get; }
        public float FactionContribution { get; }
        public float DesignationContribution { get; }
        public float Total { get; }
    }

    public enum SuccessionCommandResult
    {
        None,
        Committed,
        CoupCrisisStarted,
    }

    public enum CoupResolution
    {
        Concession,
        Suppression,
    }

    public sealed class CoupCrisisSnapshot
    {
        public CoupCrisisSnapshot(string candidateId, float support)
        {
            CandidateId = candidateId;
            Support = support;
        }

        public string CandidateId { get; }
        public float Support { get; }
    }

    public sealed class FactionCandidateSupportSnapshot
    {
        public FactionCandidateSupportSnapshot(string characterId, int support)
        {
            CharacterId = characterId;
            Support = support;
        }

        public string CharacterId { get; }
        public int Support { get; }
    }

    public sealed class InternalFactionStateSnapshot
    {
        private readonly ReadOnlyCollection<FactionCandidateSupportSnapshot>
            candidateSupports;

        public InternalFactionStateSnapshot(
            string factionId,
            int influence,
            int loyalty,
            IReadOnlyList<FactionCandidateSupportSnapshot> candidateSupports)
        {
            FactionId = factionId;
            Influence = influence;
            Loyalty = loyalty;
            var copy = new FactionCandidateSupportSnapshot[
                candidateSupports?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = candidateSupports[index];
            this.candidateSupports = Array.AsReadOnly(copy);
        }

        public string FactionId { get; }
        public int Influence { get; }
        public int Loyalty { get; }
        public IReadOnlyList<FactionCandidateSupportSnapshot>
            CandidateSupports => candidateSupports;
    }

    public sealed class LeadershipPoliticsSnapshot
    {
        private readonly ReadOnlyCollection<InternalFactionStateSnapshot>
            factions;

        public LeadershipPoliticsSnapshot(
            string currentLeaderId,
            string designatedSuccessorId,
            bool isInterimCouncilActive,
            CoupCrisisSnapshot crisis,
            IReadOnlyList<InternalFactionStateSnapshot> factions)
        {
            CurrentLeaderId = currentLeaderId;
            DesignatedSuccessorId = designatedSuccessorId;
            IsInterimCouncilActive = isInterimCouncilActive;
            Crisis = crisis;
            var copy = new InternalFactionStateSnapshot[factions?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = factions[index];
            this.factions = Array.AsReadOnly(copy);
        }

        public string CurrentLeaderId { get; }
        public string DesignatedSuccessorId { get; }
        public bool IsInterimCouncilActive { get; }
        public CoupCrisisSnapshot Crisis { get; }
        public IReadOnlyList<InternalFactionStateSnapshot> Factions => factions;
    }

    public sealed class CoupResolutionOutcome
    {
        internal CoupResolutionOutcome(
            CoupResolution resolution,
            string resourceCostId,
            int resourceCostAmount,
            string settlementId,
            int settlementLoyaltyDelta)
        {
            Resolution = resolution;
            ResourceCostId = resourceCostId;
            ResourceCostAmount = resourceCostAmount;
            SettlementId = settlementId;
            SettlementLoyaltyDelta = settlementLoyaltyDelta;
        }

        public CoupResolution Resolution { get; }
        public string ResourceCostId { get; }
        public int ResourceCostAmount { get; }
        public string SettlementId { get; }
        public int SettlementLoyaltyDelta { get; }
    }

    public interface ILeadershipPoliticsResolutionAuthority
    {
        bool TrySpendResource(string resourceId, int amount, out string error);
        bool TryAdjustSettlementLoyalty(
            string settlementId,
            int delta,
            out string error);
    }

    public sealed class LeadershipPoliticsRuntime
    {
        public const float InterimCouncilEfficiency = .75f;
        public const float SupportedSuccessionThreshold = 60f;
        public const int ConcessionAlloyCost = 10;
        public const int SuppressionFactionLoyaltyDelta = -15;
        public const int SuppressionSettlementLoyaltyDelta = -20;

        private readonly Dictionary<string, CharacterLifeRuntime> characters;
        private readonly Dictionary<string, FactionState> factions;
        private readonly ReadOnlyCollection<CharacterLifeRuntime> allCharacters;

        public LeadershipPoliticsRuntime(
            IReadOnlyList<CharacterLifeRuntime> characters,
            string currentLeaderId)
        {
            if (characters == null)
                throw new ArgumentNullException(nameof(characters));
            this.characters = new Dictionary<string, CharacterLifeRuntime>(
                StringComparer.Ordinal);
            var characterList = new List<CharacterLifeRuntime>();
            for (var index = 0; index < characters.Count; index++)
            {
                CharacterLifeRuntime character = characters[index] ??
                    throw new ArgumentException("角色运行时不能为空", nameof(characters));
                if (!this.characters.TryAdd(
                        character.Definition.Id.Value,
                        character))
                {
                    throw new ArgumentException("角色 ID 不能重复", nameof(characters));
                }
                characterList.Add(character);
            }
            if (!this.characters.ContainsKey(currentLeaderId))
                throw new ArgumentException("当前领袖不存在", nameof(currentLeaderId));
            CurrentLeaderId = currentLeaderId;
            allCharacters = characterList.AsReadOnly();
            factions = CreateFactions();
        }

        public IReadOnlyList<CharacterLifeRuntime> Characters => allCharacters;
        public string CurrentLeaderId { get; private set; }
        public string DesignatedSuccessorId { get; private set; } = string.Empty;
        public bool IsInterimCouncilActive { get; private set; }
        public float EfficiencyMultiplier =>
            IsInterimCouncilActive ? InterimCouncilEfficiency : 1f;
        public CoupCrisisSnapshot Crisis { get; private set; }

        public IReadOnlyList<InternalFactionSnapshot> Factions
        {
            get
            {
                var result = new InternalFactionSnapshot[
                    InternalFactionCatalog.All.Count];
                for (var index = 0; index < result.Length; index++)
                {
                    InternalFactionDefinition definition =
                        InternalFactionCatalog.All[index];
                    FactionState state = factions[definition.Id.Value];
                    result[index] = new InternalFactionSnapshot(
                        definition,
                        state.Influence,
                        state.Loyalty);
                }
                return Array.AsReadOnly(result);
            }
        }

        public bool TryDesignateSuccessor(string characterId, out string error)
        {
            if (!TryGetEligibleCandidate(characterId, out _, out error))
                return false;
            DesignatedSuccessorId = characterId;
            error = string.Empty;
            return true;
        }

        public bool TryHandleCurrentLeaderDeath(out string error)
        {
            if (!characters.TryGetValue(
                    CurrentLeaderId,
                    out CharacterLifeRuntime current) ||
                current.State != CharacterLifeState.Dead)
            {
                error = "当前领袖尚未永久死亡";
                return false;
            }
            if (IsInterimCouncilActive)
            {
                error = "临时议会已经运行";
                return false;
            }
            IsInterimCouncilActive = true;
            Crisis = null;
            error = string.Empty;
            return true;
        }

        public CandidateSupportProjection EvaluateCandidateSupport(
            string characterId)
        {
            if (!characters.TryGetValue(
                    characterId,
                    out CharacterLifeRuntime character))
            {
                return new CandidateSupportProjection(
                    characterId ?? string.Empty,
                    0f,
                    0f,
                    0f,
                    0f,
                    0f);
            }
            return EvaluateCandidateSupport(characterId, character, factions);
        }

        public bool TrySetCandidateSupport(
            string factionId,
            string characterId,
            int support,
            out string error)
        {
            if (!factions.TryGetValue(factionId, out FactionState faction))
            {
                error = "内部派系不存在";
                return false;
            }
            if (!characters.ContainsKey(characterId))
            {
                error = "候选人不存在";
                return false;
            }
            faction.SetSupport(characterId, support);
            error = string.Empty;
            return true;
        }

        public bool TryChooseSuccessor(
            string candidateId,
            bool forceLowSupport,
            out SuccessionCommandResult result,
            out string error)
        {
            result = SuccessionCommandResult.None;
            if (!IsInterimCouncilActive)
            {
                error = "当前不在继承期";
                return false;
            }
            if (Crisis != null)
            {
                error = "必须先解决政变危机";
                return false;
            }
            if (!TryGetEligibleCandidate(
                    candidateId,
                    out _,
                    out error))
            {
                return false;
            }

            CandidateSupportProjection support =
                EvaluateCandidateSupport(candidateId);
            if (support.Total >= SupportedSuccessionThreshold)
            {
                CommitSuccessor(candidateId);
                result = SuccessionCommandResult.Committed;
                error = string.Empty;
                return true;
            }
            if (!forceLowSupport)
            {
                error = "候选人支持度不足";
                return false;
            }

            Crisis = new CoupCrisisSnapshot(candidateId, support.Total);
            result = SuccessionCommandResult.CoupCrisisStarted;
            error = string.Empty;
            return true;
        }

        public bool TryResolveCoup(
            CoupResolution resolution,
            ILeadershipPoliticsResolutionAuthority authority,
            string targetSettlementId,
            out CoupResolutionOutcome outcome,
            out string error)
        {
            outcome = null;
            if (Crisis == null)
            {
                error = "当前没有政变危机";
                return false;
            }
            if (!Enum.IsDefined(typeof(CoupResolution), resolution))
            {
                error = "政变解决方式无效";
                return false;
            }
            string candidateId = Crisis.CandidateId;
            if (!TryGetEligibleCandidate(candidateId, out _, out error))
                return false;
            if (authority == null)
                throw new ArgumentNullException(nameof(authority));

            if (resolution == CoupResolution.Concession)
            {
                if (!authority.TrySpendResource(
                        ResourceIds.Alloy,
                        ConcessionAlloyCost,
                        out error))
                {
                    return false;
                }
                var nonSupporters = new List<FactionState>();
                for (var index = 0; index < InternalFactionCatalog.All.Count; index++)
                {
                    FactionState faction = factions[
                        InternalFactionCatalog.All[index].Id.Value];
                    if (faction.SupportFor(candidateId) < 50)
                        nonSupporters.Add(faction);
                }
                for (var index = 0;
                     index < Math.Min(2, nonSupporters.Count);
                     index++)
                {
                    nonSupporters[index].Influence = Clamp(
                        nonSupporters[index].Influence + 10);
                }
                outcome = new CoupResolutionOutcome(
                    resolution,
                    ResourceIds.Alloy,
                    ConcessionAlloyCost,
                    string.Empty,
                    0);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(targetSettlementId))
                {
                    error = "镇压必须指定城市";
                    return false;
                }
                if (!authority.TryAdjustSettlementLoyalty(
                        targetSettlementId,
                        SuppressionSettlementLoyaltyDelta,
                        out error))
                {
                    return false;
                }
                foreach (FactionState faction in factions.Values)
                    faction.Loyalty = Clamp(
                        faction.Loyalty + SuppressionFactionLoyaltyDelta);
                outcome = new CoupResolutionOutcome(
                    resolution,
                    string.Empty,
                    0,
                    targetSettlementId,
                    SuppressionSettlementLoyaltyDelta);
            }

            CommitSuccessor(candidateId);
            error = string.Empty;
            return true;
        }

        public LeadershipPoliticsSnapshot Capture()
        {
            var factionSnapshots = new InternalFactionStateSnapshot[
                InternalFactionCatalog.All.Count];
            for (var index = 0; index < factionSnapshots.Length; index++)
            {
                InternalFactionDefinition definition =
                    InternalFactionCatalog.All[index];
                FactionState faction = factions[definition.Id.Value];
                factionSnapshots[index] = faction.Capture(allCharacters);
            }
            CoupCrisisSnapshot crisis = Crisis == null
                ? null
                : new CoupCrisisSnapshot(Crisis.CandidateId, Crisis.Support);
            return new LeadershipPoliticsSnapshot(
                CurrentLeaderId,
                DesignatedSuccessorId,
                IsInterimCouncilActive,
                crisis,
                factionSnapshots);
        }

        public bool TryRestore(
            LeadershipPoliticsSnapshot snapshot,
            out string error)
        {
            if (!TryPrepareRestore(
                    snapshot,
                    out Dictionary<string, FactionState> restoredFactions,
                    out error))
            {
                return false;
            }

            factions.Clear();
            foreach (KeyValuePair<string, FactionState> pair in restoredFactions)
                factions.Add(pair.Key, pair.Value);
            CurrentLeaderId = snapshot.CurrentLeaderId;
            DesignatedSuccessorId = snapshot.DesignatedSuccessorId ?? string.Empty;
            IsInterimCouncilActive = snapshot.IsInterimCouncilActive;
            Crisis = snapshot.Crisis == null
                ? null
                : new CoupCrisisSnapshot(
                    snapshot.Crisis.CandidateId,
                    snapshot.Crisis.Support);
            error = string.Empty;
            return true;
        }

        private bool TryGetEligibleCandidate(
            string characterId,
            out CharacterLifeRuntime character,
            out string error)
        {
            if (!characters.TryGetValue(characterId ?? string.Empty, out character))
            {
                error = "候选人不存在";
                return false;
            }
            if (string.Equals(characterId, CurrentLeaderId, StringComparison.Ordinal))
            {
                error = "当前领袖不能成为继任候选";
                return false;
            }
            if (character.State != CharacterLifeState.Active)
            {
                error = "候选人必须处于可行动状态";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private void CommitSuccessor(string candidateId)
        {
            CurrentLeaderId = candidateId;
            DesignatedSuccessorId = string.Empty;
            Crisis = null;
            IsInterimCouncilActive = false;
        }

        private Dictionary<string, FactionState> CreateFactions()
        {
            var result = new Dictionary<string, FactionState>(
                StringComparer.Ordinal);
            for (var index = 0; index < InternalFactionCatalog.All.Count; index++)
            {
                InternalFactionDefinition definition =
                    InternalFactionCatalog.All[index];
                var state = new FactionState(definition);
                state.SetSupport(
                    CharacterCatalog.CenJinId,
                    definition == InternalFactionCatalog.MigrantMilitia
                        ? 100
                        : 0);
                state.SetSupport(
                    CharacterCatalog.LinXiId,
                    definition == InternalFactionCatalog.EngineeringCouncil
                        ? 100
                        : 0);
                state.SetSupport(
                    CharacterCatalog.HanGuId,
                    definition == InternalFactionCatalog.GarrisonCorps
                        ? 100
                        : 0);
                result.Add(definition.Id.Value, state);
            }
            return result;
        }

        private static int Clamp(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        private bool TryPrepareRestore(
            LeadershipPoliticsSnapshot snapshot,
            out Dictionary<string, FactionState> restoredFactions,
            out string error)
        {
            restoredFactions = null;
            if (snapshot == null ||
                !characters.TryGetValue(
                    snapshot.CurrentLeaderId ?? string.Empty,
                    out CharacterLifeRuntime current) ||
                snapshot.Factions == null ||
                snapshot.Factions.Count != InternalFactionCatalog.All.Count)
            {
                error = "领导与内政快照不完整";
                return false;
            }
            if (snapshot.IsInterimCouncilActive
                    ? current.State != CharacterLifeState.Dead
                    : current.State != CharacterLifeState.Active)
            {
                error = "领袖生命状态与议会状态不一致";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(snapshot.DesignatedSuccessorId) &&
                (!characters.TryGetValue(
                    snapshot.DesignatedSuccessorId,
                    out CharacterLifeRuntime designated) ||
                 designated.State != CharacterLifeState.Active ||
                 string.Equals(
                     snapshot.DesignatedSuccessorId,
                     snapshot.CurrentLeaderId,
                     StringComparison.Ordinal)))
            {
                error = "指定继承人快照无效";
                return false;
            }
            if (snapshot.Crisis != null &&
                (!snapshot.IsInterimCouncilActive ||
                 !characters.TryGetValue(
                     snapshot.Crisis.CandidateId ?? string.Empty,
                     out CharacterLifeRuntime candidate) ||
                 candidate.State != CharacterLifeState.Active ||
                 float.IsNaN(snapshot.Crisis.Support) ||
                 float.IsInfinity(snapshot.Crisis.Support) ||
                 snapshot.Crisis.Support < 0f ||
                 snapshot.Crisis.Support >= SupportedSuccessionThreshold))
            {
                error = "政变危机快照无效";
                return false;
            }
            if (!snapshot.IsInterimCouncilActive && snapshot.Crisis != null)
            {
                error = "非议会状态不能保存政变危机";
                return false;
            }

            var candidateFactions = new Dictionary<string, FactionState>(
                StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Factions.Count; index++)
            {
                InternalFactionStateSnapshot saved = snapshot.Factions[index];
                InternalFactionDefinition definition = FindFaction(
                    saved?.FactionId);
                if (definition == null ||
                    saved.Influence < 0 || saved.Influence > 100 ||
                    saved.Loyalty < 0 || saved.Loyalty > 100 ||
                    saved.CandidateSupports == null ||
                    saved.CandidateSupports.Count != characters.Count ||
                    candidateFactions.ContainsKey(definition.Id.Value))
                {
                    error = "内部派系快照无效";
                    return false;
                }
                var faction = new FactionState(definition)
                {
                    Influence = saved.Influence,
                    Loyalty = saved.Loyalty,
                };
                var seenCharacters = new HashSet<string>(StringComparer.Ordinal);
                for (var supportIndex = 0;
                     supportIndex < saved.CandidateSupports.Count;
                     supportIndex++)
                {
                    FactionCandidateSupportSnapshot support =
                        saved.CandidateSupports[supportIndex];
                    if (support == null ||
                        !characters.ContainsKey(support.CharacterId ?? string.Empty) ||
                        support.Support < 0 || support.Support > 100 ||
                        !seenCharacters.Add(support.CharacterId))
                    {
                        error = "派系候选支持快照无效";
                        return false;
                    }
                    faction.SetSupport(support.CharacterId, support.Support);
                }
                candidateFactions.Add(definition.Id.Value, faction);
            }
            if (snapshot.Crisis != null)
            {
                CharacterLifeRuntime crisisCandidate =
                    characters[snapshot.Crisis.CandidateId];
                float expectedSupport = EvaluateCandidateSupport(
                    snapshot.Crisis.CandidateId,
                    crisisCandidate,
                    candidateFactions).Total;
                if (Math.Abs(expectedSupport - snapshot.Crisis.Support) > .001f)
                {
                    error = "政变支持度与派系快照不一致";
                    return false;
                }
            }
            restoredFactions = candidateFactions;
            error = string.Empty;
            return true;
        }

        private static InternalFactionDefinition FindFaction(string id)
        {
            for (var index = 0; index < InternalFactionCatalog.All.Count; index++)
            {
                InternalFactionDefinition definition =
                    InternalFactionCatalog.All[index];
                if (string.Equals(
                        definition.Id.Value,
                        id,
                        StringComparison.Ordinal))
                {
                    return definition;
                }
            }
            return null;
        }

        private static CandidateSupportProjection EvaluateCandidateSupport(
            string characterId,
            CharacterLifeRuntime character,
            IReadOnlyDictionary<string, FactionState> sourceFactions)
        {
            float supportingInfluence = 0f;
            foreach (FactionState faction in sourceFactions.Values)
            {
                if (faction.SupportFor(characterId) >= 50)
                    supportingInfluence += faction.Influence;
            }
            return new CandidateSupportProjection(
                characterId,
                character.Definition.Prestige,
                (character.Loyalty - 50) * .5f,
                string.IsNullOrWhiteSpace(character.AssignedSettlementId)
                    ? 0f
                    : 10f,
                supportingInfluence * .5f,
                0f);
        }

        private sealed class FactionState
        {
            private readonly Dictionary<string, int> candidateSupport =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public FactionState(InternalFactionDefinition definition)
            {
                Definition = definition;
                Influence = definition.InitialInfluence;
                Loyalty = definition.InitialLoyalty;
            }

            public InternalFactionDefinition Definition { get; }
            public int Influence { get; set; }
            public int Loyalty { get; set; }

            public int SupportFor(string characterId)
            {
                return candidateSupport.TryGetValue(
                    characterId ?? string.Empty,
                    out int support)
                        ? support
                        : 0;
            }

            public void SetSupport(string characterId, int support)
            {
                candidateSupport[characterId] = Clamp(support);
            }

            public InternalFactionStateSnapshot Capture(
                IReadOnlyList<CharacterLifeRuntime> characters)
            {
                var supports = new FactionCandidateSupportSnapshot[
                    characters.Count];
                for (var index = 0; index < supports.Length; index++)
                {
                    string characterId = characters[index].Definition.Id.Value;
                    supports[index] = new FactionCandidateSupportSnapshot(
                        characterId,
                        SupportFor(characterId));
                }
                return new InternalFactionStateSnapshot(
                    Definition.Id.Value,
                    Influence,
                    Loyalty,
                    supports);
            }
        }
    }
}
