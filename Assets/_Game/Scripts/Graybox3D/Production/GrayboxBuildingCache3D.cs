using System;

namespace WasteCity.Graybox3D.Production
{
    public enum GrayboxBuildingCachePort3D
    {
        Input,
        Output
    }

    public sealed class GrayboxBuildingCache3D
    {
        private int inputAmount;
        private int outputAmount;

        public GrayboxBuildingCache3D(
            string inputResourceId,
            int inputCapacity,
            string outputResourceId,
            int outputCapacity)
        {
            if (inputCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(inputCapacity));
            if (outputCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(outputCapacity));
            if (inputCapacity > 0 && string.IsNullOrWhiteSpace(inputResourceId))
                throw new ArgumentException(
                    "A positive input capacity requires a resource ID.",
                    nameof(inputResourceId));
            if (outputCapacity > 0 && string.IsNullOrWhiteSpace(outputResourceId))
                throw new ArgumentException(
                    "A positive output capacity requires a resource ID.",
                    nameof(outputResourceId));

            InputResourceId = inputResourceId;
            InputCapacity = inputCapacity;
            OutputResourceId = outputResourceId;
            OutputCapacity = outputCapacity;
        }

        public string InputResourceId { get; }
        public int InputCapacity { get; }
        public int InputAmount => inputAmount;
        public string OutputResourceId { get; }
        public int OutputCapacity { get; }
        public int OutputAmount => outputAmount;

        public string ResourceId(GrayboxBuildingCachePort3D port)
        {
            return port == GrayboxBuildingCachePort3D.Input
                ? InputResourceId
                : OutputResourceId;
        }

        public int Capacity(GrayboxBuildingCachePort3D port)
        {
            return port == GrayboxBuildingCachePort3D.Input
                ? InputCapacity
                : OutputCapacity;
        }

        public int Amount(GrayboxBuildingCachePort3D port)
        {
            return port == GrayboxBuildingCachePort3D.Input
                ? inputAmount
                : outputAmount;
        }

        public int AvailableCapacity(GrayboxBuildingCachePort3D port)
        {
            return Capacity(port) - Amount(port);
        }

        public int Add(
            GrayboxBuildingCachePort3D port,
            string resourceId,
            int amount)
        {
            if (amount <= 0 ||
                string.IsNullOrWhiteSpace(resourceId) ||
                !string.Equals(
                    ResourceId(port),
                    resourceId,
                    StringComparison.Ordinal))
                return 0;

            int accepted = Math.Min(amount, AvailableCapacity(port));
            if (accepted <= 0) return 0;

            if (port == GrayboxBuildingCachePort3D.Input)
                inputAmount += accepted;
            else
                outputAmount += accepted;
            return accepted;
        }

        public int Remove(GrayboxBuildingCachePort3D port, int amount)
        {
            if (amount <= 0) return 0;
            int removed = Math.Min(amount, Amount(port));
            if (port == GrayboxBuildingCachePort3D.Input)
                inputAmount -= removed;
            else
                outputAmount -= removed;
            return removed;
        }
    }
}
