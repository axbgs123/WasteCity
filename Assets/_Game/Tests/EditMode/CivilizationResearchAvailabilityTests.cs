using NUnit.Framework;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class CivilizationResearchAvailabilityTests
    {
        [TestCase("core.research.alloy-armor")]
        [TestCase("core.research.sword-riding")]
        public void IDEA0021_LevelTwoProjectsOnlyApprovedNodesResearchable(
            string researchId)
        {
            ResearchDefinition source = ResearchCatalog.Find(researchId);
            Assert.That(source.ReleaseState,
                Is.EqualTo(ResearchReleaseState.PreviewOnly));
            Assert.That(CivilizationResearchAvailability.IsAvailable(
                source, 1), Is.False);
            Assert.That(CivilizationResearchAvailability.Resolve(
                source, 1), Is.SameAs(source));

            ResearchDefinition levelTwo =
                CivilizationResearchAvailability.Resolve(source, 2);
            Assert.That(levelTwo, Is.Not.SameAs(source));
            Assert.That(levelTwo.Id, Is.EqualTo(source.Id));
            Assert.That(levelTwo.ReleaseState,
                Is.EqualTo(ResearchReleaseState.Researchable));
            Assert.That(levelTwo.Costs, Is.EqualTo(source.Costs));
            Assert.That(levelTwo.RequiredResearchIds,
                Is.EqualTo(source.RequiredResearchIds));
            Assert.That(levelTwo.Duration, Is.EqualTo(60f));
            Assert.That(CivilizationResearchAvailability.Resolve(source, 2),
                Is.SameAs(levelTwo), "Effective definitions must be cached.");
        }

        [Test]
        public void IDEA0021_OtherPreviewNodesRemainClosedAtLevelTwo()
        {
            ResearchDefinition ballistics = ResearchCatalog.Find(
                "core.research.ballistics");
            Assert.That(ballistics.ReleaseState,
                Is.EqualTo(ResearchReleaseState.PreviewOnly));
            Assert.That(CivilizationResearchAvailability.IsAvailable(
                ballistics, 2), Is.False);
            Assert.That(CivilizationResearchAvailability.Resolve(
                ballistics, 2), Is.SameAs(ballistics));
        }
    }
}
