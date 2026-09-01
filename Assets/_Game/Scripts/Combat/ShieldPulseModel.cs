using System;

namespace WasteCity.Combat
{
    public sealed class ShieldPulseModel
    {
        private readonly float interval;
        private float clock;
        public ShieldPulseModel(float intervalSeconds) => interval = Math.Max(.1f, intervalSeconds);
        public float Clock => clock;
        public float Interval => interval;
        public bool Tick(float deltaTime)
        {
            clock += Math.Max(0f, deltaTime);
            if (clock < interval) return false;
            clock %= interval;
            return true;
        }
        public bool TryRestoreClock(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) ||
                value < 0f || value >= interval) return false;
            clock = value;
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
        public float Clock => pulse.Clock;
        public float Interval => pulse.Interval;
        public bool TryRestoreClock(float value)=>pulse.TryRestoreClock(value);
    }
}
