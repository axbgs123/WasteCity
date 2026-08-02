using System;

namespace WasteCity.Combat
{
    public static class MindControlModel
    {
        public const int ChancePercent = 10;
        public static bool ShouldConvert(bool researchCompleted,EnemyQuality quality,bool isHeavy,int percentRoll)
        {
            int roll=Math.Max(0,Math.Min(99,percentRoll));
            return researchCompleted&&!isHeavy&&quality==EnemyQuality.Ordinary&&roll<ChancePercent;
        }
    }
}
