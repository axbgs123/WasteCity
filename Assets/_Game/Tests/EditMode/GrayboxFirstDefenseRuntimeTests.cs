using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxFirstDefenseRuntimeTests
    {
        private const float PositionTolerance = .001f;

        [Test]
        public void SynchronizeRetainsOnlyCompletedOwnedTurretsAndTriggersTutorialOnce()
        {
            GrayboxBuildingInstance3D turretB = CreateInstance(
                "building.instance.turret-b",
                BuildingCatalog.MachineGunTurret,
                x: 1,
                y: 0);
            GrayboxBuildingInstance3D turretA = CreateInstance(
                "building.instance.turret-a",
                BuildingCatalog.MachineGunTurret,
                x: 0,
                y: 0);
            GrayboxBuildingInstance3D wall = CreateInstance(
                "building.instance.wall",
                BuildingCatalog.Wall,
                x: 2,
                y: 0);
            Complete(wall);
            var runtime = Runtime();

            Synchronize(runtime, new[] { turretB, turretA, wall });
            Assert.That(runtime.Towers, Is.Empty);
            Assert.That(runtime.Snapshot.TutorialWaveTriggerCount, Is.Zero);

            Complete(turretB);
            Synchronize(runtime, new[] { turretB, turretA, wall });
            Assert.That(
                runtime.Towers,
                Is.InstanceOf<
                    IReadOnlyList<GrayboxDefenseTowerRuntimeState3D>>());
            Assert.That(
                runtime.Towers.Select(value => value.StableId),
                Is.EqualTo(new[] { turretB.StableInstanceId }));
            Assert.That(runtime.Snapshot.TutorialWaveTriggerCount, Is.EqualTo(1));
            Assert.That(runtime.Snapshot.WavePhase, Is.EqualTo(WavePhase.Warning));
            Assert.That(
                runtime.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(15f));

            Synchronize(runtime, new[] { turretB, turretA, wall });
            Complete(turretA);
            Synchronize(runtime, new[] { turretB, turretA, wall });
            Assert.That(
                runtime.Towers.Select(value => value.StableId),
                Is.EqualTo(new[]
                {
                    turretA.StableInstanceId,
                    turretB.StableInstanceId,
                }));
            Assert.That(runtime.Snapshot.TutorialWaveTriggerCount, Is.EqualTo(1));

            Abandon(turretB);
            Synchronize(runtime, new[] { turretB, turretA, wall });
            Assert.That(
                runtime.Towers.Select(value => value.StableId),
                Is.EqualTo(new[] { turretA.StableInstanceId }));

            Synchronize(runtime, Array.Empty<GrayboxBuildingInstance3D>());
            Assert.That(runtime.Towers, Is.Empty);
        }

        [Test]
        public void SharedOperationalRulesMatchProductionLogisticsEligibility()
        {
            GrayboxBuildingInstance3D turret = CreateInstance(
                "building.instance.turret-access",
                BuildingCatalog.MachineGunTurret,
                x: 7,
                y: 0);
            Assert.That(
                GrayboxBuildingOperationalAccess3D.CanRetainState(turret),
                Is.False);
            Complete(turret);
            Assert.That(
                GrayboxBuildingOperationalAccess3D.CanRetainState(turret),
                Is.True);
            Assert.That(
                GrayboxBuildingOperationalAccess3D.CanRunLocally(
                    turret,
                    CityMode.Fortress),
                Is.True);
            Assert.That(
                GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                    turret,
                    CityMode.Fortress,
                    cityX: 0,
                    cityY: 0,
                    groundRadius: BuildingRangeRules.InitialGroundRadius),
                Is.True);
            Assert.That(
                GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                    turret,
                    CityMode.Fortress,
                    cityX: -10,
                    cityY: 0,
                    groundRadius: BuildingRangeRules.InitialGroundRadius),
                Is.False);
            Assert.That(
                GrayboxBuildingOperationalAccess3D.IsLogisticsConnected(
                    turret,
                    CityMode.Mobile,
                    cityX: 0,
                    cityY: 0,
                    groundRadius: BuildingRangeRules.InitialGroundRadius),
                Is.False);
        }

        [Test]
        public void MobileGroundTurretFiresLocalAmmoWithoutCityResupply()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.turret-mobile-local-ammo",
                x: 0,
                y: 0);
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, new[] { turret }, CityMode.Fortress, 0, 0);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Towers.Single().Combat.Ammo, Is.EqualTo(30));
            Assert.That(
                storage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(10));
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);

            Synchronize(runtime, new[] { turret }, CityMode.Mobile, 0, 0);
            GrayboxDefenseTowerSnapshot3D mobile =
                runtime.Snapshot.Towers.Single();
            Assert.That(mobile.CanRunLocally, Is.True);
            Assert.That(mobile.Connected, Is.False);

            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot.Enemies, Is.Not.Empty);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: false), Is.True);
            int cityAmmoBefore =
                storage.GetNetworkAmount(ResourceIds.Ammunition);
            int localAmmoBefore = runtime.Snapshot.Towers.Single().Ammo;
            int enemyHealthBefore =
                runtime.Snapshot.Enemies[0].CurrentHealth;

            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseTowerSnapshot3D firing =
                runtime.Snapshot.Towers.Single();
            Assert.That(firing.CanRunLocally, Is.True);
            Assert.That(firing.Connected, Is.False);
            Assert.That(firing.Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(firing.TargetId, Is.Not.Null);
            Assert.That(firing.Ammo, Is.EqualTo(localAmmoBefore - 1));
            Assert.That(
                runtime.Snapshot.Enemies[0].CurrentHealth,
                Is.LessThan(enemyHealthBefore));
            Assert.That(
                storage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(cityAmmoBefore),
                "A mobile ground tower may spend only its existing local " +
                "cache because logistics are disconnected.");
        }

        [Test]
        public void CompleteRuntimeIsIndependentOfExternalTickPartition()
        {
            GrayboxBuildingInstance3D turretA = CompletedTurret(
                "building.instance.partition-a",
                x: 0,
                y: 0);
            GrayboxBuildingInstance3D turretB = CompletedTurret(
                "building.instance.partition-b",
                x: 1,
                y: 0);
            var instances = new[] { turretB, turretA };
            GrayboxDefenseRuntime3D whole = Runtime();
            GrayboxDefenseRuntime3D fixedFrames = Runtime();
            GrayboxDefenseRuntime3D irregularFrames = Runtime();
            Synchronize(whole, instances);
            Synchronize(fixedFrames, instances);
            Synchronize(irregularFrames, instances);
            using CityResourceStorageModel wholeStorage = StorageWithAmmo(1);
            using CityResourceStorageModel fixedStorage = StorageWithAmmo(1);
            using CityResourceStorageModel irregularStorage = StorageWithAmmo(1);

            whole.Tick(
                49.7f,
                globallyPaused: false,
                cityStorage: wholeStorage);
            for (int step = 0; step < 497; step++)
            {
                fixedFrames.Tick(
                    .1f,
                    globallyPaused: false,
                    cityStorage: fixedStorage);
            }
            for (int frame = 0; frame < 497; frame++)
            {
                irregularFrames.Tick(
                    .016f,
                    globallyPaused: false,
                    cityStorage: irregularStorage);
                irregularFrames.Tick(
                    .033f,
                    globallyPaused: false,
                    cityStorage: irregularStorage);
                irregularFrames.Tick(
                    .051f,
                    globallyPaused: false,
                    cityStorage: irregularStorage);
            }

            AssertEquivalentRuntime(
                whole.Snapshot,
                fixedFrames.Snapshot,
                tolerance: .0001f);
            AssertEquivalentRuntime(
                whole.Snapshot,
                irregularFrames.Snapshot,
                tolerance: .0001f);
            Assert.That(
                fixedStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(
                    wholeStorage.GetNetworkAmount(ResourceIds.Ammunition)));
            Assert.That(
                irregularStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(
                    wholeStorage.GetNetworkAmount(ResourceIds.Ammunition)));
            Assert.That(whole.Snapshot.SpawnedEnemyCount,
                Is.GreaterThanOrEqualTo(2));
            Assert.That(whole.Snapshot.CoreCurrentHealth,
                Is.LessThan(whole.Snapshot.CoreMaximumHealth));
        }

        [Test]
        public void DisconnectedAndEvacuationLockedTurretRetainsItsState()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.turret-retained",
                x: 7,
                y: 0);
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            var runtime = Runtime();
            Synchronize(runtime, new[] { turret }, CityMode.Fortress, 0, 0);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseTowerRuntimeState3D state = runtime.Towers.Single();
            Assert.That(state.Combat.Ammo, Is.EqualTo(30));

            state.Combat.Tick(
                1f,
                DurableLightEnemy("enemy.prepare-ammo", x: 8f, z: 0f),
                globallyPaused: false);
            Assert.That(state.Combat.Ammo, Is.EqualTo(29));

            Synchronize(runtime, new[] { turret }, CityMode.Fortress, -10, 0);
            Assert.That(runtime.Towers.Single(), Is.SameAs(state));
            Assert.That(runtime.Towers.Single().Combat.Ammo, Is.EqualTo(29));
            Assert.That(runtime.Snapshot.Towers.Single().Connected, Is.False);

            Synchronize(runtime, new[] { turret }, CityMode.Mobile, 0, 0);
            Assert.That(runtime.Towers.Single(), Is.SameAs(state));
            Assert.That(runtime.Towers.Single().Combat.Ammo, Is.EqualTo(29));

            Synchronize(runtime, new[] { turret }, CityMode.Fortress, 0, 0);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Towers.Single(), Is.SameAs(state));
            Assert.That(runtime.Towers.Single().Combat.Ammo, Is.EqualTo(30));

            SetEvacuationLocked(turret, true);
            Synchronize(runtime, new[] { turret }, CityMode.Fortress, 0, 0);
            Assert.That(runtime.Towers.Single(), Is.SameAs(state));
            Assert.That(
                GrayboxBuildingOperationalAccess3D.CanRunLocally(
                    turret,
                    CityMode.Fortress),
                Is.False);
            Assert.That(runtime.Towers.Single().CanRunLocally, Is.False);
            Assert.That(runtime.Towers.Single().Combat.Ammo, Is.EqualTo(30));
        }

        [Test]
        public void ConnectedTowerRefillsWithDefenseAttributionAndIdlesOnInternalAmmo()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.turret-supply",
                x: 0,
                y: 0);
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            ResourceChangeAttribution attribution = default;
            int attributedDelta = 0;
            storage.AttributedChanged += (resourceId, delta, value) =>
            {
                if (resourceId != ResourceIds.Ammunition) return;
                attributedDelta += delta;
                attribution = value;
            };
            var runtime = Runtime();
            Synchronize(runtime, new[] { turret });

            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseTowerSnapshot3D snapshot =
                runtime.Snapshot.Towers.Single();
            Assert.That(snapshot.StableId, Is.EqualTo(turret.StableInstanceId));
            Assert.That(snapshot.Ammo, Is.EqualTo(30));
            Assert.That(snapshot.AmmoCapacity, Is.EqualTo(30));
            Assert.That(snapshot.Connected, Is.True);
            Assert.That(snapshot.TargetId, Is.Null);
            Assert.That(snapshot.Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.NoTarget));
            Assert.That(attributedDelta, Is.EqualTo(-30));
            Assert.That(attribution.Kind,
                Is.EqualTo(ResourceChangeAttributionKind.Defense));
            Assert.That(attribution.ReferenceId,
                Is.EqualTo(turret.StableInstanceId));

            runtime.Tick(1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot.Towers.Single().Ammo, Is.EqualTo(30));
            Assert.That(
                storage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(10));
        }

        [Test]
        public void TutorialEnemyIsKilledAtTwentyDpsAndGlobalPauseFreezesEverything()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.turret-combat",
                x: 0,
                y: 0);
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            var runtime = Runtime();
            Synchronize(runtime, new[] { turret });

            runtime.Tick(100f, globallyPaused: true, cityStorage: storage);
            Assert.That(runtime.Snapshot.Towers.Single().Ammo, Is.Zero);
            Assert.That(runtime.Snapshot.WavePhase, Is.EqualTo(WavePhase.Warning));
            Assert.That(runtime.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(15f));
            Assert.That(runtime.Snapshot.Enemies, Is.Empty);

            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot.Towers.Single().Ammo, Is.EqualTo(30));
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot.Enemies, Has.Count.EqualTo(1));
            GrayboxDefenseEnemySnapshot3D spawned =
                runtime.Snapshot.Enemies.Single();
            Assert.That(spawned.CurrentHealth, Is.EqualTo(60));

            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: false), Is.True);
            int ammoBeforePause = runtime.Snapshot.Towers.Single().Ammo;
            int healthBeforePause = runtime.Snapshot.Enemies.Single().CurrentHealth;
            runtime.Tick(1f, globallyPaused: true, cityStorage: storage);
            Assert.That(runtime.Snapshot.Towers.Single().Ammo,
                Is.EqualTo(ammoBeforePause));
            Assert.That(runtime.Snapshot.Enemies.Single().CurrentHealth,
                Is.EqualTo(healthBeforePause));
            Assert.That(runtime.Snapshot.Enemies, Has.Count.EqualTo(1));

            runtime.Tick(1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseTowerSnapshot3D firing =
                runtime.Snapshot.Towers.Single();
            Assert.That(runtime.Snapshot.Enemies.Single().CurrentHealth,
                Is.EqualTo(40));
            Assert.That(firing.Ammo, Is.EqualTo(30));
            Assert.That(firing.TargetId, Is.EqualTo(spawned.StableId));
            Assert.That(firing.Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(
                storage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(9),
                "The weapon spends only its internal cache; the city change " +
                "comes from the next deterministic logistics refill step " +
                "with Defense attribution.");

            runtime.Tick(2f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot.Enemies, Is.Empty);
            Assert.That(runtime.Snapshot.DefeatedEnemyCount, Is.EqualTo(1));

            DefenseEnemyCombatModel stableB = DurableLightEnemy(
                "enemy.stable-b",
                x: 4f,
                z: 0f);
            DefenseEnemyCombatModel stableA = DurableLightEnemy(
                "enemy.stable-a",
                x: -4f,
                z: 0f);
            Assert.That(
                runtime.Towers.Single().Combat.AcquireTarget(
                    new[] { stableB, stableA }).StableId,
                Is.EqualTo("enemy.stable-a"));
        }

        [Test]
        public void MovingCorePreservesGrayboxWaveCombatStateAndOriginalSpawnPoint()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.turret-moving-core",
                x: 0,
                y: 0);
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            var runtime = Runtime();
            Synchronize(runtime, new[] { turret });

            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: false), Is.True);
            runtime.Tick(1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);

            GrayboxDefenseRuntimeSnapshot3D before = runtime.Snapshot;
            Assert.That(before.TutorialWaveTriggerCount, Is.EqualTo(1));
            Assert.That(before.SpawnedEnemyCount, Is.EqualTo(1));
            Assert.That(before.Enemies, Has.Count.EqualTo(1));
            Assert.That(before.Enemies[0].CurrentHealth, Is.EqualTo(40));
            string enemyStableId = before.Enemies[0].StableId;
            float enemyX = before.Enemies[0].X;

            runtime.SetCorePosition(x: 30f, z: 0f);

            GrayboxDefenseRuntimeSnapshot3D immediatelyAfter = runtime.Snapshot;
            Assert.That(immediatelyAfter.TutorialWaveTriggerCount,
                Is.EqualTo(before.TutorialWaveTriggerCount));
            Assert.That(immediatelyAfter.WavePhase,
                Is.EqualTo(before.WavePhase));
            Assert.That(immediatelyAfter.WarningRemainingSeconds,
                Is.EqualTo(before.WarningRemainingSeconds)
                    .Within(PositionTolerance));
            Assert.That(immediatelyAfter.SpawnedEnemyCount,
                Is.EqualTo(before.SpawnedEnemyCount));
            Assert.That(immediatelyAfter.CoreCurrentHealth,
                Is.EqualTo(before.CoreCurrentHealth));
            Assert.That(
                immediatelyAfter.Enemies.Select(enemy => enemy.StableId),
                Is.EqualTo(before.Enemies.Select(enemy => enemy.StableId)));
            Assert.That(
                immediatelyAfter.Enemies.Select(enemy => enemy.CurrentHealth),
                Is.EqualTo(before.Enemies.Select(enemy => enemy.CurrentHealth)));

            runtime.Tick(1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseRuntimeSnapshot3D retargeted = runtime.Snapshot;
            GrayboxDefenseEnemySnapshot3D existing = retargeted.Enemies
                .Single(enemy => enemy.StableId == enemyStableId);
            Assert.That(existing.X, Is.GreaterThan(enemyX));
            Assert.That(existing.CurrentHealth, Is.EqualTo(40));
            Assert.That(retargeted.CoreCurrentHealth,
                Is.EqualTo(before.CoreCurrentHealth));

            runtime.Tick(2.9f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseEnemySnapshot3D nextSpawn = runtime.Snapshot.Enemies
                .Single(enemy => enemy.SpawnOrder == 1);
            Assert.That(nextSpawn.X, Is.EqualTo(9f).Within(PositionTolerance));
            Assert.That(nextSpawn.Z, Is.Zero.Within(PositionTolerance));
        }

        [Test]
        public void SnapshotDistinguishesAllApprovedTowerStatuses()
        {
            GrayboxBuildingInstance3D loadedTurret = CompletedTurret(
                "building.instance.turret-loaded",
                x: 0,
                y: 0);
            var loadedInventory = new ResourceInventory(500);
            loadedInventory.Add(ResourceIds.Ammunition, 30);
            using var loadedStorage = new CityResourceStorageModel(
                loadedInventory,
                150);
            var loadedRuntime = Runtime();
            Synchronize(loadedRuntime, new[] { loadedTurret });
            loadedRuntime.Tick(
                .1f,
                globallyPaused: false,
                cityStorage: loadedStorage);
            AssertTowerSnapshot(
                loadedRuntime.Snapshot.Towers.Single(),
                loadedTurret.StableInstanceId,
                ammo: 30,
                connected: true,
                playerPaused: false,
                targetId: null,
                status: GrayboxDefenseTowerStatus3D.NoTarget);

            Assert.That(loadedRuntime.TrySetPlayerPaused(
                loadedTurret.StableInstanceId,
                paused: true), Is.True);
            AssertTowerSnapshot(
                loadedRuntime.Snapshot.Towers.Single(),
                loadedTurret.StableInstanceId,
                ammo: 30,
                connected: true,
                playerPaused: true,
                targetId: null,
                status: GrayboxDefenseTowerStatus3D.PlayerPaused);

            GrayboxBuildingInstance3D missingTurret = CompletedTurret(
                "building.instance.turret-missing",
                x: 0,
                y: 0);
            using var emptyStorage = new CityResourceStorageModel(
                new ResourceInventory(500),
                150);
            var missingRuntime = Runtime();
            Synchronize(missingRuntime, new[] { missingTurret });
            missingRuntime.Tick(
                20.1f,
                globallyPaused: false,
                cityStorage: emptyStorage);
            Assert.That(missingRuntime.Snapshot.Enemies, Is.Not.Empty);
            string missingTargetId =
                missingRuntime.Snapshot.Enemies[0].StableId;
            AssertTowerSnapshot(
                missingRuntime.Snapshot.Towers.Single(),
                missingTurret.StableInstanceId,
                ammo: 0,
                connected: true,
                playerPaused: false,
                targetId: missingTargetId,
                status: GrayboxDefenseTowerStatus3D.MissingAmmunition);

            GrayboxBuildingInstance3D disconnectedTurret = CompletedTurret(
                "building.instance.turret-disconnected",
                x: 9,
                y: 0);
            var availableInventory = new ResourceInventory(500);
            availableInventory.Add(ResourceIds.Ammunition, 30);
            using var availableStorage = new CityResourceStorageModel(
                availableInventory,
                150);
            var disconnectedRuntime = Runtime();
            Synchronize(disconnectedRuntime, new[] { disconnectedTurret });
            disconnectedRuntime.Tick(
                20.1f,
                globallyPaused: false,
                cityStorage: availableStorage);
            Assert.That(disconnectedRuntime.Snapshot.Enemies, Is.Not.Empty);
            string disconnectedTargetId =
                disconnectedRuntime.Snapshot.Enemies[0].StableId;
            AssertTowerSnapshot(
                disconnectedRuntime.Snapshot.Towers.Single(),
                disconnectedTurret.StableInstanceId,
                ammo: 0,
                connected: false,
                playerPaused: false,
                targetId: disconnectedTargetId,
                status: GrayboxDefenseTowerStatus3D.OutOfLogistics);
        }

        private static GrayboxDefenseRuntime3D Runtime()
        {
            return new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 9f,
                spawnZ: 0f);
        }

        private static CityResourceStorageModel StorageWithAmmo(int amount)
        {
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, amount);
            return new CityResourceStorageModel(inventory, 150);
        }

        private static void AssertEquivalentRuntime(
            GrayboxDefenseRuntimeSnapshot3D expected,
            GrayboxDefenseRuntimeSnapshot3D actual,
            float tolerance)
        {
            Assert.That(actual.TutorialWaveTriggerCount,
                Is.EqualTo(expected.TutorialWaveTriggerCount));
            Assert.That(actual.WavePhase, Is.EqualTo(expected.WavePhase));
            Assert.That(actual.WarningRemainingSeconds,
                Is.EqualTo(expected.WarningRemainingSeconds).Within(tolerance));
            Assert.That(actual.SpawnedEnemyCount,
                Is.EqualTo(expected.SpawnedEnemyCount));
            Assert.That(actual.AliveEnemyCount,
                Is.EqualTo(expected.AliveEnemyCount));
            Assert.That(actual.DefeatedEnemyCount,
                Is.EqualTo(expected.DefeatedEnemyCount));
            Assert.That(actual.CoreMaximumHealth,
                Is.EqualTo(expected.CoreMaximumHealth));
            Assert.That(actual.CoreCurrentHealth,
                Is.EqualTo(expected.CoreCurrentHealth));
            Assert.That(actual.IsCoreDestroyed,
                Is.EqualTo(expected.IsCoreDestroyed));
            Assert.That(actual.Towers, Has.Count.EqualTo(expected.Towers.Count));
            for (int index = 0; index < expected.Towers.Count; index++)
            {
                GrayboxDefenseTowerSnapshot3D expectedTower =
                    expected.Towers[index];
                GrayboxDefenseTowerSnapshot3D actualTower =
                    actual.Towers[index];
                Assert.That(actualTower.StableId,
                    Is.EqualTo(expectedTower.StableId));
                Assert.That(actualTower.Ammo, Is.EqualTo(expectedTower.Ammo));
                Assert.That(actualTower.AmmoCapacity,
                    Is.EqualTo(expectedTower.AmmoCapacity));
                Assert.That(actualTower.Range,
                    Is.EqualTo(expectedTower.Range).Within(tolerance));
                Assert.That(actualTower.Connected,
                    Is.EqualTo(expectedTower.Connected));
                Assert.That(actualTower.CanRunLocally,
                    Is.EqualTo(expectedTower.CanRunLocally));
                Assert.That(actualTower.PlayerPaused,
                    Is.EqualTo(expectedTower.PlayerPaused));
                Assert.That(actualTower.TargetId,
                    Is.EqualTo(expectedTower.TargetId));
                Assert.That(actualTower.Status,
                    Is.EqualTo(expectedTower.Status));
            }
            Assert.That(actual.Enemies,
                Has.Count.EqualTo(expected.Enemies.Count));
            for (int index = 0; index < expected.Enemies.Count; index++)
            {
                GrayboxDefenseEnemySnapshot3D expectedEnemy =
                    expected.Enemies[index];
                GrayboxDefenseEnemySnapshot3D actualEnemy =
                    actual.Enemies[index];
                Assert.That(actualEnemy.StableId,
                    Is.EqualTo(expectedEnemy.StableId));
                Assert.That(actualEnemy.SpawnOrder,
                    Is.EqualTo(expectedEnemy.SpawnOrder));
                Assert.That(actualEnemy.X,
                    Is.EqualTo(expectedEnemy.X).Within(tolerance));
                Assert.That(actualEnemy.Z,
                    Is.EqualTo(expectedEnemy.Z).Within(tolerance));
                Assert.That(actualEnemy.CurrentHealth,
                    Is.EqualTo(expectedEnemy.CurrentHealth));
                Assert.That(actualEnemy.IsAttackingCore,
                    Is.EqualTo(expectedEnemy.IsAttackingCore));
                Assert.That(actualEnemy.TargetName,
                    Is.EqualTo(expectedEnemy.TargetName));
                Assert.That(actualEnemy.DistanceToCore,
                    Is.EqualTo(expectedEnemy.DistanceToCore).Within(tolerance));
            }
        }

        private static void Synchronize(
            GrayboxDefenseRuntime3D runtime,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode = CityMode.Fortress,
            int cityX = 0,
            int cityY = 0)
        {
            runtime.Synchronize(
                instances,
                cityMode,
                cityX,
                cityY,
                BuildingRangeRules.InitialGroundRadius);
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
            string stableInstanceId,
            int x,
            int y)
        {
            GrayboxBuildingInstance3D instance = CreateInstance(
                stableInstanceId,
                BuildingCatalog.MachineGunTurret,
                x,
                y);
            Complete(instance);
            return instance;
        }

        private static GrayboxBuildingInstance3D CreateInstance(
            string stableInstanceId,
            BuildingDefinition definition,
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
            return (GrayboxBuildingInstance3D)constructor.Invoke(new object[]
            {
                stableInstanceId,
                new PlacedBuilding(
                    definition,
                    x,
                    y,
                    BuildingSite.Ground,
                    BuildingOrientation.North),
                new ConstructionProgress(definition.BuildSeconds),
                ResourceNodeBinding.None,
            });
        }

        private static DefenseEnemyCombatModel DurableLightEnemy(
            string stableId,
            float x,
            float z)
        {
            var definition = new EnemyDefinition(
                "test.enemy.runtime-durable-light",
                "运行时高血量轻甲测试敌人",
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
                x,
                z);
        }

        private static void AssertTowerSnapshot(
            GrayboxDefenseTowerSnapshot3D snapshot,
            string stableId,
            int ammo,
            bool connected,
            bool playerPaused,
            string targetId,
            GrayboxDefenseTowerStatus3D status)
        {
            Assert.That(snapshot.StableId, Is.EqualTo(stableId));
            Assert.That(snapshot.Ammo, Is.EqualTo(ammo));
            Assert.That(snapshot.AmmoCapacity, Is.EqualTo(30));
            Assert.That(snapshot.Connected, Is.EqualTo(connected));
            Assert.That(snapshot.PlayerPaused, Is.EqualTo(playerPaused));
            Assert.That(snapshot.TargetId, Is.EqualTo(targetId));
            Assert.That(snapshot.Status, Is.EqualTo(status));
        }

        private static void Complete(GrayboxBuildingInstance3D instance)
        {
            Invoke(instance, "Complete");
        }

        private static void SetEvacuationLocked(
            GrayboxBuildingInstance3D instance,
            bool locked)
        {
            Invoke(instance, "SetEvacuationLocked", locked);
        }

        private static void Abandon(GrayboxBuildingInstance3D instance)
        {
            Invoke(instance, "Abandon");
        }

        private static void Invoke(
            GrayboxBuildingInstance3D instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = typeof(GrayboxBuildingInstance3D).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(instance, arguments);
        }
    }
}
