using System;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseTowerCombatModelTests
    {
        private const string ModelTypeName =
            "WasteCity.Defense.SingleCityDefenseTowerCombatModel, WasteCity.Game";
        private const float Tolerance = .001f;

        [TestCase(
            "core.building.machine-gun-turret",
            DamageType.Physical,
            20f,
            10f,
            ResourceIds.Ammunition,
            3f)]
        [TestCase(
            "core.building.laser-tower",
            DamageType.Energy,
            48f,
            12f,
            ResourceIds.EnergyCrystal,
            4f)]
        [TestCase(
            "biological.building.spore-tower",
            DamageType.Biological,
            18f,
            9f,
            ResourceIds.BiologicalWeapon,
            5f)]
        public void FormalTowerProjectsItsCatalogDefinitionAndCapsLocalStock(
            string buildingId,
            DamageType damageType,
            float damagePerSecond,
            float range,
            string consumableId,
            float secondsPerConsumable)
        {
            object model = CreateModel(
                "building.instance.catalog." + buildingId,
                buildingId,
                initialConsumable: 99);

            Assert.That(Read<string>(model, "BuildingId"),
                Is.EqualTo(buildingId));
            Assert.That(Read<DamageType>(model, "DamageType"),
                Is.EqualTo(damageType));
            Assert.That(Read<float>(model, "DamagePerSecond"),
                Is.EqualTo(damagePerSecond).Within(Tolerance));
            Assert.That(Read<float>(model, "Range"),
                Is.EqualTo(range).Within(Tolerance));
            Assert.That(Read<string>(model, "ConsumableId"),
                Is.EqualTo(consumableId));
            Assert.That(Read<float>(model, "SecondsPerConsumable"),
                Is.EqualTo(secondsPerConsumable).Within(Tolerance));
            Assert.That(Read<int>(model, "LocalCapacity"), Is.EqualTo(30));
            Assert.That(Read<int>(model, "LocalConsumableAmount"),
                Is.EqualTo(30));
        }

        [TestCase(
            "core.building.machine-gun-turret",
            ResourceIds.Ammunition)]
        [TestCase(
            "core.building.laser-tower",
            ResourceIds.EnergyCrystal)]
        [TestCase(
            "biological.building.spore-tower",
            ResourceIds.BiologicalWeapon)]
        public void ConnectedRefillAtomicallyFillsFromConfiguredCityResource(
            string buildingId,
            string consumableId)
        {
            const string stableId = "building.instance.refill";
            var inventory = new ResourceInventory(500);
            inventory.Add(consumableId, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            object model = CreateModel(
                stableId,
                buildingId,
                initialConsumable: 7);
            SetLogisticsConnected(model, true);
            var eventCount = 0;
            var attributedDelta = 0;
            ResourceChangeAttribution attribution = default;
            storage.AttributedChanged += (resourceId, delta, value) =>
            {
                if (!string.Equals(
                        resourceId,
                        consumableId,
                        StringComparison.Ordinal))
                {
                    return;
                }
                eventCount++;
                attributedDelta += delta;
                attribution = value;
            };

            int moved = Invoke<int>(
                model,
                "RefillFrom",
                new[] { typeof(CityResourceStorageModel) },
                storage);

            Assert.That(moved, Is.EqualTo(23));
            Assert.That(Read<int>(model, "LocalConsumableAmount"),
                Is.EqualTo(30));
            Assert.That(storage.GetNetworkAmount(consumableId), Is.EqualTo(17));
            Assert.That(eventCount, Is.EqualTo(1),
                "A full refill is one attributed city-storage transaction.");
            Assert.That(attributedDelta, Is.EqualTo(-23));
            Assert.That(attribution.Kind,
                Is.EqualTo(ResourceChangeAttributionKind.Defense));
            Assert.That(attribution.ReferenceId, Is.EqualTo(stableId));
        }

        [TestCase(
            "core.building.machine-gun-turret",
            ResourceIds.Ammunition,
            3f)]
        [TestCase(
            "core.building.laser-tower",
            ResourceIds.EnergyCrystal,
            4f)]
        [TestCase(
            "biological.building.spore-tower",
            ResourceIds.BiologicalWeapon,
            5f)]
        public void DisconnectedTowerRetainsAndUsesLocalStockUntilEmpty(
            string buildingId,
            string consumableId,
            float secondsPerConsumable)
        {
            var inventory = new ResourceInventory(500);
            inventory.Add(consumableId, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            object model = CreateModel(
                "building.instance.disconnected." + buildingId,
                buildingId,
                initialConsumable: 1);
            SetLogisticsConnected(model, false);

            Assert.That(Invoke<int>(
                model,
                "RefillFrom",
                new[] { typeof(CityResourceStorageModel) },
                storage), Is.Zero);
            Assert.That(Read<int>(model, "LocalConsumableAmount"),
                Is.EqualTo(1));
            Assert.That(storage.GetNetworkAmount(consumableId), Is.EqualTo(40));

            DefenseEnemyCombatModel target = DurableTarget(
                "enemy.disconnected." + buildingId);
            Assert.That(Tick(model, secondsPerConsumable, target, false),
                Is.GreaterThan(0));
            Assert.That(Read<int>(model, "LocalConsumableAmount"), Is.Zero);
            int healthAfterLease = target.CurrentHealth;

            Assert.That(Tick(model, .1f, target, false), Is.Zero);
            Assert.That(target.CurrentHealth, Is.EqualTo(healthAfterLease));
            Assert.That(storage.GetNetworkAmount(consumableId), Is.EqualTo(40));
        }

        [TestCase("core.building.machine-gun-turret")]
        [TestCase("core.building.laser-tower")]
        [TestCase("biological.building.spore-tower")]
        public void PauseAndMissingTargetDoNotStartOrBurnLease(
            string buildingId)
        {
            object model = CreateModel(
                "building.instance.pause." + buildingId,
                buildingId,
                initialConsumable: 2);
            DefenseEnemyCombatModel target = DurableTarget(
                "enemy.pause." + buildingId);
            int initialHealth = target.CurrentHealth;

            Assert.That(Tick(model, .1f, null, false), Is.Zero);
            AssertUnspent(model, expectedAmount: 2);

            SetPlayerPaused(model, true);
            Assert.That(Tick(model, .1f, target, false), Is.Zero);
            AssertUnspent(model, expectedAmount: 2);
            Assert.That(target.CurrentHealth, Is.EqualTo(initialHealth));

            SetPlayerPaused(model, false);
            Assert.That(Tick(model, .1f, target, true), Is.Zero);
            AssertUnspent(model, expectedAmount: 2);
            Assert.That(target.CurrentHealth, Is.EqualTo(initialHealth));

            Assert.That(Tick(model, .1f, target, false), Is.GreaterThan(0));
            Assert.That(Read<int>(model, "LocalConsumableAmount"),
                Is.EqualTo(1));
            Assert.That(Read<float>(model, "ActiveConsumableSeconds"),
                Is.EqualTo(
                    Read<float>(model, "SecondsPerConsumable") - .1f)
                    .Within(Tolerance));
        }

        [TestCase("core.building.machine-gun-turret", 3f)]
        [TestCase("core.building.laser-tower", 4f)]
        [TestCase("biological.building.spore-tower", 5f)]
        public void LeaseCrossingIsStableAcrossPointOneSecondPartitions(
            string buildingId,
            float secondsPerConsumable)
        {
            object whole = CreateModel(
                "building.instance.partition.whole." + buildingId,
                buildingId,
                initialConsumable: 2);
            object split = CreateModel(
                "building.instance.partition.split." + buildingId,
                buildingId,
                initialConsumable: 2);
            DefenseEnemyCombatModel wholeTarget = DurableTarget(
                "enemy.partition.whole." + buildingId);
            DefenseEnemyCombatModel splitTarget = DurableTarget(
                "enemy.partition.split." + buildingId);

            float totalSeconds = secondsPerConsumable + .1f;
            int wholeDamage = Tick(whole, totalSeconds, wholeTarget, false);
            var splitDamage = 0;
            int stepCount = Convert.ToInt32(secondsPerConsumable * 10f) + 1;
            for (var index = 0; index < stepCount; index++)
                splitDamage += Tick(split, .1f, splitTarget, false);

            Assert.That(splitDamage, Is.EqualTo(wholeDamage));
            Assert.That(splitTarget.CurrentHealth,
                Is.EqualTo(wholeTarget.CurrentHealth));
            Assert.That(Read<int>(split, "LocalConsumableAmount"),
                Is.EqualTo(Read<int>(whole, "LocalConsumableAmount")));
            Assert.That(Read<int>(whole, "LocalConsumableAmount"), Is.Zero,
                "Crossing one lease boundary must acquire exactly two units.");
            Assert.That(Read<float>(split, "ActiveConsumableSeconds"),
                Is.EqualTo(Read<float>(whole, "ActiveConsumableSeconds"))
                    .Within(Tolerance));
            Assert.That(Read<float>(split, "DamageRemainder"),
                Is.EqualTo(Read<float>(whole, "DamageRemainder"))
                    .Within(Tolerance));
        }

        private static object CreateModel(
            string stableId,
            string buildingId,
            int initialConsumable)
        {
            Type type = RequireModelType();
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(string),
                typeof(string),
                typeof(float),
                typeof(float),
                typeof(int),
            });
            Assert.That(constructor, Is.Not.Null,
                "The formal tower model requires " +
                "(stableId, buildingId, x, z, initialConsumable=0).");
            return constructor.Invoke(new object[]
            {
                stableId,
                buildingId,
                0f,
                0f,
                initialConsumable,
            });
        }

        private static Type RequireModelType()
        {
            Type type = Type.GetType(ModelTypeName, throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Missing pure formal three-tower combat model: " +
                ModelTypeName + ".");
            return type;
        }

        private static DefenseEnemyCombatModel DurableTarget(string stableId)
        {
            var definition = new EnemyDefinition(
                "test.enemy.durable-tower-target",
                "耐久测试目标",
                EnemyArchetype.Gnawer,
                10000,
                0f,
                0f,
                1f,
                ArmorType.Light,
                0,
                EnemyTargetPriority.Core);
            return new DefenseEnemyCombatModel(stableId, definition, 1f, 0f);
        }

        private static int Tick(
            object model,
            float seconds,
            DefenseEnemyCombatModel target,
            bool globallyPaused)
        {
            return Invoke<int>(
                model,
                "Tick",
                new[]
                {
                    typeof(float),
                    typeof(DefenseEnemyCombatModel),
                    typeof(bool),
                },
                seconds,
                target,
                globallyPaused);
        }

        private static void SetLogisticsConnected(object model, bool value)
        {
            Invoke<object>(
                model,
                "SetLogisticsConnected",
                new[] { typeof(bool) },
                value);
        }

        private static void SetPlayerPaused(object model, bool value)
        {
            Invoke<object>(
                model,
                "SetPlayerPaused",
                new[] { typeof(bool) },
                value);
        }

        private static void AssertUnspent(object model, int expectedAmount)
        {
            Assert.That(Read<int>(model, "LocalConsumableAmount"),
                Is.EqualTo(expectedAmount));
            Assert.That(Read<float>(model, "ActiveConsumableSeconds"),
                Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Read<float>(model, "DamageRemainder"),
                Is.EqualTo(0f).Within(Tolerance));
        }

        private static T Read<T>(object owner, string propertyName)
        {
            PropertyInfo property = owner.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                owner.GetType().Name + " must expose " + propertyName + ".");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(T)));
            return (T)property.GetValue(owner);
        }

        private static T Invoke<T>(
            object owner,
            string methodName,
            Type[] parameterTypes,
            params object[] arguments)
        {
            MethodInfo method = owner.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null,
                owner.GetType().Name + " must expose " + methodName + ".");
            object result = method.Invoke(owner, arguments);
            return result == null ? default : (T)result;
        }
    }
}
