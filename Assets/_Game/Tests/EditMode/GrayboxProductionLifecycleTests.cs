using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionLifecycleTests
    {
        private readonly List<GameObject> roots = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = roots.Count - 1; index >= 0; index--)
                Object.DestroyImmediate(roots[index]);
            roots.Clear();
        }

        [Test]
        public void MiningBindingFlowsFromRequestThroughEvaluationIntoSessionInstance()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var binding = new ResourceNodeBinding(
                "world.resource-node.5.5",
                5,
                5);
            BuildingPlacementRequest request = Request(
                session,
                BuildingCatalog.MiningStation,
                binding,
                x: 4,
                y: 4);

            bool accepted = session.TryBeginConstruction(
                request,
                new RecordingPresentation(),
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(accepted, Is.True, evaluation.PrimaryFailure.ToString());
            AssertBinding(evaluation.CompatibleResourceNode, binding);
            AssertBinding(instance.BoundResourceNode, binding);
        }

        [Test]
        public void NonMiningPlacementClearsAnOtherwiseValidResourceNodeBinding()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var unexpected = new ResourceNodeBinding(
                "world.resource-node.5.5",
                5,
                5);

            bool accepted = session.TryBeginConstruction(
                Request(session, BuildingCatalog.Wall, unexpected, 4, 4),
                new RecordingPresentation(),
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(accepted, Is.True, evaluation.PrimaryFailure.ToString());
            AssertBinding(evaluation.CompatibleResourceNode, ResourceNodeBinding.None);
            AssertBinding(instance.BoundResourceNode, ResourceNodeBinding.None);
        }

        [Test]
        public void MiningRequestWithAnIdButInvalidCoordinatesFailsWithoutSpending()
        {
            GrayboxBuildingSession3D session = CreateSession();
            int alloyBefore = session.Inventory.Get(
                BuildingCatalog.MiningStation.CostId);
            var invalid = new ResourceNodeBinding(
                "world.resource-node.5.5",
                -1,
                5);
            var presentation = new RecordingPresentation();

            bool accepted = session.TryBeginConstruction(
                Request(session, BuildingCatalog.MiningStation, invalid, 4, 4),
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(invalid.StableId, Is.Not.Empty);
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(accepted, Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(evaluation.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.IncompatibleResourceNode));
            Assert.That(session.Inventory.Get(BuildingCatalog.MiningStation.CostId),
                Is.EqualTo(alloyBefore));
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);
            Assert.That(presentation.Created, Is.Zero);
        }

        [Test]
        public void LegacyIdOnlyMiningRequestCannotCommitWithoutNodeCoordinates()
        {
            GrayboxBuildingSession3D session = CreateSession();
            int alloyBefore = session.Inventory.Get(
                BuildingCatalog.MiningStation.CostId);
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                BuildingCatalog.MiningStation,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            var legacyRequest = new BuildingPlacementRequest(
                BuildingCatalog.MiningStation,
                session.GroundGrid,
                BuildingSite.Ground,
                BuildingOrientation.North,
                4,
                4,
                12,
                12,
                session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                coversCompatibleResourceNode: true,
                compatibleResourceNodeId: "world.resource-node.5.5",
                contentVisible: true,
                unlock: unlock,
                canAfford: true);

            bool accepted = session.TryBeginConstruction(
                legacyRequest,
                new RecordingPresentation(),
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(evaluation.IsValid, Is.True,
                "Legacy evaluation compatibility remains intact.");
            Assert.That(evaluation.CompatibleResourceNode.IsValid, Is.False);
            Assert.That(accepted, Is.False);
            Assert.That(instance, Is.Null);
            Assert.That(session.Inventory.Get(
                    BuildingCatalog.MiningStation.CostId),
                Is.EqualTo(alloyBefore));
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);
        }

        [TestCase(BuildingOrientation.North)]
        [TestCase(BuildingOrientation.East)]
        public void GroundFootprintRangeMatchesTheExistingRuleForEveryRotatedCell(
            BuildingOrientation orientation)
        {
            BuildingDefinition definition = BuildingCatalog.BehemothPen;
            const int cityX = 0;
            const int cityY = 0;
            const int x = 6;
            const int y = 7;
            const int radius = BuildingRangeRules.InitialGroundRadius;
            bool expected = true;
            int width = BuildingOrientationRules.Width(definition, orientation);
            int height = BuildingOrientationRules.Height(definition, orientation);
            for (var dx = 0; dx < width; dx++)
            for (var dy = 0; dy < height; dy++)
                expected &= BuildingRangeRules.IsGroundCellInRange(
                    cityX,
                    cityY,
                    x + dx,
                    y + dy,
                    radius);

            bool actual = BuildingRangeRules.IsGroundFootprintInRange(
                definition: definition,
                x: x,
                y: y,
                orientation: orientation,
                cityX: cityX,
                cityY: cityY,
                radius: radius);

            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(actual,
                Is.EqualTo(orientation == BuildingOrientation.North));
        }

        [Test]
        public void ActiveWarehouseRequiresCompletedOwnedAndUnlockedButNotLogistics()
        {
            GrayboxBuildingSession3D session = CreateSession();
            GrayboxBuildingInstance3D warehouse = BeginWarehouse(session, 4, 4);

            Assert.That(
                GrayboxProductionEligibility3D.IsActiveWarehouse(warehouse),
                Is.False,
                "under-construction warehouses must not add capacity");

            session.TickConstruction(
                BuildingCatalog.Warehouse.BuildSeconds,
                CityMode.Fortress,
                paused: false,
                presentation: new RecordingPresentation());
            Assert.That(warehouse.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(
                GrayboxProductionEligibility3D.IsActiveWarehouse(warehouse),
                Is.True,
                "logistics connectivity is deliberately not an eligibility input");

            SetEvacuationLocked(warehouse, true);
            Assert.That(
                GrayboxProductionEligibility3D.IsActiveWarehouse(warehouse),
                Is.False);

            SetEvacuationLocked(warehouse, false);
            Abandon(warehouse);
            Assert.That(
                GrayboxProductionEligibility3D.IsActiveWarehouse(warehouse),
                Is.False);
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var root = new GameObject("GrayboxProductionLifecycleTests.Session");
            roots.Add(root);
            GrayboxBuildingSession3D session =
                root.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private static GrayboxBuildingInstance3D BeginWarehouse(
            GrayboxBuildingSession3D session,
            int x,
            int y)
        {
            Assert.That(
                session.TryBeginConstruction(
                    Request(
                        session,
                        BuildingCatalog.Warehouse,
                        ResourceNodeBinding.None,
                        x,
                        y),
                    new RecordingPresentation(),
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static BuildingPlacementRequest Request(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            ResourceNodeBinding compatibleResourceNode,
            int x,
            int y)
        {
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            return new BuildingPlacementRequest(
                definition: definition,
                grid: session.GroundGrid,
                site: BuildingSite.Ground,
                orientation: BuildingOrientation.North,
                x: x,
                y: y,
                cityX: 12,
                cityY: 12,
                groundRadius: session.GroundBuildRadius,
                cityMode: CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                compatibleResourceNode: compatibleResourceNode,
                contentVisible: true,
                unlock: unlock,
                canAfford: session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost));
        }

        private static void AssertBinding(
            ResourceNodeBinding actual,
            ResourceNodeBinding expected)
        {
            Assert.That(actual.StableId, Is.EqualTo(expected.StableId));
            Assert.That(actual.X, Is.EqualTo(expected.X));
            Assert.That(actual.Y, Is.EqualTo(expected.Y));
            Assert.That(actual.IsValid, Is.EqualTo(expected.IsValid));
        }

        private static void SetEvacuationLocked(
            GrayboxBuildingInstance3D instance,
            bool value)
        {
            InvokeInstanceMethod(instance, "SetEvacuationLocked", value);
        }

        private static void Abandon(GrayboxBuildingInstance3D instance)
        {
            InvokeInstanceMethod(instance, "Abandon");
        }

        private static void InvokeInstanceMethod(
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

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public int Created { get; private set; }

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                Created++;
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
