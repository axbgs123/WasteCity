using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;
using WasteCity.Research;

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
                status,
                activeConsumableSeconds: 0f)
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
            : this(
                stableId,
                ammo,
                ammoCapacity,
                range,
                connected,
                canRunLocally,
                playerPaused,
                targetId,
                status,
                activeConsumableSeconds: 0f)
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
            GrayboxDefenseTowerStatus3D status,
            float activeConsumableSeconds)
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
            ActiveConsumableSeconds = Math.Max(0f, activeConsumableSeconds);
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
        public float ActiveConsumableSeconds { get; }
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
                EnemyCatalog.Gnawer.Id.Value,
                spawnOrder,
                x,
                z,
                currentHealth,
                EnemyCatalog.Gnawer.MaximumHealth,
                DefenseEnemyRuntimeSnapshot.CityCoreTargetName,
                Distance(x, z, 0f, 0f),
                isAttackingCore,
                SingleCityDefenseCampaignModel.CityCoreTargetId,
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
            : this(
                stableId,
                EnemyCatalog.Gnawer.Id.Value,
                spawnOrder,
                x,
                z,
                currentHealth,
                EnemyCatalog.Gnawer.MaximumHealth,
                targetName,
                distanceToCore,
                isAttackingCore,
                CoreTargetIdFor(targetName),
                targetName,
                distanceToCore,
                isAttackingCore)
        {
        }

        public GrayboxDefenseEnemySnapshot3D(
            string stableId,
            string enemyDefinitionId,
            int spawnOrder,
            float x,
            float z,
            int currentHealth,
            int maximumHealth,
            string targetName,
            float distanceToCore,
            bool isAttackingCore)
            : this(
                stableId,
                enemyDefinitionId,
                spawnOrder,
                x,
                z,
                currentHealth,
                maximumHealth,
                targetName,
                distanceToCore,
                isAttackingCore,
                CoreTargetIdFor(targetName),
                targetName,
                distanceToCore,
                isAttackingCore)
        {
        }

        public GrayboxDefenseEnemySnapshot3D(
            string stableId,
            string enemyDefinitionId,
            int spawnOrder,
            float x,
            float z,
            int currentHealth,
            int maximumHealth,
            string targetName,
            float distanceToCore,
            bool isAttackingCore,
            string targetStableId,
            string targetDisplayName,
            float distanceToTarget,
            bool isAttackingTarget)
        {
            StableId = stableId;
            EnemyDefinitionId = enemyDefinitionId ?? string.Empty;
            SpawnOrder = spawnOrder;
            X = x;
            Z = z;
            CurrentHealth = Math.Max(0, currentHealth);
            MaximumHealth = Math.Max(1, maximumHealth);
            TargetName = targetName;
            DistanceToCore = distanceToCore;
            IsAttackingCore = isAttackingCore;
            TargetStableId = targetStableId ?? string.Empty;
            TargetDisplayName = targetDisplayName ?? string.Empty;
            DistanceToTarget = Math.Max(0f, distanceToTarget);
            IsAttackingTarget = isAttackingTarget;
        }

        public string StableId { get; }
        public string EnemyDefinitionId { get; }
        public int SpawnOrder { get; }
        public float X { get; }
        public float Z { get; }
        public int CurrentHealth { get; }
        public int MaximumHealth { get; }
        public string TargetName { get; }
        public float DistanceToCore { get; }
        public bool IsAttackingCore { get; }
        public string TargetStableId { get; }
        public string TargetDisplayName { get; }
        public float DistanceToTarget { get; }
        public bool IsAttackingTarget { get; }

        private static string CoreTargetIdFor(string targetName)
        {
            return string.Equals(
                    targetName,
                    DefenseEnemyRuntimeSnapshot.CityCoreTargetName,
                    StringComparison.Ordinal)
                ? SingleCityDefenseCampaignModel.CityCoreTargetId
                : string.Empty;
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

    public readonly struct GrayboxDefenseSettledAttackEvent3D
    {
        public GrayboxDefenseSettledAttackEvent3D(
            ulong eventSequence,
            ulong settlementSequence,
            string towerStableId,
            string targetStableId,
            int appliedDamage)
        {
            if (eventSequence == 0ul)
                throw new ArgumentOutOfRangeException(nameof(eventSequence));
            if (settlementSequence == 0ul)
                throw new ArgumentOutOfRangeException(
                    nameof(settlementSequence));
            if (string.IsNullOrWhiteSpace(towerStableId))
                throw new ArgumentException(
                    "A tower stable ID is required.",
                    nameof(towerStableId));
            if (string.IsNullOrWhiteSpace(targetStableId))
                throw new ArgumentException(
                    "A target stable ID is required.",
                    nameof(targetStableId));
            if (appliedDamage <= 0)
                throw new ArgumentOutOfRangeException(nameof(appliedDamage));

            EventSequence = eventSequence;
            SettlementSequence = settlementSequence;
            TowerStableId = towerStableId;
            TargetStableId = targetStableId;
            AppliedDamage = appliedDamage;
        }

        public ulong EventSequence { get; }
        public ulong SettlementSequence { get; }
        public string TowerStableId { get; }
        public string TargetStableId { get; }
        public int AppliedDamage { get; }
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
            : this(
                tutorialWaveTriggerCount,
                wavePhase,
                warningRemainingSeconds,
                spawnedEnemyCount,
                aliveEnemyCount,
                defeatedEnemyCount,
                coreMaximumHealth,
                coreCurrentHealth,
                towers,
                enemies,
                settledAttackEvents: null,
                spawnDirections: null,
                waveComposition: tutorialWaveTriggerCount > 0
                    ? WaveCatalog.Tutorial.Entries
                    : Array.Empty<WaveEntry>())
        {
        }

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
            IReadOnlyList<GrayboxDefenseEnemySnapshot3D> enemies,
            IReadOnlyList<GrayboxDefenseSettledAttackEvent3D>
                settledAttackEvents = null,
            IReadOnlyList<CampaignSpawnDirection> spawnDirections = null,
            IReadOnlyList<WaveEntry> waveComposition = null)
            : this(
                tutorialWaveTriggerCount,
                tutorialWaveTriggerCount > 0 ? 1 : 0,
                CampaignWaveCatalog.All.Count,
                ToCampaignPhase(wavePhase, coreCurrentHealth),
                wavePhase,
                warningRemainingSeconds,
                tutorialWaveTriggerCount > 0
                    ? CampaignWaveCatalog.All[0].TotalCount
                    : 0,
                spawnedEnemyCount,
                aliveEnemyCount,
                defeatedEnemyCount,
                coreMaximumHealth,
                coreCurrentHealth,
                towers,
                enemies,
                settledAttackEvents,
                spawnDirections,
                waveComposition)
        {
        }

        public GrayboxDefenseRuntimeSnapshot3D(
            int tutorialWaveTriggerCount,
            int currentWaveNumber,
            int totalWaveCount,
            SingleCityDefenseCampaignPhase campaignPhase,
            WavePhase wavePhase,
            float warningRemainingSeconds,
            int plannedEnemyCount,
            int spawnedEnemyCount,
            int aliveEnemyCount,
            int defeatedEnemyCount,
            int coreMaximumHealth,
            int coreCurrentHealth,
            IReadOnlyList<GrayboxDefenseTowerSnapshot3D> towers,
            IReadOnlyList<GrayboxDefenseEnemySnapshot3D> enemies)
            : this(
                tutorialWaveTriggerCount,
                currentWaveNumber,
                totalWaveCount,
                campaignPhase,
                wavePhase,
                warningRemainingSeconds,
                plannedEnemyCount,
                spawnedEnemyCount,
                aliveEnemyCount,
                defeatedEnemyCount,
                coreMaximumHealth,
                coreCurrentHealth,
                towers,
                enemies,
                settledAttackEvents: null,
                spawnDirections: CatalogDirections(currentWaveNumber),
                waveComposition: CatalogComposition(currentWaveNumber))
        {
        }

        public GrayboxDefenseRuntimeSnapshot3D(
            int tutorialWaveTriggerCount,
            int currentWaveNumber,
            int totalWaveCount,
            SingleCityDefenseCampaignPhase campaignPhase,
            WavePhase wavePhase,
            float warningRemainingSeconds,
            int plannedEnemyCount,
            int spawnedEnemyCount,
            int aliveEnemyCount,
            int defeatedEnemyCount,
            int coreMaximumHealth,
            int coreCurrentHealth,
            IReadOnlyList<GrayboxDefenseTowerSnapshot3D> towers,
            IReadOnlyList<GrayboxDefenseEnemySnapshot3D> enemies,
            IReadOnlyList<GrayboxDefenseSettledAttackEvent3D>
                settledAttackEvents = null,
            IReadOnlyList<CampaignSpawnDirection> spawnDirections = null,
            IReadOnlyList<WaveEntry> waveComposition = null)
        {
            TutorialWaveTriggerCount = Math.Max(0, tutorialWaveTriggerCount);
            TotalWaveCount = Math.Max(1, totalWaveCount);
            CurrentWaveNumber = Math.Max(
                0,
                Math.Min(TotalWaveCount, currentWaveNumber));
            CampaignPhase = campaignPhase;
            WavePhase = wavePhase;
            WarningRemainingSeconds = Math.Max(0f, warningRemainingSeconds);
            PlannedEnemyCount = Math.Max(0, plannedEnemyCount);
            SpawnedEnemyCount = Math.Max(0, spawnedEnemyCount);
            AliveEnemyCount = Math.Max(0, aliveEnemyCount);
            DefeatedEnemyCount = Math.Max(0, defeatedEnemyCount);
            CoreMaximumHealth = Math.Max(1, coreMaximumHealth);
            CoreCurrentHealth = Math.Max(0, coreCurrentHealth);
            Towers = towers ?? throw new ArgumentNullException(nameof(towers));
            Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
            SettledAttackEvents = CopySettledAttackEvents(
                settledAttackEvents);
            SpawnDirections = FreezeSpawnDirections(spawnDirections);
            WaveComposition = FreezeWaveComposition(waveComposition);
        }

        public int TutorialWaveTriggerCount { get; }
        public int CurrentWaveNumber { get; }
        public int TotalWaveCount { get; }
        public SingleCityDefenseCampaignPhase CampaignPhase { get; }
        public WavePhase WavePhase { get; }
        public float WarningRemainingSeconds { get; }
        public int PlannedEnemyCount { get; }
        public int SpawnedEnemyCount { get; }
        public int AliveEnemyCount { get; }
        public int DefeatedEnemyCount { get; }
        public int CoreMaximumHealth { get; }
        public int CoreCurrentHealth { get; }
        public bool IsCoreDestroyed => CoreCurrentHealth <= 0;
        public IReadOnlyList<GrayboxDefenseTowerSnapshot3D> Towers { get; }
        public IReadOnlyList<GrayboxDefenseEnemySnapshot3D> Enemies { get; }
        public IReadOnlyList<GrayboxDefenseSettledAttackEvent3D>
            SettledAttackEvents { get; }
        public IReadOnlyList<CampaignSpawnDirection> SpawnDirections { get; }
        public IReadOnlyList<WaveEntry> WaveComposition { get; }

        private static IReadOnlyList<GrayboxDefenseSettledAttackEvent3D>
            CopySettledAttackEvents(
                IReadOnlyList<GrayboxDefenseSettledAttackEvent3D> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<GrayboxDefenseSettledAttackEvent3D>();
            var copy = new GrayboxDefenseSettledAttackEvent3D[source.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = source[index];
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<CampaignSpawnDirection>
            FreezeSpawnDirections(
                IReadOnlyList<CampaignSpawnDirection> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<CampaignSpawnDirection>();
            for (var waveIndex = 0;
                 waveIndex < CampaignWaveCatalog.All.Count;
                 waveIndex++)
            {
                if (ReferenceEquals(
                        source,
                        CampaignWaveCatalog.All[waveIndex].Directions))
                {
                    return source;
                }
            }
            var copy = new CampaignSpawnDirection[source.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = source[index];
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<WaveEntry> FreezeWaveComposition(
            IReadOnlyList<WaveEntry> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<WaveEntry>();
            if (ReferenceEquals(source, WaveCatalog.Tutorial.Entries))
                return source;
            for (var waveIndex = 0;
                 waveIndex < CampaignWaveCatalog.All.Count;
                 waveIndex++)
            {
                if (ReferenceEquals(
                        source,
                        CampaignWaveCatalog.All[waveIndex].Entries))
                {
                    return source;
                }
            }
            var copy = new WaveEntry[source.Count];
            for (var index = 0; index < copy.Length; index++)
                copy[index] = source[index];
            return Array.AsReadOnly(copy);
        }

        private static IReadOnlyList<CampaignSpawnDirection>
            CatalogDirections(int currentWaveNumber)
        {
            return currentWaveNumber > 0 &&
                currentWaveNumber <= CampaignWaveCatalog.All.Count
                    ? CampaignWaveCatalog.All[currentWaveNumber - 1].Directions
                    : Array.Empty<CampaignSpawnDirection>();
        }

        private static IReadOnlyList<WaveEntry> CatalogComposition(
            int currentWaveNumber)
        {
            return currentWaveNumber > 0 &&
                currentWaveNumber <= CampaignWaveCatalog.All.Count
                    ? CampaignWaveCatalog.All[currentWaveNumber - 1].Entries
                    : Array.Empty<WaveEntry>();
        }

        private static SingleCityDefenseCampaignPhase ToCampaignPhase(
            WavePhase wavePhase,
            int coreCurrentHealth)
        {
            if (coreCurrentHealth <= 0)
                return SingleCityDefenseCampaignPhase.Defeat;
            switch (wavePhase)
            {
                case WavePhase.Warning:
                    return SingleCityDefenseCampaignPhase.Warning;
                case WavePhase.Spawning:
                case WavePhase.Active:
                    return SingleCityDefenseCampaignPhase.SpawningAndCombat;
                default:
                    return SingleCityDefenseCampaignPhase.Idle;
            }
        }
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
        private readonly List<GrayboxDefenseSettledAttackEvent3D>
            settledAttackEvents =
                new List<GrayboxDefenseSettledAttackEvent3D>(24);
        private readonly Func<DefenseBuildingCombatTarget[]>
            campaignBuildingTargetProvider;
        private readonly Func<string, string, int, int>
            campaignBuildingDamageApplier;
        private readonly Queue<GrayboxCombatDestructionResult3D>
            pendingPresentationRebuilds =
                new Queue<GrayboxCombatDestructionResult3D>();
        private readonly Dictionary<string, GrayboxCombatDestructionResult3D>
            destructionResultsByStableId =
                new Dictionary<string, GrayboxCombatDestructionResult3D>(
                    StringComparer.Ordinal);
        private TutorialDefenseRuntimeModel tutorial;
        private SingleCityDefenseCampaignModel campaign;
        private SingleCityDefenseCampaignModel activePressureCampaign;
        private string activePressureEncounterId;
        private CrystalBroodmotherEncounter activeBroodmotherEncounter;
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
        private ulong settledAttackEventSequence;
        private ulong settlementSequence;
        private float warningMultiplier = 1f;
        private float buildingHealthMultiplier = 1f;
        private readonly SingleCityDefenseTechnologyRuntime
            mainTechnologyStates =
            new SingleCityDefenseTechnologyRuntime();
        private SingleCityDefenseTechnologyRuntime pressureTechnologyStates =
            new SingleCityDefenseTechnologyRuntime();
        private SingleCityDefenseTechnologyUnlocks technologyUnlocks;

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

        public SingleCityDefenseCampaignSnapshot ActiveCampaignSnapshot =>
            ActiveCampaign?.Snapshot;
        public string ActivePressureEncounterId =>
            activePressureEncounterId ?? string.Empty;
        public bool HasActivePressureCampaign =>
            activePressureCampaign != null && !activePressureCampaign.IsTerminal;
        public SingleCityDefenseTechnologyStateSnapshot TechnologyState =>
            ActiveTechnologyStates.Snapshot;
        public GrayboxBuildingTechnologySnapshot3D BuildingTechnologyState =>
            campaignBuildingHealth?.TechnologySnapshot;

        public bool TryClearTechnologyFixturesForDevelopment()
        {
            bool changed = mainTechnologyStates.ClearForDevelopment();
            changed |= pressureTechnologyStates.ClearForDevelopment();
            if (campaignBuildingHealth != null)
                changed |= campaignBuildingHealth
                    .TryClearTechnologyFixturesForDevelopment();
            if (!changed) return false;
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            unchecked { persistenceGeneration++; }
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool TrySetEnemyTechnologyStatusForDevelopment(
            string stableEnemyId,
            string statusId,
            bool fillStacks)
        {
            return RecordTechnologyFixtureChange(
                ActiveTechnologyStates.TrySetEnemyStatusForDevelopment(
                    stableEnemyId,
                    statusId,
                    fillStacks));
        }

        public bool TryClearEnemyTechnologyStatusForDevelopment(
            string stableEnemyId,
            string statusId)
        {
            return RecordTechnologyFixtureChange(
                ActiveTechnologyStates.TryClearEnemyStatusForDevelopment(
                    stableEnemyId,
                    statusId));
        }

        public bool TryExpireEnemyTechnologyStatusForDevelopment(
            string stableEnemyId,
            string statusId)
        {
            return RecordTechnologyFixtureChange(
                ActiveTechnologyStates.TryExpireEnemyStatusForDevelopment(
                    stableEnemyId,
                    statusId));
        }

        public bool TryExpireTechnologyOverloadForDevelopment(
            string towerStableId)
        {
            return RecordTechnologyFixtureChange(
                ActiveTechnologyStates.TryExpireOverloadForDevelopment(
                    towerStableId));
        }

        public bool TryClearTechnologyOverloadForDevelopment(
            string towerStableId)
        {
            return RecordTechnologyFixtureChange(
                ActiveTechnologyStates.TryClearOverloadForDevelopment(
                    towerStableId));
        }

        private bool RecordTechnologyFixtureChange(bool changed)
        {
            if (!changed) return false;
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            unchecked { persistenceGeneration++; }
            return true;
        }
#endif

        public SingleCityDefenseTechnologyPersistenceSnapshot
            CaptureTechnologyForPersistence()
        {
            return ActiveTechnologyStates.CaptureForPersistence();
        }

        public bool TryRestoreTechnologyForPersistence(
            SingleCityDefenseTechnologyPersistenceSnapshot snapshot,
            out string error)
        {
            SingleCityDefenseCampaignSnapshot campaignSnapshot =
                ActiveCampaignSnapshot;
            SingleCityDefenseTechnologyRuntime technology =
                ActiveTechnologyStates;
            technology.SynchronizeEnemies(campaignSnapshot?.Enemies);
            if (!technology.TryRestore(snapshot, out error))
                return false;
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            unchecked { persistenceGeneration++; }
            return true;
        }

        public bool TryRestoreBuildingTechnologyForPersistence(
            IReadOnlyList<GrayboxBuildingTechnologyStateSnapshot3D> states,
            int coreShield,
            out string error)
        {
            if (coreShield < 0 ||
                coreShield > SingleCityDefenseTechnologyRules.MaximumShield)
            {
                error = "城市核心护盾状态无效";
                return false;
            }
            SingleCityDefenseCampaignModel owner = ActiveCampaign;
            if (owner == null && coreShield != 0)
            {
                error = "城市核心护盾无法在当前战役状态恢复";
                return false;
            }
            GrayboxBuildingTechnologySnapshot3D buildingBefore =
                campaignBuildingHealth?.TechnologySnapshot;
            if (campaignBuildingHealth != null &&
                !campaignBuildingHealth.TryRestoreTechnologyState(
                    states ?? Array.Empty<
                        GrayboxBuildingTechnologyStateSnapshot3D>(),
                    out error))
            {
                return false;
            }
            if (owner != null &&
                !owner.TryRestoreCoreShieldForPersistence(
                    coreShield,
                    SingleCityDefenseTechnologyRules.MaximumShield,
                    out error))
            {
                campaignBuildingHealth?.TryRestoreTechnologyState(
                    buildingBefore?.Buildings ?? Array.Empty<
                        GrayboxBuildingTechnologyStateSnapshot3D>(),
                    out _);
                return false;
            }
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            unchecked { persistenceGeneration++; }
            error = string.Empty;
            return true;
        }

        public void ConfigureTechnologyStates(
            SingleCityDefenseTechnologyUnlocks unlocks)
        {
            if (TechnologyUnlocksEqual(technologyUnlocks, unlocks)) return;
            technologyUnlocks = unlocks;
            mainTechnologyStates.Configure(unlocks);
            pressureTechnologyStates.Configure(unlocks);
            campaignBuildingHealth?.ConfigureTechnologySupport(
                unlocks.WallPhysicalDamageMultiplier,
                unlocks.AutomatedRepair,
                unlocks.MindShield);
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            persistenceGeneration++;
        }

        public bool TryActivateTechnologyOverload(string towerStableId)
        {
            if (string.IsNullOrWhiteSpace(towerStableId) ||
                !campaignTowerById.TryGetValue(
                    towerStableId,
                    out SingleCityDefenseTowerCombatModel tower))
            {
                return false;
            }
            bool activated = ActiveTechnologyStates.TryActivateOverload(
                towerStableId,
                tower.BuildingId);
            if (!activated) return false;
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            persistenceGeneration++;
            return true;
        }

        private static bool TechnologyUnlocksEqual(
            SingleCityDefenseTechnologyUnlocks left,
            SingleCityDefenseTechnologyUnlocks right)
        {
            return left.EnergyOverload == right.EnergyOverload &&
                left.SwordIntent == right.SwordIntent &&
                left.Infection == right.Infection &&
                left.Resonance == right.Resonance &&
                left.MindControl == right.MindControl &&
                left.AcidSpit == right.AcidSpit &&
                left.TalismanBasics == right.TalismanBasics &&
                left.AutomatedRepair == right.AutomatedRepair &&
                left.MindShield == right.MindShield &&
                left.AcidHeavyDamageMultiplier ==
                    right.AcidHeavyDamageMultiplier &&
                left.WallPhysicalDamageMultiplier ==
                    right.WallPhysicalDamageMultiplier;
        }

        public int TryApplyArmyGuardDamage(
            int rawDamage,
            DamageType damageType)
        {
            SingleCityDefenseCampaignModel targetCampaign = ActiveCampaign;
            SingleCityDefenseCampaignSnapshot targetSnapshot =
                targetCampaign?.Snapshot;
            if (targetSnapshot == null || rawDamage <= 0) return 0;
            for (var index = 0; index < targetSnapshot.Enemies.Count; index++)
            {
                SingleCityDefenseEnemySnapshot enemy =
                    targetSnapshot.Enemies[index];
                if (enemy.CurrentHealth <= 0) continue;
                int applied = targetCampaign.ApplyFriendlyUnitDamage(
                    enemy.StableId,
                    SingleCityArmyModel.DefaultSquadId,
                    rawDamage,
                    damageType);
                if (applied > 0)
                {
                    campaignSnapshotDirty = true;
                    snapshotDirty = true;
                }
                return applied;
            }
            return 0;
        }

        public event Action<string, SingleCityDefenseCampaignResult>
            PressureCampaignTerminalCommitted;
        public event Action<string> CrystalBroodmotherDefeated;
        public event Action<string, string> EnemyDefeatedForRewards;

        private SingleCityDefenseCampaignModel ActiveCampaign =>
            activePressureCampaign != null && !activePressureCampaign.IsTerminal
                ? activePressureCampaign
                : campaign;

        private SingleCityDefenseTechnologyRuntime ActiveTechnologyStates =>
            activePressureCampaign != null &&
            !activePressureCampaign.IsTerminal
                ? pressureTechnologyStates
                : mainTechnologyStates;

        private SingleCityDefenseTechnologyRuntime TechnologyFor(
            SingleCityDefenseCampaignModel owner)
        {
            return ReferenceEquals(owner, activePressureCampaign)
                ? pressureTechnologyStates
                : mainTechnologyStates;
        }

        private void ResetPressureTechnologyStates()
        {
            pressureTechnologyStates =
                new SingleCityDefenseTechnologyRuntime();
            pressureTechnologyStates.Configure(technologyUnlocks);
        }

        public bool TryStartPressure(
            SingleCityDefenseCampaignDefinition definition,
            out string error)
        {
            if (definition == null || campaign == null ||
                campaign.Snapshot.Result !=
                    SingleCityDefenseCampaignResult.Victory ||
                activePressureCampaign != null)
            {
                error = "十波战役尚未胜利或已有压力遭遇";
                return false;
            }
            var pressure = new SingleCityDefenseCampaignModel(
                requestedCoreX, requestedCoreZ, definition);
            pressure.SetWarningMultiplier(warningMultiplier);
            if (!pressure.TryStartAfterExternalWarning())
            {
                error = "压力遭遇定义无法启动";
                return false;
            }
            activePressureCampaign = pressure;
            ResetPressureTechnologyStates();
            activePressureEncounterId = definition.Id;
            pressure.TerminalCommitted += HandlePressureTerminal;
            pressure.EnemyDefeated += HandlePressureEnemyDefeated;
            activeBroodmotherEncounter = null;
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            error = string.Empty;
            return true;
        }

        public SingleCityDefenseCampaignPersistenceState
            CaptureActivePressurePersistence()
        {
            return activePressureCampaign?.CaptureForPersistence();
        }

        public bool TryRestoreActivePressure(
            SingleCityDefenseCampaignDefinition definition,
            SingleCityDefenseCampaignPersistenceState state,
            out string error)
        {
            if (definition == null || state == null || campaign == null ||
                campaign.Snapshot.Result !=
                    SingleCityDefenseCampaignResult.Victory ||
                activePressureCampaign != null ||
                !string.Equals(
                    definition.Id,
                    state.CampaignId,
                    StringComparison.Ordinal))
            {
                error = "压力遭遇恢复前置或定义身份无效";
                return false;
            }

            var candidate = new SingleCityDefenseCampaignModel(
                requestedCoreX,
                requestedCoreZ,
                definition);
            candidate.SetWarningMultiplier(warningMultiplier);
            if (!candidate.TryPrepareRestore(
                    state,
                    out SingleCityDefenseCampaignRestorePlan plan,
                    out error) ||
                !candidate.TryCommitRestore(plan, out error))
            {
                return false;
            }

            activePressureCampaign = candidate;
            ResetPressureTechnologyStates();
            activePressureEncounterId = definition.Id;
            candidate.TerminalCommitted += HandlePressureTerminal;
            candidate.EnemyDefeated += HandlePressureEnemyDefeated;
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            error = string.Empty;
            return true;
        }

        public bool ClearActivePressure()
        {
            if (activePressureCampaign == null) return false;
            activePressureCampaign.TerminalCommitted -=
                HandlePressureTerminal;
            activePressureCampaign.EnemyDefeated -=
                HandlePressureEnemyDefeated;
            activePressureCampaign = null;
            activePressureEncounterId = null;
            ResetPressureTechnologyStates();
            campaignSnapshotDirty = true;
            snapshotDirty = true;
            return true;
        }

        private void HandlePressureTerminal(
            SingleCityDefenseCampaignResult result)
        {
            string encounterId = activePressureEncounterId;
            PressureCampaignTerminalCommitted?.Invoke(encounterId, result);
            if (activePressureCampaign != null)
            {
                activePressureCampaign.TerminalCommitted -=
                    HandlePressureTerminal;
                activePressureCampaign.EnemyDefeated -=
                    HandlePressureEnemyDefeated;
            }
            activePressureCampaign = null;
            activePressureEncounterId = null;
            activeBroodmotherEncounter = null;
            ResetPressureTechnologyStates();
            campaignSnapshotDirty = true;
            snapshotDirty = true;
        }

        public void ConfigureFormalCampaign(
            SingleCityDefenseCampaignModel campaign,
            GrayboxBuildingHealthRuntime3D buildingHealth,
            GrayboxCombatDestructionCoordinator3D destructionCoordinator)
        {
            if (this.campaign != null)
                this.campaign.EnemyDefeated -= HandleCampaignEnemyDefeated;
            this.campaign = campaign ??
                throw new ArgumentNullException(nameof(campaign));
            this.campaign.SetWarningMultiplier(warningMultiplier);
            this.campaign.EnemyDefeated += HandleCampaignEnemyDefeated;
            campaignBuildingHealth = buildingHealth ??
                throw new ArgumentNullException(nameof(buildingHealth));
            campaignBuildingHealth.ConfigureTechnologySupport(
                technologyUnlocks.WallPhysicalDamageMultiplier,
                technologyUnlocks.AutomatedRepair,
                technologyUnlocks.MindShield);
            campaignDestructionCoordinator = destructionCoordinator ??
                throw new ArgumentNullException(nameof(destructionCoordinator));
            campaignTowerById.Clear();
            campaignTowers.Clear();
            campaignRetainedIds.Clear();
            campaignRunnableIds.Clear();
            campaignStatusById.Clear();
            settledAttackEvents.Clear();
            campaignBuildingTargets =
                Array.Empty<DefenseBuildingCombatTarget>();
            campaignBuildingTargetFingerprint = 0ul;
            hasCampaignBuildingTargetFingerprint = false;
            campaignTriggered = campaign.Snapshot.CurrentWaveNumber > 0;
            LastDestructionResult = null;
            destructionResultsByStableId.Clear();
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

        public bool TryGetDestructionResult(
            string stableInstanceId,
            out GrayboxCombatDestructionResult3D result)
        {
            result = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                destructionResultsByStableId.TryGetValue(
                    stableInstanceId,
                    out result);
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
                    buildingHealthMultiplier,
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
                    buildingHealthMultiplier,
                    out error))
            {
                return false;
            }

            campaignTowerById.Clear();
            campaignTowers.Clear();
            campaignRetainedIds.Clear();
            campaignStatusById.Clear();
            settledAttackEvents.Clear();
            LastDestructionResult = null;
            destructionResultsByStableId.Clear();
            pendingPresentationRebuilds.Clear();
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
            settledAttackEvents.Clear();
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
            if (ActiveCampaign == null)
                tutorial.SetCorePosition(x, z);
            else
                ActiveCampaign.SetCorePosition(x, z);
            campaignSnapshotDirty = ActiveCampaign != null;
            snapshotDirty = true;
            persistenceGeneration++;
        }

        public void ApplyElixirCoreHealth(
            int healing,
            int backlashDamage)
        {
            SingleCityDefenseCampaignModel owner = ActiveCampaign;
            if (owner == null) return;
            owner.ApplyElixirCoreHealth(healing, backlashDamage);
            campaignSnapshotDirty = true;
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
                allowCampaignStart: true,
                swordRidingCompleted: false,
                alloyArmorCompleted: false,
                automatedDefenseCompleted: false,
                swordArrayCompleted: false,
                precognitiveSenseCompleted: false);
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            bool allowCampaignStart = true,
            bool swordRidingCompleted = false,
            bool alloyArmorCompleted = false,
            bool automatedDefenseCompleted = false,
            bool swordArrayCompleted = false,
            bool precognitiveSenseCompleted = false)
        {
            var completed = new List<string>(5);
            if (swordRidingCompleted)
                completed.Add("core.research.sword-riding");
            if (alloyArmorCompleted)
                completed.Add("core.research.alloy-armor");
            if (automatedDefenseCompleted)
                completed.Add(ResearchCatalog.AutomatedDefenseId);
            if (swordArrayCompleted)
                completed.Add("core.research.sword-array");
            if (precognitiveSenseCompleted)
                completed.Add("core.research.precognitive-sense");
            Synchronize(
                instances,
                cityMode,
                cityX,
                cityY,
                groundRadius,
                allowCampaignStart,
                ResearchEffectResolver.Resolve(completed));
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            bool allowCampaignStart,
            ResearchEffectSnapshot researchEffects,
            Func<string, bool> isPlayerPaused = null)
        {
            if (campaign != null)
            {
                SynchronizeCampaign(
                    instances,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius,
                    allowCampaignStart,
                    researchEffects,
                    isPlayerPaused);
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
            BeginSettlementBatch();
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
            bool allowCampaignStart,
            ResearchEffectSnapshot researchEffects,
            Func<string, bool> isPlayerPaused)
        {
            researchEffects = researchEffects ??
                ResearchEffectResolver.Resolve(Array.Empty<string>());
            persistenceGeneration++;
            warningMultiplier = researchEffects.WarningDurationMultiplier;
            buildingHealthMultiplier =
                researchEffects.BuildingHealthMultiplier;
            campaign.SetWarningMultiplier(warningMultiplier);
            activePressureCampaign?.SetWarningMultiplier(warningMultiplier);
            bool snapshotChanged = false;
            orderedInstances.Clear();
            campaignTowers.Clear();
            campaignRetainedIds.Clear();
            campaignRunnableIds.Clear();

            if (instances != null)
            {
                campaignBuildingHealth.Synchronize(
                    instances,
                    buildingHealthMultiplier);
                campaignBuildingHealth.SynchronizeTechnologyOperationalState(
                    instances,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius,
                    stableInstanceId =>
                        isPlayerPaused?.Invoke(stableInstanceId) == true ||
                        campaignTowerById.TryGetValue(
                            stableInstanceId,
                            out SingleCityDefenseTowerCombatModel tower) &&
                        tower.IsPlayerPaused);
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

                string currentBuildingId =
                    instance.Placement.Definition.Id.Value;
                float rangeMultiplier = researchEffects
                    .ResolveTowerRangeMultiplier(currentBuildingId);
                float damageMultiplier = researchEffects
                    .ResolveTowerDamageMultiplier(currentBuildingId);
                if (!campaignTowerById.TryGetValue(
                        instance.StableInstanceId,
                        out SingleCityDefenseTowerCombatModel tower))
                {
                    tower = new SingleCityDefenseTowerCombatModel(
                        instance.StableInstanceId,
                        currentBuildingId,
                        instance.Placement.X,
                        instance.Placement.Y);
                    tower.SetRangeMultiplier(rangeMultiplier);
                    tower.SetDamageMultiplier(damageMultiplier);
                    campaignTowerById.Add(instance.StableInstanceId, tower);
                    campaignStatusById[instance.StableInstanceId] =
                        GrayboxDefenseTowerStatus3D.NoTarget;
                    snapshotChanged = true;
                }
                else if (!string.Equals(
                             tower.BuildingId,
                             currentBuildingId,
                             StringComparison.Ordinal))
                {
                    tower = tower.RebuildForBuilding(
                        currentBuildingId,
                        rangeMultiplier);
                    tower.SetDamageMultiplier(damageMultiplier);
                    campaignTowerById[instance.StableInstanceId] = tower;
                    snapshotChanged = true;
                }
                else
                {
                    tower.SetRangeMultiplier(rangeMultiplier);
                    tower.SetDamageMultiplier(damageMultiplier);
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
                SingleCityDefenseCampaignModel owner = ActiveCampaign;
                SingleCityDefenseTechnologyRuntime technology =
                    TechnologyFor(owner);
                owner.Advance(
                    StepSecondsFloat,
                    requestedSpeed: 1,
                    campaignBuildingTargetProvider,
                    campaignBuildingDamageApplier);
                technology.SynchronizeEnemies(owner.Snapshot.Enemies);
                ApplyTechnologyDamageEvents(
                    owner,
                    technology.Advance(
                        StepSecondsFloat,
                        paused: false));
                for (var index = 0; index < campaignTowers.Count; index++)
                    TickCampaignTower(
                        campaignTowers[index], cityStorage, owner, technology);
                int supportChange =
                    campaignBuildingHealth.AdvanceTechnologySupport(
                        StepSecondsFloat,
                        paused: false,
                        requestedCoreX,
                        requestedCoreZ,
                        out int coreShieldGrant);
                if (coreShieldGrant > 0)
                    owner.GrantCoreShield(coreShieldGrant);
                if (supportChange > 0 || coreShieldGrant > 0)
                    campaignSnapshotDirty = true;
                ObserveCrystalBroodmotherAuthority(owner);
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
            CityResourceStorageModel cityStorage,
            SingleCityDefenseCampaignModel owner,
            SingleCityDefenseTechnologyRuntime technology)
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
            string predictedTargetId = owner.PeekTowerTarget(
                tower.TargetStableEnemyId,
                tower.X,
                tower.Z,
                tower.Range);
            tower.SetTechnologyMultipliers(
                technology.ResolveTowerFireRateMultiplier(
                    tower.StableInstanceId),
                technology.ResolveTowerDamageMultiplier(
                    tower.StableInstanceId,
                    tower.BuildingId,
                    predictedTargetId));
            int damage = tower.Tick(
                StepSecondsFloat,
                owner,
                globallyPaused: false);
            if (damage > 0)
            {
                SingleCityDefenseTechnologyHitResult technologyHit =
                    technology.ApplyTowerHit(
                        tower.StableInstanceId,
                        tower.BuildingId,
                        predictedTargetId,
                        damage,
                        StepSecondsFloat,
                        settledAttackEventSequence + 1ul);
                ApplyTechnologyHitResult(
                    owner,
                    technology,
                    tower.BuildingId,
                    predictedTargetId,
                    technologyHit);
                AppendSettledAttack(
                    tower.StableInstanceId,
                    tower.TargetStableEnemyId,
                    damage);
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

        private void ObserveCrystalBroodmotherAuthority(
            SingleCityDefenseCampaignModel owner)
        {
            if (!ReferenceEquals(owner, activePressureCampaign)) return;
            SingleCityDefenseCampaignSnapshot snapshot = owner.Snapshot;
            SingleCityDefenseEnemySnapshot boss = null;
            for (var index = 0; index < snapshot.Enemies.Count; index++)
            {
                if (string.Equals(
                        snapshot.Enemies[index].EnemyDefinitionId,
                        CrystalBroodmotherCatalog.StableArchetypeId,
                        StringComparison.Ordinal))
                {
                    boss = snapshot.Enemies[index];
                    break;
                }
            }
            if (boss == null) return;
            if (activeBroodmotherEncounter == null)
                activeBroodmotherEncounter =
                    new CrystalBroodmotherEncounter(boss.StableId);
            ApplyBroodmotherCommands(owner,
                activeBroodmotherEncounter.ObserveAuthorityHealth(
                    boss.StableId,
                    boss.CurrentHealth,
                    CrystalBroodmotherCatalog.MaximumHealth));
        }

        private void HandlePressureEnemyDefeated(
            string stableEnemyId,
            string enemyDefinitionId)
        {
            EnemyDefeatedForRewards?.Invoke(
                stableEnemyId,
                enemyDefinitionId);
            if (activePressureCampaign == null ||
                activeBroodmotherEncounter == null ||
                !string.Equals(enemyDefinitionId,
                    CrystalBroodmotherCatalog.StableArchetypeId,
                    StringComparison.Ordinal)) return;
            IReadOnlyList<CrystalBroodmotherCommand> commands =
                activeBroodmotherEncounter.ObserveAuthorityHealth(
                    stableEnemyId, 0,
                    CrystalBroodmotherCatalog.MaximumHealth);
            ApplyBroodmotherCommands(activePressureCampaign, commands);
            for (var index = 0; index < commands.Count; index++)
                if (commands[index].Kind == CrystalBroodmotherCommandKind.Defeated)
                    CrystalBroodmotherDefeated?.Invoke(stableEnemyId);
        }

        private void HandleCampaignEnemyDefeated(
            string stableEnemyId,
            string enemyDefinitionId)
        {
            EnemyDefeatedForRewards?.Invoke(
                stableEnemyId,
                enemyDefinitionId);
        }

        private static void ApplyBroodmotherCommands(
            SingleCityDefenseCampaignModel owner,
            IReadOnlyList<CrystalBroodmotherCommand> commands)
        {
            for (var commandIndex = 0; commandIndex < commands.Count;
                 commandIndex++)
            {
                CrystalBroodmotherCommand command = commands[commandIndex];
                if (command.Kind !=
                    CrystalBroodmotherCommandKind.SpawnReinforcements) continue;
                var entries = new WaveEntry[command.Reinforcements.Count];
                for (var index = 0; index < entries.Length; index++)
                    entries[index] = new WaveEntry(
                        command.Reinforcements[index].Archetype,
                        command.Reinforcements[index].Count);
                owner.TryInjectReinforcements(command.StableCommandId, entries);
            }
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
                    DamageType.Physical,
                    out int appliedDamage,
                    out bool destroyedNow))
            {
                return 0;
            }
            if (destroyedNow)
            {
                LastDestructionResult =
                    campaignDestructionCoordinator.Commit(stableBuildingId);
                if (LastDestructionResult.IsCommitted)
                {
                    destructionResultsByStableId[stableBuildingId] =
                        LastDestructionResult;
                }
                if (LastDestructionResult.RequiresPresentationRebuild)
                    pendingPresentationRebuilds.Enqueue(
                        LastDestructionResult);
                RefreshCampaignBuildingTargets();
            }
            return appliedDamage;
        }

        private static void ApplyTechnologyDamageEvents(
            SingleCityDefenseCampaignModel owner,
            IReadOnlyList<SingleCityDefenseTechnologyDamageEvent> events)
        {
            if (owner == null || events == null) return;
            for (var index = 0; index < events.Count; index++)
            {
                SingleCityDefenseTechnologyDamageEvent item = events[index];
                owner.ApplyTechnologyDamage(
                    item.TargetStableEnemyId,
                    BuildingCatalog.SporeTower.Id.Value,
                    item.Damage);
            }
        }

        private static void ApplyTechnologyHitResult(
            SingleCityDefenseCampaignModel owner,
            SingleCityDefenseTechnologyRuntime technology,
            string sourceBuildingId,
            string primaryTargetId,
            SingleCityDefenseTechnologyHitResult result)
        {
            if (owner == null || result == null) return;
            if (result.TrueDamage > 0)
            {
                owner.ApplyTechnologyDamage(
                    primaryTargetId,
                    sourceBuildingId,
                    result.TrueDamage);
            }
            for (var index = 0;
                 index < result.SynchronizedDamageEvents.Count;
                 index++)
            {
                SingleCityDefenseTechnologyDamageEvent item =
                    result.SynchronizedDamageEvents[index];
                owner.ApplyTechnologyDamage(
                    item.TargetStableEnemyId,
                    sourceBuildingId,
                    item.Damage);
            }
            if (result.Controlled)
            {
                if (owner.TryControlEnemy(
                        primaryTargetId,
                        sourceBuildingId))
                {
                    technology?.TryCommitMindControl(primaryTargetId);
                }
            }
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
                AppendSettledAttack(
                    state.StableId,
                    target.StableId,
                    damage);
                state.Status = GrayboxDefenseTowerStatus3D.Firing;
                return;
            }

            state.Status = !state.Combat.IsLogisticsConnected &&
                cityStorage.GetNetworkAmount(ResourceIds.Ammunition) > 0
                    ? GrayboxDefenseTowerStatus3D.OutOfLogistics
                    : GrayboxDefenseTowerStatus3D.MissingAmmunition;
        }

        private void BeginSettlementBatch()
        {
            settlementSequence++;
            if (settledAttackEvents.Count == 0)
                return;
            settledAttackEvents.Clear();
            snapshotDirty = true;
        }

        private void AppendSettledAttack(
            string towerStableId,
            string targetStableId,
            int appliedDamage)
        {
            if (appliedDamage <= 0 ||
                string.IsNullOrWhiteSpace(towerStableId) ||
                string.IsNullOrWhiteSpace(targetStableId))
            {
                return;
            }
            settledAttackEventSequence++;
            settledAttackEvents.Add(
                new GrayboxDefenseSettledAttackEvent3D(
                    settledAttackEventSequence,
                    settlementSequence,
                    towerStableId,
                    targetStableId,
                    appliedDamage));
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
                    visibleStatus,
                    state.Combat.ActiveAmmunitionSeconds);
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
                    EnemyCatalog.Gnawer.Id.Value,
                    enemy.SpawnOrder,
                    enemy.X,
                    enemy.Z,
                    enemy.CurrentHealth,
                    EnemyCatalog.Gnawer.MaximumHealth,
                    DefenseEnemyRuntimeSnapshot.CityCoreTargetName,
                    tutorial.DistanceToCore(enemy),
                    tutorial.IsWithinAttackRange(enemy),
                    SingleCityDefenseCampaignModel.CityCoreTargetId,
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
                Array.AsReadOnly(enemySnapshots),
                settledAttackEvents,
                Array.Empty<CampaignSpawnDirection>(),
                tutorialWaveTriggerCount > 0
                    ? WaveCatalog.Tutorial.Entries
                    : Array.Empty<WaveEntry>());
        }

        private GrayboxDefenseRuntimeSnapshot3D CaptureCampaignSnapshot()
        {
            SingleCityDefenseCampaignSnapshot source = ActiveCampaignSnapshot;
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
                    status,
                    tower.ActiveConsumableSeconds);
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
                ResolveCampaignEnemyTarget(
                    enemy,
                    targetsCore,
                    out string targetStableId,
                    out string targetDisplayName,
                    out float distanceToTarget);
                bool isAttackingTarget = definition != null &&
                    distanceToTarget <= definition.AttackRange;
                enemySnapshots[index] = new GrayboxDefenseEnemySnapshot3D(
                    enemy.StableId,
                    enemy.EnemyDefinitionId,
                    enemy.SpawnOrder,
                    enemy.X,
                    enemy.Z,
                    enemy.CurrentHealth,
                    definition?.MaximumHealth ??
                        Math.Max(1, enemy.CurrentHealth),
                    targetsCore
                        ? DefenseEnemyRuntimeSnapshot.CityCoreTargetName
                        : enemy.TargetStableId,
                    distanceToCore,
                    targetsCore && definition != null &&
                    distanceToCore <= definition.AttackRange,
                    targetStableId,
                    targetDisplayName,
                    distanceToTarget,
                    isAttackingTarget);
            }

            IReadOnlyList<CampaignSpawnDirection> spawnDirections =
                ResolveCampaignSpawnDirections(source.CurrentWaveNumber);

            return new GrayboxDefenseRuntimeSnapshot3D(
                campaignTriggered ? 1 : 0,
                source.CurrentWaveNumber,
                CampaignWaveCatalog.All.Count,
                source.Phase,
                ToLegacyWavePhase(source.Phase),
                source.WarningRemainingSeconds,
                source.PlannedEnemyCount,
                source.SpawnedEnemyCount,
                source.AliveEnemyCount,
                source.Statistics.TotalKillCount,
                source.CoreMaximumHealth,
                source.CoreCurrentHealth,
                Array.AsReadOnly(towerSnapshots),
                Array.AsReadOnly(enemySnapshots),
                settledAttackEvents,
                spawnDirections,
                ResolveCampaignWaveComposition(source.CurrentWaveNumber));
        }

        private void ResolveCampaignEnemyTarget(
            SingleCityDefenseEnemySnapshot enemy,
            bool targetsCore,
            out string targetStableId,
            out string targetDisplayName,
            out float distanceToTarget)
        {
            if (targetsCore)
            {
                targetStableId =
                    SingleCityDefenseCampaignModel.CityCoreTargetId;
                targetDisplayName =
                    DefenseEnemyRuntimeSnapshot.CityCoreTargetName;
                distanceToTarget = Distance(
                    enemy.X,
                    enemy.Z,
                    requestedCoreX,
                    requestedCoreZ);
                return;
            }

            targetStableId = enemy.TargetStableId;
            targetDisplayName = enemy.TargetStableId;
            distanceToTarget = Distance(
                enemy.X,
                enemy.Z,
                requestedCoreX,
                requestedCoreZ);
            for (var index = 0;
                 index < campaignBuildingTargets.Length;
                 index++)
            {
                DefenseBuildingCombatTarget target =
                    campaignBuildingTargets[index];
                if (!string.Equals(
                        target.StableId,
                        targetStableId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                distanceToTarget = Distance(
                    enemy.X,
                    enemy.Z,
                    target.X,
                    target.Z);
                targetDisplayName = BuildingDisplayName(
                    target.BuildingId,
                    target.StableId);
                return;
            }
        }

        private static string BuildingDisplayName(
            string buildingId,
            string fallback)
        {
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition definition = BuildingCatalog.All[index];
                if (string.Equals(
                        definition.Id.Value,
                        buildingId,
                        StringComparison.Ordinal))
                {
                    return definition.Name;
                }
            }
            return fallback ?? string.Empty;
        }

        private static IReadOnlyList<CampaignSpawnDirection>
            ResolveCampaignSpawnDirections(int currentWaveNumber)
        {
            if (currentWaveNumber <= 0 ||
                currentWaveNumber > CampaignWaveCatalog.All.Count)
            {
                return Array.Empty<CampaignSpawnDirection>();
            }
            return CampaignWaveCatalog.All[currentWaveNumber - 1].Directions;
        }

        private static IReadOnlyList<WaveEntry>
            ResolveCampaignWaveComposition(int currentWaveNumber)
        {
            if (currentWaveNumber <= 0 ||
                currentWaveNumber > CampaignWaveCatalog.All.Count)
            {
                return Array.Empty<WaveEntry>();
            }
            return CampaignWaveCatalog.All[currentWaveNumber - 1].Entries;
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
                       StringComparison.Ordinal) ||
                   string.Equals(buildingId,
                       BuildingCatalog.HeavyMachineGunTurret.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(buildingId,
                       BuildingCatalog.SwordArrayTower.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(buildingId,
                       BuildingCatalog.SwordRidingPlatform.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(buildingId,
                       BuildingCatalog.EmpTower.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(buildingId,
                       BuildingCatalog.MindSpire.Id.Value,
                       StringComparison.Ordinal) ||
                   string.Equals(buildingId,
                       BuildingCatalog.AcidTower.Id.Value,
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
