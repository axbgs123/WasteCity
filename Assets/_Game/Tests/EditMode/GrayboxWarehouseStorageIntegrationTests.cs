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

        [TestCase(BuildingEvacuationTreatment.QuickDismantle)]
        [TestCase(BuildingEvacuationTreatment.Abandon)]
        public void NonEmptyWarehouseEvacuationRejectsOnlyWhenMigrationCannotFit(
            BuildingEvacuationTreatment treatment)
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
                treatment);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            ulong storageRevision = session.CityStorage.Revision;
            uint catalogRevision = session.CatalogRevision;
            uint placementRevision = session.PlacementRevision;
            int gridCount = session.GroundGrid.Count;
            Assert.That(session.TryCommitEvacuation(
                work,
                presentation,
                out _,
                out failureReason), Is.False);
            Assert.That(failureReason, Does.Contain("容量"));
            Assert.That(session.Instances, Does.Contain(warehouse));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                warehouse.StableInstanceId,
                ResourceIds.Alloy), Is.EqualTo(8));
            Assert.That(session.CityStorage.Revision, Is.EqualTo(storageRevision));
            Assert.That(session.CatalogRevision, Is.EqualTo(catalogRevision));
            Assert.That(session.PlacementRevision, Is.EqualTo(placementRevision));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(gridCount));
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

        [TestCase(BuildingEvacuationTreatment.QuickDismantle)]
        [TestCase(BuildingEvacuationTreatment.FullDismantle)]
        public void WarehouseEvacuationRejectsCombinedContentsAndRefundAtomically(
            BuildingEvacuationTreatment treatment)
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
                treatment);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            if (treatment == BuildingEvacuationTreatment.FullDismantle)
            {
                Assert.That(session.TryLockEvacuationWork(
                    new[] { work }, out failureReason), Is.True, failureReason);
            }
            ulong storageRevision = session.CityStorage.Revision;
            uint catalogRevision = session.CatalogRevision;
            uint placementRevision = session.PlacementRevision;
            int gridCount = session.GroundGrid.Count;
            int coreIron = session.Inventory.Get(ResourceIds.Iron);
            int coreRefund = session.Inventory.Get(
                source.Placement.Definition.CostId);
            int targetStone = session.CityStorage.GetWarehouseAmount(
                targetId, ResourceIds.Stone);
            Assert.That(session.TryCommitEvacuation(
                work,
                presentation,
                out int acceptedRefund,
                out failureReason), Is.False);

            Assert.That(acceptedRefund, Is.Zero);
            Assert.That(failureReason, Does.Contain("容量"));
            Assert.That(session.CityStorage.ContainsWarehouse(
                source.StableInstanceId), Is.True);
            Assert.That(session.CityStorage.GetWarehouseAmount(
                source.StableInstanceId,
                ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                targetId, ResourceIds.Iron), Is.Zero);
            Assert.That(session.CityStorage.GetWarehouseFreeSpace(targetId),
                Is.EqualTo(7));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                targetId, ResourceIds.Stone), Is.EqualTo(targetStone));
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(coreIron));
            Assert.That(session.Inventory.Get(
                source.Placement.Definition.CostId), Is.EqualTo(coreRefund));
            Assert.That(session.CityStorage.Revision, Is.EqualTo(storageRevision));
            Assert.That(session.CatalogRevision, Is.EqualTo(catalogRevision));
            Assert.That(session.PlacementRevision, Is.EqualTo(placementRevision));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(gridCount));
            Assert.That(session.Instances, Does.Contain(source));
        }

        [Test]
        public void EvacuationPayloadContentsAndRefundFailAsOneAtomicPreflight()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D source = BeginWarehouse(
                session,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);
            Assert.That(session.CityStorage.TrySetWarehouseConnected(
                source.StableInstanceId,
                connected: true), Is.True);
            const int sourceContents = 5;
            Assert.That(session.CityStorage.AddToWarehouse(
                source.StableInstanceId,
                ResourceIds.Iron,
                sourceContents), Is.EqualTo(sourceContents));

            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                source.StableInstanceId,
                source.Placement.Definition.Cost,
                source.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);
            Assert.That(work.Refund, Is.GreaterThan(0));
            var payload = new[]
            {
                new ResourceAmount(ResourceIds.Ammunition, 4)
            };
            int totalIncoming = sourceContents + work.Refund + payload[0].Amount;
            int targetFreeSpace = totalIncoming - 1;
            const string targetId = "building.instance.payload-target";
            Assert.That(session.CityStorage.TryRegisterWarehouse(
                targetId,
                connected: true), Is.True);
            Assert.That(session.CityStorage.AddToWarehouse(
                targetId,
                ResourceIds.Stone,
                WarehouseStorageState.FormalCapacity - targetFreeSpace),
                Is.EqualTo(
                    WarehouseStorageState.FormalCapacity - targetFreeSpace));
            Assert.That(session.CityStorage.GetWarehouseFreeSpace(targetId),
                Is.EqualTo(targetFreeSpace));
            session.Inventory.Set(
                ResourceIds.Iron,
                GrayboxBuildingSession3D.ResourceCapacity);
            session.Inventory.Set(
                source.Placement.Definition.CostId,
                GrayboxBuildingSession3D.ResourceCapacity);
            session.Inventory.Set(
                ResourceIds.Ammunition,
                GrayboxBuildingSession3D.ResourceCapacity);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            ulong storageRevision = session.CityStorage.Revision;
            uint catalogRevision = session.CatalogRevision;
            uint placementRevision = session.PlacementRevision;
            int gridCount = session.GroundGrid.Count;
            int coreIron = session.Inventory.Get(ResourceIds.Iron);
            int coreAlloy = session.Inventory.Get(ResourceIds.Alloy);
            int coreAmmunition = session.Inventory.Get(ResourceIds.Ammunition);
            int targetStone = session.CityStorage.GetWarehouseAmount(
                targetId, ResourceIds.Stone);
            int createCount = presentation.CreateCount;
            int updateCount = presentation.UpdateCount;
            int removeCount = presentation.RemoveCount;

            MethodInfo method = typeof(GrayboxBuildingSession3D).GetMethod(
                "TryCommitEvacuationWithPayload",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(BuildingEvacuationWork),
                    typeof(IReadOnlyList<ResourceAmount>),
                    typeof(IGrayboxBuildingPresentation3D),
                    typeof(int).MakeByRefType(),
                    typeof(string).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Session evacuation must expose one atomic payload-aware " +
                "commit entry point for production and defense internals.");
            var arguments = new object[]
            {
                work,
                payload,
                presentation,
                0,
                null,
            };

            Assert.That((bool)method.Invoke(session, arguments), Is.False);
            Assert.That((int)arguments[3], Is.Zero);
            Assert.That((string)arguments[4], Does.Contain("容量"));
            Assert.That(session.CityStorage.ContainsWarehouse(
                source.StableInstanceId), Is.True);
            Assert.That(session.CityStorage.GetWarehouseAmount(
                source.StableInstanceId,
                ResourceIds.Iron), Is.EqualTo(sourceContents));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                targetId, ResourceIds.Stone), Is.EqualTo(targetStone));
            Assert.That(session.CityStorage.GetWarehouseAmount(
                targetId, ResourceIds.Ammunition), Is.Zero);
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(coreIron));
            Assert.That(session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(coreAlloy));
            Assert.That(session.Inventory.Get(ResourceIds.Ammunition),
                Is.EqualTo(coreAmmunition));
            Assert.That(session.CityStorage.Revision, Is.EqualTo(storageRevision));
            Assert.That(session.CatalogRevision, Is.EqualTo(catalogRevision));
            Assert.That(session.PlacementRevision, Is.EqualTo(placementRevision));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(gridCount));
            Assert.That(session.Instances, Does.Contain(source));
            Assert.That(presentation.CreateCount, Is.EqualTo(createCount));
            Assert.That(presentation.UpdateCount, Is.EqualTo(updateCount));
            Assert.That(presentation.RemoveCount, Is.EqualTo(removeCount));
        }

        [Test]
        public void AbandonDiscardsOrdinaryPayloadButMigratesWarehouseContents()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D source = BeginWarehouse(
                session,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);
            Assert.That(session.CityStorage.TrySetWarehouseConnected(
                source.StableInstanceId,
                connected: true), Is.True);
            Assert.That(session.CityStorage.AddToWarehouse(
                source.StableInstanceId,
                ResourceIds.Iron,
                5), Is.EqualTo(5));
            Assert.That(session.CityStorage.AddToNetwork(
                ResourceIds.Ammunition,
                7), Is.EqualTo(7));
            int ammunitionBefore = session.CityStorage.GetNetworkAmount(
                ResourceIds.Ammunition);
            var payload = new[]
            {
                new ResourceAmount(ResourceIds.Ammunition, 4)
            };
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                source.StableInstanceId,
                source.Placement.Definition.Cost,
                source.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.Abandon);
            int ironBeforeEvacuation = session.CityStorage.GetNetworkAmount(
                ResourceIds.Iron);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string failureReason), Is.True, failureReason);
            Assert.That(session.TryCommitEvacuationWithPayload(
                work,
                payload,
                presentation,
                out int acceptedRefund,
                out failureReason), Is.True, failureReason);

            Assert.That(acceptedRefund, Is.Zero);
            Assert.That(session.CityStorage.GetNetworkAmount(
                ResourceIds.Ammunition), Is.EqualTo(ammunitionBefore),
                "Abandon discards ordinary production/defense payload.");
            Assert.That(session.CityStorage.GetNetworkAmount(
                ResourceIds.Iron), Is.EqualTo(ironBeforeEvacuation),
                "Warehouse contents still migrate through the evacuation plan.");
            Assert.That(session.CityStorage.ContainsWarehouse(
                source.StableInstanceId), Is.False);
            Assert.That(source.State,
                Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(source.IsPlayerOwned, Is.False);
            Assert.That(session.Instances, Does.Contain(source));
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
            public int CreateCount { get; private set; }
            public int UpdateCount { get; private set; }
            public int RemoveCount { get; private set; }

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                CreateCount++;
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                UpdateCount++;
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                RemoveCount++;
            }
        }
    }
}
