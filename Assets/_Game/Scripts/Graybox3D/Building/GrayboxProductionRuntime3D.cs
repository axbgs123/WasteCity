using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;

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
            orderedInstances.Clear();
            states.Clear();
            runnableStates.Clear();
            retainedStateIds.Clear();
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
                if (GrayboxProductionEligibility3D.IsActiveWarehouse(instance))
                    ActiveWarehouseCount++;

                if (!CanRetainProductionState(instance) ||
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

                bool canRun = !instance.IsEvacuationLocked &&
                    BuildingMobilityRules.CanOperate(
                        instance.Placement.Definition,
                        instance.Placement.Site,
                        cityMode);
                state.SetLogisticsConnected(
                    canRun && IsLogisticsConnected(
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
        }

        public bool TryGetState(
            string stableInstanceId,
            out BuildingProductionState state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                stateById.TryGetValue(stableInstanceId, out state);
        }

        private static bool CanRetainProductionState(
            GrayboxBuildingInstance3D instance)
        {
            return instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned &&
                instance.Placement?.Definition != null;
        }

        private static bool IsLogisticsConnected(
            GrayboxBuildingInstance3D instance,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius)
        {
            PlacedBuilding placement = instance.Placement;
            if (placement.Site == BuildingSite.InnerCity)
                return true;
            if (placement.Site != BuildingSite.Ground ||
                cityMode != CityMode.Fortress)
            {
                return false;
            }

            return BuildingRangeRules.IsGroundFootprintInRange(
                placement.Definition,
                placement.X,
                placement.Y,
                placement.Orientation,
                cityX,
                cityY,
                groundRadius);
        }
    }
}
