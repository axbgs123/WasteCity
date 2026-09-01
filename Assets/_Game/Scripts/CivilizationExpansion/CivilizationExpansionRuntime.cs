using System;
using System.Collections.Generic;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Research;
using WasteCity.World;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.CivilizationExpansion
{
    public sealed class CivilizationExpansionRuntime :
        IConvoyEscortStatusProvider,
        IConvoyInterceptionImmunityProvider
    {
        private readonly WorldMapModel map;
        private readonly List<CharacterLifeRuntime> characters;
        private int nextExpeditionOrdinal = 1;
        private ulong ruleTick;
        private double characterAccumulatorSeconds;
        private const double CharacterFixedStepSeconds = .1d;

        public CivilizationExpansionRuntime(
            WorldMapModel map,
            int primaryCityX,
            int primaryCityY,
            ISettlementInventoryEndpoint primaryInventory)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            Army = new SingleCityArmyModel();
            Expedition = new ArmyExpeditionModel();
            WorldLayer = new WorldLayerRuntime(
                map,
                primaryCityX,
                primaryCityY,
                primaryInventory ?? throw new ArgumentNullException(
                    nameof(primaryInventory)));
            Transport = new TransportRuntime(map, WorldLayer, this);
            characters = new List<CharacterLifeRuntime>
            {
                new CharacterLifeRuntime(CharacterCatalog.CenJin),
                new CharacterLifeRuntime(CharacterCatalog.LinXi),
                new CharacterLifeRuntime(CharacterCatalog.HanGu),
            };
            for (var index = 0; index < characters.Count; index++)
            {
                characters[index].SetPosition(
                    WorldLayerCatalog.PrimaryCity.Id,
                    primaryCityX,
                    primaryCityY);
            }
            Politics = new LeadershipPoliticsRuntime(
                characters,
                CharacterCatalog.CenJinId);
            Diplomacy = new DiplomacyRuntime();
        }

        public SingleCityArmyModel Army { get; }
        public ArmyExpeditionModel Expedition { get; private set; }
        public WorldLayerRuntime WorldLayer { get; }
        public TransportRuntime Transport { get; }
        public IReadOnlyList<CharacterLifeRuntime> Characters => characters;
        public LeadershipPoliticsRuntime Politics { get; }
        public DiplomacyRuntime Diplomacy { get; private set; }
        public int NextExpeditionOrdinal => nextExpeditionOrdinal;
        public ulong RuleTick => ruleTick;
        public float CivilizationEfficiencyMultiplier =>
            Politics.EfficiencyMultiplier;
        public float ResearchEfficiencyMultiplier
        {
            get
            {
                float result = Politics.EfficiencyMultiplier;
                WorldLayerRuntimeSnapshot snapshot = WorldLayer.Capture();
                for (var index = 0;
                     index < snapshot.Settlements.Count;
                     index++)
                {
                    SettlementRuntime settlement = WorldLayer.GetSettlement(
                        snapshot.Settlements[index].StableId);
                    if (settlement != null)
                        result = Math.Max(
                            result,
                            Politics.EfficiencyMultiplier *
                            settlement.ResearchContributionMultiplier);
                }
                return result;
            }
        }

        public CharacterLifeRuntime FindCharacter(string characterId)
        {
            for (var index = 0; index < characters.Count; index++)
            {
                if (string.Equals(
                        characters[index].Definition.Id.Value,
                        characterId,
                        StringComparison.Ordinal))
                    return characters[index];
            }
            return null;
        }

        public void ConfigureResearchEffects(
            Func<ResearchEffectSnapshot> provider)
        {
            Army.ConfigureResearchEffects(provider);
        }

        public bool TryApplyGeneSplicingToCurrentLeader()
        {
            CharacterLifeRuntime current = FindCharacter(
                Politics.CurrentLeaderId);
            return current != null && current.TryApplyGeneSplicingTrait();
        }

        public bool TryStartExpedition(
            string sessionId,
            int targetX,
            int targetY,
            out string error)
        {
            error = string.Empty;
            if (Expedition.Status != ArmyExpeditionStatus.Idle &&
                Expedition.Status != ArmyExpeditionStatus.Completed &&
                Expedition.Status != ArmyExpeditionStatus.Retreated)
            {
                error = "已有远征正在进行或等待战利品入库";
                return false;
            }
            if (Transport.IsEscortCommittedToUnfinishedConvoy(
                    SingleCityArmyModel.DefaultSquadId))
            {
                error = "默认小队正在护送运输队，不能同时出征";
                return false;
            }
            SettlementRuntime primary = WorldLayer.PrimaryCity;
            if (targetX < 0 || targetY < 0 ||
                targetX >= map.Width || targetY >= map.Height ||
                !map.IsRevealed(targetX, targetY) ||
                !CityTerrainRules.IsPassable(map.Get(targetX, targetY)) ||
                !CityPathfinder.TryFindPath(
                    map,
                    primary.X,
                    primary.Y,
                    targetX,
                    targetY,
                    out WorldGridPoint[] path))
            {
                error = "远征目标必须是已揭示且可通行的格子";
                return false;
            }

            IReadOnlyList<ArmyUnitSnapshot> current = Army.Units;
            var participants = new ArmyExpeditionUnit[current.Count];
            for (var index = 0; index < current.Count; index++)
            {
                participants[index] = new ArmyExpeditionUnit(
                    current[index].StableId,
                    current[index].DefinitionId,
                    current[index].CurrentHealth,
                    current[index].IsActive);
            }
            var next = new ArmyExpeditionModel();
            if (!next.TryStart(
                    sessionId,
                    targetX,
                    targetY,
                    nextExpeditionOrdinal,
                    Math.Max(0, path.Length - 1),
                    participants,
                    Army.DefaultSquad.LeaderAssigned &&
                    Army.DefaultSquad.LeaderHealthy))
            {
                error = "远征至少需要一个未休眠单位";
                return false;
            }
            Expedition = next;
            Army.Commands.TryExpedition(
                targetX,
                targetY,
                isRevealed: true,
                isPassable: true);
            nextExpeditionOrdinal++;
            return true;
        }

        public bool RetreatExpedition()
        {
            if (!Expedition.Retreat()) return false;
            Army.Commands.Retreat();
            return true;
        }

        public void Tick(
            float ruleDeltaSeconds,
            bool globallyPaused,
            CityResourceStorageModel primaryStorage,
            Func<string, int> operationalBuildingCount)
        {
            if (primaryStorage == null || operationalBuildingCount == null)
                return;
            float delta = Math.Max(0f, ruleDeltaSeconds);
            int characterSteps = 0;
            if (!globallyPaused && delta > 0f)
            {
                characterAccumulatorSeconds += delta;
                characterSteps = (int)Math.Floor(
                    (characterAccumulatorSeconds + .000001d) /
                    CharacterFixedStepSeconds);
                if (characterSteps > 0)
                {
                    characterAccumulatorSeconds -=
                        characterSteps * CharacterFixedStepSeconds;
                    unchecked { ruleTick += (ulong)characterSteps; }
                }
            }
            float characterDelta = (float)(
                characterSteps * CharacterFixedStepSeconds);
            float productionDelta = delta * Math.Max(
                0f,
                Politics.EfficiencyMultiplier);
            for (var index = 0; index < ArmyUnitCatalog.All.Count; index++)
            {
                ArmyUnitDefinition definition = ArmyUnitCatalog.All[index];
                Army.TickManufacturing(
                    definition.Id,
                    productionDelta,
                    Math.Max(
                        0,
                        operationalBuildingCount(
                            definition.SourceBuildingId)),
                    globallyPaused,
                    primaryStorage);
            }
            Army.TickMaintenance(
                productionDelta,
                globallyPaused,
                primaryStorage);
            Army.TickTechnologyEffects(delta, globallyPaused);

            ArmyExpeditionStatus before = Expedition.Status;
            Expedition.Tick(delta, globallyPaused);
            if (before == ArmyExpeditionStatus.Outbound &&
                Expedition.Resolution != null)
            {
                Army.ApplyExpeditionCasualties(
                    Expedition.Resolution.CasualtyStableUnitIds);
            }
            if (Expedition.Status == ArmyExpeditionStatus.Returned)
                Expedition.TryDepositReturnedLoot(primaryStorage);

            if (!globallyPaused)
            {
                WorldLayer.Tick(productionDelta);
                Transport.Tick(delta);
            }
            for (var index = 0; index < characters.Count; index++)
            {
                CharacterLifeRuntime character = characters[index];
                character.TickTechnologyEffects(delta, globallyPaused);
                CharacterLifeSnapshot characterSnapshot = character.Capture();
                CharacterRescueSnapshot rescue = characterSnapshot.Rescue;
                CharacterLifeRuntime rescueSource = rescue != null &&
                    rescue.Method == CharacterRescueMethod.CharacterContact
                        ? FindCharacter(rescue.SourceId)
                        : null;
                SettlementRuntime rescueSettlement = rescue != null &&
                    rescue.Method == CharacterRescueMethod.CityMedical
                        ? WorldLayer.GetSettlement(rescue.SourceId)
                        : null;
                CharacterLifeTickResult result =
                    character.TickFormalRescue(
                        characterDelta,
                        globallyPaused || characterSteps == 0,
                        rescueSource,
                        rescueSettlement?.StableId,
                        rescueSettlement?.X ?? 0,
                        rescueSettlement?.Y ?? 0,
                        ruleTick);
                if (result.ReleasedBiomass > 0)
                {
                    primaryStorage.TryCommitBatch(
                        Array.Empty<ResourceAmount>(),
                        new[]
                        {
                            new ResourceAmount(
                                CharacterLifeRuntime.RescueResourceId,
                                result.ReleasedBiomass),
                        });
                }
                if (result.Kind == CharacterLifeTickKind.Died &&
                    string.Equals(
                        character.Definition.Id.Value,
                        Politics.CurrentLeaderId,
                        StringComparison.Ordinal))
                {
                    Politics.TryHandleCurrentLeaderDeath(out _);
                }
            }
            Diplomacy.Tick(delta, globallyPaused);
        }

        public bool TryApplyCharacterDamage(
            string characterId,
            int rawDamage,
            string causeId,
            out bool enteredDowned)
        {
            CharacterLifeRuntime character = FindCharacter(characterId);
            if (character == null)
            {
                enteredDowned = false;
                return false;
            }
            return character.TryApplyDamageAtRuleTick(
                rawDamage,
                causeId,
                ruleTick,
                out enteredDowned);
        }

        public void RestoreRuleTick(ulong value)
        {
            ruleTick = value;
        }

        public bool IsKnownSquad(string stableSquadId)
        {
            return string.Equals(
                stableSquadId,
                SingleCityArmyModel.DefaultSquadId,
                StringComparison.Ordinal);
        }

        public bool IsNonDormant(string stableSquadId)
        {
            if (!IsKnownSquad(stableSquadId)) return false;
            if (Expedition.Status == ArmyExpeditionStatus.Outbound ||
                Expedition.Status == ArmyExpeditionStatus.Returning)
                return false;
            IReadOnlyList<ArmyUnitSnapshot> units = Army.Units;
            for (var index = 0; index < units.Count; index++)
                if (units[index].IsActive) return true;
            return false;
        }

        public bool TryConsumeConvoyInterceptionImmunity()
        {
            return Diplomacy.TryConsumeConvoyInterceptionImmunity();
        }

        public void RestoreNextExpeditionOrdinal(int value)
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            nextExpeditionOrdinal = value;
        }

        public void ReplaceExpeditionForRestore(ArmyExpeditionModel value)
        {
            Expedition = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void ReplaceDiplomacyForRestore(DiplomacyRuntime value)
        {
            Diplomacy = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void EnsureDiplomacySession(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) ||
                string.Equals(
                    Diplomacy.SessionId,
                    sessionId,
                    StringComparison.Ordinal))
                return;
            DiplomacyRuntimeSnapshot snapshot = Diplomacy.Capture();
            bool untouched = snapshot.NextOfferOrdinal == 1ul &&
                snapshot.ConvoyInterceptionImmunityCharges == 0;
            for (var index = 0; index < snapshot.Factions.Count; index++)
            {
                untouched &= snapshot.Factions[index].State ==
                    DiplomacyRelationshipState.Unknown;
            }
            if (untouched) Diplomacy = new DiplomacyRuntime(sessionId);
        }
    }
}
