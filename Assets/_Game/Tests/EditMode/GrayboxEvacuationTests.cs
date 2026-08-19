using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Content;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

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
        public void Rules_FormalBatchContextContractIsImmutableAndRecordedInWork()
        {
            Type contextType = RequireEvacuationBatchContextType();
            Assert.That(contextType.IsValueType, Is.True);
            RequireReadOnlyProperty(contextType, "IsInCombat", typeof(bool));
            RequireReadOnlyProperty(
                contextType,
                "ProductivityMultiplier",
                typeof(float));
            RequireReadOnlyProperty(
                typeof(BuildingEvacuationWork),
                "BatchContext",
                contextType);
            RequireReadOnlyProperty(
                typeof(BuildingEvacuationWork),
                "BaseDismantleSeconds",
                typeof(float));
            RequireBatchContextFactory(contextType);
            RequireFormalWorkFactory(contextType);
        }

        [Test]
        public void Rules_FormalContextsApplyApprovedCompletedTreatmentValues()
        {
            object peace = CreateBatchContext(false, 2f);
            object combat = CreateBatchContext(true, 2f);
            BuildingEvacuationWork peaceFull = CreateFormalWork(
                "building.instance.formal-peace-full", 100, 18f, 1d,
                BuildingEvacuationTreatment.FullDismantle, peace);
            BuildingEvacuationWork combatFull = CreateFormalWork(
                "building.instance.formal-combat-full", 100, 18f, 1d,
                BuildingEvacuationTreatment.FullDismantle, combat);
            BuildingEvacuationWork quick = CreateFormalWork(
                "building.instance.formal-quick", 100, 18f, 1d,
                BuildingEvacuationTreatment.QuickDismantle, combat);
            BuildingEvacuationWork abandon = CreateFormalWork(
                "building.instance.formal-abandon", 100, 18f, 1d,
                BuildingEvacuationTreatment.Abandon, combat);

            Assert.That(peaceFull.Refund, Is.EqualTo(80));
            Assert.That(ReadFloat(peaceFull, "BaseDismantleSeconds"),
                Is.EqualTo(9f));
            Assert.That(peaceFull.DismantleSeconds, Is.EqualTo(4.5f));
            Assert.That(combatFull.Refund, Is.EqualTo(60));
            Assert.That(ReadFloat(combatFull, "BaseDismantleSeconds"),
                Is.EqualTo(5f));
            Assert.That(combatFull.DismantleSeconds, Is.EqualTo(2.5f));
            Assert.That(quick.Refund, Is.EqualTo(50));
            Assert.That(quick.DismantleSeconds, Is.Zero);
            Assert.That(abandon.Refund, Is.Zero);
            Assert.That(abandon.DismantleSeconds, Is.Zero);
        }

        [Test]
        public void Rules_FormalIncompleteRefundUsesSharedDeterministicRounding()
        {
            object peace = CreateBatchContext(false, 1f);
            object combat = CreateBatchContext(true, 1f);
            BuildingEvacuationWork peaceFull = CreateFormalWork(
                "building.instance.incomplete-peace", 25, 11f, .5d,
                BuildingEvacuationTreatment.FullDismantle, peace);
            BuildingEvacuationWork combatFull = CreateFormalWork(
                "building.instance.incomplete-combat", 25, 11f, .5d,
                BuildingEvacuationTreatment.FullDismantle, combat);
            BuildingEvacuationWork repeat = CreateFormalWork(
                "building.instance.incomplete-combat-repeat", 25, 11f, .5d,
                BuildingEvacuationTreatment.FullDismantle, combat);
            BuildingEvacuationWork quick = CreateFormalWork(
                "building.instance.incomplete-quick", 25, 11f, .5d,
                BuildingEvacuationTreatment.QuickDismantle, combat);

            Assert.That(peaceFull.Refund, Is.EqualTo(10));
            Assert.That(combatFull.Refund, Is.EqualTo(8),
                "25 × 0.5 × 0.6 = 7.5 rounds away from zero.");
            Assert.That(repeat.Refund, Is.EqualTo(combatFull.Refund));
            Assert.That(quick.Refund, Is.EqualTo(6));
        }

        [Test]
        public void Rules_WarningIsPeaceAndConfirmedWorkKeepsFrozenContext()
        {
            object warning = CreateBatchContext(false, 1.25f);
            BuildingEvacuationWork warningPreview = CreateFormalWork(
                "building.instance.warning-preview", 100, 10f, 1d,
                BuildingEvacuationTreatment.FullDismantle, warning);
            object confirmedCombat = CreateBatchContext(true, 2f);
            BuildingEvacuationWork confirmed = CreateFormalWork(
                "building.instance.confirmed-combat", 100, 20f, 1d,
                BuildingEvacuationTreatment.FullDismantle, confirmedCombat);

            _ = CreateBatchContext(false, 4f);
            object frozen = ReadProperty(confirmed, "BatchContext");

            Assert.That(warningPreview.Refund, Is.EqualTo(80));
            Assert.That(warningPreview.DismantleSeconds, Is.EqualTo(4f));
            Assert.That(confirmed.Refund, Is.EqualTo(60));
            Assert.That(confirmed.DismantleSeconds, Is.EqualTo(2.5f));
            Assert.That(ReadBool(frozen, "IsInCombat"), Is.True);
            Assert.That(ReadFloat(frozen, "ProductivityMultiplier"),
                Is.EqualTo(2f));
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
        public void Session_PayloadCommitReportsStableCapacityAndInvalidCodes()
        {
            Type codeType = RequireEvacuationCommitCodeType();
            MethodInfo commit = RequirePayloadCommitWithCode(codeType);
            var presentation = new RecordingPresentation();

            GrayboxBuildingSession3D capacitySession = CreateSession();
            GrayboxBuildingInstance3D capacityWall = Begin(
                capacitySession, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, presentation);
            capacitySession.Inventory.Set(
                BuildingCatalog.Wall.CostId,
                capacitySession.CityStorage.CoreCapacityPerResource);
            BuildingEvacuationWork capacityWork = BuildingEvacuationRules.Create(
                capacityWall.StableInstanceId,
                capacityWall.Placement.Definition.Cost,
                capacityWall.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);
            Assert.That(capacitySession.TryCaptureEvacuationWork(
                new[] { capacityWork }, out string capacityCaptureFailure),
                Is.True, capacityCaptureFailure);

            bool capacityCommitted = InvokePayloadCommitWithCode(
                commit,
                capacitySession,
                capacityWork,
                presentation,
                out string capacityFailureReason,
                out object capacityCode);

            Assert.That(capacityCommitted, Is.False);
            Assert.That(capacityFailureReason, Is.Not.Empty,
                "Failure text remains a display detail, not the controller protocol.");
            Assert.That(capacityCode.ToString(),
                Is.EqualTo("CapacityInsufficient"));
            Assert.That(capacitySession.Instances.Contains(capacityWall), Is.True);

            GrayboxBuildingSession3D invalidSession = CreateSession();
            GrayboxBuildingInstance3D invalidWall = Begin(
                invalidSession, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, presentation);
            BuildingEvacuationWork capturedWork = BuildingEvacuationRules.Create(
                invalidWall.StableInstanceId,
                invalidWall.Placement.Definition.Cost,
                invalidWall.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);
            BuildingEvacuationWork fabricatedWork = BuildingEvacuationRules.Create(
                invalidWall.StableInstanceId,
                invalidWall.Placement.Definition.Cost + 1,
                invalidWall.Progress.BaseDuration,
                1d,
                BuildingEvacuationTreatment.QuickDismantle);
            Assert.That(invalidSession.TryCaptureEvacuationWork(
                new[] { capturedWork }, out string invalidCaptureFailure),
                Is.True, invalidCaptureFailure);

            bool invalidCommitted = InvokePayloadCommitWithCode(
                commit,
                invalidSession,
                fabricatedWork,
                presentation,
                out string invalidFailureReason,
                out object invalidCode);

            Assert.That(invalidCommitted, Is.False);
            Assert.That(invalidFailureReason, Is.Not.Empty);
            Assert.That(invalidCode.ToString(), Is.EqualTo("Invalid"));
            Assert.That(invalidSession.Instances.Contains(invalidWall), Is.True);
        }

        [Test]
        public void Controller_UsesStableCommitCodeInsteadOfCapacityTextParsing()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building/" +
                "GrayboxEvacuationController3D.cs"));

            Assert.That(source, Does.Not.Contain("IsCapacityFailure"),
                "Task 5 capacity blocking must use the session's stable " +
                "commit code rather than a failure-text helper.");
            Assert.That(source, Does.Not.Contain("IndexOf(\n                    \"容量\""),
                "Task 5 must not classify Blocked by parsing localized text.");
        }

        [Test]
        public void Controller_ManifestViewIsImmutableDetailedAndCachedByRevision()
        {
            Type itemType = RequireEvacuationViewType(
                "EvacuationManifestItemViewModel");
            Type manifestType = RequireEvacuationViewType(
                "EvacuationManifestViewModel");
            RequireManifestViewContract(manifestType, itemType);

            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D wall = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);

            object first = CaptureEvacuationView(
                fixture.Controller,
                "CaptureManifestView",
                manifestType);
            object unchanged = CaptureEvacuationView(
                fixture.Controller,
                "CaptureManifestView",
                manifestType);

            Assert.That(unchanged, Is.SameAs(first),
                "No relevant revision change may rebuild a manifest snapshot.");
            Assert.That(ReadProperty(first, "IsInCombat"), Is.EqualTo(false));
            Assert.That(ReadProperty(first, "CanConfirm"), Is.EqualTo(true));
            object firstItem = ReadViewItems(first, "Items").Single();
            Assert.That(ReadProperty(firstItem, "StableInstanceId"),
                Is.EqualTo(wall.StableInstanceId));
            Assert.That(ReadProperty(firstItem, "Treatment"),
                Is.EqualTo(BuildingEvacuationTreatment.FullDismantle));

            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.QuickDismantle), Is.True);
            object changed = CaptureEvacuationView(
                fixture.Controller,
                "CaptureManifestView",
                manifestType);

            Assert.That(changed, Is.Not.SameAs(first));
            Assert.That(ReadProperty(firstItem, "Treatment"),
                Is.EqualTo(BuildingEvacuationTreatment.FullDismantle),
                "An already-published snapshot must never observe later " +
                "assignment mutation.");
            Assert.That(ReadProperty(
                ReadViewItems(changed, "Items").Single(),
                "Treatment"),
                Is.EqualTo(BuildingEvacuationTreatment.QuickDismantle));
        }

        [Test]
        public void Controller_ManifestWorkIndexMirrorsPreviewAndCleanup()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D first = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            GrayboxBuildingInstance3D second = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, fixture.Presentation);
            FieldInfo indexField = typeof(GrayboxEvacuationController3D)
                .GetField(
                    "workById",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(indexField, Is.Not.Null,
                "Manifest work lookup requires a stable-id index.");

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                first.StableInstanceId,
                BuildingEvacuationTreatment.QuickDismantle), Is.True);
            var index = (IDictionary)indexField.GetValue(fixture.Controller);
            Assert.That(index.Count, Is.EqualTo(1));
            Assert.That(index.Contains(first.StableInstanceId), Is.True);
            Assert.That(index.Contains(second.StableInstanceId), Is.False);

            Assert.That(fixture.Controller.Assign(
                second.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            Assert.That(index.Count, Is.EqualTo(2));
            Assert.That(index.Contains(first.StableInstanceId), Is.True);
            Assert.That(index.Contains(second.StableInstanceId), Is.True);
            Assert.That(fixture.Controller.Work, Has.Count.EqualTo(index.Count));

            Assert.That(fixture.Controller.TryCancelManifest(), Is.True);
            Assert.That(fixture.Controller.Work, Is.Empty);
            Assert.That(index.Count, Is.Zero,
                "Every work cleanup path must also clear the stable-id index.");
        }

        [Test]
        public void Controller_ManifestWorkLookupUsesDictionaryWithoutListScan()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Game/Scripts/Graybox3D/Building/" +
                "GrayboxEvacuationController3D.cs"));
            string method = ExtractMethodBlock(
                source,
                "private bool TryFindWork(");

            StringAssert.Contains("workById.TryGetValue", method);
            StringAssert.DoesNotContain("for (", method,
                "A manifest cache miss must not scan all evacuation work " +
                "for every item.");
            StringAssert.DoesNotContain("work.Count", method);
        }

        [Test]
        public void Controller_ManifestViewRefreshesForInternalProductionOutputAndTowerAmmoWithoutStorageRevision()
        {
            EvacuationFixture fixture = CreateFixture();
            fixture.Session.UnlockAllResearchForDevelopment();
            GrayboxBuildingInstance3D smelter = Begin(
                fixture.Session, BuildingCatalog.Smelter, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Begin(
                fixture.Session, BuildingCatalog.Assembler,
                BuildingSite.InnerCity, 0, 0, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            GrayboxBuildingInstance3D turret = Begin(
                fixture.Session, BuildingCatalog.MachineGunTurret,
                BuildingSite.Ground, 14, 10, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);

            var production = new GrayboxProductionRuntime3D();
            production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: fixture.Session.GroundBuildRadius,
                cityStorage: fixture.Session.CityStorage);
            Assert.That(production.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState productionState), Is.True);
            Assert.That(productionState.Output.Add(ResourceIds.Alloy, 3),
                Is.EqualTo(3));

            var defense = new GrayboxDefenseRuntime3D(10f, 10f, 20f, 10f);
            defense.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: fixture.Session.GroundBuildRadius);
            GrayboxDefenseTowerRuntimeState3D tower = defense.Towers.Single(
                value => value.StableId == turret.StableInstanceId);
            Assert.That(fixture.Session.CityStorage.AddToNetwork(
                ResourceIds.Ammunition,
                tower.Combat.AmmoCapacity),
                Is.EqualTo(tower.Combat.AmmoCapacity));
            Assert.That(tower.Combat.RefillFrom(fixture.Session.CityStorage),
                Is.EqualTo(tower.Combat.AmmoCapacity));
            ConfigureOperationalRuntimes(
                fixture.Controller,
                production,
                defense);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.QuickDismantle), Is.EqualTo(2));
            EvacuationManifestViewModel first =
                fixture.Controller.CaptureManifestView();
            EvacuationManifestItemViewModel firstSmelter = first.Items.Single(
                item => item.StableInstanceId == smelter.StableInstanceId);
            EvacuationManifestItemViewModel firstTurret = first.Items.Single(
                item => item.StableInstanceId == turret.StableInstanceId);
            Assert.That(firstSmelter.Output.Single(
                amount => amount.ResourceId == ResourceIds.Alloy).Amount,
                Is.EqualTo(3));
            Assert.That(firstTurret.AmmunitionAmount,
                Is.EqualTo(tower.Combat.AmmoCapacity));

            ulong storageRevision = fixture.Session.CityStorage.Revision;
            Assert.That(productionState.Output.Add(ResourceIds.Alloy, 1),
                Is.EqualTo(1));
            var target = new DefenseEnemyCombatModel(
                "evacuation-manifest-ammo-target",
                EnemyCatalog.Gnawer,
                turret.Placement.X,
                turret.Placement.Y);
            Assert.That(tower.Combat.Tick(.1f, target, globallyPaused: false),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(tower.Combat.Ammo,
                Is.EqualTo(tower.Combat.AmmoCapacity - 1));
            Assert.That(fixture.Session.CityStorage.Revision,
                Is.EqualTo(storageRevision),
                "The refresh must be driven by the production/defense owners, " +
                "not an unrelated city-storage mutation.");

            EvacuationManifestViewModel refreshed =
                fixture.Controller.CaptureManifestView();
            EvacuationManifestItemViewModel refreshedSmelter =
                refreshed.Items.Single(
                    item => item.StableInstanceId == smelter.StableInstanceId);
            EvacuationManifestItemViewModel refreshedTurret =
                refreshed.Items.Single(
                    item => item.StableInstanceId == turret.StableInstanceId);

            Assert.That(refreshed, Is.Not.SameAs(first));
            Assert.That(firstSmelter.Output.Single(
                amount => amount.ResourceId == ResourceIds.Alloy).Amount,
                Is.EqualTo(3),
                "Published manifest snapshots must remain immutable.");
            Assert.That(firstTurret.AmmunitionAmount,
                Is.EqualTo(tower.Combat.AmmoCapacity),
                "Published manifest snapshots must remain immutable.");
            Assert.That(refreshedSmelter.Output.Single(
                amount => amount.ResourceId == ResourceIds.Alloy).Amount,
                Is.EqualTo(4));
            Assert.That(refreshedTurret.AmmunitionAmount,
                Is.EqualTo(tower.Combat.AmmoCapacity - 1));
        }

        [Test]
        public void Controller_QueueViewRetainsFrozenBlockedBatchAndRetryProjection()
        {
            Type queueType = RequireEvacuationViewType(
                "EvacuationQueueViewModel");
            RequireQueueViewContract(queueType);

            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D wall = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            fixture.Session.Inventory.Set(
                BuildingCatalog.Wall.CostId,
                fixture.Session.CityStorage.CoreCapacityPerResource);
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            BuildingEvacuationWork frozen = fixture.Controller.Work.Single();
            fixture.Controller.Tick(
                frozen.DismantleSeconds + 1f,
                paused: false);

            object blocked = CaptureEvacuationView(
                fixture.Controller,
                "CaptureQueueView",
                queueType);
            object unchanged = CaptureEvacuationView(
                fixture.Controller,
                "CaptureQueueView",
                queueType);

            Assert.That(unchanged, Is.SameAs(blocked),
                "A blocked queue with no changed revision must reuse its " +
                "published immutable snapshot.");
            Assert.That(ReadProperty(blocked, "BatchId"), Is.Not.Empty);
            Assert.That(ReadProperty(blocked, "BatchIsInCombat"),
                Is.EqualTo(frozen.BatchContext.IsInCombat));
            Assert.That(ReadProperty(blocked, "BatchProductivityMultiplier"),
                Is.EqualTo(frozen.BatchContext.ProductivityMultiplier));
            Assert.That(ReadProperty(blocked, "CurrentStableInstanceId"),
                Is.EqualTo(wall.StableInstanceId));
            Assert.That(ReadProperty(blocked, "IsBlocked"), Is.EqualTo(true));
            Assert.That(ReadProperty(blocked, "CanRetry"), Is.EqualTo(true));
            Assert.That(ReadProperty(blocked, "LastFailureReason"), Is.Not.Empty);
            Assert.That(ReadProperty(blocked, "CapacityHint"), Does.Contain("E"));
            Assert.That(ReadViewItems(blocked, "CapacityShortfalls"), Is.Not.Empty);
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
        public void Controller_ManifestPreviewTracksAliveEnemiesAndConfirmationFreezesBatch()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D wall = Begin(
                fixture.Session,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                10,
                10,
                fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            var production = new GrayboxProductionRuntime3D();
            var defense = new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 9f,
                spawnZ: 0f);
            GrayboxBuildingInstance3D runtimeTurret =
                CreateCompletedRuntimeInstance(
                    "building.instance.preview-authority",
                    BuildingCatalog.MachineGunTurret,
                    4,
                    0);
            defense.Synchronize(
                new[] { runtimeTurret },
                CityMode.Fortress,
                cityX: 0,
                cityY: 0,
                groundRadius: fixture.Session.GroundBuildRadius);
            Assert.That(defense.TrySetPlayerPaused(
                runtimeTurret.StableInstanceId, true), Is.True);
            ConfigureOperationalRuntimes(
                fixture.Controller,
                production,
                defense);

            Assert.That(defense.Snapshot.AliveEnemyCount, Is.Zero,
                "Tutorial warning is peaceful until an enemy is actually alive.");
            Assert.That(defense.Snapshot.WarningRemainingSeconds,
                Is.GreaterThan(0f));
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            fixture.Controller.Tick(0f, paused: false);

            BuildingEvacuationWork peacefulPreview =
                fixture.Controller.Work.Single();
            Assert.That(peacefulPreview.StableInstanceId,
                Is.EqualTo(wall.StableInstanceId));
            Assert.That(peacefulPreview.BatchContext.IsInCombat, Is.False);
            Assert.That(peacefulPreview.BatchContext.ProductivityMultiplier,
                Is.EqualTo(fixture.Session.ProductivityMultiplier));
            Assert.That(peacefulPreview.Refund, Is.EqualTo(2));

            defense.Tick(
                20f,
                globallyPaused: false,
                cityStorage: fixture.Session.CityStorage);
            Assert.That(defense.Snapshot.AliveEnemyCount,
                Is.GreaterThan(0));
            fixture.Controller.Tick(0f, paused: false);

            BuildingEvacuationWork combatPreview =
                fixture.Controller.Work.Single();
            Assert.That(combatPreview.BatchContext.IsInCombat, Is.True);
            Assert.That(combatPreview.Refund, Is.EqualTo(1));
            Assert.That(combatPreview.BaseDismantleSeconds, Is.EqualTo(5f));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            BuildingEvacuationWork frozen = fixture.Controller.Work.Single();

            fixture.Session.SetPopulationForDevelopment(0);
            DefeatAllEnemies(defense, fixture.Session.CityStorage);
            Assert.That(defense.Snapshot.AliveEnemyCount, Is.Zero);
            fixture.Controller.Tick(0f, paused: false);

            Assert.That(fixture.Controller.Work.Single(), Is.EqualTo(frozen));
            Assert.That(frozen.BatchContext.IsInCombat, Is.True);
            Assert.That(frozen.BatchContext.ProductivityMultiplier,
                Is.Not.EqualTo(fixture.Session.ProductivityMultiplier));
            Assert.That(frozen.Refund, Is.EqualTo(combatPreview.Refund));
            Assert.That(frozen.BaseDismantleSeconds,
                Is.EqualTo(combatPreview.BaseDismantleSeconds));
            Assert.That(frozen.DismantleSeconds,
                Is.EqualTo(combatPreview.DismantleSeconds));
        }

        [Test]
        public void Controller_FullQueueUsesFrozenProductivityStableIdsAndRuleTimeOnlyAcceleratesTick()
        {
            EvacuationFixture fixture = CreateFixture();
            GrayboxBuildingInstance3D first = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            GrayboxBuildingInstance3D second = Begin(
                fixture.Session, BuildingCatalog.Wall, BuildingSite.Ground,
                12, 10, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            fixture.Session.SetConstructionMultiplierForDevelopment(2f);
            ReverseSessionInstances(fixture.Session);
            ConfigureOperationalRuntimes(
                fixture.Controller,
                new GrayboxProductionRuntime3D(),
                new GrayboxDefenseRuntime3D(0f, 0f, 9f, 0f));

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(2));
            fixture.Controller.Tick(0f, paused: false);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);

            BuildingEvacuationWork[] frozen =
                fixture.Controller.Work.ToArray();
            Assert.That(frozen.Select(value => value.StableInstanceId),
                Is.EqualTo(new[]
                {
                    first.StableInstanceId,
                    second.StableInstanceId
                }));
            Assert.That(frozen[0].BaseDismantleSeconds,
                Is.EqualTo(BuildingCatalog.Wall.BuildSeconds * .5f));
            Assert.That(frozen[0].DismantleSeconds,
                Is.EqualTo(
                    frozen[0].BaseDismantleSeconds /
                    frozen[0].BatchContext.ProductivityMultiplier)
                    .Within(.0001f));
            Assert.That(fixture.Controller.TryCancelManifest(), Is.False,
                "A confirmed batch cannot be cancelled.");
            fixture.Session.SetPopulationForDevelopment(0);

            float almostEnoughUnscaled =
                (frozen[0].DismantleSeconds - .02f) /
                fixture.Session.DevelopmentRuleTimeMultiplier;
            fixture.Controller.Tick(almostEnoughUnscaled, paused: false);
            Assert.That(fixture.Session.Instances.Contains(first), Is.True);
            Assert.That(fixture.Session.Instances.Contains(second), Is.True);
            fixture.Controller.Tick(100f, paused: true);
            Assert.That(fixture.Session.Instances.Contains(first), Is.True);
            Assert.That(fixture.Session.Instances.Contains(second), Is.True);

            fixture.Controller.Tick(.02f, paused: false);

            Assert.That(fixture.Session.Instances.Contains(first), Is.False);
            Assert.That(fixture.Session.Instances.Contains(second), Is.True);
            Assert.That(second.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Controller.Work, Is.EqualTo(frozen));
        }

        [Test]
        public void Controller_AtomicPayloadCommitFinalizesQuickAndDiscardsAbandonedRuntimeState()
        {
            EvacuationFixture fixture = CreateFixture();
            fixture.Session.UnlockAllResearchForDevelopment();
            GrayboxBuildingInstance3D quickSmelter = Begin(
                fixture.Session, BuildingCatalog.Smelter, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            GrayboxBuildingInstance3D abandonedSmelter = Begin(
                fixture.Session, BuildingCatalog.Smelter, BuildingSite.Ground,
                13, 10, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Begin(
                fixture.Session, BuildingCatalog.Assembler,
                BuildingSite.InnerCity, 0, 0, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            GrayboxBuildingInstance3D quickTurret = Begin(
                fixture.Session, BuildingCatalog.MachineGunTurret,
                BuildingSite.Ground, 10, 14, fixture.Presentation);
            GrayboxBuildingInstance3D abandonedTurret = Begin(
                fixture.Session, BuildingCatalog.MachineGunTurret,
                BuildingSite.Ground, 12, 14, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);

            var production = new GrayboxProductionRuntime3D();
            production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: fixture.Session.GroundBuildRadius,
                cityStorage: fixture.Session.CityStorage);
            Assert.That(production.TryGetState(
                quickSmelter.StableInstanceId,
                out BuildingProductionState quickProduction), Is.True);
            Assert.That(production.TryGetState(
                abandonedSmelter.StableInstanceId,
                out BuildingProductionState abandonedProduction), Is.True);
            Assert.That(quickProduction.Input.Add(ResourceIds.Iron, 2),
                Is.EqualTo(2));
            Assert.That(quickProduction.Output.Add(ResourceIds.Alloy, 3),
                Is.EqualTo(3));
            Assert.That(abandonedProduction.Input.Add(ResourceIds.Iron, 5),
                Is.EqualTo(5));
            Assert.That(abandonedProduction.Output.Add(ResourceIds.Alloy, 4),
                Is.EqualTo(4));

            var defense = new GrayboxDefenseRuntime3D(10f, 10f, 20f, 10f);
            defense.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: fixture.Session.GroundBuildRadius);
            fixture.Session.Inventory.Set(ResourceIds.Ammunition, 60);
            defense.Tick(.1f, globallyPaused: false,
                cityStorage: fixture.Session.CityStorage);
            Assert.That(defense.Towers.Single(value =>
                    value.StableId == quickTurret.StableInstanceId).Combat.Ammo,
                Is.EqualTo(30));
            Assert.That(defense.Towers.Single(value =>
                    value.StableId == abandonedTurret.StableInstanceId).Combat.Ammo,
                Is.EqualTo(30));
            ConfigureOperationalRuntimes(
                fixture.Controller,
                production,
                defense);
            int ironBefore = fixture.Session.Inventory.Get(ResourceIds.Iron);
            int alloyBefore = fixture.Session.Inventory.Get(ResourceIds.Alloy);
            int stoneBefore = fixture.Session.Inventory.Get(ResourceIds.Stone);
            int ammunitionBefore =
                fixture.Session.Inventory.Get(ResourceIds.Ammunition);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                quickSmelter.StableInstanceId,
                BuildingEvacuationTreatment.QuickDismantle), Is.True);
            Assert.That(fixture.Controller.Assign(
                abandonedSmelter.StableInstanceId,
                BuildingEvacuationTreatment.Abandon), Is.True);
            Assert.That(fixture.Controller.Assign(
                quickTurret.StableInstanceId,
                BuildingEvacuationTreatment.QuickDismantle), Is.True);
            Assert.That(fixture.Controller.Assign(
                abandonedTurret.StableInstanceId,
                BuildingEvacuationTreatment.Abandon), Is.True);
            fixture.Controller.Tick(0f, paused: false);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);

            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(ironBefore + 2));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(alloyBefore + 8));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Stone),
                Is.EqualTo(stoneBefore + 3));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Ammunition),
                Is.EqualTo(ammunitionBefore + 30));
            Assert.That(production.TryGetState(
                quickSmelter.StableInstanceId, out _), Is.False);
            Assert.That(production.TryGetState(
                abandonedSmelter.StableInstanceId, out _), Is.False);
            Assert.That(defense.Towers.Any(value =>
                value.StableId == quickTurret.StableInstanceId), Is.False);
            Assert.That(defense.Towers.Any(value =>
                value.StableId == abandonedTurret.StableInstanceId), Is.False);
            Assert.That(fixture.Session.Instances.Contains(quickSmelter), Is.False);
            Assert.That(fixture.Session.Instances.Contains(quickTurret), Is.False);
            Assert.That(abandonedSmelter.IsPlayerOwned, Is.False);
            Assert.That(abandonedTurret.IsPlayerOwned, Is.False);
        }

        [Test]
        public void Controller_BlockedPayloadRetryPreservesThenCommitsExactTowerAmmo()
        {
            EvacuationFixture fixture = CreateFixture();
            fixture.Session.UnlockAllResearchForDevelopment();
            GrayboxBuildingInstance3D smelter = Begin(
                fixture.Session, BuildingCatalog.Smelter, BuildingSite.Ground,
                10, 10, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            Begin(
                fixture.Session, BuildingCatalog.Assembler,
                BuildingSite.InnerCity, 0, 0, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);
            GrayboxBuildingInstance3D turret = Begin(
                fixture.Session, BuildingCatalog.MachineGunTurret,
                BuildingSite.Ground, 14, 10, fixture.Presentation);
            fixture.Session.CompleteAllConstructionForDevelopment(
                fixture.Presentation);

            var production = new GrayboxProductionRuntime3D();
            production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: fixture.Session.GroundBuildRadius,
                cityStorage: fixture.Session.CityStorage);
            var defense = new GrayboxDefenseRuntime3D(10f, 10f, 20f, 10f);
            defense.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                cityX: 10,
                cityY: 10,
                groundRadius: fixture.Session.GroundBuildRadius);
            fixture.Session.Inventory.Set(ResourceIds.Ammunition, 30);
            defense.Tick(.1f, globallyPaused: false,
                cityStorage: fixture.Session.CityStorage);
            GrayboxDefenseTowerRuntimeState3D towerState =
                defense.Towers.Single(value =>
                    value.StableId == turret.StableInstanceId);
            Assert.That(towerState.Combat.Ammo, Is.EqualTo(30));
            fixture.Session.Inventory.Set(
                ResourceIds.Ammunition,
                fixture.Session.CityStorage.CoreCapacityPerResource);
            ConfigureOperationalRuntimes(
                fixture.Controller,
                production,
                defense);

            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                smelter.StableInstanceId,
                BuildingEvacuationTreatment.Abandon), Is.True);
            Assert.That(fixture.Controller.Assign(
                turret.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            fixture.Controller.Tick(0f, paused: false);
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            BuildingEvacuationWork[] frozen =
                fixture.Controller.Work.ToArray();

            fixture.Controller.Tick(100f, paused: false);

            Assert.That(ReadControllerBool(
                fixture.Controller, "IsBlocked"), Is.True);
            Assert.That(ReadControllerString(
                fixture.Controller, "BlockedReason"), Is.Not.Empty);
            Assert.That(fixture.Controller.IsProcessing, Is.True);
            Assert.That(fixture.Controller.Work, Is.EqualTo(frozen));
            Assert.That(fixture.Session.Instances.Contains(turret), Is.True);
            Assert.That(turret.IsEvacuationLocked, Is.True);
            Assert.That(towerState.Combat.Ammo, Is.EqualTo(30));
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Ammunition),
                Is.EqualTo(fixture.Session.CityStorage.CoreCapacityPerResource));
            Assert.That(InvokeControllerBool(
                fixture.Controller, "RetryBlockedWork"), Is.False);
            Assert.That(towerState.Combat.Ammo, Is.EqualTo(30));

            Assert.That(fixture.Session.Inventory.TrySpend(
                ResourceIds.Ammunition, 30), Is.True);
            Assert.That(InvokeControllerBool(
                fixture.Controller, "RetryBlockedWork"), Is.True);

            Assert.That(ReadControllerBool(
                fixture.Controller, "IsBlocked"), Is.False);
            Assert.That(fixture.Session.Instances.Contains(turret), Is.False);
            Assert.That(defense.Towers.Any(value =>
                value.StableId == turret.StableInstanceId), Is.False);
            Assert.That(fixture.Session.Inventory.Get(ResourceIds.Ammunition),
                Is.EqualTo(fixture.Session.CityStorage.CoreCapacityPerResource));
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
        public void Controller_SerializedReferencesRestoreFirstEnableAndReenable()
        {
            EvacuationFixture fixture = CreateSerializedLifecycleFixture();
            GameObject controllerObject = fixture.Controller.gameObject;

            Assert.That(controllerObject.activeSelf, Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            controllerObject.SetActive(true);

            Assert.That(MenuSubscriberCount(
                fixture.Menu,
                fixture.Controller), Is.EqualTo(1));
            Assert.That(
                fixture.Controller.TryHandleDeploymentRequest(),
                Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));

            fixture.City.Deployment.Restore(CityMode.Fortress, 0f);
            controllerObject.SetActive(false);
            Assert.That(MenuSubscriberCount(
                fixture.Menu,
                fixture.Controller), Is.Zero);
            AssertSerializedReferences(fixture.Controller, fixture);

            controllerObject.SetActive(true);
            Assert.That(MenuSubscriberCount(
                fixture.Menu,
                fixture.Controller), Is.EqualTo(1));
            Assert.That(
                fixture.Controller.TryHandleDeploymentRequest(),
                Is.True);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
        }

        [Test]
        public void Controller_ConsumesFWhenSingleNoGroundDelegationFails()
        {
            EvacuationFixture fixture = CreateFixture(configureMenu: true);
            var deployment = new DeploymentRequestSpy(
                CityMode.Fortress,
                toggleResult: false,
                "展开失败：地面不稳定或有大型废墟");
            fixture.Controller.Configure(
                fixture.Session,
                deployment,
                fixture.Presentation,
                fixture.Menu);

            bool consumed = fixture.Controller.TryHandleDeploymentRequest();

            Assert.That(consumed, Is.True);
            Assert.That(deployment.ToggleCalls, Is.EqualTo(1));
            Assert.That(
                fixture.Menu.DeploymentFailureMessage,
                Is.EqualTo("展开失败：地面不稳定或有大型废墟"));
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
        public void Controller_CancellationBlockReleasesAfterCommittedPresentationFailure()
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

            Assert.DoesNotThrow(() => fixture.Controller.Tick(20f, false));

            Assert.That(fixture.Session.Instances.Contains(ground), Is.False);
            Assert.That(fixture.Controller.IsManifestOpen, Is.False);
            Assert.That(fixture.Controller.IsProcessing, Is.False);
            AssertConstructionButtons(constructionButtons, true);
            Assert.That(fixture.Menu.ConstructionCancellationBlocked, Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
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
                cleanupPath,
                oldFixture,
                newFixture);
            Assert.That(
                MenuSubscriberCount(
                    oldFixture.Menu,
                    oldFixture.Controller),
                Is.Zero);
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
            Assert.That(
                MenuSubscriberCount(
                    oldFixture.Menu,
                    oldFixture.Controller),
                Is.Zero);
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
            BuildingEvacuationWork firstWork = fixture.Controller.Work.Single(
                item => item.StableInstanceId == first.StableInstanceId);
            BuildingEvacuationWork secondWork = fixture.Controller.Work.Single(
                item => item.StableInstanceId == second.StableInstanceId);
            int stoneBeforeCommit = fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId);
            ulong storageRevisionBefore = fixture.Session.CityStorage.Revision;
            var presentation = new FailingPresentation { ThrowRemove = true };
            SetEvacuationPresentation(fixture.Controller, presentation);

            Assert.DoesNotThrow(() => fixture.Controller.Tick(20f, false));

            ulong storageRevisionAfterFirst =
                fixture.Session.CityStorage.Revision;
            Assert.That(fixture.Controller.IsProcessing, Is.True);
            Assert.That(fixture.Controller.IsManifestOpen, Is.False);
            Assert.That(fixture.Session.Instances.Contains(first), Is.False);
            Assert.That(fixture.Session.GroundGrid.IsOccupied(10, 10), Is.False);
            Assert.That(first.IsEvacuationLocked, Is.False);
            AssertEvacuationWorkConsumed(
                fixture.Session,
                first.StableInstanceId);
            Assert.That(fixture.Session.Instances.Contains(second), Is.True);
            Assert.That(fixture.Session.GroundGrid.IsOccupied(12, 10), Is.True);
            Assert.That(second.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId),
                Is.EqualTo(stoneBeforeCommit + firstWork.Refund));
            Assert.That(storageRevisionAfterFirst,
                Is.GreaterThan(storageRevisionBefore));
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Fortress));
            Assert.That(presentation.CreateCalls, Is.Zero);

            Assert.DoesNotThrow(() => fixture.Controller.Tick(20f, false));

            Assert.That(fixture.Controller.IsProcessing, Is.False);
            Assert.That(fixture.Controller.IsManifestOpen, Is.False);
            Assert.That(fixture.Session.Instances.Contains(second), Is.False);
            Assert.That(fixture.Session.GroundGrid.IsOccupied(12, 10), Is.False);
            Assert.That(second.IsEvacuationLocked, Is.False);
            AssertEvacuationWorkConsumed(
                fixture.Session,
                second.StableInstanceId);
            Assert.That(fixture.Session.CompletedBuildingCount(
                BuildingCatalog.Wall.Id.Value), Is.Zero);
            Assert.That(fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId),
                Is.EqualTo(
                    stoneBeforeCommit + firstWork.Refund + secondWork.Refund));
            Assert.That(fixture.Session.CityStorage.Revision,
                Is.GreaterThan(storageRevisionAfterFirst));
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
            Assert.That(presentation.RemoveCalls, Is.EqualTo(2));
            Assert.That(presentation.CreateCalls, Is.Zero);
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
            BuildingEvacuationWork firstWork = fixture.Controller.Work.Single(
                item => item.StableInstanceId == first.StableInstanceId);
            BuildingEvacuationWork secondWork = fixture.Controller.Work.Single(
                item => item.StableInstanceId == second.StableInstanceId);
            int stoneBeforeCommit = fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId);
            ulong storageRevisionBefore = fixture.Session.CityStorage.Revision;
            var presentation = new FailingPresentation
            {
                ThrowRemove = true,
                ThrowCreate = true
            };
            SetEvacuationPresentation(fixture.Controller, presentation);

            Assert.DoesNotThrow(() => fixture.Controller.Tick(20f, false));

            ulong storageRevisionAfterFirst =
                fixture.Session.CityStorage.Revision;
            Assert.That(first.IsEvacuationLocked, Is.False);
            Assert.That(fixture.Session.Instances.Contains(first), Is.False);
            Assert.That(fixture.Session.GroundGrid.IsOccupied(10, 10), Is.False);
            AssertEvacuationWorkConsumed(
                fixture.Session,
                first.StableInstanceId);
            Assert.That(second.IsEvacuationLocked, Is.True);
            Assert.That(fixture.Session.Instances.Contains(second), Is.True);
            Assert.That(fixture.Session.GroundGrid.IsOccupied(12, 10), Is.True);
            Assert.That(fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId),
                Is.EqualTo(stoneBeforeCommit + firstWork.Refund));
            Assert.That(storageRevisionAfterFirst,
                Is.GreaterThan(storageRevisionBefore));
            Assert.That(fixture.Controller.IsProcessing, Is.True);
            Assert.That(fixture.Controller.IsManifestOpen, Is.False);
            Assert.That(presentation.CreateCalls, Is.Zero,
                "Committed domain state must not call presentation.Create as rollback.");

            Assert.DoesNotThrow(() => fixture.Controller.Tick(20f, false));

            Assert.That(second.IsEvacuationLocked, Is.False);
            Assert.That(fixture.Session.Instances.Contains(second), Is.False);
            Assert.That(fixture.Session.GroundGrid.IsOccupied(12, 10), Is.False);
            AssertEvacuationWorkConsumed(
                fixture.Session,
                second.StableInstanceId);
            Assert.That(fixture.Session.Inventory.Get(
                BuildingCatalog.Wall.CostId),
                Is.EqualTo(
                    stoneBeforeCommit + firstWork.Refund + secondWork.Refund));
            Assert.That(fixture.Session.CityStorage.Revision,
                Is.GreaterThan(storageRevisionAfterFirst));
            Assert.That(fixture.Controller.IsProcessing, Is.False);
            Assert.That(fixture.Controller.IsManifestOpen, Is.False);
            Assert.That(fixture.City.Mode, Is.EqualTo(CityMode.Packing));
            Assert.That(presentation.RemoveCalls, Is.EqualTo(2));
            Assert.That(presentation.CreateCalls, Is.Zero);
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
        public void Menu_UpdateKeepsDependentCardLockedAfterCommittedPresentationFailure()
        {
            EvacuationFixture fixture = CreateFixture(configureMenu: true);
            var presenter = new GrayboxBuildingCatalogPresenter3D();
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
            Assert.That(presenter.Describe(
                    fixture.Session,
                    BuildingCatalog.Assembler).Visibility,
                Is.EqualTo(BuildingCatalogVisibility.Buildable));
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
            GrayboxBuildingCatalogItem3D queuedLocked = presenter.Describe(
                fixture.Session,
                BuildingCatalog.Assembler);
            Assert.That(queuedLocked.Visibility,
                Is.EqualTo(BuildingCatalogVisibility.Locked));
            Assert.That(queuedLocked.PrimaryLockReason,
                Is.Not.Null.And.Not.Empty);
            Assert.That(FindButton(
                fixture.Canvas.transform,
                "Catalog.Card." + BuildingCatalog.Assembler.Id.Value).interactable,
                Is.True);
            SetEvacuationPresentation(
                fixture.Controller,
                new FailingPresentation { ThrowRemove = true });

            Assert.DoesNotThrow(() => fixture.Controller.Tick(20f, false));
            InvokeMenuUpdate(fixture.Menu);

            Assert.That(fixture.Session.Instances.Contains(smelter), Is.False);
            GrayboxBuildingCatalogItem3D committedLocked = presenter.Describe(
                fixture.Session,
                BuildingCatalog.Assembler);
            Assert.That(committedLocked.Visibility,
                Is.EqualTo(BuildingCatalogVisibility.Locked));
            Assert.That(committedLocked.PrimaryLockReason,
                Is.EqualTo(queuedLocked.PrimaryLockReason));
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
        public void Session_PresentationFailureKeepsCommittedEvacuation()
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
            int stoneBeforeCommit = session.Inventory.Get(
                BuildingCatalog.Wall.CostId);
            bool committed = false;
            int acceptedRefund = -1;
            string failureReason = null;

            Assert.DoesNotThrow(() => committed = session.TryCommitEvacuation(
                work,
                presentation,
                out acceptedRefund,
                out failureReason));

            Assert.That(committed, Is.True);
            Assert.That(acceptedRefund, Is.EqualTo(work.Refund));
            Assert.That(failureReason,
                Is.EqualTo("撤离已提交，但表现需重建"));
            Assert.That(session.Inventory.Get(BuildingCatalog.Wall.CostId),
                Is.EqualTo(stoneBeforeCommit + work.Refund));
            Assert.That(session.Instances.Contains(wall), Is.False);
            Assert.That(session.GroundGrid.IsOccupied(10, 10), Is.False);
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

        private EvacuationFixture CreateSerializedLifecycleFixture()
        {
            GrayboxBuildingSession3D session = CreateSession();
            var cityObject = new GameObject("serialized-evacuation-city");
            cleanup.Add(cityObject);
            var city = cityObject.AddComponent<
                WasteCity.Graybox3D.GrayboxMobileCityController3D>();
            city.Deployment.Restore(CityMode.Fortress, 0f);

            var presentationObject =
                new GameObject("serialized-evacuation-presentation");
            cleanup.Add(presentationObject);
            var presentation = presentationObject.AddComponent<
                GrayboxBuildingWorldView3D>();
            var menuObject = new GameObject("serialized-evacuation-menu");
            cleanup.Add(menuObject);
            var menu = menuObject.AddComponent<GrayboxBuildingMenuView3D>();
            var controllerObject =
                new GameObject("serialized-evacuation-controller");
            controllerObject.SetActive(false);
            cleanup.Add(controllerObject);
            var controller = controllerObject.AddComponent<
                GrayboxEvacuationController3D>();
            controller.runInEditMode = true;
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("session").objectReferenceValue = session;
            serialized.FindProperty("city").objectReferenceValue = city;
            serialized.FindProperty("presentation").objectReferenceValue =
                presentation;
            serialized.FindProperty("menu").objectReferenceValue = menu;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return new EvacuationFixture(
                session,
                city,
                presentation,
                controller,
                menu,
                null,
                null);
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

        private static void ConfigureOperationalRuntimes(
            GrayboxEvacuationController3D controller,
            GrayboxProductionRuntime3D production,
            GrayboxDefenseRuntime3D defense)
        {
            MethodInfo method = typeof(GrayboxEvacuationController3D).GetMethod(
                "ConfigureOperationalRuntimes",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(GrayboxProductionRuntime3D),
                    typeof(GrayboxDefenseRuntime3D)
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Task 5 requires the evacuation controller to consume the " +
                "authoritative production and defense runtime owners.");
            method.Invoke(controller, new object[] { production, defense });
        }

        private static bool ReadControllerBool(
            GrayboxEvacuationController3D controller,
            string propertyName)
        {
            PropertyInfo property = typeof(GrayboxEvacuationController3D)
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "Missing Task 5 controller property: " + propertyName);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(bool)));
            Assert.That(property.CanWrite, Is.False);
            return (bool)property.GetValue(controller, null);
        }

        private static string ReadControllerString(
            GrayboxEvacuationController3D controller,
            string propertyName)
        {
            PropertyInfo property = typeof(GrayboxEvacuationController3D)
                .GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "Missing Task 5 controller property: " + propertyName);
            Assert.That(property.PropertyType, Is.EqualTo(typeof(string)));
            Assert.That(property.CanWrite, Is.False);
            return (string)property.GetValue(controller, null);
        }

        private static bool InvokeControllerBool(
            GrayboxEvacuationController3D controller,
            string methodName)
        {
            MethodInfo method = typeof(GrayboxEvacuationController3D).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null,
                "Missing Task 5 controller command: " + methodName);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            return (bool)method.Invoke(controller, null);
        }

        private static GrayboxBuildingInstance3D
            CreateCompletedRuntimeInstance(
                string stableInstanceId,
                BuildingDefinition definition,
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
                        typeof(ResourceNodeBinding)
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableInstanceId,
                    new PlacedBuilding(
                        definition,
                        x,
                        y,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(definition.BuildSeconds),
                    ResourceNodeBinding.None
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, null);
            return instance;
        }

        private static void DefeatAllEnemies(
            GrayboxDefenseRuntime3D defense,
            CityResourceStorageModel storage)
        {
            FieldInfo tutorialField = typeof(GrayboxDefenseRuntime3D).GetField(
                "tutorial",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(tutorialField, Is.Not.Null);
            object tutorial = tutorialField.GetValue(defense);
            PropertyInfo activeEnemiesProperty = tutorial.GetType().GetProperty(
                "ActiveEnemies",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(activeEnemiesProperty, Is.Not.Null);
            var enemies = (IEnumerable)activeEnemiesProperty.GetValue(
                tutorial,
                null);
            foreach (object enemy in enemies)
            {
                MethodInfo applyDamage = enemy.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(candidate =>
                        candidate.Name == "ApplyDamage" &&
                        candidate.GetParameters().Length == 2);
                Type damageType = applyDamage.GetParameters()[1].ParameterType;
                applyDamage.Invoke(
                    enemy,
                    new[]
                    {
                        (object)1000000,
                        Enum.Parse(damageType, "TrueEssence")
                    });
            }
            defense.Tick(.1f, globallyPaused: false, cityStorage: storage);
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
            string cleanupPath,
            EvacuationFixture original,
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
                if (cleanupPath == "OnDestroy")
                    Assert.That(value, Is.Null, fieldName);
                else if (cleanupPath == "OnDisable" &&
                         fieldName == "session")
                    Assert.That(value, Is.SameAs(original.Session));
                else if (cleanupPath == "OnDisable" &&
                         fieldName == "city")
                    Assert.That(value, Is.SameAs(original.City));
                else if (cleanupPath == "OnDisable" &&
                         fieldName == "presentation")
                    Assert.That(value, Is.SameAs(original.Presentation));
                else if (cleanupPath == "OnDisable" &&
                         fieldName == "menu")
                    Assert.That(value, Is.SameAs(original.Menu));
                else if (cleanupPath == "OnDisable")
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

        private static Type RequireEvacuationBatchContextType()
        {
            Type type = typeof(BuildingEvacuationRules).Assembly.GetType(
                "WasteCity.Building.EvacuationBatchContext",
                false);
            Assert.That(type, Is.Not.Null,
                "IDEA-0014 requires an immutable EvacuationBatchContext " +
                "in the pure rule assembly.");
            return type;
        }

        private static Type RequireEvacuationCommitCodeType()
        {
            Type type = typeof(GrayboxBuildingSession3D).Assembly.GetType(
                "WasteCity.Graybox3D.Building.GrayboxEvacuationCommitCode3D",
                false);
            Assert.That(type, Is.Not.Null,
                "Task 5 requires a public narrow commit code so orchestration " +
                "does not classify localized failure text.");
            Assert.That(type.IsEnum, Is.True);
            foreach (string name in new[]
                     {
                         "Completed",
                         "CapacityInsufficient",
                         "Invalid"
                     })
            {
                Assert.That(Enum.IsDefined(type, name), Is.True,
                    "Missing evacuation commit code: " + name);
            }
            return type;
        }

        private static Type RequireEvacuationViewType(string typeName)
        {
            Type type = typeof(GrayboxEvacuationController3D).Assembly.GetType(
                "WasteCity.Graybox3D.Building." + typeName,
                false);
            Assert.That(type, Is.Not.Null,
                "Task 6 requires the immutable evacuation view type: " +
                typeName);
            Assert.That(type.IsClass, Is.True);
            return type;
        }

        private static void RequireManifestViewContract(
            Type manifestType,
            Type itemType)
        {
            RequireReadOnlyProperty(manifestType, "Revision", typeof(ulong));
            RequireReadOnlyProperty(manifestType, "IsInCombat", typeof(bool));
            RequireReadOnlyProperty(
                manifestType, "ProductivityMultiplier", typeof(float));
            RequireReadOnlyProperty(manifestType, "CanConfirm", typeof(bool));
            RequireReadOnlyProperty(manifestType, "FailureReason", typeof(string));
            RequireReadOnlyProperty(
                manifestType,
                "Items",
                typeof(IReadOnlyList<>).MakeGenericType(itemType));
            RequireReadOnlyProperty(
                manifestType,
                "CapacityShortfalls",
                typeof(IReadOnlyList<ResourceAmount>));

            RequireReadOnlyProperty(itemType, "StableInstanceId", typeof(string));
            RequireReadOnlyProperty(itemType, "BuildingName", typeof(string));
            RequireReadOnlyProperty(
                itemType, "Category", typeof(BuildingMenuCategory));
            RequireReadOnlyProperty(
                itemType, "State", typeof(GrayboxBuildingInstanceState));
            RequireReadOnlyProperty(itemType, "RemainingRatio", typeof(double));
            RequireReadOnlyProperty(
                itemType, "Treatment", typeof(BuildingEvacuationTreatment));
            RequireReadOnlyProperty(
                itemType, "ExpectedRefunds", typeof(IReadOnlyList<ResourceAmount>));
            RequireReadOnlyProperty(
                itemType, "BaseDismantleSeconds", typeof(float));
            RequireReadOnlyProperty(
                itemType, "DismantleSeconds", typeof(float));
            RequireReadOnlyProperty(
                itemType, "Input", typeof(IReadOnlyList<ResourceAmount>));
            RequireReadOnlyProperty(
                itemType, "ReservedInput", typeof(IReadOnlyList<ResourceAmount>));
            RequireReadOnlyProperty(
                itemType, "Output", typeof(IReadOnlyList<ResourceAmount>));
            RequireReadOnlyProperty(itemType, "AmmunitionAmount", typeof(int));
            RequireReadOnlyProperty(
                itemType, "WarehouseContents", typeof(IReadOnlyList<ResourceAmount>));
            RequireReadOnlyProperty(
                itemType, "LostOnAbandon", typeof(IReadOnlyList<ResourceAmount>));
            RequireReadOnlyProperty(itemType, "CanCommit", typeof(bool));
            RequireReadOnlyProperty(
                itemType, "CapacityShortfalls", typeof(IReadOnlyList<ResourceAmount>));
            RequireReadOnlyProperty(itemType, "FailureReason", typeof(string));
            AssertViewTypeDoesNotOwnRuntimeOrRules(manifestType);
            AssertViewTypeDoesNotOwnRuntimeOrRules(itemType);
        }

        private static void RequireQueueViewContract(Type queueType)
        {
            RequireReadOnlyProperty(queueType, "Revision", typeof(ulong));
            RequireReadOnlyProperty(queueType, "BatchId", typeof(string));
            RequireReadOnlyProperty(
                queueType, "BatchIsInCombat", typeof(bool));
            RequireReadOnlyProperty(
                queueType, "BatchProductivityMultiplier", typeof(float));
            RequireReadOnlyProperty(queueType, "CompletedCount", typeof(int));
            RequireReadOnlyProperty(queueType, "TotalCount", typeof(int));
            RequireReadOnlyProperty(
                queueType, "CurrentStableInstanceId", typeof(string));
            RequireReadOnlyProperty(
                queueType, "RemainingBaseSeconds", typeof(float));
            RequireReadOnlyProperty(
                queueType, "RemainingActualSeconds", typeof(float));
            RequireReadOnlyProperty(queueType, "IsPaused", typeof(bool));
            RequireReadOnlyProperty(queueType, "IsBlocked", typeof(bool));
            RequireReadOnlyProperty(queueType, "CanRetry", typeof(bool));
            RequireReadOnlyProperty(
                queueType, "LastFailureReason", typeof(string));
            RequireReadOnlyProperty(queueType, "CapacityHint", typeof(string));
            RequireReadOnlyProperty(
                queueType,
                "CapacityShortfalls",
                typeof(IReadOnlyList<ResourceAmount>));
            AssertViewTypeDoesNotOwnRuntimeOrRules(queueType);
        }

        private static void AssertViewTypeDoesNotOwnRuntimeOrRules(Type type)
        {
            Type[] forbidden =
            {
                typeof(CityResourceStorageModel),
                typeof(GrayboxDefenseRuntime3D),
                typeof(ConstructionRefundRules)
            };
            foreach (MemberInfo member in type.GetMembers(
                         BindingFlags.Instance | BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                Type memberType = member is FieldInfo field
                    ? field.FieldType
                    : member is PropertyInfo property
                        ? property.PropertyType
                        : null;
                if (memberType == null) continue;
                Assert.That(Array.IndexOf(forbidden, memberType), Is.EqualTo(-1),
                    type.Name + "." + member.Name +
                    " must receive projected data, not own runtime/rule truth.");
            }
        }

        private static object CaptureEvacuationView(
            GrayboxEvacuationController3D controller,
            string methodName,
            Type expectedType)
        {
            MethodInfo method = typeof(GrayboxEvacuationController3D).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null,
                "Task 6 requires controller snapshot command " + methodName +
                "().");
            Assert.That(method.ReturnType, Is.EqualTo(expectedType));
            object view = method.Invoke(controller, null);
            Assert.That(view, Is.Not.Null);
            return view;
        }

        private static IEnumerable<object> ReadViewItems(
            object view,
            string propertyName)
        {
            object values = ReadProperty(view, propertyName);
            Assert.That(values, Is.InstanceOf<IEnumerable>());
            return ((IEnumerable)values).Cast<object>();
        }

        private static MethodInfo RequirePayloadCommitWithCode(Type codeType)
        {
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
                    codeType.MakeByRefType()
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Task 5 requires a payload commit overload that returns a " +
                "stable GrayboxEvacuationCommitCode3D.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            return method;
        }

        private static bool InvokePayloadCommitWithCode(
            MethodInfo method,
            GrayboxBuildingSession3D session,
            BuildingEvacuationWork work,
            IGrayboxBuildingPresentation3D presentation,
            out string failureReason,
            out object code)
        {
            object[] arguments =
            {
                work,
                Array.Empty<ResourceAmount>(),
                presentation,
                0,
                string.Empty,
                Enum.ToObject(
                    method.GetParameters()[5].ParameterType.GetElementType(),
                    0)
            };
            bool committed = (bool)method.Invoke(session, arguments);
            failureReason = (string)arguments[4];
            code = arguments[5];
            return committed;
        }

        private static MethodInfo RequireBatchContextFactory(Type contextType)
        {
            MethodInfo method = typeof(BuildingEvacuationRules).GetMethod(
                "CreateBatchContext",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(bool), typeof(float) },
                null);
            Assert.That(method, Is.Not.Null,
                "CreateBatchContext(bool, float) must freeze combat and " +
                "formal productivity.");
            Assert.That(method.ReturnType, Is.EqualTo(contextType));
            return method;
        }

        private static MethodInfo RequireFormalWorkFactory(Type contextType)
        {
            MethodInfo method = typeof(BuildingEvacuationRules).GetMethod(
                "Create",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(string), typeof(int), typeof(float), typeof(double),
                    typeof(BuildingEvacuationTreatment), contextType
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Create must accept the frozen EvacuationBatchContext.");
            return method;
        }

        private static object CreateBatchContext(
            bool isInCombat,
            float productivityMultiplier)
        {
            Type contextType = RequireEvacuationBatchContextType();
            return RequireBatchContextFactory(contextType).Invoke(
                null,
                new object[] { isInCombat, productivityMultiplier });
        }

        private static BuildingEvacuationWork CreateFormalWork(
            string stableInstanceId,
            int originalCost,
            float originalBuildSeconds,
            double remainingRatio,
            BuildingEvacuationTreatment treatment,
            object batchContext)
        {
            Assert.That(batchContext, Is.Not.Null);
            object value = RequireFormalWorkFactory(batchContext.GetType()).Invoke(
                null,
                new object[]
                {
                    stableInstanceId, originalCost, originalBuildSeconds,
                    remainingRatio, treatment, batchContext
                });
            Assert.That(value, Is.TypeOf<BuildingEvacuationWork>());
            return (BuildingEvacuationWork)value;
        }

        private static PropertyInfo RequireReadOnlyProperty(
            Type owner,
            string name,
            Type expectedType)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, owner.FullName + "." + name);
            Assert.That(property.PropertyType, Is.EqualTo(expectedType));
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.CanWrite, Is.False);
            return property;
        }

        private static object ReadProperty(object owner, string name)
        {
            Assert.That(owner, Is.Not.Null);
            PropertyInfo property = owner.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                owner.GetType().FullName + "." + name);
            Assert.That(property.CanWrite, Is.False);
            return property.GetValue(owner, null);
        }

        private static string ExtractMethodBlock(
            string source,
            string declaration)
        {
            int start = source.IndexOf(declaration, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), declaration);
            int openingBrace = source.IndexOf('{', start);
            Assert.That(openingBrace, Is.GreaterThanOrEqualTo(0));
            var depth = 0;
            for (int index = openingBrace; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}') depth--;
                if (depth == 0)
                    return source.Substring(start, index - start + 1);
            }
            throw new AssertionException("Unbalanced method: " + declaration);
        }

        private static bool ReadBool(object owner, string name)
        {
            return (bool)RequireReadOnlyProperty(
                owner.GetType(), name, typeof(bool)).GetValue(owner, null);
        }

        private static float ReadFloat(object owner, string name)
        {
            return (float)RequireReadOnlyProperty(
                owner.GetType(), name, typeof(float)).GetValue(owner, null);
        }

        private static void AssertSerializedReferences(
            GrayboxEvacuationController3D controller,
            EvacuationFixture fixture)
        {
            var serialized = new SerializedObject(controller);
            Assert.That(
                serialized.FindProperty("session").objectReferenceValue,
                Is.SameAs(fixture.Session));
            Assert.That(
                serialized.FindProperty("city").objectReferenceValue,
                Is.SameAs(fixture.City));
            Assert.That(
                serialized.FindProperty("presentation").objectReferenceValue,
                Is.SameAs(fixture.Presentation));
            Assert.That(
                serialized.FindProperty("menu").objectReferenceValue,
                Is.SameAs(fixture.Menu));
        }

        private static int MenuSubscriberCount(
            GrayboxBuildingMenuView3D menu,
            GrayboxEvacuationController3D controller)
        {
            FieldInfo field = typeof(GrayboxBuildingMenuView3D).GetField(
                "EvacuationConfirmationRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var listeners = field.GetValue(menu) as Delegate;
            if (listeners == null) return 0;
            return listeners.GetInvocationList().Count(
                listener => ReferenceEquals(listener.Target, controller));
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
            public int RemoveCalls { get; private set; }
            public int CreateCalls { get; private set; }
            public InvalidOperationException RemoveFailure { get; } =
                new InvalidOperationException("remove");
            public InvalidOperationException CreateFailure { get; } =
                new InvalidOperationException("create");
            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                CreateCalls++;
                if (ThrowCreate) throw CreateFailure;
                return true;
            }
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance)
            {
                RemoveCalls++;
                if (ThrowRemove) throw RemoveFailure;
            }
        }

        private sealed class DeploymentRequestSpy : IGrayboxDeploymentRequest3D
        {
            private readonly bool toggleResult;

            public DeploymentRequestSpy(
                CityMode mode,
                bool toggleResult,
                string failureReason = "rejected")
            {
                Mode = mode;
                this.toggleResult = toggleResult;
                this.failureReason = failureReason;
            }

            private readonly string failureReason;
            public CityMode Mode { get; }
            public int ToggleCalls { get; private set; }

            public bool TryToggleDeployment(out string failureReason)
            {
                ToggleCalls++;
                failureReason = toggleResult
                    ? string.Empty
                    : this.failureReason;
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
