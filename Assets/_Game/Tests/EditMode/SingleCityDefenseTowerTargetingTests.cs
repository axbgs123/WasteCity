using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseTowerTargetingTests
    {
        private const string TowerTypeName =
            "WasteCity.Defense.SingleCityDefenseTowerCombatModel";
        private static readonly string[] FormalTowerIds =
        {
            BuildingCatalog.MachineGunTurret.Id.Value,
            BuildingCatalog.LaserTower.Id.Value,
            BuildingCatalog.SporeTower.Id.Value,
        };

        [Test]
        public void ThreeTowersRefillOnlyWhenConnectedUsingFormalConsumables()
        {
            Type towerType = RequireTowerType();
            RequireConstructor(
                towerType,
                typeof(string),
                typeof(string),
                typeof(float),
                typeof(float));
            RequireMethod(
                towerType,
                "RefillFrom",
                typeof(int),
                typeof(CityResourceStorageModel),
                typeof(bool));
            RequireMethod(
                towerType,
                "AcquireTarget",
                typeof(string),
                typeof(IReadOnlyList<SingleCityDefenseEnemySnapshot>));
            RequireMethod(
                towerType,
                "Tick",
                typeof(int),
                typeof(float),
                typeof(SingleCityDefenseCampaignModel),
                typeof(bool));

            for (var index = 0; index < FormalTowerIds.Length; index++)
            {
                string buildingId = FormalTowerIds[index];
                DefenseTowerDefinition definition =
                    DefenseTowerCatalog.For(buildingId);
                Assert.That(definition, Is.Not.Null, buildingId);
                object tower = CreateTower(
                    "building.instance.refill." + index,
                    buildingId,
                    x: 0f,
                    z: 0f);
                using CityResourceStorageModel storage = StorageWith(
                    definition.ConsumableId,
                    definition.LocalCapacity + 10);
                int amountBefore = storage.GetNetworkAmount(
                    definition.ConsumableId);

                Assert.That(Refill(tower, storage, connected: false), Is.Zero,
                    buildingId);
                Assert.That(storage.GetNetworkAmount(definition.ConsumableId),
                    Is.EqualTo(amountBefore));
                Assert.That(Refill(tower, storage, connected: true),
                    Is.EqualTo(definition.LocalCapacity),
                    buildingId);
                Assert.That(storage.GetNetworkAmount(definition.ConsumableId),
                    Is.EqualTo(amountBefore - definition.LocalCapacity));
                Assert.That(Refill(tower, storage, connected: true), Is.Zero,
                    "A full local cache must not withdraw again.");
            }
        }

        [Test]
        public void ThreeTowerRangesUseNearestStableTieAndStickyValidLock()
        {
            for (var index = 0; index < FormalTowerIds.Length; index++)
            {
                string buildingId = FormalTowerIds[index];
                DefenseTowerDefinition definition =
                    DefenseTowerCatalog.For(buildingId);
                object tower = CreateTower(
                    "building.instance.targeting." + index,
                    buildingId,
                    x: 0f,
                    z: 0f);
                float range = definition.Range;

                var atBoundary = Enemy(
                    "enemy.boundary",
                    x: range,
                    z: 0f);
                var outside = Enemy(
                    "enemy.outside",
                    x: range + .01f,
                    z: 0f);
                Assert.That(Acquire(tower, new[] { outside }), Is.Null,
                    buildingId + " must reject targets beyond formal range.");
                Assert.That(Acquire(tower, new[] { outside, atBoundary }),
                    Is.EqualTo(atBoundary.StableId),
                    buildingId + " must include the exact range boundary.");

                object tieTower = CreateTower(
                    "building.instance.tie." + index,
                    buildingId,
                    x: 0f,
                    z: 0f);
                var farther = Enemy("enemy.farther", x: range - 1f, z: 0f);
                var tieB = Enemy("enemy.tie-b", x: 3f, z: 4f);
                var tieA = Enemy("enemy.tie-a", x: -3f, z: 4f);
                Assert.That(Acquire(
                    tieTower,
                    new[] { farther, tieB, tieA }),
                    Is.EqualTo(tieA.StableId),
                    "Distance ties must use ordinal enemy stable ID.");

                var lockedA = Enemy("enemy.lock-a", x: 2f, z: 0f);
                var candidateB = Enemy("enemy.lock-b", x: 4f, z: 0f);
                object lockTower = CreateTower(
                    "building.instance.lock." + index,
                    buildingId,
                    x: 0f,
                    z: 0f);
                Assert.That(Acquire(lockTower, new[] { candidateB, lockedA }),
                    Is.EqualTo(lockedA.StableId));
                Assert.That(Acquire(
                    lockTower,
                    new[]
                    {
                        Enemy(lockedA.StableId, x: 2f, z: 0f),
                        Enemy(candidateB.StableId, x: 1f, z: 0f),
                    }),
                    Is.EqualTo(lockedA.StableId),
                    "A living in-range lock must not be stolen by a closer target.");
                Assert.That(Acquire(
                    lockTower,
                    new[]
                    {
                        Enemy(lockedA.StableId, x: 2f, z: 0f, health: 0),
                        Enemy(candidateB.StableId, x: 1f, z: 0f),
                    }),
                    Is.EqualTo(candidateB.StableId),
                    "A dead lock must be replaced immediately.");
                Assert.That(Acquire(
                    lockTower,
                    new[]
                    {
                        Enemy(
                            candidateB.StableId,
                            x: range + .01f,
                            z: 0f),
                        Enemy("enemy.lock-c", x: range, z: 0f),
                    }),
                    Is.EqualTo("enemy.lock-c"),
                    "An out-of-range lock must be replaced deterministically.");
            }
        }

        [Test]
        public void LaterTowerDoesNotSpendWhenEarlierTowerKillsSharedTarget()
        {
            const string buildingId =
                "core.building.machine-gun-turret";
            DefenseTowerDefinition definition =
                DefenseTowerCatalog.For(buildingId);
            SingleCityDefenseCampaignModel campaign =
                CampaignWithFirstSpawnedEnemy();
            SingleCityDefenseEnemySnapshot target =
                campaign.Snapshot.Enemies.Single();
            object first = CreateTower(
                "building.instance.ordered-a",
                buildingId,
                x: target.X - definition.Range,
                z: target.Z);
            object later = CreateTower(
                "building.instance.ordered-b",
                buildingId,
                x: target.X - definition.Range,
                z: target.Z);
            using CityResourceStorageModel storage = StorageWith(
                definition.ConsumableId,
                definition.LocalCapacity * 2 + 10);
            Assert.That(Refill(first, storage, connected: true),
                Is.EqualTo(definition.LocalCapacity));
            Assert.That(Refill(later, storage, connected: true),
                Is.EqualTo(definition.LocalCapacity));
            Assert.That(Acquire(first, campaign.Snapshot.Enemies),
                Is.EqualTo(target.StableId));
            Assert.That(Acquire(later, campaign.Snapshot.Enemies),
                Is.EqualTo(target.StableId));

            int killingDamage = Tick(
                first,
                definition.SecondsPerConsumable,
                campaign,
                globallyPaused: false);
            Assert.That(killingDamage, Is.EqualTo(target.CurrentHealth));
            Assert.That(campaign.Snapshot.Enemies, Is.Empty);

            int laterDamage = Tick(
                later,
                definition.SecondsPerConsumable,
                campaign,
                globallyPaused: false);
            Assert.That(laterDamage, Is.Zero,
                "A stale lock must revalidate against campaign truth.");
            Assert.That(Refill(later, storage, connected: true), Is.Zero,
                "The later tower must retain its full local consumable cache.");
            Assert.That(DictionaryAmount(
                    campaign.Snapshot.Statistics.ConsumablesSpentByResourceId,
                    definition.ConsumableId),
                Is.EqualTo(1),
                "Only the tower that committed firing may record consumption.");
            Assert.That(DictionaryAmount(
                    campaign.Snapshot.Statistics.KillsByTowerBuildingId,
                    buildingId),
                Is.EqualTo(1));
        }

        [Test]
        public void TickAppliesDamageMatrixAndPublishesActualDamageStatistics()
        {
            for (var index = 0; index < FormalTowerIds.Length; index++)
            {
                string buildingId = FormalTowerIds[index];
                DefenseTowerDefinition definition =
                    DefenseTowerCatalog.For(buildingId);
                SingleCityDefenseCampaignModel campaign =
                    CampaignWithCrystalBeast(
                        FormalTowerIds[(index + 1) % FormalTowerIds.Length]);
                SingleCityDefenseEnemySnapshot target = campaign.Snapshot.Enemies
                    .Single(enemy => string.Equals(
                        enemy.EnemyDefinitionId,
                        EnemyCatalog.CrystalBeast.Id.Value,
                        StringComparison.Ordinal));
                object tower = CreateTower(
                    "building.instance.damage." + index,
                    buildingId,
                    x: target.X,
                    z: target.Z - definition.Range);
                using CityResourceStorageModel storage = StorageWith(
                    definition.ConsumableId,
                    definition.LocalCapacity + 10);
                Assert.That(Refill(tower, storage, connected: true),
                    Is.EqualTo(definition.LocalCapacity));
                Assert.That(Acquire(tower, campaign.Snapshot.Enemies),
                    Is.EqualTo(target.StableId));

                int healthBefore = target.CurrentHealth;
                Assert.That(Tick(
                    tower,
                    1f,
                    campaign,
                    globallyPaused: true), Is.Zero);
                Assert.That(Refill(tower, storage, connected: true), Is.Zero,
                    "Global pause must not spend a local consumable.");
                Assert.That(CurrentHealth(campaign, target.StableId),
                    Is.EqualTo(healthBefore));

                int rawDamage = (int)definition.DamagePerSecond;
                int expectedDamage = DamageMatrix.Apply(
                    rawDamage,
                    definition.DamageType,
                    EnemyCatalog.CrystalBeast.Armor);
                int actualDamage = Tick(
                    tower,
                    1f,
                    campaign,
                    globallyPaused: false);

                Assert.That(actualDamage, Is.EqualTo(expectedDamage), buildingId);
                Assert.That(CurrentHealth(campaign, target.StableId),
                    Is.EqualTo(healthBefore - expectedDamage));
                Assert.That(DictionaryAmount(
                        campaign.Snapshot.Statistics.DamageByTowerBuildingId,
                        buildingId),
                    Is.EqualTo(expectedDamage),
                    "Statistics must record applied post-matrix damage.");
                Assert.That(DictionaryAmount(
                        campaign.Snapshot.Statistics
                            .ConsumablesSpentByResourceId,
                        definition.ConsumableId),
                    Is.EqualTo(1));
            }
        }

        [Test]
        public void CampaignTickIsStableAcrossFixedStepPartitionsAndRetargeting()
        {
            const string buildingId =
                "core.building.machine-gun-turret";
            SingleCityDefenseCampaignModel wholeCampaign =
                CampaignWithTwoSpawnedGnawers();
            SingleCityDefenseCampaignModel splitCampaign =
                CampaignWithTwoSpawnedGnawers();
            SingleCityDefenseEnemySnapshot wholeFirst = wholeCampaign.Snapshot
                .Enemies.OrderBy(enemy => enemy.SpawnOrder).First();
            SingleCityDefenseEnemySnapshot splitFirst = splitCampaign.Snapshot
                .Enemies.OrderBy(enemy => enemy.SpawnOrder).First();
            Assert.That(wholeCampaign.ApplyTowerDamage(
                wholeFirst.StableId,
                buildingId,
                wholeFirst.CurrentHealth - 1), Is.EqualTo(59));
            Assert.That(splitCampaign.ApplyTowerDamage(
                splitFirst.StableId,
                buildingId,
                splitFirst.CurrentHealth - 1), Is.EqualTo(59));

            object wholeTower = CreateTower(
                "building.instance.partition.whole",
                buildingId,
                x: 10f,
                z: 0f);
            object splitTower = CreateTower(
                "building.instance.partition.split",
                buildingId,
                x: 10f,
                z: 0f);
            using CityResourceStorageModel wholeStorage = StorageWith(
                ResourceIds.Ammunition,
                100);
            using CityResourceStorageModel splitStorage = StorageWith(
                ResourceIds.Ammunition,
                100);
            Refill(wholeTower, wholeStorage, connected: true);
            Refill(splitTower, splitStorage, connected: true);

            int wholeDamage = Tick(
                wholeTower,
                3f,
                wholeCampaign,
                globallyPaused: false);
            var splitDamage = 0;
            for (var index = 0; index < 30; index++)
            {
                splitDamage += Tick(
                    splitTower,
                    .1f,
                    splitCampaign,
                    globallyPaused: false);
            }

            Assert.That(wholeDamage, Is.EqualTo(splitDamage));
            Assert.That(
                wholeCampaign.Snapshot.Enemies
                    .OrderBy(enemy => enemy.SpawnOrder)
                    .Select(enemy => enemy.CurrentHealth),
                Is.EqualTo(
                    splitCampaign.Snapshot.Enemies
                        .OrderBy(enemy => enemy.SpawnOrder)
                        .Select(enemy => enemy.CurrentHealth)));
        }

        [Test]
        public void ArmorResolutionAndTerminalFreezeAreFixedStepStable()
        {
            const string buildingId =
                "core.building.machine-gun-turret";
            SingleCityDefenseCampaignModel whole = CampaignWithCrystalBeast(
                BuildingCatalog.LaserTower.Id.Value);
            SingleCityDefenseCampaignModel split = CampaignWithCrystalBeast(
                BuildingCatalog.LaserTower.Id.Value);
            SingleCityDefenseEnemySnapshot wholeTarget = whole.Snapshot.Enemies
                .Single(enemy => enemy.EnemyDefinitionId ==
                    EnemyCatalog.CrystalBeast.Id.Value);
            SingleCityDefenseEnemySnapshot splitTarget = split.Snapshot.Enemies
                .Single(enemy => enemy.EnemyDefinitionId ==
                    EnemyCatalog.CrystalBeast.Id.Value);
            object wholeTower = CreateTower(
                "building.instance.armor.whole",
                buildingId,
                wholeTarget.X,
                wholeTarget.Z);
            object splitTower = CreateTower(
                "building.instance.armor.split",
                buildingId,
                splitTarget.X,
                splitTarget.Z);
            using CityResourceStorageModel wholeStorage = StorageWith(
                ResourceIds.Ammunition,
                100);
            using CityResourceStorageModel splitStorage = StorageWith(
                ResourceIds.Ammunition,
                100);
            Refill(wholeTower, wholeStorage, connected: true);
            Refill(splitTower, splitStorage, connected: true);

            int wholeDamage = Tick(wholeTower, 1f, whole, false);
            var splitDamage = 0;
            for (var index = 0; index < 10; index++)
                splitDamage += Tick(splitTower, .1f, split, false);
            Assert.That(wholeDamage, Is.EqualTo(splitDamage));
            Assert.That(wholeDamage, Is.EqualTo(14));

            SingleCityDefenseCampaignModel terminal =
                CampaignWithFirstSpawnedEnemy();
            SingleCityDefenseEnemySnapshot terminalTarget =
                terminal.Snapshot.Enemies.Single();
            object terminalTower = CreateTower(
                "building.instance.terminal",
                buildingId,
                terminalTarget.X,
                terminalTarget.Z);
            using CityResourceStorageModel terminalStorage = StorageWith(
                ResourceIds.Ammunition,
                100);
            Refill(terminalTower, terminalStorage, connected: true);
            Assert.That(terminal.ApplyCoreDamage(
                CityCoreCombatModel.FormalMaximumHealth),
                Is.EqualTo(CityCoreCombatModel.FormalMaximumHealth));

            Assert.That(Tick(terminalTower, 3f, terminal, false), Is.Zero);
            Assert.That(Refill(terminalTower, terminalStorage, true), Is.Zero,
                "Terminal combat must not spend local consumables.");
            Assert.That(DictionaryAmount(
                terminal.Snapshot.Statistics.ConsumablesSpentByResourceId,
                ResourceIds.Ammunition), Is.Zero);
        }

        private static Type RequireTowerType()
        {
            Type type = typeof(SingleCityDefenseCampaignModel).Assembly.GetType(
                TowerTypeName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null,
                TowerTypeName + " is required for the formal three-tower loop.");
            return type;
        }

        private static ConstructorInfo RequireConstructor(
            Type type,
            params Type[] parameterTypes)
        {
            ConstructorInfo constructor = type.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                null,
                parameterTypes,
                null);
            Assert.That(constructor, Is.Not.Null,
                type.FullName + " requires the public constructor " +
                "(stableInstanceId, buildingId, x, z).");
            return constructor;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            Type returnType,
            params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null,
                type.FullName + " requires public " + name + ".");
            Assert.That(method.ReturnType, Is.EqualTo(returnType), name);
            return method;
        }

        private static object CreateTower(
            string stableInstanceId,
            string buildingId,
            float x,
            float z)
        {
            Type type = RequireTowerType();
            ConstructorInfo constructor = RequireConstructor(
                type,
                typeof(string),
                typeof(string),
                typeof(float),
                typeof(float));
            return constructor.Invoke(new object[]
            {
                stableInstanceId,
                buildingId,
                x,
                z,
            });
        }

        private static int Refill(
            object tower,
            CityResourceStorageModel storage,
            bool connected)
        {
            MethodInfo method = RequireMethod(
                tower.GetType(),
                "RefillFrom",
                typeof(int),
                typeof(CityResourceStorageModel),
                typeof(bool));
            return (int)method.Invoke(tower, new object[] { storage, connected });
        }

        private static string Acquire(
            object tower,
            IReadOnlyList<SingleCityDefenseEnemySnapshot> enemies)
        {
            MethodInfo method = RequireMethod(
                tower.GetType(),
                "AcquireTarget",
                typeof(string),
                typeof(IReadOnlyList<SingleCityDefenseEnemySnapshot>));
            return (string)method.Invoke(tower, new object[] { enemies });
        }

        private static int Tick(
            object tower,
            float deltaSeconds,
            SingleCityDefenseCampaignModel campaign,
            bool globallyPaused)
        {
            MethodInfo method = RequireMethod(
                tower.GetType(),
                "Tick",
                typeof(int),
                typeof(float),
                typeof(SingleCityDefenseCampaignModel),
                typeof(bool));
            return (int)method.Invoke(
                tower,
                new object[] { deltaSeconds, campaign, globallyPaused });
        }

        private static SingleCityDefenseEnemySnapshot Enemy(
            string stableId,
            float x,
            float z,
            int health = 100)
        {
            return new SingleCityDefenseEnemySnapshot(
                stableId,
                EnemyCatalog.Gnawer.Id.Value,
                spawnOrder: 0,
                x: x,
                z: z,
                currentHealth: health);
        }

        private static CityResourceStorageModel StorageWith(
            string resourceId,
            int amount)
        {
            var inventory = new ResourceInventory(1000);
            Assert.That(inventory.Add(resourceId, amount), Is.EqualTo(amount));
            return new CityResourceStorageModel(
                inventory,
                coreCapacityPerResource: 150);
        }

        private static SingleCityDefenseCampaignModel
            CampaignWithFirstSpawnedEnemy()
        {
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);
            Assert.That(campaign.NotifyDefenseTowerCompleted(
                "building.instance.trigger",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true), Is.True);
            campaign.Advance(20f, requestedSpeed: 1);
            Assert.That(campaign.Snapshot.Enemies, Has.Count.EqualTo(1));
            return campaign;
        }

        private static SingleCityDefenseCampaignModel
            CampaignWithTwoSpawnedGnawers()
        {
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);
            Assert.That(campaign.NotifyDefenseTowerCompleted(
                "building.instance.trigger",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true), Is.True);
            campaign.Advance(25f, requestedSpeed: 1);
            Assert.That(campaign.Snapshot.Enemies, Has.Count.EqualTo(2));
            return campaign;
        }

        private static SingleCityDefenseCampaignModel CampaignWithCrystalBeast(
            string preparationTowerBuildingId)
        {
            var campaign = new SingleCityDefenseCampaignModel(0f, 0f);
            Assert.That(campaign.NotifyDefenseTowerCompleted(
                "building.instance.trigger",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true), Is.True);

            campaign.Advance(55f, requestedSpeed: 1);
            DefeatVisibleEnemies(campaign, preparationTowerBuildingId);
            campaign.Advance(.1f, requestedSpeed: 1);
            campaign.Advance(65f, requestedSpeed: 1);
            DefeatVisibleEnemies(campaign, preparationTowerBuildingId);
            campaign.Advance(.1f, requestedSpeed: 1);
            campaign.Advance(28f, requestedSpeed: 1);

            Assert.That(campaign.Snapshot.Enemies.Any(enemy => string.Equals(
                enemy.EnemyDefinitionId,
                EnemyCatalog.CrystalBeast.Id.Value,
                StringComparison.Ordinal)), Is.True);
            return campaign;
        }

        private static void DefeatVisibleEnemies(
            SingleCityDefenseCampaignModel campaign,
            string sourceTowerBuildingId)
        {
            string[] stableIds = campaign.Snapshot.Enemies
                .Select(enemy => enemy.StableId)
                .ToArray();
            for (var index = 0; index < stableIds.Length; index++)
            {
                Assert.That(campaign.DefeatEnemy(
                    stableIds[index],
                    sourceTowerBuildingId), Is.True);
            }
        }

        private static int CurrentHealth(
            SingleCityDefenseCampaignModel campaign,
            string stableEnemyId)
        {
            SingleCityDefenseEnemySnapshot snapshot = campaign.Snapshot.Enemies
                .Single(enemy => string.Equals(
                    enemy.StableId,
                    stableEnemyId,
                    StringComparison.Ordinal));
            return snapshot.CurrentHealth;
        }

        private static int DictionaryAmount(
            IReadOnlyDictionary<string, int> values,
            string key)
        {
            return values.TryGetValue(key, out int amount) ? amount : 0;
        }
    }
}
