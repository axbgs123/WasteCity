using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;

namespace WasteCity.Progression
{
    public sealed class FormalFateDefinition
    {
        internal FormalFateDefinition(
            string id,
            string displayName,
            string brief,
            string levelOneSummary,
            string levelTwoSummary,
            string costSummary,
            string effectAdapterId)
        {
            Id = new StableId(id);
            DisplayName = RequireText(displayName, nameof(displayName));
            Brief = RequireText(brief, nameof(brief));
            LevelOneSummary = RequireText(
                levelOneSummary,
                nameof(levelOneSummary));
            LevelTwoSummary = RequireText(
                levelTwoSummary,
                nameof(levelTwoSummary));
            CostSummary = RequireText(costSummary, nameof(costSummary));
            EffectAdapterId = new StableId(effectAdapterId).Value;
        }

        public StableId Id { get; }
        public string DisplayName { get; }
        public string Brief { get; }
        public string LevelOneSummary { get; }
        public string LevelTwoSummary { get; }
        public string CostSummary { get; }
        public string EffectAdapterId { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Formal fate display text cannot be blank.",
                    parameterName);
            }
            return value;
        }
    }

    public static class FormalFateCatalog
    {
        public const string PocketUniverseId =
            "core.legacy.pocket-universe";
        public const string VoidDebtId = "core.legacy.void-debt";
        public const string RewindAnchorId = "core.legacy.rewind-anchor";

        public const int MaximumLevel = 9;
        public const int MaximumImplementedLevel = 2;
        public const int PocketUniverseCollapseDamage = 150;

        private static readonly ReadOnlyCollection<FormalFateDefinition> all =
            Array.AsReadOnly(new[]
            {
                new FormalFateDefinition(
                    PocketUniverseId,
                    "袖珍宇宙",
                    "把每类正式生产建筑的第一座完工实例折叠为唯一旗舰。",
                    "每类旗舰完整生产输出×2；被摧毁时产生3×3空间坍缩。",
                    "每类旗舰完整生产输出×4；被摧毁时产生4×4空间坍缩。",
                    "旗舰资格不会自动转移；旗舰毁坏会伤及坍缩范围。",
                    "core.fate-effect.pocket-universe"),
                new FormalFateDefinition(
                    VoidDebtId,
                    "虚空债",
                    "允许以未来资源偿还当前建筑施工缺口。",
                    "仅施工可透支；每30秒按每完整10点未还债务增加1关注度。",
                    "透支范围不变，债务关注度结算间隔延长为60秒。",
                    "后续同资源收入优先还债；生产、研究、合成和转移不能透支。",
                    "core.fate-effect.void-debt"),
                new FormalFateDefinition(
                    RewindAnchorId,
                    "回溯锚点",
                    "保存内部世界锚点，并在规则允许时恢复锚点中的世界状态。",
                    "可保留1个锚点，新锚点原子替换旧锚点。",
                    "可保留2个锚点，满槽后按稳定创建顺序替换最旧锚点。",
                    "每次成功读取保留当前关注度和阈值，并额外增加12关注度。",
                    "core.fate-effect.rewind-anchor"),
            });

        private static readonly IReadOnlyDictionary<string,
            FormalFateDefinition> byId = BuildLookup();

        public static IReadOnlyList<FormalFateDefinition> All => all;
        public static IReadOnlyList<FormalFateDefinition> FixedOffers => all;
        public static bool EffectsReady => true;

        public static FormalFateDefinition Find(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                byId.TryGetValue(id, out FormalFateDefinition definition)
                ? definition
                : null;
        }

        private static IReadOnlyDictionary<string,
            FormalFateDefinition> BuildLookup()
        {
            var lookup = new Dictionary<string, FormalFateDefinition>(
                all.Count,
                StringComparer.Ordinal);
            for (var index = 0; index < all.Count; index++)
            {
                FormalFateDefinition definition = all[index];
                lookup.Add(definition.Id.Value, definition);
            }
            return new ReadOnlyDictionary<string, FormalFateDefinition>(lookup);
        }
    }
}
