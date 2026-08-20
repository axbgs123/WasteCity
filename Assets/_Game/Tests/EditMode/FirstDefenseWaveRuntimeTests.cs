using System;
using System.Linq;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Defense;

namespace WasteCity.Tests
{
    public sealed class FirstDefenseWaveRuntimeTests
    {
        private const float PositionTolerance = .001f;

        [Test]
        public void FirstCompletedMachineGunTurretTriggersTutorialWaveOnlyOnce()
        {
            TutorialDefenseRuntimeModel runtime = CreateRuntime();

            Assert.That(
                runtime.NotifyMachineGunTurretCompleted("turret.completed.001"),
                Is.True);
            Assert.That(runtime.Snapshot.WavePhase, Is.EqualTo(WavePhase.Warning));
            Assert.That(
                runtime.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(15f).Within(PositionTolerance));

            Assert.That(
                runtime.NotifyMachineGunTurretCompleted("turret.completed.001"),
                Is.False);
            Assert.That(
                runtime.NotifyMachineGunTurretCompleted("turret.completed.002"),
                Is.False);
            Assert.That(runtime.Snapshot.SpawnedEnemyCount, Is.Zero);
        }

        [Test]
        public void WarningSpawnsNothingThenEightGnawersUseStableFiveSecondCadence()
        {
            TutorialDefenseRuntimeModel runtime = CreateTriggeredRuntime();

            runtime.Advance(15f, globallyPaused: false);

            Assert.That(runtime.Snapshot.WavePhase, Is.EqualTo(WavePhase.Spawning));
            Assert.That(runtime.Snapshot.SpawnedEnemyCount, Is.Zero);
            Assert.That(runtime.Snapshot.Enemies, Is.Empty);

            runtime.Advance(4.999f, globallyPaused: false);
            Assert.That(runtime.Snapshot.SpawnedEnemyCount, Is.Zero);

            runtime.Advance(.001f, globallyPaused: false);
            DefenseRuntimeSnapshot firstSpawn = runtime.Snapshot;
            Assert.That(firstSpawn.SpawnedEnemyCount, Is.EqualTo(1));
            Assert.That(firstSpawn.AliveEnemyCount, Is.EqualTo(1));
            Assert.That(firstSpawn.Enemies[0].SpawnOrder, Is.Zero);
            Assert.That(firstSpawn.Enemies[0].X,
                Is.EqualTo(20f).Within(PositionTolerance));
            Assert.That(firstSpawn.Enemies[0].Z,
                Is.Zero.Within(PositionTolerance));

            runtime.Advance(34.999f, globallyPaused: false);
            Assert.That(runtime.Snapshot.SpawnedEnemyCount, Is.EqualTo(7));

            runtime.Advance(.001f, globallyPaused: false);
            DefenseRuntimeSnapshot completeSpawn = runtime.Snapshot;
            Assert.That(completeSpawn.WavePhase, Is.EqualTo(WavePhase.Active));
            Assert.That(completeSpawn.SpawnedEnemyCount, Is.EqualTo(8));
            Assert.That(completeSpawn.AliveEnemyCount, Is.EqualTo(8));
            Assert.That(
                completeSpawn.Enemies.Select(enemy => enemy.SpawnOrder),
                Is.EqualTo(Enumerable.Range(0, 8)));
            Assert.That(
                completeSpawn.Enemies.Select(enemy => enemy.StableId).Distinct().Count(),
                Is.EqualTo(8));

            TutorialDefenseRuntimeModel replay = CreateTriggeredRuntime();
            replay.Advance(55f, globallyPaused: false);
            Assert.That(
                replay.Snapshot.Enemies.Select(enemy => enemy.StableId),
                Is.EqualTo(completeSpawn.Enemies.Select(enemy => enemy.StableId)));
            Assert.That(
                replay.Snapshot.Enemies.Select(enemy => enemy.SpawnOrder),
                Is.EqualTo(completeSpawn.Enemies.Select(enemy => enemy.SpawnOrder)));
        }

