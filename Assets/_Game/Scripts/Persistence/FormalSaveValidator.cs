using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Persistence.ThreeD;

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
            result = ValidateResearch(data.research);
            if (result != null) return result;
            result = ValidateProduction(data.production, buildingIds, nodeIds);
            if (result != null) return result;
            result = ValidateDefense(data.defense, buildingIds);
            if (result != null) return result;
            result = ValidateEvacuation(data.evacuation, buildingIds);
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
            if (data.evacuation == null) return Missing("formal3D.evacuation");
            if (data.pause == null) return Missing("formal3D.pause");
            return null;
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
                if (instance.state < 0 || instance.state > 2)
                    return Invalid(FormalSaveValidationError.InvalidEnumValue,
                        item + ".state");
                FormalSaveValidationResult result = NonNegativeFinite(
                    instance.constructionRemainingSeconds,
                    item + ".constructionRemainingSeconds");
                if (result != null) return result;
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
            FormalThreeDResearchSaveData research)
        {
            const string path = "formal3D.research";
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
    }
}
