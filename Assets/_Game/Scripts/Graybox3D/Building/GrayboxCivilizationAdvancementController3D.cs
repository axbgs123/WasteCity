using System;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxCivilizationAdvancementCode3D
    {
        Committed,
        RequirementsMissing,
        PrepareFailed,
        OwnerCommitFailed,
    }

    public sealed class GrayboxCivilizationAdvancementResult3D
    {
        internal GrayboxCivilizationAdvancementResult3D(
            GrayboxCivilizationAdvancementCode3D code,
            bool success,
            string message,
            FormalCivilizationAscensionCommand command = null,
            string diagnostic = null)
        {
            Code = code;
            Success = success;
            Message = message ?? string.Empty;
            Command = command;
            Diagnostic = diagnostic ?? string.Empty;
        }
        public GrayboxCivilizationAdvancementCode3D Code { get; }
        public bool Success { get; }
        public string Message { get; }
        public FormalCivilizationAscensionCommand Command { get; }
        public string Diagnostic { get; }
        public string CheckpointReasonId => Command?.CheckpointReasonId ?? "";
        public string StableEventKey => Command?.StableEventKey ?? "";
        public AdvancementSequenceStage RequestedSequenceStage =>
            Success ? AdvancementSequenceStage.Scanning :
                AdvancementSequenceStage.None;
    }

    public sealed class GrayboxCivilizationAdvancementController3D
    {
        private readonly FormalCivilizationAscensionRuntime ascension;
        private readonly FormalFateRuntime fate;
        private readonly FormalAttentionRuntime attention;
        private readonly PocketUniverseFateEffect pocket;
        private readonly FormalVoidDebtRuntime debt;
        private readonly FormalRewindAnchorMetadataRuntime rewind;

        public GrayboxCivilizationAdvancementController3D(
            FormalCivilizationAscensionRuntime ascension,
            FormalFateRuntime fate,
            FormalAttentionRuntime attention,
            PocketUniverseFateEffect pocket,
            FormalVoidDebtRuntime debt,
            FormalRewindAnchorMetadataRuntime rewind)
        {
            this.ascension = ascension ?? throw new ArgumentNullException(nameof(ascension));
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.attention = attention ?? throw new ArgumentNullException(nameof(attention));
            this.pocket = pocket ?? throw new ArgumentNullException(nameof(pocket));
            this.debt = debt ?? throw new ArgumentNullException(nameof(debt));
            this.rewind = rewind ?? throw new ArgumentNullException(nameof(rewind));
        }

        public GrayboxCivilizationAdvancementResult3D Execute(
            FormalCivilizationAscensionRequirements requirements)
        {
            if (requirements == null || !requirements.CanAscend)
                return Result(GrayboxCivilizationAdvancementCode3D.RequirementsMissing,
                    "advancement.requirements-missing");
            if (!ascension.TryPrepareAscension(requirements,
                    out FormalCivilizationAscensionPlan plan, out string error))
                return Result(GrayboxCivilizationAdvancementCode3D.PrepareFailed,
                    "advancement.prepare-failed", error);

            FormalCivilizationAscensionSnapshot ascensionBefore = ascension.Capture();
            FormalFateSnapshot fateBefore = fate.Capture();
            FormalAttentionSnapshot attentionBefore = attention.Capture();
            PocketUniverseFateSnapshot pocketBefore = pocket.Capture();
            FormalVoidDebtSnapshot debtBefore = debt.Capture();
            FormalRewindAnchorMetadataSnapshot rewindBefore = rewind.Capture();
            int rewindCapacityBefore = rewind.MaximumAnchors;

            if (!fate.TryPromoteToLevelTwo(out error) ||
                !PromoteSelectedEffect(fateBefore.SelectedId, out error) ||
                !attention.TryApply(
                    ascension.AttentionReasonId,
                    FormalCivilizationAscensionCatalog.StableEventKey,
                    out error) ||
                !ascension.TryCommitAscension(plan,
                    out FormalCivilizationAscensionCommand command, out error))
            {
                bool rolledBack = Rollback(ascensionBefore, fateBefore, attentionBefore,
                    pocketBefore, debtBefore, rewindBefore,
                    rewindCapacityBefore);
                return Result(
                    GrayboxCivilizationAdvancementCode3D.OwnerCommitFailed,
                    rolledBack
                        ? "advancement.owner-commit-failed"
                        : "advancement.rollback-failed",
                    error);
            }
            return new GrayboxCivilizationAdvancementResult3D(
                GrayboxCivilizationAdvancementCode3D.Committed,
                true,
                "advancement.committed",
                command);
        }

        private bool PromoteSelectedEffect(string fateId, out string error)
        {
            if (fateId == FormalFateCatalog.PocketUniverseId)
                return pocket.TrySetLevel(2, out error);
            if (fateId == FormalFateCatalog.VoidDebtId)
            {
                FormalVoidDebtSnapshot before = debt.Capture();
                var entries = new FormalVoidDebtEntry[before.Debts.Count];
                for (var index = 0; index < entries.Length; index++)
                    entries[index] = new FormalVoidDebtEntry(
                        before.Debts[index].ResourceId,
                        before.Debts[index].Amount);
                return debt.TryRestore(new FormalVoidDebtSnapshot(
                    2, before.SettlementRemainingSeconds,
                    before.NextSettlementOrdinal, before.Revision + 1UL,
                    entries), out error);
            }
            if (fateId == FormalFateCatalog.RewindAnchorId)
                return rewind.TrySetFateLevel(2, out error);
            error = "正式命轨身份无效";
            return false;
        }

        private bool Rollback(
            FormalCivilizationAscensionSnapshot ascensionBefore,
            FormalFateSnapshot fateBefore,
            FormalAttentionSnapshot attentionBefore,
            PocketUniverseFateSnapshot pocketBefore,
            FormalVoidDebtSnapshot debtBefore,
            FormalRewindAnchorMetadataSnapshot rewindBefore,
            int rewindCapacityBefore)
        {
            bool success = ascension.TryRestore(ascensionBefore, out _);
            success &= fate.TryRestore(fateBefore, out _);
            success &= attention.TryRestore(attentionBefore, out _);
            success &= pocket.TryRestore(pocketBefore, out _);
            success &= debt.TryRestore(debtBefore, out _);
            success &= rewind.TrySetFateLevel(
                rewindCapacityBefore == 2 ? 2 : 1, out _);
            success &= rewind.TryRestore(rewindBefore, out _);
            return success;
        }

        private static GrayboxCivilizationAdvancementResult3D Result(
            GrayboxCivilizationAdvancementCode3D code,
            string message,
            string diagnostic = null) =>
            new GrayboxCivilizationAdvancementResult3D(
                code, false, message, diagnostic: diagnostic);
    }
}
