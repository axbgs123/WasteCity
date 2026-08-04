using System;

namespace WasteCity.Combat
{
    public enum TechnologyOverloadPhase
    {
        Ready,
        Boosting,
        Lockout,
        Cooldown
    }

    public sealed class TechnologyOverloadModel
    {
        public const float BoostSeconds = 5f;
        public const float LockoutSeconds = 3f;
        public const float CooldownSeconds = 30f;

        public float CooldownRemaining { get; private set; }
        public float BoostRemaining { get; private set; }
        public float LockoutRemaining { get; private set; }

        public TechnologyOverloadPhase Phase =>
            BoostRemaining > 0f
                ? TechnologyOverloadPhase.Boosting
                : LockoutRemaining > 0f
                    ? TechnologyOverloadPhase.Lockout
                    : CooldownRemaining > 0f
                        ? TechnologyOverloadPhase.Cooldown
                        : TechnologyOverloadPhase.Ready;

        public float FireRateMultiplier =>
            Phase == TechnologyOverloadPhase.Boosting
                ? 2f
                : Phase == TechnologyOverloadPhase.Lockout ? 0f : 1f;

        public float DamageMultiplier(DamageType type) =>
            Phase == TechnologyOverloadPhase.Boosting && type == DamageType.Energy ? 1.3f : 1f;

        public bool TryActivate(bool unlocked)
        {
            if (!unlocked || CooldownRemaining > 0f) return false;
            CooldownRemaining = CooldownSeconds;
            BoostRemaining = BoostSeconds;
            LockoutRemaining = 0f;
            return true;
        }

        public void Tick(float deltaSeconds)
        {
            float remaining = Math.Max(0f, deltaSeconds);
            CooldownRemaining = Math.Max(0f, CooldownRemaining - remaining);

            if (BoostRemaining > 0f)
            {
                float used = Math.Min(remaining, BoostRemaining);
                BoostRemaining -= used;
                remaining -= used;
                if (BoostRemaining <= 0f) LockoutRemaining = LockoutSeconds;
            }

            if (remaining > 0f && LockoutRemaining > 0f)
                LockoutRemaining = Math.Max(0f, LockoutRemaining - remaining);
        }

        public void Restore(bool unlocked, float cooldown, float boost, float lockout)
        {
            if (!unlocked)
            {
                CooldownRemaining = 0f;
                BoostRemaining = 0f;
                LockoutRemaining = 0f;
                return;
            }

            CooldownRemaining = Math.Max(0f, cooldown);
            BoostRemaining = Math.Max(0f, boost);
            LockoutRemaining = Math.Max(0f, lockout);
        }
    }

    public static class TurretCombatModifierRules
    {
        public static float ResolveFireRate(float leaderMultiplier, float technologyMultiplier)
        {
            if (leaderMultiplier <= 0f || technologyMultiplier <= 0f) return 0f;
            return Math.Max(1f, Math.Max(leaderMultiplier, technologyMultiplier));
        }

        public static float ResolveDamage(DamageType type, float technologyMultiplier) =>
            type == DamageType.Energy ? Math.Max(1f, technologyMultiplier) : 1f;
    }
}
