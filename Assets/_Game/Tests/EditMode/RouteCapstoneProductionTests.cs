using NUnit.Framework;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class RouteCapstoneProductionTests
    {
        [Test]
        public void PowerPlantProducesOneEnergyProxyEverySixSeconds()
        {
            var inventory = new ResourceInventory(100);
            var process = RouteCapstoneProductionCatalog.CreatePowerPlant();

            Assert.That(process.Tick(5.9f, inventory, 1), Is.Zero);
            Assert.That(process.Tick(.1f, inventory, 1), Is.EqualTo(1));
            Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.EqualTo(1));
        }

        [Test]
        public void MetabolicFurnaceConvertsTwoBiomassIntoOneEnergyProxy()
        {
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Biomass, 2);
            var process = RouteCapstoneProductionCatalog.CreateMetabolicFurnace();

            Assert.That(process.Tick(8f, inventory, 1), Is.EqualTo(1));

            Assert.That(inventory.Get(ResourceIds.Biomass), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.EqualTo(1));
        }

        [Test]
        public void ConsciousnessNetworkProducesOnePsionicProxyEveryTenSeconds()
        {
            var inventory = new ResourceInventory(100);
            var process = RouteCapstoneProductionCatalog.CreateConsciousnessNetwork();

            Assert.That(process.Tick(10f, inventory, 1), Is.EqualTo(1));

            Assert.That(inventory.Get(ResourceIds.PsionicAmplifier), Is.EqualTo(1));
        }

        [Test]
        public void CapstoneProcessesDoNothingWithoutOnlineBuildings()
        {
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Biomass, 10);

            Assert.That(
                RouteCapstoneProductionCatalog.CreatePowerPlant().Tick(20f, inventory, 0),
                Is.Zero);
            Assert.That(
                RouteCapstoneProductionCatalog.CreateMetabolicFurnace().Tick(20f, inventory, 0),
                Is.Zero);
            Assert.That(
                RouteCapstoneProductionCatalog.CreateConsciousnessNetwork().Tick(20f, inventory, 0),
                Is.Zero);
            Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.PsionicAmplifier), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.EqualTo(10));
        }
    }
}
