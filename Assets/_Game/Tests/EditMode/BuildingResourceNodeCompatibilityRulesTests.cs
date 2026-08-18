using System;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class BuildingResourceNodeCompatibilityRulesTests
    {
        [TestCase(ResourceIds.Iron)]
        [TestCase(ResourceIds.EnergyCrystal)]
        [TestCase(ResourceIds.Stone)]
        public void IDEA0010_MiningStation_AcceptsApprovedResource(
            string resourceId)
        {
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    BuildingCatalog.MiningStation,
                    resourceId),
                Is.True,
                $"IDEA0010 MiningStation must accept {resourceId}");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(ResourceIds.Biomass)]
        [TestCase(ResourceIds.Water)]
        [TestCase("unknown.resource")]
        public void IDEA0010_MiningStation_RejectsUnapprovedResource(
            string resourceId)
        {
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    BuildingCatalog.MiningStation,
                    resourceId),
                Is.False,
                $"IDEA0010 MiningStation must reject {resourceId ?? "null"}");
        }

        [Test]
        public void IDEA0010_NullDefinition_NeverMatches()
        {
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    null,
                    ResourceIds.Iron),
                Is.False,
                "IDEA0010 null definition must reject Iron");
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    null,
                    ResourceIds.EnergyCrystal),
                Is.False,
                "IDEA0010 null definition must reject EnergyCrystal");
        }

        [Test]
        public void IDEA0010_AllOtherCatalogBuildingsRejectCompatibleResources()
        {
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition definition = BuildingCatalog.All[index];
                if (ReferenceEquals(
                        definition,
                        BuildingCatalog.MiningStation))
                    continue;

                Assert.That(
                    BuildingResourceNodeCompatibilityRules.IsCompatible(
                        definition,
                        ResourceIds.Iron),
                    Is.False,
                    $"IDEA0010 {definition.Id.Value} must reject Iron");
                Assert.That(
                    BuildingResourceNodeCompatibilityRules.IsCompatible(
                        definition,
                        ResourceIds.EnergyCrystal),
                    Is.False,
                    $"IDEA0010 {definition.Id.Value} must reject EnergyCrystal");
                Assert.That(
                    BuildingResourceNodeCompatibilityRules.IsCompatible(
                        definition,
                        ResourceIds.Stone),
                    Is.False,
                    $"IDEA0012 {definition.Id.Value} must reject Stone");
            }
        }

        [Test]
        public void IDEA0010_EquivalentStableIdDoesNotRequireReferenceIdentity()
        {
            var equivalent = new BuildingDefinition(
                BuildingCatalog.MiningStation.Id.Value,
                "Equivalent Mining Station",
                BuildingCatalog.MiningStation.Width,
                BuildingCatalog.MiningStation.Height,
                BuildingCatalog.MiningStation.CostId,
                BuildingCatalog.MiningStation.Cost,
                true);

            Assert.That(
                equivalent,
                Is.Not.SameAs(BuildingCatalog.MiningStation));
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    equivalent,
                    ResourceIds.Iron),
                Is.True,
                "IDEA0010 equivalent MiningStation stable ID must accept Iron");
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    equivalent,
                    ResourceIds.EnergyCrystal),
                Is.True,
                "IDEA0010 equivalent MiningStation stable ID must accept EnergyCrystal");
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    equivalent,
                    ResourceIds.Stone),
                Is.True,
                "IDEA0012 equivalent MiningStation stable ID must accept Stone");
        }
    }
}
