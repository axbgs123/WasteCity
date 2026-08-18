using System;

namespace WasteCity.Economy
{
    public enum ProductionStopReason
    {
        None,
        MissingInput,
        OutputFull,
        OutOfLogistics,
        Depleted,
        PlayerPaused
    }

    public sealed class BuildingProductionState
    {
        public BuildingProductionState(
            string stableInstanceId,
            FormalProductionDefinition definition,
            string boundResourceNodeId = null,
            int boundNodeX = -1,
            int boundNodeY = -1)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId))
                throw new ArgumentException("A stable building instance ID is required.", nameof(stableInstanceId));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (definition.UsesBoundResourceNode &&
                (string.IsNullOrWhiteSpace(boundResourceNodeId) ||
                 boundNodeX < 0 || boundNodeY < 0))
            {
                throw new ArgumentException("Extraction production requires a bound resource node.");
            }

            StableInstanceId = stableInstanceId;
            BoundResourceNodeId = boundResourceNodeId;
            BoundNodeX = boundNodeX;
            BoundNodeY = boundNodeY;
            Input = new ResourceInventory(definition.InputCapacity);
            Output = new ResourceInventory(definition.OutputCapacity);
            InputCapacityPolicy = new ResourceCapacityPolicy(
                definition.InputCapacity,
                0);
        }

        public string StableInstanceId { get; }
        public FormalProductionDefinition Definition { get; }
        public ResourceInventory Input { get; }
        public ResourceInventory Output { get; }
        public ResourceCapacityPolicy InputCapacityPolicy { get; }
        public string BoundResourceNodeId { get; }
        public int BoundNodeX { get; }
        public int BoundNodeY { get; }
        public float ProgressSeconds { get; private set; }
        public float ProgressNormalized => Math.Min(
            1f,
            ProgressSeconds / Definition.DurationSeconds);
        public bool HasReservedInputs { get; private set; }
        public bool IsLogisticsConnected { get; private set; }
        public bool IsPlayerPaused { get; private set; }
        public ProductionStopReason StopReason { get; private set; }

        public void SetLogisticsConnected(bool connected)
        {
            IsLogisticsConnected = connected;
        }

        public void SetPlayerPaused(bool paused)
        {
            IsPlayerPaused = paused;
            if (paused)
                StopReason = ProductionStopReason.PlayerPaused;
            else if (StopReason == ProductionStopReason.PlayerPaused)
                StopReason = ProductionStopReason.None;
        }

        internal void BeginCycle()
        {
            HasReservedInputs = true;
            ProgressSeconds = 0f;
            StopReason = ProductionStopReason.None;
        }

        internal void Advance(float deltaSeconds)
        {
            ProgressSeconds = Math.Min(
                Definition.DurationSeconds,
                ProgressSeconds + Math.Max(0f, deltaSeconds));
        }

        internal void CompleteCycle()
        {
            HasReservedInputs = false;
            ProgressSeconds = 0f;
        }

        internal void SetStopReason(ProductionStopReason reason)
        {
            StopReason = reason;
        }
    }
}
