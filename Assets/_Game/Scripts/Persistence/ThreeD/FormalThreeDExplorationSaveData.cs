using System;

namespace WasteCity.Persistence.ThreeD
{
    [Serializable]
    public sealed class FormalThreeDExplorationSaveData
    {
        public const string ConfigurationSignature =
            "builtin:exploration-leader-outpost@1";
        public const int ConfigurationVersion = 1;

        public string configurationSignature = ConfigurationSignature;
        public int configurationVersion = ConfigurationVersion;
        public string worldConfigurationSignature =
            "core.world.formal-3d.v2.64x48";
        public int width = 64;
        public int height = 48;
        public bool[] exploredCells = new bool[64 * 48];
        public FormalThreeDScanZoneSaveData[] scanZones =
            Array.Empty<FormalThreeDScanZoneSaveData>();
        public FormalThreeDIntelSaveData[] intel =
            Array.Empty<FormalThreeDIntelSaveData>();
        public FormalThreeDLeaderInteractionSaveData leader =
            new FormalThreeDLeaderInteractionSaveData();
        public FormalThreeDCenJinDistressSaveData cenJinDistress =
            new FormalThreeDCenJinDistressSaveData();
        public FormalThreeDOutpostAlertSaveData[] outpostAlerts =
            Array.Empty<FormalThreeDOutpostAlertSaveData>();
        public ulong revision;
    }

    [Serializable]
    public sealed class FormalThreeDScanZoneSaveData
    {
        public string zoneId;
        public string committedEventKey;
    }

    [Serializable]
    public sealed class FormalThreeDIntelSaveData
    {
        public string stableIntelId;
        public int ownerKind;
        public string ownerStableId;
        public string summary = string.Empty;
        public int x;
        public int y;
        public float remainingFreshSeconds;
        public float remainingExpirySeconds;
        public bool hasMutableValue;
        public int mutableValue;
        public bool depleted;
        public ulong sourceRevision;
    }

    [Serializable]
    public sealed class FormalThreeDLeaderInteractionSaveData
    {
        public int requestedControlMode;
        public ulong revision;
        public FormalThreeDManualGatherSaveData manualGather =
            new FormalThreeDManualGatherSaveData();
    }

    [Serializable]
    public sealed class FormalThreeDManualGatherSaveData
    {
        public bool active;
        public string targetNodeId = string.Empty;
        public string targetResourceId = string.Empty;
        public float remainingCycleSeconds;
        public ulong cycleOrdinal;
        public ulong revision;
    }

    [Serializable]
    public sealed class FormalThreeDCenJinDistressSaveData
    {
        public const string SiteId =
            "core.exploration.site.cen-jin-distress";

        public string siteId = SiteId;
        public int state = 5;
        public float elapsedSinceDiscoverySeconds;
        public float rescueRemainingSeconds;
        public int reservedBiomass;
        public string committedRewardKey = string.Empty;
        public ulong revision;
    }

    [Serializable]
    public sealed class FormalThreeDOutpostAlertSaveData
    {
        public string stableAlertId;
        public string settlementId;
        public string attackFactId;
        public int severity;
        public int x;
        public int y;
        public string threatSummary;
        public int estimatedLossRiskPercent;
        public float estimatedSecondsToLoss;
        public double firstRuleTimeSeconds;
        public double latestRuleTimeSeconds;
        public bool acknowledged;
        public bool resolved;
        public ulong revision;
    }
}
