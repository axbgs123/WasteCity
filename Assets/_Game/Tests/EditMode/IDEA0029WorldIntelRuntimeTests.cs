using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029WorldIntelRuntimeTests
    {
        [Test]
        public void IntelBecomesStaleThenExpiresWithoutLeakingMutableValues()
        {
            var runtime = new WorldIntelRuntime();
            var observed = new WorldIntelObservation(
                "world.deposit.safe-iron.01",
                WorldIntelKind.Resource,
                4,
                5,
                "铁矿 240",
                true,
                240,
                10f);
            Assert.That(runtime.Observe(observed), Is.True);

            Assert.That(runtime.TryGet(
                observed.StableId, 69.999f, out WorldIntelSnapshot fresh),
                Is.True);
            Assert.That(fresh.State, Is.EqualTo(WorldIntelState.Fresh));
            Assert.That(fresh.HasMutableValue, Is.True);
            Assert.That(fresh.MutableValue, Is.EqualTo(240));

            Assert.That(runtime.TryGet(
                observed.StableId, 70f, out WorldIntelSnapshot stale),
                Is.True);
            Assert.That(stale.State, Is.EqualTo(WorldIntelState.Stale));
            Assert.That(stale.Summary, Is.EqualTo("铁矿 240"));

            Assert.That(runtime.TryGet(
                observed.StableId, 190f, out WorldIntelSnapshot expired),
                Is.True);
            Assert.That(expired.State, Is.EqualTo(WorldIntelState.Expired));
            Assert.That(expired.StableId, Is.EqualTo(observed.StableId));
            Assert.That(expired.X, Is.EqualTo(4));
            Assert.That(expired.Y, Is.EqualTo(5));
            Assert.That(expired.Summary, Is.Empty);
            Assert.That(expired.HasMutableValue, Is.False);
        }

        [Test]
        public void ReobservationRefreshesValueAgeAndCachedSnapshot()
        {
            var runtime = new WorldIntelRuntime();
            Assert.That(runtime.Observe(new WorldIntelObservation(
                "enemy.1", WorldIntelKind.Enemy, 2, 3, "啃噬者", true,
                60, 0f)), Is.True);
            Assert.That(runtime.TryGet(
                "enemy.1", 61f, out WorldIntelSnapshot stale), Is.True);
            Assert.That(stale.State, Is.EqualTo(WorldIntelState.Stale));

            Assert.That(runtime.Observe(new WorldIntelObservation(
                "enemy.1", WorldIntelKind.Enemy, 3, 3, "啃噬者受伤",
                true, 20, 61f)), Is.True);
            Assert.That(runtime.TryGet(
                "enemy.1", 61f, out WorldIntelSnapshot refreshed), Is.True);
            Assert.That(refreshed.State, Is.EqualTo(WorldIntelState.Fresh));
            Assert.That(refreshed.X, Is.EqualTo(3));
            Assert.That(refreshed.MutableValue, Is.EqualTo(20));
        }

        [Test]
        public void ObserveRejectsInvalidOrOlderFactsWithoutMutation()
        {
            var runtime = new WorldIntelRuntime();
            Assert.That(runtime.Observe(new WorldIntelObservation(
                "resource.1", WorldIntelKind.Resource, 1, 1, "石料",
                true, 30, 20f)), Is.True);
            ulong revision = runtime.Revision;

            Assert.That(runtime.Observe(new WorldIntelObservation(
                "resource.1", WorldIntelKind.Resource, 1, 1, "旧石料",
                true, 99, 19f)), Is.False);
            Assert.That(runtime.Revision, Is.EqualTo(revision));
            Assert.That(runtime.TryGet(
                "resource.1", 20f, out WorldIntelSnapshot value), Is.True);
            Assert.That(value.MutableValue, Is.EqualTo(30));
            Assert.That(() => runtime.Observe(new WorldIntelObservation(
                string.Empty, WorldIntelKind.Resource, 0, 0, string.Empty,
                false, 0, 0f)), Throws.ArgumentException);
        }

        [Test]
        public void CaptureReturnsStableIdOrderAndIndependentValues()
        {
            var runtime = new WorldIntelRuntime();
            runtime.Observe(new WorldIntelObservation(
                "z", WorldIntelKind.Building, 0, 0, "Z", false, 0, 0f));
            runtime.Observe(new WorldIntelObservation(
                "a", WorldIntelKind.Settlement, 1, 1, "A", false, 0, 0f));

            WorldIntelObservation[] captured = runtime.Capture();

            Assert.That(captured, Has.Length.EqualTo(2));
            Assert.That(captured[0].StableId, Is.EqualTo("a"));
            Assert.That(captured[1].StableId, Is.EqualTo("z"));
        }
    }
}
