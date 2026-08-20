using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveEvacuationTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building.GrayboxEvacuationSaveAdapter3D, " +
            "WasteCity.Graybox3D.Building";
        private const float Tolerance = .0001f;

        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void FormalEvacuationSaveAdapterExposesTransactionalContract()
        {
            Type type = RequireAdapterType();
            Assert.That(type.GetConstructor(
                new[] { typeof(GrayboxEvacuationController3D) }), Is.Not.Null);
            AssertMethod(type, "Capture", 0,
                typeof(FormalThreeDEvacuationSaveData));
            AssertMethod(type, "TryRestore", 2, typeof(bool));
            AssertMethod(type, "TryPrepareRestore", 3, typeof(bool));
            AssertMethod(type, "TryCommitRestore", 2, typeof(bool));
        }

        [Test]
        public void UnconfirmedManifestAndViewDraftAreNotPersisted()
        {
            Fixture fixture = CreateFixture();
            GrayboxBuildingInstance3D wall = BuildCompletedWall(fixture, 10, 10);
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.Assign(
                wall.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            EvacuationManifestViewModel view =
                fixture.Controller.CaptureManifestView();

            FormalThreeDEvacuationSaveData saved = Capture(
                Adapter(fixture.Controller));

            Assert.That(view.Items, Has.Count.EqualTo(1));
            Assert.That(saved.isProcessing, Is.False);
            Assert.That(saved.activeBatchId, Is.Null.Or.Empty);
            Assert.That(saved.batchContext, Is.Null);
            Assert.That(saved.work, Is.Empty);
            Assert.That(saved.fullQueueStableInstanceIds, Is.Empty);
            Assert.That(saved.runtimePayloads, Is.Empty);
            Assert.That(saved.lockedStableInstanceIds, Is.Empty);
            Assert.That(saved.pendingRollbackStableInstanceIds, Is.Empty);
        }

        [Test]
        public void ConfirmedBatchCaptureFreezesIdentityWorkQueueTimeAndLocks()
        {
            Fixture fixture = CreateFixture();
            GrayboxBuildingInstance3D later = BuildCompletedWall(fixture, 12, 10);
            GrayboxBuildingInstance3D earlier = BuildCompletedWall(fixture, 10, 10);
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle), Is.EqualTo(2));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
            fixture.Controller.Tick(.25f, paused: false);
            EvacuationQueueViewModel queue = fixture.Controller.CaptureQueueView();

            FormalThreeDEvacuationSaveData saved = Capture(
                Adapter(fixture.Controller));

            Assert.That(saved.isProcessing, Is.True);
            Assert.That(saved.activeBatchId, Is.EqualTo(queue.BatchId));
            long activeOrdinal = ParseBatchOrdinal(saved.activeBatchId);
            Assert.That(saved.nextBatchOrdinal, Is.GreaterThan(activeOrdinal),
                "The persisted next ordinal is the next unused high-water mark.");
            Assert.That(saved.batchContext, Is.Not.Null);
            Assert.That(saved.batchContext.isInCombat, Is.False);
            Assert.That(saved.batchContext.productivityMultiplier,
                Is.EqualTo(fixture.Session.ProductivityMultiplier)
                    .Within(Tolerance));
            CollectionAssert.AreEqual(
                new[] { earlier.StableInstanceId, later.StableInstanceId }
                    .OrderBy(value => value, StringComparer.Ordinal),
                saved.work.Select(item => item.stableInstanceId));
            CollectionAssert.AreEqual(
                saved.work.Select(item => item.stableInstanceId),
                saved.fullQueueStableInstanceIds);
            Assert.That(saved.currentQueueIndex, Is.Zero);
            Assert.That(saved.currentStableInstanceId,
                Is.EqualTo(saved.fullQueueStableInstanceIds[0]));
            Assert.That(saved.remainingSeconds,
                Is.EqualTo(queue.RemainingActualSeconds).Within(Tolerance));
            Assert.That(saved.work.All(item =>
                item.treatment ==
                (int)BuildingEvacuationTreatment.FullDismantle), Is.True);
            CollectionAssert.AreEquivalent(
                saved.fullQueueStableInstanceIds,
                saved.lockedStableInstanceIds);
            CollectionAssert.AreEquivalent(
                saved.fullQueueStableInstanceIds,
                saved.pendingRollbackStableInstanceIds);
        }

        [Test]
        public void CaptureDeepCopiesFrozenTransactionAndDoesNotUnlockOrRecapture()
        {
            Fixture fixture = CreateFixture();
            GrayboxBuildingInstance3D wall = BuildCompletedWall(fixture, 10, 10);
            ConfirmSingle(fixture, BuildingEvacuationTreatment.FullDismantle);
            object adapter = Adapter(fixture.Controller);

            FormalThreeDEvacuationSaveData first = Capture(adapter);
            int frozenRefund = first.work[0].refund;
            float frozenRemaining = first.remainingSeconds;
            first.work[0].refund = 999999;
            first.remainingSeconds = 0f;
            first.lockedStableInstanceIds[0] = "building.instance.999999";

            FormalThreeDEvacuationSaveData second = Capture(adapter);

            Assert.That(second.work[0].refund, Is.EqualTo(frozenRefund));
            Assert.That(second.remainingSeconds,
                Is.EqualTo(frozenRemaining).Within(Tolerance));
            Assert.That(second.lockedStableInstanceIds.Single(),
                Is.EqualTo(wall.StableInstanceId));
            Assert.That(wall.IsEvacuationLocked, Is.True,
                "Saving is observational and must not release the batch lock.");
            Assert.That(fixture.Controller.IsProcessing, Is.True);
        }

        [Test]
        public void RestoreContinuesTheExactBatchAndRemainingRuleTime()
        {
            Fixture source = CreateFixture();
            BuildCompletedWall(source, 10, 10);
            BuildCompletedWall(source, 12, 10);
            source.Controller.TryHandleDeploymentRequest();
            source.Controller.AssignAll(
                BuildingEvacuationTreatment.FullDismantle);
            Assert.That(source.Controller.ConfirmManifest(), Is.True);
            source.Controller.Tick(.25f, paused: false);
            FormalThreeDEvacuationSaveData saved = Capture(
                Adapter(source.Controller));

            Fixture target = CreateFixture();
            BuildCompletedWall(target, 10, 10);
            BuildCompletedWall(target, 12, 10);
            Assert.That(TryRestore(
                Adapter(target.Controller), saved, out string error),
                Is.True, error);

            FormalThreeDEvacuationSaveData restored = Capture(
                Adapter(target.Controller));
            Assert.That(restored.activeBatchId, Is.EqualTo(saved.activeBatchId));
            Assert.That(restored.nextBatchOrdinal,
                Is.EqualTo(saved.nextBatchOrdinal));
            Assert.That(restored.currentQueueIndex,
                Is.EqualTo(saved.currentQueueIndex));
            Assert.That(restored.currentStableInstanceId,
                Is.EqualTo(saved.currentStableInstanceId));
            Assert.That(restored.remainingSeconds,
                Is.EqualTo(saved.remainingSeconds).Within(Tolerance));
            target.Controller.Tick(.1f, paused: false);
            Assert.That(Capture(Adapter(target.Controller)).remainingSeconds,
                Is.EqualTo(saved.remainingSeconds - .1f).Within(Tolerance),
                "Restore must continue the active item instead of restarting it.");
        }

        [Test]
        public void RestorePreservesBlockedIdentityButRecomputesCapacityShortfall()
        {
            Fixture source = CreateFixture();
            GrayboxBuildingInstance3D sourceWall =
                BuildCompletedWall(source, 10, 10);
            source.Session.Inventory.Set(
                BuildingCatalog.Wall.CostId,
                source.Session.CityStorage.CoreCapacityPerResource);
            ConfirmSingle(source, BuildingEvacuationTreatment.QuickDismantle);
            Assert.That(source.Controller.IsBlocked, Is.True);
            FormalThreeDEvacuationSaveData saved = Capture(
                Adapter(source.Controller));
            Assert.That(saved.isBlocked, Is.True);
            Assert.That(saved.blockedStableInstanceId,
                Is.EqualTo(sourceWall.StableInstanceId));
            Assert.That(saved.blockedCode, Is.Not.Null.And.Not.Empty);

            Fixture target = CreateFixture();
            GrayboxBuildingInstance3D targetWall =
                BuildCompletedWall(target, 10, 10);
            target.Session.Inventory.Set(BuildingCatalog.Wall.CostId, 0);
            Assert.That(TryRestore(
                Adapter(target.Controller), saved, out string error),
                Is.True, error);

            EvacuationQueueViewModel restoredView =
                target.Controller.CaptureQueueView();
            Assert.That(restoredView.BatchId, Is.EqualTo(saved.activeBatchId));
            Assert.That(restoredView.IsBlocked, Is.True,
                "Loading must not silently skip the blocked transaction.");
            Assert.That(restoredView.CurrentStableInstanceId,
                Is.EqualTo(targetWall.StableInstanceId));
            Assert.That(restoredView.CapacityShortfalls, Is.Empty,
                "Shortfall amounts are derived from restored storage, not saved.");
            Assert.That(target.Controller.RetryBlockedWork(), Is.True);
            Assert.That(target.Session.Instances.Contains(targetWall), Is.False);
        }

        [Test]
        public void MissingStableInstanceRejectsBeforeMutatingControllerOrLocks()
        {
            Fixture source = CreateFixture();
            BuildCompletedWall(source, 10, 10);
            ConfirmSingle(source, BuildingEvacuationTreatment.FullDismantle);
            FormalThreeDEvacuationSaveData saved = Capture(
                Adapter(source.Controller));

            Fixture target = CreateFixture();
            object targetAdapter = Adapter(target.Controller);
            Assert.That(TryRestore(targetAdapter, saved, out string error),
                Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(target.Controller.IsProcessing, Is.False);
            Assert.That(target.Controller.IsBlocked, Is.False);
            Assert.That(target.Session.Instances, Is.Empty);
            Assert.That(Capture(targetAdapter).activeBatchId, Is.Null.Or.Empty);
        }

        [Test]
        public void ExistingPlaceholderDefinitionKeepsFrozenWorkLockAndPayload()
        {
            const string stableId = "building.instance.000001";
            Fixture target = CreateFixture();
            var missingDefinition = new BuildingDefinition(
                "missing.building.retired-factory",
                "缺失内容占位体",
                1,
                1,
                ResourceIds.Stone,
                7,
                buildSeconds: 9f);
            var entry = new GrayboxBuildingRestoreEntry3D(
                stableId,
                missingDefinition,
                BuildingSite.Ground,
                10,
                10,
                BuildingOrientation.North,
                GrayboxBuildingInstanceState.Completed,
                0f,
                isPlayerOwned: true,
                isEvacuationLocked: false,
                boundResourceNode: default);
            Assert.That(target.Session.TryRestoreBuildings(
                new[] { entry },
                2,
                target.Presentation,
                out string buildingError), Is.True, buildingError);
            FormalThreeDEvacuationSaveData saved = ProcessingData(stableId);

            Assert.That(TryRestore(
                Adapter(target.Controller), saved, out string error),
                Is.True, error);
            FormalThreeDEvacuationSaveData restored = Capture(
                Adapter(target.Controller));

            Assert.That(restored.work.Single().stableInstanceId,
                Is.EqualTo(stableId));
            Assert.That(restored.work.Single().refund, Is.EqualTo(6));
            Assert.That(target.Session.Instances.Single().IsEvacuationLocked,
                Is.True);
            Assert.That(restored.runtimePayloads.Single().resourcePayload.Single()
                .amount, Is.EqualTo(3));
        }

        [Test]
        public void ProductionPayloadRestoresToMissingDefinitionPlaceholderAndQuickRefundsOnce()
        {
            Fixture source = CreateFixture();
            source.Session.UnlockAllResearchForDevelopment();
            GrayboxBuildingInstance3D smelter = BuildCompleted(
                source,
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            var production = new GrayboxProductionRuntime3D();
            production.Synchronize(
                source.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                source.Session.GroundBuildRadius,
                source.Session.CityStorage);
            Assert.That(production.TryGetState(
                smelter.StableInstanceId,
                out BuildingProductionState productionState), Is.True);
            Assert.That(productionState.Input.Add(ResourceIds.Iron, 2),
                Is.EqualTo(2));
            Assert.That(productionState.Output.Add(ResourceIds.Alloy, 3),
                Is.EqualTo(3));
            source.Controller.ConfigureOperationalRuntimes(
                production,
                new GrayboxDefenseRuntime3D(10f, 10f, 20f, 10f));
            source.Session.Inventory.Set(
                ResourceIds.Iron,
                source.Session.CityStorage.CoreCapacityPerResource);
            ConfirmSingle(source, BuildingEvacuationTreatment.QuickDismantle);
            Assert.That(source.Controller.IsBlocked, Is.True);
            FormalThreeDEvacuationSaveData saved = Capture(
                Adapter(source.Controller));
            FormalThreeDEvacuationRuntimePayloadSaveData payload =
                saved.runtimePayloads.Single();
            AssertAmounts(payload.productionInputAmounts, ResourceIds.Iron, 2);
            AssertAmounts(payload.productionOutputAmounts, ResourceIds.Alloy, 3);
            Assert.That(payload.resourcePayload.Sum(item => item.amount),
                Is.EqualTo(5));

            Fixture target = CreateFixture();
            GrayboxBuildingInstance3D placeholder = RestorePlaceholder(
                target,
                smelter.StableInstanceId,
                ResourceIds.Stone,
                nextOrdinal: 2);
            int ironBefore = target.Session.Inventory.Get(ResourceIds.Iron);
            int alloyBefore = target.Session.Inventory.Get(ResourceIds.Alloy);
            int stoneBefore = target.Session.Inventory.Get(ResourceIds.Stone);

            Assert.That(TryRestore(
                Adapter(target.Controller), saved, out string error),
                Is.True, error);
            Assert.That(target.Controller.IsBlocked, Is.True);
            Assert.That(target.Controller.RetryBlockedWork(), Is.True);
            Assert.That(target.Session.Instances.Contains(placeholder), Is.False);
            Assert.That(target.Session.Inventory.Get(ResourceIds.Iron),
                Is.EqualTo(ironBefore + 2));
            Assert.That(target.Session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(alloyBefore + 3));
            Assert.That(target.Session.Inventory.Get(ResourceIds.Stone),
                Is.EqualTo(stoneBefore + saved.work.Single().refund));
            int[] committed = ResourceIds.All.Select(
                target.Session.Inventory.Get).ToArray();
            Assert.That(target.Controller.RetryBlockedWork(), Is.False);
            target.Controller.Tick(100f, paused: false);
            CollectionAssert.AreEqual(
                committed,
                ResourceIds.All.Select(
                    target.Session.Inventory.Get).ToArray());
        }

        [Test]
        public void DefensePayloadRestoresToMissingDefinitionPlaceholderAndFullRefundsOnce()
        {
            Fixture source = CreateFixture();
            source.Session.UnlockAllResearchForDevelopment();
            GrayboxBuildingInstance3D smelter = BuildCompleted(
                source,
                BuildingCatalog.Smelter,
                BuildingSite.Ground,
                10,
                10);
            BuildCompleted(
                source,
                BuildingCatalog.Assembler,
                BuildingSite.InnerCity,
                0,
                0);
            GrayboxBuildingInstance3D turret = BuildCompleted(
                source,
                BuildingCatalog.MachineGunTurret,
                BuildingSite.Ground,
                14,
                10);
            var defense = new GrayboxDefenseRuntime3D(
                10f, 10f, 20f, 10f);
            defense.Synchronize(
                source.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                source.Session.GroundBuildRadius);
            source.Session.Inventory.Set(ResourceIds.Ammunition, 30);
            defense.Tick(
                .1f,
                globallyPaused: false,
                source.Session.CityStorage);
            source.Controller.ConfigureOperationalRuntimes(
                new GrayboxProductionRuntime3D(),
                defense);
            Assert.That(source.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(source.Controller.Assign(
                smelter.StableInstanceId,
                BuildingEvacuationTreatment.Abandon), Is.True);
            Assert.That(source.Controller.Assign(
                turret.StableInstanceId,
                BuildingEvacuationTreatment.FullDismantle), Is.True);
            Assert.That(source.Controller.ConfirmManifest(), Is.True);
            FormalThreeDEvacuationSaveData saved = Capture(
                Adapter(source.Controller));
            FormalThreeDEvacuationRuntimePayloadSaveData payload =
                saved.runtimePayloads.Single();
            Assert.That(payload.stableInstanceId,
                Is.EqualTo(turret.StableInstanceId));
            Assert.That(payload.hasDefensePayload, Is.True);
            Assert.That(payload.towerAmmunitionAmount, Is.EqualTo(30));
            AssertAmounts(
                payload.resourcePayload,
                ResourceIds.Ammunition,
                30);

            Fixture target = CreateFixture();
            GrayboxBuildingInstance3D placeholder = RestorePlaceholder(
                target,
                turret.StableInstanceId,
                ResourceIds.Alloy,
                nextOrdinal: 4);
            int ammunitionBefore =
                target.Session.Inventory.Get(ResourceIds.Ammunition);
            int alloyBefore = target.Session.Inventory.Get(ResourceIds.Alloy);
            int refund = saved.work.Single(item =>
                item.stableInstanceId == turret.StableInstanceId).refund;

            Assert.That(TryRestore(
                Adapter(target.Controller), saved, out string error),
                Is.True, error);
            target.Controller.Tick(100f, paused: false);
            Assert.That(target.Session.Instances.Contains(placeholder), Is.False);
            Assert.That(target.Session.Inventory.Get(ResourceIds.Ammunition),
                Is.EqualTo(ammunitionBefore + 30));
            Assert.That(target.Session.Inventory.Get(ResourceIds.Alloy),
                Is.EqualTo(alloyBefore + refund));
            int[] committed = ResourceIds.All.Select(
                target.Session.Inventory.Get).ToArray();
            target.Controller.Tick(100f, paused: false);
            CollectionAssert.AreEqual(
                committed,
                ResourceIds.All.Select(
                    target.Session.Inventory.Get).ToArray());
        }

        private Fixture CreateFixture()
        {
            GrayboxBuildingSession3D session = AddComponent<
                GrayboxBuildingSession3D>("SaveEvacuation.Session");
            session.Configure(true);
            session.ConfigureDevelopmentFixture();
            GrayboxBuildingMenuView3D menu = AddComponent<
                GrayboxBuildingMenuView3D>("SaveEvacuation.Menu");
            GrayboxEvacuationController3D controller = AddComponent<
                GrayboxEvacuationController3D>("SaveEvacuation.Controller");
            var presentation = new RecordingPresentation();
            controller.Configure(
                session,
                new FortressDeploymentRequest(),
                presentation,
                menu);
            return new Fixture(session, controller, presentation);
        }

        private GrayboxBuildingInstance3D BuildCompletedWall(
            Fixture fixture,
            int x,
            int y)
        {
            return BuildCompleted(
                fixture,
                BuildingCatalog.Wall,
                BuildingSite.Ground,
                x,
                y);
        }

        private GrayboxBuildingInstance3D BuildCompleted(
            Fixture fixture,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y)
        {
            BuildingGrid grid = site == BuildingSite.InnerCity
                ? fixture.Session.InnerGrid
                : fixture.Session.GroundGrid;
            var request = new BuildingPlacementRequest(
                definition,
                grid,
                site,
                BuildingOrientation.North,
                x,
                y,
                12,
                12,
                fixture.Session.GroundBuildRadius,
                CityMode.Fortress,
                projectionSucceeded: true,
                footprintTouchesCity: false,
                terrainPassable: true,
                obstacleFree: true,
                coversCompatibleResourceNode: true,
                compatibleResourceNodeId: null,
                contentVisible: true,
                unlock: BuildingUnlockModel.Evaluate(
                    definition,
                    fixture.Session.Population,
                    fixture.Session.IsResearchCompleted,
                    fixture.Session.CompletedBuildingCount),
                canAfford: true);
            Assert.That(fixture.Session.TryBeginConstruction(
                request,
                fixture.Presentation,
                out GrayboxBuildingInstance3D instance,
                out BuildingPlacementEvaluation evaluation),
                Is.True,
                evaluation.PrimaryFailure.ToString());
            fixture.Session.SetConstructionMultiplierForDevelopment(100f);
            fixture.Session.TickConstruction(
                1f,
                CityMode.Fortress,
                paused: false,
                fixture.Presentation);
            fixture.Session.SetConstructionMultiplierForDevelopment(1f);
            Assert.That(instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            return instance;
        }

        private static GrayboxBuildingInstance3D RestorePlaceholder(
            Fixture fixture,
            string stableInstanceId,
            string costResourceId,
            int nextOrdinal)
        {
            var missingDefinition = new BuildingDefinition(
                "missing.building.restored-evacuation-owner",
                "缺失内容占位体",
                1,
                1,
                costResourceId,
                1,
                buildSeconds: 1f);
            var entry = new GrayboxBuildingRestoreEntry3D(
                stableInstanceId,
                missingDefinition,
                BuildingSite.Ground,
                10,
                10,
                BuildingOrientation.North,
                GrayboxBuildingInstanceState.Completed,
                0f,
                isPlayerOwned: true,
                isEvacuationLocked: false,
                boundResourceNode: default);
            Assert.That(fixture.Session.TryRestoreBuildings(
                new[] { entry },
                nextOrdinal,
                fixture.Presentation,
                out string error), Is.True, error);
            return fixture.Session.Instances.Single();
        }

        private static void ConfirmSingle(
            Fixture fixture,
            BuildingEvacuationTreatment treatment)
        {
            Assert.That(fixture.Controller.TryHandleDeploymentRequest(), Is.True);
            Assert.That(fixture.Controller.AssignAll(treatment), Is.EqualTo(1));
            Assert.That(fixture.Controller.ConfirmManifest(), Is.True);
        }

        private static object Adapter(GrayboxEvacuationController3D controller)
        {
            Type type = RequireAdapterType();
            ConstructorInfo constructor = type.GetConstructor(
                new[] { typeof(GrayboxEvacuationController3D) });
            Assert.That(constructor, Is.Not.Null,
                "The adapter must use the evacuation controller as rule owner.");
            return constructor.Invoke(new object[] { controller });
        }

        private static Type RequireAdapterType()
        {
            Type type = Type.GetType(AdapterTypeName);
            Assert.That(type, Is.Not.Null,
                "Task 9 requires a dedicated confirmed-evacuation save adapter.");
            return type;
        }

        private static FormalThreeDEvacuationSaveData Capture(object adapter)
        {
            MethodInfo method = FindMethod(adapter, "Capture", 0);
            return (FormalThreeDEvacuationSaveData)Invoke(
                method,
                adapter,
                Array.Empty<object>());
        }

        private static bool TryRestore(
            object adapter,
            FormalThreeDEvacuationSaveData data,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, "TryRestore", 2);
            object[] arguments = { data, null };
            bool restored = (bool)Invoke(method, adapter, arguments);
            error = arguments[1] as string;
            return restored;
        }

        private static MethodInfo FindMethod(
            object instance,
            string name,
            int parameterCount)
        {
            MethodInfo method = instance.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate =>
                    candidate.Name == name &&
                    candidate.GetParameters().Length == parameterCount);
            Assert.That(method, Is.Not.Null,
                instance.GetType().FullName + "." + name);
            return method;
        }

        private static object Invoke(
            MethodInfo method,
            object instance,
            object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void AssertMethod(
            Type type,
            string name,
            int parameterCount,
            Type returnType)
        {
            MethodInfo method = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate =>
                    candidate.Name == name &&
                    candidate.GetParameters().Length == parameterCount);
            Assert.That(method, Is.Not.Null, type.FullName + "." + name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
        }

        private static long ParseBatchOrdinal(string batchId)
        {
            const string prefix = "evacuation.batch.";
            Assert.That(batchId, Does.StartWith(prefix));
            Assert.That(long.TryParse(
                batchId.Substring(prefix.Length), out long ordinal), Is.True);
            return ordinal;
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

        private static FormalThreeDResourceAmountSaveData[] Amounts(
            string resourceId,
            int amount)
        {
            return new[] { Amount(resourceId, amount) };
        }

        private static void AssertAmounts(
            FormalThreeDResourceAmountSaveData[] amounts,
            string resourceId,
            int amount)
        {
            Assert.That(amounts, Has.Length.EqualTo(1));
            Assert.That(amounts[0].resourceId, Is.EqualTo(resourceId));
            Assert.That(amounts[0].amount, Is.EqualTo(amount));
        }

        private static FormalThreeDEvacuationSaveData ProcessingData(
            string stableId)
        {
            return new FormalThreeDEvacuationSaveData
            {
                nextBatchOrdinal = 43,
                activeBatchId = "evacuation.batch.000042",
                isProcessing = true,
                batchContext = new FormalThreeDEvacuationBatchContextSaveData
                {
                    isInCombat = true,
                    productivityMultiplier = 2f,
                },
                work = new[]
                {
                    new FormalThreeDEvacuationWorkSaveData
                    {
                        stableInstanceId = stableId,
                        treatment = (int)BuildingEvacuationTreatment.FullDismantle,
                        remainingRatio = 1d,
                        baseDismantleSeconds = 5f,
                        dismantleSeconds = 2.5f,
                        refund = 6,
                    }
                },
                fullQueueStableInstanceIds = new[] { stableId },
                currentQueueIndex = 0,
                currentStableInstanceId = stableId,
                remainingSeconds = 1.25f,
                isBlocked = false,
                blockedCode = string.Empty,
                blockedStableInstanceId = string.Empty,
                runtimePayloads = new[]
                {
                    new FormalThreeDEvacuationRuntimePayloadSaveData
                    {
                        stableInstanceId = stableId,
                        productionInputAmounts = Array.Empty<
                            FormalThreeDResourceAmountSaveData>(),
                        productionReservedInputs = Array.Empty<
                            FormalThreeDResourceAmountSaveData>(),
                        productionOutputAmounts = Array.Empty<
                            FormalThreeDResourceAmountSaveData>(),
                        resourcePayload = Amounts(ResourceIds.Stone, 3),
                    }
                },
                lockedStableInstanceIds = new[] { stableId },
                pendingRollbackStableInstanceIds = new[] { stableId },
            };
        }

        private T AddComponent<T>(string name) where T : Component
        {
            var value = new GameObject(name);
            cleanup.Add(value);
            return value.AddComponent<T>();
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }

        private sealed class FortressDeploymentRequest :
            IGrayboxDeploymentRequest3D
        {
            public CityMode Mode => CityMode.Fortress;

            public bool TryToggleDeployment(out string failureReason)
            {
                failureReason = string.Empty;
                return true;
            }
        }

        private readonly struct Fixture
        {
            public Fixture(
                GrayboxBuildingSession3D session,
                GrayboxEvacuationController3D controller,
                IGrayboxBuildingPresentation3D presentation)
            {
                Session = session;
                Controller = controller;
                Presentation = presentation;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxEvacuationController3D Controller { get; }
            public IGrayboxBuildingPresentation3D Presentation { get; }
        }
    }
}
