using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public enum DemoResearchReleaseState
    {
        InitiallyCompleted,
        Researchable,
        PreviewOnly,
    }

    public static class DemoResearchCatalog
    {
        public const string ScrapProcessingId =
            "core.research.scrap-processing";
        public const string BasicMetallurgyId =
            "core.research.automated-machinery";
        public const string AmmunitionAssemblyId =
            "core.research.precision-assembly";
        public const string AutomatedDefenseId =
            "core.research.automated-defense";
        public const string ReinforcedStructuresId =
            "core.research.reinforced-structures";
        public const string LegacyAnalysisId =
            "core.research.legacy-analysis";

        private static readonly ReadOnlyCollection<ResearchDefinition>
            definitions = Array.AsReadOnly(new[]
        {
            Node(
                ScrapProcessingId,
                "废料加工",
                Array.Empty<ResourceAmount>(),
                0f,
                "解锁采矿站、研究站、住房、仓库与城墙"),
            Node(
                BasicMetallurgyId,
                "基础冶金",
                new[] { new ResourceAmount(ResourceIds.Iron, 10) },
                20f,
                "解锁冶炼厂与应急合金",
                ScrapProcessingId),
            Node(
                AmmunitionAssemblyId,
                "弹药装配",
                new[] { new ResourceAmount(ResourceIds.Alloy, 10) },
                30f,
                "解锁装配厂与应急弹药",
                BasicMetallurgyId),
            Node(
                AutomatedDefenseId,
                "自动防御",
                new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 12),
                    new ResourceAmount(ResourceIds.Biomass, 10),
                },
                35f,
                "解锁机枪塔",
                AmmunitionAssemblyId),
            Node(
                ReinforcedStructuresId,
                "加固结构",
                new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 20),
                    new ResourceAmount(ResourceIds.Biomass, 10),
                },
                45f,
                "城市核心与城墙生命 +25%",
                AutomatedDefenseId),
            Node(
                LegacyAnalysisId,
                "遗产解析",
                new[]
                {
                    new ResourceAmount(ResourceIds.Alloy, 30),
                    new ResourceAmount(ResourceIds.Biomass, 20),
                },
                60f,
                "满足文明升阶条件之一",
                AutomatedDefenseId),
        });

        public static ReadOnlyCollection<ResearchDefinition> All => definitions;

        public static ResearchDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int index = 0; index < definitions.Count; index++)
                if (string.Equals(
                        definitions[index].Id.Value,
                        id,
                        StringComparison.Ordinal))
                    return definitions[index];
            return null;
        }

        public static DemoResearchReleaseState ReleaseState(string id)
        {
            if (string.Equals(id, ScrapProcessingId, StringComparison.Ordinal))
                return DemoResearchReleaseState.InitiallyCompleted;
            if (string.Equals(id, BasicMetallurgyId, StringComparison.Ordinal) ||
                string.Equals(
                    id,
                    AmmunitionAssemblyId,
                    StringComparison.Ordinal))
            {
                return DemoResearchReleaseState.Researchable;
            }
            return DemoResearchReleaseState.PreviewOnly;
        }

        private static ResearchDefinition Node(
            string id,
            string name,
            IReadOnlyList<ResourceAmount> costs,
            float duration,
            string effect,
            string required = null)
        {
            return new ResearchDefinition(
                id,
                name,
                DevelopmentRoute.Technology,
                costs,
                duration,
                required,
                tier: 1,
                effectSummary: effect);
        }
    }
}
