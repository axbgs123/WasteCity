using System;

namespace WasteCity.Legacy
{
    public sealed class LocalHasteModel
    {
        public const float DailyPool = 60f;
        public const float ActiveMultiplier = 5f;
        public int PoolDay { get; private set; } = 1;
        public float Remaining { get; private set; } = DailyPool;
        public bool Active { get; private set; }
        public float Multiplier => Active && Remaining > 0f ? ActiveMultiplier : 1f;
        public void SetActive(bool value) => Active = value && Remaining > 0f;
        public void Tick(float delta, int day)
        {
            if (day != PoolDay) { PoolDay = Math.Max(1, day); Remaining = DailyPool; }
            if (!Active || delta <= 0f) return; Remaining = Math.Max(0f, Remaining - delta); if (Remaining <= 0f) Active = false;
        }
        public void Restore(int day, float remaining, bool active) { PoolDay = Math.Max(1, day); Remaining = Math.Max(0f, Math.Min(DailyPool, remaining)); Active = active && Remaining > 0f; }
    }
}
