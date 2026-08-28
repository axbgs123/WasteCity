using System;

namespace WasteCity.Persistence.ThreeD
{
    [Serializable]
    public sealed class FormalThreeDCivilizationExpansionSaveData
    {
        public const string ConfigurationSignature =
            "builtin:civilization-expansion@1";

        public string configurationSignature = ConfigurationSignature;
        public FormalThreeDArmyLeaderSaveData armyLeader =
            new FormalThreeDArmyLeaderSaveData();
        public FormalThreeDWorldLayerSaveData worldLayer =
            new FormalThreeDWorldLayerSaveData();
        public FormalThreeDCharactersPoliticsSaveData charactersPolitics =
            new FormalThreeDCharactersPoliticsSaveData();
    }

    [Serializable]
    public sealed class FormalThreeDArmyLeaderSaveData
    {
        public ulong revision;
        public ulong nextUnitOrdinal = 1;
        public ulong nextSquadOrdinal = 2;
        public ulong nextExpeditionOrdinal = 1;
        public float fixedStepAccumulatorSeconds;
        public bool leaderAssigned;
        public bool leaderHealthy;
        public FormalThreeDLeaderSaveData leader =
            new FormalThreeDLeaderSaveData();
        public FormalThreeDArmyUnitSaveData[] units =
            Array.Empty<FormalThreeDArmyUnitSaveData>();
        public FormalThreeDArmyManufacturingSaveData[] manufacturing =
            Array.Empty<FormalThreeDArmyManufacturingSaveData>();
        public FormalThreeDArmyLossSaveData[] losses =
            Array.Empty<FormalThreeDArmyLossSaveData>();
        public FormalThreeDArmySquadSaveData[] squads =
            Array.Empty<FormalThreeDArmySquadSaveData>();
        public FormalThreeDArmyExpeditionSaveData expedition;
    }

    [Serializable]
    public sealed class FormalThreeDArmyManufacturingSaveData
    {
        public string definitionId;
        public float progressSeconds;
    }

    [Serializable]
    public sealed class FormalThreeDArmyLossSaveData
    {
        public string definitionId;
        public int count;
    }

    [Serializable]
    public sealed class FormalThreeDLeaderSaveData
    {
        public string characterId = "core.character.cen-jin";
        public bool recruited = true;
        public bool injured;
        public float x;
        public float y;
        public float cooldownRemaining;
        public float boostRemaining;
        public float lockoutRemaining;
    }

    [Serializable]
    public sealed class FormalThreeDArmyUnitSaveData
    {
        public string stableUnitId;
        public string definitionId;
        public string squadId;
        public int currentHealth;
        public bool dormant;
        public float maintenanceRemainingSeconds;
        public float maintenanceElapsedSeconds;
    }

