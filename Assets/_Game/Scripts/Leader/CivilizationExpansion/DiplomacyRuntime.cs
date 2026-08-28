using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Leader.CivilizationExpansion
{
    public sealed class ExternalFactionDefinition
    {
        internal ExternalFactionDefinition(
            string id,
            string displayName,
            int initialRelation)
        {
            Id = new StableId(id);
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException("外部势力名称不能为空", nameof(displayName))
                : displayName;
            InitialRelation = initialRelation;
        }

        public StableId Id { get; }
        public string DisplayName { get; }
        public int InitialRelation { get; }
    }

    public static class ExternalFactionCatalog
    {
        public static ExternalFactionDefinition AshCaravan { get; } =
            new ExternalFactionDefinition(
                "core.external-faction.ash-caravan",
                "灰烬商团",
                10);

        public static ExternalFactionDefinition CrystalAccord { get; } =
            new ExternalFactionDefinition(
                "core.external-faction.crystal-accord",
                "晶律协定会",
                -5);

        private static readonly ReadOnlyCollection<ExternalFactionDefinition>
            all = Array.AsReadOnly(new[] { AshCaravan, CrystalAccord });

        public static IReadOnlyList<ExternalFactionDefinition> All => all;
    }

    public enum DiplomacyRelationshipState
    {
        Unknown,
        Contacted,
        TradeAgreement,
        DefensePact,
        Hostile,
    }

    public enum DiplomacyOfferKind
    {
        AlloyForStone,
        BiomassForEnergyCrystal,
        ConvoyInterceptionImmunity,
    }

    public sealed class DiplomacyOfferSnapshot
    {
        public DiplomacyOfferSnapshot(
            string stableOfferId,
            string factionId,
            DiplomacyOfferKind kind,
            string costResourceId,
            int costAmount,
            string rewardResourceId,
            int rewardAmount,
            float remainingSeconds)
        {
            StableOfferId = stableOfferId;
            FactionId = factionId;
            Kind = kind;
            CostResourceId = costResourceId;
            CostAmount = costAmount;
            RewardResourceId = rewardResourceId;
            RewardAmount = rewardAmount;
            RemainingSeconds = remainingSeconds;
        }

        public string StableOfferId { get; }
        public string FactionId { get; }
        public DiplomacyOfferKind Kind { get; }
        public string CostResourceId { get; }
        public int CostAmount { get; }
        public string RewardResourceId { get; }
        public int RewardAmount { get; }
        public float RemainingSeconds { get; }
        public int RelationDelta => DiplomacyRuntime.AcceptanceRelationDelta;
        public bool GrantsConvoyInterceptionImmunity =>
            Kind == DiplomacyOfferKind.ConvoyInterceptionImmunity;

        internal DiplomacyOfferSnapshot WithRemaining(float remainingSeconds)
        {
            return new DiplomacyOfferSnapshot(
                StableOfferId,
                FactionId,
                Kind,
                CostResourceId,
                CostAmount,
                RewardResourceId,
                RewardAmount,
                remainingSeconds);
        }
    }

    public sealed class DiplomacyFactionSnapshot
    {
        internal DiplomacyFactionSnapshot(
            ExternalFactionDefinition definition,
            DiplomacyRelationshipState state,
            int relation,
            float refreshRemainingSeconds,
            DiplomacyOfferSnapshot activeOffer)
        {
            Definition = definition;
            State = state;
            Relation = relation;
            CooldownRemainingSeconds = refreshRemainingSeconds;
            ActiveOffer = activeOffer;
        }

        public ExternalFactionDefinition Definition { get; }
        public DiplomacyRelationshipState State { get; }
        public bool Contacted => State != DiplomacyRelationshipState.Unknown;
        public int Relation { get; }
        public float CooldownRemainingSeconds { get; }
        public DiplomacyOfferSnapshot ActiveOffer { get; }
    }

    public sealed class DiplomacyFactionStateSnapshot
    {
        public DiplomacyFactionStateSnapshot(
            string factionId,
            DiplomacyRelationshipState state,
            int relation,
            float refreshRemainingSeconds,
            DiplomacyOfferSnapshot activeOffer)
        {
            FactionId = factionId;
            State = state;
            Relation = relation;
            RefreshRemainingSeconds = refreshRemainingSeconds;
            ActiveOffer = activeOffer;
        }

        public DiplomacyFactionStateSnapshot(
            string factionId,
            bool contacted,
            int relation,
            float refreshRemainingSeconds,
            DiplomacyOfferSnapshot activeOffer)
            : this(
                factionId,
                contacted
                    ? relation < DiplomacyRuntime.HostileThreshold
                        ? DiplomacyRelationshipState.Hostile
                        : DiplomacyRelationshipState.Contacted
                    : DiplomacyRelationshipState.Unknown,
                relation,
                refreshRemainingSeconds,
                activeOffer)
        {
        }

        public string FactionId { get; }
        public DiplomacyRelationshipState State { get; }
        public bool Contacted => State != DiplomacyRelationshipState.Unknown;
        public int Relation { get; }
        public float RefreshRemainingSeconds { get; }
        public float CooldownRemainingSeconds => RefreshRemainingSeconds;
        public DiplomacyOfferSnapshot ActiveOffer { get; }
    }

    public sealed class DiplomacyRuntimeSnapshot
    {
        private readonly ReadOnlyCollection<DiplomacyFactionStateSnapshot>
            factions;

        public DiplomacyRuntimeSnapshot(
            string sessionId,
            ulong nextOfferOrdinal,
            IReadOnlyList<DiplomacyFactionStateSnapshot> factions,
            int convoyInterceptionImmunityCharges)
        {
            SessionId = sessionId;
            NextOfferOrdinal = nextOfferOrdinal;
            var copy = new DiplomacyFactionStateSnapshot[factions?.Count ?? 0];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = factions[index];
            this.factions = Array.AsReadOnly(copy);
            ConvoyInterceptionImmunityCharges =
                convoyInterceptionImmunityCharges;
        }

        public string SessionId { get; }
        public ulong NextOfferOrdinal { get; }
        public IReadOnlyList<DiplomacyFactionStateSnapshot> Factions => factions;
        public int ConvoyInterceptionImmunityCharges { get; }
    }

    public sealed class DiplomacySettlement
    {
        internal DiplomacySettlement(
            string factionId,
            string stableOfferId,
            int relationDelta,
            int convoyInterceptionImmunityDelta)
        {
            FactionId = factionId;
            StableOfferId = stableOfferId;
            RelationDelta = relationDelta;
            ConvoyInterceptionImmunityDelta =
                convoyInterceptionImmunityDelta;
        }

        public string FactionId { get; }
        public string StableOfferId { get; }
        public int RelationDelta { get; }
        public int ConvoyInterceptionImmunityDelta { get; }
    }

    public interface IDiplomacyResourceWallet
    {
        bool TryExchange(
            string costResourceId,
            int costAmount,
            string rewardResourceId,
            int rewardAmount,
            out string error);
    }

    public sealed class DiplomacyRuntime
    {
        public const string DefaultSessionId = "formal.session.default";
        public const int MinimumRelation = -100;
        public const int MaximumRelation = 100;
        public const int AcceptanceRelationDelta = 5;
        public const int RejectionRelationDelta = -1;
        public const int TradeAgreementThreshold = 40;
        public const int DefensePactThreshold = 70;
        public const int HostileThreshold = -40;
        public const float OfferRefreshSeconds = 60f;

        private readonly Dictionary<string, FactionState> factions =
            new Dictionary<string, FactionState>(StringComparer.Ordinal);
        private readonly string sessionId;
        private ulong nextOfferOrdinal = 1ul;

        public DiplomacyRuntime(string sessionId = DefaultSessionId)
        {
            this.sessionId = string.IsNullOrWhiteSpace(sessionId)
                ? throw new ArgumentException("外交会话 ID 不能为空", nameof(sessionId))
                : sessionId;
            for (var index = 0; index < ExternalFactionCatalog.All.Count; index++)
            {
                ExternalFactionDefinition definition =
                    ExternalFactionCatalog.All[index];
                factions.Add(definition.Id.Value, new FactionState(definition));
            }
        }

        public string SessionId => sessionId;
        public int ConvoyInterceptionImmunityCharges { get; private set; }

        public DiplomacyRuntimeSnapshot Capture()
        {
            var snapshots = new DiplomacyFactionStateSnapshot[
                ExternalFactionCatalog.All.Count];
            for (var index = 0; index < snapshots.Length; index++)
            {
                ExternalFactionDefinition definition =
                    ExternalFactionCatalog.All[index];
                FactionState faction = factions[definition.Id.Value];
                snapshots[index] = new DiplomacyFactionStateSnapshot(
                    definition.Id.Value,
                    faction.State,
                    faction.Relation,
                    faction.RefreshRemainingSeconds,
                    CloneOffer(faction.ActiveOffer));
            }
            return new DiplomacyRuntimeSnapshot(
                sessionId,
                nextOfferOrdinal,
                snapshots,
                ConvoyInterceptionImmunityCharges);
        }

        public bool TryRestore(DiplomacyRuntimeSnapshot snapshot, out string error)
        {
            if (!TryPrepareRestore(
                    snapshot,
                    out Dictionary<string, FactionState> restored,
                    out error))
            {
                return false;
            }
            factions.Clear();
            foreach (KeyValuePair<string, FactionState> pair in restored)
                factions.Add(pair.Key, pair.Value);
            nextOfferOrdinal = snapshot.NextOfferOrdinal;
            ConvoyInterceptionImmunityCharges =
                snapshot.ConvoyInterceptionImmunityCharges;
            error = string.Empty;
            return true;
        }

        public DiplomacyFactionSnapshot GetFaction(string factionId)
        {
            return factions.TryGetValue(
                    factionId ?? string.Empty,
                    out FactionState state)
                ? state.Capture()
                : null;
        }

        public bool EstablishContact(string factionId, out string error)
        {
            if (!TryGet(factionId, out FactionState faction, out error))
                return false;
            if (faction.State != DiplomacyRelationshipState.Unknown)
            {
                error = "已与该势力建立接触";
                return false;
            }
            faction.State = faction.Relation < HostileThreshold
                ? DiplomacyRelationshipState.Hostile
                : DiplomacyRelationshipState.Contacted;
            error = string.Empty;
            return true;
        }

        public bool AdjustRelation(
            string factionId,
            int delta,
            out string error)
        {
            if (!TryGet(factionId, out FactionState faction, out error))
                return false;
            if (faction.State == DiplomacyRelationshipState.Unknown)
            {
                error = "尚未与该势力建立接触";
                return false;
            }
            faction.Relation = ClampRelation(faction.Relation + delta);
            NormalizeRelationshipState(faction);
            error = string.Empty;
            return true;
        }

        public bool TrySignTradeAgreement(string factionId, out string error)
        {
            if (!TryGet(factionId, out FactionState faction, out error))
                return false;
            if (faction.State == DiplomacyRelationshipState.Unknown ||
                faction.State == DiplomacyRelationshipState.Hostile ||
                faction.Relation < TradeAgreementThreshold)
            {
                error = "贸易协定需要关系达到 40";
                return false;
            }
            faction.State = DiplomacyRelationshipState.TradeAgreement;
            error = string.Empty;
            return true;
        }

        public bool TrySignDefensePact(string factionId, out string error)
        {
            if (!TryGet(factionId, out FactionState faction, out error))
                return false;
            if (faction.State == DiplomacyRelationshipState.Unknown ||
                faction.State == DiplomacyRelationshipState.Hostile ||
                faction.Relation < DefensePactThreshold)
            {
                error = "防御条约需要关系达到 70";
                return false;
            }
            faction.State = DiplomacyRelationshipState.DefensePact;
            error = string.Empty;
            return true;
        }

        public bool TryRefreshOffer(
            string factionId,
            out DiplomacyOfferSnapshot offer,
            out string error)
        {
            offer = null;
            if (!TryGet(factionId, out FactionState faction, out error))
                return false;
            if (faction.State == DiplomacyRelationshipState.Unknown ||
                faction.State == DiplomacyRelationshipState.Hostile)
            {
                error = "当前外交状态不允许报价";
                return false;
            }
            if (faction.RefreshRemainingSeconds > 0f ||
                faction.ActiveOffer != null)
            {
                error = "外交报价仍在 60 秒刷新周期内";
                return false;
            }

            offer = CreateOffer(
                faction.Definition,
                sessionId,
                nextOfferOrdinal++);
            faction.ActiveOffer = offer;
            faction.RefreshRemainingSeconds = OfferRefreshSeconds;
            error = string.Empty;
            return true;
        }

        public bool TryAcceptOffer(
            string factionId,
            IDiplomacyResourceWallet wallet,
            out DiplomacySettlement settlement,
            out string error)
        {
            settlement = null;
            if (wallet == null)
                throw new ArgumentNullException(nameof(wallet));
            if (!TryGet(factionId, out FactionState faction, out error))
                return false;
            DiplomacyOfferSnapshot offer = faction.ActiveOffer;
            if (offer == null ||
                faction.State == DiplomacyRelationshipState.Unknown ||
                faction.State == DiplomacyRelationshipState.Hostile)
            {
                error = "没有可接受的外交报价";
                return false;
            }
            if (!wallet.TryExchange(
                    offer.CostResourceId,
                    offer.CostAmount,
                    offer.RewardResourceId,
                    offer.RewardAmount,
                    out error))
            {
                return false;
            }

            faction.Relation = ClampRelation(
                faction.Relation + AcceptanceRelationDelta);
            NormalizeRelationshipState(faction);
            faction.ActiveOffer = null;
            int immunityDelta = offer.GrantsConvoyInterceptionImmunity ? 1 : 0;
            ConvoyInterceptionImmunityCharges += immunityDelta;
            settlement = new DiplomacySettlement(
                factionId,
                offer.StableOfferId,
                AcceptanceRelationDelta,
                immunityDelta);
            error = string.Empty;
            return true;
        }

        public bool TryRejectOffer(string factionId, out string error)
        {
            if (!TryGet(factionId, out FactionState faction, out error))
                return false;
            if (faction.ActiveOffer == null)
            {
                error = "没有可拒绝的外交报价";
                return false;
            }
            faction.ActiveOffer = null;
            faction.Relation = ClampRelation(
                faction.Relation + RejectionRelationDelta);
            NormalizeRelationshipState(faction);
            error = string.Empty;
            return true;
        }

        public bool TryConsumeConvoyInterceptionImmunity()
        {
            if (ConvoyInterceptionImmunityCharges <= 0) return false;
            ConvoyInterceptionImmunityCharges--;
            return true;
        }

        public void Tick(float deltaSeconds, bool paused)
        {
            if (paused) return;
            float delta = Math.Max(0f, deltaSeconds);
            if (delta <= 0f) return;
            foreach (FactionState faction in factions.Values)
            {
                faction.RefreshRemainingSeconds = Math.Max(
                    0f,
                    faction.RefreshRemainingSeconds - delta);
                if (faction.ActiveOffer == null) continue;
                float remaining = Math.Max(
                    0f,
                    faction.ActiveOffer.RemainingSeconds - delta);
                faction.ActiveOffer = remaining <= 0f
                    ? null
                    : faction.ActiveOffer.WithRemaining(remaining);
            }
        }

        private static DiplomacyOfferSnapshot CreateOffer(
            ExternalFactionDefinition faction,
            string sessionId,
            ulong ordinal)
        {
            uint seed = StableHash(faction.Id.Value + ":" + sessionId);
            DiplomacyOfferKind kind = (DiplomacyOfferKind)(
                (seed + ordinal - 1ul) % 3ul);
            string stableId = "diplomacy.offer." + ordinal;
            switch (kind)
            {
                case DiplomacyOfferKind.AlloyForStone:
                    return new DiplomacyOfferSnapshot(
                        stableId,
                        faction.Id.Value,
                        kind,
                        ResourceIds.Alloy,
                        10,
                        ResourceIds.Stone,
                        20,
                        OfferRefreshSeconds);
                case DiplomacyOfferKind.BiomassForEnergyCrystal:
                    return new DiplomacyOfferSnapshot(
                        stableId,
                        faction.Id.Value,
                        kind,
                        ResourceIds.Biomass,
                        12,
                        ResourceIds.EnergyCrystal,
                        8,
                        OfferRefreshSeconds);
                default:
                    return new DiplomacyOfferSnapshot(
                        stableId,
                        faction.Id.Value,
                        kind,
                        ResourceIds.Ammunition,
                        15,
                        string.Empty,
                        0,
                        OfferRefreshSeconds);
            }
        }

        private bool TryGet(
            string factionId,
            out FactionState faction,
            out string error)
        {
            if (!factions.TryGetValue(factionId ?? string.Empty, out faction))
            {
                error = "外部势力不存在";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool TryPrepareRestore(
            DiplomacyRuntimeSnapshot snapshot,
            out Dictionary<string, FactionState> restored,
            out string error)
        {
            restored = null;
            if (snapshot == null ||
                !string.Equals(snapshot.SessionId, sessionId, StringComparison.Ordinal) ||
                snapshot.NextOfferOrdinal == 0ul ||
                snapshot.Factions == null ||
                snapshot.Factions.Count != ExternalFactionCatalog.All.Count ||
                snapshot.ConvoyInterceptionImmunityCharges < 0)
            {
                error = "外交快照顶层状态无效";
                return false;
            }
            var candidate = new Dictionary<string, FactionState>(
                StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Factions.Count; index++)
            {
                DiplomacyFactionStateSnapshot saved = snapshot.Factions[index];
                ExternalFactionDefinition definition = FindFaction(saved?.FactionId);
                if (definition == null ||
                    !Enum.IsDefined(typeof(DiplomacyRelationshipState), saved.State) ||
                    saved.Relation < MinimumRelation ||
                    saved.Relation > MaximumRelation ||
                    !IsFiniteRange(
                        saved.RefreshRemainingSeconds,
                        0f,
                        OfferRefreshSeconds) ||
                    candidate.ContainsKey(definition.Id.Value) ||
                    !RelationshipMatches(saved.State, saved.Relation) ||
                    (saved.State == DiplomacyRelationshipState.Unknown &&
                     (saved.Relation != definition.InitialRelation ||
                      saved.ActiveOffer != null ||
                      saved.RefreshRemainingSeconds > 0f)) ||
                    (saved.State == DiplomacyRelationshipState.Hostile &&
                     saved.ActiveOffer != null) ||
                    !IsValidOffer(
                        saved.ActiveOffer,
                        saved.FactionId,
                        snapshot.NextOfferOrdinal))
                {
                    error = "外部势力快照无效";
                    return false;
                }
                candidate.Add(
                    definition.Id.Value,
                    new FactionState(definition)
                    {
                        State = saved.State,
                        Relation = saved.Relation,
                        RefreshRemainingSeconds = saved.RefreshRemainingSeconds,
                        ActiveOffer = CloneOffer(saved.ActiveOffer),
                    });
            }
            restored = candidate;
            error = string.Empty;
            return true;
        }

        private static bool IsValidOffer(
            DiplomacyOfferSnapshot offer,
            string factionId,
            ulong nextOfferOrdinal)
        {
            if (offer == null) return true;
            if (!string.Equals(offer.FactionId, factionId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(offer.StableOfferId) ||
                !Enum.IsDefined(typeof(DiplomacyOfferKind), offer.Kind) ||
                !IsFiniteRange(
                    offer.RemainingSeconds,
                    float.Epsilon,
                    OfferRefreshSeconds) ||
                !TryReadOfferOrdinal(
                    offer.StableOfferId,
                    out ulong offerOrdinal) ||
                offerOrdinal == 0ul || offerOrdinal >= nextOfferOrdinal)
            {
                return false;
            }
            switch (offer.Kind)
            {
                case DiplomacyOfferKind.AlloyForStone:
                    return offer.CostResourceId == ResourceIds.Alloy &&
                        offer.CostAmount == 10 &&
                        offer.RewardResourceId == ResourceIds.Stone &&
                        offer.RewardAmount == 20;
                case DiplomacyOfferKind.BiomassForEnergyCrystal:
                    return offer.CostResourceId == ResourceIds.Biomass &&
                        offer.CostAmount == 12 &&
                        offer.RewardResourceId == ResourceIds.EnergyCrystal &&
                        offer.RewardAmount == 8;
                default:
                    return offer.CostResourceId == ResourceIds.Ammunition &&
                        offer.CostAmount == 15 &&
                        string.IsNullOrEmpty(offer.RewardResourceId) &&
                        offer.RewardAmount == 0;
            }
        }

        private static bool TryReadOfferOrdinal(
            string stableOfferId,
            out ulong ordinal)
        {
            const string Prefix = "diplomacy.offer.";
            ordinal = 0ul;
            return stableOfferId != null &&
                stableOfferId.StartsWith(Prefix, StringComparison.Ordinal) &&
                ulong.TryParse(
                    stableOfferId.Substring(Prefix.Length),
                    out ordinal);
        }

        private static bool RelationshipMatches(
            DiplomacyRelationshipState state,
            int relation)
        {
            switch (state)
            {
                case DiplomacyRelationshipState.Unknown:
                case DiplomacyRelationshipState.Contacted:
                    return relation >= HostileThreshold;
                case DiplomacyRelationshipState.TradeAgreement:
                    return relation >= TradeAgreementThreshold;
                case DiplomacyRelationshipState.DefensePact:
                    return relation >= DefensePactThreshold;
                case DiplomacyRelationshipState.Hostile:
                    return relation < HostileThreshold;
                default:
                    return false;
            }
        }

        private static void NormalizeRelationshipState(FactionState faction)
        {
            if (faction.Relation < HostileThreshold)
            {
                faction.State = DiplomacyRelationshipState.Hostile;
                faction.ActiveOffer = null;
                return;
            }
            if (faction.State == DiplomacyRelationshipState.Hostile)
                faction.State = DiplomacyRelationshipState.Contacted;
            if (faction.State == DiplomacyRelationshipState.DefensePact &&
                faction.Relation < DefensePactThreshold)
            {
                faction.State = faction.Relation >= TradeAgreementThreshold
                    ? DiplomacyRelationshipState.TradeAgreement
                    : DiplomacyRelationshipState.Contacted;
            }
            else if (faction.State == DiplomacyRelationshipState.TradeAgreement &&
                     faction.Relation < TradeAgreementThreshold)
            {
                faction.State = DiplomacyRelationshipState.Contacted;
            }
        }

        private static DiplomacyOfferSnapshot CloneOffer(
            DiplomacyOfferSnapshot offer)
        {
            return offer == null
                ? null
                : new DiplomacyOfferSnapshot(
                    offer.StableOfferId,
                    offer.FactionId,
                    offer.Kind,
                    offer.CostResourceId,
                    offer.CostAmount,
                    offer.RewardResourceId,
                    offer.RewardAmount,
                    offer.RemainingSeconds);
        }

        private static ExternalFactionDefinition FindFaction(string id)
        {
            for (var index = 0; index < ExternalFactionCatalog.All.Count; index++)
            {
                ExternalFactionDefinition definition =
                    ExternalFactionCatalog.All[index];
                if (string.Equals(definition.Id.Value, id, StringComparison.Ordinal))
                    return definition;
            }
            return null;
        }

        private static int ClampRelation(int value)
        {
            return Math.Max(MinimumRelation, Math.Min(MaximumRelation, value));
        }

        private static bool IsFiniteRange(float value, float minimum, float maximum)
        {
            return !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value >= minimum && value <= maximum;
        }

        private static uint StableHash(string value)
        {
            uint hash = 2166136261u;
            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                hash = (hash ^ (byte)character) * 16777619u;
                hash = (hash ^ (byte)(character >> 8)) * 16777619u;
            }
            return hash;
        }

        private sealed class FactionState
        {
            public FactionState(ExternalFactionDefinition definition)
            {
                Definition = definition;
                Relation = definition.InitialRelation;
                State = DiplomacyRelationshipState.Unknown;
            }

            public ExternalFactionDefinition Definition { get; }
            public DiplomacyRelationshipState State { get; set; }
            public int Relation { get; set; }
            public float RefreshRemainingSeconds { get; set; }
            public DiplomacyOfferSnapshot ActiveOffer { get; set; }

            public DiplomacyFactionSnapshot Capture()
            {
                return new DiplomacyFactionSnapshot(
                    Definition,
                    State,
                    Relation,
                    RefreshRemainingSeconds,
                    CloneOffer(ActiveOffer));
            }
        }
    }
}
