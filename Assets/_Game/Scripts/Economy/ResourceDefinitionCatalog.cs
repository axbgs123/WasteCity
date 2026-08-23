using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Economy
{
    public enum ResourceRoute
    {
        Common,
        Technology,
        Cultivation,
        Biological,
        Psionics,
        Fusion
    }

    public enum ResourceTier
    {
        Raw,
        Intermediate,
        Product
    }

    public enum ResourceSourceKind
    {
        WorldNode,
        ExistingWorldEntry,
        InitialInventory,
        MachineProduction,
        ManualCrafting,
        FusionProduction
    }

    public enum ResourceUseKind
    {
        ProductionInput,
        Construction,
        Research,
        CombatSupply,
        Consumable
    }

    public enum ResourceDiscoveryRule
    {
        Always,
        OwnedOrResearch,
        OwnedOrRecipe,
        OwnedOrAllRequirements
    }

    public sealed class ResourceDefinition
    {
        public string Id { get; }
        public string ChineseName { get; }
        public ResourceRoute Route { get; }
        public ResourceTier Tier { get; }
        public int StackLimit { get; }
        public string IconFallbackKey { get; }
        public int FormalInitialCityAmount { get; }
        public IReadOnlyList<ResourceSourceKind> SourceKinds { get; }
        public string SourceSummary { get; }
        public IReadOnlyList<ResourceUseKind> UseKinds { get; }
        public string UseSummary { get; }
        public ResourceDiscoveryRule DiscoveryRule { get; }
        public IReadOnlyList<string> RequiredResearchIds { get; }
        public string IconId { get; }
        public string LoreBrief { get; }
        public string VisualKeywords { get; }
        public string ForbiddenVisualElements { get; }
        public IReadOnlyList<int> DisplaySizesPx { get; }

        internal ResourceDefinition(
            string id,
            string chineseName,
            ResourceRoute route,
            ResourceTier tier,
            int stackLimit,
            string iconFallbackKey,
            int formalInitialCityAmount,
            ResourceSourceKind[] sourceKinds,
            string sourceSummary,
            ResourceUseKind[] useKinds,
            string useSummary,
            ResourceDiscoveryRule discoveryRule,
            string[] requiredResearchIds,
            string iconId,
            string loreBrief,
            string visualKeywords,
            string forbiddenVisualElements,
            int[] displaySizesPx)
        {
            Id = id;
            ChineseName = chineseName;
            Route = route;
            Tier = tier;
            StackLimit = stackLimit;
            IconFallbackKey = iconFallbackKey;
            FormalInitialCityAmount = formalInitialCityAmount;
            SourceKinds = Freeze(sourceKinds);
            SourceSummary = sourceSummary;
            UseKinds = Freeze(useKinds);
            UseSummary = useSummary;
            DiscoveryRule = discoveryRule;
            RequiredResearchIds = Freeze(requiredResearchIds);
            IconId = iconId;
            LoreBrief = loreBrief;
            VisualKeywords = visualKeywords;
            ForbiddenVisualElements = forbiddenVisualElements;
            DisplaySizesPx = Freeze(displaySizesPx);
        }

        private static IReadOnlyList<T> Freeze<T>(T[] values)
        {
            T[] snapshot = values == null
                ? Array.Empty<T>()
                : (T[])values.Clone();
            return Array.AsReadOnly(snapshot);
        }
    }

    public static class ResourceDefinitionCatalog
    {
        private const int DefaultStackLimit = 100;

        private static readonly ResourceSourceKind[] WorldNodeSource =
        {
            ResourceSourceKind.WorldNode
        };

        private static readonly ResourceSourceKind[] ExistingWorldSource =
        {
            ResourceSourceKind.ExistingWorldEntry
        };

        private static readonly ResourceSourceKind[] ExistingWorldAndInitialSource =
        {
            ResourceSourceKind.ExistingWorldEntry,
            ResourceSourceKind.InitialInventory
        };

        private static readonly ResourceSourceKind[] MachineSource =
        {
            ResourceSourceKind.MachineProduction
        };

        private static readonly ResourceSourceKind[] MachineAndManualSource =
        {
            ResourceSourceKind.MachineProduction,
            ResourceSourceKind.ManualCrafting
        };

        private static readonly ResourceSourceKind[] MachineAndFusionSource =
        {
            ResourceSourceKind.MachineProduction,
            ResourceSourceKind.FusionProduction
        };

        private static readonly ResourceUseKind[] ProductionConstructionResearchUse =
        {
            ResourceUseKind.ProductionInput,
            ResourceUseKind.Construction,
            ResourceUseKind.Research
        };

        private static readonly ResourceUseKind[] ProductionResearchUse =
        {
            ResourceUseKind.ProductionInput,
            ResourceUseKind.Research
        };

        private static readonly ResourceUseKind[] ResearchUse =
        {
            ResourceUseKind.Research
        };

        private static readonly ResourceUseKind[] CombatResearchUse =
        {
            ResourceUseKind.CombatSupply,
            ResourceUseKind.Research
        };

        private static readonly ResourceUseKind[] ConsumableProductionUse =
        {
            ResourceUseKind.Consumable,
            ResourceUseKind.ProductionInput
        };

        private static readonly string[] NoResearch = Array.Empty<string>();
        private static readonly int[] FormalDisplaySizesPx = { 20, 24, 32, 40, 64 };

        private static readonly ReadOnlyCollection<ResourceDefinition> all =
            Array.AsReadOnly(new[]
            {
                Define(
                    ResourceIds.Iron,
                    "铁矿",
                    ResourceRoute.Common,
                    ResourceTier.Raw,
                    WorldNodeSource,
                    "铁矿节点采矿。",
                    ProductionConstructionResearchUse,
                    "用于合金、灵铁、骨钢、共振金属、建筑和研究。",
                    ResourceDiscoveryRule.Always,
                    NoResearch,
                    "氧化废铁中仍可回收的金属矿。",
                    "橙褐矿块、深色断面",
                    "钢锭、文字、数字、完整背景板、资源数量、路线状态、细碎装饰",
                    formalInitialCityAmount: 20),
                Define(
                    ResourceIds.EnergyCrystal,
                    "能晶",
                    ResourceRoute.Common,
                    ResourceTier.Raw,
                    WorldNodeSource,
                    "能晶节点采矿。",
                    ProductionResearchUse,
                    "用于冷却液、能量电池、灵石、路线产品和研究。",
                    ResourceDiscoveryRule.Always,
                    NoResearch,
                    "储存不稳定能量的青色晶体。",
                    "锐利晶簇、青色内发光",
                    "宝石首饰、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.Stone,
                    "石料",
                    ResourceRoute.Common,
                    ResourceTier.Raw,
                    WorldNodeSource,
                    "石料节点采矿。",
                    ProductionConstructionResearchUse,
                    "用于精制石材、建筑、阵基和研究。",
                    ResourceDiscoveryRule.Always,
                    NoResearch,
                    "废墟地层中可重新利用的结构石。",
                    "灰色层理块、粗糙断面",
                    "水泥砖文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.Biomass,
                    "生物质",
                    ResourceRoute.Common,
                    ResourceTier.Raw,
                    ExistingWorldAndInitialSource,
                    "既有尸体回收入口和正式初始库存。",
                    ProductionResearchUse,
                    "用于碳纤维、营养加工、血肉路线和研究。",
                    ResourceDiscoveryRule.Always,
                    NoResearch,
                    "可回收有机组织形成的稳定混合物。",
                    "绿色纤维团、少量湿润高光",
                    "血腥肢体、文字、数字、完整背景板、资源数量、路线状态、细碎装饰",
                    formalInitialCityAmount: 10),
                Define(
                    ResourceIds.Water,
                    "水",
                    ResourceRoute.Common,
                    ResourceTier.Raw,
                    ExistingWorldSource,
                    "既有水资源入口和城市库存。",
                    ProductionResearchUse,
                    "用于冷却、营养、炼丹、意识处理和研究。",
                    ResourceDiscoveryRule.Always,
                    NoResearch,
                    "经移动城市回收净化的工业水。",
                    "密封水囊、蓝色液面",
                    "自然风景、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.Alloy,
                    "合金",
                    ResourceRoute.Technology,
                    ResourceTier.Intermediate,
                    MachineAndManualSource,
                    "冶炼厂量产，紧急时可由背包低效率合成。",
                    ProductionConstructionResearchUse,
                    "用于弹药、科技组件、建筑和研究。",
                    ResourceDiscoveryRule.OwnedOrResearch,
                    new[] { "core.research.automated-machinery" },
                    "废土工业采用的标准化耐热金属锭。",
                    "冷灰金属锭、橙色炉痕",
                    "黄金质感、文字、数字、完整背景板、资源数量、路线状态、细碎装饰",
                    formalInitialCityAmount: 20),
                Define(
                    ResourceIds.Ammunition,
                    "弹药",
                    ResourceRoute.Technology,
                    ResourceTier.Product,
                    MachineAndManualSource,
                    "装配厂量产，紧急时可由背包低效率合成。",
                    CombatResearchUse,
                    "用于机枪塔供弹和弹道研究。",
                    ResourceDiscoveryRule.OwnedOrResearch,
                    new[] { "core.research.precision-assembly" },
                    "封装为城市防御系统统一供弹规格的弹箱。",
                    "紧凑弹匣、工业弹箱、黄色识别条",
                    "散落实弹堆、文字、数字、完整背景板、资源数量、路线状态、细碎装饰",
                    formalInitialCityAmount: 30),
                Define(
                    ResourceIds.SpiritIron,
                    "灵铁",
                    ResourceRoute.Cultivation,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "灵火炉淬炼铁矿与能晶。",
                    ProductionConstructionResearchUse,
                    "用于飞剑、阵法核心、灵丹路线和研究。",
                    ResourceDiscoveryRule.OwnedOrResearch,
                    new[] { "core.research.spirit-sensing" },
                    "被灵火重新排列内部纹理的金属。",
                    "暗银锭、细青金纹",
                    "普通合金锭、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.FlyingSword,
                    "飞剑",
                    ResourceRoute.Cultivation,
                    ResourceTier.Product,
                    MachineSource,
                    "炼器坊加工灵铁与合金。",
                    ProductionResearchUse,
                    "用于剑阵、御剑研究与高周波桥节点。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.artifact-crafting" },
                    "适于神识驱动的标准化短剑器。",
                    "短剑、清楚悬浮轮廓、青色灵纹",
                    "人物持剑场景、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.BoneSteel,
                    "骨钢",
                    ResourceRoute.Biological,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "菌落池让活性生物质附着铁矿生长。",
                    ProductionConstructionResearchUse,
                    "用于生物武器、血肉建筑和研究。",
                    ResourceDiscoveryRule.OwnedOrResearch,
                    new[] { "core.research.adaptive-tissue" },
                    "兼具结构韧性和自愈倾向的骨质金属。",
                    "象牙灰骨架、暗色铁芯",
                    "人体骨骼、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.BiomassConcentrate,
                    "生物质浓缩液",
                    ResourceRoute.Biological,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "菌落池浓缩水和生物质。",
                    ProductionResearchUse,
                    "用于活性生物质、酸腺、炼丹和研究。",
                    ResourceDiscoveryRule.OwnedOrResearch,
                    new[] { "core.research.adaptive-tissue" },
                    "去除惰性组织后的高营养培养浆液。",
                    "绿色密封罐、可见沉降层",
                    "血液、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.BiologicalWeapon,
                    "生物武器",
                    ResourceRoute.Biological,
                    ResourceTier.Product,
                    MachineSource,
                    "培育室组合骨钢与酸腺。",
                    ProductionResearchUse,
                    "用于孢子与酸液防御研究，并作为融合消费品。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.bio-cultivation" },
                    "在城市约束下生长的攻击性器官模块。",
                    "骨壳喷口、绿色囊体",
                    "完整怪物、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.ResonanceMetal,
                    "共振金属",
                    ResourceRoute.Psionics,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "共振炉加工铁矿与能晶。",
                    ProductionConstructionResearchUse,
                    "用于灵能增幅器、灵能建筑和研究。",
                    ResourceDiscoveryRule.OwnedOrResearch,
                    new[] { "core.research.mind-resonance" },
                    "能够记录微弱精神频率的金属薄片。",
                    "银灰薄片、紫青波纹",
                    "普通合金锭、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.PsionicAmplifier,
                    "灵能增幅器",
                    ResourceRoute.Psionics,
                    ResourceTier.Product,
                    MachineSource,
                    "灵能工坊组合共振金属与意识碎片。",
                    ProductionConstructionResearchUse,
                    "用于灵能结晶、护盾建筑和灵能研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.psionic-workshop" },
                    "放大并约束精神信号的工业器件。",
                    "双环谐振器、紫色核心",
                    "扬声器、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.Elixir,
                    "灵丹",
                    ResourceRoute.Cultivation,
                    ResourceTier.Product,
                    MachineAndFusionSource,
                    "炼丹房常规提炼，完成血肉灵丹研究后可融合增产。",
                    ConsumableProductionUse,
                    "作为消耗品，并用于血肉灵丹桥节点。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.alchemy", "core.research.bridge.flesh-elixir" },
                    "将短时活性封存在标准容器中的丹剂。",
                    "密封丹丸、工业灵纹容器",
                    "古风药瓶背景、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.RefinedStone,
                    "精制石材",
                    ResourceRoute.Common,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "冶炼厂精整石料。",
                    ProductionConstructionResearchUse,
                    "用于机械组件、阵法核心、建筑和研究成本。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.automated-machinery" },
                    "压实并校准过尺寸的标准结构材料。",
                    "分层灰板、整齐切边",
                    "普通原石轮廓、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.Coolant,
                    "冷却液",
                    ResourceRoute.Common,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "装配厂混合水与能晶。",
                    ProductionResearchUse,
                    "用于超导线圈和高负载科技研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.precision-assembly" },
                    "能够稳定吸收设备废热的蓝色介质。",
                    "金属罐、青蓝液窗",
                    "饮料瓶、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.CarbonFiber,
                    "碳纤维",
                    ResourceRoute.Common,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "装配厂碳化生物质。",
                    ProductionResearchUse,
                    "用于机械组件和轻质防护研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.precision-assembly" },
                    "从生物质中抽取并定向编织的高强纤维。",
                    "黑色编织卷、青灰夹扣",
                    "布料花纹、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.EnergyCell,
                    "能量电池",
                    ResourceRoute.Technology,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "装配厂封装能晶与合金。",
                    ProductionConstructionResearchUse,
                    "用于控制芯片、科技路线建筑和研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.thermal-engineering" },
                    "适配废土机械接口的可更换工业储能单元。",
                    "厚壳电芯、青色工业端口",
                    "现代消费电池、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.MechanicalComponent,
                    "机械组件",
                    ResourceRoute.Technology,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "装配厂加工合金、精制石材与碳纤维。",
                    ProductionConstructionResearchUse,
                    "用于控制芯片、无人系统、融合核心和建筑。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.precision-assembly" },
                    "标准轴承、执行器和结构件组成的维修模块。",
                    "齿轮、执行器、框架强轮廓",
                    "工具箱、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.ControlChip,
                    "控制芯片",
                    ResourceRoute.Technology,
                    ResourceTier.Product,
                    MachineSource,
                    "装配厂加工机械组件与能量电池。",
                    ProductionResearchUse,
                    "用于无人系统、融合核心和高级研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.unmanned-systems" },
                    "为废土机械封装确定控制逻辑的坚固芯片。",
                    "厚陶瓷芯片、粗引脚、青色状态灯",
                    "现代手机芯片、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.SuperconductiveCoil,
                    "超导线圈",
                    ResourceRoute.Technology,
                    ResourceTier.Product,
                    MachineSource,
                    "装配厂以合金、能晶和冷却液绕制。",
                    ProductionResearchUse,
                    "用于能量武器、精神脉冲和高阶研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.energy-weapons" },
                    "能够在低温下稳定输送高能量的工业绕组。",
                    "铜灰线圈、青色冷凝",
                    "巨大特斯拉塔、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.SpiritStone,
                    "灵石",
                    ResourceRoute.Cultivation,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "聚灵阵压缩能晶与精制石材。",
                    ProductionResearchUse,
                    "用于阵法核心、炼丹和修仙研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.spirit-gathering" },
                    "能够稳定储存灵性流动的工业阵材。",
                    "石质晶核、环形纹路",
                    "能晶相同轮廓、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.FormationCore,
                    "阵法核心",
                    ResourceRoute.Cultivation,
                    ResourceTier.Product,
                    MachineSource,
                    "炼器坊组合灵石与灵铁。",
                    ProductionResearchUse,
                    "用于阵法强化、高周波桥接和融合核心。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.formation-reinforcement" },
                    "把复杂阵法拓扑固化为可更换模块的核心。",
                    "多边环、悬浮中心、青金灵纹",
                    "完整法阵背景、文字、数字、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.ActiveBiomass,
                    "活性生物质",
                    ResourceRoute.Biological,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "菌落池激活生物质浓缩液与能晶。",
                    ProductionResearchUse,
                    "用于骨钢、变异基因和灵植精华。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.adaptive-tissue" },
                    "能对工业刺激作出稳定反应的培养组织。",
                    "紧凑组织团、绿色纤维、脉冲青光",
                    "肢体、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.MutantGene,
                    "变异基因",
                    ResourceRoute.Biological,
                    ResourceTier.Product,
                    MachineSource,
                    "培育室筛选活性生物质与水。",
                    ProductionResearchUse,
                    "用于酸腺、基因研究和融合核心。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.gene-splicing" },
                    "经过筛选并封装的适应性遗传片段。",
                    "双螺旋胶囊、绿紫色带",
                    "文字标签、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.AcidGland,
                    "酸腺",
                    ResourceRoute.Biological,
                    ResourceTier.Product,
                    MachineSource,
                    "培育室培养生物质浓缩液与变异基因。",
                    ProductionResearchUse,
                    "用于生物武器和酸液科技。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.acid-spit" },
                    "可接入防御设备的高压腐蚀腺体。",
                    "骨质接口、黄绿色囊体",
                    "写实内脏、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.ConsciousnessShard,
                    "意识碎片",
                    ResourceRoute.Psionics,
                    ResourceTier.Intermediate,
                    MachineSource,
                    "意识网络从水和能晶中沉淀信号。",
                    ProductionResearchUse,
                    "用于灵能增幅器、灵能结晶和研究。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.consciousness-network" },
                    "被工业介质捕获并稳定下来的残余认知片段。",
                    "半透明碎片、断续紫青波纹",
                    "人脸、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.PsionicCrystal,
                    "灵能结晶",
                    ResourceRoute.Psionics,
                    ResourceTier.Product,
                    MachineSource,
                    "意识网络压缩意识碎片与增幅信号。",
                    ProductionResearchUse,
                    "用于心灵护盾、预知和融合核心。",
                    ResourceDiscoveryRule.OwnedOrRecipe,
                    new[] { "core.research.collective-consciousness" },
                    "由高密度精神能量形成的稳定工业结晶。",
                    "紫青核心、同心精神波",
                    "与能晶同色同轮廓、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.SpiritPlantExtract,
                    "灵植精华",
                    ResourceRoute.Fusion,
                    ResourceTier.Intermediate,
                    MachineAndFusionSource,
                    "培育室在灵植培育前置下融合灵石、活性生物质和水。",
                    ConsumableProductionUse,
                    "用于血肉灵丹和恢复类研究。",
                    ResourceDiscoveryRule.OwnedOrAllRequirements,
                    new[]
                    {
                        "core.research.artifact-crafting",
                        "core.research.bio-cultivation"
                    },
                    "同时保持灵性流动与生物活性的植物提取物。",
                    "青绿叶状液滴、紧凑阵纹",
                    "自然植物场景、文字、数字、完整背景板、资源数量、路线状态、细碎装饰"),
                Define(
                    ResourceIds.HybridCore,
                    "融合核心",
                    ResourceRoute.Fusion,
                    ResourceTier.Product,
                    MachineAndFusionSource,
                    "装配厂组合控制芯片、阵法核心、变异基因和灵能结晶。",
                    ResearchUse,
                    "用于四路线终局研究，并作为后续融合建筑输入。",
                    ResourceDiscoveryRule.OwnedOrAllRequirements,
                    new[]
                    {
                        "core.research.unmanned-systems",
                        "core.research.formation-reinforcement",
                        "core.research.gene-splicing",
                        "core.research.collective-consciousness"
                    },
                    "四套规则在同一工业壳体内维持平衡的接口核心。",
                    "四色受控分区、中心锁环、厚重工业壳",
                    "彩虹光团、文字、数字、完整背景板、资源数量、路线状态、细碎装饰")
            });

        private static readonly ReadOnlyCollection<string> baseHudResourceIds =
            Array.AsReadOnly(new[]
            {
                ResourceIds.Iron,
                ResourceIds.EnergyCrystal,
                ResourceIds.Stone,
                ResourceIds.Biomass,
                ResourceIds.Water
            });

        private static readonly IReadOnlyDictionary<string, ResourceDefinition> byId =
            BuildLookup();

        public static IReadOnlyList<ResourceDefinition> All => all;

        public static IReadOnlyList<string> BaseHudResourceIds => baseHudResourceIds;

        public static ResourceInventory CreateFormalCityInventory()
        {
            var inventory = new ResourceInventory(int.MaxValue);
            foreach (ResourceDefinition definition in all)
            {
                if (definition.FormalInitialCityAmount > 0)
                {
                    inventory.Add(
                        definition.Id,
                        definition.FormalInitialCityAmount);
                }
            }

            return inventory;
        }

        public static bool TryGet(string resourceId, out ResourceDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(resourceId) &&
                   byId.TryGetValue(resourceId, out definition);
        }

        private static ResourceDefinition Define(
            string id,
            string chineseName,
            ResourceRoute route,
            ResourceTier tier,
            ResourceSourceKind[] sourceKinds,
            string sourceSummary,
            ResourceUseKind[] useKinds,
            string useSummary,
            ResourceDiscoveryRule discoveryRule,
            string[] requiredResearchIds,
            string loreBrief,
            string visualKeywords,
            string forbiddenVisualElements,
            int formalInitialCityAmount = 0)
        {
            string iconId = "art.icon.item." +
                            id.Substring(id.LastIndexOf('.') + 1);
            return new ResourceDefinition(
                id,
                chineseName,
                route,
                tier,
                DefaultStackLimit,
                id,
                formalInitialCityAmount,
                sourceKinds,
                sourceSummary,
                useKinds,
                useSummary,
                discoveryRule,
                requiredResearchIds,
                iconId,
                loreBrief,
                visualKeywords,
                forbiddenVisualElements,
                FormalDisplaySizesPx);
        }

        private static IReadOnlyDictionary<string, ResourceDefinition> BuildLookup()
        {
            var definitions = new Dictionary<string, ResourceDefinition>(
                all.Count,
                StringComparer.Ordinal);

            foreach (ResourceDefinition definition in all)
            {
                definitions.Add(definition.Id, definition);
            }

            return new ReadOnlyDictionary<string, ResourceDefinition>(definitions);
        }
    }
}
