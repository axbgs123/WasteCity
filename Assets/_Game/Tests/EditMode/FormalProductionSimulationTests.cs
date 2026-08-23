using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
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

        [Test]
        public void SmeltingOwnsAnImmutableSnapshotOfTheExactReservedInputs()
        {
            BuildingProductionState state = Smelter("smelter.reserved", connected: false);
            state.Input.Add(ResourceIds.Iron, 2);

            Tick(new FormalProductionSimulation(), state, .1f);

            Assert.That(state.ReservedInputs, Has.Count.EqualTo(1));
            Assert.That(state.ReservedInputs[0].ResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(state.ReservedInputs[0].Amount, Is.EqualTo(2));
            Assert.That(state.ReservedInputs, Is.Not.InstanceOf<ResourceAmount[]>());
        }

        [Test]
        public void PersistenceRestoreIsAtomicAndDoesNotRestoreDerivedStopReason()
        {
            BuildingProductionState state = Smelter("smelter.restore", connected: true);
            state.Input.Add(ResourceIds.Iron, 7);
            state.SetPlayerPaused(true);

            Assert.That(state.TryRestoreForPersistence(
                new[] { new ResourceAmount(ResourceIds.Iron, 3) },
                hasReservedInputs: true,
                new[] { new ResourceAmount(ResourceIds.Iron, 2) },
                new[] { new ResourceAmount(ResourceIds.Alloy, 1) },
                progressSeconds: 2f,
                isPlayerPaused: false,
                out string error), Is.True, error);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(3));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.ReservedInputs[0].Amount, Is.EqualTo(2));
            Assert.That(state.ProgressSeconds, Is.EqualTo(2f));
            Assert.That(state.IsPlayerPaused, Is.False);
            Assert.That(state.IsLogisticsConnected, Is.True);
            Assert.That(state.StopReason, Is.EqualTo(ProductionStopReason.None));

            Assert.That(state.TryRestoreForPersistence(
                new[] { new ResourceAmount(ResourceIds.Iron, 20) },
                hasReservedInputs: false,
                new ResourceAmount[0],
                new ResourceAmount[0],
                progressSeconds: 1f,
                isPlayerPaused: false,
                out _), Is.False);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(3));
            Assert.That(state.ProgressSeconds, Is.EqualTo(2f));
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

        [Test]
        public void MultiInputCycleReservesNothingUntilEveryIngredientIsAvailable()
        {
            FormalProductionDefinition definition = MultiResourceDefinition();
            var state = new BuildingProductionState("multi-input.001", definition);
            state.Input.Add(ResourceIds.Iron, 2);
            var simulation = new FormalProductionSimulation();

            Tick(simulation, state, 0f);

            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.Input.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(state.HasReservedInputs, Is.False);
            Assert.That(state.ReservedInputs, Is.Empty);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.MissingInput));

            state.Input.Add(ResourceIds.EnergyCrystal, 1);
            Tick(simulation, state, 1f);

            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.Input.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(state.HasReservedInputs, Is.True);
            CollectionAssert.AreEqual(
                new[] { ResourceIds.Iron, ResourceIds.EnergyCrystal },
                state.ReservedInputs.Select(value => value.ResourceId));
            CollectionAssert.AreEqual(
                new[] { 2, 1 },
                state.ReservedInputs.Select(value => value.Amount));

            Tick(simulation, state, 3f);

            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.Output.Get(ResourceIds.RefinedStone), Is.EqualTo(2));
        }

        [Test]
        public void MultiOutputCapacityBlocksCycleStartWithoutConsumingAnyInput()
        {
            FormalProductionDefinition definition = MultiResourceDefinition();
            var state = new BuildingProductionState("multi-output.start", definition);
            state.Input.Add(ResourceIds.Iron, 2);
            state.Input.Add(ResourceIds.EnergyCrystal, 1);
            state.Output.Add(ResourceIds.RefinedStone, 2);

            Tick(new FormalProductionSimulation(), state, 0f);

            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.Input.Get(ResourceIds.EnergyCrystal), Is.EqualTo(1));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.RefinedStone), Is.EqualTo(2));
            Assert.That(state.HasReservedInputs, Is.False);
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutputFull));
        }

        [Test]
        public void MultiOutputCompletionProducesNothingUntilEveryOutputFits()
        {
            FormalProductionDefinition definition = MultiResourceDefinition();
            var state = new BuildingProductionState("multi-output.finish", definition);
            state.Input.Add(ResourceIds.Iron, 2);
            state.Input.Add(ResourceIds.EnergyCrystal, 1);
            var simulation = new FormalProductionSimulation();
            Tick(simulation, state, 2f);
            state.Output.Add(ResourceIds.RefinedStone, 2);

            Tick(simulation, state, 2f);

            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.RefinedStone), Is.EqualTo(2));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.ProgressSeconds, Is.EqualTo(4f));
            Assert.That(state.StopReason,
                Is.EqualTo(ProductionStopReason.OutputFull));

            Assert.That(state.Output.TrySpend(ResourceIds.RefinedStone, 1),
                Is.True);
            Tick(simulation, state, 0f);

            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.Output.Get(ResourceIds.RefinedStone), Is.EqualTo(3));
            Assert.That(state.HasReservedInputs, Is.False);
        }

        [Test]
        public void ConnectedMultiResourceMachineTransfersEveryInputAndOutputType()
        {
            FormalProductionDefinition definition = MultiResourceDefinition();
            var state = new BuildingProductionState("multi-logistics.001", definition);
            state.SetLogisticsConnected(true);
            state.Output.Add(ResourceIds.Alloy, 1);
            state.Output.Add(ResourceIds.RefinedStone, 2);
            var city = new ResourceInventory(1000);
            city.Add(ResourceIds.Iron, 2);
            city.Add(ResourceIds.EnergyCrystal, 1);

            Tick(new FormalProductionSimulation(), state, 0f, city);

            Assert.That(city.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(city.Get(ResourceIds.RefinedStone), Is.EqualTo(2));
            Assert.That(city.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(city.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(state.Output.Get(ResourceIds.RefinedStone), Is.Zero);
            Assert.That(state.HasReservedInputs, Is.True);
        }

        [Test]
        public void AuthoritativeCityStorageTransfersEveryMultiResourceType()
        {
            FormalProductionDefinition definition = MultiResourceDefinition();
            var state = new BuildingProductionState("multi-storage.001", definition);
            state.SetLogisticsConnected(true);
            state.Output.Add(ResourceIds.Alloy, 1);
            state.Output.Add(ResourceIds.RefinedStone, 2);
            var core = new ResourceInventory(1000);
            core.Add(ResourceIds.Iron, 2);
            core.Add(ResourceIds.EnergyCrystal, 1);
            using (var cityStorage = new CityResourceStorageModel(core, 150))
            {
                new FormalProductionSimulation().Tick(
                    new[] { state },
                    0f,
                    world: null,
                    cityStorage: cityStorage,
                    globallyPaused: false);

                Assert.That(cityStorage.GetNetworkAmount(ResourceIds.Alloy),
                    Is.EqualTo(1));
                Assert.That(cityStorage.GetNetworkAmount(ResourceIds.RefinedStone),
                    Is.EqualTo(2));
                Assert.That(cityStorage.GetNetworkAmount(ResourceIds.Iron),
                    Is.Zero);
                Assert.That(cityStorage.GetNetworkAmount(ResourceIds.EnergyCrystal),
                    Is.Zero);
                Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
                Assert.That(state.Output.Get(ResourceIds.RefinedStone), Is.Zero);
                Assert.That(state.HasReservedInputs, Is.True);
            }
        }

        [Test]
        public void MultiResourceRestoreRejectsWrongDuplicateAndWrongAmountReservations()
        {
            IReadOnlyList<ResourceAmount>[] invalidReservations =
            {
                new[]
                {
                    new ResourceAmount(ResourceIds.Iron, 2),
                    new ResourceAmount(ResourceIds.Stone, 1),
                },
                new[]
                {
                    new ResourceAmount(ResourceIds.Iron, 2),
                    new ResourceAmount(ResourceIds.Iron, 1),
                },
                new[]
                {
                    new ResourceAmount(ResourceIds.Iron, 2),
                    new ResourceAmount(ResourceIds.EnergyCrystal, 2),
                },
            };

            for (int index = 0; index < invalidReservations.Length; index++)
            {
                var state = new BuildingProductionState(
                    "multi-restore.invalid." + index,
                    MultiResourceDefinition());

                Assert.That(state.TryRestoreForPersistence(
                    Array.Empty<ResourceAmount>(),
                    hasReservedInputs: true,
                    invalidReservations[index],
                    Array.Empty<ResourceAmount>(),
                    progressSeconds: 1f,
                    isPlayerPaused: false,
                    out string error), Is.False, error);
                Assert.That(state.HasReservedInputs, Is.False);
                Assert.That(state.ProgressSeconds, Is.Zero);
            }
        }

        [Test]
        public void MultiResourceRestoreAcceptsReversedReservationsAndCanonicalizesOrder()
        {
            FormalProductionDefinition definition = MultiResourceDefinition();
            var state = new BuildingProductionState(
                "multi-restore.reversed",
                definition);

            Assert.That(state.TryRestoreForPersistence(
                Array.Empty<ResourceAmount>(),
                hasReservedInputs: true,
                new[]
                {
                    new ResourceAmount(ResourceIds.EnergyCrystal, 1),
                    new ResourceAmount(ResourceIds.Iron, 2),
                },
                Array.Empty<ResourceAmount>(),
                progressSeconds: 1f,
                isPlayerPaused: false,
                out string error), Is.True, error);

            CollectionAssert.AreEqual(
                definition.Inputs,
                state.ReservedInputs);
        }

        [Test]
        public void MultiResourceProductionCrossesManyCyclesWithoutManagedAllocations()
        {
            const int warmupCycles = 2;
            const int measuredCycles = 64;
            const int reservedCycles = warmupCycles + measuredCycles + 1;
            FormalProductionDefinition definition = MultiResourceDefinition(
                inputCapacity: 512,
                outputCapacity: 512);
            var state = new BuildingProductionState(
                "multi-allocation.001",
                definition);
            state.Input.Add(ResourceIds.Iron, reservedCycles * 2);
            state.Input.Add(ResourceIds.EnergyCrystal, reservedCycles);
            var states = new[] { state };
            var city = new ResourceInventory(1000);
            var cityCapacity = new ResourceCapacityPolicy();
            var simulation = new FormalProductionSimulation();

            simulation.Tick(
                states,
                warmupCycles * definition.DurationSeconds,
                world: null,
                cityInventory: city,
                cityCapacity: cityCapacity,
                activeWarehouseCount: 0,
                globallyPaused: false);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            ProfilerRecorder recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory,
                "GC.Alloc",
                1024,
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread |
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            long before = GC.GetAllocatedBytesForCurrentThread();

            simulation.Tick(
                states,
                measuredCycles * definition.DurationSeconds,
                world: null,
                cityInventory: city,
                cityCapacity: cityCapacity,
                activeWarehouseCount: 0,
                globallyPaused: false);

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            recorder.Stop();
            long profiledBytes = 0;
            for (var index = 0; index < recorder.Count; index++)
            {
                ProfilerRecorderSample sample = recorder.GetSample(index);
                profiledBytes += sample.Value * sample.Count;
            }
            recorder.Dispose();
            TestContext.WriteLine(
                "MultiResourceProductionAllocationBytes=" + allocated);
            TestContext.WriteLine(
                "MultiResourceProductionProfiledBytes=" + profiledBytes);
            Assert.That(allocated, Is.Zero,
                "A warmed multi-input/multi-output production path must not allocate per completed cycle.");
            Assert.That(profiledBytes, Is.Zero,
                "The Unity managed-allocation recorder must remain empty across completed cycles.");
            Assert.That(state.Output.Get(ResourceIds.Alloy),
                Is.EqualTo(warmupCycles + measuredCycles));
            Assert.That(state.Output.Get(ResourceIds.RefinedStone),
                Is.EqualTo((warmupCycles + measuredCycles) * 2));
            Assert.That(state.HasReservedInputs, Is.True);
        }

        private static BuildingProductionState Smelter(string stableId, bool connected)
        {
            var state = new BuildingProductionState(
                stableId,
                FormalProductionDefinitionCatalog.Smelting);
            state.SetLogisticsConnected(connected);
            return state;
        }

        private static FormalProductionDefinition MultiResourceDefinition(
            int inputCapacity = 10,
            int outputCapacity = 3)
        {
            ConstructorInfo constructor = typeof(FormalProductionDefinition)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(float),
                        typeof(IReadOnlyList<ResourceAmount>),
                        typeof(IReadOnlyList<ResourceAmount>),
                        typeof(int),
                        typeof(int),
                        typeof(bool),
                    },
                    modifiers: null);
            Assert.That(constructor, Is.Not.Null);
            return (FormalProductionDefinition)constructor.Invoke(new object[]
            {
                "test.production.multi-resource",
                BuildingCatalog.Smelter.Id.Value,
                4f,
                new[]
                {
                    new ResourceAmount(ResourceIds.Iron, 2),
                    new ResourceAmount(ResourceIds.EnergyCrystal, 1),
                },
                new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 1),
                    new ResourceAmount(ResourceIds.RefinedStone, 2),
                },
                inputCapacity,
                outputCapacity,
                false,
            });
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
