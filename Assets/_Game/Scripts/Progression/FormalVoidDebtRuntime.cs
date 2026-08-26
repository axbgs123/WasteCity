using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using WasteCity.Economy;

namespace WasteCity.Progression
{
    public sealed class FormalVoidDebtEntry
    {
        public FormalVoidDebtEntry(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public string ResourceId { get; }
        public int Amount { get; }
    }

    public sealed class FormalVoidDebtSnapshot
    {
        private readonly ReadOnlyCollection<FormalVoidDebtEntry> debts;

        public FormalVoidDebtSnapshot(
            int level,
            double settlementRemainingSeconds,
            ulong nextSettlementOrdinal,
            ulong revision,
            FormalVoidDebtEntry[] debts)
        {
            Level = level;
            SettlementRemainingSeconds = settlementRemainingSeconds;
            NextSettlementOrdinal = nextSettlementOrdinal;
            Revision = revision;
            FormalVoidDebtEntry[] source = debts ??
                Array.Empty<FormalVoidDebtEntry>();
            var copy = new FormalVoidDebtEntry[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                FormalVoidDebtEntry entry = source[index];
                copy[index] = entry == null
                    ? null
                    : new FormalVoidDebtEntry(
                        entry.ResourceId,
                        entry.Amount);
            }
            this.debts = Array.AsReadOnly(copy);
        }

        public int Level { get; }
        public double SettlementRemainingSeconds { get; }
        public ulong NextSettlementOrdinal { get; }
        public ulong Revision { get; }
        public IReadOnlyList<FormalVoidDebtEntry> Debts => debts;
    }

    public sealed class FormalVoidDebtRuntime
    {
        public const double LevelOneSettlementSeconds = 30d;
        public const double LevelTwoSettlementSeconds = 60d;
        public const int DebtPerAttentionUnit = 10;

        private const double SettlementEpsilon = 0.0000001d;

        private readonly SortedDictionary<string, int> debts =
            new SortedDictionary<string, int>(StringComparer.Ordinal);
        private int level;
        private int totalDebt;
        private double settlementRemainingSeconds;
        private ulong nextSettlementOrdinal = 1ul;
        private ulong revision;
        private FormalVoidDebtSnapshot cachedSnapshot;

        public FormalVoidDebtRuntime()
            : this(1)
        {
        }

        public FormalVoidDebtRuntime(int level)
        {
            if (!IsSupportedLevel(level))
                throw new ArgumentOutOfRangeException(nameof(level));
            this.level = level;
            RebuildSnapshot();
        }

        public int Level => level;
        public int TotalDebt => totalDebt;
        public double SettlementRemainingSeconds =>
            settlementRemainingSeconds;
        public ulong NextSettlementOrdinal => nextSettlementOrdinal;
        public ulong Revision => revision;

        public int GetDebt(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                debts.TryGetValue(resourceId, out int amount)
                ? amount
                : 0;
        }

        public bool TryBorrowConstruction(
            string resourceId,
            int amount,
            out string error)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                amount <= 0)
            {
                error = "Void debt requires a known resource and positive amount.";
                return false;
            }
            int before = GetDebt(resourceId);
            long resourceTotal = (long)before + amount;
            long nextTotal = (long)totalDebt + amount;
            if (resourceTotal > int.MaxValue || nextTotal > int.MaxValue)
            {
                error = "Void debt exceeds the supported amount.";
                return false;
            }

            debts[resourceId] = (int)resourceTotal;
            totalDebt = (int)nextTotal;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool Repay(
            string resourceId,
            int amount,
            out int repaid,
            out int residual,
            out string error)
        {
            repaid = 0;
            residual = 0;
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                amount <= 0)
            {
                error = "Void debt repayment requires a known resource and " +
                    "positive amount.";
                return false;
            }

            int before = GetDebt(resourceId);
            repaid = Math.Min(before, amount);
            residual = amount - repaid;
            if (repaid == 0)
            {
                error = string.Empty;
                return true;
            }

