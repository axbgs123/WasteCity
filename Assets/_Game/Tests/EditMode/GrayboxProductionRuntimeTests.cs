using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        public void GroundProductionKeepsRunningLocallyWhenCityLeavesLogisticsRange()
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
            Assert.That(runtime.RunnableStates, Does.Contain(insideState));
            Assert.That(runtime.RunnableStates, Does.Contain(outsideState));
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
        public void InnerCityProductionStillUsesMobilityRulesDuringTransition()
        {
            GrayboxBuildingInstance3D assembler = CreateInstance(
                "building.instance.000005.transition",
                BuildingCatalog.Assembler,
                BuildingSite.InnerCity,
                1,
                1);
            Complete(assembler);
            var runtime = new GrayboxProductionRuntime3D();

            runtime.Synchronize(
                new[] { assembler },
                CityMode.Deploying,
                cityX: 40,
                cityY: 30,
                groundRadius: BuildingRangeRules.InitialGroundRadius);

            Assert.That(runtime.TryGetState(
                assembler.StableInstanceId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.IsLogisticsConnected, Is.False);
            Assert.That(runtime.RunnableStates, Is.Empty);
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
        public void FullDismantleLockFreezesLogisticsAndCycleUntilUnlockWhileManifestPreviewDoesNot()
        {
            const string stableId = "building.instance.production-lock";
            GrayboxProductionRuntime3D runtime = RuntimeWithActiveSmelter(
                stableId,
                out GrayboxBuildingInstance3D smelter,
                out BuildingProductionState state);
            var cityInventory = new ResourceInventory(100);
            cityInventory.Add(ResourceIds.Iron, 10);
            using var cityStorage = new CityResourceStorageModel(
                cityInventory,
                100);
            var simulation = new FormalProductionSimulation();

            Assert.That(smelter.IsEvacuationLocked, Is.False,
                "Opening the manifest without confirming must not lock production.");
            runtime.Synchronize(
                new[] { smelter },
                CityMode.Mobile,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius,
                cityStorage);
            simulation.Tick(
                runtime.RunnableStates,
                1f,
                null,
                cityStorage,
                globallyPaused: false);
            Assert.That(state.ProgressSeconds, Is.EqualTo(2f));
            Assert.That(state.HasReservedInputs, Is.True);

            int inputBeforeLock = state.Input.Get(ResourceIds.Iron);
            int outputBeforeLock = state.Output.Get(ResourceIds.Alloy);
            float progressBeforeLock = state.ProgressSeconds;
            int cityIronBeforeLock =
                cityStorage.GetNetworkAmount(ResourceIds.Iron);
            int cityAlloyBeforeLock =
                cityStorage.GetNetworkAmount(ResourceIds.Alloy);
            ulong storageRevisionBeforeLock = cityStorage.Revision;
            SetEvacuationLocked(smelter, true);

            Assert.That(
                GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                    smelter,
                    CityMode.Fortress,
                    10,
                    10,
                    BuildingRangeRules.InitialGroundRadius),
                Is.False,
                "The shared access rule must reject logistics immediately when a full-dismantle lock is acquired.");
            runtime.Synchronize(
                new[] { smelter },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius,
                cityStorage);
            Assert.That(runtime.TryGetState(stableId, out BuildingProductionState locked), Is.True);
            Assert.That(locked, Is.SameAs(state));
            Assert.That(runtime.States, Is.EqualTo(new[] { state }));
            Assert.That(runtime.RunnableStates, Is.Empty);

            simulation.Tick(
                runtime.RunnableStates,
                10f,
                null,
                cityStorage,
                globallyPaused: false);

            Assert.That(state.Input.Get(ResourceIds.Iron),
                Is.EqualTo(inputBeforeLock));
            Assert.That(state.Output.Get(ResourceIds.Alloy),
                Is.EqualTo(outputBeforeLock));
            Assert.That(state.ProgressSeconds, Is.EqualTo(progressBeforeLock));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(cityStorage.GetNetworkAmount(ResourceIds.Iron),
                Is.EqualTo(cityIronBeforeLock));
            Assert.That(cityStorage.GetNetworkAmount(ResourceIds.Alloy),
                Is.EqualTo(cityAlloyBeforeLock));
            Assert.That(cityStorage.Revision,
                Is.EqualTo(storageRevisionBeforeLock));

            SetEvacuationLocked(smelter, false);
            runtime.Synchronize(
                new[] { smelter },
                CityMode.Mobile,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius,
                cityStorage);
            Assert.That(runtime.TryGetState(stableId, out BuildingProductionState restored), Is.True);
            Assert.That(restored, Is.SameAs(state));
            simulation.Tick(
                runtime.RunnableStates,
                1f,
                null,
                cityStorage,
                globallyPaused: false);

            Assert.That(state.ProgressSeconds,
                Is.EqualTo(progressBeforeLock + 1f));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Input.Get(ResourceIds.Iron),
                Is.EqualTo(inputBeforeLock),
                "Unlocking must not rebuild the cycle or deduct its reserved input twice.");
            Assert.That(state.Output.Get(ResourceIds.Alloy),
                Is.EqualTo(outputBeforeLock));
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
        public void EvacuationPayloadCaptureUsesStableIdAndDoesNotMutateProductionState()
        {
            const string stableId = "building.instance.evacuation.capture";
            GrayboxProductionRuntime3D runtime =
                RuntimeWithProductionPayload(stableId, out BuildingProductionState state);

            Assert.That(
                TryCaptureEvacuationPayload(runtime, stableId, out object payload),
                Is.True);

            Assert.That(PayloadStableId(payload), Is.EqualTo(stableId));
            Assert.That(PayloadAmount(payload, "Input", ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(PayloadAmount(payload, "ReservedInput", ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(PayloadAmount(payload, "Output", ResourceIds.Alloy), Is.EqualTo(3));
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.ProgressSeconds, Is.EqualTo(1f));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(3));

            state.Output.Add(ResourceIds.Alloy, 1);
            Assert.That(
                PayloadAmount(payload, "Output", ResourceIds.Alloy),
                Is.EqualTo(3),
                "A captured evacuation payload must remain an immutable snapshot.");
        }

        [TestCase(BuildingEvacuationTreatment.FullDismantle)]
        [TestCase(BuildingEvacuationTreatment.QuickDismantle)]
        public void SuccessfulDismantleMigrationFinalizesTheExactCapturedProductionPayload(
            BuildingEvacuationTreatment treatment)
        {
            string stableId = "building.instance.evacuation." + treatment;
            GrayboxProductionRuntime3D runtime =
                RuntimeWithProductionPayload(stableId, out _);
            Assert.That(
                TryCaptureEvacuationPayload(runtime, stableId, out object payload),
                Is.True);
            var destination = new ResourceInventory(100);

            AddPayloadSection(destination, payload, "Input");
            AddPayloadSection(destination, payload, "ReservedInput");
            AddPayloadSection(destination, payload, "Output");
            Assert.That(
                TryFinalizeEvacuationPayload(runtime, stableId, payload),
                Is.True);

            Assert.That(destination.Get(ResourceIds.Iron), Is.EqualTo(4));
            Assert.That(destination.Get(ResourceIds.Alloy), Is.EqualTo(3));
            Assert.That(runtime.TryGetState(stableId, out _), Is.False);
            Assert.That(runtime.TryGetState(stableId + ".other", out _), Is.True);
        }

        [Test]
        public void StaleOrRejectedProductionPayloadCannotFinalizeCurrentState()
        {
            const string stableId = "building.instance.evacuation.stale";
            GrayboxProductionRuntime3D runtime =
                RuntimeWithProductionPayload(stableId, out BuildingProductionState state);
            Assert.That(
                TryCaptureEvacuationPayload(runtime, stableId, out object payload),
                Is.True);
            state.Output.Add(ResourceIds.Alloy, 1);

            Assert.That(
                TryFinalizeEvacuationPayload(runtime, stableId, payload),
                Is.False,
                "A failed capacity commit or stale payload must not authorize cache clearing.");
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(4));
        }

        [Test]
        public void CapacityRejectedProductionMigrationLeavesRuntimePayloadIntact()
        {
            const string stableId = "building.instance.evacuation.capacity";
            GrayboxProductionRuntime3D runtime =
                RuntimeWithProductionPayload(stableId, out BuildingProductionState state);
            Assert.That(
                TryCaptureEvacuationPayload(runtime, stableId, out object payload),
                Is.True);
            var destination = new ResourceInventory(3);

            bool committed = ResourceTransaction.TryCommitBatch(
                new ResourceInventory(0),
                Array.Empty<ResourceAmount>(),
                destination,
                new ResourceCapacityPolicy(3, 0),
                0,
                new[]
                {
                    new ResourceAmount(
                        ResourceIds.Iron,
                        PayloadAmount(payload, "Input", ResourceIds.Iron) +
                        PayloadAmount(
                            payload,
                            "ReservedInput",
                            ResourceIds.Iron)),
                    new ResourceAmount(
                        ResourceIds.Alloy,
                        PayloadAmount(payload, "Output", ResourceIds.Alloy)),
                });

            Assert.That(committed, Is.False);
            Assert.That(destination.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(destination.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(runtime.TryGetState(stableId, out _), Is.True);
            Assert.That(state.Input.Get(ResourceIds.Iron), Is.EqualTo(2));
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.EqualTo(3));
        }

        [Test]
        public void AbandonExplicitlyDiscardsOrdinaryProductionPayload()
        {
            const string stableId = "building.instance.evacuation.abandon";
            GrayboxProductionRuntime3D runtime =
                RuntimeWithProductionPayload(stableId, out _);

            Assert.That(
                TryDiscardEvacuationPayload(runtime, stableId),
                Is.True);

            Assert.That(runtime.TryGetState(stableId, out _), Is.False);
            Assert.That(runtime.TryGetState(stableId + ".other", out _), Is.True);
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

        private static GrayboxProductionRuntime3D RuntimeWithProductionPayload(
            string stableId,
            out BuildingProductionState state)
        {
            GrayboxBuildingInstance3D instance = CreateInstance(
                stableId,
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D other = CreateInstance(
                stableId + ".other",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                12,
                10);
            Complete(instance);
            Complete(other);
            var runtime = new GrayboxProductionRuntime3D();
            runtime.Synchronize(
                new[] { other, instance },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.TryGetState(stableId, out state), Is.True);
            Assert.That(state.Input.Add(ResourceIds.Iron, 4), Is.EqualTo(4));
            new FormalProductionSimulation().Tick(
                new[] { state },
                1f,
                null,
                new ResourceInventory(100),
                new ResourceCapacityPolicy(),
                0,
                globallyPaused: false);
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Output.Add(ResourceIds.Alloy, 3), Is.EqualTo(3));
            return runtime;
        }

        private static GrayboxProductionRuntime3D RuntimeWithActiveSmelter(
            string stableId,
            out GrayboxBuildingInstance3D instance,
            out BuildingProductionState state)
        {
            instance = CreateInstance(
                stableId,
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            Complete(instance);
            var runtime = new GrayboxProductionRuntime3D();
            runtime.Synchronize(
                new[] { instance },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.TryGetState(stableId, out state), Is.True);
            Assert.That(state.Input.Add(ResourceIds.Iron, 4), Is.EqualTo(4));
            new FormalProductionSimulation().Tick(
                new[] { state },
                1f,
                null,
                new ResourceInventory(100),
                new ResourceCapacityPolicy(),
                0,
                globallyPaused: false);
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.ProgressSeconds, Is.EqualTo(1f));
            Assert.That(state.Output.Add(ResourceIds.Alloy, 3), Is.EqualTo(3));
            return runtime;
        }

        private static bool TryCaptureEvacuationPayload(
            GrayboxProductionRuntime3D runtime,
            string stableId,
            out object payload)
        {
            MethodInfo method = FindRuntimeMethod(
                runtime,
                "TryCaptureEvacuationPayload",
                2);
            object[] arguments = { stableId, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            payload = arguments[1];
            return result;
        }

        private static bool TryFinalizeEvacuationPayload(
            GrayboxProductionRuntime3D runtime,
            string stableId,
            object payload)
        {
            MethodInfo method = FindRuntimeMethod(
                runtime,
                "TryFinalizeEvacuationPayload",
                2);
            return (bool)method.Invoke(runtime, new[] { (object)stableId, payload });
        }

        private static bool TryDiscardEvacuationPayload(
            GrayboxProductionRuntime3D runtime,
            string stableId)
        {
            MethodInfo method = FindRuntimeMethod(
                runtime,
                "TryDiscardEvacuationPayload",
                1);
            return (bool)method.Invoke(runtime, new object[] { stableId });
        }

        private static MethodInfo FindRuntimeMethod(
            object runtime,
            string name,
            int parameterCount)
        {
            MethodInfo method = runtime.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate =>
                    candidate.Name == name &&
                    candidate.GetParameters().Length == parameterCount);
            Assert.That(
                method,
                Is.Not.Null,
                $"Missing evacuation runtime API: {runtime.GetType().Name}.{name}");
            return method;
        }

        private static string PayloadStableId(object payload)
        {
            Assert.That(payload, Is.Not.Null);
            PropertyInfo property = payload.GetType().GetProperty(
                "StableInstanceId",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Payload.StableInstanceId");
            return (string)property.GetValue(payload);
        }

        private static int PayloadAmount(
            object payload,
            string sectionName,
            string resourceId)
        {
            return PayloadSection(payload, sectionName)
                .Where(amount => amount.ResourceId == resourceId)
                .Sum(amount => amount.Amount);
        }

        private static IReadOnlyList<ResourceAmount> PayloadSection(
            object payload,
            string sectionName)
        {
            Assert.That(payload, Is.Not.Null);
            PropertyInfo property = payload.GetType().GetProperty(
                sectionName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Payload.{sectionName}");
            Assert.That(property.GetValue(payload), Is.InstanceOf<IEnumerable>());
            var values = new List<ResourceAmount>();
            foreach (object value in (IEnumerable)property.GetValue(payload))
            {
                Assert.That(value, Is.InstanceOf<ResourceAmount>());
                values.Add((ResourceAmount)value);
            }
            return values;
        }

        private static void AddPayloadSection(
            ResourceInventory destination,
            object payload,
            string sectionName)
        {
            IReadOnlyList<ResourceAmount> values =
                PayloadSection(payload, sectionName);
            for (int index = 0; index < values.Count; index++)
            {
                ResourceAmount amount = values[index];
                Assert.That(
                    destination.Add(amount.ResourceId, amount.Amount),
                    Is.EqualTo(amount.Amount));
            }
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
