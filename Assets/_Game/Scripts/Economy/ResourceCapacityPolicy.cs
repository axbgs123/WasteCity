using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public sealed class ResourceCapacityPolicy
    {
        public const int FormalBaseCapacityPerResource = 150;
        public const int FormalCapacityPerWarehouse = 150;

        private static readonly HashSet<string> RegisteredResourceIds =
            new HashSet<string>(ResourceIds.All, StringComparer.Ordinal);

        private readonly int baseCapacityPerResource;
        private readonly int capacityPerWarehouse;

        public ResourceCapacityPolicy(
            int baseCapacityPerResource = FormalBaseCapacityPerResource,
            int capacityPerWarehouse = FormalCapacityPerWarehouse)
        {
            this.baseCapacityPerResource = Math.Max(0, baseCapacityPerResource);
            this.capacityPerWarehouse = Math.Max(0, capacityPerWarehouse);
        }

        public int GetCapacityPerResource(int activeWarehouseCount)
        {
            long warehouseCount = Math.Max(0, activeWarehouseCount);
            long capacity = baseCapacityPerResource + warehouseCount * capacityPerWarehouse;
            return capacity >= int.MaxValue ? int.MaxValue : (int)capacity;
        }

        public int GetAcceptableAmount(
            ResourceInventory inventory,
            string resourceId,
            int requestedAmount,
            int activeWarehouseCount)
        {
            if (inventory == null ||
                !IsRegisteredResource(resourceId) ||
                requestedAmount <= 0)
            {
                return 0;
            }

            int current = inventory.Get(resourceId);
            long policySpace = (long)GetCapacityPerResource(activeWarehouseCount) - current;
            long ledgerSpace = (long)inventory.CapacityPerResource - current;
            long acceptable = Math.Min(requestedAmount, Math.Min(policySpace, ledgerSpace));
            return acceptable <= 0 ? 0 : (int)acceptable;
        }

        public int Add(
            ResourceInventory inventory,
            string resourceId,
            int requestedAmount,
            int activeWarehouseCount)
        {
            int acceptable = GetAcceptableAmount(
                inventory,
                resourceId,
                requestedAmount,
                activeWarehouseCount);
            return acceptable <= 0 ? 0 : inventory.Add(resourceId, acceptable);
        }

        internal static bool IsRegisteredResource(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                   RegisteredResourceIds.Contains(resourceId);
        }
    }
}
