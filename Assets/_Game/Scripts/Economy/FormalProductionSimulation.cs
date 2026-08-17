using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.World;

namespace WasteCity.Economy
{
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
            bool globallyPaused)
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
                AdvanceProduction(state, safeDelta, world, cityInventory);
            }
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

            string outputResourceId = ResolveOutputResourceId(state, world);
            if (!string.IsNullOrEmpty(outputResourceId))
            {
                int outputAmount = state.Output.Get(outputResourceId);
                if (outputAmount > 0)
                {
                    ResourceTransaction.Transfer(
                        state.Output,
                        cityInventory,
                        cityCapacity,
                        activeWarehouseCount,
                        outputResourceId,
                        outputAmount);
                }
            }

            FormalProductionDefinition definition = state.Definition;
            if (definition.InputAmount <= 0 ||
                string.IsNullOrEmpty(definition.InputResourceId))
            {
                return;
            }

            int missing = Math.Max(
                0,
                definition.InputCapacity -
                state.Input.Get(definition.InputResourceId));
            if (missing == 0)
                return;

            ResourceTransaction.Transfer(
                cityInventory,
                state.Input,
                new ResourceCapacityPolicy(definition.InputCapacity, 0),
                0,
                definition.InputResourceId,
                missing);
        }

        private static void AdvanceProduction(
            BuildingProductionState state,
            float deltaSeconds,
            WorldMapModel world,
            ResourceInventory cityInventory)
        {
            if (state.IsPlayerPaused)
            {
                state.SetStopReason(ProductionStopReason.PlayerPaused);
                return;
            }

            float remaining = deltaSeconds;
            while (true)
            {
                if (!state.HasReservedInputs &&
                    !TryBeginCycle(state, world, cityInventory))
                {
                    return;
                }

                float needed = Math.Max(
                    0f,
                    state.Definition.DurationSeconds - state.ProgressSeconds);
                if (remaining < needed)
                {
                    state.Advance(remaining);
                    state.SetStopReason(ProductionStopReason.None);
                    return;
                }

                state.Advance(needed);
                remaining -= needed;
                if (!TryCompleteCycle(state, world))
                    return;

                if (remaining <= 0f)
                {
                    TryBeginCycle(state, world, cityInventory);
                    return;
                }
            }
        }

        private static bool TryBeginCycle(
            BuildingProductionState state,
            WorldMapModel world,
            ResourceInventory cityInventory)
        {
            FormalProductionDefinition definition = state.Definition;
            string outputResourceId = ResolveOutputResourceId(state, world);
            if (string.IsNullOrEmpty(outputResourceId))
            {
                state.SetStopReason(ProductionStopReason.Depleted);
                return false;
            }

            if (state.Output.Get(outputResourceId) + definition.OutputAmount >
                definition.OutputCapacity)
            {
                state.SetStopReason(ProductionStopReason.OutputFull);
                return false;
            }

            if (definition.UsesBoundResourceNode)
            {
                if (!HasHarvestableCompatibleNode(state, world))
                {
                    state.SetStopReason(ProductionStopReason.Depleted);
                    return false;
                }

                state.BeginCycle();
                return true;
            }

            if (state.Input.Get(definition.InputResourceId) <
                definition.InputAmount)
            {
                int localShortfall = Math.Max(
                    0,
                    definition.InputAmount -
                    state.Input.Get(definition.InputResourceId));
                bool cityCouldSupply = cityInventory != null &&
                    cityInventory.Get(definition.InputResourceId) >=
                    localShortfall;
                state.SetStopReason(
                    !state.IsLogisticsConnected && cityCouldSupply
                        ? ProductionStopReason.OutOfLogistics
                        : ProductionStopReason.MissingInput);
                return false;
            }

            if (!ResourceTransaction.TrySpendAll(
                    state.Input,
                    new ResourceAmount(
                        definition.InputResourceId,
                        definition.InputAmount)))
            {
                state.SetStopReason(ProductionStopReason.MissingInput);
                return false;
            }

            state.BeginCycle();
            return true;
        }

        private static bool TryCompleteCycle(
            BuildingProductionState state,
            WorldMapModel world)
        {
            FormalProductionDefinition definition = state.Definition;
            if (definition.UsesBoundResourceNode)
            {
                string pendingResourceId = ResolveOutputResourceId(state, world);
                if (string.IsNullOrEmpty(pendingResourceId))
                {
                    state.CompleteCycle();
                    state.SetStopReason(ProductionStopReason.Depleted);
                    return false;
                }

                if (state.Output.Get(pendingResourceId) +
                    definition.OutputAmount > definition.OutputCapacity)
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
                        definition.OutputAmount,
                        out resourceId);
                if (harvested != definition.OutputAmount ||
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
            else if (state.Output.Add(
                         definition.OutputResourceId,
                         definition.OutputAmount) != definition.OutputAmount)
            {
                state.SetStopReason(ProductionStopReason.OutputFull);
                return false;
            }

            state.CompleteCycle();
            return true;
        }

        private static bool HasHarvestableCompatibleNode(
            BuildingProductionState state,
            WorldMapModel world)
        {
            if (world == null ||
                state.BoundNodeX < 0 || state.BoundNodeY < 0 ||
                state.BoundNodeX >= world.Width ||
                state.BoundNodeY >= world.Height)
            {
                return false;
            }

            WorldCell cell = world.Get(state.BoundNodeX, state.BoundNodeY);
            return cell.ResourceAmount >= state.Definition.OutputAmount &&
                BuildingResourceNodeCompatibilityRules.IsCompatible(
                    BuildingCatalog.MiningStation,
                    cell.ResourceId);
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
