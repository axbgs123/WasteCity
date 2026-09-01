using System;
using WasteCity.Combat;
using WasteCity.Progression;

namespace WasteCity.Persistence.ThreeD
{
    [Serializable]
    public sealed class FormalThreeDSaveData
    {
        public string sessionId;
        public FormalThreeDWorldSaveData world;
        public FormalThreeDCitySaveData city;
        public FormalThreeDBuildingsSaveData buildings;
        public FormalThreeDStorageSaveData storage;
        public FormalThreeDBackpackSaveData backpack;
        public FormalThreeDCraftingSaveData crafting;
        public FormalThreeDResearchSaveData research;
        public FormalThreeDResearchEffectStateSaveData researchEffectState =
            new FormalThreeDResearchEffectStateSaveData();
        public FormalThreeDProductionSaveData production;
        public FormalThreeDDefenseSaveData defense;
        public FormalThreeDDefenseCampaignSaveData defenseCampaign;
        public FormalThreeDEvacuationSaveData evacuation;
        public FormalThreeDPauseSaveData pause;
        public FormalThreeDProgressionSaveData progression =
            new FormalThreeDProgressionSaveData();
        public FormalThreeDCivilizationExpansionSaveData
            civilizationExpansion =
                new FormalThreeDCivilizationExpansionSaveData();
    }

    [Serializable]
    public sealed class FormalThreeDProgressionSaveData
    {
        public const string ConfigurationSignature =
            "builtin:progression@1";

        public string configurationSignature = ConfigurationSignature;
        public FormalThreeDAttentionSaveData attention =
            new FormalThreeDAttentionSaveData();
        public FormalThreeDFateSaveData fate = new FormalThreeDFateSaveData();
        public FormalThreeDFateEffectsSaveData fateEffects =
            new FormalThreeDFateEffectsSaveData();
        public FormalThreeDAttentionPressureSaveData pressure =
            new FormalThreeDAttentionPressureSaveData();
        public FormalThreeDCivilizationSaveData civilization =
            new FormalThreeDCivilizationSaveData();
    }

    [Serializable]
    public sealed class FormalThreeDAttentionSaveData
    {
        public int value = FormalAttentionCatalog.InitialValue;
        public ulong revision;
        public FormalThreeDAttentionHistorySaveData[] history =
            Array.Empty<FormalThreeDAttentionHistorySaveData>();
        public int[] reachedThresholds = Array.Empty<int>();
        public string[] committedStableEventKeys = Array.Empty<string>();
        public string[] completedOneShotReasonIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalThreeDAttentionHistorySaveData
    {
        public string reasonId;
        public string stableEventKey;
        public int requestedDelta;
        public int appliedDelta;
        public int valueAfter;
        public ulong revision;
        public float ruleTimeSeconds;
        public string sourceInstanceId;
    }

    [Serializable]
    public sealed class FormalThreeDAttentionPressureSaveData
    {
        public ulong revision;
        public FormalThreeDAttentionPressureEntrySaveData[] entries =
            Array.Empty<FormalThreeDAttentionPressureEntrySaveData>();
        public string activeEncounterId = string.Empty;
        public FormalThreeDPressureCampaignSaveData activeCampaign;
    }

    [Serializable]
    public sealed class FormalThreeDAttentionPressureEntrySaveData
    {
        public int threshold;
        public int state;
        public float warningRemainingSeconds;
    }

