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
}
