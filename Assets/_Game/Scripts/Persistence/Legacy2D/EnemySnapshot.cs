using System;

namespace WasteCity.Combat
{
    [Serializable]
    public sealed class EnemySnapshot
    {
        public EnemySnapshot()
        {
        }

        public int archetype, quality, waveTrigger, health, shield,
            infectionStacks, swordIntentStacks;
        public float x, y, infectionElapsed, psionicResonanceRemaining;
        public bool controlled;
        public BossEncounterSnapshot boss;
    }
}
