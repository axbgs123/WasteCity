using System;

namespace WasteCity.Graybox3D.Production
{
    public enum GrayboxProductionStopReason3D
    {
        None,
        PlayerPaused,
        ResourceDepleted,
        OutputFull,
        OutsideLogistics,
        MissingInput
    }

    public sealed class GrayboxBuildingProductionState3D
    {
        private bool manuallyPaused;
        private bool logisticsConnected = true;

        private GrayboxBuildingProductionState3D(
            string stableInstanceId,
            GrayboxProductionDefinition3D definition,
            GrayboxBuildingCache3D cache,
            string boundNodeId,
            int boundNodeX,
            int boundNodeY,
            string boundNodeResourceId)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId))
                throw new ArgumentException(
                    "A stable instance ID is required.",
                    nameof(stableInstanceId));
            StableInstanceId = stableInstanceId;
            Definition = definition ??
                         throw new ArgumentNullException(nameof(definition));
            Cache = cache ?? throw new ArgumentNullException(nameof(cache));
            BoundNodeId = boundNodeId;
            BoundNodeX = boundNodeX;
            BoundNodeY = boundNodeY;
            BoundNodeResourceId = boundNodeResourceId;
        }

        public string StableInstanceId { get; }
        public GrayboxProductionDefinition3D Definition { get; }
        public GrayboxBuildingCache3D Cache { get; }
        public string BoundNodeId { get; }
        public int BoundNodeX { get; }
        public int BoundNodeY { get; }
        public string BoundNodeResourceId { get; }
        public float ProgressSeconds { get; internal set; }
        public GrayboxProductionStopReason3D StopReason { get; internal set; }
        public int CompletedCycles { get; internal set; }
        internal bool CycleActive { get; set; }
        internal bool ManuallyPaused => manuallyPaused;
        public bool LogisticsConnected => logisticsConnected;

        public static GrayboxBuildingProductionState3D CreateRecipe(
            string stableInstanceId,
            GrayboxProductionDefinition3D definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (definition.Kind != GrayboxProductionKind3D.Recipe)
                throw new ArgumentException(
                    "The definition is not a recipe.",
                    nameof(definition));

            return new GrayboxBuildingProductionState3D(
                stableInstanceId,
                definition,
                new GrayboxBuildingCache3D(
                    definition.InputResourceId,
                    definition.InputCapacity,
                    definition.OutputResourceId,
                    definition.OutputCapacity),
                null,
                -1,
                -1,
                null);
        }

        public static GrayboxBuildingProductionState3D CreateExtraction(
            string stableInstanceId,
            GrayboxProductionDefinition3D definition,
            string boundNodeId,
            int boundNodeX,
            int boundNodeY,
            string boundNodeResourceId)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (definition.Kind != GrayboxProductionKind3D.Extraction)
                throw new ArgumentException(
                    "The definition is not extraction.",
                    nameof(definition));
            if (string.IsNullOrWhiteSpace(boundNodeId))
                throw new ArgumentException(
                    "A bound node ID is required.",
                    nameof(boundNodeId));
            if (boundNodeX < 0)
                throw new ArgumentOutOfRangeException(nameof(boundNodeX));
            if (boundNodeY < 0)
                throw new ArgumentOutOfRangeException(nameof(boundNodeY));
            if (string.IsNullOrWhiteSpace(boundNodeResourceId))
                throw new ArgumentException(
                    "A bound node resource is required.",
                    nameof(boundNodeResourceId));

            return new GrayboxBuildingProductionState3D(
                stableInstanceId,
                definition,
                new GrayboxBuildingCache3D(
                    null,
                    0,
                    boundNodeResourceId,
                    definition.OutputCapacity),
                boundNodeId,
                boundNodeX,
                boundNodeY,
                boundNodeResourceId);
        }

        public void SetManuallyPaused(bool paused)
        {
            manuallyPaused = paused;
        }

        public void SetLogisticsConnected(bool connected)
        {
            logisticsConnected = connected;
        }
    }
}
