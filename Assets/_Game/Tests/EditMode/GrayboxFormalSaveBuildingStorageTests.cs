using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveBuildingStorageTests
    {
        private const string RequirementId = "IDEA-0015";
        private const string UnknownBuildingId = "mod.building.lost-found";
        private const string UnknownCoreResourceId = "mod.resource.dark-matter";
        private const string UnknownWarehouseResourceId =
            "mod.resource.void-crystal";
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void AdapterSourceOwnsNoFilesOrSceneDiscovery()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building/" +
                "GrayboxBuildingStorageSaveAdapter3D.cs");

            Assert.That(File.Exists(sourcePath), Is.True);
            string source = File.ReadAllText(sourcePath);
            Assert.That(source, Does.Not.Contain("System.IO"));
            Assert.That(source, Does.Not.Contain("persistentDataPath"));
            Assert.That(source, Does.Not.Contain("FindObject"));
            Assert.That(source, Does.Not.Contain("File."));
        }

        [Test]
        public void AdapterAcceptsCurrentWorldProviderInsteadOfFrozenWorld()
        {
            ConstructorInfo constructor =
                typeof(GrayboxBuildingStorageSaveAdapter3D).GetConstructor(
                    new[]
                    {
                        typeof(GrayboxBuildingSession3D),
                        typeof(IGrayboxBuildingPresentation3D),
                        typeof(Func<WorldMapModel>),
                    });

            Assert.That(
                constructor,
                Is.Not.Null,
                RequirementId + " requires a current-world provider so " +
                "resource binding validation follows a restored world " +
                "instead of retaining the adapter's construction-time map.");

            GrayboxBuildingSession3D session = CreateFormalSession();
            WorldMapModel currentWorld = null;
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                new RecordingPresentation(),
                () => currentWorld);
            currentWorld = CreateBindingWorld();
            Assert.That(
                adapter.TryRestore(
                    MiningBuilding(
                        GrayboxResourceNodeIdentity3D.Create(1, 1),
                        1,
                        1),
                    EmptyStorage(),
                    out string error),
                Is.True,
                error);
        }

        [Test]
        public void CaptureDisturbRestoreRoundTripsKnownBuildingAndStorageTruth()
        {
            GrayboxBuildingSession3D session = CreateFormalSession();
            var presentation = new RecordingPresentation();
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                presentation);
            FormalThreeDBuildingsSaveData seedBuildings =
                CreateKnownBuildings(nextOrdinal: 42);
            FormalThreeDStorageSaveData seedStorage = CreateStorage(
                GrayboxBuildingStorageSaveAdapter3D
                    .StorageConfigurationSignature,
                new[] { Amount(ResourceIds.Iron, 12) },
                new[]
                {
                    Warehouse(
                        "building.instance.000003",
                        ResourceIds.Alloy,
                        Amount(ResourceIds.Alloy, 40)),
                });

            Assert.That(
                adapter.TryRestore(
                    seedBuildings,
                    seedStorage,
                    out string seedError),
                Is.True,
                seedError);
            FormalThreeDBuildingsSaveData savedBuildings =
                adapter.CaptureBuildings();
            FormalThreeDStorageSaveData savedStorage =
                adapter.CaptureStorage();

            session.TickConstruction(
                100f,
                CityMode.Mobile,
                paused: false,
                presentation);
            session.Inventory.Set(ResourceIds.Iron, 1);
            Assert.That(
                session.CityStorage.AddToWarehouse(
                    "building.instance.000003",
                    ResourceIds.Alloy,
                    9),
                Is.EqualTo(9));

            Assert.That(
                adapter.TryRestore(
                    savedBuildings,
                    savedStorage,
                    out string restoreError),
                Is.True,
                restoreError);

            Assert.That(session.NextStableInstanceOrdinal, Is.EqualTo(42));
            Assert.That(session.Instances, Has.Count.EqualTo(3));
            AssertInstance(
                session.Instances[0],
                "building.instance.000001",
                BuildingCatalog.Housing,
                BuildingSite.InnerCity,
                1,
                1,
                BuildingOrientation.South,
                GrayboxBuildingInstanceState.UnderConstruction,
                2.25f,
                playerOwned: true);
            AssertInstance(
                session.Instances[1],
                "building.instance.000002",
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                10,
                10,
                BuildingOrientation.West,
                GrayboxBuildingInstanceState.AbandonedRuin,
                1f,
                playerOwned: false);
            AssertInstance(
                session.Instances[2],
                "building.instance.000003",
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                5,
                5,
                BuildingOrientation.East,
                GrayboxBuildingInstanceState.Completed,
                0f,
                playerOwned: true);
            Assert.That(
                session.CityStorage.GetCoreAmount(ResourceIds.Iron),
                Is.EqualTo(12));
            Assert.That(
                session.CityStorage.GetWarehouseFilter(
                    "building.instance.000003"),
                Is.EqualTo(ResourceIds.Alloy));
            Assert.That(
                session.CityStorage.GetWarehouseAmount(
                    "building.instance.000003",
                    ResourceIds.Alloy),
                Is.EqualTo(40));
        }

        [Test]
        public void RestoredHighWaterMarkIsUsedByNextNormalConstruction()
        {
            GrayboxBuildingSession3D session = CreateFormalSession();
            var presentation = new RecordingPresentation();
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                presentation);
            var buildings = new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 42,
                instances = Array.Empty<FormalThreeDBuildingInstanceSaveData>(),
            };

            Assert.That(
                adapter.TryRestore(
                    buildings,
                    EmptyStorage(),
                    out string restoreError),
                Is.True,
                restoreError);
            Assert.That(
                session.CityStorage.AddToNetwork(ResourceIds.Stone, 2),
                Is.EqualTo(2));

            Assert.That(
                session.TryBeginConstruction(
                    ValidWallRequest(session, 20, 20),
                    presentation,
                    out GrayboxBuildingInstance3D placed,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            Assert.That(
                placed.StableInstanceId,
                Is.EqualTo("building.instance.000042"));
            Assert.That(session.NextStableInstanceOrdinal, Is.EqualTo(43));
        }

        [Test]
        public void InvalidBuildingCandidatesAreRejectedBeforeAnyMutation()
        {
            WorldMapModel world = CreateBindingWorld();
            FormalThreeDBuildingsSaveData[] invalidCandidates =
            {
                OverlappingBuildings(),
                KnownFootprintMismatch(),
                MiningBuilding(
                    GrayboxResourceNodeIdentity3D.Create(2, 2),
                    1,
                    1),
                MiningBuilding(
                    GrayboxResourceNodeIdentity3D.Create(3, 3),
                    3,
                    3),
            };

            for (var index = 0; index < invalidCandidates.Length; index++)
            {
                GrayboxBuildingSession3D session = CreateFormalSession();
                var presentation = new RecordingPresentation();
                var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                    session,
                    presentation,
                    () => world);
                uint catalogBefore = session.CatalogRevision;
                uint placementBefore = session.PlacementRevision;
                ulong storageBefore = session.CityStorage.Revision;
                int ironBefore = session.CityStorage.GetCoreAmount(
                    ResourceIds.Iron);

                bool restored = adapter.TryRestore(
                    invalidCandidates[index],
                    EmptyStorage(),
                    out string error);

                Assert.That(restored, Is.False, "candidate " + index);
                Assert.That(error, Is.Not.Empty, "candidate " + index);
                Assert.That(session.Instances, Is.Empty);
                Assert.That(session.GroundGrid.Count, Is.Zero);
                Assert.That(session.InnerGrid.Count, Is.Zero);
                Assert.That(session.NextStableInstanceOrdinal, Is.EqualTo(1));
                Assert.That(session.CatalogRevision, Is.EqualTo(catalogBefore));
                Assert.That(
                    session.PlacementRevision,
                    Is.EqualTo(placementBefore));
                Assert.That(session.CityStorage.Revision, Is.EqualTo(storageBefore));
                Assert.That(
                    session.CityStorage.GetCoreAmount(ResourceIds.Iron),
                    Is.EqualTo(ironBefore));
                Assert.That(presentation.Created, Is.Empty);
                Assert.That(presentation.Removed, Is.Empty);
            }
        }

        [Test]
        public void MatchingSignatureRejectsOverCapacityWarehouseAtomically()
        {
            GrayboxBuildingSession3D session = CreateFormalSession();
            var presentation = new RecordingPresentation();
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                presentation);
            ulong revisionBefore = session.CityStorage.Revision;

            bool restored = adapter.TryRestore(
                SingleWarehouseBuilding(),
                OverCapacityStorage(
                    GrayboxBuildingStorageSaveAdapter3D
                        .StorageConfigurationSignature),
                out string error);

            Assert.That(restored, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(session.Instances, Is.Empty);
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.NextStableInstanceOrdinal, Is.EqualTo(1));
            Assert.That(session.CityStorage.Revision, Is.EqualTo(revisionBefore));
            Assert.That(session.CityStorage.WarehouseCount, Is.Zero);
            Assert.That(presentation.Created, Is.Empty);
        }

        [Test]
        public void ChangedSignaturePreservesOverCapacityWarehouseAsUsableState()
        {
            GrayboxBuildingSession3D session = CreateFormalSession();
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                new RecordingPresentation());
            const string warehouseId = "building.instance.000003";

            Assert.That(
                adapter.TryRestore(
                    SingleWarehouseBuilding(),
                    OverCapacityStorage("legacy.capacity.v0"),
                    out string error),
                Is.True,
                error);
            Assert.That(
                session.CityStorage.GetWarehouseCapacity(warehouseId),
                Is.EqualTo(150));
            Assert.That(
                session.CityStorage.GetWarehouseAmount(
                    warehouseId,
                    ResourceIds.Iron),
                Is.EqualTo(151));
            Assert.That(
                session.CityStorage.GetWarehouseFreeSpace(warehouseId),
                Is.Zero);
            Assert.That(
                session.CityStorage.AddToWarehouse(
                    warehouseId,
                    ResourceIds.Iron,
                    1),
                Is.Zero);
            Assert.That(
                session.CityStorage.TrySpendFromWarehouse(
                    warehouseId,
                    ResourceIds.Iron,
                    1),
                Is.True);
            Assert.That(
                session.CityStorage.GetWarehouseAmount(
                    warehouseId,
                    ResourceIds.Iron),
                Is.EqualTo(150));
        }

        [Test]
        public void LegacyOverflowRemainsRestorableAfterCaptureAndNewSession()
        {
            const string warehouseId = "building.instance.000003";
            GrayboxBuildingSession3D first = CreateFormalSession();
            var firstAdapter = new GrayboxBuildingStorageSaveAdapter3D(
                first,
                new RecordingPresentation());
            Assert.That(
                firstAdapter.TryRestore(
                    SingleWarehouseBuilding(),
                    OverCapacityStorage("legacy.capacity.v0"),
                    out string firstError),
                Is.True,
                firstError);
            FormalThreeDBuildingsSaveData recapturedBuildings =
                firstAdapter.CaptureBuildings();
            FormalThreeDStorageSaveData recapturedStorage =
                firstAdapter.CaptureStorage();
            Assert.That(
                recapturedStorage.warehouses[0].amounts[0].amount,
                Is.EqualTo(151));

            GrayboxBuildingSession3D second = CreateFormalSession();
            var secondAdapter = new GrayboxBuildingStorageSaveAdapter3D(
                second,
                new RecordingPresentation());
            Assert.That(
                secondAdapter.TryRestore(
                    recapturedBuildings,
                    recapturedStorage,
                    out string secondError),
                Is.True,
                secondError);
            Assert.That(
                second.CityStorage.GetWarehouseAmount(
                    warehouseId,
                    ResourceIds.Iron),
                Is.EqualTo(151));
            Assert.That(
                second.CityStorage.GetWarehouseFreeSpace(warehouseId),
                Is.Zero);
            Assert.That(
                second.CityStorage.AddToWarehouse(
                    warehouseId,
                    ResourceIds.Iron,
                    1),
                Is.Zero);
            Assert.That(
                second.CityStorage.TrySpendFromWarehouse(
                    warehouseId,
                    ResourceIds.Iron,
                    1),
                Is.True);
            Assert.That(
                second.CityStorage.GetWarehouseAmount(
                    warehouseId,
                    ResourceIds.Iron),
                Is.EqualTo(150));
        }

        [Test]
        public void UnknownContentRoundTripsAsNonOperationalOrphans()
        {
            var buildings = new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 8,
                instances = new[]
                {
                    Building(
                        "building.instance.000007",
                        UnknownBuildingId,
                        BuildingSite.Ground,
                        7,
                        8,
                        BuildingOrientation.North,
                        GrayboxBuildingInstanceState.Completed,
                        0f,
                        playerOwned: true,
                        footprintWidth: 2,
                        footprintHeight: 1),
                },
            };
            FormalThreeDStorageSaveData storage = CreateStorage(
                "legacy.mods.v4",
                new[] { Amount(UnknownCoreResourceId, 17) },
                new[]
                {
                    Warehouse(
                        "building.instance.000007",
                        UnknownWarehouseResourceId,
                        Amount(UnknownWarehouseResourceId, 19)),
                });

            GrayboxBuildingSession3D first = CreateFormalSession();
            var firstAdapter = new GrayboxBuildingStorageSaveAdapter3D(
                first,
                new RecordingPresentation());
            Assert.That(
                firstAdapter.TryRestore(buildings, storage, out string error),
                Is.True,
                error);
            FormalThreeDBuildingsSaveData capturedBuildings =
                firstAdapter.CaptureBuildings();
            FormalThreeDStorageSaveData capturedStorage =
                firstAdapter.CaptureStorage();

            Assert.That(first.Instances, Has.Count.EqualTo(1));
            Assert.That(
                first.Instances[0].Placement.Definition.Id.Value,
                Is.EqualTo(UnknownBuildingId));
            Assert.That(
                first.CompletedBuildingCount(UnknownBuildingId),
                Is.Zero,
                "Missing definitions remain occupying placeholders, not " +
                "operational formal buildings.");
            Assert.That(
                first.CityStorage.GetNetworkAmount(UnknownCoreResourceId),
                Is.Zero);
            Assert.That(
                first.CityStorage.GetNetworkAmount(
                    UnknownWarehouseResourceId),
                Is.Zero);
            Assert.That(
                capturedStorage.warehouses[0].filterResourceId,
                Is.EqualTo(UnknownWarehouseResourceId));
            Assert.That(capturedStorage.warehouses[0].amounts, Is.Empty);
            Assert.That(capturedStorage.orphanResources, Has.Length.EqualTo(2));

            GrayboxBuildingSession3D second = CreateFormalSession();
            var secondAdapter = new GrayboxBuildingStorageSaveAdapter3D(
                second,
                new RecordingPresentation());
            Assert.That(
                secondAdapter.TryRestore(
                    capturedBuildings,
                    capturedStorage,
                    out string secondError),
                Is.True,
                secondError);
            FormalThreeDStorageSaveData roundTrip =
                secondAdapter.CaptureStorage();
            Assert.That(
                roundTrip.warehouses[0].filterResourceId,
                Is.EqualTo(UnknownWarehouseResourceId));
            AssertOrphan(
                roundTrip.orphanResources[0],
                UnknownCoreResourceId,
                17,
                CityStorageOrphanResource.CoreOwnerKind,
                CityStorageOrphanResource.CoreOwnerStableId);
            AssertOrphan(
                roundTrip.orphanResources[1],
                UnknownWarehouseResourceId,
                19,
                CityStorageOrphanResource.WarehouseOwnerKind,
                "building.instance.000007");
        }

        [Test]
        public void PresentationRejectionLeavesAllAuthoritativeTruthUntouched()
        {
            GrayboxBuildingSession3D session = CreateFormalSession();
            var presentation = new RecordingPresentation
            {
                CreateResult = false,
            };
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                presentation);
            uint catalogBefore = session.CatalogRevision;
            uint placementBefore = session.PlacementRevision;
            ulong storageBefore = session.CityStorage.Revision;
            int ironBefore = session.CityStorage.GetCoreAmount(ResourceIds.Iron);

            bool restored = adapter.TryRestore(
                SingleWarehouseBuilding(),
                CreateStorage(
                    GrayboxBuildingStorageSaveAdapter3D
                        .StorageConfigurationSignature,
                    new[] { Amount(ResourceIds.Iron, 21) },
                    new[]
                    {
                        Warehouse(
                            "building.instance.000003",
                            null,
                            Amount(ResourceIds.Stone, 9)),
                    }),
                out string error);

            Assert.That(restored, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(session.Instances, Is.Empty);
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.NextStableInstanceOrdinal, Is.EqualTo(1));
            Assert.That(session.CatalogRevision, Is.EqualTo(catalogBefore));
            Assert.That(session.PlacementRevision, Is.EqualTo(placementBefore));
            Assert.That(session.CityStorage.Revision, Is.EqualTo(storageBefore));
            Assert.That(
                session.CityStorage.GetCoreAmount(ResourceIds.Iron),
                Is.EqualTo(ironBefore));
            Assert.That(session.CityStorage.WarehouseCount, Is.Zero);
        }

        [Test]
        public void CompletedPlayerWarehouseRequiresStorageRecordExactSet()
        {
            AssertRejectedWithoutMutation(
                SingleWarehouseBuilding(),
                EmptyStorage(),
                "A completed player warehouse omitted from storage must " +
                "be rejected before apply.");
        }

        [TestCase(
            GrayboxBuildingInstanceState.UnderConstruction,
            true,
            1f,
            TestName = "KnownUnderConstructionWarehouseCannotOwnStorage")]
        [TestCase(
            GrayboxBuildingInstanceState.AbandonedRuin,
            false,
            1f,
            TestName = "KnownRuinWarehouseCannotOwnStorage")]
        [TestCase(
            GrayboxBuildingInstanceState.Completed,
            false,
            0f,
            TestName = "KnownNonPlayerWarehouseCannotOwnStorage")]
        public void KnownWarehouseStorageRecordRequiresCompletedPlayerOwner(
            GrayboxBuildingInstanceState state,
            bool playerOwned,
            float remaining)
        {
            AssertRejectedWithoutMutation(
                WarehouseBuilding(
                    BuildingCatalog.Warehouse.Id.Value,
                    state,
                    playerOwned,
                    remaining),
                StorageForWarehouse(
                    GrayboxBuildingStorageSaveAdapter3D
                        .StorageConfigurationSignature,
                    ResourceIds.Iron),
                "Known warehouse storage requires one completed, " +
                "player-owned warehouse.");
        }

        [TestCase(
            GrayboxBuildingInstanceState.UnderConstruction,
            true,
            1f,
            TestName = "UnknownUnderConstructionWarehouseCannotOwnStorage")]
        [TestCase(
            GrayboxBuildingInstanceState.AbandonedRuin,
            false,
            1f,
            TestName = "UnknownRuinWarehouseCannotOwnStorage")]
        [TestCase(
            GrayboxBuildingInstanceState.Completed,
            false,
            0f,
            TestName = "UnknownNonPlayerWarehouseCannotOwnStorage")]
        public void UnknownWarehouseStorageRecordRequiresCompletedPlayerOwner(
            GrayboxBuildingInstanceState state,
            bool playerOwned,
            float remaining)
        {
            AssertRejectedWithoutMutation(
                WarehouseBuilding(
                    UnknownBuildingId,
                    state,
                    playerOwned,
                    remaining),
                StorageForWarehouse(
                    "legacy.mods.v4",
                    UnknownWarehouseResourceId),
                "Unknown warehouse placeholders obey the same completed " +
                "player ownership gate before preserving orphan content.");
        }

        [TestCase(
            "definition",
            TestName = "MalformedUnknownDefinitionStableIdIsAtomicFalse")]
        [TestCase(
            "resource",
            TestName = "MalformedUnknownResourceStableIdIsAtomicFalse")]
        [TestCase(
            "filter",
            TestName = "MalformedUnknownFilterStableIdIsAtomicFalse")]
        public void MalformedUnknownStableIdsReturnFalseWithoutThrowing(
            string target)
        {
            const string malformedStableId = "mod..not-formal";
            FormalThreeDBuildingsSaveData buildings =
                target == "definition"
                    ? WarehouseBuilding(
                        malformedStableId,
                        GrayboxBuildingInstanceState.Completed,
                        playerOwned: true,
                        remaining: 0f)
                    : SingleWarehouseBuilding();
            FormalThreeDStorageSaveData storage;
            if (target == "resource")
            {
                storage = CreateStorage(
                    GrayboxBuildingStorageSaveAdapter3D
                        .StorageConfigurationSignature,
                    new[] { Amount(malformedStableId, 3) },
                    new[]
                    {
                        Warehouse("building.instance.000003", null),
                    });
            }
            else if (target == "filter")
            {
                storage = StorageForWarehouse(
                    GrayboxBuildingStorageSaveAdapter3D
                        .StorageConfigurationSignature,
                    ResourceIds.Iron);
                storage.warehouses[0].filterResourceId = malformedStableId;
                storage.warehouses[0].amounts =
                    Array.Empty<FormalThreeDResourceAmountSaveData>();
            }
            else
            {
                storage = StorageForWarehouse(
                    "legacy.mods.v4",
                    UnknownWarehouseResourceId);
            }

            AssertRejectedWithoutMutation(
                buildings,
                storage,
                "Malformed unknown " + target +
                " StableId must return false without throwing.");
        }

        [Test]
        public void CaptureIsDeterministicDeepCopiedAndExcludesDerivedStorage()
        {
            GrayboxBuildingSession3D session = CreateFormalSession();
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                new RecordingPresentation());
            var buildings = new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 11,
                instances = new[]
                {
                    KnownCompleted(
                        "building.instance.000010",
                        BuildingCatalog.Warehouse,
                        BuildingSite.Ground,
                        12,
                        12,
                        BuildingOrientation.North),
                    KnownCompleted(
                        "building.instance.000002",
                        BuildingCatalog.Warehouse,
                        BuildingSite.InnerCity,
                        1,
                        1,
                        BuildingOrientation.North),
                },
            };
            FormalThreeDStorageSaveData storage = CreateStorage(
                GrayboxBuildingStorageSaveAdapter3D
                    .StorageConfigurationSignature,
                new[]
                {
                    Amount(ResourceIds.Stone, 3),
                    Amount(ResourceIds.Iron, 4),
                },
                new[]
                {
                    Warehouse(
                        "building.instance.000010",
                        null,
                        Amount(ResourceIds.Stone, 2),
                        Amount(ResourceIds.Iron, 1)),
                    Warehouse(
                        "building.instance.000002",
                        null,
                        Amount(ResourceIds.Stone, 5)),
                });
            Assert.That(
                adapter.TryRestore(buildings, storage, out string error),
                Is.True,
                error);

            FormalThreeDBuildingsSaveData firstBuildings =
                adapter.CaptureBuildings();
            FormalThreeDStorageSaveData firstStorage =
                adapter.CaptureStorage();
            Assert.That(
                Ids(firstBuildings.instances),
                Is.EqualTo(new[]
                {
                    "building.instance.000002",
                    "building.instance.000010",
                }));
            Assert.That(
                WarehouseIds(firstStorage.warehouses),
                Is.EqualTo(new[]
                {
                    "building.instance.000002",
                    "building.instance.000010",
                }));
            AssertStrictResourceOrder(firstStorage.coreAmounts);
            AssertStrictResourceOrder(firstStorage.warehouses[1].amounts);

            firstBuildings.instances[0].x = 99;
            firstStorage.coreAmounts[0].amount = 99;
            firstStorage.warehouses[0].stableInstanceId = "mutated";
            firstStorage.warehouses[1].amounts[0].amount = 99;
            FormalThreeDBuildingsSaveData secondBuildings =
                adapter.CaptureBuildings();
            FormalThreeDStorageSaveData secondStorage =
                adapter.CaptureStorage();

            Assert.That(secondBuildings.instances[0].x, Is.EqualTo(1));
            Assert.That(secondStorage.coreAmounts[0].amount, Is.EqualTo(4));
            Assert.That(
                secondStorage.warehouses[0].stableInstanceId,
                Is.EqualTo("building.instance.000002"));
            Assert.That(
                secondStorage.warehouses[1].amounts[0].amount,
                Is.EqualTo(1));
            Assert.That(
                typeof(FormalThreeDWarehouseSaveData).GetField("capacity"),
                Is.Null);
            Assert.That(
                typeof(FormalThreeDWarehouseSaveData).GetField("isConnected"),
                Is.Null);
            Assert.That(
                typeof(FormalThreeDSaveData).Assembly.GetType(
                    "WasteCity.Persistence.ThreeD." +
                    "FormalThreeDStorageConnectionSaveData"),
                Is.Null,
                "Connection remains derived and must not enter schema 31.");
        }

        private GrayboxBuildingSession3D CreateFormalSession()
        {
            var gameObject = new GameObject(
                "formal-building-storage-save-test");
            cleanup.Add(gameObject);
            GrayboxBuildingSession3D session =
                gameObject.AddComponent<GrayboxBuildingSession3D>();
            session.Configure(false);
            session.ConfigureFormalSession();
            return session;
        }

        private static FormalThreeDBuildingsSaveData CreateKnownBuildings(
            int nextOrdinal)
        {
            return new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = nextOrdinal,
                instances = new[]
                {
                    Building(
                        "building.instance.000003",
                        BuildingCatalog.Warehouse.Id.Value,
                        BuildingSite.Ground,
                        5,
                        5,
                        BuildingOrientation.East,
                        GrayboxBuildingInstanceState.Completed,
                        0f,
                        playerOwned: true,
                        BuildingCatalog.Warehouse.Width,
                        BuildingCatalog.Warehouse.Height),
                    Building(
                        "building.instance.000001",
                        BuildingCatalog.Housing.Id.Value,
                        BuildingSite.InnerCity,
                        1,
                        1,
                        BuildingOrientation.South,
                        GrayboxBuildingInstanceState.UnderConstruction,
                        2.25f,
                        playerOwned: true,
                        BuildingCatalog.Housing.Width,
                        BuildingCatalog.Housing.Height),
                    Building(
                        "building.instance.000002",
                        BuildingCatalog.Wall.Id.Value,
                        BuildingSite.Ground,
                        10,
                        10,
                        BuildingOrientation.West,
                        GrayboxBuildingInstanceState.AbandonedRuin,
                        1f,
                        playerOwned: false,
                        BuildingCatalog.Wall.Width,
                        BuildingCatalog.Wall.Height),
                },
            };
        }

        private static FormalThreeDBuildingsSaveData SingleWarehouseBuilding()
        {
            return new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 42,
                instances = new[]
                {
                    KnownCompleted(
                        "building.instance.000003",
                        BuildingCatalog.Warehouse,
                        BuildingSite.Ground,
                        5,
                        5,
                        BuildingOrientation.North),
                },
            };
        }

        private static FormalThreeDBuildingsSaveData WarehouseBuilding(
            string definitionId,
            GrayboxBuildingInstanceState state,
            bool playerOwned,
            float remaining)
        {
            BuildingDefinition known = string.Equals(
                    definitionId,
                    BuildingCatalog.Warehouse.Id.Value,
                    StringComparison.Ordinal)
                ? BuildingCatalog.Warehouse
                : null;
            return new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 42,
                instances = new[]
                {
                    Building(
                        "building.instance.000003",
                        definitionId,
                        BuildingSite.Ground,
                        5,
                        5,
                        BuildingOrientation.North,
                        state,
                        remaining,
                        playerOwned,
                        known?.Width ?? 2,
                        known?.Height ?? 2),
                },
            };
        }

        private static FormalThreeDBuildingsSaveData OverlappingBuildings()
        {
            return new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 3,
                instances = new[]
                {
                    KnownCompleted(
                        "building.instance.000001",
                        BuildingCatalog.Wall,
                        BuildingSite.Ground,
                        4,
                        4,
                        BuildingOrientation.North),
                    KnownCompleted(
                        "building.instance.000002",
                        BuildingCatalog.Wall,
                        BuildingSite.Ground,
                        4,
                        4,
                        BuildingOrientation.North),
                },
            };
        }

        private static FormalThreeDBuildingsSaveData KnownFootprintMismatch()
        {
            FormalThreeDBuildingInstanceSaveData wall = KnownCompleted(
                "building.instance.000001",
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                4,
                4,
                BuildingOrientation.North);
            wall.footprintWidth++;
            return new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 2,
                instances = new[] { wall },
            };
        }

        private static FormalThreeDBuildingsSaveData MiningBuilding(
            string boundId,
            int nodeX,
            int nodeY)
        {
            FormalThreeDBuildingInstanceSaveData mining = KnownCompleted(
                "building.instance.000001",
                BuildingCatalog.MiningStation,
                BuildingSite.Ground,
                0,
                0,
                BuildingOrientation.North);
            mining.boundResourceNodeId = boundId;
            mining.boundNodeX = nodeX;
            mining.boundNodeY = nodeY;
            return new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal = 2,
                instances = new[] { mining },
            };
        }

        private static WorldMapModel CreateBindingWorld()
        {
            var cells = new WorldCell[4, 4];
            for (var x = 0; x < 4; x++)
                for (var y = 0; y < 4; y++)
                    cells[x, y] = new WorldCell(
                        TerrainKind.Wasteland,
                        null,
                        0);
            cells[1, 1] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                100);
            cells[3, 3] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                100);
            return new WorldMapModel(cells);
        }

        private static FormalThreeDStorageSaveData OverCapacityStorage(
            string signature)
        {
            return CreateStorage(
                signature,
                Array.Empty<FormalThreeDResourceAmountSaveData>(),
                new[]
                {
                    Warehouse(
                        "building.instance.000003",
                        null,
                        Amount(ResourceIds.Iron, 151)),
                });
        }

        private static FormalThreeDStorageSaveData EmptyStorage()
        {
            return CreateStorage(
                GrayboxBuildingStorageSaveAdapter3D
                    .StorageConfigurationSignature,
                Array.Empty<FormalThreeDResourceAmountSaveData>(),
                Array.Empty<FormalThreeDWarehouseSaveData>());
        }

        private static FormalThreeDStorageSaveData CreateStorage(
            string signature,
            FormalThreeDResourceAmountSaveData[] core,
            FormalThreeDWarehouseSaveData[] warehouses)
        {
            return new FormalThreeDStorageSaveData
            {
                configurationSignature = signature,
                coreAmounts = core,
                warehouses = warehouses,
                orphanResources =
                    Array.Empty<FormalThreeDOrphanResourceSaveData>(),
            };
        }

        private static FormalThreeDStorageSaveData StorageForWarehouse(
            string signature,
            string resourceId)
        {
            return CreateStorage(
                signature,
                Array.Empty<FormalThreeDResourceAmountSaveData>(),
                new[]
                {
                    Warehouse(
                        "building.instance.000003",
                        resourceId,
                        Amount(resourceId, 7)),
                });
        }

        private static FormalThreeDWarehouseSaveData Warehouse(
            string stableInstanceId,
            string filterResourceId,
            params FormalThreeDResourceAmountSaveData[] amounts)
        {
            return new FormalThreeDWarehouseSaveData
            {
                stableInstanceId = stableInstanceId,
                filterResourceId = filterResourceId,
                amounts = amounts,
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

        private static FormalThreeDBuildingInstanceSaveData KnownCompleted(
            string stableInstanceId,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            BuildingOrientation orientation)
        {
            return Building(
                stableInstanceId,
                definition.Id.Value,
                site,
                x,
                y,
                orientation,
                GrayboxBuildingInstanceState.Completed,
                0f,
                playerOwned: true,
                definition.Width,
                definition.Height);
        }

        private static FormalThreeDBuildingInstanceSaveData Building(
            string stableInstanceId,
            string definitionId,
            BuildingSite site,
            int x,
            int y,
            BuildingOrientation orientation,
            GrayboxBuildingInstanceState state,
            float remaining,
            bool playerOwned,
            int footprintWidth,
            int footprintHeight)
        {
            return new FormalThreeDBuildingInstanceSaveData
            {
                stableInstanceId = stableInstanceId,
                definitionId = definitionId,
                site = (int)site,
                x = x,
                y = y,
                orientation = (int)orientation,
                state = (int)state,
                constructionRemainingSeconds = remaining,
                isPlayerOwned = playerOwned,
                boundResourceNodeId = string.Empty,
                boundNodeX = -1,
                boundNodeY = -1,
                footprintWidth = footprintWidth,
                footprintHeight = footprintHeight,
                evacuationLockedCrossCheck = false,
            };
        }

        private static BuildingPlacementRequest ValidWallRequest(
            GrayboxBuildingSession3D session,
            int x,
            int y)
        {
            BuildingDefinition definition = BuildingCatalog.Wall;
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            return new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                x,
                y,
                x,
                y,
                session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                coversCompatibleResourceNode: true,
                compatibleResourceNodeId: null,
                contentVisible: true,
                unlock,
                session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost));
        }

        private static void AssertInstance(
            GrayboxBuildingInstance3D instance,
            string stableId,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            BuildingOrientation orientation,
            GrayboxBuildingInstanceState state,
            float remaining,
            bool playerOwned)
        {
            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.Placement.Definition, Is.SameAs(definition));
            Assert.That(instance.Placement.Site, Is.EqualTo(site));
            Assert.That(instance.Placement.X, Is.EqualTo(x));
            Assert.That(instance.Placement.Y, Is.EqualTo(y));
            Assert.That(instance.Placement.Orientation, Is.EqualTo(orientation));
            Assert.That(instance.State, Is.EqualTo(state));
            Assert.That(instance.Progress.Remaining, Is.EqualTo(remaining));
            Assert.That(instance.IsPlayerOwned, Is.EqualTo(playerOwned));
        }

        private static void AssertOrphan(
            FormalThreeDOrphanResourceSaveData orphan,
            string resourceId,
            int amount,
            string ownerKind,
            string ownerStableId)
        {
            Assert.That(orphan.resourceId, Is.EqualTo(resourceId));
            Assert.That(orphan.amount, Is.EqualTo(amount));
            Assert.That(orphan.ownerKind, Is.EqualTo(ownerKind));
            Assert.That(orphan.ownerStableId, Is.EqualTo(ownerStableId));
        }

        private static string[] Ids(
            FormalThreeDBuildingInstanceSaveData[] instances)
        {
            var ids = new string[instances.Length];
            for (var index = 0; index < instances.Length; index++)
                ids[index] = instances[index].stableInstanceId;
            return ids;
        }

        private static string[] WarehouseIds(
            FormalThreeDWarehouseSaveData[] warehouses)
        {
            var ids = new string[warehouses.Length];
            for (var index = 0; index < warehouses.Length; index++)
                ids[index] = warehouses[index].stableInstanceId;
            return ids;
        }

        private static void AssertStrictResourceOrder(
            FormalThreeDResourceAmountSaveData[] amounts)
        {
            for (var index = 1; index < amounts.Length; index++)
                Assert.That(
                    string.CompareOrdinal(
                        amounts[index - 1].resourceId,
                        amounts[index].resourceId),
                    Is.LessThan(0));
        }

        private void AssertRejectedWithoutMutation(
            FormalThreeDBuildingsSaveData buildings,
            FormalThreeDStorageSaveData storage,
            string message)
        {
            GrayboxBuildingSession3D session = CreateFormalSession();
            var presentation = new RecordingPresentation();
            var adapter = new GrayboxBuildingStorageSaveAdapter3D(
                session,
                presentation);
            uint catalogBefore = session.CatalogRevision;
            uint placementBefore = session.PlacementRevision;
            ulong storageBefore = session.CityStorage.Revision;
            FormalThreeDStorageSaveData storageTruthBefore =
                adapter.CaptureStorage();
            bool restored = true;
            string error = null;

            Assert.That(
                () => restored = adapter.TryRestore(
                    buildings,
                    storage,
                    out error),
                Throws.Nothing,
                message);
            Assert.That(restored, Is.False, message);
            Assert.That(error, Is.Not.Empty, message);
            Assert.That(session.Instances, Is.Empty, message);
            Assert.That(session.GroundGrid.Count, Is.Zero, message);
            Assert.That(session.InnerGrid.Count, Is.Zero, message);
            Assert.That(session.NextStableInstanceOrdinal, Is.EqualTo(1));
            Assert.That(session.CatalogRevision, Is.EqualTo(catalogBefore));
            Assert.That(session.PlacementRevision, Is.EqualTo(placementBefore));
            Assert.That(session.CityStorage.Revision, Is.EqualTo(storageBefore));
            Assert.That(session.CityStorage.WarehouseCount, Is.Zero, message);
            Assert.That(presentation.Created, Is.Empty, message);
            Assert.That(presentation.Removed, Is.Empty, message);
            AssertStorageEquivalent(
                storageTruthBefore,
                adapter.CaptureStorage());
        }

        private static void AssertStorageEquivalent(
            FormalThreeDStorageSaveData expected,
            FormalThreeDStorageSaveData actual)
        {
            Assert.That(
                actual.configurationSignature,
                Is.EqualTo(expected.configurationSignature));
            Assert.That(actual.coreAmounts, Has.Length.EqualTo(
                expected.coreAmounts.Length));
            for (var index = 0; index < expected.coreAmounts.Length; index++)
            {
                Assert.That(
                    actual.coreAmounts[index].resourceId,
                    Is.EqualTo(expected.coreAmounts[index].resourceId));
                Assert.That(
                    actual.coreAmounts[index].amount,
                    Is.EqualTo(expected.coreAmounts[index].amount));
            }
            Assert.That(actual.warehouses, Is.Empty);
            Assert.That(actual.orphanResources, Is.Empty);
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool CreateResult { get; set; } = true;
            public List<GrayboxBuildingInstance3D> Created { get; } =
                new List<GrayboxBuildingInstance3D>();
            public List<GrayboxBuildingInstance3D> Removed { get; } =
                new List<GrayboxBuildingInstance3D>();

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                if (!CreateResult) return false;
                Created.Add(instance);
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                Created.Remove(instance);
                Removed.Add(instance);
            }
        }
    }
}
