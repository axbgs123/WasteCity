using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionClockTests
    {
        [Test]
        public void FixedStepWaitsForOneTenthSecondBeforeSynchronizingAndAdvancing()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.000001",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 2);
            var clock = new GrayboxProductionClock3D();

            Tick(clock, .09f, false, new[] { smelter }, city);
            Assert.That(clock.Runtime.States, Is.Empty);
            Assert.That(clock.AccumulatorSeconds, Is.EqualTo(.09f).Within(.0001f));

            Tick(clock, .01f, false, new[] { smelter }, city);
            Assert.That(clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.ProgressSeconds, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(city.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(clock.AccumulatorSeconds, Is.Zero.Within(.0001f));
        }

        [Test]
        public void CombinedAndSplitDeltaProduceTheSameFixedStepSnapshot()
        {
            GrayboxBuildingInstance3D combinedInstance = CompletedInstance(
                "building.instance.000001",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D splitInstance = CompletedInstance(
                "building.instance.000001",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            var combinedCity = new ResourceInventory(1000);
            var splitCity = new ResourceInventory(1000);
            combinedCity.Add(ResourceIds.Iron, 20);
            splitCity.Add(ResourceIds.Iron, 20);
            var combined = new GrayboxProductionClock3D();
            var split = new GrayboxProductionClock3D();

            Tick(combined, .3f, false, new[] { combinedInstance }, combinedCity);
            Tick(split, .1f, false, new[] { splitInstance }, splitCity);
            Tick(split, .1f, false, new[] { splitInstance }, splitCity);
            Tick(split, .1f, false, new[] { splitInstance }, splitCity);

            combined.Runtime.TryGetState(
                combinedInstance.StableInstanceId,
                out BuildingProductionState combinedState);
            split.Runtime.TryGetState(
                splitInstance.StableInstanceId,
                out BuildingProductionState splitState);
            Assert.That(combinedState.ProgressSeconds,
                Is.EqualTo(splitState.ProgressSeconds).Within(.0001f));
            Assert.That(combinedState.Input.Get(ResourceIds.Iron),
                Is.EqualTo(splitState.Input.Get(ResourceIds.Iron)));
            Assert.That(combinedState.Output.Get(ResourceIds.Alloy),
                Is.EqualTo(splitState.Output.Get(ResourceIds.Alloy)));
            Assert.That(combinedCity.Get(ResourceIds.Iron),
                Is.EqualTo(splitCity.Get(ResourceIds.Iron)));
            Assert.That(combined.AccumulatorSeconds,
                Is.EqualTo(split.AccumulatorSeconds).Within(.0001f));
        }

        [Test]
        public void GlobalPauseDoesNotAccumulateOrOverwriteBuildingStopReason()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.000001",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                40,
                40);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 2);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, .1f, false, new[] { smelter }, city);
            clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutOfLogistics));

            Tick(clock, 30f, true, new[] { smelter }, city);

            Assert.That(clock.AccumulatorSeconds, Is.Zero.Within(.0001f));
            Assert.That(state.ProgressSeconds, Is.Zero);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutOfLogistics));
        }

        [Test]
        public void ActiveWarehouseCountExpandsTheCityCapacityUsedByLogistics()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.000001",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D warehouse = CompletedInstance(
                "building.instance.000002",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                40,
                40);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Alloy, 150);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, .1f, false, new[] { smelter, warehouse }, city);
            clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state);
            state.Output.Add(ResourceIds.Alloy, 1);

            Tick(clock, .1f, false, new[] { smelter, warehouse }, city);

            Assert.That(clock.Runtime.ActiveWarehouseCount, Is.EqualTo(1));
            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(151));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
        }

        [Test]
        public void MobileGroundProductionConsumesOnlyItsLocalCache()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.000003",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            var city = new ResourceInventory(1000);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, .1f, false, new[] { smelter }, city);
            Assert.That(clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.Input.Add(ResourceIds.Iron, 2), Is.EqualTo(2));
            city.Add(ResourceIds.Iron, 100);

            Tick(
                clock,
                6.1f,
                false,
                new[] { smelter },
                city,
                CityMode.Mobile);

            Assert.That(state.IsLogisticsConnected, Is.False);
            Assert.That(clock.Runtime.RunnableStates, Does.Contain(state));
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutOfLogistics));
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(100));
            Assert.That(city.Get(ResourceIds.Alloy), Is.Zero);
        }

        private static void Tick(
            GrayboxProductionClock3D clock,
            float deltaSeconds,
            bool paused,
            GrayboxBuildingInstance3D[] instances,
            ResourceInventory city,
            CityMode cityMode = CityMode.Fortress)
        {
            clock.Tick(
                deltaSeconds,
                paused,
                instances,
                cityMode,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius,
                world: null,
                cityInventory: city);
        }

        private static GrayboxBuildingInstance3D CompletedInstance(
            string stableId,
            BuildingDefinition definition,
            BuildingSite site,
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
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableId,
                    new PlacedBuilding(
                        definition,
                        x,
                        y,
                        site,
                        BuildingOrientation.North),
                    new ConstructionProgress(definition.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            typeof(GrayboxBuildingInstance3D).GetMethod(
                    "Complete",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(instance, null);
            return instance;
        }
    }
}
