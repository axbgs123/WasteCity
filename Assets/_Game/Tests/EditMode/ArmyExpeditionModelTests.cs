using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ArmyExpeditionModelTests
    {
        [Test]
        public void IDEA0022_SameSessionTargetAndOrdinalResolveIdentically()
        {
            ArmyExpeditionUnit[] units = StrongSquad();
            ArmyExpeditionModel first = Start(
                "session.0022", 22, 17, 4, 12f, units, true);
            ArmyExpeditionModel second = Start(
                "session.0022", 22, 17, 4, 12f, units, true);

            first.Tick(first.OutboundDurationSeconds, false);
            second.Tick(second.OutboundDurationSeconds, false);

            Assert.That(first.Encounter.EnemyDefinitionIds,
                Is.EqualTo(second.Encounter.EnemyDefinitionIds));
            Assert.That(first.Resolution.Victory,
                Is.EqualTo(second.Resolution.Victory));
            Assert.That(first.Resolution.CasualtyStableUnitIds,
                Is.EqualTo(second.Resolution.CasualtyStableUnitIds));
            Assert.That(first.PendingLoot, Is.EqualTo(second.PendingLoot));
        }

        [Test]
        public void EncounterUsesExistingEnemiesAndApprovedLootRanges()
        {
            ArmyExpeditionModel model = Start(
                "session.loot", 33, 12, 8, 6f, StrongSquad(), true);

            model.Tick(model.OutboundDurationSeconds, false);

            Assert.That(model.Status,
                Is.EqualTo(ArmyExpeditionStatus.Returning));
            Assert.That(model.Resolution.Victory, Is.True);
            Assert.That(model.Encounter.EnemyDefinitionIds,
                Is.Not.Empty);
            foreach (string enemyId in model.Encounter.EnemyDefinitionIds)
                Assert.That(EnemyCatalog.All.Any(
                    definition => definition.Id.Value == enemyId), Is.True);
            AssertLoot(model.PendingLoot, ResourceIds.Alloy, 10, 24);
            AssertLoot(model.PendingLoot, ResourceIds.Biomass, 8, 20);
            AssertLoot(model.PendingLoot, ResourceIds.EnergyCrystal, 4, 12);
        }

        [Test]
        public void LeaderProvidesExactOnePointTwoExpeditionPower()
        {
            ArmyExpeditionUnit[] units = StrongSquad();
            ArmyExpeditionModel withoutLeader = Start(
                "session.leader", 4, 9, 2, 2f, units, false);
            ArmyExpeditionModel withLeader = Start(
                "session.leader", 4, 9, 2, 2f, units, true);

            withoutLeader.Tick(withoutLeader.OutboundDurationSeconds, false);
            withLeader.Tick(withLeader.OutboundDurationSeconds, false);

            Assert.That(withLeader.Resolution.ArmyPower,
                Is.EqualTo(withoutLeader.Resolution.ArmyPower * 1.2f)
                    .Within(.001f));
        }

        [Test]
        public void LootCannotBeClaimedUntilReturnAndIsClaimedOnce()
        {
            ArmyExpeditionModel model = Start(
                "session.return", 10, 10, 1, 10f, StrongSquad(), true);
            model.Tick(model.OutboundDurationSeconds, false);
            Assert.That(model.TryClaimReturnedLoot(out _), Is.False);

            model.Tick(model.ReturnDurationSeconds, false);

            Assert.That(model.Status,
                Is.EqualTo(ArmyExpeditionStatus.Returned));
            Assert.That(model.TryClaimReturnedLoot(
                out ResourceAmount[] loot), Is.True);
            Assert.That(loot, Is.Not.Empty);
            Assert.That(model.Status,
                Is.EqualTo(ArmyExpeditionStatus.Completed));
            Assert.That(model.TryClaimReturnedLoot(out _), Is.False);
        }

        [Test]
        public void ReturnedLootDepositsAtomicallyAndWaitsForCapacity()
        {
            ArmyExpeditionModel model = Start(
                "session.deposit", 11, 14, 7, 3f, StrongSquad(), true);
            model.Tick(
                model.OutboundDurationSeconds + model.ReturnDurationSeconds,
                false);
            using var full = new CityResourceStorageModel(
                new ResourceInventory(150));
            full.AddToNetwork(ResourceIds.Alloy, 150);
            using var destination = new CityResourceStorageModel(
                new ResourceInventory(150));

            Assert.That(model.TryDepositReturnedLoot(full), Is.False);
            Assert.That(model.Status,
                Is.EqualTo(ArmyExpeditionStatus.Returned));
            ResourceAmount[] expected = model.PendingLoot.ToArray();
            Assert.That(model.TryDepositReturnedLoot(destination), Is.True);

            Assert.That(model.Status,
                Is.EqualTo(ArmyExpeditionStatus.Completed));
            foreach (ResourceAmount amount in expected)
            {
                Assert.That(destination.GetNetworkAmount(amount.ResourceId),
                    Is.EqualTo(amount.Amount));
            }
        }

        [Test]
        public void RetreatPreservesUnitsAndDiscardsPendingLoot()
        {
            ArmyExpeditionUnit[] units = StrongSquad();
            ArmyExpeditionModel model = Start(
                "session.retreat", 10, 10, 3, 4f, units, true);
            model.Tick(model.OutboundDurationSeconds, false);
            Assert.That(model.PendingLoot, Is.Not.Empty);
            string[] casualties = model.Resolution.CasualtyStableUnitIds;

            Assert.That(model.Retreat(), Is.True);

            Assert.That(model.PendingLoot, Is.Empty);
            Assert.That(model.Resolution.CasualtyStableUnitIds,
                Is.EqualTo(casualties),
                "Retreat must not invent additional casualties.");
            model.Tick(model.ReturnDurationSeconds, false);
            Assert.That(model.Status,
                Is.EqualTo(ArmyExpeditionStatus.Retreated));
            Assert.That(model.TryClaimReturnedLoot(out _), Is.False);
        }

        [Test]
        public void PauseAndSplitTicksAreDeterministic()
        {
            ArmyExpeditionUnit[] units = StrongSquad();
            ArmyExpeditionModel whole = Start(
                "session.tick", 2, 3, 5, 8f, units, true);
            ArmyExpeditionModel split = Start(
                "session.tick", 2, 3, 5, 8f, units, true);

            whole.Tick(whole.OutboundDurationSeconds, true);
            Assert.That(whole.RemainingSeconds,
                Is.EqualTo(whole.OutboundDurationSeconds));
            whole.Tick(whole.OutboundDurationSeconds, false);
            split.Tick(split.OutboundDurationSeconds * .4f, false);
            split.Tick(split.OutboundDurationSeconds * .6f, false);

            Assert.That(split.Status, Is.EqualTo(whole.Status));
            Assert.That(split.Encounter.EnemyDefinitionIds,
                Is.EqualTo(whole.Encounter.EnemyDefinitionIds));
            Assert.That(split.PendingLoot, Is.EqualTo(whole.PendingLoot));
        }

        private static ArmyExpeditionModel Start(
            string sessionId,
            int targetX,
            int targetY,
            int ordinal,
            float pathCost,
            IReadOnlyList<ArmyExpeditionUnit> units,
            bool leaderHealthy)
        {
            var model = new ArmyExpeditionModel();
            Assert.That(model.TryStart(
                sessionId,
                targetX,
                targetY,
                ordinal,
                pathCost,
                units,
                leaderHealthy), Is.True);
            Assert.That(model.OutboundDurationSeconds,
                Is.EqualTo(45f + pathCost * 1.5f));
            return model;
        }

        private static ArmyExpeditionUnit[] StrongSquad()
        {
            var result = new ArmyExpeditionUnit[12];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new ArmyExpeditionUnit(
                    "core.army-unit." + (index + 1).ToString("D6"),
                    ArmyUnitCatalog.BioMechanicalBehemothId,
                    ArmyUnitCatalog.BioMechanicalBehemoth.MaximumHealth,
                    true);
            }
            return result;
        }

        private static void AssertLoot(
            IReadOnlyList<ResourceAmount> loot,
            string resourceId,
            int minimum,
            int maximum)
        {
            ResourceAmount amount = loot.Single(
                value => value.ResourceId == resourceId);
            Assert.That(amount.Amount, Is.InRange(minimum, maximum));
        }
    }
}
