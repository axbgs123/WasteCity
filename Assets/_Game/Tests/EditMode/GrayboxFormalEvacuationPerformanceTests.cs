using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalEvacuationPerformanceTests
    {
        private const int StableSampleCount = 300;
        private const string PerformanceProbeTypeName =
            "WasteCity.Editor.GrayboxPerformanceProbe";

        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void FormalMixedFixture_UsesCanonicalChainEightEnemiesAndVisibleViews()
        {
            MixedFixture fixture = CreateMixedFixture();

            Assert.That(fixture.Production.States, Has.Count.EqualTo(5),
                "The mixed workload owns 2 mines, 2 smelters and 1 assembler.");
            Assert.That(fixture.Production.RunnableStates,
                Has.Count.EqualTo(5));
            for (int index = 0;
                 index < fixture.Production.RunnableStates.Count;
                 index++)
            {
                BuildingProductionState state =
                    fixture.Production.RunnableStates[index];
                Assert.That(state.ProgressSeconds, Is.GreaterThan(0f));
                Assert.That(state.StopReason, Is.EqualTo(
                    ProductionStopReason.None));
            }
            Assert.That(fixture.Defense.Snapshot.AliveEnemyCount,
                Is.EqualTo(8));
            Assert.That(fixture.Hud.SummaryRect.gameObject.activeInHierarchy,
                Is.True);
            Assert.That(fixture.Hud.SummaryText.text,
                Does.Contain("敌人 8"));
            Assert.That(fixture.Evacuation.IsManifestOpen, Is.True);
            Assert.That(fixture.Menu.EvacuationVisible, Is.True);
            Assert.That(fixture.Manifest.IsInCombat, Is.True);
            Assert.That(fixture.Manifest.Items.Count, Is.EqualTo(16),
                "The formal mixed gate uses the approved 16-item outer-city " +
                "manifest rather than a one-building micro benchmark.");
            Assert.That(
                fixture.Session.CompletedBuildingCount(
                    BuildingCatalog.Warehouse.Id.Value),
                Is.EqualTo(1));
            Assert.That(
                fixture.Session.CompletedBuildingCount(
                    BuildingCatalog.ResearchStation.Id.Value),
                Is.EqualTo(1));
            Assert.That(
                fixture.Session.CompletedBuildingCount(
                    BuildingCatalog.MachineGunTurret.Id.Value),
                Is.EqualTo(1));
            Assert.That(
                CountOuterState(
                    fixture.Session,
                    GrayboxBuildingInstanceState.UnderConstruction),
                Is.EqualTo(1));
            Assert.That(
                CountOuterState(
                    fixture.Session,
                    GrayboxBuildingInstanceState.Completed),
                Is.EqualTo(15));
        }

        [Test]
        public void FormalMixedFixture_ActuallyAdvancesTheThreeStageChain()
        {
            MixedFixture fixture = CreateMixedFixture();
            int firstNodeBefore = fixture.World.Get(5, 9).ResourceAmount;
            int secondNodeBefore = fixture.World.Get(5, 13).ResourceAmount;
            int ammunitionBefore = fixture.Session.GetCityResourceAmount(
                ResourceIds.Ammunition);

            for (int index = 0; index < 300; index++)
            {
                fixture.Simulation.Tick(
                    fixture.Production.RunnableStates,
                    .1f,
                    fixture.World,
                    fixture.Session.CityStorage,
                    globallyPaused: false);
            }

            Assert.That(
                fixture.World.Get(5, 9).ResourceAmount,
                Is.LessThan(firstNodeBefore),
                "The first mining station must perform real extraction work.");
            Assert.That(
                fixture.World.Get(5, 13).ResourceAmount,
                Is.LessThan(secondNodeBefore),
                "The second mining station must perform real extraction work.");
            Assert.That(
                fixture.Session.GetCityResourceAmount(ResourceIds.Ammunition),
                Is.GreaterThan(ammunitionBefore + 4),
                "Smelting and assembly must complete real upstream and " +
                "downstream production rather than remain OutputFull.");
        }

        [Test]
        public void PausedMixedFrame_DoesNotAdvanceAnyRuleOwner()
        {
            MixedFixture fixture = CreateMixedFixture();
            var progress = new float[fixture.Production.States.Count];
            for (int index = 0; index < progress.Length; index++)
                progress[index] = fixture.Production.States[index].ProgressSeconds;
            GrayboxDefenseRuntimeSnapshot3D defense = fixture.Defense.Snapshot;
            EvacuationManifestViewModel manifest =
                fixture.Evacuation.CaptureManifestView();
            ulong storageRevision = fixture.Session.CityStorage.Revision;

            fixture.Simulation.Tick(
                fixture.Production.RunnableStates,
                5f,
                fixture.World,
                fixture.Session.CityStorage,
                globallyPaused: true);
            fixture.Defense.Tick(
                5f,
                globallyPaused: true,
                fixture.Session.CityStorage);
            fixture.Evacuation.Tick(5f, paused: true);

            for (int index = 0; index < progress.Length; index++)
                Assert.That(fixture.Production.States[index].ProgressSeconds,
                    Is.EqualTo(progress[index]));
            Assert.That(fixture.Defense.Snapshot, Is.SameAs(defense));
            Assert.That(fixture.Evacuation.CaptureManifestView(),
                Is.SameAs(manifest));
            Assert.That(fixture.Session.CityStorage.Revision,
                Is.EqualTo(storageRevision));
        }

        [Test]
        public void StableManifestAdapters_AllocateZeroAndKeepObjectsAndListenersBounded()
        {
            MixedFixture fixture = CreateMixedFixture();
            for (int warmup = 0; warmup < 12; warmup++)
                fixture.TickStableAdapters();

            EvacuationManifestViewModel stable =
                fixture.Evacuation.CaptureManifestView();
            ulong renderedRevision = fixture.Menu.EvacuationRenderedRevision;
            int objectCount = fixture.UiObjectCount;
            string[] eventNames =
            {
                "EvacuationItemTreatmentRequested",
                "EvacuationCategoryTreatmentRequested",
                "EvacuationAllTreatmentRequested",
                "EvacuationConfirmationRequested",
                "EvacuationRetryRequested"
            };
            var listenerCounts = new int[eventNames.Length];
            for (int index = 0; index < eventNames.Length; index++)
                listenerCounts[index] = ListenerCount(
                    fixture.Menu,
                    eventNames[index]);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int sample = 0; sample < StableSampleCount; sample++)
                fixture.TickStableAdapters();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "FormalEvacuationStableManifestAllocationBytes=" + allocated);
            TestContext.WriteLine(
                "FormalEvacuationStableManifestUiObjectCount=" + objectCount);
            Assert.That(allocated, Is.Zero,
                "After warmup, unchanged input/manifest/UI adapters must " +
                "allocate 0 B across 300 calls.");
            Assert.That(fixture.Evacuation.CaptureManifestView(),
                Is.SameAs(stable));
            Assert.That(fixture.Menu.EvacuationRenderedRevision,
                Is.EqualTo(renderedRevision));
            Assert.That(fixture.UiObjectCount, Is.EqualTo(objectCount));
            Assert.That(objectCount, Is.LessThan(1024));
            for (int index = 0; index < eventNames.Length; index++)
            {
                Assert.That(
                    ListenerCount(fixture.Menu, eventNames[index]),
                    Is.EqualTo(listenerCounts[index]),
                    eventNames[index]);
                Assert.That(listenerCounts[index], Is.EqualTo(1),
                    eventNames[index]);
            }
        }

        [Test]
        public void ManifestPayloadSignature_UsesConstantTimeRuntimeLookups()
        {
            MethodInfo productionLookup = typeof(GrayboxProductionRuntime3D)
                .GetMethod(
                    "TryGetState",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(BuildingProductionState).MakeByRefType()
                    },
                    null);
            MethodInfo defenseLookup = typeof(GrayboxDefenseRuntime3D)
                .GetMethod(
                    "TryGetTowerState",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(GrayboxDefenseTowerRuntimeState3D).MakeByRefType()
                    },
                    null);

            Assert.That(productionLookup, Is.Not.Null);
            Assert.That(defenseLookup, Is.Not.Null,
                "Evacuation signatures need an O(1) tower lookup instead of " +
                "scanning every tower for every manifest item.");

            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxEvacuationController3D.cs"));
            string method = ExtractMethodBlock(
                source,
                "private void MixManifestRuntimePayload");
            StringAssert.Contains("productionRuntime.TryGetState", method);
            StringAssert.Contains("defenseRuntime.TryGetTowerState", method);
            StringAssert.DoesNotContain("productionRuntime?.States", method);
            StringAssert.DoesNotContain("defenseRuntime?.Towers", method);
        }

        [Test]
        public void FormalMixedProbe_ExposesExternalEntryPointAndMarkerContract()
        {
            Type probe = FindLoadedType(PerformanceProbeTypeName);
            MethodInfo method = probe?.GetMethod(
                "MeasureFormalEvacuationMixedPerformance",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null,
                "Task 9 requires a batchmode-callable mixed workload probe.");
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);
            AssertPublicStaticVoid(
                probe,
                "PrepareFormalEvacuationMixedProfilerCapture");
            AssertPublicStaticVoid(
                probe,
                "PulseFormalEvacuationTransactionalMarkersForProfiler");
            AssertPublicStaticVoid(
                probe,
                "CleanupFormalEvacuationMixedProfilerCapture");
            AssertPublicStaticVoid(
                probe,
                "CaptureFormalEvacuationMixedProfiler300Frames");
            AssertPublicStaticVoid(
                probe,
                "SummarizeFormalEvacuationMixedGuiProfilerCapture");

            Type heartbeat = FindLoadedType(
                "WasteCity.Graybox3D.Building." +
                "GrayboxFormalMixedProfilerHeartbeat3D");
            Assert.That(heartbeat, Is.Not.Null,
                "The formal GUI gate must run inside the PlayMode PlayerLoop.");
            Assert.That(heartbeat?.GetMethod("RequestPulse"), Is.Not.Null);
            Assert.That(
                heartbeat?.GetProperty("PulseCompletionCount"),
                Is.Not.Null);

            string[] sourcePaths =
            {
                "Assets/_Game/Editor/GrayboxPerformanceProbe.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxProductionController3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxDefenseController3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseRuntime3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxDefenseHudView3D.cs",
                "Assets/_Game/Scripts/Graybox3D/Building/GrayboxEvacuationController3D.cs"
            };
            string sources = string.Empty;
            for (int index = 0; index < sourcePaths.Length; index++)
                sources += File.ReadAllText(ProjectPath(sourcePaths[index]));

            string[] markers =
            {
                "WasteCity.Formal.Production.Tick",
                "WasteCity.Formal.Defense.Tick",
                "WasteCity.Formal.DefenseHud.Apply",
                "WasteCity.Formal.Evacuation.Tick",
                "WasteCity.Formal.Evacuation.ManifestView.Build",
                "WasteCity.Formal.Evacuation.CapacityPreflight",
                "WasteCity.Formal.Evacuation.Commit"
            };
            for (int index = 0; index < markers.Length; index++)
                StringAssert.Contains(markers[index], sources);
            StringAssert.Contains(
                "WASTECITY_FORMAL_EVACUATION_MIXED_PERF_RESULT",
                sources);
            StringAssert.Contains("300", sources);
            StringAssert.Contains("GC.Alloc", sources);
            StringAssert.Contains(
                "WasteCity.Formal.MixedWorkload.Frame",
                sources);
            StringAssert.Contains(
                "transactionMeasuredAllocationBytes",
                sources);
            StringAssert.Contains(
                "transactionAllocationBudgetBytes",
                sources);
            StringAssert.Contains(
                "transactionCommittedItemCount",
                sources);
            StringAssert.Contains(
                "productionStateAdvanceFrameCounts",
                sources);
            StringAssert.Contains(
                "WASTECITY_FORMAL_EVACUATION_MIXED_GUI_PROFILER_RESULT",
                sources);
            StringAssert.Contains(
                "ProfilerDriver.ClearAllFrames",
                sources);
            StringAssert.Contains(
                "ProfilerDriver.SaveProfile",
                sources);
            StringAssert.Contains("formalRequired", sources);
        }

        private static void AssertPublicStaticVoid(
            Type type,
            string methodName)
        {
            MethodInfo method = type?.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            Assert.That(method?.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(method?.GetParameters(), Is.Empty);
        }

        private MixedFixture CreateMixedFixture()
        {
            GrayboxBuildingSession3D session = AddComponent<
                GrayboxBuildingSession3D>("FormalMixed.Session");
            session.Configure(true);
            session.ConfigureDevelopmentFixture();
            session.UnlockAllResearchForDevelopment();
            session.Inventory.Set(ResourceIds.Iron, 5000);
            session.Inventory.Set(ResourceIds.Alloy, 5000);
            session.Inventory.Set(ResourceIds.Stone, 5000);
            session.Inventory.Set(ResourceIds.Ammunition, 5000);

            WorldMapModel world = CreateProductionWorld();
            BuildCanonicalMixedPopulation(session);
            session.Inventory.Set(ResourceIds.Iron, 40);
            session.Inventory.Set(ResourceIds.Alloy, 4);
            session.Inventory.Set(ResourceIds.Stone, 0);
            session.Inventory.Set(ResourceIds.Ammunition, 0);

            var production = new GrayboxProductionRuntime3D();
            production.Synchronize(
                session.Instances,
                CityMode.Fortress,
                12,
                12,
                session.GroundBuildRadius,
                session.CityStorage);
            var simulation = new FormalProductionSimulation();
            simulation.Tick(
                production.RunnableStates,
                .5f,
                world,
                session.CityStorage,
                globallyPaused: false);

            var defense = new GrayboxDefenseRuntime3D(
                coreX: 12f,
                coreZ: 12f,
                spawnX: 1000f,
                spawnZ: 12f);
            defense.Synchronize(
                session.Instances,
                CityMode.Fortress,
                12,
                12,
                session.GroundBuildRadius);
            defense.Tick(55f, globallyPaused: false, session.CityStorage);
            Assert.That(defense.Snapshot.AliveEnemyCount, Is.EqualTo(8));

            Canvas canvas = AddComponent<Canvas>("FormalMixed.Canvas");
            EventSystem eventSystem = AddComponent<EventSystem>(
                "FormalMixed.EventSystem");
            GrayboxBuildingInteractionModel3D interaction = AddComponent<
                GrayboxBuildingInteractionModel3D>("FormalMixed.Interaction");
            GrayboxBuildingMenuView3D menu = AddComponent<
                GrayboxBuildingMenuView3D>("FormalMixed.Menu");
            menu.Configure(canvas, eventSystem, session, interaction);
            GrayboxDefenseHudView3D hud = AddComponent<
                GrayboxDefenseHudView3D>("FormalMixed.DefenseHud");
            hud.Configure(canvas, eventSystem);
            hud.Apply(
                defense.Snapshot,
                GrayboxDefenseSelectionKind3D.None,
                null);

            GrayboxEvacuationController3D evacuation = AddComponent<
                GrayboxEvacuationController3D>("FormalMixed.Evacuation");
            evacuation.Configure(
                session,
                new FortressDeploymentRequest(),
                NullPresentation.Instance,
                menu);
            evacuation.ConfigureOperationalRuntimes(production, defense);
            Assert.That(evacuation.TryHandleDeploymentRequest(), Is.True);
            EvacuationManifestViewModel manifest =
                evacuation.CaptureManifestView();
            menu.ShowEvacuationManifest(manifest);

            GrayboxBuildingInputRouter3D input = AddComponent<
                GrayboxBuildingInputRouter3D>("FormalMixed.Input");
            input.Configure(
                menu,
                interaction,
                null,
                null,
                evacuation,
                null);
            return new MixedFixture(
                session,
                world,
                production,
                simulation,
                defense,
                hud,
                evacuation,
                menu,
                input,
                canvas);
        }

        private static WorldMapModel CreateProductionWorld()
        {
            var cells = new WorldCell[24, 24];
            var open = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
            for (int x = 0; x < 24; x++)
            for (int y = 0; y < 24; y++)
                cells[x, y] = open;
            cells[5, 9] = new WorldCell(
                TerrainKind.Rocky, ResourceIds.Iron, 1000);
            cells[5, 13] = new WorldCell(
                TerrainKind.Rocky, ResourceIds.Iron, 1000);
            return new WorldMapModel(cells);
        }

        private static void BuildCanonicalMixedPopulation(
            GrayboxBuildingSession3D session)
        {
            Begin(session, BuildingCatalog.MiningStation, BuildingSite.Ground,
                5, 9, new ResourceNodeBinding("world.resource-node.5.9", 5, 9));
            Begin(session, BuildingCatalog.MiningStation, BuildingSite.Ground,
                5, 13, new ResourceNodeBinding("world.resource-node.5.13", 5, 13));
            session.CompleteAllConstructionForDevelopment(NullPresentation.Instance);

            Begin(session, BuildingCatalog.Smelter, BuildingSite.Ground, 8, 8);
            Begin(session, BuildingCatalog.Smelter, BuildingSite.Ground, 8, 14);
            session.CompleteAllConstructionForDevelopment(NullPresentation.Instance);
            Begin(session, BuildingCatalog.Assembler, BuildingSite.InnerCity, 0, 0);
            session.CompleteAllConstructionForDevelopment(NullPresentation.Instance);
            Begin(session, BuildingCatalog.MachineGunTurret, BuildingSite.Ground,
                10, 12);
            session.CompleteAllConstructionForDevelopment(NullPresentation.Instance);

            Begin(session, BuildingCatalog.Warehouse, BuildingSite.Ground,
                14, 8);
            Begin(session, BuildingCatalog.ResearchStation, BuildingSite.Ground,
                14, 14);
            session.CompleteAllConstructionForDevelopment(NullPresentation.Instance);

            int[,] walls =
            {
                { 4, 6 }, { 6, 6 }, { 8, 6 }, { 10, 6 },
                { 12, 6 }, { 14, 6 }, { 16, 6 }, { 18, 6 },
                { 18, 10 }
            };
            for (int index = 0; index < walls.GetLength(0) - 1; index++)
                Begin(session, BuildingCatalog.Wall, BuildingSite.Ground,
                    walls[index, 0], walls[index, 1]);
            session.CompleteAllConstructionForDevelopment(NullPresentation.Instance);
            int finalIndex = walls.GetLength(0) - 1;
            Begin(session, BuildingCatalog.Wall, BuildingSite.Ground,
                walls[finalIndex, 0], walls[finalIndex, 1]);
        }

        private static int CountOuterState(
            GrayboxBuildingSession3D session,
            GrayboxBuildingInstanceState state)
        {
            int count = 0;
            for (int index = 0; index < session.Instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = session.Instances[index];
                if (instance.Placement.Site == BuildingSite.Ground &&
                    instance.State == state)
                    count++;
            }
            return count;
        }

        private static GrayboxBuildingInstance3D Begin(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            ResourceNodeBinding resourceNode = default)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var request = new BuildingPlacementRequest(
                definition,
                site == BuildingSite.Ground
                    ? session.GroundGrid
                    : session.InnerGrid,
                site,
                BuildingOrientation.North,
                x,
                y,
                12,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                compatibleResourceNode: resourceNode,
                contentVisible: true,
                unlock: unlock,
                canAfford: true);
            Assert.That(session.TryBeginConstruction(
                request,
                NullPresentation.Instance,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                definition.Name + ": " + evaluation.PrimaryFailure);
            return instance;
        }

        private static int ListenerCount(
            GrayboxBuildingMenuView3D menu,
            string eventName)
        {
            FieldInfo field = typeof(GrayboxBuildingMenuView3D).GetField(
                eventName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, eventName);
            return (field.GetValue(menu) as Delegate)?.GetInvocationList().Length
                   ?? 0;
        }

        private static Type FindLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        private static string ExtractMethodBlock(
            string source,
            string methodName)
        {
            int start = source.IndexOf(methodName, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), methodName);
            int openingBrace = source.IndexOf('{', start);
            Assert.That(openingBrace, Is.GreaterThanOrEqualTo(0));
            int depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}') depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            throw new AssertionException("Unbalanced method: " + methodName);
        }

        private T AddComponent<T>(string name) where T : Component
        {
            GameObject value = new GameObject(name);
            cleanup.Add(value);
            return value.AddComponent<T>();
        }

        private sealed class MixedFixture
        {
            public MixedFixture(
                GrayboxBuildingSession3D session,
                WorldMapModel world,
                GrayboxProductionRuntime3D production,
                FormalProductionSimulation simulation,
                GrayboxDefenseRuntime3D defense,
                GrayboxDefenseHudView3D hud,
                GrayboxEvacuationController3D evacuation,
                GrayboxBuildingMenuView3D menu,
                GrayboxBuildingInputRouter3D input,
                Canvas canvas)
            {
                Session = session;
                World = world;
                Production = production;
                Simulation = simulation;
                Defense = defense;
                Hud = hud;
                Evacuation = evacuation;
                Menu = menu;
                Input = input;
                Canvas = canvas;
                Manifest = evacuation.CaptureManifestView();
            }

            public GrayboxBuildingSession3D Session { get; }
            public WorldMapModel World { get; }
            public GrayboxProductionRuntime3D Production { get; }
            public FormalProductionSimulation Simulation { get; }
            public GrayboxDefenseRuntime3D Defense { get; }
            public GrayboxDefenseHudView3D Hud { get; }
            public GrayboxEvacuationController3D Evacuation { get; }
            public GrayboxBuildingMenuView3D Menu { get; }
            public GrayboxBuildingInputRouter3D Input { get; }
            public Canvas Canvas { get; }
            public EvacuationManifestViewModel Manifest { get; }
            public int UiObjectCount =>
                Canvas.GetComponentsInChildren<Transform>(true).Length;

            public void TickStableAdapters()
            {
                Input.ProcessCurrentInput();
                Evacuation.Tick(0f, paused: false);
                Menu.ShowEvacuationManifest(
                    Evacuation.CaptureManifestView());
            }
        }

        private sealed class FortressDeploymentRequest :
            IGrayboxDeploymentRequest3D
        {
            public CityMode Mode => CityMode.Fortress;

            public bool TryToggleDeployment(out string failureReason)
            {
                failureReason = string.Empty;
                return true;
            }
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
