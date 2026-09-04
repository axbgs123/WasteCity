using System;
using System.Collections.Generic;

namespace WasteCity.World.Exploration
{
    public sealed class OutpostAlertEntry : IEquatable<OutpostAlertEntry>
    {
        internal OutpostAlertEntry(
            string stableAlertId,
            string settlementId,
            int x,
            int y,
            OutpostAlertSeverity severity,
            string threatSummary,
            int estimatedLossRiskPercent,
            float estimatedSecondsToLoss,
            double firstRuleTime,
            double latestRuleTime,
            bool isAcknowledged,
            bool isResolved)
        {
            StableAlertId = stableAlertId;
            SettlementId = settlementId;
            X = x;
            Y = y;
            Severity = severity;
            ThreatSummary = threatSummary;
            EstimatedLossRiskPercent = estimatedLossRiskPercent;
            EstimatedSecondsToLoss = estimatedSecondsToLoss;
            FirstRuleTime = firstRuleTime;
            LatestRuleTime = latestRuleTime;
            IsAcknowledged = isAcknowledged;
            IsResolved = isResolved;
        }

        public string StableAlertId { get; }
        public string SettlementId { get; }
        public int X { get; }
        public int Y { get; }
        public OutpostAlertSeverity Severity { get; }
        public string ThreatSummary { get; }
        public int EstimatedLossRiskPercent { get; }
        public float EstimatedSecondsToLoss { get; }
        public double FirstRuleTime { get; }
        public double LatestRuleTime { get; }
        public bool IsAcknowledged { get; }
        public bool IsResolved { get; }

