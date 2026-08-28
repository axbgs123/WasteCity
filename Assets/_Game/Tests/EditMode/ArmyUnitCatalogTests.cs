using System.Linq;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ArmyUnitCatalogTests
    {
        [Test]
        public void IDEA0022_CatalogContainsExactlyFourApprovedUnits()
        {
            Assert.That(ArmyUnitCatalog.All.Count, Is.EqualTo(4));
            AssertUnit(
                ArmyUnitCatalog.CombatPuppet,
                "cultivation.unit.combat-puppet",
                "cultivation.building.puppet-workshop",
                20f, 3, 60f, 100, 12,
                DamageType.TrueEssence, ArmorType.Light, 2.5f,
                Costs((ResourceIds.Alloy, 1), (ResourceIds.SpiritIron, 1)),
                Costs((ResourceIds.EnergyCrystal, 1)));
            AssertUnit(
                ArmyUnitCatalog.BredBehemoth,
                "biological.unit.bred-behemoth",
                "biological.building.behemoth-pen",
                35f, 1, 45f, 320, 24,
                DamageType.Biological, ArmorType.BiologicalShell, 1.2f,
                Costs(
                    (ResourceIds.BoneSteel, 2),
                    (ResourceIds.BiomassConcentrate, 3)),
                Costs((ResourceIds.Biomass, 1)));
            AssertUnit(
                ArmyUnitCatalog.PsionicMech,
                "fusion.unit.psionic-mech",
                "bridge.building.psionic-mech-factory",
                30f, 2, 60f, 220, 20,
                DamageType.Psionic, ArmorType.PsionicShield, 1.8f,
                Costs(
                    (ResourceIds.ControlChip, 2),
                    (ResourceIds.PsionicAmplifier, 1),
                    (ResourceIds.EnergyCell, 1)),
                Costs((ResourceIds.EnergyCell, 1)));
            AssertUnit(
                ArmyUnitCatalog.BioMechanicalBehemoth,
                "fusion.unit.bio-mechanical-behemoth",
                "bridge.building.bio-hangar",
                45f, 1, 60f, 380, 28,
                DamageType.Biological, ArmorType.Heavy, 1.1f,
                Costs(
                    (ResourceIds.BiologicalWeapon, 2),
                    (ResourceIds.MechanicalComponent, 2),
                    (ResourceIds.ActiveBiomass, 1)),
                Costs(
                    (ResourceIds.Biomass, 1),
                    (ResourceIds.EnergyCell, 1)));
        }

        [Test]
        public void FindUsesExactStableIdAndNeverAliasesUnknownInput()
        {
            Assert.That(ArmyUnitCatalog.Find(
                    ArmyUnitCatalog.PsionicMechId),
                Is.SameAs(ArmyUnitCatalog.PsionicMech));
            Assert.That(ArmyUnitCatalog.Find("PSIONIC-MECH"), Is.Null);
            Assert.That(ArmyUnitCatalog.Find(null), Is.Null);
            Assert.That(ArmyUnitCatalog.All.Select(value => value.Id),
                Is.Unique);
        }

        private static ResourceAmount[] Costs(
            params (string id, int amount)[] values)
        {
            return values.Select(value =>
                new ResourceAmount(value.id, value.amount)).ToArray();
        }

        private static void AssertUnit(
            ArmyUnitDefinition actual,
            string id,
            string sourceBuildingId,
            float manufactureSeconds,
            int capacity,
            float maintenanceSeconds,
            int hp,
            int damage,
            DamageType damageType,
            ArmorType armor,
            float speed,
            ResourceAmount[] costs,
            ResourceAmount[] maintenance)
        {
            Assert.That(actual.Id, Is.EqualTo(id));
            Assert.That(actual.SourceBuildingId, Is.EqualTo(sourceBuildingId));
            Assert.That(actual.ManufactureSeconds,
                Is.EqualTo(manufactureSeconds));
            Assert.That(actual.CapacityPerBuilding, Is.EqualTo(capacity));
            Assert.That(actual.MaintenanceSeconds,
                Is.EqualTo(maintenanceSeconds));
            Assert.That(actual.MaximumHealth, Is.EqualTo(hp));
            Assert.That(actual.Damage, Is.EqualTo(damage));
            Assert.That(actual.DamageType, Is.EqualTo(damageType));
            Assert.That(actual.Armor, Is.EqualTo(armor));
            Assert.That(actual.MoveSpeed, Is.EqualTo(speed));
            Assert.That(actual.ManufactureCosts, Is.EqualTo(costs));
            Assert.That(actual.MaintenanceCosts, Is.EqualTo(maintenance));
        }
    }
}
