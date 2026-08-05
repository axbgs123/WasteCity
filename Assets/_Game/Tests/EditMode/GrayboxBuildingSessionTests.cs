using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxBuildingSessionTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void DevelopmentFixture_UsesApprovedFiniteModelsAndEmptyUnlockState()
        {
            GrayboxBuildingSession3D session = CreateSession();

            Assert.That(session.DevelopmentFixtureEnabled, Is.True);
            Assert.That(session.Inventory.CapacityPerResource, Is.EqualTo(5000));
            Assert.That(session.Population, Is.EqualTo(200));
            Assert.That(session.GroundGrid.Width, Is.EqualTo(32));
            Assert.That(session.GroundGrid.Height, Is.EqualTo(24));
            Assert.That(session.InnerGrid.Width, Is.EqualTo(8));
            Assert.That(session.InnerGrid.Height, Is.EqualTo(6));
            Assert.That(session.GroundBuildRadius, Is.EqualTo(8));
            Assert.That(session.ConstructionMultiplier, Is.EqualTo(1f));
            Assert.That(session.Instances, Is.Empty);
            Assert.That(session.Research.CompletedCount, Is.Zero);
            Assert.That(session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value), Is.Zero);
            Assert.That(session.HasContactedRoute(ContentRoute.Technology), Is.False);
            Assert.That(session.HasContactedRoute(ContentRoute.Cultivation), Is.False);
            Assert.That(session.HasContactedRoute(ContentRoute.BiologicalAscension), Is.False);
            Assert.That(session.HasContactedRoute(ContentRoute.Psionics), Is.False);
        }

        [TestCase(ResourceIds.Iron, 30)]
        [TestCase(ResourceIds.EnergyCrystal, 10)]
        [TestCase(ResourceIds.Stone, 30)]
        [TestCase(ResourceIds.Biomass, 20)]
        [TestCase(ResourceIds.Water, 20)]
        [TestCase(ResourceIds.Alloy, 30)]
        [TestCase(ResourceIds.Ammunition, 0)]
        [TestCase(ResourceIds.SpiritIron, 0)]
        [TestCase(ResourceIds.FlyingSword, 0)]
        [TestCase(ResourceIds.BoneSteel, 0)]
        [TestCase(ResourceIds.BiomassConcentrate, 0)]
        [TestCase(ResourceIds.BiologicalWeapon, 0)]
        [TestCase(ResourceIds.ResonanceMetal, 0)]
        [TestCase(ResourceIds.PsionicAmplifier, 0)]
        [TestCase(ResourceIds.Elixir, 0)]
        public void DevelopmentFixture_UsesExactApprovedResourceAmount(
            string resourceId,
            int expectedAmount)
        {
            Assert.That(CreateSession().Inventory.Get(resourceId), Is.EqualTo(expectedAmount));
        }

        [Test]
        public void DevelopmentUnlockMethods_MutateTheSessionResearchAndRouteModels()
        {
            GrayboxBuildingSession3D session = CreateSession();
            const string automatedMachinery = "core.research.automated-machinery";

            session.UnlockResearchForDevelopment(automatedMachinery);

            Assert.That(session.IsResearchCompleted(automatedMachinery), Is.True);
            Assert.That(session.HasContactedRoute(ContentRoute.Technology), Is.False);

            session.UnlockRouteForDevelopment(ContentRoute.Technology);

            Assert.That(session.HasContactedRoute(ContentRoute.Technology), Is.True);
            Assert.That(
                session.IsResearchCompleted("core.research.energy-weapons"),
                Is.True);

            session.UnlockAllResearchForDevelopment();

            Assert.That(session.Research.CompletedCount, Is.EqualTo(ResearchCatalog.All.Length));
            Assert.That(session.HasContactedRoute(ContentRoute.Cultivation), Is.True);
            Assert.That(session.HasContactedRoute(ContentRoute.BiologicalAscension), Is.True);
            Assert.That(session.HasContactedRoute(ContentRoute.Psionics), Is.True);

            session.SetRouteContact(ContentRoute.Technology, false);
            Assert.That(session.HasContactedRoute(ContentRoute.Technology), Is.False);
        }

        [Test]
        public void TryBeginConstruction_CommitsOneSpendOneFootprintAndStableInstance()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            BuildingPlacementRequest request = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);
            int stoneBefore = session.Inventory.Get(ResourceIds.Stone);

            bool result = session.TryBeginConstruction(
                request,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(result, Is.True);
            Assert.That(evaluation.IsValid, Is.True);
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.StableInstanceId, Is.EqualTo("building.instance.000001"));
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(instance.IsPlayerOwned, Is.True);
            Assert.That(instance.IsEvacuationLocked, Is.False);
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.EqualTo(stoneBefore - 2));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(1));
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
            Assert.That(session.Instances, Is.EqualTo(new[] { instance }));
            Assert.That(presentation.Created, Is.EqualTo(new[] { instance }));
        }

        [Test]
        public void TryBeginConstruction_RechecksInventoryAndLeavesAllStateUntouched()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            BuildingPlacementRequest staleRequest = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);
            session.Inventory.Set(ResourceIds.Stone, 0);

            bool result = session.TryBeginConstruction(
                staleRequest,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(result, Is.False);
            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(BuildingPlacementFailure.InsufficientMaterials));
            Assert.That(instance, Is.Null);
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.Zero);
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);
            Assert.That(presentation.Created, Is.Empty);
        }

        [Test]
        public void TryBeginConstruction_RechecksGridBeforeSpending()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            BuildingPlacementRequest staleRequest = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);
            Assert.That(
                session.GroundGrid.TryRestore(
                    BuildingCatalog.Wall,
                    10,
                    10,
                    out _),
                Is.True);
            int stoneBefore = session.Inventory.Get(ResourceIds.Stone);

            bool result = session.TryBeginConstruction(
                staleRequest,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(result, Is.False);
            Assert.That(evaluation.PrimaryFailure, Is.EqualTo(BuildingPlacementFailure.Overlap));
            Assert.That(instance, Is.Null);
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.EqualTo(stoneBefore));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(1));
            Assert.That(session.Instances, Is.Empty);
            Assert.That(presentation.Created, Is.Empty);
        }

        [Test]
        public void TryBeginConstruction_PresentationFalseRollsBackAndRetryReusesNextStableId()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation { CreateResult = false };
            BuildingPlacementRequest request = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);
            int stoneBefore = session.Inventory.Get(ResourceIds.Stone);

            Assert.That(
                session.TryBeginConstruction(request, presentation, out _, out _),
                Is.False);
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.EqualTo(stoneBefore));
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);

            presentation.CreateResult = true;
            Assert.That(
                session.TryBeginConstruction(
                    request,
                    presentation,
                    out GrayboxBuildingInstance3D retry,
                    out _),
                Is.True);
            Assert.That(retry.StableInstanceId, Is.EqualTo("building.instance.000001"));
            Assert.That(session.Instances, Is.EqualTo(new[] { retry }));
        }

        [Test]
        public void TryBeginConstruction_PresentationExceptionRollsBackThenRethrows()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation
            {
                CreateException = new InvalidOperationException("presentation failed")
            };
            BuildingPlacementRequest request = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);
            int stoneBefore = session.Inventory.Get(ResourceIds.Stone);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                session.TryBeginConstruction(request, presentation, out _, out _));

            Assert.That(thrown.Message, Is.EqualTo("presentation failed"));
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.EqualTo(stoneBefore));
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);

            presentation.CreateException = null;
            Assert.That(
                session.TryBeginConstruction(
                    request,
                    presentation,
                    out GrayboxBuildingInstance3D retry,
                    out _),
                Is.True);
            Assert.That(retry.StableInstanceId, Is.EqualTo("building.instance.000001"));
        }

        [Test]
        public void TickConstruction_PausedAndIllegalModeDoNotAdvance()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            float before = instance.Progress.Remaining;

            session.TickConstruction(1f, CityMode.Fortress, true, presentation);
            session.TickConstruction(1f, CityMode.Mobile, false, presentation);

            Assert.That(instance.Progress.Remaining, Is.EqualTo(before));
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(presentation.Updated, Is.Empty);
        }

        [Test]
        public void TickConstruction_AdvancesMobileInnerAndFortressGround()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D inner = Begin(
                session,
                BuildingCatalog.Housing,
                BuildingSite.InnerCity,
                CityMode.Mobile,
                0,
                0,
                presentation);
            GrayboxBuildingInstance3D ground = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);

            session.TickConstruction(1f, CityMode.Mobile, false, presentation);
            Assert.That(inner.Progress.Remaining, Is.EqualTo(4f));
            Assert.That(ground.Progress.Remaining, Is.EqualTo(2f));

            session.TickConstruction(1f, CityMode.Fortress, false, presentation);
            Assert.That(inner.Progress.Remaining, Is.EqualTo(3f));
            Assert.That(ground.Progress.Remaining, Is.EqualTo(1f));
        }

        [TestCase(1f, 9f)]
        [TestCase(10f, 0f)]
        [TestCase(100f, 0f)]
        public void TickConstruction_UsesDevelopmentMultiplier(
            float multiplier,
            float expectedRemaining)
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.ResearchStation,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            session.SetConstructionMultiplierForDevelopment(multiplier);

            session.TickConstruction(1f, CityMode.Fortress, false, presentation);

            Assert.That(instance.Progress.Remaining, Is.EqualTo(expectedRemaining));
        }

        [Test]
        public void TickConstruction_CompletesSameStableInstanceAndUpdatesPresentationOnce()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            string stableId = instance.StableInstanceId;

            session.TickConstruction(2f, CityMode.Fortress, false, presentation);
            session.TickConstruction(2f, CityMode.Fortress, false, presentation);

            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.Progress.IsComplete, Is.True);
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { instance }));
        }

        [Test]
        public void CompleteAllConstructionForDevelopment_CompletesEveryPendingInstance()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D ground = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            GrayboxBuildingInstance3D inner = Begin(
                session,
                BuildingCatalog.Housing,
                BuildingSite.InnerCity,
                CityMode.Mobile,
                0,
                0,
                presentation);

            session.CompleteAllConstructionForDevelopment(presentation);
            session.CompleteAllConstructionForDevelopment(presentation);

            Assert.That(ground.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(inner.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { ground, inner }));
        }

        [Test]
        public void CompletedBuildingCount_CountsOnlyMatchingCompletedGroundAndInnerInstances()
        {
            GrayboxBuildingSession3D session = CreateSession();
            session.Inventory.Set(ResourceIds.Alloy, 100);
            var presentation = new RecordingPresentation();
            Begin(
                session,
                BuildingCatalog.Housing,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            Begin(
                session,
                BuildingCatalog.Housing,
                BuildingSite.InnerCity,
                CityMode.Mobile,
                0,
                0,
                presentation);
            Begin(
                session,
                BuildingCatalog.Housing,
                BuildingSite.Ground,
                CityMode.Fortress,
                14,
                10,
                presentation);
            Begin(
                session,
                BuildingCatalog.Warehouse,
                BuildingSite.InnerCity,
                CityMode.Mobile,
                3,
                0,
                presentation);

            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Mobile, false, presentation);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);

            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Housing.Id.Value),
                Is.EqualTo(3));
            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Warehouse.Id.Value),
                Is.EqualTo(1));
            Assert.That(session.CompletedBuildingCount(null), Is.Zero);
            Assert.That(session.CompletedBuildingCount(string.Empty), Is.Zero);
            Assert.That(session.CompletedBuildingCount("unknown.building"), Is.Zero);
        }

        [TestCase(1, .49d, 1d, 0)]
        [TestCase(1, .5d, 1d, 1)]
        [TestCase(1, .51d, 1d, 1)]
        [TestCase(10, 1d, 1d, 10)]
        [TestCase(10, 1d, .8d, 8)]
        [TestCase(10, 1d, .5d, 5)]
        [TestCase(10, 1d, 0d, 0)]
        [TestCase(10, -1d, 1d, 0)]
        [TestCase(10, 2d, 1d, 10)]
        [TestCase(10, 1d, 2d, 10)]
        [TestCase(-10, 1d, 1d, 0)]
        public void ConstructionRefundRules_UsesApprovedRoundingAndClamps(
            int originalCost,
            double remainingRatio,
            double handlingRatio,
            int expected)
        {
            Assert.That(
                ConstructionRefundRules.Calculate(
                    originalCost,
                    remainingRatio,
                    handlingRatio),
                Is.EqualTo(expected));
        }

        [Test]
        public void TryCancelConstruction_RefundsOnlyAcceptedCapacityAndRemovesGridAndView()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Housing,
                BuildingSite.InnerCity,
                CityMode.Mobile,
                0,
                0,
                presentation);
            session.TickConstruction(2.5f, CityMode.Mobile, false, presentation);
            session.Inventory.Set(ResourceIds.Alloy, 4998);

            bool result = session.TryCancelConstruction(
                instance.StableInstanceId,
                1d,
                presentation,
                out int acceptedRefund);

            Assert.That(result, Is.True);
            Assert.That(acceptedRefund, Is.EqualTo(2));
            Assert.That(session.Inventory.Get(ResourceIds.Alloy), Is.EqualTo(5000));
            Assert.That(session.InnerGrid.Count, Is.Zero);
            Assert.That(session.InnerGrid.IsOccupied(0, 0), Is.False);
            Assert.That(session.Instances, Is.Empty);
            Assert.That(presentation.Removed, Is.EqualTo(new[] { instance }));
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var gameObject = new GameObject("graybox-building-session-test");
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
            RecordingPresentation presentation)
        {
            Assert.That(
                session.TryBeginConstruction(
                    ValidRequest(session, definition, site, mode, x, y),
                    presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private static BuildingPlacementRequest ValidRequest(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            CityMode mode,
            int x,
            int y)
        {
            BuildingGrid grid =
                site == BuildingSite.InnerCity ? session.InnerGrid : session.GroundGrid;
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                session.Population,
                session.IsResearchCompleted,
                session.CompletedBuildingCount);
            return new BuildingPlacementRequest(
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
                !definition.RequiresResourceNode,
                definition.RequiresResourceNode ? "test.node" : null,
                true,
                unlock,
                session.Inventory.CanSpend(definition.CostId, definition.Cost));
        }

        private sealed class RecordingPresentation : IGrayboxBuildingPresentation3D
        {
            public bool CreateResult { get; set; } = true;
            public Exception CreateException { get; set; }
            public List<GrayboxBuildingInstance3D> Created { get; } =
                new List<GrayboxBuildingInstance3D>();
            public List<GrayboxBuildingInstance3D> Updated { get; } =
                new List<GrayboxBuildingInstance3D>();
            public List<GrayboxBuildingInstance3D> Removed { get; } =
                new List<GrayboxBuildingInstance3D>();

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                if (CreateException != null) throw CreateException;
                if (CreateResult) Created.Add(instance);
                return CreateResult;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                Updated.Add(instance);
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                Removed.Add(instance);
            }
        }
    }
}
