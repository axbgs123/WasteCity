using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Persistence;

namespace WasteCity.Progression
{
    public sealed class FormalAdvancementController : MonoBehaviour
    {
        [SerializeField] private FormalProgressionController progression;
        [SerializeField] private FormalSaveController saves;
        [SerializeField] private GameObject scanVisual;
        public AdvancementSequenceModel Model { get; } = new AdvancementSequenceModel();
        public bool IsPresenting => Model.IsPresenting;

        private void Start()
        {
            progression.Advanced += Begin;
            Model.Changed += OnStageChanged;
            OnStageChanged(Model.Stage);
        }

        private void Update()
        {
            if (!Model.IsPresenting) return;
            var previous = Model.Stage;
            Model.Tick(Time.unscaledDeltaTime);
            if (previous != AdvancementSequenceStage.Results && Model.Stage == AdvancementSequenceStage.Results) saves.Save();
            if (Model.Stage == AdvancementSequenceStage.Results && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                Model.Continue(); Time.timeScale = 1f; saves.Save();
            }
        }

        private void Begin()
        {
            if (!Model.Start()) return;
            Time.timeScale = 0f; saves.Save();
        }

        private void OnStageChanged(AdvancementSequenceStage stage)
        {
            if (scanVisual != null) scanVisual.SetActive(stage >= AdvancementSequenceStage.Scanning && stage <= AdvancementSequenceStage.Warning);
        }

        public void Restore(int stage, float remaining)
        {
            Model.Restore(stage, remaining);
            Time.timeScale = Model.IsPresenting ? 0f : 1f;
        }

        private void OnGUI()
        {
            if (!Model.IsPresenting) return;
            var rect = new Rect(Screen.width * .16f, Screen.height * .2f, Screen.width * .68f, Screen.height * .56f);
            string message = Model.Stage == AdvancementSequenceStage.Scanning
                ? $"文明升阶完成\n\n规则扫描中…… {Model.Remaining:0.0}s"
                : Model.Stage == AdvancementSequenceStage.Confirmed
                    ? "异常变量确认。\n遗产响应程度：超出历史样本。"
                    : Model.Stage == AdvancementSequenceStage.Warning
                        ? "未知信息覆盖了扫描结果：\n\n“它们已经看见你了。下一次，不会只派这些东西。”"
                        : "阶段结算\n\n文明等级提升至 2\n命轨提升至 Lv.2\n异常观测值 +25\n晶壳母体已击败\n\n按 C 继续游玩当前地图";
            GUI.Box(rect, message);
        }
    }
}
