using System;
using System.Collections.Generic;
using WasteCity.Content;
using WasteCity.World;

namespace WasteCity.Legacy
{
    public sealed class LegacyPathDefinition
    {
        public StableId Id { get; }
        public string DisplayName { get; }
        public string RuleSummary { get; }
        public LegacyPathDefinition(string id, string name, string summary)
        { Id = new StableId(id); DisplayName = name; RuleSummary = summary; }
    }

    public static class LegacyPathCatalog
    {
        public static readonly LegacyPathDefinition[] Approved =
        {
            new LegacyPathDefinition("core.legacy.pocket-universe", "袖珍宇宙", "每类一座旗舰建筑获得倍增产能，毁坏会坍缩"),
            new LegacyPathDefinition("core.legacy.quantum-entanglement", "量子纠缠", "跨领土共享资源，同时同步污染风险"),
            new LegacyPathDefinition("core.legacy.spatial-template", "空间模板", "录制并复用建筑模板"),
            new LegacyPathDefinition("core.legacy.rewind-anchor", "回溯锚点", "回到锚点，但关注度不会被抹除"),
            new LegacyPathDefinition("core.legacy.local-haste", "局部时加", "有限时间池内加速选定区域"),
            new LegacyPathDefinition("core.legacy.foresight-delay", "预知迟滞", "观察不完整的未来命运碎片"),
            new LegacyPathDefinition("core.legacy.void-debt", "虚空债", "允许资源透支，利息转化为关注度"),
            new LegacyPathDefinition("core.legacy.causal-transparency", "因果透明", "公开关注度因果和阈值"),
            new LegacyPathDefinition("core.legacy.void-chest", "虚空宝箱", "敌人有概率掉落跨循环宝箱")
        };
    }

    public sealed class LegacySelectionModel
    {
        public IReadOnlyList<LegacyPathDefinition> Choices { get; }
        public LegacyPathDefinition Selected { get; private set; }
        public LegacySelectionModel(WorldSeed seed)
        {
            var pool = new List<LegacyPathDefinition>(LegacyPathCatalog.Approved);
            var choices = new List<LegacyPathDefinition>();
            for (int i = 0; i < 3; i++) { int index = seed.Sample(i, 0, 91) % pool.Count; choices.Add(pool[index]); pool.RemoveAt(index); }
            Choices = choices;
        }
        public bool Select(int index)
        {
            if (Selected != null || index < 0 || index >= Choices.Count) return false;
            Selected = Choices[index]; return true;
        }
    }
}
