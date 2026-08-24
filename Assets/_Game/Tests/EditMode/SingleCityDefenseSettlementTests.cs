using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseSettlementTests
    {
        [Test]
        public void VictoryUsesAuthoritativeResultAndOnlyAllowsContinueSandbox()
        {
            SingleCityDefenseCampaignSnapshot campaign = Campaign(
                SingleCityDefenseCampaignResult.Victory,
                coreCurrentHealth: 0);
            var model = new SingleCityDefenseSettlementModel();

            Assert.That(model.TryPublish(
                101ul,
                campaign,
                Session(),
                out SingleCityDefenseSettlementSnapshot snapshot), Is.True);

            Assert.That(snapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory),
                "Settlement must trust the campaign result instead of " +
                "re-evaluating core health.");
            CollectionAssert.AreEqual(
                new[] { SingleCityDefenseSettlementAction.ContinueSandbox },
                snapshot.AvailableActions);
        }

        [Test]
        public void DefeatOnlyAllowsWaveCheckpointRetryOrReturnToTitle()
        {
            var model = new SingleCityDefenseSettlementModel();

            Assert.That(model.TryPublish(
                202ul,
                Campaign(SingleCityDefenseCampaignResult.Defeat, 2000),
                Session(),
                out SingleCityDefenseSettlementSnapshot snapshot), Is.True);

            CollectionAssert.AreEqual(new[]
            {
                SingleCityDefenseSettlementAction.RetryWaveCheckpoint,
                SingleCityDefenseSettlementAction.ReturnToTitle,
            }, snapshot.AvailableActions);
        }

        [Test]
        public void MetricsUseFormalCatalogOrderAndImmutableValues()
        {
            var enemyKills = new Dictionary<string, int>
            {
                [EnemyCatalog.Howler.Id.Value] = 7,
                [EnemyCatalog.Gnawer.Id.Value] = 11,
                [EnemyCatalog.CrystalBeast.Id.Value] = 5,
            };
            var towerDamage = new Dictionary<string, int>
            {
                [BuildingCatalog.SporeTower.Id.Value] = 18,
                [BuildingCatalog.MachineGunTurret.Id.Value] = 20,
                [BuildingCatalog.LaserTower.Id.Value] = 48,
            };
            var towerKills = new Dictionary<string, int>
            {
                [BuildingCatalog.SporeTower.Id.Value] = 3,
                [BuildingCatalog.MachineGunTurret.Id.Value] = 4,
                [BuildingCatalog.LaserTower.Id.Value] = 5,
            };
            var consumables = new Dictionary<string, int>
            {
                [ResourceIds.BiologicalWeapon] = 2,
                [ResourceIds.Ammunition] = 4,
                [ResourceIds.EnergyCrystal] = 3,
            };
            var model = new SingleCityDefenseSettlementModel();

            Assert.That(model.TryPublish(
                303ul,
                Campaign(
                    SingleCityDefenseCampaignResult.Victory,
                    1500,
                    enemyKills,
                    towerDamage,
                    towerKills,
                    consumables),
                Session(),
                out SingleCityDefenseSettlementSnapshot snapshot), Is.True);

            CollectionAssert.AreEqual(new[]
            {
                EnemyCatalog.Gnawer.Id.Value,
                EnemyCatalog.CrystalBeast.Id.Value,
                EnemyCatalog.Howler.Id.Value,
            }, snapshot.EnemyKills.Select(metric => metric.StableId));
            CollectionAssert.AreEqual(
                new[] { 11, 5, 7 },
                snapshot.EnemyKills.Select(metric => metric.Amount));
            CollectionAssert.AreEqual(new[]
            {
                BuildingCatalog.MachineGunTurret.Id.Value,
                BuildingCatalog.LaserTower.Id.Value,
                BuildingCatalog.SporeTower.Id.Value,
            }, snapshot.TowerDamage.Select(metric => metric.StableId));
            CollectionAssert.AreEqual(
                new[] { 20, 48, 18 },
                snapshot.TowerDamage.Select(metric => metric.Amount));
            CollectionAssert.AreEqual(
                new[] { 4, 5, 3 },
                snapshot.TowerKills.Select(metric => metric.Amount));
            CollectionAssert.AreEqual(new[]
            {
                ResourceIds.Ammunition,
                ResourceIds.EnergyCrystal,
                ResourceIds.BiologicalWeapon,
            }, snapshot.ConsumablesSpent.Select(metric => metric.StableId));

            enemyKills[EnemyCatalog.Gnawer.Id.Value] = 999;
            Assert.That(snapshot.EnemyKills[0].Amount, Is.EqualTo(11));
            Assert.That(snapshot.EnemyKills, Is.Not.InstanceOf<List<
                SingleCityDefenseSettlementMetric>>());
        }

        [Test]
        public void SessionFactsExposeMigrationModifierEfficiencyAndDefenseStyle()
        {
            var model = new SingleCityDefenseSettlementModel();

            Assert.That(model.TryPublish(
                404ul,
                Campaign(
                    SingleCityDefenseCampaignResult.Victory,
                    1600,
                    partialFromMigration: true),
                new SingleCityDefenseSettlementSessionStatistics(
                    completedProductionBatchCount: 12,
                    productionActiveProgressSeconds: 25f,
                    productionEligibleSeconds: 100f,
                    cityWasPackedAfterCampaignStart: true,
                    developerModifierUsed: true),
                out SingleCityDefenseSettlementSnapshot snapshot), Is.True);

            Assert.That(snapshot.PartialFromMigration, Is.True);
            Assert.That(snapshot.DeveloperModifierUsed, Is.True);
            Assert.That(snapshot.CompletedProductionBatchCount, Is.EqualTo(12));
            Assert.That(snapshot.ProductionActiveProgressSeconds,
                Is.EqualTo(25f));
            Assert.That(snapshot.ProductionEligibleSeconds,
                Is.EqualTo(100f));
            Assert.That(snapshot.HasProductionEfficiency, Is.True);
            Assert.That(snapshot.ProductionEfficiency,
                Is.EqualTo(.25f).Within(.0001f));
            Assert.That(snapshot.DefenseStyle,
                Is.EqualTo(SingleCityDefenseStyle.MobileDefense));
        }

        [Test]
        public void ZeroEligibleTimeReportsNoEfficiencyData()
        {
            var model = new SingleCityDefenseSettlementModel();

            Assert.That(model.TryPublish(
                505ul,
                Campaign(SingleCityDefenseCampaignResult.Defeat, 0),
                new SingleCityDefenseSettlementSessionStatistics(
                    3,
                    10f,
                    0f,
                    false,
                    false),
                out SingleCityDefenseSettlementSnapshot snapshot), Is.True);

            Assert.That(snapshot.HasProductionEfficiency, Is.False);
            Assert.That(snapshot.ProductionEfficiency, Is.Zero);
            Assert.That(snapshot.DefenseStyle,
                Is.EqualTo(SingleCityDefenseStyle.HoldFast));
        }

        [Test]
        public void SameTerminalRevisionPublishesOnlyOnceAndNonTerminalIsRejected()
        {
            var model = new SingleCityDefenseSettlementModel();
            SingleCityDefenseSettlementSessionStatistics session = Session();

            Assert.That(model.TryPublish(
                606ul,
                Campaign(SingleCityDefenseCampaignResult.None, 2000),
                session,
                out _), Is.False);
            Assert.That(model.TryPublish(
                606ul,
                Campaign(SingleCityDefenseCampaignResult.Victory, 1000),
                session,
                out SingleCityDefenseSettlementSnapshot first), Is.True);
            Assert.That(first.TerminalRevision, Is.EqualTo(606ul));
            Assert.That(model.TryPublish(
                606ul,
                Campaign(SingleCityDefenseCampaignResult.Victory, 1000),
                session,
                out _), Is.False);
            Assert.That(model.TryPublish(
                607ul,
                Campaign(SingleCityDefenseCampaignResult.Defeat, 0),
                session,
                out _), Is.True);
        }

        private static SingleCityDefenseSettlementSessionStatistics Session()
        {
            return new SingleCityDefenseSettlementSessionStatistics(
                0,
                0f,
                0f,
                false,
                false);
        }

        private static SingleCityDefenseCampaignSnapshot Campaign(
            SingleCityDefenseCampaignResult result,
            int coreCurrentHealth,
            IReadOnlyDictionary<string, int> enemyKills = null,
            IReadOnlyDictionary<string, int> towerDamage = null,
            IReadOnlyDictionary<string, int> towerKills = null,
            IReadOnlyDictionary<string, int> consumables = null,
            bool partialFromMigration = false)
        {
            var statistics = new SingleCityDefenseCampaignStatisticsSnapshot(
                elapsedRuleSeconds: 125f,
                completedWaveCount: result ==
                    SingleCityDefenseCampaignResult.Victory ? 10 : 4,
                totalKillCount: 23,
                killsByEnemyId: enemyKills,
                damageByTowerBuildingId: towerDamage,
                killsByTowerBuildingId: towerKills,
                consumablesSpentByResourceId: consumables,
                buildingLossCount: 2,
                coreCurrentHealth: coreCurrentHealth,
                coreMaximumHealth: 2000,
                highestAliveEnemyCount: 17,
                partialFromMigration: partialFromMigration);
            return new SingleCityDefenseCampaignSnapshot(
                currentWaveNumber: result ==
                    SingleCityDefenseCampaignResult.Victory ? 10 : 5,
                phase: result == SingleCityDefenseCampaignResult.Victory
                    ? SingleCityDefenseCampaignPhase.Victory
                    : result == SingleCityDefenseCampaignResult.Defeat
                        ? SingleCityDefenseCampaignPhase.Defeat
                        : SingleCityDefenseCampaignPhase.CombatCleanup,
                warningRemainingSeconds: 0f,
                plannedEnemyCount: 0,
                spawnedEnemyCount: 0,
                aliveEnemyCount: 0,
                coreCurrentHealth: coreCurrentHealth,
                coreMaximumHealth: 2000,
                result: result,
                enemies: null,
                statistics: statistics);
        }
    }
}
