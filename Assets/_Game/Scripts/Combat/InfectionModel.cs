using System;

namespace WasteCity.Combat
{
    public sealed class InfectionModel
    {
        public const int BurstThreshold = 10;
        public const float TickSeconds = 1f;
        public const float MaximumHealthFraction = .02f;

        public int Stacks { get; private set; }
        public float Elapsed { get; private set; }

        public bool AddStacks(int amount)
        {
            if (amount <= 0) return false;
            if (amount >= BurstThreshold - Stacks)
            {
                Clear();
                return true;
            }

            Stacks += amount;
            return false;
        }

        public int Tick(float deltaSeconds, int maximumHealth)
        {
            if (Stacks <= 0 || deltaSeconds <= 0f) return 0;
            Elapsed += deltaSeconds;
            int ticks = (int)(Elapsed / TickSeconds);
            if (ticks <= 0) return 0;
            Elapsed -= ticks * TickSeconds;
            int damagePerTick = Math.Max(1, (int)Math.Ceiling(Math.Max(1, maximumHealth) * MaximumHealthFraction));
            return damagePerTick * ticks;
        }

        public void Clear()
        {
            Stacks = 0;
            Elapsed = 0f;
        }

        public void Restore(int stacks, float elapsed)
        {
            Stacks = Math.Max(0, Math.Min(BurstThreshold - 1, stacks));
            Elapsed = elapsed <= 0f ? 0f : elapsed % TickSeconds;
        }
    }
}
