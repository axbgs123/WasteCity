using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalSaveDefenseTests
    {
        private const string AdapterTypeName =
            "WasteCity.Graybox3D.Building.GrayboxDefenseSaveAdapter3D, " +
            "WasteCity.Graybox3D.Building";
        private const float Tolerance = .0001f;

        [Test]
        public void FormalDefenseSaveAdapterTypeExists()
        {
            Assert.That(Type.GetType(AdapterTypeName), Is.Not.Null,
                "Task 8 requires a dedicated formal defense save adapter.");
        }

        [Test]
        public void CanonicalSchemaThirtyOneDefenseRestoresThroughFormalAdapter()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateDecoded(decoded);
            Assert.That(validation.IsValid, Is.True, validation.Message);
            FormalThreeDDefenseSaveData expected =
                decoded.Envelope.formal3D.defense;

            GrayboxBuildingInstance3D turret = Turret(
                expected.towers.Single().stableInstanceId,
                x: 14,
                y: 11);
            SetEvacuationLocked(turret, value: true);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { turret },
                spawnX: expected.spawnOriginX);
            object adapter = Adapter(runtime);

            Assert.That(TryRestore(
                adapter,
                expected,
                new[] { turret },
                out string error), Is.True, error);
            AssertDefenseEqual(expected, Capture(adapter));

            using CityResourceStorageModel storage = Storage(0);
            runtime.Tick(.05f, globallyPaused: false, storage);
            FormalThreeDDefenseSaveData advanced = Capture(adapter);
            Assert.That(advanced.spawnedEnemyCount,
                Is.EqualTo(expected.spawnedEnemyCount));
            Assert.That(advanced.towers.Single().ammunitionAmount,
                Is.EqualTo(expected.towers.Single().ammunitionAmount));
        }

        [Test]
        public void CaptureDeepCopiesAndSortsActiveCombatByStableIdentity()
        {
            GrayboxBuildingInstance3D later = Turret(
                "building.instance.turret-020",
                20,
                0);
            GrayboxBuildingInstance3D earlier = Turret(
                "building.instance.turret-010",
                19,
                0);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { later, earlier },
                spawnX: 2f);
            PauseAll(runtime);
            using CityResourceStorageModel storage = Storage(0);
            runtime.Tick(25.05f, globallyPaused: false, storage);
            object adapter = Adapter(runtime);

            FormalThreeDDefenseSaveData first = Capture(adapter);

            CollectionAssert.AreEqual(
                new[] { earlier.StableInstanceId, later.StableInstanceId },
                first.towers.Select(item => item.stableInstanceId).ToArray());
            CollectionAssert.AreEqual(
                first.enemies
                    .OrderBy(item => item.spawnOrder)
                    .ThenBy(item => item.stableEnemyId, StringComparer.Ordinal)
                    .Select(item => item.stableEnemyId)
                    .ToArray(),
                first.enemies.Select(item => item.stableEnemyId).ToArray());
            Assert.That(first.enemies, Has.Length.EqualTo(2));

            int capturedAmmo = first.towers[0].ammunitionAmount;
            int capturedHealth = first.enemies[0].currentHealth;
            first.towers[0].ammunitionAmount = 999;
            first.enemies[0].currentHealth = 1;

            FormalThreeDDefenseSaveData second = Capture(adapter);
            Assert.That(second.towers[0].ammunitionAmount,
                Is.EqualTo(capturedAmmo));
            Assert.That(second.enemies[0].currentHealth,
                Is.EqualTo(capturedHealth));
        }

        [TestCase(7.35f, 7.65f)]
        [TestCase(22.35f, 2.65f)]
        public void RestoreContinuesWarningOrSpawningWithoutRestartOrDuplicateSpawn(
            float beforeSaveSeconds,
            float afterSaveSeconds)
        {
            GrayboxBuildingInstance3D sourceTurret = Turret(
                "building.instance.turret-wave",
                20,
                0);
            GrayboxDefenseRuntime3D source = Runtime(
                new[] { sourceTurret },
                spawnX: 20f);
            PauseAll(source);
            using CityResourceStorageModel sourceStorage = Storage(0);
            source.Tick(beforeSaveSeconds, globallyPaused: false, sourceStorage);
            FormalThreeDDefenseSaveData saved = Capture(Adapter(source));

            GrayboxBuildingInstance3D restoredTurret = Turret(
                sourceTurret.StableInstanceId,
                20,
                0);
            GrayboxDefenseRuntime3D restored = Runtime(
                new[] { restoredTurret },
                spawnX: 20f);
            PauseAll(restored);
            using CityResourceStorageModel restoredStorage = Storage(0);

            Assert.That(TryRestore(
                Adapter(restored),
                saved,
                new[] { restoredTurret },
                out string error), Is.True, error);
            Assert.That(restored.Snapshot.SpawnedEnemyCount,
                Is.EqualTo(saved.spawnedEnemyCount));

            source.Tick(afterSaveSeconds, globallyPaused: false, sourceStorage);
            restored.Tick(
                afterSaveSeconds,
                globallyPaused: false,
                restoredStorage);

            AssertCombatEquivalent(source.Snapshot, restored.Snapshot);
            Assert.That(restored.Snapshot.SpawnedEnemyCount,
                Is.EqualTo(source.Snapshot.SpawnedEnemyCount),
                "Restore must continue the saved cadence, not schedule a new wave.");
        }

        [Test]
        public void FrozenSpawnOriginSurvivesCityMovementAndDifferentSceneDefaults()
        {
            const string stableId = "building.instance.turret-spawn-origin";
            GrayboxBuildingInstance3D sourceTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D source = Runtime(
                new[] { sourceTurret },
                spawnX: 20f);
            PauseAll(source);
            using CityResourceStorageModel sourceStorage = Storage(0);
            source.Tick(7.35f, globallyPaused: false, sourceStorage);
            source.SetCorePosition(x: 30f, z: 5f);
            FormalThreeDDefenseSaveData saved = Capture(Adapter(source));

            Assert.That(ReadFormalField<float>(saved, "spawnOriginX"),
                Is.EqualTo(20f).Within(Tolerance));
            Assert.That(ReadFormalField<float>(saved, "spawnOriginZ"),
                Is.Zero.Within(Tolerance));

            GrayboxBuildingInstance3D restoredTurret = Turret(stableId, 30, 5);
            GrayboxDefenseRuntime3D restored = Runtime(
                new[] { restoredTurret },
                spawnX: 99f);
            PauseAll(restored);
            restored.SetCorePosition(x: 30f, z: 5f);
            using CityResourceStorageModel restoredStorage = Storage(0);
            Assert.That(TryRestore(
                Adapter(restored),
                saved,
                new[] { restoredTurret },
                out string error), Is.True, error);

            restored.Tick(12.65f, globallyPaused: false, restoredStorage);

            GrayboxDefenseEnemySnapshot3D spawned =
                restored.Snapshot.Enemies.Single();
            Assert.That(spawned.X, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(spawned.Z, Is.Zero.Within(Tolerance));
            Assert.That(spawned.X, Is.Not.EqualTo(99f).Within(Tolerance),
                "Restore must use the wave's frozen origin, not scene defaults.");
        }

        [Test]
        public void ConfigurationSignatureCapturesAndMismatchRejectsWithoutMutation()
        {
            const string stableId = "building.instance.turret-config";
            GrayboxBuildingInstance3D sourceTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D source = Runtime(
                new[] { sourceTurret },
                spawnX: 20f);
            FormalThreeDDefenseSaveData saved = Capture(Adapter(source));
            string signature = ReadFormalField<string>(
                saved,
                "configurationSignature");
            Assert.That(signature, Is.Not.Null.And.Not.Empty);

            GrayboxBuildingInstance3D targetTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D target = Runtime(
                new[] { targetTurret },
                spawnX: 20f);
            object adapter = Adapter(target);
            FormalThreeDDefenseSaveData before = Capture(adapter);
            WriteFormalField(
                saved,
                "configurationSignature",
                signature + ".incompatible");

            Assert.That(TryRestore(
                adapter,
                saved,
                new[] { targetTurret },
                out _), Is.False);
            AssertDefenseEqual(before, Capture(adapter));
        }

        [Test]
        public void RestoredTowerContinuesAmmoLeaseAndDamageRemainderWithoutSecondSpend()
        {
            const string stableId = "building.instance.turret-lease";
            GrayboxBuildingInstance3D sourceTurret = Turret(stableId, 0, 0);
            GrayboxDefenseRuntime3D source = Runtime(
                new[] { sourceTurret },
                spawnX: 20f);
            MachineGunTurretCombatModel sourceCombat =
                source.Towers.Single().Combat;
            SetField(sourceCombat, "Ammo", 3);
            DefenseEnemyCombatModel sourceTarget = DurableEnemy(
                "enemy.test.turret-source");
            Assert.That(sourceCombat.Tick(
                .125f,
                sourceTarget,
                globallyPaused: false), Is.EqualTo(2));
            sourceCombat.SetPlayerPaused(true);
            FormalThreeDDefenseSaveData saved = Capture(Adapter(source));
            Assert.That(saved.towers.Single().ammunitionAmount, Is.EqualTo(2));
            Assert.That(saved.towers.Single().isPlayerPaused, Is.True);
            Assert.That(saved.towers.Single().activeAmmunitionSeconds,
                Is.EqualTo(2.875f).Within(Tolerance));
            Assert.That(saved.towers.Single().damageRemainder,
                Is.EqualTo(.5f).Within(Tolerance));

            GrayboxBuildingInstance3D restoredTurret = Turret(stableId, 0, 0);
            GrayboxDefenseRuntime3D restored = Runtime(
                new[] { restoredTurret },
                spawnX: 20f);
            Assert.That(TryRestore(
                Adapter(restored),
                saved,
                new[] { restoredTurret },
                out string error), Is.True, error);
            MachineGunTurretCombatModel restoredCombat =
                restored.Towers.Single().Combat;
            Assert.That(restoredCombat.Ammo, Is.EqualTo(2));
            Assert.That(restoredCombat.IsPlayerPaused, Is.True);
            restoredCombat.SetPlayerPaused(false);
            var target = DurableEnemy("enemy.test.turret-restored");

            Assert.That(restoredCombat.Tick(
                .025f,
                target,
                globallyPaused: false), Is.EqualTo(1));
            Assert.That(restoredCombat.Ammo, Is.EqualTo(2),
                "An active saved ammunition lease must not consume again.");
        }

        [Test]
        public void EnemyCoreAndAttackRemainderRoundTripAsAuthoritativeCombatTruth()
        {
            const string stableId = "building.instance.turret-enemy-state";
            GrayboxBuildingInstance3D sourceTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D source = Runtime(
                new[] { sourceTurret },
                spawnX: 2f);
            PauseAll(source);
            using CityResourceStorageModel sourceStorage = Storage(0);
            source.Tick(21.25f, globallyPaused: false, sourceStorage);
            FormalThreeDDefenseSaveData saved = Capture(Adapter(source));
            FormalThreeDDefenseEnemySaveData enemy = saved.enemies.Single();

            Assert.That(enemy.archetypeId, Is.EqualTo(EnemyCatalog.Gnawer.Id.Value));
            Assert.That(enemy.spawnOrder, Is.Zero);
            Assert.That(enemy.positionX, Is.EqualTo(2f).Within(Tolerance));
            Assert.That(enemy.positionZ, Is.Zero.Within(Tolerance));
            Assert.That(enemy.currentHealth, Is.EqualTo(60));
            Assert.That(enemy.movementRemainder, Is.Zero);
            Assert.That(enemy.attackDamageRemainder,
                Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(saved.coreCurrentHealth, Is.LessThan(2000));

            GrayboxBuildingInstance3D restoredTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D restored = Runtime(
                new[] { restoredTurret },
                spawnX: 2f);
            PauseAll(restored);
            using CityResourceStorageModel restoredStorage = Storage(0);
            Assert.That(TryRestore(
                Adapter(restored),
                saved,
                new[] { restoredTurret },
                out string error), Is.True, error);
            FormalThreeDDefenseSaveData recaptured = Capture(Adapter(restored));

            AssertEnemyEqual(enemy, recaptured.enemies.Single());
            Assert.That(recaptured.coreCurrentHealth,
                Is.EqualTo(saved.coreCurrentHealth));

            source.Tick(.1f, globallyPaused: false, sourceStorage);
            restored.Tick(.1f, globallyPaused: false, restoredStorage);
            AssertCombatEquivalent(source.Snapshot, restored.Snapshot);
        }

        [Test]
        public void OuterFixedStepRemainderRestoresBeforeTheNextCombatStep()
        {
            const string stableId = "building.instance.turret-fixed-remainder";
            GrayboxBuildingInstance3D sourceTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D source = Runtime(
                new[] { sourceTurret },
                spawnX: 2f);
            PauseAll(source);
            using CityResourceStorageModel sourceStorage = Storage(0);
            source.Tick(20.05f, globallyPaused: false, sourceStorage);
            FormalThreeDDefenseSaveData saved = Capture(Adapter(source));
            Assert.That(saved.fixedStepAccumulatorSeconds,
                Is.EqualTo(.05f).Within(Tolerance));

            GrayboxBuildingInstance3D restoredTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D restored = Runtime(
                new[] { restoredTurret },
                spawnX: 2f);
            PauseAll(restored);
            using CityResourceStorageModel restoredStorage = Storage(0);
            Assert.That(TryRestore(
                Adapter(restored),
                saved,
                new[] { restoredTurret },
                out string error), Is.True, error);
            int healthBefore = restored.Snapshot.CoreCurrentHealth;

            restored.Tick(.04f, globallyPaused: false, restoredStorage);
            Assert.That(restored.Snapshot.CoreCurrentHealth,
                Is.EqualTo(healthBefore));
            restored.Tick(.01f, globallyPaused: false, restoredStorage);
            source.Tick(.05f, globallyPaused: false, sourceStorage);

            AssertCombatEquivalent(source.Snapshot, restored.Snapshot);
        }

        [Test]
        public void PrepareIsZeroWriteAndCommitRejectsStaleOrConsumedPlan()
        {
            const string stableId = "building.instance.turret-atomic";
            GrayboxBuildingInstance3D sourceTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D source = Runtime(
                new[] { sourceTurret },
                spawnX: 20f);
            PauseAll(source);
            using CityResourceStorageModel sourceStorage = Storage(0);
            source.Tick(7.35f, globallyPaused: false, sourceStorage);
            FormalThreeDDefenseSaveData saved = Capture(Adapter(source));

            GrayboxBuildingInstance3D targetTurret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D target = Runtime(
                new[] { targetTurret },
                spawnX: 20f);
            object adapter = Adapter(target);
            FormalThreeDDefenseSaveData before = Capture(adapter);

            Assert.That(TryPrepare(
                adapter,
                saved,
                new[] { targetTurret },
                out object stalePlan,
                out string error), Is.True, error);
            AssertDefenseEqual(before, Capture(adapter));

            using CityResourceStorageModel targetStorage = Storage(0);
            target.Tick(.1f, globallyPaused: false, targetStorage);
            Assert.That(TryCommit(adapter, stalePlan, out _), Is.False);

            Assert.That(TryPrepare(
                adapter,
                saved,
                new[] { targetTurret },
                out object plan,
                out error), Is.True, error);
            Assert.That(TryCommit(adapter, plan, out error), Is.True, error);
            Assert.That(TryCommit(adapter, plan, out _), Is.False,
                "A defense restore plan is owner-bound and single-use.");
        }

        [Test]
        public void PrepareRejectsTurretLockedAfterItsLastSynchronization()
        {
            const string stableId = "building.instance.turret-stale-lock";
            GrayboxBuildingInstance3D turret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { turret },
                spawnX: 20f);
            object adapter = Adapter(runtime);
            FormalThreeDDefenseSaveData saved = Capture(adapter);
            FormalThreeDDefenseSaveData before = Capture(adapter);

            SetEvacuationLocked(turret, value: true);

            Assert.That(TryPrepare(
                adapter,
                saved,
                new[] { turret },
                out _,
                out _), Is.False,
                "Prepare must reject an instance whose operational state " +
                "changed after the defense runtime was synchronized.");
            AssertDefenseEqual(before, Capture(adapter));
        }

        [Test]
        public void PrepareRejectsUnsynchronizedReplacementAtSameIdentityAndCell()
        {
            const string stableId = "building.instance.turret-replacement";
            GrayboxBuildingInstance3D synchronized = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { synchronized },
                spawnX: 20f);
            object adapter = Adapter(runtime);
            FormalThreeDDefenseSaveData saved = Capture(adapter);
            FormalThreeDDefenseSaveData before = Capture(adapter);
            GrayboxBuildingInstance3D replacement = Turret(stableId, 20, 0);

            Assert.That(TryPrepare(
                adapter,
                saved,
                new[] { replacement },
                out _,
                out _), Is.False,
                "Stable identity and placement are insufficient when the " +
                "runtime was synchronized against another instance object.");
            AssertDefenseEqual(before, Capture(adapter));
        }

        [Test]
        public void RuntimePrepareRejectsASecondInnerFixedStepRemainder()
        {
            const string stableId = "building.instance.turret-inner-clock";
            GrayboxBuildingInstance3D turret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { turret },
                spawnX: 20f);
            object adapter = Adapter(runtime);
            FormalThreeDDefenseSaveData before = Capture(adapter);
            GrayboxDefensePersistenceState3D captured =
                runtime.CaptureForPersistence();
            TutorialDefensePersistenceState source = captured.Tutorial;
            var tutorialWithInnerRemainder =
                new TutorialDefensePersistenceState(
                    source.TutorialTriggered,
                    source.WavePhase,
                    source.WarningRemainingSeconds,
                    source.SpawnClockSeconds,
                    source.SpawnedEnemyCount,
                    source.DefeatedEnemyCount,
                    source.NextEnemyOrdinal,
                    .05f,
                    source.SpawnOriginX,
                    source.SpawnOriginZ,
                    source.CoreCurrentHealth,
                    source.Enemies);
            var invalid = new GrayboxDefensePersistenceState3D(
                captured.TutorialWaveTriggerCount,
                .02f,
                captured.RandomState,
                tutorialWithInnerRemainder,
                captured.Towers.ToArray());

            Assert.That(runtime.TryPrepareRestore(
                invalid,
                new[] { turret },
                out _,
                out _), Is.False,
                "The outer accumulator is the sole formal pending-time " +
                "owner; an inner remainder must be rejected.");
            AssertDefenseEqual(before, Capture(adapter));
        }

        [Test]
        public void CommitRejectsDirectLogisticsMutationAfterPrepare()
        {
            const string stableId = "building.instance.turret-logistics-stale";
            GrayboxBuildingInstance3D turret = Turret(stableId, 0, 0);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { turret },
                spawnX: 20f);
            object adapter = Adapter(runtime);
            FormalThreeDDefenseSaveData saved = Capture(adapter);

            Assert.That(TryPrepare(
                adapter,
                saved,
                new[] { turret },
                out object plan,
                out string error), Is.True, error);
            runtime.Towers.Single().Combat.SetLogisticsConnected(false);

            Assert.That(TryCommit(adapter, plan, out _), Is.False,
                "A prepared plan must become stale when copied logistics " +
                "truth changes before commit.");
            Assert.That(runtime.Towers.Single().Combat.IsLogisticsConnected,
                Is.False);
        }

        [Test]
        public void RestoreDoesNotPersistTargetOrStatusAndRebuildsThemOnRuleTick()
        {
            const string stableId = "building.instance.turret-derived";
            GrayboxBuildingInstance3D turret = Turret(stableId, 0, 0);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { turret },
                spawnX: 2f);
            using CityResourceStorageModel storage = Storage(40);
            runtime.Tick(21f, globallyPaused: false, storage);
            Assert.That(runtime.Towers.Single().TargetId, Is.Not.Null);
            Assert.That(runtime.Towers.Single().Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            object adapter = Adapter(runtime);
            FormalThreeDDefenseSaveData saved = Capture(adapter);

            Assert.That(typeof(FormalThreeDDefenseTowerSaveData).GetField(
                "targetId"), Is.Null);
            Assert.That(typeof(FormalThreeDDefenseTowerSaveData).GetField(
                "status"), Is.Null);
            Assert.That(TryRestore(
                adapter,
                saved,
                new[] { turret },
                out string error), Is.True, error);
            Assert.That(runtime.Towers.Single().TargetId, Is.Null);
            Assert.That(runtime.Towers.Single().Status,
                Is.Not.EqualTo(GrayboxDefenseTowerStatus3D.Firing));

            runtime.Tick(.1f, globallyPaused: false, storage);
            Assert.That(runtime.Towers.Single().TargetId, Is.Not.Null);
            Assert.That(runtime.Towers.Single().Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
        }

        [Test]
        public void CompletedWaveAndDestroyedCoreRemainExactAndDoNotRestart()
        {
            const string stableId = "building.instance.turret-completed";
            GrayboxBuildingInstance3D turret = Turret(stableId, 20, 0);
            GrayboxDefenseRuntime3D runtime = Runtime(
                new[] { turret },
                spawnX: 20f);
            object adapter = Adapter(runtime);
            FormalThreeDDefenseSaveData completed = Capture(adapter);
            completed.tutorialTriggered = true;
            completed.tutorialWaveTriggerCount = 1;
            completed.wavePhase = (int)WavePhase.Idle;
            completed.warningRemainingSeconds = 0f;
            completed.spawnClockSeconds = 0f;
            completed.fixedStepAccumulatorSeconds = 0f;
            completed.spawnedEnemyCount = WaveCatalog.Tutorial.TotalCount;
            completed.defeatedEnemyCount = WaveCatalog.Tutorial.TotalCount;
            completed.nextEnemyOrdinal = WaveCatalog.Tutorial.TotalCount;
            completed.coreCurrentHealth = 0;
            completed.enemies = Array.Empty<FormalThreeDDefenseEnemySaveData>();

            Assert.That(TryRestore(
                adapter,
                completed,
                new[] { turret },
                out string error), Is.True, error);
            FormalThreeDDefenseSaveData restored = Capture(adapter);
            Assert.That(restored.wavePhase, Is.EqualTo((int)WavePhase.Idle));
            Assert.That(restored.spawnedEnemyCount,
                Is.EqualTo(WaveCatalog.Tutorial.TotalCount));
            Assert.That(restored.defeatedEnemyCount,
                Is.EqualTo(WaveCatalog.Tutorial.TotalCount));
            Assert.That(restored.nextEnemyOrdinal,
                Is.EqualTo(WaveCatalog.Tutorial.TotalCount));
            Assert.That(restored.coreCurrentHealth, Is.Zero);
            Assert.That(runtime.Snapshot.IsCoreDestroyed, Is.True);

            runtime.Synchronize(
                new[] { turret },
                CityMode.Fortress,
                cityX: 0,
                cityY: 0,
                BuildingRangeRules.InitialGroundRadius);
            using CityResourceStorageModel storage = Storage(0);
            runtime.Tick(30f, globallyPaused: false, storage);

            Assert.That(runtime.Snapshot.WavePhase, Is.EqualTo(WavePhase.Idle));
            Assert.That(runtime.Snapshot.SpawnedEnemyCount,
                Is.EqualTo(WaveCatalog.Tutorial.TotalCount));
            Assert.That(runtime.Snapshot.Enemies, Is.Empty);
            Assert.That(runtime.Snapshot.CoreCurrentHealth, Is.Zero);
        }

        [Test]
        public void SchemaThirtyTwoCoordinatorPreservesAuthoritativeCampaignAfterLegacyDefenseRebuild()
        {
            FormalSaveDecodeResult decoded = FormalSaveCodec.DecodeAny(
                ReadFixture("schema-31-formal-3d.json"));
            Assert.That(decoded.Success, Is.True, decoded.Message);
            FormalSaveEnvelope envelope = CloneEnvelope(decoded.Envelope);
            envelope.formal3D.defenseCampaign = MixedCampaignState();
            envelope.formal3D.defense.coreCurrentHealth = 1987;
            envelope.payloadHashSha256 =
                FormalSaveCodec.ComputePayloadHashSha256(envelope.formal3D);

            FormalSaveValidationResult validation =
                FormalSaveValidator.ValidateEnvelope(envelope);
            Assert.That(validation.IsValid, Is.True, validation.Message);
            FormalThreeDDefenseCampaignSaveData expected = CloneCampaign(
                envelope.formal3D.defenseCampaign);

            var authority = new CoordinatorAuthority(
                ClonePayload(decoded.Envelope.formal3D));
            var domains = new List<IFormalThreeDSaveDomain>();
            foreach (GrayboxFormalSaveDomainId3D domainId in
                     GrayboxFormalSaveCoordinator3D.DomainOrder)
            {
                domains.Add(new CoordinatorDomain(authority, domainId));
            }
            var rebuilder = new LegacyDefenseRebuilder(authority);
            var coordinator = new GrayboxFormalSaveCoordinator3D(
                domains,
                rebuilder);

            GrayboxFormalSaveCoordinatorResult3D restored =
                coordinator.RestoreEnvelope(envelope);
            Assert.That(restored.Success, Is.True, restored.Message);
            Assert.That(rebuilder.RebuildCount, Is.EqualTo(1));

            envelope.formal3D.defenseCampaign.phase = 0;
            envelope.formal3D.defenseCampaign.enemyStates[0].currentHealth = 1;
            envelope.formal3D.defenseCampaign.towerCombatStates[0]
                .targetStableEnemyId = string.Empty;

            GrayboxFormalSaveCoordinatorResult3D captured =
                coordinator.CaptureEnvelope(
                    expected.campaignId + ".session",
                    decoded.Envelope.gameVersion,
                    decoded.Envelope.contentSources,
                    decoded.Envelope.checkpoint,
                    new DateTime(2026, 8, 24, 12, 0, 0,
                        DateTimeKind.Utc));

            Assert.That(captured.Success, Is.True, captured.Message);
            Assert.That(captured.Envelope.formal3D.defense.coreCurrentHealth,
                Is.EqualTo(LegacyDefenseRebuilder.RebuiltCoreHealth),
                "The test must exercise a real legacy-defense rebuild.");
            Assert.That(
                JsonUtility.ToJson(
                    captured.Envelope.formal3D.defenseCampaign,
                    false),
                Is.EqualTo(JsonUtility.ToJson(expected, false)),
                "IDEA-0017 campaign truth must survive coordinator restore " +
                "and capture without being regenerated from legacy defense.");
        }

        private static FormalThreeDDefenseCampaignSaveData MixedCampaignState()
        {
            const string miningId = "building.instance.000001";
            const string turretId = "building.instance.000003";
            const string gnawerId = "campaign.enemy.wave-05.0009";
            const string crystalId = "campaign.enemy.wave-05.0010";
            const string howlerId = "campaign.enemy.wave-05.0011";
            return new FormalThreeDDefenseCampaignSaveData
            {
                campaignId = CampaignWaveCatalog.Id,
                phase = (int)SingleCityDefenseCampaignPhase.Defeat,
                currentWaveNumber = 5,
                plannedEnemyCountsByEnemyId = new[]
                {
                    EnemyCount(EnemyCatalog.CrystalBeast.Id.Value, 4),
                    EnemyCount(EnemyCatalog.Gnawer.Id.Value, 16),
                    EnemyCount(EnemyCatalog.Howler.Id.Value, 2),
                },
                spawnedEnemyCountsByEnemyId = new[]
                {
                    EnemyCount(EnemyCatalog.CrystalBeast.Id.Value, 2),
                    EnemyCount(EnemyCatalog.Gnawer.Id.Value, 5),
                    EnemyCount(EnemyCatalog.Howler.Id.Value, 1),
                },
                defeatedEnemyCountsByEnemyId = new[]
                {
                    EnemyCount(EnemyCatalog.CrystalBeast.Id.Value, 1),
                    EnemyCount(EnemyCatalog.Gnawer.Id.Value, 4),
                    EnemyCount(EnemyCatalog.Howler.Id.Value, 0),
                },
                frozenSpawnAnchors = new[]
                {
                    new FormalThreeDDefenseCampaignSpawnAnchorSaveData
                    {
                        direction = CampaignSpawnDirection.East,
                        positionX = 61.25f,
                        positionZ = 23.5f,
                    },
                    new FormalThreeDDefenseCampaignSpawnAnchorSaveData
                    {
                        direction = CampaignSpawnDirection.South,
                        positionX = 31.75f,
                        positionZ = 1.5f,
                    },
                },
                warningRemainingSeconds = 3.25f,
                spawnClockSeconds = 17.75f,
                fixedStepAccumulatorSeconds = .075f,
                nextEnemyOrdinal = 13,
                coreCurrentHealth = 0,
                requestedSpeed = 0f,
                lastNonZeroSpeed = 2f,
                result = (int)SingleCityDefenseCampaignResult.Defeat,
                towerCombatStates = new[]
                {
                    new FormalThreeDDefenseCampaignTowerCombatStateSaveData
                    {
                        stableInstanceId = turretId,
                        consumableId = ResourceIds.Ammunition,
                        amount = 9,
                        isPlayerPaused = true,
                        activeConsumableSeconds = 1.375f,
                        damageRemainder = .625f,
                        targetStableEnemyId = crystalId,
                    },
                },
                enemyStates = new[]
                {
                    EnemyState(
                        gnawerId,
                        EnemyCatalog.Gnawer.Id.Value,
                        9,
                        42.75f,
                        20.5f,
                        31,
                        string.Empty),
                    EnemyState(
                        crystalId,
                        EnemyCatalog.CrystalBeast.Id.Value,
                        10,
                        44.5f,
                        18.25f,
                        137,
                        miningId),
                    EnemyState(
                        howlerId,
                        EnemyCatalog.Howler.Id.Value,
                        11,
                        39.25f,
                        16.75f,
                        72,
                        miningId),
                },
                buildingHealthStates = new[]
                {
                    new FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                    {
                        stableInstanceId = miningId,
                        currentHealth = 83,
                        isDestroyed = false,
                    },
                    new FormalThreeDDefenseCampaignBuildingHealthStateSaveData
                    {
                        stableInstanceId = turretId,
                        currentHealth = 211,
                        isDestroyed = false,
                    },
                },
                statistics = new FormalThreeDDefenseCampaignStatisticsSaveData
                {
                    elapsedRuleSeconds = 418.375f,
                    spawnedEnemyCount = 52,
                    defeatedEnemyCount = 47,
                    completedWaveCount = 4,
                    killsByEnemyId = new[]
                    {
                        Metric(EnemyCatalog.CrystalBeast.Id.Value, 6),
                        Metric(EnemyCatalog.Gnawer.Id.Value, 38),
                        Metric(EnemyCatalog.Howler.Id.Value, 3),
                    },
                    highestAliveEnemyCount = 17,
                    coreDamageTaken = 2000,
                    buildingLossesByBuildingId = new[]
                    {
                        Metric(BuildingCatalog.Wall.Id.Value, 2),
                    },
                    damageByTowerBuildingId = new[]
                    {
                        Metric(BuildingCatalog.MachineGunTurret.Id.Value, 913),
                    },
                    killsByTowerBuildingId = new[]
                    {
                        Metric(BuildingCatalog.MachineGunTurret.Id.Value, 47),
                    },
                    consumablesSpentByResourceId = new[]
                    {
                        Metric(ResourceIds.BiologicalWeapon, 3),
                        Metric(ResourceIds.EnergyCrystal, 7),
                        Metric(ResourceIds.Ammunition, 19),
                    },
                    completedProductionBatchCount = 31,
                    productionActiveProgressSeconds = 188.5f,
                    productionEligibleSeconds = 231.75f,
                    cityWasPackedAfterCampaignStart = true,
                    developmentModifierUsed = true,
                    partialFromMigration = false,
                },
            };
        }

        private static FormalThreeDDefenseCampaignEnemyCountSaveData EnemyCount(
            string enemyId,
            int count)
        {
            return new FormalThreeDDefenseCampaignEnemyCountSaveData
            {
                enemyId = enemyId,
                count = count,
            };
        }

        private static FormalThreeDDefenseCampaignMetricSaveData Metric(
            string stableId,
            int amount)
        {
            return new FormalThreeDDefenseCampaignMetricSaveData
            {
                stableId = stableId,
                amount = amount,
            };
        }

        private static FormalThreeDDefenseCampaignEnemyStateSaveData EnemyState(
            string stableEnemyId,
            string archetypeId,
            int spawnOrder,
            float x,
            float z,
            int currentHealth,
            string targetStableId)
        {
            return new FormalThreeDDefenseCampaignEnemyStateSaveData
            {
                stableEnemyId = stableEnemyId,
                archetypeId = archetypeId,
                spawnOrder = spawnOrder,
                positionX = x,
                positionZ = z,
                currentHealth = currentHealth,
                movementRemainder = .25f,
                attackDamageRemainder = .5f,
                targetStableId = targetStableId,
            };
        }

        private static FormalSaveEnvelope CloneEnvelope(
            FormalSaveEnvelope source)
        {
            return JsonUtility.FromJson<FormalSaveEnvelope>(
                JsonUtility.ToJson(source, false));
        }

        private static FormalThreeDSaveData ClonePayload(
            FormalThreeDSaveData source)
        {
            return JsonUtility.FromJson<FormalThreeDSaveData>(
                JsonUtility.ToJson(source, false));
        }

        private static FormalThreeDDefenseCampaignSaveData CloneCampaign(
            FormalThreeDDefenseCampaignSaveData source)
        {
            return JsonUtility.FromJson<FormalThreeDDefenseCampaignSaveData>(
                JsonUtility.ToJson(source, false));
        }

        private static GrayboxDefenseRuntime3D Runtime(
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            float spawnX)
        {
            var runtime = new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: spawnX,
                spawnZ: 0f);
            runtime.Synchronize(
                instances,
                CityMode.Fortress,
                cityX: 0,
                cityY: 0,
                BuildingRangeRules.InitialGroundRadius);
            return runtime;
        }

        private static string ReadFixture(string fileName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            string path = Path.Combine(
                projectRoot,
                "Assets/_Game/Tests/Fixtures/Persistence",
                fileName);
            Assert.That(File.Exists(path), Is.True,
                "Missing fixture: " + path);
            return File.ReadAllText(path);
        }

        private static void PauseAll(GrayboxDefenseRuntime3D runtime)
        {
            foreach (GrayboxDefenseTowerRuntimeState3D tower in runtime.Towers)
            {
                Assert.That(runtime.TrySetPlayerPaused(
                    tower.StableId,
                    paused: true), Is.True);
            }
        }

        private static CityResourceStorageModel Storage(int ammunition)
        {
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, ammunition);
            return new CityResourceStorageModel(inventory, 150);
        }

        private static GrayboxBuildingInstance3D Turret(
            string stableInstanceId,
            int x,
            int y)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(string),
                        typeof(PlacedBuilding),
                        typeof(ConstructionProgress),
                        typeof(ResourceNodeBinding),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableInstanceId,
                    new PlacedBuilding(
                        BuildingCatalog.MachineGunTurret,
                        x,
                        y,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(
                        BuildingCatalog.MachineGunTurret.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, Array.Empty<object>());
            return instance;
        }

        private static DefenseEnemyCombatModel DurableEnemy(string stableId)
        {
            var definition = new EnemyDefinition(
                "test.enemy.formal-save-durable",
                "存档高耐久测试敌人",
                EnemyArchetype.Gnawer,
                health: 1000,
                speed: 1f,
                dps: 0f,
                range: 1f,
                armor: ArmorType.Light,
                biomass: 0,
                priority: EnemyTargetPriority.Nearest);
            return new DefenseEnemyCombatModel(
                stableId,
                definition,
                x: 1f,
                z: 0f);
        }

        private static void SetEvacuationLocked(
            GrayboxBuildingInstance3D instance,
            bool value)
        {
            MethodInfo setter = typeof(GrayboxBuildingInstance3D).GetMethod(
                "SetEvacuationLocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(instance, new object[] { value });
        }

        private static object Adapter(GrayboxDefenseRuntime3D runtime)
        {
            Type type = Type.GetType(AdapterTypeName);
            Assert.That(type, Is.Not.Null,
                "Task 8 requires the formal defense save adapter.");
            return Activator.CreateInstance(type, runtime);
        }

        private static FormalThreeDDefenseSaveData Capture(object adapter)
        {
            return (FormalThreeDDefenseSaveData)Invoke(
                FindMethod(adapter, "Capture", 0),
                adapter,
                Array.Empty<object>());
        }

        private static bool TryRestore(
            object adapter,
            FormalThreeDDefenseSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, "TryRestore", 3);
            object[] arguments = { data, instances, null };
            bool restored = (bool)Invoke(method, adapter, arguments);
            error = arguments[2] as string;
            return restored;
        }

        private static bool TryPrepare(
            object adapter,
            FormalThreeDDefenseSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out object plan,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, "TryPrepareRestore", 4);
            object[] arguments = { data, instances, null, null };
            bool prepared = (bool)Invoke(method, adapter, arguments);
            plan = arguments[2];
            error = arguments[3] as string;
            return prepared;
        }

        private static bool TryCommit(
            object adapter,
            object plan,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, "TryCommitRestore", 2);
            object[] arguments = { plan, null };
            bool committed = (bool)Invoke(method, adapter, arguments);
            error = arguments[1] as string;
            return committed;
        }

        private static MethodInfo FindMethod(
            object instance,
            string name,
            int parameterCount)
        {
            MethodInfo method = instance.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .SingleOrDefault(candidate =>
                    candidate.Name == name &&
                    candidate.GetParameters().Length == parameterCount);
            Assert.That(method, Is.Not.Null,
                name + " must expose the Task 8 persistence contract.");
            return method;
        }

        private static object Invoke(
            MethodInfo method,
            object instance,
            object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void SetField(object instance, string propertyName, int value)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            MethodInfo setter = property.GetSetMethod(nonPublic: true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(instance, new object[] { value });
        }

        private static T ReadFormalField<T>(
            FormalThreeDDefenseSaveData data,
            string fieldName)
        {
            FieldInfo field = typeof(FormalThreeDDefenseSaveData).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null,
                "Schema 31 defense requires field " + fieldName + ".");
            return (T)field.GetValue(data);
        }

        private static void WriteFormalField(
            FormalThreeDDefenseSaveData data,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(FormalThreeDDefenseSaveData).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null,
                "Schema 31 defense requires field " + fieldName + ".");
            field.SetValue(data, value);
        }

        private static void AssertEnemyEqual(
            FormalThreeDDefenseEnemySaveData expected,
            FormalThreeDDefenseEnemySaveData actual)
        {
            Assert.That(actual.stableEnemyId, Is.EqualTo(expected.stableEnemyId));
            Assert.That(actual.archetypeId, Is.EqualTo(expected.archetypeId));
            Assert.That(actual.spawnOrder, Is.EqualTo(expected.spawnOrder));
            Assert.That(actual.positionX,
                Is.EqualTo(expected.positionX).Within(Tolerance));
            Assert.That(actual.positionZ,
                Is.EqualTo(expected.positionZ).Within(Tolerance));
            Assert.That(actual.currentHealth, Is.EqualTo(expected.currentHealth));
            Assert.That(actual.movementRemainder,
                Is.EqualTo(expected.movementRemainder).Within(Tolerance));
            Assert.That(actual.attackDamageRemainder,
                Is.EqualTo(expected.attackDamageRemainder).Within(Tolerance));
        }

        private static void AssertDefenseEqual(
            FormalThreeDDefenseSaveData expected,
            FormalThreeDDefenseSaveData actual)
        {
            Assert.That(ReadFormalField<string>(actual, "configurationSignature"),
                Is.EqualTo(ReadFormalField<string>(
                    expected,
                    "configurationSignature")));
            Assert.That(ReadFormalField<float>(actual, "spawnOriginX"),
                Is.EqualTo(ReadFormalField<float>(expected, "spawnOriginX"))
                    .Within(Tolerance));
            Assert.That(ReadFormalField<float>(actual, "spawnOriginZ"),
                Is.EqualTo(ReadFormalField<float>(expected, "spawnOriginZ"))
                    .Within(Tolerance));
            Assert.That(actual.tutorialTriggered,
                Is.EqualTo(expected.tutorialTriggered));
            Assert.That(actual.tutorialWaveTriggerCount,
                Is.EqualTo(expected.tutorialWaveTriggerCount));
            Assert.That(actual.wavePhase, Is.EqualTo(expected.wavePhase));
            Assert.That(actual.warningRemainingSeconds,
                Is.EqualTo(expected.warningRemainingSeconds).Within(Tolerance));
            Assert.That(actual.spawnClockSeconds,
                Is.EqualTo(expected.spawnClockSeconds).Within(Tolerance));
            Assert.That(actual.fixedStepAccumulatorSeconds,
                Is.EqualTo(expected.fixedStepAccumulatorSeconds)
                    .Within(Tolerance));
            Assert.That(actual.spawnedEnemyCount,
                Is.EqualTo(expected.spawnedEnemyCount));
            Assert.That(actual.defeatedEnemyCount,
                Is.EqualTo(expected.defeatedEnemyCount));
            Assert.That(actual.nextEnemyOrdinal,
                Is.EqualTo(expected.nextEnemyOrdinal));
            Assert.That(actual.coreCurrentHealth,
                Is.EqualTo(expected.coreCurrentHealth));
            Assert.That(actual.towers, Has.Length.EqualTo(expected.towers.Length));
            Assert.That(actual.enemies, Has.Length.EqualTo(expected.enemies.Length));
            for (var index = 0; index < expected.towers.Length; index++)
            {
                Assert.That(actual.towers[index].stableInstanceId,
                    Is.EqualTo(expected.towers[index].stableInstanceId));
                Assert.That(actual.towers[index].ammunitionAmount,
                    Is.EqualTo(expected.towers[index].ammunitionAmount));
                Assert.That(actual.towers[index].isPlayerPaused,
                    Is.EqualTo(expected.towers[index].isPlayerPaused));
                Assert.That(actual.towers[index].activeAmmunitionSeconds,
                    Is.EqualTo(expected.towers[index].activeAmmunitionSeconds)
                        .Within(Tolerance));
                Assert.That(actual.towers[index].damageRemainder,
                    Is.EqualTo(expected.towers[index].damageRemainder)
                        .Within(Tolerance));
            }
            for (var index = 0; index < expected.enemies.Length; index++)
                AssertEnemyEqual(expected.enemies[index], actual.enemies[index]);
        }

        private static void AssertCombatEquivalent(
            GrayboxDefenseRuntimeSnapshot3D expected,
            GrayboxDefenseRuntimeSnapshot3D actual)
        {
            Assert.That(actual.TutorialWaveTriggerCount,
                Is.EqualTo(expected.TutorialWaveTriggerCount));
            Assert.That(actual.WavePhase, Is.EqualTo(expected.WavePhase));
            Assert.That(actual.WarningRemainingSeconds,
                Is.EqualTo(expected.WarningRemainingSeconds).Within(Tolerance));
            Assert.That(actual.SpawnedEnemyCount,
                Is.EqualTo(expected.SpawnedEnemyCount));
            Assert.That(actual.DefeatedEnemyCount,
                Is.EqualTo(expected.DefeatedEnemyCount));
            Assert.That(actual.CoreCurrentHealth,
                Is.EqualTo(expected.CoreCurrentHealth));
            Assert.That(actual.Enemies, Has.Count.EqualTo(expected.Enemies.Count));
            for (var index = 0; index < expected.Enemies.Count; index++)
            {
                Assert.That(actual.Enemies[index].StableId,
                    Is.EqualTo(expected.Enemies[index].StableId));
                Assert.That(actual.Enemies[index].SpawnOrder,
                    Is.EqualTo(expected.Enemies[index].SpawnOrder));
                Assert.That(actual.Enemies[index].X,
                    Is.EqualTo(expected.Enemies[index].X).Within(Tolerance));
                Assert.That(actual.Enemies[index].Z,
                    Is.EqualTo(expected.Enemies[index].Z).Within(Tolerance));
                Assert.That(actual.Enemies[index].CurrentHealth,
                    Is.EqualTo(expected.Enemies[index].CurrentHealth));
            }
        }

        private sealed class CoordinatorAuthority
        {
            public CoordinatorAuthority(FormalThreeDSaveData payload)
            {
                Payload = payload ?? throw new ArgumentNullException(
                    nameof(payload));
            }

            public FormalThreeDSaveData Payload { get; }
        }

        private sealed class CoordinatorDomain : IFormalThreeDSaveDomain
        {
            private readonly CoordinatorAuthority authority;

            public CoordinatorDomain(
                CoordinatorAuthority authority,
                GrayboxFormalSaveDomainId3D domainId)
            {
                this.authority = authority ?? throw new ArgumentNullException(
                    nameof(authority));
                DomainId = domainId;
            }

            public GrayboxFormalSaveDomainId3D DomainId { get; }

            public bool TryCapture(
                FormalThreeDSaveData destination,
                out string error)
            {
                CopyDomain(authority.Payload, destination, DomainId);
                error = string.Empty;
                return true;
            }

            public bool TryApply(
                FormalThreeDSaveData source,
                out string error)
            {
                CopyDomain(source, authority.Payload, DomainId);
                error = string.Empty;
                return true;
            }

            private static void CopyDomain(
                FormalThreeDSaveData source,
                FormalThreeDSaveData destination,
                GrayboxFormalSaveDomainId3D domainId)
            {
                FormalThreeDSaveData copy = ClonePayload(source);
                switch (domainId)
                {
                    case GrayboxFormalSaveDomainId3D.WorldCity:
                        destination.world = copy.world;
                        destination.city = copy.city;
                        break;
                    case GrayboxFormalSaveDomainId3D.BuildingStorage:
                        destination.buildings = copy.buildings;
                        destination.storage = copy.storage;
                        break;
                    case GrayboxFormalSaveDomainId3D.Economy:
                        destination.backpack = copy.backpack;
                        destination.crafting = copy.crafting;
                        destination.research = copy.research;
                        break;
                    case GrayboxFormalSaveDomainId3D.Production:
                        destination.production = copy.production;
                        break;
                    case GrayboxFormalSaveDomainId3D.Progression:
                        destination.progression = copy.progression;
                        break;
                    case GrayboxFormalSaveDomainId3D.Defense:
                        destination.defense = copy.defense;
                        destination.defenseCampaign = copy.defenseCampaign;
                        break;
                    case GrayboxFormalSaveDomainId3D.ResearchEffectState:
                        destination.researchEffectState =
                            copy.researchEffectState;
                        break;
                    case GrayboxFormalSaveDomainId3D.Evacuation:
                        destination.evacuation = copy.evacuation;
                        break;
                    case GrayboxFormalSaveDomainId3D.CivilizationExpansion:
                        destination.civilizationExpansion =
                            copy.civilizationExpansion;
                        break;
                    case GrayboxFormalSaveDomainId3D.Pause:
                        destination.pause = copy.pause;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(domainId),
                            domainId,
                            null);
                }
            }
        }

        private sealed class LegacyDefenseRebuilder :
            IFormalThreeDDerivedStateRebuilder
        {
            public const int RebuiltCoreHealth = 1777;

            private readonly CoordinatorAuthority authority;

            public LegacyDefenseRebuilder(CoordinatorAuthority authority)
            {
                this.authority = authority ?? throw new ArgumentNullException(
                    nameof(authority));
            }

            public int RebuildCount { get; private set; }

            public void RebuildDerivedState()
            {
                RebuildCount++;
                authority.Payload.defense.coreCurrentHealth =
                    RebuiltCoreHealth;
            }
        }
    }
}
