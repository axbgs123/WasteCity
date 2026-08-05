using System;

namespace WasteCity.Building
{
    public static class BuildingRangeRules
    {
        public const int InitialGroundRadius = 8;
        private const int FormationReinforcementGroundRadius = 12;
        private const int OrbitalSupplyGroundRadius = 24;
        private const int InnerGridWidth = 8;
        private const int InnerGridHeight = 6;

        public static bool IsSupportedGroundRadius(int radius)
        {
            return radius == InitialGroundRadius ||
                   radius == FormationReinforcementGroundRadius ||
                   radius == OrbitalSupplyGroundRadius;
        }

        public static bool IsGroundCellInRange(int cityX, int cityY, int cellX, int cellY, int radius)
        {
            return IsSupportedGroundRadius(radius) &&
                   Math.Max(
                       Math.Abs((long)cellX - cityX),
                       Math.Abs((long)cellY - cityY)) <= radius;
        }

        public static bool IsInnerFootprintInBounds(
            BuildingDefinition definition,
            int x,
            int y,
            BuildingOrientation orientation)
        {
            if (definition == null || !BuildingOrientationRules.IsValid(orientation)) return false;
            return x >= 0 && y >= 0 &&
                   (long)x + BuildingOrientationRules.Width(definition, orientation) <= InnerGridWidth &&
                   (long)y + BuildingOrientationRules.Height(definition, orientation) <= InnerGridHeight;
        }
    }
}
