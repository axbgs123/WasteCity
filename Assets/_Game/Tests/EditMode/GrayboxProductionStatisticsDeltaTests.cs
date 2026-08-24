using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionStatisticsDeltaTests
    {
        [Test]
        public void SuccessfulCyclesPublishCompletedActiveAndEligibleDeltas()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.smelter",
                BuildingCatalog.Smelter,
                10,
                10,
                ResourceNodeBinding.None);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 4);
            var clock = new GrayboxProductionClock3D();

            Tick(clock, 6.1f, false, new[] { smelter }, city);

            ProductionStatisticsDelta delta = clock.LastStatisticsDelta;
            Assert.That(delta.CompletedProductionBatchCount, Is.EqualTo(1));
            Assert.That(delta.ProductionActiveProgressSeconds,
                Is.EqualTo(6.1f).Within(.0001f));
            Assert.That(delta.ProductionEligibleSeconds,
                Is.EqualTo(6.1f).Within(.0001f));
            Assert.That(delta.IsEmpty, Is.False);
            Assert.That(clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.CompletionRevision, Is.EqualTo(1));
            Assert.That(state.ProgressSeconds, Is.EqualTo(.1f).Within(.0001f));
        }

        [Test]
        public void BlockedReasonsRemainEligibleWithoutReportingActiveTime()
        {
            AssertBlockedTick(
                x: 10,
                cityIron: 0,
                expected: ProductionStopReason.MissingInput);
            AssertBlockedTick(
                x: 40,
                cityIron: 2,
                expected: ProductionStopReason.OutOfLogistics);

            GrayboxBuildingInstance3D fullSmelter = CompletedInstance(
                "building.instance.full",
                BuildingCatalog.Smelter,
                10,
                10,
                ResourceNodeBinding.None);
            var fullClock = new GrayboxProductionClock3D();
            var fullCity = new ResourceInventory(1000);
            Tick(fullClock, .1f, false, new[] { fullSmelter }, fullCity);
            fullClock.Runtime.TryGetState(
                fullSmelter.StableInstanceId,
                out BuildingProductionState fullState);
            fullState.Output.Add(
                ResourceIds.Alloy,
                fullState.Output.CapacityPerResource);
            fullState.Input.Add(ResourceIds.Iron, 2);

            Tick(
                fullClock,
                .1f,
                false,
                new[] { fullSmelter },
                fullCity,
                CityMode.Mobile);

            Assert.That(fullState.StopReason,
                Is.EqualTo(ProductionStopReason.OutputFull));
            AssertEligibleButInactive(fullClock.LastStatisticsDelta);

            var depletedWorld = new WorldMapModel(new[,]
            {
                {
                    new WorldCell(
                        TerrainKind.Rocky,
                        ResourceIds.Iron,
                        0)
                }
            });
            GrayboxBuildingInstance3D mine = CompletedInstance(
                "building.instance.mine",
                BuildingCatalog.MiningStation,
                0,
                0,
                new ResourceNodeBinding("world.resource-node.0.0", 0, 0));
            var mineClock = new GrayboxProductionClock3D();

            Tick(
                mineClock,
                .1f,
                false,
                new[] { mine },
                new ResourceInventory(1000),
                CityMode.Fortress,
                depletedWorld);

            mineClock.Runtime.TryGetState(
                mine.StableInstanceId,
                out BuildingProductionState mineState);
            Assert.That(mineState.StopReason,
                Is.EqualTo(ProductionStopReason.Depleted));
            AssertEligibleButInactive(mineClock.LastStatisticsDelta);
        }

        [Test]
        public void PlayerPauseGlobalPauseAndZeroTickPublishNoTime()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.paused",
                BuildingCatalog.Smelter,
                10,
                10,
                ResourceNodeBinding.None);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 4);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, .1f, false, new[] { smelter }, city);
            clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state);

            state.SetPlayerPaused(true);
            Tick(clock, .1f, false, new[] { smelter }, city);
            Assert.That(clock.LastStatisticsDelta.IsEmpty, Is.True);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.PlayerPaused));

            state.SetPlayerPaused(false);
            Tick(clock, .1f, true, new[] { smelter }, city);
            Assert.That(clock.LastStatisticsDelta.IsEmpty, Is.True);

            Tick(clock, 0f, false, new[] { smelter }, city);
            Assert.That(clock.LastStatisticsDelta.IsEmpty, Is.True);
        }

        [Test]
        public void CombinedAndSplitTicksPublishTheSameStableTotals()
        {
            GrayboxBuildingInstance3D combinedSmelter = CompletedInstance(
                "building.instance.stable",
                BuildingCatalog.Smelter,
                10,
                10,
                ResourceNodeBinding.None);
            GrayboxBuildingInstance3D splitSmelter = CompletedInstance(
                "building.instance.stable",
                BuildingCatalog.Smelter,
                10,
                10,
                ResourceNodeBinding.None);
            var combinedCity = new ResourceInventory(1000);
            var splitCity = new ResourceInventory(1000);
            combinedCity.Add(ResourceIds.Iron, 4);
            splitCity.Add(ResourceIds.Iron, 4);
            var combinedClock = new GrayboxProductionClock3D();
            var splitClock = new GrayboxProductionClock3D();

            Tick(
                combinedClock,
                6.1f,
                false,
                new[] { combinedSmelter },
                combinedCity);
            int splitCompleted = 0;
            float splitActive = 0f;
            float splitEligible = 0f;
            for (var index = 0; index < 61; index++)
            {
                Tick(
                    splitClock,
                    .1f,
                    false,
                    new[] { splitSmelter },
                    splitCity);
                splitCompleted += splitClock.LastStatisticsDelta
                    .CompletedProductionBatchCount;
                splitActive += splitClock.LastStatisticsDelta
                    .ProductionActiveProgressSeconds;
                splitEligible += splitClock.LastStatisticsDelta
                    .ProductionEligibleSeconds;
            }

            Assert.That(splitCompleted, Is.EqualTo(combinedClock
                .LastStatisticsDelta.CompletedProductionBatchCount));
            Assert.That(splitActive, Is.EqualTo(combinedClock
                .LastStatisticsDelta.ProductionActiveProgressSeconds)
                .Within(.0001f));
            Assert.That(splitEligible, Is.EqualTo(combinedClock
                .LastStatisticsDelta.ProductionEligibleSeconds)
                .Within(.0001f));
        }

        [Test]
        public void StorageBackedTickPublishesTheSameEligibleContract()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.storage",
                BuildingCatalog.Smelter,
                10,
                10,
                ResourceNodeBinding.None);
            var inventory = new ResourceInventory(1000);
            using var storage = new CityResourceStorageModel(inventory, 150);
            var clock = new GrayboxProductionClock3D();

            clock.Tick(
                .1f,
                false,
                new[] { smelter },
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius,
                world: null,
                cityStorage: storage);

            AssertEligibleButInactive(clock.LastStatisticsDelta);
        }

        [Test]
        public void StatisticsRevisionAdvancesOncePerPublishedNonEmptyDelta()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.revision",
                BuildingCatalog.Smelter,
                10,
                10,
                ResourceNodeBinding.None);
            var city = new ResourceInventory(1000);
            var clock = new GrayboxProductionClock3D();

            Assert.That(clock.StatisticsRevision, Is.Zero);
            Tick(clock, .1f, false, new[] { smelter }, city);
            Assert.That(clock.LastStatisticsDelta.IsEmpty, Is.False);
            Assert.That(clock.StatisticsRevision, Is.EqualTo(1ul));

            Tick(clock, 0f, false, new[] { smelter }, city);
            Assert.That(clock.LastStatisticsDelta.IsEmpty, Is.True);
            Assert.That(clock.StatisticsRevision, Is.EqualTo(1ul));

            Tick(clock, .1f, false, new[] { smelter }, city);
            Assert.That(clock.StatisticsRevision, Is.EqualTo(2ul));
        }

        private static void AssertBlockedTick(
            int x,
            int cityIron,
            ProductionStopReason expected)
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance." + expected,
                BuildingCatalog.Smelter,
                x,
                10,
                ResourceNodeBinding.None);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, cityIron);
            var clock = new GrayboxProductionClock3D();

            Tick(clock, .1f, false, new[] { smelter }, city);

            clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state);
            Assert.That(state.StopReason, Is.EqualTo(expected));
            AssertEligibleButInactive(clock.LastStatisticsDelta);
        }

        private static void AssertEligibleButInactive(
            ProductionStatisticsDelta delta)
        {
            Assert.That(delta.CompletedProductionBatchCount, Is.Zero);
            Assert.That(delta.ProductionActiveProgressSeconds,
                Is.Zero.Within(.0001f));
            Assert.That(delta.ProductionEligibleSeconds,
                Is.EqualTo(.1f).Within(.0001f));
        }

        private static void Tick(
            GrayboxProductionClock3D clock,
            float deltaSeconds,
            bool paused,
            GrayboxBuildingInstance3D[] instances,
            ResourceInventory city,
            CityMode cityMode = CityMode.Fortress,
            WorldMapModel world = null)
        {
            clock.Tick(
                deltaSeconds,
                paused,
                instances,
                cityMode,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius,
                world: world,
                cityInventory: city);
        }

        private static GrayboxBuildingInstance3D CompletedInstance(
            string stableId,
            BuildingDefinition definition,
            int x,
            int y,
            ResourceNodeBinding binding)
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
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableId,
                    new PlacedBuilding(
                        definition,
                        x,
                        y,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(definition.BuildSeconds),
                    binding,
                });
            typeof(GrayboxBuildingInstance3D).GetMethod(
                    "Complete",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);
            return instance;
        }
    }
}
