using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Combat;

namespace WasteCity.Defense
{
    public sealed class DefenseEnemyRuntimeSnapshot
    {
        public const string CityCoreTargetName = "城市核心";

        public DefenseEnemyRuntimeSnapshot(
            string stableId,
            int spawnOrder,
            float x,
            float z,
            int currentHealth,
            bool isAttackingCore)
            : this(
                stableId,
                spawnOrder,
                x,
                z,
                currentHealth,
                CityCoreTargetName,
                Distance(x, z, 0f, 0f),
                isAttackingCore)
        {
        }

        public DefenseEnemyRuntimeSnapshot(
            string stableId,
            int spawnOrder,
            float x,
            float z,
            int currentHealth,
            string targetName,
            float distanceToCore,
            bool isAttackingCore)
        {
            StableId = stableId;
            SpawnOrder = spawnOrder;
            X = x;
            Z = z;
            CurrentHealth = currentHealth;
            TargetName = targetName;
            DistanceToCore = distanceToCore;
            IsAttackingCore = isAttackingCore;
        }

        public string StableId { get; }
        public int SpawnOrder { get; }
        public float X { get; }
        public float Z { get; }
        public int CurrentHealth { get; }
        public string TargetName { get; }
        public float DistanceToCore { get; }
        public bool IsAttackingCore { get; }

        private static float Distance(
            float x,
            float z,
            float targetX,
            float targetZ)
        {
            float offsetX = x - targetX;
            float offsetZ = z - targetZ;
            return (float)Math.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
        }
    }

    public sealed class DefenseRuntimeSnapshot
    {
        public DefenseRuntimeSnapshot(
            WavePhase wavePhase,
            float warningRemainingSeconds,
            int spawnedEnemyCount,
            int aliveEnemyCount,
            int coreMaximumHealth,
            int coreCurrentHealth,
            IReadOnlyList<DefenseEnemyRuntimeSnapshot> enemies)
        {
            WavePhase = wavePhase;
            WarningRemainingSeconds = warningRemainingSeconds;
            SpawnedEnemyCount = spawnedEnemyCount;
            AliveEnemyCount = aliveEnemyCount;
            CoreMaximumHealth = coreMaximumHealth;
            CoreCurrentHealth = coreCurrentHealth;
            Enemies = enemies ??
                throw new ArgumentNullException(nameof(enemies));
        }

        public WavePhase WavePhase { get; }
        public float WarningRemainingSeconds { get; }
        public int SpawnedEnemyCount { get; }
        public int AliveEnemyCount { get; }
        public int CoreMaximumHealth { get; }
        public int CoreCurrentHealth { get; }
        public bool IsCoreDestroyed => CoreCurrentHealth <= 0;
        public IReadOnlyList<DefenseEnemyRuntimeSnapshot> Enemies { get; }
    }

    public sealed class TutorialDefenseRuntimeModel
    {
        private const double FixedStepSeconds = .1d;
        private const float FixedStepSecondsFloat = .1f;
        private const float WaveBoundaryEpsilon = .000001f;
        private const float DistanceEpsilon = .0001f;

        private float coreX;
        private float coreZ;
        private float targetCoreX;
        private float targetCoreZ;
        private readonly float spawnX;
        private readonly float spawnZ;
        private readonly WaveDirectorModel waveDirector =
            new WaveDirectorModel();
        private readonly CityCoreCombatModel core =
            new CityCoreCombatModel();
        private readonly List<DefenseEnemyCombatModel> activeEnemies =
            new List<DefenseEnemyCombatModel>();
        private readonly ReadOnlyCollection<DefenseEnemyCombatModel>
            activeEnemiesView;
        private readonly List<EnemyArchetype> spawnedArchetypes =
            new List<EnemyArchetype>();
        private double accumulatorSeconds;
        private bool tutorialTriggered;

        public TutorialDefenseRuntimeModel(
            float coreX,
            float coreZ,
            float spawnX,
            float spawnZ)
        {
            this.coreX = coreX;
            this.coreZ = coreZ;
            targetCoreX = coreX;
            targetCoreZ = coreZ;
            this.spawnX = spawnX;
            this.spawnZ = spawnZ;
            activeEnemiesView = activeEnemies.AsReadOnly();
        }

        public IReadOnlyList<DefenseEnemyCombatModel> ActiveEnemies =>
            activeEnemiesView;

        public CityCoreCombatModel Core => core;

        public WavePhase WavePhase => waveDirector.Phase;
        public float WarningRemainingSeconds =>
            waveDirector.WarningRemaining;
        public int SpawnedEnemyCount => waveDirector.SpawnedCount;
        public int AliveEnemyCount
        {
            get
            {
                int aliveCount = 0;
                for (int index = 0; index < activeEnemies.Count; index++)
                {
                    if (!activeEnemies[index].IsDead)
                        aliveCount++;
                }
                return aliveCount;
            }
        }
        public float CoreX => coreX;
        public float CoreZ => coreZ;

        public DefenseRuntimeSnapshot Snapshot => CreateSnapshot();

        public bool NotifyMachineGunTurretCompleted(string stableId)
        {
            if (tutorialTriggered || string.IsNullOrWhiteSpace(stableId))
                return false;

            tutorialTriggered = waveDirector.Schedule(
                WaveCatalog.Tutorial.Trigger);
            return tutorialTriggered;
        }

