using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;
using WasteCity.Research;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Persistence
{
    public enum FormalSaveValidationError
    {
        None,
        MissingRequiredValue,
        InvalidArray,
        NonFiniteNumber,
        NegativeValue,
        InvalidBackpack,
        DuplicateStableId,
        MissingStableReference,
        InvalidHighWaterMark,
        InvalidEnumValue,
        InvalidWorld,
        InvalidStableId,
        InvalidResearch,
        InvalidDefense,
        InvalidEvacuation,
        DecodeFailure,
        UnsupportedFutureSchema,
        InvalidTimestamp,
        PayloadHashMismatch,
    }

    public sealed class FormalSaveValidationResult
    {
        private FormalSaveValidationResult(
            bool isValid,
            FormalSavePayloadKind payloadKind,
            FormalSaveValidationError error,
            string fieldPath,
            string message)
        {
            IsValid = isValid;
            PayloadKind = payloadKind;
            Error = error;
            FieldPath = fieldPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }
        public FormalSavePayloadKind PayloadKind { get; }
        public FormalSaveValidationError Error { get; }
        public string FieldPath { get; }
        public string Message { get; }

        internal static FormalSaveValidationResult Valid(
            FormalSavePayloadKind payloadKind)
        {
            return new FormalSaveValidationResult(
                true,
                payloadKind,
                FormalSaveValidationError.None,
                string.Empty,
                string.Empty);
        }

        internal static FormalSaveValidationResult Invalid(
            FormalSaveValidationError error,
            string fieldPath,
            string message)
        {
            return new FormalSaveValidationResult(
                false,
                FormalSavePayloadKind.None,
                error,
                fieldPath,
                message);
        }
    }

    public static class FormalSaveValidator
    {
        private static readonly Regex StableIdPattern = new Regex(
            "^[a-z0-9]+(?:[.-][a-z0-9]+){2,}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly string[][] RequiredSourceArrays =
        {
            new[] { "contentSources" },
            new[] { "checkpoint", "completedMilestoneIds" },
            new[] { "formal3D", "world", "resourceNodes" },
            new[] { "formal3D", "world", "orphanResources" },
            new[] { "formal3D", "buildings", "instances" },
            new[] { "formal3D", "storage", "coreAmounts" },
            new[] { "formal3D", "storage", "warehouses" },
            new[] { "formal3D", "storage", "orphanResources" },
            new[] { "formal3D", "backpack", "slots" },
            new[] { "formal3D", "crafting", "executions" },
            new[] { "formal3D", "research", "completedResearchIds" },
            new[] { "formal3D", "production", "states" },
            new[] { "formal3D", "defense", "towers" },
            new[] { "formal3D", "defense", "enemies" },
            new[] { "formal3D", "evacuation", "work" },
            new[] { "formal3D", "evacuation", "fullQueueStableInstanceIds" },
            new[] { "formal3D", "evacuation", "runtimePayloads" },
            new[] { "formal3D", "evacuation", "lockedStableInstanceIds" },
            new[] { "formal3D", "evacuation", "pendingRollbackStableInstanceIds" },
        };

        private static readonly string[][] RequiredProgressionSourceArrays =
        {
            new[] { "formal3D", "progression", "attention", "history" },
            new[] { "formal3D", "progression", "attention", "reachedThresholds" },
            new[] { "formal3D", "progression", "attention", "committedStableEventKeys" },
            new[] { "formal3D", "progression", "attention", "completedOneShotReasonIds" },
            new[] { "formal3D", "progression", "fate", "offeredIds" },
            new[] { "formal3D", "progression", "pressure", "entries" },
            new[] { "formal3D", "progression", "civilization", "committedAscensionIds" },
        };

        public static FormalSaveValidationResult ValidateDecoded(
            FormalSaveDecodeResult decoded)
        {
            if (decoded == null)
                return Invalid(
                    FormalSaveValidationError.DecodeFailure,
                    "save");
            if (!decoded.Success)
                return decoded.Error ==
                        FormalSaveDecodeError.UnsupportedFutureSchema
                    ? Invalid(
                        FormalSaveValidationError.UnsupportedFutureSchema,
                        "saveSchemaVersion")
                    : Invalid(
                        FormalSaveValidationError.DecodeFailure,
                        "save");
            if (decoded.PayloadKind == FormalSavePayloadKind.Legacy2D)
                return ValidateLegacy(decoded.Legacy2D);
            if (decoded.PayloadKind != FormalSavePayloadKind.Formal3D)
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "save");

            string source = decoded.SourceJson;
            if (!string.IsNullOrEmpty(source))
            {
                for (int index = 0;
                     index < RequiredSourceArrays.Length;
                     index++)
                {
                    string[] path = RequiredSourceArrays[index];
                    if (!HasJsonPath(source, path))
                    {
                        return Invalid(
                            FormalSaveValidationError.MissingRequiredValue,
                            string.Join(".", path));
                    }
                }
                if (ReadSourceSchemaVersion(source) ==
                    FormalSaveEnvelope.CurrentSchemaVersion)
                {
                    for (int index = 0;
                         index < RequiredProgressionSourceArrays.Length;
                         index++)
                    {
                        string[] path = RequiredProgressionSourceArrays[index];
                        if (!HasJsonPath(source, path))
                            return Invalid(
                                FormalSaveValidationError.MissingRequiredValue,
                                string.Join(".", path));
                    }
                }
                if (TryFindJsonPath(
                        source,
                        new[] { "formal3D", "defenseCampaign" },
                        out _,
                        out _))
                {
                    string[][] campaignArrays =
                    {
                        new[] { "formal3D", "defenseCampaign", "plannedEnemyCountsByEnemyId" },
                        new[] { "formal3D", "defenseCampaign", "spawnedEnemyCountsByEnemyId" },
                        new[] { "formal3D", "defenseCampaign", "defeatedEnemyCountsByEnemyId" },
                        new[] { "formal3D", "defenseCampaign", "frozenSpawnAnchors" },
                        new[] { "formal3D", "defenseCampaign", "towerCombatStates" },
                        new[] { "formal3D", "defenseCampaign", "enemyStates" },
                        new[] { "formal3D", "defenseCampaign", "buildingHealthStates" },
                        new[] { "formal3D", "defenseCampaign", "statistics", "killsByEnemyId" },
                        new[] { "formal3D", "defenseCampaign", "statistics", "buildingLossesByBuildingId" },
                        new[] { "formal3D", "defenseCampaign", "statistics", "damageByTowerBuildingId" },
                        new[] { "formal3D", "defenseCampaign", "statistics", "consumablesSpentByResourceId" },
                    };
                    for (int index = 0;
                         index < campaignArrays.Length;
                         index++)
                    {
                        if (!HasJsonPath(source, campaignArrays[index]))
                            return Invalid(
                                FormalSaveValidationError.MissingRequiredValue,
                                string.Join(".", campaignArrays[index]));
                    }
                }
                if (TryFindJsonPath(
                        source,
                        new[]
                        {
                            "formal3D", "progression", "pressure",
                            "activeCampaign",
                        },
                        out int pressureStart,
                        out int pressureEnd) &&
                    SkipWhitespace(source, pressureStart, pressureEnd) <
                        pressureEnd &&
                    source[SkipWhitespace(
                        source, pressureStart, pressureEnd)] == '{')
                {
                    string[][] pressureArrays =
                    {
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "plannedEnemyCountsByEnemyId" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "spawnedEnemyCountsByEnemyId" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "defeatedEnemyCountsByEnemyId" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "frozenSpawnAnchors" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "enemyStates" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "injectedReinforcements" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "statistics", "killsByEnemyId" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "statistics", "buildingLossesByBuildingId" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "statistics", "damageByTowerBuildingId" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "statistics", "killsByTowerBuildingId" },
                        new[] { "formal3D", "progression", "pressure", "activeCampaign", "statistics", "consumablesSpentByResourceId" },
                    };
                    for (var index = 0; index < pressureArrays.Length; index++)
                        if (!HasJsonPath(source, pressureArrays[index]))
                            return Invalid(
                                FormalSaveValidationError.MissingRequiredValue,
                                string.Join(".", pressureArrays[index]));
                }
                FormalThreeDSaveData payload = decoded.Envelope.formal3D;
                FormalSaveValidationResult nestedArrays =
                    RequireArrayMembersForEveryItem(
                        source,
                        new[] { "formal3D", "storage", "warehouses" },
                        payload.storage.warehouses.Length,
                        new[] { "amounts" },
                        "formal3D.storage.warehouses");
                if (nestedArrays != null) return nestedArrays;
                nestedArrays = RequireArrayMembersForEveryItem(
                    source,
                    new[] { "formal3D", "crafting", "executions" },
                    payload.crafting.executions.Length,
                    new[] { "reservedInputs" },
                    "formal3D.crafting.executions");
                if (nestedArrays != null) return nestedArrays;
                nestedArrays = RequireArrayMembersForEveryItem(
                    source,
                    new[] { "formal3D", "production", "states" },
                    payload.production.states.Length,
                    new[]
                    {
                        "inputAmounts",
                        "reservedInputs",
                        "outputAmounts",
                    },
                    "formal3D.production.states");
                if (nestedArrays != null) return nestedArrays;
                nestedArrays = RequireArrayMembersForEveryItem(
                    source,
                    new[]
                    {
                        "formal3D",
                        "evacuation",
                        "runtimePayloads",
                    },
                    payload.evacuation.runtimePayloads.Length,
                    new[]
                    {
                        "productionInputAmounts",
                        "productionReservedInputs",
                        "productionOutputAmounts",
                        "resourcePayload",
                    },
                    "formal3D.evacuation.runtimePayloads");
                if (nestedArrays != null) return nestedArrays;
            }
            return ValidateEnvelope(decoded.Envelope);
        }

        public static FormalSaveValidationResult ValidateLegacy(
            FormalSaveData legacy)
        {
            if (legacy == null || legacy.schema < 1 || legacy.schema > 30)
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "legacy2D");
            return FormalSaveValidationResult.Valid(
                FormalSavePayloadKind.Legacy2D);
        }

        public static FormalSaveValidationResult ValidateEnvelope(
            FormalSaveEnvelope envelope)
        {
            FormalSaveValidationResult result = ValidateEnvelopeHeader(envelope);
            if (result != null) return result;

            FormalThreeDSaveData data = envelope.formal3D;
            result = RequireDomains(data);
            if (result != null) return result;

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            result = ValidateWorld(data.world, nodeIds);
            if (result != null) return result;
            result = ValidateCity(data.city, data.world);
            if (result != null) return result;

            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            result = ValidateBuildings(data.buildings, nodeIds, buildingIds);
            if (result != null) return result;
            result = ValidateStorage(data.storage, buildingIds);
            if (result != null) return result;
            result = ValidateBackpack(data.backpack);
            if (result != null) return result;
            result = ValidateCrafting(data.crafting);
            if (result != null) return result;
            result = ValidateResearch(
                data.research,
                data.progression.civilization);
            if (result != null) return result;
            result = ValidateProduction(data.production, buildingIds, nodeIds);
            if (result != null) return result;
            result = ValidateDefense(data.defense, buildingIds);
            if (result != null) return result;
            result = ValidateDefenseCampaign(
                data.defenseCampaign,
                buildingIds);
            if (result != null) return result;
            result = ValidateEvacuation(data.evacuation, buildingIds);
            if (result != null) return result;
            result = ValidateProgression(data.progression);
            if (result != null) return result;
            result = ValidateCivilizationExpansion(
                data.civilizationExpansion);
            if (result != null) return result;

            string computedHash =
                FormalSaveCodec.ComputePayloadHashSha256(data);
            if (!string.Equals(
                    envelope.payloadHashSha256,
                    computedHash,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.PayloadHashMismatch,
                    "payloadHashSha256");

            return FormalSaveValidationResult.Valid(
                FormalSavePayloadKind.Formal3D);
        }

        private static FormalSaveValidationResult ValidateEnvelopeHeader(
            FormalSaveEnvelope envelope)
        {
            if (envelope == null)
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "save");
            if (envelope.saveSchemaVersion !=
                    FormalSaveEnvelope.CurrentSchemaVersion)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    "saveSchemaVersion");
            if (!string.Equals(
                    envelope.runtimeKind,
                    FormalSaveEnvelope.FormalThreeDRuntimeKind,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    "runtimeKind");
            if (string.IsNullOrWhiteSpace(envelope.gameVersion))
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "gameVersion");
            DateTime createdAt;
            DateTime updatedAt;
            if (!TryParseUtcRoundTrip(envelope.createdAt, out createdAt))
                return Invalid(
                    FormalSaveValidationError.InvalidTimestamp,
                    "createdAt");
            if (!TryParseUtcRoundTrip(envelope.updatedAt, out updatedAt) ||
                updatedAt < createdAt)
                return Invalid(
                    FormalSaveValidationError.InvalidTimestamp,
                    "updatedAt");
            if (envelope.contentSources == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    "contentSources");
            if (envelope.contentSources.Length == 0)
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "contentSources");
            if (envelope.checkpoint == null)
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "checkpoint");
            if (envelope.checkpoint.completedMilestoneIds == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    "checkpoint.completedMilestoneIds");
            if (envelope.checkpoint.sequence < 0)
                return Invalid(
                    FormalSaveValidationError.NegativeValue,
                    "checkpoint.sequence");
            FormalSaveValidationResult result = NonNegativeFinite(
                envelope.checkpoint.ruleTimeSeconds,
                "checkpoint.ruleTimeSeconds");
            if (result != null) return result;
            if (string.IsNullOrWhiteSpace(envelope.checkpoint.reasonId))
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "checkpoint.reasonId");
            result = UniqueNonBlank(
                envelope.contentSources,
                "contentSources");
            if (result != null) return result;
            result = UniqueNonBlank(
                envelope.checkpoint.completedMilestoneIds,
                "checkpoint.completedMilestoneIds");
            if (result != null) return result;
            if (envelope.formal3D == null)
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "formal3D");
            return null;
        }

        private static FormalSaveValidationResult RequireDomains(
            FormalThreeDSaveData data)
        {
            if (string.IsNullOrWhiteSpace(data.sessionId))
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    "formal3D.sessionId");
            if (data.world == null) return Missing("formal3D.world");
            if (data.city == null) return Missing("formal3D.city");
            if (data.buildings == null) return Missing("formal3D.buildings");
            if (data.storage == null) return Missing("formal3D.storage");
            if (data.backpack == null) return Missing("formal3D.backpack");
            if (data.crafting == null) return Missing("formal3D.crafting");
            if (data.research == null) return Missing("formal3D.research");
            if (data.production == null) return Missing("formal3D.production");
            if (data.defense == null) return Missing("formal3D.defense");
            if (data.defenseCampaign == null)
                return Missing("formal3D.defenseCampaign");
            if (data.evacuation == null) return Missing("formal3D.evacuation");
            if (data.pause == null) return Missing("formal3D.pause");
            if (data.progression == null)
                return Missing("formal3D.progression");
            if (data.civilizationExpansion == null)
                return Missing("formal3D.civilizationExpansion");
            return null;
        }

        private static FormalSaveValidationResult
            ValidateCivilizationExpansion(
                FormalThreeDCivilizationExpansionSaveData expansion)
        {
            const string path = "formal3D.civilizationExpansion";
            if (!string.Equals(
                    expansion.configurationSignature,
                    FormalThreeDCivilizationExpansionSaveData
                        .ConfigurationSignature,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".configurationSignature");
            if (expansion.armyLeader == null)
                return Missing(path + ".armyLeader");
            if (expansion.worldLayer == null)
                return Missing(path + ".worldLayer");
            if (expansion.charactersPolitics == null)
                return Missing(path + ".charactersPolitics");
            if (expansion.armyLeader.leader == null)
                return Missing(path + ".armyLeader.leader");
            if (expansion.armyLeader.units == null ||
                expansion.armyLeader.squads == null ||
                expansion.armyLeader.manufacturing == null ||
                expansion.armyLeader.losses == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".armyLeader");
            if (expansion.worldLayer.settlements == null ||
                expansion.worldLayer.convoys == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".worldLayer");
            if (!IsStableId(expansion.worldLayer.primaryCityId) ||
                !IsStableId(expansion.worldLayer.focusedSettlementId) ||
                !IsStableId(expansion.worldLayer.controlledCityId))
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".worldLayer");
            if (expansion.charactersPolitics.characters == null ||
                expansion.charactersPolitics.corpses == null ||
                expansion.charactersPolitics.internalFactions == null ||
                expansion.charactersPolitics.externalFactions == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".charactersPolitics");
            if (!IsStableId(
                    expansion.charactersPolitics.currentLeaderId))
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".charactersPolitics.currentLeaderId");
            FormalSaveValidationResult result = ValidateExpansionArmy(
                expansion.armyLeader,
                path + ".armyLeader");
            if (result != null) return result;
            result = ValidateExpansionWorld(
                expansion.worldLayer,
                path + ".worldLayer");
            if (result != null) return result;
            return ValidateExpansionPolitics(
                expansion.charactersPolitics,
                path + ".charactersPolitics");
        }

        private static FormalSaveValidationResult ValidateExpansionArmy(
            FormalThreeDArmyLeaderSaveData army,
            string path)
        {
            if (army.nextUnitOrdinal < 1 ||
                army.nextSquadOrdinal != 2 ||
                army.nextExpeditionOrdinal < 1 ||
                army.units.Length > SingleCityArmyModel
                    .DefaultSquadMaximumUnits ||
                army.leaderHealthy && !army.leaderAssigned)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path);
            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < army.units.Length; index++)
            {
                FormalThreeDArmyUnitSaveData unit = army.units[index];
                string item = path + ".units[" + index + "]";
                ArmyUnitDefinition definition = unit == null
                    ? null
                    : ArmyUnitCatalog.Find(unit.definitionId);
                if (unit == null || !IsStableId(unit.stableUnitId) ||
                    !unitIds.Add(unit.stableUnitId) || definition == null ||
                    !string.Equals(
                        unit.squadId,
                        SingleCityArmyModel.DefaultSquadId,
                        StringComparison.Ordinal) ||
                    unit.currentHealth <= 0 ||
                    unit.currentHealth > definition.MaximumHealth ||
                    !IsFinite(unit.maintenanceElapsedSeconds) ||
                    unit.maintenanceElapsedSeconds < 0f ||
                    unit.maintenanceElapsedSeconds >
                        definition.MaintenanceSeconds)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        item);
            }
            if (army.units.Length > 0 && army.squads.Length != 1)
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path + ".squads");
            if (army.squads.Length > 0)
            {
                FormalThreeDArmySquadSaveData squad = army.squads[0];
                if (squad == null || !string.Equals(
                        squad.stableSquadId,
                        SingleCityArmyModel.DefaultSquadId,
                        StringComparison.Ordinal) ||
                    !Enum.IsDefined(
                        typeof(FriendlySquadCommandType),
                        squad.command) ||
                    squad.hasExpeditionTarget !=
                        (squad.command == (int)
                            FriendlySquadCommandType.Expedition) ||
                    squad.unitIds == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".squads[0]");
                var squadIds = new HashSet<string>(StringComparer.Ordinal);
                for (var index = 0; index < squad.unitIds.Length; index++)
                {
                    if (!unitIds.Contains(squad.unitIds[index]) ||
                        !squadIds.Add(squad.unitIds[index]))
                        return Invalid(
                            FormalSaveValidationError
                                .MissingStableReference,
                            path + ".squads[0].unitIds");
                }
                if (squadIds.Count != unitIds.Count)
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        path + ".squads[0].unitIds");
            }
            if (army.expedition != null &&
                army.expedition.phase !=
                    (int)ArmyExpeditionStatus.Idle &&
                (!Enum.IsDefined(
                     typeof(ArmyExpeditionStatus),
                     army.expedition.phase) ||
                 !string.Equals(
                     army.expedition.squadId,
                     SingleCityArmyModel.DefaultSquadId,
                     StringComparison.Ordinal) ||
                 army.expedition.expeditionOrdinal < 1 ||
                 army.expedition.units == null ||
                 army.expedition.pendingLoot == null ||
                 army.expedition.enemyDefinitionIds == null ||
                 army.expedition.casualtyStableUnitIds == null))
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".expedition");
            return null;
        }

        private static FormalSaveValidationResult ValidateExpansionWorld(
            FormalThreeDWorldLayerSaveData world,
            string path)
        {
            if (!string.Equals(
                    world.primaryCityId,
                    WorldLayerCatalog.PrimaryCity.Id,
                    StringComparison.Ordinal) ||
                world.nextSettlementOrdinal != 3 ||
                world.nextConvoyOrdinal < 1)
                return Invalid(
                    FormalSaveValidationError.InvalidWorld,
                    path);
            if (world.settlements.Length == 0)
                return world.convoys.Length == 0
                    ? null
                    : Invalid(
                        FormalSaveValidationError
                            .MissingStableReference,
                        path + ".convoys");
            var settlements = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < world.settlements.Length; index++)
            {
                FormalThreeDSettlementSaveData item =
                    world.settlements[index];
                if (item == null || !IsStableId(item.stableSettlementId) ||
                    !settlements.Add(item.stableSettlementId) ||
                    !Enum.IsDefined(typeof(SettlementKind), item.kind) ||
                    !Enum.IsDefined(
                        typeof(SettlementAutonomyTemplate),
                        item.autonomousTemplate) ||
                    item.population < 0 ||
                    item.populationCapacity < item.population ||
                    item.loyalty < 0 || item.loyalty > 100 ||
                    item.inventory == null ||
                    !IsFinite(item.productionRemainingSeconds) ||
                    item.productionRemainingSeconds < 0f)
                    return Invalid(
                        FormalSaveValidationError.InvalidWorld,
                        path + ".settlements[" + index + "]");
            }
            if (!settlements.Contains(world.primaryCityId) ||
                !settlements.Contains(world.focusedSettlementId) ||
                !settlements.Contains(world.controlledCityId))
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path);
            var convoys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < world.convoys.Length; index++)
            {
                FormalThreeDConvoySaveData item = world.convoys[index];
                if (item == null || !IsStableId(item.stableConvoyId) ||
                    !convoys.Add(item.stableConvoyId) ||
                    !settlements.Contains(item.sourceSettlementId) ||
                    !settlements.Contains(item.destinationSettlementId) ||
                    string.Equals(
                        item.sourceSettlementId,
                        item.destinationSettlementId,
                        StringComparison.Ordinal) ||
                    !Enum.IsDefined(typeof(ConvoyStatus), item.status) ||
                    item.path == null || item.cargo == null ||
                    item.completedPathCells < 0 ||
                    item.completedPathCells > item.path.Length ||
                    !IsFinite(item.segmentProgressSeconds) ||
                    item.segmentProgressSeconds < 0f)
                    return Invalid(
                        FormalSaveValidationError.InvalidWorld,
                        path + ".convoys[" + index + "]");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateExpansionPolitics(
            FormalThreeDCharactersPoliticsSaveData politics,
            string path)
        {
            if (politics.characters.Length == 0)
            {
                return politics.corpses.Length == 0 &&
                       politics.internalFactions.Length == 0 &&
                       politics.externalFactions.Length == 0
                    ? null
                    : Invalid(
                        FormalSaveValidationError
                            .MissingStableReference,
                        path);
            }
            if (politics.characters.Length != CharacterCatalog.All.Count ||
                politics.internalFactions.Length !=
                    InternalFactionCatalog.All.Count ||
                politics.externalFactions.Length !=
                    ExternalFactionCatalog.All.Count ||
                politics.nextOfferOrdinal < 1 ||
                politics.convoyInterceptionImmunityCharges < 0)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path);
            var characterIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < politics.characters.Length; index++)
            {
                FormalThreeDCharacterSaveData item =
                    politics.characters[index];
                if (item == null || !IsStableId(item.characterId) ||
                    !characterIds.Add(item.characterId) ||
                    !Enum.IsDefined(typeof(CharacterLifeState), item.state) ||
                    item.currentHealth < 0 ||
                    item.currentHealth > item.maximumHealth ||
                    item.loyalty < 0 || item.loyalty > 100 ||
                    item.permanentInjuryIds == null ||
                    item.equipmentIds == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".characters[" + index + "]");
            }
            if (!characterIds.Contains(politics.currentLeaderId) ||
                !string.IsNullOrWhiteSpace(
                    politics.designatedSuccessorId) &&
                !characterIds.Contains(politics.designatedSuccessorId))
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path + ".currentLeaderId");
            var factionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 index < politics.internalFactions.Length;
                 index++)
            {
                FormalThreeDInternalFactionSaveData item =
                    politics.internalFactions[index];
                if (item == null || !IsStableId(item.factionId) ||
                    !factionIds.Add(item.factionId) ||
                    item.influence < 0 || item.influence > 100 ||
                    item.loyalty < 0 || item.loyalty > 100 ||
                    item.candidateSupports == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".internalFactions[" + index + "]");
            }
            factionIds.Clear();
            for (var index = 0;
                 index < politics.externalFactions.Length;
                 index++)
            {
                FormalThreeDExternalFactionSaveData item =
                    politics.externalFactions[index];
                if (item == null || !IsStableId(item.factionId) ||
                    !factionIds.Add(item.factionId) ||
                    item.relation < DiplomacyRuntime.MinimumRelation ||
                    item.relation > DiplomacyRuntime.MaximumRelation ||
                    !Enum.IsDefined(
                        typeof(DiplomacyRelationshipState),
                        item.state) ||
                    !IsFinite(item.offerCooldownRemainingSeconds) ||
                    item.offerCooldownRemainingSeconds < 0f ||
                    item.offerCooldownRemainingSeconds >
                        DiplomacyRuntime.OfferRefreshSeconds)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".externalFactions[" + index + "]");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateProgression(
            FormalThreeDProgressionSaveData progression)
        {
            const string path = "formal3D.progression";
            if (!string.Equals(
                    progression.configurationSignature,
                    FormalThreeDProgressionSaveData.ConfigurationSignature,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".configurationSignature");
            if (progression.attention == null)
                return Missing(path + ".attention");
            if (progression.fate == null)
                return Missing(path + ".fate");
            if (progression.fateEffects == null)
                return Missing(path + ".fateEffects");
            if (progression.pressure == null)
                return Missing(path + ".pressure");
            if (progression.civilization == null)
                return Missing(path + ".civilization");

            FormalSaveValidationResult result = ValidateAttention(
                progression.attention,
                path + ".attention");
            if (result != null) return result;
            result = ValidateFate(progression.fate, path + ".fate");
            if (result != null) return result;
            result = ValidateCivilization(
                progression.civilization,
                progression.fate,
                path + ".civilization");
            if (result != null) return result;
            result = ValidateFateEffects(
                progression.fate,
                progression.fateEffects,
                path + ".fateEffects");
            if (result != null) return result;
            result = ValidatePressure(
                progression.pressure,
                path + ".pressure");
            if (result != null) return result;
            return null;
        }

        private static FormalSaveValidationResult ValidatePressure(
            FormalThreeDAttentionPressureSaveData data,
            string path)
        {
            if (data.entries == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".entries");
            var entries = new AttentionPressureEntrySnapshot[
                data.entries.Length];
            AttentionPressureEntrySnapshot active = null;
            for (var index = 0; index < entries.Length; index++)
            {
                FormalThreeDAttentionPressureEntrySaveData item =
                    data.entries[index];
                string itemPath = path + ".entries[" + index + "]";
                if (item == null || !Enum.IsDefined(
                        typeof(AttentionPressureState), item.state))
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        itemPath + ".state");
                entries[index] = new AttentionPressureEntrySnapshot(
                    item.threshold,
                    (AttentionPressureState)item.state,
                    item.warningRemainingSeconds);
                if (entries[index].State == AttentionPressureState.Active)
                    active = entries[index];
            }
            var validator = new AttentionPressureRuntime();
            if (!validator.TryRestore(new AttentionPressureSnapshot(
                    data.revision, entries), out _))
                return Invalid(FormalSaveValidationError.InvalidDefense, path);

            bool hasCampaign = data.activeCampaign != null &&
                !string.IsNullOrEmpty(data.activeCampaign.campaignId);
            if ((active != null) != hasCampaign)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".activeCampaign");
            if (!hasCampaign)
                return string.IsNullOrEmpty(data.activeEncounterId)
                    ? null
                    : Invalid(FormalSaveValidationError.InvalidDefense,
                        path + ".activeEncounterId");
            FormalThreeDPressureCampaignSaveData campaign =
                data.activeCampaign;
            if (string.IsNullOrWhiteSpace(data.activeEncounterId) ||
                !string.Equals(active.EncounterId,
                    data.activeEncounterId, StringComparison.Ordinal) ||
                !string.Equals(campaign.campaignId,
                    data.activeEncounterId, StringComparison.Ordinal) ||
                AttentionPressureCampaignCatalog.Find(
                    data.activeEncounterId) == null)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".activeEncounterId");
            if (campaign.plannedEnemyCountsByEnemyId == null ||
                campaign.spawnedEnemyCountsByEnemyId == null ||
                campaign.defeatedEnemyCountsByEnemyId == null ||
                campaign.frozenSpawnAnchors == null ||
                campaign.enemyStates == null || campaign.statistics == null ||
                campaign.injectedReinforcements == null ||
                campaign.statistics.killsByEnemyId == null ||
                campaign.statistics.buildingLossesByBuildingId == null ||
                campaign.statistics.damageByTowerBuildingId == null ||
                campaign.statistics.killsByTowerBuildingId == null ||
                campaign.statistics.consumablesSpentByResourceId == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".activeCampaign");
            for (var index = 0;
                 index < campaign.injectedReinforcements.Length;
                 index++)
            {
                FormalThreeDPressureInjectedReinforcementSaveData item =
                    campaign.injectedReinforcements[index];
                if (item == null || string.IsNullOrWhiteSpace(
                        item.stableEventId) || item.entries == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        path + ".activeCampaign.injectedReinforcements[" +
                        index + "]");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateAttention(
            FormalThreeDAttentionSaveData attention,
            string path)
        {
            if (attention.value < FormalAttentionCatalog.MinimumValue ||
                attention.value > FormalAttentionCatalog.MaximumValue)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".value");
            if (attention.history == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".history");
            if (attention.history.Length >
                FormalAttentionCatalog.HistoryCapacity)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".history");
            if (attention.reachedThresholds == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".reachedThresholds");
            if (attention.committedStableEventKeys == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".committedStableEventKeys");
            if (attention.completedOneShotReasonIds == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".completedOneShotReasonIds");

            FormalSaveValidationResult result = ValidateThresholds(
                attention.reachedThresholds,
                path + ".reachedThresholds");
            if (result != null) return result;
            result = UniqueNonBlank(
                attention.committedStableEventKeys,
                path + ".committedStableEventKeys");
            if (result != null) return result;
            result = UniqueNonBlank(
                attention.completedOneShotReasonIds,
                path + ".completedOneShotReasonIds");
            if (result != null) return result;

            var committed = new HashSet<string>(
                attention.committedStableEventKeys,
                StringComparer.Ordinal);
            var completedOneShotReasons = new HashSet<string>(
                attention.completedOneShotReasonIds,
                StringComparer.Ordinal);
            var historyEventKeys = new HashSet<string>(
                StringComparer.Ordinal);
            ulong previousRevision = 0;
            for (int index = 0; index < attention.history.Length; index++)
            {
                FormalThreeDAttentionHistorySaveData entry =
                    attention.history[index];
                string item = path + ".history[" + index + "]";
                if (entry == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        item);
                if (!IsStableId(entry.reasonId))
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        item + ".reasonId");
                if (string.IsNullOrWhiteSpace(entry.stableEventKey))
                    return Missing(item + ".stableEventKey");
                if (!historyEventKeys.Add(entry.stableEventKey))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        item + ".stableEventKey");
                if (!committed.Contains(entry.stableEventKey))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        item + ".stableEventKey");
                if (entry.requestedDelta == 0 ||
                    !IsAppliedDeltaValid(
                        entry.requestedDelta,
                        entry.appliedDelta))
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        item + ".appliedDelta");
                if (entry.valueAfter < FormalAttentionCatalog.MinimumValue ||
                    entry.valueAfter > FormalAttentionCatalog.MaximumValue)
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        item + ".valueAfter");
                if (entry.revision == 0 ||
                    entry.revision <= previousRevision ||
                    entry.revision > attention.revision)
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        item + ".revision");
                result = NonNegativeFinite(
                    entry.ruleTimeSeconds,
                    item + ".ruleTimeSeconds");
                if (result != null) return result;
                if (!string.IsNullOrEmpty(entry.sourceInstanceId) &&
                    !IsStableId(entry.sourceInstanceId))
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        item + ".sourceInstanceId");
                FormalAttentionReasonDefinition reason =
                    FormalAttentionCatalog.Find(entry.reasonId);
                if (reason != null &&
                    reason.RepeatPolicy ==
                        FormalAttentionRepeatPolicy.OncePerSession &&
                    !completedOneShotReasons.Contains(entry.reasonId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        item + ".reasonId");
                previousRevision = entry.revision;
            }

            if (attention.history.Length == 0)
            {
                if (attention.revision != 0 ||
                    attention.reachedThresholds.Length != 0 ||
                    attention.committedStableEventKeys.Length != 0 ||
                    attention.completedOneShotReasonIds.Length != 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        path + ".revision");
            }
            else if (previousRevision != attention.revision ||
                     attention.history[attention.history.Length - 1]
                         .valueAfter != attention.value)
            {
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".revision");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateThresholds(
            int[] thresholds,
            string path)
        {
            int previous = 0;
            for (int index = 0; index < thresholds.Length; index++)
            {
                int value = thresholds[index];
                if (!IsAttentionThreshold(value) || value <= previous)
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        path + "[" + index + "]");
                previous = value;
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateFate(
            FormalThreeDFateSaveData fate,
            string path)
        {
            string[] expected =
            {
                FormalFateCatalog.PocketUniverseId,
                FormalFateCatalog.VoidDebtId,
                FormalFateCatalog.RewindAnchorId,
            };
            if (fate.offeredIds == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".offeredIds");
            if (fate.offeredIds.Length != expected.Length)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".offeredIds");
            for (int index = 0; index < expected.Length; index++)
            {
                if (!string.Equals(
                        fate.offeredIds[index],
                        expected[index],
                        StringComparison.Ordinal))
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        path + ".offeredIds[" + index + "]");
            }

            if (string.IsNullOrEmpty(fate.selectedId))
            {
                if (fate.level != 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        path + ".level");
                return null;
            }

            bool known = false;
            for (int index = 0; index < expected.Length; index++)
                known |= string.Equals(
                    fate.selectedId,
                    expected[index],
                    StringComparison.Ordinal);
            if (!known)
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".selectedId");
            if (fate.level != 1 && fate.level != 2)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".level");
            return null;
        }

        private static FormalSaveValidationResult ValidateCivilization(
            FormalThreeDCivilizationSaveData civilization,
            FormalThreeDFateSaveData fate,
            string path)
        {
            if (civilization.committedAscensionIds == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".committedAscensionIds");
            FormalSaveValidationResult unique = UniqueNonBlank(
                civilization.committedAscensionIds,
                path + ".committedAscensionIds");
            if (unique != null) return unique;
            if (!Enum.IsDefined(
                    typeof(AdvancementSequenceStage),
                    civilization.sequenceStage) ||
                float.IsNaN(civilization.remainingRuleSeconds) ||
                float.IsInfinity(civilization.remainingRuleSeconds) ||
                civilization.remainingRuleSeconds < 0f)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".sequenceStage");

            AdvancementSequenceStage stage =
                (AdvancementSequenceStage)civilization.sequenceStage;
            bool clean = civilization.level == 1 &&
                civilization.revision == 0ul &&
                string.IsNullOrEmpty(civilization.ascensionId) &&
                !civilization.ascensionCompleted &&
                civilization.committedAscensionIds.Length == 0 &&
                stage == AdvancementSequenceStage.None &&
                civilization.remainingRuleSeconds == 0f &&
                fate.level <= 1;
            if (clean) return null;

            bool committed = civilization.level == 2 &&
                civilization.revision > 0ul &&
                civilization.ascensionCompleted &&
                civilization.committedAscensionIds.Length == 1 &&
                string.Equals(
                    civilization.ascensionId,
                    FormalThreeDCivilizationSaveData.FirstAscensionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    civilization.committedAscensionIds[0],
                    FormalThreeDCivilizationSaveData.FirstAscensionId,
                    StringComparison.Ordinal) &&
                !string.IsNullOrEmpty(fate.selectedId) &&
                fate.level == 2 &&
                IsValidAscensionSequenceTime(
                    stage,
                    civilization.remainingRuleSeconds);
            if (!committed)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path);

            try
            {
                var runtime = new FormalCivilizationAscensionRuntime(
                    fate.selectedId);
                if (!runtime.TryRestore(
                        new FormalCivilizationAscensionSnapshot(
                            civilization.level,
                            fate.selectedId,
                            fate.level,
                            civilization.ascensionCompleted,
                            civilization.revision),
                        out _))
                {
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        path);
                }
            }
            catch (ArgumentException)
            {
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".ascensionId");
            }
            return null;
        }

        private static bool IsValidAscensionSequenceTime(
            AdvancementSequenceStage stage,
            float remaining)
        {
            switch (stage)
            {
                case AdvancementSequenceStage.Scanning:
                    return remaining > 0f && remaining <= 2.5f;
                case AdvancementSequenceStage.Confirmed:
                    return remaining > 0f && remaining <= 3f;
                case AdvancementSequenceStage.Warning:
                    return remaining > 0f && remaining <= 4f;
                case AdvancementSequenceStage.Results:
                case AdvancementSequenceStage.Continued:
                    return remaining == 0f;
                default:
                    return false;
            }
        }

        private static FormalSaveValidationResult ValidateFateEffects(
            FormalThreeDFateSaveData fate,
            FormalThreeDFateEffectsSaveData effects,
            string path)
        {
            if (effects.pocketUniverse == null)
                return Missing(path + ".pocketUniverse");
            if (effects.voidDebt == null)
                return Missing(path + ".voidDebt");
            if (effects.rewindAnchors == null)
                return Missing(path + ".rewindAnchors");
            FormalThreeDPocketUniverseSaveData pocket =
                effects.pocketUniverse;
            FormalThreeDVoidDebtSaveData debt = effects.voidDebt;
            FormalThreeDRewindAnchorMetadataSaveData rewind =
                effects.rewindAnchors;
            if (pocket.flagships == null ||
                pocket.collapsedFlagshipIds == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".pocketUniverse");
            if (debt.debts == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".voidDebt.debts");
            if (rewind.anchors == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".rewindAnchors.anchors");

            var flagships = new PocketUniverseFlagshipState[
                pocket.flagships.Length];
            for (var index = 0; index < flagships.Length; index++)
            {
                FormalThreeDPocketUniverseFlagshipSaveData item =
                    pocket.flagships[index];
                if (item == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".pocketUniverse.flagships[" + index + "]");
                flagships[index] = new PocketUniverseFlagshipState(
                    item.buildingDefinitionId,
                    item.stableInstanceId);
            }
            var pocketRuntime = new PocketUniverseFateEffect();
            if (!pocketRuntime.TryRestore(
                    new PocketUniverseFateSnapshot(
                        pocket.level,
                        pocket.revision,
                        flagships,
                        pocket.collapsedFlagshipIds,
                        pocket.firstProductionFlagshipId),
                    out _))
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".pocketUniverse");

            var debts = new FormalVoidDebtEntry[debt.debts.Length];
            for (var index = 0; index < debts.Length; index++)
            {
                FormalThreeDVoidDebtEntrySaveData item = debt.debts[index];
                if (item == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".voidDebt.debts[" + index + "]");
                debts[index] = new FormalVoidDebtEntry(
                    item.resourceId,
                    item.amount);
            }
            FormalVoidDebtRuntime debtRuntime;
            try
            {
                debtRuntime = new FormalVoidDebtRuntime(debt.level);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".voidDebt.level");
            }
            if (!debtRuntime.TryRestore(
                    new FormalVoidDebtSnapshot(
                        debt.level,
                        debt.settlementRemainingSeconds,
                        debt.nextSettlementOrdinal,
                        debt.revision,
                        debts),
                    out _))
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".voidDebt");

            bool pocketSelected = string.Equals(
                fate.selectedId,
                FormalFateCatalog.PocketUniverseId,
                StringComparison.Ordinal);
            bool debtSelected = string.Equals(
                fate.selectedId,
                FormalFateCatalog.VoidDebtId,
                StringComparison.Ordinal);
            bool rewindSelected = string.Equals(
                fate.selectedId,
                FormalFateCatalog.RewindAnchorId,
                StringComparison.Ordinal);
            int expectedPocketLevel = pocketSelected ? fate.level : 1;
            int expectedDebtLevel = debtSelected ? fate.level : 1;
            int expectedRewindCapacity = rewindSelected && fate.level == 2
                ? FormalRewindAnchorMetadataRuntime.MaximumAnchorsAtLevelTwo
                : FormalRewindAnchorMetadataRuntime.MaximumAnchorsAtLevelOne;
            if ((!pocketSelected &&
                 (pocket.flagships.Length != 0 ||
                  pocket.collapsedFlagshipIds.Length != 0 ||
                  !string.IsNullOrEmpty(pocket.firstProductionFlagshipId))) ||
                (!debtSelected &&
                 (debt.debts.Length != 0 ||
                  debt.settlementRemainingSeconds != 0d)) ||
                (!rewindSelected && rewind.anchors.Length != 0) ||
                pocket.level != expectedPocketLevel ||
                debt.level != expectedDebtLevel ||
                rewind.anchors.Length > expectedRewindCapacity ||
                rewind.nextCreationOrdinal <= 0L)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path);

            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            long previousCreation = 0L;
            for (var index = 0; index < rewind.anchors.Length; index++)
            {
                FormalThreeDRewindAnchorEntrySaveData item =
                    rewind.anchors[index];
                if (item == null ||
                    !IsStableId(item.stableAnchorId) ||
                    string.IsNullOrWhiteSpace(item.internalKey) ||
                    string.IsNullOrWhiteSpace(item.sessionId) ||
                    string.IsNullOrWhiteSpace(item.payloadHashSha256) ||
                    string.IsNullOrWhiteSpace(item.checkpointReasonId) ||
                    item.checkpointSequence < 0 ||
                    float.IsNaN(item.checkpointRuleTimeSeconds) ||
                    float.IsInfinity(item.checkpointRuleTimeSeconds) ||
                    item.checkpointRuleTimeSeconds < 0f ||
                    item.completedMilestoneIds == null ||
                    item.creationOrdinal <= previousCreation ||
                    !anchorIds.Add(item.stableAnchorId))
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".rewindAnchors.anchors[" + index + "]");
                previousCreation = item.creationOrdinal;
            }
            return null;
        }

        private static bool IsAppliedDeltaValid(
            int requested,
            int applied)
        {
            return requested > 0
                ? applied >= 0 && applied <= requested
                : applied <= 0 && applied >= requested;
        }

        private static bool IsAttentionThreshold(int candidate)
        {
            IReadOnlyList<int> thresholds = FormalAttentionCatalog.Thresholds;
            for (int index = 0; index < thresholds.Count; index++)
            {
                if (thresholds[index] == candidate)
                    return true;
            }
            return false;
        }

        private static FormalSaveValidationResult ValidateWorld(
            FormalThreeDWorldSaveData world,
            HashSet<string> nodeIds)
        {
            const string path = "formal3D.world";
            if (world.width <= 0)
                return Invalid(FormalSaveValidationError.InvalidWorld,
                    path + ".width");
            if (world.height <= 0)
                return Invalid(FormalSaveValidationError.InvalidWorld,
                    path + ".height");
            if (world.worldGenerationVersion < 0)
                return Invalid(FormalSaveValidationError.NegativeValue,
                    path + ".worldGenerationVersion");
            if (!IsStableId(world.worldDefinitionId))
                return Invalid(FormalSaveValidationError.InvalidStableId,
                    path + ".worldDefinitionId");
            if (world.resourceNodes == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".resourceNodes");
            if (world.orphanResources == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".orphanResources");
            for (int index = 0; index < world.resourceNodes.Length; index++)
            {
                FormalThreeDResourceNodeSaveData node =
                    world.resourceNodes[index];
                string item = path + ".resourceNodes[" + index + "]";
                if (node == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                FormalSaveValidationResult result = AddStableId(
                    nodeIds,
                    node.stableNodeId,
                    item + ".stableNodeId");
                if (result != null) return result;
                if (!IsStableId(node.resourceId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".resourceId");
                if (node.x < 0 || node.x >= world.width)
                    return Invalid(FormalSaveValidationError.InvalidWorld,
                        item + ".x");
                if (node.y < 0 || node.y >= world.height)
                    return Invalid(FormalSaveValidationError.InvalidWorld,
                        item + ".y");
                if (node.remainingAmount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".remainingAmount");
                if (node.isDepleted != (node.remainingAmount == 0))
                    return Invalid(FormalSaveValidationError.InvalidWorld,
                        item + ".isDepleted");
            }
            return ValidateOrphans(
                world.orphanResources,
                path + ".orphanResources");
        }

        private static FormalSaveValidationResult ValidateCity(
            FormalThreeDCitySaveData city,
            FormalThreeDWorldSaveData world)
        {
            const string path = "formal3D.city";
            FormalSaveValidationResult result = Finite(
                city.positionX,
                path + ".positionX");
            if (result != null) return result;
            result = Finite(city.positionZ, path + ".positionZ");
            if (result != null) return result;
            result = NonNegativeFinite(
                city.transitionRemainingSeconds,
                path + ".transitionRemainingSeconds");
            if (result != null) return result;
            if (city.cityMode < 0 || city.cityMode > 3)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".cityMode");
            if (city.transitionReturnMode != 0 &&
                city.transitionReturnMode != 2)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".transitionReturnMode");
            if (city.population < 0)
                return Invalid(FormalSaveValidationError.NegativeValue,
                    path + ".population");
            if (city.populationCapacity < 0)
                return Invalid(FormalSaveValidationError.NegativeValue,
                    path + ".populationCapacity");
            if (city.cellX < 0 || city.cellX >= world.width ||
                city.cellY < 0 || city.cellY >= world.height)
                return Invalid(FormalSaveValidationError.InvalidWorld,
                    path + ".cellX");
            if (city.autopilotActive &&
                (city.destinationX < 0 ||
                 city.destinationX >= world.width ||
                 city.destinationY < 0 ||
                 city.destinationY >= world.height))
                return Invalid(FormalSaveValidationError.InvalidWorld,
                    path + ".destinationX");
            return null;
        }

        private static FormalSaveValidationResult ValidateBuildings(
            FormalThreeDBuildingsSaveData buildings,
            HashSet<string> nodeIds,
            HashSet<string> buildingIds)
        {
            const string path = "formal3D.buildings";
            if (buildings.instances == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".instances");
            int maxOrdinal = 0;
            for (int index = 0; index < buildings.instances.Length; index++)
            {
                FormalThreeDBuildingInstanceSaveData instance =
                    buildings.instances[index];
                string item = path + ".instances[" + index + "]";
                if (instance == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                int ordinal;
                if (!TryGeneratedOrdinal(
                        instance.stableInstanceId,
                        "building.instance.",
                        out ordinal))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".stableInstanceId");
                if (!buildingIds.Add(instance.stableInstanceId))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item + ".stableInstanceId");
                maxOrdinal = Math.Max(maxOrdinal, ordinal);
                if (!IsStableId(instance.definitionId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".definitionId");
                if (instance.site < 0 || instance.site > 1)
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        item + ".site");
                if (instance.orientation < 0 || instance.orientation > 3)
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        item + ".orientation");
                if (instance.state < 0 || instance.state > 3)
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        item + ".state");
                FormalSaveValidationResult result = NonNegativeFinite(
                    instance.constructionRemainingSeconds,
                    item + ".constructionRemainingSeconds");
                if (result != null) return result;
                if (instance.state == 3)
                {
                    if (instance.isPlayerOwned)
                        return Invalid(
                            FormalSaveValidationError.InvalidDefense,
                            item + ".isPlayerOwned");
                    if (instance.evacuationLockedCrossCheck)
                        return Invalid(
                            FormalSaveValidationError.InvalidDefense,
                            item + ".evacuationLockedCrossCheck");
                    if (instance.constructionRemainingSeconds != 0f)
                        return Invalid(
                            FormalSaveValidationError.InvalidDefense,
                            item + ".constructionRemainingSeconds");
                }
                if (instance.footprintWidth <= 0 ||
                    instance.footprintHeight <= 0)
                    return Invalid(FormalSaveValidationError.InvalidWorld,
                        item + ".footprintWidth");
                if (!string.IsNullOrEmpty(instance.boundResourceNodeId))
                {
                    if (!IsStableId(instance.boundResourceNodeId))
                        return Invalid(FormalSaveValidationError.InvalidStableId,
                            item + ".boundResourceNodeId");
                    if (!nodeIds.Contains(instance.boundResourceNodeId))
                        return Invalid(
                            FormalSaveValidationError.MissingStableReference,
                            item + ".boundResourceNodeId");
                }
            }
            if (buildings.nextStableInstanceOrdinal <= maxOrdinal)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextStableInstanceOrdinal");
            return null;
        }

        private static FormalSaveValidationResult ValidateStorage(
            FormalThreeDStorageSaveData storage,
            HashSet<string> buildingIds)
        {
            const string path = "formal3D.storage";
            if (storage.coreAmounts == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".coreAmounts");
            if (storage.warehouses == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".warehouses");
            if (storage.orphanResources == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".orphanResources");
            FormalSaveValidationResult result = ValidateAmounts(
                storage.coreAmounts,
                path + ".coreAmounts");
            if (result != null) return result;
            var warehouseIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < storage.warehouses.Length; index++)
            {
                FormalThreeDWarehouseSaveData warehouse =
                    storage.warehouses[index];
                string item = path + ".warehouses[" + index + "]";
                if (warehouse == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                result = AddReference(
                    warehouseIds,
                    buildingIds,
                    warehouse.stableInstanceId,
                    item + ".stableInstanceId");
                if (result != null) return result;
                if (!string.IsNullOrEmpty(warehouse.filterResourceId) &&
                    !IsStableId(warehouse.filterResourceId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".filterResourceId");
                result = ValidateAmounts(
                    warehouse.amounts,
                    item + ".amounts");
                if (result != null) return result;
            }
            return ValidateOrphans(
                storage.orphanResources,
                path + ".orphanResources");
        }

        private static FormalSaveValidationResult ValidateBackpack(
            FormalThreeDBackpackSaveData backpack)
        {
            const string path = "formal3D.backpack.slots";
            if (backpack.slots == null || backpack.slots.Length != 30)
                return Invalid(FormalSaveValidationError.InvalidBackpack,
                    path);
            for (int index = 0; index < backpack.slots.Length; index++)
            {
                FormalThreeDBackpackSlotSaveData slot = backpack.slots[index];
                string item = path + "[" + index + "]";
                if (slot == null)
                    return Invalid(FormalSaveValidationError.InvalidBackpack,
                        item);
                if (slot.slotIndex != index)
                    return Invalid(FormalSaveValidationError.InvalidBackpack,
                        item + ".slotIndex");
                if (slot.amount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".amount");
                bool empty = string.IsNullOrEmpty(slot.resourceId);
                if (empty != (slot.amount == 0))
                    return Invalid(FormalSaveValidationError.InvalidBackpack,
                        item + ".resourceId");
                if (!empty && !IsStableId(slot.resourceId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".resourceId");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateCrafting(
            FormalThreeDCraftingSaveData crafting)
        {
            const string path = "formal3D.crafting";
            if (crafting.executions == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".executions");
            FormalSaveValidationResult result = NonNegativeFinite(
                crafting.activeProgressSeconds,
                path + ".activeProgressSeconds");
            if (result != null) return result;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int maxOrdinal = 0;
            for (int index = 0; index < crafting.executions.Length; index++)
            {
                FormalThreeDCraftingExecutionSaveData execution =
                    crafting.executions[index];
                string item = path + ".executions[" + index + "]";
                if (execution == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                int ordinal;
                if (!TryGeneratedOrdinal(
                        execution.stableExecutionId,
                        "craft.execution.",
                        out ordinal))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".stableExecutionId");
                if (!ids.Add(execution.stableExecutionId))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item + ".stableExecutionId");
                maxOrdinal = Math.Max(maxOrdinal, ordinal);
                if (!IsStableId(execution.recipeId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".recipeId");
                result = ValidateAmounts(
                    execution.reservedInputs,
                    item + ".reservedInputs");
                if (result != null) return result;
            }
            if (crafting.nextQueueOrdinal <= maxOrdinal)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextQueueOrdinal");
            return null;
        }

        private static FormalSaveValidationResult ValidateResearch(
            FormalThreeDResearchSaveData research,
            FormalThreeDCivilizationSaveData civilization)
        {
            const string path = "formal3D.research";
            int civilizationLevel = civilization?.level ?? 1;
            if (research.completedResearchIds == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".completedResearchIds");
            var completed = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0;
                 index < research.completedResearchIds.Length;
                 index++)
            {
                string id = research.completedResearchIds[index];
                string item = path + ".completedResearchIds[" + index + "]";
                if (!IsStableId(id))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item);
                if (!completed.Add(id))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item);
                if (CivilizationResearchAvailability.IsGated(id) &&
                    civilizationLevel <
                        CivilizationResearchAvailability
                            .RequiredCivilizationLevel)
                    return Invalid(FormalSaveValidationError.InvalidResearch,
                        item);
            }
            FormalSaveValidationResult result = NonNegativeFinite(
                research.remainingSeconds,
                path + ".remainingSeconds");
            if (result != null) return result;
            if (string.IsNullOrEmpty(research.activeResearchId))
                return research.remainingSeconds == 0f
                    ? null
                    : Invalid(FormalSaveValidationError.InvalidResearch,
                        path + ".remainingSeconds");
            if (!IsStableId(research.activeResearchId))
                return Invalid(FormalSaveValidationError.InvalidStableId,
                    path + ".activeResearchId");
            if (completed.Contains(research.activeResearchId))
                return Invalid(FormalSaveValidationError.InvalidResearch,
                    path + ".activeResearchId");
            if (CivilizationResearchAvailability.IsGated(
                    research.activeResearchId) && civilizationLevel <
                        CivilizationResearchAvailability
                            .RequiredCivilizationLevel)
                return Invalid(FormalSaveValidationError.InvalidResearch,
                    path + ".activeResearchId");
            return null;
        }

        private static FormalSaveValidationResult ValidateProduction(
            FormalThreeDProductionSaveData production,
            HashSet<string> buildingIds,
            HashSet<string> nodeIds)
        {
            const string path = "formal3D.production.states";
            if (production.states == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < production.states.Length; index++)
            {
                FormalThreeDProductionStateSaveData state =
                    production.states[index];
                string item = path + "[" + index + "]";
                if (state == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                FormalSaveValidationResult result = AddReference(
                    ids,
                    buildingIds,
                    state.stableInstanceId,
                    item + ".stableInstanceId");
                if (result != null) return result;
                if (!IsStableId(state.definitionId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".definitionId");
                result = ValidateAmounts(state.inputAmounts,
                    item + ".inputAmounts");
                if (result != null) return result;
                result = ValidateAmounts(state.reservedInputs,
                    item + ".reservedInputs");
                if (result != null) return result;
                result = ValidateAmounts(state.outputAmounts,
                    item + ".outputAmounts");
                if (result != null) return result;
                result = NonNegativeFinite(
                    state.progressSeconds,
                    item + ".progressSeconds");
                if (result != null) return result;
                if (!string.IsNullOrEmpty(state.boundResourceNodeId))
                {
                    if (!IsStableId(state.boundResourceNodeId))
                        return Invalid(FormalSaveValidationError.InvalidStableId,
                            item + ".boundResourceNodeId");
                    if (!nodeIds.Contains(state.boundResourceNodeId))
                        return Invalid(
                            FormalSaveValidationError.MissingStableReference,
                            item + ".boundResourceNodeId");
                }
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateDefense(
            FormalThreeDDefenseSaveData defense,
            HashSet<string> buildingIds)
        {
            const string path = "formal3D.defense";
            int tutorialEnemyCount = WaveCatalog.Tutorial.TotalCount;
            int ammunitionCapacity =
                DefenseTowerCatalog.MachineGunAmmunitionCapacity;
            float ammunitionDurationSeconds = DefenseTowerCatalog.For(
                BuildingCatalog.MachineGunTurret.Id.Value)
                .SecondsPerConsumable;
            const float fixedStepSeconds =
                TutorialDefenseRuntimeModel.FormalFixedStepSeconds;
            const int coreMaximumHealth =
                CityCoreCombatModel.FormalMaximumHealth;
            int tutorialCompletionDefeatCount = Math.Max(
                1,
                (int)Math.Ceiling(tutorialEnemyCount * .9f));
            float tutorialSpawnCadenceSeconds =
                WaveCatalog.Tutorial.SpawnSeconds /
                Math.Max(1, tutorialEnemyCount);
            if (defense.towers == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".towers");
            if (defense.enemies == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".enemies");
            if (string.IsNullOrWhiteSpace(defense.configurationSignature))
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    path + ".configurationSignature");
            FormalSaveValidationResult result = Finite(
                defense.spawnOriginX,
                path + ".spawnOriginX");
            if (result != null) return result;
            result = Finite(
                defense.spawnOriginZ,
                path + ".spawnOriginZ");
            if (result != null) return result;
            if (defense.wavePhase < 0 || defense.wavePhase > 3)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".wavePhase");
            int[] counts =
            {
                defense.tutorialWaveTriggerCount,
                defense.spawnedEnemyCount,
                defense.defeatedEnemyCount,
                defense.coreCurrentHealth,
            };
            string[] names =
            {
                "tutorialWaveTriggerCount",
                "spawnedEnemyCount",
                "defeatedEnemyCount",
                "coreCurrentHealth",
            };
            for (int index = 0; index < counts.Length; index++)
                if (counts[index] < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        path + "." + names[index]);
            float[] times =
            {
                defense.warningRemainingSeconds,
                defense.spawnClockSeconds,
                defense.fixedStepAccumulatorSeconds,
            };
            string[] timeNames =
            {
                "warningRemainingSeconds",
                "spawnClockSeconds",
                "fixedStepAccumulatorSeconds",
            };
            for (int index = 0; index < times.Length; index++)
            {
                result = NonNegativeFinite(
                    times[index], path + "." + timeNames[index]);
                if (result != null) return result;
            }
            if (defense.tutorialWaveTriggerCount > 1 ||
                defense.tutorialTriggered !=
                (defense.tutorialWaveTriggerCount == 1))
            {
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".tutorialWaveTriggerCount");
            }
            if (!string.IsNullOrEmpty(defense.randomState))
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".randomState");
            if (!defense.tutorialTriggered)
            {
                if (defense.wavePhase != 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".wavePhase");
                if (defense.spawnedEnemyCount != 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".spawnedEnemyCount");
                if (defense.defeatedEnemyCount != 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".defeatedEnemyCount");
                if (defense.nextEnemyOrdinal != 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".nextEnemyOrdinal");
                if (defense.enemies.Length != 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".enemies");
                if (defense.warningRemainingSeconds != 0f)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".warningRemainingSeconds");
                if (defense.spawnClockSeconds != 0f)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".spawnClockSeconds");
            }
            else if (defense.wavePhase == 0 &&
                (defense.spawnedEnemyCount != tutorialEnemyCount ||
                 defense.defeatedEnemyCount != tutorialEnemyCount ||
                 defense.enemies.Length != 0))
            {
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".wavePhase");
            }
            else if (defense.wavePhase == 3 &&
                defense.defeatedEnemyCount >=
                tutorialCompletionDefeatCount)
            {
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".wavePhase");
            }
            if (defense.defeatedEnemyCount >
                defense.spawnedEnemyCount)
            {
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".defeatedEnemyCount");
            }
            if (defense.spawnedEnemyCount > tutorialEnemyCount)
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".spawnedEnemyCount");
            if (defense.enemies.Length !=
                defense.spawnedEnemyCount -
                defense.defeatedEnemyCount)
            {
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".enemies");
            }
            if (defense.fixedStepAccumulatorSeconds >= fixedStepSeconds)
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".fixedStepAccumulatorSeconds");
            if (defense.coreCurrentHealth > coreMaximumHealth)
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".coreCurrentHealth");
            if (defense.tutorialTriggered)
            {
                switch ((WavePhase)defense.wavePhase)
                {
                    case WavePhase.Idle:
                        if (defense.warningRemainingSeconds != 0f)
                            return Invalid(
                                FormalSaveValidationError.InvalidDefense,
                                path + ".warningRemainingSeconds");
                        break;
                    case WavePhase.Warning:
                        if (defense.warningRemainingSeconds <= 0f ||
                            defense.spawnClockSeconds != 0f ||
                            defense.spawnedEnemyCount != 0 ||
                            defense.defeatedEnemyCount != 0 ||
                            defense.nextEnemyOrdinal != 0 ||
                            defense.enemies.Length != 0)
                        {
                            return Invalid(
                                FormalSaveValidationError.InvalidDefense,
                                path + ".wavePhase");
                        }
                        break;
                    case WavePhase.Spawning:
                        if (defense.warningRemainingSeconds != 0f)
                            return Invalid(
                                FormalSaveValidationError.InvalidDefense,
                                path + ".warningRemainingSeconds");
                        if (defense.spawnedEnemyCount >= tutorialEnemyCount)
                            return Invalid(
                                FormalSaveValidationError.InvalidDefense,
                                path + ".wavePhase");
                        if (defense.spawnClockSeconds >=
                            tutorialSpawnCadenceSeconds)
                        {
                            return Invalid(
                                FormalSaveValidationError.InvalidDefense,
                                path + ".spawnClockSeconds");
                        }
                        break;
                    case WavePhase.Active:
                        if (defense.warningRemainingSeconds != 0f ||
                            defense.spawnedEnemyCount != tutorialEnemyCount)
                        {
                            return Invalid(
                                FormalSaveValidationError.InvalidDefense,
                                path + ".wavePhase");
                        }
                        break;
                }
            }
            var towerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < defense.towers.Length; index++)
            {
                FormalThreeDDefenseTowerSaveData tower = defense.towers[index];
                string item = path + ".towers[" + index + "]";
                if (tower == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                result = AddReference(
                    towerIds,
                    buildingIds,
                    tower.stableInstanceId,
                    item + ".stableInstanceId");
                if (result != null) return result;
                if (tower.ammunitionAmount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".ammunitionAmount");
                if (tower.ammunitionAmount > ammunitionCapacity)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".ammunitionAmount");
                result = NonNegativeFinite(tower.activeAmmunitionSeconds,
                    item + ".activeAmmunitionSeconds");
                if (result != null) return result;
                if (tower.activeAmmunitionSeconds >
                    ammunitionDurationSeconds)
                {
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".activeAmmunitionSeconds");
                }
                result = NonNegativeFinite(tower.damageRemainder,
                    item + ".damageRemainder");
                if (result != null) return result;
                if (tower.damageRemainder >= 1f)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".damageRemainder");
            }
            var enemyIds = new HashSet<string>(StringComparer.Ordinal);
            var spawnOrders = new HashSet<int>();
            int maxSpawn = -1;
            for (int index = 0; index < defense.enemies.Length; index++)
            {
                FormalThreeDDefenseEnemySaveData enemy = defense.enemies[index];
                string item = path + ".enemies[" + index + "]";
                if (enemy == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                result = AddStableId(
                    enemyIds,
                    enemy.stableEnemyId,
                    item + ".stableEnemyId");
                if (result != null) return result;
                if (!IsStableId(enemy.archetypeId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".archetypeId");
                if (!string.Equals(
                        enemy.archetypeId,
                        EnemyCatalog.Gnawer.Id.Value,
                        StringComparison.Ordinal))
                {
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".archetypeId");
                }
                if (enemy.spawnOrder < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".spawnOrder");
                if (!spawnOrders.Add(enemy.spawnOrder))
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".spawnOrder");
                maxSpawn = Math.Max(maxSpawn, enemy.spawnOrder);
                if (enemy.currentHealth < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".currentHealth");
                if (enemy.currentHealth == 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".currentHealth");
                if (enemy.currentHealth >
                    EnemyCatalog.Gnawer.MaximumHealth)
                {
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".currentHealth");
                }
                result = Finite(enemy.positionX, item + ".positionX");
                if (result != null) return result;
                result = Finite(enemy.positionZ, item + ".positionZ");
                if (result != null) return result;
                result = NonNegativeFinite(enemy.movementRemainder,
                    item + ".movementRemainder");
                if (result != null) return result;
                if (enemy.movementRemainder != 0f)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".movementRemainder");
                result = NonNegativeFinite(enemy.attackDamageRemainder,
                    item + ".attackDamageRemainder");
                if (result != null) return result;
                if (enemy.attackDamageRemainder >= 1f)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        item + ".attackDamageRemainder");
            }
            if (defense.nextEnemyOrdinal <= maxSpawn ||
                defense.nextEnemyOrdinal < defense.spawnedEnemyCount)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextEnemyOrdinal");
            return null;
        }

        private static FormalSaveValidationResult ValidateDefenseCampaign(
            FormalThreeDDefenseCampaignSaveData campaign,
            HashSet<string> buildingIds)
        {
            const string path = "formal3D.defenseCampaign";
            if (campaign == null) return Missing(path);
            if (!string.Equals(
                    campaign.campaignId,
                    CampaignWaveCatalog.Id,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".campaignId");
            if (campaign.phase < 0 || campaign.phase > 5)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".phase");
            if (campaign.result < 0 || campaign.result > 2)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".result");
            if (campaign.currentWaveNumber < 0 ||
                campaign.currentWaveNumber > CampaignWaveCatalog.All.Count)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".currentWaveNumber");
            if (campaign.requestedSpeed != 0f &&
                campaign.requestedSpeed != 1f &&
                campaign.requestedSpeed != 2f)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".requestedSpeed");
            if (campaign.lastNonZeroSpeed != 1f &&
                campaign.lastNonZeroSpeed != 2f)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".lastNonZeroSpeed");
            FormalSaveValidationResult result = NonNegativeFinite(
                campaign.warningRemainingSeconds,
                path + ".warningRemainingSeconds");
            if (result != null) return result;
            result = NonNegativeFinite(
                campaign.spawnClockSeconds,
                path + ".spawnClockSeconds");
            if (result != null) return result;
            result = NonNegativeFinite(
                campaign.fixedStepAccumulatorSeconds,
                path + ".fixedStepAccumulatorSeconds");
            if (result != null) return result;
            if (campaign.fixedStepAccumulatorSeconds >=
                SingleCityDefenseCampaignModel.FormalFixedStepSeconds)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".fixedStepAccumulatorSeconds");
            if (campaign.nextEnemyOrdinal < 0)
                return Invalid(FormalSaveValidationError.NegativeValue,
                    path + ".nextEnemyOrdinal");
            if (campaign.coreCurrentHealth < 0 ||
                campaign.coreCurrentHealth >
                    CityCoreCombatModel.FormalMaximumHealth)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".coreCurrentHealth");

            result = ValidateCampaignEnemyCounts(
                campaign.plannedEnemyCountsByEnemyId,
                path + ".plannedEnemyCountsByEnemyId");
            if (result != null) return result;
            result = ValidateCampaignEnemyCounts(
                campaign.spawnedEnemyCountsByEnemyId,
                path + ".spawnedEnemyCountsByEnemyId");
            if (result != null) return result;
            result = ValidateCampaignEnemyCounts(
                campaign.defeatedEnemyCountsByEnemyId,
                path + ".defeatedEnemyCountsByEnemyId");
            if (result != null) return result;
            if (campaign.frozenSpawnAnchors == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".frozenSpawnAnchors");
            var directions = new HashSet<int>();
            for (int index = 0;
                 index < campaign.frozenSpawnAnchors.Length;
                 index++)
            {
                FormalThreeDDefenseCampaignSpawnAnchorSaveData anchor =
                    campaign.frozenSpawnAnchors[index];
                string item = path + ".frozenSpawnAnchors[" + index + "]";
                if (anchor == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                int direction = (int)anchor.direction;
                if (direction < 0 || direction > 3)
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        item + ".direction");
                if (!directions.Add(direction))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        item + ".direction");
                result = Finite(anchor.positionX, item + ".positionX");
                if (result != null) return result;
                result = Finite(anchor.positionZ, item + ".positionZ");
                if (result != null) return result;
            }

            result = ValidateCampaignTowerStates(
                campaign.towerCombatStates,
                buildingIds,
                path + ".towerCombatStates");
            if (result != null) return result;
            result = ValidateCampaignEnemyStates(
                campaign.enemyStates,
                path + ".enemyStates");
            if (result != null) return result;
            result = ValidateCampaignCountRelationships(campaign, path);
            if (result != null) return result;
            result = ValidateCampaignBuildingHealthStates(
                campaign.buildingHealthStates,
                buildingIds,
                path + ".buildingHealthStates");
            if (result != null) return result;
            return ValidateCampaignStatistics(
                campaign.statistics,
                path + ".statistics");
        }

        private static FormalSaveValidationResult
            ValidateCampaignCountRelationships(
                FormalThreeDDefenseCampaignSaveData campaign,
                string path)
        {
            if (campaign.phase == 0 && campaign.currentWaveNumber != 0)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".currentWaveNumber");
            if (campaign.phase >= 1 && campaign.phase <= 3 &&
                campaign.currentWaveNumber <= 0)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".currentWaveNumber");
            if ((campaign.phase == 4) != (campaign.result == 1) ||
                (campaign.phase == 5) != (campaign.result == 2) ||
                (campaign.phase < 4 && campaign.result != 0))
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".result");

            int totalSpawned = 0;
            int totalDefeated = 0;
            for (int index = 0;
                 index < campaign.spawnedEnemyCountsByEnemyId.Length;
                 index++)
            {
                FormalThreeDDefenseCampaignEnemyCountSaveData spawned =
                    campaign.spawnedEnemyCountsByEnemyId[index];
                int planned = FindCampaignCount(
                    campaign.plannedEnemyCountsByEnemyId,
                    spawned.enemyId);
                int defeated = FindCampaignCount(
                    campaign.defeatedEnemyCountsByEnemyId,
                    spawned.enemyId);
                if (spawned.count > planned || defeated > spawned.count)
                    return Invalid(FormalSaveValidationError.InvalidDefense,
                        path + ".spawnedEnemyCountsByEnemyId[" + index + "]");
                totalSpawned += spawned.count;
                totalDefeated += defeated;
            }
            for (int index = 0;
                 index < campaign.defeatedEnemyCountsByEnemyId.Length;
                 index++)
            {
                FormalThreeDDefenseCampaignEnemyCountSaveData defeated =
                    campaign.defeatedEnemyCountsByEnemyId[index];
                if (FindCampaignCount(
                        campaign.spawnedEnemyCountsByEnemyId,
                        defeated.enemyId) < defeated.count)
                    return Invalid(FormalSaveValidationError.InvalidDefense,
                        path + ".defeatedEnemyCountsByEnemyId[" + index + "]");
            }
            if (campaign.enemyStates.Length !=
                totalSpawned - totalDefeated)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".enemyStates");
            if (campaign.nextEnemyOrdinal < totalSpawned)
                return Invalid(FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextEnemyOrdinal");
            return null;
        }

        private static int FindCampaignCount(
            FormalThreeDDefenseCampaignEnemyCountSaveData[] values,
            string enemyId)
        {
            for (int index = 0; index < values.Length; index++)
                if (string.Equals(
                        values[index].enemyId,
                        enemyId,
                        StringComparison.Ordinal))
                    return values[index].count;
            return 0;
        }

        private static FormalSaveValidationResult ValidateCampaignEnemyCounts(
            FormalThreeDDefenseCampaignEnemyCountSaveData[] values,
            string path)
        {
            if (values == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyCountSaveData value =
                    values[index];
                string item = path + "[" + index + "]";
                if (value == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                FormalSaveValidationResult result = AddStableId(
                    ids,
                    value.enemyId,
                    item + ".enemyId");
                if (result != null) return result;
                if (value.count < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".count");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateCampaignTowerStates(
            FormalThreeDDefenseCampaignTowerCombatStateSaveData[] values,
            HashSet<string> buildingIds,
            string path)
        {
            if (values == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                FormalThreeDDefenseCampaignTowerCombatStateSaveData value =
                    values[index];
                string item = path + "[" + index + "]";
                if (value == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                FormalSaveValidationResult result = AddReference(
                    ids,
                    buildingIds,
                    value.stableInstanceId,
                    item + ".stableInstanceId");
                if (result != null) return result;
                if (!IsStableId(value.consumableId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".consumableId");
                if (value.amount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".amount");
                result = NonNegativeFinite(
                    value.activeConsumableSeconds,
                    item + ".activeConsumableSeconds");
                if (result != null) return result;
                result = NonNegativeFinite(
                    value.damageRemainder,
                    item + ".damageRemainder");
                if (result != null) return result;
                if (!string.IsNullOrEmpty(value.targetStableEnemyId) &&
                    !IsStableId(value.targetStableEnemyId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".targetStableEnemyId");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateCampaignEnemyStates(
            FormalThreeDDefenseCampaignEnemyStateSaveData[] values,
            string path)
        {
            if (values == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyStateSaveData value =
                    values[index];
                string item = path + "[" + index + "]";
                if (value == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                FormalSaveValidationResult result = AddStableId(
                    ids,
                    value.stableEnemyId,
                    item + ".stableEnemyId");
                if (result != null) return result;
                if (!IsStableId(value.archetypeId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".archetypeId");
                if (value.spawnOrder < 0 || value.currentHealth < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item);
                result = Finite(value.positionX, item + ".positionX");
                if (result != null) return result;
                result = Finite(value.positionZ, item + ".positionZ");
                if (result != null) return result;
                result = NonNegativeFinite(
                    value.movementRemainder,
                    item + ".movementRemainder");
                if (result != null) return result;
                result = NonNegativeFinite(
                    value.attackDamageRemainder,
                    item + ".attackDamageRemainder");
                if (result != null) return result;
                if (!string.IsNullOrEmpty(value.targetStableId) &&
                    !string.Equals(
                        value.targetStableId,
                        SingleCityDefenseCampaignModel.CityCoreTargetId,
                        StringComparison.Ordinal) &&
                    !IsStableId(value.targetStableId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".targetStableId");
            }
            return null;
        }

        private static FormalSaveValidationResult
            ValidateCampaignBuildingHealthStates(
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
                    values,
                HashSet<string> buildingIds,
                string path)
        {
            if (values == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData value =
                    values[index];
                string item = path + "[" + index + "]";
                if (value == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                FormalSaveValidationResult result = AddReference(
                    ids,
                    buildingIds,
                    value.stableInstanceId,
                    item + ".stableInstanceId");
                if (result != null) return result;
                if (value.currentHealth < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".currentHealth");
                if (value.isDestroyed && value.currentHealth != 0)
                    return Invalid(FormalSaveValidationError.InvalidDefense,
                        item + ".currentHealth");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateCampaignStatistics(
            FormalThreeDDefenseCampaignStatisticsSaveData statistics,
            string path)
        {
            if (statistics == null) return Missing(path);
            FormalSaveValidationResult result = NonNegativeFinite(
                statistics.elapsedRuleSeconds,
                path + ".elapsedRuleSeconds");
            if (result != null) return result;
            if (statistics.spawnedEnemyCount < 0 ||
                statistics.defeatedEnemyCount < 0 ||
                statistics.completedWaveCount < 0 ||
                statistics.highestAliveEnemyCount < 0 ||
                statistics.coreDamageTaken < 0 ||
                statistics.completedProductionBatchCount < 0)
                return Invalid(FormalSaveValidationError.NegativeValue, path);
            result = ValidateCampaignMetrics(
                statistics.killsByEnemyId,
                path + ".killsByEnemyId");
            if (result != null) return result;
            result = ValidateCampaignMetrics(
                statistics.buildingLossesByBuildingId,
                path + ".buildingLossesByBuildingId");
            if (result != null) return result;
            result = ValidateCampaignMetrics(
                statistics.damageByTowerBuildingId,
                path + ".damageByTowerBuildingId");
            if (result != null) return result;
            result = ValidateCampaignMetrics(
                statistics.killsByTowerBuildingId,
                path + ".killsByTowerBuildingId");
            if (result != null) return result;
            result = ValidateCampaignMetrics(
                statistics.consumablesSpentByResourceId,
                path + ".consumablesSpentByResourceId");
            if (result != null) return result;
            result = NonNegativeFinite(
                statistics.productionActiveProgressSeconds,
                path + ".productionActiveProgressSeconds");
            if (result != null) return result;
            result = NonNegativeFinite(
                statistics.productionEligibleSeconds,
                path + ".productionEligibleSeconds");
            if (result != null) return result;
            if (statistics.productionActiveProgressSeconds >
                statistics.productionEligibleSeconds)
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".productionActiveProgressSeconds");
            if (!statistics.partialFromMigration &&
                SumCampaignMetrics(statistics.killsByTowerBuildingId) !=
                statistics.defeatedEnemyCount)
                return Invalid(
                    FormalSaveValidationError.InvalidDefense,
                    path + ".killsByTowerBuildingId");
            return null;
        }

        private static int SumCampaignMetrics(
            FormalThreeDDefenseCampaignMetricSaveData[] values)
        {
            var total = 0;
            for (var index = 0; index < values.Length; index++)
                total += values[index].amount;
            return total;
        }

        private static FormalSaveValidationResult ValidateCampaignMetrics(
            FormalThreeDDefenseCampaignMetricSaveData[] values,
            string path)
        {
            if (values == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                FormalThreeDDefenseCampaignMetricSaveData value = values[index];
                string item = path + "[" + index + "]";
                if (value == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                FormalSaveValidationResult result = AddStableId(
                    ids,
                    value.stableId,
                    item + ".stableId");
                if (result != null) return result;
                if (value.amount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".amount");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateEvacuation(
            FormalThreeDEvacuationSaveData evacuation,
            HashSet<string> buildingIds)
        {
            const string path = "formal3D.evacuation";
            if (evacuation.work == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".work");
            if (evacuation.fullQueueStableInstanceIds == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".fullQueueStableInstanceIds");
            if (evacuation.runtimePayloads == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".runtimePayloads");
            if (evacuation.lockedStableInstanceIds == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".lockedStableInstanceIds");
            if (evacuation.pendingRollbackStableInstanceIds == null)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".pendingRollbackStableInstanceIds");
            if (evacuation.nextBatchOrdinal < 0)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextBatchOrdinal");

            var workIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < evacuation.work.Length; index++)
            {
                FormalThreeDEvacuationWorkSaveData work = evacuation.work[index];
                string item = path + ".work[" + index + "]";
                if (work == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                if (!IsStableId(work.stableInstanceId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".stableInstanceId");
                if (!workIds.Add(work.stableInstanceId))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item + ".stableInstanceId");
                if (work.treatment < 1 || work.treatment > 3)
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        item + ".treatment");
                FormalSaveValidationResult result = NonNegativeFinite(
                    work.remainingRatio,
                    item + ".remainingRatio");
                if (result != null) return result;
                if (work.remainingRatio > 1d)
                    return Invalid(FormalSaveValidationError.InvalidEvacuation,
                        item + ".remainingRatio");
                result = NonNegativeFinite(work.baseDismantleSeconds,
                    item + ".baseDismantleSeconds");
                if (result != null) return result;
                result = NonNegativeFinite(work.dismantleSeconds,
                    item + ".dismantleSeconds");
                if (result != null) return result;
                if (work.refund < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".refund");
            }

            var queueIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0;
                 index < evacuation.fullQueueStableInstanceIds.Length;
                 index++)
            {
                string id = evacuation.fullQueueStableInstanceIds[index];
                string item = path + ".fullQueueStableInstanceIds[" +
                              index + "]";
                if (!IsStableId(id))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item);
                if (!queueIds.Add(id))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item);
                if (!workIds.Contains(id))
                    return Invalid(FormalSaveValidationError.InvalidEvacuation,
                        item);
            }

            if (evacuation.isProcessing)
            {
                int batchOrdinal;
                if (!TryGeneratedOrdinal(
                        evacuation.activeBatchId,
                        "evacuation.batch.",
                        out batchOrdinal))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        path + ".activeBatchId");
                if (evacuation.nextBatchOrdinal <= batchOrdinal)
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        path + ".nextBatchOrdinal");
                if (evacuation.batchContext == null)
                    return Missing(path + ".batchContext");
                FormalSaveValidationResult result = NonNegativeFinite(
                    evacuation.batchContext.productivityMultiplier,
                    path + ".batchContext.productivityMultiplier");
                if (result != null) return result;
                if (evacuation.currentQueueIndex < 0 ||
                    evacuation.currentQueueIndex >=
                        evacuation.fullQueueStableInstanceIds.Length ||
                    !string.Equals(
                        evacuation.currentStableInstanceId,
                        evacuation.fullQueueStableInstanceIds[
                            evacuation.currentQueueIndex],
                        StringComparison.Ordinal))
                    return Invalid(FormalSaveValidationError.InvalidEvacuation,
                        path + ".currentStableInstanceId");
                if (!buildingIds.Contains(
                        evacuation.currentStableInstanceId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        path + ".currentStableInstanceId");
                for (int index = evacuation.currentQueueIndex;
                     index < evacuation.fullQueueStableInstanceIds.Length;
                     index++)
                {
                    if (!buildingIds.Contains(
                            evacuation.fullQueueStableInstanceIds[index]))
                    {
                        return Invalid(
                            FormalSaveValidationError.MissingStableReference,
                            path + ".fullQueueStableInstanceIds[" + index + "]");
                    }
                }
                result = NonNegativeFinite(evacuation.remainingSeconds,
                    path + ".remainingSeconds");
                if (result != null) return result;
            }
            else if (!string.IsNullOrEmpty(evacuation.activeBatchId) ||
                     !string.IsNullOrEmpty(evacuation.currentStableInstanceId))
            {
                return Invalid(FormalSaveValidationError.InvalidEvacuation,
                    path + ".activeBatchId");
            }

            FormalSaveValidationResult references = ValidateReferenceList(
                evacuation.lockedStableInstanceIds,
                buildingIds,
                path + ".lockedStableInstanceIds");
            if (references != null) return references;
            references = ValidateReferenceList(
                evacuation.pendingRollbackStableInstanceIds,
                buildingIds,
                path + ".pendingRollbackStableInstanceIds");
            if (references != null) return references;

            var payloadIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0;
                 index < evacuation.runtimePayloads.Length;
                 index++)
            {
                FormalThreeDEvacuationRuntimePayloadSaveData payload =
                    evacuation.runtimePayloads[index];
                string item = path + ".runtimePayloads[" + index + "]";
                if (payload == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                references = AddReference(
                    payloadIds,
                    buildingIds,
                    payload.stableInstanceId,
                    item + ".stableInstanceId");
                if (references != null) return references;
                references = ValidateAmounts(payload.productionInputAmounts,
                    item + ".productionInputAmounts");
                if (references != null) return references;
                references = ValidateAmounts(payload.productionReservedInputs,
                    item + ".productionReservedInputs");
                if (references != null) return references;
                references = ValidateAmounts(payload.productionOutputAmounts,
                    item + ".productionOutputAmounts");
                if (references != null) return references;
                references = ValidateAmounts(payload.resourcePayload,
                    item + ".resourcePayload");
                if (references != null) return references;
                if (payload.towerAmmunitionAmount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".towerAmmunitionAmount");
            }
            if (evacuation.isBlocked)
            {
                if (!evacuation.isProcessing ||
                    string.IsNullOrWhiteSpace(evacuation.blockedCode) ||
                    !string.Equals(
                        evacuation.blockedStableInstanceId,
                        evacuation.currentStableInstanceId,
                        StringComparison.Ordinal))
                    return Invalid(FormalSaveValidationError.InvalidEvacuation,
                        path + ".blockedStableInstanceId");
            }
            else if (!string.IsNullOrEmpty(evacuation.blockedCode) ||
                     !string.IsNullOrEmpty(
                         evacuation.blockedStableInstanceId))
            {
                return Invalid(FormalSaveValidationError.InvalidEvacuation,
                    path + ".blockedCode");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateAmounts(
            FormalThreeDResourceAmountSaveData[] amounts,
            string path)
        {
            if (amounts == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < amounts.Length; index++)
            {
                FormalThreeDResourceAmountSaveData amount = amounts[index];
                string item = path + "[" + index + "]";
                if (amount == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                if (!IsStableId(amount.resourceId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".resourceId");
                if (!ids.Add(amount.resourceId))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item + ".resourceId");
                if (amount.amount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".amount");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateOrphans(
            FormalThreeDOrphanResourceSaveData[] orphans,
            string path)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < orphans.Length; index++)
            {
                FormalThreeDOrphanResourceSaveData orphan = orphans[index];
                string item = path + "[" + index + "]";
                if (orphan == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                if (!IsStableId(orphan.resourceId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".resourceId");
                if (string.IsNullOrWhiteSpace(orphan.ownerKind))
                    return Missing(item + ".ownerKind");
                if (string.IsNullOrWhiteSpace(orphan.ownerStableId))
                    return Missing(item + ".ownerStableId");
                if (orphan.amount < 0)
                    return Invalid(FormalSaveValidationError.NegativeValue,
                        item + ".amount");
                string key = orphan.ownerKind + "\n" +
                             orphan.ownerStableId + "\n" + orphan.resourceId;
                if (!keys.Add(key))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item + ".resourceId");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateReferenceList(
            string[] values,
            HashSet<string> targets,
            string path)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                FormalSaveValidationResult result = AddReference(
                    ids,
                    targets,
                    values[index],
                    path + "[" + index + "]");
                if (result != null) return result;
            }
            return null;
        }

        private static FormalSaveValidationResult AddReference(
            HashSet<string> seen,
            HashSet<string> targets,
            string value,
            string path)
        {
            FormalSaveValidationResult result = AddStableId(
                seen,
                value,
                path);
            if (result != null) return result;
            return targets.Contains(value)
                ? null
                : Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path);
        }

        private static FormalSaveValidationResult AddStableId(
            HashSet<string> seen,
            string value,
            string path)
        {
            if (!IsStableId(value))
                return Invalid(FormalSaveValidationError.InvalidStableId,
                    path);
            return seen.Add(value)
                ? null
                : Invalid(FormalSaveValidationError.DuplicateStableId, path);
        }

        private static FormalSaveValidationResult UniqueNonBlank(
            string[] values,
            string path)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(values[index]))
                    return Missing(path + "[" + index + "]");
                if (!unique.Add(values[index]))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        path + "[" + index + "]");
            }
            return null;
        }

        private static FormalSaveValidationResult Finite(
            float value,
            string path)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? Invalid(FormalSaveValidationError.NonFiniteNumber, path)
                : null;
        }

        private static FormalSaveValidationResult NonNegativeFinite(
            float value,
            string path)
        {
            FormalSaveValidationResult result = Finite(value, path);
            if (result != null) return result;
            return value < 0f
                ? Invalid(FormalSaveValidationError.NegativeValue, path)
                : null;
        }

        private static FormalSaveValidationResult NonNegativeFinite(
            double value,
            string path)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return Invalid(FormalSaveValidationError.NonFiniteNumber,
                    path);
            return value < 0d
                ? Invalid(FormalSaveValidationError.NegativeValue, path)
                : null;
        }

        private static bool IsStableId(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   StableIdPattern.IsMatch(value);
        }

        private static bool TryGeneratedOrdinal(
            string value,
            string prefix,
            out int ordinal)
        {
            ordinal = 0;
            if (string.IsNullOrEmpty(value) ||
                !value.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string suffix = value.Substring(prefix.Length);
            return suffix.Length == 6 &&
                   int.TryParse(
                       suffix,
                       NumberStyles.None,
                       CultureInfo.InvariantCulture,
                       out ordinal) &&
                   ordinal > 0;
        }

        private static FormalSaveValidationResult Missing(string path)
        {
            return Invalid(
                FormalSaveValidationError.MissingRequiredValue,
                path);
        }

        private static FormalSaveValidationResult Invalid(
            FormalSaveValidationError error,
            string path)
        {
            return FormalSaveValidationResult.Invalid(
                error,
                path,
                "存档字段无效：" + path);
        }

        private static bool HasJsonPath(string json, string[] path)
        {
            int start = 0;
            int end = json.Length;
            for (int index = 0; index < path.Length; index++)
            {
                int valueStart;
                int valueEnd;
                if (!TryFindObjectMember(
                        json,
                        start,
                        end,
                        path[index],
                        out valueStart,
                        out valueEnd))
                    return false;
                start = valueStart;
                end = valueEnd;
            }
            int finalStart = SkipWhitespace(json, start, end);
            return finalStart < end && json[finalStart] == '[';
        }

        private static int ReadSourceSchemaVersion(string json)
        {
            if (!TryFindObjectMember(
                    json,
                    0,
                    json.Length,
                    "saveSchemaVersion",
                    out int valueStart,
                    out int valueEnd))
                return 0;
            string value = json.Substring(
                valueStart,
                valueEnd - valueStart).Trim();
            return int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int schemaVersion)
                ? schemaVersion
                : 0;
        }

        private static FormalSaveValidationResult
            RequireArrayMembersForEveryItem(
            string json,
            string[] arrayPath,
            int expectedCount,
            string[] requiredMembers,
            string displayPath)
        {
            int arrayStart;
            int arrayEnd;
            if (!TryFindJsonPath(
                    json,
                    arrayPath,
                    out arrayStart,
                    out arrayEnd))
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    displayPath);
            int cursor = SkipWhitespace(json, arrayStart, arrayEnd);
            if (cursor >= arrayEnd || json[cursor] != '[')
                return Invalid(
                    FormalSaveValidationError.MissingRequiredValue,
                    displayPath);
            cursor++;
            for (int itemIndex = 0;
                 itemIndex < expectedCount;
                 itemIndex++)
            {
                cursor = SkipWhitespace(json, cursor, arrayEnd);
                if (cursor >= arrayEnd || json[cursor] == ']')
                    return Invalid(
                        FormalSaveValidationError.MissingRequiredValue,
                        displayPath + "[" + itemIndex + "]");
                int itemEnd = ScanValue(json, cursor, arrayEnd);
                if (itemEnd < 0 || json[cursor] != '{')
                    return Invalid(
                        FormalSaveValidationError.MissingRequiredValue,
                        displayPath + "[" + itemIndex + "]");
                for (int memberIndex = 0;
                     memberIndex < requiredMembers.Length;
                     memberIndex++)
                {
                    int valueStart;
                    int valueEnd;
                    string member = requiredMembers[memberIndex];
                    if (!TryFindObjectMember(
                        json,
                        cursor,
                        itemEnd,
                        member,
                        out valueStart,
                        out valueEnd) ||
                        valueStart >= valueEnd ||
                        json[valueStart] != '[')
                        return Invalid(
                            FormalSaveValidationError.MissingRequiredValue,
                            displayPath + "[" + itemIndex + "]." +
                            member);
                }
                cursor = SkipWhitespace(json, itemEnd, arrayEnd);
                if (cursor < arrayEnd && json[cursor] == ',') cursor++;
            }
            cursor = SkipWhitespace(json, cursor, arrayEnd);
            return cursor < arrayEnd && json[cursor] == ']'
                ? null
                : Invalid(
                    FormalSaveValidationError.InvalidArray,
                    displayPath);
        }

        private static bool TryFindJsonPath(
            string json,
            string[] path,
            out int start,
            out int end)
        {
            start = 0;
            end = json.Length;
            for (int index = 0; index < path.Length; index++)
            {
                int valueStart;
                int valueEnd;
                if (!TryFindObjectMember(
                        json,
                        start,
                        end,
                        path[index],
                        out valueStart,
                        out valueEnd))
                    return false;
                start = valueStart;
                end = valueEnd;
            }
            return true;
        }

        private static bool TryParseUtcRoundTrip(
            string value,
            out DateTime result)
        {
            return DateTime.TryParseExact(
                       value,
                       "O",
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.RoundtripKind,
                       out result) &&
                   result.Kind == DateTimeKind.Utc;
        }

        private static bool TryFindObjectMember(
            string json,
            int start,
            int end,
            string member,
            out int valueStart,
            out int valueEnd)
        {
            valueStart = valueEnd = -1;
            int cursor = SkipWhitespace(json, start, end);
            if (cursor >= end || json[cursor] != '{') return false;
            cursor++;
            while (cursor < end)
            {
                cursor = SkipWhitespace(json, cursor, end);
                if (cursor >= end || json[cursor] == '}') return false;
                if (json[cursor] != '"') return false;
                int keyStart = cursor + 1;
                int keyEnd = ScanString(json, cursor, end);
                if (keyEnd < 0) return false;
                cursor = SkipWhitespace(json, keyEnd, end);
                if (cursor >= end || json[cursor] != ':') return false;
                cursor = SkipWhitespace(json, cursor + 1, end);
                int scannedEnd = ScanValue(json, cursor, end);
                if (scannedEnd < 0) return false;
                if (keyEnd - keyStart - 1 == member.Length &&
                    string.CompareOrdinal(
                        json,
                        keyStart,
                        member,
                        0,
                        member.Length) == 0)
                {
                    valueStart = cursor;
                    valueEnd = scannedEnd;
                    return true;
                }
                cursor = SkipWhitespace(json, scannedEnd, end);
                if (cursor < end && json[cursor] == ',') cursor++;
            }
            return false;
        }

        private static int ScanValue(string json, int start, int end)
        {
            if (start >= end) return -1;
            if (json[start] == '"') return ScanString(json, start, end);
            if (json[start] != '{' && json[start] != '[')
            {
                int cursor = start;
                while (cursor < end && json[cursor] != ',' &&
                       json[cursor] != '}' && json[cursor] != ']')
                    cursor++;
                return cursor;
            }
            char opening = json[start];
            char closing = opening == '{' ? '}' : ']';
            int depth = 1;
            for (int cursor = start + 1; cursor < end; cursor++)
            {
                if (json[cursor] == '"')
                {
                    int after = ScanString(json, cursor, end);
                    if (after < 0) return -1;
                    cursor = after - 1;
                }
                else if (json[cursor] == opening)
                {
                    depth++;
                }
                else if (json[cursor] == closing && --depth == 0)
                {
                    return cursor + 1;
                }
            }
            return -1;
        }

        private static int ScanString(string json, int start, int end)
        {
            bool escaped = false;
            for (int cursor = start + 1; cursor < end; cursor++)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (json[cursor] == '\\')
                {
                    escaped = true;
                }
                else if (json[cursor] == '"')
                {
                    return cursor + 1;
                }
            }
            return -1;
        }

        private static int SkipWhitespace(string json, int start, int end)
        {
            while (start < end && char.IsWhiteSpace(json[start])) start++;
            return start;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
