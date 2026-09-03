using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxVoidChestController3D
    {
        private readonly FormalFateRuntime fate;
        private readonly VoidChestRuntime runtime;
        private readonly HashSet<string> committedDeathIds =
            new HashSet<string>(StringComparer.Ordinal);
        private ulong nextDeathOrdinal = 1ul;

        public GrayboxVoidChestController3D(
            FormalFateRuntime fate,
            VoidChestRuntime runtime)
        {
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
            SynchronizeFromRuntime();
        }

        public ulong NextDeathOrdinal
        {
            get
            {
                SynchronizeFromRuntime();
                return nextDeathOrdinal;
            }
        }

        private void SynchronizeFromRuntime()
        {
            committedDeathIds.Clear();
            nextDeathOrdinal = 1ul;
            VoidChestSnapshot snapshot = runtime.Capture();
            for (var index = 0; index < snapshot.Evaluations.Count; index++)
            {
                VoidChestEvaluation evaluation = snapshot.Evaluations[index];
                committedDeathIds.Add(evaluation.DeathId);
                nextDeathOrdinal = Math.Max(
                    nextDeathOrdinal,
                    evaluation.SequenceOrdinal + 1ul);
            }
        }

        public bool TryEvaluateOrdinaryEnemyDeath(
            string stableDeathId,
            out VoidChestEvaluation evaluation,
            out string error)
        {
            evaluation = null;
            if (!IsSelected())
            {
                error = "当前命轨不是虚空宝箱";
                return false;
            }
            SynchronizeFromRuntime();
            if (string.IsNullOrWhiteSpace(stableDeathId) ||
                committedDeathIds.Contains(stableDeathId))
            {
                error = "敌人死亡事件无效或已经处理";
                return false;
            }
            if (!runtime.TryEvaluateDeath(
                    stableDeathId,
                    nextDeathOrdinal,
                    out evaluation,
                    out error))
            {
                return false;
            }
            committedDeathIds.Add(stableDeathId);
            unchecked { nextDeathOrdinal++; }
            return true;
        }

        public bool TryClaim(
            string chestId,
            CityResourceStorageModel storage,
            out string error)
        {
            if (!IsSelected() || storage == null ||
                !TryFindUnclaimed(chestId, out VoidChestEvaluation reward))
            {
                error = "灰烬宝箱不存在、已领取或当前不可领取";
                return false;
            }

            int accepted = storage.AddToNetwork(
                reward.ResourceId,
                reward.Amount);
            if (accepted != reward.Amount)
            {
                if (accepted > 0)
                    storage.TrySpendFromNetwork(reward.ResourceId, accepted);
                error = "城市库存空间不足，灰烬宝箱保持未领取";
                return false;
            }
            if (runtime.TryClaim(chestId, out error)) return true;

            storage.TrySpendFromNetwork(reward.ResourceId, reward.Amount);
            return false;
        }

        private bool TryFindUnclaimed(
            string chestId,
            out VoidChestEvaluation result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(chestId)) return false;
            IReadOnlyList<VoidChestEvaluation> evaluations =
                runtime.Capture().Evaluations;
            for (var index = 0; index < evaluations.Count; index++)
            {
                VoidChestEvaluation evaluation = evaluations[index];
                if (evaluation.Dropped && !evaluation.Claimed &&
                    string.Equals(
                        evaluation.ChestId,
                        chestId,
                        StringComparison.Ordinal))
                {
                    result = evaluation;
                    return true;
                }
            }
            return false;
        }

        private bool IsSelected()
        {
            FormalFateSnapshot snapshot = fate.Capture();
            return snapshot.Level >= 1 && string.Equals(
                snapshot.SelectedId,
                FormalFateCatalog.VoidChestId,
                StringComparison.Ordinal);
        }
    }
}
