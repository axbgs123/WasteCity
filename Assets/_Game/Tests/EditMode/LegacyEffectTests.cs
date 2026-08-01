using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Legacy;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class LegacyEffectTests
    {
        private static LegacyEffectModel Select(string id)
        { var selection = new LegacySelectionModel(new WorldSeed(1)); Assert.That(selection.Restore(id), Is.True); return new LegacyEffectModel(selection, new WorldSeed(8128)); }
        [Test] public void PocketUniverseDoublesFirstCompletedBuilding()
        { var model = Select(LegacyEffectModel.PocketUniverse); Assert.That(model.ProductionUnits(0), Is.Zero); Assert.That(model.ProductionUnits(1), Is.EqualTo(2)); Assert.That(model.ProductionUnits(3), Is.EqualTo(4)); }
        [Test] public void OtherLegacyDoesNotChangeProductionUnits()
        { Assert.That(Select(LegacyEffectModel.VoidChest).ProductionUnits(3), Is.EqualTo(3)); }
        [Test] public void VoidDebtAllowsConfiguredNegativeInventory()
        { var inventory = new ResourceInventory(100); inventory.SetDebtLimit(20); Assert.That(inventory.TrySpend(ResourceIds.Iron, 15), Is.True); Assert.That(inventory.Get(ResourceIds.Iron), Is.EqualTo(-15)); Assert.That(inventory.TrySpend(ResourceIds.Iron, 6), Is.False); }
        [Test] public void GrayChestRollIsDeterministicAndOnePercent()
        { var model = Select(LegacyEffectModel.VoidChest); int hits = 0; for (int i = 0; i < 10000; i++) if (model.RollsGrayChest(i)) hits++; Assert.That(hits, Is.InRange(70, 130)); Assert.That(model.RollsGrayChest(42), Is.EqualTo(model.RollsGrayChest(42))); }
    }
}
