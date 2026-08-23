using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public sealed class ProductionResourceObservability
    {
        internal ProductionResourceObservability(
            string resourceId,
            int currentAmount,
            int capacity,
            int amountPerCycle)
        {
            ResourceId = resourceId;
            CurrentAmount = Math.Max(0, currentAmount);
            Capacity = Math.Max(0, capacity);
            AmountPerCycle = Math.Max(0, amountPerCycle);
        }

        public string ResourceId { get; }
        public int CurrentAmount { get; }
        public int Capacity { get; }
        public int AmountPerCycle { get; }
    }

    public sealed class ProductionBuildingObservability
    {
        internal ProductionBuildingObservability(
            BuildingProductionState state,
            string outputResourceId,
            string boundResourceId,
            int boundResourceRemaining)
        {
            FormalProductionDefinition definition = state.Definition;
            StableInstanceId = state.StableInstanceId;
            ProductionDefinitionId = definition.Id;
            BuildingDefinitionId = definition.BuildingId;
            DurationSeconds = definition.DurationSeconds;
            Inputs = CaptureChannels(
                definition.Inputs,
                state.Input,
                definition.InputCapacity);
            Outputs = CaptureOutputs(
                definition,
                state,
                outputResourceId);
            ReservedInputs = CopyAmounts(state.ReservedInputs);
            InputResourceId = definition.InputResourceId;
            InputRequiredPerCycle = definition.InputAmount;
            OutputResourceId = outputResourceId;
            OutputProducedPerCycle = definition.OutputAmount;
            InputCapacity = definition.InputCapacity;
            OutputCapacity = definition.OutputCapacity;
            InputAmount = string.IsNullOrEmpty(InputResourceId)
                ? 0
                : state.Input.Get(InputResourceId);
            OutputAmount = string.IsNullOrEmpty(OutputResourceId)
                ? 0
                : state.Output.Get(OutputResourceId);
            ProgressSeconds = state.ProgressSeconds;
            ProgressNormalized = state.ProgressNormalized;
            HasReservedInputs = state.HasReservedInputs;
            IsLogisticsConnected = state.IsLogisticsConnected;
            IsPlayerPaused = state.IsPlayerPaused;
            StopReason = state.StopReason;
            BoundResourceNodeId = state.BoundResourceNodeId;
            BoundNodeX = state.BoundNodeX;
            BoundNodeY = state.BoundNodeY;
            BoundResourceId = boundResourceId;
            BoundResourceRemaining = Math.Max(0, boundResourceRemaining);
        }

        public IReadOnlyList<ProductionResourceObservability> Inputs { get; }
        public IReadOnlyList<ProductionResourceObservability> Outputs { get; }
        public IReadOnlyList<ResourceAmount> ReservedInputs { get; }

        public string StableInstanceId { get; }
        public string ProductionDefinitionId { get; }
        public string BuildingDefinitionId { get; }
        public float DurationSeconds { get; }
        public string InputResourceId { get; }
        public int InputRequiredPerCycle { get; }
        public string OutputResourceId { get; }
        public int OutputProducedPerCycle { get; }
        public int InputCapacity { get; }
        public int OutputCapacity { get; }
        public int InputAmount { get; }
        public int OutputAmount { get; }
        public float ProgressSeconds { get; }
        public float ProgressNormalized { get; }
        public bool HasReservedInputs { get; }
        public bool IsLogisticsConnected { get; }
        public bool IsPlayerPaused { get; }
        public ProductionStopReason StopReason { get; }
        public string BoundResourceNodeId { get; }
        public int BoundNodeX { get; }
        public int BoundNodeY { get; }
        public string BoundResourceId { get; }
        public int BoundResourceRemaining { get; }

        private static IReadOnlyList<ProductionResourceObservability>
            CaptureChannels(
                IReadOnlyList<ResourceAmount> definitions,
                ResourceInventory inventory,
                int capacity)
        {
            var channels = new List<ProductionResourceObservability>(
                definitions.Count);
            for (var index = 0; index < definitions.Count; index++)
            {
                ResourceAmount definition = definitions[index];
                channels.Add(new ProductionResourceObservability(
                    definition.ResourceId,
                    inventory.Get(definition.ResourceId),
                    capacity,
                    definition.Amount));
            }

            return new ReadOnlyCollection<ProductionResourceObservability>(
                channels);
        }

        private static IReadOnlyList<ProductionResourceObservability>
            CaptureOutputs(
                FormalProductionDefinition definition,
                BuildingProductionState state,
                string outputResourceId)
        {
            if (!definition.UsesBoundResourceNode)
            {
                return CaptureChannels(
                    definition.Outputs,
                    state.Output,
                    definition.OutputCapacity);
            }

            var channels = new List<ProductionResourceObservability>(1);
            if (!string.IsNullOrEmpty(outputResourceId))
            {
                channels.Add(new ProductionResourceObservability(
                    outputResourceId,
                    state.Output.Get(outputResourceId),
                    definition.OutputCapacity,
                    definition.OutputAmount));
            }

            return new ReadOnlyCollection<ProductionResourceObservability>(
                channels);
        }

        private static IReadOnlyList<ResourceAmount> CopyAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            var copy = new List<ResourceAmount>(source.Count);
            for (var index = 0; index < source.Count; index++)
                copy.Add(source[index]);
            return new ReadOnlyCollection<ResourceAmount>(copy);
        }
    }

    public sealed class ProductionObservabilitySnapshot
    {
        private static readonly ProductionObservabilitySnapshot empty =
            new ProductionObservabilitySnapshot(
                0,
                0,
                Array.Empty<ProductionBuildingObservability>());

        private readonly IReadOnlyDictionary<string,
            ProductionBuildingObservability> entryById;

        internal ProductionObservabilitySnapshot(
            ulong revision,
            int activeWarehouseCount,
            IList<ProductionBuildingObservability> entries)
        {
            Revision = revision;
            ActiveWarehouseCount = Math.Max(0, activeWarehouseCount);
            var copy = new List<ProductionBuildingObservability>(entries);
            copy.Sort((left, right) => string.Compare(
                left.StableInstanceId,
                right.StableInstanceId,
                StringComparison.Ordinal));
            Entries = new ReadOnlyCollection<ProductionBuildingObservability>(
                copy);
            var lookup = new Dictionary<string,
                ProductionBuildingObservability>(
                copy.Count,
                StringComparer.Ordinal);
            for (var index = 0; index < copy.Count; index++)
                lookup.Add(copy[index].StableInstanceId, copy[index]);
            entryById = new ReadOnlyDictionary<string,
                ProductionBuildingObservability>(lookup);
        }

        public static ProductionObservabilitySnapshot Empty => empty;
        public ulong Revision { get; }
        public int ActiveWarehouseCount { get; }
        public IReadOnlyList<ProductionBuildingObservability> Entries { get; }

        public bool TryGet(
            string stableInstanceId,
            out ProductionBuildingObservability details)
        {
            details = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                entryById.TryGetValue(stableInstanceId, out details);
        }

        internal bool HasSameContentAs(
            ProductionObservabilitySnapshot other)
        {
            if (other == null ||
                ActiveWarehouseCount != other.ActiveWarehouseCount ||
                Entries.Count != other.Entries.Count)
                return false;
            for (var index = 0; index < Entries.Count; index++)
            {
                if (!HasSameContent(
                        Entries[index],
                        other.Entries[index]))
                {
                    return false;
                }
            }
            return true;
        }

        internal ProductionObservabilitySnapshot WithRevision(ulong revision)
        {
            return new ProductionObservabilitySnapshot(
                revision,
                ActiveWarehouseCount,
                new List<ProductionBuildingObservability>(Entries));
        }

        internal static ProductionObservabilitySnapshot Capture(
            ulong revision,
            IReadOnlyList<BuildingProductionState> states,
            WorldMapModel world,
            int activeWarehouseCount)
        {
            var entries = new List<ProductionBuildingObservability>();
            if (states != null)
            {
                for (var index = 0; index < states.Count; index++)
                {
                    BuildingProductionState state = states[index];
                    if (state == null) continue;
                    ResolveResources(
                        state,
                        world,
                        out string outputResourceId,
                        out string boundResourceId,
                        out int boundResourceRemaining);
                    entries.Add(new ProductionBuildingObservability(
                        state,
                        outputResourceId,
                        boundResourceId,
                        boundResourceRemaining));
                }
            }
            return new ProductionObservabilitySnapshot(
                revision,
                activeWarehouseCount,
                entries);
        }

        internal static string ResolveOutputResourceId(
            BuildingProductionState state,
            WorldMapModel world)
        {
            ResolveResources(
                state,
                world,
                out string outputResourceId,
                out _,
                out _);
            return outputResourceId;
        }

        private static void ResolveResources(
            BuildingProductionState state,
            WorldMapModel world,
            out string outputResourceId,
            out string boundResourceId,
            out int boundResourceRemaining)
        {
            FormalProductionDefinition definition = state.Definition;
            outputResourceId = definition.OutputResourceId;
            boundResourceId = null;
            boundResourceRemaining = 0;
            if (!definition.UsesBoundResourceNode || world == null ||
                state.BoundNodeX < 0 || state.BoundNodeY < 0 ||
                state.BoundNodeX >= world.Width ||
                state.BoundNodeY >= world.Height)
            {
                return;
            }

            WorldCell cell = world.Get(state.BoundNodeX, state.BoundNodeY);
            boundResourceId = cell.ResourceId;
            boundResourceRemaining = Math.Max(0, cell.ResourceAmount);
            outputResourceId = cell.ResourceId;
        }

        private static bool HasSameContent(
            ProductionBuildingObservability left,
            ProductionBuildingObservability right)
        {
            return string.Equals(left.StableInstanceId,
                       right.StableInstanceId, StringComparison.Ordinal) &&
                string.Equals(left.ProductionDefinitionId,
                    right.ProductionDefinitionId, StringComparison.Ordinal) &&
                string.Equals(left.BuildingDefinitionId,
                    right.BuildingDefinitionId, StringComparison.Ordinal) &&
                left.DurationSeconds.Equals(right.DurationSeconds) &&
                string.Equals(left.InputResourceId,
                    right.InputResourceId, StringComparison.Ordinal) &&
                left.InputRequiredPerCycle == right.InputRequiredPerCycle &&
                string.Equals(left.OutputResourceId,
                    right.OutputResourceId, StringComparison.Ordinal) &&
                left.OutputProducedPerCycle == right.OutputProducedPerCycle &&
                left.InputCapacity == right.InputCapacity &&
                left.OutputCapacity == right.OutputCapacity &&
                left.InputAmount == right.InputAmount &&
                left.OutputAmount == right.OutputAmount &&
                left.ProgressSeconds.Equals(right.ProgressSeconds) &&
                left.ProgressNormalized.Equals(right.ProgressNormalized) &&
                left.HasReservedInputs == right.HasReservedInputs &&
                left.IsLogisticsConnected == right.IsLogisticsConnected &&
                left.IsPlayerPaused == right.IsPlayerPaused &&
                left.StopReason == right.StopReason &&
                string.Equals(left.BoundResourceNodeId,
                    right.BoundResourceNodeId, StringComparison.Ordinal) &&
                left.BoundNodeX == right.BoundNodeX &&
                left.BoundNodeY == right.BoundNodeY &&
                string.Equals(left.BoundResourceId,
                    right.BoundResourceId, StringComparison.Ordinal) &&
                left.BoundResourceRemaining == right.BoundResourceRemaining &&
                HasSameChannels(left.Inputs, right.Inputs) &&
                HasSameChannels(left.Outputs, right.Outputs) &&
                HasSameAmounts(left.ReservedInputs, right.ReservedInputs);
        }

        private static bool HasSameChannels(
            IReadOnlyList<ProductionResourceObservability> left,
            IReadOnlyList<ProductionResourceObservability> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                ProductionResourceObservability leftValue = left[index];
                ProductionResourceObservability rightValue = right[index];
                if (!string.Equals(
                        leftValue.ResourceId,
                        rightValue.ResourceId,
                        StringComparison.Ordinal) ||
                    leftValue.CurrentAmount != rightValue.CurrentAmount ||
                    leftValue.Capacity != rightValue.Capacity ||
                    leftValue.AmountPerCycle != rightValue.AmountPerCycle)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasSameAmounts(
            IReadOnlyList<ResourceAmount> left,
            IReadOnlyList<ResourceAmount> right)
        {
            if (left.Count != right.Count)
                return false;
            for (var index = 0; index < left.Count; index++)
            {
                if (!string.Equals(
                        left[index].ResourceId,
                        right[index].ResourceId,
                        StringComparison.Ordinal) ||
                    left[index].Amount != right[index].Amount)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