    [Serializable]
    public sealed class FormalThreeDArmySquadSaveData
    {
        public string stableSquadId;
        public int command;
        public bool hasFixedRally;
        public bool hasExpeditionTarget;
        public int cellX;
        public int cellY;
        public int rallyX;
        public int rallyY;
        public float rallyFloatX;
        public float rallyFloatY;
        public int destinationX;
        public int destinationY;
        public int pathIndex;
        public float segmentProgress;
        public bool leaderAssigned;
        public bool leaderHealthy;
        public int puppetLosses;
        public int behemothLosses;
        public int controlledLosses;
        public string[] unitIds = Array.Empty<string>();
        public FormalThreeDGridPointSaveData[] path =
            Array.Empty<FormalThreeDGridPointSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDArmyExpeditionSaveData
    {
        public string expeditionId;
        public string squadId;
        public string sessionId;
        public int expeditionOrdinal;
        public int phase;
        public int targetX;
        public int targetY;
        public ulong encounterSeed;
        public float remainingSeconds;
        public float outboundDurationSeconds;
        public float returnDurationSeconds;
        public bool retreatRequested;
        public bool leaderHealthy;
        public bool hasResolution;
        public bool victory;
        public float armyPower;
        public int enemyPower;
        public string[] enemyDefinitionIds = Array.Empty<string>();
        public string[] casualtyStableUnitIds = Array.Empty<string>();
        public FormalThreeDArmyExpeditionUnitSaveData[] units =
            Array.Empty<FormalThreeDArmyExpeditionUnitSaveData>();
        public FormalThreeDResourceAmountSaveData[] pendingLoot =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDArmyExpeditionUnitSaveData
    {
        public string stableUnitId;
        public string definitionId;
        public int currentHealth;
        public bool active;
    }

    [Serializable]
    public sealed class FormalThreeDWorldLayerSaveData
    {
        public ulong revision;
        public ulong nextSettlementOrdinal = 3;
        public ulong nextConvoyOrdinal = 1;
        public string primaryCityId = "core.city.000001";
        public string focusedSettlementId = "core.city.000001";
        public string controlledCityId = "core.city.000001";
        public FormalThreeDSettlementSaveData[] settlements =
            Array.Empty<FormalThreeDSettlementSaveData>();
        public FormalThreeDConvoySaveData[] convoys =
            Array.Empty<FormalThreeDConvoySaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDSettlementSaveData
    {
        public string stableSettlementId;
        public int kind;
        public int x;
        public int y;
        public int population;
        public int populationCapacity;
        public int autonomousTemplate;
        public bool communicationConnected = true;
        public bool supplyConnected = true;
        public bool maintenanceConnected = true;
        public int loyalty = 70;
        public float productionRemainingSeconds;
        public ulong revision;
        public FormalThreeDResourceAmountSaveData[] inventory =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDConvoySaveData
    {
        public string stableConvoyId;
        public string sourceSettlementId;
        public string destinationSettlementId;
        public string escortSquadId;
        public string sessionId;
        public int status;
        public int pathIndex;
        public int completedPathCells;
        public float segmentProgress;
        public float segmentProgressSeconds;
        public bool riskResolved;
        public bool intercepted;
        public bool interceptionImmune;
        public int appliedRiskPercent;
        public FormalThreeDGridPointSaveData[] path =
            Array.Empty<FormalThreeDGridPointSaveData>();
        public FormalThreeDResourceAmountSaveData[] cargo =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDGridPointSaveData
    {
        public int x;
        public int y;
    }

    [Serializable]
    public sealed class FormalThreeDCharactersPoliticsSaveData
    {
        public ulong revision;
        public ulong nextRescueOrdinal = 1;
        public ulong nextSuccessionOrdinal = 1;
        public ulong nextOfferOrdinal = 1;
        public string diplomacySessionId =
            "formal.session.default";
        public int convoyInterceptionImmunityCharges;
        public string currentLeaderId = "core.character.cen-jin";
        public string designatedSuccessorId = string.Empty;
        public int leadershipState;
        public float councilEfficiencyMultiplier = 1f;
        public FormalThreeDCharacterSaveData[] characters =
            Array.Empty<FormalThreeDCharacterSaveData>();
        public FormalThreeDCorpseSaveData[] corpses =
            Array.Empty<FormalThreeDCorpseSaveData>();
        public FormalThreeDRescueSaveData rescue;
        public FormalThreeDSuccessionSaveData succession;
        public FormalThreeDInternalFactionSaveData[] internalFactions =
            Array.Empty<FormalThreeDInternalFactionSaveData>();
        public FormalThreeDExternalFactionSaveData[] externalFactions =
            Array.Empty<FormalThreeDExternalFactionSaveData>();
        public FormalThreeDDiplomacyOfferSaveData activeOffer;
    }

    [Serializable]
    public sealed class FormalThreeDCharacterSaveData
    {
        public string characterId;
        public int state;
        public int currentHealth;
        public int maximumHealth;
        public int x;
        public int y;
        public string assignedSettlementId;
        public int loyalty;
        public string permanentWoundId;
        public string[] permanentInjuryIds = Array.Empty<string>();
        public float downedRemainingSeconds;
        public float recoveryRemainingSeconds;
        public float downedElapsedSeconds;
        public int downCount;
        public string downedCauseId;
        public ulong damageRevision;
        public ulong lastDamageRuleTick = ulong.MaxValue;
        public string[] equipmentIds = Array.Empty<string>();
        public FormalThreeDRescueSaveData rescue;
    }

    [Serializable]
    public sealed class FormalThreeDCorpseSaveData
    {
        public string corpseId;
        public string characterId;
        public string settlementId;
        public int x;
        public int y;
        public bool recovered;
        public string[] equipmentIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalThreeDRescueSaveData
    {
        public string rescueId;
        public string targetCharacterId;
        public string rescuerCharacterId;
        public string settlementId;
        public string sourceId;
        public int method;
        public float progressSeconds;
        public float remainingSeconds;
        public int reservedBiomass;
        public ulong sourceDamageRevision;
    }

    [Serializable]
    public sealed class FormalThreeDSuccessionSaveData
    {
        public string successionId;
        public int phase;
        public string selectedCandidateId;
        public float support;
        public string[] candidateIds = Array.Empty<string>();
        public string[] committedEventIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalThreeDInternalFactionSaveData
    {
        public string factionId;
        public int influence;
        public int loyalty;
        public string supportedCandidateId;
        public FormalThreeDFactionCandidateSupportSaveData[] candidateSupports =
            Array.Empty<FormalThreeDFactionCandidateSupportSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDFactionCandidateSupportSaveData
    {
        public string characterId;
        public int support;
    }

    [Serializable]
    public sealed class FormalThreeDExternalFactionSaveData
    {
        public string factionId;
        public int relation;
        public int state;
        public float offerCooldownRemainingSeconds;
        public FormalThreeDDiplomacyOfferSaveData activeOffer;
    }

    [Serializable]
    public sealed class FormalThreeDDiplomacyOfferSaveData
    {
        public string offerId;
        public string factionId;
        public string giveResourceId;
        public int giveAmount;
        public string receiveResourceId;
        public int receiveAmount;
        public bool grantsConvoyImmunity;
        public int kind;
        public float remainingSeconds;
    }
}
