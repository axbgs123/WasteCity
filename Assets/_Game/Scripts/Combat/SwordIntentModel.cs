using System;

namespace WasteCity.Combat
{
    public readonly struct SwordIntentHitResult
    {
        public bool Executed { get; }
        public int TrueDamage { get; }

        public SwordIntentHitResult(bool executed, int trueDamage)
        {
            Executed = executed;
            TrueDamage = Math.Max(0, trueDamage);
        }
    }

    public sealed class SwordIntentModel
    {
        public const int MaximumStacks = 20;
        public int Stacks { get; private set; }

        public SwordIntentHitResult AddHit(int maximumHealth)
        {
            if (maximumHealth <= 0) return new SwordIntentHitResult(false, 0);
            Stacks++;
            if (Stacks < MaximumStacks) return new SwordIntentHitResult(false, 0);
            int damage = (int)Math.Ceiling(maximumHealth * Stacks * .05d);
            Stacks = 0;
            return new SwordIntentHitResult(true, damage);
        }

        public void Clear() => Stacks = 0;

        public void Restore(int stacks) => Stacks = Math.Max(0, Math.Min(MaximumStacks - 1, stacks));
    }

    public sealed class SwordIntentEmitterModel
    {
        public const float SecondsPerStack = 1f;
        private bool emitted;
        private float elapsed;

        public bool Tick(float deltaSeconds, bool dealtDamage)
        {
            elapsed += Math.Max(0f, deltaSeconds);
            if (!dealtDamage) return false;
            if (!emitted)
            {
                emitted = true;
                elapsed = 0f;
                return true;
            }
            if (elapsed + .00001f < SecondsPerStack) return false;
            elapsed %= SecondsPerStack;
            return true;
        }
    }
}
