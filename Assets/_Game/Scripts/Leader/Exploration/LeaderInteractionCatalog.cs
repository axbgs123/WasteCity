using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Leader.Exploration
{
    public static class LeaderInteractionCatalog
    {
        public const float ManualGatherMaximumDistance = 1.5f;
        public const float ManualGatherCycleSeconds = 6f;
        public const int ManualGatherAmount = 1;

        public const string CenJinDistressSiteId =
            "core.exploration.site.cen-jin-distress";
        public const string CenJinAttentionReasonId =
            "core.attention.rescue.cen-jin";
        public const float CenJinRescueMaximumDistance = 3f;
        public const float CenJinRescueSeconds = 12f;
        public const int CenJinBiomassCost = 10;
        public const float CenJinTimelyThresholdSeconds = 90f;
        public const float CenJinCriticalThresholdSeconds = 180f;
        public const int CenJinPopulationReward = 40;

        private static readonly string[] manualGatherResourceIds =
            (string[])ResourceIds.Base.Clone();
        private static readonly IReadOnlyList<string> manualGatherResourceView =
            Array.AsReadOnly(manualGatherResourceIds);
        private static readonly HashSet<string> manualGatherResourceLookup =
            new HashSet<string>(manualGatherResourceIds, StringComparer.Ordinal);

        public static string CenJinCharacterId => CharacterCatalog.CenJinId;

        public static IReadOnlyList<string> ManualGatherResourceIds =>
            manualGatherResourceView;

        public static bool IsManualGatherResource(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                manualGatherResourceLookup.Contains(resourceId);
        }

        public static string CenJinStableEventKey(string sessionId)
        {
            return string.IsNullOrWhiteSpace(sessionId)
                ? string.Empty
                : CenJinDistressSiteId + ":" + sessionId;
        }
    }
}
