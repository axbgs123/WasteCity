using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.World;

namespace WasteCity.Economy
{
    public interface IFormalProductionOutputModifier
    {
        int OutputMultiplier(string stableInstanceId);
    }

    public interface IFormalProductionResearchModifier
    {
        float ResolveCycleDurationSeconds(
            string recipeId,
            float baseDurationSeconds);
    }

    public sealed class FormalProductionSimulation
    {
        private readonly List<BuildingProductionState> orderedStates =
            new List<BuildingProductionState>();

        public void Tick(
            IReadOnlyList<BuildingProductionState> states,
            float deltaSeconds,
            WorldMapModel world,
            ResourceInventory cityInventory,
            ResourceCapacityPolicy cityCapacity,
            int activeWarehouseCount,
            bool globallyPaused,
            IFormalProductionOutputModifier outputModifier = null)
        {
            Tick(
                states,
                deltaSeconds,
                world,
                cityInventory,
                cityCapacity,
                activeWarehouseCount,
                globallyPaused,
                outputModifier,
                researchModifier: null);
        }

        public void Tick(
            IReadOnlyList<BuildingProductionState> states,
            float deltaSeconds,
            WorldMapModel world,
            ResourceInventory cityInventory,
            ResourceCapacityPolicy cityCapacity,
            int activeWarehouseCount,
            bool globallyPaused,
            IFormalProductionOutputModifier outputModifier,
            IFormalProductionResearchModifier researchModifier)
        {
            if (states == null || cityInventory == null || cityCapacity == null)
                return;

            orderedStates.Clear();
            for (int index = 0; index < states.Count; index++)
            {
                if (states[index] != null)
                    orderedStates.Add(states[index]);
            }

            orderedStates.Sort((left, right) => string.Compare(
                left.StableInstanceId,
                right.StableInstanceId,
                StringComparison.Ordinal));

            if (globallyPaused)
                return;

            float safeDelta = Math.Max(0f, deltaSeconds);
            for (int index = 0; index < orderedStates.Count; index++)
            {
                BuildingProductionState state = orderedStates[index];
                RunLogistics(
                    state,
                    world,
                    cityInventory,
                    cityCapacity,
                    activeWarehouseCount);
                AdvanceProduction(
                    state,
                    safeDelta,
                    world,
                    cityInventory,
                    outputModifier: outputModifier,
                    researchModifier: researchModifier);
            }
        }

        public void Tick(
            IReadOnlyList<BuildingProductionState> states,
            float deltaSeconds,
            WorldMapModel world,
            CityResourceStorageModel cityStorage,
            bool globallyPaused,
            IFormalProductionOutputModifier outputModifier = null,
            IFormalProductionResearchModifier researchModifier = null)
        {
            if (states == null || cityStorage == null) return;

            PrepareOrderedStates(states);
            if (globallyPaused) return;

            float safeDelta = Math.Max(0f, deltaSeconds);
            for (int index = 0; index < orderedStates.Count; index++)
            {
                BuildingProductionState state = orderedStates[index];
                using (cityStorage.AttributeChanges(
                           new ResourceChangeAttribution(
                               ResourceChangeAttributionKind.Production,
                               state.Definition.BuildingId)))
                {
                    RunLogistics(state, world, cityStorage);
                }
                AdvanceProduction(
                    state,
                    safeDelta,
                    world,
                    cityInventory: null,
                    cityStorage: cityStorage,
                    outputModifier: outputModifier,
                    researchModifier: researchModifier);
            }
        }

        private void PrepareOrderedStates(
            IReadOnlyList<BuildingProductionState> states)
        {
            orderedStates.Clear();
            for (int index = 0; index < states.Count; index++)
            {
                if (states[index] != null)
                    orderedStates.Add(states[index]);
            }
            orderedStates.Sort((left, right) => string.Compare(
                left.StableInstanceId,
                right.StableInstanceId,
                StringComparison.Ordinal));
        }

