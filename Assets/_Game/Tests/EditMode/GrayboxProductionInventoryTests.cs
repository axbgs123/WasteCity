using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Graybox3D.Production;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionInventoryTests
    {
        [Test]
        public void IDEA0011_BuildingCacheOwnsTypedInputAndOutputCapacities()
        {
            var cache = new GrayboxBuildingCache3D(
                ResourceIds.Iron,
                20,
                ResourceIds.Alloy,
                10);

            Assert.That(cache.InputResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(cache.InputCapacity, Is.EqualTo(20));
            Assert.That(cache.OutputResourceId, Is.EqualTo(ResourceIds.Alloy));
            Assert.That(cache.OutputCapacity, Is.EqualTo(10));
            Assert.That(cache.InputAmount, Is.Zero);
            Assert.That(cache.OutputAmount, Is.Zero);
            Assert.That(
                cache.ResourceId(GrayboxBuildingCachePort3D.Input),
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(
                cache.ResourceId(GrayboxBuildingCachePort3D.Output),
                Is.EqualTo(ResourceIds.Alloy));
        }

        [Test]
        public void IDEA0011_BuildingCacheRejectsWrongTypesAndClampsWithoutLoss()
        {
            var cache = new GrayboxBuildingCache3D(
                ResourceIds.Iron,
                20,
                ResourceIds.Alloy,
                10);

            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Input,
                    ResourceIds.Alloy,
                    5),
                Is.Zero);
            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Output,
                    ResourceIds.Iron,
                    5),
                Is.Zero);
            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Input,
                    ResourceIds.Iron,
                    25),
                Is.EqualTo(20));
            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Output,
                    ResourceIds.Alloy,
                    15),
                Is.EqualTo(10));
            Assert.That(cache.InputAmount, Is.EqualTo(20));
            Assert.That(cache.OutputAmount, Is.EqualTo(10));

            int before = cache.InputAmount + cache.OutputAmount;
            Assert.That(
                cache.Remove(GrayboxBuildingCachePort3D.Input, 7),
                Is.EqualTo(7));
            Assert.That(cache.InputAmount, Is.EqualTo(13));
            Assert.That(cache.OutputAmount, Is.EqualTo(10));
            Assert.That(
                cache.InputAmount + cache.OutputAmount,
                Is.EqualTo(before - 7));
        }

        [Test]
        public void IDEA0011_PlayerInventoryDefaultsToTwelveStableSlotsOfNinetyNine()
        {
            var inventory = new GrayboxPlayerInventory3D();

            Assert.That(inventory.SlotCount, Is.EqualTo(12));
            Assert.That(inventory.MaxStackPerSlot, Is.EqualTo(99));
            Assert.That(inventory.Slots.Count, Is.EqualTo(12));
            for (var index = 0; index < inventory.SlotCount; index++)
            {
                GrayboxInventorySlot3D slot = inventory.GetSlot(index);
                Assert.That(slot.Index, Is.EqualTo(index));
                Assert.That(slot.IsEmpty, Is.True);
                Assert.That(slot.ResourceId, Is.Null);
                Assert.That(slot.Amount, Is.Zero);
            }
        }

        [Test]
        public void IDEA0011_PlayerInventoryMergesSameResourceInStableSlotOrder()
        {
            var inventory = new GrayboxPlayerInventory3D();

            Assert.That(inventory.Add(ResourceIds.Iron, 100), Is.EqualTo(100));
            AssertSlot(inventory, 0, ResourceIds.Iron, 99);
            AssertSlot(inventory, 1, ResourceIds.Iron, 1);
            Assert.That(inventory.Add(ResourceIds.Alloy, 5), Is.EqualTo(5));
            AssertSlot(inventory, 2, ResourceIds.Alloy, 5);

            Assert.That(
                inventory.RemoveFromSlot(0, 90, out string removedId),
                Is.EqualTo(90));
            Assert.That(removedId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(inventory.Add(ResourceIds.Iron, 80), Is.EqualTo(80));
            AssertSlot(inventory, 0, ResourceIds.Iron, 89);
            AssertSlot(inventory, 1, ResourceIds.Iron, 1);
            AssertSlot(inventory, 2, ResourceIds.Alloy, 5);
        }

        [Test]
        public void IDEA0011_WholeCacheStackMovesToRequestedPlayerSlot()
        {
            GrayboxBuildingCache3D cache = MiningCacheWithIron(20);
            var inventory = new GrayboxPlayerInventory3D();

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveAllCacheToPlayer(
                    cache,
                    GrayboxBuildingCachePort3D.Output,
                    inventory,
                    4);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.Moved));
            Assert.That(result.Requested, Is.EqualTo(20));
            Assert.That(result.Moved, Is.EqualTo(20));
            Assert.That(cache.OutputAmount, Is.Zero);
            AssertSlot(inventory, 4, ResourceIds.Iron, 20);
            Assert.That(Total(cache, inventory), Is.EqualTo(20));
        }

        [Test]
        public void IDEA0011_CacheToPlayerMovesOnlyRequestedAmount()
        {
            GrayboxBuildingCache3D cache = MiningCacheWithIron(20);
            var inventory = new GrayboxPlayerInventory3D();

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveCacheToPlayer(
                    cache,
                    GrayboxBuildingCachePort3D.Output,
                    inventory,
                    0,
                    7);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.Moved));
            Assert.That(result.Requested, Is.EqualTo(7));
            Assert.That(result.Moved, Is.EqualTo(7));
            Assert.That(cache.OutputAmount, Is.EqualTo(13));
            AssertSlot(inventory, 0, ResourceIds.Iron, 7);
            Assert.That(Total(cache, inventory), Is.EqualTo(20));
        }

        [Test]
        public void IDEA0011_CacheToPlayerPartiallyMovesWhenTargetHasLimitedSpace()
        {
            GrayboxBuildingCache3D cache = MiningCacheWithIron(20);
            var inventory = new GrayboxPlayerInventory3D();
            Assert.That(
                inventory.AddToSlot(3, ResourceIds.Iron, 95),
                Is.EqualTo(95));

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveAllCacheToPlayer(
                    cache,
                    GrayboxBuildingCachePort3D.Output,
                    inventory,
                    3);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.PartiallyMoved));
            Assert.That(result.Requested, Is.EqualTo(20));
            Assert.That(result.Moved, Is.EqualTo(4));
            Assert.That(cache.OutputAmount, Is.EqualTo(16));
            AssertSlot(inventory, 3, ResourceIds.Iron, 99);
            Assert.That(Total(cache, inventory), Is.EqualTo(115));
        }

        [Test]
        public void IDEA0011_PlayerStackMovesBackToMatchingBuildingPort()
        {
            var cache = new GrayboxBuildingCache3D(
                ResourceIds.Iron,
                20,
                ResourceIds.Alloy,
                10);
            var inventory = new GrayboxPlayerInventory3D();
            Assert.That(
                inventory.AddToSlot(5, ResourceIds.Iron, 12),
                Is.EqualTo(12));

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveAllPlayerToCache(
                    inventory,
                    5,
                    cache,
                    GrayboxBuildingCachePort3D.Input);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.Moved));
            Assert.That(result.Moved, Is.EqualTo(12));
            Assert.That(cache.InputAmount, Is.EqualTo(12));
            Assert.That(inventory.GetSlot(5).IsEmpty, Is.True);
            Assert.That(Total(cache, inventory), Is.EqualTo(12));
        }

        [Test]
        public void IDEA0011_PlayerToCachePartiallyMovesAtTypedCapacity()
        {
            var cache = new GrayboxBuildingCache3D(
                ResourceIds.Iron,
                20,
                ResourceIds.Alloy,
                10);
            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Input,
                    ResourceIds.Iron,
                    18),
                Is.EqualTo(18));
            var inventory = new GrayboxPlayerInventory3D();
            Assert.That(
                inventory.AddToSlot(0, ResourceIds.Iron, 9),
                Is.EqualTo(9));

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MovePlayerToCache(
                    inventory,
                    0,
                    cache,
                    GrayboxBuildingCachePort3D.Input,
                    7);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.PartiallyMoved));
            Assert.That(result.Requested, Is.EqualTo(7));
            Assert.That(result.Moved, Is.EqualTo(2));
            Assert.That(cache.InputAmount, Is.EqualTo(20));
            AssertSlot(inventory, 0, ResourceIds.Iron, 7);
            Assert.That(Total(cache, inventory), Is.EqualTo(27));
        }

        [Test]
        public void IDEA0011_WrongResourceTransferIsAtomic()
        {
            var cache = new GrayboxBuildingCache3D(
                ResourceIds.Iron,
                20,
                ResourceIds.Alloy,
                10);
            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Output,
                    ResourceIds.Alloy,
                    6),
                Is.EqualTo(6));
            var inventory = new GrayboxPlayerInventory3D();
            Assert.That(
                inventory.AddToSlot(2, ResourceIds.Iron, 8),
                Is.EqualTo(8));
            int before = Total(cache, inventory);

            GrayboxInventoryTransferResult3D cacheToPlayer =
                GrayboxInventoryTransfer3D.MoveAllCacheToPlayer(
                    cache,
                    GrayboxBuildingCachePort3D.Output,
                    inventory,
                    2);
            Assert.That(cacheToPlayer.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.ResourceMismatch));
            Assert.That(cacheToPlayer.Moved, Is.Zero);

            GrayboxInventoryTransferResult3D playerToCache =
                GrayboxInventoryTransfer3D.MoveAllPlayerToCache(
                    inventory,
                    2,
                    cache,
                    GrayboxBuildingCachePort3D.Output);
            Assert.That(playerToCache.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.ResourceMismatch));
            Assert.That(playerToCache.Moved, Is.Zero);
            Assert.That(cache.OutputAmount, Is.EqualTo(6));
            AssertSlot(inventory, 2, ResourceIds.Iron, 8);
            Assert.That(Total(cache, inventory), Is.EqualTo(before));
        }

        [Test]
        public void IDEA0011_FullDestinationTransferIsAtomic()
        {
            GrayboxBuildingCache3D cache = MiningCacheWithIron(20);
            var inventory = new GrayboxPlayerInventory3D();
            Assert.That(
                inventory.AddToSlot(1, ResourceIds.Iron, 99),
                Is.EqualTo(99));
            int before = Total(cache, inventory);

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveAllCacheToPlayer(
                    cache,
                    GrayboxBuildingCachePort3D.Output,
                    inventory,
                    1);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.DestinationFull));
            Assert.That(result.Moved, Is.Zero);
            Assert.That(cache.OutputAmount, Is.EqualTo(20));
            AssertSlot(inventory, 1, ResourceIds.Iron, 99);
            Assert.That(Total(cache, inventory), Is.EqualTo(before));
        }

        [Test]
        public void IDEA0011_FullBuildingPortTransferIsAtomic()
        {
            var cache = new GrayboxBuildingCache3D(
                ResourceIds.Iron,
                20,
                ResourceIds.Alloy,
                10);
            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Input,
                    ResourceIds.Iron,
                    20),
                Is.EqualTo(20));
            var inventory = new GrayboxPlayerInventory3D();
            Assert.That(
                inventory.AddToSlot(7, ResourceIds.Iron, 11),
                Is.EqualTo(11));
            int before = Total(cache, inventory);

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveAllPlayerToCache(
                    inventory,
                    7,
                    cache,
                    GrayboxBuildingCachePort3D.Input);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.DestinationFull));
            Assert.That(result.Moved, Is.Zero);
            Assert.That(cache.InputAmount, Is.EqualTo(20));
            AssertSlot(inventory, 7, ResourceIds.Iron, 11);
            Assert.That(Total(cache, inventory), Is.EqualTo(before));
        }

        [TestCase(-1)]
        [TestCase(12)]
        public void IDEA0011_InvalidPlayerSlotTransferIsAtomic(int slotIndex)
        {
            GrayboxBuildingCache3D cache = MiningCacheWithIron(20);
            var inventory = new GrayboxPlayerInventory3D();
            int before = Total(cache, inventory);

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveAllCacheToPlayer(
                    cache,
                    GrayboxBuildingCachePort3D.Output,
                    inventory,
                    slotIndex);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.InvalidIndex));
            Assert.That(result.Moved, Is.Zero);
            Assert.That(cache.OutputAmount, Is.EqualTo(20));
            Assert.That(inventory.TotalAmount, Is.Zero);
            Assert.That(Total(cache, inventory), Is.EqualTo(before));
        }

        [TestCase(-1)]
        [TestCase(12)]
        public void IDEA0011_InvalidPlayerSourceSlotTransferIsAtomic(
            int slotIndex)
        {
            var cache = new GrayboxBuildingCache3D(
                ResourceIds.Iron,
                20,
                ResourceIds.Alloy,
                10);
            var inventory = new GrayboxPlayerInventory3D();
            Assert.That(inventory.Add(ResourceIds.Iron, 9), Is.EqualTo(9));
            int before = Total(cache, inventory);

            GrayboxInventoryTransferResult3D result =
                GrayboxInventoryTransfer3D.MoveAllPlayerToCache(
                    inventory,
                    slotIndex,
                    cache,
                    GrayboxBuildingCachePort3D.Input);

            Assert.That(result.Outcome,
                Is.EqualTo(GrayboxInventoryTransferOutcome3D.InvalidIndex));
            Assert.That(result.Moved, Is.Zero);
            Assert.That(cache.InputAmount, Is.Zero);
            AssertSlot(inventory, 0, ResourceIds.Iron, 9);
            Assert.That(Total(cache, inventory), Is.EqualTo(before));
        }

        private static GrayboxBuildingCache3D MiningCacheWithIron(int amount)
        {
            var cache = new GrayboxBuildingCache3D(
                inputResourceId: null,
                inputCapacity: 0,
                outputResourceId: ResourceIds.Iron,
                outputCapacity: 20);
            Assert.That(
                cache.Add(
                    GrayboxBuildingCachePort3D.Output,
                    ResourceIds.Iron,
                    amount),
                Is.EqualTo(amount));
            return cache;
        }

        private static int Total(
            GrayboxBuildingCache3D cache,
            GrayboxPlayerInventory3D inventory)
        {
            return cache.InputAmount + cache.OutputAmount +
                   inventory.TotalAmount;
        }

        private static void AssertSlot(
            GrayboxPlayerInventory3D inventory,
            int index,
            string resourceId,
            int amount)
        {
            GrayboxInventorySlot3D slot = inventory.GetSlot(index);
            Assert.That(slot.Index, Is.EqualTo(index));
            Assert.That(slot.IsEmpty, Is.False);
            Assert.That(slot.ResourceId, Is.EqualTo(resourceId));
            Assert.That(slot.Amount, Is.EqualTo(amount));
        }
    }
}
