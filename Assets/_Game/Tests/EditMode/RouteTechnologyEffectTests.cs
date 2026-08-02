using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class RouteTechnologyEffectTests
    {
        [Test]
        public void MetabolicAccelerationIncreasesBiomassRecoveryByHalf()
        {
            Assert.That(RouteTechnologyEffects.BiomassDrop(10, 1f, false), Is.EqualTo(10));
            Assert.That(RouteTechnologyEffects.BiomassDrop(10, 1f, true), Is.EqualTo(15));
            Assert.That(RouteTechnologyEffects.BiomassDrop(10, 1.5f, true), Is.EqualTo(23));
        }

        [Test]
        public void PrecognitiveSenseExtendsWarningByHalf()
        {
            Assert.That(RouteTechnologyEffects.WarningMultiplier(false), Is.EqualTo(1f));
            Assert.That(RouteTechnologyEffects.WarningMultiplier(true), Is.EqualTo(1.5f));
        }

        [Test]
        public void TissueRegenerationHealsCompletedBuildingWithoutResourceCost()
        {
            var health = new HealthModel(100);
            health.Apply(20, DamageType.TrueEssence, ArmorType.Light);
            var inventory = new ResourceInventory(100);
            var regeneration = new BuildingRegenerationModel();

            Assert.That(regeneration.Tick(3f, false, true, false, health, inventory), Is.EqualTo(3));
            Assert.That(health.Current, Is.EqualTo(83));
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.Zero);
        }

        [Test]
        public void CarapaceGrowthConsumesBiomassToRegenerateWalls()
        {
            var health = new HealthModel(200);
            health.Restore(150);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Biomass, 2);
            var regeneration = new BuildingRegenerationModel();

            Assert.That(regeneration.Tick(4.9f, true, false, true, health, inventory), Is.Zero);
            Assert.That(regeneration.Tick(.1f, true, false, true, health, inventory), Is.EqualTo(10));
            Assert.That(health.Current, Is.EqualTo(160));
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.EqualTo(1));
        }

        [Test]
        public void CarapaceGrowthDoesNotConsumeBiomassForOtherBuildings()
        {
            var health = new HealthModel(100);
            health.Apply(20, DamageType.TrueEssence, ArmorType.Light);
            var inventory = new ResourceInventory(100);
            inventory.Add(ResourceIds.Biomass, 1);

            Assert.That(new BuildingRegenerationModel().Tick(10f, false, false, true, health, inventory), Is.Zero);
            Assert.That(inventory.Get(ResourceIds.Biomass), Is.EqualTo(1));
        }
    }
}
