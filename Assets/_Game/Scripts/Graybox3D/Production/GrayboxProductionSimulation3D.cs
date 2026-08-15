using System;
using System.Collections.Generic;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D.Production
{
    public sealed class GrayboxProductionSimulation3D
    {
        private const float Epsilon = 0.00001f;
        private const int MaximumBoundariesPerTick = 4096;

        public void Tick(
            float baseDeltaSeconds,
            CityMode cityMode,
            WorldMapModel world,
            ResourceInventory cityInventory,
            IReadOnlyList<GrayboxBuildingProductionState3D> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            if (baseDeltaSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(baseDeltaSeconds));

            float effectiveDelta = baseDeltaSeconds *
                                   CityOperationalRules.ProductionMultiplier(
                                       cityMode);
            for (int index = 0; index < states.Count; index++)
            {
                GrayboxBuildingProductionState3D state = states[index];
                if (state == null) continue;
                AdvanceState(
                    state,
                    effectiveDelta,
                    world,
                    cityInventory,
                    states);
            }
        }

        private static void AdvanceState(
            GrayboxBuildingProductionState3D state,
            float effectiveDelta,
            WorldMapModel world,
            ResourceInventory cityInventory,
            IReadOnlyList<GrayboxBuildingProductionState3D> states)
        {
            float remaining = effectiveDelta;
            int boundaries = 0;
            while (remaining > Epsilon &&
                   boundaries++ < MaximumBoundariesPerTick)
            {
                if (!state.CycleActive)
                {
                    GrayboxProductionStopReason3D blocked =
                        EvaluateStartBlock(
                            state,
                            world,
                            cityInventory,
                            states,
                            allowTransfer: true);
                    state.StopReason = blocked;
                    if (blocked != GrayboxProductionStopReason3D.None) return;
                    if (!BeginCycle(state))
                    {
                        state.StopReason =
                            GrayboxProductionStopReason3D.MissingInput;
                        return;
                    }
                }

                if (state.ManuallyPaused)
                {
                    state.StopReason =
                        GrayboxProductionStopReason3D.PlayerPaused;
                    return;
                }

                float untilComplete = state.Definition.CycleSeconds -
                                      state.ProgressSeconds;
                float step = Math.Min(remaining, untilComplete);
                state.ProgressSeconds += step;
                remaining -= step;
                if (state.ProgressSeconds + Epsilon <
                    state.Definition.CycleSeconds)
                {
                    state.StopReason = GrayboxProductionStopReason3D.None;
                    return;
                }

                GrayboxProductionStopReason3D completionBlock =
                    EvaluateCompletionBlock(state, world);
                if (completionBlock != GrayboxProductionStopReason3D.None)
                {
                    state.ProgressSeconds = state.Definition.CycleSeconds;
                    state.StopReason = completionBlock;
                    return;
                }

                if (!CompleteCycle(state, world))
                {
                    state.StopReason =
                        state.Definition.Kind ==
                        GrayboxProductionKind3D.Extraction
                            ? GrayboxProductionStopReason3D.ResourceDepleted
                            : GrayboxProductionStopReason3D.OutputFull;
                    return;
                }
                state.ProgressSeconds = 0f;
                state.CycleActive = false;
                state.CompletedCycles++;
            }

            state.StopReason = EvaluateStartBlock(
                state,
                world,
                cityInventory,
                states,
                allowTransfer: false);
        }

        private static GrayboxProductionStopReason3D EvaluateStartBlock(
            GrayboxBuildingProductionState3D state,
            WorldMapModel world,
            ResourceInventory cityInventory,
            IReadOnlyList<GrayboxBuildingProductionState3D> states,
            bool allowTransfer)
        {
            if (state.ManuallyPaused)
                return GrayboxProductionStopReason3D.PlayerPaused;
            if (state.Definition.Kind == GrayboxProductionKind3D.Extraction &&
                IsNodeDepletedOrInvalid(state, world))
                return GrayboxProductionStopReason3D.ResourceDepleted;
            if (state.Cache.AvailableCapacity(
                    GrayboxBuildingCachePort3D.Output) <
                state.Definition.OutputAmount)
                return GrayboxProductionStopReason3D.OutputFull;
            if (state.Definition.Kind == GrayboxProductionKind3D.Extraction)
                return GrayboxProductionStopReason3D.None;
            if (state.Cache.InputAmount >= state.Definition.InputAmount)
                return GrayboxProductionStopReason3D.None;
            if (!state.LogisticsConnected)
                return GrayboxProductionStopReason3D.OutsideLogistics;

            int missing = state.Definition.InputAmount -
                          state.Cache.InputAmount;
            if (!NetworkHas(
                    state,
                    state.Definition.InputResourceId,
                    missing,
                    cityInventory,
                    states))
                return GrayboxProductionStopReason3D.MissingInput;
            if (allowTransfer)
                TransferNetworkInput(
                    state,
                    state.Definition.InputResourceId,
                    missing,
                    cityInventory,
                    states);
            return GrayboxProductionStopReason3D.None;
        }

        private static bool BeginCycle(
            GrayboxBuildingProductionState3D state)
        {
            if (state.Definition.Kind == GrayboxProductionKind3D.Recipe &&
                state.Cache.Remove(
                    GrayboxBuildingCachePort3D.Input,
                    state.Definition.InputAmount) !=
                state.Definition.InputAmount)
                return false;
            state.CycleActive = true;
            return true;
        }

        private static GrayboxProductionStopReason3D EvaluateCompletionBlock(
            GrayboxBuildingProductionState3D state,
            WorldMapModel world)
        {
            if (state.ManuallyPaused)
                return GrayboxProductionStopReason3D.PlayerPaused;
            if (state.Definition.Kind == GrayboxProductionKind3D.Extraction &&
                IsNodeDepletedOrInvalid(state, world))
                return GrayboxProductionStopReason3D.ResourceDepleted;
            if (state.Cache.AvailableCapacity(
                    GrayboxBuildingCachePort3D.Output) <
                state.Definition.OutputAmount)
                return GrayboxProductionStopReason3D.OutputFull;
            return GrayboxProductionStopReason3D.None;
        }

        private static bool CompleteCycle(
            GrayboxBuildingProductionState3D state,
            WorldMapModel world)
        {
            string resourceId = state.Definition.OutputResourceId;
            int amount = state.Definition.OutputAmount;
            if (state.Definition.Kind == GrayboxProductionKind3D.Extraction)
            {
                int harvested = world.Harvest(
                    state.BoundNodeX,
                    state.BoundNodeY,
                    amount,
                    out resourceId);
                if (harvested != amount || !string.Equals(
                        resourceId,
                        state.BoundNodeResourceId,
                        StringComparison.Ordinal))
                    return false;
            }

            return state.Cache.Add(
                       GrayboxBuildingCachePort3D.Output,
                       resourceId,
                       amount) == amount;
        }

        private static bool IsNodeDepletedOrInvalid(
            GrayboxBuildingProductionState3D state,
            WorldMapModel world)
        {
            if (world == null ||
                state.BoundNodeX < 0 ||
                state.BoundNodeY < 0 ||
                state.BoundNodeX >= world.Width ||
                state.BoundNodeY >= world.Height)
                return true;
            WorldCell cell = world.Get(state.BoundNodeX, state.BoundNodeY);
            return cell.ResourceAmount < state.Definition.OutputAmount ||
                   !string.Equals(
                       cell.ResourceId,
                       state.BoundNodeResourceId,
                       StringComparison.Ordinal);
        }

        private static bool NetworkHas(
            GrayboxBuildingProductionState3D consumer,
            string resourceId,
            int required,
            ResourceInventory cityInventory,
            IReadOnlyList<GrayboxBuildingProductionState3D> states)
        {
            int available = cityInventory?.Get(resourceId) ?? 0;
            for (int index = 0; index < states.Count && available < required; index++)
            {
                GrayboxBuildingProductionState3D source = states[index];
                if (source == null || source == consumer ||
                    !source.LogisticsConnected ||
                    !string.Equals(
                        source.Cache.OutputResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    continue;
                available += source.Cache.OutputAmount;
            }
            return available >= required;
        }

        private static void TransferNetworkInput(
            GrayboxBuildingProductionState3D consumer,
            string resourceId,
            int required,
            ResourceInventory cityInventory,
            IReadOnlyList<GrayboxBuildingProductionState3D> states)
        {
            int remaining = required;
            while (remaining > 0)
            {
                GrayboxBuildingProductionState3D source =
                    NextSource(consumer, resourceId, states);
                if (source == null) break;
                int removed = source.Cache.Remove(
                    GrayboxBuildingCachePort3D.Output,
                    1);
                int accepted = consumer.Cache.Add(
                    GrayboxBuildingCachePort3D.Input,
                    resourceId,
                    removed);
                if (accepted != removed)
                {
                    source.Cache.Add(
                        GrayboxBuildingCachePort3D.Output,
                        resourceId,
                        removed);
                    break;
                }
                remaining -= accepted;
            }

            if (remaining <= 0 || cityInventory == null) return;
            if (!cityInventory.TrySpend(resourceId, remaining)) return;
            int cityAccepted = consumer.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                resourceId,
                remaining);
            if (cityAccepted != remaining)
                cityInventory.Add(resourceId, remaining);
        }

        private static GrayboxBuildingProductionState3D NextSource(
            GrayboxBuildingProductionState3D consumer,
            string resourceId,
            IReadOnlyList<GrayboxBuildingProductionState3D> states)
        {
            GrayboxBuildingProductionState3D selected = null;
            for (int index = 0; index < states.Count; index++)
            {
                GrayboxBuildingProductionState3D candidate = states[index];
                if (candidate == null || candidate == consumer ||
                    !candidate.LogisticsConnected ||
                    candidate.Cache.OutputAmount <= 0 ||
                    !string.Equals(
                        candidate.Cache.OutputResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    continue;
                if (selected == null || string.CompareOrdinal(
                        candidate.StableInstanceId,
                        selected.StableInstanceId) < 0)
                    selected = candidate;
            }
            return selected;
        }
    }
}
