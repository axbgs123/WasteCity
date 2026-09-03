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
        public const string QuantumEntanglementId =
            "core.legacy.quantum-entanglement";
        public const string SpatialTemplateId =
            "core.legacy.spatial-template";
        public const string VoidDebtId = "core.legacy.void-debt";
        public const string RewindAnchorId = "core.legacy.rewind-anchor";
        public const string LocalHasteId = "core.legacy.local-haste";
        public const string ForesightDelayId =
            "core.legacy.foresight-delay";
        public const string CausalTransparencyId =
            "core.legacy.causal-transparency";
        public const string VoidChestId = "core.legacy.void-chest";

        public const int MaximumLevel = 9;
        public const int MaximumImplementedLevel = 2;
        public const int PocketUniverseCollapseDamage = 150;
        public const float RuleCycleSeconds = 600f;
        public const float ForesightDisplaySeconds = 3f;

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
                    QuantumEntanglementId,
                    "量子纠缠",
                    "让玩家领土中的基础资源跨越距离共享访问。",
                    "基础资源在全部玩家领土间共享；连接区同步承受污染压力。",
                    "共享链路稳定化；访问范围与一级相同。",
                    "污染压力会沿纠缠链路同步传播。",
                    "core.fate-effect.quantum-entanglement"),
                new FormalFateDefinition(
                    SpatialTemplateId,
                    "空间模板",
                    "记录一块3×3建筑布局，并通过正式建造规则再次部署。",
                    "可记录并复用一个3×3空间模板。",
                    "模板链路稳定化；录制范围与一级相同。",
                    "复用模板仍需材料、合法位置和完整施工。",
                    "core.fate-effect.spatial-template"),
                new FormalFateDefinition(
                    RewindAnchorId,
                    "回溯锚点",
                    "保存内部世界锚点，并在规则允许时恢复锚点中的世界状态。",
                    "可保留1个锚点，新锚点原子替换旧锚点。",
                    "可保留2个锚点，满槽后按稳定创建顺序替换最旧锚点。",
                    "每次成功读取保留当前关注度和阈值，并额外增加12关注度。",
                    "core.fate-effect.rewind-anchor"),
                new FormalFateDefinition(
                    LocalHasteId,
                    "局部时加",
                    "把有限时间额度集中分配给选定区域。",
                    "每个规则周期获得60秒时间池，选定区域按5倍推进。",
                    "时间链路稳定化；额度与倍率保持一级。",
                    "时间额度有限，未分配区域不获得加速。",
                    "core.fate-effect.local-haste"),
                new FormalFateDefinition(
                    ForesightDelayId,
                    "预知迟滞",
                    "从权威未来计划中观察短暂且不完整的命运碎片。",
                    "每个规则周期可观察1次持续3秒的未来碎片。",
                    "预知链路稳定化；次数与持续时间保持一级。",
                    "碎片不完整，只展示已经存在的权威计划。",
                    "core.fate-effect.foresight-delay"),
                new FormalFateDefinition(
                    VoidDebtId,
                    "虚空债",
                    "允许以未来资源偿还当前建筑施工缺口。",
                    "仅施工可透支；每30秒按每完整10点未还债务增加1关注度。",
                    "透支范围不变，债务关注度结算间隔延长为60秒。",
                    "后续同资源收入优先还债；生产、研究、合成和转移不能透支。",
                    "core.fate-effect.void-debt"),
                new FormalFateDefinition(
                    CausalTransparencyId,
                    "因果透明",
                    "把关注度变化的来源、事件和阈值关系完整展开。",
                    "解锁完整关注度因果链；普通界面的最近三条仍保留。",
                    "解释链路稳定化；可见范围与一级相同。",
                    "越深入改写因果，越容易引来额外关注。",
                    "core.fate-effect.causal-transparency"),
                new FormalFateDefinition(
                    VoidChestId,
                    "虚空宝箱",
                    "让敌人死亡事件有机会留下来自其它循环的宝箱。",
                    "杂兵有1%概率掉落灰烬宝箱，包含资源与叙事碎片。",
                    "掉落链路稳定化；概率与宝箱等级保持一级。",
                    "奖励来源跨越循环，内容与风险并不完全可控。",
                    "core.fate-effect.void-chest"),
            });

        private static readonly ReadOnlyCollection<FormalFateDefinition>
            fixedOffers = Array.AsReadOnly(new[]
            {
                all[0],
                all[6],
                all[3],
            });

        private static readonly IReadOnlyDictionary<string,
            FormalFateDefinition> byId = BuildLookup();

        public static IReadOnlyList<FormalFateDefinition> All => all;
        public static IReadOnlyList<FormalFateDefinition> FixedOffers =>
            fixedOffers;
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
