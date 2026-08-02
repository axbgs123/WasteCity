using System;

namespace WasteCity.Narrative
{
    public enum GuidanceStage { Awakening,Discovery,FirstFortress,ProductionChain,PressureTest,Broodmother,Complete,Advancement }
    public sealed class GuidanceFlowModel
    {
        public GuidanceStage Stage { get; private set; }
        public event Action<GuidanceStage> Changed;
        public string Title=>Stage==GuidanceStage.Awakening?"遗产苏醒":Stage==GuidanceStage.Discovery?"寻找矿脉":Stage==GuidanceStage.FirstFortress?"第一次展开":Stage==GuidanceStage.ProductionChain?"建立生产链":Stage==GuidanceStage.PressureTest?"监察者测试":Stage==GuidanceStage.Broodmother?"坐标锁定":Stage==GuidanceStage.Advancement?"文明升阶":"阶段完成";
        public string Objective=>Stage==GuidanceStage.Awakening?"使用 WASD 驾驶城市移动至少 3 格":Stage==GuidanceStage.Discovery?"寻找铁矿脉，按 F 展开为堡垒":Stage==GuidanceStage.FirstFortress?"按 B 打开建造，在铁矿脉上建造采矿站":Stage==GuidanceStage.ProductionChain?"研究基础冶金→精密装配→自动防御，完成冶炼→装配→机枪塔链条":Stage==GuidanceStage.PressureTest?"扩充弹药与防线，击退 30/60 关注度攻击":Stage==GuidanceStage.Broodmother?"准备撤离或坚守，击败 90 关注度晶壳母体":Stage==GuidanceStage.Advancement?"完成遗产解析、两座机枪塔与持续生产，然后按 U 主动升阶":"首个文明循环已完成，可继续建设与测试";
        public void SignalMoved()=>Advance(GuidanceStage.Awakening,GuidanceStage.Discovery);
        public void SignalFortress()=>Advance(GuidanceStage.Discovery,GuidanceStage.FirstFortress);
        public void SignalMiningBuilt()=>Advance(GuidanceStage.FirstFortress,GuidanceStage.ProductionChain);
        public void SignalTurretBuilt()=>Advance(GuidanceStage.ProductionChain,GuidanceStage.PressureTest);
        public void SignalWaveCompleted(int trigger){if(trigger>=60)Advance(GuidanceStage.PressureTest,GuidanceStage.Broodmother);}
        public void SignalBossDefeated()=>Advance(GuidanceStage.Broodmother,GuidanceStage.Advancement);
        public void SignalAdvanced()=>Advance(GuidanceStage.Advancement,GuidanceStage.Complete);
        public void Restore(int stage){Stage=Enum.IsDefined(typeof(GuidanceStage),stage)?(GuidanceStage)stage:GuidanceStage.Awakening;Changed?.Invoke(Stage);}
        private void Advance(GuidanceStage expected,GuidanceStage next){if(Stage!=expected)return;Stage=next;Changed?.Invoke(Stage);}
    }
}
