using System.Linq;
using NUnit.Framework;
using WasteCity.Building;

namespace WasteCity.Tests
{
    public sealed class RouteCapstoneBuildingTests
    {
        [Test]
        public void CatalogContainsOneTerminalBuildingForEachRoute()
        {
            var definitions = new[]
            {
                BuildingCatalog.PowerPlant,
                BuildingCatalog.SpiritGatheringArray,
                BuildingCatalog.MetabolicFurnace,
                BuildingCatalog.ConsciousnessNetwork
            };
            string[] expectedIds =
            {
                "technology.building.power-plant",
                "cultivation.building.spirit-gathering-array",
                "biological.building.metabolic-furnace",
                "psionics.building.consciousness-network"
            };

            Assert.That(definitions.Select(value => value.Id.Value), Is.EqualTo(expectedIds));
            Assert.That(definitions.Select(value => value.Id.Value).Distinct().Count(), Is.EqualTo(4));
            foreach (var definition in definitions)
            {
                Assert.That(definition.Width, Is.EqualTo(2));
                Assert.That(definition.Height, Is.EqualTo(2));
                Assert.That(definition.MinimumPopulation, Is.EqualTo(1000));
                Assert.That(
                    BuildingCatalog.All.Count(value => value.Id.Value == definition.Id.Value),
                    Is.EqualTo(1));
                Assert.That(
                    BuildingCatalog.BuildMenu.Count(value => value.Id.Value == definition.Id.Value),
                    Is.EqualTo(1));
            }
        }

        [TestCase("technology.building.power-plant", "core.research.thermal-engineering")]
        [TestCase("cultivation.building.spirit-gathering-array", "core.research.spirit-gathering")]
        [TestCase("biological.building.metabolic-furnace", "core.research.metabolic-acceleration")]
        [TestCase("psionics.building.consciousness-network", "core.research.consciousness-network")]
        public void TerminalBuildingRequiresPopulationAndExactResearch(
            string buildingId,
            string researchId)
        {
            var definition = BuildingCatalog.All.Single(value => value.Id.Value == buildingId);

            Assert.That(
                BuildingUnlockModel.IsUnlocked(
                    definition,
                    999,
                    id => id == researchId,
                    _ => 0,
                    out string populationReason),
                Is.False);
            Assert.That(populationReason, Does.Contain("1000"));
            Assert.That(
                BuildingUnlockModel.IsUnlocked(
                    definition,
                    1000,
                    _ => false,
                    _ => 0,
                    out string researchReason),
                Is.False);
            Assert.That(researchReason, Does.Contain(researchId));
            Assert.That(
                BuildingUnlockModel.IsUnlocked(
                    definition,
                    1000,
                    id => id == researchId,
                    _ => 0,
                    out _),
                Is.True);
        }
    }
}
