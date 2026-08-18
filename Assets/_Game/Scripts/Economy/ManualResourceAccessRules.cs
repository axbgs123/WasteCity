using System;
using WasteCity.City;

namespace WasteCity.Economy
{
    public static class ManualResourceAccessRules
    {
        public const float FormalInteractionRadius = 2f;
        private const double BoundaryTolerance = .00001d;

        public static bool EvaluateCityInventory(
            DirectControlTarget controlTarget,
            bool leaderRecruited,
            float controlledX,
            float controlledY,
            int footprintX,
            int footprintY,
            int footprintWidth,
            int footprintHeight)
        {
            if (controlTarget == DirectControlTarget.City)
                return true;
            return controlTarget == DirectControlTarget.Leader &&
                leaderRecruited &&
                IsWithinFootprintRange(
                    controlledX,
                    controlledY,
                    footprintX,
                    footprintY,
                    footprintWidth,
                    footprintHeight);
        }

        public static bool EvaluateBuildingInventory(
            DirectControlTarget controlTarget,
            bool leaderRecruited,
            float controlledX,
            float controlledY,
            int footprintX,
            int footprintY,
            int footprintWidth,
            int footprintHeight,
            bool completed,
            bool playerOwned,
            bool evacuationLocked)
        {
            if (!completed || !playerOwned || evacuationLocked ||
                (controlTarget != DirectControlTarget.City &&
                 (controlTarget != DirectControlTarget.Leader ||
                  !leaderRecruited)))
            {
                return false;
            }

            return IsWithinFootprintRange(
                controlledX,
                controlledY,
                footprintX,
                footprintY,
                footprintWidth,
                footprintHeight);
        }

        private static bool IsWithinFootprintRange(
            float controlledX,
            float controlledY,
            int footprintX,
            int footprintY,
            int footprintWidth,
            int footprintHeight)
        {
            if (!IsFinite(controlledX) || !IsFinite(controlledY) ||
                footprintWidth <= 0 || footprintHeight <= 0)
            {
                return false;
            }

            double minimumX = footprintX - .5d;
            double maximumX = (double)footprintX + footprintWidth - .5d;
            double minimumY = footprintY - .5d;
            double maximumY = (double)footprintY + footprintHeight - .5d;
            double deltaX = AxisDistance(
                controlledX,
                minimumX,
                maximumX);
            double deltaY = AxisDistance(
                controlledY,
                minimumY,
                maximumY);
            double radius = FormalInteractionRadius;
            return deltaX * deltaX + deltaY * deltaY <=
                radius * radius + BoundaryTolerance;
        }

        private static double AxisDistance(
            double value,
            double minimum,
            double maximum)
        {
            if (value < minimum) return minimum - value;
            if (value > maximum) return value - maximum;
            return 0d;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
