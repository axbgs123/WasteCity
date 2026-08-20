using System;
using System.Collections.Generic;
using WasteCity.Content;

namespace WasteCity.Economy
{
    public readonly struct BackpackSlot
    {
        public string ResourceId { get; }
        public int Amount { get; }

        internal BackpackSlot(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }
    }

    public readonly struct PlayerBackpackRestoreSlot
    {
        public PlayerBackpackRestoreSlot(
            int slotIndex,
            string resourceId,
            int amount)
        {
            SlotIndex = slotIndex;
            ResourceId = resourceId;
            Amount = amount;
        }

        public int SlotIndex { get; }
        public string ResourceId { get; }
        public int Amount { get; }
    }

    public sealed class PlayerBackpackRestorePlan
    {
        internal PlayerBackpackRestorePlan(
            PlayerBackpackModel owner,
            PlayerBackpackRestoreSlot[] slots)
        {
            Owner = owner;
            Slots = (PlayerBackpackRestoreSlot[])slots.Clone();
        }

        public int SlotCount => Slots.Length;

        internal PlayerBackpackModel Owner { get; }
        internal PlayerBackpackRestoreSlot[] Slots { get; }
        internal bool committed;
    }

    public sealed class PlayerBackpackModel
    {
        private const int FormalSlotCount = 30;

        private readonly string[] resourceIds = new string[FormalSlotCount];
        private readonly int[] amounts = new int[FormalSlotCount];

        public int SlotCount => resourceIds.Length;

        public int StackLimit
        {
            get
            {
                return ResourceDefinitionCatalog.TryGet(ResourceIds.Iron, out ResourceDefinition definition)
                    ? definition.StackLimit
                    : 0;
            }
        }

