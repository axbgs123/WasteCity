using System;

namespace WasteCity.Building
{
    public sealed class RepairProcess
    {
        public int HealAmount { get; }
        public float Remaining { get; private set; }
        public bool IsComplete => Remaining <= 0f;
        public RepairProcess(float duration = 2f, int healAmount = 50) { Remaining = Math.Max(.1f, duration); HealAmount = Math.Max(1, healAmount); }
        public bool Tick(float delta, float productivity)
        { if (IsComplete || delta <= 0f) return false; Remaining = Math.Max(0f, Remaining - delta * Math.Max(0f, productivity)); return IsComplete; }
        public void Restore(float remaining) => Remaining = Math.Max(0f, remaining);
    }
}
