using System;

namespace WasteCity.Combat
{
    public enum DamageType { Physical, Energy, Psionic, Biological, TrueEssence }
    public enum ArmorType { Light, Heavy, PsionicShield, BiologicalShell, SpiritualBarrier }

    public static class DamageMatrix
    {
        private static readonly int[,] Percent =
        {
            {100, 70, 130, 100, 100},
            {100, 130, 100, 70, 130},
            {100, 100, 70, 130, 130},
            {100, 100, 100, 130, 70},
            {100, 130, 130, 70, 100}
        };
        public static int Apply(int rawDamage, DamageType damage, ArmorType armor)
            => (int)Math.Min(
                int.MaxValue,
                (long)Math.Max(0, rawDamage) *
                Percent[(int)damage, (int)armor] / 100L);

        public static float Multiplier(DamageType damage, ArmorType armor)
            => Percent[(int)damage, (int)armor] / 100f;
    }
}
