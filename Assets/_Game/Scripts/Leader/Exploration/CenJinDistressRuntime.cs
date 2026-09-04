using System;

namespace WasteCity.Leader.Exploration
{
    public enum CenJinDistressState
    {
        Undiscovered = 0,
        Discovered = 1,
        Rescuing = 2,
        RescuedTimely = 3,
        RescuedDelayed = 4,
        RescuedLegacy = 5,
    }

    public enum CenJinRescueOutcome
    {
        None = 0,
        Timely = 1,
        Delayed = 2,
        Legacy = 3,
    }

    public enum CenJinDistressTickKind
    {
        None = 0,
        Progressed = 1,
        Paused = 2,
        Cancelled = 3,
        Completed = 4,
        CommitFailed = 5,
        ReleaseFailed = 6,
    }

    public sealed class CenJinRescueCommitRequest
    {
        internal CenJinRescueCommitRequest(
            CenJinRescueOutcome outcome,
            bool injured,
            string characterId,
            int reservedBiomass,
            int populationReward,
            string attentionReasonId,
            string stableEventKey)
        {
            Outcome = outcome;
            Injured = injured;
            CharacterId = characterId;
            ReservedBiomass = reservedBiomass;
            PopulationReward = populationReward;
            AttentionReasonId = attentionReasonId;
            StableEventKey = stableEventKey;
        }

        public CenJinRescueOutcome Outcome { get; }
        public bool Injured { get; }
        public string CharacterId { get; }
        public int ReservedBiomass { get; }
        public int PopulationReward { get; }
        public string AttentionReasonId { get; }
        public string StableEventKey { get; }
    }

    public interface ICenJinDistressTransaction
    {
        bool TryReserveBiomass(int amount, out string error);
        bool TryReleaseBiomass(int amount, out string error);
        bool TryCommit(
            CenJinRescueCommitRequest request,
            out string error);
    }

    public readonly struct CenJinDistressTickResult
    {
        internal CenJinDistressTickResult(
            CenJinDistressTickKind kind,
            CenJinRescueOutcome outcome,
            string message)
        {
            Kind = kind;
            Outcome = outcome;
            Message = message ?? string.Empty;
        }

        public CenJinDistressTickKind Kind { get; }
        public CenJinRescueOutcome Outcome { get; }
        public string Message { get; }
    }

    public sealed class CenJinDistressSnapshot
    {
        public CenJinDistressSnapshot(
            string sessionId,
            string siteId,
            CenJinDistressState state,
            float elapsedSinceDiscoverySeconds,
            float rescueRemainingSeconds,
            int reservedBiomass,
            string committedEventKey)
        {
            SessionId = sessionId;
            SiteId = siteId;
            State = state;
            ElapsedSinceDiscoverySeconds = elapsedSinceDiscoverySeconds;
            RescueRemainingSeconds = rescueRemainingSeconds;
            ReservedBiomass = reservedBiomass;
            CommittedEventKey = committedEventKey;
        }

        public string SessionId { get; }
        public string SiteId { get; }
        public CenJinDistressState State { get; }
        public float ElapsedSinceDiscoverySeconds { get; }
        public float RescueRemainingSeconds { get; }
        public int ReservedBiomass { get; }
        public string CommittedEventKey { get; }
    }

    public sealed class CenJinDistressRuntime
    {
        private const float Epsilon = .00001f;
        private readonly string sessionId;

        public CenJinDistressRuntime(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentException(
                    "岑烬求救需要稳定会话 ID",
                    nameof(sessionId));
            }
            this.sessionId = sessionId;
        }

        public CenJinDistressState State { get; private set; } =
            CenJinDistressState.Undiscovered;
        public string SessionId => sessionId;
        public string SiteId => LeaderInteractionCatalog.CenJinDistressSiteId;
        public float ElapsedSinceDiscoverySeconds { get; private set; }
        public float RescueRemainingSeconds { get; private set; }
        public int ReservedBiomass { get; private set; }
        public string CommittedEventKey { get; private set; } = string.Empty;
        public ulong Revision { get; private set; }
        public bool IsCritical =>
            !IsCompleted && State != CenJinDistressState.Undiscovered &&
            ElapsedSinceDiscoverySeconds >=
                LeaderInteractionCatalog.CenJinCriticalThresholdSeconds;
        public bool IsCompleted =>
            State == CenJinDistressState.RescuedTimely ||
            State == CenJinDistressState.RescuedDelayed ||
            State == CenJinDistressState.RescuedLegacy;

