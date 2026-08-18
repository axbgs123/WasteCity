using System;
using System.Collections.Generic;
using System.Reflection;
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
            Assert.That(session.GroundGrid.Width, Is.EqualTo(64));
            Assert.That(session.GroundGrid.Height, Is.EqualTo(48));
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
            uint revision = CatalogRevision(session);

            session.UnlockResearchForDevelopment(automatedMachinery);
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(revision + 1u)));
            revision = CatalogRevision(session);

            Assert.That(session.IsResearchCompleted(automatedMachinery), Is.True);
            Assert.That(session.HasContactedRoute(ContentRoute.Technology), Is.False);

            int researchBeforeRoute = session.Research.CompletedCount;
            session.UnlockRouteForDevelopment(ContentRoute.Technology);

            Assert.That(session.HasContactedRoute(ContentRoute.Technology), Is.True);
            Assert.That(
                session.IsResearchCompleted("core.research.energy-weapons"),
                Is.True);
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(
                revision + 1u + (uint)(session.Research.CompletedCount - researchBeforeRoute))));
            revision = CatalogRevision(session);

            int researchBeforeAll = session.Research.CompletedCount;
            var routesBeforeAll = new[]
            {
                session.HasContactedRoute(ContentRoute.Technology),
                session.HasContactedRoute(ContentRoute.Cultivation),
                session.HasContactedRoute(ContentRoute.BiologicalAscension),
                session.HasContactedRoute(ContentRoute.Psionics)
            };
            var newlyContactedRoutes = 0;
            for (var index = 0; index < routesBeforeAll.Length; index++)
                if (!routesBeforeAll[index]) newlyContactedRoutes++;
            session.UnlockAllResearchForDevelopment();

            Assert.That(session.Research.CompletedCount, Is.EqualTo(ResearchCatalog.All.Length));
            Assert.That(session.HasContactedRoute(ContentRoute.Cultivation), Is.True);
            Assert.That(session.HasContactedRoute(ContentRoute.BiologicalAscension), Is.True);
            Assert.That(session.HasContactedRoute(ContentRoute.Psionics), Is.True);
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(
                revision + (uint)newlyContactedRoutes +
                (uint)(session.Research.CompletedCount - researchBeforeAll))));
            revision = CatalogRevision(session);
            session.UnlockAllResearchForDevelopment();
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));

            session.SetRouteContact(ContentRoute.Technology, false);
            Assert.That(session.HasContactedRoute(ContentRoute.Technology), Is.False);
            Assert.That(
                CatalogRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
        }

        [Test]
        public void CompletedResearchAdvancesCatalogAndPlacementRevisions()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var runtime = new DemoResearchRuntime(session.Research);
            session.Inventory.Set(ResourceIds.Iron, 10);
            uint catalogBefore = session.CatalogRevision;
            uint placementBefore = session.PlacementRevision;

            Assert.That(runtime.TryStart(
                DemoResearchCatalog.BasicMetallurgyId,
                session.Inventory,
                hasEligibleResearchStation: true), Is.True);
            Assert.That(runtime.Tick(
                20f,
                CityMode.Fortress,
                globallyPaused: false,
                hasEligibleResearchStation: true), Is.True);

            Assert.That(session.IsResearchCompleted(
                DemoResearchCatalog.BasicMetallurgyId), Is.True);
            Assert.That(session.CatalogRevision,
                Is.EqualTo(unchecked(catalogBefore + 1u)));
            Assert.That(session.PlacementRevision,
                Is.EqualTo(unchecked(placementBefore + 1u)));
        }

        [Test]
        public void CatalogRevision_AdvancesOnlyForCommittedCatalogProjectionChanges()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            uint revision = CatalogRevision(session);

            session.ConfigureDevelopmentFixture();
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(revision + 1u)));
            revision = CatalogRevision(session);
            session.SetRouteContact(ContentRoute.Technology, true);
            session.SetRouteContact(ContentRoute.Technology, true);
            session.SetRouteContact(ContentRoute.Technology, false);
            session.UnlockResearchForDevelopment("core.research.automated-machinery");
            session.UnlockResearchForDevelopment("core.research.automated-machinery");
            session.UnlockResearchForDevelopment("unknown.research");
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(revision + 3u)));

            GrayboxBuildingInstance3D instance = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 10, 10, presentation);
            revision = CatalogRevision(session);
            session.TickConstruction(1f, CityMode.Fortress, false, presentation);
            Assert.That(presentation.Updated, Is.EqualTo(new[] { instance }));
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));
            session.TickConstruction(1f, CityMode.Fortress, false, presentation);
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(revision + 1u)));
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
        }

        [Test]
        public void CatalogRevision_BeginAndCancelPathsHaveExactZeroDelta()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            uint revision = CatalogRevision(session);
            BuildingPlacementRequest invalidRequest = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);
            session.Inventory.Set(ResourceIds.Stone, 0);

            Assert.That(
                session.TryBeginConstruction(
                    invalidRequest,
                    presentation,
                    out _,
                    out _),
                Is.False);
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));
            Assert.That(
                session.TryCancelConstruction(
                    "building.instance.missing",
                    1d,
                    presentation,
                    out int missingRefund),
                Is.False);
            Assert.That(missingRefund, Is.Zero);
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));

            session.Inventory.Set(
                ResourceIds.Stone,
                BuildingCatalog.Wall.Cost);
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));
            Assert.That(
                session.TryCancelConstruction(
                    instance.StableInstanceId,
                    1d,
                    presentation,
                    out int acceptedRefund),
                Is.True);
            Assert.That(
                acceptedRefund,
                Is.EqualTo(BuildingCatalog.Wall.Cost));
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));
        }

        [Test]
        public void IDEA0010_PlacementRevisionAdvancesOnlyAfterCommittedGridChanges()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            uint revision = PlacementRevision(session);
            BuildingPlacementRequest request = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);
            session.Inventory.Set(ResourceIds.Stone, 0);

            Assert.That(
                session.TryBeginConstruction(
                    request,
                    presentation,
                    out _,
                    out _),
                Is.False);
            Assert.That(PlacementRevision(session), Is.EqualTo(revision));

            session.Inventory.Set(ResourceIds.Stone, 30);
            Assert.That(
                session.TryBeginConstruction(
                    request,
                    presentation,
                    out GrayboxBuildingInstance3D instance,
                    out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
            revision = PlacementRevision(session);

            Assert.That(
                session.TryCancelConstruction(
                    instance.StableInstanceId,
                    0d,
                    presentation,
                    out _),
                Is.True);
            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
        }

        [Test]
        public void IDEA0010_PlacementRevisionTracksCompletionAndEvacuationOnce()
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
            uint revision = PlacementRevision(session);

            session.TickConstruction(
                100f,
                CityMode.Fortress,
                false,
                presentation);
            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
            revision = PlacementRevision(session);
            var work = BuildingEvacuationRules.Create(
                instance.StableInstanceId,
                instance.Placement.Definition.Cost,
                instance.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.Abandon);
            Assert.That(
                session.TryCaptureEvacuationWork(
                    new[] { work },
                    out string captureFailure),
                Is.True,
                captureFailure);
            Assert.That(
                session.TryCommitEvacuation(
                    work,
                    presentation,
                    out _,
                    out string commitFailure),
                Is.True,
                commitFailure);
            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
        }

        [Test]
        public void IDEA0010_PlacementRevisionTracksLockAndRollbackOnce()
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
            var work = BuildingEvacuationRules.Create(
                instance.StableInstanceId,
                instance.Placement.Definition.Cost,
                instance.Progress.BaseDuration,
                instance.Progress.Remaining /
                instance.Progress.BaseDuration,
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(
                session.TryCaptureEvacuationWork(
                    new[] { work },
                    out string captureFailure),
                Is.True,
                captureFailure);
            uint revision = PlacementRevision(session);

            Assert.That(
                session.TryLockEvacuationWork(
                    new[] { work },
                    out string lockFailure),
                Is.True,
                lockFailure);
            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
            revision = PlacementRevision(session);

            session.RollbackEvacuationLocksAfterFailure(
                new[] { work });
            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
        }

        [Test]
        public void IDEA0010_PlacementRevisionTracksFixtureUnlockAndPopulationInputs()
        {
            GrayboxBuildingSession3D session = CreateSession();
            uint revision = PlacementRevision(session);

            session.ConfigureDevelopmentFixture();
            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
            revision = PlacementRevision(session);
            session.SetPopulationForDevelopment(session.Population + 1);
            session.SetPopulationForDevelopment(session.Population);
            session.SetRouteContact(ContentRoute.Technology, true);
            session.SetRouteContact(ContentRoute.Technology, true);
            session.UnlockResearchForDevelopment(
                "core.research.automated-machinery");
            session.UnlockResearchForDevelopment(
                "core.research.automated-machinery");

            Assert.That(
                PlacementRevision(session),
                Is.EqualTo(unchecked(revision + 3u)));
        }

        [Test]
        public void IDEA0010_PlacementRevisionWrapsAndIsNotSerialized()
        {
            GrayboxBuildingSession3D session = CreateSession();
            FieldInfo field = typeof(GrayboxBuildingSession3D).GetField(
                "placementRevision",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            Assert.That(
                field.IsDefined(typeof(SerializeField), false),
                Is.False);
            field.SetValue(session, uint.MaxValue);

            session.ConfigureDevelopmentFixture();

            Assert.That(PlacementRevision(session), Is.Zero);
            Assert.That(
                JsonUtility.ToJson(session),
                Does.Not.Contain("placementRevision"));
        }

        [Test]
        public void CatalogRevision_DoesNotAdvanceWhenPresentationCompletionRollsBack()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation
            {
                UpdateException = new InvalidOperationException("update failed")
            };
            Begin(session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 10, 10, presentation);
            uint revision = CatalogRevision(session);

            Assert.Throws<InvalidOperationException>(() =>
                session.TickConstruction(2f, CityMode.Fortress, false, presentation));

            Assert.That(CatalogRevision(session), Is.EqualTo(revision));
        }

        [Test]
        public void CatalogRevision_FixtureRebuildsAreMonotonicAcrossUncheckedOverflow()
        {
            GrayboxBuildingSession3D session = CreateSession();
            typeof(GrayboxBuildingSession3D).GetField(
                "catalogRevision",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(
                session,
                uint.MaxValue);

            session.ConfigureDevelopmentFixture();
            Assert.That(CatalogRevision(session), Is.Zero);
            session.ConfigureDevelopmentFixture();
            Assert.That(CatalogRevision(session), Is.EqualTo(1u));
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
        public void TryBeginConstruction_TwoSuccessfulCommitsAdvanceStableOrdinal()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();

            GrayboxBuildingInstance3D first = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            GrayboxBuildingInstance3D second = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                12,
                10,
                presentation);

            Assert.That(first.StableInstanceId, Is.EqualTo("building.instance.000001"));
            Assert.That(second.StableInstanceId, Is.EqualTo("building.instance.000002"));
            Assert.That(session.Instances, Is.EqualTo(new[] { first, second }));
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
        public void TryBeginConstruction_PostSpendGridFailureRestoresInventoryAndOrdinal()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            session.Inventory.Set(ResourceIds.Stone, 2);
            var callbackCount = 0;
            PlacedBuilding injectedPlacement = null;
            session.CityStorage.AttributedChanged +=
                (resourceId, delta, _) =>
            {
                if (resourceId != ResourceIds.Stone || delta >= 0 ||
                    callbackCount > 0)
                    return;
                callbackCount++;
                session.GroundGrid.TryRestore(
                    BuildingCatalog.Wall,
                    10,
                    10,
                    out injectedPlacement);
            };
            BuildingPlacementRequest request = ValidRequest(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10);

            bool result = session.TryBeginConstruction(
                request,
                presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation);

            Assert.That(result, Is.False);
            Assert.That(evaluation.IsValid, Is.True);
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(injectedPlacement, Is.Not.Null);
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.EqualTo(2));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(1));
            Assert.That(session.Instances, Is.Empty);
            Assert.That(instance, Is.Null);
            Assert.That(presentation.Created, Is.Empty);

            Assert.That(session.GroundGrid.Remove(injectedPlacement), Is.True);
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
            Assert.That(presentation.Created, Is.Empty);
            Assert.That(presentation.Removed, Has.Count.EqualTo(1));

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
            Assert.That(presentation.Created, Is.Empty);
            Assert.That(presentation.Removed, Has.Count.EqualTo(1));

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
        public void TryBeginConstruction_CleanupExceptionDoesNotMaskCreateException()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var createFailure = new InvalidOperationException("create failed");
            var cleanupFailure = new InvalidOperationException("cleanup failed");
            var presentation = new RecordingPresentation
            {
                CreateException = createFailure,
                RemoveException = cleanupFailure
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

            Assert.That(thrown, Is.SameAs(createFailure));
            Assert.That(session.Inventory.Get(ResourceIds.Stone), Is.EqualTo(stoneBefore));
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);
            Assert.That(presentation.Created, Is.Empty);
            Assert.That(presentation.Removed, Has.Count.EqualTo(1));
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
            Assert.That(presentation.Updated, Is.EqualTo(new[] { inner }));
            Assert.That(inner.Progress.Remaining, Is.EqualTo(4f));
            Assert.That(ground.Progress.Remaining, Is.EqualTo(2f));

            session.TickConstruction(1f, CityMode.Fortress, false, presentation);
            Assert.That(inner.Progress.Remaining, Is.EqualTo(3f));
            Assert.That(ground.Progress.Remaining, Is.EqualTo(1f));
        }

        [Test]
        public void IDEA0009_Housing_UsesUnifiedConstructionAndEvaluationPaths()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            int alloyBefore = session.Inventory.Get(ResourceIds.Alloy);

            Assert.That(
                session.TryBeginConstruction(
                    ValidRequest(
                        session,
                        BuildingCatalog.Housing,
                        BuildingSite.InnerCity,
                        CityMode.Mobile,
                        0,
                        0),
                    presentation,
                    out GrayboxBuildingInstance3D inner,
                    out BuildingPlacementEvaluation innerEvaluation),
                Is.True,
                "IDEA0009 Housing Mobile InnerCity must use unified construction: " +
                innerEvaluation.PrimaryFailure);
            Assert.That(
                session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(alloyBefore - BuildingCatalog.Housing.Cost),
                "IDEA0009 Housing Mobile InnerCity must spend exactly once");
            Assert.That(
                session.InnerGrid.Count,
                Is.EqualTo(1),
                "IDEA0009 Housing Mobile InnerCity must occupy exactly once");
            Assert.That(
                session.InnerGrid.IsOccupied(0, 0),
                Is.True,
                "IDEA0009 Housing Mobile InnerCity footprint must be occupied");

            int alloyBeforeRejectedGround =
                session.Inventory.Get(ResourceIds.Alloy);
            Assert.That(
                session.TryBeginConstruction(
                    ValidRequest(
                        session,
                        BuildingCatalog.Housing,
                        BuildingSite.Ground,
                        CityMode.Mobile,
                        10,
                        10),
                    presentation,
                    out GrayboxBuildingInstance3D rejectedGround,
                    out BuildingPlacementEvaluation rejectedEvaluation),
                Is.False,
                "IDEA0009 Housing Mobile Ground must be rejected by unified evaluation");
            Assert.That(
                rejectedGround,
                Is.Null,
                "IDEA0009 Housing Mobile Ground must not create an instance");
            Assert.That(
                rejectedEvaluation.PrimaryFailure,
                Is.EqualTo(BuildingPlacementFailure.InvalidCityMode),
                "IDEA0009 Housing Mobile Ground must report InvalidCityMode");
            Assert.That(
                session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(alloyBeforeRejectedGround),
                "IDEA0009 Housing Mobile Ground rejection must not spend");
            Assert.That(
                session.GroundGrid.Count,
                Is.Zero,
                "IDEA0009 Housing Mobile Ground rejection must not occupy");

            Assert.That(
                session.TryBeginConstruction(
                    ValidRequest(
                        session,
                        BuildingCatalog.Housing,
                        BuildingSite.Ground,
                        CityMode.Fortress,
                        10,
                        10),
                    presentation,
                    out GrayboxBuildingInstance3D ground,
                    out BuildingPlacementEvaluation groundEvaluation),
                Is.True,
                "IDEA0009 Housing Fortress Ground must use unified construction: " +
                groundEvaluation.PrimaryFailure);
            Assert.That(
                ground.Placement.Definition,
                Is.SameAs(inner.Placement.Definition)
                    .And.SameAs(BuildingCatalog.Housing),
                "IDEA0009 Housing InnerCity and Ground must share canonical definition");
            Assert.That(
                session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(alloyBefore - BuildingCatalog.Housing.Cost * 2),
                "IDEA0009 Housing Fortress Ground must spend exactly once");
            Assert.That(
                session.GroundGrid.Count,
                Is.EqualTo(1),
                "IDEA0009 Housing Fortress Ground must occupy exactly once");
            Assert.That(
                session.GroundGrid.IsOccupied(10, 10),
                Is.True,
                "IDEA0009 Housing Fortress Ground footprint must be occupied");

            session.TickConstruction(
                BuildingCatalog.Housing.BuildSeconds,
                CityMode.Fortress,
                false,
                presentation);

            Assert.That(
                inner.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed),
                "IDEA0009 Housing Mobile InnerCity construction must complete");
            Assert.That(
                ground.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed),
                "IDEA0009 Housing Fortress Ground construction must complete in place");
            Assert.That(
                session.Instances,
                Is.EqualTo(new[] { inner, ground }),
                "IDEA0009 Housing unified construction must preserve both instances");
        }

        [Test]
        public void IDEA0009_FortressGroundHousing_UsesExistingEvacuationPath()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            int alloyBefore = session.Inventory.Get(ResourceIds.Alloy);
            GrayboxBuildingInstance3D ground = Begin(
                session,
                BuildingCatalog.Housing,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            session.TickConstruction(
                BuildingCatalog.Housing.BuildSeconds,
                CityMode.Fortress,
                false,
                presentation);
            var manifest = new List<GrayboxBuildingInstance3D>();

            session.CopyPlayerOwnedGroundInstances(manifest);

            Assert.That(
                manifest,
                Is.EqualTo(new[] { ground }),
                "IDEA0009 Housing Fortress Ground must enter evacuation manifest source");
            BuildingEvacuationWork quickDismantle =
                BuildingEvacuationRules.Create(
                    ground.StableInstanceId,
                    ground.Placement.Definition.Cost,
                    ground.Progress.BaseDuration,
                    1d,
                    BuildingEvacuationTreatment.QuickDismantle);
            Assert.That(
                session.TryCaptureEvacuationWork(
                    new[] { quickDismantle },
                    out string captureFailure),
                Is.True,
                "IDEA0009 Housing Fortress Ground evacuation capture: " +
                captureFailure);
            Assert.That(
                session.TryCommitEvacuation(
                    quickDismantle,
                    presentation,
                    out int acceptedRefund,
                    out string commitFailure),
                Is.True,
                "IDEA0009 Housing Fortress Ground evacuation commit: " +
                commitFailure);
            Assert.That(
                acceptedRefund,
                Is.EqualTo(BuildingCatalog.Housing.Cost / 2),
                "IDEA0009 Housing Fortress Ground quick dismantle refund");
            Assert.That(
                session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(
                    alloyBefore - BuildingCatalog.Housing.Cost +
                    acceptedRefund),
                "IDEA0009 Housing Fortress Ground evacuation resource path");
            Assert.That(
                session.GroundGrid.Count,
                Is.Zero,
                "IDEA0009 Housing Fortress Ground evacuation must release occupancy");
            Assert.That(
                session.Instances,
                Is.Empty,
                "IDEA0009 Housing Fortress Ground evacuation must remove instance");
            Assert.That(
                presentation.Removed,
                Is.EqualTo(new[] { ground }),
                "IDEA0009 Housing Fortress Ground evacuation must remove presentation");
        }

        [Test]
        public void ResearchStation_ConstructsAndCompletesInMobileInnerCity()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            int ironBefore = session.Inventory.Get(ResourceIds.Iron);

            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.ResearchStation,
                BuildingSite.InnerCity,
                CityMode.Mobile,
                0,
                0,
                presentation);
            string stableId = instance.StableInstanceId;

            Assert.That(
                session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(ironBefore - BuildingCatalog.ResearchStation.Cost));
            Assert.That(instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(instance.Placement.Site,
                Is.EqualTo(BuildingSite.InnerCity));
            Assert.That(session.InnerGrid.IsOccupied(0, 0), Is.True);

            session.TickConstruction(
                BuildingCatalog.ResearchStation.BuildSeconds,
                CityMode.Mobile,
                false,
                presentation);

            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(session.Instances, Has.Count.EqualTo(1));
            Assert.That(session.Instances[0], Is.SameAs(instance));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { instance }));
        }

        [TestCase(1f, 9.95f)]
        [TestCase(10f, 9.5f)]
        [TestCase(100f, 5f)]
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

            session.TickConstruction(.05f, CityMode.Fortress, false, presentation);

            Assert.That(
                instance.Progress.Remaining,
                Is.EqualTo(expectedRemaining).Within(.0001f));
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
        public void TickConstruction_UpdateFailureRollsBackProgressAndCanRetrySameInstance()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var updateFailure = new InvalidOperationException("update failed");
            var presentation = new RecordingPresentation
            {
                UpdateException = updateFailure
            };
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            string stableId = instance.StableInstanceId;

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                session.TickConstruction(2f, CityMode.Fortress, false, presentation));

            Assert.That(thrown, Is.SameAs(updateFailure));
            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.Progress.Remaining, Is.EqualTo(2f));
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.Zero);
            Assert.That(presentation.Updated, Is.Empty);

            presentation.UpdateException = null;
            session.TickConstruction(2f, CityMode.Fortress, false, presentation);

            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.Progress.IsComplete, Is.True);
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.EqualTo(1));
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

            uint revision = CatalogRevision(session);
            session.CompleteAllConstructionForDevelopment(presentation);
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(revision + 2u)));
            revision = CatalogRevision(session);
            session.CompleteAllConstructionForDevelopment(presentation);
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));

            Assert.That(ground.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(inner.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { ground, inner }));
        }

        [Test]
        public void CompleteAllConstructionForDevelopment_SkipsEveryEvacuationLock()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D currentFull = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 10, 10, presentation);
            GrayboxBuildingInstance3D unlocked = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 12, 10, presentation);
            GrayboxBuildingInstance3D laterFull = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 14, 10, presentation);
            currentFull.Progress.Restore(1.75f);
            unlocked.Progress.Restore(1.25f);
            laterFull.Progress.Restore(.75f);
            var currentWork = BuildingEvacuationRules.Create(
                currentFull.StableInstanceId,
                currentFull.Placement.Definition.Cost,
                currentFull.Progress.BaseDuration,
                currentFull.Progress.Remaining / currentFull.Progress.BaseDuration,
                BuildingEvacuationTreatment.FullDismantle);
            var laterWork = BuildingEvacuationRules.Create(
                laterFull.StableInstanceId,
                laterFull.Placement.Definition.Cost,
                laterFull.Progress.BaseDuration,
                laterFull.Progress.Remaining / laterFull.Progress.BaseDuration,
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { currentWork, laterWork }, out string captureFailure),
                Is.True, captureFailure);
            Assert.That(session.TryLockEvacuationWork(
                new[] { currentWork, laterWork }, out string lockFailure),
                Is.True, lockFailure);
            uint revision = CatalogRevision(session);

            session.CompleteAllConstructionForDevelopment(presentation);

            Assert.That(currentFull.State,
                Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(currentFull.Progress.Remaining, Is.EqualTo(1.75f));
            Assert.That(currentFull.IsEvacuationLocked, Is.True);
            Assert.That(laterFull.State,
                Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(laterFull.Progress.Remaining, Is.EqualTo(.75f));
            Assert.That(laterFull.IsEvacuationLocked, Is.True);
            Assert.That(unlocked.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(unlocked.Progress.Remaining, Is.Zero);
            Assert.That(presentation.Updated, Is.EqualTo(new[] { unlocked }));
            Assert.That(CatalogRevision(session), Is.EqualTo(revision + 1));
        }

        [Test]
        public void CompleteAllConstructionForDevelopment_UpdateFailureRestoresAndRetriesSameInstance()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var updateFailure = new InvalidOperationException("complete-all update failed");
            var presentation = new RecordingPresentation
            {
                UpdateException = updateFailure
            };
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            instance.Progress.Restore(1.25f);
            string stableId = instance.StableInstanceId;
            uint revision = CatalogRevision(session);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                session.CompleteAllConstructionForDevelopment(presentation));

            Assert.That(thrown, Is.SameAs(updateFailure));
            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.Progress.Remaining, Is.EqualTo(1.25f));
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.Zero);
            Assert.That(presentation.Updated, Is.Empty);
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));

            presentation.UpdateException = null;
            session.CompleteAllConstructionForDevelopment(presentation);

            Assert.That(instance.StableInstanceId, Is.EqualTo(stableId));
            Assert.That(instance.Progress.IsComplete, Is.True);
            Assert.That(instance.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.EqualTo(1));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { instance }));
            Assert.That(
                CatalogRevision(session),
                Is.EqualTo(unchecked(revision + 1u)));
        }

        [Test]
        public void CompleteAllConstructionForDevelopment_LaterFailurePreservesCommittedAndPendingOrder()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var updateFailure = new InvalidOperationException("second update failed");
            var presentation = new RecordingPresentation
            {
                UpdateException = updateFailure,
                UpdateExceptionOnCall = 2
            };
            GrayboxBuildingInstance3D first = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            GrayboxBuildingInstance3D second = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                12,
                10,
                presentation);
            GrayboxBuildingInstance3D third = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                14,
                10,
                presentation);
            first.Progress.Restore(1.5f);
            second.Progress.Restore(.75f);
            third.Progress.Restore(.25f);
            uint revision = CatalogRevision(session);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                session.CompleteAllConstructionForDevelopment(presentation));

            Assert.That(thrown, Is.SameAs(updateFailure));
            Assert.That(first.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(first.Progress.Remaining, Is.Zero);
            Assert.That(second.State, Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(second.Progress.Remaining, Is.EqualTo(.75f));
            Assert.That(third.State, Is.EqualTo(GrayboxBuildingInstanceState.UnderConstruction));
            Assert.That(third.Progress.Remaining, Is.EqualTo(.25f));
            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.EqualTo(1));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { first }));
            Assert.That(CatalogRevision(session), Is.EqualTo(unchecked(revision + 1u)));
            uint revisionAfterFailure = CatalogRevision(session);

            presentation.UpdateException = null;
            session.CompleteAllConstructionForDevelopment(presentation);

            Assert.That(first.StableInstanceId, Is.EqualTo("building.instance.000001"));
            Assert.That(second.StableInstanceId, Is.EqualTo("building.instance.000002"));
            Assert.That(third.StableInstanceId, Is.EqualTo("building.instance.000003"));
            Assert.That(second.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(third.State, Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(
                session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.EqualTo(3));
            Assert.That(presentation.Updated, Is.EqualTo(new[] { first, second, third }));
            Assert.That(
                CatalogRevision(session),
                Is.EqualTo(unchecked(revisionAfterFailure + 2u)));
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

        [Test]
        public void TryCancelConstruction_EvacuationLockRejectsBeforeAnyMutation()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D instance = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 10, 10, presentation);
            instance.Progress.Restore(1.5f);
            var work = BuildingEvacuationRules.Create(
                instance.StableInstanceId,
                instance.Placement.Definition.Cost,
                instance.Progress.BaseDuration,
                instance.Progress.Remaining / instance.Progress.BaseDuration,
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { work }, out string captureFailure),
                Is.True, captureFailure);
            Assert.That(session.TryLockEvacuationWork(
                new[] { work }, out string lockFailure),
                Is.True, lockFailure);
            int inventory = session.Inventory.Get(BuildingCatalog.Wall.CostId);
            uint revision = CatalogRevision(session);

            bool cancelled = session.TryCancelConstruction(
                instance.StableInstanceId,
                1d,
                presentation,
                out int acceptedRefund);

            Assert.That(cancelled, Is.False);
            Assert.That(acceptedRefund, Is.Zero);
            Assert.That(session.Inventory.Get(BuildingCatalog.Wall.CostId),
                Is.EqualTo(inventory));
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
            Assert.That(session.Instances, Is.EqualTo(new[] { instance }));
            Assert.That(instance.Progress.Remaining, Is.EqualTo(1.5f));
            Assert.That(instance.IsEvacuationLocked, Is.True);
            Assert.That(presentation.Removed, Is.Empty);
            Assert.That(presentation.Updated, Is.Empty);
            Assert.That(CatalogRevision(session), Is.EqualTo(revision));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TryCancelConstruction_RemoveFailureRestoresPresentationWithoutCommitting(
            bool throwAfterSideEffect)
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation =
                new CancellationFaultPresentation(throwAfterSideEffect);
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            int stoneBeforeCancellation =
                session.Inventory.Get(ResourceIds.Stone);
            var acceptedRefund = -1;

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                session.TryCancelConstruction(
                    instance.StableInstanceId,
                    1d,
                    presentation,
                    out acceptedRefund));

            Assert.That(thrown, Is.SameAs(presentation.RemoveFailure));
            Assert.That(acceptedRefund, Is.Zero);
            Assert.That(
                session.Inventory.Get(ResourceIds.Stone),
                Is.EqualTo(stoneBeforeCancellation));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(1));
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
            Assert.That(session.Instances, Is.EqualTo(new[] { instance }));
            Assert.That(presentation.Contains(instance), Is.True);

            Assert.That(
                session.TryCancelConstruction(
                    instance.StableInstanceId,
                    1d,
                    presentation,
                    out acceptedRefund),
                Is.True);
            Assert.That(acceptedRefund, Is.EqualTo(BuildingCatalog.Wall.Cost));
            Assert.That(session.GroundGrid.Count, Is.Zero);
            Assert.That(session.Instances, Is.Empty);
            Assert.That(presentation.Contains(instance), Is.False);
        }

        [Test]
        public void TryCancelConstruction_RestoreFailureThrowsCompoundAfterSessionRollback()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var restoreFailure = new InvalidOperationException("restore failed");
            var presentation = new CancellationFaultPresentation(
                throwAfterSideEffect: true)
            {
                RestoreException = restoreFailure
            };
            GrayboxBuildingInstance3D instance = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                10,
                10,
                presentation);
            int stoneBeforeCancellation =
                session.Inventory.Get(ResourceIds.Stone);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
                session.TryCancelConstruction(
                    instance.StableInstanceId,
                    1d,
                    presentation,
                    out _));

            Assert.That(thrown.Message, Does.Contain("restore presentation"));
            var failures = thrown.InnerException as AggregateException;
            Assert.That(failures, Is.Not.Null);
            Assert.That(
                failures.InnerExceptions,
                Does.Contain(presentation.RemoveFailure));
            Assert.That(failures.InnerExceptions, Does.Contain(restoreFailure));
            Assert.That(
                session.Inventory.Get(ResourceIds.Stone),
                Is.EqualTo(stoneBeforeCancellation));
            Assert.That(session.GroundGrid.Count, Is.EqualTo(1));
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
            Assert.That(session.Instances, Is.EqualTo(new[] { instance }));
        }

        [Test]
        public void CompletedBuildingCount_ExcludesLockedAndAbandonedGroundButCountsInnerCity()
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
                CityMode.Fortress,
                1,
                1,
                presentation);
            GrayboxBuildingInstance3D ruined = Begin(
                session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                CityMode.Fortress,
                12,
                10,
                presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(.1f, CityMode.Fortress, false, presentation);

            Assert.That(session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.EqualTo(2));
            Assert.That(session.CompletedBuildingCount(BuildingCatalog.Housing.Id.Value),
                Is.EqualTo(1));
            BuildingEvacuationWork abandon = BuildingEvacuationRules.Create(
                ruined.StableInstanceId,
                ruined.Placement.Definition.Cost,
                ruined.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.Abandon);
            Assert.That(session.TryCaptureEvacuationWork(new[] { abandon }, out _),
                Is.True);
            Assert.That(session.TryCommitEvacuation(
                abandon,
                presentation,
                out int refund,
                out _), Is.True);
            Assert.That(refund, Is.Zero);
            BuildingEvacuationWork full = BuildingEvacuationRules.Create(
                ground.StableInstanceId,
                ground.Placement.Definition.Cost,
                ground.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(session.TryCaptureEvacuationWork(new[] { full }, out _),
                Is.True);
            Assert.That(session.TryLockEvacuationWork(new[] { full }, out _),
                Is.True);

            Assert.That(session.CompletedBuildingCount(BuildingCatalog.Wall.Id.Value),
                Is.Zero);
            Assert.That(session.CompletedBuildingCount(BuildingCatalog.Housing.Id.Value),
                Is.EqualTo(1));
            Assert.That(ruined.State,
                Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(session.GroundGrid.IsOccupied(12, 10), Is.True);
        }

        [Test]
        public void CompletedBuildingCount_CoversEveryOwnershipSiteAndDismantlePermutation()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 10, 10, presentation);
            Begin(
                session, BuildingCatalog.Housing, BuildingSite.InnerCity,
                CityMode.Fortress, 1, 1, presentation);
            GrayboxBuildingInstance3D nonOwnedCompleted = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 12, 10, presentation);
            GrayboxBuildingInstance3D ruin = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 14, 10, presentation);
            GrayboxBuildingInstance3D currentFull = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 16, 10, presentation);
            GrayboxBuildingInstance3D laterLocked = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 18, 10, presentation);
            session.SetConstructionMultiplierForDevelopment(100f);
            session.TickConstruction(
                .1f, CityMode.Fortress, false, presentation);
            SetEvacuationState(
                nonOwnedCompleted,
                false,
                GrayboxBuildingInstanceState.Completed);
            BuildingEvacuationWork abandon = BuildingEvacuationRules.Create(
                ruin.StableInstanceId,
                ruin.Placement.Definition.Cost,
                ruin.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.Abandon);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { abandon }, out string abandonCaptureFailure),
                Is.True, abandonCaptureFailure);
            Assert.That(session.TryCommitEvacuation(
                abandon, presentation, out _, out string abandonFailure),
                Is.True, abandonFailure);
            BuildingEvacuationWork currentWork =
                BuildingEvacuationRules.Create(
                    currentFull.StableInstanceId,
                    currentFull.Placement.Definition.Cost,
                    currentFull.Progress.BaseDuration,
                    1d,
                    BuildingEvacuationTreatment.FullDismantle);
            BuildingEvacuationWork laterWork =
                BuildingEvacuationRules.Create(
                    laterLocked.StableInstanceId,
                    laterLocked.Placement.Definition.Cost,
                    laterLocked.Progress.BaseDuration,
                    1d,
                    BuildingEvacuationTreatment.FullDismantle);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { currentWork, laterWork }, out string fullCaptureFailure),
                Is.True, fullCaptureFailure);
            Assert.That(session.TryLockEvacuationWork(
                new[] { currentWork, laterWork }, out string lockFailure),
                Is.True, lockFailure);

            Assert.That(nonOwnedCompleted.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(nonOwnedCompleted.IsPlayerOwned, Is.False);
            Assert.That(ruin.State,
                Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(currentFull.IsEvacuationLocked, Is.True);
            Assert.That(laterLocked.IsEvacuationLocked, Is.True);
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.Wall.Id.Value), Is.EqualTo(1));
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.Housing.Id.Value), Is.EqualTo(1));
        }

        [Test]
        public void AbandonedRuinCannotReenterSessionOperationsButKeepsGridOccupied()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var presentation = new RecordingPresentation();
            GrayboxBuildingInstance3D ruin = Begin(
                session, BuildingCatalog.Wall, BuildingSite.Ground,
                CityMode.Fortress, 10, 10, presentation);
            BuildingEvacuationWork abandon = BuildingEvacuationRules.Create(
                ruin.StableInstanceId,
                ruin.Placement.Definition.Cost,
                ruin.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.Abandon);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { abandon }, out string captureFailure),
                Is.True, captureFailure);
            Assert.That(session.TryCommitEvacuation(
                abandon, presentation, out int refund, out string commitFailure),
                Is.True, commitFailure);
            float remainingAfterAbandon = ruin.Progress.Remaining;

            session.TickConstruction(
                100f, CityMode.Fortress, false, presentation);
            var ownedGround = new List<GrayboxBuildingInstance3D>();
            session.CopyPlayerOwnedGroundInstances(ownedGround);
            BuildingEvacuationWork attemptedRecovery =
                BuildingEvacuationRules.Create(
                    ruin.StableInstanceId,
                    ruin.Placement.Definition.Cost,
                    ruin.Progress.BaseDuration,
                    1d,
                    BuildingEvacuationTreatment.QuickDismantle);

            Assert.That(refund, Is.Zero);
            Assert.That(ruin.State,
                Is.EqualTo(GrayboxBuildingInstanceState.AbandonedRuin));
            Assert.That(ruin.Progress.Remaining,
                Is.EqualTo(remainingAfterAbandon));
            Assert.That(session.CompletedBuildingCount(
                BuildingCatalog.Wall.Id.Value), Is.Zero);
            Assert.That(session.HasPlayerOwnedGroundInstances, Is.False);
            Assert.That(ownedGround, Is.Empty);
            Assert.That(session.TryCaptureEvacuationWork(
                new[] { attemptedRecovery }, out string recoveryFailure),
                Is.False);
            Assert.That(recoveryFailure, Is.Not.Empty);
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.True);
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

        private static uint CatalogRevision(GrayboxBuildingSession3D session)
        {
            var property = typeof(GrayboxBuildingSession3D).GetProperty(
                "CatalogRevision");
            Assert.That(property, Is.Not.Null);
            return (uint)property.GetValue(session);
        }

        private static uint PlacementRevision(
            GrayboxBuildingSession3D session)
        {
            PropertyInfo property = typeof(GrayboxBuildingSession3D)
                .GetProperty("PlacementRevision");
            Assert.That(
                property,
                Is.Not.Null,
                "IDEA-0010 requires a non-persisted placement revision.");
            return (uint)property.GetValue(session);
        }

        private static void SetEvacuationState(
            GrayboxBuildingInstance3D instance,
            bool playerOwned,
            GrayboxBuildingInstanceState state)
        {
            MethodInfo method = typeof(GrayboxBuildingInstance3D).GetMethod(
                "RestoreEvacuationState",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(instance, new object[] { playerOwned, state });
        }

        private static GrayboxBuildingInstance3D Begin(
            GrayboxBuildingSession3D session,
            BuildingDefinition definition,
            BuildingSite site,
            CityMode mode,
            int x,
            int y,
            IGrayboxBuildingPresentation3D presentation)
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
            public Exception RemoveException { get; set; }
            public Exception UpdateException { get; set; }
            public int UpdateExceptionOnCall { get; set; } = 1;
            private int updateCalls;
            public List<GrayboxBuildingInstance3D> Created { get; } =
                new List<GrayboxBuildingInstance3D>();
            public List<GrayboxBuildingInstance3D> Updated { get; } =
                new List<GrayboxBuildingInstance3D>();
            public List<GrayboxBuildingInstance3D> Removed { get; } =
                new List<GrayboxBuildingInstance3D>();

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                Created.Add(instance);
                if (CreateException != null) throw CreateException;
                return CreateResult;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                updateCalls++;
                if (UpdateException != null &&
                    updateCalls == UpdateExceptionOnCall)
                    throw UpdateException;
                Updated.Add(instance);
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                Created.Remove(instance);
                Removed.Add(instance);
                if (RemoveException != null) throw RemoveException;
            }
        }

        private sealed class CancellationFaultPresentation :
            IGrayboxBuildingPresentation3D
        {
            private readonly bool throwAfterSideEffect;
            private readonly List<GrayboxBuildingInstance3D> existing =
                new List<GrayboxBuildingInstance3D>();
            private bool removeFaultArmed = true;
            private int createCalls;

            public CancellationFaultPresentation(bool throwAfterSideEffect)
            {
                this.throwAfterSideEffect = throwAfterSideEffect;
                RemoveFailure =
                    new InvalidOperationException("remove failed");
            }

            public InvalidOperationException RemoveFailure { get; }
            public Exception RestoreException { get; set; }

            public bool Contains(GrayboxBuildingInstance3D instance)
            {
                return existing.Contains(instance);
            }

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                createCalls++;
                if (createCalls > 1 && RestoreException != null)
                    throw RestoreException;
                if (existing.Contains(instance)) return false;
                existing.Add(instance);
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
            }

            public void Remove(GrayboxBuildingInstance3D instance)
            {
                if (removeFaultArmed)
                {
                    removeFaultArmed = false;
                    if (!throwAfterSideEffect) throw RemoveFailure;
                    existing.Remove(instance);
                    throw RemoveFailure;
                }
                existing.Remove(instance);
            }
        }
    }
}
