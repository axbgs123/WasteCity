using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Economy
{
    public sealed class ResourceDefinition
    {
        public string Id { get; }
        public string ChineseName { get; }
        public int StackLimit { get; }
        public string IconFallbackKey { get; }
        public int FormalInitialCityAmount { get; }

        internal ResourceDefinition(
            string id,
            string chineseName,
            int stackLimit,
            string iconFallbackKey,
            int formalInitialCityAmount)
        {
            Id = id;
            ChineseName = chineseName;
            StackLimit = stackLimit;
            IconFallbackKey = iconFallbackKey;
            FormalInitialCityAmount = formalInitialCityAmount;
        }
    }

    public static class ResourceDefinitionCatalog
    {
        private const int DefaultStackLimit = 100;

        private static readonly ReadOnlyCollection<ResourceDefinition> all =
            Array.AsReadOnly(new[]
            {
                Define(ResourceIds.Iron, "铁矿", formalInitialCityAmount: 20),
                Define(ResourceIds.EnergyCrystal, "能晶"),
                Define(ResourceIds.Stone, "石料"),
                Define(ResourceIds.Biomass, "生物质", formalInitialCityAmount: 10),
                Define(ResourceIds.Water, "水"),
                Define(ResourceIds.Alloy, "合金", formalInitialCityAmount: 20),
                Define(ResourceIds.Ammunition, "弹药", formalInitialCityAmount: 30),
                Define(ResourceIds.SpiritIron, "灵铁"),
                Define(ResourceIds.FlyingSword, "飞剑"),
                Define(ResourceIds.BoneSteel, "骨钢"),
                Define(ResourceIds.BiomassConcentrate, "生物质浓缩液"),
                Define(ResourceIds.BiologicalWeapon, "生物武器"),
                Define(ResourceIds.ResonanceMetal, "共振金属"),
                Define(ResourceIds.PsionicAmplifier, "灵能增幅器"),
                Define(ResourceIds.Elixir, "灵丹")
            });

        private static readonly ReadOnlyCollection<string> baseHudResourceIds =
            Array.AsReadOnly(new[]
            {
                ResourceIds.Iron,
                ResourceIds.EnergyCrystal,
                ResourceIds.Stone,
                ResourceIds.Biomass,
                ResourceIds.Water
            });

        private static readonly IReadOnlyDictionary<string, ResourceDefinition> byId =
            BuildLookup();

        public static IReadOnlyList<ResourceDefinition> All => all;

        public static IReadOnlyList<string> BaseHudResourceIds => baseHudResourceIds;

        public static ResourceInventory CreateFormalCityInventory()
        {
            var inventory = new ResourceInventory(int.MaxValue);
            foreach (ResourceDefinition definition in all)
            {
                if (definition.FormalInitialCityAmount > 0)
                {
                    inventory.Add(
                        definition.Id,
                        definition.FormalInitialCityAmount);
                }
            }

            return inventory;
        }

        public static bool TryGet(string resourceId, out ResourceDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(resourceId) &&
                   byId.TryGetValue(resourceId, out definition);
        }

        private static ResourceDefinition Define(
            string id,
            string chineseName,
            int formalInitialCityAmount = 0)
        {
            return new ResourceDefinition(
                id,
                chineseName,
                DefaultStackLimit,
                id,
                formalInitialCityAmount);
        }

        private static IReadOnlyDictionary<string, ResourceDefinition> BuildLookup()
        {
            var definitions = new Dictionary<string, ResourceDefinition>(
                all.Count,
                StringComparer.Ordinal);

            foreach (ResourceDefinition definition in all)
            {
                definitions.Add(definition.Id, definition);
            }

            return new ReadOnlyDictionary<string, ResourceDefinition>(definitions);
        }
    }
}
