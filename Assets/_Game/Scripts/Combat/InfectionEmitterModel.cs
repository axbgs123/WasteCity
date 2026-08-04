using System;

namespace WasteCity.Combat
{
    public sealed class InfectionEmitterModel
    {
        public const float IntervalSeconds = 1f;
        private float cooldownRemaining;

        public bool Tick(float deltaSeconds, bool dealtBiologicalDamage)
        {
            if (deltaSeconds > 0f)
            {
                cooldownRemaining = Math.Max(0f, cooldownRemaining - deltaSeconds);
                if (cooldownRemaining < .0001f) cooldownRemaining = 0f;
            }

            if (!dealtBiologicalDamage || cooldownRemaining > 0f) return false;
            cooldownRemaining = IntervalSeconds;
            return true;
        }
    }
}
