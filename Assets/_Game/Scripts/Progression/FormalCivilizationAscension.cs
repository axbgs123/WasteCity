using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Progression
{
    public sealed class FormalCivilizationAscensionRequirements
    {
        private readonly ReadOnlyCollection<
            FormalCivilizationAscensionRequirementStatus> statuses;
        public FormalCivilizationAscensionRequirements(
            bool legacyAnalysisCompleted,
            int completedPlayerMachineGunTurretCount,
            bool crystalBroodmotherDefeated,
            bool productionCurrentlyRunning)
        {
            LegacyAnalysisCompleted = legacyAnalysisCompleted;
            CompletedPlayerMachineGunTurretCount = Math.Max(
                0, completedPlayerMachineGunTurretCount);
            CrystalBroodmotherDefeated = crystalBroodmotherDefeated;
            ProductionCurrentlyRunning = productionCurrentlyRunning;
            statuses = Array.AsReadOnly(new[]
            {
                new FormalCivilizationAscensionRequirementStatus(
                    "legacy-analysis", "遗产解析", legacyAnalysisCompleted,
                    "尚未完成遗产解析"),
                new FormalCivilizationAscensionRequirementStatus(
                    "machine-gun-turrets", "机枪塔防线",
                    CompletedPlayerMachineGunTurretCount >= 2,
                    "需要至少两座已完成的玩家机枪塔"),
                new FormalCivilizationAscensionRequirementStatus(
                    "crystal-broodmother", "晶壳母体",
                    crystalBroodmotherDefeated, "尚未击败晶壳母体"),
                new FormalCivilizationAscensionRequirementStatus(
                    "production-running", "生产运行",
                    productionCurrentlyRunning, "当前没有可运行的生产建筑"),
            });
        }

        public bool LegacyAnalysisCompleted { get; }
        public int CompletedPlayerMachineGunTurretCount { get; }
        public bool CrystalBroodmotherDefeated { get; }
        public bool ProductionCurrentlyRunning { get; }
        public bool CanAscend => LegacyAnalysisCompleted &&
            CompletedPlayerMachineGunTurretCount >= 2 &&
            CrystalBroodmotherDefeated && ProductionCurrentlyRunning;
        public IReadOnlyList<FormalCivilizationAscensionRequirementStatus>
            Statuses => statuses;
    }

    public sealed class FormalCivilizationAscensionRequirementStatus
    {
        internal FormalCivilizationAscensionRequirementStatus(
            string stableId, string displayName, bool isMet, string missingText)
        {
            StableId = stableId;
            DisplayName = displayName;
            IsMet = isMet;
            MissingText = missingText;
        }
        public string StableId { get; }
        public string DisplayName { get; }
        public bool IsMet { get; }
        public string MissingText { get; }
    }

    public sealed class FormalCivilizationAscensionPlan
    {
        internal FormalCivilizationAscensionPlan(
            FormalCivilizationAscensionRuntime owner,
            FormalCivilizationAscensionSnapshot expected)
        {
            Owner = owner;
            Expected = expected;
        }
        internal FormalCivilizationAscensionRuntime Owner { get; }
        internal FormalCivilizationAscensionSnapshot Expected { get; }
        internal bool Consumed { get; set; }
    }

    public static class FormalCivilizationAscensionCatalog
    {
        public const int TargetCivilizationLevel = 2;
        public const int TargetFateLevel = 2;
        public const string AttentionReasonId =
            "core.attention.civilization.advanced";
        public const string StableEventKey =
            "first-civilization-ascension";
        public const string CheckpointReasonId =
            "first-civilization-ascension";

        public static int AttentionReward =>
            FormalAttentionCatalog.Find(AttentionReasonId)?.Delta ?? 0;
    }

    public sealed class FormalCivilizationAscensionCommand
    {
        internal FormalCivilizationAscensionCommand(string fateId)
        {
            FateId = fateId;
            AttentionReasonId =
                FormalCivilizationAscensionCatalog.AttentionReasonId;
            StableEventKey =
                FormalCivilizationAscensionCatalog.StableEventKey;
            CheckpointReasonId =
                FormalCivilizationAscensionCatalog.CheckpointReasonId;
            AttentionDelta =
                FormalCivilizationAscensionCatalog.AttentionReward;
            PocketUniverseOutputMultiplier =
                FormalFateLevelTwoCatalog.PocketUniverseOutputMultiplier;
            PocketUniverseCollapseSize =
                FormalFateLevelTwoCatalog.PocketUniverseCollapseSize;
            VoidDebtSettlementSeconds =
                FormalFateLevelTwoCatalog.VoidDebtSettlementSeconds;
            RewindAnchorCapacity =
                FormalFateLevelTwoCatalog.RewindAnchorCapacity;
        }

        public string FateId { get; }
        public int TargetCivilizationLevel =>
            FormalCivilizationAscensionCatalog.TargetCivilizationLevel;
        public int TargetFateLevel =>
            FormalCivilizationAscensionCatalog.TargetFateLevel;
        public string AttentionReasonId { get; }
        public string StableEventKey { get; }
        public string CheckpointReasonId { get; }
        public int AttentionDelta { get; }
        public int PocketUniverseOutputMultiplier { get; }
        public int PocketUniverseCollapseSize { get; }
        public double VoidDebtSettlementSeconds { get; }
        public int RewindAnchorCapacity { get; }
    }

    public sealed class FormalCivilizationAscensionSnapshot
    {
        public FormalCivilizationAscensionSnapshot(
            int civilizationLevel,
            string fateId,
            int fateLevel,
            bool ascended,
            ulong revision)
        {
            CivilizationLevel = civilizationLevel;
            FateId = fateId;
            FateLevel = fateLevel;
            Ascended = ascended;
            Revision = revision;
        }

        public int CivilizationLevel { get; }
        public string FateId { get; }
        public int FateLevel { get; }
        public bool Ascended { get; }
        public ulong Revision { get; }
    }

    public sealed class FormalCivilizationAscensionRuntime
    {
        public const string LegacyAnalysisResearchId =
            "core.research.legacy-analysis";
        public const int RequiredMachineGunTurretCount = 2;
        public const int LegacyAnalysisAlloyCost = 30;
        public const int LegacyAnalysisBiomassCost = 20;
        public const float LegacyAnalysisResearchSeconds = 60f;
        public const int AdditionalAscensionResourceCost = 0;

        private string fateId = string.Empty;
        private int civilizationLevel = 1;
        private int fateLevel;
        private bool ascended;
        private ulong revision;
        private FormalCivilizationAscensionSnapshot cachedSnapshot;

        public FormalCivilizationAscensionRuntime()
        {
            RebuildSnapshot();
        }

        public FormalCivilizationAscensionRuntime(string fateId)
        {
            this.fateId = FormalFateCatalog.Find(fateId) != null
                ? fateId
                : throw new ArgumentException(
                    "文明升阶需要已选择的正式命轨", nameof(fateId));
            fateLevel = 1;
            RebuildSnapshot();
        }

        public int TargetCivilizationLevel =>
            FormalCivilizationAscensionCatalog.TargetCivilizationLevel;
        public int TargetFateLevel =>
            FormalCivilizationAscensionCatalog.TargetFateLevel;
        public string AttentionReasonId =>
            FormalCivilizationAscensionCatalog.AttentionReasonId;
        public int AttentionReward =>
            FormalCivilizationAscensionCatalog.AttentionReward;

        public bool CanPrepareAscension(
            FormalCivilizationAscensionRequirements requirements)
        {
            return requirements?.CanAscend == true &&
                !string.IsNullOrEmpty(fateId) && !ascended &&
                civilizationLevel == TargetCivilizationLevel - 1 &&
                fateLevel == TargetFateLevel - 1;
        }

        public bool TryBindFate(string selectedFateId, out string error)
        {
            if (FormalFateCatalog.Find(selectedFateId) == null)
            {
                error = "文明升阶命轨身份无效";
                return false;
            }
            if (!string.IsNullOrEmpty(fateId))
            {
                bool same = string.Equals(
                    fateId, selectedFateId, StringComparison.Ordinal);
                error = same ? string.Empty : "文明升阶命轨已经绑定";
                return same;
            }
            fateId = selectedFateId;
            fateLevel = 1;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool TryAscend(
            FormalCivilizationAscensionRequirements requirements,
            out FormalCivilizationAscensionCommand command,
            out string error)
        {
            if (!TryPrepareAscension(requirements, out var plan, out error))
            {
                command = null;
                return false;
            }
            return TryCommitAscension(plan, out command, out error);
        }

        public bool TryPrepareAscension(
            FormalCivilizationAscensionRequirements requirements,
            out FormalCivilizationAscensionPlan plan,
            out string error)
        {
            plan = null;
            if (!CanPrepareAscension(requirements))
            {
                error = requirements?.CanAscend != true
                    ? "文明升阶四项条件尚未全部满足"
                    : "首次文明升阶已经完成或当前等级无效";
                return false;
            }
            plan = new FormalCivilizationAscensionPlan(this, cachedSnapshot);
            error = string.Empty;
            return true;
        }

        public bool TryCommitAscension(
            FormalCivilizationAscensionPlan plan,
            out FormalCivilizationAscensionCommand command,
            out string error)
        {
            command = null;
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.Consumed || !ReferenceEquals(plan.Expected, cachedSnapshot))
            {
                error = "文明升阶计划无效、已使用或已过期";
                return false;
            }
            civilizationLevel = TargetCivilizationLevel;
            fateLevel = TargetFateLevel;
            ascended = true;
            unchecked { revision++; }
            command = new FormalCivilizationAscensionCommand(fateId);
            plan.Consumed = true;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public FormalCivilizationAscensionSnapshot Capture() => cachedSnapshot;

        public bool TryRestore(
            FormalCivilizationAscensionSnapshot snapshot,
            out string error)
        {
            bool initial = snapshot != null &&
                snapshot.CivilizationLevel == 1 && snapshot.FateLevel == 1 &&
                !snapshot.Ascended;
            bool pending = snapshot != null &&
                snapshot.CivilizationLevel == 1 && snapshot.FateLevel == 0 &&
                !snapshot.Ascended && string.IsNullOrEmpty(snapshot.FateId) &&
                snapshot.Revision == 0UL;
            bool completed = snapshot != null &&
                snapshot.CivilizationLevel == 2 && snapshot.FateLevel == 2 &&
                snapshot.Ascended;
            bool fateMatches = pending || string.IsNullOrEmpty(fateId) ||
                string.Equals(
                    snapshot.FateId, fateId, StringComparison.Ordinal);
            if ((!pending && !initial && !completed) || !fateMatches)
            {
                error = "文明升阶快照等级、命轨或完成锁无效";
                return false;
            }
            fateId = snapshot.FateId ?? string.Empty;
            civilizationLevel = snapshot.CivilizationLevel;
            fateLevel = snapshot.FateLevel;
            ascended = snapshot.Ascended;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private void RebuildSnapshot()
        {
            cachedSnapshot = new FormalCivilizationAscensionSnapshot(
                civilizationLevel, fateId, fateLevel, ascended, revision);
        }
    }
}
