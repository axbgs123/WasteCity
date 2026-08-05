using UnityEngine;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Combat;
using WasteCity.Population;
using WasteCity.Core;
using WasteCity.Content;

namespace WasteCity.UI
{
    public sealed class FormalPlaceholderHud : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private HealthComponent cityHealth;
        [SerializeField] private FormalPopulationController population;
        [SerializeField] private OnboardingGuideController guide;
        [SerializeField] private GameSpeedController gameSpeed;
        [SerializeField] private FormalGameClockController clock;
        private void OnGUI()
        {
            string resources = economy == null ? string.Empty : RouteContentDisplayCatalog.InventorySummary(economy.Inventory);
            string mode = city == null ? "" : city.Deployment.Mode.ToString();
            string health = cityHealth == null ? "" : $"核心 {cityHealth.Value.Current}/{cityHealth.Value.Maximum}";
            string people = population == null ? "" : $"人口 {population.Model.Current}/{population.Model.Capacity} · 有效劳动力 {population.Model.EffectiveWorkers} · 等待 {population.Model.Waiting} · 生产力 {population.Model.ProductivityMultiplier:P0}";
            string guidance = guide == null ? "" : guide.CurrentInstruction;
            string speed = gameSpeed == null ? "" : $"时间 {gameSpeed.Model.Speed:0.#}×（空格暂停，[ 1×，] 2×）";
            string day = clock == null ? "" : $"第 {clock.Model.Day} 天 · {clock.Model.SecondsIntoDay / clock.Model.SecondsPerDay:P0}";
            GUI.Box(new Rect(18, 18, 820, 285), $"废土移动城市 · 正式版技术原型\n世界种子 8128 · {day} · {mode} · {health} · {speed}\n{people}\n{resources}\n{guidance}\nWASD 驾驶 | F 展开/收起 | 堡垒状态在资源格按 E 采集\n所有视觉为待替换建模占位符");
        }
    }
}