        public void SetCorePosition(float x, float z)
        {
            targetCoreX = x;
            targetCoreZ = z;
        }

        public void Advance(float deltaSeconds, bool globallyPaused)
        {
            if (globallyPaused || deltaSeconds <= 0f)
                return;

            accumulatorSeconds += deltaSeconds;
            while (accumulatorSeconds + WaveBoundaryEpsilon >=
                   FixedStepSeconds)
            {
                accumulatorSeconds -= FixedStepSeconds;
                if (accumulatorSeconds < 0d &&
                    accumulatorSeconds > -WaveBoundaryEpsilon)
                {
                    accumulatorSeconds = 0d;
                }
                SimulateFixedStep();
            }
        }

        private void SimulateFixedStep()
        {
            coreX = targetCoreX;
            coreZ = targetCoreZ;
            RegisterAndRemoveDefeatedEnemies();

            int existingEnemyCount = activeEnemies.Count;
            spawnedArchetypes.Clear();

            // WaveDirector owns the warning and cadence truth. Its float
            // accumulator can land microscopically below an exact 5-second
            // boundary after fifty 0.1-second ticks, so this sub-microsecond
            // tolerance keeps the externally visible fixed-step boundary
            // exact without creating a second spawn schedule.
            waveDirector.Tick(
                FixedStepSecondsFloat + WaveBoundaryEpsilon,
                spawnedArchetypes);
            SpawnEnemies(spawnedArchetypes);

            // New enemies are appended above but intentionally skipped here.
            // Their first movement step begins on the next fixed tick, so they
            // never receive time from before their spawn event.
            for (int index = 0; index < existingEnemyCount; index++)
            {
                DefenseEnemyCombatModel enemy = activeEnemies[index];
                if (enemy.IsDead || core.IsDestroyed)
                    continue;

                float movedDistance = enemy.MoveTowards(
                    coreX,
                    coreZ,
                    FixedStepSecondsFloat,
                    EnemyCatalog.Gnawer.AttackRange);
                float movementSeconds = movedDistance /
                    enemy.Definition.MoveSpeed;
                float attackSeconds = Math.Max(
                    0f,
                    FixedStepSecondsFloat - movementSeconds);
                if (attackSeconds > 0f && IsWithinAttackRange(enemy))
                {
                    enemy.TickAttack(
                        attackSeconds,
                        core,
                        globallyPaused: false);
                }
            }

            RegisterAndRemoveDefeatedEnemies();
        }

        private void SpawnEnemies(IReadOnlyList<EnemyArchetype> archetypes)
        {
            for (int index = 0; index < archetypes.Count; index++)
            {
                if (archetypes[index] != EnemyArchetype.Gnawer)
                {
                    throw new InvalidOperationException(
                        "The tutorial defense wave may only spawn gnawers.");
                }

                int spawnOrder = waveDirector.SpawnedCount -
                    archetypes.Count + index;
                activeEnemies.Add(new DefenseEnemyCombatModel(
                    EnemyCatalog.Gnawer.Id.Value + ".tutorial." +
                    spawnOrder.ToString("D3"),
                    EnemyCatalog.Gnawer,
                    spawnX,
                    spawnZ,
                    spawnOrder));
            }
        }

        private void RegisterAndRemoveDefeatedEnemies()
        {
            for (int index = activeEnemies.Count - 1; index >= 0; index--)
            {
                if (!activeEnemies[index].IsDead)
                    continue;

                activeEnemies.RemoveAt(index);
                waveDirector.RegisterDefeat(WaveCatalog.Tutorial.Trigger);
            }
        }

        public bool IsWithinAttackRange(DefenseEnemyCombatModel enemy)
        {
            float offsetX = enemy.X - coreX;
            float offsetZ = enemy.Z - coreZ;
            float range = enemy.Definition.AttackRange + DistanceEpsilon;
            return offsetX * offsetX + offsetZ * offsetZ <= range * range;
        }

        private DefenseRuntimeSnapshot CreateSnapshot()
        {
            var enemies = new DefenseEnemyRuntimeSnapshot[
                activeEnemies.Count];
            int aliveCount = 0;
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                DefenseEnemyCombatModel enemy = activeEnemies[index];
                if (!enemy.IsDead)
                    aliveCount++;
                float distanceToCore = DistanceToCore(enemy);
                enemies[index] = new DefenseEnemyRuntimeSnapshot(
                    enemy.StableId,
                    enemy.SpawnOrder,
                    enemy.X,
                    enemy.Z,
                    enemy.CurrentHealth,
                    DefenseEnemyRuntimeSnapshot.CityCoreTargetName,
                    distanceToCore,
                    IsWithinAttackRange(enemy));
            }

            return new DefenseRuntimeSnapshot(
                waveDirector.Phase,
                waveDirector.WarningRemaining,
                waveDirector.SpawnedCount,
                aliveCount,
                core.MaximumHealth,
                core.CurrentHealth,
                Array.AsReadOnly(enemies));
        }

        public float DistanceToCore(DefenseEnemyCombatModel enemy)
        {
            float offsetX = enemy.X - coreX;
            float offsetZ = enemy.Z - coreZ;
            return (float)Math.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
        }

    }
}
