#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxDeveloperCatalogKind3D
    {
        Resource,
        Research,
        TechnologyStatus,
        ProgressionAction,
    }

    public enum GrayboxDeveloperCommandCode3D
    {
        Success,
        PartialCapacity,
        CapacityFull,
        InvalidAmount,
        UnknownResource,
        UnknownResearch,
        AlreadyCompleted,
        InvalidRoute,
        NoChange,
        UnknownAction,
        ProgressionUnavailable,
        CommandFailed,
    }

    public sealed class GrayboxDeveloperCatalogEntry3D
    {
        internal GrayboxDeveloperCatalogEntry3D(
            GrayboxDeveloperCatalogKind3D kind,
            string stableId,
            string displayName)
        {
            Kind = kind;
            StableId = stableId;
            DisplayName = displayName;
        }

        public GrayboxDeveloperCatalogKind3D Kind { get; }
        public string StableId { get; }
        public string DisplayName { get; }

        public bool Matches(string query)
        {
            string normalized = Normalize(query);
            return normalized.Length == 0 ||
                DisplayName.IndexOf(
                    normalized,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                StableId.IndexOf(
                    normalized,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool IsExactMatch(string query)
        {
            string normalized = Normalize(query);
            return normalized.Length > 0 &&
                (string.Equals(
                     DisplayName,
                     normalized,
                     StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(
                     StableId,
                     normalized,
                     StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string query)
        {
            return string.IsNullOrWhiteSpace(query)
                ? string.Empty
                : query.Trim();
        }
    }

    public readonly struct GrayboxDeveloperCommandResult3D
    {
        internal GrayboxDeveloperCommandResult3D(
            GrayboxDeveloperCommandCode3D code,
            bool succeeded,
            string stableId,
            string displayName,
            int requestedAmount,
            int appliedAmount,
            int affectedCount,
            string message)
        {
            Code = code;
            Succeeded = succeeded;
            StableId = stableId;
            DisplayName = displayName;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            AffectedCount = affectedCount;
            Message = message ?? string.Empty;
        }

        public GrayboxDeveloperCommandCode3D Code { get; }
        public bool Succeeded { get; }
        public string StableId { get; }
        public string DisplayName { get; }
        public int RequestedAmount { get; }
        public int AppliedAmount { get; }
        public int AffectedCount { get; }
        public string Message { get; }
    }

    public static class GrayboxDeveloperCatalogQuery3D
    {
        private static readonly GrayboxDeveloperCatalogEntry3D[] resources =
            BuildResources();
        private static readonly GrayboxDeveloperCatalogEntry3D[] research =
            BuildResearch();
        private static readonly GrayboxDeveloperCatalogEntry3D[]
            technologyStatuses = BuildTechnologyStatuses();
        private static readonly GrayboxDeveloperCatalogEntry3D[] actions =
            BuildActions();
        private static readonly IReadOnlyList<
            GrayboxDeveloperCatalogEntry3D> readOnlyResources =
                Array.AsReadOnly(resources);
        private static readonly IReadOnlyList<
            GrayboxDeveloperCatalogEntry3D> readOnlyResearch =
                Array.AsReadOnly(research);
        private static readonly IReadOnlyList<
            GrayboxDeveloperCatalogEntry3D> readOnlyTechnologyStatuses =
                Array.AsReadOnly(technologyStatuses);
        private static readonly IReadOnlyList<
            GrayboxDeveloperCatalogEntry3D> readOnlyActions =
                Array.AsReadOnly(actions);

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            ResourceEntries => readOnlyResources;

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            ResearchEntries => readOnlyResearch;

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            TechnologyStatusEntries => readOnlyTechnologyStatuses;

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            ProgressionActionEntries => readOnlyActions;

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            SearchResources(string query)
        {
            return string.IsNullOrWhiteSpace(query)
                ? readOnlyResources
                : Search(resources, query);
        }

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            SearchResearch(string query)
        {
            return string.IsNullOrWhiteSpace(query)
                ? readOnlyResearch
                : Search(research, query);
        }

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            SearchTechnologyStatuses(string query)
        {
            return string.IsNullOrWhiteSpace(query)
                ? readOnlyTechnologyStatuses
                : Search(technologyStatuses, query);
        }

        public static IReadOnlyList<GrayboxDeveloperCatalogEntry3D>
            SearchProgressionActions(string query)
        {
            return string.IsNullOrWhiteSpace(query)
                ? readOnlyActions
                : Search(actions, query);
        }

        public static bool TryResolveResource(
            string query,
            out GrayboxDeveloperCatalogEntry3D entry)
        {
            return TryResolve(resources, query, out entry);
        }

        public static bool TryResolveResearch(
            string query,
            out GrayboxDeveloperCatalogEntry3D entry)
        {
            return TryResolve(research, query, out entry);
        }

        public static bool TryResolveTechnologyStatus(
            string query,
            out GrayboxDeveloperCatalogEntry3D entry)
        {
            return TryResolve(technologyStatuses, query, out entry);
        }

        public static bool TryResolveProgressionAction(
            string query,
            out GrayboxDeveloperCatalogEntry3D entry)
        {
            return TryResolve(actions, query, out entry);
        }

        private static GrayboxDeveloperCatalogEntry3D[] BuildResources()
        {
            IReadOnlyList<ResourceDefinition> definitions =
                ResourceDefinitionCatalog.All;
            var result = new GrayboxDeveloperCatalogEntry3D[
                definitions.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new GrayboxDeveloperCatalogEntry3D(
                    GrayboxDeveloperCatalogKind3D.Resource,
                    definitions[index].Id,
                    definitions[index].ChineseName);
            }
            return result;
        }

        private static GrayboxDeveloperCatalogEntry3D[] BuildResearch()
        {
            ResearchDefinition[] definitions = ResearchCatalog.All;
            var result = new GrayboxDeveloperCatalogEntry3D[
                definitions.Length];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new GrayboxDeveloperCatalogEntry3D(
                    GrayboxDeveloperCatalogKind3D.Research,
                    definitions[index].Id.Value,
                    definitions[index].Name);
            }
            return result;
        }

        private static GrayboxDeveloperCatalogEntry3D[]
            BuildTechnologyStatuses()
        {
            IReadOnlyList<ResearchStatusDefinition> definitions =
                ResearchStatusCatalog.All;
            var result = new GrayboxDeveloperCatalogEntry3D[
                definitions.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new GrayboxDeveloperCatalogEntry3D(
                    GrayboxDeveloperCatalogKind3D.TechnologyStatus,
                    definitions[index].Id,
                    definitions[index].DisplayName);
            }
            return result;
        }

        private static GrayboxDeveloperCatalogEntry3D[] BuildActions()
        {
            (string Id, string Name)[] definitions =
            {
                ("developer.attention.increase", "增加关注度"),
                ("developer.attention.decrease", "降低关注度"),
                ("developer.attention.set", "设置关注度"),
                ("developer.fate.select-pocket-universe", "选择袖珍宇宙命轨"),
                ("developer.fate.select-void-debt", "选择虚空债命轨"),
                ("developer.fate.select-rewind-anchor", "选择回溯锚点命轨"),
                ("developer.fate.upgrade-level-two", "命轨升到二级"),
                ("developer.rewind.create", "创建回溯锚点"),
                ("developer.rewind.read", "读取指定回溯锚点"),
                ("developer.rewind.clear", "清理全部回溯锚点"),
                ("developer.void-debt.add", "增加指定资源虚空债"),
                ("developer.void-debt.repay", "偿还指定资源虚空债"),
                ("developer.pressure.trigger", "触发压力夹具"),
                ("developer.pressure.complete", "完成压力夹具"),
                ("developer.pressure.reset", "重置压力夹具"),
                ("developer.boss.set-defeated", "设置晶壳母体已击败"),
                ("developer.boss.clear-defeated", "清除晶壳母体击败状态"),
                ("developer.ascension.requirements-satisfy", "满足升阶测试条件"),
                ("developer.ascension.requirements-clear", "清除升阶测试条件"),
                ("developer.civilization.first-ascension", "执行首次文明升阶"),
                ("developer.query.committed-ids", "查询已提交稳定ID"),
                ("developer.query.thresholds", "查询关注度阈值"),
                ("developer.query.pressure-queue", "查询压力队列"),
                ("developer.query.configuration-signature", "查询进度配置签名"),
            };
            var result = new GrayboxDeveloperCatalogEntry3D[
                definitions.Length];
            for (var index = 0; index < result.Length; index++)
                result[index] = new GrayboxDeveloperCatalogEntry3D(
                    GrayboxDeveloperCatalogKind3D.ProgressionAction,
                    definitions[index].Id,
                    definitions[index].Name);
            return result;
        }

        private static IReadOnlyList<GrayboxDeveloperCatalogEntry3D> Search(
            GrayboxDeveloperCatalogEntry3D[] source,
            string query)
        {
            var matches = new List<GrayboxDeveloperCatalogEntry3D>();
            for (var index = 0; index < source.Length; index++)
                if (source[index].Matches(query))
                    matches.Add(source[index]);
            return matches.AsReadOnly();
        }

        private static bool TryResolve(
            GrayboxDeveloperCatalogEntry3D[] source,
            string query,
            out GrayboxDeveloperCatalogEntry3D entry)
        {
            for (var index = 0; index < source.Length; index++)
            {
                if (!source[index].IsExactMatch(query)) continue;
                entry = source[index];
                return true;
            }
            entry = null;
            return false;
        }
    }
}
#endif
