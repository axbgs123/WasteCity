using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using WasteCity.Building;
using WasteCity.Combat;

namespace WasteCity.Defense
{
    public enum SingleCityDefenseCampaignPhase
    {
        Idle,
        Warning,
        SpawningAndCombat,
        CombatCleanup,
        Victory,
        Defeat,
    }

    public enum SingleCityDefenseCampaignResult
    {
        None,
        Victory,
        Defeat,
    }

    public sealed class DefenseBuildingTargetCandidate
    {
        public DefenseBuildingTargetCandidate(
            string stableId,
            string buildingId,
            float distance,
            bool isCompleted,
            bool isPlayerOwned,
            bool isDestroyed,
            bool isProduction)
        {
            StableId = stableId;
            BuildingId = buildingId;
            Distance = Math.Max(0f, distance);
            IsCompleted = isCompleted;
            IsPlayerOwned = isPlayerOwned;
            IsDestroyed = isDestroyed;
            IsProduction = isProduction;
        }

        public string StableId { get; }
        public string BuildingId { get; }
        public float Distance { get; }
        public bool IsCompleted { get; }
        public bool IsPlayerOwned { get; }
        public bool IsDestroyed { get; }
        public bool IsProduction { get; }

        public bool IsValidTarget =>
            IsCompleted &&
            IsPlayerOwned &&
            !IsDestroyed &&
            !string.IsNullOrWhiteSpace(StableId);
    }

    public sealed class DefenseBuildingCombatTarget
    {
        public DefenseBuildingCombatTarget(
            string stableId,
            string buildingId,
            float x,
            float z,
            bool isCompleted,
            bool isPlayerOwned,
            bool isDestroyed,
            bool isEvacuationLocked,
            bool isProduction)
        {
            StableId = stableId;
            BuildingId = buildingId;
            X = x;
            Z = z;
            IsCompleted = isCompleted;
            IsPlayerOwned = isPlayerOwned;
            IsDestroyed = isDestroyed;
            IsEvacuationLocked = isEvacuationLocked;
            IsProduction = isProduction;
        }

        public string StableId { get; }
        public string BuildingId { get; }
        public float X { get; }
        public float Z { get; }
        public bool IsCompleted { get; }
        public bool IsPlayerOwned { get; }
        public bool IsDestroyed { get; }
        public bool IsEvacuationLocked { get; }
        public bool IsProduction { get; }

        public bool IsValidTarget =>
            IsCompleted &&
            IsPlayerOwned &&
            !IsDestroyed &&
            !IsEvacuationLocked &&
            !string.IsNullOrWhiteSpace(StableId);
    }

    public sealed class SingleCityDefenseEnemySnapshot
    {
        public SingleCityDefenseEnemySnapshot(
            string stableId,
            string enemyDefinitionId,
            int spawnOrder,
            float x,
            float z,
            int currentHealth,
            string targetStableId = null)
        {
            StableId = stableId;
            EnemyDefinitionId = enemyDefinitionId;
            SpawnOrder = spawnOrder;
            X = x;
            Z = z;
            CurrentHealth = Math.Max(0, currentHealth);
            TargetStableId = targetStableId;
        }

        public string StableId { get; }
        public string EnemyDefinitionId { get; }
        public int SpawnOrder { get; }
        public float X { get; }
        public float Z { get; }
        public int CurrentHealth { get; }
        public string TargetStableId { get; }
    }

    public sealed class SingleCityDefenseCampaignStatisticsSnapshot
    {
        public SingleCityDefenseCampaignStatisticsSnapshot(
            float elapsedRuleSeconds,
            int completedWaveCount,
            int totalKillCount,
            IReadOnlyDictionary<string, int> killsByEnemyId,
            IReadOnlyDictionary<string, int> damageByTowerBuildingId,
            IReadOnlyDictionary<string, int> killsByTowerBuildingId,
            IReadOnlyDictionary<string, int> consumablesSpentByResourceId,
            int buildingLossCount,
            int coreCurrentHealth,
            int coreMaximumHealth,
            int highestAliveEnemyCount)
        {
            ElapsedRuleSeconds = Math.Max(0f, elapsedRuleSeconds);
            CompletedWaveCount = Math.Max(0, completedWaveCount);
            TotalKillCount = Math.Max(0, totalKillCount);
            KillsByEnemyId = Copy(killsByEnemyId);
            DamageByTowerBuildingId = Copy(damageByTowerBuildingId);
            KillsByTowerBuildingId = Copy(killsByTowerBuildingId);
            ConsumablesSpentByResourceId = Copy(
                consumablesSpentByResourceId);
            BuildingLossCount = Math.Max(0, buildingLossCount);
            CoreCurrentHealth = Math.Max(0, coreCurrentHealth);
            CoreMaximumHealth = Math.Max(1, coreMaximumHealth);
            HighestAliveEnemyCount = Math.Max(0, highestAliveEnemyCount);
        }