        [Test]
        public void GlobalPauseFreezesWarningSpawnMovementAndCoreAttack()
        {
            TutorialDefenseRuntimeModel runtime = CreateTriggeredRuntime();

            runtime.Advance(5f, globallyPaused: false);
            DefenseRuntimeSnapshot warningBeforePause = runtime.Snapshot;
            runtime.Advance(100f, globallyPaused: true);
            AssertEquivalent(warningBeforePause, runtime.Snapshot);

            runtime.Advance(15f, globallyPaused: false);
            Assert.That(runtime.Snapshot.SpawnedEnemyCount, Is.EqualTo(1));
            runtime.Advance(10.5f, globallyPaused: false);
            DefenseRuntimeSnapshot attackingBeforePause = runtime.Snapshot;
            Assert.That(attackingBeforePause.Enemies[0].IsAttackingCore, Is.True);
            Assert.That(attackingBeforePause.CoreCurrentHealth, Is.EqualTo(1996));

            runtime.Advance(100f, globallyPaused: true);
            AssertEquivalent(attackingBeforePause, runtime.Snapshot);
        }

        [Test]
        public void GnawerMovesDirectlyAtOnePointEightAndAttacksAtTwoRange()
        {
            TutorialDefenseRuntimeModel runtime = CreateTriggeredRuntime();

            runtime.Advance(20f, globallyPaused: false);
            Assert.That(runtime.Snapshot.Enemies[0].X,
                Is.EqualTo(20f).Within(PositionTolerance));
            Assert.That(runtime.Snapshot.CoreCurrentHealth, Is.EqualTo(2000));

            runtime.Advance(5f, globallyPaused: false);
            Assert.That(runtime.Snapshot.Enemies[0].X,
                Is.EqualTo(11f).Within(PositionTolerance));
            Assert.That(runtime.Snapshot.Enemies[0].Z,
                Is.Zero.Within(PositionTolerance));

            runtime.Advance(5f, globallyPaused: false);
            Assert.That(runtime.Snapshot.Enemies[0].X,
                Is.EqualTo(2f).Within(PositionTolerance));
            Assert.That(runtime.Snapshot.Enemies[0].IsAttackingCore, Is.True);
            Assert.That(runtime.Snapshot.CoreCurrentHealth, Is.EqualTo(2000));

            runtime.Advance(1f, globallyPaused: false);
            Assert.That(runtime.Snapshot.Enemies[0].X,
                Is.EqualTo(2f).Within(PositionTolerance));
            Assert.That(runtime.Snapshot.CoreCurrentHealth, Is.EqualTo(1992));
        }

        [Test]
        public void MovingCoreRetargetsExistingEnemiesWithoutResettingWaveOrSpawn()
        {
            TutorialDefenseRuntimeModel runtime = CreateTriggeredRuntime();
            runtime.Advance(31f, globallyPaused: false);
            DefenseRuntimeSnapshot before = runtime.Snapshot;
            Assert.That(before.SpawnedEnemyCount, Is.EqualTo(3));
            Assert.That(before.CoreCurrentHealth, Is.LessThan(2000));
            Assert.That(before.Enemies[0].X,
                Is.EqualTo(2f).Within(PositionTolerance));

            runtime.SetCorePosition(x: 30f, z: 0f);

            DefenseRuntimeSnapshot immediatelyAfter = runtime.Snapshot;
            AssertEquivalent(before, immediatelyAfter);

            runtime.Advance(1f, globallyPaused: false);
            DefenseRuntimeSnapshot retargeted = runtime.Snapshot;
            Assert.That(retargeted.Enemies[0].X,
                Is.EqualTo(3.8f).Within(PositionTolerance));
            Assert.That(retargeted.CoreCurrentHealth,
                Is.EqualTo(before.CoreCurrentHealth));

            runtime.Advance(3f, globallyPaused: false);
            DefenseEnemyRuntimeSnapshot nextSpawn = runtime.Snapshot.Enemies
                .Single(enemy => enemy.SpawnOrder == 3);
            Assert.That(nextSpawn.X,
                Is.EqualTo(20f).Within(PositionTolerance));
            Assert.That(nextSpawn.Z,
                Is.Zero.Within(PositionTolerance));
        }