        private static void RunLogistics(
            BuildingProductionState state,
            WorldMapModel world,
            ResourceInventory cityInventory,
            ResourceCapacityPolicy cityCapacity,
            int activeWarehouseCount)
        {
            if (!state.IsLogisticsConnected)
                return;

            FormalProductionDefinition definition = state.Definition;
            if (definition.UsesBoundResourceNode)
            {
                TransferOutputToCity(
                    state,
                    cityInventory,
                    cityCapacity,
                    activeWarehouseCount,
                    ResolveOutputResourceId(state, world));
            }
            else
            {
                for (var index = 0; index < definition.Outputs.Count; index++)
                {
                    TransferOutputToCity(
                        state,
                        cityInventory,
                        cityCapacity,
                        activeWarehouseCount,
                        definition.Outputs[index].ResourceId);
                }
            }

            for (var index = 0; index < definition.Inputs.Count; index++)
            {
                string resourceId = definition.Inputs[index].ResourceId;
                int missing = Math.Max(
                    0,
                    definition.InputCapacity - state.Input.Get(resourceId));
                if (missing == 0) continue;

                using (cityInventory.AttributeChanges(
                           new ResourceChangeAttribution(
                               ResourceChangeAttributionKind.Production,
                               definition.BuildingId)))
                {
                    ResourceTransaction.Transfer(
                        cityInventory,
                        state.Input,
                        state.InputCapacityPolicy,
                        0,
                        resourceId,
                        missing);
                }
            }
        }

        private static void RunLogistics(
            BuildingProductionState state,
            WorldMapModel world,
            CityResourceStorageModel cityStorage)
        {
            if (!state.IsLogisticsConnected) return;

            FormalProductionDefinition definition = state.Definition;
            if (definition.UsesBoundResourceNode)
            {
                TransferOutputToCity(
                    state,
                    cityStorage,
                    ResolveOutputResourceId(state, world));
            }
            else
            {
                for (var index = 0; index < definition.Outputs.Count; index++)
                {
                    TransferOutputToCity(
                        state,
                        cityStorage,
                        definition.Outputs[index].ResourceId);
                }
            }

            for (var index = 0; index < definition.Inputs.Count; index++)
            {
                string resourceId = definition.Inputs[index].ResourceId;
                int missing = Math.Max(
                    0,
                    definition.InputCapacity - state.Input.Get(resourceId));
                int supplied = Math.Min(
                    missing,
                    cityStorage.GetNetworkAmount(resourceId));
                if (supplied <= 0 || !cityStorage.TrySpendFromNetwork(
                        resourceId,
                        supplied))
                {
                    continue;
                }
                if (state.Input.Add(resourceId, supplied) != supplied)
                    cityStorage.AddToNetwork(resourceId, supplied);
            }
        }

        private static void TransferOutputToCity(
            BuildingProductionState state,
            ResourceInventory cityInventory,
            ResourceCapacityPolicy cityCapacity,
            int activeWarehouseCount,
            string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return;
            int outputAmount = state.Output.Get(resourceId);
            if (outputAmount <= 0) return;
            using (cityInventory.AttributeChanges(
                       new ResourceChangeAttribution(
                           ResourceChangeAttributionKind.Production,
                           state.Definition.BuildingId)))
            {
                ResourceTransaction.Transfer(
                    state.Output,
                    cityInventory,
                    cityCapacity,
                    activeWarehouseCount,
                    resourceId,
                    outputAmount);
            }
        }

        private static void TransferOutputToCity(
            BuildingProductionState state,
            CityResourceStorageModel cityStorage,
            string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return;
            int outputAmount = state.Output.Get(resourceId);
            int moved = Math.Min(
                outputAmount,
                cityStorage.GetNetworkAcceptableSpace(resourceId));
            if (moved <= 0) return;

            int before = outputAmount;
            if (!state.Output.TrySpend(resourceId, moved)) return;
            int accepted = cityStorage.AddToNetwork(resourceId, moved);
            if (accepted == moved) return;
            if (accepted > 0)
                cityStorage.TrySpendFromNetwork(resourceId, accepted);
            state.Output.Restore(resourceId, before);
        }

        private static void AdvanceProduction(
            BuildingProductionState state,
            float deltaSeconds,
            WorldMapModel world,
            ResourceInventory cityInventory,
            CityResourceStorageModel cityStorage = null,
            IFormalProductionOutputModifier outputModifier = null,
            IFormalProductionResearchModifier researchModifier = null)
        {
            if (state.IsPlayerPaused)
            {
                state.SetStopReason(ProductionStopReason.PlayerPaused);
                return;
            }

            float remaining = deltaSeconds;
            float cycleDuration = ResolveCycleDuration(
                state.Definition,
                researchModifier);
            while (true)
            {
                if (!state.HasReservedInputs &&
                    !TryBeginCycle(
                        state,
                        world,
                        cityInventory,
                        cityStorage,
                        outputModifier))
                {
                    return;
                }

                float needed = Math.Max(
                    0f,
                    cycleDuration - state.ProgressSeconds);
                if (remaining < needed)
                {
                    state.Advance(remaining);
                    state.SetStopReason(ProductionStopReason.None);
                    return;
                }

                state.Advance(needed);
                remaining -= needed;
                if (!TryCompleteCycle(state, world, outputModifier))
                    return;

                if (remaining <= 0f)
                {
                    TryBeginCycle(
                        state,
                        world,
                        cityInventory,
                        cityStorage,
                        outputModifier);
                    return;
                }
            }
        }