        public float ElapsedRuleSeconds { get; }
        public int CompletedWaveCount { get; }
        public int TotalKillCount { get; }
        public IReadOnlyDictionary<string, int> KillsByEnemyId { get; }
        public IReadOnlyDictionary<string, int> DamageByTowerBuildingId
        {
            get;
        }
        public IReadOnlyDictionary<string, int> KillsByTowerBuildingId
        {
            get;
        }
        public IReadOnlyDictionary<string, int> ConsumablesSpentByResourceId
        {
            get;
        }
        public int BuildingLossCount { get; }
        public int CoreCurrentHealth { get; }
        public int CoreMaximumHealth { get; }
        public int HighestAliveEnemyCount { get; }

        private static IReadOnlyDictionary<string, int> Copy(
            IReadOnlyDictionary<string, int> source)
        {
            var copy = new SortedDictionary<string, int>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (KeyValuePair<string, int> pair in source)
                    copy[pair.Key] = pair.Value;
            }
            return new ReadOnlyDictionary<string, int>(copy);
        }
    }

    public sealed class SingleCityDefenseCampaignSnapshot
    {
        private readonly ReadOnlyCollection<SingleCityDefenseEnemySnapshot>
            enemies;

        public SingleCityDefenseCampaignSnapshot(
            int currentWaveNumber,
            SingleCityDefenseCampaignPhase phase,
            float warningRemainingSeconds,
            int plannedEnemyCount,
            int spawnedEnemyCount,
            int aliveEnemyCount,
            int coreCurrentHealth,
            int coreMaximumHealth,
            SingleCityDefenseCampaignResult result,
            IEnumerable<SingleCityDefenseEnemySnapshot> enemies,
            SingleCityDefenseCampaignStatisticsSnapshot statistics)
        {
            CurrentWaveNumber = Math.Max(0, currentWaveNumber);
            Phase = phase;
            WarningRemainingSeconds = Math.Max(0f, warningRemainingSeconds);
            PlannedEnemyCount = Math.Max(0, plannedEnemyCount);
            SpawnedEnemyCount = Math.Max(0, spawnedEnemyCount);
            AliveEnemyCount = Math.Max(0, aliveEnemyCount);
            CoreCurrentHealth = Math.Max(0, coreCurrentHealth);
            CoreMaximumHealth = Math.Max(1, coreMaximumHealth);
            Result = result;
            this.enemies = Array.AsReadOnly(
                new List<SingleCityDefenseEnemySnapshot>(
                    enemies ?? Array.Empty<SingleCityDefenseEnemySnapshot>())
                    .ToArray());
            Statistics = statistics ??
                throw new ArgumentNullException(nameof(statistics));
        }

        public int CurrentWaveNumber { get; }
        public SingleCityDefenseCampaignPhase Phase { get; }
        public float WarningRemainingSeconds { get; }
        public int PlannedEnemyCount { get; }
        public int SpawnedEnemyCount { get; }
        public int AliveEnemyCount { get; }
        public int CoreCurrentHealth { get; }
        public int CoreMaximumHealth { get; }
        public SingleCityDefenseCampaignResult Result { get; }
        public IReadOnlyList<SingleCityDefenseEnemySnapshot> Enemies => enemies;
        public SingleCityDefenseCampaignStatisticsSnapshot Statistics
        {
            get;
        }
    }

    public sealed class SingleCityDefenseCampaignModel
    {
        public const float FormalFixedStepSeconds = .1f;
        public const string CityCoreTargetId = "city.core";

        private const double FixedStepSeconds = .1d;
        private const double StepEpsilon = .0000001d;

        private float coreX;
        private float coreZ;
        private readonly List<EnemyState> enemies = new List<EnemyState>();
        private readonly List<SpawnDefinition> spawnSequence =
            new List<SpawnDefinition>();
        private readonly Dictionary<string, int> killsByEnemyId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> damageByTowerBuildingId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> killsByTowerBuildingId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> consumablesSpentByResourceId =
            new Dictionary<string, int>(StringComparer.Ordinal);

        private double fixedStepAccumulatorSeconds;
        private double warningRemainingSeconds;
        private double spawnClockSeconds;
        private double elapsedRuleSeconds;
        private CampaignWaveDefinition currentWave;
        private SingleCityDefenseCampaignPhase phase =
            SingleCityDefenseCampaignPhase.Idle;
        private SingleCityDefenseCampaignResult result =
            SingleCityDefenseCampaignResult.None;
        private bool campaignTriggered;
        private int nextSpawnIndex;
        private int completedWaveCount;
        private int buildingLossCount;
        private int highestAliveEnemyCount;
        private int coreCurrentHealth = CityCoreCombatModel.FormalMaximumHealth;

        public SingleCityDefenseCampaignModel(float coreX, float coreZ)
        {
            this.coreX = coreX;
            this.coreZ = coreZ;
        }

        public SingleCityDefenseCampaignSnapshot Snapshot => CreateSnapshot();
        public bool IsTerminal =>
            result != SingleCityDefenseCampaignResult.None;

        public void SetCorePosition(float x, float z)
        {
            if (float.IsNaN(x) || float.IsInfinity(x) ||
                float.IsNaN(z) || float.IsInfinity(z))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    "City core position must be finite.");
            }
            coreX = x;
            coreZ = z;
        }

        public bool NotifyDefenseTowerCompleted(
            string stableInstanceId,
            string buildingId,
            bool isCompleted,
            bool isPlayerOwned)
        {
            DefenseTowerDefinition tower = DefenseTowerCatalog.For(buildingId);
            if (campaignTriggered ||
                !isCompleted ||
                !isPlayerOwned ||
                string.IsNullOrWhiteSpace(stableInstanceId) ||
                !IsFormalDefenseTower(buildingId) ||
                tower == null ||
                tower.LocalCapacity <= 0 ||
                CampaignWaveCatalog.All.Count == 0)
            {
                return false;
            }

            campaignTriggered = true;
            BeginWave(0);
            return true;
        }

        public void Advance(float unscaledDeltaSeconds, int requestedSpeed)
        {
            AdvanceInternal(
                unscaledDeltaSeconds,
                requestedSpeed,
                buildingTargetProvider: null,
                applyBuildingDamage: null,
                advanceEnemyCombat: false);
        }

        public void Advance(
            float unscaledDeltaSeconds,
            int requestedSpeed,
            Func<DefenseBuildingCombatTarget[]> buildingTargetProvider,
            Func<string, string, int, int> applyBuildingDamage)
        {
            AdvanceInternal(
                unscaledDeltaSeconds,
                requestedSpeed,
                buildingTargetProvider,
                applyBuildingDamage,
                advanceEnemyCombat: true);
        }

        private void AdvanceInternal(
            float unscaledDeltaSeconds,
            int requestedSpeed,
            Func<DefenseBuildingCombatTarget[]> buildingTargetProvider,
            Func<string, string, int, int> applyBuildingDamage,
            bool advanceEnemyCombat)
        {
            if (!campaignTriggered ||
                IsTerminal ||
                unscaledDeltaSeconds <= 0f)
            {
                return;
            }

            int speed = Math.Max(0, Math.Min(2, requestedSpeed));
            if (speed == 0) return;

            fixedStepAccumulatorSeconds +=
                (double)unscaledDeltaSeconds * speed;
            while (fixedStepAccumulatorSeconds + StepEpsilon >=
                   FixedStepSeconds)
            {
                fixedStepAccumulatorSeconds -= FixedStepSeconds;
                if (fixedStepAccumulatorSeconds < 0d)
                    fixedStepAccumulatorSeconds = 0d;
                Step(
                    FormalFixedStepSeconds,
                    buildingTargetProvider,
                    applyBuildingDamage,
                    advanceEnemyCombat);
                if (IsTerminal)
                {
                    fixedStepAccumulatorSeconds = 0d;
                    break;
                }
            }
        }

        public bool DefeatEnemy(
            string stableEnemyId,
            string sourceTowerBuildingId)
        {
            if (IsTerminal ||
                DefenseTowerCatalog.For(sourceTowerBuildingId) == null)
            {
                return false;
            }

            EnemyState enemy = FindAliveEnemy(stableEnemyId);
            if (enemy == null) return false;

            int appliedDamage = enemy.CurrentHealth;
            enemy.CurrentHealth = 0;
            RegisterDamage(sourceTowerBuildingId, appliedDamage);
            RegisterKill(enemy.Definition.Id.Value, sourceTowerBuildingId);
            return true;
        }

        public int ApplyTowerDamage(
            string stableEnemyId,
            string towerBuildingId,
            int rawDamage)
        {
            if (IsTerminal || rawDamage <= 0) return 0;
            EnemyState enemy = FindAliveEnemy(stableEnemyId);
            if (enemy == null) return 0;

            int resolved = ResolveTowerDamage(
                towerBuildingId,
                enemy.Definition.Id.Value,
                rawDamage);
            int applied = Math.Min(enemy.CurrentHealth, resolved);
            if (applied <= 0) return 0;

            enemy.CurrentHealth -= applied;
            RegisterDamage(towerBuildingId, applied);
            if (enemy.CurrentHealth == 0)
            {
                RegisterKill(enemy.Definition.Id.Value, towerBuildingId);
            }
            return applied;
        }

        internal string AcquireTowerTarget(
            string lockedStableEnemyId,
            float towerX,
            float towerZ,
            float range)
        {
            float safeRange = Math.Max(0f, range);
            float rangeSquared = safeRange * safeRange;
            EnemyState locked = FindAliveEnemy(lockedStableEnemyId);
            if (locked != null && DistanceSquared(
                    towerX,
                    towerZ,
                    locked.X,
                    locked.Z) <= rangeSquared)
            {
                return locked.StableId;
            }

            EnemyState selected = null;
            float selectedDistanceSquared = float.MaxValue;
            for (var index = 0; index < enemies.Count; index++)
            {
                EnemyState candidate = enemies[index];
                if (candidate.CurrentHealth <= 0) continue;
                float distanceSquared = DistanceSquared(
                    towerX,
                    towerZ,
                    candidate.X,
                    candidate.Z);
                if (distanceSquared > rangeSquared) continue;
                if (selected == null ||
                    distanceSquared < selectedDistanceSquared ||
                    distanceSquared == selectedDistanceSquared &&
                    string.CompareOrdinal(
                        candidate.StableId,
                        selected.StableId) < 0)
                {
                    selected = candidate;
                    selectedDistanceSquared = distanceSquared;
                }
            }
            return selected?.StableId;
        }

        internal float ResolveTowerDamageMultiplier(
            string stableEnemyId,
            string towerBuildingId)
        {
            EnemyState enemy = FindAliveEnemy(stableEnemyId);
            DefenseTowerDefinition tower = DefenseTowerCatalog.For(
                towerBuildingId);
            return enemy == null || tower == null
                ? 0f
                : DamageMatrix.Multiplier(
                    tower.DamageType,
                    enemy.Definition.Armor);
        }

        internal int ApplyResolvedTowerDamage(
            string stableEnemyId,
            string towerBuildingId,
            int resolvedDamage)
        {
            if (IsTerminal || resolvedDamage <= 0 ||
                DefenseTowerCatalog.For(towerBuildingId) == null)
            {
                return 0;
            }

            EnemyState enemy = FindAliveEnemy(stableEnemyId);
            if (enemy == null) return 0;
            int applied = Math.Min(enemy.CurrentHealth, resolvedDamage);
            enemy.CurrentHealth -= applied;
            RegisterDamage(towerBuildingId, applied);
            if (enemy.CurrentHealth == 0)
            {
                RegisterKill(
                    enemy.Definition.Id.Value,
                    towerBuildingId);
            }
            return applied;
        }

        public int ApplyCoreDamage(int damage)
        {
            if (IsTerminal || damage <= 0) return 0;
            int applied = Math.Min(coreCurrentHealth, damage);
            coreCurrentHealth -= applied;
            if (coreCurrentHealth == 0)
            {
                result = SingleCityDefenseCampaignResult.Defeat;
                phase = SingleCityDefenseCampaignPhase.Defeat;
            }
            return applied;
        }

        public void RegisterConsumableSpent(string resourceId, int amount)
        {
            if (IsTerminal) return;
            Add(consumablesSpentByResourceId, resourceId, amount);
        }

        public void RegisterBuildingLoss()
        {
            buildingLossCount++;
        }

        public string ResolveEnemyTarget(
            string enemyDefinitionId,
            DefenseBuildingTargetCandidate[] candidates)
        {
            EnemyDefinition enemy = FindEnemyDefinition(enemyDefinitionId);
            if (enemy == null || enemy.TargetPriority == EnemyTargetPriority.Core)
                return CityCoreTargetId;

            DefenseBuildingTargetCandidate selected = null;
            if (candidates != null)
            {
                for (var index = 0; index < candidates.Length; index++)
                {
                    DefenseBuildingTargetCandidate candidate = candidates[index];
                    if (!MatchesPriority(enemy.TargetPriority, candidate))
                        continue;
                    if (selected == null || Compare(candidate, selected) < 0)
                        selected = candidate;
                }
            }
            return selected == null ? CityCoreTargetId : selected.StableId;
        }

        public int ResolveTowerDamage(
            string towerBuildingId,
            string enemyDefinitionId,
            int rawDamage)
        {
            DefenseTowerDefinition tower = DefenseTowerCatalog.For(
                towerBuildingId);
            EnemyDefinition enemy = FindEnemyDefinition(enemyDefinitionId);
            if (tower == null || enemy == null || rawDamage <= 0) return 0;
            return DamageMatrix.Apply(
                rawDamage,
                tower.DamageType,
                enemy.Armor);
        }

        public static SingleCityDefenseCampaignResult ResolveTerminalResult(
            int currentWaveNumber,
            bool allPlannedEnemiesSpawned,
            int aliveEnemyCount,
            int coreCurrentHealth)
        {
            if (coreCurrentHealth <= 0)
                return SingleCityDefenseCampaignResult.Defeat;
            if (CampaignWaveCatalog.All.Count > 0 &&
                currentWaveNumber ==
                    CampaignWaveCatalog.All[CampaignWaveCatalog.All.Count - 1]
                        .Number &&
                allPlannedEnemiesSpawned &&
                aliveEnemyCount <= 0)
            {
                return SingleCityDefenseCampaignResult.Victory;
            }
            return SingleCityDefenseCampaignResult.None;
        }

        private void Step(
            float deltaSeconds,
            Func<DefenseBuildingCombatTarget[]> buildingTargetProvider,
            Func<string, string, int, int> applyBuildingDamage,
            bool advanceEnemyCombat)
        {
            elapsedRuleSeconds += deltaSeconds;
            if (phase == SingleCityDefenseCampaignPhase.Warning)
            {
                double consumed = Math.Min(
                    warningRemainingSeconds,
                    deltaSeconds);
                warningRemainingSeconds -= consumed;
                double remaining = deltaSeconds - consumed;
                if (warningRemainingSeconds <= StepEpsilon)
                {
                    warningRemainingSeconds = 0d;
                    phase = SingleCityDefenseCampaignPhase.SpawningAndCombat;
                    Spawn(remaining);
                }
                return;
            }

            if (phase == SingleCityDefenseCampaignPhase.SpawningAndCombat)
            {
                if (advanceEnemyCombat && !IsTerminal)
                {
                    StepEnemyCombat(
                        deltaSeconds,
                        buildingTargetProvider,
                        applyBuildingDamage);
                }
                if (!IsTerminal)
                    Spawn(deltaSeconds);
            }
            else if (phase == SingleCityDefenseCampaignPhase.CombatCleanup &&
                     advanceEnemyCombat && !IsTerminal)
            {
                StepEnemyCombat(
                    deltaSeconds,
                    buildingTargetProvider,
                    applyBuildingDamage);
            }
            if (!IsTerminal &&
                phase == SingleCityDefenseCampaignPhase.CombatCleanup)
            {
                TryCompleteWave();
            }
        }

        private void StepEnemyCombat(
            float deltaSeconds,
            Func<DefenseBuildingCombatTarget[]> buildingTargetProvider,
            Func<string, string, int, int> applyBuildingDamage)
        {
            if (deltaSeconds <= 0f || AliveEnemyCount == 0) return;
            DefenseBuildingCombatTarget[] buildingTargets =
                applyBuildingDamage != null && HasBuildingSeekingEnemy()
                    ? buildingTargetProvider?.Invoke() ??
                        Array.Empty<DefenseBuildingCombatTarget>()
                    : Array.Empty<DefenseBuildingCombatTarget>();
            enemies.Sort(EnemyStateProcessingComparer.Instance);
            for (var index = 0; index < enemies.Count; index++)
            {
                if (IsTerminal) return;
                EnemyState enemy = enemies[index];
                if (enemy.CurrentHealth <= 0) continue;

                string targetStableId = ResolveEnemyCombatTarget(
                    enemy,
                    buildingTargets,
                    applyBuildingDamage != null);
                ResolveEnemyTargetPosition(
                    targetStableId,
                    buildingTargets,
                    out float targetX,
                    out float targetZ,
                    out DefenseBuildingCombatTarget buildingTarget);
                enemy.TargetStableId = buildingTarget == null
                    ? CityCoreTargetId
                    : buildingTarget.StableId;

                if (!MoveEnemyIntoRange(
                        enemy,
                        targetX,
                        targetZ,
                        deltaSeconds))
                {
                    continue;
                }

                float attackDamageRemainder =
                    enemy.AttackDamageRemainder +
                    enemy.Definition.DamagePerSecond * deltaSeconds;
                int rawDamage = WholeDamage(ref attackDamageRemainder);
                enemy.AttackDamageRemainder = attackDamageRemainder;
                if (rawDamage <= 0) continue;
                if (buildingTarget == null)
                {
                    ApplyCoreDamage(rawDamage);
                }
                else
                {
                    applyBuildingDamage?.Invoke(
                        enemy.StableId,
                        buildingTarget.StableId,
                        rawDamage);
                }
            }
        }

        private bool HasBuildingSeekingEnemy()
        {
            for (var index = 0; index < enemies.Count; index++)
            {
                EnemyState enemy = enemies[index];
                if (enemy.CurrentHealth > 0 &&
                    enemy.Definition.TargetPriority !=
                        EnemyTargetPriority.Core)
                {
                    return true;
                }
            }
            return false;
        }

        private string ResolveEnemyCombatTarget(
            EnemyState enemy,
            DefenseBuildingCombatTarget[] buildingTargets,
            bool canDamageBuildings)
        {
            if (enemy.Definition.TargetPriority == EnemyTargetPriority.Core ||
                !canDamageBuildings)
            {
                return CityCoreTargetId;
            }

            DefenseBuildingCombatTarget locked = FindBuildingCombatTarget(
                enemy.TargetStableId,
                buildingTargets);
            if (MatchesPriority(enemy.Definition.TargetPriority, locked))
                return locked.StableId;

            DefenseBuildingCombatTarget selected = null;
            float selectedDistanceSquared = float.MaxValue;
            for (var index = 0;
                 index < (buildingTargets?.Length ?? 0);
                 index++)
            {
                DefenseBuildingCombatTarget target = buildingTargets[index];
                if (!MatchesPriority(
                        enemy.Definition.TargetPriority,
                        target))
                {
                    continue;
                }
                float distanceSquared = DistanceSquared(
                    enemy.X,
                    enemy.Z,
                    target.X,
                    target.Z);
                if (selected == null ||
                    distanceSquared < selectedDistanceSquared ||
                    distanceSquared == selectedDistanceSquared &&
                    string.CompareOrdinal(
                        target.StableId,
                        selected.StableId) < 0)
                {
                    selected = target;
                    selectedDistanceSquared = distanceSquared;
                }
            }
            return selected == null ? CityCoreTargetId : selected.StableId;
        }

        private void ResolveEnemyTargetPosition(
            string targetStableId,
            DefenseBuildingCombatTarget[] buildingTargets,
            out float targetX,
            out float targetZ,
            out DefenseBuildingCombatTarget buildingTarget)
        {
            buildingTarget = string.Equals(
                targetStableId,
                CityCoreTargetId,
                StringComparison.Ordinal)
                ? null
                : FindBuildingCombatTarget(
                    targetStableId,
                    buildingTargets);
            targetX = buildingTarget == null ? coreX : buildingTarget.X;
            targetZ = buildingTarget == null ? coreZ : buildingTarget.Z;
        }

        private static DefenseBuildingCombatTarget FindBuildingCombatTarget(
            string stableId,
            DefenseBuildingCombatTarget[] buildingTargets)
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                buildingTargets == null)
            {
                return null;
            }
            for (var index = 0; index < buildingTargets.Length; index++)
            {
                DefenseBuildingCombatTarget target = buildingTargets[index];
                if (target != null && target.IsValidTarget && string.Equals(
                        target.StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    return target;
                }
            }
            return null;
        }

        private static bool MoveEnemyIntoRange(
            EnemyState enemy,
            float targetX,
            float targetZ,
            float deltaSeconds)
        {
            float offsetX = targetX - enemy.X;
            float offsetZ = targetZ - enemy.Z;
            float distance = (float)Math.Sqrt(
                offsetX * offsetX + offsetZ * offsetZ);
            float attackRange = Math.Max(0f, enemy.Definition.AttackRange);
            float availableDistance = Math.Max(0f, distance - attackRange);
            if (availableDistance > 0f && distance > 0f)
            {
                float moved = Math.Min(
                    availableDistance,
                    enemy.Definition.MoveSpeed * deltaSeconds);
                enemy.X += offsetX / distance * moved;
                enemy.Z += offsetZ / distance * moved;
                return false;
            }
            return distance <= attackRange + (float)StepEpsilon;
        }

        private static int WholeDamage(ref float remainder)
        {
            int whole = (int)remainder;
            float fraction = remainder - whole;
            if (fraction > 0f && 1f - fraction <= .00001f)
            {
                whole++;
                fraction = 0f;
            }
            remainder = fraction;
            return whole;
        }

        private void Spawn(double deltaSeconds)
        {
            if (currentWave == null || spawnSequence.Count == 0) return;
            double cadence = currentWave.SpawnSeconds / spawnSequence.Count;
            spawnClockSeconds += Math.Max(0d, deltaSeconds);
            while (nextSpawnIndex < spawnSequence.Count &&
                   spawnClockSeconds + StepEpsilon >= cadence)
            {
                spawnClockSeconds -= cadence;
                SpawnEnemy(spawnSequence[nextSpawnIndex], nextSpawnIndex);
                nextSpawnIndex++;
            }

            if (nextSpawnIndex >= spawnSequence.Count)
            {
                phase = AliveEnemyCount == 0
                    ? SingleCityDefenseCampaignPhase.SpawningAndCombat
                    : SingleCityDefenseCampaignPhase.CombatCleanup;
                TryCompleteWave();
            }
        }

        private void SpawnEnemy(SpawnDefinition spawn, int spawnOrder)
        {
            ResolveSpawnPosition(spawn.Direction, out float x, out float z);
            string stableId = "campaign.enemy.wave-" +
                currentWave.Number.ToString("00", CultureInfo.InvariantCulture) +
                "." + spawnOrder.ToString("0000", CultureInfo.InvariantCulture);
            enemies.Add(new EnemyState(
                stableId,
                spawn.Definition,
                spawnOrder,
                x,
                z));
            highestAliveEnemyCount = Math.Max(
                highestAliveEnemyCount,
                AliveEnemyCount);
        }

        private void ResolveSpawnPosition(
            CampaignSpawnDirection direction,
            out float x,
            out float z)
        {
            x = coreX;
            z = coreZ;
            switch (direction)
            {
                case CampaignSpawnDirection.North:
                    z += 20f;
                    break;
                case CampaignSpawnDirection.South:
                    z -= 20f;
                    break;
                case CampaignSpawnDirection.West:
                    x -= 20f;
                    break;
                default:
                    x += 20f;
                    break;
            }
        }

        private void TryCompleteWave()
        {
            if (currentWave == null ||
                nextSpawnIndex < currentWave.TotalCount ||
                AliveEnemyCount > 0)
            {
                return;
            }

            SingleCityDefenseCampaignResult terminal = ResolveTerminalResult(
                currentWave.Number,
                allPlannedEnemiesSpawned: true,
                aliveEnemyCount: 0,
                coreCurrentHealth);
            if (terminal != SingleCityDefenseCampaignResult.None)
            {
                result = terminal;
                phase = terminal == SingleCityDefenseCampaignResult.Defeat
                    ? SingleCityDefenseCampaignPhase.Defeat
                    : SingleCityDefenseCampaignPhase.Victory;
                completedWaveCount++;
                return;
            }

            completedWaveCount++;
            BeginWave(currentWave.Number);
        }

        private void BeginWave(int catalogIndex)
        {
            if (catalogIndex < 0 ||
                catalogIndex >= CampaignWaveCatalog.All.Count)
            {
                return;
            }

            currentWave = CampaignWaveCatalog.All[catalogIndex];
            warningRemainingSeconds = currentWave.WarningSeconds;
            spawnClockSeconds = 0d;
            nextSpawnIndex = 0;
            enemies.Clear();
            BuildSpawnSequence(currentWave);
            phase = SingleCityDefenseCampaignPhase.Warning;
        }

        private void BuildSpawnSequence(CampaignWaveDefinition wave)
        {
            spawnSequence.Clear();
            var remaining = new int[wave.Entries.Count];
            for (var index = 0; index < wave.Entries.Count; index++)
                remaining[index] = wave.Entries[index].Count;

            bool added;
            do
            {
                added = false;
                for (var index = 0; index < wave.Entries.Count; index++)
                {
                    if (remaining[index] <= 0) continue;
                    remaining[index]--;
                    EnemyDefinition definition = FindEnemyDefinition(
                        wave.Entries[index].Archetype);
                    if (definition == null) continue;
                    CampaignSpawnDirection direction = wave.Directions.Count == 0
                        ? CampaignSpawnDirection.East
                        : wave.Directions[spawnSequence.Count %
                            wave.Directions.Count];
                    spawnSequence.Add(new SpawnDefinition(
                        definition,
                        direction));
                    added = true;
                }
            }
            while (added);
        }

        private int AliveEnemyCount
        {
            get
            {
                int count = 0;
                for (var index = 0; index < enemies.Count; index++)
                {
                    if (enemies[index].CurrentHealth > 0) count++;
                }
                return count;
            }
        }

        private EnemyState FindAliveEnemy(string stableEnemyId)
        {
            if (string.IsNullOrWhiteSpace(stableEnemyId)) return null;
            for (var index = 0; index < enemies.Count; index++)
            {
                EnemyState enemy = enemies[index];
                if (enemy.CurrentHealth > 0 &&
                    string.Equals(
                        enemy.StableId,
                        stableEnemyId,
                        StringComparison.Ordinal))
                {
                    return enemy;
                }
            }
            return null;
        }

        private SingleCityDefenseCampaignSnapshot CreateSnapshot()
        {
            var snapshots = new List<SingleCityDefenseEnemySnapshot>();
            for (var index = 0; index < enemies.Count; index++)
            {
                EnemyState enemy = enemies[index];
                if (enemy.CurrentHealth <= 0) continue;
                snapshots.Add(new SingleCityDefenseEnemySnapshot(
                    enemy.StableId,
                    enemy.Definition.Id.Value,
                    enemy.SpawnOrder,
                    enemy.X,
                    enemy.Z,
                    enemy.CurrentHealth,
                    enemy.TargetStableId));
            }

            return new SingleCityDefenseCampaignSnapshot(
                currentWave == null ? 0 : currentWave.Number,
                phase,
                (float)warningRemainingSeconds,
                currentWave == null ? 0 : currentWave.TotalCount,
                nextSpawnIndex,
                AliveEnemyCount,
                coreCurrentHealth,
                CityCoreCombatModel.FormalMaximumHealth,
                result,
                snapshots,
                new SingleCityDefenseCampaignStatisticsSnapshot(
                    (float)elapsedRuleSeconds,
                    completedWaveCount,
                    TotalKillCount,
                    killsByEnemyId,
                    damageByTowerBuildingId,
                    killsByTowerBuildingId,
                    consumablesSpentByResourceId,
                    buildingLossCount,
                    coreCurrentHealth,
                    CityCoreCombatModel.FormalMaximumHealth,
                    highestAliveEnemyCount));
        }

        private int TotalKillCount
        {
            get
            {
                int total = 0;
                foreach (KeyValuePair<string, int> pair in killsByEnemyId)
                    total += pair.Value;
                return total;
            }
        }

        private static bool IsFormalDefenseTower(string buildingId)
        {
            return string.Equals(
                       buildingId,
                       BuildingCatalog.MachineGunTurret.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       buildingId,
                       BuildingCatalog.LaserTower.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       buildingId,
                       BuildingCatalog.SporeTower.Id.Value,
                       StringComparison.Ordinal);
        }

        private static bool MatchesPriority(
            EnemyTargetPriority priority,
            DefenseBuildingTargetCandidate candidate)
        {
            if (candidate == null || !candidate.IsValidTarget) return false;
            if (priority == EnemyTargetPriority.Walls)
            {
                return string.Equals(
                    candidate.BuildingId,
                    BuildingCatalog.Wall.Id.Value,
                    StringComparison.Ordinal);
            }
            return priority == EnemyTargetPriority.Production &&
                candidate.IsProduction;
        }

        private static bool MatchesPriority(
            EnemyTargetPriority priority,
            DefenseBuildingCombatTarget candidate)
        {
            if (candidate == null || !candidate.IsValidTarget) return false;
            if (priority == EnemyTargetPriority.Walls)
            {
                return string.Equals(
                    candidate.BuildingId,
                    BuildingCatalog.Wall.Id.Value,
                    StringComparison.Ordinal);
            }
            return priority == EnemyTargetPriority.Production &&
                candidate.IsProduction;
        }

        private static int Compare(
            DefenseBuildingTargetCandidate left,
            DefenseBuildingTargetCandidate right)
        {
            int distance = left.Distance.CompareTo(right.Distance);
            return distance != 0
                ? distance
                : string.CompareOrdinal(left.StableId, right.StableId);
        }

        private static float DistanceSquared(
            float leftX,
            float leftZ,
            float rightX,
            float rightZ)
        {
            float offsetX = rightX - leftX;
            float offsetZ = rightZ - leftZ;
            return offsetX * offsetX + offsetZ * offsetZ;
        }

        private static EnemyDefinition FindEnemyDefinition(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                EnemyDefinition definition = EnemyCatalog.All[index];
                if (string.Equals(
                    definition.Id.Value,
                    stableId,
                    StringComparison.Ordinal))
                {
                    return definition;
                }
            }
            return null;
        }

        private static EnemyDefinition FindEnemyDefinition(
            EnemyArchetype archetype)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
            {
                if (EnemyCatalog.All[index].Archetype == archetype)
                    return EnemyCatalog.All[index];
            }
            return null;
        }

        private void RegisterDamage(string towerBuildingId, int amount)
        {
            Add(damageByTowerBuildingId, towerBuildingId, amount);
        }

        private void RegisterKill(
            string enemyDefinitionId,
            string towerBuildingId)
        {
            Add(killsByEnemyId, enemyDefinitionId, 1);
            Add(killsByTowerBuildingId, towerBuildingId, 1);
        }

        private static void Add(
            IDictionary<string, int> target,
            string key,
            int amount)
        {
            if (target == null ||
                string.IsNullOrWhiteSpace(key) ||
                amount <= 0)
            {
                return;
            }

            target.TryGetValue(key, out int current);
            long combined = (long)current + amount;
            target[key] = combined >= int.MaxValue
                ? int.MaxValue
                : (int)combined;
        }

        private sealed class EnemyState
        {
            public EnemyState(
                string stableId,
                EnemyDefinition definition,
                int spawnOrder,
                float x,
                float z)
            {
                StableId = stableId;
                Definition = definition;
                SpawnOrder = spawnOrder;
                X = x;
                Z = z;
                CurrentHealth = definition.MaximumHealth;
            }

            public string StableId { get; }
            public EnemyDefinition Definition { get; }
            public int SpawnOrder { get; }
            public float X { get; set; }
            public float Z { get; set; }
            public int CurrentHealth { get; set; }
            public float AttackDamageRemainder { get; set; }
            public string TargetStableId { get; set; }
        }

        private sealed class EnemyStateProcessingComparer :
            IComparer<EnemyState>
        {
            public static readonly EnemyStateProcessingComparer Instance =
                new EnemyStateProcessingComparer();

            public int Compare(EnemyState left, EnemyState right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                int spawnOrder = left.SpawnOrder.CompareTo(right.SpawnOrder);
                return spawnOrder != 0
                    ? spawnOrder
                    : string.CompareOrdinal(left.StableId, right.StableId);
            }
        }

        private readonly struct SpawnDefinition
        {
            public SpawnDefinition(
                EnemyDefinition definition,
                CampaignSpawnDirection direction)
            {
                Definition = definition;
                Direction = direction;
            }

            public EnemyDefinition Definition { get; }
            public CampaignSpawnDirection Direction { get; }
        }
    }
}
