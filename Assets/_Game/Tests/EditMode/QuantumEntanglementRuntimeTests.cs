using System.Linq;
using NUnit.Framework;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class QuantumEntanglementRuntimeTests
    {
        [Test]
        public void IDEA0028_SharedResourcesUseNetworkOnlyWhileConnected()
        {
            var runtime = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.water",
                "core.resource.iron",
                "core.resource.iron",
            });

            Assert.That(runtime.Capture().SharedResourceIds,
                Is.EqualTo(new[]
                {
                    "core.resource.iron",
                    "core.resource.water",
                }));
            Assert.That(runtime.ProjectRoute("core.resource.iron").Policy,
                Is.EqualTo(QuantumEntanglementRoutePolicy.SharedNetwork));
            Assert.That(runtime.ProjectRoute("core.resource.alloy").Policy,
                Is.EqualTo(QuantumEntanglementRoutePolicy.LocalInventoryOnly));

            Assert.That(runtime.TrySetConnected(false), Is.True);
            QuantumEntanglementRouteProjection disconnected =
                runtime.ProjectRoute("core.resource.iron");
            Assert.That(disconnected.IsSharedResource, Is.True);
            Assert.That(disconnected.ConnectionAvailable, Is.False);
            Assert.That(disconnected.Policy,
                Is.EqualTo(QuantumEntanglementRoutePolicy.LocalInventoryOnly));
            Assert.That(runtime.TrySetConnected(false), Is.False);
        }

        [Test]
        public void IDEA0028_SnapshotIsStableAndInvalidResourceStaysLocal()
        {
            var runtime = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.iron",
            });
            QuantumEntanglementSnapshot before = runtime.Capture();

            Assert.That(runtime.ProjectRoute(null).Policy,
                Is.EqualTo(QuantumEntanglementRoutePolicy.LocalInventoryOnly));
            Assert.That(runtime.ProjectRoute(" ").Policy,
                Is.EqualTo(QuantumEntanglementRoutePolicy.LocalInventoryOnly));
            Assert.That(runtime.Capture(), Is.SameAs(before));
            Assert.That(runtime.Capture().SharedResourceIds.Count(), Is.EqualTo(1));
            Assert.That(runtime.Capture().CommittedSynchronizationKeys,
                Is.Empty);
        }

        [Test]
        public void IDEA0028_RestoreRoundTripsConnectionAndSharedResources()
        {
            var source = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.water",
                "core.resource.iron",
            });
            Assert.That(source.TrySetConnected(false), Is.True);

            Assert.That(source.TryCommitSynchronization(
                QuantumEntanglementRuntime.FirstSynchronizationKey), Is.True);
            var restored = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.water",
                "core.resource.iron",
            });
            Assert.That(restored.TryRestore(
                source.Capture(), out string error), Is.True, error);

            Assert.That(restored.Capture().Connected, Is.False);
            Assert.That(restored.Capture().Revision,
                Is.EqualTo(source.Capture().Revision));
            Assert.That(restored.Capture().SharedResourceIds,
                Is.EqualTo(source.Capture().SharedResourceIds));
            Assert.That(restored.Capture().CommittedSynchronizationKeys,
                Is.EqualTo(source.Capture().CommittedSynchronizationKeys));
            Assert.That(restored.ProjectRoute("core.resource.iron").Policy,
                Is.EqualTo(QuantumEntanglementRoutePolicy.LocalInventoryOnly));
        }

        [Test]
        public void IDEA0028_SynchronizationCommitIsStableIdempotentTruth()
        {
            var runtime = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.iron",
            });
            QuantumEntanglementSnapshot clean = runtime.Capture();

            Assert.That(runtime.TryCommitSynchronization(null), Is.False);
            Assert.That(runtime.TryCommitSynchronization("core.resource.iron"),
                Is.False, "Resource configuration is not a synchronization key.");
            Assert.That(runtime.Capture(), Is.SameAs(clean));
            Assert.That(runtime.TryCommitSynchronization(
                QuantumEntanglementRuntime.FirstSynchronizationKey), Is.True);
            QuantumEntanglementSnapshot committed = runtime.Capture();
            Assert.That(committed.Revision, Is.EqualTo(1ul));
            Assert.That(committed.CommittedSynchronizationKeys,
                Is.EqualTo(new[]
                {
                    QuantumEntanglementRuntime.FirstSynchronizationKey,
                }));
            Assert.That(runtime.TryCommitSynchronization(
                QuantumEntanglementRuntime.FirstSynchronizationKey), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(committed));
        }

        [Test]
        public void IDEA0028_RestoreCannotReplaceFixedSharedResourceConfig()
        {
            var source = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.iron",
                "core.resource.water",
            });
            Assert.That(source.TryCommitSynchronization(
                QuantumEntanglementRuntime.FirstSynchronizationKey), Is.True);
            var target = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.iron",
            });
            QuantumEntanglementSnapshot before = target.Capture();

            Assert.That(target.TryRestore(source.Capture(), out _), Is.False);
            Assert.That(target.Capture(), Is.SameAs(before));
            Assert.That(target.Capture().SharedResourceIds,
                Is.EqualTo(new[] { "core.resource.iron" }));
        }

        [Test]
        public void IDEA0028_InvalidRestoreIsAtomic()
        {
            var runtime = new QuantumEntanglementRuntime(new[]
            {
                "core.resource.iron",
            });
            QuantumEntanglementSnapshot before = runtime.Capture();
            var invalid = new QuantumEntanglementSnapshot(
                false,
                0,
                new[] { "core.resource.iron" },
                new[]
                {
                    QuantumEntanglementRuntime.FirstSynchronizationKey,
                    QuantumEntanglementRuntime.FirstSynchronizationKey,
                });

            Assert.That(runtime.TryRestore(invalid, out _), Is.False);
            Assert.That(runtime.Capture(), Is.SameAs(before));
        }
    }
}
