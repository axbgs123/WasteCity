using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class BehemothBreedingTests
    {
        [Test]
        public void CompletedCycleConsumesBiologicalMaterials()
        {
            var inventory = StockedInventory(); var model = new BehemothBreedingModel();
            Assert.That(model.Tick(BehemothBreedingModel.SecondsPerUnit, 1, 0, inventory), Is.EqualTo(1));
            Assert.That(inventory.Get(ResourceIds.BoneSteel), Is.EqualTo(8));
            Assert.That(inventory.Get(ResourceIds.BiomassConcentrate), Is.EqualTo(7));
        }

        [Test]
        public void EachCompletedPenSupportsOneLivingBehemoth()
        {
            var inventory = StockedInventory(); var model = new BehemothBreedingModel();
            Assert.That(model.Capacity(2), Is.EqualTo(2));
            Assert.That(model.Tick(100f, 1, 1, inventory), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.BoneSteel), Is.EqualTo(10));
        }

        [Test]
        public void MissingConcentrateDoesNotPartiallySpendBoneSteel()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.BoneSteel, 10);
            var model = new BehemothBreedingModel();
            Assert.That(model.Tick(40f, 1, 0, inventory), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.BoneSteel), Is.EqualTo(10));
            Assert.That(model.Progress, Is.EqualTo(BehemothBreedingModel.SecondsPerUnit));
        }

        private static ResourceInventory StockedInventory()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.BoneSteel, 10); inventory.Add(ResourceIds.BiomassConcentrate, 10); return inventory;
        }
    }
}
