using System.Linq;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class SingleCityArmyModelTests
    {
        [Test]
        public void IDEA0022_DefaultSquadManufacturesStableUnitsAtomically()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.Alloy, 1),
                (ResourceIds.SpiritIron, 1));
            var model = new SingleCityArmyModel();

            Assert.That(model.DefaultSquad.StableId,
                Is.EqualTo(SingleCityArmyModel.DefaultSquadId));
            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f,
                operationalSourceBuildings: 1,
                globallyPaused: false,
                storage), Is.EqualTo(1));

            Assert.That(model.Units.Count, Is.EqualTo(1));
            Assert.That(model.Units[0].StableId,
                Is.EqualTo("core.army-unit.000001"));
            Assert.That(model.Units[0].SquadId,
                Is.EqualTo(SingleCityArmyModel.DefaultSquadId));
            Assert.That(model.Units[0].DefinitionId,
                Is.EqualTo(ArmyUnitCatalog.CombatPuppetId));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Alloy), Is.Zero);
            Assert.That(storage.GetNetworkAmount(ResourceIds.SpiritIron),
                Is.Zero);
        }

        [Test]
        public void MissingOneInputKeepsReadyProgressAndDoesNotPartiallySpend()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.Alloy, 1));
            var model = new SingleCityArmyModel();

            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f, 1, false, storage), Is.Zero);

            Assert.That(model.ManufacturingProgress(
                ArmyUnitCatalog.CombatPuppetId), Is.EqualTo(20f));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Alloy),
                Is.EqualTo(1));
            Assert.That(model.Units, Is.Empty);
        }

        [Test]
        public void BuildingAndSquadCapacityStopProgressAndSpending()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.Alloy, 20),
                (ResourceIds.SpiritIron, 20));
            var model = new SingleCityArmyModel();
            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                60f, 1, false, storage), Is.EqualTo(3));
            int alloy = storage.GetNetworkAmount(ResourceIds.Alloy);

            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                100f, 1, false, storage), Is.Zero);
            Assert.That(model.UnitCount(
                ArmyUnitCatalog.CombatPuppetId), Is.EqualTo(3));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Alloy),
                Is.EqualTo(alloy));
            Assert.That(model.ManufacturingProgress(
                ArmyUnitCatalog.CombatPuppetId), Is.Zero);
        }

        [Test]
        public void PausedManufacturingAndMaintenanceDoNotAdvance()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.Alloy, 1),
                (ResourceIds.SpiritIron, 1),
                (ResourceIds.EnergyCrystal, 1));
            var model = new SingleCityArmyModel();
            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f, 1, true, storage), Is.Zero);
            Assert.That(model.ManufacturingProgress(
                ArmyUnitCatalog.CombatPuppetId), Is.Zero);
            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f, 1, false, storage), Is.EqualTo(1));

            model.TickMaintenance(60f, true, storage);
            Assert.That(model.Units[0].MaintenanceElapsed, Is.Zero);
            Assert.That(storage.GetNetworkAmount(ResourceIds.EnergyCrystal),
                Is.EqualTo(1));
        }

        [Test]
        public void MaintenanceFailureSleepsUnitAndAtomicRestockWakesIt()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.BiologicalWeapon, 2),
                (ResourceIds.MechanicalComponent, 2),
                (ResourceIds.ActiveBiomass, 1),
                (ResourceIds.Biomass, 1));
            var model = new SingleCityArmyModel();
            Assert.That(model.TickManufacturing(
                ArmyUnitCatalog.BioMechanicalBehemothId,
                45f, 1, false, storage), Is.EqualTo(1));

            Assert.That(model.TickMaintenance(60f, false, storage), Is.Zero);
            Assert.That(model.Units[0].IsActive, Is.False);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Biomass),
                Is.EqualTo(1), "Two-resource maintenance must be atomic.");

            Assert.That(storage.AddToNetwork(ResourceIds.EnergyCell, 1),
                Is.EqualTo(1));
            Assert.That(model.TickMaintenance(0f, false, storage),
                Is.EqualTo(1));
            Assert.That(model.Units[0].IsActive, Is.True);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Biomass), Is.Zero);
            Assert.That(storage.GetNetworkAmount(ResourceIds.EnergyCell),
                Is.Zero);
        }

        [Test]
        public void CommandsAndHealthyLeaderMultiplierBelongToDefaultSquad()
        {
            var model = new SingleCityArmyModel();

            model.Commands.SetRally(4f, 7f);
            Assert.That(model.Commands.Command,
                Is.EqualTo(FriendlySquadCommandType.Rally));
            model.Commands.Guard();
            Assert.That(model.Commands.Command,
                Is.EqualTo(FriendlySquadCommandType.Guard));
            model.Commands.FollowLeader();
            Assert.That(model.Commands.Command,
                Is.EqualTo(FriendlySquadCommandType.FollowLeader));
            Assert.That(model.Commands.TryExpedition(12, 9, true, true),
                Is.True);
            Assert.That(model.Commands.Command,
                Is.EqualTo(FriendlySquadCommandType.Expedition));
            Assert.That(model.Commands.TryExpedition(12, 9, false, true),
                Is.False);
            model.Commands.Retreat();
            Assert.That(model.Commands.Command,
                Is.EqualTo(FriendlySquadCommandType.Retreat));

            model.SetLeaderAssignment(true, leaderHealthy: true);
            Assert.That(model.ResolveSquadDamageMultiplier(),
                Is.EqualTo(1.2f));
            model.SetLeaderAssignment(true, leaderHealthy: false);
            Assert.That(model.ResolveSquadDamageMultiplier(), Is.EqualTo(1f));
        }

        [Test]
        public void DamageUsesMatrixAndDeathRecordsTypedLoss()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.Alloy, 1),
                (ResourceIds.SpiritIron, 1));
            var model = new SingleCityArmyModel();
            model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f, 1, false, storage);
            string unitId = model.Units.Single().StableId;

            Assert.That(model.ApplyDamage(
                unitId, 200, DamageType.Physical), Is.EqualTo(100));
            Assert.That(model.Units, Is.Empty);
            Assert.That(model.Commands.PuppetLosses, Is.EqualTo(1));
            Assert.That(model.LossCount(
                ArmyUnitCatalog.CombatPuppetId), Is.EqualTo(1));
        }

        [Test]
        public void ExpeditionCasualtiesRemoveExactUnitsAndKeepTypedLosses()
        {
            using CityResourceStorageModel storage = Stock(
                (ResourceIds.Alloy, 1),
                (ResourceIds.SpiritIron, 1),
                (ResourceIds.BiologicalWeapon, 2),
                (ResourceIds.MechanicalComponent, 2),
                (ResourceIds.ActiveBiomass, 1));
            var model = new SingleCityArmyModel();
            model.TickManufacturing(
                ArmyUnitCatalog.CombatPuppetId,
                20f, 1, false, storage);
            model.TickManufacturing(
                ArmyUnitCatalog.BioMechanicalBehemothId,
                45f, 1, false, storage);
            string[] casualties = model.Units
                .Select(value => value.StableId)
                .ToArray();

            Assert.That(model.ApplyExpeditionCasualties(casualties),
                Is.EqualTo(2));

            Assert.That(model.Units, Is.Empty);
            Assert.That(model.LossCount(
                ArmyUnitCatalog.CombatPuppetId), Is.EqualTo(1));
            Assert.That(model.LossCount(
                ArmyUnitCatalog.BioMechanicalBehemothId), Is.EqualTo(1));
        }

        private static CityResourceStorageModel Stock(
            params (string id, int amount)[] values)
        {
            var storage = new CityResourceStorageModel(
                new ResourceInventory(150));
            foreach ((string id, int amount) in values)
                Assert.That(storage.AddToNetwork(id, amount),
                    Is.EqualTo(amount));
            return storage;
        }
    }
}
