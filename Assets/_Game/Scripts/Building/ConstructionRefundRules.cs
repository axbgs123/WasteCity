using System;

namespace WasteCity.Building
{
    public static class ConstructionRefundRules
    {
        public static int Calculate(
            int originalCost,
            double remainingRatio,
            double handlingRatio)
        {
            double raw = Math.Max(0d, originalCost) *
                         Math.Max(0d, Math.Min(1d, remainingRatio)) *
                         Math.Max(0d, handlingRatio);
            int rounded = (int)Math.Round(
                raw,
                MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(originalCost, rounded));
        }
    }
}
