using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxEvacuationTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [TestCase(BuildingEvacuationTreatment.Abandon, 0, 0f)]
        [TestCase(BuildingEvacuationTreatment.FullDismantle, 80, 5f)]
        [TestCase(BuildingEvacuationTreatment.QuickDismantle, 50, 0f)]
        public void Rules_CompletedWorkUsesApprovedHandlingAndDuration(
            BuildingEvacuationTreatment treatment,
            int refund,
            float seconds)
        {
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                "building.instance.000007",
                100,
                10f,
                1d,
                treatment);

            Assert.That(work.Treatment, Is.EqualTo(treatment));
            Assert.That(work.RemainingRatio, Is.EqualTo(1d));
            Assert.That(work.Refund, Is.EqualTo(refund));
            Assert.That(work.DismantleSeconds, Is.EqualTo(seconds));
        }

        [Test]
        public void Rules_IncompleteWorkUsesRemainingRatioBeforeAwayFromZeroHandling()
        {
            BuildingEvacuationWork full = BuildingEvacuationRules.Create(
                "building.instance.000008",
                25,
                11f,
                .5d,
                BuildingEvacuationTreatment.FullDismantle);
            BuildingEvacuationWork quick = BuildingEvacuationRules.Create(
                "building.instance.000009",
                25,
                11f,
                .5d,
                BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(full.Refund, Is.EqualTo(10));
            Assert.That(full.DismantleSeconds, Is.EqualTo(5.5f));
            Assert.That(quick.Refund, Is.EqualTo(6));
            Assert.That(quick.DismantleSeconds, Is.Zero);
        }

        [Test]
        public void Rules_ClampRatiosAndSortFullQueueByOrdinalStableInstanceId()
        {
            BuildingEvacuationWork low = BuildingEvacuationRules.Create(
                "building.instance.000010",
                10,
                4f,
                -1d,
                BuildingEvacuationTreatment.QuickDismantle);
            BuildingEvacuationWork high = BuildingEvacuationRules.Create(
                "building.instance.000002",
                10,
                4f,
                2d,
                BuildingEvacuationTreatment.FullDismantle);
            BuildingEvacuationWork middle = BuildingEvacuationRules.Create(
                "building.instance.000001",
                10,
                4f,
                1d,
                BuildingEvacuationTreatment.FullDismantle);

            IReadOnlyList<BuildingEvacuationWork> queue =
                BuildingEvacuationRules.CreateStableFullDismantleQueue(
                    new[] { high, low, middle });

            Assert.That(low.Refund, Is.Zero);
            Assert.That(high.Refund, Is.EqualTo(8));
            Assert.That(queue, Has.Count.EqualTo(2));
            Assert.That(queue[0].StableInstanceId,
                Is.EqualTo("building.instance.000001"));
            Assert.That(queue[1].StableInstanceId,
                Is.EqualTo("building.instance.000002"));
        }

        [Test]
        public void Session_LockingFullWorkRemovesCompletedPrerequisiteUntilRollback()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D wall = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                10,
                10,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);
            uint revisionBefore = session.CatalogRevision;
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                wall.StableInstanceId,
                wall.Placement.Definition.Cost,
                wall.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.FullDismantle);

            Assert.That(session.TryLockEvacuationWork(
                new[] { work }, out string failure), Is.True, failure);
            Assert.That(wall.IsEvacuationLocked, Is.True);
            Assert.That(session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.Zero);
            Assert.That(session.CatalogRevision, Is.EqualTo(revisionBefore + 1));

            session.RollbackEvacuationLocksAfterFailure(new[] { work });

            Assert.That(wall.IsEvacuationLocked, Is.False);
            Assert.That(session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.EqualTo(1));
            Assert.That(session.CatalogRevision, Is.EqualTo(revisionBefore + 2));
        }

        [Test]
        public void Session_AbandonLeavesNonOwnedBlockingRuinWithZeroRefund()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D wall = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                10,
                10,
                presentation);
            int stoneBefore = session.Inventory.Get(BuildingCatalog.Wall.CostId);
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                wall.StableInstanceId,
                wall.Placement.Definition.Cost,
                wall.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.Abandon);

            Assert.That(session.TryCommitEvacuation(
                work, presentation, out int refund, out string failure),
                Is.True, failure);
            Assert.That(refund, Is.Zero);
            Assert.That(wall.State, Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(wall.IsPlayerOwned, Is.False);
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
            Assert.That(session.Inventory.Get(BuildingCatalog.Wall.CostId),
                Is.EqualTo(stoneBefore));
        }

        [Test]
        public void Controller_InterceptsOwnedGroundAndQuicklyResumesExistingPacking()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D ground = Begin(
                fixture.Session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                10,
                10,
                fixture.Presentation);
            Begin(
                fixture.Session,
                BuildingCatalog.Housing,
                BuildingSite.InnerCity,
                1,
                1,
                fixture.Presentation);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.IsManifestOpen, Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.QuickDismantle), Is.EqualTo(1));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);

            Assert.That(fixture.Session.Instances.Contains(ground), Is.False);
            Assert.That(fixture.Session.HasPlayerOwnedGroundInstances, Is.False);
            Assert.That(fixture.Session.Instances, Has.Count.EqualTo(1));
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
        }

        [Test]
        public void Controller_LocksFullQueueBeforeTimerAndPauseDoesNotAdvanceIt()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D ground = Begin(
                fixture.Session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                10,
                10,
                fixture.Presentation);
            fixture.Session.SetConstructionMultiplierForDevelopment(100f);
            fixture.Session.TickConstruction(.1f, CityMode.Fortress, false,
                fixture.Presentation);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                ground.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            Assert.That(ground.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Session.CompletedBuildingCount(
                BuildingCatalog.Wall.Id.Value), Is.Zero);

            fixture.Controller.Tick(10f, true);
            Assert.That(fixture.Session.Instances.Contains(ground), Is.True);
            fixture.Session.TickConstruction(10f, CityMode.Fortress, false,
                fixture.Presentation);
            Assert.That(ground.IsEvacuationLocked, Is.True);
            fixture.Controller.Tick(10f, false);

            Assert.That(fixture.Session.Instances.Contains(ground), Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
        }

        private GrayboxBuildingSession3D CreateSession()
        {
            var gameObject = new GameObject("graybox-evacuation-test");
            cleanup.Add(gameObject);
            var session = gameObject.AddComponent<GrayboxBuildingSession3D>();
            session.Configure(true);
            session.ConfigureDevelopmentFixture();
            return session;
        }

        private EvacuationFixture CreateFixture()
        {
            var session = CreateSession();
            var cityObject = new GameObject("evacuation-city");
            cleanup.Add(cityObject);
            var city = cityObject.AddComponent<WasteCity.Graybox3D.GrayboxMobileCityController3D>();
            city.Deployment.Restore(CityMode.Fortress, 0f);

            var presentationObject = new GameObject("evacuation-presentation");
            cleanup.Add(presentationObject);
            var presentation = presentationObject.AddComponent<GrayboxBuildingWorldView3D>();
            var instanceRoot = new GameObject("instances");
            var infrastructureRoot = new GameObject("infrastructure");
            cleanup.Add(instanceRoot);
            cleanup.Add(infrastructureRoot);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Standard");
            var material = new Material(shader);
            presentation.Configure(
                instanceRoot.transform,
                infrastructureRoot.transform,
                material,
                city);

            var menuObject = new GameObject("evacuation-menu");
            cleanup.Add(menuObject);
            var menu = menuObject.AddComponent<GrayboxBuildingMenuView3D>();
            var controllerObject = new GameObject("evacuation-controller");
            cleanup.Add(controllerObject);
            var controller = controllerObject.AddComponent<GrayboxEvacuationController3D>();
            controller.Configure(session, city, presentation, menu);
            return new EvacuationFixture(session, city, presentation, controller);
        }

        private static GrayboxBuildingInstance3D Begin(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            IGrayboxBuildingPresentation3D presentation)
        {
            BuildingGrid grid = site == BuildingSite.Ground
                ? session.GroundGrid
                : session.InnerGrid;
            var request = new BuildingPlacementRequest(
                definition, grid, site, BuildingOrientation.North, x, y,
                12, 12, session.GroundBuildRadius, CityMode.Fortress,
                true, false, true, true, !definition.RequiresResourceNode,
                definition.RequiresResourceNode ? "test.node" : null,
                true, BuildingUnlockModel.Evaluate(definition,
                    session.Population, session.IsResearchCompleted,
                    session.CompletedBuildingCount),
                session.Inventory.CanSpend(definition.CostId, definition.Cost));
            Assert.That(session.TryBeginConstruction(
                request, presentation, out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True, evaluation.PrimaryFailure.ToString());
            return instance;
        }

        private sealed class RecordingPresentation : IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }

        private readonly struct EvacuationFixture
        {
            public EvacuationFixture(
                GrayboxBuildingSession3D session,
                WasteCity.Graybox3D.GrayboxMobileCityController3D city,
                GrayboxBuildingWorldView3D presentation,
                GrayboxEvacuationController3D controller)
            {
                Session = session;
                City = city;
                Presentation = presentation;
                Controller = controller;
            }

            public GrayboxBuildingSession3D Session { get; }
            public WasteCity.Graybox3D.GrayboxMobileCityController3D City { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public GrayboxEvacuationController3D Controller { get; }
        }
    }
}
