using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;
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
        public void ResearchCatalogUsesAllFortyFourFormalChineseDefinitions()
        {
            var entries = GrayboxDeveloperCatalogQuery3D.ResearchEntries;

            Assert.That(entries, Has.Count.EqualTo(44));
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
                Has.Count.EqualTo(44));
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

        [Test]
        public void ProgressionActionCatalogUsesStableIdsAndPlayerFacingChinese()
        {
            var entries = GrayboxDeveloperCatalogQuery3D
                .ProgressionActionEntries;

            Assert.That(entries, Is.Not.Empty);
            Assert.That(entries.Select(value => value.StableId), Is.Unique);
            Assert.That(entries, Has.All.Matches<
                GrayboxDeveloperCatalogEntry3D>(value =>
                    value.Kind ==
                        GrayboxDeveloperCatalogKind3D.ProgressionAction &&
                    value.StableId.StartsWith("developer.") &&
                    !string.IsNullOrWhiteSpace(value.DisplayName) &&
                    value.DisplayName != value.StableId &&
                    !value.DisplayName.Contains("Try") &&
                    !value.DisplayName.Contains("Execute") &&
                    !value.DisplayName.Contains("Fixture")));
            Assert.That(entries.Select(value => value.DisplayName),
                Does.Contain("增加关注度")
                    .And.Contain("选择回溯锚点命轨")
                    .And.Contain("执行首次文明升阶")
                    .And.Contain("查询进度配置签名")
                    .And.Contain("查询命轨领域状态")
                    .And.Contain("探索整张地图")
                    .And.Contain("准备领袖手采验收")
                    .And.Contain("设置前哨危急警报"));
        }

        [Test]
        public void IDEA0029_ExplorationActionsSupportChineseListAndSearch()
        {
            var entries = GrayboxDeveloperCatalogQuery3D
                .SearchProgressionActions("前哨");

            Assert.That(entries.Select(value => value.DisplayName),
                Does.Contain("设置前哨正常运行")
                    .And.Contain("设置前哨通信中断")
                    .And.Contain("设置前哨补给中断")
                    .And.Contain("设置前哨维护中断")
                    .And.Contain("设置前哨警戒警报")
                    .And.Contain("设置前哨受袭警报")
                    .And.Contain("设置前哨危急警报")
                    .And.Contain("清除前哨警报"));
            Assert.That(GrayboxDeveloperCatalogQuery3D
                    .TryResolveProgressionAction(
                        "准备领袖手采验收",
                        out GrayboxDeveloperCatalogEntry3D gather),
                Is.True);
            Assert.That(gather.StableId,
                Is.EqualTo("developer.exploration.gather-ready"));
        }

        [Test]
        public void ProgressionActionSearchSupportsListChineseAndStableId()
        {
            var chinese = GrayboxDeveloperCatalogQuery3D
                .SearchProgressionActions("关注度");
            var stable = GrayboxDeveloperCatalogQuery3D
                .SearchProgressionActions("REWIND.READ");

            Assert.That(chinese, Is.Not.Empty);
            Assert.That(chinese, Has.All.Matches<
                GrayboxDeveloperCatalogEntry3D>(value =>
                    value.DisplayName.Contains("关注度")));
            Assert.That(stable, Has.Count.EqualTo(1));
            Assert.That(stable[0].DisplayName, Is.EqualTo("读取指定回溯锚点"));
            Assert.That(GrayboxDeveloperCatalogQuery3D
                    .TryResolveProgressionAction(
                        "查询压力队列",
                        out GrayboxDeveloperCatalogEntry3D resolved),
                Is.True);
            Assert.That(resolved.StableId,
                Is.EqualTo("developer.query.pressure-queue"));

            var fateStates = GrayboxDeveloperCatalogQuery3D
                .SearchProgressionActions("命轨领域");
            Assert.That(fateStates, Has.Count.EqualTo(1));
            Assert.That(fateStates[0].StableId,
                Is.EqualTo("developer.query.fate-domain-states"));
        }

        [Test]
        public void IDEA0028_FateActionsMirrorNineFormalChineseDefinitions()
        {
            GrayboxDeveloperCatalogEntry3D[] entries =
                GrayboxDeveloperCatalogQuery3D.ProgressionActionEntries
                    .Where(value => value.StableId.StartsWith(
                        "developer.fate.select-"))
                    .ToArray();

            Assert.That(entries, Has.Length.EqualTo(9));
            Assert.That(entries.Select(value => value.StableId),
                Is.EqualTo(FormalFateCatalog.All.Select(value =>
                    "developer.fate.select-" +
                    value.Id.Value.Substring("core.legacy.".Length))));
            Assert.That(entries.Select(value => value.DisplayName),
                Is.EqualTo(FormalFateCatalog.All.Select(value =>
                    "选择" + value.DisplayName + "命轨")));
            Assert.That(entries, Has.All.Matches<
                GrayboxDeveloperCatalogEntry3D>(value =>
                    !value.DisplayName.Contains("Select") &&
                    !value.DisplayName.Contains("Try") &&
                    value.DisplayName != value.StableId));

            var searched = GrayboxDeveloperCatalogQuery3D
                .SearchProgressionActions("量子纠缠");
            Assert.That(searched, Has.Count.EqualTo(1));
            Assert.That(searched[0].DisplayName,
                Is.EqualTo("选择量子纠缠命轨"));
        }
    }
}
