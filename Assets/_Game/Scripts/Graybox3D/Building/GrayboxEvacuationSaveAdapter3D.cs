using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxEvacuationPayloadPersistenceState3D
    {
        public GrayboxEvacuationPayloadPersistenceState3D(
            string stableInstanceId,
            IReadOnlyList<ResourceAmount> productionInput,
            IReadOnlyList<ResourceAmount> productionReservedInput,
            IReadOnlyList<ResourceAmount> productionOutput,
            bool hasDefensePayload,
            int towerAmmunitionAmount,
            IReadOnlyList<ResourceAmount> resources)
        {
            StableInstanceId = stableInstanceId;
            ProductionInput = CopyAmounts(productionInput);
            ProductionReservedInput = CopyAmounts(productionReservedInput);
            ProductionOutput = CopyAmounts(productionOutput);
            HasDefensePayload = hasDefensePayload;
            TowerAmmunitionAmount = towerAmmunitionAmount;
            Resources = CopyAmounts(resources);
        }

        public string StableInstanceId { get; }
        public IReadOnlyList<ResourceAmount> ProductionInput { get; }
        public IReadOnlyList<ResourceAmount> ProductionReservedInput { get; }
        public IReadOnlyList<ResourceAmount> ProductionOutput { get; }
        public bool HasDefensePayload { get; }
        public int TowerAmmunitionAmount { get; }
        public IReadOnlyList<ResourceAmount> Resources { get; }

        private static IReadOnlyList<ResourceAmount> CopyAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<ResourceAmount>();
            var copy = new ResourceAmount[source.Count];
            for (var index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class GrayboxEvacuationPersistenceState3D
    {
        public GrayboxEvacuationPersistenceState3D(
            long nextBatchOrdinal,
            string activeBatchId,
            bool isProcessing,
            EvacuationBatchContext batchContext,
            IReadOnlyList<BuildingEvacuationWork> work,
            IReadOnlyList<string> fullQueueStableInstanceIds,
            int currentQueueIndex,
            string currentStableInstanceId,
            float remainingSeconds,
            bool isBlocked,
            string blockedCode,
            string blockedStableInstanceId,
            IReadOnlyList<GrayboxEvacuationPayloadPersistenceState3D> payloads,
            IReadOnlyList<string> lockedStableInstanceIds,
            IReadOnlyList<string> pendingRollbackStableInstanceIds)
        {
            NextBatchOrdinal = nextBatchOrdinal;
            ActiveBatchId = activeBatchId ?? string.Empty;
            IsProcessing = isProcessing;
            BatchContext = batchContext;
            Work = Copy(work);
            FullQueueStableInstanceIds = Copy(fullQueueStableInstanceIds);
            CurrentQueueIndex = currentQueueIndex;
            CurrentStableInstanceId = currentStableInstanceId ?? string.Empty;
            RemainingSeconds = remainingSeconds;
            IsBlocked = isBlocked;
            BlockedCode = blockedCode ?? string.Empty;
            BlockedStableInstanceId = blockedStableInstanceId ?? string.Empty;
            Payloads = Copy(payloads);
            LockedStableInstanceIds = Copy(lockedStableInstanceIds);
            PendingRollbackStableInstanceIds = Copy(
                pendingRollbackStableInstanceIds);
        }

        public long NextBatchOrdinal { get; }
        public string ActiveBatchId { get; }
        public bool IsProcessing { get; }
        public EvacuationBatchContext BatchContext { get; }
        public IReadOnlyList<BuildingEvacuationWork> Work { get; }
        public IReadOnlyList<string> FullQueueStableInstanceIds { get; }
        public int CurrentQueueIndex { get; }
        public string CurrentStableInstanceId { get; }
        public float RemainingSeconds { get; }
        public bool IsBlocked { get; }
        public string BlockedCode { get; }
        public string BlockedStableInstanceId { get; }
        public IReadOnlyList<GrayboxEvacuationPayloadPersistenceState3D>
            Payloads { get; }
        public IReadOnlyList<string> LockedStableInstanceIds { get; }
        public IReadOnlyList<string> PendingRollbackStableInstanceIds { get; }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<T>();
            var copy = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
                copy[index] = source[index];
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class GrayboxEvacuationRestorePlan3D
    {
        internal GrayboxEvacuationRestorePlan3D(
            GrayboxEvacuationController3D owner,
            ulong expectedGeneration,
            GrayboxEvacuationPersistenceState3D state,
            BuildingEvacuationWork[] work,
            BuildingEvacuationWork[] fullQueue,
            BuildingEvacuationWork[] rollbackWork,
            Dictionary<string, object> payloads,
            GrayboxBuildingEvacuationRestorePlan3D sessionPlan)
        {
            Owner = owner;
            ExpectedGeneration = expectedGeneration;
            State = state;
            Work = work;
            FullQueue = fullQueue;
            RollbackWork = rollbackWork;
            Payloads = payloads;
            SessionPlan = sessionPlan;
        }

        internal GrayboxEvacuationController3D Owner { get; }
        internal ulong ExpectedGeneration { get; }
        internal GrayboxEvacuationPersistenceState3D State { get; }
        internal BuildingEvacuationWork[] Work { get; }
        internal BuildingEvacuationWork[] FullQueue { get; }
        internal BuildingEvacuationWork[] RollbackWork { get; }
        internal Dictionary<string, object> Payloads { get; }
        internal GrayboxBuildingEvacuationRestorePlan3D SessionPlan { get; }
        internal bool Consumed { get; set; }
    }


    public sealed class GrayboxEvacuationSaveAdapter3D
    {
        private readonly GrayboxEvacuationController3D controller;

        public GrayboxEvacuationSaveAdapter3D(
            GrayboxEvacuationController3D controller)
        {
            this.controller = controller ??
                throw new ArgumentNullException(nameof(controller));
        }

        public FormalThreeDEvacuationSaveData Capture()
        {
            GrayboxEvacuationPersistenceState3D source =
                controller.CaptureForPersistence();
            var work = new FormalThreeDEvacuationWorkSaveData[
                source.Work.Count];
            for (var index = 0; index < source.Work.Count; index++)
            {
                BuildingEvacuationWork item = source.Work[index];
                work[index] = new FormalThreeDEvacuationWorkSaveData
                {
                    stableInstanceId = item.StableInstanceId,
                    treatment = (int)item.Treatment,
                    remainingRatio = item.RemainingRatio,
                    baseDismantleSeconds = item.BaseDismantleSeconds,
                    dismantleSeconds = item.DismantleSeconds,
                    refund = item.Refund,
                };
            }

            var payloads = new FormalThreeDEvacuationRuntimePayloadSaveData[
                source.Payloads.Count];
            for (var index = 0; index < source.Payloads.Count; index++)
            {
                GrayboxEvacuationPayloadPersistenceState3D payload =
                    source.Payloads[index];
                payloads[index] =
                    new FormalThreeDEvacuationRuntimePayloadSaveData
                    {
                        stableInstanceId = payload.StableInstanceId,
                        productionInputAmounts = ToSaveAmounts(
                            payload.ProductionInput),
                        productionReservedInputs = ToSaveAmounts(
                            payload.ProductionReservedInput),
                        productionOutputAmounts = ToSaveAmounts(
                            payload.ProductionOutput),
                        hasDefensePayload = payload.HasDefensePayload,
                        towerAmmunitionAmount =
                            payload.TowerAmmunitionAmount,
                        resourcePayload = ToSaveAmounts(payload.Resources),
                    };
            }

            return new FormalThreeDEvacuationSaveData
            {
                nextBatchOrdinal = source.NextBatchOrdinal,
                activeBatchId = source.ActiveBatchId,
                isProcessing = source.IsProcessing,
                batchContext = source.IsProcessing
                    ? new FormalThreeDEvacuationBatchContextSaveData
                    {
                        isInCombat = source.BatchContext.IsInCombat,
                        productivityMultiplier =
                            source.BatchContext.ProductivityMultiplier,
                    }
                    : null,
                work = work,
                fullQueueStableInstanceIds = CopyStrings(
                    source.FullQueueStableInstanceIds),
                currentQueueIndex = source.CurrentQueueIndex,
                currentStableInstanceId = source.CurrentStableInstanceId,
                remainingSeconds = source.RemainingSeconds,
                isBlocked = source.IsBlocked,
                blockedCode = source.BlockedCode,
                blockedStableInstanceId = source.BlockedStableInstanceId,
                runtimePayloads = payloads,
                lockedStableInstanceIds = CopyStrings(
                    source.LockedStableInstanceIds),
                pendingRollbackStableInstanceIds = CopyStrings(
                    source.PendingRollbackStableInstanceIds),
            };
        }

        public bool TryRestore(
            FormalThreeDEvacuationSaveData data,
            out string error)
        {
            if (!TryPrepareRestore(
                    data,
                    out GrayboxEvacuationRestorePlan3D plan,
                    out error))
            {
                return false;
            }
            return TryCommitRestore(plan, out error);
        }

        public bool TryPrepareRestore(
            FormalThreeDEvacuationSaveData data,
            out GrayboxEvacuationRestorePlan3D plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (data == null || data.work == null ||
                data.fullQueueStableInstanceIds == null ||
                data.runtimePayloads == null ||
                data.lockedStableInstanceIds == null ||
                data.pendingRollbackStableInstanceIds == null)
            {
                error = "撤离存档或状态数组不能为空";
                return false;
            }
            if (data.isProcessing && data.batchContext == null)
            {
                error = "活动撤离批次上下文不能为空";
                return false;
            }

            EvacuationBatchContext batchContext = data.batchContext == null
                ? BuildingEvacuationRules.CreateBatchContext(false, 1f)
                : BuildingEvacuationRules.CreateBatchContext(
                    data.batchContext.isInCombat,
                    data.batchContext.productivityMultiplier);
            var work = new BuildingEvacuationWork[data.work.Length];
            for (var index = 0; index < data.work.Length; index++)
            {
                FormalThreeDEvacuationWorkSaveData saved = data.work[index];
                if (saved == null ||
                    !BuildingEvacuationRules.TryRestoreFrozenWork(
                        saved?.stableInstanceId,
                        saved == null
                            ? BuildingEvacuationTreatment.Unassigned
                            : (BuildingEvacuationTreatment)saved.treatment,
                        saved?.remainingRatio ?? 0d,
                        saved?.baseDismantleSeconds ?? 0f,
                        saved?.dismantleSeconds ?? 0f,
                        saved?.refund ?? 0,
                        batchContext,
                        out work[index],
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "撤离冻结项目不能为空";
                    return false;
                }
            }

            var payloads = new GrayboxEvacuationPayloadPersistenceState3D[
                data.runtimePayloads.Length];
            for (var index = 0; index < data.runtimePayloads.Length; index++)
            {
                FormalThreeDEvacuationRuntimePayloadSaveData saved =
                    data.runtimePayloads[index];
                if (saved == null ||
                    !TryConvertAmounts(
                        saved?.productionInputAmounts,
                        out ResourceAmount[] input,
                        out error) ||
                    !TryConvertAmounts(
                        saved.productionReservedInputs,
                        out ResourceAmount[] reserved,
                        out error) ||
                    !TryConvertAmounts(
                        saved.productionOutputAmounts,
                        out ResourceAmount[] output,
                        out error) ||
                    !TryConvertAmounts(
                        saved.resourcePayload,
                        out ResourceAmount[] resources,
                        out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "撤离运行时载荷不能为空";
                    return false;
                }
                payloads[index] =
                    new GrayboxEvacuationPayloadPersistenceState3D(
                        saved.stableInstanceId,
                        input,
                        reserved,
                        output,
                        saved.hasDefensePayload,
                        saved.towerAmmunitionAmount,
                        resources);
            }

            var state = new GrayboxEvacuationPersistenceState3D(
                data.nextBatchOrdinal,
                data.activeBatchId,
                data.isProcessing,
                batchContext,
                work,
                CopyStrings(data.fullQueueStableInstanceIds),
                data.currentQueueIndex,
                data.currentStableInstanceId,
                data.remainingSeconds,
                data.isBlocked,
                data.blockedCode,
                data.blockedStableInstanceId,
                payloads,
                CopyStrings(data.lockedStableInstanceIds),
                CopyStrings(data.pendingRollbackStableInstanceIds));
            return controller.TryPrepareRestore(state, out plan, out error);
        }

        public bool TryCommitRestore(
            GrayboxEvacuationRestorePlan3D plan,
            out string error)
        {
            return controller.TryCommitRestore(plan, out error);
        }

        private static FormalThreeDResourceAmountSaveData[] ToSaveAmounts(
            IReadOnlyList<ResourceAmount> source)
        {
            var ordered = new ResourceAmount[source.Count];
            for (var index = 0; index < source.Count; index++)
                ordered[index] = source[index];
            Array.Sort(ordered, (left, right) => string.CompareOrdinal(
                left.ResourceId,
                right.ResourceId));

            var result = new FormalThreeDResourceAmountSaveData[
                ordered.Length];
            for (var index = 0; index < ordered.Length; index++)
            {
                result[index] = new FormalThreeDResourceAmountSaveData
                {
                    resourceId = ordered[index].ResourceId,
                    amount = ordered[index].Amount,
                };
            }
            return result;
        }

        private static bool TryConvertAmounts(
            FormalThreeDResourceAmountSaveData[] source,
            out ResourceAmount[] result,
            out string error)
        {
            if (source == null)
            {
                result = null;
                error = "撤离资源数量数组不能为空";
                return false;
            }
            result = new ResourceAmount[source.Length];
            var resourceIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Length; index++)
            {
                FormalThreeDResourceAmountSaveData amount = source[index];
                if (amount == null ||
                    string.IsNullOrWhiteSpace(amount.resourceId) ||
                    amount.amount < 0 ||
                    !resourceIds.Add(amount.resourceId))
                {
                    error = "撤离资源数量为空、无效或重复";
                    return false;
                }
                result[index] = new ResourceAmount(
                    amount.resourceId,
                    amount.amount);
            }
            Array.Sort(result, (left, right) => string.CompareOrdinal(
                left.ResourceId,
                right.ResourceId));
            error = string.Empty;
            return true;
        }

        private static string[] CopyStrings(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }
    }

}
