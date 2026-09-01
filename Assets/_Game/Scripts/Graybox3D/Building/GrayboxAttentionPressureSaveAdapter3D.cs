using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Persistence.ThreeD;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxAttentionPressureRestorePlan3D
    {
        internal GrayboxAttentionPressureRestorePlan3D(
            GrayboxAttentionPressureSaveAdapter3D owner,
            AttentionPressureSnapshot expectedPressure,
            string expectedDefenseFingerprint,
            AttentionPressureSnapshot targetPressure,
            string targetEncounterId,
            SingleCityDefenseCampaignPersistenceState targetCampaign)
        {
            Owner = owner;
            ExpectedPressure = expectedPressure;
            ExpectedDefenseFingerprint = expectedDefenseFingerprint;
            TargetPressure = targetPressure;
            TargetEncounterId = targetEncounterId;
            TargetCampaign = targetCampaign;
        }

        internal GrayboxAttentionPressureSaveAdapter3D Owner { get; }
        internal AttentionPressureSnapshot ExpectedPressure { get; }
        internal string ExpectedDefenseFingerprint { get; }
        internal AttentionPressureSnapshot TargetPressure { get; }
        internal string TargetEncounterId { get; }
        internal SingleCityDefenseCampaignPersistenceState TargetCampaign
            { get; }
        internal bool Consumed { get; set; }
    }

    public sealed class GrayboxAttentionPressureSaveAdapter3D
    {
        private readonly AttentionPressureRuntime pressure;
        private readonly GrayboxDefenseRuntime3D defense;

        public GrayboxAttentionPressureSaveAdapter3D(
            AttentionPressureRuntime pressure,
            GrayboxDefenseRuntime3D defense)
        {
            this.pressure = pressure ??
                throw new ArgumentNullException(nameof(pressure));
            this.defense = defense ??
                throw new ArgumentNullException(nameof(defense));
        }

        public FormalThreeDAttentionPressureSaveData Capture()
        {
            AttentionPressureSnapshot snapshot = pressure.Capture();
            var entries = new FormalThreeDAttentionPressureEntrySaveData[
                snapshot.Entries.Count];
            for (var index = 0; index < entries.Length; index++)
            {
                AttentionPressureEntrySnapshot entry = snapshot.Entries[index];
                entries[index] = new FormalThreeDAttentionPressureEntrySaveData
                {
                    threshold = entry.Threshold,
                    state = (int)entry.State,
                    warningRemainingSeconds = entry.WarningRemainingSeconds,
                };
            }
            SingleCityDefenseCampaignPersistenceState campaign =
                defense.CaptureActivePressurePersistence();
            return new FormalThreeDAttentionPressureSaveData
            {
                revision = snapshot.Revision,
                entries = entries,
                activeEncounterId = defense.ActivePressureEncounterId,
                activeCampaign = Campaign(campaign),
            };
        }

        public bool TryPrepareRestore(
            FormalThreeDAttentionPressureSaveData data,
            out GrayboxAttentionPressureRestorePlan3D plan,
            out string error)
        {
            plan = null;
            if (data?.entries == null)
            {
                error = "压力存档或条目数组不能为空";
                return false;
            }
            var entries = new AttentionPressureEntrySnapshot[
                data.entries.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                FormalThreeDAttentionPressureEntrySaveData item =
                    data.entries[index];
                if (item == null || !Enum.IsDefined(
                        typeof(AttentionPressureState), item.state))
                {
                    error = "压力条目状态无效";
                    return false;
                }
                entries[index] = new AttentionPressureEntrySnapshot(
                    item.threshold,
                    (AttentionPressureState)item.state,
                    item.warningRemainingSeconds);
            }
            var pressureCandidate = new AttentionPressureSnapshot(
                data.revision, entries);
            var pressureValidator = new AttentionPressureRuntime();
            if (!pressureValidator.TryRestore(pressureCandidate, out error))
                return false;

            AttentionPressureEntrySnapshot active = null;
            for (var index = 0;
                 index < pressureCandidate.Entries.Count;
                 index++)
            {
                if (pressureCandidate.Entries[index].State ==
                    AttentionPressureState.Active)
                    active = pressureCandidate.Entries[index];
            }
            bool hasCampaign = data.activeCampaign != null &&
                !string.IsNullOrEmpty(data.activeCampaign.campaignId);
            if ((active != null) != hasCampaign ||
                hasCampaign && (string.IsNullOrWhiteSpace(
                    data.activeEncounterId) ||
                    !string.Equals(active.EncounterId,
                        data.activeEncounterId, StringComparison.Ordinal) ||
                    !string.Equals(data.activeCampaign.campaignId,
                        data.activeEncounterId, StringComparison.Ordinal)))
            {
                error = "活动压力条目与活动战役身份不一致";
                return false;
            }
            if (!hasCampaign && !string.IsNullOrEmpty(data.activeEncounterId))
            {
                error = "无活动战役时遭遇 ID 必须为空";
                return false;
            }

            SingleCityDefenseCampaignPersistenceState targetCampaign = null;
            if (hasCampaign)
            {
                SingleCityDefenseCampaignDefinition definition =
                    AttentionPressureCampaignCatalog.Find(
                        data.activeEncounterId);
                if (definition == null ||
                    !TryCampaign(data.activeCampaign, out targetCampaign,
                        out error))
                    return false;
                var validator = new SingleCityDefenseCampaignModel(
                    0f, 0f, definition);
                if (!validator.TryPrepareRestore(targetCampaign,
                        out SingleCityDefenseCampaignRestorePlan campaignPlan,
                        out error) ||
                    !validator.TryCommitRestore(campaignPlan, out error))
                    return false;
            }

            plan = new GrayboxAttentionPressureRestorePlan3D(
                this,
                pressure.Capture(),
                DefenseFingerprint(),
                pressureValidator.Capture(),
                data.activeEncounterId ?? string.Empty,
                targetCampaign);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            GrayboxAttentionPressureRestorePlan3D plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.Consumed)
            {
                error = "压力恢复计划无效或已经提交";
                return false;
            }
            if (!ReferenceEquals(pressure.Capture(), plan.ExpectedPressure) ||
                !string.Equals(DefenseFingerprint(),
                    plan.ExpectedDefenseFingerprint,
                    StringComparison.Ordinal))
            {
                error = "压力恢复计划已过期";
                return false;
            }

            AttentionPressureSnapshot previousPressure = pressure.Capture();
            string previousId = defense.ActivePressureEncounterId;
            SingleCityDefenseCampaignPersistenceState previousCampaign =
                defense.CaptureActivePressurePersistence();
            if (!pressure.TryRestore(plan.TargetPressure, out error))
                return false;
            if (defense.HasActivePressureCampaign)
                defense.ClearActivePressure();
            if (plan.TargetCampaign != null)
            {
                SingleCityDefenseCampaignDefinition definition =
                    AttentionPressureCampaignCatalog.Find(
                        plan.TargetEncounterId);
                if (!defense.TryRestoreActivePressure(
                        definition, plan.TargetCampaign, out error))
                {
                    Rollback(previousPressure, previousId, previousCampaign);
                    return false;
                }
            }
            plan.Consumed = true;
            error = string.Empty;
            return true;
        }

        private void Rollback(
            AttentionPressureSnapshot previousPressure,
            string previousId,
            SingleCityDefenseCampaignPersistenceState previousCampaign)
        {
            pressure.TryRestore(previousPressure, out _);
            if (defense.HasActivePressureCampaign)
                defense.ClearActivePressure();
            if (previousCampaign != null)
                defense.TryRestoreActivePressure(
                    AttentionPressureCampaignCatalog.Find(previousId),
                    previousCampaign,
                    out _);
        }

        private string DefenseFingerprint()
        {
            return defense.ActivePressureEncounterId + "|" +
                JsonUtility.ToJson(Campaign(
                    defense.CaptureActivePressurePersistence()));
        }

        private static FormalThreeDPressureCampaignSaveData Campaign(
            SingleCityDefenseCampaignPersistenceState source)
        {
            if (source == null) return null;
            return new FormalThreeDPressureCampaignSaveData
            {
                campaignId = source.CampaignId,
                phase = (int)source.Phase,
                currentWaveNumber = source.CurrentWaveNumber,
                plannedEnemyCountsByEnemyId = Counts(
                    source.PlannedEnemyCountsByEnemyId),
                spawnedEnemyCountsByEnemyId = Counts(
                    source.SpawnedEnemyCountsByEnemyId),
                defeatedEnemyCountsByEnemyId = Counts(
                    source.DefeatedEnemyCountsByEnemyId),
                frozenSpawnAnchors = Anchors(source.FrozenSpawnAnchors),
                warningRemainingSeconds = source.WarningRemainingSeconds,
                spawnClockSeconds = source.SpawnClockSeconds,
                fixedStepAccumulatorSeconds =
                    source.FixedStepAccumulatorSeconds,
                nextEnemyOrdinal = source.NextEnemyOrdinal,
                coreCurrentHealth = source.CoreCurrentHealth,
                result = (int)source.Result,
                statistics = Statistics(source.Statistics),
                enemyStates = Enemies(source.Enemies),
                injectedReinforcements = Injected(
                    source.InjectedReinforcements),
            };
        }

        private static bool TryCampaign(
            FormalThreeDPressureCampaignSaveData source,
            out SingleCityDefenseCampaignPersistenceState campaign,
            out string error)
        {
            campaign = null;
            if (source.plannedEnemyCountsByEnemyId == null ||
                source.spawnedEnemyCountsByEnemyId == null ||
                source.defeatedEnemyCountsByEnemyId == null ||
                source.frozenSpawnAnchors == null ||
                source.enemyStates == null || source.statistics == null ||
                source.injectedReinforcements == null ||
                source.statistics.killsByEnemyId == null ||
                source.statistics.buildingLossesByBuildingId == null ||
                source.statistics.damageByTowerBuildingId == null ||
                source.statistics.killsByTowerBuildingId == null ||
                source.statistics.consumablesSpentByResourceId == null)
            {
                error = "活动压力战役字段或数组不完整";
                return false;
            }
            var injected = new
                SingleCityDefenseInjectedReinforcementPersistenceState[
                    source.injectedReinforcements.Length];
            for (var index = 0; index < injected.Length; index++)
            {
                FormalThreeDPressureInjectedReinforcementSaveData item =
                    source.injectedReinforcements[index];
                if (item?.entries == null)
                {
                    error = "压力增援记录无效";
                    return false;
                }
                var wave = new WaveEntry[item.entries.Length];
                for (var waveIndex = 0; waveIndex < wave.Length; waveIndex++)
                {
                    FormalThreeDDefenseCampaignEnemyCountSaveData entry =
                        item.entries[waveIndex];
                    EnemyDefinition enemy = entry == null
                        ? null
                        : EnemyById(entry.enemyId);
                    if (enemy == null)
                    {
                        error = "压力增援敌人身份无效";
                        return false;
                    }
                    wave[waveIndex] = new WaveEntry(
                        enemy.Archetype, entry.count);
                }
                injected[index] = new
                    SingleCityDefenseInjectedReinforcementPersistenceState(
                        item.stableEventId, wave);
            }
            FormalThreeDDefenseCampaignStatisticsSaveData stats =
                source.statistics;
            campaign = new SingleCityDefenseCampaignPersistenceState(
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
                new SingleCityDefenseCampaignStatisticsPersistenceState(
                    stats.elapsedRuleSeconds, stats.spawnedEnemyCount,
                    stats.defeatedEnemyCount, stats.completedWaveCount,
                    Metrics(stats.killsByEnemyId),
                    stats.highestAliveEnemyCount, stats.coreDamageTaken,
                    Metrics(stats.damageByTowerBuildingId),
                    Metrics(stats.killsByTowerBuildingId),
                    Metrics(stats.consumablesSpentByResourceId),
                    Sum(stats.buildingLossesByBuildingId),
                    Metrics(stats.buildingLossesByBuildingId),
                    stats.partialFromMigration,
                    stats.completedProductionBatchCount,
                    stats.productionActiveProgressSeconds,
                    stats.productionEligibleSeconds,
                    stats.cityWasPackedAfterCampaignStart,
                    stats.developmentModifierUsed,
                    stats.controlledUnitLossCount),
                injected);
            error = string.Empty;
            return true;
        }

        private static FormalThreeDDefenseCampaignEnemyCountSaveData[] Counts(
            IReadOnlyList<SingleCityDefenseCampaignEnemyCountPersistenceState>
                source)
        {
            var result = new FormalThreeDDefenseCampaignEnemyCountSaveData[
                source.Count];
            for (var i = 0; i < result.Length; i++) result[i] = new
                FormalThreeDDefenseCampaignEnemyCountSaveData
                { enemyId = source[i].EnemyDefinitionId, count = source[i].Count };
            return result;
        }

        private static SingleCityDefenseCampaignEnemyCountPersistenceState[]
            Counts(FormalThreeDDefenseCampaignEnemyCountSaveData[] source)
        {
            var result = new SingleCityDefenseCampaignEnemyCountPersistenceState[
                source.Length];
            for (var i = 0; i < result.Length; i++) result[i] = source[i] == null
                ? null : new SingleCityDefenseCampaignEnemyCountPersistenceState(
                    source[i].enemyId, source[i].count);
            return result;
        }

        private static FormalThreeDDefenseCampaignSpawnAnchorSaveData[] Anchors(
            IReadOnlyList<SingleCityDefenseCampaignSpawnAnchorPersistenceState>
                source)
        {
            var result = new FormalThreeDDefenseCampaignSpawnAnchorSaveData[
                source.Count];
            for (var i = 0; i < result.Length; i++) result[i] = new
                FormalThreeDDefenseCampaignSpawnAnchorSaveData
                { direction = source[i].Direction, positionX = source[i].X,
                    positionZ = source[i].Z };
            return result;
        }

        private static SingleCityDefenseCampaignSpawnAnchorPersistenceState[]
            Anchors(FormalThreeDDefenseCampaignSpawnAnchorSaveData[] source)
        {
            var result = new SingleCityDefenseCampaignSpawnAnchorPersistenceState[
                source.Length];
            for (var i = 0; i < result.Length; i++) result[i] = source[i] == null
                ? null : new SingleCityDefenseCampaignSpawnAnchorPersistenceState(
                    source[i].direction, source[i].positionX,
                    source[i].positionZ);
            return result;
        }

        private static FormalThreeDDefenseCampaignEnemyStateSaveData[] Enemies(
            IReadOnlyList<SingleCityDefenseCampaignEnemyPersistenceState> source)
        {
            var result = new FormalThreeDDefenseCampaignEnemyStateSaveData[
                source.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var item = source[i];
                result[i] = new FormalThreeDDefenseCampaignEnemyStateSaveData
                { stableEnemyId = item.StableId, archetypeId = item.EnemyDefinitionId,
                    spawnOrder = item.SpawnOrder, positionX = item.X,
                    positionZ = item.Z, currentHealth = item.CurrentHealth,
                    movementRemainder = item.MovementRemainder,
                    attackDamageRemainder = item.AttackDamageRemainder,
                    targetStableId = item.TargetStableId,
                    isControlled = item.IsControlled };
            }
            return result;
        }

        private static SingleCityDefenseCampaignEnemyPersistenceState[] Enemies(
            FormalThreeDDefenseCampaignEnemyStateSaveData[] source)
        {
            var result = new SingleCityDefenseCampaignEnemyPersistenceState[
                source.Length];
            for (var i = 0; i < result.Length; i++)
            {
                var item = source[i];
                result[i] = item == null ? null : new
                    SingleCityDefenseCampaignEnemyPersistenceState(
                        item.stableEnemyId, item.archetypeId, item.spawnOrder,
                        item.positionX, item.positionZ, item.currentHealth,
                        item.movementRemainder, item.attackDamageRemainder,
                        item.targetStableId, item.isControlled);
            }
            return result;
        }

        private static FormalThreeDDefenseCampaignStatisticsSaveData Statistics(
            SingleCityDefenseCampaignStatisticsPersistenceState source)
        {
            if (source == null) return null;
            return new FormalThreeDDefenseCampaignStatisticsSaveData
            { elapsedRuleSeconds = source.ElapsedRuleSeconds,
                spawnedEnemyCount = source.SpawnedEnemyCount,
                defeatedEnemyCount = source.DefeatedEnemyCount,
                completedWaveCount = source.CompletedWaveCount,
                killsByEnemyId = Metrics(source.KillsByEnemyId),
                highestAliveEnemyCount = source.HighestAliveEnemyCount,
                coreDamageTaken = source.CoreDamageTaken,
                damageByTowerBuildingId = Metrics(source.DamageByTowerBuildingId),
                killsByTowerBuildingId = Metrics(source.KillsByTowerBuildingId),
                consumablesSpentByResourceId = Metrics(source.ConsumablesSpentByResourceId),
                buildingLossesByBuildingId = Metrics(source.BuildingLossesByBuildingId),
                completedProductionBatchCount = source.CompletedProductionBatchCount,
                productionActiveProgressSeconds = source.ProductionActiveProgressSeconds,
                productionEligibleSeconds = source.ProductionEligibleSeconds,
                cityWasPackedAfterCampaignStart = source.CityWasPackedAfterCampaignStart,
                developmentModifierUsed = source.DevelopmentModifierUsed,
                partialFromMigration = source.PartialFromMigration,
                controlledUnitLossCount = source.ControlledUnitLossCount };
        }

        private static FormalThreeDDefenseCampaignMetricSaveData[] Metrics(
            IReadOnlyList<SingleCityDefenseCampaignMetricPersistenceState> source)
        {
            var result = new FormalThreeDDefenseCampaignMetricSaveData[source.Count];
            for (var i = 0; i < result.Length; i++) result[i] = new
                FormalThreeDDefenseCampaignMetricSaveData
                { stableId = source[i].StableId, amount = source[i].Amount };
            return result;
        }

        private static SingleCityDefenseCampaignMetricPersistenceState[] Metrics(
            FormalThreeDDefenseCampaignMetricSaveData[] source)
        {
            var result = new SingleCityDefenseCampaignMetricPersistenceState[
                source.Length];
            for (var i = 0; i < result.Length; i++) result[i] = source[i] == null
                ? null : new SingleCityDefenseCampaignMetricPersistenceState(
                    source[i].stableId, source[i].amount);
            return result;
        }

        private static FormalThreeDPressureInjectedReinforcementSaveData[]
            Injected(IReadOnlyList<
                SingleCityDefenseInjectedReinforcementPersistenceState> source)
        {
            var result = new FormalThreeDPressureInjectedReinforcementSaveData[
                source.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var item = source[i];
                var entries = new FormalThreeDDefenseCampaignEnemyCountSaveData[
                    item.Entries.Count];
                for (var j = 0; j < entries.Length; j++)
                    entries[j] = new FormalThreeDDefenseCampaignEnemyCountSaveData
                    { enemyId = EnemyByArchetype(item.Entries[j].Archetype)
                            .Id.Value,
                        count = item.Entries[j].Count };
                result[i] = new FormalThreeDPressureInjectedReinforcementSaveData
                { stableEventId = item.StableEventId, entries = entries };
            }
            return result;
        }

        private static int Sum(FormalThreeDDefenseCampaignMetricSaveData[] source)
        {
            int total = 0;
            for (var i = 0; i < source.Length; i++)
                if (source[i] != null) total += source[i].amount;
            return total;
        }

        private static EnemyDefinition EnemyById(string id)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
                if (string.Equals(EnemyCatalog.All[index].Id.Value, id,
                        StringComparison.Ordinal))
                    return EnemyCatalog.All[index];
            return null;
        }

        private static EnemyDefinition EnemyByArchetype(EnemyArchetype value)
        {
            for (var index = 0; index < EnemyCatalog.All.Length; index++)
                if (EnemyCatalog.All[index].Archetype == value)
                    return EnemyCatalog.All[index];
            return null;
        }
    }
}
