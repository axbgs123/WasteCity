using System;
using System.Collections.Generic;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxFormalProgressionRestorePlan3D
    {
        internal GrayboxFormalProgressionRestorePlan3D(
            GrayboxFormalProgressionSaveAdapter3D owner,
            FormalAttentionSnapshot expectedAttention,
            FormalFateSnapshot expectedFate,
            PocketUniverseFateSnapshot expectedPocketUniverse,
            FormalVoidDebtSnapshot expectedVoidDebt,
            FormalRewindAnchorMetadataSnapshot expectedRewindAnchors,
            FormalAttentionSnapshot targetAttention,
            FormalFateSnapshot targetFate,
            PocketUniverseFateSnapshot targetPocketUniverse,
            FormalVoidDebtSnapshot targetVoidDebt,
            FormalRewindAnchorMetadataSnapshot targetRewindAnchors)
        {
            Owner = owner;
            ExpectedAttention = expectedAttention;
            ExpectedFate = expectedFate;
            ExpectedPocketUniverse = expectedPocketUniverse;
            ExpectedVoidDebt = expectedVoidDebt;
            ExpectedRewindAnchors = expectedRewindAnchors;
            TargetAttention = targetAttention;
            TargetFate = targetFate;
            TargetPocketUniverse = targetPocketUniverse;
            TargetVoidDebt = targetVoidDebt;
            TargetRewindAnchors = targetRewindAnchors;
        }

        internal GrayboxFormalProgressionSaveAdapter3D Owner { get; }
        internal FormalAttentionSnapshot ExpectedAttention { get; }
        internal FormalFateSnapshot ExpectedFate { get; }
        internal PocketUniverseFateSnapshot ExpectedPocketUniverse { get; }
        internal FormalVoidDebtSnapshot ExpectedVoidDebt { get; }
        internal FormalRewindAnchorMetadataSnapshot ExpectedRewindAnchors
            { get; }
        internal FormalAttentionSnapshot TargetAttention { get; }
        internal FormalFateSnapshot TargetFate { get; }
        internal PocketUniverseFateSnapshot TargetPocketUniverse { get; }
        internal FormalVoidDebtSnapshot TargetVoidDebt { get; }
        internal FormalRewindAnchorMetadataSnapshot TargetRewindAnchors
            { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class GrayboxFormalProgressionSaveAdapter3D
    {
        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;
        private readonly PocketUniverseFateEffect pocketUniverse;
        private readonly FormalVoidDebtRuntime voidDebt;
        private readonly FormalRewindAnchorMetadataRuntime rewindAnchors;

        public GrayboxFormalProgressionSaveAdapter3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            PocketUniverseFateEffect pocketUniverse,
            FormalVoidDebtRuntime voidDebt,
            FormalRewindAnchorMetadataRuntime rewindAnchors)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.pocketUniverse = pocketUniverse ??
                throw new ArgumentNullException(nameof(pocketUniverse));
            this.voidDebt = voidDebt ??
                throw new ArgumentNullException(nameof(voidDebt));
            this.rewindAnchors = rewindAnchors ??
                throw new ArgumentNullException(nameof(rewindAnchors));
        }

        public FormalThreeDProgressionSaveData Capture()
        {
            FormalAttentionSnapshot attentionState = attention.Capture();
            FormalFateSnapshot fateState = fate.Capture();
            var history = new FormalThreeDAttentionHistorySaveData[
                attentionState.History.Count];
            for (var index = 0; index < history.Length; index++)
            {
                FormalAttentionHistoryEntry entry =
                    attentionState.History[index];
                history[index] = new FormalThreeDAttentionHistorySaveData
                {
                    reasonId = entry.ReasonId,
                    stableEventKey = entry.StableEventKey,
                    requestedDelta = entry.RequestedDelta,
                    appliedDelta = entry.AppliedDelta,
                    valueAfter = entry.ValueAfter,
                    revision = entry.Revision,
                    ruleTimeSeconds = entry.RuleTimeSeconds,
                    sourceInstanceId = entry.SourceInstanceId,
                };
            }

            return new FormalThreeDProgressionSaveData
            {
                configurationSignature =
                    FormalThreeDProgressionSaveData.ConfigurationSignature,
                attention = new FormalThreeDAttentionSaveData
                {
                    value = attentionState.Value,
                    revision = attentionState.Revision,
                    history = history,
                    reachedThresholds = Copy(
                        attentionState.ReachedThresholds),
                    committedStableEventKeys = Copy(
                        attentionState.CommittedStableEventKeys),
                    completedOneShotReasonIds = Copy(
                        attentionState.AppliedOnceReasonIds),
                },
                fate = new FormalThreeDFateSaveData
                {
                    offeredIds = Copy(fateState.OfferedIds),
                    selectedId = fateState.SelectedId ?? string.Empty,
                    level = fateState.Level,
                    revision = fateState.Revision,
                },
                fateEffects = new FormalThreeDFateEffectsSaveData
                {
                    pocketUniverse = CapturePocketUniverse(
                        pocketUniverse.Capture()),
                    voidDebt = CaptureVoidDebt(voidDebt.Capture()),
                    rewindAnchors = CaptureRewindAnchors(
                        rewindAnchors.Capture()),
                },
                civilization = new FormalThreeDCivilizationSaveData
                {
                    level = 1,
                    committedAscensionIds = Array.Empty<string>(),
                },
            };
        }

        public bool TryRestore(
            FormalThreeDProgressionSaveData data,
            out string error)
        {
            if (!TryPrepareRestore(
                    data,
                    out GrayboxFormalProgressionRestorePlan3D plan,
                    out error))
            {
                return false;
            }
            return TryCommitRestore(plan, out error);
        }

        public bool TryPrepareRestore(
            FormalThreeDProgressionSaveData data,
            out GrayboxFormalProgressionRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (!HasRequiredData(data))
            {
                error = "正式进度存档数据或必需数组不完整";
                return false;
            }
            if (data.civilization.level != 1 ||
                data.civilization.committedAscensionIds.Length != 0)
            {
                error = "当前正式进度存档仅支持文明等级一的未升阶状态";
                return false;
            }

            if (!TryPrepareAttention(
                    data.attention,
                    out FormalAttentionSnapshot attentionTarget,
                    out error) ||
                !TryPrepareFate(
                    data.fate,
                    out FormalFateSnapshot fateTarget,
                    out error) ||
                !TryPreparePocketUniverse(
                    data.fateEffects.pocketUniverse,
                    out PocketUniverseFateSnapshot pocketTarget,
                    out error) ||
                !TryPrepareVoidDebt(
                    data.fateEffects.voidDebt,
                    out FormalVoidDebtSnapshot debtTarget,
                    out error) ||
                !TryPrepareRewindAnchors(
                    data.fateEffects.rewindAnchors,
                    out FormalRewindAnchorMetadataSnapshot rewindTarget,
                    out error) ||
                !ValidateEffectOwnership(
                    fateTarget,
                    pocketTarget,
                    debtTarget,
                    data.fateEffects.rewindAnchors,
                    out error))
            {
                return false;
            }

            plan = new GrayboxFormalProgressionRestorePlan3D(
                this,
                attention.Capture(),
                fate.Capture(),
                pocketUniverse.Capture(),
                voidDebt.Capture(),
                rewindAnchors.Capture(),
                attentionTarget,
                fateTarget,
                pocketTarget,
                debtTarget,
                rewindTarget);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            GrayboxFormalProgressionRestorePlan3D plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "正式进度恢复计划不属于当前适配器";
                return false;
            }
            if (plan.Consumed)
            {
                error = "正式进度恢复计划已提交";
                return false;
            }
            if (!ReferenceEquals(attention.Capture(), plan.ExpectedAttention) ||
                !ReferenceEquals(fate.Capture(), plan.ExpectedFate) ||
                !ReferenceEquals(
                    pocketUniverse.Capture(),
                    plan.ExpectedPocketUniverse) ||
                !ReferenceEquals(voidDebt.Capture(), plan.ExpectedVoidDebt) ||
                !ReferenceEquals(
                    rewindAnchors.Capture(),
                    plan.ExpectedRewindAnchors))
            {
                error = "正式进度恢复计划已过期";
                return false;
            }

            FormalAttentionSnapshot previousAttention = attention.Capture();
            FormalFateSnapshot previousFate = fate.Capture();
            PocketUniverseFateSnapshot previousPocket =
                pocketUniverse.Capture();
            FormalVoidDebtSnapshot previousDebt = voidDebt.Capture();
            FormalRewindAnchorMetadataSnapshot previousRewind =
                rewindAnchors.Capture();
            if (!attention.TryRestore(plan.TargetAttention, out error) ||
                !fate.TryRestore(plan.TargetFate, out error) ||
                !pocketUniverse.TryRestore(
                    plan.TargetPocketUniverse,
                    out error) ||
                !voidDebt.TryRestore(plan.TargetVoidDebt, out error) ||
                !rewindAnchors.TryRestore(
                    plan.TargetRewindAnchors,
                    out error))
            {
                Rollback(
                    previousAttention,
                    previousFate,
                    previousPocket,
                    previousDebt,
                    previousRewind);
                return false;
            }

            plan.Consumed = true;
            error = string.Empty;
            return true;
        }

        private static bool HasRequiredData(
            FormalThreeDProgressionSaveData data)
        {
            return data != null &&
                string.Equals(
                    data.configurationSignature,
                    FormalThreeDProgressionSaveData.ConfigurationSignature,
                    StringComparison.Ordinal) &&
                data.attention != null && data.fate != null &&
                data.fateEffects != null &&
                data.fateEffects.pocketUniverse != null &&
                data.fateEffects.voidDebt != null &&
                data.fateEffects.rewindAnchors != null &&
                data.civilization != null &&
                data.attention.history != null &&
                data.attention.reachedThresholds != null &&
                data.attention.committedStableEventKeys != null &&
                data.attention.completedOneShotReasonIds != null &&
                data.fate.offeredIds != null &&
                data.fateEffects.pocketUniverse.flagships != null &&
                data.fateEffects.pocketUniverse.collapsedFlagshipIds != null &&
                data.fateEffects.voidDebt.debts != null &&
                data.fateEffects.rewindAnchors.anchors != null &&
                data.civilization.committedAscensionIds != null;
        }

        private static bool TryPrepareAttention(
            FormalThreeDAttentionSaveData data,
            out FormalAttentionSnapshot snapshot,
            out string error)
        {
            var history = new FormalAttentionHistoryEntry[data.history.Length];
            for (var index = 0; index < history.Length; index++)
            {
                FormalThreeDAttentionHistorySaveData entry =
                    data.history[index];
                if (entry == null)
                {
                    snapshot = null;
                    error = "关注度历史记录不能为空";
                    return false;
                }
                history[index] = new FormalAttentionHistoryEntry(
                    entry.reasonId,
                    entry.stableEventKey,
                    entry.requestedDelta,
                    entry.appliedDelta,
                    entry.valueAfter,
                    entry.revision,
                    entry.ruleTimeSeconds,
                    entry.sourceInstanceId);
            }
            var candidate = new FormalAttentionSnapshot(
                data.value,
                data.revision,
                history,
                Copy(data.reachedThresholds),
                Copy(data.committedStableEventKeys),
                Copy(data.completedOneShotReasonIds));
            var validator = new FormalAttentionRuntime();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static bool TryPrepareFate(
            FormalThreeDFateSaveData data,
            out FormalFateSnapshot snapshot,
            out string error)
        {
            var candidate = new FormalFateSnapshot(
                data.revision,
                Copy(data.offeredIds),
                data.selectedId,
                data.level);
            var validator = new FormalFateRuntime();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static FormalThreeDPocketUniverseSaveData
            CapturePocketUniverse(PocketUniverseFateSnapshot snapshot)
        {
            var flagships = new FormalThreeDPocketUniverseFlagshipSaveData[
                snapshot.Flagships.Count];
            for (var index = 0; index < flagships.Length; index++)
            {
                PocketUniverseFlagshipState flagship = snapshot.Flagships[index];
                flagships[index] =
                    new FormalThreeDPocketUniverseFlagshipSaveData
                    {
                        buildingDefinitionId = flagship.BuildingDefinitionId,
                        stableInstanceId = flagship.StableInstanceId,
                    };
            }
            return new FormalThreeDPocketUniverseSaveData
            {
                level = snapshot.Level,
                revision = snapshot.Revision,
                flagships = flagships,
                collapsedFlagshipIds = Copy(snapshot.CollapsedFlagshipIds),
                firstProductionFlagshipId =
                    snapshot.FirstProductionFlagshipId ?? string.Empty,
            };
        }

        private static bool TryPreparePocketUniverse(
            FormalThreeDPocketUniverseSaveData data,
            out PocketUniverseFateSnapshot snapshot,
            out string error)
        {
            var flagships = new PocketUniverseFlagshipState[
                data.flagships.Length];
            for (var index = 0; index < flagships.Length; index++)
            {
                FormalThreeDPocketUniverseFlagshipSaveData entry =
                    data.flagships[index];
                if (entry == null)
                {
                    snapshot = null;
                    error = "袖珍宇宙旗舰存档记录不能为空";
                    return false;
                }
                flagships[index] = new PocketUniverseFlagshipState(
                    entry.buildingDefinitionId,
                    entry.stableInstanceId);
            }
            var candidate = new PocketUniverseFateSnapshot(
                data.level,
                data.revision,
                flagships,
                Copy(data.collapsedFlagshipIds),
                data.firstProductionFlagshipId);
            var validator = new PocketUniverseFateEffect();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static FormalThreeDVoidDebtSaveData CaptureVoidDebt(
            FormalVoidDebtSnapshot snapshot)
        {
            var debts = new FormalThreeDVoidDebtEntrySaveData[
                snapshot.Debts.Count];
            for (var index = 0; index < debts.Length; index++)
            {
                FormalVoidDebtEntry entry = snapshot.Debts[index];
                debts[index] = new FormalThreeDVoidDebtEntrySaveData
                {
                    resourceId = entry.ResourceId,
                    amount = entry.Amount,
                };
            }
            return new FormalThreeDVoidDebtSaveData
            {
                level = snapshot.Level,
                settlementRemainingSeconds =
                    snapshot.SettlementRemainingSeconds,
                nextSettlementOrdinal = snapshot.NextSettlementOrdinal,
                revision = snapshot.Revision,
                debts = debts,
            };
        }

        private static bool TryPrepareVoidDebt(
            FormalThreeDVoidDebtSaveData data,
            out FormalVoidDebtSnapshot snapshot,
            out string error)
        {
            var debts = new FormalVoidDebtEntry[data.debts.Length];
            for (var index = 0; index < debts.Length; index++)
            {
                FormalThreeDVoidDebtEntrySaveData entry = data.debts[index];
                if (entry == null)
                {
                    snapshot = null;
                    error = "虚空债存档记录不能为空";
                    return false;
                }
                debts[index] = new FormalVoidDebtEntry(
                    entry.resourceId,
                    entry.amount);
            }
            FormalVoidDebtRuntime validator;
            try
            {
                validator = new FormalVoidDebtRuntime(data.level);
            }
            catch (ArgumentOutOfRangeException)
            {
                snapshot = null;
                error = "虚空债命轨等级无效";
                return false;
            }
            var candidate = new FormalVoidDebtSnapshot(
                data.level,
                data.settlementRemainingSeconds,
                data.nextSettlementOrdinal,
                data.revision,
                debts);
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static FormalThreeDRewindAnchorMetadataSaveData
            CaptureRewindAnchors(
                FormalRewindAnchorMetadataSnapshot snapshot)
        {
            var anchors = new FormalThreeDRewindAnchorEntrySaveData[
                snapshot.Entries.Count];
            for (var index = 0; index < anchors.Length; index++)
            {
                FormalRewindAnchorMetadata entry = snapshot.Entries[index];
                anchors[index] = new FormalThreeDRewindAnchorEntrySaveData
                {
                    stableAnchorId = entry.AnchorId,
                    internalKey = entry.InternalKey,
                    creationOrdinal = entry.CreationOrdinal,
                    sessionId = entry.SessionId,
                    payloadHashSha256 = entry.PayloadHashSha256,
                    checkpointSequence = entry.CheckpointSequence,
                    checkpointReasonId = entry.CheckpointReasonId,
                    checkpointRuleTimeSeconds =
                        entry.CheckpointRuleTimeSeconds,
                    completedMilestoneIds = Copy(
                        entry.CompletedMilestoneIds),
                };
            }
            return new FormalThreeDRewindAnchorMetadataSaveData
            {
                revision = snapshot.Revision,
                nextCreationOrdinal = snapshot.NextCreationOrdinal,
                anchors = anchors,
            };
        }

        private static bool TryPrepareRewindAnchors(
            FormalThreeDRewindAnchorMetadataSaveData data,
            out FormalRewindAnchorMetadataSnapshot snapshot,
            out string error)
        {
            var entries = new FormalRewindAnchorMetadata[data.anchors.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                FormalThreeDRewindAnchorEntrySaveData item =
                    data.anchors[index];
                if (item == null || item.completedMilestoneIds == null)
                {
                    snapshot = null;
                    error = "回溯锚点元数据记录不能为空";
                    return false;
                }
                try
                {
                    entries[index] = new FormalRewindAnchorMetadata(
                        item.stableAnchorId,
                        item.internalKey,
                        item.sessionId,
                        item.payloadHashSha256,
                        new WasteCity.Persistence.FormalSaveCheckpointMetadata
                        {
                            sequence = item.checkpointSequence,
                            reasonId = item.checkpointReasonId,
                            ruleTimeSeconds = item.checkpointRuleTimeSeconds,
                            completedMilestoneIds = Copy(
                                item.completedMilestoneIds),
                        },
                        item.creationOrdinal);
                }
                catch (ArgumentException exception)
                {
                    snapshot = null;
                    error = exception.Message;
                    return false;
                }
            }
            var candidate = new FormalRewindAnchorMetadataSnapshot(
                data.revision,
                data.nextCreationOrdinal,
                entries);
            var validator = new FormalRewindAnchorMetadataRuntime();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static bool ValidateEffectOwnership(
            FormalFateSnapshot fateState,
            PocketUniverseFateSnapshot pocket,
            FormalVoidDebtSnapshot debt,
            FormalThreeDRewindAnchorMetadataSaveData rewind,
            out string error)
        {
            bool pocketSelected = string.Equals(
                fateState.SelectedId,
                FormalFateCatalog.PocketUniverseId,
                StringComparison.Ordinal);
            bool debtSelected = string.Equals(
                fateState.SelectedId,
                FormalFateCatalog.VoidDebtId,
                StringComparison.Ordinal);
            bool rewindSelected = string.Equals(
                fateState.SelectedId,
                FormalFateCatalog.RewindAnchorId,
                StringComparison.Ordinal);
            if ((!pocketSelected &&
                 (pocket.Flagships.Count != 0 ||
                  pocket.CollapsedFlagshipIds.Count != 0 ||
                  !string.IsNullOrEmpty(pocket.FirstProductionFlagshipId))) ||
                (!debtSelected &&
                 (debt.Debts.Count != 0 ||
                  debt.SettlementRemainingSeconds != 0d)) ||
                (!rewindSelected && rewind.anchors.Length != 0))
            {
                error = "未选择的命轨不能携带已激活效果状态";
                return false;
            }
            if ((pocketSelected && pocket.Level != fateState.Level) ||
                (debtSelected && debt.Level != fateState.Level))
            {
                error = "命轨效果等级与正式命轨等级不一致";
                return false;
            }
            if (rewind.nextCreationOrdinal <= 0L)
            {
                error = "回溯锚点创建序号无效";
                return false;
            }
            for (var index = 0; index < rewind.anchors.Length; index++)
            {
                FormalThreeDRewindAnchorEntrySaveData entry =
                    rewind.anchors[index];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.stableAnchorId) ||
                    string.IsNullOrWhiteSpace(entry.internalKey) ||
                    entry.creationOrdinal <= 0L)
                {
                    error = "回溯锚点元数据无效";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        private void Rollback(
            FormalAttentionSnapshot attentionState,
            FormalFateSnapshot fateState,
            PocketUniverseFateSnapshot pocketState,
            FormalVoidDebtSnapshot debtState,
            FormalRewindAnchorMetadataSnapshot rewindState)
        {
            attention.TryRestore(attentionState, out _);
            fate.TryRestore(fateState, out _);
            pocketUniverse.TryRestore(pocketState, out _);
            voidDebt.TryRestore(debtState, out _);
            rewindAnchors.TryRestore(rewindState, out _);
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            var result = new string[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }

        private static int[] Copy(IReadOnlyList<int> source)
        {
            var result = new int[source.Count];
            for (var index = 0; index < source.Count; index++)
                result[index] = source[index];
            return result;
        }
    }
}
