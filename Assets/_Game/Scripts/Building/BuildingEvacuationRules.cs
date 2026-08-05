using System;
using System.Collections.Generic;

namespace WasteCity.Building
{
    public enum BuildingEvacuationTreatment
    {
        Unassigned,
        Abandon,
        FullDismantle,
        QuickDismantle
    }

    public readonly struct BuildingEvacuationWork : IEquatable<BuildingEvacuationWork>
    {
        public BuildingEvacuationWork(
            string stableInstanceId,
            BuildingEvacuationTreatment treatment,
            double remainingRatio,
            float dismantleSeconds,
            int refund)
        {
            StableInstanceId = stableInstanceId;
            Treatment = treatment;
            RemainingRatio = remainingRatio;
            DismantleSeconds = dismantleSeconds;
            Refund = refund;
        }

        public string StableInstanceId { get; }
        public BuildingEvacuationTreatment Treatment { get; }
        public double RemainingRatio { get; }
        public float DismantleSeconds { get; }
        public int Refund { get; }

        public bool Equals(BuildingEvacuationWork other)
        {
            return string.Equals(StableInstanceId, other.StableInstanceId,
                       StringComparison.Ordinal) &&
                   Treatment == other.Treatment &&
                   RemainingRatio.Equals(other.RemainingRatio) &&
                   DismantleSeconds.Equals(other.DismantleSeconds) &&
                   Refund == other.Refund;
        }

        public override bool Equals(object obj) =>
            obj is BuildingEvacuationWork other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StableInstanceId == null
                    ? 0
                    : StableInstanceId.GetHashCode();
                hash = hash * 397 ^ (int)Treatment;
                hash = hash * 397 ^ RemainingRatio.GetHashCode();
                hash = hash * 397 ^ DismantleSeconds.GetHashCode();
                return hash * 397 ^ Refund;
            }
        }
    }

    public static class BuildingEvacuationRules
    {
        public static BuildingEvacuationWork Create(
            string stableInstanceId,
            int originalCost,
            float originalBuildSeconds,
            double remainingRatio,
            BuildingEvacuationTreatment treatment)
        {
            double clampedRemaining = Math.Max(0d, Math.Min(1d, remainingRatio));
            double handlingRatio;
            float dismantleSeconds;
            switch (treatment)
            {
                case BuildingEvacuationTreatment.Abandon:
                    handlingRatio = 0d;
                    dismantleSeconds = 0f;
                    break;
                case BuildingEvacuationTreatment.FullDismantle:
                    handlingRatio = .8d;
                    dismantleSeconds = Math.Max(0f, originalBuildSeconds) * .5f;
                    break;
                case BuildingEvacuationTreatment.QuickDismantle:
                    handlingRatio = .5d;
                    dismantleSeconds = 0f;
                    break;
                default:
                    handlingRatio = 0d;
                    dismantleSeconds = 0f;
                    break;
            }

            return new BuildingEvacuationWork(
                stableInstanceId,
                treatment,
                clampedRemaining,
                dismantleSeconds,
                ConstructionRefundRules.Calculate(
                    originalCost,
                    clampedRemaining,
                    handlingRatio));
        }

        public static IReadOnlyList<BuildingEvacuationWork>
            CreateStableFullDismantleQueue(
                IEnumerable<BuildingEvacuationWork> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            var queue = new List<BuildingEvacuationWork>();
            foreach (BuildingEvacuationWork value in work)
                if (value.Treatment == BuildingEvacuationTreatment.FullDismantle)
                    queue.Add(value);
            queue.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
            return queue;
        }
    }
}
