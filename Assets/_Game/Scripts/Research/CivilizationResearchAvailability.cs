using System;
using System.Collections.Generic;

namespace WasteCity.Research
{
    public static class CivilizationResearchAvailability
    {
        public const int RequiredCivilizationLevel = 2;
        public const string AlloyArmorId = "core.research.alloy-armor";
        public const string SwordRidingId = "core.research.sword-riding";
        public const string LockedReason = "需要文明 Lv.2";

        private static readonly IReadOnlyDictionary<string,
            ResearchDefinition> levelTwoDefinitions = BuildLevelTwo();
        private static readonly IReadOnlyDictionary<string,
            ResearchDefinition> levelOneDefinitions = BuildLevelOne();

        public static bool IsGated(string researchId)
        {
            return string.Equals(
                       researchId, AlloyArmorId, StringComparison.Ordinal) ||
                   string.Equals(
                       researchId, SwordRidingId, StringComparison.Ordinal);
        }

        public static bool IsAvailable(
            ResearchDefinition definition,
            int civilizationLevel)
        {
            if (definition == null) return false;
            return IsGated(definition.Id.Value)
                ? civilizationLevel >= RequiredCivilizationLevel
                : definition.ReleaseState == ResearchReleaseState.Researchable;
        }

        public static ResearchDefinition Resolve(
            ResearchDefinition definition,
            int civilizationLevel)
        {
            if (definition == null || !IsGated(definition.Id.Value))
                return definition;
            return civilizationLevel >= RequiredCivilizationLevel
                ? levelTwoDefinitions[definition.Id.Value]
                : levelOneDefinitions[definition.Id.Value];
        }

        public static ResearchDefinition ResolveForPersistence(
            ResearchDefinition definition)
        {
            if (definition == null || !IsGated(definition.Id.Value))
                return definition;
            return levelTwoDefinitions[definition.Id.Value];
        }

        private static IReadOnlyDictionary<string, ResearchDefinition>
            BuildLevelTwo()
        {
            var result = new Dictionary<string, ResearchDefinition>(
                StringComparer.Ordinal);
            Add(result, ResearchCatalog.Find(AlloyArmorId),
                "解锁机枪塔升级为重型机枪塔，建筑耐久提高 30%");
            Add(result, ResearchCatalog.Find(SwordRidingId),
                "解锁剑阵台升级为御剑台，飞剑射程提高 30%");
            return result;
        }

        private static IReadOnlyDictionary<string, ResearchDefinition>
            BuildLevelOne()
        {
            var result = new Dictionary<string, ResearchDefinition>(
                StringComparer.Ordinal);
            Add(result, ResearchCatalog.Find(AlloyArmorId),
                "需要文明 Lv.2：解锁机枪塔升级为重型机枪塔，建筑耐久提高 30%",
                ResearchReleaseState.PreviewOnly);
            Add(result, ResearchCatalog.Find(SwordRidingId),
                "需要文明 Lv.2：解锁剑阵台升级为御剑台，飞剑射程提高 30%",
                ResearchReleaseState.PreviewOnly);
            return result;
        }

        private static void Add(
            IDictionary<string, ResearchDefinition> destination,
            ResearchDefinition source,
            string effectSummary,
            ResearchReleaseState releaseState =
                ResearchReleaseState.Researchable)
        {
            if (source == null)
                throw new InvalidOperationException(
                    "文明二级科技缺少正式目录定义");
            destination.Add(
                source.Id.Value,
                new ResearchDefinition(
                    source.Id.Value,
                    source.Name,
                    source.Route,
                    source.Costs,
                    source.Duration,
                    source.RequiredResearchIds,
                    source.Tier,
                    effectSummary,
                    source.CatalogOrder,
                    source.LayoutRow,
                    releaseState,
                    source.EffectReferences));
        }
    }
}
