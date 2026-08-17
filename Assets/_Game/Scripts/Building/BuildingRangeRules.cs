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

        public static bool IsGroundFootprintInRange(
            BuildingDefinition definition,
            int x,
            int y,
            BuildingOrientation orientation,
            int cityX,
            int cityY,
            int radius)
        {
            if (definition == null ||
                !BuildingOrientationRules.IsValid(orientation))
            {
                return false;
            }

            int width = BuildingOrientationRules.Width(
                definition,
                orientation);
            int height = BuildingOrientationRules.Height(
                definition,
                orientation);
            for (int offsetX = 0; offsetX < width; offsetX++)
            for (int offsetY = 0; offsetY < height; offsetY++)
            {
                long cellX = (long)x + offsetX;
                long cellY = (long)y + offsetY;
                if (cellX < int.MinValue || cellX > int.MaxValue ||
                    cellY < int.MinValue || cellY > int.MaxValue ||
                    !IsGroundCellInRange(
                        cityX,
                        cityY,
                        (int)cellX,
                        (int)cellY,
                        radius))
                {
                    return false;
                }
            }

            return true;
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
