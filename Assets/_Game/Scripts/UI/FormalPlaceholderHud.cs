using UnityEngine;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Combat;
using WasteCity.Population;

namespace WasteCity.UI
{
    public sealed class FormalPlaceholderHud : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private HealthComponent cityHealth;
        [SerializeField] private FormalPopulationController population;
        private void OnGUI()
        {
            string resources = economy == null ? string.Empty : $"铁 {economy.Inventory.Get(ResourceIds.Iron)} 能晶 {economy.Inventory.Get(ResourceIds.EnergyCrystal)} 石 {economy.Inventory.Get(ResourceIds.Stone)} 生物质 {economy.Inventory.Get(ResourceIds.Biomass)} 水 {economy.Inventory.Get(ResourceIds.Water)}\n合金 {economy.Inventory.Get(ResourceIds.Alloy)} 弹药 {economy.Inventory.Get(ResourceIds.Ammunition)}";
            string mode = city == null ? "" : city.Deployment.Mode.ToString();
            string health = cityHealth == null ? "" : $"核心 {cityHealth.Value.Current}/{cityHealth.Value.Maximum}";
            string people = population == null ? "" : $"人口 {population.Model.Current}/{population.Model.Capacity} · 有效劳动力 {population.Model.EffectiveWorkers} · 等待 {population.Model.Waiting} · 生产力 {population.Model.ProductivityMultiplier:P0}";
            GUI.Box(new Rect(18, 18, 620, 185), $"废土移动城市 · 正式版技术原型\n世界种子 8128 · 一级行星文明 · {mode} · {health}\n{people}\n{resources}\nWASD 驾驶 | X 展开/收起 | 堡垒状态在资源格按 E 采集\n所有视觉为待替换建模占位符");
        }
    }
}