        public BackpackSlot GetSlot(int index)
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index), index, "Backpack slot index is out of range.");

            return new BackpackSlot(resourceIds[index], amounts[index]);
        }

        public int Add(string resourceId, int amount)
        {
            if (!TryGetUsableDefinition(resourceId, out ResourceDefinition definition) || amount <= 0)
                return 0;

            int remaining = amount;
            for (int index = 0; index < SlotCount && remaining > 0; index++)
            {
                if (!string.Equals(resourceIds[index], resourceId, StringComparison.Ordinal))
                    continue;

                int available = definition.StackLimit - amounts[index];
                if (available <= 0)
                    continue;

                int moved = Math.Min(available, remaining);
                amounts[index] += moved;
                remaining -= moved;
            }

            for (int index = 0; index < SlotCount && remaining > 0; index++)
            {
                if (!IsEmpty(index))
                    continue;

                int moved = Math.Min(definition.StackLimit, remaining);
                resourceIds[index] = resourceId;
                amounts[index] = moved;
                remaining -= moved;
            }

            return amount - remaining;
        }

        public int Remove(string resourceId, int amount)
        {
            if (!TryGetUsableDefinition(resourceId, out _) || amount <= 0)
                return 0;

            int remaining = amount;
            for (int index = 0; index < SlotCount && remaining > 0; index++)
            {
                if (!string.Equals(resourceIds[index], resourceId, StringComparison.Ordinal))
                    continue;

                int moved = Math.Min(amounts[index], remaining);
                amounts[index] -= moved;
                remaining -= moved;
                if (amounts[index] == 0)
                    resourceIds[index] = null;
            }

            return amount - remaining;
        }

        internal int RemoveFromSlot(int index, int amount)
        {
            if (!IsValidIndex(index) || amount <= 0 || IsEmpty(index))
                return 0;

            int removed = Math.Min(amounts[index], amount);
            amounts[index] -= removed;
            if (amounts[index] == 0)
                resourceIds[index] = null;
            return removed;
        }

        internal BackpackSlot[] CaptureSlots()
        {
            var snapshot = new BackpackSlot[SlotCount];
            for (int index = 0; index < SlotCount; index++)
                snapshot[index] = new BackpackSlot(
                    resourceIds[index],
                    amounts[index]);
            return snapshot;
        }

        internal void RestoreSlots(BackpackSlot[] snapshot)
        {
            if (snapshot == null || snapshot.Length != SlotCount)
                throw new ArgumentException(
                    "Backpack snapshot does not match slot count.",
                    nameof(snapshot));
            for (int index = 0; index < SlotCount; index++)
            {
                resourceIds[index] = snapshot[index].ResourceId;
                amounts[index] = snapshot[index].Amount;
            }
        }

        internal bool TryRefundReservedInputs(
            IReadOnlyList<ResourceAmount> reservedInputs)
        {
            if (reservedInputs == null) return false;

            var restoredResourceIds = (string[])resourceIds.Clone();
            var restoredAmounts = (int[])amounts.Clone();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int amountIndex = 0;
                 amountIndex < reservedInputs.Count;
                 amountIndex++)
            {
                ResourceAmount reserved = reservedInputs[amountIndex];
                if (!IsValidStableId(reserved.ResourceId) ||
                    reserved.Amount <= 0 ||
                    !seen.Add(reserved.ResourceId))
                {
                    return false;
                }

                int stackLimit = ResourceDefinitionCatalog.TryGet(
                        reserved.ResourceId,
                        out ResourceDefinition definition) &&
                    definition.StackLimit > 0
                    ? definition.StackLimit
                    : StackLimit;
                if (stackLimit <= 0) return false;
                int remaining = reserved.Amount;
                for (int index = 0;
                     index < SlotCount && remaining > 0;
                     index++)
                {
                    if (!string.Equals(
                            restoredResourceIds[index],
                            reserved.ResourceId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    int accepted = Math.Min(
                        Math.Max(0, stackLimit - restoredAmounts[index]),
                        remaining);
                    restoredAmounts[index] += accepted;
                    remaining -= accepted;
                }
                for (int index = 0;
                     index < SlotCount && remaining > 0;
                     index++)
                {
                    if (restoredAmounts[index] != 0) continue;
                    int accepted = Math.Min(stackLimit, remaining);
                    restoredResourceIds[index] = reserved.ResourceId;
                    restoredAmounts[index] = accepted;
                    remaining -= accepted;
                }
                if (remaining > 0) return false;
            }

            for (int index = 0; index < SlotCount; index++)
            {
                resourceIds[index] = restoredResourceIds[index];
                amounts[index] = restoredAmounts[index];
            }
            return true;
        }

        public PlayerBackpackRestoreSlot[] CaptureRestoreSlots()
        {
            var snapshot = new PlayerBackpackRestoreSlot[SlotCount];
            for (int index = 0; index < SlotCount; index++)
            {
                snapshot[index] = new PlayerBackpackRestoreSlot(
                    index,
                    resourceIds[index],
                    amounts[index]);
            }
            return snapshot;
        }

        public bool TryPrepareRestore(
            IReadOnlyList<PlayerBackpackRestoreSlot> slots,
            bool allowOverStack,
            out PlayerBackpackRestorePlan plan,
            out string error)
        {
            plan = null;
            if (slots == null || slots.Count != FormalSlotCount)
            {
                error = "背包恢复记录必须恰好包含 30 个槽位";
                return false;
            }

            var restored = new PlayerBackpackRestoreSlot[FormalSlotCount];
            for (int index = 0; index < restored.Length; index++)
            {
                PlayerBackpackRestoreSlot slot = slots[index];
                if (slot.SlotIndex != index)
                {
                    error = "背包恢复槽位索引无效";
                    return false;
                }
                if (slot.Amount < 0)
                {
                    error = "背包恢复数量不能为负数";
                    return false;
                }

                bool empty = string.IsNullOrEmpty(slot.ResourceId);
                if (empty != (slot.Amount == 0))
                {
                    error = "背包空槽的资源和数量不一致";
                    return false;
                }

                string resourceId = empty ? null : slot.ResourceId;
                if (!empty && !IsValidStableId(resourceId))
                {
                    error = "背包资源 ID 不是有效的稳定 ID";
                    return false;
                }
                if (!empty &&
                    ResourceDefinitionCatalog.TryGet(
                        resourceId,
                        out ResourceDefinition definition) &&
                    (definition.StackLimit <= 0 ||
                     slot.Amount > definition.StackLimit) &&
                    !allowOverStack)
                {
                    error = "背包资源堆叠超过当前上限";
                    return false;
                }

                restored[index] = new PlayerBackpackRestoreSlot(
                    index,
                    resourceId,
                    slot.Amount);
            }

            plan = new PlayerBackpackRestorePlan(this, restored);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            PlayerBackpackRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "背包恢复计划不属于当前背包";
                return false;
            }
            if (plan.committed)
            {
                error = "背包恢复计划已经提交";
                return false;
            }

            for (int index = 0; index < SlotCount; index++)
            {
                PlayerBackpackRestoreSlot slot = plan.Slots[index];
                resourceIds[index] = slot.ResourceId;
                amounts[index] = slot.Amount;
            }
            plan.committed = true;
            error = string.Empty;
            return true;
        }

        public bool SplitHalf(int sourceIndex, int targetIndex)
        {
            if (!CanMoveBetween(sourceIndex, targetIndex))
                return false;

            string resourceId = resourceIds[sourceIndex];
            if (!TryGetUsableDefinition(resourceId, out ResourceDefinition definition))
                return false;

            int moved = amounts[sourceIndex] / 2 + amounts[sourceIndex] % 2;
            if (!CanAccept(targetIndex, resourceId, moved, definition.StackLimit))
                return false;

            Transfer(sourceIndex, targetIndex, resourceId, moved);
            return true;
        }

        public bool MoveOne(int sourceIndex, int targetIndex)
        {
            if (!CanMoveBetween(sourceIndex, targetIndex))
                return false;

            string resourceId = resourceIds[sourceIndex];
            if (!TryGetUsableDefinition(resourceId, out ResourceDefinition definition) ||
                !CanAccept(targetIndex, resourceId, 1, definition.StackLimit))
            {
                return false;
            }

            Transfer(sourceIndex, targetIndex, resourceId, 1);
            return true;
        }

        public bool MoveWholeStack(int sourceIndex, int targetIndex)
        {
            if (!CanMoveBetween(sourceIndex, targetIndex))
                return false;

            string sourceResourceId = resourceIds[sourceIndex];
            if (!TryGetUsableDefinition(sourceResourceId, out ResourceDefinition sourceDefinition))
                return false;

            if (IsEmpty(targetIndex))
            {
                Transfer(
                    sourceIndex,
                    targetIndex,
                    sourceResourceId,
                    amounts[sourceIndex]);
                return true;
            }

            string targetResourceId = resourceIds[targetIndex];
            if (string.Equals(sourceResourceId, targetResourceId, StringComparison.Ordinal))
            {
                int available = sourceDefinition.StackLimit - amounts[targetIndex];
                if (available <= 0)
                    return false;

                Transfer(
                    sourceIndex,
                    targetIndex,
                    sourceResourceId,
                    Math.Min(amounts[sourceIndex], available));
                return true;
            }

            if (!TryGetUsableDefinition(targetResourceId, out _))
                return false;

            int sourceAmount = amounts[sourceIndex];
            int targetAmount = amounts[targetIndex];
            resourceIds[sourceIndex] = targetResourceId;
            amounts[sourceIndex] = targetAmount;
            resourceIds[targetIndex] = sourceResourceId;
            amounts[targetIndex] = sourceAmount;
            return true;
        }

        private bool CanMoveBetween(int sourceIndex, int targetIndex)
        {
            return IsValidIndex(sourceIndex) &&
                   IsValidIndex(targetIndex) &&
                   sourceIndex != targetIndex &&
                   !IsEmpty(sourceIndex);
        }

        private bool CanAccept(int index, string resourceId, int amount, int stackLimit)
        {
            if (IsEmpty(index))
                return amount <= stackLimit;

            return string.Equals(resourceIds[index], resourceId, StringComparison.Ordinal) &&
                   amounts[index] <= stackLimit - amount;
        }

        private void Transfer(int sourceIndex, int targetIndex, string resourceId, int amount)
        {
            amounts[sourceIndex] -= amount;
            if (amounts[sourceIndex] == 0)
                resourceIds[sourceIndex] = null;

            if (IsEmpty(targetIndex))
                resourceIds[targetIndex] = resourceId;
            amounts[targetIndex] += amount;
        }

        private bool IsEmpty(int index)
        {
            return amounts[index] == 0;
        }

        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < SlotCount;
        }

        private static bool TryGetUsableDefinition(
            string resourceId,
            out ResourceDefinition definition)
        {
            return ResourceDefinitionCatalog.TryGet(resourceId, out definition) &&
                   definition.StackLimit > 0;
        }

        private static bool IsValidStableId(string value)
        {
            try
            {
                _ = new StableId(value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
