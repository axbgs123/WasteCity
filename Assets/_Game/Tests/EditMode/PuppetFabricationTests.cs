using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class PuppetFabricationTests
    {
        [Test]
        public void NoWorkshopDoesNotAdvanceOrProduce()
        {
            var inventory = StockedInventory();
            var model = new PuppetFabricationModel();
            Assert.That(model.Tick(60f, 0, 0, inventory), Is.Zero);
            Assert.That(model.Progress, Is.Zero);
        }

        [Test]
        public void CompletedCycleConsumesBothMaterialsAndProducesPuppet()
        {
            var inventory = StockedInventory();
            var model = new PuppetFabricationModel();
            Assert.That(model.Tick(PuppetFabricationModel.SecondsPerUnit, 1, 0, inventory), Is.EqualTo(1));
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(9));
            Assert.That(inventory.Get(ResourceIds.SpiritIron), Is.EqualTo(9));
            Assert.That(model.Progress, Is.EqualTo(0f).Within(.001f));
        }

        [Test]
        public void WorkshopCapacityPreventsProductionAndSpending()
        {
            var inventory = StockedInventory();
            var model = new PuppetFabricationModel();
            Assert.That(model.Tick(60f, 1, PuppetFabricationModel.UnitsPerWorkshop, inventory), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(10));
            Assert.That(inventory.Get(ResourceIds.SpiritIron), Is.EqualTo(10));
            Assert.That(model.Capacity(1), Is.EqualTo(3));
        }

        [Test]
        public void MissingEitherMaterialKeepsReadyProgressWithoutPartialSpend()
        {
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Alloy, 10);
            var model = new PuppetFabricationModel();
            Assert.That(model.Tick(30f, 1, 0, inventory), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Alloy), Is.EqualTo(10));
            Assert.That(model.Progress, Is.EqualTo(PuppetFabricationModel.SecondsPerUnit));
        }

        private static ResourceInventory StockedInventory()
        {
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Alloy, 10);
            inventory.Add(ResourceIds.SpiritIron, 10);
            return inventory;
        }
    }
}
