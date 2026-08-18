using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FormalProductionSimulationTests
    {
        [Test]
        public void FormalCatalogContainsTheExactThreeStableMachineDefinitions()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "core.production.extract-node-resource",
                    "core.production.smelt-alloy",
                    "core.production.assemble-ammunition",
                },
                FormalProductionDefinitionCatalog.All
                    .Select(definition => definition.Id)
                    .ToArray());

            AssertDefinition(
                FormalProductionDefinitionCatalog.Extraction,
                BuildingCatalog.MiningStation.Id.Value,
                durationSeconds: 3f,
                inputResourceId: null,
                inputAmount: 0,
                outputResourceId: null,
                outputAmount: 1,
                inputCapacity: 0,
                outputCapacity: 20,
                usesBoundResourceNode: true);
            AssertDefinition(
                FormalProductionDefinitionCatalog.Smelting,
                BuildingCatalog.Smelter.Id.Value,
                durationSeconds: 6f,
                inputResourceId: ResourceIds.Iron,
                inputAmount: 2,
                outputResourceId: ResourceIds.Alloy,
                outputAmount: 1,
                inputCapacity: 20,
                outputCapacity: 10,
                usesBoundResourceNode: false);
            AssertDefinition(
                FormalProductionDefinitionCatalog.Assembly,
                BuildingCatalog.Assembler.Id.Value,
                durationSeconds: 6f,
                inputResourceId: ResourceIds.Alloy,
                inputAmount: 2,
                outputResourceId: ResourceIds.Ammunition,
                outputAmount: 2,
                inputCapacity: 20,
                outputCapacity: 30,
                usesBoundResourceNode: false);
        }

        [Test]
        public void SmeltingReservesTwoIronAtomicallyAtCycleStartAndOutputsAfterSixSeconds()
        {
            BuildingProductionState state = Smelter("smelter.001", connected: false);
            state.Input.Add(ResourceIds.Iron, 2);
            var simulation = new FormalProductionSimulation();

            Tick(simulation, state, .1f);

            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(state.ProgressSeconds, Is.EqualTo(.1f).Within(.0001f));

            Tick(simulation, state, 5.9f);

            Assert.That(state.HasReservedInputs, Is.False);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
        }

        [TestCase(true, ProductionStopReason.OutOfLogistics)]
        [TestCase(false, ProductionStopReason.MissingInput)]
        public void DisconnectedCompletedCycleDistinguishesReachableCityInputFromTrueShortage(
            bool cityHasIron,
            ProductionStopReason expectedReason)
        {
            BuildingProductionState state = Smelter("smelter.001", connected: false);
            state.Input.Add(ResourceIds.Iron, 2);
            var city = new ResourceInventory(1000);
            if (cityHasIron) city.Add(ResourceIds.Iron, 2);

            Tick(new FormalProductionSimulation(), state, 6.1f, city);

            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.StopReason, Is.EqualTo(expectedReason));
        }

        [Test]
        public void DisconnectedMachineReportsOutOfLogisticsWhenCityCouldFillItsLocalShortfall()
        {
            BuildingProductionState state = Smelter("smelter.001", connected: false);
            state.Input.Add(ResourceIds.Iron, 1);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 1);

            Tick(new FormalProductionSimulation(), state, 0f, city);

            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutOfLogistics));
        }

        [Test]
        public void FullOutputCacheDoesNotReserveOrConsumeTheNextInputBatch()
        {
            BuildingProductionState state = Smelter("smelter.001", connected: false);
            state.Input.Add(ResourceIds.Iron, 2);
            state.Output.Add(ResourceIds.Alloy, 10);

            Tick(new FormalProductionSimulation(), state, 6f);

            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(10));
            Assert.That(state.HasReservedInputs, Is.False);
            Assert.That(state.ProgressSeconds, Is.Zero);
            Assert.That(state.StopReason, Is.EqualTo(ProductionStopReason.OutputFull));
        }

        [Test]
        public void PlayerPausePreservesReservedInputsAndExactProgressUntilResume()
        {
            BuildingProductionState state = Smelter("smelter.001", connected: false);
            state.Input.Add(ResourceIds.Iron, 2);
            var simulation = new FormalProductionSimulation();
            Tick(simulation, state, 2f);
            state.SetPlayerPaused(true);

            Tick(simulation, state, 30f);

            Assert.That(state.ProgressSeconds, Is.EqualTo(2f));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(state.StopReason, Is.EqualTo(ProductionStopReason.PlayerPaused));

            state.SetPlayerPaused(false);
            Tick(simulation, state, 4f);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
        }

        [Test]
        public void GlobalPausePreservesStateAndDoesNotOverwriteTheBuildingReason()
        {
            BuildingProductionState state = Smelter("smelter.001", connected: false);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 2);
            var simulation = new FormalProductionSimulation();
            Tick(simulation, state, 0f, city);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutOfLogistics));

            simulation.Tick(
                new[] { state },
                deltaSeconds: 30f,
                world: null,
                cityInventory: city,
                cityCapacity: new ResourceCapacityPolicy(),
                activeWarehouseCount: 0,
                globallyPaused: true);

            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutOfLogistics));
            Assert.That(state.ProgressSeconds, Is.Zero);
            Assert.That(city.Get(ResourceIds.Iron), Is.EqualTo(2));
        }

        [Test]
        public void ExactCycleBoundaryImmediatelyReportsWhyTheNextCycleCannotStart()
        {
            BuildingProductionState state = Smelter("smelter.001", connected: false);
            state.Input.Add(ResourceIds.Iron, 2);

            Tick(new FormalProductionSimulation(), state, 6f);

            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.ProgressSeconds, Is.Zero);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.MissingInput));
        }

        [TestCase(ResourceIds.Iron)]
        [TestCase(ResourceIds.EnergyCrystal)]
        [TestCase(ResourceIds.Stone)]
        public void MiningHarvestsTheBoundNodeOnlyAtCompletionAndProducesItsTrueResource(
            string resourceId)
        {
            WorldMapModel world = SingleNode(resourceId, amount: 2);
            BuildingProductionState state = Mine("mine.001", connected: false);
            var simulation = new FormalProductionSimulation();

            Tick(simulation, state, 2.9f, world: world);
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(2));
            Assert.That(state.Output.Get(resourceId), Is.Zero);

            Tick(simulation, state, .1f, world: world);
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(1));
            Assert.That(state.Output.Get(resourceId), Is.EqualTo(1));
            Assert.That(state.BoundResourceNodeId,
                Is.EqualTo("world.resource-node.0.0"));
        }

        [Test]
        public void DepletedBoundNodeProducesNothingAndReportsDepleted()
        {
            WorldMapModel world = SingleNode(ResourceIds.Iron, amount: 0);
            BuildingProductionState state = Mine("mine.001", connected: false);

            Tick(new FormalProductionSimulation(), state, 30f, world: world);

            Assert.That(world.Get(0, 0).ResourceAmount, Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.StopReason, Is.EqualTo(ProductionStopReason.Depleted));
        }

        [Test]
        public void DepletedMiningNodeTakesPriorityOverFullOutput()
        {
            WorldMapModel world = SingleNode(ResourceIds.Iron, amount: 0);
            BuildingProductionState state = Mine("mine.001", connected: false);
            state.Output.Add(ResourceIds.Iron, 20);

            Tick(new FormalProductionSimulation(), state, .1f, world: world);

            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.Depleted));
        }

        [Test]
        public void MiningCompletionDoesNotHarvestWhenItsReservedOutputSpaceWasFilled()
        {
            WorldMapModel world = SingleNode(ResourceIds.Iron, amount: 2);
            BuildingProductionState state = Mine("mine.001", connected: false);
            state.Output.Add(ResourceIds.Iron, 19);
            var simulation = new FormalProductionSimulation();
            Tick(simulation, state, 2f, world: world);
            state.Output.Add(ResourceIds.Iron, 1);

            Tick(simulation, state, 1f, world: world);

            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(2));
            Assert.That(state.Output.Get(ResourceIds.Iron), Is.EqualTo(20));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.ProgressSeconds, Is.EqualTo(3f));
            Assert.That(state.StopReason, Is.EqualTo(ProductionStopReason.OutputFull));
        }

        [Test]
        public void LogisticsUnloadsOldOutputThenRefillsInputBeforeProductionAndKeepsNewOutputLocal()
        {
            BuildingProductionState state = Smelter("smelter.001", connected: true);
            state.Output.Add(ResourceIds.Alloy, 1);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 2);

            Tick(new FormalProductionSimulation(), state, 6f, city);

            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(city.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
        }

        [Test]
        public void ConnectedMiningUnloadsOldOutputBeforeHarvestingNewOutput()
        {
            WorldMapModel world = SingleNode(ResourceIds.EnergyCrystal, amount: 2);
            BuildingProductionState state = Mine("mine.001", connected: true);
            state.Output.Add(ResourceIds.EnergyCrystal, 1);
            var city = new ResourceInventory(1000);

            Tick(new FormalProductionSimulation(), state, 3f, city, world);

            Assert.That(city.Get(ResourceIds.EnergyCrystal), Is.EqualTo(1));
            Assert.That(state.Output.Get(ResourceIds.EnergyCrystal), Is.EqualTo(1));
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(1));
        }

        [Test]
        public void LogisticsAndProductionUseStableInstanceIdOrderRegardlessOfEnumerationOrder()
        {
            BuildingProductionState later = Smelter("smelter.020", connected: true);
            BuildingProductionState earlier = Smelter("smelter.003", connected: true);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 2);
            var simulation = new FormalProductionSimulation();

            simulation.Tick(
                new List<BuildingProductionState> { later, earlier },
                deltaSeconds: 6f,
                world: null,
                cityInventory: city,
                cityCapacity: new ResourceCapacityPolicy(),
                activeWarehouseCount: 0,
                globallyPaused: false);

            Assert.That(earlier.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(later.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(later.StopReason, Is.EqualTo(ProductionStopReason.MissingInput));
        }

        private static BuildingProductionState Smelter(string stableId, bool connected)
        {
            var state = new BuildingProductionState(
                stableId,
                FormalProductionDefinitionCatalog.Smelting);
            state.SetLogisticsConnected(connected);
            return state;
        }

        private static BuildingProductionState Mine(string stableId, bool connected)
        {
            var state = new BuildingProductionState(
                stableId,
                FormalProductionDefinitionCatalog.Extraction,
                boundResourceNodeId: "world.resource-node.0.0",
                boundNodeX: 0,
                boundNodeY: 0);
            state.SetLogisticsConnected(connected);
            return state;
        }

        private static void Tick(
            FormalProductionSimulation simulation,
            BuildingProductionState state,
            float deltaSeconds,
            ResourceInventory city = null,
            WorldMapModel world = null)
        {
            simulation.Tick(
                new[] { state },
                deltaSeconds,
                world,
                city ?? new ResourceInventory(1000),
                new ResourceCapacityPolicy(),
                activeWarehouseCount: 0,
                globallyPaused: false);
        }

        private static WorldMapModel SingleNode(string resourceId, int amount)
        {
            return new WorldMapModel(new[,]
            {
                { new WorldCell(TerrainKind.Rocky, resourceId, amount) },
            });
        }

        private static void AssertDefinition(
            FormalProductionDefinition definition,
            string buildingId,
            float durationSeconds,
            string inputResourceId,
            int inputAmount,
            string outputResourceId,
            int outputAmount,
            int inputCapacity,
            int outputCapacity,
            bool usesBoundResourceNode)
        {
            Assert.That(definition.BuildingId, Is.EqualTo(buildingId));
            Assert.That(definition.DurationSeconds, Is.EqualTo(durationSeconds));
            Assert.That(definition.InputResourceId, Is.EqualTo(inputResourceId));
            Assert.That(definition.InputAmount, Is.EqualTo(inputAmount));
            Assert.That(definition.OutputResourceId, Is.EqualTo(outputResourceId));
            Assert.That(definition.OutputAmount, Is.EqualTo(outputAmount));
            Assert.That(definition.InputCapacity, Is.EqualTo(inputCapacity));
            Assert.That(definition.OutputCapacity, Is.EqualTo(outputCapacity));
            Assert.That(definition.UsesBoundResourceNode,
                Is.EqualTo(usesBoundResourceNode));
        }
    }
}
