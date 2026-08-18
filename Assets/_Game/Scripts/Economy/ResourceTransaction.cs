using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public readonly struct ResourceAmount
    {
        public string ResourceId { get; }
        public int Amount { get; }

        public ResourceAmount(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }
    }

    public enum ResourceTransferStatus
    {
        Completed,
        Partial,
        InvalidRequest,
        SourceEmpty,
        TargetFull,
        CommitFailed
    }

    public readonly struct ResourceTransferResult
    {
        public int RequestedAmount { get; }
        public int MovedAmount { get; }
        public int RemainingAmount => Math.Max(0, RequestedAmount - MovedAmount);
        public ResourceTransferStatus Status { get; }
        public bool Succeeded => MovedAmount > 0;

        public ResourceTransferResult(
            int requestedAmount,
            int movedAmount,
            ResourceTransferStatus status)
        {
            RequestedAmount = Math.Max(0, requestedAmount);
            MovedAmount = Math.Max(0, Math.Min(RequestedAmount, movedAmount));
            Status = status;
        }
    }

    public static class ResourceTransaction
    {
        public static bool TrySpendAll(
            ResourceInventory inventory,
            params ResourceAmount[] requirements)
        {
            if (inventory == null ||
                !TryAggregate(requirements, out Dictionary<string, int> totals) ||
                !CanSpendAll(inventory, totals))
            {
                return false;
            }

            Dictionary<string, int> before = Capture(inventory, totals.Keys);
            try
            {
                foreach (KeyValuePair<string, int> requirement in totals)
                {
                    if (!inventory.TrySpend(requirement.Key, requirement.Value))
                    {
                        Restore(inventory, before);
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                Restore(inventory, before);
                return false;
            }
        }

        public static bool TryCommitBatch(
            ResourceInventory source,
            ResourceAmount[] inputs,
            ResourceInventory target,
            ResourceCapacityPolicy targetCapacity,
            int activeTargetWarehouseCount,
            ResourceAmount[] outputs)
        {
            if (source == null || target == null || targetCapacity == null ||
                !TryAggregate(
                    inputs,
                    out Dictionary<string, int> inputTotals,
                    allowEmpty: true) ||
                !TryAggregate(outputs, out Dictionary<string, int> outputTotals) ||
                !CanSpendAll(source, inputTotals) ||
                !CanAcceptAll(
                    source,
                    target,
                    targetCapacity,
                    activeTargetWarehouseCount,
                    inputTotals,
                    outputTotals))
            {
                return false;
            }

            bool usesSingleInventory = ReferenceEquals(source, target);
            Dictionary<string, int> sourceBefore = Capture(source, inputTotals.Keys);
            if (usesSingleInventory)
            {
                CaptureMissing(source, sourceBefore, outputTotals.Keys);
            }

            Dictionary<string, int> targetBefore = usesSingleInventory
                ? sourceBefore
                : Capture(target, outputTotals.Keys);

            try
            {
                foreach (KeyValuePair<string, int> input in inputTotals)
                {
                    if (!source.TrySpend(input.Key, input.Value))
                    {
                        RestoreBoth(source, sourceBefore, target, targetBefore);
                        return false;
                    }
                }

                foreach (KeyValuePair<string, int> output in outputTotals)
                {
                    if (targetCapacity.Add(
                            target,
                            output.Key,
                            output.Value,
                            activeTargetWarehouseCount) != output.Value)
                    {
                        RestoreBoth(source, sourceBefore, target, targetBefore);
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                RestoreBoth(source, sourceBefore, target, targetBefore);
                return false;
            }
        }

        public static ResourceTransferResult Transfer(
            ResourceInventory source,
            ResourceInventory target,
            ResourceCapacityPolicy targetCapacity,
            int activeTargetWarehouseCount,
            string resourceId,
            int requestedAmount)
        {
            if (source == null || target == null || targetCapacity == null ||
                ReferenceEquals(source, target) ||
                !ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                requestedAmount <= 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.InvalidRequest);
            }

            int sourceAvailable = Math.Max(0, source.Get(resourceId));
            if (sourceAvailable == 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.SourceEmpty);
            }

            int candidate = Math.Min(requestedAmount, sourceAvailable);
            int acceptable = targetCapacity.GetAcceptableAmount(
                target,
                resourceId,
                candidate,
                activeTargetWarehouseCount);
            if (acceptable == 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.TargetFull);
            }

            bool committed = TryCommitBatch(
                source,
                new[] { new ResourceAmount(resourceId, acceptable) },
                target,
                targetCapacity,
                activeTargetWarehouseCount,
                new[] { new ResourceAmount(resourceId, acceptable) });
            if (!committed)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.CommitFailed);
            }

            return new ResourceTransferResult(
                requestedAmount,
                acceptable,
                acceptable == requestedAmount
                    ? ResourceTransferStatus.Completed
                    : ResourceTransferStatus.Partial);
        }

        public static ResourceTransferResult TransferToBackpack(
            ResourceInventory source,
            PlayerBackpackModel target,
            string resourceId,
            int requestedAmount)
        {
            if (source == null || target == null ||
                !ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                requestedAmount <= 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.InvalidRequest);
            }

            int sourceAvailable = Math.Max(0, source.Get(resourceId));
            if (sourceAvailable == 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.SourceEmpty);
            }

            int candidate = Math.Min(requestedAmount, sourceAvailable);
            int acceptable = GetBackpackAcceptableAmount(
                target,
                resourceId,
                candidate);
            if (acceptable == 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.TargetFull);
            }

            int sourceBefore = source.Get(resourceId);
            int added = 0;
            try
            {
                if (!source.TrySpend(resourceId, acceptable))
                {
                    return new ResourceTransferResult(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }

                added = target.Add(resourceId, acceptable);
                if (added != acceptable)
                {
                    if (added > 0)
                        target.Remove(resourceId, added);
                    source.Restore(resourceId, sourceBefore);
                    return new ResourceTransferResult(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }
            }
            catch
            {
                if (added > 0)
                    target.Remove(resourceId, added);
                source.Restore(resourceId, sourceBefore);
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.CommitFailed);
            }

            return CompletedOrPartial(requestedAmount, acceptable);
        }

        public static ResourceTransferResult TransferFromBackpack(
            PlayerBackpackModel source,
            ResourceInventory target,
            ResourceCapacityPolicy targetCapacity,
            int activeTargetWarehouseCount,
            string resourceId,
            int requestedAmount)
        {
            if (source == null || target == null || targetCapacity == null ||
                !ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                requestedAmount <= 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.InvalidRequest);
            }

            int sourceAvailable = GetBackpackAmount(source, resourceId);
            if (sourceAvailable == 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.SourceEmpty);
            }

            int candidate = Math.Min(requestedAmount, sourceAvailable);
            int acceptable = targetCapacity.GetAcceptableAmount(
                target,
                resourceId,
                candidate,
                activeTargetWarehouseCount);
            if (acceptable == 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.TargetFull);
            }

            int targetBefore = target.Get(resourceId);
            int removed = 0;
            try
            {
                removed = source.Remove(resourceId, acceptable);
                if (removed != acceptable)
                {
                    if (removed > 0)
                        source.Add(resourceId, removed);
                    return new ResourceTransferResult(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }

                int added = targetCapacity.Add(
                    target,
                    resourceId,
                    acceptable,
                    activeTargetWarehouseCount);
                if (added != acceptable)
                {
                    target.Restore(resourceId, targetBefore);
                    source.Add(resourceId, removed);
                    return new ResourceTransferResult(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }
            }
            catch
            {
                target.Restore(resourceId, targetBefore);
                if (removed > 0)
                    source.Add(resourceId, removed);
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.CommitFailed);
            }

            return CompletedOrPartial(requestedAmount, acceptable);
        }

        public static ResourceTransferResult TransferFromBackpackSlot(
            PlayerBackpackModel source,
            int sourceSlotIndex,
            ResourceInventory target,
            ResourceCapacityPolicy targetCapacity,
            int activeTargetWarehouseCount,
            int requestedAmount)
        {
            if (source == null || target == null || targetCapacity == null ||
                sourceSlotIndex < 0 || sourceSlotIndex >= source.SlotCount ||
                requestedAmount <= 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.InvalidRequest);
            }

            BackpackSlot slot = source.GetSlot(sourceSlotIndex);
            if (!ResourceCapacityPolicy.IsRegisteredResource(slot.ResourceId))
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.SourceEmpty);
            }

            int candidate = Math.Min(requestedAmount, slot.Amount);
            int acceptable = targetCapacity.GetAcceptableAmount(
                target,
                slot.ResourceId,
                candidate,
                activeTargetWarehouseCount);
            if (acceptable == 0)
            {
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.TargetFull);
            }

            BackpackSlot[] sourceBefore = source.CaptureSlots();
            int targetBefore = target.Get(slot.ResourceId);
            try
            {
                if (source.RemoveFromSlot(sourceSlotIndex, acceptable) != acceptable)
                {
                    source.RestoreSlots(sourceBefore);
                    return new ResourceTransferResult(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }

                int added = targetCapacity.Add(
                    target,
                    slot.ResourceId,
                    acceptable,
                    activeTargetWarehouseCount);
                if (added != acceptable)
                {
                    target.Restore(slot.ResourceId, targetBefore);
                    source.RestoreSlots(sourceBefore);
                    return new ResourceTransferResult(
                        requestedAmount,
                        0,
                        ResourceTransferStatus.CommitFailed);
                }
            }
            catch
            {
                target.Restore(slot.ResourceId, targetBefore);
                source.RestoreSlots(sourceBefore);
                return new ResourceTransferResult(
                    requestedAmount,
                    0,
                    ResourceTransferStatus.CommitFailed);
            }

            return CompletedOrPartial(requestedAmount, acceptable);
        }

        private static bool TryAggregate(
            ResourceAmount[] amounts,
            out Dictionary<string, int> totals,
            bool allowEmpty = false)
        {
            totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (amounts == null || amounts.Length == 0)
            {
                return allowEmpty;
            }

            foreach (ResourceAmount amount in amounts)
            {
                if (!ResourceCapacityPolicy.IsRegisteredResource(amount.ResourceId) ||
                    amount.Amount <= 0)
                {
                    totals.Clear();
                    return false;
                }

                totals.TryGetValue(amount.ResourceId, out int existing);
                long aggregate = (long)existing + amount.Amount;
                if (aggregate > int.MaxValue)
                {
                    totals.Clear();
                    return false;
                }

                totals[amount.ResourceId] = (int)aggregate;
            }

            return true;
        }

        private static bool CanSpendAll(
            ResourceInventory inventory,
            Dictionary<string, int> requirements)
        {
            foreach (KeyValuePair<string, int> requirement in requirements)
            {
                if (inventory.Get(requirement.Key) < requirement.Value)
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetBackpackAmount(
            PlayerBackpackModel backpack,
            string resourceId)
        {
            long total = 0;
            for (int index = 0; index < backpack.SlotCount; index++)
            {
                BackpackSlot slot = backpack.GetSlot(index);
                if (string.Equals(slot.ResourceId, resourceId, StringComparison.Ordinal))
                    total += Math.Max(0, slot.Amount);
            }

            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        private static int GetBackpackAcceptableAmount(
            PlayerBackpackModel backpack,
            string resourceId,
            int requestedAmount)
        {
            if (requestedAmount <= 0 ||
                !ResourceDefinitionCatalog.TryGet(
                    resourceId,
                    out ResourceDefinition definition) ||
                definition.StackLimit <= 0)
            {
                return 0;
            }

            long available = 0;
            for (int index = 0; index < backpack.SlotCount; index++)
            {
                BackpackSlot slot = backpack.GetSlot(index);
                if (slot.Amount <= 0)
                {
                    available += definition.StackLimit;
                }
                else if (string.Equals(
                             slot.ResourceId,
                             resourceId,
                             StringComparison.Ordinal))
                {
                    available += Math.Max(0, definition.StackLimit - slot.Amount);
                }

                if (available >= requestedAmount)
                    return requestedAmount;
            }

            return (int)Math.Min(requestedAmount, available);
        }

        private static ResourceTransferResult CompletedOrPartial(
            int requestedAmount,
            int movedAmount)
        {
            return new ResourceTransferResult(
                requestedAmount,
                movedAmount,
                movedAmount == requestedAmount
                    ? ResourceTransferStatus.Completed
                    : ResourceTransferStatus.Partial);
        }

        private static bool CanAcceptAll(
            ResourceInventory source,
            ResourceInventory target,
            ResourceCapacityPolicy targetCapacity,
            int activeTargetWarehouseCount,
            Dictionary<string, int> inputs,
            Dictionary<string, int> outputs)
        {
            int policyCapacity = targetCapacity.GetCapacityPerResource(activeTargetWarehouseCount);
            int ledgerCapacity = target.CapacityPerResource;
            foreach (KeyValuePair<string, int> output in outputs)
            {
                long projected = target.Get(output.Key);
                if (ReferenceEquals(source, target) &&
                    inputs.TryGetValue(output.Key, out int consumedFromSameInventory))
                {
                    projected -= consumedFromSameInventory;
                }

                projected += output.Value;
                if (projected > policyCapacity || projected > ledgerCapacity)
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, int> Capture(
            ResourceInventory inventory,
            IEnumerable<string> resourceIds)
        {
            var snapshot = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string resourceId in resourceIds)
            {
                snapshot[resourceId] = inventory.Get(resourceId);
            }

            return snapshot;
        }

        private static void CaptureMissing(
            ResourceInventory inventory,
            Dictionary<string, int> snapshot,
            IEnumerable<string> resourceIds)
        {
            foreach (string resourceId in resourceIds)
            {
                if (!snapshot.ContainsKey(resourceId))
                {
                    snapshot[resourceId] = inventory.Get(resourceId);
                }
            }
        }

        private static void Restore(
            ResourceInventory inventory,
            Dictionary<string, int> snapshot)
        {
            foreach (KeyValuePair<string, int> value in snapshot)
            {
                inventory.Restore(value.Key, value.Value);
            }
        }

        private static void RestoreBoth(
            ResourceInventory source,
            Dictionary<string, int> sourceBefore,
            ResourceInventory target,
            Dictionary<string, int> targetBefore)
        {
            Restore(source, sourceBefore);
            if (!ReferenceEquals(source, target))
            {
                Restore(target, targetBefore);
            }
        }
    }
}
