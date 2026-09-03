using System;
using System.Collections.Generic;
using System.Globalization;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    internal sealed class GrayboxIdea0028ProgressionState3D
    {
        public GrayboxIdea0028ProgressionState3D(
            QuantumEntanglementSnapshot quantum,
            SpatialTemplateSnapshot spatial,
            LocalHasteSnapshot haste,
            ForesightDelaySnapshot foresight,
            CausalTransparencySnapshot causal,
            VoidChestSnapshot chests,
            CoordinateLockSnapshot coordinate)
        {
            Quantum = quantum;
            Spatial = spatial;
            Haste = haste;
            Foresight = foresight;
            Causal = causal;
            Chests = chests;
            Coordinate = coordinate;
        }

        public QuantumEntanglementSnapshot Quantum { get; }
        public SpatialTemplateSnapshot Spatial { get; }
        public LocalHasteSnapshot Haste { get; }
        public ForesightDelaySnapshot Foresight { get; }
        public CausalTransparencySnapshot Causal { get; }
        public VoidChestSnapshot Chests { get; }
        public CoordinateLockSnapshot Coordinate { get; }
    }

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
            int targetRewindFateLevel,
            GrayboxIdea0028ProgressionState3D expectedIdea0028,
            GrayboxIdea0028ProgressionState3D targetIdea0028)
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
            ExpectedIdea0028 = expectedIdea0028;
            TargetIdea0028 = targetIdea0028;
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
        internal GrayboxIdea0028ProgressionState3D ExpectedIdea0028 { get; }
        internal GrayboxIdea0028ProgressionState3D TargetIdea0028 { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class GrayboxFormalProgressionSaveAdapter3D
    {
        public const string FormalSpatialTemplateSlotId =
            "player.template.formal-slot";

        private const string QuantumDisconnectedKey =
            "__wastecity.quantum.disconnected__";
        private const string CausalFullAccessKey =
            "__wastecity.causal.full-reason-access__";

        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;
        private readonly PocketUniverseFateEffect pocketUniverse;
        private readonly FormalVoidDebtRuntime voidDebt;
        private readonly FormalRewindAnchorMetadataRuntime rewindAnchors;
        private readonly GrayboxAttentionPressureSaveAdapter3D pressureAdapter;
        private readonly FormalCivilizationAscensionRuntime civilization;
        private readonly AdvancementSequenceModel advancementSequence;
        private QuantumEntanglementRuntime quantumEntanglement;
        private SpatialTemplateRuntime spatialTemplate;
        private LocalHasteRuntime localHaste;
        private ForesightDelayRuntime foresightDelay;
        private CausalTransparencyRuntime causalTransparency;
        private VoidChestRuntime voidChest;
        private CoordinateLockRuntime coordinateLock;
        private bool idea0028OwnersBound;

        public GrayboxFormalProgressionSaveAdapter3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            PocketUniverseFateEffect pocketUniverse,
            FormalVoidDebtRuntime voidDebt,
            FormalRewindAnchorMetadataRuntime rewindAnchors,
            GrayboxAttentionPressureSaveAdapter3D pressureAdapter = null,
            FormalCivilizationAscensionRuntime civilization = null,
            AdvancementSequenceModel advancementSequence = null)
            : this(
                attention,
                fate,
                pocketUniverse,
                voidDebt,
                rewindAnchors,
                pressureAdapter,
                civilization,
                advancementSequence,
                null,
                null,
                null,
                null,
                null,
                null,
                null)
        {
        }

        public GrayboxFormalProgressionSaveAdapter3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            PocketUniverseFateEffect pocketUniverse,
            FormalVoidDebtRuntime voidDebt,
            FormalRewindAnchorMetadataRuntime rewindAnchors,
            GrayboxAttentionPressureSaveAdapter3D pressureAdapter,
            FormalCivilizationAscensionRuntime civilization,
            AdvancementSequenceModel advancementSequence,
            QuantumEntanglementRuntime quantumEntanglement,
            SpatialTemplateRuntime spatialTemplate,
            LocalHasteRuntime localHaste,
            ForesightDelayRuntime foresightDelay,
            CausalTransparencyRuntime causalTransparency,
            VoidChestRuntime voidChest,
            CoordinateLockRuntime coordinateLock)
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
            bool anyIdea0028 = quantumEntanglement != null ||
                spatialTemplate != null || localHaste != null ||
                foresightDelay != null || causalTransparency != null ||
                voidChest != null || coordinateLock != null;
            bool allIdea0028 = quantumEntanglement != null &&
                spatialTemplate != null && localHaste != null &&
                foresightDelay != null && causalTransparency != null &&
                voidChest != null && coordinateLock != null;
            if (anyIdea0028 && !allIdea0028)
                throw new ArgumentException(
                    "IDEA-0028 persistence owners must be configured together.");
            if (allIdea0028)
            {
                BindIdea0028Owners(
                    quantumEntanglement,
                    spatialTemplate,
                    localHaste,
                    foresightDelay,
                    causalTransparency,
                    voidChest,
                    coordinateLock);
            }
        }

        public bool ConfigureIdea0028Owners(
            QuantumEntanglementRuntime quantumEntanglement,
            SpatialTemplateRuntime spatialTemplate,
            LocalHasteRuntime localHaste,
            ForesightDelayRuntime foresightDelay,
            CausalTransparencyRuntime causalTransparency,
            VoidChestRuntime voidChest,
            CoordinateLockRuntime coordinateLock,
            out string error)
        {
            if (quantumEntanglement == null || spatialTemplate == null ||
                localHaste == null || foresightDelay == null ||
                causalTransparency == null || voidChest == null ||
                coordinateLock == null ||
                !AreExistingOwnersCleanForBinding() ||
                idea0028OwnersBound && !IsCleanIdea0028Owners(
                    this.quantumEntanglement,
                    this.spatialTemplate,
                    this.localHaste,
                    this.foresightDelay,
                    this.causalTransparency,
                    this.voidChest,
                    this.coordinateLock) ||
                !IsCleanIdea0028Owners(
                    quantumEntanglement,
                    spatialTemplate,
                    localHaste,
                    foresightDelay,
                    causalTransparency,
                    voidChest,
                    coordinateLock))
            {
                error = "新增命轨持久化 owner 只能在双方清洁状态下重绑";
                return false;
            }
            BindIdea0028Owners(
                quantumEntanglement,
                spatialTemplate,
                localHaste,
                foresightDelay,
                causalTransparency,
                voidChest,
                coordinateLock);
            error = string.Empty;
            return true;
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

            FormalThreeDAttentionPressureSaveData pressureState =
                pressureAdapter?.Capture() ??
                new FormalThreeDAttentionPressureSaveData();
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
                    offerSelectionVersion =
                        fateState.OfferSelectionVersion,
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
                pressure = pressureState,
                civilization = CaptureCivilization(fateState),
                quantumEntanglement = idea0028OwnersBound
                    ? CaptureQuantum(quantumEntanglement.Capture())
                    : new FormalThreeDQuantumEntanglementSaveData(),
                spatialTemplate = idea0028OwnersBound
                    ? CaptureSpatial(spatialTemplate.Capture())
                    : new FormalThreeDSpatialTemplateSaveData(),
                localHaste = idea0028OwnersBound
                    ? CaptureHaste(localHaste.Capture())
                    : new FormalThreeDLocalHasteSaveData(),
                foresightDelay = idea0028OwnersBound
                    ? CaptureForesight(foresightDelay.Capture())
                    : new FormalThreeDForesightDelaySaveData(),
                causalTransparency = idea0028OwnersBound
                    ? CaptureCausal(causalTransparency.Capture())
                    : new FormalThreeDCausalTransparencySaveData(),
                voidChest = idea0028OwnersBound
                    ? CaptureVoidChests(voidChest.Capture())
                    : new FormalThreeDVoidChestSaveData(),
                coordinateLock = idea0028OwnersBound
                    ? CaptureCoordinate(
                        coordinateLock.Capture(),
                        pressureState)
                    : new FormalThreeDCoordinateLockSaveData(),
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
            GrayboxIdea0028ProgressionState3D idea0028Target = null;
            if (idea0028OwnersBound)
            {
                if (!TryPrepareIdea0028(data, out idea0028Target, out error))
                    return false;
            }
            else if (!HasCleanIdea0028FoundationState(data))
            {
                error = "当前进度适配器尚未配置新增命轨持久化 owner";
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
                targetRewindFateLevel,
                CaptureIdea0028State(),
                idea0028Target);
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
                            .MaximumAnchorsAtLevelOne) ||
                !MatchesIdea0028Expected(plan.ExpectedIdea0028))
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
            GrayboxIdea0028ProgressionState3D previousIdea0028 =
                CaptureIdea0028State();
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
                    out error) ||
                plan.TargetIdea0028 != null &&
                !TryRestoreIdea0028(plan.TargetIdea0028, out error))
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
                    plan.ExpectedRewindFateLevel,
                    previousIdea0028);
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
                    plan.ExpectedRewindFateLevel,
                    previousIdea0028);
                return false;
            }

            plan.Consumed = true;
            error = string.Empty;
            return true;
        }

        private void BindIdea0028Owners(
            QuantumEntanglementRuntime quantum,
            SpatialTemplateRuntime spatial,
            LocalHasteRuntime haste,
            ForesightDelayRuntime foresight,
            CausalTransparencyRuntime causal,
            VoidChestRuntime chests,
            CoordinateLockRuntime coordinate)
        {
            quantumEntanglement = quantum;
            spatialTemplate = spatial;
            localHaste = haste;
            foresightDelay = foresight;
            causalTransparency = causal;
            voidChest = chests;
            coordinateLock = coordinate;
            idea0028OwnersBound = true;
        }

        private static bool IsCleanIdea0028Owners(
            QuantumEntanglementRuntime quantum,
            SpatialTemplateRuntime spatial,
            LocalHasteRuntime haste,
            ForesightDelayRuntime foresight,
            CausalTransparencyRuntime causal,
            VoidChestRuntime chests,
            CoordinateLockRuntime coordinate)
        {
            QuantumEntanglementSnapshot quantumState = quantum.Capture();
            SpatialTemplateSnapshot spatialState = spatial.Capture();
            LocalHasteSnapshot hasteState = haste.Capture();
            ForesightDelaySnapshot foresightState = foresight.Capture();
            CausalTransparencySnapshot causalState = causal.Capture();
            VoidChestSnapshot chestState = chests.Capture();
            CoordinateLockSnapshot coordinateState = coordinate.Capture();
            return quantumState.Connected && quantumState.Revision == 0ul &&
                quantumState.CommittedSynchronizationKeys.Count == 0 &&
                spatialState.Revision == 0ul &&
                spatialState.Templates.Count == 0 &&
                hasteState.Revision == 0ul && !hasteState.Active &&
                hasteState.CurrentCycleOrdinal == 0ul &&
                string.IsNullOrEmpty(hasteState.TargetId) &&
                hasteState.RemainingBudgetSeconds ==
                    LocalHasteRuntime.LevelOneBudgetSeconds &&
                foresightState.Revision == 0ul &&
                foresightState.CurrentCycleOrdinal == 0ul &&
                foresightState.LastConsumedCycleOrdinal == 0ul &&
                foresightState.LastProjection == null &&
                causalState.Revision == 0ul &&
                !causalState.FullReasonAccess &&
                chestState.Revision == 0ul &&
                chestState.Evaluations.Count == 0 &&
                !coordinateState.Committed &&
                coordinateState.Revision == 0ul;
        }

        private bool AreExistingOwnersCleanForBinding()
        {
            FormalAttentionSnapshot attentionState = attention.Capture();
            FormalFateSnapshot fateState = fate.Capture();
            PocketUniverseFateSnapshot pocketState = pocketUniverse.Capture();
            FormalVoidDebtSnapshot debtState = voidDebt.Capture();
            FormalRewindAnchorMetadataSnapshot rewindState =
                rewindAnchors.Capture();
            FormalCivilizationAscensionSnapshot civilizationState =
                civilization?.Capture();
            return attentionState.Revision == 0ul &&
                fateState.Revision == 0ul && !fateState.HasSelection &&
                fateState.Level == 0 &&
                pocketState.Revision == 0ul &&
                pocketState.Flagships.Count == 0 &&
                pocketState.CollapsedFlagshipIds.Count == 0 &&
                debtState.Revision == 0ul && debtState.Debts.Count == 0 &&
                debtState.SettlementRemainingSeconds == 0d &&
                rewindState.Revision == 0ul && rewindState.Entries.Count == 0 &&
                (pressureAdapter == null ||
                 IsCleanPressure(pressureAdapter.Capture())) &&
                (civilizationState == null ||
                 civilizationState.Revision == 0ul &&
                 !civilizationState.Ascended) &&
                (advancementSequence == null ||
                 advancementSequence.Stage == AdvancementSequenceStage.None &&
                 advancementSequence.Remaining == 0f);
        }

        private GrayboxIdea0028ProgressionState3D CaptureIdea0028State()
        {
            return !idea0028OwnersBound
                ? null
                : new GrayboxIdea0028ProgressionState3D(
                    quantumEntanglement.Capture(),
                    spatialTemplate.Capture(),
                    localHaste.Capture(),
                    foresightDelay.Capture(),
                    causalTransparency.Capture(),
                    voidChest.Capture(),
                    coordinateLock.Capture());
        }

        private bool MatchesIdea0028Expected(
            GrayboxIdea0028ProgressionState3D expected)
        {
            if (expected == null) return !idea0028OwnersBound;
            return idea0028OwnersBound &&
                ReferenceEquals(
                    quantumEntanglement.Capture(), expected.Quantum) &&
                ReferenceEquals(spatialTemplate.Capture(), expected.Spatial) &&
                ReferenceEquals(localHaste.Capture(), expected.Haste) &&
                ReferenceEquals(foresightDelay.Capture(), expected.Foresight) &&
                ReferenceEquals(
                    causalTransparency.Capture(), expected.Causal) &&
                ReferenceEquals(voidChest.Capture(), expected.Chests) &&
                ReferenceEquals(coordinateLock.Capture(), expected.Coordinate);
        }

        private bool TryRestoreIdea0028(
            GrayboxIdea0028ProgressionState3D state,
            out string error)
        {
            if (!idea0028OwnersBound || state == null)
            {
                error = "新增命轨持久化 owner 未配置";
                return false;
            }
            return quantumEntanglement.TryRestore(state.Quantum, out error) &&
                spatialTemplate.TryRestore(state.Spatial, out error) &&
                localHaste.TryRestore(state.Haste, out error) &&
                foresightDelay.TryRestore(state.Foresight, out error) &&
                causalTransparency.TryRestore(state.Causal, out error) &&
                voidChest.TryRestore(state.Chests, out error) &&
                coordinateLock.TryRestore(state.Coordinate, out error);
        }

        private FormalThreeDQuantumEntanglementSaveData CaptureQuantum(
            QuantumEntanglementSnapshot snapshot)
        {
            int extra = snapshot.Connected ? 0 : 1;
            var keys = new string[
                snapshot.CommittedSynchronizationKeys.Count + extra];
            for (var index = 0;
                 index < snapshot.CommittedSynchronizationKeys.Count;
                 index++)
            {
                if (string.Equals(
                        snapshot.CommittedSynchronizationKeys[index],
                        QuantumDisconnectedKey,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "量子纠缠同步键与正式保留标记冲突");
                keys[index] = snapshot.CommittedSynchronizationKeys[index];
            }
            if (!snapshot.Connected) keys[keys.Length - 1] =
                QuantumDisconnectedKey;
            return new FormalThreeDQuantumEntanglementSaveData
            {
                revision = snapshot.Revision,
                committedSynchronizationKeys = keys,
            };
        }

        private static FormalThreeDSpatialTemplateSaveData CaptureSpatial(
            SpatialTemplateSnapshot snapshot)
        {
            if (snapshot.Templates.Count > 1 ||
                snapshot.Templates.Count == 1 && !string.Equals(
                    snapshot.Templates[0].Id,
                    FormalSpatialTemplateSlotId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "正式空间模板只允许一个固定槽");
            }
            if (snapshot.Templates.Count == 0)
                return new FormalThreeDSpatialTemplateSaveData
                {
                    revision = snapshot.Revision,
                };
            IReadOnlyList<SpatialTemplateCell> cells =
                snapshot.Templates[0].Cells;
            var entries = new FormalThreeDSpatialTemplateEntrySaveData[
                cells.Count];
            for (var index = 0; index < entries.Length; index++)
            {
                SpatialTemplateCell cell = cells[index];
                entries[index] = new FormalThreeDSpatialTemplateEntrySaveData
                {
                    relativeX = cell.X,
                    relativeZ = cell.Y,
                    buildingDefinitionId = cell.BuildingDefinitionId,
                    quarterTurns = cell.RotationQuarterTurns,
                };
            }
            return new FormalThreeDSpatialTemplateSaveData
            {
                revision = snapshot.Revision,
                entries = entries,
            };
        }

        private static FormalThreeDLocalHasteSaveData CaptureHaste(
            LocalHasteSnapshot snapshot)
        {
            if (snapshot.CurrentCycleOrdinal > long.MaxValue)
                throw new InvalidOperationException(
                    "局部时加周期序号超出正式存档范围");
            if (!LocalHasteRuntime.TryGetTargetKind(
                    snapshot.TargetId,
                    out int targetKind))
                throw new InvalidOperationException(
                    "局部时加运行时目标不属于正式区域");
            return new FormalThreeDLocalHasteSaveData
            {
                revision = snapshot.Revision,
                cycleOrdinal = (long)snapshot.CurrentCycleOrdinal,
                remainingBudgetSeconds = snapshot.RemainingBudgetSeconds,
                targetKind = targetKind,
                targetStableId = snapshot.TargetId,
                active = snapshot.Active,
            };
        }

        private static FormalThreeDForesightDelaySaveData CaptureForesight(
            ForesightDelaySnapshot snapshot)
        {
            if (snapshot.CurrentCycleOrdinal > long.MaxValue ||
                snapshot.LastConsumedCycleOrdinal > long.MaxValue)
                throw new InvalidOperationException(
                    "预知迟滞周期序号超出正式存档范围");
            long cycle = (long)snapshot.CurrentCycleOrdinal;
            long lastConsumed = (long)snapshot.LastConsumedCycleOrdinal;
            return new FormalThreeDForesightDelaySaveData
            {
                revision = snapshot.Revision,
                cycleOrdinal = cycle,
                plannedStableEventId =
                    snapshot.LastProjection?.EventId ?? string.Empty,
                remainingDisplaySeconds = snapshot.DisplayRemainingSeconds,
                displayedCycleOrdinals =
                    snapshot.LastConsumedCycleOrdinal == 0ul
                        ? Array.Empty<long>()
                        : new[] { lastConsumed },
            };
        }

        private static FormalThreeDCausalTransparencySaveData CaptureCausal(
            CausalTransparencySnapshot snapshot)
        {
            return new FormalThreeDCausalTransparencySaveData
            {
                revision = snapshot.Revision,
                scannedStableEventKeys = snapshot.FullReasonAccess
                    ? new[] { CausalFullAccessKey }
                    : Array.Empty<string>(),
            };
        }

        private static FormalThreeDVoidChestSaveData CaptureVoidChests(
            VoidChestSnapshot snapshot)
        {
            var committed = new string[snapshot.Evaluations.Count];
            var pending = new List<FormalThreeDVoidChestEntrySaveData>();
            ulong highestOrdinal = 0ul;
            for (var index = 0; index < snapshot.Evaluations.Count; index++)
            {
                VoidChestEvaluation evaluation = snapshot.Evaluations[index];
                committed[index] = EncodeDeathEvaluation(
                    evaluation.DeathId,
                    evaluation.SequenceOrdinal);
                highestOrdinal = Math.Max(
                    highestOrdinal,
                    evaluation.SequenceOrdinal);
                if (!evaluation.Dropped || evaluation.Claimed) continue;
                pending.Add(new FormalThreeDVoidChestEntrySaveData
                {
                    stableChestId = evaluation.ChestId,
                    dropOrdinal = checked((long)evaluation.SequenceOrdinal),
                    deathEventId = evaluation.DeathId,
                    resourceId = evaluation.ResourceId,
                    amount = evaluation.Amount,
                    narrativeFragmentId = evaluation.NarrativeFragmentId,
                    rewardKey = evaluation.ChestId,
                });
            }
            if (highestOrdinal >= long.MaxValue)
                throw new InvalidOperationException(
                    "虚空宝箱掉落序号超出正式存档范围");
            return new FormalThreeDVoidChestSaveData
            {
                revision = snapshot.Revision,
                nextDropOrdinal = checked((long)highestOrdinal + 1L),
                pendingChests = pending.ToArray(),
                committedDeathEventIds = committed,
                claimedRewardKeys = Copy(snapshot.ClaimedChestIds),
            };
        }

        internal static bool TryMergeVoidChestMonotonic(
            FormalThreeDVoidChestSaveData current,
            FormalThreeDVoidChestSaveData historical,
            out FormalThreeDVoidChestSaveData merged,
            out string error)
        {
            merged = null;
            var encodedByDeath = new Dictionary<string, string>(
                StringComparer.Ordinal);
            var ordinalByDeath = new Dictionary<string, ulong>(
                StringComparer.Ordinal);
            var deathByOrdinal = new Dictionary<ulong, string>();
            if (!TryCollectVoidChestCommitted(
                    current,
                    encodedByDeath,
                    ordinalByDeath,
                    deathByOrdinal,
                    out error) ||
                !TryCollectVoidChestCommitted(
                    historical,
                    encodedByDeath,
                    ordinalByDeath,
                    deathByOrdinal,
                    out error))
            {
                return false;
            }

            var claimed = new HashSet<string>(StringComparer.Ordinal);
            if (!TryCollectNonBlankUnique(
                    current?.claimedRewardKeys,
                    claimed,
                    out error) ||
                !TryCollectNonBlankUnique(
                    historical?.claimedRewardKeys,
                    claimed,
                    out error))
            {
                return false;
            }

            var pendingById = new Dictionary<
                string,
                FormalThreeDVoidChestEntrySaveData>(StringComparer.Ordinal);
            if (!TryCollectVoidChestPending(
                    current?.pendingChests,
                    pendingById,
                    out error) ||
                !TryCollectVoidChestPending(
                    historical?.pendingChests,
                    pendingById,
                    out error))
            {
                return false;
            }

            var pending = new List<FormalThreeDVoidChestEntrySaveData>();
            foreach (FormalThreeDVoidChestEntrySaveData entry in
                     pendingById.Values)
            {
                if (!ordinalByDeath.TryGetValue(
                        entry.deathEventId,
                        out ulong ordinal) ||
                    ordinal > long.MaxValue ||
                    checked((long)ordinal) != entry.dropOrdinal)
                {
                    error = "虚空宝箱单调合并缺少对应死亡事件";
                    return false;
                }
                if (claimed.Contains(entry.rewardKey)) continue;
                pending.Add(CloneVoidChestEntry(entry));
            }

            var deathIds = new List<string>(encodedByDeath.Keys);
            deathIds.Sort(StringComparer.Ordinal);
            var committed = new string[deathIds.Count];
            ulong highestOrdinal = 0ul;
            for (var index = 0; index < deathIds.Count; index++)
            {
                string deathId = deathIds[index];
                committed[index] = encodedByDeath[deathId];
                highestOrdinal = Math.Max(
                    highestOrdinal,
                    ordinalByDeath[deathId]);
            }
            if (highestOrdinal >= long.MaxValue)
            {
                error = "虚空宝箱单调合并序号超出正式存档范围";
                return false;
            }

            pending.Sort((left, right) => string.CompareOrdinal(
                left.stableChestId,
                right.stableChestId));
            var claimedKeys = new string[claimed.Count];
            claimed.CopyTo(claimedKeys);
            Array.Sort(claimedKeys, StringComparer.Ordinal);
            merged = new FormalThreeDVoidChestSaveData
            {
                revision = checked((ulong)committed.Length +
                    (ulong)claimedKeys.Length),
                nextDropOrdinal = checked((long)highestOrdinal + 1L),
                pendingChests = pending.ToArray(),
                committedDeathEventIds = committed,
                claimedRewardKeys = claimedKeys,
            };
            error = string.Empty;
            return true;
        }

        private static FormalThreeDCoordinateLockSaveData CaptureCoordinate(
            CoordinateLockSnapshot snapshot,
            FormalThreeDAttentionPressureSaveData pressure)
        {
            bool bossPressureScheduled = snapshot.Committed &&
                HasCoordinateBossPressure(pressure);
            if (snapshot.Committed && !bossPressureScheduled)
                throw new InvalidOperationException(
                    "坐标锁定已提交但 90 关注度 Boss 压力不存在");
            return new FormalThreeDCoordinateLockSaveData
            {
                revision = snapshot.Revision,
                committed = snapshot.Committed,
                stableEventKey = snapshot.Committed
                    ? CoordinateLockCatalog.StableEventKey
                    : string.Empty,
                bossPressureScheduled = bossPressureScheduled,
            };
        }

        private bool TryPrepareIdea0028(
            FormalThreeDProgressionSaveData data,
            out GrayboxIdea0028ProgressionState3D state,
            out string error)
        {
            state = null;
            if (!TryPrepareQuantum(
                    data.quantumEntanglement,
                    out QuantumEntanglementSnapshot quantum,
                    out error) ||
                !TryPrepareSpatial(
                    data.spatialTemplate,
                    out SpatialTemplateSnapshot spatial,
                    out error) ||
                !TryPrepareHaste(
                    data.localHaste,
                    out LocalHasteSnapshot haste,
                    out error) ||
                !TryPrepareForesight(
                    data.foresightDelay,
                    data.pressure,
                    out ForesightDelaySnapshot foresight,
                    out error) ||
                !TryPrepareCausal(
                    data.causalTransparency,
                    out CausalTransparencySnapshot causal,
                    out error) ||
                !TryPrepareVoidChests(
                    data.voidChest,
                    out VoidChestSnapshot chests,
                    out error) ||
                !TryPrepareCoordinate(
                    data.coordinateLock,
                    data.pressure,
                    out CoordinateLockSnapshot coordinate,
                    out error))
                return false;
            state = new GrayboxIdea0028ProgressionState3D(
                quantum,
                spatial,
                haste,
                foresight,
                causal,
                chests,
                coordinate);
            error = string.Empty;
            return true;
        }

        private bool TryPrepareQuantum(
            FormalThreeDQuantumEntanglementSaveData data,
            out QuantumEntanglementSnapshot snapshot,
            out string error)
        {
            if (quantumEntanglement == null)
            {
                snapshot = null;
                error = "量子纠缠持久化 owner 未配置";
                return false;
            }
            bool connected = true;
            var synchronizationKeys = new List<string>();
            for (var index = 0;
                 index < data.committedSynchronizationKeys.Length;
                 index++)
            {
                string key = data.committedSynchronizationKeys[index];
                if (string.Equals(
                        key, QuantumDisconnectedKey, StringComparison.Ordinal))
                {
                    if (!connected)
                    {
                        snapshot = null;
                        error = "量子纠缠断连标记重复";
                        return false;
                    }
                    connected = false;
                }
                else
                {
                    synchronizationKeys.Add(key);
                }
            }
            string[] fixedResourceIds = Copy(
                quantumEntanglement.Capture().SharedResourceIds);
            var validator = new QuantumEntanglementRuntime(fixedResourceIds);
            var candidate = new QuantumEntanglementSnapshot(
                connected,
                data.revision,
                fixedResourceIds,
                synchronizationKeys.ToArray());
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static bool TryPrepareSpatial(
            FormalThreeDSpatialTemplateSaveData data,
            out SpatialTemplateSnapshot snapshot,
            out string error)
        {
            var cells = new SpatialTemplateCell[data.entries.Length];
            for (var index = 0; index < cells.Length; index++)
            {
                FormalThreeDSpatialTemplateEntrySaveData entry =
                    data.entries[index];
                if (entry == null)
                {
                    snapshot = null;
                    error = "空间模板记录不能为空";
                    return false;
                }
                cells[index] = new SpatialTemplateCell(
                    entry.relativeX,
                    entry.relativeZ,
                    entry.buildingDefinitionId,
                    entry.quarterTurns);
            }
            SpatialTemplateDefinition[] templates = cells.Length == 0
                ? Array.Empty<SpatialTemplateDefinition>()
                : new[]
                {
                    new SpatialTemplateDefinition(
                        FormalSpatialTemplateSlotId,
                        cells),
                };
            var candidate = new SpatialTemplateSnapshot(
                data.revision,
                templates);
            var validator = new SpatialTemplateRuntime();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static bool TryPrepareHaste(
            FormalThreeDLocalHasteSaveData data,
            out LocalHasteSnapshot snapshot,
            out string error)
        {
            if (data.cycleOrdinal < 0L ||
                !LocalHasteRuntime.TryGetTargetKind(
                    data.targetStableId,
                    out int expectedTargetKind) ||
                data.targetKind != expectedTargetKind)
            {
                snapshot = null;
                error = "局部时加目标类型或周期字段无效";
                return false;
            }
            var candidate = new LocalHasteSnapshot(
                data.targetStableId,
                data.active,
                data.remainingBudgetSeconds,
                data.revision,
                (ulong)data.cycleOrdinal);
            var validator = new LocalHasteRuntime();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static bool TryPrepareForesight(
            FormalThreeDForesightDelaySaveData data,
            FormalThreeDAttentionPressureSaveData pressure,
            out ForesightDelaySnapshot snapshot,
            out string error)
        {
            if (data.cycleOrdinal < 0L ||
                float.IsNaN(data.remainingDisplaySeconds) ||
                float.IsInfinity(data.remainingDisplaySeconds) ||
                data.remainingDisplaySeconds < 0f ||
                data.remainingDisplaySeconds >
                    FormalFateCatalog.ForesightDisplaySeconds)
            {
                snapshot = null;
                error = "预知迟滞周期或显示时间无效";
                return false;
            }
            ForesightProjection projection = null;
            ulong lastConsumed = 0ul;
            if (data.cycleOrdinal == 0L)
            {
                if (!string.IsNullOrEmpty(data.plannedStableEventId) ||
                    data.remainingDisplaySeconds != 0f ||
                    data.displayedCycleOrdinals.Length != 0)
                {
                    snapshot = null;
                    error = "预知迟滞清洁状态不一致";
                    return false;
                }
            }
            else
            {
                if (data.displayedCycleOrdinals.Length > 1 ||
                    data.displayedCycleOrdinals.Length == 1 &&
                    (data.displayedCycleOrdinals[0] <= 0L ||
                     data.displayedCycleOrdinals[0] > data.cycleOrdinal))
                {
                    snapshot = null;
                    error = "预知迟滞展示周期索引不一致";
                    return false;
                }
                if (data.displayedCycleOrdinals.Length == 1)
                    lastConsumed =
                        (ulong)data.displayedCycleOrdinals[0];
                bool hasProjection = !string.IsNullOrEmpty(
                    data.plannedStableEventId);
                if ((hasProjection &&
                     (lastConsumed != (ulong)data.cycleOrdinal ||
                      data.remainingDisplaySeconds <= 0f)) ||
                    (!hasProjection && data.remainingDisplaySeconds != 0f))
                {
                    snapshot = null;
                    error = "预知迟滞当前预览与已消费周期不一致";
                    return false;
                }
                if (hasProjection)
                {
                    if (!IsPlannedPressureEvent(
                            pressure,
                            data.plannedStableEventId))
                    {
                        snapshot = null;
                        error = "预知迟滞计划不属于权威关注度压力队列";
                        return false;
                    }
                    projection = new ForesightProjection(
                        data.plannedStableEventId,
                        data.plannedStableEventId + ".summary",
                        0f,
                        0f);
                }
            }
            var candidate = new ForesightDelaySnapshot(
                (ulong)data.cycleOrdinal,
                lastConsumed,
                projection,
                data.remainingDisplaySeconds,
                data.revision);
            var validator = new ForesightDelayRuntime();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static bool IsPlannedPressureEvent(
            FormalThreeDAttentionPressureSaveData pressure,
            string stableEventId)
        {
            if (pressure?.entries == null ||
                string.IsNullOrEmpty(stableEventId))
                return false;
            for (var index = 0; index < pressure.entries.Length; index++)
            {
                FormalThreeDAttentionPressureEntrySaveData entry =
                    pressure.entries[index];
                if (entry == null ||
                    entry.state != (int)AttentionPressureState.Queued &&
                    entry.state != (int)AttentionPressureState.Warning)
                    continue;
                AttentionPressureDefinition definition =
                    AttentionPressureCatalog.FindByThreshold(entry.threshold);
                if (definition != null && string.Equals(
                        definition.EncounterId.Value,
                        stableEventId,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool TryPrepareCausal(
            FormalThreeDCausalTransparencySaveData data,
            out CausalTransparencySnapshot snapshot,
            out string error)
        {
            bool fullAccess = data.scannedStableEventKeys.Length == 1 &&
                string.Equals(
                    data.scannedStableEventKeys[0],
                    CausalFullAccessKey,
                    StringComparison.Ordinal);
            if (data.scannedStableEventKeys.Length > 1 ||
                data.scannedStableEventKeys.Length == 1 && !fullAccess)
            {
                snapshot = null;
                error = "因果透明扫描标记无效";
                return false;
            }
            var candidate = new CausalTransparencySnapshot(
                fullAccess,
                data.revision);
            var validator = new CausalTransparencyRuntime();
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private bool TryPrepareVoidChests(
            FormalThreeDVoidChestSaveData data,
            out VoidChestSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            var pendingById = new Dictionary<
                string,
                FormalThreeDVoidChestEntrySaveData>(StringComparer.Ordinal);
            for (var index = 0; index < data.pendingChests.Length; index++)
            {
                FormalThreeDVoidChestEntrySaveData entry =
                    data.pendingChests[index];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.stableChestId) ||
                    !pendingById.TryAdd(entry.stableChestId, entry))
                {
                    error = "虚空宝箱待领取记录无效或重复";
                    return false;
                }
            }
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < data.claimedRewardKeys.Length; index++)
            {
                string key = data.claimedRewardKeys[index];
                if (string.IsNullOrWhiteSpace(key) || !claimed.Add(key))
                {
                    error = "虚空宝箱已领取键无效或重复";
                    return false;
                }
            }

            var validator = new VoidChestRuntime(
                voidChest.SessionId,
                voidChest.SelectionVersion);
            ulong highestOrdinal = 0ul;
            var consumedPending = new HashSet<string>(StringComparer.Ordinal);
            var consumedClaimed = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 index < data.committedDeathEventIds.Length;
                 index++)
            {
                if (!TryDecodeDeathEvaluation(
                        data.committedDeathEventIds[index],
                        out string deathId,
                        out ulong ordinal))
                {
                    error = "虚空宝箱稳定死亡事件编码无效";
                    return false;
                }
                if (!validator.TryEvaluateDeath(
                        deathId,
                        ordinal,
                        out VoidChestEvaluation evaluation,
                        out error))
                    return false;
                highestOrdinal = Math.Max(highestOrdinal, ordinal);
                if (!evaluation.Dropped) continue;
                if (claimed.Contains(evaluation.ChestId))
                {
                    if (!validator.TryClaim(evaluation.ChestId, out error))
                        return false;
                    consumedClaimed.Add(evaluation.ChestId);
                    continue;
                }
                if (ordinal > long.MaxValue ||
                    !pendingById.TryGetValue(
                        evaluation.ChestId,
                        out FormalThreeDVoidChestEntrySaveData pending) ||
                    pending.dropOrdinal != checked((long)ordinal) ||
                    !string.Equals(
                        pending.deathEventId,
                        evaluation.DeathId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        pending.resourceId,
                        evaluation.ResourceId,
                        StringComparison.Ordinal) ||
                    pending.amount != evaluation.Amount ||
                    !string.Equals(
                        pending.narrativeFragmentId,
                        evaluation.NarrativeFragmentId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        pending.rewardKey,
                        evaluation.ChestId,
                        StringComparison.Ordinal))
                {
                    error = "虚空宝箱确定性奖励与待领取记录不一致";
                    return false;
                }
                consumedPending.Add(evaluation.ChestId);
            }
            if (highestOrdinal >= long.MaxValue ||
                data.nextDropOrdinal != checked((long)highestOrdinal + 1L) ||
                consumedPending.Count != pendingById.Count ||
                consumedClaimed.Count != claimed.Count ||
                validator.Capture().Revision != data.revision)
            {
                error = "虚空宝箱序号、提交集或修订号不一致";
                return false;
            }
            snapshot = validator.Capture();
            error = string.Empty;
            return true;
        }

        private static bool TryPrepareCoordinate(
            FormalThreeDCoordinateLockSaveData data,
            FormalThreeDAttentionPressureSaveData pressure,
            out CoordinateLockSnapshot snapshot,
            out string error)
        {
            if (data.committed != data.bossPressureScheduled ||
                !string.Equals(
                    data.stableEventKey,
                    data.committed
                        ? CoordinateLockCatalog.StableEventKey
                        : string.Empty,
                    StringComparison.Ordinal))
            {
                snapshot = null;
                error = "坐标锁定事件键或压力调度标记不一致";
                return false;
            }
            if (data.committed && !HasCoordinateBossPressure(pressure))
            {
                snapshot = null;
                error = "坐标锁定缺少 90 关注度 Boss 压力计划";
                return false;
            }
            var candidate = new CoordinateLockSnapshot(
                data.committed,
                data.revision);
            var validator = new CoordinateLockRuntime(
                new FormalAttentionRuntime(),
                new AttentionPressureRuntime());
            if (!validator.TryRestore(candidate, out error))
            {
                snapshot = null;
                return false;
            }
            snapshot = validator.Capture();
            return true;
        }

        private static bool HasCoordinateBossPressure(
            FormalThreeDAttentionPressureSaveData pressure)
        {
            if (pressure?.entries == null) return false;
            for (var index = 0; index < pressure.entries.Length; index++)
            {
                FormalThreeDAttentionPressureEntrySaveData entry =
                    pressure.entries[index];
                if (entry != null &&
                    entry.threshold == CoordinateLockCatalog.TargetAttention)
                    return true;
            }
            return false;
        }

        private static string EncodeDeathEvaluation(
            string deathId,
            ulong ordinal)
        {
            return deathId.Length.ToString(CultureInfo.InvariantCulture) +
                ":" + deathId + ":" +
                ordinal.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryCollectVoidChestCommitted(
            FormalThreeDVoidChestSaveData source,
            IDictionary<string, string> encodedByDeath,
            IDictionary<string, ulong> ordinalByDeath,
            IDictionary<ulong, string> deathByOrdinal,
            out string error)
        {
            if (source?.committedDeathEventIds == null)
            {
                error = "虚空宝箱单调合并来源不完整";
                return false;
            }
            for (var index = 0;
                 index < source.committedDeathEventIds.Length;
                 index++)
            {
                if (!TryDecodeDeathEvaluation(
                        source.committedDeathEventIds[index],
                        out string deathId,
                        out ulong ordinal))
                {
                    error = "虚空宝箱单调合并死亡事件编码无效";
                    return false;
                }
                if (ordinalByDeath.TryGetValue(
                        deathId,
                        out ulong existingOrdinal))
                {
                    if (existingOrdinal != ordinal)
                    {
                        error = "同一死亡事件的虚空宝箱序号发生冲突";
                        return false;
                    }
                    continue;
                }
                if (deathByOrdinal.TryGetValue(
                        ordinal,
                        out string existingDeathId) &&
                    !string.Equals(
                        existingDeathId,
                        deathId,
                        StringComparison.Ordinal))
                {
                    error = "虚空宝箱死亡事件序号发生冲突";
                    return false;
                }
                ordinalByDeath.Add(deathId, ordinal);
                deathByOrdinal.Add(ordinal, deathId);
                encodedByDeath.Add(
                    deathId,
                    EncodeDeathEvaluation(deathId, ordinal));
            }
            error = string.Empty;
            return true;
        }

        private static bool TryCollectNonBlankUnique(
            string[] source,
            ISet<string> destination,
            out string error)
        {
            if (source == null)
            {
                error = "虚空宝箱单调合并领取键不完整";
                return false;
            }
            for (var index = 0; index < source.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(source[index]))
                {
                    error = "虚空宝箱单调合并领取键无效";
                    return false;
                }
                destination.Add(source[index]);
            }
            error = string.Empty;
            return true;
        }

        private static bool TryCollectVoidChestPending(
            FormalThreeDVoidChestEntrySaveData[] source,
            IDictionary<string, FormalThreeDVoidChestEntrySaveData>
                destination,
            out string error)
        {
            if (source == null)
            {
                error = "虚空宝箱单调合并待领取记录不完整";
                return false;
            }
            for (var index = 0; index < source.Length; index++)
            {
                FormalThreeDVoidChestEntrySaveData entry = source[index];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.stableChestId) ||
                    string.IsNullOrWhiteSpace(entry.deathEventId) ||
                    string.IsNullOrWhiteSpace(entry.rewardKey))
                {
                    error = "虚空宝箱单调合并待领取记录无效";
                    return false;
                }
                if (destination.TryGetValue(
                        entry.stableChestId,
                        out FormalThreeDVoidChestEntrySaveData existing))
                {
                    if (!MatchesVoidChestEntry(existing, entry))
                    {
                        error = "同一虚空宝箱的待领取记录发生冲突";
                        return false;
                    }
                    continue;
                }
                destination.Add(
                    entry.stableChestId,
                    CloneVoidChestEntry(entry));
            }
            error = string.Empty;
            return true;
        }

        private static bool MatchesVoidChestEntry(
            FormalThreeDVoidChestEntrySaveData left,
            FormalThreeDVoidChestEntrySaveData right)
        {
            return left.dropOrdinal == right.dropOrdinal &&
                left.amount == right.amount &&
                string.Equals(
                    left.stableChestId,
                    right.stableChestId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.deathEventId,
                    right.deathEventId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.resourceId,
                    right.resourceId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.narrativeFragmentId,
                    right.narrativeFragmentId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    left.rewardKey,
                    right.rewardKey,
                    StringComparison.Ordinal);
        }

        private static FormalThreeDVoidChestEntrySaveData CloneVoidChestEntry(
            FormalThreeDVoidChestEntrySaveData source)
        {
            return new FormalThreeDVoidChestEntrySaveData
            {
                stableChestId = source.stableChestId,
                dropOrdinal = source.dropOrdinal,
                deathEventId = source.deathEventId,
                resourceId = source.resourceId,
                amount = source.amount,
                narrativeFragmentId = source.narrativeFragmentId,
                rewardKey = source.rewardKey,
            };
        }

        private static bool TryDecodeDeathEvaluation(
            string value,
            out string deathId,
            out ulong ordinal)
        {
            deathId = string.Empty;
            ordinal = 0ul;
            if (string.IsNullOrEmpty(value)) return false;
            int separator = value.IndexOf(':');
            if (separator <= 0 || !int.TryParse(
                    value.Substring(0, separator),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int length) ||
                length <= 0 || separator + 1 + length >= value.Length ||
                value[separator + 1 + length] != ':')
                return false;
            deathId = value.Substring(separator + 1, length);
            return !string.IsNullOrWhiteSpace(deathId) && ulong.TryParse(
                value.Substring(separator + 2 + length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out ordinal) && ordinal > 0ul;
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
                data.quantumEntanglement != null &&
                data.quantumEntanglement.committedSynchronizationKeys != null &&
                data.spatialTemplate != null &&
                data.spatialTemplate.entries != null &&
                data.localHaste != null &&
                data.foresightDelay != null &&
                data.foresightDelay.displayedCycleOrdinals != null &&
                data.causalTransparency != null &&
                data.causalTransparency.scannedStableEventKeys != null &&
                data.voidChest != null &&
                data.voidChest.pendingChests != null &&
                data.voidChest.committedDeathEventIds != null &&
                data.voidChest.claimedRewardKeys != null &&
                data.coordinateLock != null &&
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

        private static bool HasCleanIdea0028FoundationState(
            FormalThreeDProgressionSaveData data)
        {
            return data.quantumEntanglement.revision == 0ul &&
                data.quantumEntanglement.committedSynchronizationKeys.Length == 0 &&
                data.spatialTemplate.revision == 0ul &&
                data.spatialTemplate.entries.Length == 0 &&
                data.localHaste.revision == 0ul &&
                data.localHaste.cycleOrdinal == 0L &&
                data.localHaste.remainingBudgetSeconds == 60f &&
                data.localHaste.targetKind == 0 &&
                string.IsNullOrEmpty(data.localHaste.targetStableId) &&
                !data.localHaste.active &&
                data.foresightDelay.revision == 0ul &&
                data.foresightDelay.cycleOrdinal == 0L &&
                string.IsNullOrEmpty(
                    data.foresightDelay.plannedStableEventId) &&
                data.foresightDelay.remainingDisplaySeconds == 0f &&
                data.foresightDelay.displayedCycleOrdinals.Length == 0 &&
                data.causalTransparency.revision == 0ul &&
                data.causalTransparency.scannedStableEventKeys.Length == 0 &&
                data.voidChest.revision == 0ul &&
                data.voidChest.nextDropOrdinal == 1L &&
                data.voidChest.pendingChests.Length == 0 &&
                data.voidChest.committedDeathEventIds.Length == 0 &&
                data.voidChest.claimedRewardKeys.Length == 0 &&
                data.coordinateLock.revision == 0ul &&
                !data.coordinateLock.committed &&
                string.IsNullOrEmpty(data.coordinateLock.stableEventKey) &&
                !data.coordinateLock.bossPressureScheduled;
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
                data.level,
                data.offerSelectionVersion);
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
            int rewindFateLevel,
            GrayboxIdea0028ProgressionState3D idea0028State)
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
            if (idea0028State != null)
                TryRestoreIdea0028(idea0028State, out _);
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
