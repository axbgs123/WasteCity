using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class IDEA0027DefenseTechnologyRuntimeTests
    {
        private const BindingFlags InstanceAny =
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic;

        [Test]
        public void OverloadFreezesWhenPausedAndPublishesImmutablePhase()
        {
            var runtime = new SingleCityDefenseTechnologyRuntime();
            runtime.Configure(new SingleCityDefenseTechnologyUnlocks(
                energyOverload: true));

            Assert.That(runtime.TryActivateOverload(
                "tower.laser.1",
                BuildingCatalog.LaserTower.Id.Value), Is.True);
            Assert.That(runtime.ResolveTowerFireRateMultiplier(
                "tower.laser.1"), Is.EqualTo(2f));
            Assert.That(runtime.ResolveTowerDamageMultiplier(
                "tower.laser.1",
                BuildingCatalog.LaserTower.Id.Value,
                null), Is.EqualTo(1.3f));

            runtime.Advance(2f, paused: true);
            Assert.That(runtime.Snapshot.Overloads.Single().BoostRemaining,
                Is.EqualTo(TechnologyOverloadModel.BoostSeconds));

            runtime.Advance(TechnologyOverloadModel.BoostSeconds, false);
            Assert.That(runtime.Snapshot.Overloads.Single().Phase,
                Is.EqualTo(TechnologyOverloadPhase.Lockout));
            Assert.That(runtime.ResolveTowerFireRateMultiplier(
                "tower.laser.1"), Is.Zero);

            runtime.Advance(TechnologyOverloadModel.LockoutSeconds, false);
            Assert.That(runtime.Snapshot.Overloads.Single().Phase,
                Is.EqualTo(TechnologyOverloadPhase.Cooldown));
            Assert.That(
                () => ((IList<SingleCityDefenseOverloadSnapshot>)runtime
                    .Snapshot.Overloads).Add(null),
                Throws.TypeOf<NotSupportedException>());
        }

        [Test]
        public void SwordIntentExecutesAtTwentyStacksAndClears()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(swordIntent: true),
                Enemy("enemy.1", EnemyCatalog.CrystalBeast, 0f, 0f));
            SingleCityDefenseTechnologyHitResult result = null;

            for (ulong hit = 1; hit <= SwordIntentModel.MaximumStacks; hit++)
            {
                if (hit > 1) runtime.Advance(1f, paused: false);
                result = runtime.ApplyTowerHit(
                    "tower.sword.1",
                    BuildingCatalog.SwordArrayTower.Id.Value,
                    "enemy.1",
                    primaryAppliedDamage: 1,
                    elapsedSincePreviousHit: 1f,
                    stableHitSequence: hit);
            }

            Assert.That(result.TrueDamage,
                Is.EqualTo(EnemyCatalog.CrystalBeast.MaximumHealth));
            Assert.That(runtime.Snapshot.Enemies.Single().SwordIntentStacks,
                Is.Zero);
        }

        [Test]
        public void SwordIntentEmitterUsesUnpausedRuleTimeAndRoundTripsCooldown()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(swordIntent: true),
                Enemy("enemy.1", EnemyCatalog.CrystalBeast, 0f, 0f));

            runtime.ApplyTowerHit(
                "tower.sword.1", BuildingCatalog.SwordArrayTower.Id.Value,
                "enemy.1", 1, elapsedSincePreviousHit: 99f,
                stableHitSequence: 1ul);
            runtime.ApplyTowerHit(
                "tower.sword.1", BuildingCatalog.SwordArrayTower.Id.Value,
                "enemy.1", 1, elapsedSincePreviousHit: 99f,
                stableHitSequence: 2ul);
            Assert.That(runtime.Snapshot.Enemies.Single().SwordIntentStacks,
                Is.EqualTo(1),
                "Hit arguments must not advance authoritative rule time.");

            runtime.Advance(.75f, paused: false);
            SingleCityDefenseTechnologyPersistenceSnapshot saved =
                runtime.CaptureForPersistence();
            Assert.That(EmitterCooldown(
                    saved,
                    "SwordIntentEmitters"),
                Is.EqualTo(.25f).Within(.0001f));

            var restored = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(swordIntent: true),
                Enemy("enemy.1", EnemyCatalog.CrystalBeast, 0f, 0f));
            Assert.That(restored.TryRestore(saved, out string error),
                Is.True, error);
            restored.ApplyTowerHit(
                "tower.sword.1", BuildingCatalog.SwordArrayTower.Id.Value,
                "enemy.1", 1, 99f, 3ul);
            Assert.That(restored.Snapshot.Enemies.Single().SwordIntentStacks,
                Is.EqualTo(1), "Loading must not grant an early stack.");

            restored.Advance(.25f, paused: true);
            restored.ApplyTowerHit(
                "tower.sword.1", BuildingCatalog.SwordArrayTower.Id.Value,
                "enemy.1", 1, 99f, 4ul);
            Assert.That(restored.Snapshot.Enemies.Single().SwordIntentStacks,
                Is.EqualTo(1), "Pause must freeze emitter cooldowns.");

            restored.Advance(.25f, paused: false);
            restored.ApplyTowerHit(
                "tower.sword.1", BuildingCatalog.SwordArrayTower.Id.Value,
                "enemy.1", 1, 0f, 5ul);
            Assert.That(restored.Snapshot.Enemies.Single().SwordIntentStacks,
                Is.EqualTo(2));
        }

        [Test]
        public void InfectionEmitterUsesUnpausedRuleTimeAndRoundTripsCooldown()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(infection: true),
                Enemy("enemy.1", EnemyCatalog.CrystalBeast, 0f, 0f));
            runtime.ApplyTowerHit(
                "tower.spore.1", BuildingCatalog.SporeTower.Id.Value,
                "enemy.1", 1, 99f, 1ul);
            runtime.Advance(.6f, paused: false);

            SingleCityDefenseTechnologyPersistenceSnapshot saved =
                runtime.CaptureForPersistence();
            Assert.That(EmitterCooldown(
                    saved,
                    "InfectionEmitters"),
                Is.EqualTo(.4f).Within(.0001f));

            var restored = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(infection: true),
                Enemy("enemy.1", EnemyCatalog.CrystalBeast, 0f, 0f));
            Assert.That(restored.TryRestore(saved, out string error),
                Is.True, error);
            restored.ApplyTowerHit(
                "tower.spore.1", BuildingCatalog.SporeTower.Id.Value,
                "enemy.1", 1, 99f, 2ul);
            Assert.That(restored.Snapshot.Enemies.Single().InfectionStacks,
                Is.EqualTo(1));

            restored.Advance(.4f, paused: false);
            restored.ApplyTowerHit(
                "tower.spore.1", BuildingCatalog.SporeTower.Id.Value,
                "enemy.1", 1, 0f, 3ul);
            Assert.That(restored.Snapshot.Enemies.Single().InfectionStacks,
                Is.EqualTo(2));
        }

        [Test]
        public void InfectionTicksAndBurstSpreadsFiveStacksWithinThreeCells()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(infection: true),
                Enemy("enemy.source", EnemyCatalog.CrystalBeast, 0f, 0f),
                Enemy("enemy.near", EnemyCatalog.Gnawer, 3f, 0f),
                Enemy("enemy.far", EnemyCatalog.Gnawer, 3.01f, 0f));

            runtime.ApplyTowerHit(
                "tower.spore.1",
                BuildingCatalog.SporeTower.Id.Value,
                "enemy.source",
                1,
                1f,
                1ul);
            IReadOnlyList<SingleCityDefenseTechnologyDamageEvent> damage =
                runtime.Advance(1f, paused: false);
            Assert.That(damage.Single().TargetStableEnemyId,
                Is.EqualTo("enemy.source"));
            Assert.That(damage.Single().Damage,
                Is.EqualTo((int)Math.Ceiling(
                    EnemyCatalog.CrystalBeast.MaximumHealth * .02d)));

            SingleCityDefenseTechnologyHitResult burst = null;
            for (ulong hit = 2; hit <= 10; hit++)
            {
                runtime.Advance(1f, paused: false);
                burst = runtime.ApplyTowerHit(
                    "tower.spore.1",
                    BuildingCatalog.SporeTower.Id.Value,
                    "enemy.source",
                    1,
                    1f,
                    hit);
            }

            Assert.That(burst.InfectionBurst, Is.True);
            Assert.That(burst.SpreadTargetStableIds,
                Is.EqualTo(new[] { "enemy.near" }));
            Assert.That(runtime.Snapshot.Enemies.Single(
                value => value.StableEnemyId == "enemy.near")
                .InfectionStacks, Is.EqualTo(5));
            Assert.That(runtime.Snapshot.Enemies.Single(
                value => value.StableEnemyId == "enemy.far")
                .InfectionStacks, Is.Zero);
        }

        [Test]
        public void InfectionSpreadChainsDeterministicallyAtThresholdOncePerTarget()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(infection: true),
                Enemy("enemy.source", EnemyCatalog.CrystalBeast, 0f, 0f),
                Enemy("enemy.bridge", EnemyCatalog.Gnawer, 3f, 0f),
                Enemy("enemy.chain", EnemyCatalog.Gnawer, 6f, 0f));

            for (ulong hit = 1; hit <= 5; hit++)
            {
                runtime.ApplyTowerHit(
                    "tower.seed", BuildingCatalog.SporeTower.Id.Value,
                    "enemy.bridge", 1, 0f, hit);
                runtime.Advance(1f, paused: false);
            }
            Assert.That(runtime.Snapshot.Enemies.Single(value =>
                value.StableEnemyId == "enemy.bridge").InfectionStacks,
                Is.EqualTo(5));

            SingleCityDefenseTechnologyHitResult burst = null;
            for (ulong hit = 1; hit <= 10; hit++)
            {
                burst = runtime.ApplyTowerHit(
                    "tower.source", BuildingCatalog.SporeTower.Id.Value,
                    "enemy.source", 1, 0f, hit);
                if (hit < 10) runtime.Advance(1f, paused: false);
            }

            Assert.That(burst.InfectionBurst, Is.True);
            Assert.That(burst.SpreadTargetStableIds,
                Is.EqualTo(new[]
                {
                    "enemy.bridge",
                    "enemy.chain",
                    "enemy.source",
                }));
            Assert.That(runtime.Snapshot.Enemies.Single(value =>
                value.StableEnemyId == "enemy.bridge").InfectionStacks,
                Is.Zero);
            Assert.That(runtime.Snapshot.Enemies.Single(value =>
                value.StableEnemyId == "enemy.chain").InfectionStacks,
                Is.EqualTo(5));
            Assert.That(runtime.Snapshot.Enemies.Single(value =>
                value.StableEnemyId == "enemy.source").InfectionStacks,
                Is.EqualTo(5),
                "A completed source may receive spread, but must not burst " +
                "twice in the same deterministic chain.");
        }

        [Test]
        public void ResonanceSynchronizesThirtyPercentWithoutRecursiveEvents()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(resonance: true),
                Enemy("enemy.a", EnemyCatalog.Gnawer, 0f, 0f),
                Enemy("enemy.b", EnemyCatalog.Gnawer, 1f, 0f));
            runtime.ApplyTowerHit(
                "tower.mind.1", BuildingCatalog.MindSpire.Id.Value,
                "enemy.a", 10, .1f, 1ul);
            runtime.ApplyTowerHit(
                "tower.mind.1", BuildingCatalog.MindSpire.Id.Value,
                "enemy.b", 10, .1f, 2ul);

            SingleCityDefenseTechnologyHitResult result =
                runtime.ApplyTowerHit(
                    "tower.mind.1", BuildingCatalog.MindSpire.Id.Value,
                    "enemy.a", 20, .1f, 3ul);

            Assert.That(result.SynchronizedDamageEvents.Count, Is.EqualTo(1));
            Assert.That(result.SynchronizedDamageEvents[0]
                .TargetStableEnemyId, Is.EqualTo("enemy.b"));
            Assert.That(result.SynchronizedDamageEvents[0].Damage,
                Is.EqualTo(6));
            Assert.That(result.SynchronizedDamageEvents[0].IsSynchronized,
                Is.True,
                "The consumer must not feed synchronized damage back into " +
                "ApplyTowerHit and recursively fan out again.");
        }

        [Test]
        public void MindControlIsDeterministicAndHeavyTargetsStayImmune()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(mindControl: true),
                Enemy("enemy.light", EnemyCatalog.Gnawer, 0f, 0f),
                Enemy("enemy.heavy", EnemyCatalog.CrystalBeast, 1f, 0f));
            bool converted = false;
            for (ulong hit = 1; hit <= 100 && !converted; hit++)
            {
                converted = runtime.ApplyTowerHit(
                    "tower.mind.1", BuildingCatalog.MindSpire.Id.Value,
                    "enemy.light", 1, .1f, hit).Controlled;
            }
            Assert.That(converted, Is.True);

            for (ulong hit = 1; hit <= 100; hit++)
            {
                Assert.That(runtime.ApplyTowerHit(
                    "tower.mind.1", BuildingCatalog.MindSpire.Id.Value,
                    "enemy.heavy", 1, .1f, hit).Controlled, Is.False);
            }
        }

        [Test]
        public void MindControlOnlyCommitsAfterCampaignAuthorityConfirms()
        {
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(mindControl: true),
                Enemy("enemy.light", EnemyCatalog.Gnawer, 0f, 0f));
            SingleCityDefenseTechnologyHitResult proposal = null;
            for (ulong hit = 1; hit <= 100 &&
                 (proposal == null || !proposal.Controlled); hit++)
            {
                proposal = runtime.ApplyTowerHit(
                    "tower.mind.1", BuildingCatalog.MindSpire.Id.Value,
                    "enemy.light", 1, 0f, hit);
            }

            Assert.That(proposal.Controlled, Is.True);
            Assert.That(runtime.Snapshot.Enemies.Single().Controlled, Is.False,
                "A deterministic roll is only a conversion proposal.");
            Assert.That(CommitMindControl(runtime, "enemy.light"), Is.True);
            Assert.That(runtime.Snapshot.Enemies.Single().Controlled, Is.True);

            runtime.SynchronizeEnemies(
                Array.Empty<SingleCityDefenseEnemySnapshot>());
            Assert.That(runtime.Snapshot.Enemies, Is.Empty,
                "Destroyed or out-of-authority controlled targets must be " +
                "removed instead of becoming save ghosts.");
            Assert.That(runtime.CaptureForPersistence().Enemies, Is.Empty);
        }

        [Test]
        public void AcidDamageBonusOnlyTargetsHeavyArmor()
        {
            ResearchEffectSnapshot effects = ResearchEffectResolver.Resolve(
                new[] { "core.research.acid-spit" });
            var runtime = RuntimeWithEnemies(
                new SingleCityDefenseTechnologyUnlocks(
                    acidSpit: true,
                    acidHeavyDamageMultiplier: effects
                        .ResolveHeavyArmorDamageMultiplier(
                            BuildingCatalog.AcidTower.Id.Value)),
                Enemy("enemy.light", EnemyCatalog.Gnawer, 0f, 0f),
                Enemy("enemy.heavy", EnemyCatalog.CrystalBeast, 1f, 0f));

            Assert.That(runtime.ResolveTowerDamageMultiplier(
                "tower.acid.1", BuildingCatalog.AcidTower.Id.Value,
                "enemy.light"), Is.EqualTo(1f));
            Assert.That(runtime.ResolveTowerDamageMultiplier(
                "tower.acid.1", BuildingCatalog.AcidTower.Id.Value,
                "enemy.heavy"), Is.EqualTo(1.3f));
        }

        [Test]
        public void BuildingHealthConsumesTalismanShieldAndRepairRules()
        {
            GrayboxBuildingInstance3D wall = Instance(
                "building.wall.1", BuildingCatalog.Wall, 1, 1);
            GrayboxBuildingInstance3D warehouse = Instance(
                "building.warehouse.1", BuildingCatalog.Warehouse, 2, 1);
            GrayboxBuildingInstance3D repair = Instance(
                "building.repair.1", BuildingCatalog.AutomatedRepairBay,
                1, 2);
            GrayboxBuildingInstance3D shield = Instance(
                "building.shield.1", BuildingCatalog.ShieldGenerator, 2, 2);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { wall, warehouse, repair, shield });
            health.ConfigureTechnologySupport(
                wallPhysicalDamageMultiplier: ResearchEffectResolver.Resolve(
                    new[] { "core.research.talisman-basics" })
                    .ResolvePhysicalDamageTakenMultiplier(
                        BuildingCatalog.Wall.Id.Value),
                automatedRepair: true,
                mindShield: true);

            Assert.That(health.TryApplyDamage(
                wall.StableInstanceId,
                100,
                DamageType.Physical,
                out int wallDamage,
                out _), Is.True);
            Assert.That(wallDamage, Is.EqualTo(80));
            Assert.That(health.TryGrantShield(
                wall.StableInstanceId, 50, 100), Is.EqualTo(50));
            health.TryApplyDamage(
                wall.StableInstanceId,
                30,
                DamageType.Physical,
                out int absorbed,
                out _);
            Assert.That(absorbed, Is.EqualTo(24));

            health.TryApplyDamage(
                warehouse.StableInstanceId, 40, out _, out _);
            Assert.That(health.AdvanceTechnologySupport(
                5.1f, paused: false, coreX: 1f, coreZ: 1f,
                out int firstCoreShield), Is.GreaterThanOrEqualTo(20));
            Assert.That(firstCoreShield, Is.Zero);
            health.AdvanceTechnologySupport(
                2.9f, paused: false, coreX: 1f, coreZ: 1f,
                out int coreShield);
            Assert.That(coreShield, Is.EqualTo(20));

            GrayboxBuildingTechnologyStateSnapshot3D warehouseState =
                health.TechnologySnapshot.Buildings.Single(
                    value => value.StableInstanceId ==
                        warehouse.StableInstanceId);
            Assert.That(warehouseState.CurrentHealth,
                Is.EqualTo(BuildingCatalog.Warehouse.MaximumHealth - 20));
            GrayboxBuildingTechnologyStateSnapshot3D wallState =
                health.TechnologySnapshot.Buildings.Single(
                    value => value.StableInstanceId == wall.StableInstanceId);
            Assert.That(wallState.Shield, Is.EqualTo(46));
        }

        [Test]
        public void BuildingTechnologySourcesRequireFormalOperationalTruth()
        {
            GrayboxBuildingInstance3D target = Instance(
                "building.target.1", BuildingCatalog.Warehouse, 10, 10);
            GrayboxBuildingInstance3D repair = Instance(
                "building.repair.1", BuildingCatalog.AutomatedRepairBay,
                11, 10);
            GrayboxBuildingInstance3D shield = Instance(
                "building.shield.1", BuildingCatalog.ShieldGenerator,
                11, 11);
            var instances = new[] { target, repair, shield };
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(instances);
            health.ConfigureTechnologySupport(
                wallPhysicalDamageMultiplier: 1f,
                automatedRepair: true,
                mindShield: true);
            health.TryApplyDamage(
                target.StableInstanceId, 100, out _, out _);

            MethodInfo synchronizeOperations =
                typeof(GrayboxBuildingHealthRuntime3D).GetMethod(
                    "SynchronizeTechnologyOperationalState",
                    InstanceAny);
            Assert.That(synchronizeOperations, Is.Not.Null,
                "Building technology must consume the shared formal " +
                "operational/logistics truth through one narrow boundary.");

            InvokeOperationalSync(
                synchronizeOperations,
                health,
                instances,
                radius: BuildingRangeRules.InitialGroundRadius,
                _ => false);
            health.AdvanceTechnologySupport(
                8f, false, 10f, 10f, out int disconnectedCoreShield);
            health.AdvanceRegeneration(
                1f, tissueRegeneration: true, carapaceGrowth: false,
                cityStorage: null);
            GrayboxBuildingTechnologyStateSnapshot3D disconnected =
                health.TechnologySnapshot.Buildings.Single(value =>
                    value.StableInstanceId == target.StableInstanceId);
            Assert.That(disconnected.CurrentHealth,
                Is.EqualTo(BuildingCatalog.Warehouse.MaximumHealth - 100));
            Assert.That(disconnected.Shield, Is.Zero);
            Assert.That(disconnectedCoreShield, Is.Zero);

            InvokeOperationalSync(
                synchronizeOperations,
                health,
                instances,
                radius: 24,
                _ => true);
            health.AdvanceTechnologySupport(
                8f, false, 10f, 10f, out int pausedCoreShield);
            health.AdvanceRegeneration(
                1f, tissueRegeneration: true, carapaceGrowth: false,
                cityStorage: null);
            GrayboxBuildingTechnologyStateSnapshot3D paused =
                health.TechnologySnapshot.Buildings.Single(value =>
                    value.StableInstanceId == target.StableInstanceId);
            Assert.That(paused.CurrentHealth, Is.EqualTo(disconnected.CurrentHealth));
            Assert.That(paused.Shield, Is.Zero);
            Assert.That(pausedCoreShield, Is.Zero);

            InvokeOperationalSync(
                synchronizeOperations,
                health,
                instances,
                radius: 24,
                _ => false);
            Assert.That(GrayboxBuildingOperationalAccess3D.CanRunLocally(
                repair, CityMode.Fortress), Is.True);
            Assert.That(GrayboxBuildingOperationalAccess3D
                .IsLogisticsConnected(
                    repair, CityMode.Fortress, 0, 0, 24), Is.True);
            Assert.That(GrayboxBuildingOperationalAccess3D.CanRunLocally(
                shield, CityMode.Fortress), Is.True);
            Assert.That(GrayboxBuildingOperationalAccess3D
                .IsLogisticsConnected(
                    shield, CityMode.Fortress, 0, 0, 24), Is.True);
            health.AdvanceTechnologySupport(
                8f, false, 10f, 10f, out int runningCoreShield);
            health.AdvanceRegeneration(
                1f, tissueRegeneration: true, carapaceGrowth: false,
                cityStorage: null);
            GrayboxBuildingTechnologyStateSnapshot3D running =
                health.TechnologySnapshot.Buildings.Single(value =>
                    value.StableInstanceId == target.StableInstanceId);
            Assert.That(running.CurrentHealth, Is.GreaterThan(paused.CurrentHealth));
            Assert.That(running.Shield, Is.GreaterThan(0));
            Assert.That(runningCoreShield, Is.GreaterThan(0));
        }

        [Test]
        public void BuildingTechnologyRestoreSetsCoreShieldExactlyIncludingZero()
        {
            var runtime = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(Array.Empty<GrayboxBuildingInstance3D>());
            SetRuntimeField(runtime, "campaign", campaign);
            SetRuntimeField(runtime, "campaignBuildingHealth", health);
            campaign.GrantCoreShield(80);

            Assert.That(runtime.TryRestoreBuildingTechnologyForPersistence(
                Array.Empty<GrayboxBuildingTechnologyStateSnapshot3D>(),
                20,
                out string error), Is.True, error);
            Assert.That(runtime.ActiveCampaignSnapshot.CoreShield, Is.EqualTo(20));

            Assert.That(runtime.TryRestoreBuildingTechnologyForPersistence(
                Array.Empty<GrayboxBuildingTechnologyStateSnapshot3D>(),
                0,
                out error), Is.True, error);
            Assert.That(runtime.ActiveCampaignSnapshot.CoreShield, Is.Zero);
        }

        [Test]
        public void BuildingTechnologyRestoreSetsTerminalCoreShieldExactly()
        {
            var runtime = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(Array.Empty<GrayboxBuildingInstance3D>());
            SetRuntimeField(runtime, "campaign", campaign);
            SetRuntimeField(runtime, "campaignBuildingHealth", health);
            campaign.GrantCoreShield(80);
            SetCampaignField(
                campaign,
                "result",
                SingleCityDefenseCampaignResult.Victory);
            SetCampaignField(
                campaign,
                "phase",
                SingleCityDefenseCampaignPhase.Victory);

            Assert.That(runtime.TryRestoreBuildingTechnologyForPersistence(
                Array.Empty<GrayboxBuildingTechnologyStateSnapshot3D>(),
                20,
                out string error), Is.True, error);
            Assert.That(runtime.ActiveCampaignSnapshot.CoreShield, Is.EqualTo(20));

            Assert.That(runtime.TryRestoreBuildingTechnologyForPersistence(
                Array.Empty<GrayboxBuildingTechnologyStateSnapshot3D>(),
                0,
                out error), Is.True, error);
            Assert.That(runtime.ActiveCampaignSnapshot.CoreShield, Is.Zero);
        }

        [Test]
        public void MainAndPressureCampaignTechnologyStatesStayIsolated()
        {
            var runtime = new GrayboxDefenseRuntime3D(0f, 0f, 2f, 2f);
            runtime.ConfigureTechnologyStates(
                new SingleCityDefenseTechnologyUnlocks(
                    energyOverload: true));
            SetRuntimeField(
                runtime,
                "campaign",
                new SingleCityDefenseCampaignModel(0f, 0f));
            var mainState = new SingleCityDefenseTechnologyPersistenceSnapshot(
                new[]
                {
                    new SingleCityDefenseOverloadPersistenceState(
                        "tower.main", 30f, 5f, 0f),
                },
                Array.Empty<
                    SingleCityDefenseEnemyTechnologyPersistenceState>());
            Assert.That(runtime.TryRestoreTechnologyForPersistence(
                mainState, out string error), Is.True, error);
            Assert.That(runtime.TechnologyState.Overloads.Single()
                .TowerStableId, Is.EqualTo("tower.main"));
            Assert.That(runtime.CaptureTechnologyForPersistence()
                .Overloads.Single().TowerStableId, Is.EqualTo("tower.main"));

            SetRuntimeField(
                runtime,
                "activePressureCampaign",
                new SingleCityDefenseCampaignModel(0f, 0f));
            Assert.That(runtime.TechnologyState.Overloads, Is.Empty,
                "Entering a pressure campaign must not expose main-campaign " +
                "overloads.");
            var pressureState =
                new SingleCityDefenseTechnologyPersistenceSnapshot(
                    new[]
                    {
                        new SingleCityDefenseOverloadPersistenceState(
                            "tower.pressure", 30f, 5f, 0f),
                    },
                    Array.Empty<
                        SingleCityDefenseEnemyTechnologyPersistenceState>());
            Assert.That(runtime.TryRestoreTechnologyForPersistence(
                pressureState, out error), Is.True, error);
            Assert.That(runtime.TechnologyState.Overloads.Single()
                .TowerStableId, Is.EqualTo("tower.pressure"));
            Assert.That(runtime.CaptureTechnologyForPersistence()
                .Overloads.Single().TowerStableId,
                Is.EqualTo("tower.pressure"),
                "Formal persistence must capture only the active pressure " +
                "campaign technology state.");

            Assert.That(runtime.ClearActivePressure(), Is.True);
            Assert.That(runtime.TechnologyState.Overloads.Single()
                .TowerStableId, Is.EqualTo("tower.main"));
            Assert.That(runtime.CaptureTechnologyForPersistence()
                .Overloads.Single().TowerStableId, Is.EqualTo("tower.main"));
        }

        private static void SetRuntimeField(
            GrayboxDefenseRuntime3D runtime,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(GrayboxDefenseRuntime3D).GetField(
                fieldName, InstanceAny);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(runtime, value);
        }

        private static void SetCampaignField(
            SingleCityDefenseCampaignModel campaign,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(SingleCityDefenseCampaignModel).GetField(
                fieldName,
                InstanceAny);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(campaign, value);
        }

        private static float EmitterCooldown(
            SingleCityDefenseTechnologyPersistenceSnapshot snapshot,
            string propertyName)
        {
            PropertyInfo property = snapshot.GetType().GetProperty(
                propertyName,
                InstanceAny);
            Assert.That(property, Is.Not.Null, propertyName);
            var values = property.GetValue(snapshot) as Array;
            Assert.That(values, Is.Not.Null, propertyName);
            Assert.That(values.Length, Is.EqualTo(1), propertyName);
            object value = values.GetValue(0);
            PropertyInfo cooldown = value.GetType().GetProperty(
                "CooldownRemaining",
                InstanceAny);
            Assert.That(cooldown, Is.Not.Null, "CooldownRemaining");
            return (float)cooldown.GetValue(value);
        }

        private static bool CommitMindControl(
            SingleCityDefenseTechnologyRuntime runtime,
            string stableEnemyId)
        {
            MethodInfo method = runtime.GetType().GetMethod(
                "TryCommitMindControl",
                InstanceAny);
            Assert.That(method, Is.Not.Null, "TryCommitMindControl");
            return (bool)method.Invoke(runtime, new object[] { stableEnemyId });
        }

        private static void InvokeOperationalSync(
            MethodInfo method,
            GrayboxBuildingHealthRuntime3D health,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            int radius,
            Func<string, bool> isPlayerPaused)
        {
            method.Invoke(
                health,
                new object[]
                {
                    instances,
                    CityMode.Fortress,
                    0,
                    0,
                    radius,
                    isPlayerPaused,
                });
        }

        private static SingleCityDefenseTechnologyRuntime RuntimeWithEnemies(
            SingleCityDefenseTechnologyUnlocks unlocks,
            params SingleCityDefenseEnemySnapshot[] enemies)
        {
            var runtime = new SingleCityDefenseTechnologyRuntime();
            runtime.Configure(unlocks);
            runtime.SynchronizeEnemies(enemies);
            return runtime;
        }

        private static SingleCityDefenseEnemySnapshot Enemy(
            string stableId,
            EnemyDefinition definition,
            float x,
            float z)
        {
            return new SingleCityDefenseEnemySnapshot(
                stableId,
                definition.Id.Value,
                spawnOrder: 0,
                x,
                z,
                definition.MaximumHealth);
        }

        private static GrayboxBuildingInstance3D Instance(
            string stableId,
            BuildingDefinition definition,
            int x,
            int y)
        {
            ConstructorInfo constructor = typeof(GrayboxBuildingInstance3D)
                .GetConstructor(
                    InstanceAny,
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
                    stableId,
                    new PlacedBuilding(definition, x, y),
                    new ConstructionProgress(definition.BuildSeconds),
                    default(ResourceNodeBinding),
                });
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete", InstanceAny);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, null);
            return instance;
        }
    }
}
