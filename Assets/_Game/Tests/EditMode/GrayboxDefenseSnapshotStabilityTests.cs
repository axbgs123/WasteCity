using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseSnapshotStabilityTests
    {
        private const int SnapshotAllocationSampleCount = 300;
        private const long ActiveSnapshotAllocationBudgetBytes = 64000;

        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void RuntimeReusesSnapshotUntilARealFixedStepChangesState()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.snapshot-fixed-step",
                x: 0,
                y: 0);
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, new[] { turret });

            GrayboxDefenseRuntimeSnapshot3D initial = runtime.Snapshot;
            Assert.That(runtime.Snapshot, Is.SameAs(initial),
                "Repeated reads of unchanged runtime state must be free of " +
                "new snapshot objects.");
            Assert.That(initial.WarningRemainingSeconds, Is.EqualTo(15f));
            Assert.That(initial.Towers[0].Ammo, Is.Zero);

            runtime.Tick(.04f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot, Is.SameAs(initial));
            runtime.Tick(.05f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot, Is.SameAs(initial),
                "Accumulating less than the 0.1-second fixed step is not an " +
                "observable state change.");

            runtime.Tick(.01f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseRuntimeSnapshot3D advanced = runtime.Snapshot;
            Assert.That(advanced, Is.Not.SameAs(initial));
            Assert.That(runtime.Snapshot, Is.SameAs(advanced));
            Assert.That(advanced.WarningRemainingSeconds,
                Is.EqualTo(14.9f).Within(.0001f));
            Assert.That(advanced.Towers[0].Ammo, Is.EqualTo(30));
            Assert.That(initial.WarningRemainingSeconds, Is.EqualTo(15f),
                "A published snapshot remains immutable after later ticks.");
            Assert.That(initial.Towers[0].Ammo, Is.Zero);
        }

        [Test]
        public void ActiveDefenseSnapshotPublication_StaysWithinAllocationBudget()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.snapshot-allocation",
                x: 0,
                y: 0);
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            var runtime = new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 1000f,
                spawnZ: 0f);
            Synchronize(runtime, new[] { turret });
            runtime.Tick(55f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseRuntimeSnapshot3D active = runtime.Snapshot;
            Assert.That(active.Towers, Has.Count.EqualTo(1));
            Assert.That(active.SpawnedEnemyCount, Is.EqualTo(8));
            Assert.That(active.AliveEnemyCount, Is.EqualTo(8));
            Assert.That(active.Enemies, Has.Count.EqualTo(8));

            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            _ = runtime.Snapshot;
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            _ = runtime.Snapshot;

            int observable = 0;
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                16384,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var sample = 0;
                 sample < SnapshotAllocationSampleCount;
                 sample++)
            {
                runtime.Tick(
                    .1f,
                    globallyPaused: false,
                    cityStorage: storage);
                GrayboxDefenseRuntimeSnapshot3D snapshot = runtime.Snapshot;
                observable += snapshot.AliveEnemyCount;
                observable += snapshot.Enemies[0].CurrentHealth;
            }
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;
            recorder.Stop();
            long profiledBytes = 0;
            for (var sample = 0; sample < recorder.Count; sample++)
            {
                ProfilerRecorderSample allocation =
                    recorder.GetSample(sample);
                profiledBytes += allocation.Value * allocation.Count;
            }
            recorder.Dispose();
            long measuredBytes = Math.Max(allocated, profiledBytes);

            TestContext.WriteLine(
                "ActiveDefenseSnapshotPublicationCurrentThreadBytes=" +
                allocated);
            TestContext.WriteLine(
                "ActiveDefenseSnapshotPublicationProfiledBytes=" +
                profiledBytes);
            TestContext.WriteLine(
                "ActiveDefenseSnapshotPublicationMeasuredBytes=" +
                measuredBytes);
            TestContext.WriteLine(
                "ActiveDefenseSnapshotPublicationBudgetBytes=" +
                ActiveSnapshotAllocationBudgetBytes);
            Assert.That(observable, Is.GreaterThan(0));
            Assert.That(
                measuredBytes,
                Is.LessThanOrEqualTo(ActiveSnapshotAllocationBudgetBytes),
                "A warmed active defense runtime should publish one bounded " +
                "snapshot per fixed step without rebuilding nested snapshot " +
                "graphs, parsing enemy IDs, or wrapping temporary arrays.");
        }

        [Test]
        public void PausePublishesOnlyWhenTowerStateActuallyChanges()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.snapshot-pause",
                x: 0,
                y: 0);
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, new[] { turret });
            GrayboxDefenseRuntimeSnapshot3D running = runtime.Snapshot;

            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            GrayboxDefenseRuntimeSnapshot3D paused = runtime.Snapshot;
            Assert.That(paused, Is.Not.SameAs(running));
            Assert.That(running.Towers[0].PlayerPaused, Is.False);
            Assert.That(paused.Towers[0].PlayerPaused, Is.True);

            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            Assert.That(runtime.Snapshot, Is.SameAs(paused),
                "Setting an already-paused tower to the same value must not " +
                "publish a duplicate snapshot.");
        }

        [Test]
        public void TopologyAndLogisticsChangesPublishImmutableSnapshots()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.snapshot-topology",
                x: 0,
                y: 0);
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, new[] { turret });
            GrayboxDefenseRuntimeSnapshot3D connected = runtime.Snapshot;
            Assert.That(connected.Towers[0].Connected, Is.True);

            Synchronize(runtime, new[] { turret });
            Assert.That(runtime.Snapshot, Is.SameAs(connected),
                "An identical synchronization pass must retain the current " +
                "snapshot reference.");

            Synchronize(
                runtime,
                new[] { turret },
                cityMode: CityMode.Fortress,
                cityX: -20,
                cityY: 0);
            GrayboxDefenseRuntimeSnapshot3D disconnected = runtime.Snapshot;
            Assert.That(disconnected, Is.Not.SameAs(connected));
            Assert.That(disconnected.Towers[0].Connected, Is.False);
            Assert.That(connected.Towers[0].Connected, Is.True,
                "Previously published connectivity remains immutable.");

            Synchronize(
                runtime,
                new[] { turret },
                cityMode: CityMode.Fortress,
                cityX: -20,
                cityY: 0);
            Assert.That(runtime.Snapshot, Is.SameAs(disconnected));

            Synchronize(runtime,
                Array.Empty<GrayboxBuildingInstance3D>());
            GrayboxDefenseRuntimeSnapshot3D removed = runtime.Snapshot;
            Assert.That(removed, Is.Not.SameAs(disconnected));
            Assert.That(removed.Towers, Is.Empty);
            Assert.That(disconnected.Towers, Has.Count.EqualTo(1),
                "Topology changes must not mutate an older snapshot.");
        }

        [Test]
        public void ControllerSubstepFramesReuseSnapshotWithoutRefreshingViews()
        {
            ControllerFixture fixture = CreateControllerFixture();
            CreateCompletedDefenseChain(fixture.Session);
            fixture.Controller.Configure(
                fixture.Session,
                fixture.City,
                fixture.World,
                fixture.BuildingPresentation,
                fixture.DefenseWorldView,
                fixture.Hud);
            Assert.That(fixture.Controller.Tick(.1f, paused: false), Is.True);

            GrayboxDefenseRuntimeSnapshot3D stable =
                fixture.Controller.Snapshot;
            int worldRefreshes = fixture.DefenseWorldView.RefreshCount;
            int hudRefreshes = fixture.Hud.RefreshCount;
            for (int frame = 0; frame < 4; frame++)
            {
                Assert.That(
                    fixture.Controller.Tick(.02f, paused: false),
                    Is.True);
                Assert.That(fixture.Controller.Snapshot, Is.SameAs(stable));
                Assert.That(fixture.DefenseWorldView.LastSnapshot,
                    Is.SameAs(stable));
                Assert.That(fixture.Hud.LastSnapshot, Is.SameAs(stable));
            }

            Assert.That(fixture.DefenseWorldView.RefreshCount,
                Is.EqualTo(worldRefreshes),
                "An unchanged immutable snapshot must stay out of the " +
                "presentation formatting and visual refresh hot path.");
            Assert.That(fixture.Hud.RefreshCount,
                Is.EqualTo(hudRefreshes));
        }

        private ControllerFixture CreateControllerFixture()
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

            return new ControllerFixture(
                session,
                city,
                world,
                AddComponent<GrayboxBuildingWorldView3D>("Buildings"),
                AddComponent<GrayboxDefenseWorldView3D>("DefenseWorld"),
                AddComponent<GrayboxDefenseHud3D>("DefenseHud"),
                AddComponent<GrayboxDefenseController3D>("Defense"));
        }

        private static void CreateCompletedDefenseChain(
            GrayboxBuildingSession3D session)
        {
            BeginBuilding(session, BuildingCatalog.Smelter, 14, 14);
            session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
            BeginBuilding(session, BuildingCatalog.Assembler, 16, 14);
            session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
            BeginBuilding(
                session,
                BuildingCatalog.MachineGunTurret,
                14,
                12);
            session.CompleteAllConstructionForDevelopment(
                NullPresentation.Instance);
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

        private static GrayboxDefenseRuntime3D Runtime()
        {
            return new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 9f,
                spawnZ: 0f);
        }

        private static void Synchronize(
            GrayboxDefenseRuntime3D runtime,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode = CityMode.Fortress,
            int cityX = 0,
            int cityY = 0)
        {
            runtime.Synchronize(
                instances,
                cityMode,
                cityX,
                cityY,
                BuildingRangeRules.InitialGroundRadius);
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
            string stableInstanceId,
            int x,
            int y)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableInstanceId,
                    new PlacedBuilding(
                        BuildingCatalog.MachineGunTurret,
                        x,
                        y,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(
                        BuildingCatalog.MachineGunTurret.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, Array.Empty<object>());
            return instance;
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

        private sealed class ControllerFixture
        {
            public ControllerFixture(
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
