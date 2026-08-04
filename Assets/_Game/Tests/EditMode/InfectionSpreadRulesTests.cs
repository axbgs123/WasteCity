using NUnit.Framework;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class InfectionSpreadRulesTests
    {
        [Test]
        public void BurstSelectsLivingHostilesInsideInclusiveThreeUnitRadius()
        {
            var candidates = new[]
            {
                new InfectionSpreadCandidate(1, 0f, 0f, true, false),
                new InfectionSpreadCandidate(2, 3f, 0f, true, false),
                new InfectionSpreadCandidate(3, 3.01f, 0f, true, false),
                new InfectionSpreadCandidate(4, 1f, 0f, false, false),
                new InfectionSpreadCandidate(5, 1f, 0f, true, true)
            };

            int[] selected = InfectionSpreadRules.SelectTargets(1, 0f, 0f, 3f, candidates);

            Assert.That(selected, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void NullCandidatesProduceNoTargets()
        {
            Assert.That(
                InfectionSpreadRules.SelectTargets(1, 0f, 0f, 3f, null),
                Is.Empty);
        }

        [Test]
        public void NegativeRadiusClampsToZero()
        {
            var candidates = new[]
            {
                new InfectionSpreadCandidate(2, 0f, 0f, true, false),
                new InfectionSpreadCandidate(3, .01f, 0f, true, false)
            };

            int[] selected = InfectionSpreadRules.SelectTargets(1, 0f, 0f, -2f, candidates);

            Assert.That(selected, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void SelectionPreservesCandidateOrder()
        {
            var candidates = new[]
            {
                new InfectionSpreadCandidate(30, 2f, 0f, true, false),
                new InfectionSpreadCandidate(10, 1f, 0f, true, false),
                new InfectionSpreadCandidate(20, 0f, 2f, true, false)
            };

            int[] selected = InfectionSpreadRules.SelectTargets(99, 0f, 0f, 3f, candidates);

            Assert.That(selected, Is.EqualTo(new[] { 30, 10, 20 }));
        }
    }
}
