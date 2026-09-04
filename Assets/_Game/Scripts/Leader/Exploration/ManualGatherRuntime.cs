using System;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Leader.Exploration
{
    public enum ManualGatherStatus
    {
        Idle = 0,
        Gathering = 1,
        Paused = 2,
        Gathered = 3,
        BackpackFull = 4,
        NodeDepleted = 5,
        Interrupted = 6,
        TransactionFailed = 7,
    }

    public readonly struct ManualGatherContext
    {
        public ManualGatherContext(
            bool leaderRecruited,
            CharacterLifeState leaderState,
            LeaderControlMode controlMode,
            bool modalBlocked,
            bool targetVisible,
            string targetStableId,
            string resourceId,
            int nodeAmount,
            float distanceToFootprint)
        {
            LeaderRecruited = leaderRecruited;
            LeaderState = leaderState;
            ControlMode = controlMode;
            ModalBlocked = modalBlocked;
            TargetVisible = targetVisible;
            TargetStableId = targetStableId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            NodeAmount = nodeAmount;
            DistanceToFootprint = distanceToFootprint;
        }

        public bool LeaderRecruited { get; }
        public CharacterLifeState LeaderState { get; }
        public LeaderControlMode ControlMode { get; }
        public bool ModalBlocked { get; }
        public bool TargetVisible { get; }
        public string TargetStableId { get; }
        public string ResourceId { get; }
        public int NodeAmount { get; }
        public float DistanceToFootprint { get; }
    }

    public readonly struct ManualGatherTickResult
    {
        internal ManualGatherTickResult(
            ManualGatherStatus status,
            int unitsGathered,
            string message)
        {
            Status = status;
            UnitsGathered = Math.Max(0, unitsGathered);
            Message = message ?? string.Empty;
        }

        public ManualGatherStatus Status { get; }
        public int UnitsGathered { get; }
        public string Message { get; }
    }

    public readonly struct ManualGatherSnapshot
    {
        public ManualGatherSnapshot(
            bool isActive,
            string targetStableId,
            string targetResourceId,
            float remainingSeconds)
        {
            IsActive = isActive;
            TargetStableId = targetStableId ?? string.Empty;
            TargetResourceId = targetResourceId ?? string.Empty;
            RemainingSeconds = remainingSeconds;
        }

        public bool IsActive { get; }
        public string TargetStableId { get; }
        public string TargetResourceId { get; }
        public float RemainingSeconds { get; }
    }

    public sealed class ManualGatherRuntime
    {
        private const float Epsilon = .00001f;
        private float elapsedSeconds;

        public bool IsActive { get; private set; }
        public string TargetStableId { get; private set; } = string.Empty;
        public string TargetResourceId { get; private set; } = string.Empty;
        public float ElapsedSeconds => elapsedSeconds;
        public float RemainingSeconds => IsActive
            ? Math.Max(
                0f,
                LeaderInteractionCatalog.ManualGatherCycleSeconds -
                elapsedSeconds)
            : 0f;
        public ManualGatherStatus Status { get; private set; } =
            ManualGatherStatus.Idle;
        public ulong Revision { get; private set; }

        public bool TryStart(ManualGatherContext context, out string error)
        {
            if (!TryValidate(context, requireSameTarget: false, out error))
                return false;

            bool sameActiveTarget = IsActive &&
                string.Equals(
                    TargetStableId,
                    context.TargetStableId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    TargetResourceId,
                    context.ResourceId,
                    StringComparison.Ordinal);
            if (sameActiveTarget)
            {
                error = string.Empty;
                return true;
            }

            IsActive = true;
            TargetStableId = context.TargetStableId;
            TargetResourceId = context.ResourceId;
            elapsedSeconds = 0f;
            Status = ManualGatherStatus.Gathering;
            unchecked { Revision++; }
            error = string.Empty;
            return true;
        }

        public bool Cancel()
        {
            if (!IsActive) return false;
            Clear(ManualGatherStatus.Idle);
            return true;
        }

        public ManualGatherSnapshot Capture()
        {
            return new ManualGatherSnapshot(
                IsActive,
                TargetStableId,
                TargetResourceId,
                RemainingSeconds);
        }

        public bool TryRestore(ManualGatherSnapshot snapshot, out string error)
        {
            bool finiteRemaining = IsFinite(snapshot.RemainingSeconds);
            if (!snapshot.IsActive)
            {
                if (!string.IsNullOrEmpty(snapshot.TargetStableId) ||
                    !string.IsNullOrEmpty(snapshot.TargetResourceId) ||
                    !finiteRemaining ||
                    Math.Abs(snapshot.RemainingSeconds) > Epsilon)
                {
                    return Fail("非活动手采快照包含活动数据", out error);
                }

                IsActive = false;
                TargetStableId = string.Empty;
                TargetResourceId = string.Empty;
                elapsedSeconds = 0f;
                Status = ManualGatherStatus.Idle;
                unchecked { Revision++; }
                error = string.Empty;
                return true;
            }

            if (string.IsNullOrWhiteSpace(snapshot.TargetStableId) ||
                !LeaderInteractionCatalog.IsManualGatherResource(
                    snapshot.TargetResourceId) ||
                !finiteRemaining ||
                snapshot.RemainingSeconds < 0f ||
                snapshot.RemainingSeconds >
                    LeaderInteractionCatalog.ManualGatherCycleSeconds + Epsilon)
            {
                return Fail("手采快照无效", out error);
            }

            IsActive = true;
            TargetStableId = snapshot.TargetStableId;
            TargetResourceId = snapshot.TargetResourceId;
            elapsedSeconds = Math.Max(
                0f,
                LeaderInteractionCatalog.ManualGatherCycleSeconds -
                snapshot.RemainingSeconds);
            Status = ManualGatherStatus.Gathering;
            unchecked { Revision++; }
            error = string.Empty;
            return true;
        }

        public ManualGatherTickResult Tick(
            float deltaSeconds,
            bool paused,
            ManualGatherContext context,
            Func<string, WorldHarvestTransactionResult> commitOne)
        {
            if (!IsActive)
            {
                return Result(ManualGatherStatus.Idle, 0, string.Empty);
            }
            if (paused)
            {
                Status = ManualGatherStatus.Paused;
                return Result(Status, 0, "玩家暂停运行");
            }
            if (!TryValidate(context, requireSameTarget: true, out string error))
            {
                ManualGatherStatus failure = context.NodeAmount <= 0
                    ? ManualGatherStatus.NodeDepleted
                    : ManualGatherStatus.Interrupted;
                Clear(failure);
                return Result(failure, 0, error);
            }
            if (commitOne == null)
            {
                Status = ManualGatherStatus.TransactionFailed;
                return Result(Status, 0, "手采事务不可用");
            }

            float delta = IsFinite(deltaSeconds)
                ? Math.Max(0f, deltaSeconds)
                : 0f;
            elapsedSeconds += delta;
            int gathered = 0;
            ManualGatherStatus current = ManualGatherStatus.Gathering;
            string message = string.Empty;

            while (elapsedSeconds + Epsilon >=
                   LeaderInteractionCatalog.ManualGatherCycleSeconds)
            {
                WorldHarvestTransactionResult transaction =
                    commitOne(TargetStableId);
                switch (transaction.Status)
                {
                    case WorldHarvestTransactionStatus.Completed:
                        if (!string.Equals(
                                transaction.ResourceId,
                                TargetResourceId,
                                StringComparison.Ordinal) ||
                            transaction.Amount !=
                                LeaderInteractionCatalog.ManualGatherAmount)
                        {
                            Status = ManualGatherStatus.TransactionFailed;
                            return Result(
                                Status,
                                gathered,
                                "手采事务返回了不一致的资源");
                        }
                        elapsedSeconds = Math.Max(
                            0f,
                            elapsedSeconds -
                            LeaderInteractionCatalog.ManualGatherCycleSeconds);
                        gathered += transaction.Amount;
                        current = ManualGatherStatus.Gathered;
                        unchecked { Revision++; }
                        break;
                    case WorldHarvestTransactionStatus.BackpackFull:
                        Status = ManualGatherStatus.BackpackFull;
                        return Result(
                            Status,
                            gathered,
                            EmptyFallback(transaction.Message, "背包已满"));
                    case WorldHarvestTransactionStatus.NodeDepleted:
                        Clear(ManualGatherStatus.NodeDepleted);
                        return Result(
                            ManualGatherStatus.NodeDepleted,
                            gathered,
                            EmptyFallback(transaction.Message, "矿脉已枯竭"));
                    default:
                        Status = ManualGatherStatus.TransactionFailed;
                        return Result(
                            Status,
                            gathered,
                            EmptyFallback(transaction.Message, "手采提交失败"));
                }
            }

            Status = current;
            return Result(Status, gathered, message);
        }

        private bool TryValidate(
            ManualGatherContext context,
            bool requireSameTarget,
            out string error)
        {
            if (!context.LeaderRecruited)
                return Fail("领袖尚未招募", out error);
            if (context.LeaderState != CharacterLifeState.Active)
                return Fail("领袖当前无法行动", out error);
            if (context.ControlMode != LeaderControlMode.Manual)
                return Fail("只有手动控制领袖时才能采集", out error);
            if (context.ModalBlocked)
                return Fail("当前界面阻止世界交互", out error);
            if (!context.TargetVisible)
                return Fail("资源节点不在实时视野中", out error);
            if (string.IsNullOrWhiteSpace(context.TargetStableId) ||
                !LeaderInteractionCatalog.IsManualGatherResource(
                    context.ResourceId))
            {
                return Fail("手采目标或资源无效", out error);
            }
            if (context.NodeAmount <= 0)
                return Fail("矿脉已枯竭", out error);
            if (!IsFinite(context.DistanceToFootprint) ||
                context.DistanceToFootprint < 0f ||
                context.DistanceToFootprint >
                    LeaderInteractionCatalog.ManualGatherMaximumDistance +
                    Epsilon)
            {
                return Fail("资源节点超出手采距离", out error);
            }
            if (requireSameTarget &&
                (!string.Equals(
                    TargetStableId,
                    context.TargetStableId,
                    StringComparison.Ordinal) ||
                 !string.Equals(
                    TargetResourceId,
                    context.ResourceId,
                    StringComparison.Ordinal)))
            {
                return Fail("手采目标已经改变", out error);
            }
            error = string.Empty;
            return true;
        }

        private void Clear(ManualGatherStatus status)
        {
            IsActive = false;
            TargetStableId = string.Empty;
            TargetResourceId = string.Empty;
            elapsedSeconds = 0f;
            Status = status;
            unchecked { Revision++; }
        }

        private static ManualGatherTickResult Result(
            ManualGatherStatus status,
            int gathered,
            string message)
        {
            return new ManualGatherTickResult(status, gathered, message);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private static string EmptyFallback(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
