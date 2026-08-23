using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Combat;

namespace WasteCity.Defense
{
    public sealed class SingleCityDefenseCampaignEnemyCountPersistenceState
    {
        public SingleCityDefenseCampaignEnemyCountPersistenceState(
            string enemyDefinitionId,
            int count)
        {
            EnemyDefinitionId = enemyDefinitionId;
            Count = count;
        }

        public string EnemyDefinitionId { get; }
        public int Count { get; }
    }

    public sealed class SingleCityDefenseCampaignSpawnAnchorPersistenceState
    {
        public SingleCityDefenseCampaignSpawnAnchorPersistenceState(
            CampaignSpawnDirection direction,
            float x,
            float z)
        {
            Direction = direction;
            X = x;
            Z = z;
        }

        public CampaignSpawnDirection Direction { get; }
        public float X { get; }
        public float Z { get; }
    }

    public sealed class SingleCityDefenseCampaignMetricPersistenceState
    {
        public SingleCityDefenseCampaignMetricPersistenceState(
            string stableId,
            int amount)
        {
            StableId = stableId;
            Amount = amount;
        }

        public string StableId { get; }
        public int Amount { get; }
    }

    public sealed class SingleCityDefenseCampaignEnemyPersistenceState
    {
        public SingleCityDefenseCampaignEnemyPersistenceState(
            string stableId,
            string enemyDefinitionId,
            int spawnOrder,
            float x,
            float z,
            int currentHealth,
            float movementRemainder,
            float attackDamageRemainder,
            string targetStableId)
        {
            StableId = stableId;
            EnemyDefinitionId = enemyDefinitionId;
            SpawnOrder = spawnOrder;
            X = x;
            Z = z;
            CurrentHealth = currentHealth;
            MovementRemainder = movementRemainder;
            AttackDamageRemainder = attackDamageRemainder;
            TargetStableId = targetStableId;
        }

        public string StableId { get; }
        public string EnemyDefinitionId { get; }
        public int SpawnOrder { get; }
        public float X { get; }
        public float Z { get; }
        public int CurrentHealth { get; }
        public float MovementRemainder { get; }
        public float AttackDamageRemainder { get; }
        public string TargetStableId { get; }
    }

    public sealed class SingleCityDefenseCampaignStatisticsPersistenceState
    {
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignMetricPersistenceState> killsByEnemyId;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignMetricPersistenceState>
            damageByTowerBuildingId;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignMetricPersistenceState>
            killsByTowerBuildingId;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignMetricPersistenceState>
            consumablesSpentByResourceId;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignMetricPersistenceState>
            buildingLossesByBuildingId;

        public SingleCityDefenseCampaignStatisticsPersistenceState(
            float elapsedRuleSeconds,
            int spawnedEnemyCount,
            int defeatedEnemyCount,
            int completedWaveCount,
            IEnumerable<SingleCityDefenseCampaignMetricPersistenceState>
                killsByEnemyId,
            int highestAliveEnemyCount,
            int coreDamageTaken,
            IEnumerable<SingleCityDefenseCampaignMetricPersistenceState>
                damageByTowerBuildingId,
            IEnumerable<SingleCityDefenseCampaignMetricPersistenceState>
                killsByTowerBuildingId,
            IEnumerable<SingleCityDefenseCampaignMetricPersistenceState>
                consumablesSpentByResourceId,
            int buildingLossCount,
            IEnumerable<SingleCityDefenseCampaignMetricPersistenceState>
                buildingLossesByBuildingId = null)
        {
            ElapsedRuleSeconds = elapsedRuleSeconds;
            SpawnedEnemyCount = spawnedEnemyCount;
            DefeatedEnemyCount = defeatedEnemyCount;
            CompletedWaveCount = completedWaveCount;
            HighestAliveEnemyCount = highestAliveEnemyCount;
            CoreDamageTaken = coreDamageTaken;
            BuildingLossCount = buildingLossCount;
            this.killsByEnemyId = CopyMetrics(killsByEnemyId);
            this.damageByTowerBuildingId = CopyMetrics(
                damageByTowerBuildingId);
            this.killsByTowerBuildingId = CopyMetrics(killsByTowerBuildingId);
            this.consumablesSpentByResourceId = CopyMetrics(
                consumablesSpentByResourceId);
            this.buildingLossesByBuildingId = CopyMetrics(
                buildingLossesByBuildingId);
        }

