using System;
using WasteCity.World;

namespace WasteCity.City
{
    public enum CityDeploymentFailure
    {
        None = 0,
        OutsideWorld = 1,
        Blocked = 2,
        UnstableGround = 3
    }

    public static class CityDeploymentRules
    {
        public const float FormalDeployDurationSeconds = 5f;
        public const float FormalPackDurationSeconds = 8f;

        public static CityDeploymentFailure Validate(
            WorldMapModel map,
            int centerX,
            int centerY,
            int radiusX = 1,
            int radiusY = 1)
        {
            if (map == null) return CityDeploymentFailure.OutsideWorld;
            radiusX = Math.Max(0, radiusX);
            radiusY = Math.Max(0, radiusY);
            int minimumX = centerX - radiusX;
            int maximumX = centerX + radiusX;
            int minimumY = centerY - radiusY;
            int maximumY = centerY + radiusY;
            if (minimumX < 0 ||
                minimumY < 0 ||
                maximumX >= map.Width ||
                maximumY >= map.Height)
                return CityDeploymentFailure.OutsideWorld;

            bool unstable = false;
            for (int x = minimumX; x <= maximumX; x++)
                for (int y = minimumY; y <= maximumY; y++)
                {
                    WorldCell cell = map.Get(x, y);
                    if (!CityTerrainRules.IsPassable(cell))
                        return CityDeploymentFailure.Blocked;
                    if (!CityTerrainRules.SupportsDeployment(cell))
                        unstable = true;
                }
            return unstable
                ? CityDeploymentFailure.UnstableGround
                : CityDeploymentFailure.None;
        }

        public static string FailureReason(CityDeploymentFailure failure)
        {
            switch (failure)
            {
                case CityDeploymentFailure.OutsideWorld:
                    return "展开失败：空间不足";
                case CityDeploymentFailure.Blocked:
                    return "展开失败：范围内存在深水或悬崖";
                case CityDeploymentFailure.UnstableGround:
                    return "展开失败：地面不稳定或有大型废墟";
                default:
                    return string.Empty;
            }
        }
    }
}
