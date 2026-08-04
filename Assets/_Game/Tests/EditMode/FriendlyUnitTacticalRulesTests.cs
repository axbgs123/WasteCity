using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class FriendlyUnitTacticalRulesTests
    {
        private static readonly FriendlyTacticalProfile Profile = new FriendlyTacticalProfile(9f, 13f, 1.25f, 1.5f);

        [Test]
        public void SelectsNearestLivingHostileInsideGuardRadius()
        {
            var candidates = new[]
            {
                new FriendlyTargetCandidate(10, 6f, 0f, true, false),
                new FriendlyTargetCandidate(20, 2f, 0f, true, false),
                new FriendlyTargetCandidate(30, 1f, 0f, false, false),
                new FriendlyTargetCandidate(40, .5f, 0f, true, true)
            };

            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(0f, 0f, 0f, 0f, Profile, candidates);

            Assert.That(decision.Type, Is.EqualTo(FriendlyUnitDecisionType.Chase));
            Assert.That(decision.TargetId, Is.EqualTo(20));
        }

        [Test]
        public void NewTargetOutsideGuardRadiusIsIgnored()
        {
            var candidates = new[] { new FriendlyTargetCandidate(10, 9.1f, 0f, true, false) };

            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(0f, 0f, 0f, 0f, Profile, candidates);

            Assert.That(decision.Type, Is.EqualTo(FriendlyUnitDecisionType.Hold));
            Assert.That(decision.HasTarget, Is.False);
        }

        [Test]
        public void ExistingTargetRemainsValidInsideLeash()
        {
            var candidates = new[] { new FriendlyTargetCandidate(10, 12.9f, 0f, true, false) };

            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(8f, 0f, 0f, 0f, Profile, candidates, 10);

            Assert.That(decision.Type, Is.EqualTo(FriendlyUnitDecisionType.Chase));
            Assert.That(decision.TargetId, Is.EqualTo(10));
        }

        [Test]
        public void ExistingTargetOutsideLeashIsDropped()
        {
            var candidates = new[] { new FriendlyTargetCandidate(10, 13.1f, 0f, true, false) };

            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(4f, 0f, 0f, 0f, Profile, candidates, 10);

            Assert.That(decision.Type, Is.EqualTo(FriendlyUnitDecisionType.ReturnToRally));
            Assert.That(decision.HasTarget, Is.False);
        }

        [Test]
        public void NoTargetReturnsToRallyWhenOutsideTolerance()
        {
            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(1.3f, 0f, 0f, 0f, Profile, null);

            Assert.That(decision.Type, Is.EqualTo(FriendlyUnitDecisionType.ReturnToRally));
        }

        [Test]
        public void NoTargetHoldsInsideRallyTolerance()
        {
            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(1.2f, 0f, 0f, 0f, Profile, null);

            Assert.That(decision.Type, Is.EqualTo(FriendlyUnitDecisionType.Hold));
        }

        [Test]
        public void TargetInsideAttackRangeAttacks()
        {
            var candidates = new[] { new FriendlyTargetCandidate(10, 1.4f, 0f, true, false) };

            FriendlyUnitDecision decision = FriendlyUnitTacticalRules.Decide(0f, 0f, 0f, 0f, Profile, candidates);

            Assert.That(decision.Type, Is.EqualTo(FriendlyUnitDecisionType.Attack));
            Assert.That(decision.TargetId, Is.EqualTo(10));
        }
    }
}
