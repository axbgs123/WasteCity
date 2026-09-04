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
            new[] { "formal3D", "progression", "quantumEntanglement", "committedSynchronizationKeys" },
            new[] { "formal3D", "progression", "spatialTemplate", "entries" },
            new[] { "formal3D", "progression", "foresightDelay", "displayedCycleOrdinals" },
            new[] { "formal3D", "progression", "causalTransparency", "scannedStableEventKeys" },
            new[] { "formal3D", "progression", "voidChest", "pendingChests" },
            new[] { "formal3D", "progression", "voidChest", "committedDeathEventIds" },
            new[] { "formal3D", "progression", "voidChest", "claimedRewardKeys" },
            new[] { "formal3D", "researchEffectState", "states" },
            new[] { "formal3D", "researchEffectState", "emitters" },
            new[] { "formal3D", "researchEffectState", "rewardLedger", "committedRewardKeys" },
        };

        private static readonly string[][] RequiredProgressionSourceMembers =
        {
            new[] { "formal3D", "researchEffectState", "configurationSignature" },
            new[] { "formal3D", "researchEffectState", "revision" },
            new[] { "formal3D", "researchEffectState", "nextStableStateOrdinal" },
            new[] { "formal3D", "progression", "fate", "offerSelectionVersion" },
        };

        private static readonly string[][] RequiredExplorationSourceArrays =
        {
            new[] { "formal3D", "exploration", "exploredCells" },
            new[] { "formal3D", "exploration", "scanZones" },
            new[] { "formal3D", "exploration", "intel" },
            new[] { "formal3D", "exploration", "outpostAlerts" },
        };

        private static readonly string[][] RequiredExplorationSourceMembers =
        {
            new[] { "formal3D", "exploration", "configurationSignature" },
            new[] { "formal3D", "exploration", "configurationVersion" },
            new[] { "formal3D", "exploration", "worldConfigurationSignature" },
            new[] { "formal3D", "exploration", "width" },
            new[] { "formal3D", "exploration", "height" },
            new[] { "formal3D", "exploration", "leader" },
            new[] { "formal3D", "exploration", "cenJinDistress" },
            new[] { "formal3D", "exploration", "revision" },
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
                    for (int index = 0;
                         index < RequiredProgressionSourceMembers.Length;
                         index++)
                    {
                        string[] path = RequiredProgressionSourceMembers[index];
                        if (!TryFindJsonPath(source, path, out _, out _))
                            return Invalid(
                                FormalSaveValidationError.MissingRequiredValue,
                                string.Join(".", path));
                    }
                    for (int index = 0;
                         index < RequiredExplorationSourceArrays.Length;
                         index++)
                    {
                        string[] path = RequiredExplorationSourceArrays[index];
                        if (!HasJsonPath(source, path))
                            return Invalid(
                                FormalSaveValidationError.MissingRequiredValue,
                                string.Join(".", path));
                    }
                    for (int index = 0;
                         index < RequiredExplorationSourceMembers.Length;
                         index++)
                    {
                        string[] path = RequiredExplorationSourceMembers[index];
                        if (!TryFindJsonPath(source, path, out _, out _))
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
            result = ValidateCivilizationExpansionWithResearch(
                data.civilizationExpansion,
                data.research,
                data.researchEffectState);
            if (result != null) return result;
            result = ValidateResearchEffectState(
                data.researchEffectState,
                data,
                buildingIds);
            if (result != null) return result;
            result = ValidateExploration(data.exploration, data);
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
            if (data.researchEffectState == null)
                return Missing("formal3D.researchEffectState");
            if (data.exploration == null)
                return Missing("formal3D.exploration");
            return null;
        }

        private static FormalSaveValidationResult ValidateExploration(
            FormalThreeDExplorationSaveData exploration,
            FormalThreeDSaveData data)
        {
            const string path = "formal3D.exploration";
            if (!string.Equals(
                    exploration.configurationSignature,
                    FormalThreeDExplorationSaveData.ConfigurationSignature,
                    StringComparison.Ordinal) ||
                exploration.configurationVersion !=
                    FormalThreeDExplorationSaveData.ConfigurationVersion)
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".configurationSignature");
            if (exploration.width != data.world.width ||
                exploration.height != data.world.height ||
                !string.Equals(
                    exploration.worldConfigurationSignature,
                    data.world.configurationSignature,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.InvalidWorld,
                    path + ".worldConfigurationSignature");
            if (exploration.width <= 0 || exploration.height <= 0 ||
                exploration.width > int.MaxValue / exploration.height ||
                exploration.exploredCells == null ||
                exploration.exploredCells.Length !=
                    exploration.width * exploration.height)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".exploredCells");
            if (exploration.scanZones == null || exploration.intel == null ||
                exploration.outpostAlerts == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path);
            if (exploration.leader == null)
                return Missing(path + ".leader");
            if (exploration.leader.manualGather == null)
                return Missing(path + ".leader.manualGather");
            if (exploration.cenJinDistress == null)
                return Missing(path + ".cenJinDistress");

            var scanIds = new HashSet<string>(StringComparer.Ordinal);
            var scanKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < exploration.scanZones.Length; index++)
            {
                FormalThreeDScanZoneSaveData item =
                    exploration.scanZones[index];
                string itemPath = path + ".scanZones[" + index + "]";
                if (item == null ||
                    (!string.Equals(
                         item.zoneId,
                         "core.exploration.zone.safe-mining",
                         StringComparison.Ordinal) &&
                     !string.Equals(
                         item.zoneId,
                         "core.exploration.zone.crystal-rift",
                         StringComparison.Ordinal)))
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        itemPath + ".zoneId");
                if (!scanIds.Add(item.zoneId))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        itemPath + ".zoneId");
                if (!IsStableLedgerKey(item.committedEventKey) ||
                    !scanKeys.Add(item.committedEventKey))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        itemPath + ".committedEventKey");
            }

            var intelIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < exploration.intel.Length; index++)
            {
                FormalThreeDIntelSaveData item = exploration.intel[index];
                string itemPath = path + ".intel[" + index + "]";
                if (item == null || !IsStableId(item.stableIntelId) ||
                    !intelIds.Add(item.stableIntelId))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        itemPath + ".stableIntelId");
                if (item.ownerKind < 0 || item.ownerKind > 4)
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        itemPath + ".ownerKind");
                if (!IsStableId(item.ownerStableId) ||
                    !ExplorationOwnerExists(data, item.ownerKind,
                        item.ownerStableId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        itemPath + ".ownerStableId");
                if (!CellInBounds(data.world, item.x, item.y))
                    return Invalid(
                        FormalSaveValidationError.InvalidWorld,
                        itemPath + ".x");
                if (!IsFinite(item.remainingFreshSeconds) ||
                    !IsFinite(item.remainingExpirySeconds) ||
                    item.remainingFreshSeconds < 0f ||
                    item.remainingFreshSeconds > 60f ||
                    item.remainingExpirySeconds < 0f ||
                    item.remainingExpirySeconds > 180f ||
                    item.remainingFreshSeconds > item.remainingExpirySeconds ||
                    item.remainingExpirySeconds == 0f && item.hasMutableValue)
                    return Invalid(
                        FormalSaveValidationError.NegativeValue,
                        itemPath + ".remainingExpirySeconds");
            }

            FormalThreeDLeaderInteractionSaveData leader = exploration.leader;
            if (leader.requestedControlMode < 0 ||
                leader.requestedControlMode > 1)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".leader.requestedControlMode");
            FormalThreeDManualGatherSaveData gather = leader.manualGather;
            if (!IsFinite(gather.remainingCycleSeconds) ||
                gather.remainingCycleSeconds < 0f ||
                gather.remainingCycleSeconds > 6f)
                return Invalid(
                    FormalSaveValidationError.NegativeValue,
                    path + ".leader.manualGather.remainingCycleSeconds");
            if (gather.active)
            {
                if (leader.requestedControlMode != 1 ||
                    !ResourceNodeExists(data.world, gather.targetNodeId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        path + ".leader.manualGather.targetNodeId");
            }
            else if (!string.IsNullOrEmpty(gather.targetNodeId) ||
                     gather.remainingCycleSeconds != 0f)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".leader.manualGather");

            FormalThreeDCenJinDistressSaveData distress =
                exploration.cenJinDistress;
            if (!string.Equals(
                    distress.siteId,
                    FormalThreeDCenJinDistressSaveData.SiteId,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".cenJinDistress.siteId");
            if (distress.state < 0 || distress.state > 5)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".cenJinDistress.state");
            if (!IsFinite(distress.elapsedSinceDiscoverySeconds) ||
                !IsFinite(distress.rescueRemainingSeconds) ||
                distress.elapsedSinceDiscoverySeconds < 0f ||
                distress.rescueRemainingSeconds < 0f ||
                distress.rescueRemainingSeconds > 12f ||
                distress.reservedBiomass < 0)
                return Invalid(
                    FormalSaveValidationError.NegativeValue,
                    path + ".cenJinDistress");
            bool rescuing = distress.state == 2;
            bool completed = distress.state >= 3;
            if (rescuing != (distress.reservedBiomass == 10) ||
                rescuing != (distress.rescueRemainingSeconds > 0f) ||
                completed && distress.reservedBiomass != 0 ||
                completed && distress.rescueRemainingSeconds != 0f ||
                !rescuing && !completed &&
                    !string.IsNullOrEmpty(distress.committedRewardKey) ||
                distress.state >= 3 && distress.state <= 4 &&
                    !IsStableLedgerKey(distress.committedRewardKey))
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".cenJinDistress");

            var alertIds = new HashSet<string>(StringComparer.Ordinal);
            var attackIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0;
                 index < exploration.outpostAlerts.Length;
                 index++)
            {
                FormalThreeDOutpostAlertSaveData item =
                    exploration.outpostAlerts[index];
                string itemPath = path + ".outpostAlerts[" + index + "]";
                if (item == null || !IsStableId(item.stableAlertId) ||
                    !alertIds.Add(item.stableAlertId))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        itemPath + ".stableAlertId");
                if (!IsStableId(item.attackFactId) ||
                    !attackIds.Add(item.attackFactId))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        itemPath + ".attackFactId");
                if (!OutpostExists(data, item.settlementId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        itemPath + ".settlementId");
                if (item.severity < 1 || item.severity > 3)
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        itemPath + ".severity");
                if (!CellInBounds(data.world, item.x, item.y) ||
                    string.IsNullOrWhiteSpace(item.threatSummary) ||
                    item.estimatedLossRiskPercent < 0 ||
                    item.estimatedLossRiskPercent > 100 ||
                    !IsFinite(item.estimatedSecondsToLoss) ||
                    item.estimatedSecondsToLoss < 0f ||
                    !IsFinite(item.firstRuleTimeSeconds) ||
                    !IsFinite(item.latestRuleTimeSeconds) ||
                    item.firstRuleTimeSeconds < 0d ||
                    item.latestRuleTimeSeconds < item.firstRuleTimeSeconds)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        itemPath);
            }
            return null;
        }

        private static bool ExplorationOwnerExists(
            FormalThreeDSaveData data,
            int kind,
            string stableId)
        {
            if (kind == 0) return ResourceNodeExists(data.world, stableId);
            if (kind == 1)
            {
                for (int i = 0; i < data.buildings.instances.Length; i++)
                    if (string.Equals(data.buildings.instances[i]
                            .stableInstanceId, stableId,
                            StringComparison.Ordinal)) return true;
                return false;
            }
            if (kind == 2)
            {
                FormalThreeDSettlementSaveData[] values = data
                    .civilizationExpansion.worldLayer.settlements;
                for (int i = 0; i < values.Length; i++)
                    if (string.Equals(values[i].stableSettlementId, stableId,
                            StringComparison.Ordinal)) return true;
                return false;
            }
            if (kind == 3)
            {
                FormalThreeDCharacterSaveData[] values = data
                    .civilizationExpansion.charactersPolitics.characters;
                for (int i = 0; i < values.Length; i++)
                    if (string.Equals(values[i].characterId, stableId,
                            StringComparison.Ordinal)) return true;
                return false;
            }
            FormalThreeDDefenseCampaignEnemyStateSaveData[] enemies =
                data.defenseCampaign.enemyStates;
            for (int i = 0; i < enemies.Length; i++)
                if (string.Equals(enemies[i].stableEnemyId, stableId,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ResourceNodeExists(
            FormalThreeDWorldSaveData world,
            string stableId)
        {
            if (world == null || world.resourceNodes == null) return false;
            for (int index = 0; index < world.resourceNodes.Length; index++)
                if (string.Equals(
                        world.resourceNodes[index].stableNodeId,
                        stableId,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool OutpostExists(
            FormalThreeDSaveData data,
            string stableId)
        {
            FormalThreeDSettlementSaveData[] values = data
                .civilizationExpansion.worldLayer.settlements;
            for (int index = 0; index < values.Length; index++)
                if (values[index].kind == 2 && string.Equals(
                        values[index].stableSettlementId,
                        stableId,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool CellInBounds(
            FormalThreeDWorldSaveData world,
            int x,
            int y)
        {
            return world != null && x >= 0 && y >= 0 &&
                x < world.width && y < world.height;
        }

        private static bool IsStableLedgerKey(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }

        private static FormalSaveValidationResult
            ValidateResearchEffectState(
                FormalThreeDResearchEffectStateSaveData state,
                FormalThreeDSaveData data,
                HashSet<string> buildingIds)
        {
            const string path = "formal3D.researchEffectState";
            if (!string.Equals(
                    state.configurationSignature,
                    FormalThreeDResearchEffectStateSaveData
                        .ConfigurationSignature,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.InvalidStableId,
                    path + ".configurationSignature");
            if (state.states == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".states");
            if (state.emitters == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".emitters");
            if (state.rewardLedger == null)
                return Missing(path + ".rewardLedger");
            if (state.rewardLedger.committedRewardKeys == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".rewardLedger.committedRewardKeys");
            if (state.nextStableStateOrdinal < 1L)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextStableStateOrdinal");
            if (state.revision == 0UL &&
                (state.states.Length > 0 ||
                 state.emitters.Length > 0 ||
                 state.rewardLedger.committedRewardKeys.Length > 0))
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".revision");

            var stateIds = new HashSet<string>(StringComparer.Ordinal);
            var ordinals = new HashSet<long>();
            var statusTargets = new HashSet<string>(StringComparer.Ordinal);
            var completedResearchIds = new HashSet<string>(
                data.research.completedResearchIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            long maximumOrdinal = 0L;
            long previousOrdinal = 0L;
            string previousStateId = string.Empty;
            for (int index = 0; index < state.states.Length; index++)
            {
                FormalThreeDResearchEffectStateEntrySaveData entry =
                    state.states[index];
                string entryPath = path + ".states[" + index + "]";
                if (entry == null) return Missing(entryPath);

                FormalSaveValidationResult result = AddStableId(
                    stateIds,
                    entry.stableStateId,
                    entryPath + ".stableStateId");
                if (result != null) return result;
                if (!TryParseResearchStateOrdinal(
                        entry.stableStateId,
                        out long stableIdOrdinal) ||
                    stableIdOrdinal != entry.creationOrdinal)
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        entryPath + ".stableStateId");
                if (entry.creationOrdinal < 1L ||
                    !ordinals.Add(entry.creationOrdinal))
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        entryPath + ".creationOrdinal");
                if (entry.creationOrdinal < previousOrdinal ||
                    entry.creationOrdinal == previousOrdinal &&
                    string.CompareOrdinal(
                        entry.stableStateId,
                        previousStateId) <= 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        entryPath);
                previousOrdinal = entry.creationOrdinal;
                previousStateId = entry.stableStateId;
                maximumOrdinal = Math.Max(
                    maximumOrdinal,
                    entry.creationOrdinal);

                ResearchStatusDefinition definition =
                    ResearchStatusCatalog.Find(entry.effectId);
                if (definition == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        entryPath + ".effectId");
                if (!completedResearchIds.Contains(
                        definition.SourceResearchId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        entryPath + ".effectId");
                if (!Enum.IsDefined(
                        typeof(FormalResearchEffectTargetKind),
                        entry.targetKind))
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        entryPath + ".targetKind");
                ResearchStatusTarget statusTarget = ToResearchStatusTarget(
                    entry.targetKind);
                if (!definition.Allows(statusTarget))
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        entryPath + ".targetKind");
                if (!Enum.IsDefined(
                        typeof(FormalResearchEffectStatePhase),
                        entry.phase))
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        entryPath + ".phase");
                if (!IsStableId(entry.targetStableId))
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        entryPath + ".targetStableId");
                if (!ResearchEffectTargetExists(
                        entry.targetKind,
                        entry.targetStableId,
                        data,
                        buildingIds))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        entryPath + ".targetStableId");
                string statusTargetKey = entry.effectId + "\n" +
                    ((int)entry.targetKind).ToString(
                        CultureInfo.InvariantCulture) + "\n" +
                    entry.targetStableId;
                if (!statusTargets.Add(statusTargetKey))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        entryPath + ".effectId");

                result = NonNegativeFinite(
                    entry.remainingRuleSeconds,
                    entryPath + ".remainingRuleSeconds");
                if (result != null) return result;
                float maximumRemaining = definition.MaximumRemainingSeconds(
                    (ResearchStatusPhase)(int)entry.phase);
                if (definition.SavesPhase && maximumRemaining <= 0f)
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        entryPath + ".phase");
                if (maximumRemaining < 0f ||
                    (maximumRemaining <= 0f &&
                     entry.remainingRuleSeconds > 0f) ||
                    (maximumRemaining > 0f &&
                     entry.remainingRuleSeconds > maximumRemaining))
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        entryPath + ".remainingRuleSeconds");
                if (entry.stacks <= 0)
                    return Invalid(
                        FormalSaveValidationError.NegativeValue,
                        entryPath + ".stacks");
                if (entry.stacks > definition.MaximumPersistedStacks)
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        entryPath + ".stacks");
                result = NonNegativeFinite(
                    entry.periodAccumulatorSeconds,
                    entryPath + ".periodAccumulatorSeconds");
                if (result != null) return result;
                if ((definition.PeriodSeconds <= 0f &&
                     entry.periodAccumulatorSeconds > 0f) ||
                    (definition.PeriodSeconds > 0f &&
                     entry.periodAccumulatorSeconds >=
                     definition.PeriodSeconds))
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        entryPath + ".periodAccumulatorSeconds");
                result = NonNegativeFinite(
                    entry.currentValue,
                    entryPath + ".currentValue");
                if (result != null) return result;
                if ((definition.MaximumValue <= 0f &&
                     entry.currentValue > 0f) ||
                    (definition.MaximumValue > 0f &&
                     entry.currentValue > definition.MaximumValue))
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        entryPath + ".currentValue");
                if (!definition.SavesPhase &&
                    entry.phase != FormalResearchEffectStatePhase.Active)
                    return Invalid(
                        FormalSaveValidationError.InvalidEnumValue,
                        entryPath + ".phase");
            }

            FormalSaveValidationResult emitterValidation =
                ValidateResearchEffectEmitters(
                    state.emitters,
                    data,
                    completedResearchIds,
                    stateIds,
                    ordinals,
                    ref maximumOrdinal,
                    path + ".emitters");
            if (emitterValidation != null) return emitterValidation;

            if (state.nextStableStateOrdinal <= maximumOrdinal)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextStableStateOrdinal");

            FormalSaveValidationResult ledger = UniqueNonBlank(
                state.rewardLedger.committedRewardKeys,
                path + ".rewardLedger.committedRewardKeys");
            if (ledger != null) return ledger;
            for (int index = 0;
                 index < state.rewardLedger.committedRewardKeys.Length;
                 index++)
            {
                string rewardKey =
                    state.rewardLedger.committedRewardKeys[index];
                string rewardPath =
                    path + ".rewardLedger.committedRewardKeys[" +
                    index + "]";
                if (!string.Equals(
                        rewardKey,
                        ResearchStatusCatalog.GeneSplicingRewardKey,
                        StringComparison.Ordinal))
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        rewardPath);
                if (!completedResearchIds.Contains(
                        ResearchStatusCatalog.Find(
                            ResearchStatusCatalog.GeneSplicingTraitId)
                            .SourceResearchId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        rewardPath);
            }
            bool hasGeneTrait = false;
            for (var index = 0; index < state.states.Length; index++)
                if (string.Equals(
                        state.states[index].effectId,
                        ResearchStatusCatalog.GeneSplicingTraitId,
                        StringComparison.Ordinal))
                {
                    hasGeneTrait = true;
                    break;
                }
            if (hasGeneTrait && !Array.Exists(
                    state.rewardLedger.committedRewardKeys,
                    value => string.Equals(
                        value,
                        ResearchStatusCatalog.GeneSplicingRewardKey,
                        StringComparison.Ordinal)))
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path + ".rewardLedger.committedRewardKeys");
            return ValidateMindControlCampaignProjection(state, data);
        }

        private static FormalSaveValidationResult
            ValidateResearchEffectEmitters(
                FormalThreeDResearchEffectEmitterSaveData[] emitters,
                FormalThreeDSaveData data,
                HashSet<string> completedResearchIds,
                HashSet<string> stateIds,
                HashSet<long> ordinals,
                ref long maximumOrdinal,
                string path)
        {
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            long previousOrdinal = 0L;
            string previousStateId = string.Empty;
            for (int index = 0; index < emitters.Length; index++)
            {
                FormalThreeDResearchEffectEmitterSaveData emitter =
                    emitters[index];
                string itemPath = path + "[" + index + "]";
                if (emitter == null) return Missing(itemPath);

                FormalSaveValidationResult result = AddStableId(
                    stateIds,
                    emitter.stableStateId,
                    itemPath + ".stableStateId");
                if (result != null) return result;
                if (!TryParseResearchStateOrdinal(
                        emitter.stableStateId,
                        out long stableIdOrdinal) ||
                    stableIdOrdinal != emitter.creationOrdinal)
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        itemPath + ".stableStateId");
                if (emitter.creationOrdinal < 1L ||
                    !ordinals.Add(emitter.creationOrdinal))
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        itemPath + ".creationOrdinal");
                if (emitter.creationOrdinal < previousOrdinal ||
                    emitter.creationOrdinal == previousOrdinal &&
                    string.CompareOrdinal(
                        emitter.stableStateId,
                        previousStateId) <= 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        itemPath);
                previousOrdinal = emitter.creationOrdinal;
                previousStateId = emitter.stableStateId;
                maximumOrdinal = Math.Max(
                    maximumOrdinal,
                    emitter.creationOrdinal);

                bool swordIntent = string.Equals(
                    emitter.effectId,
                    ResearchStatusCatalog.SwordIntentId,
                    StringComparison.Ordinal);
                bool infection = string.Equals(
                    emitter.effectId,
                    ResearchStatusCatalog.InfectionId,
                    StringComparison.Ordinal);
                if (!swordIntent && !infection)
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        itemPath + ".effectId");
                ResearchStatusDefinition definition =
                    ResearchStatusCatalog.Find(emitter.effectId);
                if (definition == null ||
                    !completedResearchIds.Contains(
                        definition.SourceResearchId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        itemPath + ".effectId");
                if (!IsMatchingEmitterTower(
                        data.buildings,
                        emitter.sourceTowerStableId,
                        swordIntent))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        itemPath + ".sourceTowerStableId");
                if (!IsCurrentEmitterEnemy(
                        data.defenseCampaign,
                        emitter.targetEnemyStableId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        itemPath + ".targetEnemyStableId");
                if (!IsFinite(emitter.cooldownRemaining) ||
                    emitter.cooldownRemaining <= 0f)
                    return Invalid(
                        FormalSaveValidationError.NegativeValue,
                        itemPath + ".cooldownRemaining");
                if (emitter.cooldownRemaining > 1f)
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        itemPath + ".cooldownRemaining");
                string pair = emitter.effectId + "\n" +
                    emitter.sourceTowerStableId + "\n" +
                    emitter.targetEnemyStableId;
                if (!pairs.Add(pair))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        itemPath + ".effectId");
            }
            return null;
        }

        private static bool IsMatchingEmitterTower(
            FormalThreeDBuildingsSaveData buildings,
            string stableTowerId,
            bool swordIntent)
        {
            if (!IsStableId(stableTowerId) || buildings?.instances == null)
                return false;
            for (int index = 0; index < buildings.instances.Length; index++)
            {
                FormalThreeDBuildingInstanceSaveData building =
                    buildings.instances[index];
                if (building == null || !string.Equals(
                        building.stableInstanceId,
                        stableTowerId,
                        StringComparison.Ordinal))
                    continue;
                if (building.state != 1 || !building.isPlayerOwned)
                    return false;
                return swordIntent
                    ? string.Equals(
                          building.definitionId,
                          BuildingCatalog.SwordArrayTower.Id.Value,
                          StringComparison.Ordinal) ||
                      string.Equals(
                          building.definitionId,
                          BuildingCatalog.SwordRidingPlatform.Id.Value,
                          StringComparison.Ordinal)
                    : string.Equals(
                        building.definitionId,
                        BuildingCatalog.SporeTower.Id.Value,
                        StringComparison.Ordinal);
            }
            return false;
        }

        private static bool IsCurrentEmitterEnemy(
            FormalThreeDDefenseCampaignSaveData campaign,
            string stableEnemyId)
        {
            if (!IsStableId(stableEnemyId) || campaign?.enemyStates == null)
                return false;
            for (int index = 0; index < campaign.enemyStates.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyStateSaveData enemy =
                    campaign.enemyStates[index];
                if (enemy != null && enemy.currentHealth > 0 &&
                    !enemy.isControlled && string.Equals(
                        enemy.stableEnemyId,
                        stableEnemyId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static FormalSaveValidationResult
            ValidateMindControlCampaignProjection(
                FormalThreeDResearchEffectStateSaveData state,
                FormalThreeDSaveData data)
        {
            const string statePath = "formal3D.researchEffectState.states";
            FormalThreeDPressureCampaignSaveData pressure =
                data.progression.pressure?.activeCampaign;
            string campaignPath = pressure == null
                ? "formal3D.defenseCampaign.enemyStates"
                : "formal3D.progression.pressure.activeCampaign.enemyStates";
            var controlled = new HashSet<string>(StringComparer.Ordinal);
            FormalThreeDDefenseCampaignEnemyStateSaveData[] enemies =
                pressure?.enemyStates ?? data.defenseCampaign.enemyStates;
            for (var index = 0; index < enemies.Length; index++)
                if (enemies[index].isControlled)
                    controlled.Add(enemies[index].stableEnemyId);
            var projected = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < state.states.Length; index++)
            {
                FormalThreeDResearchEffectStateEntrySaveData entry =
                    state.states[index];
                if (!string.Equals(
                        entry.effectId,
                        ResearchStatusCatalog.MindControlId,
                        StringComparison.Ordinal)) continue;
                if (entry.targetKind != FormalResearchEffectTargetKind.Enemy ||
                    !controlled.Contains(entry.targetStableId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        statePath + "[" + index + "].targetStableId");
                projected.Add(entry.targetStableId);
            }
            foreach (string stableId in controlled)
                if (!projected.Contains(stableId))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        campaignPath);
            return null;
        }

        private static ResearchStatusTarget ToResearchStatusTarget(
            FormalResearchEffectTargetKind kind)
        {
            switch (kind)
            {
                case FormalResearchEffectTargetKind.City:
                    return ResearchStatusTarget.CityCore;
                case FormalResearchEffectTargetKind.Building:
                    return ResearchStatusTarget.Building;
                case FormalResearchEffectTargetKind.Tower:
                    return ResearchStatusTarget.Tower;
                case FormalResearchEffectTargetKind.Enemy:
                    return ResearchStatusTarget.Enemy;
                case FormalResearchEffectTargetKind.ArmyUnit:
                    return ResearchStatusTarget.ArmyUnit;
                case FormalResearchEffectTargetKind.Character:
                    return ResearchStatusTarget.Leader;
                default:
                    return ResearchStatusTarget.None;
            }
        }

        private static bool ResearchEffectTargetExists(
            FormalResearchEffectTargetKind kind,
            string stableId,
            FormalThreeDSaveData data,
            HashSet<string> buildingIds)
        {
            switch (kind)
            {
                case FormalResearchEffectTargetKind.Global:
                    return string.Equals(
                        stableId,
                        "global.research.effects",
                        StringComparison.Ordinal);
                case FormalResearchEffectTargetKind.City:
                    if (string.Equals(
                            stableId,
                            data.civilizationExpansion.worldLayer
                                .primaryCityId,
                            StringComparison.Ordinal))
                        return true;
                    FormalThreeDSettlementSaveData[] settlements =
                        data.civilizationExpansion.worldLayer.settlements;
                    for (int index = 0; index < settlements.Length; index++)
                        if (string.Equals(
                                settlements[index].stableSettlementId,
                                stableId,
                                StringComparison.Ordinal))
                            return true;
                    return false;
                case FormalResearchEffectTargetKind.Building:
                    return buildingIds.Contains(stableId);
                case FormalResearchEffectTargetKind.Tower:
                    for (int index = 0;
                         index < data.buildings.instances.Length;
                         index++)
                    {
                        FormalThreeDBuildingInstanceSaveData building =
                            data.buildings.instances[index];
                        if (string.Equals(
                                building.stableInstanceId,
                                stableId,
                                StringComparison.Ordinal))
                            return DefenseTowerCatalog.For(
                                building.definitionId) != null;
                    }
                    return false;
                case FormalResearchEffectTargetKind.Enemy:
                    if (data.defense.enemies != null)
                        for (int index = 0;
                             index < data.defense.enemies.Length;
                             index++)
                            if (string.Equals(
                                    data.defense.enemies[index].stableEnemyId,
                                    stableId,
                                    StringComparison.Ordinal))
                                return true;
                    if (data.defenseCampaign.enemyStates != null)
                        for (int index = 0;
                             index < data.defenseCampaign.enemyStates.Length;
                             index++)
                            if (string.Equals(
                                    data.defenseCampaign.enemyStates[index]
                                        .stableEnemyId,
                                    stableId,
                                    StringComparison.Ordinal))
                                return true;
                    FormalThreeDPressureCampaignSaveData pressure =
                        data.progression.pressure.activeCampaign;
                    if (pressure != null && pressure.enemyStates != null)
                        for (int index = 0;
                             index < pressure.enemyStates.Length;
                             index++)
                            if (string.Equals(
                                    pressure.enemyStates[index].stableEnemyId,
                                    stableId,
                                    StringComparison.Ordinal))
                                return true;
                    return false;
                case FormalResearchEffectTargetKind.ArmyUnit:
                    FormalThreeDArmyUnitSaveData[] units =
                        data.civilizationExpansion.armyLeader.units;
                    for (int index = 0; index < units.Length; index++)
                        if (string.Equals(
                                units[index].stableUnitId,
                                stableId,
                                StringComparison.Ordinal))
                            return true;
                    FormalThreeDArmyExpeditionSaveData expedition =
                        data.civilizationExpansion.armyLeader.expedition;
                    if (expedition != null && expedition.units != null)
                        for (int index = 0;
                             index < expedition.units.Length;
                             index++)
                            if (string.Equals(
                                    expedition.units[index].stableUnitId,
                                    stableId,
                                    StringComparison.Ordinal))
                                return true;
                    return false;
                case FormalResearchEffectTargetKind.Character:
                    FormalThreeDCharactersPoliticsSaveData politics =
                        data.civilizationExpansion.charactersPolitics;
                    if (string.Equals(
                            politics.currentLeaderId,
                            stableId,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            data.civilizationExpansion.armyLeader.leader
                                .characterId,
                            stableId,
                            StringComparison.Ordinal))
                        return true;
                    for (int index = 0;
                         index < politics.characters.Length;
                         index++)
                        if (string.Equals(
                                politics.characters[index].characterId,
                                stableId,
                                StringComparison.Ordinal))
                            return true;
                    return false;
                default:
                    return false;
            }
        }

        private static FormalSaveValidationResult
            ValidateCivilizationExpansion(
                FormalThreeDCivilizationExpansionSaveData expansion)
        {
            return ValidateCivilizationExpansionCore(
                expansion,
                ResearchEffectResolver.Resolve(Array.Empty<string>()),
                null);
        }

        private static FormalSaveValidationResult
            ValidateCivilizationExpansionWithResearch(
                FormalThreeDCivilizationExpansionSaveData expansion,
                FormalThreeDResearchSaveData research,
                FormalThreeDResearchEffectStateSaveData effectState)
        {
            return ValidateCivilizationExpansionCore(
                expansion,
                ResearchEffectResolver.Resolve(
                    research?.completedResearchIds ?? Array.Empty<string>()),
                effectState);
        }

        private static FormalSaveValidationResult
            ValidateCivilizationExpansionCore(
                FormalThreeDCivilizationExpansionSaveData expansion,
                ResearchEffectSnapshot researchEffects,
                FormalThreeDResearchEffectStateSaveData effectState)
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
                path + ".armyLeader",
                researchEffects);
            if (result != null) return result;
            result = ValidateExpansionWorld(
                expansion.worldLayer,
                path + ".worldLayer");
            if (result != null) return result;
            result = ValidateExpansionPolitics(
                expansion.charactersPolitics,
                expansion.worldLayer,
                path + ".charactersPolitics",
                effectState);
            if (result != null) return result;
            if (!string.Equals(
                    expansion.armyLeader.leader.characterId,
                    expansion.charactersPolitics.currentLeaderId,
                    StringComparison.Ordinal))
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path + ".armyLeader.leader.characterId");
            if (expansion.charactersPolitics.characters.Length > 0)
            {
                FormalThreeDCharacterSaveData current = null;
                for (var index = 0;
                     index < expansion.charactersPolitics.characters.Length;
                     index++)
                    if (string.Equals(
                            expansion.charactersPolitics.characters[index]
                                .characterId,
                            expansion.charactersPolitics.currentLeaderId,
                            StringComparison.Ordinal))
                        current = expansion.charactersPolitics
                            .characters[index];
                if (current == null ||
                    expansion.armyLeader.leader.recruited !=
                        (current.state != (int)CharacterLifeState.Dead) ||
                    expansion.armyLeader.leader.injured !=
                        (current.state != (int)CharacterLifeState.Active))
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".armyLeader.leader");
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateExpansionArmy(
            FormalThreeDArmyLeaderSaveData army,
            string path,
            ResearchEffectSnapshot researchEffects)
        {
            if (army.nextUnitOrdinal < 1 ||
                army.nextSquadOrdinal != 2 ||
                army.nextExpeditionOrdinal < 1 ||
                army.units.Length > SingleCityArmyModel
                    .DefaultSquadMaximumUnits ||
                army.leaderHealthy && !army.leaderAssigned ||
                army.leader == null ||
                !IsStableId(army.leader.characterId) ||
                !IsFinite(army.leader.x) || !IsFinite(army.leader.y))
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path);
            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            ulong maximumUnitOrdinal = 0;
            for (var index = 0; index < army.units.Length; index++)
            {
                FormalThreeDArmyUnitSaveData unit = army.units[index];
                string item = path + ".units[" + index + "]";
                ArmyUnitDefinition definition = unit == null
                    ? null
                    : ArmyUnitCatalog.Find(unit.definitionId);
                int maximumHealth = definition == null
                    ? 0
                    : Math.Max(
                        1,
                        (int)Math.Round(
                            definition.MaximumHealth *
                            researchEffects.ResolveUnitHealthMultiplier(
                                definition.Id),
                            MidpointRounding.AwayFromZero));
                if (unit == null || !IsStableId(unit.stableUnitId) ||
                    !unitIds.Add(unit.stableUnitId) || definition == null ||
                    !string.Equals(
                        unit.squadId,
                        SingleCityArmyModel.DefaultSquadId,
                        StringComparison.Ordinal) ||
                    unit.currentHealth <= 0 ||
                    unit.currentHealth > maximumHealth ||
                    !IsFinite(unit.maintenanceElapsedSeconds) ||
                    unit.maintenanceElapsedSeconds < 0f ||
                    unit.maintenanceElapsedSeconds >
                        definition.MaintenanceSeconds)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        item);
                string suffix = unit.stableUnitId.StartsWith(
                        "core.army-unit.",
                        StringComparison.Ordinal)
                    ? unit.stableUnitId.Substring(
                        "core.army-unit.".Length)
                    : string.Empty;
                if (!ulong.TryParse(
                        suffix,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out ulong ordinal) || ordinal == 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        item + ".stableUnitId");
                maximumUnitOrdinal = Math.Max(maximumUnitOrdinal, ordinal);
            }
            if (army.nextUnitOrdinal <= maximumUnitOrdinal)
                return Invalid(
                    FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextUnitOrdinal");
            var manufacturingIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < army.manufacturing.Length; index++)
            {
                FormalThreeDArmyManufacturingSaveData item =
                    army.manufacturing[index];
                ArmyUnitDefinition definition = item == null
                    ? null
                    : ArmyUnitCatalog.Find(item.definitionId);
                if (definition == null ||
                    !manufacturingIds.Add(item.definitionId) ||
                    !IsFinite(item.progressSeconds) ||
                    item.progressSeconds < 0f ||
                    item.progressSeconds > definition.ManufactureSeconds)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".manufacturing[" + index + "]");
            }
            var lossIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < army.losses.Length; index++)
            {
                FormalThreeDArmyLossSaveData item = army.losses[index];
                if (item == null || ArmyUnitCatalog.Find(item.definitionId) ==
                        null || !lossIds.Add(item.definitionId) || item.count <= 0)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".losses[" + index + "]");
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
                    squad.unitIds == null ||
                    squad.leaderAssigned != army.leaderAssigned ||
                    squad.leaderHealthy != army.leaderHealthy)
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
            if (army.expedition != null &&
                army.expedition.phase != (int)ArmyExpeditionStatus.Idle)
            {
                FormalThreeDArmyExpeditionSaveData expedition =
                    army.expedition;
                if (string.IsNullOrWhiteSpace(expedition.sessionId) ||
                    !IsFinite(expedition.remainingSeconds) ||
                    !IsFinite(expedition.outboundDurationSeconds) ||
                    !IsFinite(expedition.returnDurationSeconds) ||
                    expedition.remainingSeconds < 0f ||
                    expedition.outboundDurationSeconds < 0f ||
                    expedition.returnDurationSeconds < 0f)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".expedition");
                FormalSaveValidationResult amounts = ValidateAmounts(
                    expedition.pendingLoot,
                    path + ".expedition.pendingLoot");
                if (amounts != null) return amounts;
                for (var index = 0;
                     index < expedition.pendingLoot.Length;
                     index++)
                    if (!IsValidExpeditionLoot(
                            expedition.pendingLoot[index]))
                        return Invalid(
                            FormalSaveValidationError.InvalidArray,
                            path + ".expedition.pendingLoot[" + index + "]");
                var expeditionUnits = new HashSet<string>(
                    StringComparer.Ordinal);
                for (var index = 0; index < expedition.units.Length; index++)
                {
                    FormalThreeDArmyExpeditionUnitSaveData unit =
                        expedition.units[index];
                    if (unit == null || !IsStableId(unit.stableUnitId) ||
                        !expeditionUnits.Add(unit.stableUnitId) ||
                        ArmyUnitCatalog.Find(unit.definitionId) == null ||
                        unit.currentHealth <= 0)
                        return Invalid(
                            FormalSaveValidationError.InvalidArray,
                            path + ".expedition.units[" + index + "]");
                }
                for (var index = 0;
                     index < expedition.casualtyStableUnitIds.Length;
                     index++)
                {
                    if (!expeditionUnits.Contains(
                            expedition.casualtyStableUnitIds[index]))
                        return Invalid(
                            FormalSaveValidationError
                                .MissingStableReference,
                            path + ".expedition.casualtyStableUnitIds");
                }
                for (var index = 0;
                     index < expedition.enemyDefinitionIds.Length;
                     index++)
                {
                    if (!IsKnownEnemy(expedition.enemyDefinitionIds[index]))
                        return Invalid(
                            FormalSaveValidationError.InvalidStableId,
                            path + ".expedition.enemyDefinitionIds[" +
                            index + "]");
                }
            }
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
            var occupied = new HashSet<string>(StringComparer.Ordinal);
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
                SettlementDefinition definition = WorldLayerCatalog.Find(
                    item.stableSettlementId);
                if (definition == null || (int)definition.Kind != item.kind ||
                    item.x < 0 || item.x >= 64 ||
                    item.y < 0 || item.y >= 48 ||
                    !occupied.Add(item.x + "," + item.y))
                    return Invalid(
                        FormalSaveValidationError.InvalidWorld,
                        path + ".settlements[" + index + "]");
                FormalSaveValidationResult amounts = ValidateAmounts(
                    item.inventory,
                    path + ".settlements[" + index + "].inventory");
                if (amounts != null) return amounts;
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
                if (!string.IsNullOrWhiteSpace(item.escortSquadId) &&
                    !string.Equals(
                        item.escortSquadId,
                        SingleCityArmyModel.DefaultSquadId,
                        StringComparison.Ordinal))
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        path + ".convoys[" + index + "].escortSquadId");
                FormalSaveValidationResult cargo = ValidateAmounts(
                    item.cargo,
                    path + ".convoys[" + index + "].cargo");
                if (cargo != null) return cargo;
                for (var pointIndex = 0;
                     pointIndex < item.path.Length;
                     pointIndex++)
                {
                    if (item.path[pointIndex] == null ||
                        item.path[pointIndex].x < 0 ||
                        item.path[pointIndex].x >= 64 ||
                        item.path[pointIndex].y < 0 ||
                        item.path[pointIndex].y >= 48 ||
                        pointIndex > 0 &&
                        Math.Abs(
                            item.path[pointIndex].x -
                            item.path[pointIndex - 1].x) +
                        Math.Abs(
                            item.path[pointIndex].y -
                            item.path[pointIndex - 1].y) != 1)
                        return Invalid(
                            FormalSaveValidationError.InvalidWorld,
                            path + ".convoys[" + index + "].path[" +
                            pointIndex + "]");
                }
            }
            return null;
        }

        private static FormalSaveValidationResult ValidateExpansionPolitics(
            FormalThreeDCharactersPoliticsSaveData politics,
            FormalThreeDWorldLayerSaveData world,
            string path,
            FormalThreeDResearchEffectStateSaveData effectState)
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
            var settlementIds = new HashSet<string>(StringComparer.Ordinal);
            if (world.settlements.Length == 0)
                settlementIds.Add(WorldLayerCatalog.PrimaryCity.Id);
            else
                for (var index = 0; index < world.settlements.Length; index++)
                    settlementIds.Add(
                        world.settlements[index].stableSettlementId);
            var corpseByCharacter = new Dictionary<
                string,
                FormalThreeDCorpseSaveData>(StringComparer.Ordinal);
            for (var index = 0; index < politics.corpses.Length; index++)
            {
                FormalThreeDCorpseSaveData corpse = politics.corpses[index];
                if (corpse == null || !IsStableId(corpse.corpseId) ||
                    !IsStableId(corpse.characterId) ||
                    !settlementIds.Contains(corpse.settlementId) ||
                    corpse.equipmentIds == null ||
                    !corpseByCharacter.TryAdd(corpse.characterId, corpse))
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".corpses[" + index + "]");
            }
            var characterIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < politics.characters.Length; index++)
            {
                FormalThreeDCharacterSaveData item =
                    politics.characters[index];
                CharacterDefinition definition = item == null
                    ? null
                    : CharacterCatalog.Find(item.characterId);
                bool hasCorpse = item != null &&
                    corpseByCharacter.ContainsKey(item.characterId);
                bool hasActiveGeneSplicing = item != null &&
                    HasActiveGeneSplicingState(
                        effectState,
                        item.characterId);
                int expectedMaximumHealth = definition == null
                    ? 0
                    : CalculateCharacterMaximumHealth(
                        definition,
                        hasActiveGeneSplicing);
                if (item == null || definition == null ||
                    !characterIds.Add(item.characterId) ||
                    !Enum.IsDefined(typeof(CharacterLifeState), item.state) ||
                    !IsCharacterResearchEffectStateCompatible(
                        item,
                        effectState) ||
                    item.currentHealth < 0 ||
                    item.currentHealth > item.maximumHealth ||
                    item.maximumHealth != expectedMaximumHealth ||
                    item.loyalty < 0 || item.loyalty > 100 ||
                    !settlementIds.Contains(item.assignedSettlementId) ||
                    !IsFinite(item.downedRemainingSeconds) ||
                    !IsFinite(item.recoveryRemainingSeconds) ||
                    !IsFinite(item.downedElapsedSeconds) ||
                    item.downedRemainingSeconds < 0f ||
                    item.recoveryRemainingSeconds < 0f ||
                    item.permanentInjuryIds == null ||
                    item.equipmentIds == null ||
                    item.lastDamageRuleTick != ulong.MaxValue &&
                    item.lastDamageRuleTick > politics.revision ||
                    !CharacterStateMatches(item, hasCorpse))
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".characters[" + index + "]");
                if (item.rescue != null &&
                    !string.IsNullOrWhiteSpace(item.rescue.sourceId) &&
                    (!Enum.IsDefined(
                         typeof(CharacterRescueMethod),
                         item.rescue.method) ||
                     item.rescue.reservedBiomass !=
                         CharacterLifeRuntime.RescueBiomassCost ||
                     !IsFinite(item.rescue.remainingSeconds) ||
                     item.rescue.remainingSeconds <= 0f ||
                     !string.Equals(
                         item.rescue.targetCharacterId,
                         item.characterId,
                         StringComparison.Ordinal)))
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".characters[" + index + "].rescue");
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
                if (item == null ||
                    !IsKnownInternalFaction(item.factionId) ||
                    !factionIds.Add(item.factionId) ||
                    item.influence < 0 || item.influence > 100 ||
                    item.loyalty < 0 || item.loyalty > 100 ||
                    item.candidateSupports == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".internalFactions[" + index + "]");
                var supportedCharacters = new HashSet<string>(
                    StringComparer.Ordinal);
                for (var supportIndex = 0;
                     supportIndex < item.candidateSupports.Length;
                     supportIndex++)
                {
                    FormalThreeDFactionCandidateSupportSaveData support =
                        item.candidateSupports[supportIndex];
                    if (support == null ||
                        !characterIds.Contains(support.characterId) ||
                        !supportedCharacters.Add(support.characterId) ||
                        support.support < 0 || support.support > 100)
                        return Invalid(
                            FormalSaveValidationError.InvalidArray,
                            path + ".internalFactions[" + index +
                            "].candidateSupports");
                }
            }
            if (politics.succession != null &&
                politics.succession.phase != 0 &&
                (politics.succession.phase != 1 ||
                 !characterIds.Contains(
                     politics.succession.selectedCandidateId) ||
                 !IsFinite(politics.succession.support) ||
                 politics.succession.support < 0f ||
                 politics.succession.support > 100f))
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".succession");
            factionIds.Clear();
            for (var index = 0;
                 index < politics.externalFactions.Length;
                 index++)
            {
                FormalThreeDExternalFactionSaveData item =
                    politics.externalFactions[index];
                if (item == null ||
                    !IsKnownExternalFaction(item.factionId) ||
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
                if (item.activeOffer != null &&
                    !string.IsNullOrWhiteSpace(item.activeOffer.offerId) &&
                    !ValidateDiplomacyOffer(
                        item.activeOffer,
                        item.factionId))
                    return Invalid(
                        FormalSaveValidationError.InvalidArray,
                        path + ".externalFactions[" + index +
                        "].activeOffer");
            }
            FormalThreeDCharacterSaveData leader = null;
            for (var index = 0; index < politics.characters.Length; index++)
                if (string.Equals(
                        politics.characters[index].characterId,
                        politics.currentLeaderId,
                        StringComparison.Ordinal))
                    leader = politics.characters[index];
            if (leader == null ||
                (politics.leadershipState == 1) !=
                    (leader.state == (int)CharacterLifeState.Dead) ||
                politics.leadershipState != 0 &&
                politics.leadershipState != 1 ||
                !IsFinite(politics.councilEfficiencyMultiplier) ||
                Math.Abs(
                    politics.councilEfficiencyMultiplier -
                    (politics.leadershipState == 1 ? .75f : 1f)) > .0001f)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".leadershipState");
            return null;
        }

        private static bool CharacterStateMatches(
            FormalThreeDCharacterSaveData item,
            bool hasCorpse)
        {
            switch ((CharacterLifeState)item.state)
            {
                case CharacterLifeState.Active:
                    return item.currentHealth > 0 &&
                        item.downedRemainingSeconds == 0f &&
                        item.recoveryRemainingSeconds == 0f &&
                        !hasCorpse &&
                        (item.rescue == null ||
                         string.IsNullOrWhiteSpace(item.rescue.sourceId));
                case CharacterLifeState.Downed:
                    return item.currentHealth == 0 &&
                        item.downedRemainingSeconds > 0f &&
                        item.downCount > 0 &&
                        !string.IsNullOrWhiteSpace(item.downedCauseId) &&
                        !hasCorpse;
                case CharacterLifeState.Recovering:
                    return item.currentHealth > 0 &&
                        item.recoveryRemainingSeconds > 0f &&
                        item.downCount > 0 && !hasCorpse;
                case CharacterLifeState.Dead:
                    return item.currentHealth == 0 && hasCorpse &&
                        item.equipmentIds.Length == 0;
                default:
                    return false;
            }
        }

        private static int ResolveCharacterMaximumHealth(
            CharacterDefinition definition,
            FormalThreeDResearchEffectStateSaveData effectState)
        {
            return CalculateCharacterMaximumHealth(
                definition,
                HasActiveGeneSplicingState(
                    effectState,
                    definition.Id.Value));
        }

        private static bool IsCharacterResearchEffectStateCompatible(
            FormalThreeDCharacterSaveData character,
            FormalThreeDResearchEffectStateSaveData effectState)
        {
            return character != null &&
                (character.state != (int)CharacterLifeState.Dead ||
                 !HasActiveGeneSplicingState(
                     effectState,
                     character.characterId));
        }

        private static bool HasActiveGeneSplicingState(
            FormalThreeDResearchEffectStateSaveData effectState,
            string characterId)
        {
            FormalThreeDResearchEffectStateEntrySaveData[] states =
                effectState?.states;
            if (states != null)
            {
                for (int index = 0; index < states.Length; index++)
                {
                    FormalThreeDResearchEffectStateEntrySaveData state =
                        states[index];
                    if (state != null &&
                        string.Equals(
                            state.effectId,
                            ResearchStatusCatalog.GeneSplicingTraitId,
                            StringComparison.Ordinal) &&
                        state.targetKind ==
                            FormalResearchEffectTargetKind.Character &&
                        string.Equals(
                            state.targetStableId,
                            characterId,
                            StringComparison.Ordinal) &&
                        state.phase ==
                            FormalResearchEffectStatePhase.Active &&
                        state.remainingRuleSeconds > 0f &&
                        state.stacks > 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int CalculateCharacterMaximumHealth(
            CharacterDefinition definition,
            bool hasGeneSplicing)
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    definition.MaximumHealth *
                    (hasGeneSplicing
                        ? CharacterLifeRuntime
                            .GeneSplicingMaximumHealthMultiplier
                        : 1f),
                    MidpointRounding.AwayFromZero));
        }

        private static bool IsValidExpeditionLoot(
            FormalThreeDResourceAmountSaveData amount)
        {
            if (amount == null) return false;
            if (amount.resourceId == ResourceIds.Alloy)
                return amount.amount >= 10 && amount.amount <= 24;
            if (amount.resourceId == ResourceIds.Biomass)
                return amount.amount >= 8 && amount.amount <= 20;
            if (amount.resourceId == ResourceIds.EnergyCrystal)
                return amount.amount >= 4 && amount.amount <= 12;
            return false;
        }

        private static bool IsKnownEnemy(string id)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
                if (string.Equals(
                        EnemyCatalog.All[index].Id.Value,
                        id,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsKnownInternalFaction(string id)
        {
            for (var index = 0;
                 index < InternalFactionCatalog.All.Count;
                 index++)
                if (string.Equals(
                        InternalFactionCatalog.All[index].Id.Value,
                        id,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsKnownExternalFaction(string id)
        {
            for (var index = 0;
                 index < ExternalFactionCatalog.All.Count;
                 index++)
                if (string.Equals(
                        ExternalFactionCatalog.All[index].Id.Value,
                        id,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool ValidateDiplomacyOffer(
            FormalThreeDDiplomacyOfferSaveData offer,
            string factionId)
        {
            if (!IsStableId(offer.offerId) || !string.Equals(
                    offer.factionId,
                    factionId,
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(DiplomacyOfferKind), offer.kind) ||
                !IsFinite(offer.remainingSeconds) ||
                offer.remainingSeconds <= 0f ||
                offer.remainingSeconds > DiplomacyRuntime.OfferRefreshSeconds)
                return false;
            switch ((DiplomacyOfferKind)offer.kind)
            {
                case DiplomacyOfferKind.AlloyForStone:
                    return offer.giveResourceId == ResourceIds.Alloy &&
                        offer.giveAmount == 10 &&
                        offer.receiveResourceId == ResourceIds.Stone &&
                        offer.receiveAmount == 20;
                case DiplomacyOfferKind.BiomassForEnergyCrystal:
                    return offer.giveResourceId == ResourceIds.Biomass &&
                        offer.giveAmount == 12 &&
                        offer.receiveResourceId == ResourceIds.EnergyCrystal &&
                        offer.receiveAmount == 8;
                default:
                    return offer.giveResourceId == ResourceIds.Ammunition &&
                        offer.giveAmount == 15 &&
                        string.IsNullOrEmpty(offer.receiveResourceId) &&
                        offer.receiveAmount == 0 &&
                        offer.grantsConvoyImmunity;
            }
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
            if (progression.quantumEntanglement == null)
                return Missing(path + ".quantumEntanglement");
            if (progression.spatialTemplate == null)
                return Missing(path + ".spatialTemplate");
            if (progression.localHaste == null)
                return Missing(path + ".localHaste");
            if (progression.foresightDelay == null)
                return Missing(path + ".foresightDelay");
            if (progression.causalTransparency == null)
                return Missing(path + ".causalTransparency");
            if (progression.voidChest == null)
                return Missing(path + ".voidChest");
            if (progression.coordinateLock == null)
                return Missing(path + ".coordinateLock");

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
            result = ValidateIdea0028State(progression, path);
            if (result != null) return result;
            return null;
        }

        private static FormalSaveValidationResult ValidateIdea0028State(
            FormalThreeDProgressionSaveData progression,
            string path)
        {
            FormalSaveValidationResult result = UniqueNonBlank(
                progression.quantumEntanglement
                    .committedSynchronizationKeys,
                path + ".quantumEntanglement.committedSynchronizationKeys");
            if (result != null) return result;

            FormalThreeDSpatialTemplateEntrySaveData[] entries =
                progression.spatialTemplate.entries;
            if (entries == null || entries.Length > 9)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".spatialTemplate.entries");
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.Length; index++)
            {
                FormalThreeDSpatialTemplateEntrySaveData entry =
                    entries[index];
                string item = path + ".spatialTemplate.entries[" + index + "]";
                if (entry == null)
                    return Invalid(FormalSaveValidationError.InvalidArray,
                        item);
                if (entry.relativeX < -1 || entry.relativeX > 1 ||
                    entry.relativeZ < -1 || entry.relativeZ > 1 ||
                    entry.quarterTurns < 0 || entry.quarterTurns > 3)
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        item);
                string cell = entry.relativeX + ":" + entry.relativeZ;
                if (!occupied.Add(cell))
                    return Invalid(FormalSaveValidationError.DuplicateStableId,
                        item);
                if (!IsKnownBuildingDefinition(entry.buildingDefinitionId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        item + ".buildingDefinitionId");
            }

            FormalThreeDLocalHasteSaveData haste = progression.localHaste;
            if (haste.cycleOrdinal < 0 ||
                !IsFinite(haste.remainingBudgetSeconds) ||
                haste.remainingBudgetSeconds < 0f ||
                haste.remainingBudgetSeconds > 60f ||
                !LocalHasteRuntime.TryGetTargetKind(
                    haste.targetStableId,
                    out int expectedHasteTargetKind) ||
                haste.targetKind != expectedHasteTargetKind)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".localHaste.targetStableId");
            if (haste.targetKind == 0)
            {
                if (!string.IsNullOrEmpty(haste.targetStableId) || haste.active)
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        path + ".localHaste.targetStableId");
            }
            else if (string.IsNullOrWhiteSpace(haste.targetStableId) ||
                     (haste.active && haste.remainingBudgetSeconds <= 0f))
            {
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path + ".localHaste.targetStableId");
            }

            FormalThreeDForesightDelaySaveData foresight =
                progression.foresightDelay;
            if (foresight.displayedCycleOrdinals == null ||
                foresight.cycleOrdinal < 0 ||
                !IsFinite(foresight.remainingDisplaySeconds) ||
                foresight.remainingDisplaySeconds < 0f ||
                foresight.remainingDisplaySeconds > 3f)
                return Invalid(FormalSaveValidationError.InvalidArray,
                    path + ".foresightDelay");
            long previousCycle = 0;
            for (var index = 0;
                 index < foresight.displayedCycleOrdinals.Length;
                 index++)
            {
                long cycle = foresight.displayedCycleOrdinals[index];
                if (cycle <= previousCycle || cycle > foresight.cycleOrdinal)
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        path + ".foresightDelay.displayedCycleOrdinals[" +
                        index + "]");
                previousCycle = cycle;
            }
            if (string.IsNullOrEmpty(foresight.plannedStableEventId))
            {
                if (foresight.remainingDisplaySeconds != 0f)
                    return Invalid(
                        FormalSaveValidationError.MissingStableReference,
                        path + ".foresightDelay.plannedStableEventId");
            }
            else if (foresight.cycleOrdinal <= 0 ||
                     !IsPlannedPressureEvent(
                         progression.pressure,
                         foresight.plannedStableEventId))
            {
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path + ".foresightDelay.plannedStableEventId");
            }

            result = UniqueNonBlank(
                progression.causalTransparency.scannedStableEventKeys,
                path + ".causalTransparency.scannedStableEventKeys");
            if (result != null) return result;
            result = ValidateVoidChest(
                progression.voidChest,
                path + ".voidChest");
            if (result != null) return result;

            FormalThreeDCoordinateLockSaveData coordinate =
                progression.coordinateLock;
            bool clean = !coordinate.committed &&
                string.IsNullOrEmpty(coordinate.stableEventKey) &&
                !coordinate.bossPressureScheduled && coordinate.revision == 0;
            if (clean) return null;
            if (!coordinate.committed)
                return Invalid(FormalSaveValidationError.InvalidEnumValue,
                    path + ".coordinateLock");
            if (!string.Equals(
                    coordinate.stableEventKey,
                    CoordinateLockCatalog.StableEventKey,
                    StringComparison.Ordinal))
                return Invalid(FormalSaveValidationError.InvalidStableId,
                    path + ".coordinateLock.stableEventKey");
            if (!coordinate.bossPressureScheduled ||
                !HasCoordinateBossPressure(progression.pressure))
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
                    path + ".coordinateLock.bossPressureScheduled");
            return coordinate.revision > 0
                ? null
                : Invalid(FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".coordinateLock.revision");
        }

        private static bool HasCoordinateBossPressure(
            FormalThreeDAttentionPressureSaveData pressure)
        {
            if (pressure?.entries == null) return false;
            for (var index = 0; index < pressure.entries.Length; index++)
            {
                FormalThreeDAttentionPressureEntrySaveData entry =
                    pressure.entries[index];
                if (entry != null &&
                    entry.threshold == CoordinateLockCatalog.TargetAttention)
                    return true;
            }
            return false;
        }

        private static FormalSaveValidationResult ValidateVoidChest(
            FormalThreeDVoidChestSaveData data,
            string path)
        {
            if (data.nextDropOrdinal < 1 || data.pendingChests == null ||
                data.committedDeathEventIds == null ||
                data.claimedRewardKeys == null)
                return Invalid(FormalSaveValidationError.InvalidArray, path);
            FormalSaveValidationResult result = UniqueNonBlank(
                data.committedDeathEventIds,
                path + ".committedDeathEventIds");
            if (result != null) return result;
            result = UniqueNonBlank(
                data.claimedRewardKeys,
                path + ".claimedRewardKeys");
            if (result != null) return result;
            var deathOrdinals = new Dictionary<string, long>(
                StringComparer.Ordinal);
            var committedOrdinals = new HashSet<long>();
            for (var index = 0;
                 index < data.committedDeathEventIds.Length;
                 index++)
            {
                string encoded = data.committedDeathEventIds[index];
                string encodedPath = path + ".committedDeathEventIds[" +
                    index + "]";
                if (!TryDecodeVoidChestDeathEvaluation(
                        encoded,
                        out string deathId,
                        out long ordinal))
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        encodedPath);
                if (!deathOrdinals.TryAdd(deathId, ordinal) ||
                    !committedOrdinals.Add(ordinal))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
                        encodedPath);
                if (ordinal >= data.nextDropOrdinal)
                    return Invalid(
                        FormalSaveValidationError.InvalidHighWaterMark,
                        encodedPath);
            }
            var claimed = new HashSet<string>(
                data.claimedRewardKeys, StringComparer.Ordinal);
            var chestIds = new HashSet<string>(StringComparer.Ordinal);
            var ordinals = new HashSet<long>();
            for (var index = 0; index < data.pendingChests.Length; index++)
            {
                FormalThreeDVoidChestEntrySaveData item =
                    data.pendingChests[index];
                string itemPath = path + ".pendingChests[" + index + "]";
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.stableChestId) ||
                    string.IsNullOrWhiteSpace(item.deathEventId) ||
                    string.IsNullOrWhiteSpace(item.narrativeFragmentId) ||
                    string.IsNullOrWhiteSpace(item.rewardKey) ||
                    item.dropOrdinal < 1 ||
                    item.dropOrdinal >= data.nextDropOrdinal ||
                    item.amount <= 0 ||
                    !chestIds.Add(item.stableChestId) ||
                    !ordinals.Add(item.dropOrdinal) ||
                    !deathOrdinals.TryGetValue(
                        item.deathEventId,
                        out long committedOrdinal) ||
                    committedOrdinal != item.dropOrdinal ||
                    claimed.Contains(item.rewardKey) ||
                    !IsVoidChestResource(item.resourceId))
                    return Invalid(FormalSaveValidationError.InvalidStableId,
                        itemPath);
            }
            return null;
        }

        private static bool TryDecodeVoidChestDeathEvaluation(
            string encoded,
            out string deathId,
            out long ordinal)
        {
            deathId = string.Empty;
            ordinal = 0L;
            if (string.IsNullOrWhiteSpace(encoded)) return false;
            int lengthSeparator = encoded.IndexOf(':');
            if (lengthSeparator <= 0 || !int.TryParse(
                    encoded.Substring(0, lengthSeparator),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int deathIdLength) ||
                deathIdLength <= 0)
                return false;
            int deathIdStart = lengthSeparator + 1;
            int ordinalSeparator = deathIdStart + deathIdLength;
            if (ordinalSeparator >= encoded.Length ||
                encoded[ordinalSeparator] != ':')
                return false;
            deathId = encoded.Substring(deathIdStart, deathIdLength);
            return !string.IsNullOrWhiteSpace(deathId) && long.TryParse(
                encoded.Substring(ordinalSeparator + 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ordinal) && ordinal > 0L;
        }

        private static bool IsPlannedPressureEvent(
            FormalThreeDAttentionPressureSaveData pressure,
            string stableEventId)
        {
            if (pressure?.entries == null ||
                string.IsNullOrEmpty(stableEventId))
                return false;
            for (var index = 0; index < pressure.entries.Length; index++)
            {
                FormalThreeDAttentionPressureEntrySaveData entry =
                    pressure.entries[index];
                if (entry == null ||
                    entry.state != (int)AttentionPressureState.Queued &&
                    entry.state != (int)AttentionPressureState.Warning)
                    continue;
                AttentionPressureDefinition definition =
                    AttentionPressureCatalog.FindByThreshold(entry.threshold);
                if (definition != null && string.Equals(
                        definition.EncounterId.Value,
                        stableEventId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool IsVoidChestResource(string resourceId)
        {
            return string.Equals(resourceId, ResourceIds.Iron,
                       StringComparison.Ordinal) ||
                string.Equals(resourceId, ResourceIds.Stone,
                    StringComparison.Ordinal) ||
                string.Equals(resourceId, ResourceIds.Water,
                    StringComparison.Ordinal) ||
                string.Equals(resourceId, ResourceIds.Biomass,
                    StringComparison.Ordinal);
        }

        private static bool IsKnownBuildingDefinition(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                if (string.Equals(
                        BuildingCatalog.All[index].Id.Value,
                        id,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
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
            if (fate.offerSelectionVersion < 0 ||
                fate.offerSelectionVersion > 1)
                return Invalid(
                    FormalSaveValidationError.InvalidEnumValue,
                    path + ".offerSelectionVersion");
            if (fate.offeredIds == null)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".offeredIds");
            if (fate.offeredIds.Length != 3)
                return Invalid(
                    FormalSaveValidationError.InvalidArray,
                    path + ".offeredIds");
            var offered = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < fate.offeredIds.Length; index++)
            {
                string id = fate.offeredIds[index];
                if (FormalFateCatalog.Find(id) == null)
                    return Invalid(
                        FormalSaveValidationError.InvalidStableId,
                        path + ".offeredIds[" + index + "]");
                if (!offered.Add(id))
                    return Invalid(
                        FormalSaveValidationError.DuplicateStableId,
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

            if (!offered.Contains(fate.selectedId))
                return Invalid(
                    FormalSaveValidationError.MissingStableReference,
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
            var hostileCurrentWaveCount = 0;
            var controlledCurrentWaveCount = 0;
            for (var index = 0; index < campaign.enemyStates.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyStateSaveData enemy =
                    campaign.enemyStates[index];
                if (!TryParseCampaignEnemySlot(
                        enemy.stableEnemyId,
                        out int waveNumber,
                        out int spawnOrder) ||
                    spawnOrder != enemy.spawnOrder ||
                    waveNumber > campaign.currentWaveNumber ||
                    !enemy.isControlled &&
                    waveNumber != campaign.currentWaveNumber)
                    return Invalid(
                        FormalSaveValidationError.InvalidDefense,
                        path + ".enemyStates[" + index + "]");
                if (waveNumber != campaign.currentWaveNumber) continue;
                if (enemy.isControlled) controlledCurrentWaveCount++;
                else hostileCurrentWaveCount++;
            }
            if (hostileCurrentWaveCount !=
                totalSpawned - totalDefeated -
                    controlledCurrentWaveCount)
                return Invalid(FormalSaveValidationError.InvalidDefense,
                    path + ".enemyStates");
            if (campaign.nextEnemyOrdinal < totalSpawned)
                return Invalid(FormalSaveValidationError.InvalidHighWaterMark,
                    path + ".nextEnemyOrdinal");
            return null;
        }

        private static bool TryParseCampaignEnemySlot(
            string stableId,
            out int waveNumber,
            out int spawnOrder)
        {
            const string prefix = "campaign.enemy.wave-";
            waveNumber = 0;
            spawnOrder = -1;
            if (string.IsNullOrEmpty(stableId) ||
                !stableId.StartsWith(prefix, StringComparison.Ordinal) ||
                stableId.Length != prefix.Length + 7 ||
                stableId[prefix.Length + 2] != '.') return false;
            return int.TryParse(
                    stableId.Substring(prefix.Length, 2),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out waveNumber) &&
                waveNumber > 0 &&
                int.TryParse(
                    stableId.Substring(prefix.Length + 3, 4),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out spawnOrder) &&
                spawnOrder >= 0;
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
                statistics.completedProductionBatchCount < 0 ||
                statistics.controlledUnitLossCount < 0)
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

        private static bool TryParseResearchStateOrdinal(
            string value,
            out long ordinal)
        {
            const string prefix = "research.state.";
            ordinal = 0L;
            if (string.IsNullOrEmpty(value) ||
                !value.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            string suffix = value.Substring(prefix.Length);
            return suffix.Length == 6 &&
                long.TryParse(
                    suffix,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out ordinal) &&
                ordinal > 0L;
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

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
