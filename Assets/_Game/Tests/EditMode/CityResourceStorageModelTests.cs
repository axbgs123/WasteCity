using NUnit.Framework;
using WasteCity.Economy;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace WasteCity.Tests
{
    public sealed class CityResourceStorageModelTests
    {
        [Test]
        public void WarehousesHaveIsolatedSharedCapacityAndResourceFilters()
        {
            CityResourceStorageModel storage = CreateStorage();
            Assert.That(storage.TryRegisterWarehouse("warehouse.iron"), Is.True);
            Assert.That(storage.TryRegisterWarehouse("warehouse.stone"), Is.True);
            Assert.That(storage.TrySetWarehouseFilter(
                "warehouse.iron", ResourceIds.Iron), Is.True);
            Assert.That(storage.TrySetWarehouseFilter(
                "warehouse.stone", ResourceIds.Stone), Is.True);

            Assert.That(storage.AddToNetwork(ResourceIds.Iron, 180),
                Is.EqualTo(180));
            Assert.That(storage.AddToNetwork(ResourceIds.Stone, 180),
                Is.EqualTo(180));

            Assert.That(storage.GetWarehouseAmount(
                "warehouse.iron", ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.iron", ResourceIds.Stone), Is.Zero);
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.stone", ResourceIds.Stone), Is.EqualTo(150));
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.stone", ResourceIds.Iron), Is.Zero);
            Assert.That(storage.GetCoreAmount(ResourceIds.Iron), Is.EqualTo(30));
            Assert.That(storage.GetCoreAmount(ResourceIds.Stone), Is.EqualTo(30));
        }

        [Test]
        public void FilterDoesNotChangeCapacityAndRejectsIncompatibleContents()
        {
            CityResourceStorageModel storage = CreateStorage();
            Assert.That(storage.TryRegisterWarehouse("warehouse.alpha"), Is.True);
            Assert.That(storage.AddToWarehouse(
                "warehouse.alpha", ResourceIds.Iron, 40), Is.EqualTo(40));

            Assert.That(storage.TrySetWarehouseFilter(
                "warehouse.alpha", ResourceIds.Stone), Is.False);
            Assert.That(storage.GetWarehouseFilter("warehouse.alpha"), Is.Null);
            Assert.That(storage.TrySetWarehouseFilter(
                "warehouse.alpha", ResourceIds.Iron), Is.True);
            Assert.That(storage.GetWarehouseCapacity("warehouse.alpha"),
                Is.EqualTo(WarehouseStorageState.FormalCapacity));
            Assert.That(storage.GetWarehouseFreeSpace("warehouse.alpha"),
                Is.EqualTo(110));
            Assert.That(storage.TrySetWarehouseFilter(
                "warehouse.alpha", null), Is.True);
            Assert.That(storage.GetWarehouseCapacity("warehouse.alpha"),
                Is.EqualTo(150));
        }

        [Test]
        public void DisconnectedWarehouseRetainsContentsButLeavesNetworkAggregate()
        {
            ResourceInventory core = new ResourceInventory(1000);
            core.Add(ResourceIds.Iron, 10);
            var storage = new CityResourceStorageModel(core, 150);
            storage.TryRegisterWarehouse("warehouse.alpha", connected: true);
            storage.TrySetWarehouseFilter("warehouse.alpha", ResourceIds.Iron);
            storage.AddToWarehouse("warehouse.alpha", ResourceIds.Iron, 50);

            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(60));
            Assert.That(storage.GetNetworkAcceptableSpace(ResourceIds.Iron),
                Is.EqualTo(240));

            Assert.That(storage.TrySetWarehouseConnected(
                "warehouse.alpha", connected: false), Is.True);

            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(storage.GetNetworkAcceptableSpace(ResourceIds.Iron),
                Is.EqualTo(140));
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.alpha", ResourceIds.Iron), Is.EqualTo(50));
        }

        [Test]
        public void NetworkRoutingIsStableByWarehouseIdRegardlessOfRegistrationOrder()
        {
            CityResourceStorageModel storage = CreateStorage();
            storage.TryRegisterWarehouse("warehouse.b");
            storage.TryRegisterWarehouse("warehouse.a");

            Assert.That(storage.AddToNetwork(ResourceIds.Iron, 170),
                Is.EqualTo(170));

            Assert.That(storage.GetWarehouseAmount(
                "warehouse.a", ResourceIds.Iron), Is.EqualTo(150));
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.b", ResourceIds.Iron), Is.EqualTo(20));
            Assert.That(storage.GetCoreAmount(ResourceIds.Iron), Is.Zero);
        }

        [Test]
        public void ExactSpendAndBatchFailureRollBackEveryStorageLocation()
        {
            ResourceInventory core = new ResourceInventory(1000);
            core.Add(ResourceIds.Iron, 5);
            var storage = new CityResourceStorageModel(core, 150);
            storage.TryRegisterWarehouse("warehouse.alpha");
            storage.AddToWarehouse("warehouse.alpha", ResourceIds.Iron, 5);

            Assert.That(storage.TrySpendFromNetwork(ResourceIds.Iron, 11),
                Is.False);
            Assert.That(storage.GetCoreAmount(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.alpha", ResourceIds.Iron), Is.EqualTo(5));

            ulong revisionBefore = storage.Revision;
            Assert.That(storage.TryCommitBatch(
                new[] { new ResourceAmount(ResourceIds.Iron, 10) },
                new[] { new ResourceAmount(ResourceIds.Stone, 301) }), Is.False);
            Assert.That(storage.Revision, Is.EqualTo(revisionBefore));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(10));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Stone), Is.Zero);

            Assert.That(storage.TryCommitBatch(
                new[] { new ResourceAmount(ResourceIds.Iron, 10) },
                new[] { new ResourceAmount(ResourceIds.Stone, 300) }), Is.True);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.Zero);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Stone), Is.EqualTo(300));
        }

        [Test]
        public void AcceptableSpaceUsesSharedWarehouseSpaceOnlyOncePerResourceQuery()
        {
            ResourceInventory core = new ResourceInventory(1000);
            core.Add(ResourceIds.Stone, 140);
            var storage = new CityResourceStorageModel(core, 150);
            storage.TryRegisterWarehouse("warehouse.alpha");
            storage.AddToWarehouse("warehouse.alpha", ResourceIds.Iron, 100);

            Assert.That(storage.GetNetworkAcceptableSpace(ResourceIds.Stone),
                Is.EqualTo(60));
            Assert.That(storage.GetNetworkAcceptableSpace(ResourceIds.Iron),
                Is.EqualTo(200));
            Assert.That(storage.GetNetworkCapacityLimit(ResourceIds.Stone),
                Is.EqualTo(200));
            Assert.That(storage.GetNetworkCapacityLimit(ResourceIds.Iron),
                Is.EqualTo(300));

            Assert.That(storage.TrySetWarehouseFilter(
                "warehouse.alpha", ResourceIds.Iron), Is.True);
            Assert.That(storage.GetNetworkAcceptableSpace(ResourceIds.Stone),
                Is.EqualTo(10));
            Assert.That(storage.GetNetworkAcceptableSpace(ResourceIds.Iron),
                Is.EqualTo(200));
            Assert.That(storage.GetNetworkCapacityLimit(ResourceIds.Stone),
                Is.EqualTo(150));
            Assert.That(storage.GetNetworkCapacityLimit(ResourceIds.Iron),
                Is.EqualTo(300));

            core.Set(ResourceIds.Stone, 157);
            Assert.That(storage.GetNetworkCapacityLimit(ResourceIds.Stone),
                Is.EqualTo(150),
                "Existing overage must not inflate the displayed capacity.");
        }

        [Test]
        public void SnapshotIsImmutableAndRevisionChangesOnlyWithContent()
        {
            CityResourceStorageModel storage = CreateStorage();
            storage.TryRegisterWarehouse("warehouse.alpha");
            CityResourceStorageSnapshot before = storage.CaptureSnapshot();

            Assert.That(storage.AddToWarehouse(
                "warehouse.alpha", ResourceIds.Iron, 12), Is.EqualTo(12));
            CityResourceStorageSnapshot after = storage.CaptureSnapshot();

            Assert.That(after.Revision, Is.GreaterThan(before.Revision));
            Assert.That(before.GetWarehouseAmount(
                "warehouse.alpha", ResourceIds.Iron), Is.Zero);
            Assert.That(after.GetWarehouseAmount(
                "warehouse.alpha", ResourceIds.Iron), Is.EqualTo(12));

            ulong stableRevision = storage.Revision;
            Assert.That(storage.AddToWarehouse(
                "warehouse.alpha", ResourceIds.Iron, 0), Is.Zero);
            Assert.That(storage.TrySetWarehouseConnected(
                "warehouse.alpha", connected: true), Is.True);
            Assert.That(storage.Revision, Is.EqualTo(stableRevision));

            Assert.That(storage.TryCommitBatch(
                new[] { new ResourceAmount(ResourceIds.Iron, 12) },
                new[] { new ResourceAmount(ResourceIds.Iron, 12) }), Is.True);
            Assert.That(storage.Revision, Is.EqualTo(stableRevision),
                "A transaction whose final physical contents are unchanged must not publish a revision.");

            Assert.That(storage.TrySetWarehouseConnected(
                "warehouse.alpha", connected: false), Is.True);
            Assert.That(storage.Revision, Is.GreaterThan(stableRevision));
            Assert.That(before.GetWarehouseAmount(
                "warehouse.alpha", ResourceIds.Iron), Is.Zero);
        }

        [Test]
        public void NonEmptyWarehouseCannotBeRemovedAndNeverLosesContents()
        {
            CityResourceStorageModel storage = CreateStorage();
            storage.TryRegisterWarehouse("warehouse.alpha");
            storage.AddToWarehouse("warehouse.alpha", ResourceIds.Alloy, 8);

            Assert.That(storage.TryRemoveWarehouse("warehouse.alpha"), Is.False);
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.alpha", ResourceIds.Alloy), Is.EqualTo(8));
            Assert.That(storage.TrySpendFromWarehouse(
                "warehouse.alpha", ResourceIds.Alloy, 8), Is.True);
            Assert.That(storage.TryRemoveWarehouse("warehouse.alpha"), Is.True);
            Assert.That(storage.TryGetWarehouseSnapshot(
                "warehouse.alpha", out _), Is.False);
        }

        [Test]
        public void RemovalMigratesContentsAtomicallyOrLeavesEverythingUntouched()
        {
            ResourceInventory core = new ResourceInventory(1000);
            var storage = new CityResourceStorageModel(core, 10);
            storage.TryRegisterWarehouse("warehouse.source");
            storage.TryRegisterWarehouse("warehouse.target");
            storage.TrySetWarehouseFilter(
                "warehouse.target", ResourceIds.Iron);
            storage.AddToWarehouse(
                "warehouse.source", ResourceIds.Iron, 12);

            Assert.That(storage.TryRemoveWarehouseWithMigration(
                "warehouse.source",
                out WarehouseRemovalStatus completed), Is.True);
            Assert.That(completed, Is.EqualTo(WarehouseRemovalStatus.Completed));
            Assert.That(storage.TryGetWarehouseSnapshot(
                "warehouse.source", out _), Is.False);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(12));
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.target", ResourceIds.Iron), Is.EqualTo(12));

            storage.TryRegisterWarehouse("warehouse.blocked");
            storage.AddToWarehouse(
                "warehouse.blocked", ResourceIds.Stone, 150);
            core.Set(ResourceIds.Stone, 10);
            ulong revisionBefore = storage.Revision;

            Assert.That(storage.TryRemoveWarehouseWithMigration(
                "warehouse.blocked",
                out WarehouseRemovalStatus rejected), Is.False);
            Assert.That(rejected,
                Is.EqualTo(WarehouseRemovalStatus.InsufficientNetworkSpace));
            Assert.That(storage.Revision, Is.EqualTo(revisionBefore));
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.blocked", ResourceIds.Stone), Is.EqualTo(150));
            Assert.That(storage.GetCoreAmount(ResourceIds.Stone), Is.EqualTo(10));
        }

        [Test]
        public void EvacuationPlanAggregatesSourceContentsAndRefundWithExactShortfall()
        {
            var core = new ResourceInventory(1000);
            core.Add(ResourceIds.Alloy, 10);
            var storage = new CityResourceStorageModel(core, 10);
            Assert.That(storage.TryRegisterWarehouse("warehouse.source"),
                Is.True);
            Assert.That(storage.TryRegisterWarehouse("warehouse.target"),
                Is.True);
            Assert.That(storage.AddToWarehouse(
                "warehouse.source", ResourceIds.Iron, 5), Is.EqualTo(5));
            Assert.That(storage.AddToWarehouse(
                "warehouse.target", ResourceIds.Stone, 148), Is.EqualTo(148));
            Assert.That(storage.TryGetWarehouseSnapshot(
                "warehouse.target",
                out WarehouseStorageSnapshot target), Is.True);
            Assert.That(target.IsConnected, Is.True);
            Assert.That(target.FilterResourceId, Is.Null);
            Assert.That(target.FreeSpace, Is.EqualTo(2));

            object plan = CreateEvacuationPlan(
                storage,
                "warehouse.source",
                new[] { new ResourceAmount(ResourceIds.Alloy, 4) });

            Assert.That(ReadPlanBool(plan, "CanCommit"), Is.False);
            Assert.That(ReadPlanUlong(plan, "PreparedRevision"),
                Is.EqualTo(storage.Revision));
            Assert.That(InvokePlanAmount(
                plan, "GetIncomingAmount", ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(InvokePlanAmount(
                plan, "GetIncomingAmount", ResourceIds.Alloy), Is.EqualTo(4));
            Assert.That(InvokePlanAmount(
                plan, "GetShortfall", ResourceIds.Iron), Is.Zero);
            Assert.That(InvokePlanAmount(
                plan, "GetShortfall", ResourceIds.Alloy), Is.EqualTo(2));
            Assert.That(ReadPlanInt(plan, "TotalShortfall"), Is.EqualTo(2));
        }

        [Test]
        public void EvacuationPlanRejectsStaleRevisionAndRepreflightsLatestCapacity()
        {
            var core = new ResourceInventory(1000);
            var storage = new CityResourceStorageModel(core, 10);
            storage.TryRegisterWarehouse("warehouse.source");
            storage.AddToWarehouse(
                "warehouse.source", ResourceIds.Iron, 5);
            object plan = CreateEvacuationPlan(
                storage,
                "warehouse.source",
                new[] { new ResourceAmount(ResourceIds.Alloy, 4) });
            Assert.That(ReadPlanBool(plan, "CanCommit"), Is.True);

            Assert.That(storage.AddToNetwork(ResourceIds.Alloy, 10),
                Is.EqualTo(10));
            ulong revisionAfterCapacityChange = storage.Revision;

            Assert.That(TryCommitEvacuationPlan(
                storage, plan, out string status), Is.False);
            Assert.That(status, Is.EqualTo("StalePlan"));
            Assert.That(storage.Revision, Is.EqualTo(revisionAfterCapacityChange));
            Assert.That(storage.ContainsWarehouse("warehouse.source"), Is.True);
            Assert.That(storage.GetWarehouseAmount(
                "warehouse.source", ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Alloy), Is.EqualTo(10));

            object refreshed = CreateEvacuationPlan(
                storage,
                "warehouse.source",
                new[] { new ResourceAmount(ResourceIds.Alloy, 4) });
            Assert.That(ReadPlanBool(refreshed, "CanCommit"), Is.False);
            Assert.That(InvokePlanAmount(
                refreshed, "GetShortfall", ResourceIds.Alloy), Is.EqualTo(4));
        }

        [Test]
        public void EvacuationPlanCommitsMigrationPayloadAndRemovalExactlyOnce()
        {
            var core = new ResourceInventory(1000);
            var storage = new CityResourceStorageModel(core, 10);
            storage.TryRegisterWarehouse("warehouse.source");
            storage.TryRegisterWarehouse("warehouse.target");
            storage.AddToWarehouse(
                "warehouse.source", ResourceIds.Iron, 5);
            object plan = CreateEvacuationPlan(
                storage,
                "warehouse.source",
                new[] { new ResourceAmount(ResourceIds.Alloy, 4) });
            Assert.That(ReadPlanBool(plan, "CanCommit"), Is.True);
            ulong revisionBeforeCommit = storage.Revision;

            Assert.That(TryCommitEvacuationPlan(
                storage, plan, out string firstStatus), Is.True);
            Assert.That(firstStatus, Is.EqualTo("Completed"));
            Assert.That(storage.Revision,
                Is.EqualTo(revisionBeforeCommit + 1),
                "Migration, payload write, and source removal are one commit.");
            Assert.That(storage.ContainsWarehouse("warehouse.source"), Is.False);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Alloy), Is.EqualTo(4));

            ulong committedRevision = storage.Revision;
            Assert.That(TryCommitEvacuationPlan(
                storage, plan, out string secondStatus), Is.False);
            Assert.That(secondStatus, Is.EqualTo("AlreadyCommitted"));
            Assert.That(storage.Revision, Is.EqualTo(committedRevision));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Alloy), Is.EqualTo(4));
            Assert.That(storage.ContainsWarehouse("warehouse.source"), Is.False);
        }

        [Test]
        public void EmptyEvacuationPlanIsInvalidAndCannotAdvanceRevision()
        {
            var core = new ResourceInventory(1000);
            var storage = new CityResourceStorageModel(core, 10);
            object plan = CreateEvacuationPlan(
                storage,
                null,
                Array.Empty<ResourceAmount>());
            ulong revisionBefore = storage.Revision;

            Assert.That(ReadPlanBool(plan, "CanCommit"), Is.False);
            Assert.That(TryCommitEvacuationPlan(
                storage, plan, out string status), Is.False);
            Assert.That(status, Is.EqualTo("Invalid"));
            Assert.That(storage.Revision, Is.EqualTo(revisionBefore));
        }

        [Test]
        public void EvacuationCommitSurvivesNotificationFailureAndContinuesObservers()
        {
            var core = new ResourceInventory(1000);
            var storage = new CityResourceStorageModel(core, 10);
            Assert.That(storage.TryRegisterWarehouse("warehouse.source"),
                Is.True);
            Assert.That(storage.TryRegisterWarehouse("warehouse.target"),
                Is.True);
            Assert.That(storage.AddToWarehouse(
                "warehouse.source", ResourceIds.Iron, 5), Is.EqualTo(5));
            CityResourceEvacuationPlan plan = storage.CreateEvacuationPlan(
                "warehouse.source",
                new[] { new ResourceAmount(ResourceIds.Alloy, 4) });
            Assert.That(plan.CanCommit, Is.True);
            ulong revisionBeforeCommit = storage.Revision;
            var observed = new List<RecordedChange>();
            storage.AttributedChanged += (_, _, _) =>
                throw new InvalidOperationException("observer failure");
            storage.AttributedChanged += (resourceId, delta, attribution) =>
                observed.Add(new RecordedChange(resourceId, delta, attribution));

            bool committed = false;
            CityResourceEvacuationCommitStatus status =
                CityResourceEvacuationCommitStatus.Invalid;
            Assert.DoesNotThrow(() =>
            {
                committed = storage.TryCommitEvacuationPlan(plan, out status);
            });

            Assert.That(committed, Is.True);
            Assert.That(status,
                Is.EqualTo(CityResourceEvacuationCommitStatus.Completed));
            Assert.That(storage.Revision, Is.EqualTo(revisionBeforeCommit + 1));
            Assert.That(storage.ContainsWarehouse("warehouse.source"), Is.False);
            Assert.That(storage.GetNetworkAmount(ResourceIds.Iron), Is.EqualTo(5));
            Assert.That(storage.GetNetworkAmount(ResourceIds.Alloy), Is.EqualTo(4));
            Assert.That(observed, Has.Some.Matches<RecordedChange>(change =>
                change.ResourceId == ResourceIds.Alloy && change.Delta == 4));

            PropertyInfo diagnostic = typeof(CityResourceStorageModel).GetProperty(
                "LastNotificationFailure",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(diagnostic, Is.Not.Null,
                "Observer failures require a readable diagnostic surface.");
            Assert.That(diagnostic.CanRead, Is.True);
            Assert.That(diagnostic.CanWrite, Is.False);
            Assert.That(diagnostic.GetValue(storage), Is.Not.Null);

            ulong committedRevision = storage.Revision;
            Assert.That(storage.TryCommitEvacuationPlan(
                plan,
                out CityResourceEvacuationCommitStatus secondStatus), Is.False);
            Assert.That(secondStatus,
                Is.EqualTo(CityResourceEvacuationCommitStatus.AlreadyCommitted));
            Assert.That(storage.Revision, Is.EqualTo(committedRevision));
        }

        [Test]
        public void AggregateChangesPublishActualNetworkDeltaAndAttribution()
        {
            ResourceInventory core = new ResourceInventory(1000);
            var storage = new CityResourceStorageModel(core, 150);
            storage.TryRegisterWarehouse("warehouse.alpha");
            var changes = new List<RecordedChange>();
            storage.AttributedChanged += (resourceId, delta, attribution) =>
                changes.Add(new RecordedChange(resourceId, delta, attribution));

            using (storage.AttributeChanges(new ResourceChangeAttribution(
                       ResourceChangeAttributionKind.Backpack,
                       "backpack.transfer")))
            {
                storage.AddToWarehouse(
                    "warehouse.alpha", ResourceIds.Iron, 12);
            }

            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].ResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(changes[0].Delta, Is.EqualTo(12));
            Assert.That(changes[0].Attribution.Kind,
                Is.EqualTo(ResourceChangeAttributionKind.Backpack));
            Assert.That(changes[0].Attribution.ReferenceId,
                Is.EqualTo("backpack.transfer"));

            changes.Clear();
            storage.TrySetWarehouseConnected("warehouse.alpha", false);
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].Delta, Is.EqualTo(-12));
            changes.Clear();
            storage.AddToWarehouse(
                "warehouse.alpha", ResourceIds.Iron, 3);
            Assert.That(changes, Is.Empty,
                "Disconnected contents are not part of the network aggregate.");

            using (core.AttributeChanges(new ResourceChangeAttribution(
                       ResourceChangeAttributionKind.Research,
                       "research.test")))
            {
                core.Add(ResourceIds.Stone, 4);
            }
            Assert.That(changes, Has.Count.EqualTo(1));
            Assert.That(changes[0].ResourceId, Is.EqualTo(ResourceIds.Stone));
            Assert.That(changes[0].Delta, Is.EqualTo(4));
            Assert.That(changes[0].Attribution.Kind,
                Is.EqualTo(ResourceChangeAttributionKind.Research));
        }

        private static CityResourceStorageModel CreateStorage()
        {
            return new CityResourceStorageModel(
                new ResourceInventory(1000),
                coreCapacityPerResource: 150);
        }

        private static Type RequireEvacuationPlanType()
        {
            Type type = typeof(CityResourceStorageModel).Assembly.GetType(
                "WasteCity.Economy.CityResourceEvacuationPlan",
                false);
            Assert.That(type, Is.Not.Null,
                "IDEA-0014 requires an immutable city-resource " +
                "evacuation preflight plan.");
            return type;
        }

        private static object CreateEvacuationPlan(
            CityResourceStorageModel storage,
            string sourceWarehouseId,
            IReadOnlyList<ResourceAmount> additions)
        {
            Type planType = RequireEvacuationPlanType();
            MethodInfo method = typeof(CityResourceStorageModel).GetMethod(
                "CreateEvacuationPlan",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[]
                {
                    typeof(string),
                    typeof(IReadOnlyList<ResourceAmount>)
                },
                null);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(planType));
            return method.Invoke(storage, new object[]
            {
                sourceWarehouseId,
                additions
            });
        }

        private static bool TryCommitEvacuationPlan(
            CityResourceStorageModel storage,
            object plan,
            out string status)
        {
            Type statusType = typeof(CityResourceStorageModel).Assembly.GetType(
                "WasteCity.Economy.CityResourceEvacuationCommitStatus",
                false);
            Assert.That(statusType, Is.Not.Null);
            MethodInfo method = typeof(CityResourceStorageModel).GetMethod(
                "TryCommitEvacuationPlan",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { plan.GetType(), statusType.MakeByRefType() },
                null);
            Assert.That(method, Is.Not.Null);
            var arguments = new[] { plan, Activator.CreateInstance(statusType) };
            bool committed = (bool)method.Invoke(storage, arguments);
            status = arguments[1].ToString();
            return committed;
        }

        private static int InvokePlanAmount(
            object plan,
            string methodName,
            string resourceId)
        {
            MethodInfo method = plan.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(method, Is.Not.Null);
            return (int)method.Invoke(plan, new object[] { resourceId });
        }

        private static object ReadPlanProperty(object plan, string name)
        {
            PropertyInfo property = plan.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.CanWrite, Is.False,
                plan.GetType().FullName + "." + name + " must be immutable.");
            return property.GetValue(plan, null);
        }

        private static bool ReadPlanBool(object plan, string name)
        {
            return (bool)ReadPlanProperty(plan, name);
        }

        private static int ReadPlanInt(object plan, string name)
        {
            return (int)ReadPlanProperty(plan, name);
        }

        private static ulong ReadPlanUlong(object plan, string name)
        {
            return (ulong)ReadPlanProperty(plan, name);
        }

        private readonly struct RecordedChange
        {
            public RecordedChange(
                string resourceId,
                int delta,
                ResourceChangeAttribution attribution)
            {
                ResourceId = resourceId;
                Delta = delta;
                Attribution = attribution;
            }

            public string ResourceId { get; }
            public int Delta { get; }
            public ResourceChangeAttribution Attribution { get; }
        }
    }
}
