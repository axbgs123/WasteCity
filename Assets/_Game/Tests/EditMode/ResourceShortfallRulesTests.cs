using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class ResourceShortfallRulesTests
    {
        [Test]
        public void BuildingShortfallReportsExactOwnedRequiredAndMissing()
        {
            IReadOnlyList<ResourceShortfall> result =
                ResourceShortfallRules.EvaluateBuilding(
                    BuildingCatalog.Smelter,
                    resourceId => resourceId == ResourceIds.Stone ? 2 : 0);

            Assert.That(result, Has.Count.EqualTo(1));
            Assert.That(result[0].ResourceId, Is.EqualTo(ResourceIds.Stone));
            Assert.That(result[0].Owned, Is.EqualTo(2));
            Assert.That(result[0].Required, Is.EqualTo(6));
            Assert.That(result[0].Missing, Is.EqualTo(4));
        }

        [Test]
        public void MultipleRequirementsKeepFormalOrderAndOmitSatisfiedItems()
        {
            var requirements = new[]
            {
                new ResourceAmount(ResourceIds.Iron, 5),
                new ResourceAmount(ResourceIds.Stone, 8),
                new ResourceAmount(ResourceIds.Alloy, 3),
            };

            IReadOnlyList<ResourceShortfall> result =
                ResourceShortfallRules.Evaluate(
                    requirements,
                    resourceId => resourceId == ResourceIds.Iron
                        ? 1
                        : resourceId == ResourceIds.Stone
                            ? 8
                            : 0);

            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].ResourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(result[0].Missing, Is.EqualTo(4));
            Assert.That(result[1].ResourceId, Is.EqualTo(ResourceIds.Alloy));
            Assert.That(result[1].Missing, Is.EqualTo(3));
        }

        [Test]
        public void FullyAffordableRequirementsReturnEmptyResult()
        {
            IReadOnlyList<ResourceShortfall> result =
                ResourceShortfallRules.EvaluateBuilding(
                    BuildingCatalog.Wall,
                    _ => BuildingCatalog.Wall.Cost);

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void BuildingTryEvaluationProvidesReusableSingleShortfall()
        {
            Assert.That(
                ResourceShortfallRules.TryEvaluateBuilding(
                    BuildingCatalog.Smelter,
                    _ => 2,
                    out ResourceShortfall shortfall),
                Is.True);
            Assert.That(shortfall.ResourceId, Is.EqualTo(ResourceIds.Stone));
            Assert.That(shortfall.Missing, Is.EqualTo(4));
            Assert.That(
                ResourceShortfallRules.TryEvaluateBuilding(
                    BuildingCatalog.Smelter,
                    _ => 6,
                    out _),
                Is.False);
        }
    }
}
