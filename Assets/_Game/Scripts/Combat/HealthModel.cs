using System;

namespace WasteCity.Combat
{
    public sealed class HealthModel
    {
        public int Maximum { get; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public event Action<int> Damaged;
        public event Action Died;
        public HealthModel(int maximum) { Maximum = Math.Max(1, maximum); Current = Maximum; }
        public int Apply(int rawDamage, DamageType type, ArmorType armor)
        {
            if (IsDead) return 0; int amount = Math.Min(Current, DamageMatrix.Apply(rawDamage, type, armor));
            if (amount <= 0) return 0; Current -= amount; Damaged?.Invoke(amount); if (IsDead) Died?.Invoke(); return amount;
        }
        public void Restore(int current) => Current = Math.Max(1, Math.Min(Maximum, current));
        public int Heal(int amount) { if (amount <= 0 || IsDead) return 0; int accepted = Math.Min(amount, Maximum - Current); Current += accepted; return accepted; }
    }
}
