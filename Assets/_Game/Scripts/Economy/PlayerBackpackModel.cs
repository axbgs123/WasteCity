using System;

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
    }
}
