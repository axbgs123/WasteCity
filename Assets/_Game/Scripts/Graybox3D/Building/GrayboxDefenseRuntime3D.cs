using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxDefenseTowerStatus3D
    {
        NoTarget,
        Firing,
        MissingAmmunition,
        OutOfLogistics,
        PlayerPaused,
        Unavailable,
    }

    public sealed class GrayboxDefenseTowerRuntimeState3D
    {
        internal GrayboxDefenseTowerRuntimeState3D(
            MachineGunTurretCombatModel combat)
        {
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            Status = GrayboxDefenseTowerStatus3D.NoTarget;
        }

        public string StableId => Combat.StableId;
        public MachineGunTurretCombatModel Combat { get; }
        public bool CanRunLocally { get; internal set; }
        public string TargetId { get; internal set; }
        public GrayboxDefenseTowerStatus3D Status { get; internal set; }
    }

    public sealed class GrayboxDefenseEvacuationPayload3D
    {
        internal GrayboxDefenseEvacuationPayload3D(
            GrayboxDefenseTowerRuntimeState3D sourceState)
        {
            SourceState = sourceState ??
                throw new ArgumentNullException(nameof(sourceState));
            StableInstanceId = sourceState.StableId;
            AmmunitionAmount = sourceState.Combat.Ammo;
        }

        public string StableInstanceId { get; }
        public int AmmunitionAmount { get; }

        internal GrayboxDefenseTowerRuntimeState3D SourceState { get; }
    }

    public sealed class GrayboxDefenseTowerSnapshot3D
    {
        public GrayboxDefenseTowerSnapshot3D(
            string stableId,
            int ammo,
            int ammoCapacity,
            bool connected,
            bool playerPaused,
            string targetId,
            GrayboxDefenseTowerStatus3D status)
            : this(
                stableId,
                ammo,
                ammoCapacity,
                DefenseTowerCatalog.For(
                    BuildingCatalog.MachineGunTurret.Id.Value).Range,
                connected,
                canRunLocally: true,
                playerPaused,
                targetId,
                status)
        {
        }

        public GrayboxDefenseTowerSnapshot3D(
            string stableId,
            int ammo,
            int ammoCapacity,
            float range,
            bool connected,
            bool canRunLocally,
            bool playerPaused,
            string targetId,
            GrayboxDefenseTowerStatus3D status)
        {
            StableId = stableId;
            Ammo = ammo;
            AmmoCapacity = ammoCapacity;
            Range = range;
            Connected = connected;
            CanRunLocally = canRunLocally;
            PlayerPaused = playerPaused;
            TargetId = targetId;
            Status = status;
        }

        public string StableId { get; }
        public int Ammo { get; }
        public int AmmoCapacity { get; }
        public float Range { get; }
        public bool Connected { get; }
        public bool CanRunLocally { get; }
        public bool PlayerPaused { get; }
        public string TargetId { get; }
        public GrayboxDefenseTowerStatus3D Status { get; }
    }

    public sealed class GrayboxDefenseEnemySnapshot3D
    {
        public GrayboxDefenseEnemySnapshot3D(
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
                DefenseEnemyRuntimeSnapshot.CityCoreTargetName,
                Distance(x, z, 0f, 0f),
                isAttackingCore)
        {
        }

        public GrayboxDefenseEnemySnapshot3D(
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

    public sealed class GrayboxDefenseRuntimeSnapshot3D
    {
        public GrayboxDefenseRuntimeSnapshot3D(
            int tutorialWaveTriggerCount,
            WavePhase wavePhase,
            float warningRemainingSeconds,
            int spawnedEnemyCount,
            int aliveEnemyCount,
            int defeatedEnemyCount,
            int coreMaximumHealth,
            int coreCurrentHealth,
            IReadOnlyList<GrayboxDefenseTowerSnapshot3D> towers,
            IReadOnlyList<GrayboxDefenseEnemySnapshot3D> enemies)
        {
            TutorialWaveTriggerCount = tutorialWaveTriggerCount;
            WavePhase = wavePhase;
            WarningRemainingSeconds = warningRemainingSeconds;
            SpawnedEnemyCount = spawnedEnemyCount;
            AliveEnemyCount = aliveEnemyCount;
            DefeatedEnemyCount = defeatedEnemyCount;
            CoreMaximumHealth = coreMaximumHealth;
            CoreCurrentHealth = coreCurrentHealth;
            Towers = towers ?? throw new ArgumentNullException(nameof(towers));
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        }

        public int TutorialWaveTriggerCount { get; }
        public WavePhase WavePhase { get; }
        public float WarningRemainingSeconds { get; }
        public int SpawnedEnemyCount { get; }
        public int AliveEnemyCount { get; }
        public int DefeatedEnemyCount { get; }
        public int CoreMaximumHealth { get; }
        public int CoreCurrentHealth { get; }
        public bool IsCoreDestroyed => CoreCurrentHealth <= 0;
        public IReadOnlyList<GrayboxDefenseTowerSnapshot3D> Towers { get; }
        public IReadOnlyList<GrayboxDefenseEnemySnapshot3D> Enemies { get; }
    }

    public sealed class GrayboxDefenseRuntime3D
    {
        private const double StepSeconds = .1d;
        private const float StepSecondsFloat = .1f;
        private const double StepEpsilon = .000001d;

        private readonly Dictionary<string, GrayboxDefenseTowerRuntimeState3D>
            stateById =
                new Dictionary<string, GrayboxDefenseTowerRuntimeState3D>(
                    StringComparer.Ordinal);
        private readonly List<GrayboxBuildingInstance3D> orderedInstances =
            new List<GrayboxBuildingInstance3D>();
        private readonly List<GrayboxDefenseTowerRuntimeState3D> towers =
            new List<GrayboxDefenseTowerRuntimeState3D>();
        private readonly ReadOnlyCollection<GrayboxDefenseTowerRuntimeState3D>
            readOnlyTowers;
        private readonly HashSet<string> retainedIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> runnableIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removedIds = new List<string>();
        private readonly TutorialDefenseRuntimeModel tutorial;
        private double accumulatorSeconds;
        private int tutorialWaveTriggerCount;
        private GrayboxDefenseRuntimeSnapshot3D cachedSnapshot;
        private bool snapshotDirty = true;

        public GrayboxDefenseRuntime3D(
            float coreX,
            float coreZ,
            float spawnX,
            float spawnZ)
        {
            readOnlyTowers = towers.AsReadOnly();
            tutorial = new TutorialDefenseRuntimeModel(
                coreX,
                coreZ,
                spawnX,
                spawnZ);
        }

        public IReadOnlyList<GrayboxDefenseTowerRuntimeState3D> Towers =>
            readOnlyTowers;

        public GrayboxDefenseRuntimeSnapshot3D Snapshot
        {
            get
            {
                if (cachedSnapshot == null || snapshotDirty)
                {
                    cachedSnapshot = CaptureSnapshot();
                    snapshotDirty = false;
                }
                return cachedSnapshot;
            }
        }

        public void SetCorePosition(float x, float z)
        {
            tutorial.SetCorePosition(x, z);
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius)
        {
            bool snapshotChanged = false;
            orderedInstances.Clear();
            towers.Clear();
            retainedIds.Clear();
            runnableIds.Clear();

            if (instances != null)
            {
                for (int index = 0; index < instances.Count; index++)
                {
                    if (instances[index] != null)
                        orderedInstances.Add(instances[index]);
                }
            }
            orderedInstances.Sort((left, right) => string.Compare(
                left.StableInstanceId,
                right.StableInstanceId,
                StringComparison.Ordinal));

            for (int index = 0; index < orderedInstances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = orderedInstances[index];
                if (!GrayboxBuildingOperationalAccess3D.CanRetainState(instance) ||
                    !IsMachineGunTurret(instance) ||
                    !retainedIds.Add(instance.StableInstanceId))
                {
                    continue;
                }

                if (!stateById.TryGetValue(
                        instance.StableInstanceId,
                        out GrayboxDefenseTowerRuntimeState3D state))
                {
                    state = new GrayboxDefenseTowerRuntimeState3D(
                        new MachineGunTurretCombatModel(
                            instance.StableInstanceId,
                            instance.Placement.X,
                            instance.Placement.Y));
                    stateById.Add(instance.StableInstanceId, state);
                    snapshotChanged = true;
                    if (tutorial.NotifyMachineGunTurretCompleted(
                            instance.StableInstanceId))
                    {
                        tutorialWaveTriggerCount++;
                    }
                }

                bool canRun =
                    GrayboxBuildingOperationalAccess3D.CanRunLocally(
                        instance,
                        cityMode);
                bool connected = canRun &&
                    GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                        instance,
                        cityMode,
                        cityX,
                        cityY,
                        groundRadius);
                if (state.CanRunLocally != canRun)
                    snapshotChanged = true;
                state.CanRunLocally = canRun;
                if (!canRun)
                {
                    if (!instance.IsEvacuationLocked)
                    {
                        if (state.TargetId != null ||
                            state.Status !=
                            GrayboxDefenseTowerStatus3D.Unavailable)
                        {
                            snapshotChanged = true;
                        }
                        state.TargetId = null;
                        state.Status =
                            GrayboxDefenseTowerStatus3D.Unavailable;
                    }
                }
                else if (state.Status ==
                         GrayboxDefenseTowerStatus3D.Unavailable)
                {
                    state.TargetId = null;
                    state.Status = GrayboxDefenseTowerStatus3D.NoTarget;
                    snapshotChanged = true;
                }
                if (state.Combat.IsLogisticsConnected != connected)
                    snapshotChanged = true;
                state.Combat.SetLogisticsConnected(connected);
                if (canRun)
                    runnableIds.Add(instance.StableInstanceId);
                towers.Add(state);
            }

            removedIds.Clear();
            foreach (string stableInstanceId in stateById.Keys)
            {
                if (!retainedIds.Contains(stableInstanceId))
                    removedIds.Add(stableInstanceId);
            }
            for (int index = 0; index < removedIds.Count; index++)
            {
                stateById.Remove(removedIds[index]);
                snapshotChanged = true;
            }
            if (snapshotChanged)
                snapshotDirty = true;
        }

        public void Tick(
            float deltaSeconds,
            bool globallyPaused,
            CityResourceStorageModel cityStorage)
        {
            if (globallyPaused || deltaSeconds <= 0f || cityStorage == null)
                return;

            accumulatorSeconds += deltaSeconds;
            bool advancedFixedStep = false;
            while (accumulatorSeconds + StepEpsilon >= StepSeconds)
            {
                for (int index = 0; index < towers.Count; index++)
                    TickTower(towers[index], cityStorage);
                tutorial.Advance(
                    StepSecondsFloat,
                    globallyPaused: false);
                advancedFixedStep = true;
                accumulatorSeconds -= StepSeconds;
                if (accumulatorSeconds < 0d &&
                    accumulatorSeconds > -StepEpsilon)
                {
                    accumulatorSeconds = 0d;
                }
            }
            if (advancedFixedStep)
                snapshotDirty = true;
        }

        public bool TrySetPlayerPaused(
            string stableInstanceId,
            bool paused)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                !stateById.TryGetValue(
                    stableInstanceId,
                    out GrayboxDefenseTowerRuntimeState3D state))
            {
                return false;
            }

            if (state.Combat.IsPlayerPaused == paused)
                return true;

            state.Combat.SetPlayerPaused(paused);
            state.TargetId = null;
            state.Status = paused
                ? GrayboxDefenseTowerStatus3D.PlayerPaused
                : GrayboxDefenseTowerStatus3D.NoTarget;
            snapshotDirty = true;
            return true;
        }

        public bool TryCaptureEvacuationPayload(
            string stableInstanceId,
            out GrayboxDefenseEvacuationPayload3D payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                !stateById.TryGetValue(
                    stableInstanceId,
                    out GrayboxDefenseTowerRuntimeState3D state))
            {
                return false;
            }

            payload = new GrayboxDefenseEvacuationPayload3D(state);
            return true;
        }

        public bool TryFinalizeEvacuationPayload(
            string stableInstanceId,
            GrayboxDefenseEvacuationPayload3D payload)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                payload == null ||
                !string.Equals(
                    stableInstanceId,
                    payload.StableInstanceId,
                    StringComparison.Ordinal) ||
                !stateById.TryGetValue(
                    stableInstanceId,
                    out GrayboxDefenseTowerRuntimeState3D state) ||
                !ReferenceEquals(state, payload.SourceState) ||
                state.Combat.Ammo != payload.AmmunitionAmount)
            {
                return false;
            }

            RemoveTowerState(stableInstanceId, state);
            return true;
        }

        public bool TryDiscardEvacuationPayload(string stableInstanceId)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                !stateById.TryGetValue(
                    stableInstanceId,
                    out GrayboxDefenseTowerRuntimeState3D state))
            {
                return false;
            }

            RemoveTowerState(stableInstanceId, state);
            return true;
        }

        private void TickTower(
            GrayboxDefenseTowerRuntimeState3D state,
            CityResourceStorageModel cityStorage)
        {
            if (!runnableIds.Contains(state.StableId)) return;

            if (state.Combat.IsPlayerPaused)
            {
                state.TargetId = null;
                state.Status = GrayboxDefenseTowerStatus3D.PlayerPaused;
                return;
            }

            state.Combat.RefillFrom(cityStorage);

            DefenseEnemyCombatModel target = state.Combat.AcquireTarget(
                tutorial.ActiveEnemies);
            state.TargetId = target?.StableId;
            if (target == null)
            {
                state.Status = GrayboxDefenseTowerStatus3D.NoTarget;
                return;
            }

            int damage = state.Combat.Tick(
                StepSecondsFloat,
                target,
                globallyPaused: false);
            if (damage > 0)
            {
                state.Status = GrayboxDefenseTowerStatus3D.Firing;
                return;
            }

            state.Status = !state.Combat.IsLogisticsConnected &&
                cityStorage.GetNetworkAmount(ResourceIds.Ammunition) > 0
                    ? GrayboxDefenseTowerStatus3D.OutOfLogistics
                    : GrayboxDefenseTowerStatus3D.MissingAmmunition;
        }

        private GrayboxDefenseRuntimeSnapshot3D CaptureSnapshot()
        {
            var towerSnapshots =
                new GrayboxDefenseTowerSnapshot3D[towers.Count];
            for (int index = 0; index < towers.Count; index++)
            {
                GrayboxDefenseTowerRuntimeState3D state = towers[index];
                towerSnapshots[index] = new GrayboxDefenseTowerSnapshot3D(
                    state.StableId,
                    state.Combat.Ammo,
                    state.Combat.AmmoCapacity,
                    state.Combat.Range,
                    state.Combat.IsLogisticsConnected,
                    state.CanRunLocally,
                    state.Combat.IsPlayerPaused,
                    state.TargetId,
                    state.Status);
            }

            IReadOnlyList<DefenseEnemyCombatModel> activeEnemies =
                tutorial.ActiveEnemies;
            var enemySnapshots =
                new GrayboxDefenseEnemySnapshot3D[activeEnemies.Count];
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                DefenseEnemyCombatModel enemy = activeEnemies[index];
                enemySnapshots[index] = new GrayboxDefenseEnemySnapshot3D(
                    enemy.StableId,
                    enemy.SpawnOrder,
                    enemy.X,
                    enemy.Z,
                    enemy.CurrentHealth,
                    DefenseEnemyRuntimeSnapshot.CityCoreTargetName,
                    tutorial.DistanceToCore(enemy),
                    tutorial.IsWithinAttackRange(enemy));
            }

            return new GrayboxDefenseRuntimeSnapshot3D(
                tutorialWaveTriggerCount,
                tutorial.WavePhase,
                tutorial.WarningRemainingSeconds,
                tutorial.SpawnedEnemyCount,
                tutorial.AliveEnemyCount,
                Math.Max(
                    0,
                    tutorial.SpawnedEnemyCount - tutorial.AliveEnemyCount),
                tutorial.Core.MaximumHealth,
                tutorial.Core.CurrentHealth,
                Array.AsReadOnly(towerSnapshots),
                Array.AsReadOnly(enemySnapshots));
        }

        private void RemoveTowerState(
            string stableInstanceId,
            GrayboxDefenseTowerRuntimeState3D state)
        {
            stateById.Remove(stableInstanceId);
            retainedIds.Remove(stableInstanceId);
            runnableIds.Remove(stableInstanceId);
            towers.Remove(state);
            snapshotDirty = true;
        }

        private static bool IsMachineGunTurret(
            GrayboxBuildingInstance3D instance)
        {
            return string.Equals(
                instance.Placement.Definition.Id.Value,
                BuildingCatalog.MachineGunTurret.Id.Value,
                StringComparison.Ordinal);
        }
    }
}
