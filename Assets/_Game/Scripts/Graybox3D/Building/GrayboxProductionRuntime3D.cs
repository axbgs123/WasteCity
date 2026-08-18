using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProductionRuntime3D
    {
        private readonly Dictionary<string, BuildingProductionState> stateById =
            new Dictionary<string, BuildingProductionState>(StringComparer.Ordinal);
        private readonly List<GrayboxBuildingInstance3D> orderedInstances =
            new List<GrayboxBuildingInstance3D>();
        private readonly List<BuildingProductionState> states =
            new List<BuildingProductionState>();
        private readonly List<BuildingProductionState> runnableStates =
            new List<BuildingProductionState>();
        private readonly HashSet<string> retainedStateIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> removedStateIds = new List<string>();
        private readonly HashSet<string> retainedWarehouseIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> warehouseIds = new List<string>();
        private readonly ReadOnlyCollection<BuildingProductionState> readOnlyStates;
        private readonly ReadOnlyCollection<BuildingProductionState>
            readOnlyRunnableStates;

        public GrayboxProductionRuntime3D()
        {
            readOnlyStates = new ReadOnlyCollection<BuildingProductionState>(states);
            readOnlyRunnableStates =
                new ReadOnlyCollection<BuildingProductionState>(runnableStates);
        }

        public IReadOnlyList<BuildingProductionState> States => readOnlyStates;
        public IReadOnlyList<BuildingProductionState> RunnableStates =>
            readOnlyRunnableStates;
        public int ActiveWarehouseCount { get; private set; }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius)
        {
            SynchronizeCore(
                instances,
                cityMode,
                cityX,
                cityY,
                groundRadius,
                cityStorage: null);
        }

        public void Synchronize(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            CityResourceStorageModel cityStorage)
        {
            if (cityStorage == null)
                throw new ArgumentNullException(nameof(cityStorage));
            SynchronizeCore(
                instances,
                cityMode,
                cityX,
                cityY,
                groundRadius,
                cityStorage);
        }

        private void SynchronizeCore(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            CityResourceStorageModel cityStorage)
        {
            orderedInstances.Clear();
            states.Clear();
            runnableStates.Clear();
            retainedStateIds.Clear();
            retainedWarehouseIds.Clear();
            ActiveWarehouseCount = 0;

            if (instances != null)
            {
                for (int index = 0; index < instances.Count; index++)
                {
                    if (instances[index] != null)
                        orderedInstances.Add(instances[index]);
                }
            }

            orderedInstances.Sort((left, right) => string.Compare(
                left.StableInstanceId,
                right.StableInstanceId,
                StringComparison.Ordinal));

            for (int index = 0; index < orderedInstances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = orderedInstances[index];
                if (cityStorage == null)
                {
                    if (GrayboxProductionEligibility3D.IsActiveWarehouse(instance))
                        ActiveWarehouseCount++;
                }
                else
                {
                    SynchronizeWarehouse(
                        instance,
                        cityMode,
                        cityX,
                        cityY,
                        groundRadius,
                        cityStorage);
                }

                if (!GrayboxBuildingOperationalAccess3D.CanRetainState(instance) ||
                    !FormalProductionDefinitionCatalog.TryGetByBuildingId(
                        instance.Placement.Definition.Id.Value,
                        out FormalProductionDefinition definition) ||
                    (definition.UsesBoundResourceNode &&
                     !instance.BoundResourceNode.IsValid))
                {
                    continue;
                }

                retainedStateIds.Add(instance.StableInstanceId);
                if (!stateById.TryGetValue(
                        instance.StableInstanceId,
                        out BuildingProductionState state))
                {
                    ResourceNodeBinding binding = instance.BoundResourceNode;
                    state = new BuildingProductionState(
                        instance.StableInstanceId,
                        definition,
                        binding.StableId,
                        binding.X,
                        binding.Y);
                    stateById.Add(instance.StableInstanceId, state);
                }

                bool canRun =
                    GrayboxBuildingOperationalAccess3D.CanRunLocally(
                        instance,
                        cityMode);
                state.SetLogisticsConnected(
                    canRun &&
                    GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                        instance,
                        cityMode,
                        cityX,
                        cityY,
                        groundRadius));
                states.Add(state);
                if (canRun)
                    runnableStates.Add(state);
            }

            removedStateIds.Clear();
            foreach (string stableInstanceId in stateById.Keys)
            {
                if (!retainedStateIds.Contains(stableInstanceId))
                    removedStateIds.Add(stableInstanceId);
            }
            for (int index = 0; index < removedStateIds.Count; index++)
                stateById.Remove(removedStateIds[index]);

            if (cityStorage != null)
            {
                cityStorage.CopyWarehouseIds(warehouseIds);
                for (int index = 0; index < warehouseIds.Count; index++)
                {
                    string stableInstanceId = warehouseIds[index];
                    if (retainedWarehouseIds.Contains(stableInstanceId))
                        continue;
                    cityStorage.TrySetWarehouseConnected(
                        stableInstanceId,
                        connected: false);
                    cityStorage.TryRemoveWarehouse(stableInstanceId);
                }
            }
        }

        private void SynchronizeWarehouse(
            GrayboxBuildingInstance3D instance,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            CityResourceStorageModel cityStorage)
        {
            if (!IsWarehouse(instance)) return;
            bool canOwnStorage =
                instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned;
            if (canOwnStorage &&
                !cityStorage.ContainsWarehouse(instance.StableInstanceId))
            {
                cityStorage.TryRegisterWarehouse(
                    instance.StableInstanceId,
                    connected: false);
            }
            if (!cityStorage.ContainsWarehouse(instance.StableInstanceId))
                return;

            retainedWarehouseIds.Add(instance.StableInstanceId);
            bool connected =
                GrayboxProductionEligibility3D.IsActiveWarehouse(instance) &&
                GrayboxBuildingOperationalAccess3D.CanRunLocally(
                    instance,
                    cityMode) &&
                GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                    instance,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius);
            cityStorage.TrySetWarehouseConnected(
                instance.StableInstanceId,
                connected);
            if (connected) ActiveWarehouseCount++;
        }

        private static bool IsWarehouse(GrayboxBuildingInstance3D instance)
        {
            return instance?.Placement?.Definition != null &&
                string.Equals(
                    instance.Placement.Definition.Id.Value,
                    BuildingCatalog.Warehouse.Id.Value,
                    StringComparison.Ordinal);
        }

        public bool TryGetState(
            string stableInstanceId,
            out BuildingProductionState state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                stateById.TryGetValue(stableInstanceId, out state);
        }

        public ProductionObservabilitySnapshot CaptureObservability(
            ulong revision,
            WorldMapModel world)
        {
            return ProductionObservabilitySnapshot.Capture(
                revision,
                readOnlyStates,
                world,
                ActiveWarehouseCount);
        }

        public ulong ComputeObservabilityContentHash(WorldMapModel world)
        {
            ulong value = 1469598103934665603ul;
            Mix(ref value, ActiveWarehouseCount);
            Mix(ref value, states.Count);
            for (var index = 0; index < states.Count; index++)
            {
                BuildingProductionState state = states[index];
                FormalProductionDefinition definition = state.Definition;
                string outputResourceId =
                    ProductionObservabilitySnapshot.ResolveOutputResourceId(
                        state,
                        world);
                Mix(ref value, state.StableInstanceId);
                Mix(ref value, definition.Id);
                Mix(ref value, definition.BuildingId);
                Mix(ref value, definition.DurationSeconds.GetHashCode());
                Mix(ref value, definition.InputResourceId);
                Mix(ref value, definition.InputAmount);
                Mix(ref value, definition.OutputAmount);
                Mix(ref value, definition.InputCapacity);
                Mix(ref value, definition.OutputCapacity);
                Mix(ref value, outputResourceId);
                Mix(ref value, string.IsNullOrEmpty(definition.InputResourceId)
                    ? 0
                    : state.Input.Get(definition.InputResourceId));
                Mix(ref value, string.IsNullOrEmpty(outputResourceId)
                    ? 0
                    : state.Output.Get(outputResourceId));
                Mix(ref value, state.ProgressSeconds.GetHashCode());
                Mix(ref value, state.HasReservedInputs);
                Mix(ref value, state.IsLogisticsConnected);
                Mix(ref value, state.IsPlayerPaused);
                Mix(ref value, (int)state.StopReason);
                Mix(ref value, state.BoundResourceNodeId);
                Mix(ref value, state.BoundNodeX);
                Mix(ref value, state.BoundNodeY);
                if (definition.UsesBoundResourceNode && world != null &&
                    state.BoundNodeX >= 0 && state.BoundNodeY >= 0 &&
                    state.BoundNodeX < world.Width &&
                    state.BoundNodeY < world.Height)
                {
                    WorldCell cell = world.Get(
                        state.BoundNodeX,
                        state.BoundNodeY);
                    Mix(ref value, cell.ResourceId);
                    Mix(ref value, Math.Max(0, cell.ResourceAmount));
                }
            }
            return value;
        }

        private static void Mix(ref ulong value, bool item)
        {
            Mix(ref value, item ? 1 : 0);
        }

        private static void Mix(ref ulong value, int item)
        {
            unchecked
            {
                value ^= (uint)item;
                value *= 1099511628211ul;
            }
        }

        private static void Mix(ref ulong value, string item)
        {
            Mix(ref value,
                string.IsNullOrEmpty(item) ? 0 : item.GetHashCode());
        }

    }
}
