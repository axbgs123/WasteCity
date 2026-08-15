using System;
using WasteCity.Building;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Production
{
    public enum GrayboxProductionKind3D
    {
        Extraction,
        Recipe
    }

    public sealed class GrayboxProductionDefinition3D
    {
        internal GrayboxProductionDefinition3D(
            string buildingId,
            GrayboxProductionKind3D kind,
            string inputResourceId,
            int inputAmount,
            string outputResourceId,
            int outputAmount,
            float cycleSeconds,
            int inputCapacity,
            int outputCapacity)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
                throw new ArgumentException(
                    "A stable building ID is required.",
                    nameof(buildingId));
            if (kind == GrayboxProductionKind3D.Recipe &&
                (string.IsNullOrWhiteSpace(inputResourceId) ||
                 string.IsNullOrWhiteSpace(outputResourceId)))
                throw new ArgumentException(
                    "Recipe production requires input and output resources.");
            if (cycleSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cycleSeconds));

            BuildingId = buildingId;
            Kind = kind;
            InputResourceId = inputResourceId;
            InputAmount = Math.Max(0, inputAmount);
            OutputResourceId = outputResourceId;
            OutputAmount = Math.Max(1, outputAmount);
            CycleSeconds = cycleSeconds;
            InputCapacity = Math.Max(0, inputCapacity);
            OutputCapacity = Math.Max(1, outputCapacity);
        }

        public string BuildingId { get; }
        public GrayboxProductionKind3D Kind { get; }
        public string InputResourceId { get; }
        public int InputAmount { get; }
        public string OutputResourceId { get; }
        public int OutputAmount { get; }
        public float CycleSeconds { get; }
        public int InputCapacity { get; }
        public int OutputCapacity { get; }
    }

    public static class GrayboxProductionCatalog3D
    {
        private static readonly GrayboxProductionDefinition3D Mining =
            new GrayboxProductionDefinition3D(
                BuildingCatalog.MiningStation.Id.Value,
                GrayboxProductionKind3D.Extraction,
                null,
                0,
                null,
                1,
                3f,
                0,
                20);

        private static readonly GrayboxProductionDefinition3D Smelter =
            new GrayboxProductionDefinition3D(
                BuildingCatalog.Smelter.Id.Value,
                GrayboxProductionKind3D.Recipe,
                ResourceIds.Iron,
                2,
                ResourceIds.Alloy,
                1,
                6f,
                20,
                10);

        private static readonly GrayboxProductionDefinition3D Assembler =
            new GrayboxProductionDefinition3D(
                BuildingCatalog.Assembler.Id.Value,
                GrayboxProductionKind3D.Recipe,
                ResourceIds.Alloy,
                2,
                ResourceIds.Ammunition,
                2,
                6f,
                20,
                30);

        public static bool TryGet(
            string buildingId,
            out GrayboxProductionDefinition3D definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(buildingId)) return false;

            if (string.Equals(
                    buildingId,
                    Mining.BuildingId,
                    StringComparison.Ordinal))
                definition = Mining;
            else if (string.Equals(
                         buildingId,
                         Smelter.BuildingId,
                         StringComparison.Ordinal))
                definition = Smelter;
            else if (string.Equals(
                         buildingId,
                         Assembler.BuildingId,
                         StringComparison.Ordinal))
                definition = Assembler;

            return definition != null;
        }
    }
}
