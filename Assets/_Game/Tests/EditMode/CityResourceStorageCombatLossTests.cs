using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class CityResourceStorageCombatLossTests
    {
        private const string DestroyedWarehouseId =
            "building.instance.warehouse.destroyed";
        private const string OtherWarehouseId =
            "building.instance.warehouse.other";

        [Test]
        public void ConnectedNonEmptyWarehouseCombatLossIsAtomicAndIdempotent()
        {
            var core = new ResourceInventory(1000);
            Assert.That(core.Add(ResourceIds.Stone, 7), Is.EqualTo(7));
            using var storage = new CityResourceStorageModel(core, 150);
            Assert.That(storage.TryRegisterWarehouse(DestroyedWarehouseId),
                Is.True);
            Assert.That(storage.TryRegisterWarehouse(OtherWarehouseId),
                Is.True);
            Assert.That(storage.TrySetWarehouseFilter(
                OtherWarehouseId,
                ResourceIds.Biomass), Is.True);
            Assert.That(storage.AddToWarehouse(
                OtherWarehouseId,
                ResourceIds.Biomass,
                13), Is.EqualTo(13));
            Assert.That(storage.AddToWarehouse(
                DestroyedWarehouseId,
                ResourceIds.Iron,
                9), Is.EqualTo(9));
            Assert.That(storage.AddToWarehouse(
                DestroyedWarehouseId,
                ResourceIds.EnergyCrystal,
                6), Is.EqualTo(6));
            Assert.That(storage.AddToWarehouse(
                DestroyedWarehouseId,
                ResourceIds.Alloy,
                3), Is.EqualTo(3));

            ulong revisionBefore = storage.Revision;
            int coreStoneBefore = storage.GetCoreAmount(ResourceIds.Stone);
            int otherBiomassBefore = storage.GetWarehouseAmount(
                OtherWarehouseId,
                ResourceIds.Biomass);
            var changes = new List<RecordedChange>();
            storage.AttributedChanged += (resourceId, delta, _) =>
                changes.Add(new RecordedChange(resourceId, delta));

            bool destroyed = TryDestroyWarehouseForCombat(
                storage,
                DestroyedWarehouseId,
                out ResourceAmount[] lost);

            Assert.That(destroyed, Is.True);
            AssertAmounts(lost,
                new ResourceAmount(ResourceIds.EnergyCrystal, 6),
                new ResourceAmount(ResourceIds.Iron, 9),
                new ResourceAmount(ResourceIds.Alloy, 3));
            Assert.That(storage.ContainsWarehouse(DestroyedWarehouseId),
                Is.False);
            Assert.That(storage.TryGetWarehouseSnapshot(
                DestroyedWarehouseId,
                out _), Is.False);
            Assert.That(storage.GetWarehouseFilter(DestroyedWarehouseId),
                Is.Null);
            Assert.That(storage.GetCoreAmount(ResourceIds.Stone),
                Is.EqualTo(coreStoneBefore));
            Assert.That(storage.ContainsWarehouse(OtherWarehouseId), Is.True);
            Assert.That(storage.GetWarehouseFilter(OtherWarehouseId),
                Is.EqualTo(ResourceIds.Biomass));
            Assert.That(storage.GetWarehouseAmount(
                OtherWarehouseId,
                ResourceIds.Biomass), Is.EqualTo(otherBiomassBefore));
            Assert.That(storage.Revision, Is.EqualTo(revisionBefore + 1),
                "Combat loss and warehouse removal must be one commit.");
            AssertNetworkLoss(changes, ResourceIds.EnergyCrystal, 6);
            AssertNetworkLoss(changes, ResourceIds.Iron, 9);
            AssertNetworkLoss(changes, ResourceIds.Alloy, 3);
            Assert.That(changes, Has.Count.EqualTo(3));

            ulong revisionAfter = storage.Revision;
            int eventCountAfter = changes.Count;
            Assert.That(TryDestroyWarehouseForCombat(
                storage,
                DestroyedWarehouseId,
                out ResourceAmount[] repeatedLost), Is.False);
            Assert.That(repeatedLost, Is.Not.Null);
            Assert.That(repeatedLost.Length, Is.Zero);
            Assert.That(storage.Revision, Is.EqualTo(revisionAfter));
            Assert.That(changes, Has.Count.EqualTo(eventCountAfter));
        }

        [Test]
        public void DisconnectedFilteredWarehouseCombatLossDoesNotForgeNetworkChange()
        {
            var core = new ResourceInventory(1000);
            Assert.That(core.Add(ResourceIds.Iron, 5), Is.EqualTo(5));
            using var storage = new CityResourceStorageModel(core, 150);
            Assert.That(storage.TryRegisterWarehouse(DestroyedWarehouseId),
                Is.True);
            Assert.That(storage.TryRegisterWarehouse(OtherWarehouseId),
                Is.True);
            Assert.That(storage.TrySetWarehouseFilter(
                DestroyedWarehouseId,
                ResourceIds.EnergyCrystal), Is.True);
            Assert.That(storage.TrySetWarehouseFilter(
                OtherWarehouseId,
                ResourceIds.Stone), Is.True);
            Assert.That(storage.AddToWarehouse(
                DestroyedWarehouseId,
                ResourceIds.EnergyCrystal,
                17), Is.EqualTo(17));
            Assert.That(storage.AddToWarehouse(
                OtherWarehouseId,
                ResourceIds.Stone,
                12), Is.EqualTo(12));
            Assert.That(storage.TrySetWarehouseConnected(
                DestroyedWarehouseId,
                connected: false), Is.True);

            ulong revisionBefore = storage.Revision;
            int coreIronBefore = storage.GetCoreAmount(ResourceIds.Iron);
            int otherStoneBefore = storage.GetWarehouseAmount(
                OtherWarehouseId,
                ResourceIds.Stone);
            int networkEnergyBefore = storage.GetNetworkAmount(
                ResourceIds.EnergyCrystal);
            var changes = new List<RecordedChange>();
            storage.AttributedChanged += (resourceId, delta, _) =>
                changes.Add(new RecordedChange(resourceId, delta));

            bool destroyed = TryDestroyWarehouseForCombat(
                storage,
                DestroyedWarehouseId,
                out ResourceAmount[] lost);

            Assert.That(destroyed, Is.True);
            AssertAmounts(lost,
                new ResourceAmount(ResourceIds.EnergyCrystal, 17));
            Assert.That(storage.ContainsWarehouse(DestroyedWarehouseId),
                Is.False);
            Assert.That(storage.GetWarehouseFilter(DestroyedWarehouseId),
                Is.Null);
            Assert.That(storage.GetCoreAmount(ResourceIds.Iron),
                Is.EqualTo(coreIronBefore));
            Assert.That(storage.GetWarehouseFilter(OtherWarehouseId),
                Is.EqualTo(ResourceIds.Stone));
            Assert.That(storage.GetWarehouseAmount(
                OtherWarehouseId,
                ResourceIds.Stone), Is.EqualTo(otherStoneBefore));
            Assert.That(storage.GetNetworkAmount(ResourceIds.EnergyCrystal),
                Is.EqualTo(networkEnergyBefore));
            Assert.That(changes, Is.Empty,
                "Disconnected contents were already outside the city network.");
            Assert.That(storage.Revision, Is.EqualTo(revisionBefore + 1),
                "Combat loss and warehouse removal must be one commit.");

            ulong revisionAfter = storage.Revision;
            Assert.That(TryDestroyWarehouseForCombat(
                storage,
                DestroyedWarehouseId,
                out ResourceAmount[] repeatedLost), Is.False);
            Assert.That(repeatedLost, Is.Not.Null);
            Assert.That(repeatedLost.Length, Is.Zero);
            Assert.That(storage.Revision, Is.EqualTo(revisionAfter));
            Assert.That(changes, Is.Empty);
        }

        private static bool TryDestroyWarehouseForCombat(
            CityResourceStorageModel storage,
            string stableInstanceId,
            out ResourceAmount[] lostResources)
        {
            MethodInfo method = typeof(CityResourceStorageModel).GetMethod(
                "TryDestroyWarehouseForCombat",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(ResourceAmount[]).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Combat destruction requires the public atomic API " +
                "TryDestroyWarehouseForCombat(string, out ResourceAmount[]).");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));

            object[] arguments = { stableInstanceId, null };
            bool result = (bool)method.Invoke(storage, arguments);
            lostResources = arguments[1] as ResourceAmount[];
            Assert.That(lostResources, Is.Not.Null,
                "The combat-loss result must always be a non-null array.");
            return result;
        }

        private static void AssertAmounts(
            IReadOnlyList<ResourceAmount> actual,
            params ResourceAmount[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].ResourceId,
                    Is.EqualTo(expected[index].ResourceId),
                    "Lost resources must use stable ordinal ID order.");
                Assert.That(actual[index].Amount,
                    Is.EqualTo(expected[index].Amount),
                    actual[index].ResourceId);
            }
        }

        private static void AssertNetworkLoss(
            IReadOnlyList<RecordedChange> changes,
            string resourceId,
            int amount)
        {
            Assert.That(changes, Has.Exactly(1).Matches<RecordedChange>(change =>
                string.Equals(
                    change.ResourceId,
                    resourceId,
                    StringComparison.Ordinal) &&
                change.Delta == -amount));
        }

        private readonly struct RecordedChange
        {
            public RecordedChange(string resourceId, int delta)
            {
                ResourceId = resourceId;
                Delta = delta;
            }

            public string ResourceId { get; }
            public int Delta { get; }
        }
    }
}
