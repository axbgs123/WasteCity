using System;
using System.Collections.Generic;

namespace WasteCity.Graybox3D.Production
{
    public sealed class GrayboxInventorySlot3D
    {
        internal GrayboxInventorySlot3D(int index)
        {
            Index = index;
        }

        public int Index { get; }
        public bool IsEmpty => Amount == 0;
        public string ResourceId { get; internal set; }
        public int Amount { get; internal set; }
    }

    public sealed class GrayboxPlayerInventory3D
    {
        public const int DefaultSlotCount = 12;
        public const int DefaultMaxStackPerSlot = 99;

        private readonly GrayboxInventorySlot3D[] slots;

        public GrayboxPlayerInventory3D()
            : this(DefaultSlotCount, DefaultMaxStackPerSlot)
        {
        }

        internal GrayboxPlayerInventory3D(int slotCount, int maxStackPerSlot)
        {
            if (slotCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(slotCount));
            if (maxStackPerSlot <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxStackPerSlot));

            MaxStackPerSlot = maxStackPerSlot;
            slots = new GrayboxInventorySlot3D[slotCount];
            for (int index = 0; index < slots.Length; index++)
                slots[index] = new GrayboxInventorySlot3D(index);
        }

        public int SlotCount => slots.Length;
        public int MaxStackPerSlot { get; }
        public IReadOnlyList<GrayboxInventorySlot3D> Slots => slots;

        public int TotalAmount
        {
            get
            {
                int total = 0;
                for (int index = 0; index < slots.Length; index++)
                    total += slots[index].Amount;
                return total;
            }
        }

        public bool IsValidIndex(int index)
        {
            return index >= 0 && index < slots.Length;
        }

        public GrayboxInventorySlot3D GetSlot(int index)
        {
            if (!IsValidIndex(index))
                throw new ArgumentOutOfRangeException(nameof(index));
            return slots[index];
        }

        public int Add(string resourceId, int amount)
        {
            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0)
                return 0;

            int remaining = amount;
            for (int index = 0; index < slots.Length && remaining > 0; index++)
            {
                GrayboxInventorySlot3D slot = slots[index];
                if (!slot.IsEmpty && string.Equals(
                        slot.ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    remaining -= AddToSlot(index, resourceId, remaining);
            }

            for (int index = 0; index < slots.Length && remaining > 0; index++)
            {
                if (slots[index].IsEmpty)
                    remaining -= AddToSlot(index, resourceId, remaining);
            }

            return amount - remaining;
        }

        public int AddToSlot(int index, string resourceId, int amount)
        {
            if (!IsValidIndex(index) ||
                string.IsNullOrWhiteSpace(resourceId) ||
                amount <= 0)
                return 0;

            GrayboxInventorySlot3D slot = slots[index];
            if (!slot.IsEmpty && !string.Equals(
                    slot.ResourceId,
                    resourceId,
                    StringComparison.Ordinal))
                return 0;

            int accepted = Math.Min(amount, MaxStackPerSlot - slot.Amount);
            if (accepted <= 0) return 0;
            slot.ResourceId = resourceId;
            slot.Amount += accepted;
            return accepted;
        }

        public int RemoveFromSlot(
            int index,
            int amount,
            out string resourceId)
        {
            resourceId = null;
            if (!IsValidIndex(index) || amount <= 0) return 0;

            GrayboxInventorySlot3D slot = slots[index];
            if (slot.IsEmpty) return 0;
            resourceId = slot.ResourceId;
            int removed = Math.Min(amount, slot.Amount);
            slot.Amount -= removed;
            if (slot.Amount == 0) slot.ResourceId = null;
            return removed;
        }
    }
}
