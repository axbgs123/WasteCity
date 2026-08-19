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

    public readonly struct EvacuationBatchContext :
        IEquatable<EvacuationBatchContext>
    {
        public EvacuationBatchContext(
            bool isInCombat,
            float productivityMultiplier)
        {
            IsInCombat = isInCombat;
            ProductivityMultiplier =
                float.IsNaN(productivityMultiplier) ||
                float.IsInfinity(productivityMultiplier)
                    ? 0f
                    : Math.Max(0f, productivityMultiplier);
        }

        public bool IsInCombat { get; }
        public float ProductivityMultiplier { get; }

        public bool Equals(EvacuationBatchContext other)
        {
            return IsInCombat == other.IsInCombat &&
                   ProductivityMultiplier.Equals(
                       other.ProductivityMultiplier);
        }

        public override bool Equals(object obj)
        {
            return obj is EvacuationBatchContext other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((IsInCombat ? 1 : 0) * 397) ^
                       ProductivityMultiplier.GetHashCode();
            }
        }
    }

    public readonly struct BuildingEvacuationWork : IEquatable<BuildingEvacuationWork>
    {
        public BuildingEvacuationWork(
            string stableInstanceId,
            BuildingEvacuationTreatment treatment,
            double remainingRatio,
            float dismantleSeconds,
            int refund)
            : this(
                stableInstanceId,
                treatment,
                remainingRatio,
                dismantleSeconds,
                dismantleSeconds,
                refund,
                BuildingEvacuationRules.CreateBatchContext(false, 1f))
        {
        }

        public BuildingEvacuationWork(
            string stableInstanceId,
            BuildingEvacuationTreatment treatment,
            double remainingRatio,
            float baseDismantleSeconds,
            float dismantleSeconds,
            int refund,
            EvacuationBatchContext batchContext)
        {
            StableInstanceId = stableInstanceId;
            Treatment = treatment;
            RemainingRatio = remainingRatio;
            BaseDismantleSeconds = baseDismantleSeconds;
            DismantleSeconds = dismantleSeconds;
            Refund = refund;
            BatchContext = batchContext;
        }

        public string StableInstanceId { get; }
        public BuildingEvacuationTreatment Treatment { get; }
        public double RemainingRatio { get; }
        public float BaseDismantleSeconds { get; }
        public float DismantleSeconds { get; }
        public int Refund { get; }
        public EvacuationBatchContext BatchContext { get; }

        public bool Equals(BuildingEvacuationWork other)
        {
            return string.Equals(StableInstanceId, other.StableInstanceId,
                       StringComparison.Ordinal) &&
                   Treatment == other.Treatment &&
                   RemainingRatio.Equals(other.RemainingRatio) &&
                   BaseDismantleSeconds.Equals(
                       other.BaseDismantleSeconds) &&
                   DismantleSeconds.Equals(other.DismantleSeconds) &&
                   Refund == other.Refund &&
                   BatchContext.Equals(other.BatchContext);
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
                hash = hash * 397 ^ BaseDismantleSeconds.GetHashCode();
                hash = hash * 397 ^ DismantleSeconds.GetHashCode();
                hash = hash * 397 ^ Refund;
                return hash * 397 ^ BatchContext.GetHashCode();
            }
        }
    }

    public static class BuildingEvacuationRules
    {
        public static EvacuationBatchContext CreateBatchContext(
            bool isInCombat,
            float productivityMultiplier)
        {
            return new EvacuationBatchContext(
                isInCombat,
                productivityMultiplier);
        }

        public static BuildingEvacuationWork Create(
            string stableInstanceId,
            int originalCost,
            float originalBuildSeconds,
            double remainingRatio,
            BuildingEvacuationTreatment treatment)
        {
            return Create(
                stableInstanceId,
                originalCost,
                originalBuildSeconds,
                remainingRatio,
                treatment,
                CreateBatchContext(false, 1f));
        }

        public static BuildingEvacuationWork Create(
            string stableInstanceId,
            int originalCost,
            float originalBuildSeconds,
            double remainingRatio,
            BuildingEvacuationTreatment treatment,
            EvacuationBatchContext batchContext)
        {
            double clampedRemaining = Math.Max(0d, Math.Min(1d, remainingRatio));
            double handlingRatio;
            float baseDismantleSeconds;
            switch (treatment)
            {
                case BuildingEvacuationTreatment.Abandon:
                    handlingRatio = 0d;
                    baseDismantleSeconds = 0f;
                    break;
                case BuildingEvacuationTreatment.FullDismantle:
                    handlingRatio = batchContext.IsInCombat ? .6d : .8d;
                    baseDismantleSeconds = batchContext.IsInCombat
                        ? 5f
                        : Math.Max(0f, originalBuildSeconds) * .5f;
                    break;
                case BuildingEvacuationTreatment.QuickDismantle:
                    handlingRatio = .5d;
                    baseDismantleSeconds = 0f;
                    break;
                default:
                    handlingRatio = 0d;
                    baseDismantleSeconds = 0f;
                    break;
            }

            float dismantleSeconds = EffectiveDismantleSeconds(
                baseDismantleSeconds,
                batchContext.ProductivityMultiplier);

            return new BuildingEvacuationWork(
                stableInstanceId,
                treatment,
                clampedRemaining,
                baseDismantleSeconds,
                dismantleSeconds,
                ConstructionRefundRules.Calculate(
                    originalCost,
                    clampedRemaining,
                    handlingRatio),
                batchContext);
        }

        private static float EffectiveDismantleSeconds(
            float baseDismantleSeconds,
            float productivityMultiplier)
        {
            if (baseDismantleSeconds <= 0f) return 0f;
            return productivityMultiplier > 0f
                ? baseDismantleSeconds / productivityMultiplier
                : float.PositiveInfinity;
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
