using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;

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
        private readonly Dictionary<string, SingleCityDefenseTowerCombatModel>
            campaignTowerById =
                new Dictionary<string, SingleCityDefenseTowerCombatModel>(
                    StringComparer.Ordinal);
        private readonly List<SingleCityDefenseTowerCombatModel>
            campaignTowers =
                new List<SingleCityDefenseTowerCombatModel>();
        private readonly HashSet<string> campaignRetainedIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> campaignRunnableIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, GrayboxDefenseTowerStatus3D>
            campaignStatusById =
                new Dictionary<string, GrayboxDefenseTowerStatus3D>(
                    StringComparer.Ordinal);
        private readonly Func<DefenseBuildingCombatTarget[]>
            campaignBuildingTargetProvider;
        private readonly Func<string, string, int, int>
            campaignBuildingDamageApplier;
        private readonly Queue<GrayboxCombatDestructionResult3D>
            pendingPresentationRebuilds =
                new Queue<GrayboxCombatDestructionResult3D>();
        private TutorialDefenseRuntimeModel tutorial;
        private SingleCityDefenseCampaignModel campaign;
        private GrayboxBuildingHealthRuntime3D campaignBuildingHealth;
        private GrayboxCombatDestructionCoordinator3D
            campaignDestructionCoordinator;
        private DefenseBuildingCombatTarget[] campaignBuildingTargets =
            Array.Empty<DefenseBuildingCombatTarget>();
        private ulong campaignBuildingTargetFingerprint;
        private bool hasCampaignBuildingTargetFingerprint;
        private SingleCityDefenseCampaignSnapshot cachedCampaignSnapshot;
        private bool campaignSnapshotDirty = true;
        private bool campaignTriggered;
        private double accumulatorSeconds;
        private int tutorialWaveTriggerCount;
        private float requestedCoreX;
        private float requestedCoreZ;
        private ulong persistenceGeneration;
        private GrayboxDefenseRuntimeSnapshot3D cachedSnapshot;
        private bool snapshotDirty = true;
        private Func<string, bool> campaignPresentationRecovery;

        public GrayboxDefenseRuntime3D(
            float coreX,
            float coreZ,
            float spawnX,
            float spawnZ)
        {
            readOnlyTowers = towers.AsReadOnly();
            campaignBuildingTargetProvider = GetCampaignBuildingTargets;
            campaignBuildingDamageApplier = ApplyCampaignBuildingDamage;
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

        public GrayboxCombatDestructionResult3D LastDestructionResult
        {
            get;
            private set;
        }

        public int PendingPresentationRebuildCount =>
            pendingPresentationRebuilds.Count;
        public bool HasPresentationRecovery =>
            campaignPresentationRecovery != null;

        public SingleCityDefenseCampaignSnapshot CampaignSnapshot
        {
            get
            {
                if (campaign == null) return null;
                if (cachedCampaignSnapshot == null || campaignSnapshotDirty)
                {
                    cachedCampaignSnapshot = campaign.Snapshot;
                    campaignSnapshotDirty = false;
                }
                return cachedCampaignSnapshot;
            }
        }

        public void ConfigureFormalCampaign(
            SingleCityDefenseCampaignModel campaign,
            GrayboxBuildingHealthRuntime3D buildingHealth,
            GrayboxCombatDestructionCoordinator3D destructionCoordinator)
        {
            this.campaign = campaign ??
                throw new ArgumentNullException(nameof(campaign));
            campaignBuildingHealth = buildingHealth ??
                throw new ArgumentNullException(nameof(buildingHealth));
            campaignDestructionCoordinator = destructionCoordinator ??
                throw new ArgumentNullException(nameof(destructionCoordinator));
            campaignTowerById.Clear();
            campaignTowers.Clear();
            campaignRetainedIds.Clear();
            campaignRunnableIds.Clear();
            campaignStatusById.Clear();
            campaignBuildingTargets =
                Array.Empty<DefenseBuildingCombatTarget>();
            campaignBuildingTargetFingerprint = 0ul;
            hasCampaignBuildingTargetFingerprint = false;
            campaignTriggered = campaign.Snapshot.CurrentWaveNumber > 0;
            LastDestructionResult = null;
            pendingPresentationRebuilds.Clear();
            campaignPresentationRecovery = null;
            cachedCampaignSnapshot = null;
            campaignSnapshotDirty = true;
            cachedSnapshot = null;
            snapshotDirty = true;
        }

        public void ConfigurePresentationRecovery(
            Func<string, bool> tryRebuildPresentation)
        {
            campaignPresentationRecovery = tryRebuildPresentation ??
                throw new ArgumentNullException(
                    nameof(tryRebuildPresentation));
        }

        public void DetachPresentationRecovery()
        {
            campaignPresentationRecovery = null;
            pendingPresentationRebuilds.Clear();
        }

        public bool TryGetCampaignTowerState(
            string stableInstanceId,
            out SingleCityDefenseTowerCombatModel state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                campaignTowerById.TryGetValue(stableInstanceId, out state);
        }

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

        public GrayboxFormalDefenseCampaignPersistenceState3D
            CaptureFormalCampaignForPersistence()
        {
            if (campaign == null || campaignBuildingHealth == null)
            {
                throw new InvalidOperationException(
                    "正式单城防御战役尚未配置");
            }

            var capturedTowers = new SingleCityDefenseTowerPersistenceState[
                campaignTowerById.Count];
            var index = 0;
            foreach (SingleCityDefenseTowerCombatModel tower in
                     campaignTowerById.Values)
            {
                capturedTowers[index++] = tower.CaptureForPersistence();
            }
            Array.Sort(capturedTowers, (left, right) =>
                string.CompareOrdinal(
                    left.StableInstanceId,
                    right.StableInstanceId));
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] health =
                CloneHealth(campaignBuildingHealth.Capture());
            Array.Sort(health, (left, right) => string.CompareOrdinal(
                left.stableInstanceId,
                right.stableInstanceId));
            return new GrayboxFormalDefenseCampaignPersistenceState3D(
                campaign.CaptureForPersistence(),
                capturedTowers,
                health);
        }

        public bool TryPrepareFormalCampaignRestore(
            GrayboxFormalDefenseCampaignPersistenceState3D snapshot,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out GrayboxFormalDefenseCampaignRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (campaign == null || campaignBuildingHealth == null ||
                snapshot?.Campaign == null || snapshot.Towers == null ||
                snapshot.BuildingHealth == null || instances == null)
            {
                error = "正式防御战役恢复状态不完整";
                return false;
            }

            ulong expectedGeneration = persistenceGeneration;
            ulong expectedFingerprint =
                ComputeFormalCampaignPersistenceFingerprint();
            error = string.Empty;
            var instanceById = new Dictionary<
                string,
                GrayboxBuildingInstance3D>(StringComparer.Ordinal);
            var capturedInstances = new GrayboxBuildingInstance3D[
                instances.Count];
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance == null ||
                    string.IsNullOrWhiteSpace(instance.StableInstanceId) ||
                    instance.Placement?.Definition == null ||
                    instanceById.ContainsKey(instance.StableInstanceId))
                {
                    error = "正式防御战役引用的建筑实例无效或重复";
                    return false;
                }
                instanceById.Add(instance.StableInstanceId, instance);
                capturedInstances[index] = instance;
            }

            var restoredTowers = new SingleCityDefenseTowerCombatModel[
                snapshot.Towers.Count];
            var towerIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                SingleCityDefenseTowerPersistenceState tower =
                    snapshot.Towers[index];
                if (tower == null ||
                    !towerIds.Add(tower.StableInstanceId) ||
                    !instanceById.TryGetValue(
                        tower.StableInstanceId,
                        out GrayboxBuildingInstance3D instance) ||
                    !string.Equals(
                        instance.Placement.Definition.Id.Value,
                        tower.BuildingId,
                        StringComparison.Ordinal) ||
                    instance.Placement.X != tower.X ||
                    instance.Placement.Y != tower.Z ||
                    !SingleCityDefenseTowerCombatModel.TryCreateForPersistence(
                        tower,
                        tower.StableInstanceId,
                        out restoredTowers[index],
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "正式防御塔状态与建筑实例不一致";
                    return false;
                }
            }
            Array.Sort(restoredTowers, (left, right) =>
                string.CompareOrdinal(
                    left.StableInstanceId,
                    right.StableInstanceId));

            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] health =
                CloneHealth(snapshot.BuildingHealth);
            var healthValidator = new GrayboxBuildingHealthRuntime3D();
            if (!healthValidator.TryRestore(
                    health,
                    instances,
                    out error) ||
                !campaign.TryPrepareRestore(
                    snapshot.Campaign,
                    out SingleCityDefenseCampaignRestorePlan campaignPlan,
                    out error))
            {
                return false;
            }

            plan = new GrayboxFormalDefenseCampaignRestorePlan3D(
                this,
                expectedGeneration,
                expectedFingerprint,
                campaignPlan,
                restoredTowers,
                health,
                capturedInstances);
            error = string.Empty;
            return true;
        }

        public bool TryCommitFormalCampaignRestore(
            GrayboxFormalDefenseCampaignRestorePlan3D plan,
            out string error)
        {
            if (plan == null || plan.Consumed ||
                !ReferenceEquals(plan.Owner, this))
            {
                error = "正式防御战役恢复计划无效、已消费或不属于当前运行时";
                return false;
            }
            if (persistenceGeneration != plan.ExpectedGeneration ||
                ComputeFormalCampaignPersistenceFingerprint() !=
                    plan.ExpectedFingerprint)
            {
                error = "正式防御战役恢复计划已过期";
                return false;
            }
            if (!campaign.TryCommitRestore(plan.CampaignPlan, out error))
                return false;
            if (!campaignBuildingHealth.TryRestore(
                    plan.Health,
                    plan.Instances,
                    out error))
            {
                return false;
            }

            campaignTowerById.Clear();
            campaignTowers.Clear();
            campaignRetainedIds.Clear();
            campaignStatusById.Clear();
            for (var index = 0; index < plan.Towers.Length; index++)
            {
                SingleCityDefenseTowerCombatModel tower = plan.Towers[index];
                campaignTowerById.Add(tower.StableInstanceId, tower);
                campaignTowers.Add(tower);
                campaignRetainedIds.Add(tower.StableInstanceId);
                campaignStatusById.Add(
                    tower.StableInstanceId,
                    tower.IsPlayerPaused
                        ? GrayboxDefenseTowerStatus3D.PlayerPaused
                        : GrayboxDefenseTowerStatus3D.NoTarget);
            }
            campaignRunnableIds.RemoveWhere(
                stableId => !campaignRetainedIds.Contains(stableId));
            campaignTriggered = plan.CampaignPlan != null &&
                campaign.CaptureForPersistence().CurrentWaveNumber > 0;
            plan.Consumed = true;
            persistenceGeneration++;
            cachedCampaignSnapshot = null;
            campaignSnapshotDirty = true;
            cachedSnapshot = null;
            snapshotDirty = true;
            hasCampaignBuildingTargetFingerprint = false;
            RefreshCampaignBuildingTargets();
            error = string.Empty;
            return true;
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
            if (requestedCoreX == x && requestedCoreZ == z) return;
            requestedCoreX = x;
            requestedCoreZ = z;
            if (campaign == null)
                tutorial.SetCorePosition(x, z);
            else
                campaign.SetCorePosition(x, z);
            campaignSnapshotDirty = campaign != null;
            snapshotDirty = true;
            persistenceGeneration++;
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius)
        {
            Synchronize(
                instances,
                cityMode,
                cityX,
                cityY,
                groundRadius,
                allowCampaignStart: true);
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            bool allowCampaignStart = true)
        {
            if (campaign != null)
            {
                SynchronizeCampaign(
                    instances,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius,
                    allowCampaignStart);
                return;
            }

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

            if (campaign != null)
            {
                TickCampaign(deltaSeconds, cityStorage);
                return;
            }

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
            if (campaign != null)
            {
                if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                    !campaignTowerById.TryGetValue(
                        stableInstanceId,
                        out SingleCityDefenseTowerCombatModel campaignTower))
                {
                    return false;
                }
                if (campaignTower.IsPlayerPaused == paused)
                    return true;
                campaignTower.SetPlayerPaused(paused);
                campaignStatusById[stableInstanceId] = paused
                    ? GrayboxDefenseTowerStatus3D.PlayerPaused
                    : GrayboxDefenseTowerStatus3D.NoTarget;
                campaignSnapshotDirty = true;
                snapshotDirty = true;
                persistenceGeneration++;
                return true;
            }

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
            if (campaign != null && !string.IsNullOrWhiteSpace(
                    stableInstanceId) && campaignTowerById.TryGetValue(
                    stableInstanceId,
                    out SingleCityDefenseTowerCombatModel campaignTower))
            {
                if (campaignTower.LocalConsumableAmount > 0)
                {
                    lostResources = new[]
                    {
                        new ResourceAmount(
                            campaignTower.ConsumableId,
                            campaignTower.LocalConsumableAmount),
                    };
                }
                campaignTowerById.Remove(stableInstanceId);
                campaignTowers.Remove(campaignTower);
                campaignRetainedIds.Remove(stableInstanceId);
                campaignRunnableIds.Remove(stableInstanceId);
                campaignStatusById.Remove(stableInstanceId);
                campaignSnapshotDirty = true;
                snapshotDirty = true;
                persistenceGeneration++;
                return true;
            }
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

        private void SynchronizeCampaign(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            bool allowCampaignStart)
        {
            persistenceGeneration++;
            bool snapshotChanged = false;
            orderedInstances.Clear();
            campaignTowers.Clear();
            campaignRetainedIds.Clear();
            campaignRunnableIds.Clear();

            if (instances != null)
            {
                campaignBuildingHealth.Synchronize(instances);
                for (var index = 0; index < instances.Count; index++)
                {
                    if (instances[index] != null)
                        orderedInstances.Add(instances[index]);
                }
            }
            orderedInstances.Sort((left, right) => string.Compare(
                left.StableInstanceId,
                right.StableInstanceId,
                StringComparison.Ordinal));

            for (var index = 0; index < orderedInstances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = orderedInstances[index];
                if (!GrayboxBuildingOperationalAccess3D.CanRetainState(
                        instance) ||
                    !IsFormalCampaignTower(instance) ||
                    !campaignRetainedIds.Add(instance.StableInstanceId))
                {
                    continue;
                }

                if (!campaignTowerById.TryGetValue(
                        instance.StableInstanceId,
                        out SingleCityDefenseTowerCombatModel tower))
                {
                    tower = new SingleCityDefenseTowerCombatModel(
                        instance.StableInstanceId,
                        instance.Placement.Definition.Id.Value,
                        instance.Placement.X,
                        instance.Placement.Y);
                    campaignTowerById.Add(instance.StableInstanceId, tower);
                    campaignStatusById[instance.StableInstanceId] =
                        GrayboxDefenseTowerStatus3D.NoTarget;
                    snapshotChanged = true;
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
                if (tower.IsLogisticsConnected != connected)
                    snapshotChanged = true;
                tower.SetLogisticsConnected(connected);
                if (canRun)
                {
                    campaignRunnableIds.Add(instance.StableInstanceId);
                    if (campaignStatusById[instance.StableInstanceId] ==
                        GrayboxDefenseTowerStatus3D.Unavailable)
                    {
                        campaignStatusById[instance.StableInstanceId] =
                            tower.IsPlayerPaused
                                ? GrayboxDefenseTowerStatus3D.PlayerPaused
                                : GrayboxDefenseTowerStatus3D.NoTarget;
                        snapshotChanged = true;
                    }
                }
                else
                {
                    if (campaignStatusById[instance.StableInstanceId] !=
                        GrayboxDefenseTowerStatus3D.Unavailable)
                    {
                        snapshotChanged = true;
                    }
                    campaignStatusById[instance.StableInstanceId] =
                        GrayboxDefenseTowerStatus3D.Unavailable;
                }
                campaignTowers.Add(tower);

                if (allowCampaignStart &&
                    !campaignTriggered &&
                    campaign.NotifyDefenseTowerCompleted(
                        instance.StableInstanceId,
                        instance.Placement.Definition.Id.Value,
                        isCompleted: true,
                        isPlayerOwned: true))
                {
                    campaignTriggered = true;
                    snapshotChanged = true;
                }
            }

            removedIds.Clear();
            foreach (string stableInstanceId in campaignTowerById.Keys)
            {
                if (!campaignRetainedIds.Contains(stableInstanceId))
                    removedIds.Add(stableInstanceId);
            }
            for (var index = 0; index < removedIds.Count; index++)
            {
                string stableInstanceId = removedIds[index];
                campaignTowerById.Remove(stableInstanceId);
                campaignStatusById.Remove(stableInstanceId);
                snapshotChanged = true;
            }

            if (RefreshCampaignBuildingTargets())
                snapshotChanged = true;
            if (snapshotChanged)
            {
                campaignSnapshotDirty = true;
                snapshotDirty = true;
            }
        }

        private void TickCampaign(
            float deltaSeconds,
            CityResourceStorageModel cityStorage)
        {
            RetryPendingPresentationRebuilds();
            accumulatorSeconds += deltaSeconds;
            persistenceGeneration++;
            bool advancedFixedStep = false;
            while (accumulatorSeconds + StepEpsilon >= StepSeconds)
            {
                campaign.Advance(
                    StepSecondsFloat,
                    requestedSpeed: 1,
                    campaignBuildingTargetProvider,
                    campaignBuildingDamageApplier);
                for (var index = 0; index < campaignTowers.Count; index++)
                    TickCampaignTower(campaignTowers[index], cityStorage);
                accumulatorSeconds -= StepSeconds;
                if (accumulatorSeconds < 0d &&
                    accumulatorSeconds > -StepEpsilon)
                {
                    accumulatorSeconds = 0d;
                }
                advancedFixedStep = true;
            }
            if (!advancedFixedStep) return;
            campaignSnapshotDirty = true;
            snapshotDirty = true;
        }

        private void TickCampaignTower(
            SingleCityDefenseTowerCombatModel tower,
            CityResourceStorageModel cityStorage)
        {
            if (!campaignRunnableIds.Contains(tower.StableInstanceId))
                return;
            if (tower.IsPlayerPaused)
            {
                campaignStatusById[tower.StableInstanceId] =
                    GrayboxDefenseTowerStatus3D.PlayerPaused;
                return;
            }

            tower.RefillFrom(cityStorage);
            int damage = tower.Tick(
                StepSecondsFloat,
                campaign,
                globallyPaused: false);
            if (damage > 0)
            {
                campaignStatusById[tower.StableInstanceId] =
                    GrayboxDefenseTowerStatus3D.Firing;
                return;
            }
            if (string.IsNullOrEmpty(tower.TargetStableEnemyId))
            {
                campaignStatusById[tower.StableInstanceId] =
                    GrayboxDefenseTowerStatus3D.NoTarget;
                return;
            }
            campaignStatusById[tower.StableInstanceId] =
                !tower.IsLogisticsConnected &&
                cityStorage.GetNetworkAmount(tower.ConsumableId) > 0
                    ? GrayboxDefenseTowerStatus3D.OutOfLogistics
                    : GrayboxDefenseTowerStatus3D.MissingAmmunition;
        }

        private DefenseBuildingCombatTarget[] GetCampaignBuildingTargets()
        {
            return campaignBuildingTargets;
        }

        private int ApplyCampaignBuildingDamage(
            string stableEnemyId,
            string stableBuildingId,
            int damage)
        {
            if (!campaignBuildingHealth.TryApplyDamage(
                    stableBuildingId,
                    damage,
                    out int appliedDamage,
                    out bool destroyedNow))
            {
                return 0;
            }
            if (destroyedNow)
            {
                LastDestructionResult =
                    campaignDestructionCoordinator.Commit(stableBuildingId);
                if (LastDestructionResult.RequiresPresentationRebuild)
                    pendingPresentationRebuilds.Enqueue(
                        LastDestructionResult);
                RefreshCampaignBuildingTargets();
            }
            return appliedDamage;
        }

        private void RetryPendingPresentationRebuilds()
        {
            if (campaignPresentationRecovery == null) return;
            while (pendingPresentationRebuilds.Count > 0)
            {
                GrayboxCombatDestructionResult3D pending =
                    pendingPresentationRebuilds.Peek();
                bool recovered;
                try
                {
                    recovered = campaignPresentationRecovery(
                        pending.StableInstanceId);
                }
                catch (Exception)
                {
                    recovered = false;
                }
                if (!recovered) return;
                pendingPresentationRebuilds.Dequeue();
            }
        }

        private bool RefreshCampaignBuildingTargets()
        {
            ulong fingerprint = ComputeCampaignBuildingTargetFingerprint();
            if (hasCampaignBuildingTargetFingerprint &&
                fingerprint == campaignBuildingTargetFingerprint)
            {
                return false;
            }
            campaignBuildingTargetFingerprint = fingerprint;
            hasCampaignBuildingTargetFingerprint = true;
            if (orderedInstances.Count == 0)
            {
                campaignBuildingTargets =
                    Array.Empty<DefenseBuildingCombatTarget>();
                return true;
            }

            var targets = new List<DefenseBuildingCombatTarget>(
                orderedInstances.Count);
            for (var index = 0; index < orderedInstances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = orderedInstances[index];
                if (instance?.Placement?.Definition == null ||
                    !campaignBuildingHealth.TryGetHealth(
                        instance.StableInstanceId,
                        out _,
                        out _,
                        out bool destroyed))
                {
                    continue;
                }
                string buildingId = instance.Placement.Definition.Id.Value;
                targets.Add(new DefenseBuildingCombatTarget(
                    instance.StableInstanceId,
                    buildingId,
                    instance.Placement.X,
                    instance.Placement.Y,
                    instance.State == GrayboxBuildingInstanceState.Completed,
                    instance.IsPlayerOwned,
                    destroyed,
                    instance.IsEvacuationLocked,
                    FormalProductionDefinitionCatalog.TryGetByBuildingId(
                        buildingId,
                        out _)));
            }
            campaignBuildingTargets = targets.Count == 0
                ? Array.Empty<DefenseBuildingCombatTarget>()
                : targets.ToArray();
            return true;
        }

        private ulong ComputeCampaignBuildingTargetFingerprint()
        {
            ulong value = 1469598103934665603ul;
            Mix(ref value, orderedInstances.Count);
            for (var index = 0; index < orderedInstances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = orderedInstances[index];
                Mix(ref value, instance?.StableInstanceId);
                if (instance?.Placement?.Definition == null)
                {
                    Mix(ref value, -1);
                    continue;
                }
                string buildingId = instance.Placement.Definition.Id.Value;
                Mix(ref value, buildingId);
                Mix(ref value, instance.Placement.X);
                Mix(ref value, instance.Placement.Y);
                Mix(ref value, (int)instance.State);
                Mix(ref value, instance.IsPlayerOwned);
                Mix(ref value, instance.IsEvacuationLocked);
                bool hasHealth = campaignBuildingHealth.TryGetHealth(
                    instance.StableInstanceId,
                    out _,
                    out _,
                    out bool destroyed);
                Mix(ref value, hasHealth);
                Mix(ref value, destroyed);
                Mix(ref value,
                    FormalProductionDefinitionCatalog.TryGetByBuildingId(
                        buildingId,
                        out _));
            }
            return value;
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
            if (campaign != null)
                return CaptureCampaignSnapshot();

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

        private GrayboxDefenseRuntimeSnapshot3D CaptureCampaignSnapshot()
        {
            SingleCityDefenseCampaignSnapshot source = CampaignSnapshot;
            var towerSnapshots =
                new GrayboxDefenseTowerSnapshot3D[campaignTowers.Count];
            for (var index = 0; index < campaignTowers.Count; index++)
            {
                SingleCityDefenseTowerCombatModel tower =
                    campaignTowers[index];
                bool canRun = campaignRunnableIds.Contains(
                    tower.StableInstanceId);
                GrayboxDefenseTowerStatus3D status = canRun &&
                    campaignStatusById.TryGetValue(
                        tower.StableInstanceId,
                        out GrayboxDefenseTowerStatus3D currentStatus)
                        ? currentStatus
                        : GrayboxDefenseTowerStatus3D.Unavailable;
                towerSnapshots[index] = new GrayboxDefenseTowerSnapshot3D(
                    tower.StableInstanceId,
                    tower.LocalConsumableAmount,
                    tower.LocalCapacity,
                    tower.Range,
                    tower.IsLogisticsConnected,
                    canRun,
                    tower.IsPlayerPaused,
                    canRun ? tower.TargetStableEnemyId : null,
                    status);
            }

            var enemySnapshots =
                new GrayboxDefenseEnemySnapshot3D[source.Enemies.Count];
            for (var index = 0; index < source.Enemies.Count; index++)
            {
                SingleCityDefenseEnemySnapshot enemy = source.Enemies[index];
                EnemyDefinition definition = FindEnemyDefinition(
                    enemy.EnemyDefinitionId);
                float distanceToCore = Distance(
                    enemy.X,
                    enemy.Z,
                    requestedCoreX,
                    requestedCoreZ);
                bool targetsCore = string.Equals(
                    enemy.TargetStableId,
                    SingleCityDefenseCampaignModel.CityCoreTargetId,
                    StringComparison.Ordinal) ||
                    string.IsNullOrEmpty(enemy.TargetStableId);
                enemySnapshots[index] = new GrayboxDefenseEnemySnapshot3D(
                    enemy.StableId,
                    enemy.SpawnOrder,
                    enemy.X,
                    enemy.Z,
                    enemy.CurrentHealth,
                    targetsCore
                        ? DefenseEnemyRuntimeSnapshot.CityCoreTargetName
                        : enemy.TargetStableId,
                    distanceToCore,
                    targetsCore && definition != null &&
                    distanceToCore <= definition.AttackRange);
            }

            return new GrayboxDefenseRuntimeSnapshot3D(
                campaignTriggered ? 1 : 0,
                ToLegacyWavePhase(source.Phase),
                source.WarningRemainingSeconds,
                source.SpawnedEnemyCount,
                source.AliveEnemyCount,
                source.Statistics.TotalKillCount,
                source.CoreMaximumHealth,
                source.CoreCurrentHealth,
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

        private ulong ComputeFormalCampaignPersistenceFingerprint()
        {
            GrayboxFormalDefenseCampaignPersistenceState3D snapshot =
                CaptureFormalCampaignForPersistence();
            SingleCityDefenseCampaignSnapshot campaignSnapshot =
                CampaignSnapshot;
            ulong value = 1469598103934665603ul;
            Mix(ref value, (int)campaignSnapshot.Phase);
            Mix(ref value, campaignSnapshot.CurrentWaveNumber);
            Mix(ref value, campaignSnapshot.WarningRemainingSeconds);
            Mix(ref value, campaignSnapshot.SpawnedEnemyCount);
            Mix(ref value, campaignSnapshot.CoreCurrentHealth);
            Mix(ref value, (int)campaignSnapshot.Result);
            Mix(ref value, campaignSnapshot.Enemies.Count);
            for (var index = 0;
                 index < campaignSnapshot.Enemies.Count;
                 index++)
            {
                SingleCityDefenseEnemySnapshot enemy =
                    campaignSnapshot.Enemies[index];
                Mix(ref value, enemy.StableId);
                Mix(ref value, enemy.EnemyDefinitionId);
                Mix(ref value, enemy.SpawnOrder);
                Mix(ref value, enemy.X);
                Mix(ref value, enemy.Z);
                Mix(ref value, enemy.CurrentHealth);
                Mix(ref value, enemy.TargetStableId);
            }
            Mix(ref value, snapshot.Towers.Count);
            for (var index = 0; index < snapshot.Towers.Count; index++)
            {
                SingleCityDefenseTowerPersistenceState tower =
                    snapshot.Towers[index];
                Mix(ref value, tower.StableInstanceId);
                Mix(ref value, tower.BuildingId);
                Mix(ref value, tower.X);
                Mix(ref value, tower.Z);
                Mix(ref value, tower.LocalConsumableAmount);
                Mix(ref value, tower.ActiveConsumableSeconds);
                Mix(ref value, tower.DamageRemainder);
                Mix(ref value, tower.TargetStableEnemyId);
                Mix(ref value, tower.IsLogisticsConnected);
                Mix(ref value, tower.IsPlayerPaused);
            }
            Mix(ref value, snapshot.BuildingHealth.Count);
            for (var index = 0;
                 index < snapshot.BuildingHealth.Count;
                 index++)
            {
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData health =
                    snapshot.BuildingHealth[index];
                Mix(ref value, health.stableInstanceId);
                Mix(ref value, health.currentHealth);
                Mix(ref value, health.isDestroyed);
            }
            return value;
        }

        private static
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
            CloneHealth(
                IReadOnlyList<
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData>
                    source)
        {
            if (source == null)
            {
                return Array.Empty<
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData>();
            }
            var result =
                new FormalThreeDDefenseCampaignBuildingHealthStateSaveData[
                    source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData item =
                    source[index];
                result[index] = item == null
                    ? null
                    : new
                        FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                        {
                            stableInstanceId = item.stableInstanceId,
                            currentHealth = item.currentHealth,
                            isDestroyed = item.isDestroyed,
                        };
            }
            return result;
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

        private static bool IsFormalCampaignTower(
            GrayboxBuildingInstance3D instance)
        {
            if (instance?.Placement?.Definition == null) return false;
            string buildingId = instance.Placement.Definition.Id.Value;
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

        private static EnemyDefinition FindEnemyDefinition(string stableId)
        {
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

        private static WavePhase ToLegacyWavePhase(
            SingleCityDefenseCampaignPhase phase)
        {
            switch (phase)
            {
                case SingleCityDefenseCampaignPhase.Warning:
                    return WavePhase.Warning;
                case SingleCityDefenseCampaignPhase.SpawningAndCombat:
                    return WavePhase.Spawning;
                case SingleCityDefenseCampaignPhase.CombatCleanup:
                case SingleCityDefenseCampaignPhase.Victory:
                case SingleCityDefenseCampaignPhase.Defeat:
                    return WavePhase.Active;
                default:
                    return WavePhase.Idle;
            }
        }

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
}
