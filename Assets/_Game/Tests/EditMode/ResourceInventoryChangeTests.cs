using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ResourceInventoryChangeTests
    {
        [Test]
        public void AddEmitsOnlyTheActuallyAcceptedPositiveDelta()
        {
            var inventory = new ResourceInventory(10);
            var changes = CaptureChanges(inventory);

            Assert.That(inventory.Add(ResourceIds.Iron, 7), Is.EqualTo(7));
            Assert.That(inventory.Add(ResourceIds.Iron, 8), Is.EqualTo(3));
            Assert.That(inventory.Add(ResourceIds.Iron, 1), Is.Zero);
            Assert.That(inventory.Add(ResourceIds.Iron, 0), Is.Zero);

            Assert.That(changes, Is.EqualTo(new[]
            {
                Change(ResourceIds.Iron, 7),
                Change(ResourceIds.Iron, 3)
            }));
        }

        [Test]
        public void TrySpendEmitsOnlySuccessfulNonZeroNegativeDelta()
        {
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.Iron, 7);
            var changes = CaptureChanges(inventory);

            Assert.That(inventory.TrySpend(ResourceIds.Iron, 4), Is.True);
            Assert.That(inventory.TrySpend(ResourceIds.Iron, 4), Is.False);
            Assert.That(inventory.TrySpend(ResourceIds.Iron, 0), Is.True);

            Assert.That(changes, Is.EqualTo(new[]
            {
                Change(ResourceIds.Iron, -4)
            }));
        }

        [Test]
        public void SetAndRestoreEmitTheirClampedActualDeltasIncludingRollback()
        {
            var inventory = new ResourceInventory(10);
            inventory.SetDebtLimit(5);
            var changes = CaptureChanges(inventory);

            inventory.Set(ResourceIds.Iron, 20);
            inventory.Set(ResourceIds.Iron, 10);
            Assert.That(inventory.TrySpend(ResourceIds.Iron, 4), Is.True);
            inventory.Restore(ResourceIds.Iron, 10);
            inventory.Restore(ResourceIds.Iron, -20);
            inventory.Restore(ResourceIds.Iron, -5);

            Assert.That(changes, Is.EqualTo(new[]
            {
                Change(ResourceIds.Iron, 10),
                Change(ResourceIds.Iron, -4),
                Change(ResourceIds.Iron, 4),
                Change(ResourceIds.Iron, -15)
            }));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(-5));
        }

        [Test]
        public void AddCapacityReductionEmitsEachResourcesActualLoss()
        {
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.Iron, 10);
            inventory.Add(ResourceIds.Water, 8);
            var changes = CaptureChanges(inventory);

            inventory.AddCapacity(5);
            inventory.AddCapacity(-9);

            Assert.That(inventory.CapacityPerResource, Is.EqualTo(6));
            Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(6));
            Assert.That(inventory.Get(ResourceIds.Water), Is.EqualTo(6));
            Assert.That(changes, Is.EquivalentTo(new[]
            {
                Change(ResourceIds.Iron, -4),
                Change(ResourceIds.Water, -2)
            }));
        }

        private static List<(string ResourceId, int Delta)> CaptureChanges(
            ResourceInventory inventory)
        {
            var changes = new List<(string ResourceId, int Delta)>();
            inventory.Changed += (resourceId, delta) =>
                changes.Add(Change(resourceId, delta));
            return changes;
        }

        private static (string ResourceId, int Delta) Change(
            string resourceId,
            int delta)
        {
            return (resourceId, delta);
        }
    }
}
