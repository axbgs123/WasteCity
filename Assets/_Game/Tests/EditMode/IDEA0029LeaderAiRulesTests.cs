using NUnit.Framework;
using WasteCity.City;
using WasteCity.Leader.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029LeaderAiRulesTests
    {
        [TestCase(CityMode.Mobile)]
        [TestCase(CityMode.Deploying)]
        [TestCase(CityMode.Packing)]
        public void MovingCityMakesAiFollowSafeDock(CityMode cityMode)
        {
            LeaderIntent intent = LeaderAiRules.Resolve(new LeaderAiContext(
                cityMode,
                LeaderControlMode.AI,
                leaderX: 12f,
                leaderY: 8f,
                dockX: 3f,
                dockY: 4f,
                pathAvailable: true));

            Assert.That(intent.Kind, Is.EqualTo(LeaderIntentKind.FollowCity));
            Assert.That(intent.TargetX, Is.EqualTo(3f));
            Assert.That(intent.TargetY, Is.EqualTo(4f));
        }

        [Test]
        public void FortressAiReturnsToDockThenHoldsPosition()
        {
            LeaderIntent returning = LeaderAiRules.Resolve(new LeaderAiContext(
                CityMode.Fortress,
                LeaderControlMode.AI,
                leaderX: 8f,
                leaderY: 8f,
                dockX: 4f,
                dockY: 4f,
                pathAvailable: true));
            LeaderIntent holding = LeaderAiRules.Resolve(new LeaderAiContext(
                CityMode.Fortress,
                LeaderControlMode.AI,
                leaderX: 4.05f,
                leaderY: 4.05f,
                dockX: 4f,
                dockY: 4f,
                pathAvailable: true));

            Assert.That(returning.Kind, Is.EqualTo(LeaderIntentKind.ReturnToDock));
            Assert.That(holding.Kind, Is.EqualTo(LeaderIntentKind.HoldPosition));
        }

        [Test]
        public void ManualOrUnreachableAiNeverInventsMovement()
        {
            LeaderIntent manual = LeaderAiRules.Resolve(new LeaderAiContext(
                CityMode.Fortress,
                LeaderControlMode.Manual,
                8f,
                8f,
                4f,
                4f,
                pathAvailable: true));
            LeaderIntent unreachable = LeaderAiRules.Resolve(new LeaderAiContext(
                CityMode.Fortress,
                LeaderControlMode.AI,
                8f,
                8f,
                4f,
                4f,
                pathAvailable: false));

            Assert.That(manual.Kind, Is.EqualTo(LeaderIntentKind.None));
            Assert.That(unreachable.Kind, Is.EqualTo(LeaderIntentKind.HoldPosition));
        }
    }
}
