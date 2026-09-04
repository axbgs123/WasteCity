using System;
using System.Collections.Generic;

namespace WasteCity.World.Exploration
{
    public enum OutpostAlertSeverity
    {
        None = 0,
        Guard = 1,
        UnderAttack = 2,
        Critical = 3,
    }

    public sealed class OutpostAlertDefinition
    {
        internal OutpostAlertDefinition(
            string stableId,
            string chineseName,
            OutpostAlertSeverity severity)
        {
            StableId = stableId ?? throw new ArgumentNullException(
                nameof(stableId));
            ChineseName = chineseName ?? throw new ArgumentNullException(
                nameof(chineseName));
            Severity = severity;
        }

        public string StableId { get; }
        public string ChineseName { get; }
        public OutpostAlertSeverity Severity { get; }
    }

    public static class OutpostAlertCatalog
    {
        public const string GuardId = "core.outpost.alert.guard";
        public const string UnderAttackId = "core.outpost.alert.under-attack";
        public const string CriticalId = "core.outpost.alert.critical";

        public static readonly OutpostAlertDefinition Guard =
            new OutpostAlertDefinition(
                GuardId,
                "警戒",
                OutpostAlertSeverity.Guard);

        public static readonly OutpostAlertDefinition UnderAttack =
            new OutpostAlertDefinition(
                UnderAttackId,
                "受袭",
                OutpostAlertSeverity.UnderAttack);

        public static readonly OutpostAlertDefinition Critical =
            new OutpostAlertDefinition(
                CriticalId,
                "危急",
                OutpostAlertSeverity.Critical);

        public static readonly IReadOnlyList<OutpostAlertDefinition> All =
            Array.AsReadOnly(new[] { Guard, UnderAttack, Critical });

        public static OutpostAlertDefinition Find(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId)) return null;
            for (var index = 0; index < All.Count; index++)
            {
                OutpostAlertDefinition candidate = All[index];
                if (string.Equals(
                        candidate.StableId,
                        stableId,
                        StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        public static OutpostAlertDefinition ForSeverity(
            OutpostAlertSeverity severity)
        {
            for (var index = 0; index < All.Count; index++)
            {
                OutpostAlertDefinition candidate = All[index];
                if (candidate.Severity == severity) return candidate;
            }
            return null;
        }
    }
}
