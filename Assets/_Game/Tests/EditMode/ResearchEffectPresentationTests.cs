using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    /// <summary>
    /// RED contract for structured research effects. The research tree and
    /// development console must consume this projection instead of rebuilding
    /// effect numbers or Chinese labels in their views.
    /// </summary>
    public sealed class ResearchEffectPresentationTests
    {
        [Test]
        public void ThoughtAccelerationShowsPassiveBeforeAfterAndRules()
        {
            ResearchEffectLinePresentation3D line = SingleLine(
                ResearchCatalog.ThoughtAccelerationId,
                completed: false);

            Assert.That(line.Tag, Is.EqualTo("[被动]"));
            Assert.That(line.Summary, Does.Contain("研究效率"));
            Assert.That(line.Summary, Does.Contain("100% → 125%"));
            Assert.That(line.Scope, Is.EqualTo("范围：全城研究"));
            Assert.That(line.Stacking, Is.EqualTo("叠加：不叠加"));
            Assert.That(line.Activation, Is.EqualTo("研究完成后生效"));
            Assert.That(line.IsApplied, Is.False);
            Assert.That(line.IsPreviewOnly, Is.False);
        }

        [Test]
        public void CompletedThoughtAccelerationShowsAppliedImmediately()
        {
            ResearchEffectLinePresentation3D line = SingleLine(
                ResearchCatalog.ThoughtAccelerationId,
                completed: true);

            Assert.That(line.Activation, Is.EqualTo("已生效"));
            Assert.That(line.IsApplied, Is.True);
            Assert.That(line.Summary, Does.Contain("100% → 125%"));
        }

        [Test]
        public void FormationReinforcementShowsBothNumericEffects()
        {
            IReadOnlyList<ResearchEffectLinePresentation3D> lines = Resolve(
                "core.research.formation-reinforcement",
                completed: false);

            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines.All(line => line.Tag == "[被动]"), Is.True);
            Assert.That(lines.Any(line =>
                    line.Summary.Contains("物流范围") &&
                    line.Summary.Contains("8格 → 12格")),
                Is.True);
            Assert.That(lines.Any(line =>
                    line.Summary.Contains("聚灵生产效率") &&
                    line.Summary.Contains("100% → 150%")),
                Is.True,
                string.Join(" | ", lines.Select(line => line.Summary)));
            Assert.That(lines.All(line =>
                    line.Activation == "研究完成后生效"),
                Is.True);
        }

        [Test]
        public void PreviewOnlyTechnologyNeverClaimsItsEffectsAreApplied()
        {
            const string researchId = "core.research.energy-weapons";
            Assert.That(
                ResearchCatalog.Find(researchId).ReleaseState,
                Is.EqualTo(ResearchReleaseState.PreviewOnly));

            IReadOnlyList<ResearchEffectLinePresentation3D> lines = Resolve(
                researchId,
                completed: true);

            Assert.That(lines, Is.Not.Empty);
            Assert.That(lines.All(line => line.IsPreviewOnly), Is.True);
            Assert.That(lines.All(line => !line.IsApplied), Is.True);
            Assert.That(lines.All(line =>
                    line.Activation == "仅预览，效果待接入"),
                Is.True);
            Assert.That(lines.Any(line => line.Activation == "已生效"),
                Is.False);
        }

        [Test]
        public void CompletedCivilizationGateShowsExecutableEffectAsApplied()
        {
            const string researchId = "core.research.alloy-armor";
            Assert.That(
                ResearchCatalog.Find(researchId).ReleaseState,
                Is.EqualTo(ResearchReleaseState.PreviewOnly));

            IReadOnlyList<ResearchEffectLinePresentation3D> lines = Resolve(
                researchId,
                completed: true);

            Assert.That(lines, Is.Not.Empty);
            Assert.That(lines.All(line => line.IsApplied), Is.True);
            Assert.That(lines.All(line => !line.IsPreviewOnly), Is.True);
            Assert.That(lines.All(line => line.Activation == "已生效"),
                Is.True);
        }

        [Test]
        public void MetabolicAccelerationUsesUnlockAndNumericCompoundTags()
        {
            IReadOnlyList<ResearchEffectLinePresentation3D> lines = Resolve(
                "core.research.metabolic-acceleration",
                completed: false);

            Assert.That(lines.Select(line => line.Tag),
                Does.Contain("[解锁]").And.Contain("[被动]"));
            Assert.That(lines.Any(line =>
                    line.Tag == "[解锁]" &&
                    line.Summary.Contains("代谢炉")),
                Is.True);
            Assert.That(lines.Any(line =>
                    line.Tag == "[被动]" &&
                    line.Summary.Contains("生物质回收") &&
                    line.Summary.Contains("100% → 150%")),
                Is.True);
        }

        [Test]
        public void ActiveNodeDoesNotClaimUnwiredInformationalRuleIsApplied()
        {
            IReadOnlyList<ResearchEffectLinePresentation3D> lines = Resolve(
                "core.research.consciousness-network",
                completed: true);
            ResearchEffectLinePresentation3D communication = lines.Single(
                line => line.Summary.Contains("远程通信成本"));

            Assert.That(communication.IsApplied, Is.False);
            Assert.That(communication.IsPreviewOnly, Is.True);
            Assert.That(
                communication.Activation,
                Is.EqualTo("仅预览，效果待接入"));
            Assert.That(lines.Any(line =>
                    line.Summary.Contains("意识碎片沉淀效率") &&
                    line.IsApplied),
                Is.True);
        }

        private static ResearchEffectLinePresentation3D SingleLine(
            string researchId,
            bool completed)
        {
            IReadOnlyList<ResearchEffectLinePresentation3D> lines = Resolve(
                researchId,
                completed);
            Assert.That(lines, Has.Count.EqualTo(1));
            return lines[0];
        }

        private static IReadOnlyList<ResearchEffectLinePresentation3D>
            Resolve(string researchId, bool completed)
        {
            ResearchDefinition definition = ResearchCatalog.Find(researchId);
            Assert.That(definition, Is.Not.Null, researchId);
            return ResearchEffectPresentationCatalog3D.Resolve(
                definition,
                completed);
        }
    }
}
