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
            FormalRewindAnchorMetadataSnapshot targetRewindAnchors,
            GrayboxAttentionPressureRestorePlan3D pressurePlan,
            FormalCivilizationAscensionSnapshot expectedCivilization,
            FormalCivilizationAscensionSnapshot targetCivilization,
            AdvancementSequenceStage expectedSequenceStage,
            float expectedSequenceRemaining,
            AdvancementSequenceStage targetSequenceStage,
            float targetSequenceRemaining,
            int expectedRewindFateLevel,
            int targetRewindFateLevel)
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
            PressurePlan = pressurePlan;
            ExpectedCivilization = expectedCivilization;
            TargetCivilization = targetCivilization;
            ExpectedSequenceStage = expectedSequenceStage;
            ExpectedSequenceRemaining = expectedSequenceRemaining;
            TargetSequenceStage = targetSequenceStage;
            TargetSequenceRemaining = targetSequenceRemaining;
            ExpectedRewindFateLevel = expectedRewindFateLevel;
            TargetRewindFateLevel = targetRewindFateLevel;
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
        internal GrayboxAttentionPressureRestorePlan3D PressurePlan { get; }
        internal FormalCivilizationAscensionSnapshot ExpectedCivilization
            { get; }
        internal FormalCivilizationAscensionSnapshot TargetCivilization
            { get; }
        internal AdvancementSequenceStage ExpectedSequenceStage { get; }
        internal float ExpectedSequenceRemaining { get; }
        internal AdvancementSequenceStage TargetSequenceStage { get; }
        internal float TargetSequenceRemaining { get; }
        internal int ExpectedRewindFateLevel { get; }
        internal int TargetRewindFateLevel { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class GrayboxFormalProgressionSaveAdapter3D
    {
        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;
        private readonly PocketUniverseFateEffect pocketUniverse;
        private readonly FormalVoidDebtRuntime voidDebt;
        private readonly FormalRewindAnchorMetadataRuntime rewindAnchors;
        private readonly GrayboxAttentionPressureSaveAdapter3D pressureAdapter;
        private readonly FormalCivilizationAscensionRuntime civilization;
        private readonly AdvancementSequenceModel advancementSequence;

        public GrayboxFormalProgressionSaveAdapter3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            PocketUniverseFateEffect pocketUniverse,
            FormalVoidDebtRuntime voidDebt,
            FormalRewindAnchorMetadataRuntime rewindAnchors,
            GrayboxAttentionPressureSaveAdapter3D pressureAdapter = null,
            FormalCivilizationAscensionRuntime civilization = null,
            AdvancementSequenceModel advancementSequence = null)
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
            this.pressureAdapter = pressureAdapter;
            this.civilization = civilization;
            this.advancementSequence = advancementSequence;
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
                pressure = pressureAdapter?.Capture() ??
                    new FormalThreeDAttentionPressureSaveData(),
                civilization = CaptureCivilization(fateState),
            };
        }

        private FormalThreeDCivilizationSaveData CaptureCivilization(
            FormalFateSnapshot fateState)
        {
            if (civilization == null)
                return new FormalThreeDCivilizationSaveData();
            FormalCivilizationAscensionSnapshot snapshot =
                civilization.Capture();
            bool pending = fateState != null && !fateState.HasSelection &&
                string.IsNullOrEmpty(snapshot.FateId) &&
                snapshot.FateLevel == 0 && !snapshot.Ascended;
            if (!pending && (fateState == null || !fateState.HasSelection ||
                !string.Equals(
                    snapshot.FateId,
                    fateState.SelectedId,
                    StringComparison.Ordinal) ||
                snapshot.FateLevel != fateState.Level))
            {
                throw new InvalidOperationException(
                    "文明升阶 owner 与正式命轨真值不一致");
            }
            AdvancementSequenceStage stage = advancementSequence?.Stage ??
                AdvancementSequenceStage.None;
            float remaining = advancementSequence?.Remaining ?? 0f;
            if (snapshot.Ascended && advancementSequence == null)
                throw new InvalidOperationException(
                    "已升阶文明缺少演出序列 owner");
            return new FormalThreeDCivilizationSaveData
            {
                level = snapshot.CivilizationLevel,
                revision = snapshot.Revision,
                ascensionId = snapshot.Ascended
                    ? FormalThreeDCivilizationSaveData.FirstAscensionId
                    : string.Empty,
                ascensionCompleted = snapshot.Ascended,
                sequenceStage = (int)stage,
                remainingRuleSeconds = remaining,
                committedAscensionIds = snapshot.Ascended
                    ? new[]
                    {
                        FormalThreeDCivilizationSaveData.FirstAscensionId,
                    }
                    : Array.Empty<string>(),
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
                    fateTarget,
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
            int targetRewindFateLevel = fateTarget.Level == 2 &&
                string.Equals(
                    fateTarget.SelectedId,
                    FormalFateCatalog.RewindAnchorId,
                    StringComparison.Ordinal)
                        ? 2
                        : 1;

            if (!TryPrepareCivilization(
                    data.civilization,
                    fateTarget,
                    out FormalCivilizationAscensionSnapshot civilizationTarget,
                    out AdvancementSequenceStage sequenceStageTarget,
                    out float sequenceRemainingTarget,
                    out error))
            {
                return false;
            }

            GrayboxAttentionPressureRestorePlan3D pressurePlan = null;
            if (pressureAdapter != null)
            {
                if (!pressureAdapter.TryPrepareRestore(
                        data.pressure, out pressurePlan, out error))
                    return false;
            }
            else if (!IsCleanPressure(data.pressure))
            {
                error = "当前进度适配器未配置压力持久化 owner";
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
                rewindTarget,
                pressurePlan,
                civilization?.Capture(),
                civilizationTarget,
                advancementSequence?.Stage ??
                    AdvancementSequenceStage.None,
                advancementSequence?.Remaining ?? 0f,
                sequenceStageTarget,
                sequenceRemainingTarget,
                rewindAnchors.MaximumAnchors ==
                    FormalRewindAnchorMetadataRuntime.MaximumAnchorsAtLevelTwo
                        ? 2
                        : 1,
                targetRewindFateLevel);
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
                    plan.ExpectedRewindAnchors) ||
                civilization != null && !ReferenceEquals(
                    civilization.Capture(),
                    plan.ExpectedCivilization) ||
                advancementSequence != null &&
                (advancementSequence.Stage != plan.ExpectedSequenceStage ||
                 advancementSequence.Remaining !=
                    plan.ExpectedSequenceRemaining) ||
                rewindAnchors.MaximumAnchors !=
                    (plan.ExpectedRewindFateLevel == 2
                        ? FormalRewindAnchorMetadataRuntime
                            .MaximumAnchorsAtLevelTwo
                        : FormalRewindAnchorMetadataRuntime
                            .MaximumAnchorsAtLevelOne))
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
            FormalCivilizationAscensionSnapshot previousCivilization =
                civilization?.Capture();
            AdvancementSequenceStage previousSequenceStage =
                advancementSequence?.Stage ?? AdvancementSequenceStage.None;
            float previousSequenceRemaining =
                advancementSequence?.Remaining ?? 0f;
            if (!attention.TryRestore(plan.TargetAttention, out error) ||
                !fate.TryRestore(plan.TargetFate, out error) ||
                !pocketUniverse.TryRestore(
                    plan.TargetPocketUniverse,
                    out error) ||
                !voidDebt.TryRestore(plan.TargetVoidDebt, out error) ||
                !TryRestoreRewindAnchors(
                    plan.TargetRewindAnchors,
                    plan.TargetRewindFateLevel,
                    out error) ||
                plan.TargetCivilization != null &&
                !civilization.TryRestore(
                    plan.TargetCivilization,
                    out error))
            {
                Rollback(
                    previousAttention,
                    previousFate,
                    previousPocket,
                    previousDebt,
                    previousRewind,
                    previousCivilization,
                    previousSequenceStage,
                    previousSequenceRemaining,
                    plan.ExpectedRewindFateLevel);
                return false;
            }
            if (advancementSequence != null)
                advancementSequence.Restore(
                    (int)plan.TargetSequenceStage,
                    plan.TargetSequenceRemaining);
            if (plan.PressurePlan != null &&
                !pressureAdapter.TryCommitRestore(
                    plan.PressurePlan, out error))
            {
                Rollback(
                    previousAttention,
                    previousFate,
                    previousPocket,
                    previousDebt,
                    previousRewind,
                    previousCivilization,
                    previousSequenceStage,
                    previousSequenceRemaining,
                    plan.ExpectedRewindFateLevel);
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
                data.pressure != null && data.pressure.entries != null &&
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

        private static bool IsCleanPressure(
            FormalThreeDAttentionPressureSaveData data)
        {
            return data != null && data.revision == 0ul &&
                data.entries != null && data.entries.Length == 0 &&
                string.IsNullOrEmpty(data.activeEncounterId) &&
                data.activeCampaign == null;
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

        private bool TryPrepareCivilization(
            FormalThreeDCivilizationSaveData data,
            FormalFateSnapshot fateTarget,
            out FormalCivilizationAscensionSnapshot snapshot,
            out AdvancementSequenceStage sequenceStage,
            out float sequenceRemaining,
            out string error)
        {
            snapshot = null;
            sequenceStage = AdvancementSequenceStage.None;
            sequenceRemaining = 0f;
            if (data == null || data.committedAscensionIds == null ||
                !Enum.IsDefined(
                    typeof(AdvancementSequenceStage),
                    data.sequenceStage) ||
                float.IsNaN(data.remainingRuleSeconds) ||
                float.IsInfinity(data.remainingRuleSeconds) ||
                data.remainingRuleSeconds < 0f)
            {
                error = "文明升阶或演出序列存档无效";
                return false;
            }
            sequenceStage = (AdvancementSequenceStage)data.sequenceStage;
            sequenceRemaining = data.remainingRuleSeconds;
            bool clean = data.level == 1 && data.revision == 0ul &&
                string.IsNullOrEmpty(data.ascensionId) &&
                !data.ascensionCompleted &&
                data.committedAscensionIds.Length == 0 &&
                sequenceStage == AdvancementSequenceStage.None &&
                sequenceRemaining == 0f && fateTarget.Level <= 1;
            if (clean)
            {
                if (civilization == null) return Success(out error);
                snapshot = new FormalCivilizationAscensionSnapshot(
                    1,
                    fateTarget.HasSelection
                        ? fateTarget.SelectedId
                        : string.Empty,
                    fateTarget.HasSelection ? 1 : 0,
                    false,
                    0ul);
                var validator = fateTarget.HasSelection
                    ? new FormalCivilizationAscensionRuntime(
                        fateTarget.SelectedId)
                    : new FormalCivilizationAscensionRuntime();
                if (!validator.TryRestore(snapshot, out error)) return false;
                snapshot = validator.Capture();
                return true;
            }

            bool committed = data.level == 2 && data.revision > 0ul &&
                data.ascensionCompleted &&
                data.committedAscensionIds.Length == 1 &&
                string.Equals(
                    data.ascensionId,
                    FormalThreeDCivilizationSaveData.FirstAscensionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    data.committedAscensionIds[0],
                    FormalThreeDCivilizationSaveData.FirstAscensionId,
                    StringComparison.Ordinal) &&
                fateTarget.HasSelection && fateTarget.Level == 2 &&
                IsValidSequence(sequenceStage, sequenceRemaining);
            if (!committed)
            {
                error = "文明等级、升阶锁、命轨或演出序列组合无效";
                return false;
            }
            if (civilization == null || advancementSequence == null)
            {
                error = "二级文明恢复缺少升阶规则或演出序列 owner";
                return false;
            }
            var runtime = new FormalCivilizationAscensionRuntime(
                fateTarget.SelectedId);
            var candidate = new FormalCivilizationAscensionSnapshot(
                data.level,
                fateTarget.SelectedId,
                fateTarget.Level,
                data.ascensionCompleted,
                data.revision);
            if (!runtime.TryRestore(candidate, out error)) return false;
            snapshot = runtime.Capture();
            return true;
        }

        private static bool Success(out string error)
        {
            error = string.Empty;
            return true;
        }

        private static bool IsValidSequence(
            AdvancementSequenceStage stage,
            float remaining)
        {
            switch (stage)
            {
                case AdvancementSequenceStage.Scanning:
                    return remaining > 0f && remaining <= 2.5f;
                case AdvancementSequenceStage.Confirmed:
                    return remaining > 0f && remaining <= 3f;
                case AdvancementSequenceStage.Warning:
                    return remaining > 0f && remaining <= 4f;
                case AdvancementSequenceStage.Results:
                case AdvancementSequenceStage.Continued:
                    return remaining == 0f;
                default:
                    return false;
            }
        }

        private static bool TryPrepareRewindAnchors(
            FormalThreeDRewindAnchorMetadataSaveData data,
            FormalFateSnapshot fateState,
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
            bool rewindLevelTwo = fateState != null &&
                fateState.Level == 2 && string.Equals(
                    fateState.SelectedId,
                    FormalFateCatalog.RewindAnchorId,
                    StringComparison.Ordinal);
            var validator = new FormalRewindAnchorMetadataRuntime(
                rewindLevelTwo ? 2 : 1);
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
            int expectedPocketLevel = pocketSelected ? fateState.Level : 1;
            int expectedDebtLevel = debtSelected ? fateState.Level : 1;
            int expectedRewindCapacity = rewindSelected &&
                fateState.Level == 2
                    ? FormalRewindAnchorMetadataRuntime
                        .MaximumAnchorsAtLevelTwo
                    : FormalRewindAnchorMetadataRuntime
                        .MaximumAnchorsAtLevelOne;
            if ((!pocketSelected &&
                 (pocket.Flagships.Count != 0 ||
                  pocket.CollapsedFlagshipIds.Count != 0 ||
                  !string.IsNullOrEmpty(pocket.FirstProductionFlagshipId))) ||
                (!debtSelected &&
                 (debt.Debts.Count != 0 ||
                  debt.SettlementRemainingSeconds != 0d)) ||
                (!rewindSelected && rewind.anchors.Length != 0) ||
                pocket.Level != expectedPocketLevel ||
                debt.Level != expectedDebtLevel ||
                rewind.anchors.Length > expectedRewindCapacity)
            {
                error = "未选择的命轨不能携带已激活效果状态";
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
            FormalRewindAnchorMetadataSnapshot rewindState,
            FormalCivilizationAscensionSnapshot civilizationState,
            AdvancementSequenceStage sequenceStage,
            float sequenceRemaining,
            int rewindFateLevel)
        {
            attention.TryRestore(attentionState, out _);
            fate.TryRestore(fateState, out _);
            pocketUniverse.TryRestore(pocketState, out _);
            voidDebt.TryRestore(debtState, out _);
            TryRestoreRewindAnchors(
                rewindState,
                rewindFateLevel,
                out _);
            if (civilization != null && civilizationState != null)
                civilization.TryRestore(civilizationState, out _);
            advancementSequence?.Restore(
                (int)sequenceStage,
                sequenceRemaining);
        }

        private bool TryRestoreRewindAnchors(
            FormalRewindAnchorMetadataSnapshot snapshot,
            int fateLevel,
            out string error)
        {
            int targetCapacity = fateLevel == 2
                ? FormalRewindAnchorMetadataRuntime.MaximumAnchorsAtLevelTwo
                : FormalRewindAnchorMetadataRuntime.MaximumAnchorsAtLevelOne;
            if (rewindAnchors.MaximumAnchors < targetCapacity)
            {
                return rewindAnchors.TrySetFateLevel(fateLevel, out error) &&
                    rewindAnchors.TryRestore(snapshot, out error);
            }
            if (!rewindAnchors.TryRestore(snapshot, out error)) return false;
            return rewindAnchors.TrySetFateLevel(fateLevel, out error);
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
