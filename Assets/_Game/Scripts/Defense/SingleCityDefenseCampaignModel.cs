using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Core;

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
            int highestAliveEnemyCount,
            bool partialFromMigration = false)
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
            PartialFromMigration = partialFromMigration;
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
        public bool PartialFromMigration { get; }

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
        private const double StepEpsilon = .000001d;

        private float coreX;
        private float coreZ;
        private readonly List<EnemyState> enemies = new List<EnemyState>();
        private readonly List<SpawnDefinition> spawnSequence =
            new List<SpawnDefinition>();
        private readonly SortedDictionary<string, WaveEntry[]>
            injectedReinforcements = new SortedDictionary<string, WaveEntry[]>(
                StringComparer.Ordinal);
        private readonly SessionStatisticsModel statistics =
            new SessionStatisticsModel();
        private readonly Dictionary<CampaignSpawnDirection,
            SingleCityDefenseCampaignSpawnAnchorPersistenceState>
            frozenSpawnAnchors = new Dictionary<CampaignSpawnDirection,
                SingleCityDefenseCampaignSpawnAnchorPersistenceState>();
        private readonly HashSet<string> movementSuppressedNextStep =
            new HashSet<string>(StringComparer.Ordinal);

        private double fixedStepAccumulatorSeconds;
        private double warningRemainingSeconds;
        private double spawnClockSeconds;
        private CampaignWaveDefinition currentWave;
        private SingleCityDefenseCampaignPhase phase =
            SingleCityDefenseCampaignPhase.Idle;
        private SingleCityDefenseCampaignResult result =
            SingleCityDefenseCampaignResult.None;
        private bool campaignTriggered;
        private int nextSpawnIndex;
        private int coreCurrentHealth = CityCoreCombatModel.FormalMaximumHealth;
        private ulong persistenceGeneration;
        private ulong terminalRevision;
        private readonly SingleCityDefenseCampaignDefinition definition;

        public SingleCityDefenseCampaignModel(float coreX, float coreZ)
            : this(coreX, coreZ, CampaignWaveCatalog.Default)
        {
        }

        public SingleCityDefenseCampaignModel(
            float coreX,
            float coreZ,
            SingleCityDefenseCampaignDefinition definition)
        {
            this.coreX = coreX;
            this.coreZ = coreZ;
            this.definition = definition ??
                throw new ArgumentNullException(nameof(definition));
        }

        public SingleCityDefenseCampaignSnapshot Snapshot => CreateSnapshot();
        public SessionStatisticsSnapshot SessionStatistics =>
            statistics.Capture();
        public bool IsTerminal =>
            result != SingleCityDefenseCampaignResult.None;
        public ulong TerminalRevision => terminalRevision;

        public event Action<int> WaveWarningStarted;
        public event Action<SingleCityDefenseCampaignResult>
            TerminalCommitted;
        public event Action<string, string> EnemyDefeated;

        public bool TryInjectReinforcements(
            string stableEventId,
            IReadOnlyList<WaveEntry> entries)
        {
            if (IsTerminal || currentWave == null ||
                string.IsNullOrWhiteSpace(stableEventId) || entries == null ||
                entries.Count == 0 || injectedReinforcements.ContainsKey(
                    stableEventId))
                return false;
            var copy = new WaveEntry[entries.Count];
            for (var index = 0; index < copy.Length; index++)
            {
                WaveEntry entry = entries[index];
                if (entry.Count <= 0 ||
                    FindEnemyDefinition(entry.Archetype) == null)
                    return false;
                copy[index] = entry;
            }
            injectedReinforcements.Add(stableEventId, copy);
            AppendSpawnEntries(currentWave, copy, spawnSequence);
            phase = SingleCityDefenseCampaignPhase.SpawningAndCombat;
            persistenceGeneration++;
            return true;
        }

        public SingleCityDefenseCampaignPersistenceState CaptureForPersistence()
        {
            SessionStatisticsSnapshot capturedStatistics =
                statistics.Capture();
            var planned = new Dictionary<string, int>(StringComparer.Ordinal);
            if (currentWave != null)
            {
                for (var index = 0; index < spawnSequence.Count; index++)
                    Add(planned, spawnSequence[index].Definition.Id.Value, 1);
            }

            var spawned = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0;
                 index < nextSpawnIndex && index < spawnSequence.Count;
                 index++)
            {
                Add(spawned, spawnSequence[index].Definition.Id.Value, 1);
            }

            var alive = new Dictionary<string, int>(StringComparer.Ordinal);
            var persistedEnemies =
                new List<SingleCityDefenseCampaignEnemyPersistenceState>();
            for (var index = 0; index < enemies.Count; index++)
            {
                EnemyState enemy = enemies[index];
                if (enemy.CurrentHealth <= 0) continue;
                Add(alive, enemy.Definition.Id.Value, 1);
                persistedEnemies.Add(
                    new SingleCityDefenseCampaignEnemyPersistenceState(
                        enemy.StableId,
                        enemy.Definition.Id.Value,
                        enemy.SpawnOrder,
                        enemy.X,
                        enemy.Z,
                        enemy.CurrentHealth,
                        0f,
                        enemy.AttackDamageRemainder,
                        enemy.TargetStableId));
            }

            var defeated = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in spawned)
            {
                alive.TryGetValue(pair.Key, out int aliveCount);
                defeated[pair.Key] = pair.Value - aliveCount;
            }

            var anchors =
                new List<SingleCityDefenseCampaignSpawnAnchorPersistenceState>();
            foreach (KeyValuePair<CampaignSpawnDirection,
                     SingleCityDefenseCampaignSpawnAnchorPersistenceState> pair
                     in frozenSpawnAnchors)
            {
                anchors.Add(pair.Value);
            }

            return new SingleCityDefenseCampaignPersistenceState(
                definition.Id,
                phase,
                currentWave == null ? 0 : currentWave.Number,
                (float)warningRemainingSeconds,
                (float)spawnClockSeconds,
                (float)fixedStepAccumulatorSeconds,
                nextSpawnIndex,
                coreCurrentHealth,
                result,
                CountStates(planned),
                CountStates(spawned),
                CountStates(defeated),
                anchors,
                persistedEnemies,
                new SingleCityDefenseCampaignStatisticsPersistenceState(
                    capturedStatistics.ElapsedRuleSeconds,
                    TotalSpawnedEnemyCount,
                    capturedStatistics.TotalKillCount,
                    capturedStatistics.CompletedWaveCount,
                    MetricStates(capturedStatistics.KillsByEnemyId),
                    capturedStatistics.HighestAliveEnemyCount,
                    CityCoreCombatModel.FormalMaximumHealth -
                        coreCurrentHealth,
                    MetricStates(
                        capturedStatistics.DamageByTowerBuildingId),
                    MetricStates(capturedStatistics.KillsByTowerBuildingId),
                    MetricStates(
                        capturedStatistics.ConsumablesSpentByResourceId),
                    capturedStatistics.TotalBuildingLossCount,
                    MetricStates(
                        capturedStatistics.BuildingLossesByBuildingId),
                    capturedStatistics.PartialFromMigration,
                    capturedStatistics.CompletedProductionBatchCount,
                    capturedStatistics.ProductionActiveProgressSeconds,
                    capturedStatistics.ProductionEligibleSeconds,
                    capturedStatistics.CityWasPackedAfterCampaignStart,
                    capturedStatistics.DevelopmentModifierUsed),
                InjectedPersistenceStates());
        }

        public bool TryPrepareRestore(
            SingleCityDefenseCampaignPersistenceState state,
            out SingleCityDefenseCampaignRestorePlan plan,
            out string error)
        {
            plan = null;
            if (!TryBuildRestoreCandidate(state, out RestoreCandidate candidate,
                    out error))
            {
                return false;
            }

            plan = new SingleCityDefenseCampaignRestorePlan(
                this,
                persistenceGeneration,
                ComputePersistenceFingerprint(CaptureForPersistence()),
                candidate);
            error = null;
            return true;
        }

        public bool TryCommitRestore(
            SingleCityDefenseCampaignRestorePlan plan,
            out string error)
        {
            if (plan == null)
            {
                error = "Restore plan is required.";
                return false;
            }
            if (!ReferenceEquals(plan.Owner, this))
            {
                error = "Restore plan belongs to another campaign model.";
                return false;
            }
            if (plan.Consumed)
            {
                error = "Restore plan has already been consumed.";
                return false;
            }
            if (plan.ExpectedGeneration != persistenceGeneration ||
                plan.ExpectedFingerprint != ComputePersistenceFingerprint(
                    CaptureForPersistence()))
            {
                error = "Campaign changed after restore preparation.";
                return false;
            }
            if (!(plan.Candidate is RestoreCandidate candidate))
            {
                error = "Restore plan is invalid.";
                return false;
            }

            enemies.Clear();
            enemies.AddRange(candidate.Enemies);
            spawnSequence.Clear();
            spawnSequence.AddRange(candidate.SpawnSequence);
            injectedReinforcements.Clear();
            foreach (KeyValuePair<string, WaveEntry[]> pair in
                     candidate.InjectedReinforcements)
                injectedReinforcements.Add(pair.Key, pair.Value);
            frozenSpawnAnchors.Clear();
            foreach (KeyValuePair<CampaignSpawnDirection,
                     SingleCityDefenseCampaignSpawnAnchorPersistenceState> pair
                     in candidate.FrozenSpawnAnchors)
            {
                frozenSpawnAnchors.Add(pair.Key, pair.Value);
            }

            currentWave = candidate.CurrentWave;
            phase = candidate.Phase;
            result = candidate.Result;
            campaignTriggered = candidate.CampaignTriggered;
            warningRemainingSeconds = candidate.WarningRemainingSeconds;
            spawnClockSeconds = candidate.SpawnClockSeconds;
            fixedStepAccumulatorSeconds =
                candidate.FixedStepAccumulatorSeconds;
            nextSpawnIndex = candidate.NextSpawnIndex;
            coreCurrentHealth = candidate.CoreCurrentHealth;
            var restoredStatistics = new SessionStatisticsSnapshot(
                (float)candidate.ElapsedRuleSeconds,
                candidate.CompletedWaveCount,
                0,
                SessionMetrics(candidate.KillsByEnemyId),
                SessionMetrics(candidate.DamageByTowerBuildingId),
                SessionMetrics(candidate.KillsByTowerBuildingId),
                SessionMetrics(candidate.ConsumablesSpentByResourceId),
                0,
                SessionMetrics(candidate.BuildingLossesByBuildingId),
                candidate.HighestAliveEnemyCount,
                candidate.CompletedProductionBatchCount,
                candidate.ProductionActiveProgressSeconds,
                candidate.ProductionEligibleSeconds,
                candidate.CityWasPackedAfterCampaignStart,
                candidate.DevelopmentModifierUsed,
                candidate.PartialFromMigration,
                candidate.Result != SingleCityDefenseCampaignResult.None,
                0f,
                0,
                0,
                false);
            if (!statistics.TryRestore(restoredStatistics, out error))
                return false;
            if (candidate.Result != SingleCityDefenseCampaignResult.None)
            {
                unchecked { terminalRevision++; }
            }
            plan.Consumed = true;
            persistenceGeneration++;
            error = null;
            return true;
        }

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
            persistenceGeneration++;
        }

        public void ApplyElixirCoreHealth(
            int healing,
            int backlashDamage)
        {
            if (coreCurrentHealth <= 0) return;
            int before = coreCurrentHealth;
            coreCurrentHealth = Math.Min(
                CityCoreCombatModel.FormalMaximumHealth,
                coreCurrentHealth + Math.Max(0, healing));
            coreCurrentHealth -= Math.Min(
                coreCurrentHealth,
                Math.Max(0, backlashDamage));
            if (coreCurrentHealth != before) persistenceGeneration++;
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
                definition.Waves.Count == 0)
            {
                return false;
            }

            campaignTriggered = true;
            BeginWave(0);
            persistenceGeneration++;
            return true;
        }

        public bool TryStartAfterExternalWarning()
        {
            if (ReferenceEquals(definition, CampaignWaveCatalog.Default) ||
                campaignTriggered || definition.Waves.Count == 0)
                return false;
            campaignTriggered = true;
            currentWave = definition.Waves[0];
            warningRemainingSeconds = 0d;
            spawnClockSeconds = 0d;
            nextSpawnIndex = 0;
            enemies.Clear();
            BuildSpawnSequence(currentWave);
            FreezeSpawnAnchors(currentWave);
            phase = SingleCityDefenseCampaignPhase.SpawningAndCombat;
            persistenceGeneration++;
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
            persistenceGeneration++;
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
            EnemyDefeated?.Invoke(enemy.StableId, enemy.Definition.Id.Value);
            persistenceGeneration++;
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
                EnemyDefeated?.Invoke(
                    enemy.StableId,
                    enemy.Definition.Id.Value);
            }
            persistenceGeneration++;
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
                EnemyDefeated?.Invoke(
                    enemy.StableId,
                    enemy.Definition.Id.Value);
            }
            persistenceGeneration++;
            return applied;
        }

        internal void SuppressMechanicalMovementNextStep(
            string stableEnemyId,
            string towerBuildingId)
        {
            if (!string.Equals(
                    towerBuildingId,
                    BuildingCatalog.EmpTower.Id.Value,
                    StringComparison.Ordinal)) return;
            EnemyState enemy = FindAliveEnemy(stableEnemyId);
            if (enemy?.Definition.IsMechanical == true)
                movementSuppressedNextStep.Add(enemy.StableId);
        }

        public int ApplyCoreDamage(int damage)
        {
            if (IsTerminal || damage <= 0) return 0;
            int applied = Math.Min(coreCurrentHealth, damage);
            coreCurrentHealth -= applied;
            if (coreCurrentHealth == 0)
            {
                CommitTerminalResult(
                    SingleCityDefenseCampaignResult.Defeat);
            }
            persistenceGeneration++;
            return applied;
        }

        public void RegisterConsumableSpent(string resourceId, int amount)
        {
            if (IsTerminal) return;
            if (string.IsNullOrWhiteSpace(resourceId) || amount <= 0) return;
            statistics.RegisterConsumableSpent(resourceId, amount);
            persistenceGeneration++;
        }

        public void RegisterBuildingLoss(string buildingId)
        {
            if (IsTerminal || FindBuildingDefinition(buildingId) == null)
                return;
            statistics.RegisterBuildingLoss(buildingId, 1);
            persistenceGeneration++;
        }

        public void RegisterProductionStatistics(
            int completedBatchCount,
            float activeProgressSeconds,
            float eligibleSeconds)
        {
            if (IsTerminal) return;
            statistics.RegisterCompletedProductionBatches(
                completedBatchCount);
            statistics.RegisterProductionTime(
                activeProgressSeconds,
                eligibleSeconds);
            persistenceGeneration++;
        }

        public void MarkCityPackedAfterCampaignStart()
        {
            if (IsTerminal) return;
            statistics.MarkCityPackedAfterCampaignStart();
            persistenceGeneration++;
        }

        public void MarkDevelopmentModifierUsed()
        {
            if (IsTerminal) return;
            statistics.MarkDevelopmentModifierUsed();
            persistenceGeneration++;
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

        private SingleCityDefenseCampaignResult ResolveCurrentTerminalResult(
            int currentWaveNumber,
            bool allPlannedEnemiesSpawned,
            int aliveEnemyCount,
            int currentCoreHealth)
        {
            if (currentCoreHealth <= 0)
                return SingleCityDefenseCampaignResult.Defeat;
            return definition.Waves.Count > 0 &&
                currentWaveNumber ==
                    definition.Waves[definition.Waves.Count - 1].Number &&
                allPlannedEnemiesSpawned && aliveEnemyCount <= 0
                    ? SingleCityDefenseCampaignResult.Victory
                    : SingleCityDefenseCampaignResult.None;
        }

        private void Step(
            float deltaSeconds,
            Func<DefenseBuildingCombatTarget[]> buildingTargetProvider,
            Func<string, string, int, int> applyBuildingDamage,
            bool advanceEnemyCombat)
        {
            statistics.AdvanceRuleTime(deltaSeconds);
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

                bool movementSuppressed =
                    movementSuppressedNextStep.Remove(enemy.StableId);
                if (movementSuppressed
                        ? !IsWithinAttackRange(enemy, targetX, targetZ)
                        : !MoveEnemyIntoRange(
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

        private static bool IsWithinAttackRange(
            EnemyState enemy,
            float targetX,
            float targetZ)
        {
            float offsetX = targetX - enemy.X;
            float offsetZ = targetZ - enemy.Z;
            float range = Math.Max(0f, enemy.Definition.AttackRange);
            return offsetX * offsetX + offsetZ * offsetZ <=
                range * range + (float)StepEpsilon;
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
            string stableId = EnemyStableId(currentWave.Number, spawnOrder);
            enemies.Add(new EnemyState(
                stableId,
                spawn.Definition,
                spawnOrder,
                x,
                z));
            statistics.ObserveAliveEnemyCount(AliveEnemyCount);
        }

        private void ResolveSpawnPosition(
            CampaignSpawnDirection direction,
            out float x,
            out float z)
        {
            if (frozenSpawnAnchors.TryGetValue(
                    direction,
                    out SingleCityDefenseCampaignSpawnAnchorPersistenceState
                        anchor))
            {
                x = anchor.X;
                z = anchor.Z;
                return;
            }
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

            SingleCityDefenseCampaignResult terminal =
                ResolveCurrentTerminalResult(
                currentWave.Number,
                allPlannedEnemiesSpawned: true,
                aliveEnemyCount: 0,
                coreCurrentHealth);
            if (terminal != SingleCityDefenseCampaignResult.None)
            {
                statistics.RegisterCompletedWaves(1);
                CommitTerminalResult(terminal);
                return;
            }

            statistics.RegisterCompletedWaves(1);
            BeginWave(currentWave.Number);
        }

        private void BeginWave(int catalogIndex)
        {
            if (catalogIndex < 0 ||
                catalogIndex >= definition.Waves.Count)
            {
                return;
            }

            currentWave = definition.Waves[catalogIndex];
            warningRemainingSeconds = currentWave.WarningSeconds;
            spawnClockSeconds = 0d;
            nextSpawnIndex = 0;
            enemies.Clear();
            BuildSpawnSequence(currentWave);
            FreezeSpawnAnchors(currentWave);
            phase = SingleCityDefenseCampaignPhase.Warning;
            WaveWarningStarted?.Invoke(currentWave.Number);
        }

        private bool CommitTerminalResult(
            SingleCityDefenseCampaignResult terminal)
        {
            if (result != SingleCityDefenseCampaignResult.None ||
                terminal == SingleCityDefenseCampaignResult.None)
            {
                return false;
            }

            result = terminal;
            phase = terminal == SingleCityDefenseCampaignResult.Defeat
                ? SingleCityDefenseCampaignPhase.Defeat
                : SingleCityDefenseCampaignPhase.Victory;
            statistics.FreezeAtTerminal();
            unchecked { terminalRevision++; }
            TerminalCommitted?.Invoke(terminal);
            return true;
        }

        private void FreezeSpawnAnchors(CampaignWaveDefinition wave)
        {
            frozenSpawnAnchors.Clear();
            for (var index = 0; index < wave.Directions.Count; index++)
            {
                CampaignSpawnDirection direction = wave.Directions[index];
                if (frozenSpawnAnchors.ContainsKey(direction)) continue;
                float x = coreX;
                float z = coreZ;
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
                frozenSpawnAnchors.Add(
                    direction,
                    new SingleCityDefenseCampaignSpawnAnchorPersistenceState(
                        direction,
                        x,
                        z));
            }
        }

        private void BuildSpawnSequence(CampaignWaveDefinition wave)
        {
            spawnSequence.Clear();
            BuildSpawnSequenceFor(wave, spawnSequence);
        }

        private static void BuildSpawnSequenceFor(
            CampaignWaveDefinition wave,
            IList<SpawnDefinition> destination)
        {
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
                        : wave.Directions[destination.Count %
                            wave.Directions.Count];
                    destination.Add(new SpawnDefinition(
                        definition,
                        direction));
                    added = true;
                }
            }
            while (added);
        }

        private static void AppendSpawnEntries(
            CampaignWaveDefinition wave,
            IReadOnlyList<WaveEntry> entries,
            IList<SpawnDefinition> destination)
        {
            for (var entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            for (var count = 0; count < entries[entryIndex].Count; count++)
            {
                EnemyDefinition enemy = FindEnemyDefinition(
                    entries[entryIndex].Archetype);
                CampaignSpawnDirection direction = wave.Directions.Count == 0
                    ? CampaignSpawnDirection.East
                    : wave.Directions[destination.Count % wave.Directions.Count];
                destination.Add(new SpawnDefinition(enemy, direction));
            }
        }

        private SingleCityDefenseInjectedReinforcementPersistenceState[]
            InjectedPersistenceStates()
        {
            var result = new
                SingleCityDefenseInjectedReinforcementPersistenceState[
                    injectedReinforcements.Count];
            var index = 0;
            foreach (KeyValuePair<string, WaveEntry[]> pair in
                     injectedReinforcements)
            {
                result[index++] =
                    new SingleCityDefenseInjectedReinforcementPersistenceState(
                        pair.Key,
                        pair.Value);
            }
            return result;
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
            SessionStatisticsSnapshot capturedStatistics =
                statistics.Capture();
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
                currentWave == null ? 0 : spawnSequence.Count,
                nextSpawnIndex,
                AliveEnemyCount,
                coreCurrentHealth,
                CityCoreCombatModel.FormalMaximumHealth,
                result,
                snapshots,
                new SingleCityDefenseCampaignStatisticsSnapshot(
                    capturedStatistics.ElapsedRuleSeconds,
                    capturedStatistics.CompletedWaveCount,
                    capturedStatistics.TotalKillCount,
                    MetricDictionary(capturedStatistics.KillsByEnemyId),
                    MetricDictionary(
                        capturedStatistics.DamageByTowerBuildingId),
                    MetricDictionary(
                        capturedStatistics.KillsByTowerBuildingId),
                    MetricDictionary(
                        capturedStatistics.ConsumablesSpentByResourceId),
                    capturedStatistics.TotalBuildingLossCount,
                    coreCurrentHealth,
                    CityCoreCombatModel.FormalMaximumHealth,
                    capturedStatistics.HighestAliveEnemyCount,
                    capturedStatistics.PartialFromMigration));
        }

        private bool TryBuildRestoreCandidate(
            SingleCityDefenseCampaignPersistenceState state,
            out RestoreCandidate candidate,
            out string error)
        {
            candidate = null;
            if (state == null)
                return Fail("Campaign persistence state is required.", out error);
            if (!string.Equals(
                    state.CampaignId,
                    definition.Id,
                    StringComparison.Ordinal))
            {
                return Fail("Campaign id is not supported.", out error);
            }
            if (!Enum.IsDefined(typeof(SingleCityDefenseCampaignPhase),
                    state.Phase) ||
                !Enum.IsDefined(typeof(SingleCityDefenseCampaignResult),
                    state.Result))
            {
                return Fail("Campaign phase or result is invalid.", out error);
            }
            if (!IsFiniteNonNegative(state.WarningRemainingSeconds) ||
                !IsFiniteNonNegative(state.SpawnClockSeconds) ||
                !IsFiniteNonNegative(state.FixedStepAccumulatorSeconds) ||
                state.FixedStepAccumulatorSeconds >= FormalFixedStepSeconds ||
                state.NextEnemyOrdinal < 0 ||
                state.CoreCurrentHealth < 0 ||
                state.CoreCurrentHealth >
                    CityCoreCombatModel.FormalMaximumHealth)
            {
                return Fail("Campaign clocks, ordinal, or core health are invalid.",
                    out error);
            }

            bool isIdle = state.Phase == SingleCityDefenseCampaignPhase.Idle;
            if (isIdle)
            {
                if (state.CurrentWaveNumber != 0 ||
                    state.Result != SingleCityDefenseCampaignResult.None ||
                    state.NextEnemyOrdinal != 0)
                {
                    return Fail("Idle campaign state is inconsistent.", out error);
                }
            }
            else if (state.CurrentWaveNumber < 1 ||
                     state.CurrentWaveNumber > definition.Waves.Count)
            {
                return Fail("Campaign wave number is invalid.", out error);
            }

            bool isVictory =
                state.Phase == SingleCityDefenseCampaignPhase.Victory;
            bool isDefeat =
                state.Phase == SingleCityDefenseCampaignPhase.Defeat;
            if (isVictory !=
                    (state.Result ==
                        SingleCityDefenseCampaignResult.Victory) ||
                isDefeat !=
                    (state.Result ==
                        SingleCityDefenseCampaignResult.Defeat) ||
                (!isVictory && !isDefeat) !=
                    (state.Result == SingleCityDefenseCampaignResult.None))
            {
                return Fail("Campaign terminal result is inconsistent.", out error);
            }
            if (isDefeat != (state.CoreCurrentHealth == 0))
            {
                return Fail(
                    "City core health does not match the terminal phase.",
                    out error);
            }

            CampaignWaveDefinition wave = isIdle
                ? null
                : definition.Waves[state.CurrentWaveNumber - 1];
            var sequence = new List<SpawnDefinition>();
            if (wave != null) BuildSpawnSequenceFor(wave, sequence);
            var restoredInjections = new SortedDictionary<string, WaveEntry[]>(
                StringComparer.Ordinal);
            if (state.InjectedReinforcements == null)
                return Fail("Injected reinforcements are required.", out error);
            string previousInjectionId = null;
            for (var injectionIndex = 0;
                 injectionIndex < state.InjectedReinforcements.Count;
                 injectionIndex++)
            {
                SingleCityDefenseInjectedReinforcementPersistenceState injected =
                    state.InjectedReinforcements[injectionIndex];
                if (injected == null ||
                    string.IsNullOrWhiteSpace(injected.StableEventId) ||
                    previousInjectionId != null && string.CompareOrdinal(
                        previousInjectionId, injected.StableEventId) >= 0 ||
                    injected.Entries == null || injected.Entries.Count == 0)
                    return Fail("Injected reinforcement record is invalid.",
                        out error);
                var entries = new WaveEntry[injected.Entries.Count];
                for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    entries[entryIndex] = injected.Entries[entryIndex];
                    if (entries[entryIndex].Count <= 0 || FindEnemyDefinition(
                            entries[entryIndex].Archetype) == null)
                        return Fail("Injected reinforcement entry is invalid.",
                            out error);
                }
                restoredInjections.Add(injected.StableEventId, entries);
                AppendSpawnEntries(wave, entries, sequence);
                previousInjectionId = injected.StableEventId;
            }
            if (state.NextEnemyOrdinal > sequence.Count)
            {
                return Fail("Next enemy ordinal exceeds the wave plan.", out error);
            }
            if (wave != null &&
                state.WarningRemainingSeconds > wave.WarningSeconds + .0001f)
            {
                return Fail("Warning clock exceeds the wave definition.", out error);
            }
            if (wave != null && sequence.Count > 0 &&
                state.SpawnClockSeconds >
                    wave.SpawnSeconds / sequence.Count + .0001f)
            {
                return Fail("Spawn clock exceeds one spawn cadence.", out error);
            }
            if (state.Phase == SingleCityDefenseCampaignPhase.Warning &&
                (state.NextEnemyOrdinal != 0 ||
                 state.SpawnClockSeconds > .0001f))
            {
                return Fail("Warning phase cannot contain spawned enemies.",
                    out error);
            }
            if ((state.Phase == SingleCityDefenseCampaignPhase.CombatCleanup ||
                 state.Phase == SingleCityDefenseCampaignPhase.Victory) &&
                state.NextEnemyOrdinal != sequence.Count)
            {
                return Fail("Cleanup or victory requires a complete spawn plan.",
                    out error);
            }

            if (!TryReadEnemyCounts(
                    state.PlannedEnemyCountsByEnemyId,
                    out Dictionary<string, int> planned,
                    out error) ||
                !TryReadEnemyCounts(
                    state.SpawnedEnemyCountsByEnemyId,
                    out Dictionary<string, int> spawned,
                    out error) ||
                !TryReadEnemyCounts(
                    state.DefeatedEnemyCountsByEnemyId,
                    out Dictionary<string, int> defeated,
                    out error))
            {
                return false;
            }

            var expectedPlanned = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (var index = 0; index < sequence.Count; index++)
                Add(expectedPlanned, sequence[index].Definition.Id.Value, 1);
            var expectedSpawned = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (var index = 0; index < state.NextEnemyOrdinal; index++)
                Add(expectedSpawned, sequence[index].Definition.Id.Value, 1);
            if (!DictionariesEqual(planned, expectedPlanned) ||
                !DictionariesEqual(spawned, expectedSpawned))
            {
                return Fail("Persisted enemy counts do not match the wave plan.",
                    out error);
            }

            if (!TryReadAnchors(
                    state.FrozenSpawnAnchors,
                    wave,
                    out Dictionary<CampaignSpawnDirection,
                        SingleCityDefenseCampaignSpawnAnchorPersistenceState>
                        anchors,
                    out error))
            {
                return false;
            }

            var restoredEnemies = new List<EnemyState>();
            var aliveByDefinition = new Dictionary<string, int>(
                StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var spawnOrders = new HashSet<int>();
            if (state.Enemies == null)
                return Fail("Enemy collection is required.", out error);
            for (var index = 0; index < state.Enemies.Count; index++)
            {
                SingleCityDefenseCampaignEnemyPersistenceState persisted =
                    state.Enemies[index];
                if (persisted == null ||
                    !stableIds.Add(persisted.StableId) ||
                    !spawnOrders.Add(persisted.SpawnOrder) ||
                    persisted.SpawnOrder < 0 ||
                    persisted.SpawnOrder >= state.NextEnemyOrdinal ||
                    !IsFinite(persisted.X) || !IsFinite(persisted.Z) ||
                    !IsFinite(persisted.MovementRemainder) ||
                    Math.Abs(persisted.MovementRemainder) > .000001f ||
                    !IsFinite(persisted.AttackDamageRemainder) ||
                    persisted.AttackDamageRemainder < 0f ||
                    persisted.AttackDamageRemainder >= 1f ||
                    (!string.IsNullOrEmpty(persisted.TargetStableId) &&
                     string.IsNullOrWhiteSpace(persisted.TargetStableId)))
                {
                    return Fail("Persisted enemy state is invalid or duplicated.",
                        out error);
                }
                EnemyDefinition definition = FindEnemyDefinition(
                    persisted.EnemyDefinitionId);
                SpawnDefinition expected = sequence[persisted.SpawnOrder];
                string expectedStableId = EnemyStableId(
                    wave.Number,
                    persisted.SpawnOrder);
                if (definition == null ||
                    !string.Equals(definition.Id.Value,
                        expected.Definition.Id.Value,
                        StringComparison.Ordinal) ||
                    !string.Equals(persisted.StableId,
                        expectedStableId,
                        StringComparison.Ordinal) ||
                    persisted.CurrentHealth <= 0 ||
                    persisted.CurrentHealth > definition.MaximumHealth)
                {
                    return Fail("Persisted enemy does not match its wave slot.",
                        out error);
                }
                restoredEnemies.Add(new EnemyState(
                    persisted.StableId,
                    definition,
                    persisted.SpawnOrder,
                    persisted.X,
                    persisted.Z,
                    persisted.CurrentHealth,
                    persisted.AttackDamageRemainder,
                    persisted.TargetStableId));
                Add(aliveByDefinition, definition.Id.Value, 1);
            }

            var expectedDefeated = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, int> pair in spawned)
            {
                aliveByDefinition.TryGetValue(pair.Key, out int aliveCount);
                if (aliveCount > pair.Value)
                    return Fail("Alive enemy count exceeds spawned count.",
                        out error);
                expectedDefeated[pair.Key] = pair.Value - aliveCount;
            }
            if (!DictionariesEqual(defeated, expectedDefeated))
                return Fail("Defeated enemy counts are inconsistent.", out error);
            if (state.Phase == SingleCityDefenseCampaignPhase.Victory &&
                restoredEnemies.Count != 0)
            {
                return Fail("Victory cannot retain living enemies.", out error);
            }

            if (!TryReadStatistics(
                    state,
                    restoredEnemies.Count,
                    out Dictionary<string, int> restoredKills,
                    out Dictionary<string, int> restoredDamage,
                    out Dictionary<string, int> restoredTowerKills,
                    out Dictionary<string, int> restoredConsumables,
                    out Dictionary<string, int> restoredBuildingLosses,
                    out error))
            {
                return false;
            }

            SingleCityDefenseCampaignStatisticsPersistenceState statistics =
                state.Statistics;
            candidate = new RestoreCandidate(
                wave,
                state.Phase,
                state.Result,
                !isIdle,
                state.WarningRemainingSeconds,
                state.SpawnClockSeconds,
                state.FixedStepAccumulatorSeconds,
                statistics.ElapsedRuleSeconds,
                state.NextEnemyOrdinal,
                statistics.CompletedWaveCount,
                statistics.HighestAliveEnemyCount,
                statistics.PartialFromMigration,
                statistics.CompletedProductionBatchCount,
                statistics.ProductionActiveProgressSeconds,
                statistics.ProductionEligibleSeconds,
                statistics.CityWasPackedAfterCampaignStart,
                statistics.DevelopmentModifierUsed,
                state.CoreCurrentHealth,
                sequence,
                restoredEnemies,
                anchors,
                restoredKills,
                restoredDamage,
                restoredTowerKills,
                restoredConsumables,
                restoredBuildingLosses,
                restoredInjections);
            error = null;
            return true;
        }

        private bool TryReadStatistics(
            SingleCityDefenseCampaignPersistenceState state,
            int aliveEnemyCount,
            out Dictionary<string, int> restoredKills,
            out Dictionary<string, int> restoredDamage,
            out Dictionary<string, int> restoredTowerKills,
            out Dictionary<string, int> restoredConsumables,
            out Dictionary<string, int> restoredBuildingLosses,
            out string error)
        {
            restoredKills = null;
            restoredDamage = null;
            restoredTowerKills = null;
            restoredConsumables = null;
            restoredBuildingLosses = null;
            SingleCityDefenseCampaignStatisticsPersistenceState statistics =
                state.Statistics;
            if (statistics == null ||
                !IsFiniteNonNegative(statistics.ElapsedRuleSeconds) ||
                statistics.SpawnedEnemyCount < 0 ||
                statistics.DefeatedEnemyCount < 0 ||
                statistics.CompletedWaveCount < 0 ||
                statistics.BuildingLossCount < 0 ||
                statistics.CoreDamageTaken < 0 ||
                statistics.CompletedProductionBatchCount < 0 ||
                !IsFiniteNonNegative(
                    statistics.ProductionActiveProgressSeconds) ||
                !IsFiniteNonNegative(
                    statistics.ProductionEligibleSeconds) ||
                statistics.ProductionActiveProgressSeconds >
                    statistics.ProductionEligibleSeconds ||
                statistics.HighestAliveEnemyCount < aliveEnemyCount)
            {
                return Fail("Campaign statistics are invalid.", out error);
            }
            int expectedCompleted = state.Phase ==
                SingleCityDefenseCampaignPhase.Victory
                ? definition.Waves.Count
                : Math.Max(0, state.CurrentWaveNumber - 1);
            int expectedSpawned = SpawnedBeforeWave(state.CurrentWaveNumber) +
                state.NextEnemyOrdinal;
            if (statistics.CompletedWaveCount != expectedCompleted ||
                statistics.SpawnedEnemyCount != expectedSpawned ||
                statistics.CoreDamageTaken !=
                    CityCoreCombatModel.FormalMaximumHealth -
                        state.CoreCurrentHealth)
            {
                return Fail("Campaign statistics do not match campaign truth.",
                    out error);
            }
            if (!TryReadMetrics(statistics.KillsByEnemyId,
                    MetricKind.Enemy,
                    out restoredKills,
                    out error) ||
                !TryReadMetrics(statistics.DamageByTowerBuildingId,
                    MetricKind.Tower,
                    out restoredDamage,
                    out error) ||
                !TryReadMetrics(statistics.KillsByTowerBuildingId,
                    MetricKind.Tower,
                    out restoredTowerKills,
                    out error) ||
                !TryReadMetrics(statistics.ConsumablesSpentByResourceId,
                    MetricKind.Resource,
                    out restoredConsumables,
                    out error) ||
                !TryReadMetrics(statistics.BuildingLossesByBuildingId,
                    MetricKind.Building,
                    out restoredBuildingLosses,
                    out error))
            {
                return false;
            }
            if (Sum(restoredKills) != statistics.DefeatedEnemyCount)
                return Fail("Defeated enemy statistics are inconsistent.",
                    out error);
            if (!statistics.PartialFromMigration &&
                Sum(restoredTowerKills) != statistics.DefeatedEnemyCount)
            {
                return Fail("Tower kill statistics are inconsistent.",
                    out error);
            }
            if (Sum(restoredBuildingLosses) != statistics.BuildingLossCount)
                return Fail("Building loss statistics are inconsistent.",
                    out error);
            error = null;
            return true;
        }

        private static bool TryReadEnemyCounts(
            IReadOnlyList<SingleCityDefenseCampaignEnemyCountPersistenceState>
                source,
            out Dictionary<string, int> result,
            out string error)
        {
            result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null)
                return Fail("Enemy count collection is required.", out error);
            for (var index = 0; index < source.Count; index++)
            {
                SingleCityDefenseCampaignEnemyCountPersistenceState item =
                    source[index];
                if (item == null || item.Count < 0 ||
                    FindEnemyDefinition(item.EnemyDefinitionId) == null ||
                    result.ContainsKey(item.EnemyDefinitionId))
                {
                    return Fail("Enemy count collection is invalid.", out error);
                }
                result.Add(item.EnemyDefinitionId, item.Count);
            }
            error = null;
            return true;
        }

        private static bool TryReadAnchors(
            IReadOnlyList<SingleCityDefenseCampaignSpawnAnchorPersistenceState>
                source,
            CampaignWaveDefinition wave,
            out Dictionary<CampaignSpawnDirection,
                SingleCityDefenseCampaignSpawnAnchorPersistenceState> result,
            out string error)
        {
            result = new Dictionary<CampaignSpawnDirection,
                SingleCityDefenseCampaignSpawnAnchorPersistenceState>();
            if (source == null)
                return Fail("Spawn anchor collection is required.", out error);
            for (var index = 0; index < source.Count; index++)
            {
                SingleCityDefenseCampaignSpawnAnchorPersistenceState item =
                    source[index];
                if (item == null ||
                    !Enum.IsDefined(typeof(CampaignSpawnDirection),
                        item.Direction) ||
                    !IsFinite(item.X) || !IsFinite(item.Z) ||
                    result.ContainsKey(item.Direction))
                {
                    return Fail("Spawn anchor collection is invalid.", out error);
                }
                result.Add(
                    item.Direction,
                    new SingleCityDefenseCampaignSpawnAnchorPersistenceState(
                        item.Direction,
                        item.X,
                        item.Z));
            }
            var expected = new HashSet<CampaignSpawnDirection>();
            if (wave != null)
            {
                for (var index = 0; index < wave.Directions.Count; index++)
                    expected.Add(wave.Directions[index]);
            }
            if (result.Count != expected.Count)
                return Fail("Spawn anchors do not match the current wave.",
                    out error);
            foreach (CampaignSpawnDirection direction in expected)
            {
                if (!result.ContainsKey(direction))
                    return Fail("A current-wave spawn anchor is missing.",
                        out error);
            }
            error = null;
            return true;
        }

        private static bool TryReadMetrics(
            IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState>
                source,
            MetricKind kind,
            out Dictionary<string, int> result,
            out string error)
        {
            result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null)
                return Fail("Statistics metric collection is required.",
                    out error);
            for (var index = 0; index < source.Count; index++)
            {
                SingleCityDefenseCampaignMetricPersistenceState item =
                    source[index];
                bool known = item != null && item.Amount > 0 &&
                    !string.IsNullOrWhiteSpace(item.StableId);
                if (known && kind == MetricKind.Enemy)
                    known = FindEnemyDefinition(item.StableId) != null;
                else if (known && kind == MetricKind.Tower)
                    known = DefenseTowerCatalog.For(item.StableId) != null;
                else if (known && kind == MetricKind.Building)
                    known = FindBuildingDefinition(item.StableId) != null;
                if (!known || result.ContainsKey(item.StableId))
                {
                    return Fail("Statistics metric collection is invalid.",
                        out error);
                }
                result.Add(item.StableId, item.Amount);
            }
            error = null;
            return true;
        }

        private static IReadOnlyList<
            SingleCityDefenseCampaignEnemyCountPersistenceState> CountStates(
                IReadOnlyDictionary<string, int> source)
        {
            var result = new List<
                SingleCityDefenseCampaignEnemyCountPersistenceState>();
            if (source != null)
            {
                foreach (KeyValuePair<string, int> pair in source)
                {
                    result.Add(
                        new SingleCityDefenseCampaignEnemyCountPersistenceState(
                            pair.Key,
                            pair.Value));
                }
            }
            return result;
        }

        private static IReadOnlyList<
            SingleCityDefenseCampaignMetricPersistenceState> MetricStates(
                IReadOnlyDictionary<string, int> source)
        {
            var result = new List<
                SingleCityDefenseCampaignMetricPersistenceState>();
            if (source != null)
            {
                foreach (KeyValuePair<string, int> pair in source)
                {
                    result.Add(
                        new SingleCityDefenseCampaignMetricPersistenceState(
                            pair.Key,
                            pair.Value));
                }
            }
            return result;
        }

        private static IReadOnlyList<
            SingleCityDefenseCampaignMetricPersistenceState> MetricStates(
                IReadOnlyList<SessionStatisticsMetric> source)
        {
            var result = new List<
                SingleCityDefenseCampaignMetricPersistenceState>();
            if (source == null) return result;
            for (var index = 0; index < source.Count; index++)
            {
                SessionStatisticsMetric item = source[index];
                if (item == null) continue;
                result.Add(
                    new SingleCityDefenseCampaignMetricPersistenceState(
                        item.StableId,
                        item.Amount));
            }
            return result;
        }

        private static SessionStatisticsMetric[] SessionMetrics(
            IReadOnlyDictionary<string, int> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<SessionStatisticsMetric>();
            var keys = new List<string>(source.Keys);
            keys.Sort(StringComparer.Ordinal);
            var result = new SessionStatisticsMetric[keys.Count];
            for (var index = 0; index < keys.Count; index++)
            {
                string key = keys[index];
                result[index] = new SessionStatisticsMetric(
                    key,
                    source[key]);
            }
            return result;
        }

        private static IReadOnlyDictionary<string, int> MetricDictionary(
            IReadOnlyList<SessionStatisticsMetric> source)
        {
            var result = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            if (source == null) return result;
            for (var index = 0; index < source.Count; index++)
            {
                SessionStatisticsMetric item = source[index];
                if (item != null)
                    result[item.StableId] = item.Amount;
            }
            return result;
        }

        private int TotalSpawnedEnemyCount =>
            SpawnedBeforeWave(currentWave == null ? 0 : currentWave.Number) +
            nextSpawnIndex;

        private int SpawnedBeforeWave(int currentWaveNumber)
        {
            int total = 0;
            int count = Math.Max(0, currentWaveNumber - 1);
            for (var index = 0;
                 index < count && index < definition.Waves.Count;
                 index++)
            {
                total += definition.Waves[index].TotalCount;
            }
            return total;
        }

        private static int Sum(IReadOnlyDictionary<string, int> source)
        {
            int total = 0;
            foreach (KeyValuePair<string, int> pair in source)
                total += pair.Value;
            return total;
        }

        private static bool DictionariesEqual(
            IReadOnlyDictionary<string, int> left,
            IReadOnlyDictionary<string, int> right)
        {
            if (left.Count != right.Count) return false;
            foreach (KeyValuePair<string, int> pair in left)
            {
                if (!right.TryGetValue(pair.Key, out int value) ||
                    value != pair.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private static void CopyDictionary(
            IReadOnlyDictionary<string, int> source,
            IDictionary<string, int> destination)
        {
            destination.Clear();
            foreach (KeyValuePair<string, int> pair in source)
                destination.Add(pair.Key, pair.Value);
        }

        private static string EnemyStableId(int waveNumber, int spawnOrder)
        {
            return "campaign.enemy.wave-" +
                waveNumber.ToString("00", CultureInfo.InvariantCulture) +
                "." + spawnOrder.ToString("0000",
                    CultureInfo.InvariantCulture);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private static ulong ComputePersistenceFingerprint(
            SingleCityDefenseCampaignPersistenceState state)
        {
            const ulong offset = 14695981039346656037UL;
            ulong hash = offset;
            Mix(ref hash, state.CampaignId);
            Mix(ref hash, (int)state.Phase);
            Mix(ref hash, state.CurrentWaveNumber);
            Mix(ref hash, state.WarningRemainingSeconds);
            Mix(ref hash, state.SpawnClockSeconds);
            Mix(ref hash, state.FixedStepAccumulatorSeconds);
            Mix(ref hash, state.NextEnemyOrdinal);
            Mix(ref hash, state.CoreCurrentHealth);
            Mix(ref hash, (int)state.Result);
            MixCounts(ref hash, state.PlannedEnemyCountsByEnemyId);
            MixCounts(ref hash, state.SpawnedEnemyCountsByEnemyId);
            MixCounts(ref hash, state.DefeatedEnemyCountsByEnemyId);
            Mix(ref hash, state.FrozenSpawnAnchors.Count);
            for (var index = 0; index < state.FrozenSpawnAnchors.Count; index++)
            {
                SingleCityDefenseCampaignSpawnAnchorPersistenceState item =
                    state.FrozenSpawnAnchors[index];
                Mix(ref hash, (int)item.Direction);
                Mix(ref hash, item.X);
                Mix(ref hash, item.Z);
            }
            Mix(ref hash, state.Enemies.Count);
            for (var index = 0; index < state.Enemies.Count; index++)
            {
                SingleCityDefenseCampaignEnemyPersistenceState item =
                    state.Enemies[index];
                Mix(ref hash, item.StableId);
                Mix(ref hash, item.EnemyDefinitionId);
                Mix(ref hash, item.SpawnOrder);
                Mix(ref hash, item.X);
                Mix(ref hash, item.Z);
                Mix(ref hash, item.CurrentHealth);
                Mix(ref hash, item.MovementRemainder);
                Mix(ref hash, item.AttackDamageRemainder);
                Mix(ref hash, item.TargetStableId);
            }
            SingleCityDefenseCampaignStatisticsPersistenceState statistics =
                state.Statistics;
            if (statistics == null)
            {
                Mix(ref hash, -1);
                return hash;
            }
            Mix(ref hash, statistics.ElapsedRuleSeconds);
            Mix(ref hash, statistics.SpawnedEnemyCount);
            Mix(ref hash, statistics.DefeatedEnemyCount);
            Mix(ref hash, statistics.CompletedWaveCount);
            MixMetrics(ref hash, statistics.KillsByEnemyId);
            Mix(ref hash, statistics.HighestAliveEnemyCount);
            Mix(ref hash, statistics.CoreDamageTaken);
            MixMetrics(ref hash, statistics.DamageByTowerBuildingId);
            MixMetrics(ref hash, statistics.KillsByTowerBuildingId);
            MixMetrics(ref hash, statistics.ConsumablesSpentByResourceId);
            Mix(ref hash, statistics.BuildingLossCount);
            MixMetrics(ref hash, statistics.BuildingLossesByBuildingId);
            Mix(ref hash, statistics.PartialFromMigration ? 1 : 0);
            Mix(ref hash, statistics.CompletedProductionBatchCount);
            Mix(ref hash, statistics.ProductionActiveProgressSeconds);
            Mix(ref hash, statistics.ProductionEligibleSeconds);
            Mix(ref hash,
                statistics.CityWasPackedAfterCampaignStart ? 1 : 0);
            Mix(ref hash, statistics.DevelopmentModifierUsed ? 1 : 0);
            return hash;
        }

        private static void MixCounts(
            ref ulong hash,
            IReadOnlyList<SingleCityDefenseCampaignEnemyCountPersistenceState>
                values)
        {
            Mix(ref hash, values.Count);
            for (var index = 0; index < values.Count; index++)
            {
                Mix(ref hash, values[index].EnemyDefinitionId);
                Mix(ref hash, values[index].Count);
            }
        }

        private static void MixMetrics(
            ref ulong hash,
            IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState>
                values)
        {
            Mix(ref hash, values.Count);
            for (var index = 0; index < values.Count; index++)
            {
                Mix(ref hash, values[index].StableId);
                Mix(ref hash, values[index].Amount);
            }
        }

        private static void Mix(ref ulong hash, float value)
        {
            Mix(ref hash, value.GetHashCode());
        }

        private static void Mix(ref ulong hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                hash *= 1099511628211UL;
            }
        }

        private static void Mix(ref ulong hash, string value)
        {
            if (value == null)
            {
                Mix(ref hash, -1);
                return;
            }
            Mix(ref hash, value.Length);
            unchecked
            {
                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= 1099511628211UL;
                }
            }
        }

        private int TotalKillCount => statistics.Capture().TotalKillCount;

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

        private static BuildingDefinition FindBuildingDefinition(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition definition = BuildingCatalog.All[index];
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
            statistics.RegisterTowerDamage(towerBuildingId, amount);
        }

        private void RegisterKill(
            string enemyDefinitionId,
            string towerBuildingId)
        {
            statistics.RegisterEnemyKill(
                enemyDefinitionId,
                towerBuildingId);
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

            public EnemyState(
                string stableId,
                EnemyDefinition definition,
                int spawnOrder,
                float x,
                float z,
                int currentHealth,
                float attackDamageRemainder,
                string targetStableId)
                : this(stableId, definition, spawnOrder, x, z)
            {
                CurrentHealth = currentHealth;
                AttackDamageRemainder = attackDamageRemainder;
                TargetStableId = targetStableId;
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

        private enum MetricKind
        {
            Enemy,
            Tower,
            Resource,
            Building,
        }

        private sealed class RestoreCandidate
        {
            public RestoreCandidate(
                CampaignWaveDefinition currentWave,
                SingleCityDefenseCampaignPhase phase,
                SingleCityDefenseCampaignResult result,
                bool campaignTriggered,
                double warningRemainingSeconds,
                double spawnClockSeconds,
                double fixedStepAccumulatorSeconds,
                double elapsedRuleSeconds,
                int nextSpawnIndex,
                int completedWaveCount,
                int highestAliveEnemyCount,
                bool partialFromMigration,
                int completedProductionBatchCount,
                float productionActiveProgressSeconds,
                float productionEligibleSeconds,
                bool cityWasPackedAfterCampaignStart,
                bool developmentModifierUsed,
                int coreCurrentHealth,
                List<SpawnDefinition> spawnSequence,
                List<EnemyState> enemies,
                Dictionary<CampaignSpawnDirection,
                    SingleCityDefenseCampaignSpawnAnchorPersistenceState>
                    frozenSpawnAnchors,
                Dictionary<string, int> killsByEnemyId,
                Dictionary<string, int> damageByTowerBuildingId,
                Dictionary<string, int> killsByTowerBuildingId,
                Dictionary<string, int> consumablesSpentByResourceId,
                Dictionary<string, int> buildingLossesByBuildingId,
                SortedDictionary<string, WaveEntry[]> injectedReinforcements)
            {
                CurrentWave = currentWave;
                Phase = phase;
                Result = result;
                CampaignTriggered = campaignTriggered;
                WarningRemainingSeconds = warningRemainingSeconds;
                SpawnClockSeconds = spawnClockSeconds;
                FixedStepAccumulatorSeconds = fixedStepAccumulatorSeconds;
                ElapsedRuleSeconds = elapsedRuleSeconds;
                NextSpawnIndex = nextSpawnIndex;
                CompletedWaveCount = completedWaveCount;
                HighestAliveEnemyCount = highestAliveEnemyCount;
                PartialFromMigration = partialFromMigration;
                CompletedProductionBatchCount =
                    completedProductionBatchCount;
                ProductionActiveProgressSeconds =
                    productionActiveProgressSeconds;
                ProductionEligibleSeconds = productionEligibleSeconds;
                CityWasPackedAfterCampaignStart =
                    cityWasPackedAfterCampaignStart;
                DevelopmentModifierUsed = developmentModifierUsed;
                CoreCurrentHealth = coreCurrentHealth;
                SpawnSequence = spawnSequence;
                Enemies = enemies;
                FrozenSpawnAnchors = frozenSpawnAnchors;
                KillsByEnemyId = killsByEnemyId;
                DamageByTowerBuildingId = damageByTowerBuildingId;
                KillsByTowerBuildingId = killsByTowerBuildingId;
                ConsumablesSpentByResourceId = consumablesSpentByResourceId;
                BuildingLossesByBuildingId = buildingLossesByBuildingId;
                InjectedReinforcements = injectedReinforcements;
            }

            public CampaignWaveDefinition CurrentWave { get; }
            public SingleCityDefenseCampaignPhase Phase { get; }
            public SingleCityDefenseCampaignResult Result { get; }
            public bool CampaignTriggered { get; }
            public double WarningRemainingSeconds { get; }
            public double SpawnClockSeconds { get; }
            public double FixedStepAccumulatorSeconds { get; }
            public double ElapsedRuleSeconds { get; }
            public int NextSpawnIndex { get; }
            public int CompletedWaveCount { get; }
            public int HighestAliveEnemyCount { get; }
            public bool PartialFromMigration { get; }
            public int CompletedProductionBatchCount { get; }
            public float ProductionActiveProgressSeconds { get; }
            public float ProductionEligibleSeconds { get; }
            public bool CityWasPackedAfterCampaignStart { get; }
            public bool DevelopmentModifierUsed { get; }
            public int CoreCurrentHealth { get; }
            public List<SpawnDefinition> SpawnSequence { get; }
            public List<EnemyState> Enemies { get; }
            public Dictionary<CampaignSpawnDirection,
                SingleCityDefenseCampaignSpawnAnchorPersistenceState>
                FrozenSpawnAnchors { get; }
            public Dictionary<string, int> KillsByEnemyId { get; }
            public Dictionary<string, int> DamageByTowerBuildingId { get; }
            public Dictionary<string, int> KillsByTowerBuildingId { get; }
            public Dictionary<string, int> ConsumablesSpentByResourceId
            {
                get;
            }
            public Dictionary<string, int> BuildingLossesByBuildingId
            {
                get;
            }
            public SortedDictionary<string, WaveEntry[]> InjectedReinforcements
            {
                get;
            }
        }
    }
}
