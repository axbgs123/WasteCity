using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Economy;
using WasteCity.Building;
using WasteCity.City;

namespace WasteCity.Research
{
    public sealed class ResearchController : MonoBehaviour
    {
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderBuildingController buildings;
        [SerializeField] private PlaceholderMobileCity city;
        public ResearchModel Model { get; private set; } = new ResearchModel();
        private bool visible;
        private void Update()
        {
            if(city.LongWorkAllowed)Model.Tick(Time.deltaTime);
            if (Keyboard.current == null) return;
            if (Keyboard.current.kKey.wasPressedThisFrame) visible = !visible;
            if (!visible) return;
            if (buildings.CompletedCount("core.building.research-station") <= 0) return;
            if(!city.LongWorkAllowed)return;
            if (Keyboard.current.digit5Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[0], economy.Inventory);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[1], economy.Inventory);
            if (Keyboard.current.digit7Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[2], economy.Inventory);
            if (Keyboard.current.digit8Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[3], economy.Inventory);
            if (Keyboard.current.digit9Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[4], economy.Inventory);
        }
        private void OnGUI()
        {
            if (!visible) return;
            string status = buildings.CompletedCount("core.building.research-station")<=0?"锁定：需要已完成研究站":!city.LongWorkAllowed?"暂停：长周期研究仅在堡垒态推进":Model.Active == null ? $"已完成 {Model.CompletedCount}" : $"研究中：{Model.Active.Name} · {Model.Remaining:0.0}s";
            GUI.Box(new Rect(Screen.width - 430f, Screen.height - 245f, 410f, 220f), "四路线研究占位面板 [K]\n" + status + "\n\n[5] 自动机械（铁）\n[6] 灵气感知（能晶）\n[7] 适应组织（生物质）\n[8] 意识共鸣（水）\n[9] 遗产解析（需自动机械，合金30）");
        }
    }
}
