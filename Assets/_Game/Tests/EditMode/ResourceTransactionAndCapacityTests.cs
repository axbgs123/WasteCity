using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ResourceTransactionAndCapacityTests
    {
        [Test]
        public void CityCapacityStartsAtOneHundredFiftyAndEachWarehouseAddsOneHundredFifty()
        {
            var policy = new ResourceCapacityPolicy();

            Assert.That(policy.GetCapacityPerResource(0), Is.EqualTo(150));
            Assert.That(policy.GetCapacityPerResource(1), Is.EqualTo(300));
            Assert.That(policy.GetCapacityPerResource(3), Is.EqualTo(600));
        }

        [Test]
        public void CapacityReductionPreservesOverageBlocksDepositsAndRecoversAfterConsumption()
        {
            var inventory = new ResourceInventory(1000);
            var policy = new ResourceCapacityPolicy();

            Assert.That(
                policy.Add(inventory, ResourceIds.Iron, 280, activeWarehouseCount: 1),
                Is.EqualTo(280));

            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(280));
            Assert.That(policy.GetAcceptableAmount(
                inventory,
                ResourceIds.Iron,
                requestedAmount: 20,
                activeWarehouseCount: 0), Is.Zero);
            Assert.That(
                policy.Add(inventory, ResourceIds.Iron, 20, activeWarehouseCount: 0),
                Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(280));

            Assert.That(inventory.TrySpend(ResourceIds.Iron, 131), Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(149));
            Assert.That(policy.GetAcceptableAmount(
                inventory,
                ResourceIds.Iron,
                requestedAmount: 20,
                activeWarehouseCount: 0), Is.EqualTo(1));
            Assert.That(
                policy.Add(inventory, ResourceIds.Iron, 20, activeWarehouseCount: 0),
                Is.EqualTo(1));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(150));
        }

        [Test]
        public void MultiInputSpendFailsWithoutDeductingAnyIngredient()
        {
            var inventory = new ResourceInventory(1000);
            inventory.Add(ResourceIds.Iron, 10);
            inventory.Add(ResourceIds.Biomass, 4);

            bool committed = ResourceTransaction.TrySpendAll(
                inventory,
                new ResourceAmount(ResourceIds.Iron, 6),
                new ResourceAmount(ResourceIds.Biomass, 5));

            Assert.That(committed, Is.False);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.EqualTo(4));
        }

        [Test]
        public void MultiInputSpendCommitsAllIngredientsExactlyOnce()
        {
            var inventory = new ResourceInventory(1000);
            inventory.Add(ResourceIds.Iron, 10);
            inventory.Add(ResourceIds.Biomass, 5);

            bool committed = ResourceTransaction.TrySpendAll(
                inventory,
                new ResourceAmount(ResourceIds.Iron, 6),
                new ResourceAmount(ResourceIds.Biomass, 5));

            Assert.That(committed, Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(4));
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.Zero);
        }

        [Test]
        public void BatchCommitPreflightsEveryTargetBeforeSpendingInputs()
        {
            var source = new ResourceInventory(1000);
            var target = new ResourceInventory(1000);
            var targetCapacity = new ResourceCapacityPolicy();
            source.Add(ResourceIds.Iron, 2);
            source.Add(ResourceIds.Alloy, 1);
            target.Add(ResourceIds.Ammunition, 149);
            int totalBefore = Total(
                source,
                target,
                ResourceIds.Iron,
                ResourceIds.Alloy,
                ResourceIds.Ammunition);

            bool committed = ResourceTransaction.TryCommitBatch(
                source,
                new[]
                {
                    new ResourceAmount(ResourceIds.Iron, 2),
                    new ResourceAmount(ResourceIds.Alloy, 1)
                },
                target,
                targetCapacity,
                0,
                new[] { new ResourceAmount(ResourceIds.Ammunition, 2) });

            Assert.That(committed, Is.False);
            Assert.That(source.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(source.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(target.Get(ResourceIds.Ammunition), Is.EqualTo(149));
            Assert.That(Total(
                source,
                target,
                ResourceIds.Iron,
                ResourceIds.Alloy,
                ResourceIds.Ammunition), Is.EqualTo(totalBefore));
        }

        [Test]
        public void BatchCommitConsumesAllInputsOnlyAfterTargetPreflightSucceeds()
        {
            var source = new ResourceInventory(1000);
            var target = new ResourceInventory(1000);
            var targetCapacity = new ResourceCapacityPolicy();
            source.Add(ResourceIds.Iron, 2);
            source.Add(ResourceIds.Alloy, 1);
            target.Add(ResourceIds.Ammunition, 148);

            bool committed = ResourceTransaction.TryCommitBatch(
                source,
                new[]
                {
                    new ResourceAmount(ResourceIds.Iron, 2),
                    new ResourceAmount(ResourceIds.Alloy, 1)
                },
                target,
                targetCapacity,
                0,
                new[] { new ResourceAmount(ResourceIds.Ammunition, 2) });

            Assert.That(committed, Is.True);
            Assert.That(source.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(source.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(target.Get(ResourceIds.Ammunition), Is.EqualTo(150));
        }

        [Test]
        public void BatchCommitOnSameInventoryConsumesIronAndProducesAlloy()
        {
            var inventory = new ResourceInventory(1000);
            var capacity = new ResourceCapacityPolicy();
            inventory.Add(ResourceIds.Iron, 2);

            bool committed = ResourceTransaction.TryCommitBatch(
                inventory,
                new[] { new ResourceAmount(ResourceIds.Iron, 2) },
                inventory,
                capacity,
                0,
                new[] { new ResourceAmount(ResourceIds.Alloy, 1) });

            Assert.That(committed, Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(1));
        }

        [Test]
        public void BatchCommitAllowsInputFreeResourceNodeProduction()
        {
            var inventory = new ResourceInventory(1000);
            var capacity = new ResourceCapacityPolicy();

            bool committed = ResourceTransaction.TryCommitBatch(
                inventory,
                new ResourceAmount[0],
                inventory,
                capacity,
                0,
                new[] { new ResourceAmount(ResourceIds.Iron, 1) });

            Assert.That(committed, Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(1));
        }

        [Test]
        public void BatchCommitOnSameInventoryPreflightsSameResourceByNetChange()
        {
            var inventory = new ResourceInventory(1000);
            var capacity = new ResourceCapacityPolicy();
            inventory.Add(ResourceIds.Iron, 150);

            bool committed = ResourceTransaction.TryCommitBatch(
                inventory,
                new[] { new ResourceAmount(ResourceIds.Iron, 2) },
                inventory,
                capacity,
                0,
                new[] { new ResourceAmount(ResourceIds.Iron, 1) });

            Assert.That(committed, Is.True);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(149));
        }

        [Test]
        public void BatchCommitOnSameInventoryRejectsFullOutputWithoutSpendingInput()
        {
            var inventory = new ResourceInventory(1000);
            var capacity = new ResourceCapacityPolicy();
            inventory.Add(ResourceIds.Iron, 2);
            inventory.Add(ResourceIds.Alloy, 150);

            bool committed = ResourceTransaction.TryCommitBatch(
                inventory,
                new[] { new ResourceAmount(ResourceIds.Iron, 2) },
                inventory,
                capacity,
                0,
                new[] { new ResourceAmount(ResourceIds.Alloy, 1) });

            Assert.That(committed, Is.False);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(150));
        }

        [Test]
        public void ProductionBatchRejectsDebtWithoutRaisingDebtIncreased()
        {
            var source = new ResourceInventory(1000);
            var target = new ResourceInventory(1000);
            var targetCapacity = new ResourceCapacityPolicy();
            source.SetDebtLimit(100);
            source.Add(ResourceIds.Iron, 1);
            int debtIncreased = 0;
            source.DebtIncreased += amount => debtIncreased += amount;

            bool committed = ResourceTransaction.TryCommitBatch(
                source,
                new[] { new ResourceAmount(ResourceIds.Iron, 2) },
                target,
                targetCapacity,
                0,
                new[] { new ResourceAmount(ResourceIds.Alloy, 1) });

            Assert.That(committed, Is.False);
            Assert.That(source.Get(ResourceIds.Iron), Is.EqualTo(1));
            Assert.That(target.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(debtIncreased, Is.Zero);
        }

        [Test]
        public void FormalCityInventoryBackingLedgerSupportsWarehouseCapacityWithoutTrimmingOnReduction()
        {
            ResourceInventory inventory =
                ResourceDefinitionCatalog.CreateFormalCityInventory();
            var policy = new ResourceCapacityPolicy();

            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(20));
            Assert.That(inventory.CapacityPerResource, Is.GreaterThanOrEqualTo(300));
            Assert.That(
                policy.Add(
                    inventory,
                    ResourceIds.Iron,
                    280,
                    activeWarehouseCount: 1),
                Is.EqualTo(280));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(300));

            Assert.That(
                policy.GetAcceptableAmount(
                    inventory,
                    ResourceIds.Iron,
                    requestedAmount: 1,
                    activeWarehouseCount: 0),
                Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(300));
        }

        [Test]
        public void TransferMovesOnlyTheAmountTheTargetCanActuallyAccept()
        {
            var source = new ResourceInventory(1000);
            var target = new ResourceInventory(1000);
            var targetCapacity = new ResourceCapacityPolicy();
            source.Add(ResourceIds.Iron, 30);
            target.Add(ResourceIds.Iron, 145);
            int totalBefore = Total(source, target, ResourceIds.Iron);

            ResourceTransferResult result = ResourceTransaction.Transfer(
                source,
                target,
                targetCapacity,
                0,
                ResourceIds.Iron,
                20);

            Assert.That(result.RequestedAmount, Is.EqualTo(20));
            Assert.That(result.MovedAmount, Is.EqualTo(5));
            Assert.That(result.RemainingAmount, Is.EqualTo(15));
            Assert.That(source.Get(ResourceIds.Iron), Is.EqualTo(25));
            Assert.That(target.Get(ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(Total(source, target, ResourceIds.Iron), Is.EqualTo(totalBefore));
        }

        [Test]
        public void FailedTransferRollsBackBothSidesAndPreservesTotalQuantity()
        {
            var source = new ResourceInventory(1000);
            var target = new ResourceInventory(1000);
            var targetCapacity = new ResourceCapacityPolicy();
            source.Add(ResourceIds.Iron, 30);
            target.Add(ResourceIds.Iron, 150);
            int totalBefore = Total(source, target, ResourceIds.Iron);

            ResourceTransferResult result = ResourceTransaction.Transfer(
                source,
                target,
                targetCapacity,
                0,
                ResourceIds.Iron,
                20);

            Assert.That(result.MovedAmount, Is.Zero);
            Assert.That(result.RemainingAmount, Is.EqualTo(20));
            Assert.That(source.Get(ResourceIds.Iron), Is.EqualTo(30));
            Assert.That(target.Get(ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(Total(source, target, ResourceIds.Iron), Is.EqualTo(totalBefore));
        }

        [Test]
        public void TransferToBackpackPartiallyAcceptsAndPreservesTotalQuantity()
        {
            var city = new ResourceInventory(1000);
            var backpack = new PlayerBackpackModel();
            city.Add(ResourceIds.Iron, 30);
            backpack.Add(ResourceIds.Alloy, 2900);
            backpack.Add(ResourceIds.Iron, 95);
            int totalBefore = Total(city, backpack, ResourceIds.Iron);

            ResourceTransferResult result = ResourceTransaction.TransferToBackpack(
                city,
                backpack,
                ResourceIds.Iron,
                20);

            Assert.That(result.Status, Is.EqualTo(ResourceTransferStatus.Partial));
            Assert.That(result.MovedAmount, Is.EqualTo(5));
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(25));
            Assert.That(BackpackTotal(backpack, ResourceIds.Iron), Is.EqualTo(100));
            Assert.That(Total(city, backpack, ResourceIds.Iron), Is.EqualTo(totalBefore));
        }

        [Test]
        public void TransferToFullBackpackFailsWithoutChangingEitherSide()
        {
            var city = new ResourceInventory(1000);
            var backpack = new PlayerBackpackModel();
            city.Add(ResourceIds.Iron, 30);
            backpack.Add(ResourceIds.Alloy, 3000);
            int totalBefore = Total(city, backpack, ResourceIds.Iron, ResourceIds.Alloy);

            ResourceTransferResult result = ResourceTransaction.TransferToBackpack(
                city,
                backpack,
                ResourceIds.Iron,
                20);

            Assert.That(result.Status, Is.EqualTo(ResourceTransferStatus.TargetFull));
            Assert.That(result.MovedAmount, Is.Zero);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(30));
            Assert.That(BackpackTotal(backpack, ResourceIds.Alloy), Is.EqualTo(3000));
            Assert.That(
                Total(city, backpack, ResourceIds.Iron, ResourceIds.Alloy),
                Is.EqualTo(totalBefore));
        }

        [Test]
        public void CityStorageTransferToBackpackUsesConnectedWarehouse()
        {
            var core = new ResourceInventory(1000);
            using var city = new CityResourceStorageModel(core, 150);
            var backpack = new PlayerBackpackModel();
            Assert.That(city.TryRegisterWarehouse(
                "building.instance.backpack-source",
                connected: true), Is.True);
            Assert.That(city.AddToWarehouse(
                "building.instance.backpack-source",
                ResourceIds.Iron,
                20), Is.EqualTo(20));

            ResourceTransferResult result =
                ResourceTransaction.TransferToBackpack(
                    city,
                    backpack,
                    ResourceIds.Iron,
                    12);

            Assert.That(result.Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Assert.That(result.MovedAmount, Is.EqualTo(12));
            Assert.That(city.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(8));
            Assert.That(BackpackTotal(backpack, ResourceIds.Iron), Is.EqualTo(12));
        }

        [Test]
        public void TransferFromBackpackPartiallyAcceptsAndPreservesTotalQuantity()
        {
            var backpack = new PlayerBackpackModel();
            var city = new ResourceInventory(1000);
            var cityCapacity = new ResourceCapacityPolicy();
            backpack.Add(ResourceIds.Iron, 30);
            city.Add(ResourceIds.Iron, 145);
            int totalBefore = Total(city, backpack, ResourceIds.Iron);

            ResourceTransferResult result = ResourceTransaction.TransferFromBackpack(
                backpack,
                city,
                cityCapacity,
                0,
                ResourceIds.Iron,
                20);

            Assert.That(result.Status, Is.EqualTo(ResourceTransferStatus.Partial));
            Assert.That(result.MovedAmount, Is.EqualTo(5));
            Assert.That(BackpackTotal(backpack, ResourceIds.Iron), Is.EqualTo(25));
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(Total(city, backpack, ResourceIds.Iron), Is.EqualTo(totalBefore));
        }

        [Test]
        public void TransferFromBackpackToFullCityFailsWithoutChangingEitherSide()
        {
            var backpack = new PlayerBackpackModel();
            var city = new ResourceInventory(1000);
            var cityCapacity = new ResourceCapacityPolicy();
            backpack.Add(ResourceIds.Iron, 30);
            city.Add(ResourceIds.Iron, 150);
            int totalBefore = Total(city, backpack, ResourceIds.Iron);

            ResourceTransferResult result = ResourceTransaction.TransferFromBackpack(
                backpack,
                city,
                cityCapacity,
                0,
                ResourceIds.Iron,
                20);

            Assert.That(result.Status, Is.EqualTo(ResourceTransferStatus.TargetFull));
            Assert.That(result.MovedAmount, Is.Zero);
            Assert.That(BackpackTotal(backpack, ResourceIds.Iron), Is.EqualTo(30));
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(Total(city, backpack, ResourceIds.Iron), Is.EqualTo(totalBefore));
        }

        [Test]
        public void BackpackSlotTransferUsesWarehouseWhenCityCoreIsFull()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 20);
            var core = new ResourceInventory(1000);
            core.Add(ResourceIds.Iron, 150);
            using var city = new CityResourceStorageModel(core, 150);
            Assert.That(city.TryRegisterWarehouse(
                "building.instance.backpack-target",
                connected: true), Is.True);

            ResourceTransferResult result =
                ResourceTransaction.TransferFromBackpackSlot(
                    backpack,
                    0,
                    city,
                    12);

            Assert.That(result.Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Assert.That(result.MovedAmount, Is.EqualTo(12));
            Assert.That(core.Get(ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(city.GetWarehouseAmount(
                "building.instance.backpack-target",
                ResourceIds.Iron), Is.EqualTo(12));
            Assert.That(BackpackTotal(backpack, ResourceIds.Iron), Is.EqualTo(8));
        }

        [Test]
        public void TransferFromBackpackSlotRemovesOnlyTheSelectedStack()
        {
            var backpack = new PlayerBackpackModel();
            var city = new ResourceInventory(1000);
            var cityCapacity = new ResourceCapacityPolicy();
            backpack.Add(ResourceIds.Iron, 150);

            ResourceTransferResult result =
                ResourceTransaction.TransferFromBackpackSlot(
                    backpack,
                    1,
                    city,
                    cityCapacity,
                    0,
                    50);

            Assert.That(result.Status, Is.EqualTo(ResourceTransferStatus.Completed));
            Assert.That(result.MovedAmount, Is.EqualTo(50));
            Assert.That(backpack.GetSlot(0).ResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(backpack.GetSlot(0).Amount, Is.EqualTo(100));
            Assert.That(backpack.GetSlot(1).ResourceId, Is.Null);
            Assert.That(backpack.GetSlot(1).Amount, Is.Zero);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(50));
        }

        private static int Total(
            ResourceInventory first,
            ResourceInventory second,
            params string[] resourceIds)
        {
            int total = 0;
            foreach (string resourceId in resourceIds)
            {
                total += first.Get(resourceId);
                total += second.Get(resourceId);
            }

            return total;
        }

        private static int Total(
            ResourceInventory inventory,
            PlayerBackpackModel backpack,
            params string[] resourceIds)
        {
            int total = 0;
            foreach (string resourceId in resourceIds)
            {
                total += inventory.Get(resourceId);
                total += BackpackTotal(backpack, resourceId);
            }

            return total;
        }

        private static int BackpackTotal(
            PlayerBackpackModel backpack,
            string resourceId)
        {
            int total = 0;
            for (int index = 0; index < backpack.SlotCount; index++)
            {
                BackpackSlot slot = backpack.GetSlot(index);
                if (slot.ResourceId == resourceId)
                    total += slot.Amount;
            }

            return total;
        }
    }
}
