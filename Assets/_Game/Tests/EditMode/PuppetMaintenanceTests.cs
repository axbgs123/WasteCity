using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class PuppetMaintenanceTests
    {
        [Test]
        public void CompletedCycleConsumesOneEnergyCrystal()
        {
            var inventory = new ResourceInventory(10);
            inventory.Add(ResourceIds.EnergyCrystal, 1);
            var model = new PuppetMaintenanceModel();

            Assert.That(model.Tick(59.9f, inventory), Is.False);
            Assert.That(model.Active, Is.True);
            Assert.That(model.Elapsed, Is.EqualTo(59.9f).Within(.001f));
            Assert.That(model.Tick(.1f, inventory), Is.True);
            Assert.That(model.Active, Is.True);
            Assert.That(model.Elapsed, Is.Zero.Within(.001f));
            Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.Zero);
        }

        [Test]
        public void ShortageMakesPuppetDormantUntilResourceIsReplenished()
        {
            var inventory = new ResourceInventory(10);
            var model = new PuppetMaintenanceModel();

            Assert.That(model.Tick(PuppetMaintenanceModel.CycleSeconds, inventory), Is.False);
            Assert.That(model.Active, Is.False);
            Assert.That(model.Elapsed, Is.EqualTo(PuppetMaintenanceModel.CycleSeconds));

            inventory.Add(ResourceIds.EnergyCrystal, 1);
            Assert.That(model.Tick(0f, inventory), Is.True);
            Assert.That(model.Active, Is.True);
            Assert.That(model.Elapsed, Is.Zero);
        }

        [Test]
        public void MissingInventoryKeepsLegacyPuppetsActive()
        {
            var model = new PuppetMaintenanceModel();
            model.Tick(PuppetMaintenanceModel.CycleSeconds * 2f, null);

            Assert.That(model.Active, Is.True);
            Assert.That(model.Elapsed, Is.Zero);
        }

        [Test]
        public void RestoreClampsInvalidState()
        {
            var model = new PuppetMaintenanceModel();

            model.Restore(90f, false);
            Assert.That(model.Active, Is.False);
            Assert.That(model.Elapsed, Is.EqualTo(PuppetMaintenanceModel.CycleSeconds));

            model.Restore(-1f, true);
            Assert.That(model.Active, Is.True);
            Assert.That(model.Elapsed, Is.Zero);
        }
    }
}
