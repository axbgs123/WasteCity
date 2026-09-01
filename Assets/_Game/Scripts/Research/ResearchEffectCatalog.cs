using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public enum ResearchEffectKind
    {
        UnlockContent,
        ProductionCycle,
        ResearchSpeed,
        LogisticsRange,
        BuildingHealth,
        TowerRange,
        TowerDamage,
        TowerAttackInterval,
        PhysicalDamageTaken,
        BiomassRecovery,
        Regeneration,
        WarningDuration,
        CommunicationCost,
        UnitCapacity,
        UnitHealth,
        Healing,
        RuleToggle,
        Risk,
    }

    public enum ResearchEffectActivation
    {
        Active,
        Preview,
    }

    public enum ResearchEffectOperation
    {
        Multiply,
        Override,
        Toggle,
        Informational,
    }

    public sealed class ResearchEffectDefinition
    {
        internal ResearchEffectDefinition(
            string id,
            string researchId,
            ResearchEffectKind kind,
            ResearchEffectActivation activation,
            ResearchEffectOperation operation,
            bool isExecutable,
            string targetId,
            float runtimeValue,
            string displayName,
            float beforeValue,
            float afterValue,
            string unit,
            string scope,
            string stacking,
            string description)
        {
            Id = id ?? string.Empty;
            ResearchId = researchId ?? string.Empty;
            Kind = kind;
            Activation = activation;
            Operation = operation;
            IsExecutable = isExecutable;
            TargetId = targetId ?? string.Empty;
            RuntimeValue = runtimeValue;
            DisplayName = displayName ?? string.Empty;
            BeforeValue = beforeValue;
            AfterValue = afterValue;
            Unit = unit ?? string.Empty;
            Scope = scope ?? string.Empty;
            Stacking = stacking ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string Id { get; }
        public string ResearchId { get; }
        public ResearchEffectKind Kind { get; }
        public ResearchEffectActivation Activation { get; }
        public ResearchEffectOperation Operation { get; }
        public bool IsExecutable { get; }
        public string TargetId { get; }
        public float RuntimeValue { get; }
        public string DisplayName { get; }
        public float BeforeValue { get; }
        public float AfterValue { get; }
        public string Unit { get; }
        public string Scope { get; }
        public string Stacking { get; }
        public string Description { get; }
    }

    public static class ResearchEffectCatalog
    {
        private const string Scrap = "core.research.scrap-processing";
        private const string Formation =
            "core.research.formation-reinforcement";

        private static readonly ReadOnlyCollection<ResearchEffectDefinition>
            all = Array.AsReadOnly(new[]
            {
                Cycle(Scrap, "core.production.extract-node-resource", .95f,
                    "采矿效率", 100f, 105f, "全部采矿站"),
                Unlock(Scrap, "基础城市与采集建筑"),

                Cycle("core.research.automated-machinery",
                    "core.production.smelt-alloy", .9f,
                    "合金冶炼效率", 100f, 111f, "全部冶炼厂"),
                Unlock("core.research.automated-machinery", "冶炼厂与合金配方"),
                Cycle("core.research.spirit-sensing",
                    "cultivation.production.refine-spirit-iron", .9f,
                    "灵铁淬炼效率", 100f, 111f, "全部灵火炉"),
                Unlock("core.research.spirit-sensing", "灵火炉与灵铁配方"),
                Cycle("core.research.adaptive-tissue",
                    "biological.production.biomass-concentrate", .9f,
                    "菌落培养效率", 100f, 111f, "全部菌落池"),
                Unlock("core.research.adaptive-tissue", "菌落池与基础生物材料"),
                Cycle("core.research.mind-resonance",
                    "psionics.production.resonance-metal", .9f,
                    "共振金属效率", 100f, 111f, "全部共振炉"),
                Unlock("core.research.mind-resonance", "共振炉与共振金属配方"),

                Cycle("core.research.precision-assembly",
                    "core.production.assemble-ammunition", .9f,
                    "弹药装配效率", 100f, 111f, "全部装配厂"),
                Unlock("core.research.precision-assembly", "装配厂与弹药配方"),
                Scalar("core.research.automated-defense",
                    ResearchEffectKind.TowerAttackInterval, .9f,
                    "core.weapon.machine-gun", "机枪射击效率",
                    100f, 111f, "%", "机枪系炮塔", "同组取最高"),
                Unlock("core.research.automated-defense", "机枪塔"),
                Cycle("core.research.thermal-engineering",
                    "technology.production.energy-cell", .8f,
                    "能量电池生产效率", 100f, 125f, "全部发电站"),
                Unlock("core.research.thermal-engineering", "发电站与能量电池配方"),
                Scalar("core.research.ballistics",
                    ResearchEffectKind.TowerRange, 1.2f, "ammo",
                    "炮塔射程", 100f, 120f, "%", "弹药炮塔", "不叠加"),
                Scalar("core.research.ballistics",
                    ResearchEffectKind.TowerDamage, 1.15f, "ammo",
                    "弹药伤害", 100f, 115f, "%", "弹药武器", "不叠加"),

                Cycle("core.research.artifact-crafting",
                    "cultivation.production.flying-sword", .85f,
                    "飞剑炼制效率", 100f, 118f, "全部炼器坊"),
                Unlock("core.research.artifact-crafting", "炼器坊与飞剑配方"),
                Scalar("core.research.sword-array",
                    ResearchEffectKind.TowerDamage, 1.15f,
                    "cultivation.weapon.sword-array", "剑阵伤害",
                    100f, 115f, "%", "剑阵系炮塔", "同组取最高"),
                Unlock("core.research.sword-array", "剑阵塔"),
                Cycle("core.research.spirit-gathering",
                    "cultivation.production.gather-spirit-stone", .8f,
                    "聚灵生产效率", 100f, 125f, "全部聚灵阵"),
                Unlock("core.research.spirit-gathering", "聚灵阵与灵石配方"),
                Scalar("core.research.talisman-basics",
                    ResearchEffectKind.PhysicalDamageTaken, .8f,
                    "core.building.wall", "城墙物理承伤",
                    100f, 80f, "%", "全部城墙", "不叠加"),

                Cycle("core.research.bio-cultivation",
                    "biological.production.weapon", .85f,
                    "生物武器培育效率", 100f, 118f, "全部培育室"),
                Unlock("core.research.bio-cultivation", "培育室与生物武器配方"),
                Scalar("core.research.spore-dispersal",
                    ResearchEffectKind.TowerDamage, 1.15f, "biological",
                    "孢子伤害", 100f, 115f, "%", "孢子塔", "不叠加"),
                Unlock("core.research.metabolic-acceleration", "代谢炉"),
                Scalar("core.research.metabolic-acceleration",
                    ResearchEffectKind.BiomassRecovery, 1.5f, "enemy.corpse",
                    "生物质回收", 100f, 150f, "%", "敌人尸体", "不叠加"),
                Scalar("core.research.carapace-growth",
                    ResearchEffectKind.Regeneration, 10f, "core.building.wall",
                    "甲壳再生", 0f, 10f, "点/5秒", "全部城墙",
                    "与组织再生并行；消耗1生物质"),

                Cycle("core.research.psionic-workshop",
                    "psionics.production.amplifier", .85f,
                    "灵能增幅器效率", 100f, 118f, "全部灵能工坊"),
                Unlock("core.research.psionic-workshop", "灵能工坊与增幅器配方"),
                Scalar("core.research.mind-spire",
                    ResearchEffectKind.RuleToggle, 1f, "psionic.damage",
                    "灵能穿透", 0f, 1f, "规则", "心灵尖塔", "不叠加"),
                ActiveInformation("core.research.consciousness-network",
                    ResearchEffectKind.CommunicationCost, 0f, "same.planet",
                    "远程通信成本", 100f, 0f, "%", "同星球城市",
                    "多城市控制循环完成后接入"),
                Cycle("core.research.consciousness-network",
                    "psionics.production.consciousness-shard", .9f,
                    "意识碎片沉淀效率", 100f, 111f, "全部意识网络"),
                Unlock("core.research.consciousness-network",
                    "意识网络与意识碎片配方"),
                Scalar("core.research.thought-acceleration",
                    ResearchEffectKind.ResearchSpeed, 1.25f, "city.research",
                    "研究效率", 100f, 125f, "%", "全城研究", "不叠加"),

                Scalar("core.research.alloy-armor",
                    ResearchEffectKind.BuildingHealth, 1.3f, "all.building",
                    "建筑最大耐久", 100f, 130f, "%", "全部建筑",
                    "不叠加"),
                Scalar("core.research.unmanned-systems",
                    ResearchEffectKind.ProductionCycle, .85f,
                    "technology.production.automated-repair",
                    "自动维修效率", 100f, 118f, "%", "自动维修设施",
                    "不叠加"),
                Override("core.research.orbital-supply",
                    ResearchEffectKind.LogisticsRange, 24f, "city.logistics",
                    "物流范围", 8f, 24f, "格", "展开城市", "覆盖低级范围"),
                Scalar("core.research.energy-weapons",
                    ResearchEffectKind.RuleToggle, 1f, "energy.overload",
                    "能量过载", 0f, 1f, "规则", "能量武器", "不叠加"),

                Scalar("core.research.sword-riding",
                    ResearchEffectKind.TowerRange, 1.3f,
                    "cultivation.weapon.flying-sword", "飞剑射程",
                    100f, 130f, "%", "剑阵塔与御剑台", "不叠加"),
                Cycle("core.research.alchemy",
                    "cultivation.production.elixir", .85f,
                    "普通灵丹效率", 100f, 118f, "全部炼丹房"),
                Unlock("core.research.alchemy", "炼丹房与普通灵丹配方"),
                Override(Formation, ResearchEffectKind.LogisticsRange, 12f,
                    "city.logistics", "物流范围", 8f, 12f, "格",
                    "展开城市", "轨道补给优先"),
                Cycle(Formation,
                    "cultivation.production.gather-spirit-stone",
                    1f / 1.5f, "聚灵生产效率", 100f, 150f, "全部聚灵阵"),
                Scalar("core.research.puppetry",
                    ResearchEffectKind.UnitCapacity, 4f,
                    "cultivation.building.puppet-workshop", "工坊单位容量",
                    3f, 4f, "个", "单座傀儡工坊", "覆盖基础容量"),

                Scalar("core.research.behemoth-breeding",
                    ResearchEffectKind.UnitHealth, 1.1f,
                    "biological.unit.bred-behemoth",
                    "培育巨兽最大生命", 100f, 110f, "%", "培育巨兽",
                    "不叠加"),
                Scalar("core.research.acid-spit",
                    ResearchEffectKind.TowerDamage, 1.3f, "armor.heavy",
                    "对重甲伤害", 100f, 130f, "%", "酸液塔", "不叠加"),
                Scalar("core.research.tissue-regeneration",
                    ResearchEffectKind.Regeneration, 1f, "all.building",
                    "建筑与军队再生", 0f, 1f, "点/秒",
                    "全部建筑与活动军队", "不叠加"),
                Scalar("core.research.gene-splicing",
                    ResearchEffectKind.UnitHealth, 1.2f, "leader.temporary",
                    "领袖临时最大生命", 100f, 120f, "%", "当前领袖",
                    "首次正式完成时应用一次，不重复"),

                Scalar("core.research.mind-shield",
                    ResearchEffectKind.Regeneration, 20f, "city.shield",
                    "城市护盾补充", 0f, 20f, "点/8秒", "城市护盾",
                    "单目标上限100"),
                Scalar("core.research.mind-control",
                    ResearchEffectKind.RuleToggle, .1f, "enemy.normal",
                    "普通目标控制概率", 0f, 10f, "%", "普通非重型敌人",
                    "每次命中独立确定性判定"),
                Scalar("core.research.precognitive-sense",
                    ResearchEffectKind.WarningDuration, 1.5f, "wave.warning",
                    "预警时间", 100f, 150f, "%", "全部波次", "不叠加"),
                Scalar("core.research.collective-consciousness",
                    ResearchEffectKind.ResearchSpeed, .2f,
                    "multi-city.inherited-progress", "新研究继承进度",
                    0f, 20f, "%", "同文明城市", "开始研究时应用一次"),

                Cycle("core.research.bridge.psionic-mech",
                    "fusion.production.psionic-mech-components", .85f,
                    "灵能机甲组件效率", 100f, 118f, "灵能机甲厂"),
                Unlock("core.research.bridge.psionic-mech", "灵能机甲厂与组件配方"),
                Cycle("core.research.bridge.high-frequency-sword",
                    "fusion.production.high-frequency-flying-sword", .8f,
                    "高周波飞剑效率", 100f, 125f, "高周波飞剑铸造台"),
                Unlock("core.research.bridge.high-frequency-sword", "高周波飞剑铸造台"),
                Cycle("core.research.bridge.bio-hangar",
                    "fusion.production.bio-hangar-weapons", .85f,
                    "生物机库武器效率", 100f, 118f, "生物机库"),
                Unlock("core.research.bridge.bio-hangar", "生物机库与武器配方"),
                Cycle("core.research.bridge.spirit-plant",
                    "fusion.production.spirit-plant-extract", .8f,
                    "灵植精华效率", 100f, 125f, "灵植园"),
                Unlock("core.research.bridge.spirit-plant", "灵植园与精华配方"),
                Scalar("core.research.bridge.psionic-pulse",
                    ResearchEffectKind.RuleToggle, 1f, "mechanical.suppression",
                    "机械目标抑制", 0f, 1f, "规则", "EMP塔命中目标",
                    "不叠加"),
                Unlock("core.research.bridge.psionic-pulse", "EMP塔"),
                Scalar("core.research.bridge.flesh-elixir",
                    ResearchEffectKind.Healing, 3f, "fusion.elixir",
                    "血肉灵丹治疗", 100f, 300f, "%", "血肉灵丹", "不叠加"),
                Scalar("core.research.bridge.flesh-elixir",
                    ResearchEffectKind.Risk, .2f, "fusion.elixir",
                    "即时突变风险", 0f, 20f, "%", "每次使用", "独立判定"),
                Unlock("core.research.bridge.flesh-elixir", "血肉灵丹配方"),
                Scalar("core.research.legacy-analysis",
                    ResearchEffectKind.RuleToggle, 1f,
                    "civilization.advancement", "文明升阶条件",
                    0f, 1f, "规则", "首次文明升阶", "不叠加"),
            });

        private static readonly IReadOnlyDictionary<string,
            ReadOnlyCollection<ResearchEffectDefinition>> byResearch =
            BuildLookup();

        public static IReadOnlyList<ResearchEffectDefinition> All => all;

        public static IReadOnlyList<ResearchEffectDefinition> ForResearch(
            string researchId)
        {
            if (string.IsNullOrWhiteSpace(researchId) ||
                !byResearch.TryGetValue(researchId, out var values))
            {
                return Array.Empty<ResearchEffectDefinition>();
            }
            return values;
        }

        private static ResearchEffectDefinition Cycle(
            string researchId,
            string recipeId,
            float multiplier,
            string name,
            float before,
            float after,
            string scope)
        {
            return Definition(researchId, ResearchEffectKind.ProductionCycle,
                ResearchEffectActivation.Active,
                ResearchEffectOperation.Multiply, true, recipeId, multiplier,
                name, before, after, "%", scope, "同目标乘算", string.Empty);
        }

        private static ResearchEffectDefinition Scalar(
            string researchId,
            ResearchEffectKind kind,
            float value,
            string targetId,
            string name,
            float before,
            float after,
            string unit,
            string scope,
            string stacking)
        {
            return Definition(researchId, kind,
                ResearchEffectActivation.Active,
                ResearchEffectOperation.Multiply, true, targetId, value,
                name, before, after, unit, scope, stacking, string.Empty);
        }

        private static ResearchEffectDefinition Override(
            string researchId,
            ResearchEffectKind kind,
            float value,
            string targetId,
            string name,
            float before,
            float after,
            string unit,
            string scope,
            string stacking)
        {
            return Definition(researchId, kind,
                ResearchEffectActivation.Active,
                ResearchEffectOperation.Override, true, targetId, value,
                name, before, after, unit, scope, stacking, string.Empty);
        }

        private static ResearchEffectDefinition Preview(
            string researchId,
            ResearchEffectKind kind,
            float value,
            string targetId,
            string name,
            float before,
            float after,
            string unit,
            string scope)
        {
            return Definition(researchId, kind,
                ResearchEffectActivation.Preview,
                ResearchEffectOperation.Informational, false, targetId,
                value, name, before, after, unit, scope, "待接入",
                "仅预览，效果待接入");
        }

        private static ResearchEffectDefinition ActiveInformation(
            string researchId,
            ResearchEffectKind kind,
            float value,
            string targetId,
            string name,
            float before,
            float after,
            string unit,
            string scope,
            string description)
        {
            return Definition(researchId, kind,
                ResearchEffectActivation.Active,
                ResearchEffectOperation.Informational, false, targetId,
                value, name, before, after, unit, scope, "待后续系统接入",
                description);
        }

        private static ResearchEffectDefinition Unlock(
            string researchId,
            string description)
        {
            return Definition(researchId, ResearchEffectKind.UnlockContent,
                ResearchEffectActivation.Active,
                ResearchEffectOperation.Informational, false, "content",
                1f, "解锁", 0f, 1f, string.Empty, "内容目录", "不叠加",
                description);
        }

        private static ResearchEffectDefinition Definition(
            string researchId,
            ResearchEffectKind kind,
            ResearchEffectActivation activation,
            ResearchEffectOperation operation,
            bool executable,
            string targetId,
            float runtimeValue,
            string name,
            float before,
            float after,
            string unit,
            string scope,
            string stacking,
            string description)
        {
            string suffix = researchId.StartsWith(
                    "core.research.", StringComparison.Ordinal)
                ? researchId.Substring("core.research.".Length)
                : researchId;
            string target = string.IsNullOrEmpty(targetId)
                ? "global"
                : targetId.Replace('.', '-');
            return new ResearchEffectDefinition(
                "core.effect.research." + suffix + "." +
                kind.ToString().ToLowerInvariant() + "." + target,
                researchId, kind, activation, operation, executable,
                targetId, runtimeValue, name, before, after, unit, scope,
                stacking, description);
        }

        private static IReadOnlyDictionary<string,
            ReadOnlyCollection<ResearchEffectDefinition>> BuildLookup()
        {
            return all.GroupBy(value => value.ResearchId,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => new ReadOnlyCollection<ResearchEffectDefinition>(
                        group.ToArray()),
                    StringComparer.Ordinal);
        }
    }

    public sealed class ResearchEffectSnapshot
    {
        private readonly IReadOnlyDictionary<string, float> productionCycles;

        internal ResearchEffectSnapshot(
            float researchSpeedMultiplier,
            int logisticsRange,
            float buildingHealthMultiplier,
            float warningDurationMultiplier,
            float biomassRecoveryMultiplier,
            bool tissueRegeneration,
            bool carapaceGrowth,
            IReadOnlyDictionary<string, float> productionCycles,
            IReadOnlyList<ResearchEffectDefinition> appliedEffects,
            IReadOnlyList<string> appliedEffectIds)
        {
            ResearchSpeedMultiplier = researchSpeedMultiplier;
            LogisticsRange = logisticsRange;
            BuildingHealthMultiplier = buildingHealthMultiplier;
            WarningDurationMultiplier = warningDurationMultiplier;
            BiomassRecoveryMultiplier = biomassRecoveryMultiplier;
            TissueRegeneration = tissueRegeneration;
            CarapaceGrowth = carapaceGrowth;
            this.productionCycles = productionCycles;
            this.appliedEffects = appliedEffects;
            AppliedEffectIds = appliedEffectIds;
        }

        private readonly IReadOnlyList<ResearchEffectDefinition>
            appliedEffects;

        public float ResearchSpeedMultiplier { get; }
        public int LogisticsRange { get; }
        public float BuildingHealthMultiplier { get; }
        public float WarningDurationMultiplier { get; }
        public float BiomassRecoveryMultiplier { get; }
        public bool TissueRegeneration { get; }
        public bool CarapaceGrowth { get; }
        public IReadOnlyList<string> AppliedEffectIds { get; }

        public float ProductionCycleMultiplier(string recipeId)
        {
            return !string.IsNullOrWhiteSpace(recipeId) &&
                productionCycles.TryGetValue(recipeId, out float value)
                    ? value
                    : 1f;
        }

        public int ResolveLogisticsRange(int baseRange)
        {
            return Math.Max(Math.Max(0, baseRange), LogisticsRange);
        }

        public int ResolveBuildingMaximumHealth(int baseHealth)
        {
            return Math.Max(
                1,
                (int)Math.Round(
                    Math.Max(1, baseHealth) *
                    BuildingHealthMultiplier,
                    MidpointRounding.AwayFromZero));
        }

        public float ResolveTowerRangeMultiplier(string buildingId)
        {
            return ResolveTowerMultiplier(
                buildingId,
                ResearchEffectKind.TowerRange,
                invertRuntimeValue: false);
        }

        public float ResolveTowerDamageMultiplier(string buildingId)
        {
            float damage = ResolveTowerMultiplier(
                buildingId,
                ResearchEffectKind.TowerDamage,
                invertRuntimeValue: false);
            float interval = ResolveTowerMultiplier(
                buildingId,
                ResearchEffectKind.TowerAttackInterval,
                invertRuntimeValue: true);
            return Math.Max(damage, interval);
        }

        public float ResolvePhysicalDamageTakenMultiplier(string buildingId)
        {
            if (string.IsNullOrWhiteSpace(buildingId)) return 1f;
            return ResolveExactMultiplier(
                ResearchEffectKind.PhysicalDamageTaken,
                buildingId,
                1f,
                preferLowerValue: true);
        }

        public int ResolveUnitCapacity(string targetId, int baseCapacity)
        {
            int resolved = Math.Max(0, baseCapacity);
            float value = ResolveExactMultiplier(
                ResearchEffectKind.UnitCapacity,
                targetId,
                resolved,
                preferLowerValue: false);
            return Math.Max(resolved, (int)Math.Round(value));
        }

        public float ResolveUnitHealthMultiplier(string unitId)
        {
            return ResolveExactMultiplier(
                ResearchEffectKind.UnitHealth,
                unitId,
                1f,
                preferLowerValue: false);
        }

        public float ResolveHeavyArmorDamageMultiplier(string towerBuildingId)
        {
            if (!string.Equals(
                    towerBuildingId,
                    BuildingCatalog.AcidTower.Id.Value,
                    StringComparison.Ordinal))
            {
                return 1f;
            }
            return ResolveExactMultiplier(
                ResearchEffectKind.TowerDamage,
                "armor.heavy",
                1f,
                preferLowerValue: false);
        }

        public float CollectiveConsciousnessInitialProgressFraction =>
            Math.Max(
                0f,
                Math.Min(
                    1f,
                    ResolveExactMultiplier(
                        ResearchEffectKind.ResearchSpeed,
                        "multi-city.inherited-progress",
                        0f,
                        preferLowerValue: false)));

        public bool HasActiveRule(string targetId)
        {
            return !string.IsNullOrWhiteSpace(targetId) &&
                appliedEffects.Any(effect =>
                    effect.Kind == ResearchEffectKind.RuleToggle &&
                    string.Equals(
                        effect.TargetId,
                        targetId,
                        StringComparison.Ordinal));
        }

        private float ResolveExactMultiplier(
            ResearchEffectKind kind,
            string targetId,
            float fallback,
            bool preferLowerValue)
        {
            if (string.IsNullOrWhiteSpace(targetId)) return fallback;
            float resolved = fallback;
            for (var index = 0; index < appliedEffects.Count; index++)
            {
                ResearchEffectDefinition effect = appliedEffects[index];
                if (effect.Kind != kind ||
                    !string.Equals(
                        effect.TargetId,
                        targetId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                resolved = preferLowerValue
                    ? Math.Min(resolved, effect.RuntimeValue)
                    : Math.Max(resolved, effect.RuntimeValue);
            }
            return resolved;
        }

        private float ResolveTowerMultiplier(
            string buildingId,
            ResearchEffectKind kind,
            bool invertRuntimeValue)
        {
            float resolved = 1f;
            for (var index = 0; index < appliedEffects.Count; index++)
            {
                ResearchEffectDefinition effect = appliedEffects[index];
                if (effect.Kind != kind ||
                    !EffectTargetsBuilding(effect.TargetId, buildingId))
                {
                    continue;
                }

                float value = invertRuntimeValue
                    ? 1f / Math.Max(.001f, effect.RuntimeValue)
                    : effect.RuntimeValue;
                resolved = Math.Max(resolved, value);
            }
            return resolved;
        }

        private static bool EffectTargetsBuilding(
            string targetId,
            string buildingId)
        {
            if (string.IsNullOrEmpty(buildingId)) return false;
            if (targetId == "core.weapon.machine-gun" ||
                targetId == "ammo")
            {
                return buildingId == BuildingCatalog.MachineGunTurret.Id.Value ||
                    buildingId ==
                        BuildingCatalog.HeavyMachineGunTurret.Id.Value;
            }
            if (targetId == "cultivation.weapon.sword-array" ||
                targetId == "cultivation.weapon.flying-sword")
            {
                return buildingId == BuildingCatalog.SwordArrayTower.Id.Value ||
                    buildingId ==
                        BuildingCatalog.SwordRidingPlatform.Id.Value;
            }
            if (targetId == "biological")
                return buildingId == BuildingCatalog.SporeTower.Id.Value;
            if (targetId != "all.tower") return false;
            return buildingId == BuildingCatalog.MachineGunTurret.Id.Value ||
                buildingId == BuildingCatalog.HeavyMachineGunTurret.Id.Value ||
                buildingId == BuildingCatalog.SwordArrayTower.Id.Value ||
                buildingId == BuildingCatalog.SwordRidingPlatform.Id.Value ||
                buildingId == BuildingCatalog.SporeTower.Id.Value ||
                buildingId == BuildingCatalog.LaserTower.Id.Value ||
                buildingId == BuildingCatalog.AcidTower.Id.Value ||
                buildingId == BuildingCatalog.EmpTower.Id.Value;
        }
    }

    public static class ResearchEffectResolver
    {
        public static ResearchEffectSnapshot Resolve(
            IEnumerable<string> completedResearchIds)
        {
            var completed = new HashSet<string>(
                completedResearchIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var cycles = new Dictionary<string, float>(StringComparer.Ordinal);
            var applied = new SortedSet<string>(StringComparer.Ordinal);
            var appliedEffects = new List<ResearchEffectDefinition>();
            float researchSpeed = 1f;
            float buildingHealth = 1f;
            float warningDuration = 1f;
            float biomassRecovery = 1f;
            bool tissueRegeneration = false;
            bool carapaceGrowth = false;
            int logisticsRange = 8;

            foreach (ResearchEffectDefinition effect in
                     ResearchEffectCatalog.All)
            {
                if (!completed.Contains(effect.ResearchId) ||
                    !effect.IsExecutable ||
                    effect.Activation != ResearchEffectActivation.Active)
                {
                    continue;
                }

                applied.Add(effect.Id);
                appliedEffects.Add(effect);
                switch (effect.Kind)
                {
                    case ResearchEffectKind.ProductionCycle:
                        cycles.TryGetValue(effect.TargetId, out float current);
                        cycles[effect.TargetId] =
                            (current <= 0f ? 1f : current) *
                            effect.RuntimeValue;
                        break;
                    case ResearchEffectKind.ResearchSpeed:
                        if (effect.TargetId == "city.research")
                            researchSpeed = Math.Max(
                                researchSpeed, effect.RuntimeValue);
                        break;
                    case ResearchEffectKind.LogisticsRange:
                        logisticsRange = Math.Max(
                            logisticsRange,
                            (int)Math.Round(effect.RuntimeValue));
                        break;
                    case ResearchEffectKind.BuildingHealth:
                        buildingHealth = Math.Max(
                            buildingHealth, effect.RuntimeValue);
                        break;
                    case ResearchEffectKind.WarningDuration:
                        warningDuration = Math.Max(
                            warningDuration, effect.RuntimeValue);
                        break;
                    case ResearchEffectKind.BiomassRecovery:
                        biomassRecovery = Math.Max(
                            biomassRecovery, effect.RuntimeValue);
                        break;
                    case ResearchEffectKind.Regeneration:
                        tissueRegeneration |= effect.TargetId ==
                            "all.building";
                        carapaceGrowth |= effect.TargetId ==
                            "core.building.wall";
                        break;
                }
            }

            return new ResearchEffectSnapshot(
                researchSpeed,
                logisticsRange,
                buildingHealth,
                warningDuration,
                biomassRecovery,
                tissueRegeneration,
                carapaceGrowth,
                new ReadOnlyDictionary<string, float>(cycles),
                new ReadOnlyCollection<ResearchEffectDefinition>(
                    appliedEffects.OrderBy(
                            value => value.Id,
                            StringComparer.Ordinal)
                        .ToArray()),
                Array.AsReadOnly(applied.ToArray()));
        }
    }

    public static class ResearchKillRewardResolver
    {
        public static int ResolveBiomassDrop(
            int baseDrop,
            float qualityMultiplier,
            ResearchEffectSnapshot effects)
        {
            float multiplier = effects?.BiomassRecoveryMultiplier ?? 1f;
            return RouteTechnologyEffects.BiomassDrop(
                baseDrop,
                qualityMultiplier,
                multiplier);
        }
    }

    public sealed class FormalProductionResearchModifierAdapter :
        IFormalProductionResearchModifier
    {
        private readonly ResearchEffectSnapshot snapshot;

        public FormalProductionResearchModifierAdapter(
            ResearchEffectSnapshot snapshot)
        {
            this.snapshot = snapshot ??
                ResearchEffectResolver.Resolve(Array.Empty<string>());
        }

        public float ResolveCycleDurationSeconds(
            string recipeId,
            float baseDurationSeconds)
        {
            return Math.Max(
                .001f,
                Math.Max(.001f, baseDurationSeconds) *
                snapshot.ProductionCycleMultiplier(recipeId));
        }
    }
}
