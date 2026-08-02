using System;

namespace WasteCity.Combat
{
    public sealed class ShieldPulseModel
    {
        private readonly float interval;
        private float clock;
        public ShieldPulseModel(float intervalSeconds) => interval = Math.Max(.1f, intervalSeconds);
        public bool Tick(float deltaTime)
        {
            clock += Math.Max(0f, deltaTime);
            if (clock < interval) return false;
            clock %= interval;
            return true;
        }
    }

    public sealed class AutomatedRepairModel
    {
        private readonly ShieldPulseModel pulse;
        private readonly int healAmount;
        public AutomatedRepairModel(float intervalSeconds,int amount){pulse=new ShieldPulseModel(intervalSeconds);healAmount=Math.Max(1,amount);}
        public bool Tick(float deltaTime)=>pulse.Tick(deltaTime);
        public int Repair(HealthModel health)=>health?.Heal(healAmount)??0;
    }
}
