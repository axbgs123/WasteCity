using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Graybox3D.Production;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionCatalog3DTests
    {
        [Test]
        public void IDEA0011_Catalog_UsesApprovedStableBuildingIds()
        {
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    BuildingCatalog.MiningStation.Id.Value,
                    out GrayboxProductionDefinition3D mining),
                Is.True);
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    BuildingCatalog.Smelter.Id.Value,
                    out GrayboxProductionDefinition3D smelter),
                Is.True);
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    BuildingCatalog.Assembler.Id.Value,
                    out GrayboxProductionDefinition3D assembler),
                Is.True);

            Assert.That(
                mining.BuildingId,
                Is.EqualTo(BuildingCatalog.MiningStation.Id.Value));
            Assert.That(
                smelter.BuildingId,
                Is.EqualTo(BuildingCatalog.Smelter.Id.Value));
            Assert.That(
                assembler.BuildingId,
                Is.EqualTo(BuildingCatalog.Assembler.Id.Value));
        }

        [Test]
        public void IDEA0011_MiningStation_UsesBoundNodeResourceAndApprovedCapacity()
        {
            GrayboxProductionDefinition3D definition = Definition(
                BuildingCatalog.MiningStation.Id.Value);

            Assert.That(
                definition.Kind,
                Is.EqualTo(GrayboxProductionKind3D.Extraction));
            Assert.That(definition.CycleSeconds, Is.EqualTo(3f));
            Assert.That(definition.InputResourceId, Is.Null);
            Assert.That(definition.InputAmount, Is.Zero);
            Assert.That(
                definition.OutputResourceId,
                Is.Null,
                "Extraction output must come from the bound WorldMapModel node.");
            Assert.That(definition.OutputAmount, Is.EqualTo(1));
            Assert.That(definition.InputCapacity, Is.Zero);
            Assert.That(definition.OutputCapacity, Is.EqualTo(20));
        }

        [Test]
        public void IDEA0011_Smelter_UsesApprovedRecipeCycleAndBuffers()
        {
            GrayboxProductionDefinition3D definition = Definition(
                BuildingCatalog.Smelter.Id.Value);

            Assert.That(
                definition.Kind,
                Is.EqualTo(GrayboxProductionKind3D.Recipe));
            Assert.That(definition.CycleSeconds, Is.EqualTo(6f));
            Assert.That(definition.InputResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(definition.InputAmount, Is.EqualTo(2));
            Assert.That(definition.OutputResourceId, Is.EqualTo(ResourceIds.Alloy));
            Assert.That(definition.OutputAmount, Is.EqualTo(1));
            Assert.That(definition.InputCapacity, Is.EqualTo(20));
            Assert.That(definition.OutputCapacity, Is.EqualTo(10));
        }

        [Test]
        public void IDEA0011_Assembler_UsesApprovedRecipeCycleAndBuffers()
        {
            GrayboxProductionDefinition3D definition = Definition(
                BuildingCatalog.Assembler.Id.Value);

            Assert.That(
                definition.Kind,
                Is.EqualTo(GrayboxProductionKind3D.Recipe));
            Assert.That(definition.CycleSeconds, Is.EqualTo(6f));
            Assert.That(definition.InputResourceId, Is.EqualTo(ResourceIds.Alloy));
            Assert.That(definition.InputAmount, Is.EqualTo(2));
            Assert.That(
                definition.OutputResourceId,
                Is.EqualTo(ResourceIds.Ammunition));
            Assert.That(definition.OutputAmount, Is.EqualTo(2));
            Assert.That(definition.InputCapacity, Is.EqualTo(20));
            Assert.That(definition.OutputCapacity, Is.EqualTo(30));
        }

        [Test]
        public void IDEA0011_Catalog_DoesNotDefineTurretProduction()
        {
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    BuildingCatalog.MachineGunTurret.Id.Value,
                    out GrayboxProductionDefinition3D definition),
                Is.False);
            Assert.That(definition, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("unknown.building")]
        [TestCase("core.building.mining-station-forged")]
        public void IDEA0011_Catalog_RejectsUnknownOrMalformedIds(string buildingId)
        {
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    buildingId,
                    out GrayboxProductionDefinition3D definition),
                Is.False);
            Assert.That(definition, Is.Null);
        }

        [TestCase(ResourceIds.Iron)]
        [TestCase(ResourceIds.EnergyCrystal)]
        public void IDEA0011_Mining_PreservesIDEA0010CompatibleNodes(
            string resourceId)
        {
            Assert.That(
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    BuildingCatalog.MiningStation,
                    resourceId),
                Is.True,
                "IDEA0011 must preserve IDEA0010 Iron and EnergyCrystal compatibility.");
        }

        private static GrayboxProductionDefinition3D Definition(
            string buildingId)
        {
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    buildingId,
                    out GrayboxProductionDefinition3D definition),
                Is.True,
                $"Expected production definition for {buildingId}.");
            return definition;
        }
    }
}
