using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Research;

namespace WasteCity.Economy
{
    public enum ResourceRecipeKind
    {
        Machine,
        ManualCrafting,
    }

    public sealed class ResourceRecipeDefinition
    {
        internal ResourceRecipeDefinition(
            string id,
            ResourceRecipeKind kind,
            IReadOnlyList<ResourceAmount> inputs,
            IReadOnlyList<ResourceAmount> outputs,
            float durationSeconds,
            string requiredResearchId,
            bool usesBoundResourceNode = false,
            int boundResourceNodeOutputAmount = 0)
        {
            Id = id;
            Kind = kind;
            Inputs = Snapshot(inputs);
            Outputs = Snapshot(outputs);
            DurationSeconds = Math.Max(0f, durationSeconds);
            RequiredResearchId = requiredResearchId;
            UsesBoundResourceNode = usesBoundResourceNode;
            BoundResourceNodeOutputAmount = usesBoundResourceNode
                ? Math.Max(0, boundResourceNodeOutputAmount)
                : 0;
        }

        public string Id { get; }
        public ResourceRecipeKind Kind { get; }
        public ReadOnlyCollection<ResourceAmount> Inputs { get; }
        public ReadOnlyCollection<ResourceAmount> Outputs { get; }
        public float DurationSeconds { get; }
        public string RequiredResearchId { get; }
        public bool UsesBoundResourceNode { get; }
        public int BoundResourceNodeOutputAmount { get; }

        private static ReadOnlyCollection<ResourceAmount> Snapshot(
            IReadOnlyList<ResourceAmount> values)
        {
            var snapshot = new List<ResourceAmount>();
            if (values != null)
                for (int index = 0; index < values.Count; index++)
                    snapshot.Add(values[index]);
            return new ReadOnlyCollection<ResourceAmount>(snapshot);
        }
    }

    public static class ResourceRecipeCatalog
    {
        public const string FieldAlloyId = "core.crafting.field-alloy";
        public const string FieldAmmunitionId =
            "core.crafting.field-ammunition";

        private static readonly ReadOnlyCollection<ResourceRecipeDefinition>
            all = Array.AsReadOnly(new[]
            {
                FromMachine(FormalProductionDefinitionCatalog.Extraction),
                FromMachine(FormalProductionDefinitionCatalog.Smelting),
                FromMachine(FormalProductionDefinitionCatalog.Assembly),
                new ResourceRecipeDefinition(
                    FieldAlloyId,
                    ResourceRecipeKind.ManualCrafting,
                    new[] { new ResourceAmount(ResourceIds.Iron, 4) },
                    new[] { new ResourceAmount(ResourceIds.Alloy, 1) },
                    12f,
                    DemoResearchCatalog.BasicMetallurgyId),
                new ResourceRecipeDefinition(
                    FieldAmmunitionId,
                    ResourceRecipeKind.ManualCrafting,
                    new[] { new ResourceAmount(ResourceIds.Alloy, 4) },
                    new[] { new ResourceAmount(ResourceIds.Ammunition, 2) },
                    12f,
                    DemoResearchCatalog.AmmunitionAssemblyId),
            });

        private static readonly IReadOnlyDictionary<
            string,
            ResourceRecipeDefinition> byId = BuildLookup();

        public static ReadOnlyCollection<ResourceRecipeDefinition> All => all;

        public static bool TryGet(
            string recipeId,
            out ResourceRecipeDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(recipeId) &&
                byId.TryGetValue(recipeId, out definition);
        }

        private static ResourceRecipeDefinition FromMachine(
            FormalProductionDefinition definition)
        {
            ResourceAmount[] inputs = definition.InputAmount <= 0 ||
                string.IsNullOrWhiteSpace(definition.InputResourceId)
                ? Array.Empty<ResourceAmount>()
                : new[]
                {
                    new ResourceAmount(
                        definition.InputResourceId,
                        definition.InputAmount),
                };
            ResourceAmount[] outputs = definition.OutputAmount <= 0 ||
                string.IsNullOrWhiteSpace(definition.OutputResourceId)
                ? Array.Empty<ResourceAmount>()
                : new[]
                {
                    new ResourceAmount(
                        definition.OutputResourceId,
                        definition.OutputAmount),
                };
            return new ResourceRecipeDefinition(
                definition.Id,
                ResourceRecipeKind.Machine,
                inputs,
                outputs,
                definition.DurationSeconds,
                requiredResearchId: null,
                usesBoundResourceNode: definition.UsesBoundResourceNode,
                boundResourceNodeOutputAmount: definition.OutputAmount);
        }

        private static IReadOnlyDictionary<string, ResourceRecipeDefinition>
            BuildLookup()
        {
            var lookup = new Dictionary<string, ResourceRecipeDefinition>(
                all.Count,
                StringComparer.Ordinal);
            foreach (ResourceRecipeDefinition definition in all)
                lookup.Add(definition.Id, definition);
            return new ReadOnlyDictionary<string, ResourceRecipeDefinition>(
                lookup);
        }
    }
}