        public bool Equals(OutpostAlertEntry other)
        {
            return other != null &&
                   string.Equals(
                       StableAlertId,
                       other.StableAlertId,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       SettlementId,
                       other.SettlementId,
                       StringComparison.Ordinal) &&
                   X == other.X &&
                   Y == other.Y &&
                   Severity == other.Severity &&
                   string.Equals(
                       ThreatSummary,
                       other.ThreatSummary,
                       StringComparison.Ordinal) &&
                   EstimatedLossRiskPercent ==
                   other.EstimatedLossRiskPercent &&
                   EstimatedSecondsToLoss.Equals(
                       other.EstimatedSecondsToLoss) &&
                   FirstRuleTime.Equals(other.FirstRuleTime) &&
                   LatestRuleTime.Equals(other.LatestRuleTime) &&
                   IsAcknowledged == other.IsAcknowledged &&
                   IsResolved == other.IsResolved;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as OutpostAlertEntry);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = StableAlertId == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(StableAlertId);
                result = (result * 397) ^ (SettlementId == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(SettlementId));
                result = (result * 397) ^ X;
                result = (result * 397) ^ Y;
                result = (result * 397) ^ (int)Severity;
                result = (result * 397) ^ (ThreatSummary == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(ThreatSummary));
                result = (result * 397) ^ EstimatedLossRiskPercent;
                result = (result * 397) ^
                         EstimatedSecondsToLoss.GetHashCode();
                result = (result * 397) ^ FirstRuleTime.GetHashCode();
                result = (result * 397) ^ LatestRuleTime.GetHashCode();
                result = (result * 397) ^ IsAcknowledged.GetHashCode();
                result = (result * 397) ^ IsResolved.GetHashCode();
                return result;
            }
        }
    }

    public sealed class OutpostAlertRuntimeSnapshot :
        IEquatable<OutpostAlertRuntimeSnapshot>
    {
        private readonly IReadOnlyList<OutpostAlertEntry> alerts;

        public OutpostAlertRuntimeSnapshot(
            ulong revision,
            IReadOnlyList<OutpostAlertEntry> alerts)
        {
            Revision = revision;
            if (alerts == null || alerts.Count == 0)
            {
                this.alerts = Array.AsReadOnly(
                    Array.Empty<OutpostAlertEntry>());
                return;
            }

            var copy = new OutpostAlertEntry[alerts.Count];
            for (var index = 0; index < alerts.Count; index++)
                copy[index] = alerts[index];
            this.alerts = Array.AsReadOnly(copy);
        }

        public ulong Revision { get; }
        public IReadOnlyList<OutpostAlertEntry> Alerts => alerts;

        public bool Equals(OutpostAlertRuntimeSnapshot other)
        {
            if (other == null || Revision != other.Revision ||
                Alerts.Count != other.Alerts.Count)
                return false;
            for (var index = 0; index < Alerts.Count; index++)
            {
                if (!Equals(Alerts[index], other.Alerts[index]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as OutpostAlertRuntimeSnapshot);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = Revision.GetHashCode();
                for (var index = 0; index < Alerts.Count; index++)
                    result = (result * 397) ^
                             (Alerts[index]?.GetHashCode() ?? 0);
                return result;
            }
        }
    }

    /// <summary>
    /// Owns alert identity, escalation, acknowledgement, and resolution only.
    /// Settlement, coordinates, combat, and threat truth remain authoritative
    /// in their existing domain owners and are projected into this runtime.
    /// </summary>
    public sealed class OutpostAlertRuntime
    {
        private static readonly IReadOnlyList<OutpostAlertEntry> EmptyAlerts =
            Array.AsReadOnly(Array.Empty<OutpostAlertEntry>());

        private readonly Dictionary<string, OutpostAlertEntry> byStableAlertId =
            new Dictionary<string, OutpostAlertEntry>(StringComparer.Ordinal);
        private IReadOnlyList<OutpostAlertEntry> activeAlerts = EmptyAlerts;
        private IReadOnlyList<OutpostAlertEntry> unacknowledgedAlerts =
            EmptyAlerts;

        public ulong Revision { get; private set; }
        public IReadOnlyList<OutpostAlertEntry> ActiveAlerts => activeAlerts;
        public IReadOnlyList<OutpostAlertEntry> UnacknowledgedAlerts =>
            unacknowledgedAlerts;

        public OutpostAlertEntry Get(string stableAlertId)
        {
            if (string.IsNullOrWhiteSpace(stableAlertId)) return null;
            byStableAlertId.TryGetValue(
                stableAlertId,
                out OutpostAlertEntry result);
            return result;
        }

        public bool TryReport(
            string stableAlertId,
            string settlementId,
            int x,
            int y,
            OutpostAlertSeverity severity,
            string threatSummary,
            int estimatedLossRiskPercent,
            float estimatedSecondsToLoss,
            double ruleTime,
            out string error)
        {
            if (!TryValidateFact(
                    stableAlertId,
                    settlementId,
                    x,
                    y,
                    severity,
                    threatSummary,
                    estimatedLossRiskPercent,
                    estimatedSecondsToLoss,
                    ruleTime,
                    out error))
                return false;

            if (byStableAlertId.TryGetValue(
                    stableAlertId,
                    out OutpostAlertEntry existing))
            {
                if (!string.Equals(
                        existing.SettlementId,
                        settlementId,
                        StringComparison.Ordinal))
                {
                    error = "An alert cannot change its settlement identity.";
                    return false;
                }
                if (existing.IsResolved)
                {
                    error = string.Empty;
                    return true;
                }
                if (ruleTime < existing.LatestRuleTime)
                {
                    error = "Alert rule time cannot move backwards.";
                    return false;
                }

                OutpostAlertSeverity highestSeverity =
                    severity > existing.Severity
                        ? severity
                        : existing.Severity;
                bool escalated = highestSeverity > existing.Severity;
                var refreshed = new OutpostAlertEntry(
                    stableAlertId,
                    settlementId,
                    x,
                    y,
                    highestSeverity,
                    threatSummary,
                    estimatedLossRiskPercent,
                    estimatedSecondsToLoss,
                    existing.FirstRuleTime,
                    ruleTime,
                    escalated ? false : existing.IsAcknowledged,
                    false);
                if (existing.Equals(refreshed))
                {
                    error = string.Empty;
                    return true;
                }

                byStableAlertId[stableAlertId] = refreshed;
                Revision++;
                RebuildProjections();
                error = string.Empty;
                return true;
            }

            byStableAlertId.Add(
                stableAlertId,
                new OutpostAlertEntry(
                    stableAlertId,
                    settlementId,
                    x,
                    y,
                    severity,
                    threatSummary,
                    estimatedLossRiskPercent,
                    estimatedSecondsToLoss,
                    ruleTime,
                    ruleTime,
                    false,
                    false));
            Revision++;
            RebuildProjections();
            error = string.Empty;
            return true;
        }

        public bool TryAcknowledge(string stableAlertId)
        {
            if (string.IsNullOrWhiteSpace(stableAlertId) ||
                !byStableAlertId.TryGetValue(
                    stableAlertId,
                    out OutpostAlertEntry existing) ||
                existing.IsResolved)
                return false;
            if (existing.IsAcknowledged) return true;

            byStableAlertId[stableAlertId] = CopyWithState(
                existing,
                true,
                false,
                existing.LatestRuleTime);
            Revision++;
            RebuildProjections();
            return true;
        }

        public bool TryResolve(
            string stableAlertId,
            double ruleTime,
            out string error)
        {
            if (!IsStableId(stableAlertId))
            {
                error = "A stable alert ID is required.";
                return false;
            }
            if (!IsFiniteNonNegative(ruleTime))
            {
                error = "Alert rule time must be finite and non-negative.";
                return false;
            }
            if (!byStableAlertId.TryGetValue(
                    stableAlertId,
                    out OutpostAlertEntry existing))
            {
                error = "The alert does not exist.";
                return false;
            }
            if (existing.IsResolved)
            {
                error = string.Empty;
                return true;
            }
            if (ruleTime < existing.LatestRuleTime)
            {
                error = "Alert rule time cannot move backwards.";
                return false;
            }

            byStableAlertId[stableAlertId] = CopyWithState(
                existing,
                existing.IsAcknowledged,
                true,
                ruleTime);
            Revision++;
            RebuildProjections();
            error = string.Empty;
            return true;
        }

        public OutpostAlertRuntimeSnapshot Capture()
        {
            if (byStableAlertId.Count == 0)
                return new OutpostAlertRuntimeSnapshot(Revision, EmptyAlerts);

            var all = new List<OutpostAlertEntry>(byStableAlertId.Values);
            all.Sort(CompareStableIdentity);
            return new OutpostAlertRuntimeSnapshot(Revision, all);
        }

        public bool TryRestore(
            OutpostAlertRuntimeSnapshot snapshot,
            out string error)
        {
            if (snapshot == null)
            {
                error = "An outpost alert snapshot is required.";
                return false;
            }

            var candidate = new Dictionary<string, OutpostAlertEntry>(
                StringComparer.Ordinal);
            for (var index = 0; index < snapshot.Alerts.Count; index++)
            {
                OutpostAlertEntry entry = snapshot.Alerts[index];
                if (entry == null)
                {
                    error = "The snapshot contains a null outpost alert.";
                    return false;
                }
                if (!TryValidateEntry(entry, out error)) return false;
                if (candidate.ContainsKey(entry.StableAlertId))
                {
                    error = "The snapshot contains a duplicate stable alert ID.";
                    return false;
                }
                candidate.Add(entry.StableAlertId, entry);
            }

            byStableAlertId.Clear();
            foreach (KeyValuePair<string, OutpostAlertEntry> pair in candidate)
                byStableAlertId.Add(pair.Key, pair.Value);
            Revision = snapshot.Revision;
            RebuildProjections();
            error = string.Empty;
            return true;
        }

        private static OutpostAlertEntry CopyWithState(
            OutpostAlertEntry source,
            bool isAcknowledged,
            bool isResolved,
            double latestRuleTime)
        {
            return new OutpostAlertEntry(
                source.StableAlertId,
                source.SettlementId,
                source.X,
                source.Y,
                source.Severity,
                source.ThreatSummary,
                source.EstimatedLossRiskPercent,
                source.EstimatedSecondsToLoss,
                source.FirstRuleTime,
                latestRuleTime,
                isAcknowledged,
                isResolved);
        }

        private static bool TryValidateEntry(
            OutpostAlertEntry entry,
            out string error)
        {
            if (!TryValidateFact(
                    entry.StableAlertId,
                    entry.SettlementId,
                    entry.X,
                    entry.Y,
                    entry.Severity,
                    entry.ThreatSummary,
                    entry.EstimatedLossRiskPercent,
                    entry.EstimatedSecondsToLoss,
                    entry.LatestRuleTime,
                    out error))
                return false;
            if (!IsFiniteNonNegative(entry.FirstRuleTime) ||
                entry.FirstRuleTime > entry.LatestRuleTime)
            {
                error = "Alert first rule time must precede latest rule time.";
                return false;
            }
            return true;
        }

        private static bool TryValidateFact(
            string stableAlertId,
            string settlementId,
            int x,
            int y,
            OutpostAlertSeverity severity,
            string threatSummary,
            int estimatedLossRiskPercent,
            float estimatedSecondsToLoss,
            double ruleTime,
            out string error)
        {
            if (!IsStableId(stableAlertId))
            {
                error = "A stable alert ID is required.";
                return false;
            }
            if (!IsStableId(settlementId))
            {
                error = "A stable settlement ID is required.";
                return false;
            }
            if (x < 0 || y < 0)
            {
                error = "Alert coordinates must be non-negative.";
                return false;
            }
            if (OutpostAlertCatalog.ForSeverity(severity) == null)
            {
                error = "The outpost alert severity is invalid.";
                return false;
            }
            if (!IsStableId(threatSummary))
            {
                error = "A threat summary is required.";
                return false;
            }
            if (estimatedLossRiskPercent < 0 ||
                estimatedLossRiskPercent > 100)
            {
                error = "Estimated loss risk must be between 0 and 100.";
                return false;
            }
            if (estimatedSecondsToLoss < 0f ||
                float.IsNaN(estimatedSecondsToLoss) ||
                float.IsInfinity(estimatedSecondsToLoss))
            {
                error = "Estimated time to loss must be finite and non-negative.";
                return false;
            }
            if (!IsFiniteNonNegative(ruleTime))
            {
                error = "Alert rule time must be finite and non-negative.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsStableId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   string.Equals(value, value.Trim(), StringComparison.Ordinal);
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return value >= 0d && !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }

        private void RebuildProjections()
        {
            if (byStableAlertId.Count == 0)
            {
                activeAlerts = EmptyAlerts;
                unacknowledgedAlerts = EmptyAlerts;
                return;
            }

            var active = new List<OutpostAlertEntry>();
            var unread = new List<OutpostAlertEntry>();
            foreach (OutpostAlertEntry entry in byStableAlertId.Values)
            {
                if (entry.IsResolved) continue;
                active.Add(entry);
                if (!entry.IsAcknowledged) unread.Add(entry);
            }
            active.Sort(CompareActiveAlerts);
            unread.Sort(CompareActiveAlerts);
            activeAlerts = Array.AsReadOnly(active.ToArray());
            unacknowledgedAlerts = Array.AsReadOnly(unread.ToArray());
        }

        private static int CompareActiveAlerts(
            OutpostAlertEntry left,
            OutpostAlertEntry right)
        {
            int result = right.Severity.CompareTo(left.Severity);
            if (result != 0) return result;
            result = right.EstimatedLossRiskPercent.CompareTo(
                left.EstimatedLossRiskPercent);
            if (result != 0) return result;
            return CompareStableIdentity(left, right);
        }

        private static int CompareStableIdentity(
            OutpostAlertEntry left,
            OutpostAlertEntry right)
        {
            return string.Compare(
                left.StableAlertId,
                right.StableAlertId,
                StringComparison.Ordinal);
        }
    }
}
