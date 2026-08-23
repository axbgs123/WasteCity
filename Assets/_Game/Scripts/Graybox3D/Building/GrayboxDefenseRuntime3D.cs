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

    public sealed class GrayboxDefensePersistenceState3D
    {
        private readonly ReadOnlyCollection<MachineGunTurretPersistenceState>
            towers;

        public GrayboxDefensePersistenceState3D(
            int tutorialWaveTriggerCount,
            float fixedStepAccumulatorSeconds,
            string randomState,
            TutorialDefensePersistenceState tutorial,
            MachineGunTurretPersistenceState[] towers)
        {
            TutorialWaveTriggerCount = tutorialWaveTriggerCount;
            FixedStepAccumulatorSeconds = fixedStepAccumulatorSeconds;
            RandomState = randomState;
            Tutorial = tutorial ?? throw new ArgumentNullException(
                nameof(tutorial));
            this.towers = Array.AsReadOnly(
                towers == null
                    ? Array.Empty<MachineGunTurretPersistenceState>()
                    : (MachineGunTurretPersistenceState[])towers.Clone());
        }

        public int TutorialWaveTriggerCount { get; }
        public float FixedStepAccumulatorSeconds { get; }
        public string RandomState { get; }
        public TutorialDefensePersistenceState Tutorial { get; }
        public IReadOnlyList<MachineGunTurretPersistenceState> Towers =>
            towers;
    }

    public sealed class GrayboxDefenseRestorePlan3D
    {
        internal GrayboxDefenseRestorePlan3D(
            GrayboxDefenseRuntime3D owner,
            ulong expectedGeneration,
            ulong expectedFingerprint,
            GrayboxDefensePersistenceState3D snapshot,
            TutorialDefenseRuntimeModel tutorial,
            GrayboxDefenseTowerRuntimeState3D[] towers)
        {
            Owner = owner;
            ExpectedGeneration = expectedGeneration;
            ExpectedFingerprint = expectedFingerprint;
            Snapshot = snapshot;
            Tutorial = tutorial;
            Towers = towers;
        }

        internal GrayboxDefenseRuntime3D Owner { get; }
        internal ulong ExpectedGeneration { get; }
        internal ulong ExpectedFingerprint { get; }
        internal GrayboxDefensePersistenceState3D Snapshot { get; }
        internal TutorialDefenseRuntimeModel Tutorial { get; }
        internal GrayboxDefenseTowerRuntimeState3D[] Towers { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class GrayboxDefenseRuntime3D
    {
        private const double StepSeconds = .1d;
        private const float StepSecondsFloat =
            TutorialDefenseRuntimeModel.FormalFixedStepSeconds;
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
        private readonly Dictionary<string, bool> synchronizedLockById =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly List<string> removedIds = new List<string>();
        private TutorialDefenseRuntimeModel tutorial;
        private double accumulatorSeconds;
        private int tutorialWaveTriggerCount;
        private float requestedCoreX;
        private float requestedCoreZ;
        private ulong persistenceGeneration;
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
            requestedCoreX = coreX;
            requestedCoreZ = coreZ;
        }

        public IReadOnlyList<GrayboxDefenseTowerRuntimeState3D> Towers =>
            readOnlyTowers;

        public bool TryGetTowerState(
            string stableInstanceId,
            out GrayboxDefenseTowerRuntimeState3D state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                stateById.TryGetValue(stableInstanceId, out state);
        }

        public GrayboxDefensePersistenceState3D CaptureForPersistence()
        {
            var capturedTowers = new MachineGunTurretPersistenceState[
                stateById.Count];
            var index = 0;
            foreach (GrayboxDefenseTowerRuntimeState3D state in
                     stateById.Values)
            {
                capturedTowers[index++] =
                    state.Combat.CaptureForPersistence();
            }
            Array.Sort(capturedTowers, (left, right) => string.CompareOrdinal(
                left.StableId,
                right.StableId));
            return new GrayboxDefensePersistenceState3D(
                tutorialWaveTriggerCount,
                (float)accumulatorSeconds,
                randomState: null,
                tutorial.CaptureForPersistence(),
                capturedTowers);
        }

        public bool TryPrepareRestore(
            GrayboxDefensePersistenceState3D snapshot,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out GrayboxDefenseRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (snapshot == null || snapshot.Tutorial == null ||
                snapshot.Towers == null || instances == null)
            {
                error = "防御存档状态不能为空";
                return false;
            }
            if (snapshot.TutorialWaveTriggerCount < 0 ||
                snapshot.TutorialWaveTriggerCount > 1 ||
                snapshot.TutorialWaveTriggerCount !=
                    (snapshot.Tutorial.TutorialTriggered ? 1 : 0) ||
                snapshot.Tutorial.FixedStepAccumulatorSeconds != 0f ||
                !IsFinite(snapshot.FixedStepAccumulatorSeconds) ||
                snapshot.FixedStepAccumulatorSeconds < 0f ||
                snapshot.FixedStepAccumulatorSeconds >= StepSecondsFloat ||
                !string.IsNullOrEmpty(snapshot.RandomState))
            {
                error = "防御触发、固定步余量或随机状态无效";
                return false;
            }

            ulong expectedGeneration = persistenceGeneration;
            ulong expectedFingerprint = ComputePersistenceFingerprint();
            var eligibleInstances = new Dictionary<
                string,
                GrayboxBuildingInstance3D>(StringComparer.Ordinal);
            var allInstanceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance == null ||
                    string.IsNullOrWhiteSpace(instance.StableInstanceId) ||
                    !allInstanceIds.Add(instance.StableInstanceId))
                {
                    error = "建筑实例为空、ID 为空或重复";
                    return false;
                }
                if (!GrayboxBuildingOperationalAccess3D.CanRetainState(
                        instance) ||
                    !IsMachineGunTurret(instance))
                {
                    continue;
                }
                eligibleInstances.Add(instance.StableInstanceId, instance);
            }
            if (eligibleInstances.Count != stateById.Count ||
                snapshot.Towers.Count != stateById.Count)
            {
                error = "机枪塔存档必须与已同步的可保留实例一一对应";
                return false;
            }

            var seenTowerIds = new HashSet<string>(StringComparer.Ordinal);
            var preparedTowers = new GrayboxDefenseTowerRuntimeState3D[
                snapshot.Towers.Count];
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                MachineGunTurretPersistenceState saved =
                    snapshot.Towers[index];
                if (string.IsNullOrWhiteSpace(saved.StableId) ||
                    !seenTowerIds.Add(saved.StableId) ||
                    !eligibleInstances.TryGetValue(
                        saved.StableId,
                        out GrayboxBuildingInstance3D instance) ||
                    !TryGetSynchronizedInstance(
                        saved.StableId,
                        out GrayboxBuildingInstance3D synchronized) ||
                    !ReferenceEquals(instance, synchronized) ||
                    !synchronizedLockById.TryGetValue(
                        saved.StableId,
                        out bool synchronizedLock) ||
                    synchronizedLock != instance.IsEvacuationLocked ||
                    !stateById.TryGetValue(
                        saved.StableId,
                        out GrayboxDefenseTowerRuntimeState3D current) ||
                    current.Combat.X != instance.Placement.X ||
                    current.Combat.Z != instance.Placement.Y)
                {
                    error = "机枪塔存档身份或格位与已同步运行时不一致";
                    return false;
                }
                if (!MachineGunTurretCombatModel.TryCreateForPersistence(
                        saved,
                        instance.Placement.X,
                        instance.Placement.Y,
                        out MachineGunTurretCombatModel combat,
                        out error))
                {
                    return false;
                }
                combat.SetLogisticsConnected(
                    current.Combat.IsLogisticsConnected);
                var prepared = new GrayboxDefenseTowerRuntimeState3D(combat)
                {
                    CanRunLocally = current.CanRunLocally,
                    TargetId = null,
                    Status = combat.IsPlayerPaused
                        ? GrayboxDefenseTowerStatus3D.PlayerPaused
                        : current.CanRunLocally
                            ? GrayboxDefenseTowerStatus3D.NoTarget
                            : GrayboxDefenseTowerStatus3D.Unavailable,
                };
                preparedTowers[index] = prepared;
            }
            Array.Sort(preparedTowers, (left, right) => string.CompareOrdinal(
                left.StableId,
                right.StableId));

            if (!TutorialDefenseRuntimeModel.TryCreateForPersistence(
                    snapshot.Tutorial,
                    requestedCoreX,
                    requestedCoreZ,
                    out TutorialDefenseRuntimeModel preparedTutorial,
                    out error))
            {
                return false;
            }
            if (expectedGeneration != persistenceGeneration ||
                expectedFingerprint != ComputePersistenceFingerprint())
            {
                error = "防御运行时在恢复预检期间发生变化";
                return false;
            }

            plan = new GrayboxDefenseRestorePlan3D(
                this,
                expectedGeneration,
                expectedFingerprint,
                snapshot,
                preparedTutorial,
                preparedTowers);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            GrayboxDefenseRestorePlan3D plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "防御恢复计划不属于当前运行时";
                return false;
            }
            if (plan.Consumed)
            {
                error = "防御恢复计划已经使用";
                return false;
            }
            if (plan.ExpectedGeneration != persistenceGeneration ||
                plan.ExpectedFingerprint != ComputePersistenceFingerprint())
            {
                error = "防御恢复计划已过期";
                return false;
            }

            stateById.Clear();
            towers.Clear();
            for (var index = 0; index < plan.Towers.Length; index++)
            {
                GrayboxDefenseTowerRuntimeState3D state = plan.Towers[index];
                stateById.Add(state.StableId, state);
                towers.Add(state);
            }
            tutorial = plan.Tutorial;
            tutorialWaveTriggerCount =
                plan.Snapshot.TutorialWaveTriggerCount;
            accumulatorSeconds = plan.Snapshot.FixedStepAccumulatorSeconds;
            cachedSnapshot = null;
            snapshotDirty = true;
            plan.Consumed = true;
            persistenceGeneration++;
            error = string.Empty;
            return true;
        }

        public bool TryRestore(
            GrayboxDefensePersistenceState3D snapshot,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out string error)
        {
            if (!TryPrepareRestore(
                    snapshot,
                    instances,
                    out GrayboxDefenseRestorePlan3D plan,
                    out error))
            {
                return false;
            }
            return TryCommitRestore(plan, out error);
        }

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
            requestedCoreX = x;
            requestedCoreZ = z;
            tutorial.SetCorePosition(x, z);
            persistenceGeneration++;
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius)
        {
            persistenceGeneration++;
            bool snapshotChanged = false;
            orderedInstances.Clear();
            towers.Clear();
            retainedIds.Clear();
            runnableIds.Clear();
            synchronizedLockById.Clear();

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
                synchronizedLockById[instance.StableInstanceId] =
                    instance.IsEvacuationLocked;
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
            persistenceGeneration++;
            bool advancedFixedStep = false;
            while (accumulatorSeconds + StepEpsilon >= StepSeconds)
            {
                for (int index = 0; index < towers.Count; index++)
                    TickTower(towers[index], cityStorage);
                tutorial.AdvanceFixedStep();
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
            persistenceGeneration++;
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

        public bool TryDestroyTowerForCombat(
            string stableInstanceId,
            out ResourceAmount[] lostResources)
        {
            lostResources = Array.Empty<ResourceAmount>();
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                !stateById.TryGetValue(
                    stableInstanceId,
                    out GrayboxDefenseTowerRuntimeState3D state))
            {
                return false;
            }

            int ammunition = state.Combat.Ammo;
            if (ammunition > 0)
            {
                lostResources = new[]
                {
                    new ResourceAmount(ResourceIds.Ammunition, ammunition),
                };
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
                string visibleTargetId = state.CanRunLocally
                    ? state.TargetId
                    : null;
                GrayboxDefenseTowerStatus3D visibleStatus =
                    state.CanRunLocally
                        ? state.Status
                        : GrayboxDefenseTowerStatus3D.Unavailable;
                towerSnapshots[index] = new GrayboxDefenseTowerSnapshot3D(
                    state.StableId,
                    state.Combat.Ammo,
                    state.Combat.AmmoCapacity,
                    state.Combat.Range,
                    state.Combat.IsLogisticsConnected,
                    state.CanRunLocally,
                    state.Combat.IsPlayerPaused,
                    visibleTargetId,
                    visibleStatus);
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
            synchronizedLockById.Remove(stableInstanceId);
            towers.Remove(state);
            snapshotDirty = true;
            persistenceGeneration++;
        }

        private ulong ComputePersistenceFingerprint()
        {
            GrayboxDefensePersistenceState3D snapshot =
                CaptureForPersistence();
            ulong value = 1469598103934665603ul;
            Mix(ref value, snapshot.TutorialWaveTriggerCount);
            Mix(ref value, snapshot.FixedStepAccumulatorSeconds);
            Mix(ref value, snapshot.RandomState);
            MixTutorial(ref value, snapshot.Tutorial);
            Mix(ref value, snapshot.Towers.Count);
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                MachineGunTurretPersistenceState tower =
                    snapshot.Towers[index];
                Mix(ref value, tower.StableId);
                Mix(ref value, tower.AmmunitionAmount);
                Mix(ref value, tower.IsPlayerPaused);
                Mix(ref value, tower.ActiveAmmunitionSeconds);
                Mix(ref value, tower.DamageRemainder);
                if (stateById.TryGetValue(
                        tower.StableId,
                        out GrayboxDefenseTowerRuntimeState3D runtimeState))
                {
                    Mix(ref value, runtimeState.CanRunLocally);
                    Mix(ref value,
                        runtimeState.Combat.IsLogisticsConnected);
                    Mix(ref value, runnableIds.Contains(tower.StableId));
                }
                if (TryGetSynchronizedInstance(
                        tower.StableId,
                        out GrayboxBuildingInstance3D instance))
                {
                    Mix(ref value, instance.IsEvacuationLocked);
                    Mix(ref value, instance.IsPlayerOwned);
                    Mix(ref value, (int)instance.State);
                    Mix(ref value,
                        instance.Placement.Definition.Id.Value);
                    Mix(ref value, (int)instance.Placement.Site);
                    Mix(ref value, instance.Placement.X);
                    Mix(ref value, instance.Placement.Y);
                    Mix(ref value,
                        (int)instance.Placement.Orientation);
                }
            }
            return value;
        }

        private bool TryGetSynchronizedInstance(
            string stableInstanceId,
            out GrayboxBuildingInstance3D instance)
        {
            for (var index = 0; index < orderedInstances.Count; index++)
            {
                GrayboxBuildingInstance3D candidate = orderedInstances[index];
                if (!string.Equals(
                        candidate.StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                instance = candidate;
                return true;
            }
            instance = null;
            return false;
        }

        private static void MixTutorial(
            ref ulong value,
            TutorialDefensePersistenceState tutorialState)
        {
            Mix(ref value, tutorialState.TutorialTriggered);
            Mix(ref value, (int)tutorialState.WavePhase);
            Mix(ref value, tutorialState.WarningRemainingSeconds);
            Mix(ref value, tutorialState.SpawnClockSeconds);
            Mix(ref value, tutorialState.SpawnedEnemyCount);
            Mix(ref value, tutorialState.DefeatedEnemyCount);
            Mix(ref value, tutorialState.NextEnemyOrdinal);
            Mix(ref value, tutorialState.FixedStepAccumulatorSeconds);
            Mix(ref value, tutorialState.SpawnOriginX);
            Mix(ref value, tutorialState.SpawnOriginZ);
            Mix(ref value, tutorialState.CoreCurrentHealth);
            Mix(ref value, tutorialState.Enemies.Count);
            for (var index = 0; index < tutorialState.Enemies.Count; index++)
            {
                DefenseEnemyPersistenceState enemy =
                    tutorialState.Enemies[index];
                Mix(ref value, enemy.StableId);
                Mix(ref value, enemy.ArchetypeId);
                Mix(ref value, enemy.SpawnOrder);
                Mix(ref value, enemy.X);
                Mix(ref value, enemy.Z);
                Mix(ref value, enemy.CurrentHealth);
                Mix(ref value, enemy.MovementRemainder);
                Mix(ref value, enemy.AttackDamageRemainder);
            }
        }

        private static void Mix(ref ulong value, string text)
        {
            if (text == null)
            {
                Mix(ref value, -1);
                return;
            }
            Mix(ref value, text.Length);
            for (var index = 0; index < text.Length; index++)
            {
                value ^= text[index];
                value *= 1099511628211ul;
            }
        }

        private static void Mix(ref ulong value, bool item)
        {
            Mix(ref value, item ? 1 : 0);
        }

        private static void Mix(ref ulong value, int item)
        {
            unchecked
            {
                value ^= (uint)item;
                value *= 1099511628211ul;
            }
        }

        private static void Mix(ref ulong value, float item)
        {
            Mix(ref value, item.GetHashCode());
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
