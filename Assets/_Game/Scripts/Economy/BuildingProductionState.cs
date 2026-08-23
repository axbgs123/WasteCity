using System;
using System.Collections.Generic;

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
        private static readonly IReadOnlyList<ResourceAmount> EmptyAmounts =
            Array.AsReadOnly(Array.Empty<ResourceAmount>());
        private IReadOnlyList<ResourceAmount> reservedInputs = EmptyAmounts;

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
            bool hasBinding = !string.IsNullOrWhiteSpace(boundResourceNodeId);
            BoundResourceNodeId = hasBinding ? boundResourceNodeId : null;
            BoundNodeX = hasBinding ? boundNodeX : -1;
            BoundNodeY = hasBinding ? boundNodeY : -1;
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
        public IReadOnlyList<ResourceAmount> ReservedInputs => reservedInputs;
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
            reservedInputs = CaptureDefinitionInputs();
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
            reservedInputs = EmptyAmounts;
            ProgressSeconds = 0f;
        }

        public bool TryRestoreForPersistence(
            IReadOnlyList<ResourceAmount> input,
            bool hasReservedInputs,
            IReadOnlyList<ResourceAmount> reservedInputs,
            IReadOnlyList<ResourceAmount> output,
            float progressSeconds,
            bool isPlayerPaused,
            out string error)
        {
            if (float.IsNaN(progressSeconds) ||
                float.IsInfinity(progressSeconds) ||
                progressSeconds < 0f ||
                progressSeconds > Definition.DurationSeconds)
            {
                error = "Production progress must be finite and within the current cycle.";
                return false;
            }
            if (reservedInputs == null)
            {
                error = "Reserved production inputs are required.";
                return false;
            }
            if (!hasReservedInputs &&
                (progressSeconds != 0f || reservedInputs.Count != 0))
            {
                error = "Inactive production cannot contain progress or reserved inputs.";
                return false;
            }
            if (hasReservedInputs &&
                !ValidateReservedInputs(reservedInputs, out error))
            {
                return false;
            }

            var inputValidation = new ResourceInventory(
                Definition.InputCapacity);
            if (!inputValidation.TryReplaceAll(
                    input,
                    allowOverCapacity: true,
                    out error))
            {
                error = "Invalid production input inventory: " + error;
                return false;
            }
            var outputValidation = new ResourceInventory(
                Definition.OutputCapacity);
            if (!outputValidation.TryReplaceAll(
                    output,
                    allowOverCapacity: true,
                    out error))
            {
                error = "Invalid production output inventory: " + error;
                return false;
            }

            ResourceAmount[] restoredInput =
                inputValidation.CapturePositiveAmounts();
            ResourceAmount[] restoredOutput =
                outputValidation.CapturePositiveAmounts();
            IReadOnlyList<ResourceAmount> restoredReserved =
                hasReservedInputs
                    ? CaptureDefinitionInputs()
                    : EmptyAmounts;

            if (!Input.TryReplaceAll(
                    restoredInput,
                    allowOverCapacity: true,
                    out error) ||
                !Output.TryReplaceAll(
                    restoredOutput,
                    allowOverCapacity: true,
                    out error))
            {
                error = "Could not apply validated production inventory: " + error;
                return false;
            }

            HasReservedInputs = hasReservedInputs;
            this.reservedInputs = restoredReserved;
            ProgressSeconds = progressSeconds;
            IsPlayerPaused = isPlayerPaused;
            StopReason = isPlayerPaused
                ? ProductionStopReason.PlayerPaused
                : ProductionStopReason.None;
            error = string.Empty;
            return true;
        }

        internal void SetStopReason(ProductionStopReason reason)
        {
            StopReason = reason;
        }

        private IReadOnlyList<ResourceAmount> CaptureDefinitionInputs()
        {
            if (Definition.UsesBoundResourceNode ||
                Definition.Inputs.Count == 0)
            {
                return EmptyAmounts;
            }

            return Definition.Inputs;
        }

        private bool ValidateReservedInputs(
            IReadOnlyList<ResourceAmount> values,
            out string error)
        {
            if (Definition.UsesBoundResourceNode)
            {
                if (values.Count == 0)
                {
                    error = string.Empty;
                    return true;
                }

                error = "Extraction cycles cannot reserve material inputs.";
                return false;
            }

            if (values.Count != Definition.Inputs.Count)
            {
                error = "Reserved production inputs do not match the current recipe.";
                return false;
            }

            var matched = new bool[Definition.Inputs.Count];
            for (var valueIndex = 0;
                 valueIndex < values.Count;
                 valueIndex++)
            {
                ResourceAmount actual = values[valueIndex];
                bool found = false;
                for (var expectedIndex = 0;
                     expectedIndex < Definition.Inputs.Count;
                     expectedIndex++)
                {
                    ResourceAmount expected = Definition.Inputs[expectedIndex];
                    if (matched[expectedIndex] ||
                        !string.Equals(
                            actual.ResourceId,
                            expected.ResourceId,
                            StringComparison.Ordinal) ||
                        actual.Amount != expected.Amount)
                    {
                        continue;
                    }
                    matched[expectedIndex] = true;
                    found = true;
                    break;
                }
                if (!found)
                {
                    error = "Reserved production inputs do not match the current recipe.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

    }
}
