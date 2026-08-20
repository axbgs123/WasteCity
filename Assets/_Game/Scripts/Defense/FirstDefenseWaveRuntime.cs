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

    public sealed class TutorialDefensePersistenceState
    {
        private readonly ReadOnlyCollection<DefenseEnemyPersistenceState>
            enemies;

        public TutorialDefensePersistenceState(
            bool tutorialTriggered,
            WavePhase wavePhase,
            float warningRemainingSeconds,
            float spawnClockSeconds,
            int spawnedEnemyCount,
            int defeatedEnemyCount,
            int nextEnemyOrdinal,
            float fixedStepAccumulatorSeconds,
            float spawnOriginX,
            float spawnOriginZ,
            int coreCurrentHealth,
            IEnumerable<DefenseEnemyPersistenceState> enemies)
        {
            TutorialTriggered = tutorialTriggered;
            WavePhase = wavePhase;
            WarningRemainingSeconds = warningRemainingSeconds;
            SpawnClockSeconds = spawnClockSeconds;
            SpawnedEnemyCount = spawnedEnemyCount;
            DefeatedEnemyCount = defeatedEnemyCount;
            NextEnemyOrdinal = nextEnemyOrdinal;
            FixedStepAccumulatorSeconds = fixedStepAccumulatorSeconds;
            SpawnOriginX = spawnOriginX;
            SpawnOriginZ = spawnOriginZ;
            CoreCurrentHealth = coreCurrentHealth;
            this.enemies = Array.AsReadOnly(
                new List<DefenseEnemyPersistenceState>(
                    enemies ?? Array.Empty<DefenseEnemyPersistenceState>())
                    .ToArray());
        }

        public bool TutorialTriggered { get; }
        public int TutorialWaveTriggerCount => TutorialTriggered ? 1 : 0;
        public WavePhase WavePhase { get; }
        public float WarningRemainingSeconds { get; }
        public float SpawnClockSeconds { get; }
        public int SpawnedEnemyCount { get; }
        public int DefeatedEnemyCount { get; }
        public int NextEnemyOrdinal { get; }
        public float FixedStepAccumulatorSeconds { get; }
        public float SpawnOriginX { get; }
        public float SpawnOriginZ { get; }
        public int CoreCurrentHealth { get; }
        public IReadOnlyList<DefenseEnemyPersistenceState> Enemies => enemies;
    }

    public sealed class TutorialDefenseRuntimeModel
    {
        public const float FormalFixedStepSeconds = .1f;

        private const double FixedStepSeconds = .1d;
        private const float FixedStepSecondsFloat = FormalFixedStepSeconds;
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
        private CityCoreCombatModel core =
            new CityCoreCombatModel();
        private readonly List<DefenseEnemyCombatModel> activeEnemies =
            new List<DefenseEnemyCombatModel>();
        private readonly ReadOnlyCollection<DefenseEnemyCombatModel>
            activeEnemiesView;
        private readonly List<EnemyArchetype> spawnedArchetypes =
            new List<EnemyArchetype>();
        private double accumulatorSeconds;
        private bool tutorialTriggered;
        private int nextEnemyOrdinal;

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
        public float SpawnOriginX => spawnX;
        public float SpawnOriginZ => spawnZ;
        public int NextEnemyOrdinal => nextEnemyOrdinal;
        public float FixedStepAccumulatorSeconds => (float)accumulatorSeconds;
        public bool TutorialTriggered => tutorialTriggered;
        public float SpawnClockSeconds => waveDirector.SpawnClock;
        public int DefeatedEnemyCount => waveDirector.DefeatedCount;

        public DefenseRuntimeSnapshot Snapshot => CreateSnapshot();

        public TutorialDefensePersistenceState CaptureForPersistence()
        {
            var enemies = new List<DefenseEnemyPersistenceState>();
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                if (!activeEnemies[index].IsDead)
                    enemies.Add(activeEnemies[index].CaptureForPersistence());
            }
            enemies.Sort(ComparePersistenceEnemies);

            return new TutorialDefensePersistenceState(
                tutorialTriggered,
                waveDirector.Phase,
                waveDirector.WarningRemaining,
                waveDirector.SpawnClock,
                waveDirector.SpawnedCount,
                waveDirector.DefeatedCount,
                nextEnemyOrdinal,
                (float)accumulatorSeconds,
                spawnX,
                spawnZ,
                core.CurrentHealth,
                enemies);
        }

        public static bool TryCreateForPersistence(
            TutorialDefensePersistenceState state,
            float coreX,
            float coreZ,
            out TutorialDefenseRuntimeModel model,
            out string error)
        {
            model = null;
            error = null;
            if (state == null)
                return Fail("Tutorial defense persistence state is required.", out error);
            if (!IsFinite(coreX) || !IsFinite(coreZ) ||
                !IsFinite(state.SpawnOriginX) ||
                !IsFinite(state.SpawnOriginZ))
            {
                return Fail("Defense coordinates must be finite.", out error);
            }
            if (!IsFinite(state.FixedStepAccumulatorSeconds) ||
                state.FixedStepAccumulatorSeconds < 0f ||
                state.FixedStepAccumulatorSeconds >= FixedStepSecondsFloat)
            {
                return Fail("Defense fixed-step remainder is invalid.", out error);
            }
            if (!TryValidateTutorialCounters(state, out error))
                return false;
            if (!CityCoreCombatModel.TryCreateForPersistence(
                state.CoreCurrentHealth,
                out CityCoreCombatModel restoredCore,
                out error))
            {
                return false;
            }

            var restoredEnemies =
                new List<DefenseEnemyCombatModel>(state.Enemies.Count);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var spawnOrders = new HashSet<int>();
            int maximumSpawnOrder = -1;
            for (int index = 0; index < state.Enemies.Count; index++)
            {
                DefenseEnemyPersistenceState enemyState = state.Enemies[index];
                if (!stableIds.Add(enemyState.StableId))
                    return Fail("Enemy stable IDs must be unique.", out error);
                if (!spawnOrders.Add(enemyState.SpawnOrder))
                    return Fail("Enemy spawn orders must be unique.", out error);
                if (!DefenseEnemyCombatModel.TryCreateForPersistence(
                    enemyState,
                    out DefenseEnemyCombatModel restoredEnemy,
                    out error))
                {
                    return false;
                }
                if (restoredEnemy.Definition.Archetype != EnemyArchetype.Gnawer)
                    return Fail("Tutorial wave may only contain gnawers.", out error);
                maximumSpawnOrder = Math.Max(
                    maximumSpawnOrder,
                    restoredEnemy.SpawnOrder);
                restoredEnemies.Add(restoredEnemy);
            }
            if (state.Enemies.Count !=
                state.SpawnedEnemyCount - state.DefeatedEnemyCount)
            {
                return Fail("Alive enemy count disagrees with wave counters.", out error);
            }
            if (state.NextEnemyOrdinal < state.SpawnedEnemyCount ||
                state.NextEnemyOrdinal <= maximumSpawnOrder)
            {
                return Fail("Next enemy ordinal is below its high-water mark.", out error);
            }

            int currentTrigger = state.TutorialTriggered &&
                state.WavePhase != WavePhase.Idle
                ? WaveCatalog.Tutorial.Trigger
                : -1;
            var waveState = new WaveDirectorPersistenceState(
                currentTrigger,
                state.WavePhase,
                state.SpawnedEnemyCount,
                state.DefeatedEnemyCount,
                state.WarningRemainingSeconds,
                state.SpawnClockSeconds,
                Array.Empty<int>(),
                state.TutorialTriggered
                    ? new[] { WaveCatalog.Tutorial.Trigger }
                    : Array.Empty<int>());

            var restored = new TutorialDefenseRuntimeModel(
                coreX,
                coreZ,
                state.SpawnOriginX,
                state.SpawnOriginZ);
            if (!restored.waveDirector.TryRestoreForPersistence(
                waveState,
                out error))
            {
                return false;
            }
            restoredEnemies.Sort(CompareEnemies);
            restored.activeEnemies.AddRange(restoredEnemies);
            restored.core = restoredCore;
            restored.tutorialTriggered = state.TutorialTriggered;
            restored.nextEnemyOrdinal = state.NextEnemyOrdinal;
            restored.accumulatorSeconds = state.FixedStepAccumulatorSeconds;
            model = restored;
            return true;
        }

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
            if (Math.Abs(accumulatorSeconds) <= WaveBoundaryEpsilon)
                accumulatorSeconds = 0d;
        }

        public void AdvanceFixedStep()
        {
            SimulateFixedStep();
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

                int spawnOrder = nextEnemyOrdinal++;
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

        private static bool TryValidateTutorialCounters(
            TutorialDefensePersistenceState state,
            out string error)
        {
            error = null;
            if (state.SpawnedEnemyCount < 0 ||
                state.SpawnedEnemyCount > WaveCatalog.Tutorial.TotalCount ||
                state.DefeatedEnemyCount < 0 ||
                state.DefeatedEnemyCount > state.SpawnedEnemyCount ||
                state.NextEnemyOrdinal < 0)
            {
                return Fail("Tutorial wave counters are invalid.", out error);
            }
            if (!state.TutorialTriggered)
            {
                if (state.WavePhase != WavePhase.Idle ||
                    state.WarningRemainingSeconds != 0f ||
                    state.SpawnClockSeconds != 0f ||
                    state.SpawnedEnemyCount != 0 ||
                    state.DefeatedEnemyCount != 0 ||
                    state.NextEnemyOrdinal != 0 || state.Enemies.Count != 0)
                {
                    return Fail("Untriggered tutorial state is inconsistent.", out error);
                }
            }
            else if (state.WavePhase == WavePhase.Idle &&
                (state.SpawnedEnemyCount != WaveCatalog.Tutorial.TotalCount ||
                 state.DefeatedEnemyCount != WaveCatalog.Tutorial.TotalCount ||
                 state.Enemies.Count != 0))
            {
                return Fail("Completed tutorial state is inconsistent.", out error);
            }
            else if (state.WavePhase == WavePhase.Active &&
                state.DefeatedEnemyCount >= Math.Max(
                    1,
                    (int)Math.Ceiling(
                        WaveCatalog.Tutorial.TotalCount * .9f)))
            {
                return Fail(
                    "Active tutorial state already reached completion.",
                    out error);
            }
            return true;
        }

        private static int ComparePersistenceEnemies(
            DefenseEnemyPersistenceState left,
            DefenseEnemyPersistenceState right)
        {
            int order = left.SpawnOrder.CompareTo(right.SpawnOrder);
            return order != 0
                ? order
                : string.Compare(left.StableId, right.StableId,
                    StringComparison.Ordinal);
        }

        private static int CompareEnemies(
            DefenseEnemyCombatModel left,
            DefenseEnemyCombatModel right)
        {
            int order = left.SpawnOrder.CompareTo(right.SpawnOrder);
            return order != 0
                ? order
                : string.Compare(left.StableId, right.StableId,
                    StringComparison.Ordinal);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

    }
}
