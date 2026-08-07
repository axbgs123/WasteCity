using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Graybox3D;
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
            string failure;

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out failure), Is.True, failure);
            Assert.That(session.TryLockEvacuationWork(
                new[] { work }, out failure), Is.True, failure);
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
            string failure;

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out failure), Is.True, failure);
            Assert.That(session.TryCommitEvacuation(
                work, presentation, out int refund, out failure),
                Is.True, failure);
            Assert.That(refund, Is.Zero);
            Assert.That(wall.State, Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(wall.IsPlayerOwned, Is.False);
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
            Assert.That(session.Inventory.Get(BuildingCatalog.Wall.CostId),
                Is.EqualTo(stoneBefore));
        }

        [Test]
        public void Session_QuickCommitRejectsFabricatedWorkAndConsumesOnlyCapturedSnapshot()
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
            BuildingEvacuationWork captured = BuildingEvacuationRules.Create(
                wall.StableInstanceId,
                wall.Placement.Definition.Cost,
                wall.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);
            BuildingEvacuationWork fabricated = BuildingEvacuationRules.Create(
                wall.StableInstanceId,
                wall.Placement.Definition.Cost + 100,
                wall.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { fabricated }, out string rejectedCaptureFailure),
                Is.False);
            Assert.That(rejectedCaptureFailure, Is.Not.Empty);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { captured }, out string captureFailure),
                Is.True, captureFailure);
            Assert.That(session.TryCommitEvacuation(
                fabricated, presentation, out int fabricatedRefund,
                out string failure), Is.False);
            Assert.That(fabricatedRefund, Is.Zero);
            Assert.That(failure, Is.Not.Empty);
            Assert.That(session.Instances.Contains(wall), Is.True);
            Assert.That(session.TryCommitEvacuation(
                captured, presentation, out int acceptedRefund,
                out failure), Is.True, failure);
            Assert.That(acceptedRefund, Is.EqualTo(captured.Refund));
            Assert.That(session.TryCommitEvacuation(
                captured, presentation, out _, out failure), Is.False);
            Assert.That(failure, Is.Not.Empty);
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
        public void Session_CopyPlayerOwnedGroundInstancesFiltersAndOrdersStableIds()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D firstGround = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, presentation);
            Begin(
                session, BuildingCatalog.Housing, BuildingSite.InnerCity,
                1, 1, presentation);
            GrayboxBuildingInstance3D abandonedGround = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, presentation);
            GrayboxBuildingInstance3D lastGround = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                14, 10, presentation);
            BuildingEvacuationWork abandon = BuildingEvacuationRules.Create(
                abandonedGround.StableInstanceId,
                abandonedGround.Placement.Definition.Cost,
                abandonedGround.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.Abandon);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { abandon }, out string captureFailure),
                Is.True, captureFailure);
            Assert.That(session.TryCommitEvacuation(
                abandon, presentation, out _, out string commitFailure),
                Is.True, commitFailure);
            var destination = new List<GrayboxBuildingInstance3D>
            {
                abandonedGround
            };
            ReverseSessionInstances(session);

            session.CopyPlayerOwnedGroundInstances(destination);

            Assert.That(session.HasPlayerOwnedGroundInstances, Is.True);
            Assert.That(
                destination.Select(instance => instance.StableInstanceId),
                Is.EqualTo(new[]
                {
                    firstGround.StableInstanceId,
                    lastGround.StableInstanceId
                }));
        }

        [Test]
        public void Controller_OwnedGroundKeepsFortressAndOpensFilteredManifest()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D ground = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            Begin(
                fixture.Session, BuildingCatalog.Housing,
                BuildingSite.InnerCity, 1, 1, fixture.Presentation);

            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);

            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(fixture.Controller.IsManifestOpen, Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.QuickDismantle), Is.EqualTo(1));
            Assert.That(fixture.Controller.Assign(
                ground.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
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
            Assert.That(fixture.Controller.Assign(
                ground.StableInstanceId,
                BuildingEvacuationTreatment.QuickDismantle), Is.False);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.False);
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

        [Test]
        public void Session_FullLockValidationForLaterItemLeavesEarlierItemUnlocked()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D first = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, presentation);
            GrayboxBuildingInstance3D second = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, presentation);
            BuildingEvacuationWork firstWork = BuildingEvacuationRules.Create(
                first.StableInstanceId, first.Placement.Definition.Cost,
                first.Progress.BaseDuration, 1d,
                BuildingEvacuationTreatment.FullDismantle);
            BuildingEvacuationWork invalidSecond = BuildingEvacuationRules.Create(
                second.StableInstanceId, second.Placement.Definition.Cost,
                second.Progress.BaseDuration, 1d,
                BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(session.TryCaptureEvacuationWork(
                new[] { firstWork }, out string captureFailure),
                Is.True, captureFailure);
            Assert.That(session.TryLockEvacuationWork(
                new[] { firstWork, invalidSecond }, out string failure),
                Is.False);
            Assert.That(failure, Is.Not.Empty);
            Assert.That(first.IsEvacuationLocked, Is.False);
            Assert.That(second.IsEvacuationLocked, Is.False);
        }

        [Test]
        public void Controller_UnassignedBlocksConfirmationAndNoGroundConsumesRequests()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D wall = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.False);
            Assert.That(fixture.Controller.IsManifestOpen, Is.True);
            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.QuickDismantle), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));

            EvacuationFixture emptyFixture = CreateFixture();
            Assert.That(emptyFixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(emptyFixture.City.Mode, Is.EqualTo(CityMode.Packing));
            Assert.That(emptyFixture.Controller.TryHandleDeploymentRequest(), Is.True);
        }

        [Test]
        public void Controller_ConsumesFWhenSingleNoGroundDelegationFails()
        {
            EvacuationFixture fixture = CreateFixture();
            var deployment = new DeploymentRequestSpy(
                CityMode.Fortress,
                toggleResult: false);
            fixture.Controller.Configure(
                fixture.Session,
                deployment,
                fixture.Presentation,
                fixture.Menu);

            bool consumed = fixture.Controller.TryHandleDeploymentRequest();

            Assert.That(consumed, Is.True);
            Assert.That(deployment.ToggleCalls, Is.EqualTo(1));
        }

        [Test]
        public void Controller_ManifestAndProcessingConsumeFWithoutDelegating()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D ground = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            var deployment = new DeploymentRequestSpy(
                CityMode.Fortress,
                toggleResult: false);
            fixture.Controller.Configure(
                fixture.Session,
                deployment,
                fixture.Presentation,
                fixture.Menu);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.IsManifestOpen, Is.True);
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(deployment.ToggleCalls, Is.Zero);
            Assert.That(fixture.Controller.Assign(
                ground.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            Assert.That(fixture.Controller.IsProcessing, Is.True);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(deployment.ToggleCalls, Is.Zero);
        }

        [Test]
        public void Controller_CancellationBlockTracksManifestFailureAndCompletion()
        {
            EvacuationFixture fixture = CreateFixture(configureMenu: true);
            GrayboxBuildingInstance3D ground = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            Button[] constructionButtons = ConstructionButtons(fixture.Canvas);

            AssertConstructionButtons(constructionButtons, true);
            Assert.That(fixture.Menu.ConstructionCancellationBlocked, Is.False);
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            AssertConstructionButtons(constructionButtons, false);
            Assert.That(fixture.Menu.ConstructionCancellationBlocked, Is.True);
            Assert.That(fixture.Controller.Assign(
                ground.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            Assert.That(fixture.Controller.IsProcessing, Is.True);
            AssertConstructionButtons(constructionButtons, false);
            Assert.That(fixture.Menu.ConstructionCancellationBlocked, Is.True);
            SetEvacuationPresentation(
                fixture.Controller,
                new FailingPresentation { ThrowRemove = true });

            Assert.Throws<InvalidOperationException>(() =>
                fixture.Controller.Tick(20f, false));

            Assert.That(fixture.Controller.IsManifestOpen, Is.True);
            Assert.That(fixture.Controller.IsProcessing, Is.False);
            AssertConstructionButtons(constructionButtons, false);
            Assert.That(fixture.Menu.ConstructionCancellationBlocked, Is.True);
            SetEvacuationPresentation(
                fixture.Controller,
                fixture.Presentation);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            fixture.Controller.Tick(20f, false);
            Assert.That(fixture.Controller.IsManifestOpen, Is.False);
            Assert.That(fixture.Controller.IsProcessing, Is.False);
            AssertConstructionButtons(constructionButtons, true);
            Assert.That(fixture.Menu.ConstructionCancellationBlocked, Is.False);
        }

        [TestCase("Configure")]
        [TestCase("OnDisable")]
        [TestCase("OnDestroy")]
        public void Controller_ControlledCleanupRollsBackOldSessionExactlyOnce(
            string cleanupPath)
        {
            EvacuationFixture oldFixture = CreateFixture(configureMenu: true);
            EvacuationFixture newFixture = CreateFixture(configureMenu: true);
            GrayboxBuildingInstance3D committedFull = Begin(
                oldFixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, oldFixture.Presentation);
            GrayboxBuildingInstance3D pendingFull = Begin(
                oldFixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, oldFixture.Presentation);
            GrayboxBuildingInstance3D laterFull = Begin(
                oldFixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                14, 10, oldFixture.Presentation);
            oldFixture.Session.SetConstructionMultiplierForDevelopment(100f);
            oldFixture.Session.TickConstruction(
                .1f, CityMode.Fortress, false, oldFixture.Presentation);
            Assert.That(oldFixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(oldFixture.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(3));
            Assert.That(oldFixture.Controller.ConfirmManifest(), Is.True);
            oldFixture.Controller.Tick(20f, false);
            Assert.That(oldFixture.Session.Instances.Contains(committedFull), Is.False);
            Assert.That(committedFull.IsEvacuationLocked, Is.False);
            Assert.That(pendingFull.IsEvacuationLocked, Is.True);
            Assert.That(laterFull.IsEvacuationLocked, Is.True);
            uint beforeCleanup = oldFixture.Session.CatalogRevision;
            Button[] oldConstructionButtons =
                ConstructionButtons(oldFixture.Canvas);
            AssertConstructionButtons(oldConstructionButtons, false);

            InvokeCleanup(
                oldFixture.Controller,
                cleanupPath,
                newFixture);

            Assert.That(pendingFull.IsEvacuationLocked, Is.False);
            Assert.That(laterFull.IsEvacuationLocked, Is.False);
            Assert.That(oldFixture.Session.Instances.Contains(pendingFull), Is.True);
            Assert.That(oldFixture.Session.Instances.Contains(laterFull), Is.True);
            Assert.That(oldFixture.Session.Instances.Contains(committedFull), Is.False);
            Assert.That(oldFixture.Session.CatalogRevision,
                Is.EqualTo(beforeCleanup + 2));
            Assert.That(oldFixture.Menu.EvacuationVisible, Is.False);
            AssertConstructionButtons(oldConstructionButtons, true);
            Assert.That(oldFixture.Controller.Work, Is.Empty);
            Assert.That(oldFixture.Controller.IsManifestOpen, Is.False);
            Assert.That(oldFixture.Controller.IsProcessing, Is.False);
            AssertControllerCleanupReferences(
                oldFixture.Controller,
                cleanupPath == "Configure" ? newFixture : default(EvacuationFixture));
            Assert.That(CleanupDiagnosticCount(
                oldFixture.Controller,
                "cleanupRollbackInvocationCount"), Is.EqualTo(1));
            Assert.That(CleanupDiagnosticCount(
                oldFixture.Controller,
                "cleanupMenuReleaseInvocationCount"), Is.EqualTo(1));
            Assert.That(
                CleanupRollbackSnapshot(oldFixture.Controller)
                    .Select(item => item.StableInstanceId),
                Is.EqualTo(new[]
                {
                    pendingFull.StableInstanceId,
                    laterFull.StableInstanceId
                }));
            uint afterFirstCleanup = oldFixture.Session.CatalogRevision;

            InvokeCleanup(
                oldFixture.Controller,
                cleanupPath,
                newFixture);

            Assert.That(pendingFull.IsEvacuationLocked, Is.False);
            Assert.That(laterFull.IsEvacuationLocked, Is.False);
            Assert.That(oldFixture.Session.CatalogRevision,
                Is.EqualTo(afterFirstCleanup));
            Assert.That(oldFixture.Session.Instances.Contains(committedFull), Is.False);
            AssertConstructionButtons(oldConstructionButtons, true);
            Assert.That(oldFixture.Controller.Work, Is.Empty);
            Assert.That(CleanupDiagnosticCount(
                oldFixture.Controller,
                "cleanupRollbackInvocationCount"), Is.EqualTo(1));
            Assert.That(CleanupDiagnosticCount(
                oldFixture.Controller,
                "cleanupMenuReleaseInvocationCount"), Is.EqualTo(1));
        }

        [Test]
        public void Controller_ProcessesFullQueueSequentiallyAndKeepsLaterWorkLocked()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D first = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            GrayboxBuildingInstance3D later = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, fixture.Presentation);
            float laterRemaining = later.Progress.Remaining;

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(2));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            Assert.That(later.IsEvacuationLocked, Is.True);
            BuildingEvacuationWork snapshot = fixture.Controller.Work[1];
            fixture.Session.TickConstruction(20f, CityMode.Fortress, false,
                fixture.Presentation);
            Assert.That(later.Progress.Remaining, Is.EqualTo(laterRemaining));
            fixture.Controller.Tick(20f, false);

            Assert.That(fixture.Session.Instances.Contains(first), Is.False);
            Assert.That(fixture.Session.Instances.Contains(later), Is.True);
            Assert.That(later.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Controller.Work[1].RemainingRatio,
                Is.EqualTo(snapshot.RemainingRatio));
            Assert.That(fixture.Controller.Work[1].Refund,
                Is.EqualTo(snapshot.Refund));
            Assert.That(fixture.Controller.Work[1].DismantleSeconds,
                Is.EqualTo(snapshot.DismantleSeconds));
            fixture.Controller.Tick(20f, false);
            Assert.That(fixture.Session.Instances.Contains(later), Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
            fixture.Controller.Tick(20f, false);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
        }

        [Test]
        public void Controller_FullFailureRestoresEveryCountedLockAndReopensManifest()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D first = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            GrayboxBuildingInstance3D second = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, fixture.Presentation);
            fixture.Session.SetConstructionMultiplierForDevelopment(100f);
            fixture.Session.TickConstruction(
                .1f, CityMode.Fortress, false, fixture.Presentation);
            uint revisionBeforeConfirmation = fixture.Session.CatalogRevision;

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(2));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            Assert.That(fixture.Session.CatalogRevision,
                Is.EqualTo(revisionBeforeConfirmation + 2));
            Assert.That(first.IsEvacuationLocked, Is.True);
            Assert.That(second.IsEvacuationLocked, Is.True);
            SetEvacuationPresentation(
                fixture.Controller,
                new FailingPresentation { ThrowRemove = true });

            Assert.Throws<InvalidOperationException>(() =>
                fixture.Controller.Tick(20f, false));

            Assert.That(fixture.Controller.IsProcessing, Is.False);
            Assert.That(fixture.Controller.IsManifestOpen, Is.True);
            Assert.That(first.IsEvacuationLocked, Is.False);
            Assert.That(second.IsEvacuationLocked, Is.False);
            Assert.That(fixture.Session.CompletedBuildingCount(
                BuildingCatalog.Wall.Id.Value), Is.EqualTo(2));
            Assert.That(fixture.Session.CatalogRevision,
                Is.EqualTo(revisionBeforeConfirmation + 4));
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
        }

        [Test]
        public void Controller_FullRestoreFailureSurfacesCompoundAfterAllLockCleanup()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D first = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            GrayboxBuildingInstance3D second = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, fixture.Presentation);
            fixture.Session.SetConstructionMultiplierForDevelopment(100f);
            fixture.Session.TickConstruction(
                .1f, CityMode.Fortress, false, fixture.Presentation);
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(2));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            var presentation = new FailingPresentation
            {
                ThrowRemove = true,
                ThrowCreate = true
            };
            SetEvacuationPresentation(fixture.Controller, presentation);

            InvalidOperationException thrown =
                Assert.Throws<InvalidOperationException>(() =>
                    fixture.Controller.Tick(20f, false));

            Assert.That(thrown.Message, Does.Contain("restore presentation"));
            var compound = thrown.InnerException as AggregateException;
            Assert.That(compound, Is.Not.Null);
            Assert.That(compound.InnerExceptions,
                Does.Contain(presentation.RemoveFailure));
            Assert.That(compound.InnerExceptions,
                Does.Contain(presentation.CreateFailure));
            Assert.That(first.IsEvacuationLocked, Is.False);
            Assert.That(second.IsEvacuationLocked, Is.False);
            Assert.That(fixture.Controller.IsManifestOpen, Is.True);
        }

        [Test]
        public void Controller_FullCommitConsumesLockAndPackingReturnsCoordinatorOnce()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D ground = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            var leaderObject = new GameObject("evacuation-leader");
            cleanup.Add(leaderObject);
            var leader = leaderObject.AddComponent<GrayboxLeaderController3D>();
            leader.Configure(null, fixture.City, true);
            var coordinatorObject = new GameObject("evacuation-coordinator");
            cleanup.Add(coordinatorObject);
            var coordinator =
                coordinatorObject.AddComponent<GrayboxDirectControlCoordinator>();
            coordinator.Configure(fixture.City, leader);
            Assert.That(coordinator.Refresh(), Is.True);
            Assert.That(coordinator.ControlTarget,
                Is.EqualTo(DirectControlTarget.Leader));
            var targetChanges = new List<DirectControlTarget>();
            coordinator.TargetChanged += targetChanges.Add;
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(1));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            BuildingEvacuationWork snapshot = fixture.Controller.Work[0];

            fixture.Controller.Tick(20f, false);

            Assert.That(fixture.Session.Instances.Contains(ground), Is.False);
            Assert.That(ground.IsEvacuationLocked, Is.False);
            AssertEvacuationWorkConsumed(
                fixture.Session,
                ground.StableInstanceId);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
            Assert.That(fixture.City.LastFailureReason, Is.Empty);
            Assert.That(coordinator.Refresh(), Is.True);
            Assert.That(coordinator.ControlTarget,
                Is.EqualTo(DirectControlTarget.City));
            Assert.That(targetChanges,
                Is.EqualTo(new[] { DirectControlTarget.City }));
            fixture.Controller.Tick(20f, false);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
            Assert.That(fixture.City.LastFailureReason, Is.Empty);
            Assert.That(coordinator.Refresh(), Is.False);
            Assert.That(targetChanges, Has.Count.EqualTo(1));
            Assert.That(fixture.Session.TryCommitEvacuation(
                snapshot, fixture.Presentation, out _, out string failure),
                Is.False);
            Assert.That(failure, Is.Not.Empty);
        }

        [Test]
        public void Menu_UpdateRestoresDependentCardAfterControllerFailureCleanup()
        {
            EvacuationFixture fixture = CreateFixture(configureMenu: true);
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.Smelter.RequiredResearchId);
            fixture.Session.UnlockResearchForDevelopment(
                BuildingCatalog.Assembler.RequiredResearchId);
            GrayboxBuildingInstance3D smelter = Begin(
                fixture.Session, BuildingCatalog.Smelter, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            fixture.Session.SetConstructionMultiplierForDevelopment(100f);
            fixture.Session.TickConstruction(
                .1f, CityMode.Fortress, false, fixture.Presentation);
            InvokeMenuUpdate(fixture.Menu);
            fixture.Interaction.ToggleCatalog();
            fixture.Menu.SetCategory(BuildingMenuCategory.Production);
            Assert.That(FindButton(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Assembler.Id.Value).interactable,
                Is.True);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                smelter.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            InvokeMenuUpdate(fixture.Menu);
            Assert.That(FindButton(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Assembler.Id.Value).interactable,
                Is.False);
            SetEvacuationPresentation(
                fixture.Controller,
                new FailingPresentation { ThrowRemove = true });

            Assert.Throws<InvalidOperationException>(() =>
                fixture.Controller.Tick(20f, false));
            InvokeMenuUpdate(fixture.Menu);

            Assert.That(FindButton(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Assembler.Id.Value).interactable,
                Is.True);
        }

        [Test]
        public void Controller_CategorySingleAndAllAssignmentsCanMix()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D wall = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            GrayboxBuildingInstance3D housing = Begin(
                fixture.Session, BuildingCatalog.Housing, BuildingSite.Ground,
                12, 10, fixture.Presentation);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignCategory(
                BuildingMenuCategory.Basic,
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(2));
            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.Abandon), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.QuickDismantle), Is.EqualTo(2));
            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.Abandon), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);

            Assert.That(wall.State,
                Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(fixture.Session.Instances.Contains(housing), Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
        }

        [Test]
        public void Session_PresentationFailureRetainsMutationUntilFailureCleanup()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new FailingPresentation { ThrowRemove = true };
            GrayboxBuildingInstance3D wall = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, presentation);
            BuildingEvacuationWork work = BuildingEvacuationRules.Create(
                wall.StableInstanceId, wall.Placement.Definition.Cost,
                wall.Progress.BaseDuration, 1d,
                BuildingEvacuationTreatment.QuickDismantle);
            Assert.That(session.TryCaptureEvacuationWork(new[] { work }, out _),
                Is.True);

            Assert.Throws<InvalidOperationException>(() => session.TryCommitEvacuation(
                work, presentation, out _, out _));
            Assert.That(session.Instances.Contains(wall), Is.True);
            session.RollbackEvacuationLocksAfterFailure(new[] { work });
            Assert.That(session.TryCommitEvacuation(
                work, presentation, out _, out string failure), Is.False);
            Assert.That(failure, Is.Not.Empty);
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

        private EvacuationFixture CreateFixture(bool configureMenu = false)
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
            Canvas canvas = null;
            GrayboxBuildingInteractionModel3D interaction = null;
            if (configureMenu)
            {
                var eventObject = new GameObject("evacuation-event-system");
                cleanup.Add(eventObject);
                var eventSystem = eventObject.AddComponent<EventSystem>();
                var canvasObject = new GameObject("evacuation-canvas");
                cleanup.Add(canvasObject);
                canvas = canvasObject.AddComponent<Canvas>();
                var interactionObject =
                    new GameObject("evacuation-interaction");
                cleanup.Add(interactionObject);
                interaction = interactionObject
                    .AddComponent<GrayboxBuildingInteractionModel3D>();
                menu.Configure(canvas, eventSystem, session, interaction);
            }
            var controllerObject = new GameObject("evacuation-controller");
            cleanup.Add(controllerObject);
            var controller = controllerObject.AddComponent<GrayboxEvacuationController3D>();
            controller.Configure(session, city, presentation, menu);
            return new EvacuationFixture(
                session,
                city,
                presentation,
                controller,
                menu,
                canvas,
                interaction);
        }

        private static void SetEvacuationPresentation(
            GrayboxEvacuationController3D controller,
            IGrayboxBuildingPresentation3D presentation)
        {
            FieldInfo field = typeof(GrayboxEvacuationController3D).GetField(
                "evacuationPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                "Controller must retain the presentation through its interface boundary.");
            field.SetValue(controller, presentation);
        }

        private static void ReverseSessionInstances(
            GrayboxBuildingSession3D session)
        {
            FieldInfo field = typeof(GrayboxBuildingSession3D).GetField(
                "instances",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var instances =
                field.GetValue(session) as List<GrayboxBuildingInstance3D>;
            Assert.That(instances, Is.Not.Null);
            instances.Reverse();
        }

        private static void AssertEvacuationWorkConsumed(
            GrayboxBuildingSession3D session,
            string stableInstanceId)
        {
            foreach (string fieldName in new[]
                     {
                         "evacuationLocks",
                         "evacuationSnapshots"
                     })
            {
                FieldInfo field = typeof(GrayboxBuildingSession3D).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                var values = field.GetValue(session) as IDictionary;
                Assert.That(values, Is.Not.Null);
                Assert.That(values.Contains(stableInstanceId), Is.False,
                    fieldName + " retained consumed Full work.");
            }
        }

        private static void InvokeMenuUpdate(GrayboxBuildingMenuView3D menu)
        {
            typeof(GrayboxBuildingMenuView3D).GetMethod(
                    "Update",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(menu, null);
        }

        private static void InvokeCleanup(
            GrayboxEvacuationController3D controller,
            string cleanupPath,
            EvacuationFixture replacement)
        {
            if (cleanupPath == "Configure")
            {
                controller.Configure(
                    replacement.Session,
                    replacement.City,
                    replacement.Presentation,
                    replacement.Menu);
                return;
            }
            typeof(GrayboxEvacuationController3D).GetMethod(
                    cleanupPath,
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        private static void AssertControllerCleanupReferences(
            GrayboxEvacuationController3D controller,
            EvacuationFixture replacement)
        {
            foreach (string fieldName in new[]
                     {
                         "session",
                         "city",
                         "presentation",
                         "menu",
                         "evacuationPresentation"
                     })
            {
                FieldInfo field = typeof(GrayboxEvacuationController3D).GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                object value = field.GetValue(controller);
                if (replacement.Controller == null)
                    Assert.That(value, Is.Null, fieldName);
                else if (fieldName == "session")
                    Assert.That(value, Is.SameAs(replacement.Session));
                else if (fieldName == "city")
                    Assert.That(value, Is.Not.Null);
                else if (fieldName == "presentation" ||
                         fieldName == "evacuationPresentation")
                    Assert.That(value, Is.SameAs(replacement.Presentation));
                else
                    Assert.That(value, Is.SameAs(replacement.Menu));
            }
        }

        private static int CleanupDiagnosticCount(
            GrayboxEvacuationController3D controller,
            string fieldName)
        {
            FieldInfo field = typeof(GrayboxEvacuationController3D).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (int)field.GetValue(controller);
        }

        private static IReadOnlyList<BuildingEvacuationWork>
            CleanupRollbackSnapshot(GrayboxEvacuationController3D controller)
        {
            FieldInfo field = typeof(GrayboxEvacuationController3D).GetField(
                "cleanupRollbackSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "cleanupRollbackSnapshot");
            return (IReadOnlyList<BuildingEvacuationWork>)field.GetValue(controller);
        }

        private static Button[] ConstructionButtons(Canvas canvas)
        {
            return new[]
            {
                FindButton(canvas.transform, "Construction.Cancel"),
                FindButton(canvas.transform, "Construction.Confirm.Yes"),
                FindButton(canvas.transform, "Construction.Confirm.No")
            };
        }

        private static void AssertConstructionButtons(
            IEnumerable<Button> buttons,
            bool interactable)
        {
            foreach (Button button in buttons)
            {
                Assert.That(button, Is.Not.Null);
                Assert.That(button.interactable, Is.EqualTo(interactable),
                    button.name);
            }
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (var index = 0; index < transforms.Length; index++)
                if (transforms[index].name == name)
                    return transforms[index].GetComponent<Button>();
            return null;
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

        private sealed class FailingPresentation : IGrayboxBuildingPresentation3D
        {
            public bool ThrowRemove { get; set; }
            public bool ThrowCreate { get; set; }
            public InvalidOperationException RemoveFailure { get; } =
                new InvalidOperationException("remove");
            public InvalidOperationException CreateFailure { get; } =
                new InvalidOperationException("create");
            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                if (ThrowCreate) throw CreateFailure;
                return true;
            }
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance)
            {
                if (ThrowRemove) throw RemoveFailure;
            }
        }

        private sealed class DeploymentRequestSpy : IGrayboxDeploymentRequest3D
        {
            private readonly bool toggleResult;

            public DeploymentRequestSpy(CityMode mode, bool toggleResult)
            {
                Mode = mode;
                this.toggleResult = toggleResult;
            }

            public CityMode Mode { get; }
            public int ToggleCalls { get; private set; }

            public bool TryToggleDeployment(out string failureReason)
            {
                ToggleCalls++;
                failureReason = toggleResult ? string.Empty : "rejected";
                return toggleResult;
            }
        }

        private readonly struct EvacuationFixture
        {
            public EvacuationFixture(
                GrayboxBuildingSession3D session,
                WasteCity.Graybox3D.GrayboxMobileCityController3D city,
                GrayboxBuildingWorldView3D presentation,
                GrayboxEvacuationController3D controller,
                GrayboxBuildingMenuView3D menu,
                Canvas canvas,
                GrayboxBuildingInteractionModel3D interaction)
            {
                Session = session;
                City = city;
                Presentation = presentation;
                Controller = controller;
                Menu = menu;
                Canvas = canvas;
                Interaction = interaction;
            }

            public GrayboxBuildingSession3D Session { get; }
            public WasteCity.Graybox3D.GrayboxMobileCityController3D City { get; }
            public GrayboxBuildingWorldView3D Presentation { get; }
            public GrayboxEvacuationController3D Controller { get; }
            public GrayboxBuildingMenuView3D Menu { get; }
            public Canvas Canvas { get; }
            public GrayboxBuildingInteractionModel3D Interaction { get; }
        }
    }
}
