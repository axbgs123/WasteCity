using System;

namespace WasteCity.Combat
{
    public enum FriendlyUnitKind
    {
        Puppet,
        Behemoth,
        Controlled
    }

    public readonly struct FriendlyRallyPoint
    {
        public FriendlyRallyPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    public enum FriendlySquadCommandType
    {
        Rally,
        Guard,
        FollowLeader,
        Expedition,
        Retreat,
    }

    public sealed class FriendlyUnitCommandPersistenceSnapshot
    {
        public FriendlyUnitCommandPersistenceSnapshot(
            bool hasFixedRally,
            float rallyX,
            float rallyY,
            FriendlySquadCommandType command,
            bool hasExpeditionTarget,
            int expeditionTargetX,
            int expeditionTargetY,
            int puppetLosses,
            int behemothLosses,
            int controlledLosses)
        {
            HasFixedRally = hasFixedRally;
            RallyX = rallyX;
            RallyY = rallyY;
            Command = command;
            HasExpeditionTarget = hasExpeditionTarget;
            ExpeditionTargetX = expeditionTargetX;
            ExpeditionTargetY = expeditionTargetY;
            PuppetLosses = puppetLosses;
            BehemothLosses = behemothLosses;
            ControlledLosses = controlledLosses;
        }

        public bool HasFixedRally { get; }
        public float RallyX { get; }
        public float RallyY { get; }
        public FriendlySquadCommandType Command { get; }
        public bool HasExpeditionTarget { get; }
        public int ExpeditionTargetX { get; }
        public int ExpeditionTargetY { get; }
        public int PuppetLosses { get; }
        public int BehemothLosses { get; }
        public int ControlledLosses { get; }
    }

    public sealed class FriendlyUnitCommandRestorePlan
    {
        internal FriendlyUnitCommandRestorePlan(
            FriendlyUnitCommandModel owner,
            ulong expectedRevision,
            FriendlyUnitCommandPersistenceSnapshot snapshot)
        {
            Owner = owner;
            ExpectedRevision = expectedRevision;
            Snapshot = snapshot;
        }

        internal FriendlyUnitCommandModel Owner { get; }
        internal ulong ExpectedRevision { get; }
        internal FriendlyUnitCommandPersistenceSnapshot Snapshot { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class FriendlyUnitCommandModel
    {
        private float rallyX;
        private float rallyY;
        private ulong revision;

        public bool HasFixedRally { get; private set; }
        public int PuppetLosses { get; private set; }
        public int BehemothLosses { get; private set; }
        public int ControlledLosses { get; private set; }
        public int TotalLosses => PuppetLosses + BehemothLosses + ControlledLosses;
        public FriendlySquadCommandType Command { get; private set; } =
            FriendlySquadCommandType.Rally;
        public bool HasExpeditionTarget { get; private set; }
        public int ExpeditionTargetX { get; private set; }
        public int ExpeditionTargetY { get; private set; }
        public ulong Revision => revision;

        public FriendlyRallyPoint ResolveRally(float cityX, float cityY)
        {
            return HasFixedRally
                ? new FriendlyRallyPoint(rallyX, rallyY)
                : new FriendlyRallyPoint(cityX, cityY);
        }

        public void SetRally(float x, float y)
        {
            rallyX = x;
            rallyY = y;
            HasFixedRally = true;
            Command = FriendlySquadCommandType.Rally;
            HasExpeditionTarget = false;
            AdvanceRevision();
        }

        public void ClearRally()
        {
            HasFixedRally = false;
            AdvanceRevision();
        }

        public void Guard()
        {
            Command = FriendlySquadCommandType.Guard;
            HasExpeditionTarget = false;
            AdvanceRevision();
        }

        public void FollowLeader()
        {
            Command = FriendlySquadCommandType.FollowLeader;
            HasExpeditionTarget = false;
            AdvanceRevision();
        }

        public bool TryExpedition(
            int targetX,
            int targetY,
            bool isRevealed,
            bool isPassable)
        {
            if (!isRevealed || !isPassable) return false;
            Command = FriendlySquadCommandType.Expedition;
            ExpeditionTargetX = targetX;
            ExpeditionTargetY = targetY;
            HasExpeditionTarget = true;
            AdvanceRevision();
            return true;
        }

        public void Retreat()
        {
            Command = FriendlySquadCommandType.Retreat;
            HasExpeditionTarget = false;
            AdvanceRevision();
        }

        public void RecordLoss(FriendlyUnitKind kind)
        {
            switch (kind)
            {
                case FriendlyUnitKind.Puppet:
                    PuppetLosses++;
                    break;
                case FriendlyUnitKind.Behemoth:
                    BehemothLosses++;
                    break;
                case FriendlyUnitKind.Controlled:
                    ControlledLosses++;
                    break;
            }
            AdvanceRevision();
        }

        public void Restore(bool hasFixedRally, float x, float y, int puppetLosses, int behemothLosses, int controlledLosses)
        {
            HasFixedRally = hasFixedRally;
            rallyX = x;
            rallyY = y;
            PuppetLosses = Math.Max(0, puppetLosses);
            BehemothLosses = Math.Max(0, behemothLosses);
            ControlledLosses = Math.Max(0, controlledLosses);
            Command = FriendlySquadCommandType.Rally;
            HasExpeditionTarget = false;
            AdvanceRevision();
        }

        public FriendlyUnitCommandPersistenceSnapshot CaptureForPersistence()
        {
            return new FriendlyUnitCommandPersistenceSnapshot(
                HasFixedRally,
                rallyX,
                rallyY,
                Command,
                HasExpeditionTarget,
                ExpeditionTargetX,
                ExpeditionTargetY,
                PuppetLosses,
                BehemothLosses,
                ControlledLosses);
        }

        public bool TryPrepareRestoreForPersistence(
            FriendlyUnitCommandPersistenceSnapshot snapshot,
            out FriendlyUnitCommandRestorePlan plan,
            out string error)
        {
            plan = null;
            if (snapshot == null)
                return Fail("友军命令存档为空", out error);
            if (!IsFinite(snapshot.RallyX) || !IsFinite(snapshot.RallyY))
                return Fail("友军集结点不是有限坐标", out error);
            if (!Enum.IsDefined(
                    typeof(FriendlySquadCommandType),
                    snapshot.Command))
                return Fail("友军命令类型无效", out error);
            if (snapshot.HasExpeditionTarget !=
                (snapshot.Command == FriendlySquadCommandType.Expedition))
                return Fail("友军出征目标与命令不一致", out error);
            if (snapshot.PuppetLosses < 0 ||
                snapshot.BehemothLosses < 0 ||
                snapshot.ControlledLosses < 0)
                return Fail("友军损失计数不能为负", out error);
            plan = new FriendlyUnitCommandRestorePlan(
                this,
                revision,
                new FriendlyUnitCommandPersistenceSnapshot(
                    snapshot.HasFixedRally,
                    snapshot.RallyX,
                    snapshot.RallyY,
                    snapshot.Command,
                    snapshot.HasExpeditionTarget,
                    snapshot.ExpeditionTargetX,
                    snapshot.ExpeditionTargetY,
                    snapshot.PuppetLosses,
                    snapshot.BehemothLosses,
                    snapshot.ControlledLosses));
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestoreForPersistence(
            FriendlyUnitCommandRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.Consumed || plan.ExpectedRevision != revision)
                return Fail("友军命令恢复计划无效或已过期", out error);
            FriendlyUnitCommandPersistenceSnapshot value = plan.Snapshot;
            HasFixedRally = value.HasFixedRally;
            rallyX = value.RallyX;
            rallyY = value.RallyY;
            Command = value.Command;
            HasExpeditionTarget = value.HasExpeditionTarget;
            ExpeditionTargetX = value.ExpeditionTargetX;
            ExpeditionTargetY = value.ExpeditionTargetY;
            PuppetLosses = value.PuppetLosses;
            BehemothLosses = value.BehemothLosses;
            ControlledLosses = value.ControlledLosses;
            plan.Consumed = true;
            AdvanceRevision();
            error = string.Empty;
            return true;
        }

        private void AdvanceRevision()
        {
            unchecked { revision++; }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
