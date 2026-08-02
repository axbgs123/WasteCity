using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Persistence;
using WasteCity.Progression;

namespace WasteCity.UI
{
    public sealed class FormalTitleMenuController : MonoBehaviour
    {
        [SerializeField] private FormalSaveController saves;
        [SerializeField] private FormalAdvancementController advancement;
        public TitleMenuModel Model { get; }=new TitleMenuModel();
        private void Start()=>Time.timeScale=0f;
        private void Update()
        {
            if(Model.State==TitleMenuState.Started||Keyboard.current==null)return;
            if(Model.State==TitleMenuState.Help){if(Keyboard.current.escapeKey.wasPressedThisFrame||Keyboard.current.backspaceKey.wasPressedThisFrame)Model.Back();return;}
            if(Keyboard.current.nKey.wasPressedThisFrame&&Model.StartNew())Time.timeScale=1f;
            else if(Keyboard.current.lKey.wasPressedThisFrame&&saves.HasSave&&saves.Load()&&Model.Continue(true))Time.timeScale=advancement.IsPresenting?0f:1f;
            else if(Keyboard.current.hKey.wasPressedThisFrame)Model.OpenHelp();
            else if(Keyboard.current.escapeKey.wasPressedThisFrame)Application.Quit();
        }
        private void OnGUI()
        {
            if(Model.State==TitleMenuState.Started)return;var rect=new Rect(Screen.width*.17f,Screen.height*.13f,Screen.width*.66f,Screen.height*.72f);
            if(Model.State==TitleMenuState.Help)
            {
                GUI.Box(rect,"操作与目标\n\nWASD 驾驶移动城市｜F 展开/收起｜B 建造｜K 研究\n数字键选择命轨、建筑或研究｜鼠标左键放置建筑｜R 维修\nE 立即救援｜G 延迟救援｜Q 领袖强制过载｜U 文明升阶\nP 暂停｜F5 保存｜F9 读取｜C 结算后继续\n\n核心循环：探索资源 → 展开堡垒 → 建立生产链 → 防守压力波次 → 击败晶壳母体 → 主动升阶\n\n[Esc / Backspace] 返回");return;
            }
            string continueText=saves.HasSave?"[L] 继续最近存档":"[L] 继续（暂无存档）";
            GUI.Box(rect,$"废土移动城市\nWASTE CITY\n\n正式开发占位版本\n所有角色、建筑、敌人和特效均可由 VisualDefinition / Prefab 替换\n\n[N] 开始新游戏\n{continueText}\n[H] 操作说明\n[Esc] 退出\n\n目标：让文明在移动、生产与不断升级的外部压力中存活下来");
        }
    }
}
