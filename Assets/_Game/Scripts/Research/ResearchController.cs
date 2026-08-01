using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public sealed class ResearchController : MonoBehaviour
    {
        [SerializeField] private FormalEconomyController economy;
        public ResearchModel Model { get; private set; } = new ResearchModel();
        private bool visible;
        private void Update()
        {
            Model.Tick(Time.deltaTime);
            if (Keyboard.current == null) return;
            if (Keyboard.current.tKey.wasPressedThisFrame) visible = !visible;
            if (!visible) return;
            if (Keyboard.current.digit5Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[0], economy.Inventory);
            if (Keyboard.current.digit6Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[1], economy.Inventory);
            if (Keyboard.current.digit7Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[2], economy.Inventory);
            if (Keyboard.current.digit8Key.wasPressedThisFrame) Model.Start(ResearchCatalog.Starting[3], economy.Inventory);
        }
        private void OnGUI()
        {
            if (!visible) return;
            string status = Model.Active == null ? $"已完成 {Model.CompletedCount}" : $"研究中：{Model.Active.Name} · {Model.Remaining:0.0}s";
            GUI.Box(new Rect(Screen.width - 430f, Screen.height - 215f, 410f, 190f), "四路线研究占位面板 [T]\n" + status + "\n\n[5] 自动机械（铁）\n[6] 灵气感知（能晶）\n[7] 适应组织（生物质）\n[8] 意识共鸣（水）");
        }
    }
}
