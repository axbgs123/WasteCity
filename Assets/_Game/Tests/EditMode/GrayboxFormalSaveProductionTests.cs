using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveProductionTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building.GrayboxProductionSaveAdapter3D, " +
            "WasteCity.Graybox3D.Building";
        private const string UnknownDefinitionId =
            "mod.production.lost-smelting";
        private const string UnknownResourceId =
            "mod.resource.dark-matter";

        [Test]
        public void CaptureSortsStatesAndDeepCopiesAllThreeInventories()
        {
            GrayboxBuildingInstance3D later = Instance(
                "building.instance.000020",
                BuildingCatalog.Smelter,
                12,
                10);
            GrayboxBuildingInstance3D earlier = Instance(
                "building.instance.000010",
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { later, earlier },
                CityMode.Fortress,
                cityX: 10,
                cityY: 10);
            Assert.That(runtime.TryGetState(
                earlier.StableInstanceId,
                out BuildingProductionState state), Is.True);
            state.Input.Add(ResourceIds.Iron, 5);
            state.Input.Add(UnknownResourceId, 3);
            Tick(state, 1f);
            state.Output.Add(ResourceIds.Alloy, 2);
            state.Output.Add(UnknownResourceId, 4);
            object adapter = Adapter(runtime);

            FormalThreeDProductionSaveData saved = Capture(adapter);

            CollectionAssert.AreEqual(
                new[]
                {
                    earlier.StableInstanceId,
                    later.StableInstanceId,
                },
                saved.states.Select(item => item.stableInstanceId).ToArray());
            FormalThreeDProductionStateSaveData first = saved.states[0];
            AssertAmount(first.inputAmounts, ResourceIds.Iron, 3);
            AssertAmount(first.inputAmounts, UnknownResourceId, 3);
            Assert.That(first.hasReservedInputs, Is.True);
            AssertAmount(first.reservedInputs, ResourceIds.Iron, 2);
            AssertAmount(first.outputAmounts, ResourceIds.Alloy, 2);
            AssertAmount(first.outputAmounts, UnknownResourceId, 4);
            Assert.That(first.progressSeconds, Is.EqualTo(1f));

            first.inputAmounts[0].amount = 999;
            first.reservedInputs[0].amount = 999;
            first.outputAmounts[0].amount = 999;
            FormalThreeDProductionStateSaveData recaptured =
                Capture(adapter).states[0];
            AssertAmount(recaptured.inputAmounts, ResourceIds.Iron, 3);
            AssertAmount(recaptured.reservedInputs, ResourceIds.Iron, 2);
            AssertAmount(recaptured.outputAmounts, ResourceIds.Alloy, 2);
        }

        [Test]
        public void RestoredHalfCycleSmelterDoesNotSpendInputsTwiceAndCompletesOnce()
        {
            const string stableId = "building.instance.000030";
            GrayboxBuildingInstance3D sourceInstance = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D source = Runtime(
                new[] { sourceInstance },
                CityMode.Mobile,
                10,
                10);
            Assert.That(source.TryGetState(
                stableId,
                out BuildingProductionState sourceState), Is.True);
            sourceState.Input.Add(ResourceIds.Iron, 2);
            Tick(sourceState, 2f);
            FormalThreeDProductionSaveData saved = Capture(Adapter(source));

            GrayboxBuildingInstance3D restoredInstance = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D restored = Runtime(
                new[] { restoredInstance },
                CityMode.Mobile,
                10,
                10);
            var cityInventory = new ResourceInventory(100);
            cityInventory.Add(ResourceIds.Iron, 2);

            Assert.That(TryRestore(
                Adapter(restored),
                saved,
                new[] { restoredInstance },
                world: null,
                out string error), Is.True, error);
            Assert.That(cityInventory.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(restored.TryGetState(
                stableId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.ProgressSeconds, Is.EqualTo(2f));

            new FormalProductionSimulation().Tick(
                new[] { state },
                4f,
                world: null,
                cityInventory: cityInventory,
                cityCapacity: new ResourceCapacityPolicy(),
                activeWarehouseCount: 0,
                globallyPaused: false);

            Assert.That(cityInventory.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(state.HasReservedInputs, Is.False);
        }

        [Test]
        public void RestoredHalfCycleMineHarvestsOnlyWhenTheCycleCompletes()
        {
            const string stableId = "building.instance.000040";
            const string nodeId = "world.resource-node.0.0";
            var binding = new ResourceNodeBinding(nodeId, 0, 0);
            WorldMapModel sourceWorld = IronWorld(10);
            GrayboxBuildingInstance3D sourceInstance = Instance(
                stableId,
                BuildingCatalog.MiningStation,
                0,
                0,
                binding);
            GrayboxProductionRuntime3D source = Runtime(
                new[] { sourceInstance },
                CityMode.Mobile,
                0,
                0);
            Assert.That(source.TryGetState(
                stableId,
                out BuildingProductionState sourceState), Is.True);
            Tick(sourceState, 1f, sourceWorld);
            FormalThreeDProductionSaveData saved = Capture(Adapter(source));
            Assert.That(sourceWorld.Get(0, 0).ResourceAmount, Is.EqualTo(10));

            WorldMapModel restoredWorld = IronWorld(10);
            GrayboxBuildingInstance3D restoredInstance = Instance(
                stableId,
                BuildingCatalog.MiningStation,
                0,
                0,
                binding);
            GrayboxProductionRuntime3D restored = Runtime(
                new[] { restoredInstance },
                CityMode.Mobile,
                0,
                0);
            Assert.That(TryRestore(
                Adapter(restored),
                saved,
                new[] { restoredInstance },
                restoredWorld,
                out string error), Is.True, error);
            Assert.That(restoredWorld.Get(0, 0).ResourceAmount, Is.EqualTo(10),
                "Applying production truth must not harvest the world.");
            Assert.That(restored.TryGetState(
                stableId,
                out BuildingProductionState state), Is.True);

            Tick(state, 2f, restoredWorld);

            Assert.That(restoredWorld.Get(0, 0).ResourceAmount, Is.EqualTo(9));
            Assert.That(state.Output.Get(ResourceIds.Iron), Is.EqualTo(1));
        }

        [Test]
        public void PrepareIsAtomicAndCommitRejectsStaleOrAlreadyConsumedPlan()
        {
            const string stableId = "building.instance.000050";
            GrayboxBuildingInstance3D instance = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { instance },
                CityMode.Fortress,
                10,
                10);
            Assert.That(runtime.TryGetState(
                stableId,
                out BuildingProductionState state), Is.True);
            state.Input.Add(ResourceIds.Iron, 7);
            object adapter = Adapter(runtime);
            FormalThreeDProductionSaveData invalid = Production(
                State(
                    "building.instance.missing",
                    FormalProductionDefinitionCatalog.Smelting.Id));

            Assert.That(TryPrepare(
                adapter,
                invalid,
                new[] { instance },
                world: null,
                out _,
                out _), Is.False);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(7));

            FormalThreeDProductionSaveData valid = Production(
                State(
                    stableId,
                    FormalProductionDefinitionCatalog.Smelting.Id,
                    input: new[] { Amount(ResourceIds.Iron, 3) }));
            Assert.That(TryPrepare(
                adapter,
                valid,
                new[] { instance },
                world: null,
                out object stalePlan,
                out string error), Is.True, error);
            state.Input.Add(ResourceIds.Iron, 1);
            Assert.That(TryCommit(
                adapter,
                stalePlan,
                out _), Is.False,
                "A plan prepared against older runtime truth must be stale.");
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(8));

            Assert.That(TryPrepare(
                adapter,
                valid,
                new[] { instance },
                world: null,
                out object plan,
                out error), Is.True, error);
            Assert.That(TryCommit(adapter, plan, out error), Is.True, error);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(3));
            Assert.That(TryCommit(adapter, plan, out _), Is.False,
                "A restore plan is single-use.");
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(3));
        }

        [Test]
        public void RestoreKeepsInternalInventoryButUsesCurrentLogisticsTruth()
        {
            const string stableId = "building.instance.000060";
            FormalThreeDProductionSaveData saved = Production(
                State(
                    stableId,
                    FormalProductionDefinitionCatalog.Smelting.Id,
                    input: new[] { Amount(ResourceIds.Iron, 5) },
                    output: new[] { Amount(ResourceIds.Alloy, 4) }));
            GrayboxBuildingInstance3D instance = Instance(
                stableId,
                BuildingCatalog.Smelter,
                18,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { instance },
                CityMode.Fortress,
                cityX: 10,
                cityY: 10);

            Assert.That(TryRestore(
                Adapter(runtime),
                saved,
                new[] { instance },
                world: null,
                out string error), Is.True, error);
            Assert.That(runtime.TryGetState(
                stableId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(4));
            Assert.That(state.IsLogisticsConnected, Is.False,
                "Connectivity must remain derived from the current city.");
            Assert.That(runtime.RunnableStates, Does.Contain(state),
                "A ground machine outside logistics still runs locally.");
        }

        [Test]
        public void RestoreRejectsUnreservedProgressWithoutChangingRuntime()
        {
            const string stableId = "building.instance.000070";
            GrayboxBuildingInstance3D instance = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { instance },
                CityMode.Fortress,
                10,
                10);
            Assert.That(runtime.TryGetState(
                stableId,
                out BuildingProductionState state), Is.True);
            state.Input.Add(ResourceIds.Iron, 6);
            FormalThreeDProductionStateSaveData invalid = State(
                stableId,
                FormalProductionDefinitionCatalog.Smelting.Id,
                reserved: new[] { Amount(ResourceIds.Iron, 2) },
                progressSeconds: 1f,
                hasReserved: false);

            Assert.That(TryRestore(
                Adapter(runtime),
                Production(invalid),
                new[] { instance },
                world: null,
                out _), Is.False);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(6));
            Assert.That(state.ProgressSeconds, Is.Zero);
            Assert.That(state.HasReservedInputs, Is.False);
        }

        [Test]
        public void RestoreCrossChecksRecipeAndMiningBindingAgainstBuildingAndWorld()
        {
            const string smelterId = "building.instance.000080";
            GrayboxBuildingInstance3D smelter = Instance(
                smelterId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D smelterRuntime = Runtime(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10);
            FormalThreeDProductionStateSaveData boundSmelter = State(
                smelterId,
                FormalProductionDefinitionCatalog.Smelting.Id,
                boundNodeId: "world.resource-node.0.0",
                boundX: 0,
                boundY: 0);
            Assert.That(TryRestore(
                Adapter(smelterRuntime),
                Production(boundSmelter),
                new[] { smelter },
                IronWorld(10),
                out _), Is.False,
                "Non-extraction definitions cannot own node bindings.");

            const string mineId = "building.instance.000081";
            var actualBinding = new ResourceNodeBinding(
                "world.resource-node.0.0",
                0,
                0);
            GrayboxBuildingInstance3D mine = Instance(
                mineId,
                BuildingCatalog.MiningStation,
                0,
                0,
                actualBinding);
            GrayboxProductionRuntime3D mineRuntime = Runtime(
                new[] { mine },
                CityMode.Fortress,
                0,
                0);
            FormalThreeDProductionStateSaveData mismatchedMine = State(
                mineId,
                FormalProductionDefinitionCatalog.Extraction.Id,
                boundNodeId: "world.resource-node.0.1",
                boundX: 0,
                boundY: 1);
            Assert.That(TryRestore(
                Adapter(mineRuntime),
                Production(mismatchedMine),
                new[] { mine },
                IronWorld(10),
                out _), Is.False,
                "Saved binding must match the restored building identity.");

            FormalThreeDProductionStateSaveData wrongRecipe = State(
                mineId,
                FormalProductionDefinitionCatalog.Smelting.Id);
            Assert.That(TryRestore(
                Adapter(mineRuntime),
                Production(wrongRecipe),
                new[] { mine },
                IronWorld(10),
                out _), Is.False,
                "Known production definition must match the building.");
        }

        [Test]
        public void UnknownDefinitionRoundTripsInertAndPreservesInternalAssets()
        {
            const string stableId = "building.instance.000090";
            var missingBuilding = new BuildingDefinition(
                "mod.building.lost-smelter",
                "缺失内容",
                2,
                2,
                ResourceIds.Iron,
                0);
            GrayboxBuildingInstance3D placeholder = Instance(
                stableId,
                missingBuilding,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { placeholder },
                CityMode.Fortress,
                10,
                10);
            FormalThreeDProductionStateSaveData unknown = State(
                stableId,
                UnknownDefinitionId,
                input: new[] { Amount(UnknownResourceId, 7) },
                reserved: new[] { Amount(ResourceIds.Iron, 2) },
                output: new[] { Amount(ResourceIds.Alloy, 3) },
                progressSeconds: 2f,
                hasReserved: true);
            object adapter = Adapter(runtime);

            Assert.That(TryRestore(
                adapter,
                Production(unknown),
                new[] { placeholder },
                world: null,
                out string error), Is.True, error);
            Assert.That(runtime.States, Is.Empty);
            Assert.That(runtime.RunnableStates, Is.Empty);
            FormalThreeDProductionStateSaveData roundTrip =
                Capture(adapter).states.Single();
            Assert.That(roundTrip.stableInstanceId, Is.EqualTo(stableId));
            Assert.That(roundTrip.definitionId,
                Is.EqualTo(UnknownDefinitionId));
            AssertAmount(roundTrip.inputAmounts, UnknownResourceId, 7);
            AssertAmount(roundTrip.reservedInputs, ResourceIds.Iron, 2);
            AssertAmount(roundTrip.outputAmounts, ResourceIds.Alloy, 3);
            Assert.That(roundTrip.progressSeconds, Is.EqualTo(2f));
            Assert.That(roundTrip.hasReservedInputs, Is.True);
        }

        [Test]
        public void ProductionSaveDtoContainsNoDerivedOrObservabilityFields()
        {
            string[] forbiddenFragments =
            {
                "logistics",
                "stopreason",
                "observability",
                "revision",
                "hash",
            };
            FieldInfo[] fields = typeof(FormalThreeDProductionStateSaveData)
                .GetFields(BindingFlags.Instance | BindingFlags.Public);

            for (var fieldIndex = 0;
                 fieldIndex < fields.Length;
                 fieldIndex++)
            {
                string normalized = fields[fieldIndex].Name
                    .Replace("_", string.Empty)
                    .ToLowerInvariant();
                for (var fragmentIndex = 0;
                     fragmentIndex < forbiddenFragments.Length;
                     fragmentIndex++)
                {
                    Assert.That(
                        normalized,
                        Does.Not.Contain(forbiddenFragments[fragmentIndex]),
                        fields[fieldIndex].Name +
                        " is derived runtime/UI truth and must not persist.");
                }
            }
        }

        [Test]
        public void SynchronizeReplacesStateWhenRecipeOrMiningBindingIdentityChanges()
        {
            const string machineId = "building.instance.identity-machine";
            GrayboxBuildingInstance3D smelter = Instance(
                machineId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D machineRuntime = Runtime(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10);
            Assert.That(machineRuntime.TryGetState(
                machineId,
                out BuildingProductionState smelterState), Is.True);
            smelterState.Input.Add(ResourceIds.Iron, 8);

            GrayboxBuildingInstance3D assembler = Instance(
                machineId,
                BuildingCatalog.Assembler,
                10,
                10);
            Complete(assembler);
            machineRuntime.Synchronize(
                new[] { assembler },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(machineRuntime.TryGetState(
                machineId,
                out BuildingProductionState assemblerState), Is.True);
            Assert.That(assemblerState, Is.Not.SameAs(smelterState));
            Assert.That(assemblerState.Definition,
                Is.SameAs(FormalProductionDefinitionCatalog.Assembly));
            Assert.That(assemblerState.Input.Get(ResourceIds.Iron), Is.Zero,
                "A replacement building must not inherit the old recipe cache.");

            const string mineId = "building.instance.identity-mine";
            var firstBinding = new ResourceNodeBinding(
                "world.resource-node.0.0",
                0,
                0);
            GrayboxBuildingInstance3D firstMine = Instance(
                mineId,
                BuildingCatalog.MiningStation,
                0,
                0,
                firstBinding);
            GrayboxProductionRuntime3D mineRuntime = Runtime(
                new[] { firstMine },
                CityMode.Fortress,
                0,
                0);
            Assert.That(mineRuntime.TryGetState(
                mineId,
                out BuildingProductionState firstState), Is.True);
            firstState.Output.Add(ResourceIds.Iron, 5);

            var secondBinding = new ResourceNodeBinding(
                "world.resource-node.1.0",
                1,
                0);
            GrayboxBuildingInstance3D secondMine = Instance(
                mineId,
                BuildingCatalog.MiningStation,
                1,
                0,
                secondBinding);
            Complete(secondMine);
            mineRuntime.Synchronize(
                new[] { secondMine },
                CityMode.Fortress,
                0,
                0,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(mineRuntime.TryGetState(
                mineId,
                out BuildingProductionState secondState), Is.True);
            Assert.That(secondState, Is.Not.SameAs(firstState));
            Assert.That(secondState.BoundResourceNodeId,
                Is.EqualTo(secondBinding.StableId));
            Assert.That(secondState.BoundNodeX, Is.EqualTo(1));
            Assert.That(secondState.BoundNodeY, Is.Zero);
            Assert.That(secondState.Output.Get(ResourceIds.Iron), Is.Zero,
                "A new resource-node identity must not inherit old output.");
        }

        [Test]
        public void KnownBuildingWithUnknownRecipeRemainsInertAcrossSynchronize()
        {
            const string stableId = "building.instance.unknown-recipe";
            GrayboxBuildingInstance3D smelter = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10);
            FormalThreeDProductionStateSaveData unknown = State(
                stableId,
                UnknownDefinitionId,
                input: new[] { Amount(UnknownResourceId, 7) },
                reserved: new[] { Amount(ResourceIds.Iron, 2) },
                output: new[] { Amount(ResourceIds.Alloy, 3) },
                progressSeconds: 2f,
                hasReserved: true);
            object adapter = Adapter(runtime);

            Assert.That(TryRestore(
                adapter,
                Production(unknown),
                new[] { smelter },
                world: null,
                out string error), Is.True, error);
            Assert.That(runtime.States, Is.Empty);
            Assert.That(runtime.RunnableStates, Is.Empty);
            AssertUnknownRecipe(Capture(adapter).states.Single(), stableId);

            runtime.Synchronize(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.States, Is.Empty,
                "Synchronize must not replace missing content with a default recipe.");
            Assert.That(runtime.RunnableStates, Is.Empty);
            AssertUnknownRecipe(Capture(adapter).states.Single(), stableId);
        }

        [Test]
        public void OverCapacityKnownStatePreservesAssetsAndAcceptsNothingUntilReduced()
        {
            const string stableId = "building.instance.over-capacity";
            GrayboxBuildingInstance3D smelter = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10);
            object adapter = Adapter(runtime);
            FormalThreeDProductionSaveData saved = Production(
                State(
                    stableId,
                    FormalProductionDefinitionCatalog.Smelting.Id,
                    input: new[] { Amount(ResourceIds.Iron, 25) },
                    output: new[] { Amount(ResourceIds.Alloy, 12) }));

            Assert.That(TryRestore(
                adapter,
                saved,
                new[] { smelter },
                world: null,
                out string error), Is.True, error);
            Assert.That(runtime.TryGetState(
                stableId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(25));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(12));
            FormalThreeDProductionStateSaveData captured =
                Capture(adapter).states.Single();
            AssertAmount(captured.inputAmounts, ResourceIds.Iron, 25);
            AssertAmount(captured.outputAmounts, ResourceIds.Alloy, 12);

            Assert.That(state.Input.Add(ResourceIds.Iron, 1), Is.Zero);
            Assert.That(state.Output.Add(ResourceIds.Alloy, 1), Is.Zero);
            Assert.That(state.Input.TrySpend(ResourceIds.Iron, 6), Is.True);
            Assert.That(state.Output.TrySpend(ResourceIds.Alloy, 3), Is.True);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(19));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(9));
            Assert.That(state.Input.Add(ResourceIds.Iron, 2), Is.EqualTo(1));
            Assert.That(state.Output.Add(ResourceIds.Alloy, 2), Is.EqualTo(1));
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(20));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(10));
        }

        [Test]
        public void ReservedCycleAtDurationSurvivesOverCapacityRecaptureAndCompletesOnce()
        {
            const string stableId = "building.instance.blocked-cycle";
            GrayboxBuildingInstance3D firstInstance = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D firstRuntime = Runtime(
                new[] { firstInstance },
                CityMode.Mobile,
                10,
                10);
            object firstAdapter = Adapter(firstRuntime);
            FormalThreeDProductionSaveData activeOverCapacity = Production(
                State(
                    stableId,
                    FormalProductionDefinitionCatalog.Smelting.Id,
                    reserved: new[] { Amount(ResourceIds.Iron, 2) },
                    output: new[] { Amount(ResourceIds.Alloy, 12) },
                    progressSeconds: 5f,
                    hasReserved: true));
            Assert.That(TryRestore(
                firstAdapter,
                activeOverCapacity,
                new[] { firstInstance },
                world: null,
                out string error), Is.True, error);
            Assert.That(firstRuntime.TryGetState(
                stableId,
                out BuildingProductionState blocked), Is.True);

            Tick(blocked, 1f);

            Assert.That(blocked.ProgressSeconds,
                Is.EqualTo(FormalProductionDefinitionCatalog.Smelting
                    .DurationSeconds));
            Assert.That(blocked.HasReservedInputs, Is.True);
            Assert.That(blocked.StopReason,
                Is.EqualTo(ProductionStopReason.OutputFull));
            Assert.That(blocked.Output.Get(ResourceIds.Alloy), Is.EqualTo(12));
            FormalThreeDProductionSaveData blockedSave =
                Capture(firstAdapter);
            Assert.That(blockedSave.states[0].progressSeconds,
                Is.EqualTo(6f));
            AssertAmount(
                blockedSave.states[0].reservedInputs,
                ResourceIds.Iron,
                2);

            GrayboxBuildingInstance3D secondInstance = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D secondRuntime = Runtime(
                new[] { secondInstance },
                CityMode.Mobile,
                10,
                10);
            Assert.That(TryRestore(
                Adapter(secondRuntime),
                blockedSave,
                new[] { secondInstance },
                world: null,
                out error), Is.True, error);
            Assert.That(secondRuntime.TryGetState(
                stableId,
                out BuildingProductionState restored), Is.True);
            Assert.That(restored.Output.TrySpend(
                ResourceIds.Alloy,
                3), Is.True);
            var untouchedCity = new ResourceInventory(100);
            untouchedCity.Add(ResourceIds.Iron, 2);

            new FormalProductionSimulation().Tick(
                new[] { restored },
                0f,
                world: null,
                cityInventory: untouchedCity,
                cityCapacity: new ResourceCapacityPolicy(),
                activeWarehouseCount: 0,
                globallyPaused: false);

            Assert.That(restored.Output.Get(ResourceIds.Alloy), Is.EqualTo(10),
                "The already-paid cycle must produce exactly one alloy.");
            Assert.That(restored.HasReservedInputs, Is.False);
            Assert.That(restored.ProgressSeconds, Is.Zero);
            Assert.That(untouchedCity.Get(ResourceIds.Iron), Is.EqualTo(2),
                "Restoring/completing a paid cycle must not reserve twice.");
        }

        [Test]
        public void PrepareRejectsReplacementInstanceUntilRuntimeIsSynchronized()
        {
            const string stableId = "building.instance.unsynchronized";
            GrayboxBuildingInstance3D smelter = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10);
            Assert.That(runtime.TryGetState(
                stableId,
                out BuildingProductionState original), Is.True);
            original.Input.Add(ResourceIds.Iron, 7);
            GrayboxBuildingInstance3D replacement = Instance(
                stableId,
                BuildingCatalog.Assembler,
                10,
                10);
            Complete(replacement);
            FormalThreeDProductionSaveData assemblerSave = Production(
                State(
                    stableId,
                    FormalProductionDefinitionCatalog.Assembly.Id,
                    input: new[] { Amount(ResourceIds.Alloy, 4) }));

            Assert.That(TryPrepare(
                Adapter(runtime),
                assemblerSave,
                new[] { replacement },
                world: null,
                out _,
                out _), Is.False,
                "The caller must synchronize restored building identities first.");
            Assert.That(runtime.TryGetState(
                stableId,
                out BuildingProductionState after), Is.True);
            Assert.That(after, Is.SameAs(original));
            Assert.That(after.Definition,
                Is.SameAs(FormalProductionDefinitionCatalog.Smelting));
            Assert.That(after.Input.Get(ResourceIds.Iron), Is.EqualTo(7));
            Assert.That(after.Input.Get(ResourceIds.Alloy), Is.Zero);
        }

        [Test]
        public void OrphanPlanBecomesStaleWhenRuntimeSynchronizesAwayItsOwner()
        {
            const string stableId = "building.instance.stale-orphan";
            GrayboxBuildingInstance3D smelter = Instance(
                stableId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxProductionRuntime3D runtime = Runtime(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10);
            object adapter = Adapter(runtime);
            FormalThreeDProductionSaveData unknown = Production(
                State(
                    stableId,
                    UnknownDefinitionId,
                    input: new[] { Amount(UnknownResourceId, 7) }));
            Assert.That(TryPrepare(
                adapter,
                unknown,
                new[] { smelter },
                world: null,
                out object plan,
                out string error), Is.True, error);

            runtime.Synchronize(
                Array.Empty<GrayboxBuildingInstance3D>(),
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);

            Assert.That(TryCommit(adapter, plan, out _), Is.False,
                "A plan cannot install an orphan after its owner disappeared.");
            Assert.That(runtime.States, Is.Empty);
            Assert.That(runtime.RunnableStates, Is.Empty);
            Assert.That(Capture(adapter).states, Is.Empty,
                "Failed stale commit must not install orphan payload.");
        }

        private static object Adapter(GrayboxProductionRuntime3D runtime)
        {
            Type type = Type.GetType(AdapterTypeName);
            Assert.That(type, Is.Not.Null,
                "Task 7 requires the formal production save adapter.");
            return Activator.CreateInstance(type, runtime);
        }

        private static FormalThreeDProductionSaveData Capture(object adapter)
        {
            return (FormalThreeDProductionSaveData)Invoke(
                FindMethod(adapter, "Capture", 0),
                adapter,
                Array.Empty<object>());
        }

        private static bool TryRestore(
            object adapter,
            FormalThreeDProductionSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            WorldMapModel world,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, "TryRestore", 4);
            object[] arguments = { data, instances, world, null };
            bool restored = (bool)Invoke(method, adapter, arguments);
            error = arguments[3] as string;
            return restored;
        }

        private static bool TryPrepare(
            object adapter,
            FormalThreeDProductionSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            WorldMapModel world,
            out object plan,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, "TryPrepareRestore", 5);
            object[] arguments = { data, instances, world, null, null };
            bool prepared = (bool)Invoke(method, adapter, arguments);
            plan = arguments[3];
            error = arguments[4] as string;
            return prepared;
        }

        private static bool TryCommit(
            object adapter,
            object plan,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, "TryCommitRestore", 2);
            object[] arguments = { plan, null };
            bool committed = (bool)Invoke(method, adapter, arguments);
            error = arguments[1] as string;
            return committed;
        }

        private static MethodInfo FindMethod(
            object instance,
            string name,
            int parameterCount)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate =>
                    candidate.Name == name &&
                    candidate.GetParameters().Length == parameterCount);
            Assert.That(method, Is.Not.Null,
                name + " must expose the Task 7 persistence contract.");
            return method;
        }

        private static object Invoke(
            MethodInfo method,
            object instance,
            object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static GrayboxProductionRuntime3D Runtime(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode mode,
            int cityX,
            int cityY)
        {
            for (var index = 0; index < instances.Count; index++)
                Complete(instances[index]);
            var runtime = new GrayboxProductionRuntime3D();
            runtime.Synchronize(
                instances,
                mode,
                cityX,
                cityY,
                BuildingRangeRules.InitialGroundRadius);
            return runtime;
        }

        private static GrayboxBuildingInstance3D Instance(
            string stableInstanceId,
            BuildingDefinition definition,
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
                    BuildingSite.Ground,
                    BuildingOrientation.North),
                new ConstructionProgress(definition.BuildSeconds),
                binding,
            });
        }

        private static void Complete(GrayboxBuildingInstance3D instance)
        {
            MethodInfo method = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(instance, Array.Empty<object>());
        }

        private static void Tick(
            BuildingProductionState state,
            float seconds,
            WorldMapModel world = null)
        {
            new FormalProductionSimulation().Tick(
                new[] { state },
                seconds,
                world,
                new ResourceInventory(100),
                new ResourceCapacityPolicy(),
                activeWarehouseCount: 0,
                globallyPaused: false);
        }

        private static WorldMapModel IronWorld(int amount)
        {
            return new WorldMapModel(new[,]
            {
                {
                    new WorldCell(
                        TerrainKind.Rocky,
                        ResourceIds.Iron,
                        amount),
                },
            });
        }

        private static FormalThreeDProductionSaveData Production(
            params FormalThreeDProductionStateSaveData[] states)
        {
            return new FormalThreeDProductionSaveData { states = states };
        }

        private static FormalThreeDProductionStateSaveData State(
            string stableId,
            string definitionId,
            FormalThreeDResourceAmountSaveData[] input = null,
            FormalThreeDResourceAmountSaveData[] reserved = null,
            FormalThreeDResourceAmountSaveData[] output = null,
            float progressSeconds = 0f,
            bool hasReserved = false,
            string boundNodeId = null,
            int boundX = -1,
            int boundY = -1)
        {
            return new FormalThreeDProductionStateSaveData
            {
                stableInstanceId = stableId,
                definitionId = definitionId,
                inputAmounts = input ??
                    Array.Empty<FormalThreeDResourceAmountSaveData>(),
                hasReservedInputs = hasReserved,
                reservedInputs = reserved ??
                    Array.Empty<FormalThreeDResourceAmountSaveData>(),
                outputAmounts = output ??
                    Array.Empty<FormalThreeDResourceAmountSaveData>(),
                progressSeconds = progressSeconds,
                isPlayerPaused = false,
                boundResourceNodeId = boundNodeId,
                boundNodeX = boundX,
                boundNodeY = boundY,
            };
        }

        private static FormalThreeDResourceAmountSaveData Amount(
            string resourceId,
            int amount)
        {
            return new FormalThreeDResourceAmountSaveData
            {
                resourceId = resourceId,
                amount = amount,
            };
        }

        private static void AssertAmount(
            IEnumerable<FormalThreeDResourceAmountSaveData> amounts,
            string resourceId,
            int amount)
        {
            FormalThreeDResourceAmountSaveData actual = amounts.Single(
                item => string.Equals(
                    item.resourceId,
                    resourceId,
                    StringComparison.Ordinal));
            Assert.That(actual.amount, Is.EqualTo(amount));
        }

        private static void AssertUnknownRecipe(
            FormalThreeDProductionStateSaveData state,
            string stableId)
        {
            Assert.That(state.stableInstanceId, Is.EqualTo(stableId));
            Assert.That(state.definitionId, Is.EqualTo(UnknownDefinitionId));
            AssertAmount(state.inputAmounts, UnknownResourceId, 7);
            AssertAmount(state.reservedInputs, ResourceIds.Iron, 2);
            AssertAmount(state.outputAmounts, ResourceIds.Alloy, 3);
            Assert.That(state.progressSeconds, Is.EqualTo(2f));
            Assert.That(state.hasReservedInputs, Is.True);
        }
    }
}
