using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Economy;

namespace WasteCity.World.CivilizationExpansion
{
    public enum SettlementKind
    {
        PrimaryCity,
        SecondaryCity,
        Outpost,
    }

    public enum SettlementAutonomyTemplate
    {
        PrimaryReference,
        Industrial,
        Military,
        Research,
        OutpostStone,
    }

    public sealed class SettlementDefinition
    {
        internal SettlementDefinition(
            string id,
            SettlementKind kind,
            IReadOnlyList<ResourceAmount> buildCosts,
            int populationCost,
            int inventoryCapacity,
            int initialPopulation,
            int populationCapacity)
        {
            Id = id;
            Kind = kind;
            BuildCosts = buildCosts ??
                new ReadOnlyCollection<ResourceAmount>(
                    Array.Empty<ResourceAmount>());
            PopulationCost = Math.Max(0, populationCost);
            InventoryCapacity = Math.Max(0, inventoryCapacity);
            InitialPopulation = Math.Max(0, initialPopulation);
            PopulationCapacity = Math.Max(0, populationCapacity);
        }

        public string Id { get; }
        public SettlementKind Kind { get; }
        public IReadOnlyList<ResourceAmount> BuildCosts { get; }
        public int PopulationCost { get; }
        public int InventoryCapacity { get; }
        public int InitialPopulation { get; }
        public int PopulationCapacity { get; }
    }

    public static class WorldLayerCatalog
    {
        public const float AutonomyCycleSeconds = 10f;
        public const float OutpostCycleSeconds = 12f;
        public const float ResearchContributionMultiplier = 1.2f;
        public const float ConvoySecondsPerCell = 1.5f;
        public const int UnescortedInterceptionPercent = 25;
        public const int EscortedInterceptionPercent = 5;
        public const int InitialSettlementLoyalty = 70;

        private static readonly IReadOnlyList<ResourceAmount> NoCosts =
            Array.AsReadOnly(Array.Empty<ResourceAmount>());

        public static readonly SettlementDefinition PrimaryCity =
            new SettlementDefinition(
                "core.city.000001",
                SettlementKind.PrimaryCity,
                NoCosts,
                0,
                0,
                0,
                0);

        public static readonly SettlementDefinition SecondaryCity =
            new SettlementDefinition(
                "core.city.000002",
                SettlementKind.SecondaryCity,
                Array.AsReadOnly(new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 40),
                    new ResourceAmount(ResourceIds.RefinedStone, 30),
                    new ResourceAmount(ResourceIds.ControlChip, 10),
                }),
                50,
                150,
                50,
                100);

        public static readonly SettlementDefinition Outpost =
            new SettlementDefinition(
                "core.outpost.000001",
                SettlementKind.Outpost,
                Array.AsReadOnly(new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 12),
                    new ResourceAmount(ResourceIds.Stone, 12),
                }),
                0,
                150,
                0,
                0);

        public static readonly IReadOnlyList<SettlementDefinition> All =
            Array.AsReadOnly(new[] { PrimaryCity, SecondaryCity, Outpost });

        public static SettlementDefinition Find(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            for (var index = 0; index < All.Count; index++)
            {
                if (string.Equals(
                        All[index].Id,
                        stableId,
                        StringComparison.Ordinal))
                    return All[index];
            }
            return null;
        }
    }
}
