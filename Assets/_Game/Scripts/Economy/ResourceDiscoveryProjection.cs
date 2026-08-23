using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Economy
{
    public sealed class ResourceDiscoveryFacts
    {
        private readonly HashSet<string> ownedResourceIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedResearchIds =
            new HashSet<string>(StringComparer.Ordinal);

        public ResourceDiscoveryFacts(
            IEnumerable<ResourceAmount> cityNetwork,
            IEnumerable<ResourceAmount> backpack,
            IEnumerable<ResourceAmount> productionInput,
            IEnumerable<ResourceAmount> productionOutput,
            IEnumerable<ResourceAmount> productionReserved,
            IEnumerable<ResourceAmount> craftingReserved,
            IEnumerable<string> completedResearchIds)
        {
            AddOwned(cityNetwork);
            AddOwned(backpack);
            AddOwned(productionInput);
            AddOwned(productionOutput);
            AddOwned(productionReserved);
            AddOwned(craftingReserved);

            if (completedResearchIds == null)
            {
                return;
            }

            foreach (string researchId in completedResearchIds)
            {
                if (!string.IsNullOrWhiteSpace(researchId))
                {
                    this.completedResearchIds.Add(researchId);
                }
            }
        }

        public bool Owns(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                ownedResourceIds.Contains(resourceId);
        }

        public bool HasCompletedResearch(string researchId)
        {
            return !string.IsNullOrWhiteSpace(researchId) &&
                completedResearchIds.Contains(researchId);
        }

        private void AddOwned(IEnumerable<ResourceAmount> amounts)
        {
            if (amounts == null)
            {
                return;
            }

            foreach (ResourceAmount amount in amounts)
            {
                if (amount.Amount > 0 &&
                    !string.IsNullOrWhiteSpace(amount.ResourceId))
                {
                    ownedResourceIds.Add(amount.ResourceId);
                }
            }
        }
    }

    public static class ResourceDiscoveryProjection
    {
        public static IReadOnlyList<ResourceAmount>
            ProjectOwnedStorageAmounts(CityResourceStorageSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return Array.Empty<ResourceAmount>();
            }

            var owned = new List<ResourceAmount>();
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                int total = snapshot.GetCoreAmount(definition.Id);
                for (var index = 0;
                     index < snapshot.Warehouses.Count;
                     index++)
                {
                    WarehouseStorageSnapshot warehouse =
                        snapshot.Warehouses[index];
                    if (warehouse.Amounts.TryGetValue(
                            definition.Id,
                            out int amount) &&
                        amount > 0)
                    {
                        total += amount;
                    }
                }

                if (total > 0)
                {
                    owned.Add(new ResourceAmount(definition.Id, total));
                }
            }

            return new ReadOnlyCollection<ResourceAmount>(owned);
        }

        public static bool IsDiscovered(
            ResourceDefinition definition,
            ResourceDiscoveryFacts facts)
        {
            if (definition == null || facts == null)
            {
                return false;
            }

            if (definition.DiscoveryRule == ResourceDiscoveryRule.Always)
            {
                return true;
            }

            if (facts.Owns(definition.Id))
            {
                return true;
            }

            switch (definition.DiscoveryRule)
            {
                case ResourceDiscoveryRule.OwnedOrResearch:
                case ResourceDiscoveryRule.OwnedOrRecipe:
                    return HasAnyCompletedRequirement(definition, facts);
                case ResourceDiscoveryRule.OwnedOrAllRequirements:
                    return HasAllCompletedRequirements(definition, facts);
                default:
                    return false;
            }
        }

        public static IReadOnlyList<ResourceDefinition> Project(
            ResourceDiscoveryFacts facts)
        {
            var discovered = new List<ResourceDefinition>();
            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                if (IsDiscovered(definition, facts))
                {
                    discovered.Add(definition);
                }
            }

            return new ReadOnlyCollection<ResourceDefinition>(discovered);
        }

        private static bool HasAnyCompletedRequirement(
            ResourceDefinition definition,
            ResourceDiscoveryFacts facts)
        {
            foreach (string researchId in definition.RequiredResearchIds)
            {
                if (facts.HasCompletedResearch(researchId))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasAllCompletedRequirements(
            ResourceDefinition definition,
            ResourceDiscoveryFacts facts)
        {
            if (definition.RequiredResearchIds.Count == 0)
            {
                return false;
            }

            foreach (string researchId in definition.RequiredResearchIds)
            {
                if (!facts.HasCompletedResearch(researchId))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
