using System;

namespace WasteCity.Combat
{
    public sealed class PsionicResonanceModel
    {
        public const float DurationSeconds = 5f;

        public float Remaining { get; private set; }
        public bool Active => Remaining > 0f;

        public void Apply() => Remaining = DurationSeconds;

        public void Tick(float deltaSeconds)
        {
            Remaining = Math.Max(0f, Remaining - Math.Max(0f, deltaSeconds));
        }

        public void Clear() => Remaining = 0f;

        public void Restore(float remaining)
        {
            Remaining = Math.Max(0f, Math.Min(DurationSeconds, remaining));
        }
    }

    public static class PsionicResonanceRules
    {
        public const int MaximumTargets = 10;
        public const int SynchronizedPercent = 30;

        public static bool CanMark(bool alreadyMarked, int activeMarkerCount)
        {
            return alreadyMarked || activeMarkerCount < MaximumTargets;
        }

        public static int SynchronizedRawDamage(int primaryAppliedDamage)
        {
            if (primaryAppliedDamage <= 0) return 0;
            long scaled = (long)primaryAppliedDamage * SynchronizedPercent / 100;
            return (int)Math.Max(1L, Math.Min(int.MaxValue, scaled));
        }
    }
}
