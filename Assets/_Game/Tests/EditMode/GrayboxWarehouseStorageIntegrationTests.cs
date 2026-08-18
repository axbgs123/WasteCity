using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxWarehouseStorageIntegrationTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = roots.Count - 1; index >= 0; index--)
                Object.DestroyImmediate(roots[index]);
            roots.Clear();
        }

        [Test]
        public void SessionOwnsFormalAndDevelopmentCityStorageCapacities()
        {
            GrayboxBuildingSession3D session = CreateSession();

            session.ConfigureFormalSession();
            Assert.That(session.CityStorage, Is.Not.Null);
            Assert.That(session.CityStorage.CoreCapacityPerResource,
                Is.EqualTo(ResourceCapacityPolicy.FormalBaseCapacityPerResource));

            session.ConfigureDevelopmentFixture();
            Assert.That(session.CityStorage, Is.Not.Null);
            Assert.That(session.CityStorage.CoreCapacityPerResource,
                Is.EqualTo(GrayboxBuildingSession3D.ResourceCapacity));
        }

        [Test]
        public void ConstructionSpendsFromConnectedWarehouseWhenCoreIsEmpty()
        {
            GrayboxBuildingSession3D session = CreateSession();
            BuildingDefinition definition = BuildingCatalog.Warehouse;
            session.Inventory.Set(definition.CostId, 0);
            const string sourceWarehouseId =
                "building.instance.construction-source";
            Assert.That(session.CityStorage.TryRegisterWarehouse(
                sourceWarehouseId,
                connected: true), Is.True);
            Assert.That(session.CityStorage.AddToWarehouse(
                sourceWarehouseId,
                definition.CostId,
                definition.Cost), Is.EqualTo(definition.Cost));

            GrayboxBuildingInstance3D instance = BeginWarehouse(
                session,
                new RecordingPresentation());

            Assert.That(instance, Is.Not.Null);
            Assert.That(session.Inventory.Get(definition.CostId), Is.Zero);
            Assert.That(session.CityStorage.GetWarehouseAmount(
                sourceWarehouseId,
                definition.CostId), Is.Zero);
        }

        [Test]
        public void CompletedWarehouseRegistersStableSessionStorage()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D warehouse = BeginWarehouse(
                session,
                presentation);

            Assert.That(session.CityStorage.TryGetWarehouseSnapshot(
                warehouse.StableInstanceId, out _), Is.False);

            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(
                .1f,
                CityMode.Fortress,
                paused: false,
                presentation);

            Assert.That(warehouse.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(session.CityStorage.TryGetWarehouseSnapshot(
                warehouse.StableInstanceId,
                out WarehouseStorageSnapshot snapshot), Is.True);
            Assert.That(snapshot.IsConnected, Is.False,
                "Session lifecycle registration must not invent logistics connectivity.");
        }

        [Test]
        public void RuntimeSynchronizesWarehouseConnectivityAndPreservesDisconnectedContents()
        {
            GrayboxBuildingInstance3D inside = CompletedInstance(
                "building.instance.warehouse.inside",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D outside = CompletedInstance(
                "building.instance.warehouse.outside",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                40,
                40);
            var storage = new CityResourceStorageModel(
                new ResourceInventory(1000),
                150);
            var runtime = new GrayboxProductionRuntime3D();

            runtime.Synchronize(
                new[] { outside, inside },
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius,
                cityStorage: storage);

            Assert.That(storage.TryGetWarehouseSnapshot(
                inside.StableInstanceId,
                out WarehouseStorageSnapshot insideSnapshot), Is.True);
            Assert.That(insideSnapshot.IsConnected, Is.True);
            Assert.That(storage.TryGetWarehouseSnapshot(
                outside.StableInstanceId,
                out WarehouseStorageSnapshot outsideSnapshot), Is.True);
            Assert.That(outsideSnapshot.IsConnected, Is.False);
            Assert.That(runtime.ActiveWarehouseCount, Is.EqualTo(1));

            Assert.That(storage.AddToWarehouse(
                outside.StableInstanceId,
                ResourceIds.Iron,
                40), Is.EqualTo(40));
            runtime.Synchronize(
                new[] { outside, inside },
                CityMode.Mobile,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius,
                storage);

            Assert.That(runtime.ActiveWarehouseCount, Is.Zero);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.Zero);
            Assert.That(storage.GetWarehouseAmount(
                outside.StableInstanceId,
                ResourceIds.Iron), Is.EqualTo(40));
        }

        [Test]
        public void ProductionLogisticsUsesFilteredSharedWarehouseStorage()
        {
            ResourceInventory core = new ResourceInventory(1000);
            core.Add(ResourceIds.Alloy, 150);
            var storage = new CityResourceStorageModel(core, 150);
            storage.TryRegisterWarehouse("building.instance.warehouse");
            storage.TrySetWarehouseFilter(
                "building.instance.warehouse",
                ResourceIds.Alloy);
            var state = new BuildingProductionState(
                "building.instance.smelter",
                FormalProductionDefinitionCatalog.Smelting);
            state.SetLogisticsConnected(true);
            state.Output.Add(ResourceIds.Alloy, 1);

            new FormalProductionSimulation().Tick(
                new[] { state },
                .1f,
                world: null,
                cityStorage: storage,
                globallyPaused: false);

            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(storage.GetCoreAmount(ResourceIds.Alloy), Is.EqualTo(150));
            Assert.That(storage.GetWarehouseAmount(
                "building.instance.warehouse",
                ResourceIds.Alloy), Is.EqualTo(1));
        }

        [Test]
        public void ClockRoutesProductionThroughSessionOwnedStorage()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.smelter",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D warehouse = CompletedInstance(
                "building.instance.warehouse",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                11,
                10);
            ResourceInventory core = new ResourceInventory(1000);
            core.Add(ResourceIds.Alloy, 150);
            core.Add(ResourceIds.Iron, 2);
            var storage = new CityResourceStorageModel(core, 150);
            var clock = new GrayboxProductionClock3D();

            clock.Tick(
                .1f,
                paused: false,
                new[] { smelter, warehouse },
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius,
                world: null,
                cityStorage: storage);
            clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state);
            state.Output.Add(ResourceIds.Alloy, 1);

            clock.Tick(
                .1f,
                false,
                new[] { smelter, warehouse },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius,
                null,
                storage);

            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(storage.GetWarehouseAmount(
                warehouse.StableInstanceId,
                ResourceIds.Alloy), Is.EqualTo(1));
        }

        [Test]
        public void ProductionCommandFacadeUsesItsAuthoritativeCityStorage()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.smelter.command",
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            GrayboxBuildingInstance3D warehouse = CompletedInstance(
                "building.instance.warehouse.command",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                11,
                10);
            ResourceInventory core = new ResourceInventory(1000);
            core.Add(ResourceIds.Alloy, 150);
            var storage = new CityResourceStorageModel(core, 150);
            var clock = new GrayboxProductionClock3D();
            clock.Tick(
                .1f,
                false,
                new[] { smelter, warehouse },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius,
                null,
                storage);
            clock.Runtime.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState state);
            state.Output.Add(ResourceIds.Alloy, 2);

            ResourceTransferResult result =
                clock.Commands.TransferOutputToCityStorage(
                    smelter.StableInstanceId,
                    ResourceIds.Alloy,
                    2,
                    accessValidated: true);

            Assert.That(result.Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Assert.That(result.MovedAmount, Is.EqualTo(2));
            Assert.That(state.Output.Get(ResourceIds.Alloy), Is.Zero);
            Assert.That(storage.GetWarehouseAmount(
                warehouse.StableInstanceId,
                ResourceIds.Alloy), Is.EqualTo(2));
        }

        [Test]
        public void NonEmptyWarehouseEvacuationRejectsOnlyWhenMigrationCannotFit()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D warehouse = BeginWarehouse(
                session,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);
            Assert.That(session.CityStorage.AddToWarehouse(
                warehouse.StableInstanceId,
                ResourceIds.Alloy,
                8), Is.EqualTo(8));
            session.Inventory.Set(
                ResourceIds.Alloy,
                GrayboxBuildingSession3D.ResourceCapacity);
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                warehouse.StableInstanceId,
                warehouse.Placement.Definition.Cost,
                warehouse.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            Assert.That(session.TryCommitEvacuation(
                work,
                presentation,
                out _,
                out failureReason), Is.False);
            Assert.That(failureReason, Does.Contain("仓库未清空"));
            Assert.That(session.Instances, Does.Contain(warehouse));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                warehouse.StableInstanceId,
                ResourceIds.Alloy), Is.EqualTo(8));
        }

        [Test]
        public void WarehouseEvacuationMigratesContentsBeforeRemovingInstance()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D warehouse = BeginWarehouse(
                session,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);
            Assert.That(session.CityStorage.AddToWarehouse(
                warehouse.StableInstanceId,
                ResourceIds.Iron,
                8), Is.EqualTo(8));
            session.CityStorage.TrySetWarehouseConnected(
                warehouse.StableInstanceId,
                connected: true);
            int totalBefore = session.CityStorage.GetNetworkAmount(
                ResourceIds.Iron);
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                warehouse.StableInstanceId,
                warehouse.Placement.Definition.Cost,
                warehouse.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            Assert.That(session.TryCommitEvacuation(
                work,
                presentation,
                out _,
                out failureReason), Is.True, failureReason);

            Assert.That(session.Instances, Is.Empty);
            Assert.That(session.CityStorage.ContainsWarehouse(
                warehouse.StableInstanceId), Is.False);
            Assert.That(session.CityStorage.GetNetworkAmount(ResourceIds.Iron),
                Is.EqualTo(totalBefore));
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void EvacuationRefundUsesConnectedWarehouseWhenCoreIsFull()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D warehouse = BeginWarehouse(
                session,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);
            const string refundWarehouseId = "building.instance.refund-target";
            Assert.That(session.CityStorage.TryRegisterWarehouse(
                refundWarehouseId,
                connected: true), Is.True);
            string costId = warehouse.Placement.Definition.CostId;
            session.Inventory.Set(costId, GrayboxBuildingSession3D.ResourceCapacity);
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                warehouse.StableInstanceId,
                warehouse.Placement.Definition.Cost,
                warehouse.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            Assert.That(session.TryCommitEvacuation(
                work,
                presentation,
                out int acceptedRefund,
                out failureReason), Is.True, failureReason);

            Assert.That(acceptedRefund, Is.EqualTo(work.Refund));
            Assert.That(session.Inventory.Get(costId),
                Is.EqualTo(GrayboxBuildingSession3D.ResourceCapacity));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                refundWarehouseId,
                costId), Is.EqualTo(work.Refund));
        }

        [Test]
        public void WarehouseEvacuationPreservesContentsBeforePartialRefund()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D source = BeginWarehouse(
                session,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);
            session.CityStorage.TrySetWarehouseConnected(
                source.StableInstanceId,
                connected: true);
            Assert.That(session.CityStorage.AddToWarehouse(
                source.StableInstanceId,
                ResourceIds.Iron,
                5), Is.EqualTo(5));

            const string targetId = "building.instance.partial-refund-target";
            Assert.That(session.CityStorage.TryRegisterWarehouse(
                targetId,
                connected: true), Is.True);
            Assert.That(session.CityStorage.AddToWarehouse(
                targetId,
                ResourceIds.Stone,
                143), Is.EqualTo(143));
            session.Inventory.Set(
                ResourceIds.Iron,
                GrayboxBuildingSession3D.ResourceCapacity);
            session.Inventory.Set(
                source.Placement.Definition.CostId,
                GrayboxBuildingSession3D.ResourceCapacity);
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                source.StableInstanceId,
                source.Placement.Definition.Cost,
                source.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            Assert.That(session.TryCommitEvacuation(
                work,
                presentation,
                out int acceptedRefund,
                out failureReason), Is.True, failureReason);

            Assert.That(acceptedRefund, Is.EqualTo(2));
            Assert.That(session.CityStorage.ContainsWarehouse(
                source.StableInstanceId), Is.False);
            Assert.That(session.CityStorage.GetWarehouseAmount(
                targetId,
                ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                targetId,
                source.Placement.Definition.CostId), Is.EqualTo(2));
            Assert.That(session.CityStorage.GetWarehouseFreeSpace(targetId),
                Is.Zero);
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var root = new GameObject("WarehouseStorageIntegration.Session");
            roots.Add(root);
            GrayboxBuildingSession3D session =
                root.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static GrayboxBuildingInstance3D BeginWarehouse(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation)
        {
            BuildingDefinition definition = BuildingCatalog.Warehouse;
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
                10,
                10,
                12,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
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
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
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

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
