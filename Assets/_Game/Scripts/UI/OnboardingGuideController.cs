using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;

namespace WasteCity.UI
{
    public sealed class OnboardingGuideController : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderBuildingController buildings;
        private Vector2 start;
        public int Stage { get; private set; }
        public string CurrentInstruction => Stage == 0 ? "引导 1/4：使用 WASD 驾驶移动城市" : Stage == 1 ? "引导 2/4：按 X 展开城市为堡垒" : Stage == 2 ? "引导 3/4：停在资源点，按 E 采集" : Stage == 3 ? "引导 4/4：按 B 打开建造，数字选建筑，左键放置" : "基础引导完成：探索、生产并防御你的城市";
        private void Start() => start = city == null ? Vector2.zero : (Vector2)city.transform.position;
        private void Update()
        {
            if (city == null) return;
            if (Stage == 0 && Vector2.Distance(start, city.transform.position) > .25f) Stage = 1;
            if (Stage == 1 && city.Deployment.Mode == CityMode.Fortress) Stage = 2;
            if (Stage == 2 && economy != null && economy.LastHarvestedAmount > 0) Stage = 3;
            if (Stage == 3 && buildings != null && buildings.PlacedCount > 0) Stage = 4;
        }
    }
}
