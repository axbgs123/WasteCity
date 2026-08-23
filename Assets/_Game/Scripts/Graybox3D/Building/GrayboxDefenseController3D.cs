using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using WasteCity.Defense;

namespace WasteCity.Graybox3D.Building
{
    [DefaultExecutionOrder(100)]
    public sealed class GrayboxDefenseController3D : MonoBehaviour
    {
        private const float TutorialSpawnDistanceCells = 9f;
        private static readonly ProfilerMarker TickMarker =
            new ProfilerMarker("WasteCity.Formal.Defense.Tick");

        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxWorldView3D world;
        [SerializeField]
        private GrayboxBuildingWorldView3D buildingPresentation;
        [SerializeField] private GrayboxDefenseWorldView3D worldView;
        [SerializeField] private GrayboxDefenseHudView3D hud;
        [SerializeField] private GrayboxProductionController3D production;

        private GrayboxDefenseRuntime3D runtime;
        private GrayboxBuildingHealthRuntime3D buildingHealth =
            new GrayboxBuildingHealthRuntime3D();
        private SingleCityDefenseCampaignModel campaign;
        private GrayboxCombatDestructionCoordinator3D destructionCoordinator;
        private GrayboxDefenseRuntimeSnapshot3D snapshot;
        private GrayboxDefenseRuntimeSnapshot3D presentedSnapshot;
        private PlanarCoordinateMapper3D boundCoordinates;
        private GrayboxDefenseSelectionKind3D selectedKind;
        private GrayboxDefenseSelectionKind3D presentedKind;
        private string selectedStableId;
        private string presentedStableId;
        private Func<bool> persistencePauseSource;
        private bool hudBound;
        private bool firstMachineGunCheckpointPublished;
        private bool tutorialCombatCheckpointPublished;

        public GrayboxDefenseRuntime3D Runtime => runtime;
        public GrayboxDefenseRuntimeSnapshot3D Snapshot => snapshot;
        public SingleCityDefenseCampaignSnapshot CampaignSnapshot =>
            runtime?.CampaignSnapshot;
        public GrayboxBuildingHealthRuntime3D BuildingHealth => buildingHealth;
        public GrayboxCombatDestructionResult3D LastDestructionResult =>
            runtime?.LastDestructionResult;
        public GrayboxDefenseWorldView3D WorldView => worldView;
        public GrayboxDefenseHudView3D Hud => hud;
        public bool HasSelection =>
            selectedKind != GrayboxDefenseSelectionKind3D.None &&
            !string.IsNullOrWhiteSpace(selectedStableId);
        public GrayboxDefenseSelectionKind3D SelectedKind => selectedKind;
        public string SelectedStableId => selectedStableId;
        public bool IsPersistencePaused =>
            persistencePauseSource != null && persistencePauseSource();
        public bool IsConfigured =>
            session != null &&
            city != null &&
            world != null &&
            buildingPresentation != null &&
            worldView != null &&
            hud != null;

