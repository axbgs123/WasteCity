using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class FirstDefenseLoopTests
    {
        [Test]
        public void ApprovedResearchAndCombatCatalogsMatchTheFirstDefenseSlice()
        {
            Assert.That(
                DemoResearchCatalog.ReleaseState(
                    DemoResearchCatalog.AutomatedDefenseId),
                Is.EqualTo(DemoResearchReleaseState.Researchable));
            Assert.That(
                DemoResearchCatalog.ReleaseState(
                    DemoResearchCatalog.ReinforcedStructuresId),
                Is.EqualTo(DemoResearchReleaseState.PreviewOnly));
            Assert.That(
                DemoResearchCatalog.ReleaseState(
                    DemoResearchCatalog.LegacyAnalysisId),
                Is.EqualTo(DemoResearchReleaseState.PreviewOnly));

            BuildingDefinition building = BuildingCatalog.MachineGunTurret;
            Assert.That(
                building.Id.Value,
                Is.EqualTo("core.building.machine-gun-turret"));
            Assert.That(building.CostId, Is.EqualTo(ResourceIds.Alloy));
            Assert.That(building.Cost, Is.EqualTo(10));
            Assert.That(building.BuildSeconds, Is.EqualTo(10f));
            Assert.That(building.MaximumHealth, Is.EqualTo(250));

            DefenseTowerDefinition tower = DefenseTowerCatalog.For(
                building.Id.Value);
            Assert.That(tower, Is.Not.Null);
            Assert.That(tower.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(tower.DamagePerSecond, Is.EqualTo(20f));
            Assert.That(tower.Range, Is.EqualTo(10f));
            Assert.That(tower.ConsumableId, Is.EqualTo(ResourceIds.Ammunition));
            Assert.That(tower.SecondsPerConsumable, Is.EqualTo(3f));

            EnemyDefinition gnawer = EnemyCatalog.Gnawer;
            Assert.That(gnawer.Id.Value, Is.EqualTo("core.enemy.gnawer"));
            Assert.That(gnawer.MaximumHealth, Is.EqualTo(60));
            Assert.That(gnawer.MoveSpeed, Is.EqualTo(1.8f));
            Assert.That(gnawer.DamagePerSecond, Is.EqualTo(8f));
            Assert.That(gnawer.Armor, Is.EqualTo(ArmorType.Light));
        }

        [Test]
        public void TurretCapsInternalAmmoAndDoesNotSpendWithoutATarget()
        {
            var turret = new MachineGunTurretCombatModel(
                "building.instance.turret-001",
                x: 0f,
                z: 0f,
                initialAmmo: 99);

            Assert.That(turret.AmmoCapacity, Is.EqualTo(30));
            Assert.That(turret.Ammo, Is.EqualTo(30));

            Assert.That(
                turret.Tick(3f, target: null, globallyPaused: false),
                Is.Zero);
            Assert.That(turret.Ammo, Is.EqualTo(30));
        }

        [Test]
        public void OneAmmoFundsThreeSecondsAtTwentyDpsIndependentOfTickPartition()
        {
            var wholeStepTurret = new MachineGunTurretCombatModel(
                "building.instance.turret-whole",
                0f,
                0f,
                initialAmmo: 1);
            var splitStepTurret = new MachineGunTurretCombatModel(
                "building.instance.turret-split",
                0f,
                0f,
                initialAmmo: 1);
            var wholeStepTarget = Gnawer("enemy.gnawer.whole", 2f, 0f);
            var splitStepTarget = Gnawer("enemy.gnawer.split", 2f, 0f);

            wholeStepTurret.Tick(
                3f,
                wholeStepTarget,
                globallyPaused: false);
            splitStepTurret.Tick(
                1f,
                splitStepTarget,
                globallyPaused: false);
            splitStepTurret.Tick(
                2f,
                splitStepTarget,
                globallyPaused: false);

            Assert.That(wholeStepTarget.CurrentHealth, Is.Zero);
            Assert.That(splitStepTarget.CurrentHealth, Is.Zero);
            Assert.That(wholeStepTurret.Ammo, Is.Zero);
            Assert.That(splitStepTurret.Ammo, Is.Zero);
        }

        [Test]
        public void LongTickCrossesThreeAmmoLeasesWithoutLosingFractionalDamage()
        {
            var wholeStepTurret = new MachineGunTurretCombatModel(
                "building.instance.turret-long-whole",
                0f,
                0f,
                initialAmmo: 3);
            var splitStepTurret = new MachineGunTurretCombatModel(
                "building.instance.turret-long-split",
                0f,
                0f,
                initialAmmo: 3);
            DefenseEnemyCombatModel wholeStepTarget = DurableLightEnemy(
                "enemy.durable.whole");
            DefenseEnemyCombatModel splitStepTarget = DurableLightEnemy(
                "enemy.durable.split");

            int wholeStepDamage = wholeStepTurret.Tick(
                6.1f,
                wholeStepTarget,
                globallyPaused: false);
            int splitStepDamage =
                splitStepTurret.Tick(
                    3f,
                    splitStepTarget,
                    globallyPaused: false) +
                splitStepTurret.Tick(
                    3f,
                    splitStepTarget,
                    globallyPaused: false) +
                splitStepTurret.Tick(
                    .1f,
                    splitStepTarget,
                    globallyPaused: false);

            Assert.That(wholeStepDamage, Is.EqualTo(122));
            Assert.That(splitStepDamage, Is.EqualTo(122));
            Assert.That(
                wholeStepTarget.CurrentHealth,
                Is.EqualTo(splitStepTarget.CurrentHealth));
            Assert.That(wholeStepTarget.CurrentHealth, Is.EqualTo(878));
            Assert.That(wholeStepTurret.Ammo, Is.Zero);
            Assert.That(splitStepTurret.Ammo, Is.Zero);
        }

        [Test]
        public void GlobalPauseFreezesTurretDamageAndAmmo()
        {
            var turret = new MachineGunTurretCombatModel(
                "building.instance.turret-paused",
                0f,
                0f,
                initialAmmo: 1);
            DefenseEnemyCombatModel target = Gnawer(
                "enemy.gnawer.paused",
                2f,
                0f);

            Assert.That(
                turret.Tick(3f, target, globallyPaused: true),
                Is.Zero);
            Assert.That(target.CurrentHealth, Is.EqualTo(60));
            Assert.That(turret.Ammo, Is.EqualTo(1));

            Assert.That(
                turret.Tick(1f, target, globallyPaused: false),
                Is.EqualTo(20));
            Assert.That(target.CurrentHealth, Is.EqualTo(40));
            Assert.That(turret.Ammo, Is.Zero);
        }

        [Test]
        public void TargetingChoosesNearestThenStableIdAndIgnoresOutOfRangeEnemies()
        {
            var turret = new MachineGunTurretCombatModel(
                "building.instance.turret-targeting",
                0f,
                0f);
            DefenseEnemyCombatModel far = Gnawer("enemy.far", 9f, 0f);
            DefenseEnemyCombatModel near = Gnawer("enemy.near", 3f, 0f);
            DefenseEnemyCombatModel outside = Gnawer("enemy.outside", 11f, 0f);

            Assert.That(
                turret.AcquireTarget(new[] { far, outside, near }),
                Is.SameAs(near));

            DefenseEnemyCombatModel stableB = Gnawer("enemy.stable-b", 4f, 0f);
            DefenseEnemyCombatModel stableA = Gnawer("enemy.stable-a", -4f, 0f);
            Assert.That(
                turret.AcquireTarget(new[] { stableB, stableA }),
                Is.SameAs(stableA));
        }

        [Test]
        public void DisconnectedTurretRetainsAmmoAndRefillsOnlyAfterReconnect()
        {
            var backingInventory = new ResourceInventory(500);
            backingInventory.Add(ResourceIds.Ammunition, 40);
            using var cityStorage = new CityResourceStorageModel(
                backingInventory,
                coreCapacityPerResource: 150);
            var turret = new MachineGunTurretCombatModel(
                "building.instance.turret-logistics",
                0f,
                0f,
                initialAmmo: 29);

            turret.SetLogisticsConnected(false);
            Assert.That(turret.RefillFrom(cityStorage), Is.Zero);
            Assert.That(turret.Ammo, Is.EqualTo(29));
            Assert.That(
                cityStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(40));

            turret.SetLogisticsConnected(true);
            Assert.That(turret.RefillFrom(cityStorage), Is.EqualTo(1));
            Assert.That(turret.Ammo, Is.EqualTo(30));
            Assert.That(
                cityStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(39));
        }

        [Test]
        public void RefillPublishesDefenseAttributionAndConservesAmmunition()
        {
            var backingInventory = new ResourceInventory(500);
            backingInventory.Add(ResourceIds.Ammunition, 40);
            using var cityStorage = new CityResourceStorageModel(
                backingInventory,
                coreCapacityPerResource: 150);
            const string turretId = "building.instance.turret-attribution";
            var turret = new MachineGunTurretCombatModel(
                turretId,
                0f,
                0f,
                initialAmmo: 29);
            string changedResourceId = null;
            int changedAmount = 0;
            ResourceChangeAttribution attribution = default;
            cityStorage.AttributedChanged +=
                (resourceId, delta, value) =>
                {
                    changedResourceId = resourceId;
                    changedAmount += delta;
                    attribution = value;
                };
            int totalBefore = turret.Ammo +
                cityStorage.GetNetworkAmount(ResourceIds.Ammunition);

            Assert.That(turret.RefillFrom(cityStorage), Is.EqualTo(1));

            Assert.That(changedResourceId, Is.EqualTo(ResourceIds.Ammunition));
            Assert.That(changedAmount, Is.EqualTo(-1));
            Assert.That(
                attribution.Kind,
                Is.EqualTo(ResourceChangeAttributionKind.Defense));
            Assert.That(attribution.ReferenceId, Is.EqualTo(turretId));
            Assert.That(
                turret.Ammo +
                cityStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(totalBefore));
        }

        [Test]
        public void PlayerPauseFreezesRefillUntilTowerResumes()
        {
            var backingInventory = new ResourceInventory(500);
            backingInventory.Add(ResourceIds.Ammunition, 30);
            using var cityStorage = new CityResourceStorageModel(
                backingInventory,
                coreCapacityPerResource: 150);
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.turret-player-paused",
                x: 0,
                y: 0);
            var runtime = new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 9f,
                spawnZ: 0f);
            runtime.Synchronize(
                new[] { turret },
                CityMode.Fortress,
                cityX: 0,
                cityY: 0,
                BuildingRangeRules.InitialGroundRadius);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            int cityAmmoBeforePause = cityStorage.GetNetworkAmount(
                ResourceIds.Ammunition);

            runtime.Tick(.1f, globallyPaused: false, cityStorage);

            Assert.That(runtime.Snapshot.Towers[0].Ammo, Is.Zero);
            Assert.That(runtime.Snapshot.Towers[0].Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.PlayerPaused));
            Assert.That(cityStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(cityAmmoBeforePause));

            runtime.Tick(10f, globallyPaused: true, cityStorage);
            Assert.That(runtime.Snapshot.Towers[0].Ammo, Is.Zero);
            Assert.That(cityStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.EqualTo(cityAmmoBeforePause));

            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: false), Is.True);
            runtime.Tick(.1f, globallyPaused: false, cityStorage);

            Assert.That(runtime.Snapshot.Towers[0].Ammo, Is.EqualTo(30));
            Assert.That(cityStorage.GetNetworkAmount(ResourceIds.Ammunition),
                Is.Zero);
        }

        [Test]
        public void CityCoreHasTwoThousandHealthAndGnawerDealsEightDps()
        {
            var core = new CityCoreCombatModel();
            DefenseEnemyCombatModel gnawer = Gnawer(
                "enemy.gnawer.attacking",
                0f,
                0f);

            Assert.That(core.MaximumHealth, Is.EqualTo(2000));
            Assert.That(core.CurrentHealth, Is.EqualTo(2000));
            Assert.That(
                gnawer.TickAttack(1f, core, globallyPaused: false),
                Is.EqualTo(8));
            Assert.That(core.CurrentHealth, Is.EqualTo(1992));

            Assert.That(
                gnawer.TickAttack(1f, core, globallyPaused: true),
                Is.Zero);
            Assert.That(core.CurrentHealth, Is.EqualTo(1992));
        }

        private static DefenseEnemyCombatModel Gnawer(
            string stableId,
            float x,
            float z)
        {
            return new DefenseEnemyCombatModel(
                stableId,
                EnemyCatalog.Gnawer,
                x,
                z);
        }

        private static DefenseEnemyCombatModel DurableLightEnemy(
            string stableId)
        {
            var definition = new EnemyDefinition(
                "test.enemy.durable-light",
                "高血量轻甲测试敌人",
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
                x: 2f,
                z: 0f);
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
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
            complete.Invoke(instance, null);
            return instance;
        }
    }
}
