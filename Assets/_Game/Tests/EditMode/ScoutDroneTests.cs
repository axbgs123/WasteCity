using NUnit.Framework;
using WasteCity.World;

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
    }
}