    [Serializable]
    public sealed class FormalThreeDPressureCampaignSaveData
    {
        public string campaignId;
        public int phase;
        public int currentWaveNumber;
        public FormalThreeDDefenseCampaignEnemyCountSaveData[]
            plannedEnemyCountsByEnemyId =
                Array.Empty<FormalThreeDDefenseCampaignEnemyCountSaveData>();
        public FormalThreeDDefenseCampaignEnemyCountSaveData[]
            spawnedEnemyCountsByEnemyId =
                Array.Empty<FormalThreeDDefenseCampaignEnemyCountSaveData>();
        public FormalThreeDDefenseCampaignEnemyCountSaveData[]
            defeatedEnemyCountsByEnemyId =
                Array.Empty<FormalThreeDDefenseCampaignEnemyCountSaveData>();
        public FormalThreeDDefenseCampaignSpawnAnchorSaveData[]
            frozenSpawnAnchors =
                Array.Empty<FormalThreeDDefenseCampaignSpawnAnchorSaveData>();
        public float warningRemainingSeconds;
        public float spawnClockSeconds;
        public float fixedStepAccumulatorSeconds;
        public int nextEnemyOrdinal;
        public int coreCurrentHealth;
        public int result;
        public FormalThreeDDefenseCampaignStatisticsSaveData statistics;
        public FormalThreeDDefenseCampaignEnemyStateSaveData[] enemyStates =
            Array.Empty<FormalThreeDDefenseCampaignEnemyStateSaveData>();
        public FormalThreeDPressureInjectedReinforcementSaveData[]
            injectedReinforcements =
                Array.Empty<FormalThreeDPressureInjectedReinforcementSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDPressureInjectedReinforcementSaveData
    {
        public string stableEventId;
        public FormalThreeDDefenseCampaignEnemyCountSaveData[] entries =
            Array.Empty<FormalThreeDDefenseCampaignEnemyCountSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDFateSaveData
    {
        public string[] offeredIds =
        {
            FormalFateCatalog.PocketUniverseId,
            FormalFateCatalog.VoidDebtId,
            FormalFateCatalog.RewindAnchorId,
        };
        public string selectedId = string.Empty;
        public int level;
        public ulong revision;
    }

    [Serializable]
    public sealed class FormalThreeDFateEffectsSaveData
    {
        public FormalThreeDPocketUniverseSaveData pocketUniverse =
            new FormalThreeDPocketUniverseSaveData();
        public FormalThreeDVoidDebtSaveData voidDebt =
            new FormalThreeDVoidDebtSaveData();
        public FormalThreeDRewindAnchorMetadataSaveData rewindAnchors =
            new FormalThreeDRewindAnchorMetadataSaveData();
    }

    [Serializable]
    public sealed class FormalThreeDPocketUniverseSaveData
    {
        public int level = 1;
        public ulong revision;
        public FormalThreeDPocketUniverseFlagshipSaveData[] flagships =
            Array.Empty<FormalThreeDPocketUniverseFlagshipSaveData>();
        public string[] collapsedFlagshipIds = Array.Empty<string>();
        public string firstProductionFlagshipId = string.Empty;
    }

    [Serializable]
    public sealed class FormalThreeDPocketUniverseFlagshipSaveData
    {
        public string buildingDefinitionId;
        public string stableInstanceId;
    }

    [Serializable]
    public sealed class FormalThreeDVoidDebtSaveData
    {
        public int level = 1;
        public double settlementRemainingSeconds;
        public ulong nextSettlementOrdinal = 1ul;
        public ulong revision;
        public FormalThreeDVoidDebtEntrySaveData[] debts =
            Array.Empty<FormalThreeDVoidDebtEntrySaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDVoidDebtEntrySaveData
    {
        public string resourceId;
        public int amount;
    }

    [Serializable]
    public sealed class FormalThreeDRewindAnchorMetadataSaveData
    {
        public ulong revision;
        public long nextCreationOrdinal = 1L;
        public FormalThreeDRewindAnchorEntrySaveData[] anchors =
            Array.Empty<FormalThreeDRewindAnchorEntrySaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDRewindAnchorEntrySaveData
    {
        public string stableAnchorId;
        public string internalKey;
        public long creationOrdinal;
        public string sessionId;
        public string payloadHashSha256;
        public long checkpointSequence;
        public string checkpointReasonId;
        public float checkpointRuleTimeSeconds;
        public string[] completedMilestoneIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalThreeDCivilizationSaveData
    {
        public const string FirstAscensionId =
            "first-civilization-ascension";

        public int level = 1;
        public ulong revision;
        public string ascensionId = string.Empty;
        public bool ascensionCompleted;
        public int sequenceStage;
        public float remainingRuleSeconds;
        public string[] committedAscensionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalThreeDWorldSaveData
    {
        public string worldDefinitionId;
        public int worldGenerationVersion;
        public int worldSeed;
        public int width;
        public int height;
        public string configurationSignature;
        public FormalThreeDResourceNodeSaveData[] resourceNodes =
            Array.Empty<FormalThreeDResourceNodeSaveData>();
        public FormalThreeDOrphanResourceSaveData[] orphanResources =
            Array.Empty<FormalThreeDOrphanResourceSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDResourceNodeSaveData
    {
        public string stableNodeId;
        public int x;
        public int y;
        public string resourceId;
        public int remainingAmount;
        public bool isDepleted;
    }

    [Serializable]
    public sealed class FormalThreeDResourceAmountSaveData
    {
        public string resourceId;
        public int amount;
    }

    [Serializable]
    public sealed class FormalThreeDOrphanResourceSaveData
    {
        public string resourceId;
        public int amount;
        public string ownerKind;
        public string ownerStableId;
    }

    [Serializable]
    public sealed class FormalThreeDCitySaveData
    {
        public float positionX;
        public float positionZ;
        public int cellX;
        public int cellY;
        public bool autopilotActive;
        public int destinationX;
        public int destinationY;
        public int cityMode;
        public int transitionReturnMode;
        public float transitionRemainingSeconds;
        public int population;
        public int populationCapacity;
    }

    [Serializable]
    public sealed class FormalThreeDBuildingsSaveData
    {
        public int nextStableInstanceOrdinal;
        public FormalThreeDBuildingInstanceSaveData[] instances =
            Array.Empty<FormalThreeDBuildingInstanceSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDBuildingInstanceSaveData
    {
        public string stableInstanceId;
        public string definitionId;
        public int site;
        public int x;
        public int y;
        public int orientation;
        public int state;
        public float constructionRemainingSeconds;
        public bool isPlayerOwned;
        public string boundResourceNodeId;
        public int boundNodeX;
        public int boundNodeY;
        public int footprintWidth;
        public int footprintHeight;
        public bool evacuationLockedCrossCheck;
    }

    [Serializable]
    public sealed class FormalThreeDStorageSaveData
    {
        public string configurationSignature;
        public FormalThreeDResourceAmountSaveData[] coreAmounts =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
        public FormalThreeDWarehouseSaveData[] warehouses =
            Array.Empty<FormalThreeDWarehouseSaveData>();
        public FormalThreeDOrphanResourceSaveData[] orphanResources =
            Array.Empty<FormalThreeDOrphanResourceSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDWarehouseSaveData
    {
        public string stableInstanceId;
        public string filterResourceId;
        public FormalThreeDResourceAmountSaveData[] amounts =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDBackpackSaveData
    {
        public FormalThreeDBackpackSlotSaveData[] slots =
            Array.Empty<FormalThreeDBackpackSlotSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDBackpackSlotSaveData
    {
        public int slotIndex;
        public string resourceId;
        public int amount;
    }

    [Serializable]
    public sealed class FormalThreeDCraftingSaveData
    {
        public int nextQueueOrdinal;
        public float activeProgressSeconds;
        public FormalThreeDCraftingExecutionSaveData[] executions =
            Array.Empty<FormalThreeDCraftingExecutionSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDCraftingExecutionSaveData
    {
        public string stableExecutionId;
        public string recipeId;
        public FormalThreeDResourceAmountSaveData[] reservedInputs =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDResearchSaveData
    {
        public string[] completedResearchIds = Array.Empty<string>();
        public string activeResearchId;
        public float remainingSeconds;
    }

    public enum FormalResearchEffectTargetKind
    {
        Global = 0,
        City = 1,
        Building = 2,
        Tower = 3,
        Enemy = 4,
        ArmyUnit = 5,
        Character = 6,
    }

    public enum FormalResearchEffectStatePhase
    {
        Active = 0,
        Boosting = 1,
        Lockout = 2,
        Cooldown = 3,
    }

    [Serializable]
    public sealed class FormalThreeDResearchEffectStateSaveData
    {
        public const string ConfigurationSignature =
            "builtin:research-effect-state@1";

        public string configurationSignature = ConfigurationSignature;
        public ulong revision;
        public long nextStableStateOrdinal = 1L;
        public FormalThreeDResearchEffectStateEntrySaveData[] states =
            Array.Empty<FormalThreeDResearchEffectStateEntrySaveData>();
        public FormalThreeDResearchEffectEmitterSaveData[] emitters =
            Array.Empty<FormalThreeDResearchEffectEmitterSaveData>();
        public FormalThreeDResearchRewardLedgerSaveData rewardLedger =
            new FormalThreeDResearchRewardLedgerSaveData();
    }

    [Serializable]
    public sealed class FormalThreeDResearchEffectStateEntrySaveData
    {
        public string stableStateId;
        public long creationOrdinal;
        public string effectId;
        public FormalResearchEffectTargetKind targetKind;
        public string targetStableId;
        public FormalResearchEffectStatePhase phase;
        public float remainingRuleSeconds;
        public int stacks;
        public float periodAccumulatorSeconds;
        public float currentValue;
    }

    [Serializable]
    public sealed class FormalThreeDResearchEffectEmitterSaveData
    {
        public string stableStateId;
        public long creationOrdinal;
        public string effectId;
        public string sourceTowerStableId;
        public string targetEnemyStableId;
        public float cooldownRemaining;
    }

    [Serializable]
    public sealed class FormalThreeDResearchRewardLedgerSaveData
    {
        public string[] committedRewardKeys = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalThreeDProductionSaveData
    {
        public FormalThreeDProductionStateSaveData[] states =
            Array.Empty<FormalThreeDProductionStateSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDProductionStateSaveData
    {
        public string stableInstanceId;
        public string definitionId;
        public FormalThreeDResourceAmountSaveData[] inputAmounts =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
        public bool hasReservedInputs;
        public FormalThreeDResourceAmountSaveData[] reservedInputs =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
        public FormalThreeDResourceAmountSaveData[] outputAmounts =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
        public float progressSeconds;
        public bool isPlayerPaused;
        public string boundResourceNodeId;
        public int boundNodeX;
        public int boundNodeY;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseSaveData
    {
        public string configurationSignature;
        public float spawnOriginX;
        public float spawnOriginZ;
        public bool tutorialTriggered;
        public int tutorialWaveTriggerCount;
        public int wavePhase;
        public float warningRemainingSeconds;
        public float spawnClockSeconds;
        public float fixedStepAccumulatorSeconds;
        public int spawnedEnemyCount;
        public int defeatedEnemyCount;
        public int nextEnemyOrdinal;
        public string randomState;
        public int coreCurrentHealth;
        public FormalThreeDDefenseTowerSaveData[] towers =
            Array.Empty<FormalThreeDDefenseTowerSaveData>();
        public FormalThreeDDefenseEnemySaveData[] enemies =
            Array.Empty<FormalThreeDDefenseEnemySaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDDefenseTowerSaveData
    {
        public string stableInstanceId;
        public int ammunitionAmount;
        public bool isPlayerPaused;
        public float activeAmmunitionSeconds;
        public float damageRemainder;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseEnemySaveData
    {
        public string stableEnemyId;
        public string archetypeId;
        public int spawnOrder;
        public float positionX;
        public float positionZ;
        public int currentHealth;
        public float movementRemainder;
        public float attackDamageRemainder;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignSaveData
    {
        public string campaignId;
        public int phase;
        public int currentWaveNumber;
        public FormalThreeDDefenseCampaignEnemyCountSaveData[]
            plannedEnemyCountsByEnemyId =
                Array.Empty<FormalThreeDDefenseCampaignEnemyCountSaveData>();
        public FormalThreeDDefenseCampaignEnemyCountSaveData[]
            spawnedEnemyCountsByEnemyId =
                Array.Empty<FormalThreeDDefenseCampaignEnemyCountSaveData>();
        public FormalThreeDDefenseCampaignEnemyCountSaveData[]
            defeatedEnemyCountsByEnemyId =
                Array.Empty<FormalThreeDDefenseCampaignEnemyCountSaveData>();
        public FormalThreeDDefenseCampaignSpawnAnchorSaveData[]
            frozenSpawnAnchors =
                Array.Empty<FormalThreeDDefenseCampaignSpawnAnchorSaveData>();
        public float warningRemainingSeconds;
        public float spawnClockSeconds;
        public float fixedStepAccumulatorSeconds;
        public int nextEnemyOrdinal;
        public int coreCurrentHealth;
        public float requestedSpeed = 1f;
        public float lastNonZeroSpeed = 1f;
        public int result;
        public FormalThreeDDefenseCampaignStatisticsSaveData statistics;
        public FormalThreeDDefenseCampaignTowerCombatStateSaveData[]
            towerCombatStates =
                Array.Empty<
                    FormalThreeDDefenseCampaignTowerCombatStateSaveData>();
        public FormalThreeDDefenseCampaignEnemyStateSaveData[] enemyStates =
            Array.Empty<FormalThreeDDefenseCampaignEnemyStateSaveData>();
        public FormalThreeDDefenseCampaignBuildingHealthStateSaveData[]
            buildingHealthStates =
                Array.Empty<
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignStatisticsSaveData
    {
        public float elapsedRuleSeconds;
        public int spawnedEnemyCount;
        public int defeatedEnemyCount;
        public int completedWaveCount;
        public FormalThreeDDefenseCampaignMetricSaveData[] killsByEnemyId =
            Array.Empty<FormalThreeDDefenseCampaignMetricSaveData>();
        public int highestAliveEnemyCount;
        public int coreDamageTaken;
        public FormalThreeDDefenseCampaignMetricSaveData[]
            buildingLossesByBuildingId =
                Array.Empty<FormalThreeDDefenseCampaignMetricSaveData>();
        public FormalThreeDDefenseCampaignMetricSaveData[]
            damageByTowerBuildingId =
                Array.Empty<FormalThreeDDefenseCampaignMetricSaveData>();
        public FormalThreeDDefenseCampaignMetricSaveData[]
            killsByTowerBuildingId =
                Array.Empty<FormalThreeDDefenseCampaignMetricSaveData>();
        public FormalThreeDDefenseCampaignMetricSaveData[]
            consumablesSpentByResourceId =
                Array.Empty<FormalThreeDDefenseCampaignMetricSaveData>();
        public int completedProductionBatchCount;
        public float productionActiveProgressSeconds;
        public float productionEligibleSeconds;
        public bool cityWasPackedAfterCampaignStart;
        public bool developmentModifierUsed;
        public bool partialFromMigration;
        public int controlledUnitLossCount;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignEnemyCountSaveData
    {
        public string enemyId;
        public int count;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignSpawnAnchorSaveData
    {
        public CampaignSpawnDirection direction;
        public float positionX;
        public float positionZ;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignMetricSaveData
    {
        public string stableId;
        public int amount;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignTowerCombatStateSaveData
    {
        public string stableInstanceId;
        public string consumableId;
        public int amount;
        public bool isPlayerPaused;
        public float activeConsumableSeconds;
        public float damageRemainder;
        public string targetStableEnemyId;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignEnemyStateSaveData
    {
        public string stableEnemyId;
        public string archetypeId;
        public int spawnOrder;
        public float positionX;
        public float positionZ;
        public int currentHealth;
        public float movementRemainder;
        public float attackDamageRemainder;
        public string targetStableId;
        public bool isControlled;
    }

    [Serializable]
    public sealed class FormalThreeDDefenseCampaignBuildingHealthStateSaveData
    {
        public string stableInstanceId;
        public int currentHealth;
        public bool isDestroyed;
    }

    [Serializable]
    public sealed class FormalThreeDEvacuationSaveData
    {
        public long nextBatchOrdinal;
        public string activeBatchId;
        public bool isProcessing;
        public FormalThreeDEvacuationBatchContextSaveData batchContext;
        public FormalThreeDEvacuationWorkSaveData[] work =
            Array.Empty<FormalThreeDEvacuationWorkSaveData>();
        public string[] fullQueueStableInstanceIds = Array.Empty<string>();
        public int currentQueueIndex;
        public string currentStableInstanceId;
        public float remainingSeconds;
        public bool isBlocked;
        public string blockedCode;
        public string blockedStableInstanceId;
        public FormalThreeDEvacuationRuntimePayloadSaveData[] runtimePayloads =
            Array.Empty<FormalThreeDEvacuationRuntimePayloadSaveData>();
        public string[] lockedStableInstanceIds = Array.Empty<string>();
        public string[] pendingRollbackStableInstanceIds =
            Array.Empty<string>();
    }

    [Serializable]
    public sealed class FormalThreeDEvacuationBatchContextSaveData
    {
        public bool isInCombat;
        public float productivityMultiplier;
    }

    [Serializable]
    public sealed class FormalThreeDEvacuationWorkSaveData
    {
        public string stableInstanceId;
        public int treatment;
        public double remainingRatio;
        public float baseDismantleSeconds;
        public float dismantleSeconds;
        public int refund;
    }

    [Serializable]
    public sealed class FormalThreeDEvacuationRuntimePayloadSaveData
    {
        public string stableInstanceId;
        public FormalThreeDResourceAmountSaveData[] productionInputAmounts =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
        public FormalThreeDResourceAmountSaveData[] productionReservedInputs =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
        public FormalThreeDResourceAmountSaveData[] productionOutputAmounts =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
        public bool hasDefensePayload;
        public int towerAmmunitionAmount;
        public FormalThreeDResourceAmountSaveData[] resourcePayload =
            Array.Empty<FormalThreeDResourceAmountSaveData>();
    }

    [Serializable]
    public sealed class FormalThreeDPauseSaveData
    {
        public bool tacticalPaused;
    }
}
