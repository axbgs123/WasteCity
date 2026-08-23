using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            ProjectRequiredRecipe("core.production.extract-node-resource");

        public static readonly FormalProductionDefinition Smelting =
            ProjectRequiredRecipe("core.production.smelt-alloy");

        public static readonly FormalProductionDefinition Assembly =
            ProjectRequiredRecipe("core.production.assemble-ammunition");

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

        private static readonly Dictionary<string, FormalProductionDefinition>
            resolvedRecipeDefinitions =
                new Dictionary<string, FormalProductionDefinition>(
                    StringComparer.Ordinal);

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

        public static bool TryResolveRecipe(
            string recipeId,
            string buildingId,
            out FormalProductionDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(recipeId) ||
                string.IsNullOrWhiteSpace(buildingId) ||
                !ResourceRecipeCatalog.TryGet(
                    recipeId,
                    out ResourceRecipeDefinition recipe) ||
                recipe.Kind != ResourceRecipeKind.Machine ||
                !ContainsOrdinal(recipe.AllowedBuildingIds, buildingId))
            {
                return false;
            }

            if (byId.TryGetValue(recipeId, out definition))
            {
                return string.Equals(
                    definition.BuildingId,
                    buildingId,
                    StringComparison.Ordinal);
            }

            string cacheKey = buildingId + "\n" + recipeId;
            if (resolvedRecipeDefinitions.TryGetValue(cacheKey, out definition))
                return true;

            definition = ProjectRecipe(recipe, buildingId);
            resolvedRecipeDefinitions.Add(cacheKey, definition);
            return true;
        }

        private static FormalProductionDefinition ProjectRequiredRecipe(
            string recipeId)
        {
            if (!ResourceRecipeCatalog.TryGet(
                    recipeId,
                    out ResourceRecipeDefinition recipe) ||
                recipe.Kind != ResourceRecipeKind.Machine ||
                recipe.AllowedBuildingIds.Count != 1)
            {
                throw new InvalidOperationException(
                    $"正式机器配方 {recipeId} 缺失或建筑归属不唯一。");
            }

            return ProjectRecipe(recipe, recipe.AllowedBuildingIds[0]);
        }

        private static FormalProductionDefinition ProjectRecipe(
            ResourceRecipeDefinition recipe,
            string buildingId)
        {
            return recipe.UsesBoundResourceNode
                ? new FormalProductionDefinition(
                    recipe.Id,
                    buildingId,
                    recipe.DurationSeconds,
                    inputResourceId: null,
                    inputAmount: 0,
                    outputResourceId: null,
                    outputAmount: recipe.BoundResourceNodeOutputAmount,
                    inputCapacity: recipe.InputCapacity,
                    outputCapacity: recipe.OutputCapacity,
                    usesBoundResourceNode: true)
                : new FormalProductionDefinition(
                    recipe.Id,
                    buildingId,
                    recipe.DurationSeconds,
                    recipe.Inputs,
                    recipe.Outputs,
                    recipe.InputCapacity,
                    recipe.OutputCapacity,
                    usesBoundResourceNode: false);
        }

        private static bool ContainsOrdinal(
            IReadOnlyList<string> values,
            string expected)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(
                        values[index],
                        expected,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
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
