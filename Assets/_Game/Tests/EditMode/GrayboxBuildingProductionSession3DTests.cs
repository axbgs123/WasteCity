using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Production;
using WasteCity.Persistence;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingProductionSession3DTests
    {
        private const string SynchronizeMethodName =
            "SynchronizeProductionRuntime";
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void IDEA0011_PlacementCommit_PersistsEvaluatedNodeIdentityAndCoordinates()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            const int nodeX = 11;
            const int nodeY = 10;
            string nodeId = GrayboxResourceNodeIdentity3D.Create(nodeX, nodeY);

            GrayboxBuildingInstance3D mine = Begin(
                session,
                BuildingCatalog.MiningStation,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                nodeId,
                presentation,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(evaluation.CompatibleResourceNodeId, Is.EqualTo(nodeId));
            Assert.That(
                RequiredProperty<string>(mine, "CompatibleResourceNodeId"),
                Is.EqualTo(nodeId),
                "The committed instance must snapshot the refreshed placement evaluation.");
            Assert.That(
                RequiredProperty<int>(mine, "CompatibleResourceNodeX"),
                Is.EqualTo(nodeX));
            Assert.That(
                RequiredProperty<int>(mine, "CompatibleResourceNodeY"),
                Is.EqualTo(nodeY));
            Assert.That(mine.Placement.X, Is.EqualTo(10));
            Assert.That(mine.Placement.Y, Is.EqualTo(10));
        }

        [Test]
        public void IDEA0011_ProductionStates_ExistOnlyForCompletedOwnedUnlockedInstances()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            const int nodeX = 11;
            const int nodeY = 10;
            GrayboxBuildingInstance3D mine = Begin(
                session,
                BuildingCatalog.MiningStation,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                GrayboxResourceNodeIdentity3D.Create(nodeX, nodeY),
                presentation,
                out _);
            Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                14,
                10,
                null,
                presentation,
                out _);
            WorldMapModel world = WorldWithIronNode(nodeX, nodeY, 20);

            Synchronize(session, world, CityMode.Fortress, 12, 12);
            Assert.That(ProductionStates(session), Is.Empty,
                "Under-construction instances must not enter production.");

            session.CompleteAllConstructionForDevelopment(presentation);
            Synchronize(session, world, CityMode.Fortress, 12, 12);

            Assert.That(ProductionStates(session).Count, Is.EqualTo(1),
                "Completed non-production buildings must not create state.");
            Assert.That(TryGetState(session, mine.StableInstanceId, out var first),
                Is.True);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.StableInstanceId, Is.EqualTo(mine.StableInstanceId));
            Assert.That(first.BoundNodeId,
                Is.EqualTo(GrayboxResourceNodeIdentity3D.Create(nodeX, nodeY)));

            Synchronize(session, world, CityMode.Fortress, 12, 12);
            Assert.That(TryGetState(session, mine.StableInstanceId, out var retained),
                Is.True);
            Assert.That(retained, Is.SameAs(first),
                "An eligible stable instance must retain its production state.");

            BuildingEvacuationWork full = EvacuationWork(
                mine,
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(session.TryCaptureEvacuationWork(new[] { full }, out _),
                Is.True);
            Assert.That(session.TryLockEvacuationWork(new[] { full }, out _),
                Is.True);
            Synchronize(session, world, CityMode.Fortress, 12, 12);
            Assert.That(TryGetState(session, mine.StableInstanceId, out _), Is.False,
                "Evacuation-locked instances must leave the simulation set.");

            session.RollbackEvacuationLocksAfterFailure(new[] { full });
            Synchronize(session, world, CityMode.Fortress, 12, 12);
            Assert.That(TryGetState(session, mine.StableInstanceId, out _), Is.True);

            BuildingEvacuationWork abandon = EvacuationWork(
                mine,
                BuildingEvacuationTreatment.Abandon);
            Assert.That(session.TryCaptureEvacuationWork(new[] { abandon }, out _),
                Is.True);
            Assert.That(session.TryCommitEvacuation(
                abandon,
                presentation,
                out _,
                out string abandonFailure), Is.True, abandonFailure);
            Synchronize(session, world, CityMode.Fortress, 12, 12);

            Assert.That(mine.State,
                Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(mine.IsPlayerOwned, Is.False);
            Assert.That(TryGetState(session, mine.StableInstanceId, out _), Is.False,
                "Abandoned ruins must never re-enter production.");
        }

        [Test]
        public void IDEA0011_Logistics_DerivesFromCityCellModeRangeAndMobilityWithoutChains()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            Begin(
                session,
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                CityMode.Fortress,
                9,
                10,
                null,
                presentation,
                out _);
            const int nodeX = 11;
            const int nodeY = 10;
            GrayboxBuildingInstance3D mine = Begin(
                session,
                BuildingCatalog.MiningStation,
                BuildingSite.Ground,
                CityMode.Fortress,
                11,
                10,
                GrayboxResourceNodeIdentity3D.Create(nodeX, nodeY),
                presentation,
                out _);
            session.CompleteAllConstructionForDevelopment(presentation);
            WorldMapModel world = WorldWithIronNode(nodeX, nodeY, 20);

            Assert.That(
                BuildingRangeRules.IsGroundCellInRange(2, 10, 10, 11,
                    session.GroundBuildRadius),
                Is.True,
                "The adjacent warehouse footprint is inside the city range.");
            Assert.That(
                BuildingRangeRules.IsGroundCellInRange(2, 10, 11, 10,
                    session.GroundBuildRadius),
                Is.False,
                "The mine footprint starts outside the city range.");

            Synchronize(session, world, CityMode.Fortress, 2, 10);
            Assert.That(TryGetState(session, mine.StableInstanceId, out var state),
                Is.True);
            Assert.That(RequiredProperty<bool>(state, "LogisticsConnected"),
                Is.False,
                "An adjacent connected warehouse must not relay logistics range.");

            Synchronize(session, world, CityMode.Fortress, 4, 10);
            Assert.That(RequiredProperty<bool>(state, "LogisticsConnected"),
                Is.True,
                "Every footprint cell is now inside the existing city range.");

            Synchronize(session, world, CityMode.Mobile, 4, 10);
            Assert.That(RequiredProperty<bool>(state, "LogisticsConnected"),
                Is.False,
                "Ground production must also consume BuildingMobilityRules.CanOperate.");
        }

        [Test]
        public void IDEA0011_WarehouseCompletionAndRemoval_AreCapacitySafeAndAtomic()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            int baseCapacity = session.Inventory.CapacityPerResource;
            int baseRadius = session.GroundBuildRadius;
            GrayboxBuildingInstance3D warehouse = Begin(
                session,
                BuildingCatalog.Warehouse,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                null,
                presentation,
                out _);

            Assert.That(session.Inventory.CapacityPerResource,
                Is.EqualTo(baseCapacity));
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(
                0.1f,
                CityMode.Fortress,
                false,
                presentation);
            Assert.That(session.Inventory.CapacityPerResource,
                Is.EqualTo(baseCapacity + 150));
            Assert.That(session.GroundBuildRadius, Is.EqualTo(baseRadius),
                "Warehouses increase capacity, never logistics distance.");
            session.CompleteAllConstructionForDevelopment(presentation);
            Assert.That(session.Inventory.CapacityPerResource,
                Is.EqualTo(baseCapacity + 150),
                "Completion replay must not apply warehouse capacity twice.");

            session.Inventory.Set(ResourceIds.Iron, baseCapacity + 100);
            BuildingEvacuationWork full = EvacuationWork(
                warehouse,
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(session.TryCaptureEvacuationWork(new[] { full }, out _),
                Is.True);
            Assert.That(session.TryLockEvacuationWork(new[] { full }, out _),
                Is.True);

            Assert.That(session.TryCommitEvacuation(
                full,
                presentation,
                out int rejectedRefund,
                out string failureReason), Is.False);
            Assert.That(rejectedRefund, Is.Zero);
            Assert.That(failureReason, Is.Not.Empty);
            Assert.That(session.Inventory.CapacityPerResource,
                Is.EqualTo(baseCapacity + 150));
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(baseCapacity + 100),
                "A rejected removal must not clamp or destroy resources.");
            Assert.That(session.Instances.Contains(warehouse), Is.True);
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
            Assert.That(presentation.Removed, Is.Empty);

            session.Inventory.Set(ResourceIds.Iron, baseCapacity);
            Assert.That(session.TryCommitEvacuation(
                full,
                presentation,
                out _,
                out failureReason), Is.True, failureReason);
            Assert.That(session.Inventory.CapacityPerResource,
                Is.EqualTo(baseCapacity));
            Assert.That(session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(baseCapacity));
            Assert.That(session.Instances.Contains(warehouse), Is.False);
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.False);
        }

        [Test]
        public void IDEA0011_RuntimeState_DoesNotChangeFormalSaveSchemaThirty()
        {
            var data = new FormalSaveData();

            Assert.That(data.schema, Is.EqualTo(30));
            Assert.That(
                FormalSaveCodec.Decode(FormalSaveCodec.Encode(data)).schema,
                Is.EqualTo(30));
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var gameObject = new GameObject(
                "graybox-building-production-session-test");
            cleanup.Add(gameObject);
            GrayboxBuildingSession3D session =
                gameObject.AddComponent<GrayboxBuildingSession3D>();
            session.Configure(true);
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static GrayboxBuildingInstance3D Begin(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            CityMode mode,
            int x,
            int y,
            string compatibleNodeId,
            IGrayboxBuildingPresentation3D presentation,
            out BuildingPlacementEvaluation evaluation)
        {
            BuildingGrid grid = site == BuildingSite.InnerCity
                ? session.InnerGrid
                : session.GroundGrid;
            var request = new BuildingPlacementRequest(
                definition,
                grid,
                site,
                BuildingOrientation.North,
                x,
                y,
                12,
                12,
                session.GroundBuildRadius,
                mode,
                true,
                false,
                true,
                true,
                !definition.RequiresResourceNode ||
                !string.IsNullOrWhiteSpace(compatibleNodeId),
                compatibleNodeId,
                true,
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    session.IsResearchCompleted,
                    session.CompletedBuildingCount),
                session.Inventory.CanSpend(definition.CostId, definition.Cost));

            Assert.That(session.TryBeginConstruction(
                request,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out evaluation), Is.True, evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static BuildingEvacuationWork EvacuationWork(
            GrayboxBuildingInstance3D instance,
            BuildingEvacuationTreatment treatment)
        {
            return BuildingEvacuationRules.Create(
                instance.StableInstanceId,
                instance.Placement.Definition.Cost,
                instance.Progress.BaseDuration,
                1d,
                treatment);
        }

        private static WorldMapModel WorldWithIronNode(
            int nodeX,
            int nodeY,
            int amount)
        {
            var cells = new WorldCell[
                GrayboxWorldLayout3D.WorldWidth,
                GrayboxWorldLayout3D.WorldHeight];
            cells[nodeX, nodeY] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                amount);
            return new WorldMapModel(cells);
        }

        private static void Synchronize(
            GrayboxBuildingSession3D session,
            WorldMapModel world,
            CityMode mode,
            int cityX,
            int cityY)
        {
            MethodInfo method = typeof(GrayboxBuildingSession3D).GetMethod(
                SynchronizeMethodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(WorldMapModel),
                    typeof(CityMode),
                    typeof(int),
                    typeof(int)
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Required session contract: void SynchronizeProductionRuntime(" +
                "WorldMapModel, CityMode, int cityX, int cityY).");
            method.Invoke(session, new object[] { world, mode, cityX, cityY });
        }

        private static IList ProductionStates(GrayboxBuildingSession3D session)
        {
            PropertyInfo property = typeof(GrayboxBuildingSession3D).GetProperty(
                "ProductionStates",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "Required session contract: a public read-only ProductionStates view.");
            var values = property.GetValue(session) as IList;
            Assert.That(values, Is.Not.Null);
            return values;
        }

        private static bool TryGetState(
            GrayboxBuildingSession3D session,
            string stableInstanceId,
            out GrayboxBuildingProductionState3D state)
        {
            MethodInfo method = typeof(GrayboxBuildingSession3D).GetMethod(
                "TryGetProductionState",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string),
                    typeof(GrayboxBuildingProductionState3D).MakeByRefType()
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Required session contract: bool TryGetProductionState(" +
                "string, out GrayboxBuildingProductionState3D).");
            object[] arguments = { stableInstanceId, null };
            bool result = (bool)method.Invoke(session, arguments);
            state = arguments[1] as GrayboxBuildingProductionState3D;
            return result;
        }

        private static T RequiredProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                $"Required public property: {target.GetType().Name}.{propertyName}.");
            return (T)property.GetValue(target);
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public List<GrayboxBuildingInstance3D> Removed { get; } =
                new List<GrayboxBuildingInstance3D>();

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                Removed.Add(instance);
            }
        }
    }
}
