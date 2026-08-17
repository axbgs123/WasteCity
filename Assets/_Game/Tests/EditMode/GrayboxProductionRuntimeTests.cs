using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionRuntimeTests
    {
        [Test]
        public void SynchronizeCreatesFormalStateOnlyAfterConstructionCompletes()
        {
            GrayboxBuildingInstance3D instance = CreateInstance(
                "building.instance.000001",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            var runtime = new GrayboxProductionRuntime3D();

            runtime.Synchronize(
                new[] { instance },
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.States, Is.Empty);
            Assert.That(runtime.RunnableStates, Is.Empty);
            Assert.That(runtime.TryGetState(instance.StableInstanceId, out _), Is.False);

            Complete(instance);
            runtime.Synchronize(
                new[] { instance },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.TryGetState(
                instance.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.Definition,
                Is.SameAs(FormalProductionDefinitionCatalog.Smelting));
            Assert.That(runtime.States, Is.EqualTo(new[] { state }));
            Assert.That(runtime.RunnableStates, Is.EqualTo(new[] { state }));
        }

        [Test]
        public void MiningStateUsesTheExactResourceNodeBindingOwnedByTheInstance()
        {
            var binding = new ResourceNodeBinding(
                "world.resource-node.7.9",
                7,
                9);
            GrayboxBuildingInstance3D mine = CreateInstance(
                "building.instance.000002",
                BuildingCatalog.MiningStation,
                BuildingSite.Ground,
                7,
                9,
                binding);
            Complete(mine);
            var runtime = new GrayboxProductionRuntime3D();

            runtime.Synchronize(
                new[] { mine },
                CityMode.Fortress,
                cityX: 8,
                cityY: 8,
                groundRadius: BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.TryGetState(
                mine.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.Definition,
                Is.SameAs(FormalProductionDefinitionCatalog.Extraction));
            Assert.That(state.BoundResourceNodeId, Is.EqualTo(binding.StableId));
            Assert.That(state.BoundNodeX, Is.EqualTo(binding.X));
            Assert.That(state.BoundNodeY, Is.EqualTo(binding.Y));
        }

        [Test]
        public void GroundLogisticsRequiresFortressAndEveryFootprintCellInRange()
        {
            GrayboxBuildingInstance3D inside = CreateInstance(
                "building.instance.000003",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                17,
                10);
            GrayboxBuildingInstance3D outside = CreateInstance(
                "building.instance.000004",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                18,
                10);
            Complete(inside);
            Complete(outside);
            var runtime = new GrayboxProductionRuntime3D();
            var instances = new[] { outside, inside };

            runtime.Synchronize(
                instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.TryGetState(inside.StableInstanceId, out BuildingProductionState insideState), Is.True);
            Assert.That(runtime.TryGetState(outside.StableInstanceId, out BuildingProductionState outsideState), Is.True);
            Assert.That(insideState.IsLogisticsConnected, Is.True);
            Assert.That(outsideState.IsLogisticsConnected, Is.False);
            Assert.That(runtime.RunnableStates, Does.Contain(insideState));
            Assert.That(runtime.RunnableStates, Does.Contain(outsideState));

            runtime.Synchronize(
                instances,
                CityMode.Mobile,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(insideState.IsLogisticsConnected, Is.False);
            Assert.That(outsideState.IsLogisticsConnected, Is.False);
            Assert.That(runtime.RunnableStates, Is.Empty);
        }

        [Test]
        public void MobileAllowedInnerCityProductionRemainsConnectedAndRunnableWhileMobile()
        {
            GrayboxBuildingInstance3D assembler = CreateInstance(
                "building.instance.000005",
                BuildingCatalog.Assembler,
                BuildingSite.InnerCity,
                1,
                1);
            Complete(assembler);
            var runtime = new GrayboxProductionRuntime3D();

            runtime.Synchronize(
                new[] { assembler },
                CityMode.Mobile,
                cityX: 40,
                cityY: 30,
                groundRadius: BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.TryGetState(
                assembler.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.IsLogisticsConnected, Is.True);
            Assert.That(runtime.RunnableStates, Is.EqualTo(new[] { state }));
        }

        [Test]
        public void EvacuationLockTemporarilyRemovesRunnableStateWithoutReplacingItsCachesOrProgress()
        {
            GrayboxBuildingInstance3D smelter = CreateInstance(
                "building.instance.000006",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            Complete(smelter);
            var runtime = new GrayboxProductionRuntime3D();
            runtime.Synchronize(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState before), Is.True);
            before.Input.Add(ResourceIds.Iron, 2);
            new FormalProductionSimulation().Tick(
                new[] { before },
                1f,
                null,
                new ResourceInventory(1000),
                new ResourceCapacityPolicy(),
                0,
                globallyPaused: false);
            before.Output.Add(ResourceIds.Alloy, 3);
            Assert.That(before.ProgressSeconds, Is.EqualTo(1f));

            SetEvacuationLocked(smelter, true);
            runtime.Synchronize(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.TryGetState(smelter.StableInstanceId, out BuildingProductionState locked), Is.True);
            Assert.That(locked, Is.SameAs(before));
            Assert.That(runtime.States, Is.EqualTo(new[] { before }));
            Assert.That(runtime.RunnableStates, Is.Empty);
            Assert.That(locked.ProgressSeconds, Is.EqualTo(1f));
            Assert.That(locked.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(locked.Output.Get(ResourceIds.Alloy), Is.EqualTo(3));

            SetEvacuationLocked(smelter, false);
            runtime.Synchronize(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.TryGetState(smelter.StableInstanceId, out BuildingProductionState restored), Is.True);
            Assert.That(restored, Is.SameAs(before));
            Assert.That(runtime.RunnableStates, Is.EqualTo(new[] { before }));
            Assert.That(restored.ProgressSeconds, Is.EqualTo(1f));
            Assert.That(restored.Output.Get(ResourceIds.Alloy), Is.EqualTo(3));
        }

        [Test]
        public void AbandonedOrRemovedInstancesDiscardTheirProductionState()
        {
            GrayboxBuildingInstance3D abandoned = CreateInstance(
                "building.instance.000007",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D removed = CreateInstance(
                "building.instance.000008",
                BuildingCatalog.Assembler,
                BuildingSite.InnerCity,
                1,
                1);
            Complete(abandoned);
            Complete(removed);
            var runtime = new GrayboxProductionRuntime3D();
            runtime.Synchronize(
                new[] { abandoned, removed },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.States, Has.Count.EqualTo(2));

            Abandon(abandoned);
            runtime.Synchronize(
                new[] { abandoned },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.States, Is.Empty);
            Assert.That(runtime.TryGetState(abandoned.StableInstanceId, out _), Is.False);
            Assert.That(runtime.TryGetState(removed.StableInstanceId, out _), Is.False);
        }

        [Test]
        public void WarehouseCountUsesCompletedOwnedUnlockedEligibilityRegardlessOfLogisticsRange()
        {
            GrayboxBuildingInstance3D inside = CreateInstance(
                "building.instance.000009",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D outside = CreateInstance(
                "building.instance.000010",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                40,
                40);
            GrayboxBuildingInstance3D unfinished = CreateInstance(
                "building.instance.000011",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                12,
                12);
            GrayboxBuildingInstance3D locked = CreateInstance(
                "building.instance.000012",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                14,
                14);
            GrayboxBuildingInstance3D abandoned = CreateInstance(
                "building.instance.000013",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                16,
                16);
            Complete(inside);
            Complete(outside);
            Complete(locked);
            Complete(abandoned);
            SetEvacuationLocked(locked, true);
            Abandon(abandoned);
            var runtime = new GrayboxProductionRuntime3D();
            var instances = new[] { unfinished, locked, abandoned, outside, inside };

            runtime.Synchronize(
                instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.ActiveWarehouseCount, Is.EqualTo(2));

            runtime.Synchronize(
                instances,
                CityMode.Mobile,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.ActiveWarehouseCount, Is.EqualTo(2));
        }

        private static GrayboxBuildingInstance3D CreateInstance(
            string stableInstanceId,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            ResourceNodeBinding binding = default(ResourceNodeBinding))
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
            return (GrayboxBuildingInstance3D)constructor.Invoke(new object[]
            {
                stableInstanceId,
                new PlacedBuilding(
                    definition,
                    x,
                    y,
                    site,
                    BuildingOrientation.North),
                new ConstructionProgress(definition.BuildSeconds),
                binding,
            });
        }

        private static void Complete(GrayboxBuildingInstance3D instance)
        {
            Invoke(instance, "Complete");
        }

        private static void SetEvacuationLocked(
            GrayboxBuildingInstance3D instance,
            bool locked)
        {
            Invoke(instance, "SetEvacuationLocked", locked);
        }

        private static void Abandon(GrayboxBuildingInstance3D instance)
        {
            Invoke(instance, "Abandon");
        }

        private static void Invoke(
            GrayboxBuildingInstance3D instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = typeof(GrayboxBuildingInstance3D).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(instance, arguments);
        }
    }
}
