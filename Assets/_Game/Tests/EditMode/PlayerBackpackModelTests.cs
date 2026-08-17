using System;
using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class PlayerBackpackModelTests
    {
        [Test]
        public void FormalBackpackHasThirtyEmptySlotsWithOneHundredUnitStacks()
        {
            var backpack = new PlayerBackpackModel();

            Assert.That(backpack.SlotCount, Is.EqualTo(30));
            Assert.That(backpack.StackLimit, Is.EqualTo(100));
            for (int index = 0; index < backpack.SlotCount; index++)
            {
                Assert.That(backpack.GetSlot(index).ResourceId, Is.Null);
                Assert.That(backpack.GetSlot(index).Amount, Is.Zero);
            }
        }

        [Test]
        public void AddSameResourceCreatesMultipleStacksInStableSlotOrder()
        {
            var backpack = new PlayerBackpackModel();

            Assert.That(backpack.Add(ResourceIds.Iron, 250), Is.EqualTo(250));

            AssertSlot(backpack, 0, ResourceIds.Iron, 100);
            AssertSlot(backpack, 1, ResourceIds.Iron, 100);
            AssertSlot(backpack, 2, ResourceIds.Iron, 50);
            AssertEmpty(backpack, 3);
        }

        [Test]
        public void AddMergesExistingStackBeforeUsingLowestEmptySlot()
        {
            var backpack = new PlayerBackpackModel();
            Assert.That(backpack.Add(ResourceIds.Iron, 150), Is.EqualTo(150));
            Assert.That(backpack.Add(ResourceIds.Alloy, 100), Is.EqualTo(100));
            Assert.That(backpack.Remove(ResourceIds.Iron, 100), Is.EqualTo(100));

            Assert.That(backpack.Add(ResourceIds.Iron, 75), Is.EqualTo(75));

            AssertSlot(backpack, 0, ResourceIds.Iron, 25);
            AssertSlot(backpack, 1, ResourceIds.Iron, 100);
            AssertSlot(backpack, 2, ResourceIds.Alloy, 100);
        }

        [Test]
        public void RemoveConsumesSameResourceFromLowestSlotFirst()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 250);

            Assert.That(backpack.Remove(ResourceIds.Iron, 150), Is.EqualTo(150));

            AssertEmpty(backpack, 0);
            AssertSlot(backpack, 1, ResourceIds.Iron, 50);
            AssertSlot(backpack, 2, ResourceIds.Iron, 50);
            Assert.That(Total(backpack, ResourceIds.Iron), Is.EqualTo(100));
        }

        [Test]
        public void WholeAddAndRemoveReportExactMovedAmounts()
        {
            var backpack = new PlayerBackpackModel();

            Assert.That(backpack.Add(ResourceIds.Biomass, 175), Is.EqualTo(175));
            Assert.That(backpack.Remove(ResourceIds.Biomass, 75), Is.EqualTo(75));
            Assert.That(Total(backpack, ResourceIds.Biomass), Is.EqualTo(100));

            Assert.That(backpack.Remove(ResourceIds.Biomass, 200), Is.EqualTo(100));
            Assert.That(Total(backpack, ResourceIds.Biomass), Is.Zero);
        }

        [Test]
        public void AddPartiallyAcceptsOnlyTheCapacityThatRemains()
        {
            var backpack = new PlayerBackpackModel();
            Assert.That(backpack.Add(ResourceIds.Iron, 2900), Is.EqualTo(2900));

            Assert.That(backpack.Add(ResourceIds.Alloy, 150), Is.EqualTo(100));

            AssertSlot(backpack, 29, ResourceIds.Alloy, 100);
            Assert.That(Total(backpack, ResourceIds.Iron), Is.EqualTo(2900));
            Assert.That(Total(backpack, ResourceIds.Alloy), Is.EqualTo(100));
            Assert.That(backpack.Add(ResourceIds.EnergyCrystal, 1), Is.Zero);
        }

        [Test]
        public void SplitHalfRoundsMovedHalfUpAndConservesTheResourceTotal()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 5);
            int before = Total(backpack, ResourceIds.Iron);

            Assert.That(backpack.SplitHalf(0, 1), Is.True);

            AssertSlot(backpack, 0, ResourceIds.Iron, 2);
            AssertSlot(backpack, 1, ResourceIds.Iron, 3);
            Assert.That(Total(backpack, ResourceIds.Iron), Is.EqualTo(before));
        }

        [Test]
        public void SplitHalfFailsClosedForOccupiedOrInvalidTargets()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 5);
            backpack.Add(ResourceIds.Alloy, 1);
            int ironBefore = Total(backpack, ResourceIds.Iron);
            int alloyBefore = Total(backpack, ResourceIds.Alloy);

            Assert.That(backpack.SplitHalf(0, 1), Is.False);
            Assert.That(backpack.SplitHalf(-1, 2), Is.False);
            Assert.That(backpack.SplitHalf(0, backpack.SlotCount), Is.False);

            AssertSlot(backpack, 0, ResourceIds.Iron, 5);
            AssertSlot(backpack, 1, ResourceIds.Alloy, 1);
            Assert.That(Total(backpack, ResourceIds.Iron), Is.EqualTo(ironBefore));
            Assert.That(Total(backpack, ResourceIds.Alloy), Is.EqualTo(alloyBefore));
        }

        [Test]
        public void MoveOneMergesOrUsesAnEmptySlotWithoutOverflowOrLoss()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 199);
            int before = Total(backpack, ResourceIds.Iron);

            Assert.That(backpack.MoveOne(0, 1), Is.True);
            AssertSlot(backpack, 0, ResourceIds.Iron, 99);
            AssertSlot(backpack, 1, ResourceIds.Iron, 100);
            Assert.That(backpack.MoveOne(1, 2), Is.True);
            AssertSlot(backpack, 1, ResourceIds.Iron, 99);
            AssertSlot(backpack, 2, ResourceIds.Iron, 1);
            Assert.That(backpack.MoveOne(2, 1), Is.True);
            AssertSlot(backpack, 1, ResourceIds.Iron, 100);
            AssertEmpty(backpack, 2);
            Assert.That(Total(backpack, ResourceIds.Iron), Is.EqualTo(before));
        }

        [Test]
        public void MoveOneFailsClosedWhenTargetIsFullOrContainsAnotherResource()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 101);
            backpack.Add(ResourceIds.Alloy, 1);
            int before = TotalAll(backpack);

            Assert.That(backpack.MoveOne(1, 0), Is.False);
            Assert.That(backpack.MoveOne(1, 2), Is.False);

            AssertSlot(backpack, 0, ResourceIds.Iron, 100);
            AssertSlot(backpack, 1, ResourceIds.Iron, 1);
            AssertSlot(backpack, 2, ResourceIds.Alloy, 1);
            Assert.That(TotalAll(backpack), Is.EqualTo(before));
        }

        [Test]
        public void MoveWholeStackMovesEveryUnitIntoAnEmptySlot()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 25);
            int before = TotalAll(backpack);

            Assert.That(backpack.MoveWholeStack(0, 3), Is.True);

            AssertEmpty(backpack, 0);
            AssertSlot(backpack, 3, ResourceIds.Iron, 25);
            Assert.That(TotalAll(backpack), Is.EqualTo(before));
        }

        [Test]
        public void MoveWholeStackFillsSameResourceTargetAndLeavesRemainderAtSource()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 160);
            int before = Total(backpack, ResourceIds.Iron);

            Assert.That(backpack.MoveWholeStack(0, 1), Is.True);

            AssertSlot(backpack, 0, ResourceIds.Iron, 60);
            AssertSlot(backpack, 1, ResourceIds.Iron, 100);
            Assert.That(Total(backpack, ResourceIds.Iron), Is.EqualTo(before));
        }

        [Test]
        public void MoveWholeStackAtomicallySwapsDifferentResources()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 25);
            backpack.Add(ResourceIds.Alloy, 40);
            int before = TotalAll(backpack);

            Assert.That(backpack.MoveWholeStack(0, 1), Is.True);

            AssertSlot(backpack, 0, ResourceIds.Alloy, 40);
            AssertSlot(backpack, 1, ResourceIds.Iron, 25);
            Assert.That(TotalAll(backpack), Is.EqualTo(before));
        }

        [Test]
        public void MoveWholeStackFailsWithoutChangingAnySlotForInvalidSameOrEmptySources()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 25);
            backpack.Add(ResourceIds.Alloy, 40);
            BackpackSlot[] before = CaptureSlots(backpack);

            Assert.That(backpack.MoveWholeStack(-1, 0), Is.False);
            AssertSlotsEqual(before, backpack);
            Assert.That(backpack.MoveWholeStack(0, backpack.SlotCount), Is.False);
            AssertSlotsEqual(before, backpack);
            Assert.That(backpack.MoveWholeStack(0, 0), Is.False);
            AssertSlotsEqual(before, backpack);
            Assert.That(backpack.MoveWholeStack(2, 3), Is.False);
            AssertSlotsEqual(before, backpack);
        }

        [Test]
        public void GetSlotThrowsForIndexesOutsideTheFormalBackpack()
        {
            var backpack = new PlayerBackpackModel();

            Assert.Throws<ArgumentOutOfRangeException>(() => backpack.GetSlot(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => backpack.GetSlot(backpack.SlotCount));
        }

        [Test]
        public void PreviouslyReadSlotRemainsAnImmutableSnapshotAfterModelChanges()
        {
            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 7);
            BackpackSlot snapshot = backpack.GetSlot(0);

            backpack.Add(ResourceIds.Iron, 1);

            Assert.That(snapshot.ResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(snapshot.Amount, Is.EqualTo(7));
            AssertSlot(backpack, 0, ResourceIds.Iron, 8);
        }

        [Test]
        public void InvalidResourceIdsAndNonPositiveAmountsFailClosed()
        {
            var backpack = new PlayerBackpackModel();

            Assert.That(backpack.Add(null, 10), Is.Zero);
            Assert.That(backpack.Add(string.Empty, 10), Is.Zero);
            Assert.That(backpack.Add("   ", 10), Is.Zero);
            Assert.That(backpack.Add("unknown.resource", 10), Is.Zero);
            Assert.That(backpack.Add(ResourceIds.Iron, 0), Is.Zero);
            Assert.That(backpack.Add(ResourceIds.Iron, -1), Is.Zero);
            Assert.That(backpack.Remove("unknown.resource", 10), Is.Zero);
            Assert.That(backpack.Remove(ResourceIds.Iron, 0), Is.Zero);
            Assert.That(backpack.Remove(ResourceIds.Iron, -1), Is.Zero);

            Assert.That(TotalAll(backpack), Is.Zero);
            for (int index = 0; index < backpack.SlotCount; index++)
                AssertEmpty(backpack, index);
        }

        private static int Total(PlayerBackpackModel backpack, string resourceId)
        {
            int total = 0;
            for (int index = 0; index < backpack.SlotCount; index++)
            {
                if (backpack.GetSlot(index).ResourceId == resourceId)
                    total += backpack.GetSlot(index).Amount;
            }

            return total;
        }

        private static int TotalAll(PlayerBackpackModel backpack)
        {
            int total = 0;
            for (int index = 0; index < backpack.SlotCount; index++)
                total += backpack.GetSlot(index).Amount;
            return total;
        }

        private static BackpackSlot[] CaptureSlots(PlayerBackpackModel backpack)
        {
            var slots = new BackpackSlot[backpack.SlotCount];
            for (int index = 0; index < slots.Length; index++)
                slots[index] = backpack.GetSlot(index);
            return slots;
        }

        private static void AssertSlotsEqual(BackpackSlot[] expected, PlayerBackpackModel actual)
        {
            Assert.That(actual.SlotCount, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual.GetSlot(index).ResourceId, Is.EqualTo(expected[index].ResourceId));
                Assert.That(actual.GetSlot(index).Amount, Is.EqualTo(expected[index].Amount));
            }
        }

        private static void AssertSlot(PlayerBackpackModel backpack, int index, string resourceId, int amount)
        {
            Assert.That(backpack.GetSlot(index).ResourceId, Is.EqualTo(resourceId));
            Assert.That(backpack.GetSlot(index).Amount, Is.EqualTo(amount));
        }

        private static void AssertEmpty(PlayerBackpackModel backpack, int index)
        {
            Assert.That(backpack.GetSlot(index).ResourceId, Is.Null);
            Assert.That(backpack.GetSlot(index).Amount, Is.Zero);
        }
    }
}
