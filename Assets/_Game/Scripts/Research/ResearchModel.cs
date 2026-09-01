using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public enum DevelopmentRoute
    {
        Technology = 0,
        Cultivation = 1,
        BiologicalAscension = 2,
        Biological = BiologicalAscension,
        Psionics = 3,
        Common = 4,
        Bridge = 5,
    }

    public enum ResearchReleaseState
    {
        InitiallyCompleted,
        Researchable,
        PreviewOnly,
        RetiredCompatibility,
    }
    public static class CollectiveConsciousnessRules
    {
        public const float SharedProgressRatio = .2f;
        public static float InheritedProgressRatio(bool unlocked) =>
            unlocked ? SharedProgressRatio : 0f;
    }

    public sealed class ResearchDefinition
    {
        public StableId Id { get; }
        public string Name { get; }
        public DevelopmentRoute Route { get; }
        public string CostId { get; }
        public int Cost { get; }
        public IReadOnlyList<ResourceAmount> Costs { get; }
        public float Duration { get; }
        public string RequiredResearchId { get; }
        public IReadOnlyList<string> RequiredResearchIds { get; }
        public int Tier { get; }
        public string EffectSummary { get; }
        public int CatalogOrder { get; }
        public int LayoutRow { get; }
        public string NameKey { get; }
        public string BriefKey { get; }
        public string IconId { get; }
        public ResearchReleaseState ReleaseState { get; }
        public IReadOnlyList<string> EffectReferences { get; }
        public ResearchDefinition(
            string id,
            string name,
            DevelopmentRoute route,
            string costId,
            int cost,
            float duration,
            string requiredResearchId = null,
            int tier = 1,
            string effectSummary = null,
            params string[] additionalRequirements)
            : this(
                id,
                name,
                route,
                string.IsNullOrWhiteSpace(costId) || cost <= 0
                    ? Array.Empty<ResourceAmount>()
                    : new[] { new ResourceAmount(costId, cost) },
                duration,
                requiredResearchId,
                tier,
                effectSummary,
                minimumDuration: .1f,
                additionalRequirements)
        {
            CostId = costId;
            Cost = Math.Max(0, cost);
        }

        public ResearchDefinition(
            string id,
            string name,
            DevelopmentRoute route,
            IReadOnlyList<ResourceAmount> costs,
            float duration,
            string requiredResearchId = null,
            int tier = 1,
            string effectSummary = null,
            params string[] additionalRequirements)
            : this(
                id,
                name,
                route,
                costs,
                duration,
                requiredResearchId,
                tier,
                effectSummary,
                minimumDuration: 0f,
                additionalRequirements)
        {
        }

        private ResearchDefinition(
            string id,
            string name,
            DevelopmentRoute route,
            IReadOnlyList<ResourceAmount> costs,
            float duration,
            string requiredResearchId,
            int tier,
            string effectSummary,
            float minimumDuration,
            params string[] additionalRequirements)
        {
            Id = new StableId(id);
            Name = name;
            Route = route;
            var costSnapshot = new List<ResourceAmount>();
            if (costs != null)
            {
                for (int index = 0; index < costs.Count; index++)
                {
                    ResourceAmount value = costs[index];
                    if (!string.IsNullOrWhiteSpace(value.ResourceId) &&
                        value.Amount > 0)
                    {
                        costSnapshot.Add(value);
                    }
                }
            }
            Costs = new ReadOnlyCollection<ResourceAmount>(costSnapshot);
            CostId = Costs.Count == 0 ? null : Costs[0].ResourceId;
            Cost = Costs.Count == 0 ? 0 : Costs[0].Amount;
            Duration = Math.Max(minimumDuration, duration);
            RequiredResearchId = requiredResearchId;
            Tier = Math.Max(0, Math.Min(3, tier));
            EffectSummary = effectSummary ?? "规则效果待运行系统接入";
            var requirements = new List<string>();
            if (!string.IsNullOrEmpty(requiredResearchId))
                requirements.Add(requiredResearchId);
            if (additionalRequirements != null)
            {
                requirements.AddRange(additionalRequirements.Where(
                    value => !string.IsNullOrEmpty(value)));
            }
            RequiredResearchIds =
                new ReadOnlyCollection<string>(requirements);
            CatalogOrder = -1;
            LayoutRow = Tier;
            NameKey = id + ".name";
            BriefKey = id + ".brief";
            IconId = ResearchIconId(id);
            ReleaseState = ResearchReleaseState.Researchable;
            EffectReferences = Array.AsReadOnly(Array.Empty<string>());
        }

        internal ResearchDefinition(
            string id,
            string name,
            DevelopmentRoute route,
            IReadOnlyList<ResourceAmount> costs,
            float duration,
            IReadOnlyList<string> requiredResearchIds,
            int tier,
            string effectSummary,
            int catalogOrder,
            int layoutRow,
            ResearchReleaseState releaseState,
            IReadOnlyList<string> effectReferences)
            : this(
                id,
                name,
                route,
                costs,
                duration,
                requiredResearchId: null,
                tier,
                effectSummary,
                minimumDuration: 0f,
                Array.Empty<string>())
        {
            var requirements = SnapshotStrings(requiredResearchIds);
            RequiredResearchId = requirements.Count == 0
                ? null
                : requirements[0];
            RequiredResearchIds = requirements;
            CatalogOrder = catalogOrder;
            LayoutRow = Math.Max(0, Math.Min(4, layoutRow));
            NameKey = id + ".name";
            BriefKey = id + ".brief";
            IconId = ResearchIconId(id);
            ReleaseState = releaseState;
            EffectReferences = SnapshotStrings(effectReferences);
        }

        private static ReadOnlyCollection<string> SnapshotStrings(
            IReadOnlyList<string> values)
        {
            var snapshot = new List<string>();
            if (values != null)
            {
                for (var index = 0; index < values.Count; index++)
                {
                    if (!string.IsNullOrWhiteSpace(values[index]))
                        snapshot.Add(values[index]);
                }
            }
            return new ReadOnlyCollection<string>(snapshot);
        }

        private static string ResearchIconId(string id)
        {
            const string prefix = "core.research.";
            return "art.icon.research." +
                (id != null && id.StartsWith(prefix, StringComparison.Ordinal)
                    ? id.Substring(prefix.Length)
                    : id);
        }
    }

    public static class ResearchCatalog
    {
        public const string ScrapProcessingId =
            "core.research.scrap-processing";
        public const string AutomatedMachineryId =
            "core.research.automated-machinery";
        public const string PrecisionAssemblyId =
            "core.research.precision-assembly";
        public const string AutomatedDefenseId =
            "core.research.automated-defense";
        public const string LegacyAnalysisId =
            "core.research.legacy-analysis";
        public const string ThoughtAccelerationId =
            "core.research.thought-acceleration";
        public const string CollectiveConsciousnessId =
            "core.research.collective-consciousness";

        public static readonly ResearchDefinition[] All =
        {
            Node(0, ScrapProcessingId, "废料加工", DevelopmentRoute.Common,
                0, 0, ResearchReleaseState.InitiallyCompleted, Costs(), 0f,
                "解锁基础城市与采集建筑", Req(),
                "building:core.building.mining-station",
                "building:core.building.research-station",
                "building:core.building.housing",
                "building:core.building.warehouse",
                "building:core.building.wall"),

            Node(1, AutomatedMachineryId, "基础冶金",
                DevelopmentRoute.Technology, 1, 1,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Iron, 10)), 20f, "解锁冶炼与应急合金",
                Req(ScrapProcessingId),
                "building:core.building.smelter",
                "recipe:core.production.smelt-alloy",
                "recipe:core.crafting.field-alloy"),
            Node(2, "core.research.spirit-sensing", "灵火淬炼",
                DevelopmentRoute.Cultivation, 1, 1,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.EnergyCrystal, 8), Cost(ResourceIds.Iron, 4)),
                20f, "解锁灵火淬炼", Req(ScrapProcessingId),
                "building:cultivation.building.spirit-fire-furnace",
                "recipe:cultivation.production.refine-spirit-iron"),
            Node(3, "core.research.adaptive-tissue", "菌落培养",
                DevelopmentRoute.Biological, 1, 1,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Biomass, 10), Cost(ResourceIds.Water, 5)),
                20f, "解锁基础菌落培养", Req(ScrapProcessingId),
                "building:biological.building.colony-pool",
                "recipe:biological.production.biomass-concentrate",
                "recipe:biological.production.active-biomass",
                "recipe:biological.production.bone-steel"),
            Node(4, "core.research.mind-resonance", "意识共振",
                DevelopmentRoute.Psionics, 1, 1,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.EnergyCrystal, 8), Cost(ResourceIds.Water, 6)),
                20f, "解锁意识共振加工", Req(ScrapProcessingId),
                "building:psionics.building.resonance-furnace",
                "recipe:psionics.production.resonance-metal"),

            Node(5, PrecisionAssemblyId, "精密装配",
                DevelopmentRoute.Technology, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Alloy, 10)), 30f, "解锁装配与应急弹药",
                Req(AutomatedMachineryId),
                "building:core.building.assembler",
                "recipe:core.production.assemble-ammunition",
                "recipe:core.crafting.field-ammunition"),
            Node(6, AutomatedDefenseId, "自动防御架构",
                DevelopmentRoute.Technology, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Alloy, 12), Cost(ResourceIds.Biomass, 10)),
                35f, "解锁机枪塔", Req(AutomatedMachineryId),
                "building:core.building.machine-gun-turret"),
            Node(7, "core.research.thermal-engineering", "热能工程",
                DevelopmentRoute.Technology, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Iron, 16), Cost(ResourceIds.Alloy, 8)),
                40f, "解锁热能生产", Req(AutomatedMachineryId),
                "building:technology.building.power-plant",
                "recipe:technology.production.energy-cell"),
            Node(8, "core.research.ballistics", "弹道学",
                DevelopmentRoute.Technology, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Iron, 12), Cost(ResourceIds.Alloy, 10)),
                40f, "弹药炮塔射程提高20%，弹药伤害提高15%",
                Req(AutomatedMachineryId),
                "rule:core.effect.ballistics"),

            Node(9, "core.research.artifact-crafting", "炼器基础",
                DevelopmentRoute.Cultivation, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.SpiritIron, 12), Cost(ResourceIds.EnergyCrystal, 8)),
                40f, "解锁法器生产", Req("core.research.spirit-sensing"),
                "building:cultivation.building.artifact-workshop",
                "recipe:cultivation.production.flying-sword"),
            Node(10, "core.research.sword-array", "剑阵初解",
                DevelopmentRoute.Cultivation, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.SpiritIron, 14), Cost(ResourceIds.FlyingSword, 2)),
                40f, "解锁剑阵塔，剑阵伤害提高15%", Req("core.research.spirit-sensing"),
                "building:cultivation.building.sword-array-tower"),
            Node(11, "core.research.spirit-gathering", "聚灵术",
                DevelopmentRoute.Cultivation, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.EnergyCrystal, 16), Cost(ResourceIds.Stone, 8)),
                40f, "解锁聚灵生产", Req("core.research.spirit-sensing"),
                "building:cultivation.building.spirit-gathering-array",
                "recipe:cultivation.production.gather-spirit-stone"),
            Node(12, "core.research.talisman-basics", "符箓入门",
                DevelopmentRoute.Cultivation, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.SpiritIron, 8), Cost(ResourceIds.Stone, 12)),
                40f, "城墙物理承伤降低至80%", Req("core.research.spirit-sensing"),
                "rule:core.effect.wall-talisman"),

            Node(13, "core.research.bio-cultivation", "生物培育",
                DevelopmentRoute.Biological, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BoneSteel, 10),
                    Cost(ResourceIds.BiomassConcentrate, 10)),
                40f, "解锁生物武器生产", Req("core.research.adaptive-tissue"),
                "building:biological.building.breeding-chamber",
                "recipe:biological.production.weapon"),
            Node(14, "core.research.spore-dispersal", "孢子散布",
                DevelopmentRoute.Biological, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BiomassConcentrate, 16), Cost(ResourceIds.Water, 8)),
                40f, "解锁孢子塔、伤害强化与感染", Req("core.research.adaptive-tissue"),
                "building:biological.building.spore-tower"),
            Node(15, "core.research.metabolic-acceleration", "代谢加速",
                DevelopmentRoute.Biological, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Biomass, 16), Cost(ResourceIds.BoneSteel, 8)),
                40f, "解锁代谢生产", Req("core.research.adaptive-tissue"),
                "building:biological.building.metabolic-furnace",
                "rule:biological.effect.corpse-recovery-150-percent"),
            Node(16, "core.research.carapace-growth", "甲壳增生",
                DevelopmentRoute.Biological, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BoneSteel, 12), Cost(ResourceIds.Biomass, 14)),
                40f, "解锁城墙甲壳再生", Req("core.research.adaptive-tissue"),
                "rule:biological.effect.wall-carapace-regeneration"),

            Node(17, "core.research.psionic-workshop", "灵能工坊",
                DevelopmentRoute.Psionics, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.ResonanceMetal, 12),
                    Cost(ResourceIds.EnergyCrystal, 8)),
                40f, "解锁灵能增幅器生产", Req("core.research.mind-resonance"),
                "building:psionics.building.workshop",
                "recipe:psionics.production.amplifier"),
            Node(18, "core.research.mind-spire", "心灵尖塔",
                DevelopmentRoute.Psionics, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.ResonanceMetal, 14),
                    Cost(ResourceIds.PsionicAmplifier, 4)),
                40f, "解锁心灵尖塔与灵能共鸣", Req("core.research.mind-resonance"),
                "building:psionics.building.mind-spire"),
            Node(19, "core.research.consciousness-network", "意识网络",
                DevelopmentRoute.Psionics, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.ResonanceMetal, 16), Cost(ResourceIds.Water, 10)),
                40f, "解锁意识碎片生产", Req("core.research.mind-resonance"),
                "building:psionics.building.consciousness-network",
                "recipe:psionics.production.consciousness-shard"),
            Node(20, ThoughtAccelerationId, "思维加速",
                DevelopmentRoute.Psionics, 2, 2,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.PsionicAmplifier, 8), Cost(ResourceIds.Water, 14)),
                40f, "研究速度提高25%", Req("core.research.mind-resonance"),
                "rule:psionics.effect.research-speed-125-percent"),

            Node(21, "core.research.alloy-armor", "合金装甲",
                DevelopmentRoute.Technology, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Alloy, 24), Cost(ResourceIds.Stone, 8)),
                60f, "解锁重型机枪塔，建筑最大耐久提高30%",
                Req("core.research.precision-assembly"),
                "rule:core.effect.alloy-armor",
                "building:core.building.heavy-machine-gun-turret"),
            Node(22, "core.research.unmanned-systems", "无人系统",
                DevelopmentRoute.Technology, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Alloy, 24), Cost(ResourceIds.EnergyCrystal, 10)),
                60f, "解锁自动维修机甲站与维修脉冲",
                Req("core.research.automated-defense"),
                "building:core.building.automated-repair-bay",
                "rule:core.effect.scout-drone"),
            Node(23, "core.research.orbital-supply", "轨道补给",
                DevelopmentRoute.Technology, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Alloy, 30), Cost(ResourceIds.Ammunition, 15)),
                75f, "物流范围扩大至24格", Req("core.research.thermal-engineering"),
                "rule:core.effect.logistics-range-24"),
            Node(24, "core.research.energy-weapons", "能量武器",
                DevelopmentRoute.Technology, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Alloy, 20), Cost(ResourceIds.EnergyCrystal, 20)),
                75f, "解锁激光塔与主动能量过载", Req("core.research.ballistics"),
                "building:core.building.laser-tower",
                "rule:core.effect.technology-overload"),

            Node(25, "core.research.sword-riding", "御剑术",
                DevelopmentRoute.Cultivation, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.SpiritIron, 24), Cost(ResourceIds.FlyingSword, 8)),
                60f, "解锁御剑台，飞剑射程提高30%", Req("core.research.sword-array"),
                "building:cultivation.building.sword-riding-platform",
                "rule:core.effect.flying-sword-range"),
            Node(26, "core.research.alchemy", "炼丹术",
                DevelopmentRoute.Cultivation, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.SpiritIron, 16), Cost(ResourceIds.Biomass, 20)),
                60f, "解锁丹药生产", Req("core.research.artifact-crafting"),
                "building:cultivation.building.alchemy-chamber",
                "recipe:cultivation.production.elixir"),
            Node(27, "core.research.formation-reinforcement", "阵法强化",
                DevelopmentRoute.Cultivation, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.EnergyCrystal, 24), Cost(ResourceIds.Stone, 16)),
                60f, "强化物流范围与聚灵产量", Req("core.research.spirit-gathering"),
                "rule:cultivation.effect.logistics-range-12",
                "rule:cultivation.effect.spirit-output-150-percent"),
            Node(28, "core.research.puppetry", "傀儡术",
                DevelopmentRoute.Cultivation, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.SpiritIron, 20), Cost(ResourceIds.Alloy, 12)),
                60f, "解锁傀儡工坊、容量4与维护规则",
                Req("core.research.talisman-basics"),
                "building:cultivation.building.puppet-workshop",
                "rule:cultivation.effect.puppet-unit"),

            Node(29, "core.research.behemoth-breeding", "巨兽培育",
                DevelopmentRoute.Biological, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BoneSteel, 24),
                    Cost(ResourceIds.BiomassConcentrate, 18)),
                60f, "解锁巨兽栏，培育巨兽最大生命提高10%",
                Req("core.research.bio-cultivation"),
                "building:biological.building.behemoth-pen",
                "rule:biological.effect.behemoth-unit"),
            Node(30, "core.research.acid-spit", "酸液喷吐",
                DevelopmentRoute.Biological, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BiomassConcentrate, 20),
                    Cost(ResourceIds.BiologicalWeapon, 10)),
                60f, "解锁酸液塔，对重甲伤害提高30%",
                Req("core.research.spore-dispersal"),
                "building:biological.building.acid-tower",
                "rule:biological.effect.armor-corrosion"),
            Node(31, "core.research.tissue-regeneration", "组织再生",
                DevelopmentRoute.Biological, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BiomassConcentrate, 24),
                    Cost(ResourceIds.Biomass, 20)),
                60f, "解锁建筑与单位再生",
                Req("core.research.metabolic-acceleration"),
                "rule:biological.effect.building-and-unit-regeneration"),
            Node(32, "core.research.gene-splicing", "基因剪接",
                DevelopmentRoute.Biological, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BoneSteel, 18),
                    Cost(ResourceIds.BiomassConcentrate, 18)),
                60f, "当前领袖获得300秒基因强化特质",
                Req("core.research.carapace-growth"),
                "rule:biological.effect.leader-temporary-trait"),

            Node(33, "core.research.mind-shield", "心灵护盾",
                DevelopmentRoute.Psionics, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.ResonanceMetal, 24),
                    Cost(ResourceIds.PsionicAmplifier, 12)),
                60f, "解锁护盾发生器与城市护盾补充",
                Req("core.research.psionic-workshop"),
                "building:psionics.building.shield-generator",
                "rule:psionics.effect.city-damage-shield"),
            Node(34, "core.research.mind-control", "精神操控",
                DevelopmentRoute.Psionics, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.PsionicAmplifier, 20), Cost(ResourceIds.Water, 20)),
                60f, "普通非重型目标有10%概率被控制", Req("core.research.mind-spire"),
                "rule:psionics.effect.control-normal-enemy"),
            Node(35, "core.research.precognitive-sense", "预知感应",
                DevelopmentRoute.Psionics, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.PsionicAmplifier, 18),
                    Cost(ResourceIds.EnergyCrystal, 20)),
                60f, "预警时间提高50%", Req("core.research.consciousness-network"),
                "rule:psionics.effect.warning-time-150-percent"),
            Node(36, CollectiveConsciousnessId, "集体意识",
                DevelopmentRoute.Psionics, 3, 3,
                ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.PsionicAmplifier, 24), Cost(ResourceIds.Water, 24)),
                60f, "解锁灵能结晶，新研究继承20%进度",
                Req("core.research.thought-acceleration"),
                "recipe:psionics.production.psionic-crystal",
                "rule:psionics.effect.multi-city-shared-progress-20-percent"),

            Node(37, "core.research.bridge.psionic-mech", "灵能机甲",
                DevelopmentRoute.Bridge, 3, 4, ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.Alloy, 30),
                    Cost(ResourceIds.PsionicAmplifier, 20)),
                90f, "解锁灵能机甲厂前驱产线，单位部署待 F3",
                Req("core.research.precision-assembly", "core.research.psionic-workshop"),
                "building:bridge.building.psionic-mech-factory",
                "recipe:fusion.production.psionic-mech-components"),
            Node(38, "core.research.bridge.high-frequency-sword", "高周波飞剑",
                DevelopmentRoute.Bridge, 3, 4, ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.FlyingSword, 12), Cost(ResourceIds.Alloy, 30)),
                90f, "解锁高周波飞剑铸造台",
                Req("core.research.artifact-crafting", "core.research.precision-assembly"),
                "building:bridge.building.high-frequency-sword-forge",
                "recipe:fusion.production.high-frequency-flying-sword"),
            Node(39, "core.research.bridge.bio-hangar", "生物机库",
                DevelopmentRoute.Bridge, 3, 4, ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BoneSteel, 25), Cost(ResourceIds.Alloy, 25)),
                90f, "解锁生物机库前驱产线，单位部署待 F3",
                Req("core.research.bio-cultivation", "core.research.precision-assembly"),
                "building:bridge.building.bio-hangar",
                "recipe:fusion.production.bio-hangar-weapons"),
            Node(40, "core.research.bridge.spirit-plant", "灵植培育",
                DevelopmentRoute.Bridge, 3, 4, ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.SpiritIron, 20),
                    Cost(ResourceIds.BiomassConcentrate, 20)),
                90f, "解锁灵植园与灵植精华生产",
                Req("core.research.artifact-crafting", "core.research.bio-cultivation"),
                "building:bridge.building.spirit-plant-garden",
                "recipe:fusion.production.spirit-plant-extract"),
            Node(41, "core.research.bridge.psionic-pulse", "精神脉冲武器",
                DevelopmentRoute.Bridge, 3, 4, ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.PsionicAmplifier, 20),
                    Cost(ResourceIds.Ammunition, 30)),
                90f, "解锁 EMP 塔与机械目标当帧抑制",
                Req("core.research.psionic-workshop", "core.research.precision-assembly"),
                "building:bridge.building.emp-tower"),
            Node(42, "core.research.bridge.flesh-elixir", "血肉灵丹",
                DevelopmentRoute.Bridge, 3, 4, ResearchReleaseState.Researchable,
                Costs(Cost(ResourceIds.BiomassConcentrate, 25),
                    Cost(ResourceIds.EnergyCrystal, 25)),
                90f, "开放血肉灵丹三倍治疗与即时突变风险",
                Req("core.research.bio-cultivation", "core.research.artifact-crafting"),
                "recipe:fusion.production.flesh-elixir",
                "rule:bridge.effect.elixir-triple-with-mutation-risk"),

            Node(43, LegacyAnalysisId, "遗产解析",
                DevelopmentRoute.Technology, 3, 4,
                ResearchReleaseState.Researchable,
                Costs(
                    Cost(ResourceIds.Alloy, 30),
                    Cost(ResourceIds.Biomass, 20)),
                60f, "满足首循环文明升阶条件",
                Req(AutomatedDefenseId),
                "progression:core.progression.legacy-analysis")
        };

        public static ResearchDefinition[] Starting => All;

        public static ResearchDefinition Find(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            ResearchDefinition formal = All.FirstOrDefault(value =>
                string.Equals(value.Id.Value, id, StringComparison.Ordinal));
            if (formal != null) return formal;
            return null;
        }

        private static ResearchDefinition Node(
            int catalogOrder,
            string id,
            string name,
            DevelopmentRoute route,
            int tier,
            int layoutRow,
            ResearchReleaseState releaseState,
            IReadOnlyList<ResourceAmount> costs,
            float duration,
            string effectSummary,
            IReadOnlyList<string> requirements,
            params string[] effectReferences)
        {
            return new ResearchDefinition(
                id,
                name,
                route,
                costs,
                duration,
                requirements,
                tier,
                effectSummary,
                catalogOrder,
                layoutRow,
                releaseState,
                effectReferences);
        }

        private static ResourceAmount Cost(string resourceId, int amount) =>
            new ResourceAmount(resourceId, amount);

        private static ResourceAmount[] Costs(params ResourceAmount[] costs) =>
            costs ?? Array.Empty<ResourceAmount>();

        private static string[] Req(params string[] ids) =>
            ids ?? Array.Empty<string>();

        private static string[] Effects(params string[] references) =>
            references ?? Array.Empty<string>();
    }

    public sealed class ResearchPersistenceSnapshot
    {
        internal ResearchPersistenceSnapshot(
            string[] completedResearchIds,
            string activeResearchId,
            float remainingSeconds)
        {
            CompletedResearchIds = Array.AsReadOnly(
                completedResearchIds ?? Array.Empty<string>());
            ActiveResearchId = activeResearchId;
            RemainingSeconds = remainingSeconds;
        }

        public IReadOnlyList<string> CompletedResearchIds { get; }
        public string ActiveResearchId { get; }
        public float RemainingSeconds { get; }
    }

    public sealed class ResearchRestorePlan
    {
        internal ResearchRestorePlan(
            ResearchModel owner,
            ulong preparedRevision,
            StableId[] completed,
            string[] orphanCompletedIds,
            ResearchDefinition active,
            string missingActiveResearchId,
            float remainingSeconds)
        {
            Owner = owner;
            PreparedRevision = preparedRevision;
            Completed = completed;
            OrphanCompletedIds = orphanCompletedIds;
            Active = active;
            MissingActiveResearchId = missingActiveResearchId;
            RemainingSeconds = remainingSeconds;
        }

        internal ResearchModel Owner { get; }
        internal ulong PreparedRevision { get; }
        internal StableId[] Completed { get; }
        internal string[] OrphanCompletedIds { get; }
        internal ResearchDefinition Active { get; }
        internal string MissingActiveResearchId { get; }
        internal float RemainingSeconds { get; }
        internal bool Committed { get; set; }
    }

    public sealed class ResearchModel
    {
        private readonly HashSet<StableId> completed = new HashSet<StableId>();
        private readonly SortedSet<string> orphanCompletedIds =
            new SortedSet<string>(StringComparer.Ordinal);
        private string missingActiveResearchId;
        private ulong persistenceRevision;
        public ResearchDefinition Active { get; private set; }
        public float Remaining { get; private set; }
        public int CompletedCount => completed.Count;
        public bool HasMissingActiveResearch =>
            !string.IsNullOrEmpty(missingActiveResearchId);
        public string MissingActiveResearchId => missingActiveResearchId;
        public event Action<ResearchDefinition> Completed;
        public bool Start(
            ResearchDefinition definition,
            ResourceInventory inventory)
        {
            return Start(definition, inventory, 0f);
        }

        public bool Start(
            ResearchDefinition definition,
            ResourceInventory inventory,
            float initialProgressFraction)
        {
            if (Active != null || HasMissingActiveResearch ||
                definition == null || inventory == null ||
                definition.ReleaseState != ResearchReleaseState.Researchable ||
                completed.Contains(definition.Id) ||
                definition.RequiredResearchIds.Any(required =>
                    !completed.Any(id => id.Value == required)) ||
                !TrySpendResearchCosts(inventory, definition.Costs))
                return false;
            float ratio = Math.Max(0f, Math.Min(1f, initialProgressFraction));
            Active = definition;
            Remaining = Math.Max(.001f, definition.Duration * (1f - ratio));
            persistenceRevision++;
            return true;
        }
        public bool Start(
            ResearchDefinition definition,
            CityResourceStorageModel cityStorage)
        {
            return Start(definition, cityStorage, 0f);
        }

        public bool Start(
            ResearchDefinition definition,
            CityResourceStorageModel cityStorage,
            float initialProgressFraction)
        {
            if (Active != null || HasMissingActiveResearch ||
                definition == null || cityStorage == null ||
                definition.ReleaseState != ResearchReleaseState.Researchable ||
                completed.Contains(definition.Id) ||
                definition.RequiredResearchIds.Any(required =>
                    !completed.Any(id => id.Value == required)) ||
                !cityStorage.TryCommitBatch(
                    definition.Costs,
                    Array.Empty<ResourceAmount>()))
            {
                return false;
            }
            float ratio = Math.Max(0f, Math.Min(1f, initialProgressFraction));
            Active = definition;
            Remaining = Math.Max(.001f, definition.Duration * (1f - ratio));
            persistenceRevision++;
            return true;
        }
        public bool Tick(float delta)
        {
            if (Active == null) return false; Remaining -= Math.Max(0f, delta); if (Remaining > 0.0001f) return false;
            ResearchDefinition finished = Active; completed.Add(finished.Id); Active = null; Remaining = 0f; persistenceRevision++; Completed?.Invoke(finished); return true;
        }
        public bool IsCompleted(StableId id) => completed.Contains(id);
        public string[] CaptureCompleted()=>completed.Select(id=>id.Value)
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public ResearchPersistenceSnapshot CaptureForPersistence()
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (StableId id in completed) ids.Add(id.Value);
            foreach (string id in orphanCompletedIds) ids.Add(id);
            return new ResearchPersistenceSnapshot(
                ids.ToArray(),
                Active?.Id.Value ?? missingActiveResearchId,
                Active != null || HasMissingActiveResearch ? Remaining : 0f);
        }

        public bool TryPrepareRestoreForPersistence(
            IReadOnlyList<string> completedResearchIds,
            string activeResearchId,
            float remainingSeconds,
            Func<string, ResearchDefinition> knownDefinitionResolver,
            out ResearchRestorePlan plan,
            out string error)
        {
            plan = null;
            if (completedResearchIds == null ||
                knownDefinitionResolver == null)
            {
                error = "科技恢复数据或目录解析器不能为空";
                return false;
            }
            if (float.IsNaN(remainingSeconds) ||
                float.IsInfinity(remainingSeconds) ||
                remainingSeconds < 0f)
            {
                error = "活动科技剩余时间无效";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var known = new List<StableId>();
            var unknown = new List<string>();
            for (var index = 0; index < completedResearchIds.Count; index++)
            {
                string id = completedResearchIds[index];
                if (!TryCreateStableId(id, out StableId stableId) ||
                    !seen.Add(id))
                {
                    error = "已完成科技 ID 无效或重复";
                    return false;
                }
                ResearchDefinition definition;
                try
                {
                    definition = knownDefinitionResolver(id);
                }
                catch
                {
                    error = "科技目录解析失败";
                    return false;
                }
                if (definition != null)
                {
                    if (!string.Equals(
                            definition.Id.Value,
                            id,
                            StringComparison.Ordinal))
                    {
                        error = "科技目录返回了不匹配的定义";
                        return false;
                    }
                    if (definition.ReleaseState ==
                        ResearchReleaseState.RetiredCompatibility)
                    {
                        unknown.Add(id);
                    }
                    else
                    {
                        known.Add(stableId);
                    }
                }
                else
                {
                    unknown.Add(id);
                }
            }

            ResearchDefinition active = null;
            string missingActive = null;
            if (string.IsNullOrEmpty(activeResearchId))
            {
                if (remainingSeconds != 0f)
                {
                    error = "没有活动科技时剩余时间必须为零";
                    return false;
                }
            }
            else
            {
                if (!TryCreateStableId(activeResearchId, out _) ||
                    seen.Contains(activeResearchId))
                {
                    error = "活动科技 ID 无效或已经完成";
                    return false;
                }
                try
                {
                    active = knownDefinitionResolver(activeResearchId);
                }
                catch
                {
                    error = "科技目录解析失败";
                    return false;
                }
                if (active != null && !string.Equals(
                        active.Id.Value,
                        activeResearchId,
                        StringComparison.Ordinal))
                {
                    error = "科技目录返回了不匹配的活动定义";
                    return false;
                }
                if (active == null ||
                    active.ReleaseState != ResearchReleaseState.Researchable)
                {
                    active = null;
                    missingActive = activeResearchId;
                }
            }

            known.Sort((left, right) => string.CompareOrdinal(
                left.Value,
                right.Value));
            unknown.Sort(StringComparer.Ordinal);
            plan = new ResearchRestorePlan(
                this,
                persistenceRevision,
                known.ToArray(),
                unknown.ToArray(),
                active,
                missingActive,
                remainingSeconds);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestoreForPersistence(
            ResearchRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "科技恢复计划不属于当前模型";
                return false;
            }
            if (plan.Committed)
            {
                error = "科技恢复计划已经提交";
                return false;
            }
            if (plan.PreparedRevision != persistenceRevision)
            {
                error = "科技状态已变化，请重新准备恢复计划";
                return false;
            }

            completed.Clear();
            for (var index = 0; index < plan.Completed.Length; index++)
                completed.Add(plan.Completed[index]);
            orphanCompletedIds.Clear();
            for (var index = 0;
                 index < plan.OrphanCompletedIds.Length;
                 index++)
            {
                orphanCompletedIds.Add(plan.OrphanCompletedIds[index]);
            }
            Active = plan.Active;
            missingActiveResearchId = plan.MissingActiveResearchId;
            Remaining = plan.RemainingSeconds;
            plan.Committed = true;
            persistenceRevision++;
            error = string.Empty;
            return true;
        }

        internal void GrantCompleted(ResearchDefinition definition)
        {
            if (definition != null && completed.Add(definition.Id))
            {
                orphanCompletedIds.Remove(definition.Id.Value);
                persistenceRevision++;
            }
        }

        public void GrantCompletedForDevelopment(
            ResearchDefinition definition)
        {
            bool wasCompleted = definition != null &&
                completed.Contains(definition.Id);
            GrantCompleted(definition);
            if (!wasCompleted && definition != null &&
                completed.Contains(definition.Id))
            {
                Completed?.Invoke(definition);
            }
        }
        internal bool TryCancel(
            ResourceInventory inventory,
            ResourceCapacityPolicy capacity,
            int activeWarehouseCount,
            float refundRatio)
        {
            if (Active == null || inventory == null || capacity == null)
                return false;
            ResourceAmount[] refund = Active.Costs
                .Select(value => new ResourceAmount(
                    value.ResourceId,
                    (int)Math.Floor(
                        value.Amount * Math.Max(0f, refundRatio))))
                .Where(value => value.Amount > 0)
                .ToArray();
            if (refund.Length > 0 &&
                !ResourceTransaction.TryCommitBatch(
                    inventory,
                    Array.Empty<ResourceAmount>(),
                    inventory,
                    capacity,
                    activeWarehouseCount,
                    refund))
            {
                return false;
            }
            Active = null;
            Remaining = 0f;
            persistenceRevision++;
            return true;
        }

        internal bool TryCancel(
            CityResourceStorageModel cityStorage,
            float refundRatio)
        {
            if (Active == null || cityStorage == null) return false;
            ResourceAmount[] refund = Active.Costs
                .Select(value => new ResourceAmount(
                    value.ResourceId,
                    (int)Math.Floor(
                        value.Amount * Math.Max(0f, refundRatio))))
                .Where(value => value.Amount > 0)
                .ToArray();
            if (refund.Length > 0 && !cityStorage.TryCommitBatch(
                    Array.Empty<ResourceAmount>(),
                    refund))
            {
                return false;
            }
            Active = null;
            Remaining = 0f;
            persistenceRevision++;
            return true;
        }

        private static bool TrySpendResearchCosts(
            ResourceInventory inventory,
            IReadOnlyList<ResourceAmount> costs)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (costs != null)
            {
                for (int index = 0; index < costs.Count; index++)
                {
                    ResourceAmount cost = costs[index];
                    if (!ResourceCapacityPolicy.IsRegisteredResource(
                            cost.ResourceId) ||
                        cost.Amount <= 0)
                    {
                        return false;
                    }

                    totals.TryGetValue(cost.ResourceId, out int existing);
                    long aggregate = (long)existing + cost.Amount;
                    if (aggregate > int.MaxValue) return false;
                    totals[cost.ResourceId] = (int)aggregate;
                }
            }

            foreach (KeyValuePair<string, int> cost in totals)
                if (!inventory.CanSpend(cost.Key, cost.Value))
                    return false;

            var before = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string resourceId in totals.Keys)
                before[resourceId] = inventory.Get(resourceId);

            try
            {
                foreach (KeyValuePair<string, int> cost in totals)
                {
                    if (!inventory.TrySpend(cost.Key, cost.Value))
                    {
                        RestoreResearchCosts(inventory, before);
                        return false;
                    }
                }
                return true;
            }
            catch
            {
                RestoreResearchCosts(inventory, before);
                return false;
            }
        }

        private static void RestoreResearchCosts(
            ResourceInventory inventory,
            Dictionary<string, int> before)
        {
            foreach (KeyValuePair<string, int> value in before)
                inventory.Restore(value.Key, value.Value);
        }

        public void Restore(string[] completedIds,string activeId,float remaining)
        {
            completed.Clear();
            orphanCompletedIds.Clear();
            if(completedIds!=null)foreach(string id in completedIds)
            {
                var definition=ResearchCatalog.Find(id);
                if(definition!=null&&definition.ReleaseState!=ResearchReleaseState.RetiredCompatibility)
                    completed.Add(definition.Id);
                else if(!string.IsNullOrWhiteSpace(id))
                    orphanCompletedIds.Add(id);
            }
            missingActiveResearchId=null;
            ResearchDefinition restoredActive=ResearchCatalog.Find(activeId);
            if(restoredActive!=null&&restoredActive.ReleaseState==ResearchReleaseState.Researchable)
            {
                Active=restoredActive;
                Remaining=Math.Max(0.001f,Math.Min(Active.Duration,remaining));
            }
            else
            {
                Active=null;
                missingActiveResearchId=string.IsNullOrWhiteSpace(activeId)?null:activeId;
                Remaining=missingActiveResearchId==null?0f:Math.Max(0.001f,remaining);
            }
            persistenceRevision++;
        }

        private static bool TryCreateStableId(
            string value,
            out StableId stableId)
        {
            try
            {
                stableId = new StableId(value);
                return true;
            }
            catch (ArgumentException)
            {
                stableId = default;
                return false;
            }
        }
    }
}