        public float ElapsedRuleSeconds { get; }
        public int SpawnedEnemyCount { get; }
        public int DefeatedEnemyCount { get; }
        public int CompletedWaveCount { get; }
        public IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState>
            KillsByEnemyId => killsByEnemyId;
        public int HighestAliveEnemyCount { get; }
        public int CoreDamageTaken { get; }
        public IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState>
            DamageByTowerBuildingId => damageByTowerBuildingId;
        public IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState>
            KillsByTowerBuildingId => killsByTowerBuildingId;
        public IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState>
            ConsumablesSpentByResourceId => consumablesSpentByResourceId;
        public IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState>
            BuildingLossesByBuildingId => buildingLossesByBuildingId;
        public int BuildingLossCount { get; }

        private static ReadOnlyCollection<
            SingleCityDefenseCampaignMetricPersistenceState> CopyMetrics(
                IEnumerable<SingleCityDefenseCampaignMetricPersistenceState>
                    source)
        {
            var result = new List<
                SingleCityDefenseCampaignMetricPersistenceState>();
            if (source != null)
            {
                foreach (SingleCityDefenseCampaignMetricPersistenceState item
                         in source)
                {
                    result.Add(item == null
                        ? null
                        : new SingleCityDefenseCampaignMetricPersistenceState(
                            item.StableId,
                            item.Amount));
                }
            }
            result.Sort((left, right) => string.CompareOrdinal(
                left?.StableId,
                right?.StableId));
            return result.AsReadOnly();
        }
    }

    public sealed class SingleCityDefenseCampaignPersistenceState
    {
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignEnemyCountPersistenceState>
            plannedEnemyCountsByEnemyId;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignEnemyCountPersistenceState>
            spawnedEnemyCountsByEnemyId;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignEnemyCountPersistenceState>
            defeatedEnemyCountsByEnemyId;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignSpawnAnchorPersistenceState>
            frozenSpawnAnchors;
        private readonly ReadOnlyCollection<
            SingleCityDefenseCampaignEnemyPersistenceState> enemies;

        public SingleCityDefenseCampaignPersistenceState(
            string campaignId,
            SingleCityDefenseCampaignPhase phase,
            int currentWaveNumber,
            float warningRemainingSeconds,
            float spawnClockSeconds,
            float fixedStepAccumulatorSeconds,
            int nextEnemyOrdinal,
            int coreCurrentHealth,
            SingleCityDefenseCampaignResult result,
            IEnumerable<SingleCityDefenseCampaignEnemyCountPersistenceState>
                plannedEnemyCountsByEnemyId,
            IEnumerable<SingleCityDefenseCampaignEnemyCountPersistenceState>
                spawnedEnemyCountsByEnemyId,
            IEnumerable<SingleCityDefenseCampaignEnemyCountPersistenceState>
                defeatedEnemyCountsByEnemyId,
            IEnumerable<SingleCityDefenseCampaignSpawnAnchorPersistenceState>
                frozenSpawnAnchors,
            IEnumerable<SingleCityDefenseCampaignEnemyPersistenceState>
                enemies,
            SingleCityDefenseCampaignStatisticsPersistenceState statistics)
        {
            CampaignId = campaignId;
            Phase = phase;
            CurrentWaveNumber = currentWaveNumber;
            WarningRemainingSeconds = warningRemainingSeconds;
            SpawnClockSeconds = spawnClockSeconds;
            FixedStepAccumulatorSeconds = fixedStepAccumulatorSeconds;
            NextEnemyOrdinal = nextEnemyOrdinal;
            CoreCurrentHealth = coreCurrentHealth;
            Result = result;
            this.plannedEnemyCountsByEnemyId = CopyCounts(
                plannedEnemyCountsByEnemyId);
            this.spawnedEnemyCountsByEnemyId = CopyCounts(
                spawnedEnemyCountsByEnemyId);
            this.defeatedEnemyCountsByEnemyId = CopyCounts(
                defeatedEnemyCountsByEnemyId);
            this.frozenSpawnAnchors = CopyAnchors(frozenSpawnAnchors);
            this.enemies = CopyEnemies(enemies);
            Statistics = statistics == null
                ? null
                : new SingleCityDefenseCampaignStatisticsPersistenceState(
                    statistics.ElapsedRuleSeconds,
                    statistics.SpawnedEnemyCount,
                    statistics.DefeatedEnemyCount,
                    statistics.CompletedWaveCount,
                    statistics.KillsByEnemyId,
                    statistics.HighestAliveEnemyCount,
                    statistics.CoreDamageTaken,
                    statistics.DamageByTowerBuildingId,
                    statistics.KillsByTowerBuildingId,
                    statistics.ConsumablesSpentByResourceId,
                    statistics.BuildingLossCount,
                    statistics.BuildingLossesByBuildingId);
        }

