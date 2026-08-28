using System;

namespace WasteCity.Combat
{
    public enum FriendlyUnitDecisionType
    {
        Hold,
        ReturnToRally,
        Chase,
        Attack,
        FollowLeader,
    }

    public readonly struct FriendlyTacticalProfile
    {
        public FriendlyTacticalProfile(float guardRadius, float leashRadius, float arrivalTolerance, float attackRange)
        {
            GuardRadius = Math.Max(0f, guardRadius);
            LeashRadius = Math.Max(GuardRadius, leashRadius);
            ArrivalTolerance = Math.Max(0f, arrivalTolerance);
            AttackRange = Math.Max(0f, attackRange);
        }

        public float GuardRadius { get; }
        public float LeashRadius { get; }
        public float ArrivalTolerance { get; }
        public float AttackRange { get; }
    }

    public readonly struct FriendlyTargetCandidate
    {
        public FriendlyTargetCandidate(int id, float x, float y, bool isAlive, bool isFriendly)
        {
            Id = id;
            X = x;
            Y = y;
            IsAlive = isAlive;
            IsFriendly = isFriendly;
        }

        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public bool IsAlive { get; }
        public bool IsFriendly { get; }
    }

    public readonly struct FriendlyUnitDecision
    {
        public FriendlyUnitDecision(FriendlyUnitDecisionType type, int targetId = 0)
        {
            Type = type;
            TargetId = targetId;
        }

        public FriendlyUnitDecisionType Type { get; }
        public int TargetId { get; }
        public bool HasTarget => TargetId != 0;
    }

    public static class FriendlyUnitTacticalRules
    {
        public static FriendlyUnitDecision DecideFollowLeader(
            float unitX,
            float unitY,
            float leaderX,
            float leaderY,
            float arrivalTolerance)
        {
            float safeTolerance = Math.Max(0f, arrivalTolerance);
            return DistanceSquared(unitX, unitY, leaderX, leaderY) >
                   safeTolerance * safeTolerance
                ? new FriendlyUnitDecision(
                    FriendlyUnitDecisionType.FollowLeader)
                : new FriendlyUnitDecision(FriendlyUnitDecisionType.Hold);
        }

        public static FriendlyUnitDecision Decide(
            float unitX,
            float unitY,
            float rallyX,
            float rallyY,
            FriendlyTacticalProfile profile,
            FriendlyTargetCandidate[] candidates,
            int currentTargetId = 0)
        {
            FriendlyTargetCandidate? target = FindExistingTarget(candidates, currentTargetId, rallyX, rallyY, profile.LeashRadius);
            if (!target.HasValue)
                target = FindNearestNewTarget(candidates, unitX, unitY, rallyX, rallyY, profile.GuardRadius);

            if (!target.HasValue)
            {
                float rallyDistanceSquared = DistanceSquared(unitX, unitY, rallyX, rallyY);
                return rallyDistanceSquared > profile.ArrivalTolerance * profile.ArrivalTolerance
                    ? new FriendlyUnitDecision(FriendlyUnitDecisionType.ReturnToRally)
                    : new FriendlyUnitDecision(FriendlyUnitDecisionType.Hold);
            }

            FriendlyTargetCandidate selected = target.Value;
            float targetDistanceSquared = DistanceSquared(unitX, unitY, selected.X, selected.Y);
            return targetDistanceSquared <= profile.AttackRange * profile.AttackRange
                ? new FriendlyUnitDecision(FriendlyUnitDecisionType.Attack, selected.Id)
                : new FriendlyUnitDecision(FriendlyUnitDecisionType.Chase, selected.Id);
        }

        private static FriendlyTargetCandidate? FindExistingTarget(
            FriendlyTargetCandidate[] candidates,
            int targetId,
            float rallyX,
            float rallyY,
            float leashRadius)
        {
            if (candidates == null || targetId == 0) return null;
            float leashSquared = leashRadius * leashRadius;
            foreach (FriendlyTargetCandidate candidate in candidates)
                if (candidate.Id == targetId && IsHostileAlive(candidate)
                    && DistanceSquared(candidate.X, candidate.Y, rallyX, rallyY) <= leashSquared)
                    return candidate;
            return null;
        }

        private static FriendlyTargetCandidate? FindNearestNewTarget(
            FriendlyTargetCandidate[] candidates,
            float unitX,
            float unitY,
            float rallyX,
            float rallyY,
            float guardRadius)
        {
            if (candidates == null) return null;
            float guardSquared = guardRadius * guardRadius;
            float nearestSquared = float.MaxValue;
            FriendlyTargetCandidate? nearest = null;
            foreach (FriendlyTargetCandidate candidate in candidates)
            {
                if (!IsHostileAlive(candidate)
                    || DistanceSquared(candidate.X, candidate.Y, rallyX, rallyY) > guardSquared)
                    continue;

                float unitDistanceSquared = DistanceSquared(candidate.X, candidate.Y, unitX, unitY);
                if (unitDistanceSquared >= nearestSquared) continue;
                nearestSquared = unitDistanceSquared;
                nearest = candidate;
            }
            return nearest;
        }

        private static bool IsHostileAlive(FriendlyTargetCandidate candidate)
        {
            return candidate.IsAlive && !candidate.IsFriendly;
        }

        private static float DistanceSquared(float firstX, float firstY, float secondX, float secondY)
        {
            float x = firstX - secondX;
            float y = firstY - secondY;
            return x * x + y * y;
        }
    }
}
