using NUnit.Framework;
using WasteCity.World;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class ScoutDroneTests
    {
        [Test]
        public void DeploymentRequiresResearchCompletionAndLogistics()
        {
            var bays = new[]
            {
                new DroneBayState(true, true),
                new DroneBayState(true, false),
                new DroneBayState(false, true)
            };

            Assert.That(ScoutDroneDeploymentRules.ActiveCount(false, bays), Is.Zero);
            Assert.That(ScoutDroneDeploymentRules.ActiveCount(true, bays), Is.EqualTo(1));
        }

        [Test]
        public void DeploymentHandlesNullAndEmptyBayCollections()
        {
            Assert.That(ScoutDroneDeploymentRules.ActiveCount(true, null), Is.Zero);
            Assert.That(ScoutDroneDeploymentRules.ActiveCount(true, new DroneBayState[0]), Is.Zero);
        }

        [Test]
        public void PatrolUsesStableSeparatedPhases()
        {
            var first = new ScoutDronePatrolModel();
            var second = new ScoutDronePatrolModel();

            ScoutDronePosition firstPosition = first.Position(10f, 20f, 0, 2);
            ScoutDronePosition secondPosition = second.Position(10f, 20f, 1, 2);

            Assert.That(firstPosition.X, Is.Not.EqualTo(secondPosition.X).Within(.001f));
            Assert.That(firstPosition.Y, Is.EqualTo(secondPosition.Y).Within(.001f));
        }

        [Test]
        public void PatrolUsesOneSecondRevealCadenceAndRetainsRemainder()
        {
            var model = new ScoutDronePatrolModel();

            Assert.That(model.Tick(.99f), Is.False);
            Assert.That(model.Tick(.01f), Is.True);
            Assert.That(model.Tick(2.1f), Is.True);
            Assert.That(model.Tick(.89f), Is.False);
            Assert.That(model.Tick(.01f), Is.True);
        }

        [Test]
        public void NegativeTickDoesNotAdvancePatrol()
        {
            var model = new ScoutDronePatrolModel();
            ScoutDronePosition before = model.Position(0f, 0f, 0, 1);

            Assert.That(model.Tick(-10f), Is.False);
            ScoutDronePosition after = model.Position(0f, 0f, 0, 1);

            Assert.That(after.X, Is.EqualTo(before.X).Within(.001f));
            Assert.That(after.Y, Is.EqualTo(before.Y).Within(.001f));
        }

        [Test]
        public void PatrolMovesWithItsCityCenter()
        {
            var model = new ScoutDronePatrolModel();
            ScoutDronePosition before = model.Position(0f, 0f, 0, 1);
            ScoutDronePosition after = model.Position(5f, -3f, 0, 1);

            Assert.That(after.X - before.X, Is.EqualTo(5f).Within(.001f));
            Assert.That(after.Y - before.Y, Is.EqualTo(-3f).Within(.001f));
        }

        [Test]
        public void SingleDroneStartsAtPatrolRadius()
        {
            var model = new ScoutDronePatrolModel();

            ScoutDronePosition position = model.Position(2f, 3f, 0, 1);

            Assert.That(position.X, Is.EqualTo(8f).Within(.001f));
            Assert.That(position.Y, Is.EqualTo(3f).Within(.001f));
        }

        [Test]
        public void DeterministicPositionUsesAbsoluteRuleTimeAndOneSecondCadence()
        {
            ScoutDronePosition first = ScoutDronePatrolModel.PositionAtElapsed(
                10f,
                20f,
                0,
                2,
                7.2f);
            ScoutDronePosition repeated = ScoutDronePatrolModel.PositionAtElapsed(
                10f,
                20f,
                0,
                2,
                7.9f);
            ScoutDronePosition advanced = ScoutDronePatrolModel.PositionAtElapsed(
                10f,
                20f,
                0,
                2,
                8f);

            Assert.That(repeated.X, Is.EqualTo(first.X).Within(.001f));
            Assert.That(repeated.Y, Is.EqualTo(first.Y).Within(.001f));
            Assert.That(advanced.X, Is.Not.EqualTo(first.X).Within(.001f));
            Assert.That(advanced.Y, Is.Not.EqualTo(first.Y).Within(.001f));
        }

        [Test]
        public void VisionPlanRequiresResearchCompletedBayAndLogistics()
        {
            var eligible = new DroneBayState(true, true);

            Assert.That(ScoutDroneDeploymentRules.TryCreateVisionSource(
                false,
                eligible,
                "building.repair.1",
                10f,
                20f,
                0,
                1,
                3f,
                64,
                48,
                out _), Is.False);
            Assert.That(ScoutDroneDeploymentRules.TryCreateVisionSource(
                true,
                new DroneBayState(false, true),
                "building.repair.1",
                10f,
                20f,
                0,
                1,
                3f,
                64,
                48,
                out _), Is.False);
            Assert.That(ScoutDroneDeploymentRules.TryCreateVisionSource(
                true,
                new DroneBayState(true, false),
                "building.repair.1",
                10f,
                20f,
                0,
                1,
                3f,
                64,
                48,
                out _), Is.False);

            Assert.That(ScoutDroneDeploymentRules.TryCreateVisionSource(
                true,
                eligible,
                "building.repair.1",
                10f,
                20f,
                0,
                1,
                3f,
                64,
                48,
                out WorldVisionSource source), Is.True);
            Assert.That(source.StableId,
                Is.EqualTo("core.exploration.scout-drone|building.repair.1"));
            Assert.That(source.Kind, Is.EqualTo(WorldVisionSourceKind.ScoutDrone));
            Assert.That(source.Radius,
                Is.EqualTo(FormalExplorationCatalog3D.ResolveSightRadius(
                    WorldVisionSourceKind.ScoutDrone)));
        }

        [Test]
        public void IneligibleTransitionKeepsStableRemovalIdentity()
        {
            Assert.That(ScoutDroneDeploymentRules.TryCreateVisionSource(
                true,
                new DroneBayState(true, true),
                "building.repair.7",
                1f,
                1f,
                0,
                1,
                0f,
                8,
                8,
                out WorldVisionSource source), Is.True);
            Assert.That(ScoutDroneDeploymentRules.TryCreateVisionSource(
                true,
                new DroneBayState(true, false),
                "building.repair.7",
                1f,
                1f,
                0,
                1,
                0f,
                8,
                8,
                out _), Is.False);
            string removalId = ScoutDroneDeploymentRules.StableVisionSourceId(
                "building.repair.7");
            Assert.That(removalId, Is.EqualTo(source.StableId));

            var exploration = new WorldExplorationRuntime(
                8,
                8,
                "scout-removal",
                (_, __) => true);
            Assert.That(exploration.UpsertSource(source), Is.True);
            Assert.That(exploration.RemoveSource(removalId), Is.True);
            Assert.That(exploration.SourceCount, Is.Zero);
        }

        [Test]
        public void DepartureReobservationStartsIntelAgeAtDepartureTime()
        {
            var exploration = new WorldExplorationRuntime(
                24,
                24,
                "scout-departure",
                (_, __) => true);
            var source = new WorldVisionSource(
                "core.exploration.scout-drone|building.repair.1",
                WorldVisionSourceKind.ScoutDrone,
                8,
                8,
                true);
            exploration.UpsertSource(source);
            Assert.That(exploration.TryObserveVisibleResource(
                new WorldIntelObservation(
                    "resource.test.1",
                    WorldIntelKind.Resource,
                    8,
                    8,
                    "铁矿 20",
                    true,
                    20,
                    0f),
                out _,
                out string initialError), Is.True, initialError);

            Assert.That(exploration.TryObserveVisibleResource(
                new WorldIntelObservation(
                    "resource.test.1",
                    WorldIntelKind.Resource,
                    8,
                    8,
                    "铁矿 12",
                    true,
                    12,
                    181f),
                out _,
                out string departureError), Is.True, departureError);
            Assert.That(exploration.RemoveSource(source.StableId), Is.True);

            Assert.That(exploration.TryGetIntel(
                "resource.test.1",
                181f,
                out WorldIntelSnapshot atDeparture), Is.True);
            Assert.That(atDeparture.State, Is.EqualTo(WorldIntelState.Fresh));
            Assert.That(atDeparture.MutableValue, Is.EqualTo(12));
            Assert.That(exploration.TryGetIntel(
                "resource.test.1",
                240.999f,
                out WorldIntelSnapshot beforeStale), Is.True);
            Assert.That(beforeStale.State, Is.EqualTo(WorldIntelState.Fresh));
            Assert.That(exploration.TryGetIntel(
                "resource.test.1",
                241f,
                out WorldIntelSnapshot stale), Is.True);
            Assert.That(stale.State, Is.EqualTo(WorldIntelState.Stale));
        }
    }
}