            int remaining = before - repaid;
            if (remaining == 0) debts.Remove(resourceId);
            else debts[resourceId] = remaining;
            totalDebt -= repaid;
            if (totalDebt == 0) settlementRemainingSeconds = 0d;
            unchecked { revision++; }
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        public bool Tick(
            float ruleDeltaSeconds,
            out IReadOnlyList<string> stableEventKeys,
            out string error)
        {
            stableEventKeys = Array.Empty<string>();
            if (float.IsNaN(ruleDeltaSeconds) ||
                float.IsInfinity(ruleDeltaSeconds) ||
                ruleDeltaSeconds < 0f)
            {
                error = "Void debt rule delta must be finite and non-negative.";
                return false;
            }
            if (ruleDeltaSeconds == 0f || totalDebt == 0)
            {
                error = string.Empty;
                return true;
            }

            double interval = SettlementInterval(level);
            double remaining = settlementRemainingSeconds <= SettlementEpsilon
                ? interval
                : settlementRemainingSeconds;
            remaining -= ruleDeltaSeconds;
            List<string> keys = null;
            while (remaining <= SettlementEpsilon)
            {
                ulong cycleOrdinal = nextSettlementOrdinal;
                unchecked { nextSettlementOrdinal++; }
                int units = totalDebt / DebtPerAttentionUnit;
                if (units > 0)
                {
                    keys ??= new List<string>(units);
                    for (var unit = 1; unit <= units; unit++)
                    {
                        keys.Add(
                            "void-debt:" + cycleOrdinal.ToString(
                                "D6",
                                CultureInfo.InvariantCulture) +
                            ":unit:" + unit.ToString(
                                "D4",
                                CultureInfo.InvariantCulture));
                    }
                }
                remaining += interval;
            }

            settlementRemainingSeconds = remaining;
            unchecked { revision++; }
            RebuildSnapshot();
            stableEventKeys = keys == null
                ? Array.Empty<string>()
                : Array.AsReadOnly(keys.ToArray());
            error = string.Empty;
            return true;
        }

        public FormalVoidDebtSnapshot Capture()
        {
            return cachedSnapshot;
        }

        public bool TryRestore(
            FormalVoidDebtSnapshot snapshot,
            out string error)
        {
            if (snapshot == null ||
                !IsSupportedLevel(snapshot.Level) ||
                snapshot.NextSettlementOrdinal == 0ul ||
                double.IsNaN(snapshot.SettlementRemainingSeconds) ||
                double.IsInfinity(snapshot.SettlementRemainingSeconds) ||
                snapshot.SettlementRemainingSeconds < 0d ||
                snapshot.SettlementRemainingSeconds >
                    SettlementInterval(snapshot.Level))
            {
                error = "Void debt snapshot metadata is invalid.";
                return false;
            }

            var prepared = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            long preparedTotal = 0L;
            for (var index = 0; index < snapshot.Debts.Count; index++)
            {
                FormalVoidDebtEntry entry = snapshot.Debts[index];
                if (entry == null ||
                    !ResourceCapacityPolicy.IsRegisteredResource(
                        entry.ResourceId) ||
                    entry.Amount <= 0 ||
                    prepared.ContainsKey(entry.ResourceId))
                {
                    error = "Void debt snapshot entries are invalid.";
                    return false;
                }
                preparedTotal += entry.Amount;
                if (preparedTotal > int.MaxValue)
                {
                    error = "Void debt snapshot total exceeds supported bounds.";
                    return false;
                }
                prepared.Add(entry.ResourceId, entry.Amount);
            }
            if (preparedTotal == 0L &&
                snapshot.SettlementRemainingSeconds != 0d)
            {
                error = "An empty void debt snapshot cannot retain a clock.";
                return false;
            }

            debts.Clear();
            foreach (KeyValuePair<string, int> entry in prepared)
                debts.Add(entry.Key, entry.Value);
            level = snapshot.Level;
            totalDebt = (int)preparedTotal;
            settlementRemainingSeconds = snapshot.SettlementRemainingSeconds;
            nextSettlementOrdinal = snapshot.NextSettlementOrdinal;
            revision = snapshot.Revision;
            RebuildSnapshot();
            error = string.Empty;
            return true;
        }

        private void RebuildSnapshot()
        {
            var entries = new FormalVoidDebtEntry[debts.Count];
            var index = 0;
            foreach (KeyValuePair<string, int> debt in debts)
            {
                entries[index++] = new FormalVoidDebtEntry(
                    debt.Key,
                    debt.Value);
            }
            cachedSnapshot = new FormalVoidDebtSnapshot(
                level,
                settlementRemainingSeconds,
                nextSettlementOrdinal,
                revision,
                entries);
        }

        private static bool IsSupportedLevel(int candidate)
        {
            return candidate == 1 || candidate == 2;
        }

        private static double SettlementInterval(int candidateLevel)
        {
            return candidateLevel == 2
                ? LevelTwoSettlementSeconds
                : LevelOneSettlementSeconds;
        }
    }
}
