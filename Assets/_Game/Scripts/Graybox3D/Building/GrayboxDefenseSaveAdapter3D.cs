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
    }
}