        public bool TryDiscover(bool siteVisible)
        {
            if (!siteVisible || State != CenJinDistressState.Undiscovered)
                return false;
            State = CenJinDistressState.Discovered;
            ElapsedSinceDiscoverySeconds = 0f;
            unchecked { Revision++; }
            return true;
        }

        public bool TryBeginRescue(
            float cityDistance,
            bool canOperate,
            ICenJinDistressTransaction transaction,
            out string error)
        {
            if (State != CenJinDistressState.Discovered)
                return Fail("岑烬求救当前不能开始", out error);
            if (!canOperate)
                return Fail("当前状态不能执行救援", out error);
            if (!IsFinite(cityDistance) || cityDistance < 0f ||
                cityDistance >
                    LeaderInteractionCatalog.CenJinRescueMaximumDistance +
                    Epsilon)
            {
                return Fail("移动城市不在求救点 3 格内", out error);
            }
            if (transaction == null)
                return Fail("岑烬救援事务不可用", out error);
            if (!transaction.TryReserveBiomass(
                    LeaderInteractionCatalog.CenJinBiomassCost,
                    out error))
            {
                return false;
            }

            ReservedBiomass = LeaderInteractionCatalog.CenJinBiomassCost;
            RescueRemainingSeconds =
                LeaderInteractionCatalog.CenJinRescueSeconds;
            State = CenJinDistressState.Rescuing;
            unchecked { Revision++; }
            error = string.Empty;
            return true;
        }

        public bool TryCancel(
            ICenJinDistressTransaction transaction,
            out string error)
        {
            error = string.Empty;
            if (State != CenJinDistressState.Rescuing ||
                ReservedBiomass !=
                    LeaderInteractionCatalog.CenJinBiomassCost)
            {
                return Fail("当前没有进行中的岑烬救援", out error);
            }
            if (transaction == null ||
                !transaction.TryReleaseBiomass(ReservedBiomass, out error))
            {
                if (transaction == null)
                    error = "岑烬救援返还事务不可用";
                return false;
            }

            ResetToDiscovered();
            error = string.Empty;
            return true;
        }

        public CenJinDistressTickResult Tick(
            float deltaSeconds,
            bool paused,
            float cityDistance,
            bool canOperate,
            ICenJinDistressTransaction transaction)
        {
            if (State == CenJinDistressState.Undiscovered || IsCompleted)
                return Result(CenJinDistressTickKind.None);
            if (paused)
                return Result(CenJinDistressTickKind.Paused, "玩家暂停运行");

            float delta = IsFinite(deltaSeconds)
                ? Math.Max(0f, deltaSeconds)
                : 0f;
            if (State == CenJinDistressState.Discovered)
            {
                if (delta <= 0f) return Result(CenJinDistressTickKind.None);
                ElapsedSinceDiscoverySeconds += delta;
                unchecked { Revision++; }
                return Result(CenJinDistressTickKind.Progressed);
            }

            if (!canOperate || !IsFinite(cityDistance) || cityDistance < 0f ||
                cityDistance >
                    LeaderInteractionCatalog.CenJinRescueMaximumDistance +
                    Epsilon)
            {
                string releaseError = string.Empty;
                if (transaction == null ||
                    !transaction.TryReleaseBiomass(
                        ReservedBiomass,
                        out releaseError))
                {
                    return Result(
                        CenJinDistressTickKind.ReleaseFailed,
                        transaction == null
                            ? "岑烬救援返还事务不可用"
                            : releaseError);
                }
                ResetToDiscovered();
                return Result(
                    CenJinDistressTickKind.Cancelled,
                    "离开求救范围，已返还生物质");
            }

            if (RescueRemainingSeconds > 0f && delta > 0f)
            {
                float used = Math.Min(delta, RescueRemainingSeconds);
                RescueRemainingSeconds = Math.Max(
                    0f,
                    RescueRemainingSeconds - used);
                ElapsedSinceDiscoverySeconds += used;
                unchecked { Revision++; }
            }
            if (RescueRemainingSeconds > Epsilon)
                return Result(CenJinDistressTickKind.Progressed);

            if (transaction == null)
            {
                return Result(
                    CenJinDistressTickKind.CommitFailed,
                    "岑烬救援提交事务不可用");
            }

            CenJinRescueOutcome outcome =
                ElapsedSinceDiscoverySeconds <
                    LeaderInteractionCatalog.CenJinTimelyThresholdSeconds
                    ? CenJinRescueOutcome.Timely
                    : CenJinRescueOutcome.Delayed;
            string eventKey = LeaderInteractionCatalog.CenJinStableEventKey(
                sessionId);
            var request = new CenJinRescueCommitRequest(
                outcome,
                outcome == CenJinRescueOutcome.Delayed,
                LeaderInteractionCatalog.CenJinCharacterId,
                ReservedBiomass,
                LeaderInteractionCatalog.CenJinPopulationReward,
                LeaderInteractionCatalog.CenJinAttentionReasonId,
                eventKey);
            if (!transaction.TryCommit(request, out string commitError))
            {
                return Result(
                    CenJinDistressTickKind.CommitFailed,
                    commitError);
            }

            State = outcome == CenJinRescueOutcome.Timely
                ? CenJinDistressState.RescuedTimely
                : CenJinDistressState.RescuedDelayed;
            RescueRemainingSeconds = 0f;
            ReservedBiomass = 0;
            CommittedEventKey = eventKey;
            unchecked { Revision++; }
            return new CenJinDistressTickResult(
                CenJinDistressTickKind.Completed,
                outcome,
                string.Empty);
        }