        [Test]
        public void PreviouslyReadSnapshotDoesNotChangeWhenRuntimeAdvances()
        {
            TutorialDefenseRuntimeModel runtime = CreateTriggeredRuntime();
            runtime.Advance(20f, globallyPaused: false);
            DefenseRuntimeSnapshot before = runtime.Snapshot;
            string firstStableId = before.Enemies[0].StableId;
            float firstX = before.Enemies[0].X;

            runtime.Advance(10f, globallyPaused: false);

            Assert.That(before.CoreMaximumHealth, Is.EqualTo(2000));
            Assert.That(before.CoreCurrentHealth, Is.EqualTo(2000));
            Assert.That(before.SpawnedEnemyCount, Is.EqualTo(1));
            Assert.That(before.AliveEnemyCount, Is.EqualTo(1));
            Assert.That(before.Enemies, Has.Count.EqualTo(1));
            Assert.That(before.Enemies[0].StableId, Is.EqualTo(firstStableId));
            Assert.That(before.Enemies[0].X,
                Is.EqualTo(firstX).Within(PositionTolerance));
            Assert.That(runtime.Snapshot.SpawnedEnemyCount, Is.EqualTo(3));
            Assert.That(runtime.Snapshot.Enemies[0].X,
                Is.EqualTo(2f).Within(PositionTolerance));
        }

        [Test]
        public void RestoreRejectsActiveTutorialAfterCompletionThreshold()
        {
            var state = new TutorialDefensePersistenceState(
                tutorialTriggered: true,
                wavePhase: WavePhase.Active,
                warningRemainingSeconds: 0f,
                spawnClockSeconds: 0f,
                spawnedEnemyCount: WaveCatalog.Tutorial.TotalCount,
                defeatedEnemyCount: WaveCatalog.Tutorial.TotalCount,
                nextEnemyOrdinal: WaveCatalog.Tutorial.TotalCount,
                fixedStepAccumulatorSeconds: 0f,
                spawnOriginX: 20f,
                spawnOriginZ: 0f,
                coreCurrentHealth: CityCoreCombatModel.FormalMaximumHealth,
                enemies: Array.Empty<DefenseEnemyPersistenceState>());

            bool restored = TutorialDefenseRuntimeModel.TryCreateForPersistence(
                state,
                coreX: 0f,
                coreZ: 0f,
                out TutorialDefenseRuntimeModel model,
                out string error);

            Assert.That(restored, Is.False,
                "A tutorial wave at its completion threshold cannot remain Active.");
            Assert.That(model, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        private static TutorialDefenseRuntimeModel CreateTriggeredRuntime()
        {
            TutorialDefenseRuntimeModel runtime = CreateRuntime();
            Assert.That(
                runtime.NotifyMachineGunTurretCompleted("turret.completed.001"),
                Is.True);
            return runtime;
        }

        private static TutorialDefenseRuntimeModel CreateRuntime()
        {
            return new TutorialDefenseRuntimeModel(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 20f,
                spawnZ: 0f);
        }

        private static void AssertEquivalent(
            DefenseRuntimeSnapshot expected,
            DefenseRuntimeSnapshot actual)
        {
            Assert.That(actual.WavePhase, Is.EqualTo(expected.WavePhase));
            Assert.That(actual.WarningRemainingSeconds,
                Is.EqualTo(expected.WarningRemainingSeconds)
                    .Within(PositionTolerance));
            Assert.That(actual.SpawnedEnemyCount,
                Is.EqualTo(expected.SpawnedEnemyCount));
            Assert.That(actual.AliveEnemyCount,
                Is.EqualTo(expected.AliveEnemyCount));
            Assert.That(actual.CoreMaximumHealth,
                Is.EqualTo(expected.CoreMaximumHealth));
            Assert.That(actual.CoreCurrentHealth,
                Is.EqualTo(expected.CoreCurrentHealth));
            Assert.That(actual.Enemies, Has.Count.EqualTo(expected.Enemies.Count));
            for (int index = 0; index < expected.Enemies.Count; index++)
            {
                Assert.That(actual.Enemies[index].StableId,
                    Is.EqualTo(expected.Enemies[index].StableId));
                Assert.That(actual.Enemies[index].SpawnOrder,
                    Is.EqualTo(expected.Enemies[index].SpawnOrder));
                Assert.That(actual.Enemies[index].X,
                    Is.EqualTo(expected.Enemies[index].X)
                        .Within(PositionTolerance));
                Assert.That(actual.Enemies[index].Z,
                    Is.EqualTo(expected.Enemies[index].Z)
                        .Within(PositionTolerance));
                Assert.That(actual.Enemies[index].CurrentHealth,
                    Is.EqualTo(expected.Enemies[index].CurrentHealth));
                Assert.That(actual.Enemies[index].IsAttackingCore,
                    Is.EqualTo(expected.Enemies[index].IsAttackingCore));
            }
        }
    }
}
