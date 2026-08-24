using System;
using WasteCity.Combat;

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
        public FormalThreeDProductionSaveData production;
        public FormalThreeDDefenseSaveData defense;
        public FormalThreeDDefenseCampaignSaveData defenseCampaign;
        public FormalThreeDEvacuationSaveData evacuation;
        public FormalThreeDPauseSaveData pause;
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
