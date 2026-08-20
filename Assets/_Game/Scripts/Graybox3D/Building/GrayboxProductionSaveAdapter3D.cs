using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProductionSaveAdapter3D
    {
        private readonly GrayboxProductionRuntime3D runtime;

        public GrayboxProductionSaveAdapter3D(
            GrayboxProductionRuntime3D runtime)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
        }

        public FormalThreeDProductionSaveData Capture()
        {
            GrayboxProductionPersistenceState3D[] source =
                runtime.CaptureForPersistence();
            var states = new FormalThreeDProductionStateSaveData[source.Length];
            for (var index = 0; index < source.Length; index++)
                states[index] = ToSaveData(source[index]);
            return new FormalThreeDProductionSaveData { states = states };
        }

        public bool TryRestore(
            FormalThreeDProductionSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            WorldMapModel world,
            out string error)
        {
            if (!TryPrepareRestore(
                    data,
                    instances,
                    world,
                    out GrayboxProductionRestorePlan3D plan,
                    out error))
            {
                return false;
            }
            return TryCommitRestore(plan, out error);
        }

        public bool TryPrepareRestore(
            FormalThreeDProductionSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            WorldMapModel world,
            out GrayboxProductionRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (data?.states == null)
            {
                error = "生产存档状态数组不能为空";
                return false;
            }

            var snapshots = new GrayboxProductionPersistenceState3D[
                data.states.Length];
            for (var index = 0; index < data.states.Length; index++)
            {
                FormalThreeDProductionStateSaveData state = data.states[index];
                if (state == null || state.inputAmounts == null ||
                    state.reservedInputs == null ||
                    state.outputAmounts == null)
                {
                    error = "生产状态或资源数量数组不能为空";
                    return false;
                }
                if (!TryConvertAmounts(
                        state.inputAmounts,
                        out ResourceAmount[] input,
                        out error) ||
                    !TryConvertAmounts(
                        state.reservedInputs,
                        out ResourceAmount[] reserved,
                        out error) ||
                    !TryConvertAmounts(
                        state.outputAmounts,
                        out ResourceAmount[] output,
                        out error))
                {
                    return false;
                }
                snapshots[index] = new GrayboxProductionPersistenceState3D(
                    state.stableInstanceId,
                    state.definitionId,
                    input,
                    state.hasReservedInputs,
                    reserved,
                    output,
                    state.progressSeconds,
                    state.isPlayerPaused,
                    state.boundResourceNodeId,
                    state.boundNodeX,
                    state.boundNodeY);
            }

            return runtime.TryPrepareRestore(
                snapshots,
                instances,
                world,
                out plan,
                out error);
        }

        public bool TryCommitRestore(
            GrayboxProductionRestorePlan3D plan,
            out string error)
        {
            return runtime.TryCommitRestore(plan, out error);
        }

        private static FormalThreeDProductionStateSaveData ToSaveData(
            GrayboxProductionPersistenceState3D source)
        {
            return new FormalThreeDProductionStateSaveData
            {
                stableInstanceId = source.StableInstanceId,
                definitionId = source.DefinitionId,
                inputAmounts = ToSaveAmounts(source.Input),
                hasReservedInputs = source.HasReservedInputs,
                reservedInputs = ToSaveAmounts(source.ReservedInput),
                outputAmounts = ToSaveAmounts(source.Output),
                progressSeconds = source.ProgressSeconds,
                isPlayerPaused = source.IsPlayerPaused,
                boundResourceNodeId = source.BoundResourceNodeId,
                boundNodeX = source.BoundNodeX,
                boundNodeY = source.BoundNodeY,
            };
        }

        private static FormalThreeDResourceAmountSaveData[] ToSaveAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            var amounts = new FormalThreeDResourceAmountSaveData[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                amounts[index] = new FormalThreeDResourceAmountSaveData
                {
                    resourceId = source[index].ResourceId,
                    amount = source[index].Amount,
                };
            }
            return amounts;
        }

        private static bool TryConvertAmounts(
            FormalThreeDResourceAmountSaveData[] source,
            out ResourceAmount[] amounts,
            out string error)
        {
            amounts = new ResourceAmount[source.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Length; index++)
            {
                FormalThreeDResourceAmountSaveData amount = source[index];
                if (amount == null ||
                    string.IsNullOrWhiteSpace(amount.resourceId) ||
                    amount.amount < 0 || !ids.Add(amount.resourceId))
                {
                    error = "生产资源数量为空、无效或重复";
                    return false;
                }
                amounts[index] = new ResourceAmount(
                    amount.resourceId,
                    amount.amount);
            }
            Array.Sort(amounts, (left, right) => string.CompareOrdinal(
                left.ResourceId,
                right.ResourceId));
            error = string.Empty;
            return true;
        }
    }
}
