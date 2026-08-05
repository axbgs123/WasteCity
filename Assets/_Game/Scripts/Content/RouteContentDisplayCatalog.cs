using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Content
{
    public enum ContentRoute
    {
        Core,
        Technology,
        Cultivation,
        BiologicalAscension,
        Psionics
    }

    public static class RouteContentDisplayCatalog
    {
        private static readonly Dictionary<string, string> ResourceNames = new Dictionary<string, string>
        {
            { ResourceIds.Iron, "铁" },
            { ResourceIds.EnergyCrystal, "能晶" },
            { ResourceIds.Stone, "石料" },
            { ResourceIds.Biomass, "生物质" },
            { ResourceIds.Water, "水" },
            { ResourceIds.Alloy, "合金" },
            { ResourceIds.Ammunition, "弹药" },
            { ResourceIds.SpiritIron, "灵铁" },
            { ResourceIds.FlyingSword, "飞剑" },
            { ResourceIds.Elixir, "灵丹" },
            { ResourceIds.BoneSteel, "骨钢" },
            { ResourceIds.BiomassConcentrate, "生物质浓缩液" },
            { ResourceIds.BiologicalWeapon, "生物武器" },
            { ResourceIds.ResonanceMetal, "共振金属" },
            { ResourceIds.PsionicAmplifier, "灵能增幅器" }
        };

        public static string RouteName(ContentRoute route)
        {
            switch (route)
            {
                case ContentRoute.Technology: return "科技";
                case ContentRoute.Cultivation: return "修仙";
                case ContentRoute.BiologicalAscension: return "生物飞升";
                case ContentRoute.Psionics: return "灵能";
                default: return "基础";
            }
        }

        public static string RouteName(DevelopmentRoute route)
        {
            switch (route)
            {
                case DevelopmentRoute.Technology: return RouteName(ContentRoute.Technology);
                case DevelopmentRoute.Cultivation: return RouteName(ContentRoute.Cultivation);
                case DevelopmentRoute.BiologicalAscension: return RouteName(ContentRoute.BiologicalAscension);
                default: return RouteName(ContentRoute.Psionics);
            }
        }

        public static string ResourceName(string resourceId)
        {
            return resourceId != null && ResourceNames.TryGetValue(resourceId, out string value)
                ? value
                : "未知资源";
        }

        public static ContentRoute ResourceRoute(string resourceId)
        {
            if (resourceId == ResourceIds.Alloy || resourceId == ResourceIds.Ammunition)
                return ContentRoute.Technology;
            if (resourceId == ResourceIds.SpiritIron || resourceId == ResourceIds.FlyingSword || resourceId == ResourceIds.Elixir)
                return ContentRoute.Cultivation;
            if (resourceId == ResourceIds.BoneSteel || resourceId == ResourceIds.BiomassConcentrate || resourceId == ResourceIds.BiologicalWeapon)
                return ContentRoute.BiologicalAscension;
            if (resourceId == ResourceIds.ResonanceMetal || resourceId == ResourceIds.PsionicAmplifier)
                return ContentRoute.Psionics;
            return ContentRoute.Core;
        }

        public static ContentRoute BuildingRoute(BuildingDefinition definition)
        {
            string id = definition?.Id.Value ?? string.Empty;
            if (id.StartsWith("technology.", StringComparison.Ordinal)) return ContentRoute.Technology;
            if (id.StartsWith("cultivation.", StringComparison.Ordinal)) return ContentRoute.Cultivation;
            if (id.StartsWith("biological.", StringComparison.Ordinal)) return ContentRoute.BiologicalAscension;
            if (id.StartsWith("psionics.", StringComparison.Ordinal)) return ContentRoute.Psionics;
            return ContentRoute.Core;
        }

        public static string InventorySummary(ResourceInventory inventory)
        {
            if (inventory == null) return string.Empty;
            var text = new StringBuilder();
            ContentRoute[] routes =
            {
                ContentRoute.Core,
                ContentRoute.Technology,
                ContentRoute.Cultivation,
                ContentRoute.BiologicalAscension,
                ContentRoute.Psionics
            };
            foreach (ContentRoute route in routes)
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(RouteName(route)).Append("：");
                bool first = true;
                foreach (string resourceId in ResourceIds.All.Where(value => ResourceRoute(value) == route))
                {
                    if (!first) text.Append(" · ");
                    text.Append(ResourceName(resourceId)).Append(' ').Append(inventory.Get(resourceId));
                    first = false;
                }
            }
            return text.ToString();
        }

        public static string BuildingSummary(BuildingDefinition definition)
        {
            if (definition == null) return "无效建筑";
            return $"{definition.Name} [{RouteName(BuildingRoute(definition))}]\n" +
                   $"成本：{ResourceName(definition.CostId)} {definition.Cost} · 尺寸：{definition.Width}×{definition.Height} · 建造：{definition.BuildSeconds:0.#}秒 · 耐久：{definition.MaximumHealth}\n" +
                   $"功能：{BuildingFunction(definition)}\n" +
                   $"解锁：{BuildingUnlockSummary(definition)}";
        }

        public static string ResearchListLine(ResearchDefinition definition, bool completed, bool blocked)
        {
            if (definition == null) return "无效研究";
            string state = completed ? "✓ 已完成" : blocked ? "🔒 前置未完成" : "○ 可研究";
            return $"{state} · T{definition.Tier} {definition.Name} [{RouteName(definition.Route)}] · 成本 {ResourceName(definition.CostId)} {definition.Cost}";
        }

        public static string ResearchDetail(ResearchDefinition definition)
        {
            if (definition == null) return "无效研究";
            string prerequisites = definition.RequiredResearchIds.Count == 0
                ? "无"
                : string.Join("、", definition.RequiredResearchIds.Select(ResearchName));
            return $"{definition.Name} [{RouteName(definition.Route)} · T{definition.Tier}]\n" +
                   $"成本：{ResourceName(definition.CostId)} {definition.Cost} · 研究时间：{definition.Duration:0.#}秒\n" +
                   $"前置：{prerequisites}\n效果：{definition.EffectSummary}";
        }

        public static string FriendlyUnlockReason(
            BuildingDefinition definition,
            int population,
            Func<string, bool> researchCompleted,
            IEnumerable<string> completedBuildingIds)
        {
            if (definition == null) return "无效建筑";
            if (population < definition.MinimumPopulation)
                return $"需要人口：{definition.MinimumPopulation}";
            if (!string.IsNullOrEmpty(definition.RequiredResearchId) &&
                (researchCompleted == null || !researchCompleted(definition.RequiredResearchId)))
                return $"需要研究：{ResearchName(definition.RequiredResearchId)}";
            var completed = new HashSet<string>(completedBuildingIds ?? Array.Empty<string>());
            if (!string.IsNullOrEmpty(definition.RequiredBuildingId) &&
                !completed.Contains(definition.RequiredBuildingId))
                return $"需要先完成：{BuildingName(definition.RequiredBuildingId)}";
            return null;
        }

        private static string BuildingUnlockSummary(BuildingDefinition definition)
        {
            var requirements = new List<string>();
            if (definition.MinimumPopulation > 0) requirements.Add($"人口 {definition.MinimumPopulation}");
            if (!string.IsNullOrEmpty(definition.RequiredResearchId)) requirements.Add($"研究“{ResearchName(definition.RequiredResearchId)}”");
            if (!string.IsNullOrEmpty(definition.RequiredBuildingId)) requirements.Add($"建筑“{BuildingName(definition.RequiredBuildingId)}”");
            return requirements.Count == 0 ? "无额外条件" : string.Join(" + ", requirements);
        }

        private static string BuildingFunction(BuildingDefinition definition)
        {
            DefenseTowerDefinition tower = DefenseTowerCatalog.For(definition.Id.Value);
            if (tower != null)
                return $"{DamageTypeName(tower.DamageType)}防御塔，伤害 {tower.DamagePerSecond:0.#}/秒，射程 {tower.Range:0.#}，每 {tower.SecondsPerConsumable:0.#} 秒消耗 1 {ResourceName(tower.ConsumableId)}";

            switch (definition.Id.Value)
            {
                case "core.building.mining-station":
                    return "覆盖铁矿节点后采集铁，基础周期 3 秒";
                case "core.building.housing":
                    return "完成并接入物流后增加 50 人口容量";
                case "core.building.warehouse":
                    return "完成并接入物流后让每种资源容量增加 150";
                case "core.building.wall":
                    return "阻挡敌人并承受攻击的重甲防御建筑";
                case "core.building.research-station":
                    return "允许打开并推进正式科技树";
                case "core.building.smelter":
                    return Recipe(ResourceIds.Iron, 2, ResourceIds.Alloy, 1, 6);
                case "core.building.assembler":
                    return Recipe(ResourceIds.Alloy, 2, ResourceIds.Ammunition, 2, 6);
                case "technology.building.power-plant":
                    return $"每 6 秒产出 1 {ResourceName(ResourceIds.EnergyCrystal)}（当前代理：能源币）";
                case "cultivation.building.spirit-fire-furnace":
                    return Recipe(ResourceIds.Iron, 2, ResourceIds.SpiritIron, 1, 8);
                case "cultivation.building.artifact-workshop":
                    return Recipe(ResourceIds.SpiritIron, 2, ResourceIds.FlyingSword, 2, 8);
                case "cultivation.building.spirit-gathering-array":
                    return $"每 6 秒产出 1 {ResourceName(ResourceIds.EnergyCrystal)}（当前代理：灵石）";
                case "cultivation.building.alchemy-chamber":
                    return DualRecipe(ResourceIds.Biomass, 1, ResourceIds.EnergyCrystal, 1, ResourceIds.Elixir, 1, 10);
                case "cultivation.building.puppet-workshop":
                    return $"每 20 秒消耗 1 {ResourceName(ResourceIds.Alloy)} + 1 {ResourceName(ResourceIds.SpiritIron)}制造 1 个战斗傀儡，每座工坊容量 3";
                case "biological.building.colony-pool":
                    return $"{Recipe(ResourceIds.Iron, 2, ResourceIds.BoneSteel, 1, 8)}；同时{Recipe(ResourceIds.Biomass, 2, ResourceIds.BiomassConcentrate, 1, 8)}";
                case "biological.building.breeding-chamber":
                    return DualRecipe(ResourceIds.BoneSteel, 1, ResourceIds.BiomassConcentrate, 1, ResourceIds.BiologicalWeapon, 1, 8);
                case "biological.building.metabolic-furnace":
                    return $"{Recipe(ResourceIds.Biomass, 2, ResourceIds.EnergyCrystal, 1, 8)}（当前代理：能源币）";
                case "biological.building.behemoth-pen":
                    return $"每 35 秒消耗 2 {ResourceName(ResourceIds.BoneSteel)} + 3 {ResourceName(ResourceIds.BiomassConcentrate)}培育 1 只战斗巨兽，每座巨兽栏容量 1";
                case "psionics.building.resonance-furnace":
                    return Recipe(ResourceIds.Iron, 2, ResourceIds.ResonanceMetal, 1, 8);
                case "psionics.building.workshop":
                    return Recipe(ResourceIds.ResonanceMetal, 2, ResourceIds.PsionicAmplifier, 2, 8);
                case "psionics.building.consciousness-network":
                    return $"每 10 秒产出 1 {ResourceName(ResourceIds.PsionicAmplifier)}（当前代理：精神力结晶）";
                case "psionics.building.shield-generator":
                    return "每 8 秒为半径 6 格内已完成建筑提供 25 点护盾，护盾上限 100";
                case "core.building.automated-repair-bay":
                    return "每 6 秒为半径 6 格内已完成建筑恢复 20 点耐久";
                default:
                    return "未登记功能";
            }
        }

        private static string Recipe(string inputId, int input, string outputId, int output, int seconds)
        {
            return $"每 {seconds} 秒消耗 {input} {ResourceName(inputId)}，产出 {output} {ResourceName(outputId)}";
        }

        private static string DualRecipe(string inputAId, int inputA, string inputBId, int inputB, string outputId, int output, int seconds)
        {
            return $"每 {seconds} 秒消耗 {inputA} {ResourceName(inputAId)} + {inputB} {ResourceName(inputBId)}，产出 {output} {ResourceName(outputId)}";
        }

        private static string ResearchName(string researchId)
        {
            ResearchDefinition definition = ResearchCatalog.Find(researchId);
            return definition?.Name ?? "未知研究";
        }

        private static string BuildingName(string buildingId)
        {
            BuildingDefinition definition = Array.Find(BuildingCatalog.All, value => value.Id.Value == buildingId);
            return definition?.Name ?? "未知建筑";
        }

        private static string DamageTypeName(DamageType damageType)
        {
            switch (damageType)
            {
                case DamageType.Physical: return "物理";
                case DamageType.Energy: return "能量";
                case DamageType.TrueEssence: return "真元";
                case DamageType.Biological: return "生物";
                default: return "灵能";
            }
        }
    }
}
