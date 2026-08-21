using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionControllerTests
    {
        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            created.Clear();
        }

        [Test]
        public void ConfigureRejectsEveryMissingRequiredRuntimeReference()
        {
            GrayboxProductionController3D controller =
                AddComponent<GrayboxProductionController3D>("Production");
            GrayboxBuildingSession3D session = CreateSession();
            GrayboxMobileCityController3D city =
                AddComponent<GrayboxMobileCityController3D>("City");
            GrayboxWorldView3D world =
                AddComponent<GrayboxWorldView3D>("World");

            Assert.Throws<ArgumentNullException>(
                () => controller.Configure(null, city, world));
            Assert.Throws<ArgumentNullException>(
                () => controller.Configure(session, null, world));
            Assert.Throws<ArgumentNullException>(
                () => controller.Configure(session, city, null));
        }

        [Test]
        public void TickIsASafeNoOpBeforeConfigurationAndBeforeWorldGeneration()
        {
            GrayboxProductionController3D controller =
                AddComponent<GrayboxProductionController3D>("Production");

            Assert.DoesNotThrow(() => controller.Tick(.1f, paused: false));

            GrayboxBuildingSession3D session = CreateSession();
            GrayboxMobileCityController3D city =
                AddComponent<GrayboxMobileCityController3D>("City");
            GrayboxWorldView3D world =
                AddComponent<GrayboxWorldView3D>("WorldNotGenerated");
            controller.Configure(session, city, world);

            Assert.DoesNotThrow(() => controller.Tick(1f, paused: false));
            Assert.That(controller.Clock.AccumulatorSeconds, Is.Zero);
            Assert.That(controller.Clock.Runtime.States, Is.Empty);
        }

        [Test]
        public void ConfiguredControllerDrivesFixedClockFromRealSessionCityAndWorld()
        {
            RuntimeFixture fixture = CreateGeneratedRuntime();
            fixture.Session.UnlockAllResearchForDevelopment();
            GrayboxBuildingInstance3D smelter = BeginSmelter(
                fixture.Session,
                x: 10,
                y: 10);
            fixture.Session.TickConstruction(
                BuildingCatalog.Smelter.BuildSeconds,
                CityMode.Fortress,
                paused: false,
                presentation: NullPresentation.Instance);
            Assert.That(smelter.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            int cityIronBefore = fixture.Session.Inventory.Get(ResourceIds.Iron);

            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.World);
            fixture.Controller.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false);

            Assert.That(
                fixture.Controller.Clock.Runtime.TryGetState(
                    smelter.StableInstanceId,
                    out BuildingProductionState state),
                Is.True);
            Assert.That(state.IsLogisticsConnected, Is.True);
            Assert.That(state.ProgressSeconds,
                Is.EqualTo(GrayboxProductionClock3D.StepSeconds).Within(.0001f));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(18));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(cityIronBefore - 20));
            Assert.That(fixture.Controller.Clock.AccumulatorSeconds,
                Is.Zero.Within(.0001f));
            Assert.That(fixture.Controller.Snapshot,
                Is.SameAs(fixture.Controller.Clock.Snapshot));
            Assert.That(fixture.Controller.Revision,
                Is.EqualTo(fixture.Controller.Snapshot.Revision));
            Assert.That(fixture.Controller.Commands,
                Is.SameAs(fixture.Controller.Clock.Commands));
            Assert.That(fixture.Controller.Snapshot.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability details), Is.True);
            Assert.That(details.ProgressSeconds,
                Is.EqualTo(state.ProgressSeconds).Within(.0001f));
        }

        [Test]
        public void RebuildAfterPersistenceRestoreRefreshesSnapshotWithoutAdvancingTruth()
        {
            RuntimeFixture fixture = CreateGeneratedRuntime();
            fixture.Session.UnlockAllResearchForDevelopment();
            GrayboxBuildingInstance3D smelter = BeginSmelter(
                fixture.Session,
                x: 10,
                y: 10);
            fixture.Session.TickConstruction(
                BuildingCatalog.Smelter.BuildSeconds,
                CityMode.Fortress,
                paused: false,
                presentation: NullPresentation.Instance);
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.World);
            Assert.That(fixture.Controller.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false), Is.True);
            Assert.That(fixture.Controller.Clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(fixture.Controller.Snapshot.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability stale), Is.True);

            Assert.That(state.TryRestoreForPersistence(
                state.Input.CapturePositiveAmounts(),
                hasReservedInputs: true,
                state.ReservedInputs,
                state.Output.CapturePositiveAmounts(),
                progressSeconds: 2f,
                isPlayerPaused: false,
                out string restoreError), Is.True, restoreError);
            Assert.That(stale.ProgressSeconds, Is.Not.EqualTo(2f));
            int cityIron = fixture.Session.Inventory.Get(ResourceIds.Iron);
            int localIron = state.Input.Get(ResourceIds.Iron);
            int localAlloy = state.Output.Get(ResourceIds.Alloy);
            float accumulator = fixture.Controller.Clock.AccumulatorSeconds;

            Assert.That(
                fixture.Controller.TryRebuildAfterPersistenceRestore(
                    out string error),
                Is.True,
                error);

            Assert.That(state.ProgressSeconds, Is.EqualTo(2f));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(cityIron));
            Assert.That(state.Input.Get(ResourceIds.Iron),
                Is.EqualTo(localIron));
            Assert.That(state.Output.Get(ResourceIds.Alloy),
                Is.EqualTo(localAlloy));
            Assert.That(fixture.Controller.Clock.AccumulatorSeconds,
                Is.EqualTo(accumulator));
            Assert.That(fixture.Controller.Snapshot.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability refreshed), Is.True);
            Assert.That(refreshed.ProgressSeconds, Is.EqualTo(2f));
        }

        private RuntimeFixture CreateGeneratedRuntime()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var worldObject = Track(new GameObject("World"));
            GrayboxWorldView3D world =
                worldObject.AddComponent<GrayboxWorldView3D>();
            Transform terrain = NewChild(worldObject.transform, "Terrain");
            Transform resources = NewChild(worldObject.transform, "Resources");
            Transform obstacles = NewChild(worldObject.transform, "Obstacles");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = Track(new Material(shader));
            world.Configure(terrain, resources, obstacles, material);
            world.Generate(new WorldMapModel(FilledOpenMap(24, 24)));

            var cityObject = Track(new GameObject("City"));
            world.Coordinates.TryCellToWorld(
                12,
                12,
                .5f,
                out Vector3 cityPosition);
            cityObject.transform.position = cityPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider collider = cityObject.AddComponent<BoxCollider>();
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, collider);
            Assert.That(
                city.RestoreDeploymentForDevelopment(CityMode.Fortress),
                Is.True);

            GrayboxProductionController3D controller =
                AddComponent<GrayboxProductionController3D>("Production");
            return new RuntimeFixture(session, city, world, controller);
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            GrayboxBuildingSession3D session =
                AddComponent<GrayboxBuildingSession3D>("Session");
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private GrayboxBuildingInstance3D BeginSmelter(
            GrayboxBuildingSession3D session,
            int x,
            int y)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                BuildingCatalog.Smelter,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                BuildingCatalog.Smelter,
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
            Assert.That(
                session.TryBeginConstruction(
                    request,
                    NullPresentation.Instance,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private T AddComponent<T>(string name) where T : Component
        {
            var gameObject = Track(new GameObject(name));
            return gameObject.AddComponent<T>();
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static WorldCell[,] FilledOpenMap(int width, int height)
        {
            var cells = new WorldCell[width, height];
            var open = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                cells[x, y] = open;
            return cells;
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(
                GrayboxBuildingSession3D session,
                GrayboxMobileCityController3D city,
                GrayboxWorldView3D world,
                GrayboxProductionController3D controller)
            {
                Session = session;
                City = city;
                World = world;
                Controller = controller;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxWorldView3D World { get; }
            public GrayboxProductionController3D Controller { get; }
        }

        private sealed class NullPresentation : IGrayboxBuildingPresentation3D
        {
            public static NullPresentation Instance { get; } =
                new NullPresentation();

            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
