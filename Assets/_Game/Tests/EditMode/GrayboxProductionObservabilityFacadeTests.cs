using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionObservabilityFacadeTests
    {
        [Test]
        public void SnapshotIsStableSortedCompleteAndImmutable()
        {
            WorldMapModel world = WorldWithIronNode(24, 24, 7, 9, 9);
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.z",
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxBuildingInstance3D mine = CompletedInstance(
                "building.instance.a",
                BuildingCatalog.MiningStation,
                7,
                9,
                new ResourceNodeBinding("world.resource-node.7.9", 7, 9));
            var clock = new GrayboxProductionClock3D();

            Tick(clock, new[] { smelter, mine }, world, .1f);

            ProductionObservabilitySnapshot snapshot = clock.Snapshot;
            Assert.That(snapshot.Revision, Is.EqualTo(1));
            Assert.That(clock.Revision, Is.EqualTo(snapshot.Revision));
            Assert.That(snapshot.ActiveWarehouseCount, Is.Zero);
            Assert.That(snapshot.Entries, Has.Count.EqualTo(2));
            Assert.That(snapshot.Entries[0].StableInstanceId,
                Is.EqualTo(mine.StableInstanceId));
            Assert.That(snapshot.Entries[1].StableInstanceId,
                Is.EqualTo(smelter.StableInstanceId));

            ProductionBuildingObservability mineDetails = snapshot.Entries[0];
            Assert.That(mineDetails.ProductionDefinitionId,
                Is.EqualTo(FormalProductionDefinitionCatalog.Extraction.Id));
            Assert.That(mineDetails.BuildingDefinitionId,
                Is.EqualTo(BuildingCatalog.MiningStation.Id.Value));
            Assert.That(mineDetails.InputResourceId, Is.Null);
            Assert.That(mineDetails.OutputResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(mineDetails.BoundResourceNodeId,
                Is.EqualTo("world.resource-node.7.9"));
            Assert.That(mineDetails.BoundNodeX, Is.EqualTo(7));
            Assert.That(mineDetails.BoundNodeY, Is.EqualTo(9));
            Assert.That(mineDetails.BoundResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(mineDetails.BoundResourceRemaining, Is.EqualTo(9));
            Assert.That(mineDetails.ProgressSeconds, Is.EqualTo(.1f).Within(.0001f));
            Assert.That(mineDetails.ProgressNormalized,
                Is.EqualTo(.1f / 3f).Within(.0001f));
            Assert.That(mineDetails.StopReason, Is.EqualTo(ProductionStopReason.None));
            Assert.That(mineDetails.IsLogisticsConnected, Is.True);
            Assert.That(mineDetails.IsPlayerPaused, Is.False);
            Assert.That(mineDetails.HasReservedInputs, Is.True);
            Assert.That(mineDetails.InputAmount, Is.Zero);
            Assert.That(mineDetails.OutputAmount, Is.Zero);
            Assert.That(mineDetails.InputCapacity, Is.Zero);
            Assert.That(mineDetails.OutputCapacity, Is.EqualTo(20));

            Assert.That(snapshot.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability smelterDetails), Is.True);
            Assert.That(smelterDetails.InputResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(smelterDetails.OutputResourceId, Is.EqualTo(ResourceIds.Alloy));
            Assert.That(smelterDetails.InputRequiredPerCycle, Is.EqualTo(2));
            Assert.That(smelterDetails.OutputProducedPerCycle, Is.EqualTo(1));
            Assert.That(smelterDetails.DurationSeconds, Is.EqualTo(6f));
            Assert.That(smelterDetails.StopReason,
                Is.EqualTo(ProductionStopReason.MissingInput));
            Assert.That(snapshot.TryGet("missing", out _), Is.False);

            var mutableView = snapshot.Entries as IList<ProductionBuildingObservability>;
            Assert.That(mutableView, Is.Not.Null);
            Assert.That(mutableView.IsReadOnly, Is.True);
            Assert.Throws<System.NotSupportedException>(() =>
                mutableView[0] = smelterDetails);
        }

        [Test]
        public void WarehouseCountIsAVersionedSnapshotFact()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.production",
                BuildingCatalog.Smelter,
                40,
                40);
            GrayboxBuildingInstance3D warehouse = CompletedInstance(
                "building.instance.warehouse",
                BuildingCatalog.Warehouse,
                10,
                10);
            WorldMapModel world = EmptyWorld(64, 64);
            var clock = new GrayboxProductionClock3D();

            Tick(clock, new[] { smelter }, world, .1f);
            ProductionObservabilitySnapshot withoutWarehouse = clock.Snapshot;
            Assert.That(withoutWarehouse.ActiveWarehouseCount, Is.Zero);

            Tick(clock, new[] { smelter, warehouse }, world, .1f);
            ProductionObservabilitySnapshot withWarehouse = clock.Snapshot;
            Assert.That(withWarehouse.Revision,
                Is.GreaterThan(withoutWarehouse.Revision));
            Assert.That(withWarehouse.ActiveWarehouseCount, Is.EqualTo(1));
            Assert.That(withoutWarehouse.ActiveWarehouseCount, Is.Zero,
                "A published snapshot must not change when warehouse eligibility changes later.");

            Tick(clock, new[] { smelter }, world, .1f);
            Assert.That(clock.Snapshot.Revision,
                Is.GreaterThan(withWarehouse.Revision));
            Assert.That(clock.Snapshot.ActiveWarehouseCount, Is.Zero);
        }

        [Test]
        public void SnapshotDoesNotExposeObsoletePerResourceWarehouseCapacity()
        {
            PropertyInfo obsoleteCapacity =
                typeof(ProductionObservabilitySnapshot).GetProperty(
                    "CityCapacityPerResource",
                    BindingFlags.Instance | BindingFlags.Public);

            Assert.That(obsoleteCapacity, Is.Null,
                "Warehouse capacity is shared across stored resources; the city storage model owns the capacity truth.");
        }

        [Test]
        public void PauseCommandPublishesNewRevisionWithoutMutatingOldSnapshot()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.pause",
                BuildingCatalog.Smelter,
                10,
                10);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, new[] { smelter }, EmptyWorld(24, 24), .1f);
            ProductionObservabilitySnapshot before = clock.Snapshot;
            ulong beforeRevision = clock.Revision;

            Assert.That(clock.Commands.TrySetPlayerPaused(
                smelter.StableInstanceId,
                paused: true), Is.True);

            Assert.That(clock.Revision, Is.GreaterThan(beforeRevision));
            Assert.That(clock.Snapshot.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability after), Is.True);
            Assert.That(after.IsPlayerPaused, Is.True);
            Assert.That(after.StopReason,
                Is.EqualTo(ProductionStopReason.PlayerPaused));
            Assert.That(before.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability unchanged), Is.True);
            Assert.That(unchanged.IsPlayerPaused, Is.False);
            Assert.That(unchanged.StopReason,
                Is.EqualTo(ProductionStopReason.MissingInput));

            ulong afterRevision = clock.Revision;
            Assert.That(clock.Commands.TrySetPlayerPaused(
                "missing",
                paused: true), Is.False);
            Assert.That(clock.Revision, Is.EqualTo(afterRevision));
        }

        [Test]
        public void StableNoChangeTicksKeepSnapshotIdentityAndRevision()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.stable",
                BuildingCatalog.Smelter,
                40,
                40);
            WorldMapModel world = EmptyWorld(64, 64);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, new[] { smelter }, world, .1f);
            ProductionObservabilitySnapshot stable = clock.Snapshot;
            ulong revision = clock.Revision;

            for (var index = 0; index < 300; index++)
                Tick(clock, new[] { smelter }, world, .001f);

            Assert.That(clock.Snapshot, Is.SameAs(stable));
            Assert.That(clock.Revision, Is.EqualTo(revision));
        }

        [Test]
        public void QualifiedTransferCommandsAreAtomicAndPublishFreshSnapshots()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.transfer",
                BuildingCatalog.Smelter,
                40,
                40);
            WorldMapModel world = EmptyWorld(64, 64);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, new[] { smelter }, world, .1f);
            var source = new ResourceInventory(100);
            source.Add(ResourceIds.Iron, 5);
            ulong initialRevision = clock.Revision;

            ResourceTransferResult denied =
                clock.Commands.TransferInputFromInventory(
                    smelter.StableInstanceId,
                    source,
                    ResourceIds.Iron,
                    5,
                    accessValidated: false);
            Assert.That(denied.Status,
                Is.EqualTo(ResourceTransferStatus.InvalidRequest));
            Assert.That(source.Get(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(clock.Revision, Is.EqualTo(initialRevision));

            ResourceTransferResult supplied =
                clock.Commands.TransferInputFromInventory(
                    smelter.StableInstanceId,
                    source,
                    ResourceIds.Iron,
                    5,
                    accessValidated: true);
            Assert.That(supplied.Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Assert.That(source.Get(ResourceIds.Iron), Is.Zero);
            Assert.That(clock.Snapshot.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability suppliedSnapshot), Is.True);
            Assert.That(suppliedSnapshot.InputAmount, Is.EqualTo(5));
            Assert.That(clock.Revision, Is.GreaterThan(initialRevision));

            Tick(clock, new[] { smelter }, world, 6.1f);
            Assert.That(clock.Snapshot.TryGet(
                smelter.StableInstanceId,
                out ProductionBuildingObservability produced), Is.True);
            Assert.That(produced.OutputAmount, Is.EqualTo(1));
            ProductionObservabilitySnapshot beforeBlockedOutput = clock.Snapshot;
            var cityTarget = new ResourceInventory(100);
            ResourceTransferResult blocked =
                clock.Commands.TransferOutputToInventory(
                    smelter.StableInstanceId,
                    cityTarget,
                    new ResourceCapacityPolicy(0, 0),
                    resourceId: ResourceIds.Alloy,
                    requestedAmount: 1,
                    accessValidated: true);
            Assert.That(blocked.Status,
                Is.EqualTo(ResourceTransferStatus.TargetFull));
            Assert.That(beforeBlockedOutput.Entries[0].OutputAmount,
                Is.EqualTo(1));
            Assert.That(clock.Snapshot.Revision,
                Is.EqualTo(beforeBlockedOutput.Revision));

            ResourceTransferResult collected =
                clock.Commands.TransferOutputToInventory(
                    smelter.StableInstanceId,
                    cityTarget,
                    new ResourceCapacityPolicy(100, 0),
                    resourceId: ResourceIds.Alloy,
                    requestedAmount: 1,
                    accessValidated: true);
            Assert.That(collected.Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Assert.That(cityTarget.Get(ResourceIds.Alloy), Is.EqualTo(1));
            Assert.That(clock.Snapshot.Entries[0].OutputAmount, Is.Zero);
            Assert.That(beforeBlockedOutput.Entries[0].OutputAmount,
                Is.EqualTo(1),
                "A published snapshot must remain immutable after commands.");

            var backpack = new PlayerBackpackModel();
            backpack.Add(ResourceIds.Iron, 4);
            Assert.That(clock.Commands.TransferInputFromBackpack(
                    smelter.StableInstanceId,
                    backpack,
                    ResourceIds.Iron,
                    4,
                    accessValidated: true).MovedAmount,
                Is.EqualTo(4));
            Tick(clock, new[] { smelter }, world, 6.1f);
            Assert.That(clock.Commands.TransferOutputToBackpack(
                    smelter.StableInstanceId,
                    backpack,
                    ResourceIds.Alloy,
                    1,
                    accessValidated: true).MovedAmount,
                Is.EqualTo(1));
            Assert.That(BackpackAmount(backpack, ResourceIds.Alloy),
                Is.EqualTo(1));
        }

        [Test]
        public void OutputTransferDerivesWarehouseCountFromCommandOwner()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.authoritative-transfer",
                BuildingCatalog.Smelter,
                40,
                40);
            GrayboxBuildingInstance3D warehouse = CompletedInstance(
                "building.instance.authoritative-warehouse",
                BuildingCatalog.Warehouse,
                10,
                10);
            GrayboxBuildingInstance3D[] withWarehouse =
                { smelter, warehouse };
            WorldMapModel world = EmptyWorld(64, 64);
            var clock = new GrayboxProductionClock3D();
            Tick(clock, withWarehouse, world, .1f);
            var source = new ResourceInventory(100);
            source.Add(ResourceIds.Iron, 4);

            Assert.That(clock.Commands.TransferInputFromInventory(
                    smelter.StableInstanceId,
                    source,
                    ResourceIds.Iron,
                    2,
                    accessValidated: true).Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Tick(clock, withWarehouse, world, 6.1f);
            var warehouseOnlyCapacity = new ResourceCapacityPolicy(0, 1);
            var targetWithWarehouse = new ResourceInventory(100);
            ResourceTransferResult collected =
                clock.Commands.TransferOutputToInventory(
                    smelter.StableInstanceId,
                    targetWithWarehouse,
                    warehouseOnlyCapacity,
                    ResourceIds.Alloy,
                    1,
                    accessValidated: true);
            Assert.That(collected.Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Assert.That(targetWithWarehouse.Get(ResourceIds.Alloy),
                Is.EqualTo(1));

            Tick(clock, new[] { smelter }, world, .1f);
            Assert.That(clock.Snapshot.ActiveWarehouseCount, Is.Zero);
            Assert.That(clock.Commands.TransferInputFromInventory(
                    smelter.StableInstanceId,
                    source,
                    ResourceIds.Iron,
                    2,
                    accessValidated: true).Status,
                Is.EqualTo(ResourceTransferStatus.Completed));
            Tick(clock, new[] { smelter }, world, 6.1f);
            var targetWithoutWarehouse = new ResourceInventory(100);
            ResourceTransferResult blocked =
                clock.Commands.TransferOutputToInventory(
                    smelter.StableInstanceId,
                    targetWithoutWarehouse,
                    warehouseOnlyCapacity,
                    ResourceIds.Alloy,
                    1,
                    accessValidated: true);
            Assert.That(blocked.Status,
                Is.EqualTo(ResourceTransferStatus.TargetFull));
            Assert.That(targetWithoutWarehouse.Get(ResourceIds.Alloy),
                Is.Zero);
        }

        [Test]
        public void StableFixedStepProductionPublishesNoSnapshotAndAllocatesZero()
        {
            GrayboxBuildingInstance3D smelter = CompletedInstance(
                "building.instance.stable-fixed-step",
                BuildingCatalog.Smelter,
                40,
                40);
            var instances = new[] { smelter };
            WorldMapModel world = EmptyWorld(64, 64);
            var cityInventory = new ResourceInventory(1000);
            var clock = new GrayboxProductionClock3D();
            for (var warmup = 0; warmup < 3; warmup++)
            {
                TickFixedStep(
                    clock,
                    instances,
                    world,
                    cityInventory);
            }
            ulong revision = clock.Revision;
            ProductionObservabilitySnapshot snapshot = clock.Snapshot;
            uint captureCount = clock.ObservabilityCaptureCount;

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (var call = 0; call < 300; call++)
            {
                TickFixedStep(
                    clock,
                    instances,
                    world,
                    cityInventory);
            }
            long allocated =
                System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(clock.Revision, Is.EqualTo(revision));
            Assert.That(clock.Snapshot, Is.SameAs(snapshot));
            Assert.That(clock.ObservabilityCaptureCount,
                Is.EqualTo(captureCount),
                "Stable fixed steps must reject unchanged content before allocating a candidate snapshot.");
            Assert.That(allocated, Is.Zero);
        }

        private static int BackpackAmount(
            PlayerBackpackModel backpack,
            string resourceId)
        {
            var total = 0;
            for (var index = 0; index < backpack.SlotCount; index++)
            {
                BackpackSlot slot = backpack.GetSlot(index);
                if (slot.ResourceId == resourceId)
                    total += slot.Amount;
            }
            return total;
        }

        private static void Tick(
            GrayboxProductionClock3D clock,
            GrayboxBuildingInstance3D[] instances,
            WorldMapModel world,
            float seconds)
        {
            clock.Tick(
                seconds,
                paused: false,
                instances: instances,
                cityMode: CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius,
                world: world,
                cityInventory: new ResourceInventory(1000));
        }

        private static void TickFixedStep(
            GrayboxProductionClock3D clock,
            GrayboxBuildingInstance3D[] instances,
            WorldMapModel world,
            ResourceInventory cityInventory)
        {
            clock.Tick(
                GrayboxProductionClock3D.StepSeconds,
                paused: false,
                instances: instances,
                cityMode: CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: BuildingRangeRules.InitialGroundRadius,
                world: world,
                cityInventory: cityInventory);
        }

        private static GrayboxBuildingInstance3D CompletedInstance(
            string stableId,
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

        private static WorldMapModel EmptyWorld(int width, int height)
        {
            return WorldWithIronNode(width, height, -1, -1, 0);
        }

        private static WorldMapModel WorldWithIronNode(
            int width,
            int height,
            int nodeX,
            int nodeY,
            int amount)
        {
            var cells = new WorldCell[width, height];
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                cells[x, y] = new WorldCell(
                    TerrainKind.Wasteland,
                    x == nodeX && y == nodeY ? ResourceIds.Iron : null,
                    x == nodeX && y == nodeY ? amount : 0,
                    WorldTraversalKind.Open);
            }
            return new WorldMapModel(cells);
        }
    }
}