        public string CampaignId { get; }
        public SingleCityDefenseCampaignPhase Phase { get; }
        public int CurrentWaveNumber { get; }
        public float WarningRemainingSeconds { get; }
        public float SpawnClockSeconds { get; }
        public float FixedStepAccumulatorSeconds { get; }
        public int NextEnemyOrdinal { get; }
        public int CoreCurrentHealth { get; }
        public SingleCityDefenseCampaignResult Result { get; }
        public IReadOnlyList<SingleCityDefenseCampaignEnemyCountPersistenceState>
            PlannedEnemyCountsByEnemyId => plannedEnemyCountsByEnemyId;
        public IReadOnlyList<SingleCityDefenseCampaignEnemyCountPersistenceState>
            SpawnedEnemyCountsByEnemyId => spawnedEnemyCountsByEnemyId;
        public IReadOnlyList<SingleCityDefenseCampaignEnemyCountPersistenceState>
            DefeatedEnemyCountsByEnemyId => defeatedEnemyCountsByEnemyId;
        public IReadOnlyList<SingleCityDefenseCampaignSpawnAnchorPersistenceState>
            FrozenSpawnAnchors => frozenSpawnAnchors;
        public IReadOnlyList<SingleCityDefenseCampaignEnemyPersistenceState>
            Enemies => enemies;
        public SingleCityDefenseCampaignStatisticsPersistenceState Statistics
        {
            get;
        }

        private static ReadOnlyCollection<
            SingleCityDefenseCampaignEnemyCountPersistenceState> CopyCounts(
                IEnumerable<
                    SingleCityDefenseCampaignEnemyCountPersistenceState> source)
        {
            var result = new List<
                SingleCityDefenseCampaignEnemyCountPersistenceState>();
            if (source != null)
            {
                foreach (SingleCityDefenseCampaignEnemyCountPersistenceState
                         item in source)
                {
                    result.Add(item == null
                        ? null
                        : new
                            SingleCityDefenseCampaignEnemyCountPersistenceState(
                                item.EnemyDefinitionId,
                                item.Count));
                }
            }
            result.Sort((left, right) => string.CompareOrdinal(
                left?.EnemyDefinitionId,
                right?.EnemyDefinitionId));
            return result.AsReadOnly();
        }

        private static ReadOnlyCollection<
            SingleCityDefenseCampaignSpawnAnchorPersistenceState> CopyAnchors(
                IEnumerable<
                    SingleCityDefenseCampaignSpawnAnchorPersistenceState>
                    source)
        {
            var result = new List<
                SingleCityDefenseCampaignSpawnAnchorPersistenceState>();
            if (source != null)
            {
                foreach (SingleCityDefenseCampaignSpawnAnchorPersistenceState
                         item in source)
                {
                    result.Add(item == null
                        ? null
                        : new
                            SingleCityDefenseCampaignSpawnAnchorPersistenceState(
                                item.Direction,
                                item.X,
                                item.Z));
                }
            }
            result.Sort((left, right) =>
                (left?.Direction ?? CampaignSpawnDirection.East).CompareTo(
                    right?.Direction ?? CampaignSpawnDirection.East));
            return result.AsReadOnly();
        }

        private static ReadOnlyCollection<
            SingleCityDefenseCampaignEnemyPersistenceState> CopyEnemies(
                IEnumerable<SingleCityDefenseCampaignEnemyPersistenceState>
                    source)
        {
            var result = new List<
                SingleCityDefenseCampaignEnemyPersistenceState>();
            if (source != null)
            {
                foreach (SingleCityDefenseCampaignEnemyPersistenceState item
                         in source)
                {
                    result.Add(item == null
                        ? null
                        : new SingleCityDefenseCampaignEnemyPersistenceState(
                            item.StableId,
                            item.EnemyDefinitionId,
                            item.SpawnOrder,
                            item.X,
                            item.Z,
                            item.CurrentHealth,
                            item.MovementRemainder,
                            item.AttackDamageRemainder,
                            item.TargetStableId));
                }
            }
            result.Sort((left, right) => string.CompareOrdinal(
                left?.StableId,
                right?.StableId));
            return result.AsReadOnly();
        }
    }

    public sealed class SingleCityDefenseCampaignRestorePlan
    {
        internal SingleCityDefenseCampaignRestorePlan(
            object owner,
            ulong expectedGeneration,
            ulong expectedFingerprint,
            object candidate)
        {
            Owner = owner;
            ExpectedGeneration = expectedGeneration;
            ExpectedFingerprint = expectedFingerprint;
            Candidate = candidate;
        }

        internal object Owner { get; }
        internal ulong ExpectedGeneration { get; }
        internal ulong ExpectedFingerprint { get; }
        internal object Candidate { get; }
        internal bool Consumed { get; set; }
    }
}
