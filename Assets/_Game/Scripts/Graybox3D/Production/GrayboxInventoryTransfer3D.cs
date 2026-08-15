using System;

namespace WasteCity.Graybox3D.Production
{
    public enum GrayboxInventoryTransferOutcome3D
    {
        Moved,
        PartiallyMoved,
        ResourceMismatch,
        DestinationFull,
        InvalidIndex,
        SourceEmpty,
        InvalidAmount
    }

    public readonly struct GrayboxInventoryTransferResult3D
    {
        public GrayboxInventoryTransferResult3D(
            GrayboxInventoryTransferOutcome3D outcome,
            int requested,
            int moved)
        {
            Outcome = outcome;
            Requested = requested;
            Moved = moved;
        }

        public GrayboxInventoryTransferOutcome3D Outcome { get; }
        public int Requested { get; }
        public int Moved { get; }
    }

    public static class GrayboxInventoryTransfer3D
    {
        public static GrayboxInventoryTransferResult3D MoveAllCacheToPlayer(
            GrayboxBuildingCache3D cache,
            GrayboxBuildingCachePort3D port,
            GrayboxPlayerInventory3D inventory,
            int targetSlotIndex)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            return MoveCacheToPlayer(
                cache,
                port,
                inventory,
                targetSlotIndex,
                cache.Amount(port));
        }

        public static GrayboxInventoryTransferResult3D MoveCacheToPlayer(
            GrayboxBuildingCache3D cache,
            GrayboxBuildingCachePort3D port,
            GrayboxPlayerInventory3D inventory,
            int targetSlotIndex,
            int requested)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (!inventory.IsValidIndex(targetSlotIndex))
                return Result(
                    GrayboxInventoryTransferOutcome3D.InvalidIndex,
                    requested,
                    0);
            if (requested <= 0)
                return Result(
                    cache.Amount(port) == 0
                        ? GrayboxInventoryTransferOutcome3D.SourceEmpty
                        : GrayboxInventoryTransferOutcome3D.InvalidAmount,
                    requested,
                    0);

            string resourceId = cache.ResourceId(port);
            int sourceAmount = cache.Amount(port);
            if (sourceAmount <= 0 || string.IsNullOrWhiteSpace(resourceId))
                return Result(
                    GrayboxInventoryTransferOutcome3D.SourceEmpty,
                    requested,
                    0);

            GrayboxInventorySlot3D target = inventory.GetSlot(targetSlotIndex);
            if (!target.IsEmpty && !string.Equals(
                    target.ResourceId,
                    resourceId,
                    StringComparison.Ordinal))
                return Result(
                    GrayboxInventoryTransferOutcome3D.ResourceMismatch,
                    requested,
                    0);

            int targetSpace = inventory.MaxStackPerSlot - target.Amount;
            int moveAmount = Math.Min(
                Math.Min(requested, sourceAmount),
                targetSpace);
            if (moveAmount <= 0)
                return Result(
                    GrayboxInventoryTransferOutcome3D.DestinationFull,
                    requested,
                    0);

            int removed = cache.Remove(port, moveAmount);
            int accepted = inventory.AddToSlot(
                targetSlotIndex,
                resourceId,
                removed);
            if (accepted != removed)
            {
                inventory.RemoveFromSlot(
                    targetSlotIndex,
                    accepted,
                    out _);
                cache.Add(port, resourceId, removed);
                return Result(
                    GrayboxInventoryTransferOutcome3D.DestinationFull,
                    requested,
                    0);
            }

            return Result(
                accepted == requested
                    ? GrayboxInventoryTransferOutcome3D.Moved
                    : GrayboxInventoryTransferOutcome3D.PartiallyMoved,
                requested,
                accepted);
        }

        public static GrayboxInventoryTransferResult3D MoveAllPlayerToCache(
            GrayboxPlayerInventory3D inventory,
            int sourceSlotIndex,
            GrayboxBuildingCache3D cache,
            GrayboxBuildingCachePort3D port)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (!inventory.IsValidIndex(sourceSlotIndex))
                return Result(
                    GrayboxInventoryTransferOutcome3D.InvalidIndex,
                    0,
                    0);
            return MovePlayerToCache(
                inventory,
                sourceSlotIndex,
                cache,
                port,
                inventory.GetSlot(sourceSlotIndex).Amount);
        }

        public static GrayboxInventoryTransferResult3D MovePlayerToCache(
            GrayboxPlayerInventory3D inventory,
            int sourceSlotIndex,
            GrayboxBuildingCache3D cache,
            GrayboxBuildingCachePort3D port,
            int requested)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (!inventory.IsValidIndex(sourceSlotIndex))
                return Result(
                    GrayboxInventoryTransferOutcome3D.InvalidIndex,
                    requested,
                    0);
            if (requested <= 0)
                return Result(
                    inventory.GetSlot(sourceSlotIndex).IsEmpty
                        ? GrayboxInventoryTransferOutcome3D.SourceEmpty
                        : GrayboxInventoryTransferOutcome3D.InvalidAmount,
                    requested,
                    0);

            GrayboxInventorySlot3D source = inventory.GetSlot(sourceSlotIndex);
            if (source.IsEmpty)
                return Result(
                    GrayboxInventoryTransferOutcome3D.SourceEmpty,
                    requested,
                    0);

            string expectedResourceId = cache.ResourceId(port);
            if (!string.Equals(
                    source.ResourceId,
                    expectedResourceId,
                    StringComparison.Ordinal))
                return Result(
                    GrayboxInventoryTransferOutcome3D.ResourceMismatch,
                    requested,
                    0);

            int moveAmount = Math.Min(
                Math.Min(requested, source.Amount),
                cache.AvailableCapacity(port));
            if (moveAmount <= 0)
                return Result(
                    GrayboxInventoryTransferOutcome3D.DestinationFull,
                    requested,
                    0);

            int removed = inventory.RemoveFromSlot(
                sourceSlotIndex,
                moveAmount,
                out string resourceId);
            int accepted = cache.Add(port, resourceId, removed);
            if (accepted != removed)
            {
                cache.Remove(port, accepted);
                inventory.AddToSlot(sourceSlotIndex, resourceId, removed);
                return Result(
                    GrayboxInventoryTransferOutcome3D.DestinationFull,
                    requested,
                    0);
            }

            return Result(
                accepted == requested
                    ? GrayboxInventoryTransferOutcome3D.Moved
                    : GrayboxInventoryTransferOutcome3D.PartiallyMoved,
                requested,
                accepted);
        }

        private static GrayboxInventoryTransferResult3D Result(
            GrayboxInventoryTransferOutcome3D outcome,
            int requested,
            int moved)
        {
            return new GrayboxInventoryTransferResult3D(
                outcome,
                Math.Max(0, requested),
                moved);
        }
    }
}
