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
        private const float RestoreTolerance = .0001f;

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

        public static bool TryRestoreFrozenWork(
            string stableInstanceId,
            BuildingEvacuationTreatment treatment,
            double remainingRatio,
            float baseDismantleSeconds,
            float dismantleSeconds,
            int refund,
            EvacuationBatchContext batchContext,
            out BuildingEvacuationWork work,
            out string error)
        {
            work = default;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(stableInstanceId))
                return FailRestore("撤离建筑稳定 ID 不能为空", out error);
            if (!Enum.IsDefined(typeof(BuildingEvacuationTreatment), treatment) ||
                treatment == BuildingEvacuationTreatment.Unassigned)
            {
                return FailRestore("撤离处理方式无效", out error);
            }
            if (double.IsNaN(remainingRatio) ||
                double.IsInfinity(remainingRatio) ||
                remainingRatio < 0d || remainingRatio > 1d)
            {
                return FailRestore("撤离剩余比例无效", out error);
            }
            if (!IsFiniteNonNegative(baseDismantleSeconds) ||
                !IsFiniteNonNegative(dismantleSeconds) || refund < 0)
            {
                return FailRestore("撤离耗时或退款无效", out error);
            }
            if (!IsFinitePositive(batchContext.ProductivityMultiplier))
                return FailRestore("撤离批次生产力无效", out error);

            switch (treatment)
            {
                case BuildingEvacuationTreatment.Abandon:
                    if (baseDismantleSeconds != 0f ||
                        dismantleSeconds != 0f || refund != 0)
                    {
                        return FailRestore(
                            "遗弃项目不能包含拆除耗时或退款",
                            out error);
                    }
                    break;
                case BuildingEvacuationTreatment.QuickDismantle:
                    if (baseDismantleSeconds != 0f ||
                        dismantleSeconds != 0f)
                    {
                        return FailRestore(
                            "快速拆除项目不能包含拆除耗时",
                            out error);
                    }
                    break;
                case BuildingEvacuationTreatment.FullDismantle:
                    if (baseDismantleSeconds <= 0f ||
                        dismantleSeconds <= 0f)
                    {
                        return FailRestore("完整拆除耗时必须大于零", out error);
                    }
                    float expected = baseDismantleSeconds /
                        batchContext.ProductivityMultiplier;
                    float tolerance = RestoreTolerance *
                        Math.Max(1f, Math.Max(expected, dismantleSeconds));
                    if (!IsFinitePositive(expected) ||
                        Math.Abs(expected - dismantleSeconds) > tolerance)
                    {
                        return FailRestore(
                            "完整拆除耗时与冻结批次上下文不一致",
                            out error);
                    }
                    break;
            }

            work = new BuildingEvacuationWork(
                stableInstanceId,
                treatment,
                remainingRatio,
                baseDismantleSeconds,
                dismantleSeconds,
                refund,
                batchContext);
            return true;
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

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= 0f;
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                value > 0f;
        }

        private static bool FailRestore(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
