using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Defense
{
    public enum SingleCityDefenseSettlementAction
    {
        ContinueSandbox,
        RetryWaveCheckpoint,
        ReturnToTitle,
    }

    public enum SingleCityDefenseStyle
    {
        HoldFast,
        MobileDefense,
    }

    public sealed class SingleCityDefenseSettlementMetric
    {
        public SingleCityDefenseSettlementMetric(string stableId, int amount)
        {
            StableId = stableId ?? string.Empty;
            Amount = Math.Max(0, amount);
        }

        public string StableId { get; }
        public int Amount { get; }
    }

    public sealed class SingleCityDefenseSettlementSessionStatistics
    {
        public SingleCityDefenseSettlementSessionStatistics(
            int completedProductionBatchCount,
            float productionActiveProgressSeconds,
            float productionEligibleSeconds,
            bool cityWasPackedAfterCampaignStart,
            bool developerModifierUsed)
        {
            CompletedProductionBatchCount = Math.Max(
                0,
                completedProductionBatchCount);
            ProductionActiveProgressSeconds = Math.Max(
                0f,
                productionActiveProgressSeconds);
            ProductionEligibleSeconds = Math.Max(
                0f,
                productionEligibleSeconds);
            CityWasPackedAfterCampaignStart =
                cityWasPackedAfterCampaignStart;
            DeveloperModifierUsed = developerModifierUsed;
        }

        public int CompletedProductionBatchCount { get; }
        public float ProductionActiveProgressSeconds { get; }
        public float ProductionEligibleSeconds { get; }
        public bool CityWasPackedAfterCampaignStart { get; }
        public bool DeveloperModifierUsed { get; }
    }

    public sealed class SingleCityDefenseSettlementSnapshot
    {
        internal SingleCityDefenseSettlementSnapshot(
            ulong terminalRevision,
            SingleCityDefenseCampaignSnapshot campaign,
            SingleCityDefenseSettlementSessionStatistics session)
        {
            TerminalRevision = terminalRevision;
            Result = campaign.Result;
            CampaignStatistics = campaign.Statistics;
            SessionStatistics = session;
            ElapsedRuleSeconds = campaign.Statistics.ElapsedRuleSeconds;
            CompletedWaveCount = campaign.Statistics.CompletedWaveCount;
            TotalKillCount = campaign.Statistics.TotalKillCount;
            HighestAliveEnemyCount =
                campaign.Statistics.HighestAliveEnemyCount;
            BuildingLossCount = campaign.Statistics.BuildingLossCount;
            CoreCurrentHealth = campaign.Statistics.CoreCurrentHealth;
            CoreMaximumHealth = campaign.Statistics.CoreMaximumHealth;
            PartialFromMigration =
                campaign.Statistics.PartialFromMigration;
            DeveloperModifierUsed = session.DeveloperModifierUsed;
            CompletedProductionBatchCount =
                session.CompletedProductionBatchCount;
            ProductionActiveProgressSeconds =
                session.ProductionActiveProgressSeconds;
            ProductionEligibleSeconds = session.ProductionEligibleSeconds;
            HasProductionEfficiency = ProductionEligibleSeconds > 0f;
            ProductionEfficiency = HasProductionEfficiency
                ? ProductionActiveProgressSeconds /
                  ProductionEligibleSeconds
                : 0f;
            DefenseStyle = session.CityWasPackedAfterCampaignStart
                ? SingleCityDefenseStyle.MobileDefense
                : SingleCityDefenseStyle.HoldFast;

            EnemyKills = BuildMetrics(
                FormalEnemyIds,
                campaign.Statistics.KillsByEnemyId);
            TowerDamage = BuildMetrics(
                FormalTowerIds,
                campaign.Statistics.DamageByTowerBuildingId);
            TowerKills = BuildMetrics(
                FormalTowerIds,
                campaign.Statistics.KillsByTowerBuildingId);
            ConsumablesSpent = BuildMetrics(
                FormalConsumableIds,
                campaign.Statistics.ConsumablesSpentByResourceId);
            AvailableActions = campaign.Result ==
                SingleCityDefenseCampaignResult.Victory
                    ? VictoryActions
                    : DefeatActions;
        }

        private static readonly string[] FormalEnemyIds =
        {
            EnemyCatalog.Gnawer.Id.Value,
            EnemyCatalog.CrystalBeast.Id.Value,
            EnemyCatalog.Howler.Id.Value,
        };

        private static readonly string[] FormalTowerIds =
        {
            BuildingCatalog.MachineGunTurret.Id.Value,
            BuildingCatalog.LaserTower.Id.Value,
            BuildingCatalog.SporeTower.Id.Value,
        };

        private static readonly string[] FormalConsumableIds =
        {
            ResourceIds.Ammunition,
            ResourceIds.EnergyCrystal,
            ResourceIds.BiologicalWeapon,
        };

        private static readonly IReadOnlyList<
            SingleCityDefenseSettlementAction> VictoryActions =
                Array.AsReadOnly(new[]
                {
                    SingleCityDefenseSettlementAction.ContinueSandbox,
                });

        private static readonly IReadOnlyList<
            SingleCityDefenseSettlementAction> DefeatActions =
                Array.AsReadOnly(new[]
                {
                    SingleCityDefenseSettlementAction.RetryWaveCheckpoint,
                    SingleCityDefenseSettlementAction.ReturnToTitle,
                });

        public ulong TerminalRevision { get; }
        public SingleCityDefenseCampaignResult Result { get; }
        public SingleCityDefenseCampaignStatisticsSnapshot CampaignStatistics
        {
            get;
        }
        public SingleCityDefenseSettlementSessionStatistics SessionStatistics
        {
            get;
        }
        public float ElapsedRuleSeconds { get; }
        public int CompletedWaveCount { get; }
        public int TotalKillCount { get; }
        public int HighestAliveEnemyCount { get; }
        public int BuildingLossCount { get; }
        public int CoreCurrentHealth { get; }
        public int CoreMaximumHealth { get; }
        public bool PartialFromMigration { get; }
        public bool DeveloperModifierUsed { get; }
        public int CompletedProductionBatchCount { get; }
        public float ProductionActiveProgressSeconds { get; }
        public float ProductionEligibleSeconds { get; }
        public bool HasProductionEfficiency { get; }
        public float ProductionEfficiency { get; }
        public SingleCityDefenseStyle DefenseStyle { get; }
        public IReadOnlyList<SingleCityDefenseSettlementMetric> EnemyKills
        {
            get;
        }
        public IReadOnlyList<SingleCityDefenseSettlementMetric> TowerDamage
        {
            get;
        }
        public IReadOnlyList<SingleCityDefenseSettlementMetric> TowerKills
        {
            get;
        }
        public IReadOnlyList<SingleCityDefenseSettlementMetric>
            ConsumablesSpent { get; }
        public IReadOnlyList<SingleCityDefenseSettlementAction>
            AvailableActions { get; }

        private static IReadOnlyList<SingleCityDefenseSettlementMetric>
            BuildMetrics(
                IReadOnlyList<string> orderedStableIds,
                IReadOnlyDictionary<string, int> source)
        {
            var values = new SingleCityDefenseSettlementMetric[
                orderedStableIds.Count];
            for (var index = 0; index < orderedStableIds.Count; index++)
            {
                string stableId = orderedStableIds[index];
                int amount = 0;
                source?.TryGetValue(stableId, out amount);
                values[index] = new SingleCityDefenseSettlementMetric(
                    stableId,
                    amount);
            }
            return new ReadOnlyCollection<
                SingleCityDefenseSettlementMetric>(values);
        }
    }

    public sealed class SingleCityDefenseSettlementModel
    {
        private bool hasPublishedTerminalRevision;
        private ulong lastPublishedTerminalRevision;

        public bool TryPublish(
            ulong terminalRevision,
            SingleCityDefenseCampaignSnapshot campaign,
            SingleCityDefenseSettlementSessionStatistics session,
            out SingleCityDefenseSettlementSnapshot snapshot)
        {
            snapshot = null;
            if (campaign == null || session == null ||
                campaign.Result == SingleCityDefenseCampaignResult.None ||
                hasPublishedTerminalRevision &&
                terminalRevision == lastPublishedTerminalRevision)
            {
                return false;
            }

            snapshot = new SingleCityDefenseSettlementSnapshot(
                terminalRevision,
                campaign,
                session);
            lastPublishedTerminalRevision = terminalRevision;
            hasPublishedTerminalRevision = true;
            return true;
        }
    }
}
