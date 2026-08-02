using System;

namespace WasteCity.Combat
{
    public enum EnemyQuality { Ordinary, Excellent, Rare, Epic, Legendary }

    public readonly struct EnemyQualityProfile
    {
        public EnemyQuality Quality { get; }
        public string DisplayName { get; }
        public float HealthMultiplier { get; }
        public float DamageMultiplier { get; }
        public float LootMultiplier { get; }
        public EnemyQualityProfile(EnemyQuality quality, string name, float health, float damage, float loot)
        { Quality=quality;DisplayName=name;HealthMultiplier=health;DamageMultiplier=damage;LootMultiplier=loot; }
    }

    public static class EnemyQualityCatalog
    {
        private static readonly EnemyQualityProfile[] Profiles=
        {
            new EnemyQualityProfile(EnemyQuality.Ordinary,"普通",1f,1f,1f),
            new EnemyQualityProfile(EnemyQuality.Excellent,"优秀",2f,1.5f,1.5f),
            new EnemyQualityProfile(EnemyQuality.Rare,"稀有",2.5f,1.75f,2f),
            new EnemyQualityProfile(EnemyQuality.Epic,"史诗",4f,2.25f,3f),
            new EnemyQualityProfile(EnemyQuality.Legendary,"传说",7f,3f,5f)
        };
        public static EnemyQualityProfile For(EnemyQuality quality)=>Profiles[(int)quality];
    }

    public static class EnemyQualityRoller
    {
        public static EnemyQuality ForSpawn(int slot,int waveTrigger,int civilizationLevel)
        {
            unchecked
            {
                int value=(slot*73856093)^(waveTrigger*19349663)^(civilizationLevel*83492791);
                int roll=(value&int.MaxValue)%1000;
                return FromRoll(roll,civilizationLevel,waveTrigger>=120);
            }
        }

        public static EnemyQuality FromRoll(int roll,int civilizationLevel,bool crisis)
        {
            roll=Math.Max(0,Math.Min(999,roll));
            if(crisis&&roll<10)return EnemyQuality.Epic;
            int rareWindow=civilizationLevel>=2?60:30;
            if(roll<rareWindow)return EnemyQuality.Rare;
            if(roll<rareWindow+150)return EnemyQuality.Excellent;
            return EnemyQuality.Ordinary;
        }
    }
}
