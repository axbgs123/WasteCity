using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionCombatLossTests
    {
        private const string DestroyedId =
            "building.instance.combat-production.destroyed";
        private const string OtherId =
            "building.instance.combat-production.other";

        [Test]
        public void CompletedStateCombatLossIsOneExactObservableCommit()
        {
            GrayboxBuildingInstance3D destroyed = CompletedInstance(
                DestroyedId,
                BuildingCatalog.Smelter,
                10,
                10);
            GrayboxBuildingInstance3D other = CompletedInstance(
                OtherId,
                BuildingCatalog.Smelter,
                12,
                10);
            var clock = new GrayboxProductionClock3D();
            GrayboxProductionRuntime3D runtime = clock.Runtime;
            runtime.Synchronize(
                new[] { other, destroyed },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.TryGetState(
                DestroyedId,
                out BuildingProductionState destroyedState), Is.True);
            Assert.That(runtime.TryGetState(
                OtherId,
                out BuildingProductionState otherState), Is.True);
            Assert.That(destroyedState.TryRestoreForPersistence(
                new[]
                {
                    new ResourceAmount(ResourceIds.EnergyCrystal, 5),
                    new ResourceAmount(ResourceIds.Iron, 4),
                },
                hasReservedInputs: true,
                new[] { new ResourceAmount(ResourceIds.Iron, 2) },
                new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 3),
                    new ResourceAmount(ResourceIds.Iron, 1),
                },
                progressSeconds: 1f,
                isPlayerPaused: false,
                out string destroyedError), Is.True, destroyedError);
            Assert.That(otherState.TryRestoreForPersistence(
                new[] { new ResourceAmount(ResourceIds.Stone, 6) },
                hasReservedInputs: false,
                Array.Empty<ResourceAmount>(),
                new[] { new ResourceAmount(ResourceIds.Alloy, 2) },
                progressSeconds: 0f,
                isPlayerPaused: true,
                out string otherError), Is.True, otherError);

            Publish(clock);
            ProductionObservabilitySnapshot beforeSnapshot = clock.Snapshot;
            ulong beforeRevision = clock.Revision;
            uint beforeCaptureCount = clock.ObservabilityCaptureCount;
            GrayboxProductionPersistenceState3D otherBefore =
                Capture(runtime, OtherId);

            bool destroyedNow = TryDestroyStateForCombat(
                runtime,
                DestroyedId,
                out ResourceAmount[] lostResources);
            Publish(clock);

            Assert.That(destroyedNow, Is.True);
            AssertAmounts(
                lostResources,
                new ResourceAmount(ResourceIds.Iron, 7),
                new ResourceAmount(ResourceIds.EnergyCrystal, 5),
                new ResourceAmount(ResourceIds.Alloy, 3));
            Assert.That(runtime.TryGetState(DestroyedId, out _), Is.False);
            Assert.That(runtime.States.Select(state => state.StableInstanceId),
                Does.Not.Contain(DestroyedId));
            Assert.That(
                runtime.RunnableStates.Select(state => state.StableInstanceId),
                Does.Not.Contain(DestroyedId));
            Assert.That(runtime.CaptureForPersistence()
                .Select(state => state.StableInstanceId),
                Does.Not.Contain(DestroyedId),
                "Combat loss must remove both live and orphan persistence caches.");
            Assert.That(OrphanCacheContains(runtime, DestroyedId), Is.False);
            AssertPersistenceState(Capture(runtime, OtherId), otherBefore);
            Assert.That(runtime.TryGetState(OtherId, out BuildingProductionState
                otherAfter), Is.True);
            Assert.That(otherAfter, Is.SameAs(otherState));
            Assert.That(runtime.States, Does.Contain(otherState));
            Assert.That(runtime.RunnableStates, Does.Contain(otherState));

            Assert.That(clock.Revision, Is.EqualTo(beforeRevision + 1),
                "One combat-loss commit must publish one new revision.");
            Assert.That(clock.ObservabilityCaptureCount,
                Is.EqualTo(beforeCaptureCount + 1));
            Assert.That(beforeSnapshot.TryGet(DestroyedId, out _), Is.True,
                "Published snapshots must remain immutable.");
            Assert.That(clock.Snapshot.TryGet(DestroyedId, out _), Is.False);
            Assert.That(clock.Snapshot.TryGet(OtherId, out _), Is.True);

            ProductionObservabilitySnapshot committedSnapshot = clock.Snapshot;
            ulong committedRevision = clock.Revision;
            uint committedCaptureCount = clock.ObservabilityCaptureCount;
            Assert.That(TryDestroyStateForCombat(
                runtime,
                DestroyedId,
                out ResourceAmount[] repeatedLoss), Is.False);
            Assert.That(repeatedLoss, Is.Not.Null);
            Assert.That(repeatedLoss, Is.Empty);
            Publish(clock);
            Assert.That(clock.Snapshot, Is.SameAs(committedSnapshot));
            Assert.That(clock.Revision, Is.EqualTo(committedRevision));
            Assert.That(clock.ObservabilityCaptureCount,
                Is.EqualTo(committedCaptureCount));
            AssertPersistenceState(Capture(runtime, OtherId), otherBefore);
        }

        [Test]
        public void CombatLossAlsoClearsCompatibleOrphanPersistenceState()
        {
            const string unknownStableResourceId =
                "mod.resource.legacy-dust";
            GrayboxBuildingInstance3D instance = CompletedInstance(
                DestroyedId,
                BuildingCatalog.Smelter,
                10,
                10);
            var runtime = new GrayboxProductionRuntime3D();
            runtime.Synchronize(
                new[] { instance },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);
            var opaque = new GrayboxProductionPersistenceState3D(
                DestroyedId,
                "mod.production.removed-recipe",
                new[]
                {
                    new ResourceAmount(ResourceIds.Stone, 2),
                    new ResourceAmount(unknownStableResourceId, 5),
                },
                hasReservedInputs: true,
                new[] { new ResourceAmount(ResourceIds.Iron, 3) },
                new[] { new ResourceAmount(ResourceIds.Alloy, 4) },
                progressSeconds: .5f,
                isPlayerPaused: false,
                boundResourceNodeId: null,
                boundNodeX: -1,
                boundNodeY: -1);
            Assert.That(runtime.TryPrepareRestore(
                new[] { opaque },
                new[] { instance },
                world: null,
                out GrayboxProductionRestorePlan3D plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(runtime.TryCommitRestore(plan, out string commitError),
                Is.True,
                commitError);
            Assert.That(runtime.States, Is.Empty);
            Assert.That(runtime.RunnableStates, Is.Empty);
            Assert.That(OrphanCacheContains(runtime, DestroyedId), Is.True);
            Assert.That(Capture(runtime, DestroyedId).DefinitionId,
                Is.EqualTo(opaque.DefinitionId));

            Assert.That(TryDestroyStateForCombat(
                runtime,
                DestroyedId,
                out ResourceAmount[] lostResources), Is.True);

            AssertAmounts(
                lostResources,
                new ResourceAmount(ResourceIds.Iron, 3),
                new ResourceAmount(ResourceIds.Stone, 2),
                new ResourceAmount(ResourceIds.Alloy, 4),
                new ResourceAmount(unknownStableResourceId, 5));
            Assert.That(
                Array.IndexOf(ResourceIds.All, unknownStableResourceId),
                Is.LessThan(0),
                "The compatibility assertion requires an unregistered ID.");
            Assert.That(
                lostResources[^1].ResourceId,
                Is.EqualTo(unknownStableResourceId),
                "Registered resources must retain ResourceIds.All order " +
                "before unknown IDs are appended in ordinal order.");
            Assert.That(OrphanCacheContains(runtime, DestroyedId), Is.False);
            Assert.That(runtime.CaptureForPersistence(), Is.Empty);
            Assert.That(TryDestroyStateForCombat(
                runtime,
                DestroyedId,
                out ResourceAmount[] repeatedLoss), Is.False);
            Assert.That(repeatedLoss, Is.Not.Null.And.Empty);
        }

        private static bool TryDestroyStateForCombat(
            GrayboxProductionRuntime3D runtime,
            string stableInstanceId,
            out ResourceAmount[] lostResources)
        {
            MethodInfo method = typeof(GrayboxProductionRuntime3D).GetMethod(
                "TryDestroyStateForCombat",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string),
                    typeof(ResourceAmount[]).MakeByRefType(),
                },
                null);
            Assert.That(method, Is.Not.Null,
                "Combat destruction requires " +
                "TryDestroyStateForCombat(string, out ResourceAmount[]).\n");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)));
            object[] arguments = { stableInstanceId, null };
            bool result = (bool)method.Invoke(runtime, arguments);
            lostResources = arguments[1] as ResourceAmount[];
            Assert.That(lostResources, Is.Not.Null,
                "Combat loss must always return a non-null loss array.");
            return result;
        }

        private static GrayboxProductionPersistenceState3D Capture(
            GrayboxProductionRuntime3D runtime,
            string stableInstanceId)
        {
            return runtime.CaptureForPersistence().Single(state =>
                string.Equals(
                    state.StableInstanceId,
                    stableInstanceId,
                    StringComparison.Ordinal));
        }

        private static void AssertPersistenceState(
            GrayboxProductionPersistenceState3D actual,
            GrayboxProductionPersistenceState3D expected)
        {
            Assert.That(actual.StableInstanceId,
                Is.EqualTo(expected.StableInstanceId));
            Assert.That(actual.DefinitionId, Is.EqualTo(expected.DefinitionId));
            Assert.That(actual.HasReservedInputs,
                Is.EqualTo(expected.HasReservedInputs));
            Assert.That(actual.ProgressSeconds,
                Is.EqualTo(expected.ProgressSeconds));
            Assert.That(actual.IsPlayerPaused,
                Is.EqualTo(expected.IsPlayerPaused));
            Assert.That(actual.BoundResourceNodeId,
                Is.EqualTo(expected.BoundResourceNodeId));
            Assert.That(actual.BoundNodeX, Is.EqualTo(expected.BoundNodeX));
            Assert.That(actual.BoundNodeY, Is.EqualTo(expected.BoundNodeY));
            AssertAmounts(actual.Input, expected.Input.ToArray());
            AssertAmounts(actual.ReservedInput, expected.ReservedInput.ToArray());
            AssertAmounts(actual.Output, expected.Output.ToArray());
        }

        private static void AssertAmounts(
            IReadOnlyList<ResourceAmount> actual,
            params ResourceAmount[] expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].ResourceId,
                    Is.EqualTo(expected[index].ResourceId),
                    "Combat losses must use ResourceIds.All stable order.");
                Assert.That(actual[index].Amount,
                    Is.EqualTo(expected[index].Amount),
                    expected[index].ResourceId);
            }
        }

        private static bool OrphanCacheContains(
            GrayboxProductionRuntime3D runtime,
            string stableInstanceId)
        {
            FieldInfo field = typeof(GrayboxProductionRuntime3D).GetField(
                "orphanStateById",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var cache = (IDictionary)field.GetValue(runtime);
            return cache.Contains(stableInstanceId);
        }

        private static void Publish(GrayboxProductionClock3D clock)
        {
            MethodInfo method = typeof(GrayboxProductionClock3D).GetMethod(
                "PublishObservabilityIfChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(clock, null);
        }

        private static GrayboxBuildingInstance3D CompletedInstance(
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
                        typeof(ResourceNodeBinding),
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
                    default(ResourceNodeBinding),
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, null);
            return instance;
        }
    }
}
