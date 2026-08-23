using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class GrayboxDeveloperModifierCatalogTests
    {
        [Test]
        public void ResourceCatalogUsesAllThirtyOneFormalChineseDefinitions()
        {
            var entries = GrayboxDeveloperCatalogQuery3D.ResourceEntries;

            Assert.That(entries, Has.Count.EqualTo(31));
            Assert.That(
                entries.Select(value => value.StableId),
                Is.EqualTo(ResourceDefinitionCatalog.All.Select(
                    value => value.Id)));
            Assert.That(
                entries.Select(value => value.DisplayName),
                Is.EqualTo(ResourceDefinitionCatalog.All.Select(
                    value => value.ChineseName)));
            Assert.That(entries, Has.All.Matches<
                GrayboxDeveloperCatalogEntry3D>(value =>
                    value.Kind == GrayboxDeveloperCatalogKind3D.Resource &&
                    !string.IsNullOrWhiteSpace(value.DisplayName) &&
                    value.DisplayName != value.StableId &&
                    !value.DisplayName.Contains(".resource.")));
        }

        [Test]
        public void ResearchCatalogUsesAllFortyThreeFormalChineseDefinitions()
        {
            var entries = GrayboxDeveloperCatalogQuery3D.ResearchEntries;

            Assert.That(entries, Has.Count.EqualTo(43));
            Assert.That(
                entries.Select(value => value.StableId),
                Is.EqualTo(ResearchCatalog.All.Select(
                    value => value.Id.Value)));
            Assert.That(
                entries.Select(value => value.DisplayName),
                Is.EqualTo(ResearchCatalog.All.Select(value => value.Name)));
            Assert.That(entries, Has.All.Matches<
                GrayboxDeveloperCatalogEntry3D>(value =>
                    value.Kind == GrayboxDeveloperCatalogKind3D.Research &&
                    !string.IsNullOrWhiteSpace(value.DisplayName) &&
                    value.DisplayName != value.StableId &&
                    !value.DisplayName.Contains("core.research.")));
        }

        [Test]
        public void ResourceSearchSupportsChineseAndCaseInsensitiveStableId()
        {
            var chinese = GrayboxDeveloperCatalogQuery3D.SearchResources(
                "  灵  ");
            var stableId =
                GrayboxDeveloperCatalogQuery3D.SearchResources(
                    "ENERGY-CELL");

            Assert.That(chinese.Select(value => value.DisplayName),
                Does.Contain("灵铁"));
            Assert.That(chinese.Select(value => value.DisplayName),
                Does.Contain("灵石"));
            Assert.That(stableId, Has.Count.EqualTo(1));
            Assert.That(stableId[0].StableId,
                Is.EqualTo(ResourceIds.EnergyCell));
            Assert.That(
                GrayboxDeveloperCatalogQuery3D.SearchResources("  "),
                Has.Count.EqualTo(31));
        }

        [Test]
        public void ResearchSearchSupportsChineseAndCaseInsensitiveStableId()
        {
            var chinese = GrayboxDeveloperCatalogQuery3D.SearchResearch(
                "意识");
            var stableId =
                GrayboxDeveloperCatalogQuery3D.SearchResearch(
                    "SPIRIT-SENSING");

            Assert.That(chinese, Is.Not.Empty);
            Assert.That(chinese, Has.All.Matches<
                GrayboxDeveloperCatalogEntry3D>(value =>
                    value.DisplayName.Contains("意识")));
            Assert.That(stableId, Has.Count.EqualTo(1));
            Assert.That(stableId[0].StableId,
                Is.EqualTo("core.research.spirit-sensing"));
            Assert.That(
                GrayboxDeveloperCatalogQuery3D.SearchResearch(null),
                Has.Count.EqualTo(43));
        }

        [Test]
        public void ExactResolverAcceptsChineseNameAndStableIdWithoutCrossingKinds()
        {
            Assert.That(GrayboxDeveloperCatalogQuery3D.TryResolveResource(
                "融合核心",
                out GrayboxDeveloperCatalogEntry3D resource), Is.True);
            Assert.That(resource.StableId, Is.EqualTo(ResourceIds.HybridCore));
            Assert.That(GrayboxDeveloperCatalogQuery3D.TryResolveResearch(
                "CORE.RESEARCH.SPIRIT-SENSING",
                out GrayboxDeveloperCatalogEntry3D research), Is.True);
            Assert.That(research.DisplayName, Is.EqualTo("灵火淬炼"));
            Assert.That(GrayboxDeveloperCatalogQuery3D.TryResolveResource(
                research.DisplayName,
                out _), Is.False);
            Assert.That(GrayboxDeveloperCatalogQuery3D.TryResolveResearch(
                resource.DisplayName,
                out _), Is.False);
        }
    }
}
