using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029WorldVisibilityRuntimeTests
    {
        [Test]
        public void SourceCreatesCircularVisibleAreaAndPermanentExploration()
        {
            var runtime = new WorldVisibilityRuntime(20, 20);
            Assert.That(runtime.GetState(10, 10),
                Is.EqualTo(WorldVisibilityState.Hidden));

            Assert.That(runtime.UpsertSource(new WorldVisionSource(
                "core.city.000001",
                WorldVisionSourceKind.PrimaryCity,
                10,
                10,
                true)), Is.True);

            Assert.That(runtime.GetState(10, 10),
                Is.EqualTo(WorldVisibilityState.Visible));
            Assert.That(runtime.GetState(17, 10),
                Is.EqualTo(WorldVisibilityState.Visible));
            Assert.That(runtime.GetState(18, 10),
                Is.EqualTo(WorldVisibilityState.Hidden));

            Assert.That(runtime.RemoveSource("core.city.000001"), Is.True);
            Assert.That(runtime.GetState(10, 10),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(runtime.GetState(17, 10),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(runtime.GetState(18, 10),
                Is.EqualTo(WorldVisibilityState.Hidden));
        }

        [Test]
        public void MovingAndOverlappingSourcesPreserveCorrectVisibility()
        {
            var runtime = new WorldVisibilityRuntime(24, 12);
            Assert.That(runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader, 5, 5, true)),
                Is.True);
            Assert.That(runtime.UpsertSource(new WorldVisionSource(
                "outpost.1", WorldVisionSourceKind.Outpost, 8, 5, true)),
                Is.True);
            Assert.That(runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader, 16, 5, true)),
                Is.True);

            Assert.That(runtime.GetState(1, 5),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(runtime.GetState(8, 5),
                Is.EqualTo(WorldVisibilityState.Visible),
                "The overlapping outpost must retain live visibility.");
            Assert.That(runtime.GetState(16, 5),
                Is.EqualTo(WorldVisibilityState.Visible));
            Assert.That(runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader, 16, 5, true)),
                Is.False,
                "An unchanged source must not advance runtime state.");
        }

        [Test]
        public void InactiveSourceAndExplicitRevealOnlyCreateExploredCells()
        {
            var runtime = new WorldVisibilityRuntime(8, 8);

            Assert.That(runtime.UpsertSource(new WorldVisionSource(
                "leader.1", WorldVisionSourceKind.Leader, 4, 4, false)),
                Is.False);
            Assert.That(runtime.Reveal(0, 0, 2), Is.GreaterThan(0));
            Assert.That(runtime.GetState(0, 0),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(runtime.GetState(2, 0),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(runtime.GetState(3, 0),
                Is.EqualTo(WorldVisibilityState.Hidden));
            Assert.That(runtime.Reveal(0, 0, 2), Is.Zero);
        }

        [Test]
        public void CaptureAndRestoreExploredAreDefensiveAndValidated()
        {
            var runtime = new WorldVisibilityRuntime(4, 3);
            runtime.Reveal(1, 1, 1);
            bool[] captured = runtime.CaptureExplored();
            captured[0] = !captured[0];

            bool expected = runtime.IsExplored(0, 0);
            Assert.That(runtime.TryRestoreExplored(
                runtime.CaptureExplored(), out string error), Is.True, error);
            Assert.That(runtime.IsExplored(0, 0), Is.EqualTo(expected));
            Assert.That(runtime.TryRestoreExplored(
                new bool[3], out error), Is.False);
        }
    }
}