        public CenJinDistressSnapshot Capture()
        {
            return new CenJinDistressSnapshot(
                sessionId,
                SiteId,
                State,
                ElapsedSinceDiscoverySeconds,
                RescueRemainingSeconds,
                ReservedBiomass,
                CommittedEventKey);
        }

        public bool TryRestore(
            CenJinDistressSnapshot snapshot,
            out string error)
        {
            if (!IsValidSnapshot(snapshot, out error)) return false;

            State = snapshot.State;
            ElapsedSinceDiscoverySeconds =
                snapshot.ElapsedSinceDiscoverySeconds;
            RescueRemainingSeconds = snapshot.RescueRemainingSeconds;
            ReservedBiomass = snapshot.ReservedBiomass;
            CommittedEventKey = snapshot.CommittedEventKey ?? string.Empty;
            unchecked { Revision++; }
            error = string.Empty;
            return true;
        }

        public static CenJinDistressRuntime CreateLegacyRescued(
            string sessionId)
        {
            var runtime = new CenJinDistressRuntime(sessionId)
            {
                State = CenJinDistressState.RescuedLegacy,
                CommittedEventKey = string.Empty,
            };
            return runtime;
        }

        private bool IsValidSnapshot(
            CenJinDistressSnapshot snapshot,
            out string error)
        {
            if (snapshot == null ||
                !string.Equals(
                    snapshot.SessionId,
                    sessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.SiteId,
                    SiteId,
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(CenJinDistressState), snapshot.State) ||
                !IsFinite(snapshot.ElapsedSinceDiscoverySeconds) ||
                snapshot.ElapsedSinceDiscoverySeconds < 0f ||
                !IsFinite(snapshot.RescueRemainingSeconds) ||
                snapshot.RescueRemainingSeconds < 0f ||
                snapshot.RescueRemainingSeconds >
                    LeaderInteractionCatalog.CenJinRescueSeconds ||
                snapshot.ReservedBiomass < 0)
            {
                return Fail("岑烬求救存档字段无效", out error);
            }

            bool rescuing = snapshot.State == CenJinDistressState.Rescuing;
            bool completed = snapshot.State ==
                    CenJinDistressState.RescuedTimely ||
                snapshot.State == CenJinDistressState.RescuedDelayed;
            if (rescuing !=
                    (snapshot.ReservedBiomass ==
                     LeaderInteractionCatalog.CenJinBiomassCost) ||
                !rescuing && snapshot.RescueRemainingSeconds != 0f ||
                completed != !string.IsNullOrWhiteSpace(
                    snapshot.CommittedEventKey) ||
                snapshot.State == CenJinDistressState.Undiscovered &&
                    snapshot.ElapsedSinceDiscoverySeconds != 0f)
            {
                return Fail("岑烬求救存档阶段不一致", out error);
            }

            error = string.Empty;
            return true;
        }

        private void ResetToDiscovered()
        {
            State = CenJinDistressState.Discovered;
            RescueRemainingSeconds = 0f;
            ReservedBiomass = 0;
            unchecked { Revision++; }
        }

        private static CenJinDistressTickResult Result(
            CenJinDistressTickKind kind,
            string message = "")
        {
            return new CenJinDistressTickResult(
                kind,
                CenJinRescueOutcome.None,
                message);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
