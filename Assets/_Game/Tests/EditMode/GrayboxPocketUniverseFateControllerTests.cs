using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxPocketUniverseFateControllerTests
    {
        private const string ControllerTypeName =
            "WasteCity.Graybox3D.Building." +
            "GrayboxPocketUniverseFateController3D";

        [Test]
        public void IDEA0020_BindIsIdempotentSelectsExistingAndAddsLaterType()
        {
            using (Fixture fixture = new Fixture(
                       selectedFateId: FormalFateCatalog.PocketUniverseId,
                       includeAssemblerUnderConstruction: true))
            {
                object controller = fixture.CreateController();
                Bind(controller, fixture.Session, fixture.Clock);
                PocketUniverseFateSnapshot initial = fixture.Effect.Capture();
                Assert.That(initial.Flagships.Count, Is.EqualTo(1));

                Bind(controller, fixture.Session, fixture.Clock);
                Assert.That(fixture.Effect.Capture(), Is.SameAs(initial));
                fixture.Session.CompleteAllConstructionForDevelopment(
                    fixture.Presentation);
                Assert.That(fixture.Effect.Capture().Flagships.Count,
                    Is.EqualTo(2));
                Assert.That(fixture.Effect.Level, Is.EqualTo(1));
                Dispose(controller);
            }
        }

        [Test]
        public void IDEA0020_FirstRealFlagshipBatchAddsAttentionOnceWithStableKey()
        {
            using (Fixture fixture = new Fixture(
                       FormalFateCatalog.PocketUniverseId))
            {
                object controller = fixture.CreateController();
                Bind(controller, fixture.Session, fixture.Clock);
                fixture.Tick(6.1f);

                Assert.That(fixture.Attention.Value, Is.EqualTo(14));
                FormalAttentionHistoryEntry latest =
                    fixture.Attention.Capture().History.Last();
                Assert.That(latest.ReasonId, Is.EqualTo(
                    "core.attention.fate.pocket-universe-activated"));
                Assert.That(latest.StableEventKey, Is.EqualTo(
                    "pocket-universe-first-production:" +
                    fixture.SmelterStableId));

                fixture.Tick(6.1f);
                Assert.That(fixture.Attention.Value, Is.EqualTo(14));
                Dispose(controller);
            }
        }

        [Test]
        public void IDEA0020_AttentionFailureRollsBackFirstProductionFact()
        {
            using (Fixture fixture = new Fixture(
                       FormalFateCatalog.PocketUniverseId))
            {
                string eventKey = "pocket-universe-first-production:" +
                    fixture.SmelterStableId;
                Assert.That(fixture.Attention.TryApply(
                    "core.attention.fate.void-debt-periodic",
                    eventKey,
                    out _), Is.True);
                object controller = fixture.CreateController();
                Bind(controller, fixture.Session, fixture.Clock);

                fixture.Tick(6.1f);

                Assert.That(fixture.Attention.Value, Is.EqualTo(11));
                Assert.That(fixture.Effect.Capture().FirstProductionFlagshipId,
                    Is.Empty);
                Dispose(controller);
            }
        }

        [Test]
        public void IDEA0020_NonPocketAndDisposedControllerDoNotModifyProduction()
        {
            using (Fixture fixture = new Fixture(
                       FormalFateCatalog.VoidDebtId))
            {
                object controller = fixture.CreateController();
                Bind(controller, fixture.Session, fixture.Clock);
                fixture.Tick(6.1f);
                fixture.Clock.Runtime.TryGetState(
                    fixture.SmelterStableId,
                    out BuildingProductionState state);
                Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
                Assert.That(fixture.Attention.Value, Is.EqualTo(10));
                Assert.That(fixture.Effect.Capture().Flagships, Is.Empty);
                Dispose(controller);
            }

            using (Fixture fixture = new Fixture(
                       FormalFateCatalog.PocketUniverseId))
            {
                object controller = fixture.CreateController();
                Bind(controller, fixture.Session, fixture.Clock);
                Dispose(controller);
                fixture.Tick(6.1f);
                fixture.Clock.Runtime.TryGetState(
                    fixture.SmelterStableId,
                    out BuildingProductionState state);
                Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
                Assert.That(fixture.Attention.Value, Is.EqualTo(10));
            }
        }

        private static object Create(
            FormalFateRuntime fate,
            FormalAttentionRuntime attention,
            PocketUniverseFateEffect effect)
        {
            Type type = RequireType();
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(FormalFateRuntime),
                typeof(FormalAttentionRuntime),
                typeof(PocketUniverseFateEffect),
            });
            Assert.That(constructor, Is.Not.Null);
            return constructor.Invoke(new object[] { fate, attention, effect });
        }

        private static void Bind(
            object controller,
            GrayboxBuildingSession3D session,
            GrayboxProductionClock3D clock)
        {
            MethodInfo method = controller.GetType().GetMethod(
                "Bind",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(GrayboxBuildingSession3D),
                    typeof(GrayboxProductionClock3D),
                },
                null);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { session, clock });
        }

        private static void Dispose(object controller)
        {
            Assert.That(controller, Is.InstanceOf<IDisposable>());
            ((IDisposable)controller).Dispose();
        }

        private static Type RequireType()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(value => value.GetType(ControllerTypeName, false))
                .FirstOrDefault(value => value != null);
            Assert.That(type, Is.Not.Null, ControllerTypeName);
            return type;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root;
            public readonly string SmelterStableId =
                "building.instance.000001";

            public Fixture(
                string selectedFateId,
                bool includeAssemblerUnderConstruction = false)
            {
                root = new GameObject("PocketController.Test");
                Session = root.AddComponent<GrayboxBuildingSession3D>();
                Session.Configure(true);
                Session.ConfigureDevelopmentFixture();
                Presentation = new PresentationStub();
                var entries = includeAssemblerUnderConstruction
                    ? new[]
                    {
                        Entry(SmelterStableId, BuildingCatalog.Smelter,
                            10, 10, completed: true),
                        Entry("building.instance.000002",
                            BuildingCatalog.Assembler, 14, 10,
                            completed: false),
                    }
                    : new[]
                    {
                        Entry(SmelterStableId, BuildingCatalog.Smelter,
                            10, 10, completed: true),
                    };
                Assert.That(Session.TryRestoreBuildings(
                    entries,
                    entries.Length + 1,
                    Presentation,
                    out string error), Is.True, error);
                Session.CityStorage.TryCommitBatch(
                    Array.Empty<ResourceAmount>(),
                    new[] { new ResourceAmount(ResourceIds.Iron, 20) });
                Fate = new FormalFateRuntime();
                Assert.That(Fate.TrySelect(
                    selectedFateId,
                    out _,
                    out _,
                    out error), Is.True, error);
                Attention = new FormalAttentionRuntime();
                Effect = new PocketUniverseFateEffect();
                Clock = new GrayboxProductionClock3D();
            }

            public GrayboxBuildingSession3D Session { get; }
            public PresentationStub Presentation { get; }
            public FormalFateRuntime Fate { get; }
            public FormalAttentionRuntime Attention { get; }
            public PocketUniverseFateEffect Effect { get; }
            public GrayboxProductionClock3D Clock { get; }

            public object CreateController() =>
                Create(Fate, Attention, Effect);

            public void Tick(float seconds)
            {
                Clock.Tick(
                    seconds,
                    paused: false,
                    Session.Instances,
                    CityMode.Fortress,
                    10,
                    10,
                    Session.GroundBuildRadius,
                    world: null,
                    Session.CityStorage);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            private static GrayboxBuildingRestoreEntry3D Entry(
                string stableId,
                BuildingDefinition definition,
                int x,
                int y,
                bool completed)
            {
                return new GrayboxBuildingRestoreEntry3D(
                    stableId,
                    definition,
                    BuildingSite.Ground,
                    x,
                    y,
                    BuildingOrientation.North,
                    completed
                        ? GrayboxBuildingInstanceState.Completed
                        : GrayboxBuildingInstanceState.UnderConstruction,
                    completed ? 0f : 1f,
                    isPlayerOwned: true,
                    isEvacuationLocked: false,
                    ResourceNodeBinding.None);
            }
        }

        public sealed class PresentationStub : IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
