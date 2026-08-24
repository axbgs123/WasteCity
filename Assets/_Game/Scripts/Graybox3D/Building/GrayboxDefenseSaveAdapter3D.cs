using System;
using System.Collections.Generic;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxDefenseSaveAdapter3D
    {
        public const string DefenseConfigurationSignature =
            "builtin:first-defense@1";

        private readonly GrayboxDefenseRuntime3D runtime;

        public GrayboxDefenseSaveAdapter3D(GrayboxDefenseRuntime3D runtime)
        {
            this.runtime = runtime ??
                throw new ArgumentNullException(nameof(runtime));
        }

        public FormalThreeDDefenseSaveData Capture()
        {
            GrayboxDefensePersistenceState3D source =
                runtime.CaptureForPersistence();
            TutorialDefensePersistenceState tutorial = source.Tutorial;
            var towers = new FormalThreeDDefenseTowerSaveData[
                source.Towers.Count];
            for (var index = 0; index < source.Towers.Count; index++)
            {
                MachineGunTurretPersistenceState tower =
                    source.Towers[index];
                towers[index] = new FormalThreeDDefenseTowerSaveData
                {
                    stableInstanceId = tower.StableId,
                    ammunitionAmount = tower.AmmunitionAmount,
                    isPlayerPaused = tower.IsPlayerPaused,
                    activeAmmunitionSeconds =
                        tower.ActiveAmmunitionSeconds,
                    damageRemainder = tower.DamageRemainder,
                };
            }
            var enemies = new FormalThreeDDefenseEnemySaveData[
                tutorial.Enemies.Count];
            for (var index = 0; index < tutorial.Enemies.Count; index++)
            {
                DefenseEnemyPersistenceState enemy = tutorial.Enemies[index];
                enemies[index] = new FormalThreeDDefenseEnemySaveData
                {
                    stableEnemyId = enemy.StableId,
                    archetypeId = enemy.ArchetypeId,
                    spawnOrder = enemy.SpawnOrder,
                    positionX = enemy.X,
                    positionZ = enemy.Z,
                    currentHealth = enemy.CurrentHealth,
                    movementRemainder = enemy.MovementRemainder,
                    attackDamageRemainder = enemy.AttackDamageRemainder,
                };
            }
            return new FormalThreeDDefenseSaveData
            {
                configurationSignature = DefenseConfigurationSignature,
                spawnOriginX = tutorial.SpawnOriginX,
                spawnOriginZ = tutorial.SpawnOriginZ,
                tutorialTriggered = tutorial.TutorialTriggered,
                tutorialWaveTriggerCount =
                    source.TutorialWaveTriggerCount,
                wavePhase = (int)tutorial.WavePhase,
                warningRemainingSeconds =
                    tutorial.WarningRemainingSeconds,
                spawnClockSeconds = tutorial.SpawnClockSeconds,
                fixedStepAccumulatorSeconds =
                    source.FixedStepAccumulatorSeconds,
                spawnedEnemyCount = tutorial.SpawnedEnemyCount,
                defeatedEnemyCount = tutorial.DefeatedEnemyCount,
                nextEnemyOrdinal = tutorial.NextEnemyOrdinal,
                randomState = source.RandomState,
                coreCurrentHealth = tutorial.CoreCurrentHealth,
                towers = towers,
                enemies = enemies,
            };
        }

        public FormalThreeDDefenseCampaignSaveData CaptureCampaign()
        {
            GrayboxFormalDefenseCampaignPersistenceState3D source =
                runtime.CaptureFormalCampaignForPersistence();
            SingleCityDefenseCampaignPersistenceState campaign =
                source.Campaign;
            SingleCityDefenseCampaignStatisticsPersistenceState statistics =
                campaign.Statistics;

            return new FormalThreeDDefenseCampaignSaveData
            {
                campaignId = campaign.CampaignId,
                phase = (int)campaign.Phase,
                currentWaveNumber = campaign.CurrentWaveNumber,
                plannedEnemyCountsByEnemyId = Counts(
                    campaign.PlannedEnemyCountsByEnemyId),
                spawnedEnemyCountsByEnemyId = Counts(
                    campaign.SpawnedEnemyCountsByEnemyId),
                defeatedEnemyCountsByEnemyId = Counts(
                    campaign.DefeatedEnemyCountsByEnemyId),
                frozenSpawnAnchors = Anchors(campaign.FrozenSpawnAnchors),
                warningRemainingSeconds = campaign.WarningRemainingSeconds,
                spawnClockSeconds = campaign.SpawnClockSeconds,
                fixedStepAccumulatorSeconds =
                    campaign.FixedStepAccumulatorSeconds,
                nextEnemyOrdinal = campaign.NextEnemyOrdinal,
                coreCurrentHealth = campaign.CoreCurrentHealth,
                requestedSpeed = 1f,
                lastNonZeroSpeed = 1f,
                result = (int)campaign.Result,
                statistics = Statistics(statistics),
                towerCombatStates = Towers(source.Towers),
                enemyStates = Enemies(campaign.Enemies),
                buildingHealthStates = Health(source.BuildingHealth),
            };
        }

        public bool TryRestoreCampaign(
            FormalThreeDDefenseCampaignSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out string error)
        {
            if (!TryPrepareCampaignRestore(
                    data,
                    instances,
                    out GrayboxFormalDefenseCampaignRestorePlan3D plan,
                    out error))
            {
                return false;
            }
            return TryCommitCampaignRestore(plan, out error);
        }

        public bool TryPrepareCampaignRestore(
            FormalThreeDDefenseCampaignSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out GrayboxFormalDefenseCampaignRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (data?.plannedEnemyCountsByEnemyId == null ||
                data.spawnedEnemyCountsByEnemyId == null ||
                data.defeatedEnemyCountsByEnemyId == null ||
                data.frozenSpawnAnchors == null || data.statistics == null ||
                data.statistics.killsByEnemyId == null ||
                data.statistics.buildingLossesByBuildingId == null ||
                data.statistics.damageByTowerBuildingId == null ||
                data.statistics.consumablesSpentByResourceId == null ||
                data.towerCombatStates == null || data.enemyStates == null ||
                data.buildingHealthStates == null || instances == null)
            {
                error = "正式防御战役存档状态不完整";
                return false;
            }

            GrayboxFormalDefenseCampaignPersistenceState3D current;
            try
            {
                current = runtime.CaptureFormalCampaignForPersistence();
            }
            catch (InvalidOperationException exception)
            {
                error = exception.Message;
                return false;
            }
            var currentTowerById = new Dictionary<
                string,
                SingleCityDefenseTowerPersistenceState>(StringComparer.Ordinal);
            for (var index = 0; index < current.Towers.Count; index++)
            {
                SingleCityDefenseTowerPersistenceState tower =
                    current.Towers[index];
                currentTowerById[tower.StableInstanceId] = tower;
            }

            var towers = new SingleCityDefenseTowerPersistenceState[
                data.towerCombatStates.Length];
            for (var index = 0; index < towers.Length; index++)
            {
                FormalThreeDDefenseCampaignTowerCombatStateSaveData saved =
                    data.towerCombatStates[index];
                if (saved == null ||
                    !currentTowerById.TryGetValue(
                        saved.stableInstanceId,
                        out SingleCityDefenseTowerPersistenceState topology) ||
                    !string.Equals(
                        saved.consumableId,
                        DefenseTowerCatalog.For(topology.BuildingId)
                            ?.ConsumableId,
                        StringComparison.Ordinal))
                {
                    error = "正式防御塔存档引用未知实例或耗材";
                    return false;
                }
                towers[index] = new SingleCityDefenseTowerPersistenceState(
                    saved.stableInstanceId,
                    topology.BuildingId,
                    topology.X,
                    topology.Z,
                    saved.amount,
                    saved.activeConsumableSeconds,
                    saved.damageRemainder,
                    string.IsNullOrEmpty(saved.targetStableEnemyId)
                        ? null
                        : saved.targetStableEnemyId,
                    topology.IsLogisticsConnected,
                    saved.isPlayerPaused);
            }

            var aggregate = new
                GrayboxFormalDefenseCampaignPersistenceState3D(
                    Campaign(data),
                    towers,
                    Health(data.buildingHealthStates));
            return runtime.TryPrepareFormalCampaignRestore(
                aggregate,
                instances,
                out plan,
                out error);
        }

        public bool TryCommitCampaignRestore(
            GrayboxFormalDefenseCampaignRestorePlan3D plan,
            out string error)
        {
            return runtime.TryCommitFormalCampaignRestore(plan, out error);
        }

        public bool TryRestore(
            FormalThreeDDefenseSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out string error)
        {
            if (!TryPrepareRestore(
                    data,
                    instances,
                    out GrayboxDefenseRestorePlan3D plan,
                    out error))
            {
                return false;
            }
            return TryCommitRestore(plan, out error);
        }

        public bool TryPrepareRestore(
            FormalThreeDDefenseSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out GrayboxDefenseRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (data?.towers == null || data.enemies == null)
            {
                error = "防御存档或状态数组不能为空";
                return false;
            }
            if (!string.Equals(
                    data.configurationSignature,
                    DefenseConfigurationSignature,
                    StringComparison.Ordinal))
            {
                error = "防御配置签名不兼容";
                return false;
            }

            var towers = new MachineGunTurretPersistenceState[
                data.towers.Length];
            for (var index = 0; index < data.towers.Length; index++)
            {
                FormalThreeDDefenseTowerSaveData tower = data.towers[index];
                if (tower == null)
                {
                    error = "机枪塔存档状态不能为空";
                    return false;
                }
                towers[index] = new MachineGunTurretPersistenceState(
                    tower.stableInstanceId,
                    tower.ammunitionAmount,
                    tower.isPlayerPaused,
                    tower.activeAmmunitionSeconds,
                    tower.damageRemainder);
            }

            var enemies = new DefenseEnemyPersistenceState[
                data.enemies.Length];
            for (var index = 0; index < data.enemies.Length; index++)
            {
                FormalThreeDDefenseEnemySaveData enemy = data.enemies[index];
                if (enemy == null)
                {
                    error = "敌人存档状态不能为空";
                    return false;
                }
                enemies[index] = new DefenseEnemyPersistenceState(
                    enemy.stableEnemyId,
                    enemy.archetypeId,
                    enemy.spawnOrder,
                    enemy.positionX,
                    enemy.positionZ,
                    enemy.currentHealth,
                    enemy.movementRemainder,
                    enemy.attackDamageRemainder);
            }

            var tutorial = new TutorialDefensePersistenceState(
                data.tutorialTriggered,
                (WavePhase)data.wavePhase,
                data.warningRemainingSeconds,
                data.spawnClockSeconds,
                data.spawnedEnemyCount,
                data.defeatedEnemyCount,
                data.nextEnemyOrdinal,
                0f,
                data.spawnOriginX,
                data.spawnOriginZ,
                data.coreCurrentHealth,
                enemies);
            var snapshot = new GrayboxDefensePersistenceState3D(
                data.tutorialWaveTriggerCount,
                data.fixedStepAccumulatorSeconds,
                data.randomState,
                tutorial,
                towers);
            return runtime.TryPrepareRestore(
                snapshot,
                instances,
                out plan,
                out error);
        }

        public bool TryCommitRestore(
            GrayboxDefenseRestorePlan3D plan,
            out string error)
        {
            return runtime.TryCommitRestore(plan, out error);
        }

        private static FormalThreeDDefenseCampaignEnemyCountSaveData[] Counts(
            IReadOnlyList<
                SingleCityDefenseCampaignEnemyCountPersistenceState> source)
        {
            var result = new FormalThreeDDefenseCampaignEnemyCountSaveData[
                source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = new
                    FormalThreeDDefenseCampaignEnemyCountSaveData
                    {
                        enemyId = source[index].EnemyDefinitionId,
                        count = source[index].Count,
                    };
            }
            return result;
        }

        private static FormalThreeDDefenseCampaignSpawnAnchorSaveData[] Anchors(
            IReadOnlyList<
                SingleCityDefenseCampaignSpawnAnchorPersistenceState> source)
        {
            var result = new FormalThreeDDefenseCampaignSpawnAnchorSaveData[
                source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = new
                    FormalThreeDDefenseCampaignSpawnAnchorSaveData
                    {
                        direction = source[index].Direction,
                        positionX = source[index].X,
                        positionZ = source[index].Z,
                    };
            }
            return result;
        }

        private static FormalThreeDDefenseCampaignTowerCombatStateSaveData[]
            Towers(
                IReadOnlyList<SingleCityDefenseTowerPersistenceState> source)
        {
            var result =
                new FormalThreeDDefenseCampaignTowerCombatStateSaveData[
                    source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                SingleCityDefenseTowerPersistenceState tower = source[index];
                result[index] = new
                    FormalThreeDDefenseCampaignTowerCombatStateSaveData
                    {
                        stableInstanceId = tower.StableInstanceId,
                        consumableId = DefenseTowerCatalog.For(tower.BuildingId)
                            ?.ConsumableId,
                        amount = tower.LocalConsumableAmount,
                        isPlayerPaused = tower.IsPlayerPaused,
                        activeConsumableSeconds =
                            tower.ActiveConsumableSeconds,
                        damageRemainder = tower.DamageRemainder,
                        targetStableEnemyId = tower.TargetStableEnemyId,
                    };
            }
            return result;
        }

        private static FormalThreeDDefenseCampaignEnemyStateSaveData[] Enemies(
            IReadOnlyList<SingleCityDefenseCampaignEnemyPersistenceState>
                source)
        {
            var result = new FormalThreeDDefenseCampaignEnemyStateSaveData[
                source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                SingleCityDefenseCampaignEnemyPersistenceState enemy =
                    source[index];
                result[index] = new FormalThreeDDefenseCampaignEnemyStateSaveData
                {
                    stableEnemyId = enemy.StableId,
                    archetypeId = enemy.EnemyDefinitionId,
                    spawnOrder = enemy.SpawnOrder,
                    positionX = enemy.X,
                    positionZ = enemy.Z,
                    currentHealth = enemy.CurrentHealth,
                    movementRemainder = enemy.MovementRemainder,
                    attackDamageRemainder = enemy.AttackDamageRemainder,
                    targetStableId = enemy.TargetStableId,
                };
            }
            return result;
        }

        private static FormalThreeDDefenseCampaignStatisticsSaveData Statistics(
            SingleCityDefenseCampaignStatisticsPersistenceState source)
        {
            if (source == null) return null;
            return new FormalThreeDDefenseCampaignStatisticsSaveData
            {
                elapsedRuleSeconds = source.ElapsedRuleSeconds,
                spawnedEnemyCount = source.SpawnedEnemyCount,
                defeatedEnemyCount = source.DefeatedEnemyCount,
                completedWaveCount = source.CompletedWaveCount,
                killsByEnemyId = Metrics(source.KillsByEnemyId),
                highestAliveEnemyCount = source.HighestAliveEnemyCount,
                coreDamageTaken = source.CoreDamageTaken,
                buildingLossesByBuildingId = Metrics(
                    source.BuildingLossesByBuildingId),
                damageByTowerBuildingId = Metrics(
                    source.DamageByTowerBuildingId),
                consumablesSpentByResourceId = Metrics(
                    source.ConsumablesSpentByResourceId),
                partialFromMigration = source.PartialFromMigration,
            };
        }

        private static FormalThreeDDefenseCampaignMetricSaveData[] Metrics(
            IReadOnlyList<
                SingleCityDefenseCampaignMetricPersistenceState> source)
        {
            var result = new FormalThreeDDefenseCampaignMetricSaveData[
                source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                result[index] = new FormalThreeDDefenseCampaignMetricSaveData
                {
                    stableId = source[index].StableId,
                    amount = source[index].Amount,
                };
            }
            return result;
        }

        private static
            FormalThreeDDefenseCampaignBuildingHealthStateSaveData[] Health(
                IReadOnlyList<
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData>
                    source)
        {
            var result =
                new FormalThreeDDefenseCampaignBuildingHealthStateSaveData[
                    source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                FormalThreeDDefenseCampaignBuildingHealthStateSaveData item =
                    source[index];
                result[index] = new
                    FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                    {
                        stableInstanceId = item.stableInstanceId,
                        currentHealth = item.currentHealth,
                        isDestroyed = item.isDestroyed,
                    };
            }
            return result;
        }

        private static SingleCityDefenseCampaignPersistenceState Campaign(
            FormalThreeDDefenseCampaignSaveData source)
        {
            FormalThreeDDefenseCampaignStatisticsSaveData statistics =
                source.statistics;
            var campaignStatistics = new
                SingleCityDefenseCampaignStatisticsPersistenceState(
                    statistics.elapsedRuleSeconds,
                    statistics.spawnedEnemyCount,
                    statistics.defeatedEnemyCount,
                    statistics.completedWaveCount,
                    Metrics(statistics.killsByEnemyId),
                    statistics.highestAliveEnemyCount,
                    statistics.coreDamageTaken,
                    Metrics(statistics.damageByTowerBuildingId),
                    Array.Empty<
                        SingleCityDefenseCampaignMetricPersistenceState>(),
                    Metrics(statistics.consumablesSpentByResourceId),
                    SumMetrics(statistics.buildingLossesByBuildingId),
                    Metrics(statistics.buildingLossesByBuildingId),
                    statistics.partialFromMigration);
            return new SingleCityDefenseCampaignPersistenceState(
                source.campaignId,
                (SingleCityDefenseCampaignPhase)source.phase,
                source.currentWaveNumber,
                source.warningRemainingSeconds,
                source.spawnClockSeconds,
                source.fixedStepAccumulatorSeconds,
                source.nextEnemyOrdinal,
                source.coreCurrentHealth,
                (SingleCityDefenseCampaignResult)source.result,
                Counts(source.plannedEnemyCountsByEnemyId),
                Counts(source.spawnedEnemyCountsByEnemyId),
                Counts(source.defeatedEnemyCountsByEnemyId),
                Anchors(source.frozenSpawnAnchors),
                Enemies(source.enemyStates),
                campaignStatistics);
        }

        private static SingleCityDefenseCampaignEnemyCountPersistenceState[]
            Counts(FormalThreeDDefenseCampaignEnemyCountSaveData[] source)
        {
            var result =
                new SingleCityDefenseCampaignEnemyCountPersistenceState[
                    source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = source[index] == null
                    ? null
                    : new
                        SingleCityDefenseCampaignEnemyCountPersistenceState(
                            source[index].enemyId,
                            source[index].count);
            }
            return result;
        }

        private static SingleCityDefenseCampaignSpawnAnchorPersistenceState[]
            Anchors(FormalThreeDDefenseCampaignSpawnAnchorSaveData[] source)
        {
            var result =
                new SingleCityDefenseCampaignSpawnAnchorPersistenceState[
                    source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = source[index] == null
                    ? null
                    : new
                        SingleCityDefenseCampaignSpawnAnchorPersistenceState(
                            source[index].direction,
                            source[index].positionX,
                            source[index].positionZ);
            }
            return result;
        }

        private static SingleCityDefenseCampaignEnemyPersistenceState[]
            Enemies(FormalThreeDDefenseCampaignEnemyStateSaveData[] source)
        {
            var result = new SingleCityDefenseCampaignEnemyPersistenceState[
                source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                FormalThreeDDefenseCampaignEnemyStateSaveData enemy =
                    source[index];
                result[index] = enemy == null
                    ? null
                    : new SingleCityDefenseCampaignEnemyPersistenceState(
                        enemy.stableEnemyId,
                        enemy.archetypeId,
                        enemy.spawnOrder,
                        enemy.positionX,
                        enemy.positionZ,
                        enemy.currentHealth,
                        enemy.movementRemainder,
                        enemy.attackDamageRemainder,
                        enemy.targetStableId);
            }
            return result;
        }

        private static SingleCityDefenseCampaignMetricPersistenceState[]
            Metrics(FormalThreeDDefenseCampaignMetricSaveData[] source)
        {
            var result =
                new SingleCityDefenseCampaignMetricPersistenceState[
                    source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                result[index] = source[index] == null
                    ? null
                    : new SingleCityDefenseCampaignMetricPersistenceState(
                        source[index].stableId,
                        source[index].amount);
            }
            return result;
        }

        private static int SumMetrics(
            FormalThreeDDefenseCampaignMetricSaveData[] source)
        {
            var total = 0;
            for (var index = 0; index < source.Length; index++)
            {
                if (source[index] != null)
                    total += source[index].amount;
            }
            return total;
        }
    }
}