        private static bool TryBeginCycle(
            BuildingProductionState state,
            WorldMapModel world,
            ResourceInventory cityInventory,
            CityResourceStorageModel cityStorage = null,
            IFormalProductionOutputModifier outputModifier = null)
        {
            FormalProductionDefinition definition = state.Definition;
            int outputMultiplier = ResolveOutputMultiplier(
                state,
                outputModifier);
            string outputResourceId = ResolveOutputResourceId(state, world);
            if (string.IsNullOrEmpty(outputResourceId))
            {
                state.SetStopReason(ProductionStopReason.Depleted);
                return false;
            }

            if (definition.UsesBoundResourceNode &&
                !HasHarvestableCompatibleNode(
                    state,
                    world,
                    outputMultiplier))
            {
                state.SetStopReason(ProductionStopReason.Depleted);
                return false;
            }

            if (!CanStoreCycleOutputs(state, world, outputMultiplier))
            {
                state.SetStopReason(ProductionStopReason.OutputFull);
                return false;
            }

            if (definition.UsesBoundResourceNode)
            {
                state.BeginCycle();
                return true;
            }

            bool hasMissingInput = false;
            bool cityCouldSupplyEveryShortfall = true;
            for (var index = 0; index < definition.Inputs.Count; index++)
            {
                ResourceAmount input = definition.Inputs[index];
                int localShortfall = Math.Max(
                    0,
                    input.Amount - state.Input.Get(input.ResourceId));
                if (localShortfall == 0) continue;
                hasMissingInput = true;
                int cityAvailable = cityStorage != null
                    ? cityStorage.GetNetworkAmount(input.ResourceId)
                    : cityInventory == null
                        ? 0
                        : cityInventory.Get(input.ResourceId);
                if (cityAvailable < localShortfall)
                    cityCouldSupplyEveryShortfall = false;
            }
            if (hasMissingInput)
            {
                state.SetStopReason(
                    !state.IsLogisticsConnected &&
                    cityCouldSupplyEveryShortfall
                        ? ProductionStopReason.OutOfLogistics
                        : ProductionStopReason.MissingInput);
                return false;
            }

            if (!TrySpendAllInputs(state.Input, definition.Inputs))
            {
                state.SetStopReason(ProductionStopReason.MissingInput);
                return false;
            }

            state.BeginCycle();
            return true;
        }

        private static bool TryCompleteCycle(
            BuildingProductionState state,
            WorldMapModel world,
            IFormalProductionOutputModifier outputModifier)
        {
            FormalProductionDefinition definition = state.Definition;
            int outputMultiplier = ResolveOutputMultiplier(
                state,
                outputModifier);
            if (definition.UsesBoundResourceNode)
            {
                string pendingResourceId = ResolveOutputResourceId(state, world);
                if (string.IsNullOrEmpty(pendingResourceId))
                {
                    state.CompleteCycle();
                    state.SetStopReason(ProductionStopReason.Depleted);
                    return false;
                }

                int multipliedOutput =
                    definition.OutputAmount * outputMultiplier;
                if (state.Output.Get(pendingResourceId) +
                    multipliedOutput > definition.OutputCapacity)
                {
                    state.SetStopReason(ProductionStopReason.OutputFull);
                    return false;
                }

                string resourceId = null;
                int harvested = world == null
                    ? 0
                    : world.Harvest(
                        state.BoundNodeX,
                        state.BoundNodeY,
                        multipliedOutput,
                        out resourceId);
                if (harvested != multipliedOutput ||
                    !BuildingResourceNodeCompatibilityRules.IsCompatible(
                        BuildingCatalog.MiningStation,
                        resourceId) ||
                    state.Output.Add(resourceId, harvested) != harvested)
                {
                    state.CompleteCycle();
                    state.SetStopReason(ProductionStopReason.Depleted);
                    return false;
                }
            }
            else if (!TryStoreAllOutputs(
                         state.Output,
                         definition.Outputs,
                         outputMultiplier))
            {
                state.SetStopReason(ProductionStopReason.OutputFull);
                return false;
            }

            state.CompleteCycle();
            return true;
        }

