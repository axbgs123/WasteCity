using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Economy
{
    public enum ResourceRecipeKind
    {
        Machine,
        ManualCrafting,
    }

    public sealed class ResourceRecipeDefinition
    {
        internal ResourceRecipeDefinition(
            string id,
            string chineseName,
            ResourceRecipeKind kind,
            IReadOnlyList<string> allowedBuildingIds,
            IReadOnlyList<ResourceAmount> inputs,
            IReadOnlyList<ResourceAmount> outputs,
            float durationSeconds,
            IReadOnlyList<string> requiredResearchIds,
            bool defaultForBuilding,
            string iconProjection,
            string loreBrief,
            int inputCapacity,
            int outputCapacity,
            bool usesBoundResourceNode = false,
            int boundResourceNodeOutputAmount = 0)
        {
            Id = id;
            ChineseName = chineseName;
            Kind = kind;
            AllowedBuildingIds = Snapshot(allowedBuildingIds);
            Inputs = Snapshot(inputs);
            Outputs = Snapshot(outputs);
            DurationSeconds = Math.Max(0f, durationSeconds);
            RequiredResearchIds = Snapshot(requiredResearchIds);
            DefaultForBuilding = defaultForBuilding;
            IconProjection = iconProjection;
            LoreBrief = loreBrief;
            InputCapacity = Math.Max(0, inputCapacity);
            OutputCapacity = Math.Max(0, outputCapacity);
            UsesBoundResourceNode = usesBoundResourceNode;
            BoundResourceNodeOutputAmount = usesBoundResourceNode
                ? Math.Max(0, boundResourceNodeOutputAmount)
                : 0;
        }

        public string Id { get; }
        public string ChineseName { get; }
        public ResourceRecipeKind Kind { get; }
        public ReadOnlyCollection<string> AllowedBuildingIds { get; }
        public ReadOnlyCollection<ResourceAmount> Inputs { get; }
        public ReadOnlyCollection<ResourceAmount> Outputs { get; }
        public float DurationSeconds { get; }
        public ReadOnlyCollection<string> RequiredResearchIds { get; }
        public string RequiredResearchId => RequiredResearchIds.Count == 0
            ? null
            : RequiredResearchIds[0];
        public bool DefaultForBuilding { get; }
        public string IconProjection { get; }
        public string LoreBrief { get; }
        public int InputCapacity { get; }
        public int OutputCapacity { get; }
        public bool UsesBoundResourceNode { get; }
        public int BoundResourceNodeOutputAmount { get; }

        private static ReadOnlyCollection<T> Snapshot<T>(
            IReadOnlyList<T> values)
        {
            var snapshot = new List<T>();
            if (values != null)
            {
                for (int index = 0; index < values.Count; index++)
                    snapshot.Add(values[index]);
            }

            return new ReadOnlyCollection<T>(snapshot);
        }
    }

    public static class ResourceRecipeCatalog
    {
        private const int StandardMachineInputCapacity = 20;
        private const int StandardMachineOutputCapacity = 20;

        public const string FieldAlloyId = "core.crafting.field-alloy";
        public const string FieldAmmunitionId =
            "core.crafting.field-ammunition";

        private const string MiningStationId =
            "core.building.mining-station";
        private const string SmelterId = "core.building.smelter";
        private const string AssemblerId = "core.building.assembler";
        private const string SpiritFireFurnaceId =
            "cultivation.building.spirit-fire-furnace";
        private const string SpiritGatheringArrayId =
            "cultivation.building.spirit-gathering-array";
        private const string ArtifactWorkshopId =
            "cultivation.building.artifact-workshop";
        private const string AlchemyChamberId =
            "cultivation.building.alchemy-chamber";
        private const string ColonyPoolId =
            "biological.building.colony-pool";
        private const string BreedingChamberId =
            "biological.building.breeding-chamber";
        private const string ResonanceFurnaceId =
            "psionics.building.resonance-furnace";
        private const string ConsciousnessNetworkId =
            "psionics.building.consciousness-network";
        private const string PsionicWorkshopId =
            "psionics.building.workshop";

        private static readonly ReadOnlyCollection<ResourceRecipeDefinition>
            all = Array.AsReadOnly(new[]
            {
                new ResourceRecipeDefinition(
                    "core.production.extract-node-resource",
                    "节点资源采集",
                    ResourceRecipeKind.Machine,
                    R(MiningStationId),
                    Array.Empty<ResourceAmount>(),
                    Array.Empty<ResourceAmount>(),
                    3f,
                    R("core.research.scrap-processing"),
                    defaultForBuilding: true,
                    "bound-resource|badge:machine",
                    "采矿站从绑定矿点稳定提取对应的基础资源。",
                    inputCapacity: 0,
                    outputCapacity: 20,
                    usesBoundResourceNode: true,
                    boundResourceNodeOutputAmount: 1),
                new ResourceRecipeDefinition(
                    "core.production.smelt-alloy",
                    "合金冶炼",
                    ResourceRecipeKind.Machine,
                    R(SmelterId),
                    Amounts(("core.resource.iron", 2)),
                    Amounts(("technology.resource.alloy", 1)),
                    6f,
                    R("core.research.automated-machinery"),
                    defaultForBuilding: true,
                    MachineIcon("technology.resource.alloy"),
                    "以标准炉温把废铁矿冶炼为耐热合金。",
                    inputCapacity: 20,
                    outputCapacity: 10),
                new ResourceRecipeDefinition(
                    "core.production.assemble-ammunition",
                    "弹药装配",
                    ResourceRecipeKind.Machine,
                    R(AssemblerId),
                    Amounts(("technology.resource.alloy", 2)),
                    Amounts(("technology.resource.ammunition", 2)),
                    6f,
                    R("core.research.precision-assembly"),
                    defaultForBuilding: true,
                    MachineIcon("technology.resource.ammunition"),
                    "把合金封装成城市防御系统使用的统一弹箱。",
                    inputCapacity: 20,
                    outputCapacity: 30),
                Manual(
                    FieldAlloyId,
                    "应急合金",
                    Amounts(("core.resource.iron", 4)),
                    Amounts(("technology.resource.alloy", 1)),
                    12f,
                    R("core.research.automated-machinery"),
                    "在缺少机器时低效率熔炼少量合金。"),
                Manual(
                    FieldAmmunitionId,
                    "应急弹药",
                    Amounts(("technology.resource.alloy", 4)),
                    Amounts(("technology.resource.ammunition", 2)),
                    12f,
                    R("core.research.precision-assembly"),
                    "在缺少装配线时手工封装少量标准弹药。"),
                Machine(
                    "core.production.refine-stone",
                    "精整石材",
                    SmelterId,
                    Amounts(("core.resource.stone", 3)),
                    Amounts(("core.resource.refined-stone", 2)),
                    6f,
                    R("core.research.automated-machinery"),
                    defaultForBuilding: false,
                    "压实并切齐结构石，使其适合精密装配。"),
                Machine(
                    "core.production.mix-coolant",
                    "混合冷却液",
                    AssemblerId,
                    Amounts(
                        ("core.resource.water", 2),
                        ("core.resource.energy-crystal", 1)),
                    Amounts(("core.resource.coolant", 2)),
                    6f,
                    R("core.research.precision-assembly"),
                    defaultForBuilding: false,
                    "以能晶稳定工业水，制成高负载设备冷却介质。"),
                Machine(
                    "core.production.spin-carbon-fiber",
                    "纺制碳纤维",
                    AssemblerId,
                    Amounts(
                        ("core.resource.biomass", 3),
                        ("core.resource.water", 1)),
                    Amounts(("core.resource.carbon-fiber", 2)),
                    8f,
                    R("core.research.precision-assembly"),
                    defaultForBuilding: false,
                    "抽取生物质中的碳结构并纺成高强纤维。"),
                Machine(
                    "technology.production.energy-cell",
                    "封装能量电池",
                    AssemblerId,
                    Amounts(
                        ("core.resource.energy-crystal", 2),
                        ("technology.resource.alloy", 1)),
                    Amounts(("technology.resource.energy-cell", 1)),
                    8f,
                    R("core.research.thermal-engineering"),
                    defaultForBuilding: false,
                    "用合金外壳封装能晶，形成可更换工业储能单元。"),
                Machine(
                    "technology.production.mechanical-component",
                    "制造机械组件",
                    AssemblerId,
                    Amounts(
                        ("technology.resource.alloy", 2),
                        ("core.resource.refined-stone", 1),
                        ("core.resource.carbon-fiber", 1)),
                    Amounts(
                        ("technology.resource.mechanical-component", 2)),
                    8f,
                    R("core.research.precision-assembly"),
                    defaultForBuilding: false,
                    "将承力、传动与轻质材料组合成标准机械模块。"),
                Machine(
                    "technology.production.control-chip",
                    "制造控制芯片",
                    AssemblerId,
                    Amounts(
                        ("technology.resource.mechanical-component", 1),
                        ("technology.resource.energy-cell", 1)),
                    Amounts(("technology.resource.control-chip", 1)),
                    10f,
                    R("core.research.unmanned-systems"),
                    defaultForBuilding: false,
                    "把确定控制逻辑封装进耐用的废土机械芯片。"),
                Machine(
                    "technology.production.superconductive-coil",
                    "绕制超导线圈",
                    AssemblerId,
                    Amounts(
                        ("technology.resource.alloy", 2),
                        ("core.resource.energy-crystal", 2),
                        ("core.resource.coolant", 1)),
                    Amounts(
                        ("technology.resource.superconductive-coil", 1)),
                    10f,
                    R("core.research.energy-weapons"),
                    defaultForBuilding: false,
                    "在持续冷却下绕制可输送高能量的线圈。"),
                Machine(
                    "cultivation.production.refine-spirit-iron",
                    "淬炼灵铁",
                    SpiritFireFurnaceId,
                    Amounts(
                        ("core.resource.iron", 2),
                        ("core.resource.energy-crystal", 1)),
                    Amounts(("cultivation.resource.spirit-iron", 1)),
                    6f,
                    R("core.research.spirit-sensing"),
                    defaultForBuilding: true,
                    "以灵火和能晶重排铁矿纹理，淬成灵铁。"),
                Machine(
                    "cultivation.production.gather-spirit-stone",
                    "聚炼灵石",
                    SpiritGatheringArrayId,
                    Amounts(
                        ("core.resource.energy-crystal", 2),
                        ("core.resource.refined-stone", 1)),
                    Amounts(("cultivation.resource.spirit-stone", 1)),
                    8f,
                    R("core.research.spirit-gathering"),
                    defaultForBuilding: true,
                    "用阵势把能量压入精制石材，形成稳定灵石。"),
                Machine(
                    "cultivation.production.flying-sword",
                    "炼制飞剑",
                    ArtifactWorkshopId,
                    Amounts(
                        ("cultivation.resource.spirit-iron", 2),
                        ("technology.resource.alloy", 1)),
                    Amounts(("cultivation.resource.flying-sword", 1)),
                    8f,
                    R("core.research.artifact-crafting"),
                    defaultForBuilding: true,
                    "将灵铁与合金炼成可由神识驱动的标准剑器。"),
                Machine(
                    "cultivation.production.formation-core",
                    "制作阵法核心",
                    ArtifactWorkshopId,
                    Amounts(
                        ("cultivation.resource.spirit-stone", 2),
                        ("cultivation.resource.spirit-iron", 1)),
                    Amounts(("cultivation.resource.formation-core", 1)),
                    10f,
                    R("core.research.formation-reinforcement"),
                    defaultForBuilding: false,
                    "将灵石拓扑固化在灵铁框架中，制成可替换阵芯。"),
                Machine(
                    "cultivation.production.elixir",
                    "炼制灵丹",
                    AlchemyChamberId,
                    Amounts(
                        ("biological.resource.biomass-concentrate", 2),
                        ("cultivation.resource.spirit-stone", 1),
                        ("core.resource.water", 1)),
                    Amounts(("cultivation.resource.elixir", 1)),
                    10f,
                    R("core.research.alchemy"),
                    defaultForBuilding: true,
                    "以灵石约束浓缩液活性，炼成密封丹剂。"),
                Machine(
                    "biological.production.biomass-concentrate",
                    "浓缩生物质",
                    ColonyPoolId,
                    Amounts(
                        ("core.resource.biomass", 3),
                        ("core.resource.water", 1)),
                    Amounts(
                        ("biological.resource.biomass-concentrate", 2)),
                    6f,
                    R("core.research.adaptive-tissue"),
                    defaultForBuilding: true,
                    "去除惰性组织并浓缩养分，获得高活性培养液。"),
                Machine(
                    "biological.production.active-biomass",
                    "激活生物质",
                    ColonyPoolId,
                    Amounts(
                        ("biological.resource.biomass-concentrate", 2),
                        ("core.resource.energy-crystal", 1)),
                    Amounts(("biological.resource.active-biomass", 1)),
                    8f,
                    R("core.research.adaptive-tissue"),
                    defaultForBuilding: false,
                    "用能晶刺激浓缩组织，获得可稳定响应的培养体。"),
                Machine(
                    "biological.production.bone-steel",
                    "培育骨钢",
                    ColonyPoolId,
                    Amounts(
                        ("core.resource.iron", 2),
                        ("biological.resource.active-biomass", 1)),
                    Amounts(("biological.resource.bone-steel", 1)),
                    8f,
                    R("core.research.adaptive-tissue"),
                    defaultForBuilding: false,
                    "让活性组织沿铁芯生长，形成韧性骨质金属。"),
                Machine(
                    "biological.production.mutant-gene",
                    "筛选变异基因",
                    BreedingChamberId,
                    Amounts(
                        ("biological.resource.active-biomass", 2),
                        ("core.resource.water", 1)),
                    Amounts(("biological.resource.mutant-gene", 1)),
                    10f,
                    R("core.research.gene-splicing"),
                    defaultForBuilding: true,
                    "从活性培养体中筛选适应性稳定的遗传片段。"),
                Machine(
                    "biological.production.acid-gland",
                    "培育酸腺",
                    BreedingChamberId,
                    Amounts(
                        ("biological.resource.biomass-concentrate", 2),
                        ("biological.resource.mutant-gene", 1)),
                    Amounts(("biological.resource.acid-gland", 1)),
                    10f,
                    R("core.research.acid-spit"),
                    defaultForBuilding: false,
                    "引导腐蚀性组织生长为可替换的高压酸腺。"),
                Machine(
                    "biological.production.weapon",
                    "培育生物武器",
                    BreedingChamberId,
                    Amounts(
                        ("biological.resource.bone-steel", 2),
                        ("biological.resource.acid-gland", 1)),
                    Amounts(("biological.resource.weapon", 1)),
                    10f,
                    R("core.research.bio-cultivation"),
                    defaultForBuilding: false,
                    "用骨钢约束酸腺，培育受控攻击器官模块。"),
                Machine(
                    "psionics.production.resonance-metal",
                    "精炼共振金属",
                    ResonanceFurnaceId,
                    Amounts(
                        ("core.resource.iron", 2),
                        ("core.resource.energy-crystal", 1)),
                    Amounts(("psionics.resource.resonance-metal", 1)),
                    6f,
                    R("core.research.mind-resonance"),
                    defaultForBuilding: true,
                    "以稳定频率处理金属，使其能够记录精神波动。"),
                Machine(
                    "psionics.production.consciousness-shard",
                    "沉淀意识碎片",
                    ConsciousnessNetworkId,
                    Amounts(
                        ("core.resource.water", 2),
                        ("core.resource.energy-crystal", 1)),
                    Amounts(("psionics.resource.consciousness-shard", 1)),
                    8f,
                    R("core.research.consciousness-network"),
                    defaultForBuilding: true,
                    "从能量介质中捕获并沉淀残余认知信号。"),
                Machine(
                    "psionics.production.amplifier",
                    "制造灵能增幅器",
                    PsionicWorkshopId,
                    Amounts(
                        ("psionics.resource.resonance-metal", 2),
                        ("psionics.resource.consciousness-shard", 1)),
                    Amounts(("psionics.resource.amplifier", 1)),
                    10f,
                    R("core.research.psionic-workshop"),
                    defaultForBuilding: true,
                    "把共振金属与意识信号组合成受控放大器件。"),
                Machine(
                    "psionics.production.psionic-crystal",
                    "压缩灵能结晶",
                    ConsciousnessNetworkId,
                    Amounts(
                        ("psionics.resource.consciousness-shard", 2),
                        ("psionics.resource.amplifier", 1)),
                    Amounts(("psionics.resource.psionic-crystal", 1)),
                    10f,
                    R("core.research.collective-consciousness"),
                    defaultForBuilding: false,
                    "放大并压缩意识碎片，得到稳定的精神能量结晶。"),
                Machine(
                    "fusion.production.spirit-plant-extract",
                    "融合灵植精华",
                    BreedingChamberId,
                    Amounts(
                        ("cultivation.resource.spirit-stone", 1),
                        ("biological.resource.active-biomass", 2),
                        ("core.resource.water", 1)),
                    Amounts(("fusion.resource.spirit-plant-extract", 1)),
                    12f,
                    R("core.research.bridge.spirit-plant"),
                    defaultForBuilding: false,
                    "让灵性流动与活性组织共存，提取青绿灵植精华。"),
                Machine(
                    "fusion.production.flesh-elixir",
                    "血肉灵丹",
                    AlchemyChamberId,
                    Amounts(
                        ("fusion.resource.spirit-plant-extract", 1),
                        ("biological.resource.mutant-gene", 1)),
                    Amounts(("cultivation.resource.elixir", 3)),
                    12f,
                    R("core.research.bridge.flesh-elixir"),
                    defaultForBuilding: false,
                    "融合灵植精华与适应性基因，批量炼成强化灵丹。"),
                Machine(
                    "fusion.production.hybrid-core",
                    "制造融合核心",
                    AssemblerId,
                    Amounts(
                        ("technology.resource.control-chip", 1),
                        ("cultivation.resource.formation-core", 1),
                        ("biological.resource.mutant-gene", 1),
                        ("psionics.resource.psionic-crystal", 1)),
                    Amounts(("fusion.resource.hybrid-core", 1)),
                    18f,
                    R(
                        "core.research.unmanned-systems",
                        "core.research.formation-reinforcement",
                        "core.research.gene-splicing",
                        "core.research.collective-consciousness"),
                    defaultForBuilding: false,
                    "在同一接口壳体内平衡四条路线的核心规则。"),
            });

        private static readonly IReadOnlyDictionary<
            string,
            ResourceRecipeDefinition> byId = BuildLookup();

        public static ReadOnlyCollection<ResourceRecipeDefinition> All => all;

        public static bool TryGet(
            string recipeId,
            out ResourceRecipeDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(recipeId) &&
                byId.TryGetValue(recipeId, out definition);
        }

        public static string DisplayName(string recipeId)
        {
            return TryGet(recipeId, out ResourceRecipeDefinition definition)
                ? definition.ChineseName
                : recipeId ?? string.Empty;
        }

        private static ResourceRecipeDefinition Machine(
            string id,
            string chineseName,
            string buildingId,
            ResourceAmount[] inputs,
            ResourceAmount[] outputs,
            float durationSeconds,
            string[] requiredResearchIds,
            bool defaultForBuilding,
            string loreBrief)
        {
            return new ResourceRecipeDefinition(
                id,
                chineseName,
                ResourceRecipeKind.Machine,
                R(buildingId),
                inputs,
                outputs,
                durationSeconds,
                requiredResearchIds,
                defaultForBuilding,
                MachineIcon(outputs[0].ResourceId),
                loreBrief,
                StandardMachineInputCapacity,
                StandardMachineOutputCapacity);
        }

        private static ResourceRecipeDefinition Manual(
            string id,
            string chineseName,
            ResourceAmount[] inputs,
            ResourceAmount[] outputs,
            float durationSeconds,
            string[] requiredResearchIds,
            string loreBrief)
        {
            return new ResourceRecipeDefinition(
                id,
                chineseName,
                ResourceRecipeKind.ManualCrafting,
                Array.Empty<string>(),
                inputs,
                outputs,
                durationSeconds,
                requiredResearchIds,
                defaultForBuilding: false,
                ManualIcon(outputs[0].ResourceId),
                loreBrief,
                inputCapacity: 0,
                outputCapacity: 0);
        }

        private static ResourceAmount[] Amounts(
            params (string ResourceId, int Amount)[] values)
        {
            var amounts = new ResourceAmount[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                amounts[index] = new ResourceAmount(
                    values[index].ResourceId,
                    values[index].Amount);
            }

            return amounts;
        }

        private static string[] R(params string[] values)
        {
            return values;
        }

        private static string MachineIcon(string outputResourceId)
        {
            return "item:" + outputResourceId + "|badge:machine";
        }

        private static string ManualIcon(string outputResourceId)
        {
            return "item:" + outputResourceId + "|badge:manual";
        }

        private static IReadOnlyDictionary<string, ResourceRecipeDefinition>
            BuildLookup()
        {
            var lookup = new Dictionary<string, ResourceRecipeDefinition>(
                all.Count,
                StringComparer.Ordinal);
            foreach (ResourceRecipeDefinition definition in all)
                lookup.Add(definition.Id, definition);
            return new ReadOnlyDictionary<string, ResourceRecipeDefinition>(
                lookup);
        }
    }
}
