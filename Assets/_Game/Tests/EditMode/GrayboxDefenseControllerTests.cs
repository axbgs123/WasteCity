using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseControllerTests
    {
        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        [Test]
        public void UnconfiguredControllerFailsSafelyWithoutChangingSelection()
        {
            GrayboxDefenseController3D controller =
                AddComponent<GrayboxDefenseController3D>("Defense");

            Assert.DoesNotThrow(() => controller.Tick(.1f, paused: false));
            Assert.That(
                controller.TrySelect(new Ray(Vector3.zero, Vector3.forward)),
                Is.False);
            Assert.DoesNotThrow(controller.CloseSelection);
            Assert.That(controller.HasSelection, Is.False);
            Assert.That(controller.SelectedKind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.None));
            Assert.That(controller.SelectedStableId, Is.Null);
            Assert.That(controller.TryToggleSelectedTowerPause(), Is.False);
        }

        [Test]
        public void ConfigureUsesCityCellAndTicksRuntimeWorldViewAndHudTogether()
        {
            RuntimeFixture fixture = CreateGeneratedRuntime();
            GrayboxBuildingInstance3D turret =
                CreateCompletedDefenseChain(fixture);

            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.World,
                fixture.BuildingPresentation,
                fixture.DefenseWorldView,
                fixture.Hud);
            fixture.Controller.Tick(.1f, paused: false);

            Assert.That(fixture.Controller.Runtime, Is.Not.Null);
            Assert.That(fixture.Controller.Snapshot, Is.Not.Null);
            Assert.That(
                fixture.Controller.Snapshot.TutorialWaveTriggerCount,
                Is.EqualTo(
                    fixture.Controller.Runtime.Snapshot
                        .TutorialWaveTriggerCount));
            Assert.That(
                fixture.Controller.Snapshot.Towers.Select(value =>
                    value.StableId),
                Is.EqualTo(new[] { turret.StableInstanceId }));
            Assert.That(
                fixture.Controller.Snapshot.TutorialWaveTriggerCount,
                Is.EqualTo(1));
            Assert.That(
                fixture.Controller.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(14.9f).Within(.0001f));
            Assert.That(fixture.DefenseWorldView.LastSnapshot,
                Is.SameAs(fixture.Controller.Snapshot));
            Assert.That(fixture.Hud.LastSnapshot,
                Is.SameAs(fixture.Controller.Snapshot));
            Assert.That(fixture.DefenseWorldView.RefreshCount,
                Is.GreaterThan(0));
            Assert.That(fixture.Hud.RefreshCount, Is.GreaterThan(0));
            Assert.That(fixture.Hud.WarningVisible, Is.True);

            float warningBeforePause =
                fixture.Controller.Snapshot.WarningRemainingSeconds;
            int ammoBeforePause =
                fixture.Controller.Snapshot.Towers.Single().Ammo;
            fixture.Controller.Tick(10f, paused: true);
            Assert.That(
                fixture.Controller.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(warningBeforePause));
            Assert.That(fixture.Controller.Snapshot.Towers.Single().Ammo,
                Is.EqualTo(ammoBeforePause));
        }

        [Test]
        public void PersistencePauseBarrierDoesNotSynchronizeOrAdvanceRuntime()
        {
            RuntimeFixture fixture = CreateGeneratedRuntime();
            GrayboxBuildingInstance3D firstTurret =
                CreateCompletedDefenseChain(fixture);
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.World,
                fixture.BuildingPresentation,
                fixture.DefenseWorldView,
                fixture.Hud);
            fixture.Controller.Tick(.1f, paused: false);
            float warningBefore =
                fixture.Controller.Snapshot.WarningRemainingSeconds;

            GrayboxBuildingInstance3D secondTurret = BeginBuilding(
                fixture.Session,
                BuildingCatalog.MachineGunTurret,
                10,
                12);
            fixture.Session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
            fixture.Controller.ConfigurePersistencePauseSource(() => true);

            Assert.That(fixture.Controller.Tick(10f, paused: false), Is.True);

            Assert.That(fixture.Controller.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(warningBefore));
            Assert.That(
                fixture.Controller.Snapshot.Towers.Select(value =>
                    value.StableId),
                Is.EqualTo(new[] { firstTurret.StableInstanceId }));
            Assert.That(
                fixture.Controller.Snapshot.Towers.Any(value =>
                    value.StableId == secondTurret.StableInstanceId),
                Is.False,
                "The persistence barrier must not synchronize new authority.");
        }

        [Test]
        public void PersistenceRebuildSynchronizesWithoutAdvancingRules()
        {
            RuntimeFixture fixture = CreateGeneratedRuntime();
            CreateCompletedDefenseChain(fixture);
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.World,
                fixture.BuildingPresentation,
                fixture.DefenseWorldView,
                fixture.Hud);
            fixture.Controller.Tick(.1f, paused: false);
            fixture.Controller.Tick(20f, paused: false);
            float warningBefore =
                fixture.Controller.Snapshot.WarningRemainingSeconds;
            int enemyCountBefore = fixture.Controller.Snapshot.Enemies.Count;
            Assert.That(enemyCountBefore, Is.GreaterThan(0));
            GrayboxDefenseEnemySnapshot3D enemyBefore =
                fixture.Controller.Snapshot.Enemies[0];
            int coreHealthBefore =
                fixture.Controller.Snapshot.CoreCurrentHealth;

            GrayboxBuildingInstance3D secondTurret = BeginBuilding(
                fixture.Session,
                BuildingCatalog.MachineGunTurret,
                10,
                12);
            fixture.Session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
            fixture.Controller.ConfigurePersistencePauseSource(() => true);

            Assert.That(
                fixture.Controller.TryRebuildAfterPersistenceRestore(
                    out string error),
                Is.True,
                error);

            Assert.That(error, Is.Empty);
            Assert.That(fixture.Controller.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(warningBefore));
            Assert.That(fixture.Controller.Snapshot.Enemies.Count,
                Is.EqualTo(enemyCountBefore));
            GrayboxDefenseEnemySnapshot3D enemyAfter =
                fixture.Controller.Snapshot.Enemies[0];
            Assert.That(enemyAfter.StableId, Is.EqualTo(enemyBefore.StableId));
            Assert.That(enemyAfter.X, Is.EqualTo(enemyBefore.X));
            Assert.That(enemyAfter.Z, Is.EqualTo(enemyBefore.Z));
            Assert.That(enemyAfter.CurrentHealth,
                Is.EqualTo(enemyBefore.CurrentHealth));
            Assert.That(fixture.Controller.Snapshot.CoreCurrentHealth,
                Is.EqualTo(coreHealthBefore));
            Assert.That(
                fixture.Controller.Snapshot.Towers.Any(value =>
                    value.StableId == secondTurret.StableInstanceId),
                Is.True);
            Assert.That(fixture.DefenseWorldView.LastSnapshot,
                Is.SameAs(fixture.Controller.Snapshot));
            Assert.That(fixture.Hud.LastSnapshot,
                Is.SameAs(fixture.Controller.Snapshot));
        }

        [Test]
        public void RaySelectionCloseAndTowerPauseCommandsUseStableRuntimeIds()
        {
            RuntimeFixture fixture = CreateGeneratedRuntime();
            GrayboxBuildingInstance3D turret =
                CreateCompletedDefenseChain(fixture);
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.World,
                fixture.BuildingPresentation,
                fixture.DefenseWorldView,
                fixture.Hud);
            fixture.Controller.Tick(.1f, paused: false);

            Assert.That(fixture.DefenseWorldView.TryGetTowerObject(
                turret.StableInstanceId,
                out GameObject towerObject), Is.True);
            Assert.That(fixture.Controller.TrySelect(DownRay(towerObject)),
                Is.True);
            Assert.That(fixture.Controller.HasSelection, Is.True);
            Assert.That(fixture.Controller.SelectedKind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.Tower));
            Assert.That(fixture.Controller.SelectedStableId,
                Is.EqualTo(turret.StableInstanceId));
            Assert.That(fixture.Hud.DetailsVisible, Is.True);

            Assert.That(fixture.Controller.TryToggleSelectedTowerPause(),
                Is.True);
            Assert.That(
                fixture.Controller.Snapshot.Towers.Single().PlayerPaused,
                Is.True);
            Assert.That(fixture.Hud.TowerPauseButtonLabel,
                Does.Contain("恢复"));

            fixture.Controller.CloseSelection();
            Assert.That(fixture.Controller.HasSelection, Is.False);
            Assert.That(fixture.Hud.DetailsVisible, Is.False);

            Assert.That(fixture.Controller.TryToggleSelectedTowerPause(),
                Is.False);
            fixture.Controller.Tick(20f, paused: false);
            Assert.That(fixture.Controller.Snapshot.Enemies, Is.Not.Empty);
            string enemyId = fixture.Controller.Snapshot.Enemies[0].StableId;
            Assert.That(fixture.DefenseWorldView.TryGetEnemyObject(
                enemyId,
                out GameObject enemyObject), Is.True);
            Assert.That(fixture.Controller.TrySelect(DownRay(enemyObject)),
                Is.True);
            Assert.That(fixture.Controller.SelectedKind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.Enemy));
            Assert.That(fixture.Controller.SelectedStableId,
                Is.EqualTo(enemyId));
        }

        private RuntimeFixture CreateGeneratedRuntime()
        {
            GrayboxBuildingSession3D session =
                AddComponent<GrayboxBuildingSession3D>("Session");
            session.ConfigureDevelopmentFixture();
            session.UnlockAllResearchForDevelopment();

            GameObject worldObject = Track(new GameObject("World"));
            GrayboxWorldView3D world =
                worldObject.AddComponent<GrayboxWorldView3D>();
            Transform terrain = NewChild(worldObject.transform, "Terrain");
            Transform resources = NewChild(worldObject.transform, "Resources");
            Transform obstacles = NewChild(worldObject.transform, "Obstacles");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            world.Configure(terrain, resources, obstacles, material);
            world.Generate(new WorldMapModel(FilledOpenMap(24, 24)));

            GameObject cityObject = Track(new GameObject("City"));
            Assert.That(world.Coordinates.TryCellToWorld(
                12,
                12,
                .5f,
                out Vector3 cityPosition), Is.True);
            cityObject.transform.position = cityPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider collider = cityObject.AddComponent<BoxCollider>();
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, collider);
            Assert.That(city.RestoreDeploymentForDevelopment(
                CityMode.Fortress), Is.True);

            GrayboxBuildingWorldView3D buildingPresentation =
                AddComponent<GrayboxBuildingWorldView3D>("Buildings");
            GrayboxDefenseWorldView3D defenseWorldView =
                AddComponent<GrayboxDefenseWorldView3D>("DefenseWorld");
            GrayboxDefenseHud3D hud =
                AddComponent<GrayboxDefenseHud3D>("DefenseHud");
            GrayboxDefenseController3D controller =
                AddComponent<GrayboxDefenseController3D>("Defense");
            return new RuntimeFixture(
                session,
                city,
                world,
                buildingPresentation,
                defenseWorldView,
                hud,
                controller);
        }

        private static GrayboxBuildingInstance3D CreateCompletedDefenseChain(
            RuntimeFixture fixture)
        {
            BeginBuilding(fixture.Session, BuildingCatalog.Smelter, 14, 14);
            fixture.Session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
            BeginBuilding(fixture.Session, BuildingCatalog.Assembler, 16, 14);
            fixture.Session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
            GrayboxBuildingInstance3D turret = BeginBuilding(
                fixture.Session,
                BuildingCatalog.MachineGunTurret,
                14,
                12);
            fixture.Session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
            return turret;
        }

        private static GrayboxBuildingInstance3D BeginBuilding(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            int x,
            int y)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                cityX: 12,
                cityY: 12,
                groundRadius: session.GroundBuildRadius,
                cityMode: CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                compatibleResourceNode: ResourceNodeBinding.None,
                contentVisible: true,
                unlock: unlock,
                canAfford: true);
            Assert.That(session.TryBeginConstruction(
                request,
                NullPresentation.Instance,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static Ray DownRay(GameObject target)
        {
            return new Ray(
                target.transform.position + Vector3.up * 10f,
                Vector3.down);
        }

        private T AddComponent<T>(string name) where T : Component
        {
            return Track(new GameObject(name)).AddComponent<T>();
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static WorldCell[,] FilledOpenMap(int width, int height)
        {
            var cells = new WorldCell[width, height];
            var open = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = open;
            return cells;
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(
                GrayboxBuildingSession3D session,
                GrayboxMobileCityController3D city,
                GrayboxWorldView3D world,
                GrayboxBuildingWorldView3D buildingPresentation,
                GrayboxDefenseWorldView3D defenseWorldView,
                GrayboxDefenseHud3D hud,
                GrayboxDefenseController3D controller)
            {
                Session = session;
                City = city;
                World = world;
                BuildingPresentation = buildingPresentation;
                DefenseWorldView = defenseWorldView;
                Hud = hud;
                Controller = controller;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxWorldView3D World { get; }
            public GrayboxBuildingWorldView3D BuildingPresentation { get; }
            public GrayboxDefenseWorldView3D DefenseWorldView { get; }
            public GrayboxDefenseHud3D Hud { get; }
            public GrayboxDefenseController3D Controller { get; }
        }

        private sealed class NullPresentation :
            IGrayboxBuildingPresentation3D
        {
            public static NullPresentation Instance { get; } =
                new NullPresentation();

            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
