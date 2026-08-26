using System;
using System.Collections.Generic;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxVoidDebtAttentionController3D
    {
        private const string ReasonId =
            "core.attention.fate.void-debt-periodic";

        private readonly FormalAttentionRuntime attention;
        private readonly FormalFateRuntime fate;
        private readonly FormalVoidDebtRuntime debt;

        public GrayboxVoidDebtAttentionController3D(
            FormalAttentionRuntime attention,
            FormalFateRuntime fate,
            FormalVoidDebtRuntime debt)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.fate = fate ?? throw new ArgumentNullException(nameof(fate));
            this.debt = debt ?? throw new ArgumentNullException(nameof(debt));
        }

        public bool Tick(float ruleDeltaSeconds, out string error)
        {
            FormalFateSnapshot fateState = fate.Capture();
            if (!string.Equals(
                    fateState.SelectedId,
                    FormalFateCatalog.VoidDebtId,
                    StringComparison.Ordinal) ||
                fateState.Level != debt.Level)
            {
                error = string.Empty;
                return true;
            }

            FormalVoidDebtSnapshot previousDebt = debt.Capture();
            FormalAttentionSnapshot previousAttention = attention.Capture();
            if (!debt.Tick(
                    ruleDeltaSeconds,
                    out IReadOnlyList<string> eventKeys,
                    out error))
            {
                return false;
            }
            if (eventKeys.Count == 0)
            {
                error = string.Empty;
                return true;
            }

            var candidate = new FormalAttentionRuntime();
            if (!candidate.TryRestore(previousAttention, out error))
            {
                debt.TryRestore(previousDebt, out _);
                return false;
            }
            for (var index = 0; index < eventKeys.Count; index++)
            {
                if (candidate.TryApply(
                        ReasonId,
                        eventKeys[index],
                        out error))
                {
                    continue;
                }
                debt.TryRestore(previousDebt, out _);
                return false;
            }
            if (!ReferenceEquals(attention.Capture(), previousAttention) ||
                !attention.TryRestore(candidate.Capture(), out error))
            {
                debt.TryRestore(previousDebt, out _);
                return false;
            }
            error = string.Empty;
            return true;
        }
    }
}
