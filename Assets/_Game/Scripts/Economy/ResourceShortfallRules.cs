using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;

namespace WasteCity.Economy
{
    public readonly struct ResourceShortfall
    {
        public ResourceShortfall(
            string resourceId,
            int owned,
            int required)
        {
            ResourceId = resourceId;
            Owned = Math.Max(0, owned);
            Required = Math.Max(0, required);
            Missing = Math.Max(0, Required - Owned);
        }

        public string ResourceId { get; }
        public int Owned { get; }
        public int Required { get; }
        public int Missing { get; }
    }

    public static class ResourceShortfallRules
    {
        private static readonly IReadOnlyList<ResourceShortfall> Empty =
            Array.Empty<ResourceShortfall>();

        public static IReadOnlyList<ResourceShortfall> EvaluateBuilding(
            BuildingDefinition definition,
            Func<string, int> availableAmount)
        {
            if (!TryEvaluateBuilding(
                    definition,
                    availableAmount,
                    out ResourceShortfall shortfall))
                return Empty;
            return Array.AsReadOnly(new[]
            {
                shortfall
            });
        }

        public static bool TryEvaluateBuilding(
            BuildingDefinition definition,
            Func<string, int> availableAmount,
            out ResourceShortfall shortfall)
        {
            shortfall = default;
            if (definition == null || availableAmount == null ||
                definition.Cost <= 0 ||
                !ResourceCapacityPolicy.IsRegisteredResource(
                    definition.CostId))
            {
                return false;
            }
            int owned = Math.Max(0, availableAmount(definition.CostId));
            if (owned >= definition.Cost) return false;
            shortfall = new ResourceShortfall(
                definition.CostId,
                owned,
                definition.Cost);
            return true;
        }

        public static IReadOnlyList<ResourceShortfall> Evaluate(
            IReadOnlyList<ResourceAmount> requirements,
            Func<string, int> availableAmount)
        {
            if (requirements == null || requirements.Count == 0 ||
                availableAmount == null)
            {
                return Empty;
            }

            var orderedIds = new List<string>(requirements.Count);
            var requiredById = new Dictionary<string, int>(
                requirements.Count,
                StringComparer.Ordinal);
            for (var index = 0; index < requirements.Count; index++)
            {
                ResourceAmount requirement = requirements[index];
                if (requirement.Amount <= 0 ||
                    !ResourceCapacityPolicy.IsRegisteredResource(
                        requirement.ResourceId))
                {
                    continue;
                }
                if (!requiredById.TryGetValue(
                        requirement.ResourceId,
                        out int required))
                {
                    orderedIds.Add(requirement.ResourceId);
                }
                long combined = (long)required + requirement.Amount;
                requiredById[requirement.ResourceId] = combined >= int.MaxValue
                    ? int.MaxValue
                    : (int)combined;
            }

            List<ResourceShortfall> shortfalls = null;
            for (var index = 0; index < orderedIds.Count; index++)
            {
                string resourceId = orderedIds[index];
                int owned = Math.Max(0, availableAmount(resourceId));
                int required = requiredById[resourceId];
                if (owned >= required) continue;
                if (shortfalls == null)
                    shortfalls = new List<ResourceShortfall>();
                shortfalls.Add(new ResourceShortfall(
                    resourceId,
                    owned,
                    required));
            }
            return shortfalls == null
                ? Empty
                : new ReadOnlyCollection<ResourceShortfall>(shortfalls);
        }
    }
}
