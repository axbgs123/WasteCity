using UnityEngine;
using WasteCity.City;
using WasteCity.Economy;

namespace WasteCity.UI
{
    public sealed class FormalPlaceholderHud : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        private void OnGUI()
        {
            string resources = economy == null ? string.Empty : $"铁 {economy.Inventory.Get(ResourceIds.Iron)}  能晶 {economy.Inventory.Get(ResourceIds.EnergyCrystal)}  石 {economy.Inventory.Get(ResourceIds.Stone)}  生物质 {economy.Inventory.Get(ResourceIds.Biomass)}  水 {economy.Inventory.Get(ResourceIds.Water)}";
            string mode = city == null ? "" : city.Deployment.Mode.ToString();
            GUI.Box(new Rect(18, 18, 560, 145), $"废土移动城市 · 正式版技术原型\n世界种子 8128 · 一级行星文明 · {mode}\n{resources}\nWASD 驾驶 | X 展开/收起 | 堡垒状态在资源格按 E 采集\n所有视觉为待替换建模占位符");
        }
    }
}
