using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;

namespace WasteCity.Economy
{
    public sealed class FormalProductionDefinition
    {
        private readonly int legacyOutputAmount;

        public string Id { get; }
        public string BuildingId { get; }
        public float DurationSeconds { get; }
        public ReadOnlyCollection<ResourceAmount> Inputs { get; }
        public ReadOnlyCollection<ResourceAmount> Outputs { get; }
        public string InputResourceId => Inputs.Count == 0
            ? null
            : Inputs[0].ResourceId;
        public int InputAmount => Inputs.Count == 0
            ? 0
            : Inputs[0].Amount;
        public string OutputResourceId => Outputs.Count == 0
            ? null
            : Outputs[0].ResourceId;
        public int OutputAmount => Outputs.Count == 0
            ? legacyOutputAmount
            : Outputs[0].Amount;
        public int InputCapacity { get; }
        public int OutputCapacity { get; }
        public bool UsesBoundResourceNode { get; }

        internal FormalProductionDefinition(
            string id,
            string buildingId,
            float durationSeconds,
            string inputResourceId,
            int inputAmount,
            string outputResourceId,
            int outputAmount,
            int inputCapacity,
            int outputCapacity,
            bool usesBoundResourceNode)
            : this(
                id,
                buildingId,
                durationSeconds,
                string.IsNullOrWhiteSpace(inputResourceId) || inputAmount <= 0
                    ? Array.Empty<ResourceAmount>()
                    : new[] { new ResourceAmount(inputResourceId, inputAmount) },
                string.IsNullOrWhiteSpace(outputResourceId) || outputAmount <= 0
                    ? Array.Empty<ResourceAmount>()
                    : new[] { new ResourceAmount(outputResourceId, outputAmount) },
                inputCapacity,
                outputCapacity,
                usesBoundResourceNode)
        {
            legacyOutputAmount = Math.Max(0, outputAmount);
        }

        internal FormalProductionDefinition(
            string id,
            string buildingId,
            float durationSeconds,
            IReadOnlyList<ResourceAmount> inputs,
            IReadOnlyList<ResourceAmount> outputs,
            int inputCapacity,
            int outputCapacity,
            bool usesBoundResourceNode)
        {
            Id = id;
            BuildingId = buildingId;
            DurationSeconds = durationSeconds;
            Inputs = Snapshot(inputs);
            Outputs = Snapshot(outputs);
            InputCapacity = inputCapacity;
            OutputCapacity = outputCapacity;
            UsesBoundResourceNode = usesBoundResourceNode;
            legacyOutputAmount = 0;
        }

        private static ReadOnlyCollection<ResourceAmount> Snapshot(
            IReadOnlyList<ResourceAmount> values)
        {
            var snapshot = new List<ResourceAmount>();
            if (values != null)
            {
                for (int index = 0; index < values.Count; index++)
                    snapshot.Add(values[index]);
            }

            return new ReadOnlyCollection<ResourceAmount>(snapshot);
        }
    }

    public static class FormalProductionDefinitionCatalog
    {
        public static readonly FormalProductionDefinition Extraction =
            new FormalProductionDefinition(
                "core.production.extract-node-resource",
                BuildingCatalog.MiningStation.Id.Value,
                durationSeconds: 3f,
                inputResourceId: null,
                inputAmount: 0,
                outputResourceId: null,
                outputAmount: 1,
                inputCapacity: 0,
                outputCapacity: 20,
                usesBoundResourceNode: true);

        public static readonly FormalProductionDefinition Smelting =
            new FormalProductionDefinition(
                "core.production.smelt-alloy",
                BuildingCatalog.Smelter.Id.Value,
                durationSeconds: 6f,
                inputResourceId: ResourceIds.Iron,
                inputAmount: 2,
                outputResourceId: ResourceIds.Alloy,
                outputAmount: 1,
                inputCapacity: 20,
                outputCapacity: 10,
                usesBoundResourceNode: false);

        public static readonly FormalProductionDefinition Assembly =
            new FormalProductionDefinition(
                "core.production.assemble-ammunition",
                BuildingCatalog.Assembler.Id.Value,
                durationSeconds: 6f,
                inputResourceId: ResourceIds.Alloy,
                inputAmount: 2,
                outputResourceId: ResourceIds.Ammunition,
                outputAmount: 2,
                inputCapacity: 20,
                outputCapacity: 30,
                usesBoundResourceNode: false);

        private static readonly ReadOnlyCollection<FormalProductionDefinition> all =
            Array.AsReadOnly(new[]
            {
                Extraction,
                Smelting,
                Assembly
            });

        private static readonly IReadOnlyDictionary<string, FormalProductionDefinition> byId =
            BuildLookup(definition => definition.Id);

        private static readonly IReadOnlyDictionary<string, FormalProductionDefinition> byBuildingId =
            BuildLookup(definition => definition.BuildingId);

        public static IReadOnlyList<FormalProductionDefinition> All => all;

        public static bool TryGet(
            string definitionId,
            out FormalProductionDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(definitionId) &&
                   byId.TryGetValue(definitionId, out definition);
        }

        public static bool TryGetByBuildingId(
            string buildingId,
            out FormalProductionDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(buildingId) &&
                   byBuildingId.TryGetValue(buildingId, out definition);
        }

        private static IReadOnlyDictionary<string, FormalProductionDefinition> BuildLookup(
            Func<FormalProductionDefinition, string> keySelector)
        {
            var lookup = new Dictionary<string, FormalProductionDefinition>(
                all.Count,
                StringComparer.Ordinal);
            foreach (FormalProductionDefinition definition in all)
                lookup.Add(keySelector(definition), definition);
            return new ReadOnlyDictionary<string, FormalProductionDefinition>(lookup);
        }
    }
}
