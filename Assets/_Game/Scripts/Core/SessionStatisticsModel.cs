using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Core
{
    public sealed class SessionStatisticsMetric
    {
        public SessionStatisticsMetric(string stableId, int amount)
        {
            StableId = stableId;
            Amount = amount;
        }

        public string StableId { get; }
        public int Amount { get; }
    }

    public sealed class SessionStatisticsSnapshot
    {
        private readonly ReadOnlyCollection<SessionStatisticsMetric>
            killsByEnemyId;
        private readonly ReadOnlyCollection<SessionStatisticsMetric>
            damageByTowerBuildingId;
        private readonly ReadOnlyCollection<SessionStatisticsMetric>
            killsByTowerBuildingId;
        private readonly ReadOnlyCollection<SessionStatisticsMetric>
            consumablesSpentByResourceId;
        private readonly ReadOnlyCollection<SessionStatisticsMetric>
            buildingLossesByBuildingId;

        public SessionStatisticsSnapshot(
            float elapsedRuleSeconds,
            int completedWaveCount,
            int unattributedKillCount,
            IEnumerable<SessionStatisticsMetric> killsByEnemyId,
            IEnumerable<SessionStatisticsMetric> damageByTowerBuildingId,
            IEnumerable<SessionStatisticsMetric> killsByTowerBuildingId,
            IEnumerable<SessionStatisticsMetric> consumablesSpentByResourceId,
            int unattributedBuildingLossCount,
            IEnumerable<SessionStatisticsMetric> buildingLossesByBuildingId,
            int highestAliveEnemyCount,
            int completedProductionBatchCount,
            float productionActiveProgressSeconds,
            float productionEligibleSeconds,
            bool cityWasPackedAfterCampaignStart,
            bool developmentModifierUsed,
            bool partialFromMigration,
            bool isTerminal,
            float highestObservation,
            int rescues,
            int delayedRescues,
            bool retreatedDuringBoss)
        {
            ElapsedRuleSeconds = elapsedRuleSeconds;
            CompletedWaveCount = completedWaveCount;
            UnattributedKillCount = unattributedKillCount;
            this.killsByEnemyId = CopyMetrics(killsByEnemyId);
            this.damageByTowerBuildingId = CopyMetrics(
                damageByTowerBuildingId);
            this.killsByTowerBuildingId = CopyMetrics(killsByTowerBuildingId);
            this.consumablesSpentByResourceId = CopyMetrics(
                consumablesSpentByResourceId);
            UnattributedBuildingLossCount = unattributedBuildingLossCount;
            this.buildingLossesByBuildingId = CopyMetrics(
                buildingLossesByBuildingId);
            HighestAliveEnemyCount = highestAliveEnemyCount;
            CompletedProductionBatchCount = completedProductionBatchCount;
            ProductionActiveProgressSeconds = productionActiveProgressSeconds;
            ProductionEligibleSeconds = productionEligibleSeconds;
            CityWasPackedAfterCampaignStart = cityWasPackedAfterCampaignStart;
            DevelopmentModifierUsed = developmentModifierUsed;
            PartialFromMigration = partialFromMigration;
            IsTerminal = isTerminal;
            HighestObservation = highestObservation;
            Rescues = rescues;
            DelayedRescues = delayedRescues;
            RetreatedDuringBoss = retreatedDuringBoss;
        }

        public float ElapsedRuleSeconds { get; }
        public int CompletedWaveCount { get; }
        public int UnattributedKillCount { get; }
        public IReadOnlyList<SessionStatisticsMetric> KillsByEnemyId =>
            killsByEnemyId;
        public IReadOnlyList<SessionStatisticsMetric> DamageByTowerBuildingId =>
            damageByTowerBuildingId;
        public IReadOnlyList<SessionStatisticsMetric> KillsByTowerBuildingId =>
            killsByTowerBuildingId;
        public IReadOnlyList<SessionStatisticsMetric>
            ConsumablesSpentByResourceId => consumablesSpentByResourceId;
        public int UnattributedBuildingLossCount { get; }
        public IReadOnlyList<SessionStatisticsMetric> BuildingLossesByBuildingId =>
            buildingLossesByBuildingId;
        public int HighestAliveEnemyCount { get; }
        public int CompletedProductionBatchCount { get; }
        public float ProductionActiveProgressSeconds { get; }
        public float ProductionEligibleSeconds { get; }
        public float? ProductionEfficiency => ProductionEligibleSeconds > 0f
            ? ProductionActiveProgressSeconds / ProductionEligibleSeconds
            : (float?)null;
        public bool CityWasPackedAfterCampaignStart { get; }
        public bool DevelopmentModifierUsed { get; }
        public bool PartialFromMigration { get; }
        public bool IsTerminal { get; }
        public float HighestObservation { get; }
        public int Rescues { get; }
        public int DelayedRescues { get; }
        public bool RetreatedDuringBoss { get; }

        public int TotalKillCount =>
            UnattributedKillCount + Sum(killsByEnemyId);
        public int TotalBuildingLossCount =>
            UnattributedBuildingLossCount + Sum(buildingLossesByBuildingId);

        private static ReadOnlyCollection<SessionStatisticsMetric> CopyMetrics(
            IEnumerable<SessionStatisticsMetric> source)
        {
            var result = new List<SessionStatisticsMetric>();
            if (source != null)
            {
                foreach (SessionStatisticsMetric item in source)
                {
                    result.Add(item == null
                        ? null
                        : new SessionStatisticsMetric(
                            item.StableId,
                            item.Amount));
                }
            }
            result.Sort((left, right) => string.CompareOrdinal(
                left?.StableId,
                right?.StableId));
            return result.AsReadOnly();
        }

        private static int Sum(IReadOnlyList<SessionStatisticsMetric> values)
        {
            var total = 0;
            for (var index = 0; index < values.Count; index++)
                total += values[index]?.Amount ?? 0;
            return total;
        }
    }

    public sealed class SessionStatisticsModel
    {
        private readonly Dictionary<string, int> killsByEnemyId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> damageByTowerBuildingId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> killsByTowerBuildingId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> consumablesSpentByResourceId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> buildingLossesByBuildingId =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private float elapsedRuleSeconds;
        private int completedWaveCount;
        private int unattributedKillCount;
        private int unattributedBuildingLossCount;
        private int highestAliveEnemyCount;
        private int completedProductionBatchCount;
        private float productionActiveProgressSeconds;
        private float productionEligibleSeconds;
        private bool cityWasPackedAfterCampaignStart;
        private bool developmentModifierUsed;
        private bool partialFromMigration;
        private bool isTerminal;
        private float highestObservation;
        private int rescues;
        private int delayedRescues;
        private bool retreatedDuringBoss;

        // Legacy API aliases remain available for the frozen formal prototype.
        public float ElapsedSeconds => elapsedRuleSeconds;
        public int Kills => unattributedKillCount + Sum(killsByEnemyId);
        public float HighestObservation => highestObservation;
        public int ProductionCycles => completedProductionBatchCount;
        public int BuildingLosses =>
            unattributedBuildingLossCount + Sum(buildingLossesByBuildingId);
        public int Rescues => rescues;
        public int DelayedRescues => delayedRescues;
        public bool RetreatedDuringBoss => retreatedDuringBoss;
        public bool IsTerminal => isTerminal;

        public void Tick(float delta, float observation)
        {
            if (isTerminal) return;
            AdvanceRuleTime(delta);
            if (IsFiniteNonNegative(observation))
                highestObservation = Math.Max(highestObservation, observation);
        }

        public void AddKill()
        {
            if (!isTerminal) unattributedKillCount++;
        }

        public void AddProduction(int cycles)
        {
            RegisterCompletedProductionBatches(cycles);
        }

        public void AddBuildingLoss()
        {
            if (!isTerminal) unattributedBuildingLossCount++;
        }

        public void AddRescue(bool immediate)
        {
            if (isTerminal) return;
            rescues++;
            if (!immediate) delayedRescues++;
        }

        public void MarkRetreat()
        {
            if (!isTerminal) retreatedDuringBoss = true;
        }

        public void Restore(
            float elapsed,
            int kills,
            float highest,
            int production,
            int losses,
            int restoredRescues,
            int delayed,
            bool retreated)
        {
            elapsedRuleSeconds = FiniteOrZero(elapsed);
            unattributedKillCount = Math.Max(0, kills);
            highestObservation = FiniteOrZero(highest);
            completedProductionBatchCount = Math.Max(0, production);
            unattributedBuildingLossCount = Math.Max(0, losses);
            rescues = Math.Max(0, restoredRescues);
            delayedRescues = Math.Min(rescues, Math.Max(0, delayed));
            retreatedDuringBoss = retreated;
            completedWaveCount = 0;
            highestAliveEnemyCount = 0;
            productionActiveProgressSeconds = 0f;
            productionEligibleSeconds = 0f;
            cityWasPackedAfterCampaignStart = false;
            developmentModifierUsed = false;
            partialFromMigration = false;
            isTerminal = false;
            ClearMetrics();
        }

        public void AdvanceRuleTime(float deltaSeconds)
        {
            if (isTerminal || !IsFiniteNonNegative(deltaSeconds)) return;
            elapsedRuleSeconds += deltaSeconds;
        }

        public void RegisterCompletedWaves(int count)
        {
            if (isTerminal || count <= 0) return;
            completedWaveCount += count;
        }

        public void RegisterEnemyKill(
            string enemyId,
            string towerBuildingId)
        {
            if (isTerminal ||
                string.IsNullOrWhiteSpace(enemyId) ||
                string.IsNullOrWhiteSpace(towerBuildingId))
            {
                return;
            }
            Add(killsByEnemyId, enemyId, 1);
            Add(killsByTowerBuildingId, towerBuildingId, 1);
        }

        public void RegisterTowerDamage(
            string towerBuildingId,
            int actualDamage)
        {
            if (isTerminal ||
                string.IsNullOrWhiteSpace(towerBuildingId) ||
                actualDamage <= 0)
            {
                return;
            }
            Add(damageByTowerBuildingId, towerBuildingId, actualDamage);
        }

        public void RegisterConsumableSpent(string resourceId, int amount)
        {
            if (isTerminal ||
                string.IsNullOrWhiteSpace(resourceId) ||
                amount <= 0)
            {
                return;
            }
            Add(consumablesSpentByResourceId, resourceId, amount);
        }

        public void RegisterBuildingLoss(string buildingId, int count)
        {
            if (isTerminal ||
                string.IsNullOrWhiteSpace(buildingId) ||
                count <= 0)
            {
                return;
            }
            Add(buildingLossesByBuildingId, buildingId, count);
        }

        public void ObserveAliveEnemyCount(int aliveEnemyCount)
        {
            if (isTerminal || aliveEnemyCount < 0) return;
            highestAliveEnemyCount = Math.Max(
                highestAliveEnemyCount,
                aliveEnemyCount);
        }

        public void RegisterCompletedProductionBatches(int count)
        {
            if (isTerminal || count <= 0) return;
            completedProductionBatchCount += count;
        }

        public void RegisterProductionTime(
            float activeProgressSeconds,
            float eligibleSeconds)
        {
            if (isTerminal ||
                !IsFiniteNonNegative(activeProgressSeconds) ||
                !IsFiniteNonNegative(eligibleSeconds) ||
                activeProgressSeconds > eligibleSeconds)
            {
                return;
            }
            productionActiveProgressSeconds += activeProgressSeconds;
            productionEligibleSeconds += eligibleSeconds;
        }

        public void MarkCityPackedAfterCampaignStart()
        {
            if (!isTerminal) cityWasPackedAfterCampaignStart = true;
        }

        public void MarkDevelopmentModifierUsed()
        {
            if (!isTerminal) developmentModifierUsed = true;
        }

        public void MarkPartialFromMigration()
        {
            if (!isTerminal) partialFromMigration = true;
        }

        public void FreezeAtTerminal()
        {
            isTerminal = true;
        }

        public SessionStatisticsSnapshot Capture()
        {
            return new SessionStatisticsSnapshot(
                elapsedRuleSeconds,
                completedWaveCount,
                unattributedKillCount,
                Metrics(killsByEnemyId),
                Metrics(damageByTowerBuildingId),
                Metrics(killsByTowerBuildingId),
                Metrics(consumablesSpentByResourceId),
                unattributedBuildingLossCount,
                Metrics(buildingLossesByBuildingId),
                highestAliveEnemyCount,
                completedProductionBatchCount,
                productionActiveProgressSeconds,
                productionEligibleSeconds,
                cityWasPackedAfterCampaignStart,
                developmentModifierUsed,
                partialFromMigration,
                isTerminal,
                highestObservation,
                rescues,
                delayedRescues,
                retreatedDuringBoss);
        }

        public bool TryRestore(
            SessionStatisticsSnapshot snapshot,
            out string error)
        {
            if (snapshot == null)
            {
                error = "Session statistics snapshot is required.";
                return false;
            }
            if (!IsFiniteNonNegative(snapshot.ElapsedRuleSeconds) ||
                snapshot.CompletedWaveCount < 0 ||
                snapshot.UnattributedKillCount < 0 ||
                snapshot.UnattributedBuildingLossCount < 0 ||
                snapshot.HighestAliveEnemyCount < 0 ||
                snapshot.CompletedProductionBatchCount < 0 ||
                !IsFiniteNonNegative(
                    snapshot.ProductionActiveProgressSeconds) ||
                !IsFiniteNonNegative(snapshot.ProductionEligibleSeconds) ||
                snapshot.ProductionActiveProgressSeconds >
                    snapshot.ProductionEligibleSeconds ||
                !IsFiniteNonNegative(snapshot.HighestObservation) ||
                snapshot.Rescues < 0 ||
                snapshot.DelayedRescues < 0 ||
                snapshot.DelayedRescues > snapshot.Rescues)
            {
                error = "Session statistics snapshot contains invalid values.";
                return false;
            }

            if (!TryReadMetrics(snapshot.KillsByEnemyId,
                    out Dictionary<string, int> restoredEnemyKills,
                    out error) ||
                !TryReadMetrics(snapshot.DamageByTowerBuildingId,
                    out Dictionary<string, int> restoredTowerDamage,
                    out error) ||
                !TryReadMetrics(snapshot.KillsByTowerBuildingId,
                    out Dictionary<string, int> restoredTowerKills,
                    out error) ||
                !TryReadMetrics(snapshot.ConsumablesSpentByResourceId,
                    out Dictionary<string, int> restoredConsumables,
                    out error) ||
                !TryReadMetrics(snapshot.BuildingLossesByBuildingId,
                    out Dictionary<string, int> restoredBuildingLosses,
                    out error))
            {
                return false;
            }

            elapsedRuleSeconds = snapshot.ElapsedRuleSeconds;
            completedWaveCount = snapshot.CompletedWaveCount;
            unattributedKillCount = snapshot.UnattributedKillCount;
            unattributedBuildingLossCount =
                snapshot.UnattributedBuildingLossCount;
            highestAliveEnemyCount = snapshot.HighestAliveEnemyCount;
            completedProductionBatchCount =
                snapshot.CompletedProductionBatchCount;
            productionActiveProgressSeconds =
                snapshot.ProductionActiveProgressSeconds;
            productionEligibleSeconds = snapshot.ProductionEligibleSeconds;
            cityWasPackedAfterCampaignStart =
                snapshot.CityWasPackedAfterCampaignStart;
            developmentModifierUsed = snapshot.DevelopmentModifierUsed;
            partialFromMigration = snapshot.PartialFromMigration;
            isTerminal = snapshot.IsTerminal;
            highestObservation = snapshot.HighestObservation;
            rescues = snapshot.Rescues;
            delayedRescues = snapshot.DelayedRescues;
            retreatedDuringBoss = snapshot.RetreatedDuringBoss;
            Copy(restoredEnemyKills, killsByEnemyId);
            Copy(restoredTowerDamage, damageByTowerBuildingId);
            Copy(restoredTowerKills, killsByTowerBuildingId);
            Copy(restoredConsumables, consumablesSpentByResourceId);
            Copy(restoredBuildingLosses, buildingLossesByBuildingId);
            error = string.Empty;
            return true;
        }

        private void ClearMetrics()
        {
            killsByEnemyId.Clear();
            damageByTowerBuildingId.Clear();
            killsByTowerBuildingId.Clear();
            consumablesSpentByResourceId.Clear();
            buildingLossesByBuildingId.Clear();
        }

        private static SessionStatisticsMetric[] Metrics(
            IReadOnlyDictionary<string, int> source)
        {
            var keys = new List<string>(source.Keys);
            keys.Sort(StringComparer.Ordinal);
            var result = new SessionStatisticsMetric[keys.Count];
            for (var index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                result[index] = new SessionStatisticsMetric(key, source[key]);
            }
            return result;
        }

        private static bool TryReadMetrics(
            IReadOnlyList<SessionStatisticsMetric> source,
            out Dictionary<string, int> result,
            out string error)
        {
            result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null)
            {
                error = "Session statistics metrics are required.";
                return false;
            }
            for (var index = 0; index < source.Count; index++)
            {
                SessionStatisticsMetric item = source[index];
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.StableId) ||
                    item.Amount <= 0 ||
                    result.ContainsKey(item.StableId))
                {
                    error = "Session statistics metrics are invalid.";
                    return false;
                }
                result.Add(item.StableId, item.Amount);
            }
            error = string.Empty;
            return true;
        }

        private static void Add(
            IDictionary<string, int> destination,
            string stableId,
            int amount)
        {
            destination.TryGetValue(stableId, out int current);
            destination[stableId] = current + amount;
        }

        private static int Sum(IReadOnlyDictionary<string, int> values)
        {
            var total = 0;
            foreach (int amount in values.Values) total += amount;
            return total;
        }

        private static void Copy(
            IReadOnlyDictionary<string, int> source,
            IDictionary<string, int> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, int> pair in source)
                destination.Add(pair.Key, pair.Value);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private static float FiniteOrZero(float value)
        {
            return IsFiniteNonNegative(value) ? value : 0f;
        }
    }
}
