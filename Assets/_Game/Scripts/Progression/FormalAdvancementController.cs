using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Persistence;
using WasteCity.Core;

namespace WasteCity.Progression
{
    public sealed class FormalAdvancementController : MonoBehaviour
    {
        [SerializeField] private FormalProgressionController progression;
        [SerializeField] private FormalSaveController saves;
        [SerializeField] private GameObject scanVisual;
        [SerializeField] private FormalSessionStatisticsController statistics;
        private GameSpeedController gameSpeed;
        public AdvancementSequenceModel Model { get; } = new AdvancementSequenceModel();
        public bool IsPresenting => Model.IsPresenting;

        private void Start()
        {
            gameSpeed=FindObjectOfType<GameSpeedController>();
            progression.Advanced += Begin;
            Model.Changed += OnStageChanged;
            OnStageChanged(Model.Stage);
            gameSpeed?.SetPaused(GamePauseReason.Advancement,Model.IsPresenting);
        }

        private void Update()
        {
            if (!Model.IsPresenting) return;
            var previous = Model.Stage;
            Model.Tick(Time.unscaledDeltaTime);
            if (previous != AdvancementSequenceStage.Results && Model.Stage == AdvancementSequenceStage.Results) saves.Save();
            if (Model.Stage == AdvancementSequenceStage.Results && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                Model.Continue(); gameSpeed?.SetPaused(GamePauseReason.Advancement,false); saves.Save();
            }
        }

        private void Begin()
        {
            if (!Model.Start()) return;
            gameSpeed?.SetPaused(GamePauseReason.Advancement,true); saves.Save();
        }

        private void OnStageChanged(AdvancementSequenceStage stage)
        {
            if (scanVisual != null) scanVisual.SetActive(stage >= AdvancementSequenceStage.Scanning && stage <= AdvancementSequenceStage.Warning);
        }

        public void Restore(int stage, float remaining)
        {
            Model.Restore(stage, remaining);
            gameSpeed?.SetPaused(GamePauseReason.Advancement,Model.IsPresenting);
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
                        : ResultsText();
            GUI.Box(rect, message);
        }
        private string ResultsText()
        {
            var stats=statistics.Model;int minutes=Mathf.FloorToInt(stats.ElapsedSeconds/60f),seconds=Mathf.FloorToInt(stats.ElapsedSeconds)%60;float efficiency=stats.ElapsedSeconds>0?stats.ProductionCycles/(stats.ElapsedSeconds/60f):0f;string rescue=stats.Rescues==0?"未完成":$"{stats.Rescues} 次（延迟 {stats.DelayedRescues}）";string strategy=stats.RetreatedDuringBoss?"撤离重组防线":"坚守原阵地";
            return $"阶段结算\n\n完成时间 {minutes:00}:{seconds:00}｜击杀 {stats.Kills}｜最高关注度 {stats.HighestObservation:0}\n生产效率 {efficiency:0.0} 周期/分钟｜建筑损失 {stats.BuildingLosses}\n救援 {rescue}｜Boss 策略：{strategy}\n文明等级 2｜命轨 Lv.2｜晶壳母体已击败\n\n按 C 继续游玩当前地图";
        }
    }
}
