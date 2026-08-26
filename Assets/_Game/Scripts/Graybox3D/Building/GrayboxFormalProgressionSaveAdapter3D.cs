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
            FormalAttentionSnapshot targetAttention,
            FormalFateSnapshot targetFate)
        {
            Owner = owner;
            ExpectedAttention = expectedAttention;
            ExpectedFate = expectedFate;
            TargetAttention = targetAttention;
            TargetFate = targetFate;
        }

        internal GrayboxFormalProgressionSaveAdapter3D Owner { get; }
        internal FormalAttentionSnapshot ExpectedAttention { get; }
        internal FormalFateSnapshot ExpectedFate { get; }
        internal FormalAttentionSnapshot TargetAttention { get; }
        internal FormalFateSnapshot TargetFate { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class GrayboxFormalProgressionSaveAdapter3D
    {
        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;

        public GrayboxFormalProgressionSaveAdapter3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
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
                    reachedThresholds = Copy(attentionState.ReachedThresholds),
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
            if (data == null ||
                !string.Equals(
                    data.configurationSignature,
                    FormalThreeDProgressionSaveData.ConfigurationSignature,
                    StringComparison.Ordinal) ||
                data.attention == null || data.fate == null ||
                data.civilization == null ||
                data.attention.history == null ||
                data.attention.reachedThresholds == null ||
                data.attention.committedStableEventKeys == null ||
                data.attention.completedOneShotReasonIds == null ||
                data.fate.offeredIds == null ||
                data.civilization.committedAscensionIds == null)
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

            var history = new FormalAttentionHistoryEntry[
                data.attention.history.Length];
            for (var index = 0; index < history.Length; index++)
            {
                FormalThreeDAttentionHistorySaveData entry =
                    data.attention.history[index];
                if (entry == null)
                {
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

            var attentionCandidate = new FormalAttentionSnapshot(
                data.attention.value,
                data.attention.revision,
                history,
                Copy(data.attention.reachedThresholds),
                Copy(data.attention.committedStableEventKeys),
                Copy(data.attention.completedOneShotReasonIds));
            var attentionValidator = new FormalAttentionRuntime();
            if (!attentionValidator.TryRestore(
                    attentionCandidate,
                    out string attentionError))
            {
                error = "关注度存档无效：" + attentionError;
                return false;
            }

            var fateCandidate = new FormalFateSnapshot(
                data.fate.revision,
                Copy(data.fate.offeredIds),
                data.fate.selectedId,
                data.fate.level);
            var fateValidator = new FormalFateRuntime();
            if (!fateValidator.TryRestore(
                    fateCandidate,
                    out string fateError))
            {
                error = "命轨存档无效：" + fateError;
                return false;
            }

            plan = new GrayboxFormalProgressionRestorePlan3D(
                this,
                attention.Capture(),
                fate.Capture(),
                attentionValidator.Capture(),
                fateValidator.Capture());
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
            if (!ReferenceEquals(
                    attention.Capture(),
                    plan.ExpectedAttention) ||
                !ReferenceEquals(fate.Capture(), plan.ExpectedFate))
            {
                error = "正式进度恢复计划已过期";
                return false;
            }

            FormalAttentionSnapshot previousAttention = attention.Capture();
            FormalFateSnapshot previousFate = fate.Capture();
            if (!attention.TryRestore(plan.TargetAttention, out error))
                return false;
            if (!fate.TryRestore(plan.TargetFate, out string fateError))
            {
                if (!attention.TryRestore(
                        previousAttention,
                        out string rollbackError))
                {
                    error = fateError + "；关注度回滚失败：" + rollbackError;
                    return false;
                }
                if (!ReferenceEquals(fate.Capture(), previousFate))
                {
                    error = fateError + "；命轨状态在失败提交中发生变化";
                    return false;
                }
                error = fateError;
                return false;
            }

            plan.Consumed = true;
            error = string.Empty;
            return true;
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