        public event Action<string> FirstMachineGunCompleted;
        public event Action<string> TutorialCombatStarted;
        public event Action<int> CampaignWaveWarningStarted;

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingWorldView3D buildingPresentation,
            GrayboxDefenseWorldView3D worldView,
            GrayboxDefenseHudView3D hud)
        {
            ConfigureDependencies(
                session,
                city,
                world,
                buildingPresentation,
                worldView,
                hud,
                newProduction: null);
        }

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingWorldView3D buildingPresentation,
            GrayboxDefenseWorldView3D worldView,
            GrayboxDefenseHudView3D hud,
            GrayboxProductionController3D production)
        {
            ConfigureDependencies(
                session,
                city,
                world,
                buildingPresentation,
                worldView,
                hud,
                production ?? throw new ArgumentNullException(
                    nameof(production)));
        }

        private void ConfigureDependencies(
            GrayboxBuildingSession3D newSession,
            GrayboxMobileCityController3D newCity,
            GrayboxWorldView3D newWorld,
            GrayboxBuildingWorldView3D newBuildingPresentation,
            GrayboxDefenseWorldView3D newWorldView,
            GrayboxDefenseHudView3D newHud,
            GrayboxProductionController3D newProduction)
        {
            newSession = newSession ??
                throw new ArgumentNullException(nameof(newSession));
            newCity = newCity ??
                throw new ArgumentNullException(nameof(newCity));
            newWorld = newWorld ??
                throw new ArgumentNullException(nameof(newWorld));
            newBuildingPresentation = newBuildingPresentation ??
                throw new ArgumentNullException(
                    nameof(newBuildingPresentation));
            newWorldView = newWorldView ??
                throw new ArgumentNullException(nameof(newWorldView));
            newHud = newHud ?? throw new ArgumentNullException(nameof(newHud));

            bool ownershipChanged =
                !ReferenceEquals(session, newSession) ||
                !ReferenceEquals(city, newCity) ||
                !ReferenceEquals(world, newWorld) ||
                !ReferenceEquals(
                    buildingPresentation,
                    newBuildingPresentation) ||
                !ReferenceEquals(worldView, newWorldView) ||
                !ReferenceEquals(hud, newHud) ||
                !ReferenceEquals(production, newProduction);

            UnbindHud();
            session = newSession;
            city = newCity;
            world = newWorld;
            buildingPresentation = newBuildingPresentation;
            worldView = newWorldView;
            hud = newHud;
            production = newProduction;
            if (ownershipChanged)
                ResetRuntimeOwnership();
            InvalidatePresentation();
            BindHud();
        }

        private void ResetRuntimeOwnership()
        {
            if (campaign != null)
            {
                campaign.WaveWarningStarted -=
                    HandleCampaignWaveWarningStarted;
            }
            runtime?.DetachPresentationRecovery();
            runtime = null;
            campaign = null;
            destructionCoordinator = null;
            buildingHealth = new GrayboxBuildingHealthRuntime3D();
            snapshot = null;
            boundCoordinates = null;
            firstMachineGunCheckpointPublished = false;
            tutorialCombatCheckpointPublished = false;
            selectedKind = GrayboxDefenseSelectionKind3D.None;
            selectedStableId = null;
        }

        public void ConfigurePersistencePauseSource(Func<bool> pauseSource)
        {
            persistencePauseSource = pauseSource;
        }

        public bool Tick(float deltaSeconds, bool paused)
        {
            using (TickMarker.Auto())
            {
                bool effectivePaused = paused || IsPersistencePaused;
                worldView?.SetSimulationPaused(effectivePaused);
                if (effectivePaused) return true;
                int previousTutorialWaveTriggerCount =
                    snapshot?.TutorialWaveTriggerCount ?? 0;
                int previousSpawnedEnemyCount =
                    snapshot?.SpawnedEnemyCount ?? 0;
                if (session?.CityStorage == null ||
                    !TrySynchronizeRuntime(out _))
                    return false;
                runtime.Tick(
                    deltaSeconds,
                    effectivePaused,
                    session.CityStorage);
                snapshot = runtime.Snapshot;
                if (!firstMachineGunCheckpointPublished &&
                    previousTutorialWaveTriggerCount == 0 &&
                    snapshot.TutorialWaveTriggerCount > 0 &&
                    snapshot.Towers.Count > 0)
                {
                    firstMachineGunCheckpointPublished = true;
                    FirstMachineGunCompleted?.Invoke(
                        snapshot.Towers[0].StableId);
                }
                if (!tutorialCombatCheckpointPublished &&
                    previousSpawnedEnemyCount == 0 &&
                    snapshot.SpawnedEnemyCount > 0 &&
                    snapshot.Enemies.Count > 0)
                {
                    tutorialCombatCheckpointPublished = true;
                    TutorialCombatStarted?.Invoke(
                        snapshot.Enemies[0].StableId);
                }
                ValidateSelection();
                ApplyPresentation();
                return true;
            }
        }

        public bool TryRebuildAfterPersistenceRestore(out string error)
        {
            try
            {
                if (!TrySynchronizeRuntime(out error))
                    return false;
                snapshot = runtime.Snapshot;
                firstMachineGunCheckpointPublished =
                    snapshot.TutorialWaveTriggerCount > 0;
                tutorialCombatCheckpointPublished =
                    snapshot.SpawnedEnemyCount > 0;
                ValidateSelection();
                ApplyPresentation(force: true);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TrySelect(Ray ray)
        {
            if (!IsConfigured || snapshot == null)
                return false;

            Physics.SyncTransforms();
            if (
                !worldView.TryPick(
                    ray,
                    out GrayboxDefenseSelectionKind3D kind,
                    out string stableId) ||
                !SnapshotContains(kind, stableId))
            {
                return false;
            }

            selectedKind = kind;
            selectedStableId = stableId;
            ApplyPresentation(force: true);
            return true;
        }

        public void CloseSelection()
        {
            selectedKind = GrayboxDefenseSelectionKind3D.None;
            selectedStableId = null;
            ApplyPresentation(force: true);
        }

        public bool TryToggleSelectedTowerPause()
        {
            if (runtime == null || snapshot == null ||
                selectedKind != GrayboxDefenseSelectionKind3D.Tower ||
                string.IsNullOrWhiteSpace(selectedStableId) ||
                !TryFindTower(
                    selectedStableId,
                    out GrayboxDefenseTowerSnapshot3D tower) ||
                !runtime.TrySetPlayerPaused(
                    selectedStableId,
                    !tower.PlayerPaused))
            {
                return false;
            }

            snapshot = runtime.Snapshot;
            ApplyPresentation();
            return true;
        }

        private void Update()
        {
            Tick(Time.deltaTime, Time.timeScale <= 0f);
        }

        private void OnEnable()
        {
            BindHud();
        }

        private void OnDisable()
        {
            UnbindHud();
        }

        private void OnDestroy()
        {
            UnbindHud();
            if (campaign != null)
            {
                campaign.WaveWarningStarted -=
                    HandleCampaignWaveWarningStarted;
            }
            runtime?.DetachPresentationRecovery();
            session = null;
            city = null;
            world = null;
            buildingPresentation = null;
            worldView = null;
            hud = null;
            production = null;
            runtime = null;
            campaign = null;
            destructionCoordinator = null;
            snapshot = null;
            presentedSnapshot = null;
            boundCoordinates = null;
            persistencePauseSource = null;
            firstMachineGunCheckpointPublished = false;
            tutorialCombatCheckpointPublished = false;
            selectedStableId = null;
            presentedStableId = null;
            selectedKind = GrayboxDefenseSelectionKind3D.None;
            presentedKind = GrayboxDefenseSelectionKind3D.None;
        }

        private void EnsureRuntime(float coreX, float coreZ)
        {
            if (runtime != null) return;

            float spawnX = coreX + TutorialSpawnDistanceCells;
            if (spawnX >= world.Coordinates.Width)
                spawnX = coreX - TutorialSpawnDistanceCells;
            spawnX = Mathf.Clamp(
                spawnX,
                0f,
                world.Coordinates.Width - 1f);
            runtime = new GrayboxDefenseRuntime3D(
                coreX,
                coreZ,
                spawnX,
                coreZ);
            if (production == null) return;

            campaign = new SingleCityDefenseCampaignModel(coreX, coreZ);
            campaign.WaveWarningStarted +=
                HandleCampaignWaveWarningStarted;
            destructionCoordinator =
                new GrayboxCombatDestructionCoordinator3D(
                    session,
                    buildingHealth,
                    production.Clock.Runtime,
                    runtime,
                    campaign,
                    buildingPresentation);
            runtime.ConfigureFormalCampaign(
                campaign,
                buildingHealth,
                destructionCoordinator);
            runtime.ConfigurePresentationRecovery(
                TryRecoverDestroyedBuildingPresentation);
        }

        private void HandleCampaignWaveWarningStarted(int waveNumber)
        {
            CampaignWaveWarningStarted?.Invoke(waveNumber);
        }

        private bool TryRecoverDestroyedBuildingPresentation(
            string stableInstanceId)
        {
            if (session?.Instances == null || buildingPresentation == null ||
                string.IsNullOrWhiteSpace(stableInstanceId))
            {
                return false;
            }
            for (var index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance =
                    session.Instances[index];
                if (instance == null || !string.Equals(
                        instance.StableInstanceId,
                        stableInstanceId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                try
                {
                    buildingPresentation.UpdateInstance(instance);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }

        private bool TrySynchronizeRuntime(out string error)
        {
            if (!IsConfigured || session.Instances == null ||
                world.Coordinates == null)
            {
                error = "防御运行时依赖未就绪";
                return false;
            }
            if (!TryResolveCityPosition(
                    out int cityX,
                    out int cityY,
                    out float logicalCoreX,
                    out float logicalCoreZ))
            {
                error = "无法解析城市坐标";
                return false;
            }

            if (!ReferenceEquals(boundCoordinates, world.Coordinates))
            {
                worldView.BindCoordinates(world.Coordinates);
                boundCoordinates = world.Coordinates;
                InvalidatePresentation();
            }
            EnsureRuntime(logicalCoreX, logicalCoreZ);
            runtime.SetCorePosition(logicalCoreX, logicalCoreZ);
            runtime.Synchronize(
                session.Instances,
                city.Mode,
                cityX,
                cityY,
                session.GroundBuildRadius,
                allowCampaignStart: !IsPersistencePaused);
            error = string.Empty;
            return true;
        }

        private bool TryResolveCityPosition(
            out int cityX,
            out int cityY,
            out float logicalX,
            out float logicalZ)
        {
            cityX = 0;
            cityY = 0;
            logicalX = 0f;
            logicalZ = 0f;
            if (world?.Coordinates == null || city == null ||
                !world.Coordinates.TryWorldToCell(
                    city.transform.position,
                    out cityX,
                    out cityY) ||
                !world.Coordinates.TryCellToWorld(
                    cityX,
                    cityY,
                    city.transform.position.y,
                    out Vector3 cellWorld))
            {
                return false;
            }

            Vector3 cityOffset = city.transform.position - cellWorld;
            logicalX = cityX + cityOffset.x;
            logicalZ = cityY + cityOffset.z;
            return true;
        }

        private void ApplyPresentation(bool force = false)
        {
            if (snapshot == null) return;
            if (!force && ReferenceEquals(snapshot, presentedSnapshot) &&
                selectedKind == presentedKind &&
                string.Equals(
                    selectedStableId,
                    presentedStableId,
                    StringComparison.Ordinal))
            {
                return;
            }
            worldView?.Apply(snapshot, session.Instances);
            hud?.Apply(snapshot, selectedKind, selectedStableId);
            presentedSnapshot = snapshot;
            presentedKind = selectedKind;
            presentedStableId = selectedStableId;
        }

        private void InvalidatePresentation()
        {
            presentedSnapshot = null;
            presentedKind = GrayboxDefenseSelectionKind3D.None;
            presentedStableId = null;
        }

        private void ValidateSelection()
        {
            if (!HasSelection || SnapshotContains(
                    selectedKind,
                    selectedStableId))
            {
                return;
            }
            selectedKind = GrayboxDefenseSelectionKind3D.None;
            selectedStableId = null;
        }

        private bool SnapshotContains(
            GrayboxDefenseSelectionKind3D kind,
            string stableId)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(stableId))
                return false;
            switch (kind)
            {
                case GrayboxDefenseSelectionKind3D.Tower:
                    return TryFindTower(stableId, out _);
                case GrayboxDefenseSelectionKind3D.Enemy:
                    IReadOnlyList<GrayboxDefenseEnemySnapshot3D> enemies =
                        snapshot.Enemies;
                    for (int index = 0; index < enemies.Count; index++)
                    {
                        if (string.Equals(
                                enemies[index].StableId,
                                stableId,
                                StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return false;
            }
        }

        private bool TryFindTower(
            string stableId,
            out GrayboxDefenseTowerSnapshot3D tower)
        {
            tower = null;
            if (snapshot == null || string.IsNullOrWhiteSpace(stableId))
                return false;
            IReadOnlyList<GrayboxDefenseTowerSnapshot3D> towers =
                snapshot.Towers;
            for (int index = 0; index < towers.Count; index++)
            {
                if (!string.Equals(
                        towers[index].StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                tower = towers[index];
                return true;
            }
            return false;
        }

        private void BindHud()
        {
            if (hud == null || hudBound || !isActiveAndEnabled)
                return;
            hud.TowerPauseRequested += HandleTowerPauseRequested;
            hudBound = true;
        }

        private void UnbindHud()
        {
            if (hudBound && hud != null)
                hud.TowerPauseRequested -= HandleTowerPauseRequested;
            hudBound = false;
        }

        private void HandleTowerPauseRequested(string stableId)
        {
            if (selectedKind != GrayboxDefenseSelectionKind3D.Tower ||
                !string.Equals(
                    selectedStableId,
                    stableId,
                    StringComparison.Ordinal))
            {
                return;
            }
            TryToggleSelectedTowerPause();
        }
    }
}
