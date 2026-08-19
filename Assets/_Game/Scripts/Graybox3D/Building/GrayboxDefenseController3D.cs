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

        private GrayboxDefenseRuntime3D runtime;
        private GrayboxDefenseRuntimeSnapshot3D snapshot;
        private GrayboxDefenseRuntimeSnapshot3D presentedSnapshot;
        private PlanarCoordinateMapper3D boundCoordinates;
        private GrayboxDefenseSelectionKind3D selectedKind;
        private GrayboxDefenseSelectionKind3D presentedKind;
        private string selectedStableId;
        private string presentedStableId;
        private bool hudBound;

        public GrayboxDefenseRuntime3D Runtime => runtime;
        public GrayboxDefenseRuntimeSnapshot3D Snapshot => snapshot;
        public GrayboxDefenseWorldView3D WorldView => worldView;
        public GrayboxDefenseHudView3D Hud => hud;
        public bool HasSelection =>
            selectedKind != GrayboxDefenseSelectionKind3D.None &&
            !string.IsNullOrWhiteSpace(selectedStableId);
        public GrayboxDefenseSelectionKind3D SelectedKind => selectedKind;
        public string SelectedStableId => selectedStableId;
        public bool IsConfigured =>
            session != null &&
            city != null &&
            world != null &&
            buildingPresentation != null &&
            worldView != null &&
            hud != null;

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingWorldView3D buildingPresentation,
            GrayboxDefenseWorldView3D worldView,
            GrayboxDefenseHudView3D hud)
        {
            UnbindHud();
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.city = city ??
                throw new ArgumentNullException(nameof(city));
            this.world = world ??
                throw new ArgumentNullException(nameof(world));
            this.buildingPresentation = buildingPresentation ??
                throw new ArgumentNullException(nameof(buildingPresentation));
            this.worldView = worldView ??
                throw new ArgumentNullException(nameof(worldView));
            this.hud = hud ?? throw new ArgumentNullException(nameof(hud));
            InvalidatePresentation();
            BindHud();
        }

        public bool Tick(float deltaSeconds, bool paused)
        {
            using (TickMarker.Auto())
            {
                worldView?.SetSimulationPaused(paused);
                if (!IsConfigured ||
                    session.CityStorage == null ||
                    session.Instances == null ||
                    world.Coordinates == null ||
                    !TryResolveCityPosition(
                        out int cityX,
                        out int cityY,
                        out float logicalCoreX,
                        out float logicalCoreZ))
                {
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
                    session.GroundBuildRadius);
                runtime.Tick(deltaSeconds, paused, session.CityStorage);
                snapshot = runtime.Snapshot;
                ValidateSelection();
                ApplyPresentation();
                return true;
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
            session = null;
            city = null;
            world = null;
            buildingPresentation = null;
            worldView = null;
            hud = null;
            runtime = null;
            snapshot = null;
            presentedSnapshot = null;
            boundCoordinates = null;
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