        private static bool CanStoreCycleOutputs(
            BuildingProductionState state,
            WorldMapModel world,
            int outputMultiplier)
        {
            FormalProductionDefinition definition = state.Definition;
            if (definition.UsesBoundResourceNode)
            {
                string resourceId = ResolveOutputResourceId(state, world);
                return !string.IsNullOrEmpty(resourceId) &&
                    state.Output.Get(resourceId) +
                    definition.OutputAmount * outputMultiplier <=
                    definition.OutputCapacity;
            }

            return CanStoreAllOutputs(
                state.Output,
                definition.Outputs,
                outputMultiplier);
        }

        private static bool CanStoreAllOutputs(
            ResourceInventory inventory,
            IReadOnlyList<ResourceAmount> outputs,
            int outputMultiplier = 1)
        {
            if (inventory == null || outputs == null || outputs.Count == 0)
                return false;
            for (var index = 0; index < outputs.Count; index++)
            {
                ResourceAmount output = outputs[index];
                if (string.IsNullOrWhiteSpace(output.ResourceId) ||
                    output.Amount <= 0)
                {
                    return false;
                }

                int total = AggregateAmount(outputs, output.ResourceId);
                if (total > 0) total *= outputMultiplier;
                if (total <= 0) return false;
                if (inventory.Get(output.ResourceId) + total >
                    inventory.CapacityPerResource)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryStoreAllOutputs(
            ResourceInventory inventory,
            IReadOnlyList<ResourceAmount> outputs,
            int outputMultiplier = 1)
        {
            if (!CanStoreAllOutputs(
                    inventory,
                    outputs,
                    outputMultiplier)) return false;

            string previousResourceId = null;
            for (var committed = 0; committed < outputs.Count; committed++)
            {
                string resourceId = FindNextResourceId(
                    outputs,
                    previousResourceId);
                if (resourceId == null) return true;

                int total =
                    AggregateAmount(outputs, resourceId) * outputMultiplier;
                int before = inventory.Get(resourceId);
                if (inventory.Add(resourceId, total) != total)
                {
                    RollBackStoredOutputs(
                        inventory,
                        outputs,
                        previousResourceId,
                        outputMultiplier);
                    inventory.Restore(resourceId, before);
                    return false;
                }
                previousResourceId = resourceId;
            }
            return true;
        }

        private static bool TrySpendAllInputs(
            ResourceInventory inventory,
            IReadOnlyList<ResourceAmount> inputs)
        {
            if (inventory == null || inputs == null || inputs.Count == 0)
                return false;

            for (var index = 0; index < inputs.Count; index++)
            {
                ResourceAmount input = inputs[index];
                if (!ResourceCapacityPolicy.IsRegisteredResource(
                        input.ResourceId) || input.Amount <= 0)
                {
                    return false;
                }
                if (HasEarlierResourceId(inputs, index)) continue;

                int total = AggregateAmount(inputs, input.ResourceId);
                if (total <= 0 || inventory.Get(input.ResourceId) < total)
                    return false;
            }

            var lastCommittedIndex = -1;
            var attemptedIndex = -1;
            try
            {
                for (var index = 0; index < inputs.Count; index++)
                {
                    if (HasEarlierResourceId(inputs, index)) continue;
                    ResourceAmount input = inputs[index];
                    int total = AggregateAmount(inputs, input.ResourceId);
                    attemptedIndex = index;
                    if (!inventory.TrySpend(input.ResourceId, total))
                    {
                        RollBackSpentInputs(
                            inventory,
                            inputs,
                            lastCommittedIndex);
                        return false;
                    }
                    lastCommittedIndex = index;
                }
                return true;
            }
            catch
            {
                RollBackSpentInputs(inventory, inputs, attemptedIndex);
                return false;
            }
        }

        private static void RollBackSpentInputs(
            ResourceInventory inventory,
            IReadOnlyList<ResourceAmount> inputs,
            int lastCommittedIndex)
        {
            for (var index = 0; index <= lastCommittedIndex; index++)
            {
                if (HasEarlierResourceId(inputs, index)) continue;
                ResourceAmount input = inputs[index];
                inventory.Restore(
                    input.ResourceId,
                    inventory.Get(input.ResourceId) +
                    AggregateAmount(inputs, input.ResourceId));
            }
        }

        private static void RollBackStoredOutputs(
            ResourceInventory inventory,
            IReadOnlyList<ResourceAmount> outputs,
            string lastCommittedResourceId,
            int outputMultiplier = 1)
        {
            string previousResourceId = null;
            while (true)
            {
                string resourceId = FindNextResourceId(
                    outputs,
                    previousResourceId);
                if (resourceId == null ||
                    (lastCommittedResourceId != null &&
                     string.Compare(
                         resourceId,
                         lastCommittedResourceId,
                         StringComparison.Ordinal) > 0))
                {
                    return;
                }

                inventory.Restore(
                    resourceId,
                    inventory.Get(resourceId) -
                    AggregateAmount(outputs, resourceId) * outputMultiplier);
                previousResourceId = resourceId;
            }
        }

        private static string FindNextResourceId(
            IReadOnlyList<ResourceAmount> amounts,
            string previousResourceId)
        {
            string nextResourceId = null;
            for (var index = 0; index < amounts.Count; index++)
            {
                string resourceId = amounts[index].ResourceId;
                if (previousResourceId != null &&
                    string.Compare(
                        resourceId,
                        previousResourceId,
                        StringComparison.Ordinal) <= 0)
                {
                    continue;
                }
                if (nextResourceId == null ||
                    string.Compare(
                        resourceId,
                        nextResourceId,
                        StringComparison.Ordinal) < 0)
                {
                    nextResourceId = resourceId;
                }
            }
            return nextResourceId;
        }

        private static bool HasEarlierResourceId(
            IReadOnlyList<ResourceAmount> amounts,
            int index)
        {
            for (var candidateIndex = 0;
                 candidateIndex < index;
                 candidateIndex++)
            {
                if (string.Equals(
                        amounts[candidateIndex].ResourceId,
                        amounts[index].ResourceId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static int AggregateAmount(
            IReadOnlyList<ResourceAmount> amounts,
            string resourceId)
        {
            long total = 0;
            for (var index = 0; index < amounts.Count; index++)
            {
                ResourceAmount amount = amounts[index];
                if (string.Equals(
                        amount.ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                {
                    total += amount.Amount;
                    if (total > int.MaxValue) return -1;
                }
            }
            return (int)total;
        }

        private static bool HasHarvestableCompatibleNode(
            BuildingProductionState state,
            WorldMapModel world,
            int outputMultiplier = 1)
        {
            if (world == null ||
                state.BoundNodeX < 0 || state.BoundNodeY < 0 ||
                state.BoundNodeX >= world.Width ||
                state.BoundNodeY >= world.Height)
            {
                return false;
            }

            WorldCell cell = world.Get(state.BoundNodeX, state.BoundNodeY);
            return cell.ResourceAmount >=
                state.Definition.OutputAmount * outputMultiplier &&
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    BuildingCatalog.MiningStation,
                    cell.ResourceId);
        }

        private static int ResolveOutputMultiplier(
            BuildingProductionState state,
            IFormalProductionOutputModifier outputModifier)
        {
            if (state == null || outputModifier == null) return 1;
            return Math.Max(
                1,
                outputModifier.OutputMultiplier(state.StableInstanceId));
        }

        private static float ResolveCycleDuration(
            FormalProductionDefinition definition,
            IFormalProductionResearchModifier researchModifier)
        {
            if (definition == null) return .001f;
            float duration = researchModifier == null
                ? definition.DurationSeconds
                : researchModifier.ResolveCycleDurationSeconds(
                    definition.Id,
                    definition.DurationSeconds);
            return Math.Max(.001f, duration);
        }

        private static string ResolveOutputResourceId(
            BuildingProductionState state,
            WorldMapModel world = null)
        {
            if (!state.Definition.UsesBoundResourceNode)
                return state.Definition.OutputResourceId;
            if (world == null ||
                state.BoundNodeX < 0 || state.BoundNodeY < 0 ||
                state.BoundNodeX >= world.Width ||
                state.BoundNodeY >= world.Height)
            {
                return null;
            }

            return world.Get(state.BoundNodeX, state.BoundNodeY).ResourceId;
        }
    }
}
