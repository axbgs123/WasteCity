using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseEnemyCampaignCombatTests
    {
        private const string CombatTargetTypeName =
            "WasteCity.Defense.DefenseBuildingCombatTarget";
        private const float Tolerance = .001f;

        [TestCase("core.enemy.gnawer", 1.8f, 8f, 2f)]
        [TestCase("core.enemy.crystal-beast", .9f, 20f, 2f)]
        [TestCase("core.enemy.howler", 1.2f, 12f, 7f)]
        public void FormalEnemyCombatReadsSharedCatalogValues(
            string enemyId,
            float moveSpeed,
            float damagePerSecond,
            float attackRange)
        {
            EnemyDefinition definition = Enemy(enemyId);

            Assert.That(definition.MoveSpeed,
                Is.EqualTo(moveSpeed).Within(Tolerance));
            Assert.That(definition.DamagePerSecond,
                Is.EqualTo(damagePerSecond).Within(Tolerance));
            Assert.That(definition.AttackRange,
                Is.EqualTo(attackRange).Within(Tolerance));
            Assert.That(SingleCityDefenseCampaignModel.FormalFixedStepSeconds,
                Is.EqualTo(.1f).Within(Tolerance));
        }

        [TestCase("core.enemy.gnawer")]
        [TestCase("core.enemy.crystal-beast")]
        [TestCase("core.enemy.howler")]
        public void MovementBeginsOnlyOnThePointOneSecondFixedStep(
            string enemyId)
        {
            EnemyDefinition definition = Enemy(enemyId);
            SingleCityDefenseCampaignModel model = CombatModel(
                coreX: 0f,
                coreZ: 0f,
                new EnemySeed("enemy.fixed-step", definition, 0, 20f, 0f));
            Array noBuildings = CombatTargets();

            Advance(model, .09f, 1, () => noBuildings, null);
            Assert.That(ReadEnemyX(model, "enemy.fixed-step"),
                Is.EqualTo(20f).Within(Tolerance));
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(2000));

            Advance(model, .01f, 1, () => noBuildings, null);
            Assert.That(ReadEnemyX(model, "enemy.fixed-step"),
                Is.EqualTo(20f - definition.MoveSpeed * .1f)
                    .Within(Tolerance));
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(2000));
        }

        [Test]
        public void EnteringAttackRangeWaitsUntilTheNextFixedStepToAttack()
        {
            EnemyDefinition definition = EnemyCatalog.CrystalBeast;
            float startX = definition.AttackRange +
                definition.MoveSpeed * .1f;
            SingleCityDefenseCampaignModel model = CombatModel(
                0f,
                0f,
                new EnemySeed("enemy.range-boundary", definition, 0, startX, 0f));
            Array noBuildings = CombatTargets();

            Advance(model, .1f, 1, () => noBuildings, null);

            Assert.That(ReadEnemyX(model, "enemy.range-boundary"),
                Is.EqualTo(definition.AttackRange).Within(Tolerance));
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(2000),
                "The fixed step used to enter range cannot also attack.");

            Advance(model, .1f, 1, () => noBuildings, null);
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(1998));
        }

        [Test]
        public void WarningRemainderCannotExecuteAPartialCombatStep()
        {
            EnemyDefinition definition = EnemyCatalog.CrystalBeast;
            SingleCityDefenseCampaignModel model = CombatModel(
                0f,
                0f,
                new EnemySeed(
                    "enemy.warning-boundary",
                    definition,
                    0,
                    definition.AttackRange,
                    0f));
            SetField(model, "phase", SingleCityDefenseCampaignPhase.Warning);
            SetField(model, "warningRemainingSeconds", .05d);
            Array noBuildings = CombatTargets();

            Advance(model, .1f, 1, () => noBuildings, null);

            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(2000),
                "Warning remainder may feed spawning, not partial damage.");

            Advance(model, .1f, 1, () => noBuildings, null);
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(1998));
        }

        [Test]
        public void EnemySpawnedAtCadenceBoundaryActsOnTheNextFixedStep()
        {
            var model = new SingleCityDefenseCampaignModel(0f, 0f);
            Assert.That(model.NotifyDefenseTowerCompleted(
                "building.instance.spawn-boundary",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true), Is.True);
            Array noBuildings = CombatTargets();

            Advance(model, 20f, 1, () => noBuildings, null);

            SingleCityDefenseEnemySnapshot spawned =
                model.Snapshot.Enemies.Single();
            Assert.That(spawned.X, Is.EqualTo(20f).Within(Tolerance),
                "An enemy born at the end of a step cannot act retroactively.");

            Advance(model, .1f, 1, () => noBuildings, null);
            Assert.That(model.Snapshot.Enemies.Single().X,
                Is.EqualTo(20f - EnemyCatalog.Gnawer.MoveSpeed * .1f)
                    .Within(Tolerance));
        }

        [Test]
        public void TargetProviderIsSkippedDuringWarningAndWithoutLiveEnemies()
        {
            var warning = new SingleCityDefenseCampaignModel(0f, 0f);
            Assert.That(warning.NotifyDefenseTowerCompleted(
                "building.instance.provider-warning",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true), Is.True);
            SingleCityDefenseCampaignModel empty = CombatModel(0f, 0f);
            var providerCalls = 0;
            Func<Array> provider = () =>
            {
                providerCalls++;
                return CombatTargets();
            };
            Func<string, string, int, int> applyDamage =
                (sourceId, targetId, damage) => damage;

            Advance(warning, 1f, 1, provider, applyDamage);
            Advance(empty, .1f, 1, provider, applyDamage);

            Assert.That(providerCalls, Is.Zero,
                "Inactive phases must not rebuild authoritative targets.");
        }

        [TestCase("core.enemy.gnawer", 8)]
        [TestCase("core.enemy.crystal-beast", 20)]
        [TestCase("core.enemy.howler", 12)]
        public void EnemyAtAttackRangeDamagesCoreForCatalogDps(
            string enemyId,
            int expectedDamage)
        {
            EnemyDefinition definition = Enemy(enemyId);
            SingleCityDefenseCampaignModel model = CombatModel(
                0f,
                0f,
                new EnemySeed(
                    "enemy.core-range",
                    definition,
                    0,
                    definition.AttackRange,
                    0f));

            Array noBuildings = CombatTargets();
            Advance(model, 1f, 1, () => noBuildings, null);

            Assert.That(model.Snapshot.CoreCurrentHealth,
                Is.EqualTo(2000 - expectedDamage));
            Assert.That(ReadSnapshotTarget(model, "enemy.core-range"),
                Is.EqualTo(SingleCityDefenseCampaignModel.CityCoreTargetId));
            Assert.That(ReadEnemyX(model, "enemy.core-range"),
                Is.EqualTo(definition.AttackRange).Within(Tolerance));
        }

        [TestCase(
            "core.enemy.crystal-beast",
            "core.building.wall",
            false,
            20)]
        [TestCase(
            "core.enemy.howler",
            "core.building.smelter",
            true,
            12)]
        public void PreferredBuildingReceivesDamageWhileCoreRemainsUntouched(
            string enemyId,
            string buildingId,
            bool isProduction,
            int expectedDamage)
        {
            const string targetId = "building.instance.preferred";
            EnemyDefinition definition = Enemy(enemyId);
            SingleCityDefenseCampaignModel model = CombatModel(
                coreX: 100f,
                coreZ: 0f,
                new EnemySeed(
                    "enemy.preferred",
                    definition,
                    0,
                    definition.AttackRange,
                    0f));
            Array targets = CombatTargets(new CombatTargetSeed(
                targetId,
                buildingId,
                0f,
                0f,
                isProduction));
            var damageByTarget = new Dictionary<string, int>(
                StringComparer.Ordinal);
            Func<string, string, int, int> applyDamage =
                (sourceEnemyId, stableTargetId, rawDamage) =>
                {
                    Assert.That(sourceEnemyId, Is.EqualTo("enemy.preferred"));
                    damageByTarget.TryGetValue(stableTargetId, out int before);
                    damageByTarget[stableTargetId] = before + rawDamage;
                    return rawDamage;
                };

            Advance(model, 1f, 1, () => targets, applyDamage);

            Assert.That(damageByTarget[targetId], Is.EqualTo(expectedDamage));
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(2000));
            Assert.That(ReadSnapshotTarget(model, "enemy.preferred"),
                Is.EqualTo(targetId));
        }

        [TestCase("core.enemy.gnawer", 8)]
        [TestCase("core.enemy.crystal-beast", 20)]
        [TestCase("core.enemy.howler", 12)]
        public void MissingOrIgnoredPreferredTargetFallsBackToCore(
            string enemyId,
            int expectedDamage)
        {
            EnemyDefinition definition = Enemy(enemyId);
            SingleCityDefenseCampaignModel model = CombatModel(
                0f,
                0f,
                new EnemySeed(
                    "enemy.fallback",
                    definition,
                    0,
                    definition.AttackRange,
                    0f));
            Array irrelevantWall = CombatTargets(new CombatTargetSeed(
                "building.instance.irrelevant-wall",
                BuildingCatalog.Wall.Id.Value,
                definition.AttackRange,
                0f,
                isProduction: false));
            var buildingDamageCalls = 0;
            Func<string, string, int, int> applyDamage =
                (sourceEnemyId, targetId, damage) =>
                {
                    buildingDamageCalls++;
                    return damage;
                };
            Array candidates = enemyId == EnemyCatalog.Gnawer.Id.Value
                ? irrelevantWall
                : CombatTargets();

            Advance(model, 1f, 1, () => candidates, applyDamage);

            Assert.That(buildingDamageCalls, Is.Zero);
            Assert.That(model.Snapshot.CoreCurrentHealth,
                Is.EqualTo(2000 - expectedDamage));
            Assert.That(ReadSnapshotTarget(model, "enemy.fallback"),
                Is.EqualTo(SingleCityDefenseCampaignModel.CityCoreTargetId));
        }

        [Test]
        public void DeadEnemyNeitherMovesNorAttacks()
        {
            EnemyDefinition definition = EnemyCatalog.CrystalBeast;
            SingleCityDefenseCampaignModel model = CombatModel(
                0f,
                0f,
                new EnemySeed("enemy.dead", definition, 0, 12f, 0f));
            SetEnemyHealth(model, "enemy.dead", 0);

            Array noBuildings = CombatTargets();
            Advance(model, 1f, 1, () => noBuildings, null);

            Assert.That(ReadEnemyX(model, "enemy.dead"),
                Is.EqualTo(12f).Within(Tolerance));
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(2000));
        }

        [Test]
        public void ZeroFreezesAndOneTwoSpeedsProduceEqualRuleCombat()
        {
            EnemyDefinition definition = EnemyCatalog.CrystalBeast;
            var seed = new EnemySeed(
                "enemy.speed",
                definition,
                0,
                definition.AttackRange,
                0f);
            SingleCityDefenseCampaignModel paused = CombatModel(0f, 0f, seed);
            SingleCityDefenseCampaignModel oneSpeed = CombatModel(0f, 0f, seed);
            SingleCityDefenseCampaignModel twoSpeed = CombatModel(0f, 0f, seed);
            Array noBuildings = CombatTargets();

            Advance(paused, 10f, 0, () => noBuildings, null);
            Advance(oneSpeed, 1f, 1, () => noBuildings, null);
            Advance(twoSpeed, .5f, 2, () => noBuildings, null);

            Assert.That(paused.Snapshot.CoreCurrentHealth, Is.EqualTo(2000));
            Assert.That(ReadEnemyX(paused, "enemy.speed"),
                Is.EqualTo(definition.AttackRange).Within(Tolerance));
            Assert.That(twoSpeed.Snapshot.CoreCurrentHealth,
                Is.EqualTo(oneSpeed.Snapshot.CoreCurrentHealth));
            Assert.That(ReadEnemyX(twoSpeed, "enemy.speed"),
                Is.EqualTo(ReadEnemyX(oneSpeed, "enemy.speed"))
                    .Within(Tolerance));
        }

        [Test]
        public void SpawnOrderThenStableIdDefinesEnemyProcessingOrder()
        {
            const string wallId = "building.instance.order-wall";
            EnemyDefinition definition = EnemyCatalog.CrystalBeast;
            SingleCityDefenseCampaignModel model = CombatModel(
                coreX: 100f,
                coreZ: 0f,
                new EnemySeed("enemy.order-b", definition, 1, 2f, 0f),
                new EnemySeed("enemy.order-c", definition, 0, 2f, 0f),
                new EnemySeed("enemy.order-a", definition, 1, 2f, 0f));
            Array targets = CombatTargets(new CombatTargetSeed(
                wallId,
                BuildingCatalog.Wall.Id.Value,
                0f,
                0f,
                isProduction: false));
            var sourceOrder = new List<string>();
            Func<string, string, int, int> applyDamage =
                (sourceEnemyId, targetId, damage) =>
                {
                    Assert.That(targetId, Is.EqualTo(wallId));
                    sourceOrder.Add(sourceEnemyId);
                    return damage;
                };

            Advance(model, .1f, 1, () => targets, applyDamage);

            Assert.That(sourceOrder, Is.EqualTo(new[]
            {
                "enemy.order-c",
                "enemy.order-a",
                "enemy.order-b",
            }));
        }

        [Test]
        public void CoreReachesZeroAtTwoThousandDamageAndCampaignDefeats()
        {
            EnemyDefinition definition = EnemyCatalog.CrystalBeast;
            SingleCityDefenseCampaignModel model = CombatModel(
                0f,
                0f,
                new EnemySeed(
                    "enemy.core-destruction",
                    definition,
                    0,
                    definition.AttackRange,
                    0f));

            Array noBuildings = CombatTargets();
            Advance(model, 100f, 1, () => noBuildings, null);

            Assert.That(model.Snapshot.CoreMaximumHealth, Is.EqualTo(2000));
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.Zero);
            Assert.That(model.Snapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Defeat));
            Assert.That(model.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Defeat));
        }

        [Test]
        public void DestroyedBuildingIsRequeriedAndCoreTargetedNextFixedStep()
        {
            const string wallId = "building.instance.destroyed-between-steps";
            EnemyDefinition definition = EnemyCatalog.CrystalBeast;
            SingleCityDefenseCampaignModel model = CombatModel(
                0f,
                0f,
                new EnemySeed(
                    "enemy.retarget-after-destruction",
                    definition,
                    0,
                    definition.AttackRange,
                    0f));
            var wallDestroyed = false;
            var buildingDamageCalls = 0;
            Func<Array> targets = () => CombatTargets(new CombatTargetSeed(
                wallId,
                BuildingCatalog.Wall.Id.Value,
                0f,
                0f,
                isProduction: false,
                isDestroyed: wallDestroyed));
            Func<string, string, int, int> applyDamage =
                (sourceEnemyId, targetId, rawDamage) =>
                {
                    buildingDamageCalls++;
                    wallDestroyed = true;
                    return rawDamage;
                };

            Advance(model, .2f, 1, targets, applyDamage);

            Assert.That(buildingDamageCalls, Is.EqualTo(1));
            Assert.That(model.Snapshot.CoreCurrentHealth, Is.EqualTo(1998));
            Assert.That(ReadSnapshotTarget(
                model,
                "enemy.retarget-after-destruction"),
                Is.EqualTo(SingleCityDefenseCampaignModel.CityCoreTargetId));
        }

        private static SingleCityDefenseCampaignModel CombatModel(
            float coreX,
            float coreZ,
            params EnemySeed[] enemies)
        {
            var model = new SingleCityDefenseCampaignModel(coreX, coreZ);
            SetField(model, "campaignTriggered", true);
            SetField(
                model,
                "phase",
                SingleCityDefenseCampaignPhase.CombatCleanup);
            SetField(model, "currentWave", CampaignWaveCatalog.All[0]);
            IList runtimeEnemies = (IList)RequireField(
                typeof(SingleCityDefenseCampaignModel),
                "enemies").GetValue(model);
            Type stateType = typeof(SingleCityDefenseCampaignModel)
                .GetNestedType("EnemyState", BindingFlags.NonPublic);
            Assert.That(stateType, Is.Not.Null,
                "The campaign enemy state remains the only runtime truth.");
            ConstructorInfo constructor = stateType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(string),
                    typeof(EnemyDefinition),
                    typeof(int),
                    typeof(float),
                    typeof(float),
                },
                null);
            Assert.That(constructor, Is.Not.Null);
            for (var index = 0; index < enemies.Length; index++)
            {
                EnemySeed enemy = enemies[index];
                runtimeEnemies.Add(constructor.Invoke(new object[]
                {
                    enemy.StableId,
                    enemy.Definition,
                    enemy.SpawnOrder,
                    enemy.X,
                    enemy.Z,
                }));
            }
            return model;
        }

        private static void Advance(
            SingleCityDefenseCampaignModel model,
            float deltaSeconds,
            int requestedSpeed,
            Func<Array> targets,
            Func<string, string, int, int> applyBuildingDamage)
        {
            Type targetArrayType = RequireCombatTargetType().MakeArrayType();
            Type targetProviderType = typeof(Func<>).MakeGenericType(
                targetArrayType);
            Delegate targetProvider = CreateTargetProvider(
                targetProviderType,
                targetArrayType,
                targets);
            MethodInfo method = typeof(SingleCityDefenseCampaignModel).GetMethod(
                "Advance",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(float),
                    typeof(int),
                    targetProviderType,
                    typeof(Func<string, string, int, int>),
                },
                null);
            Assert.That(method, Is.Not.Null,
                "The campaign must own one fixed-step combat path: " +
                "Advance(delta, speed, targetProvider, applyDamage).");
            method.Invoke(model, new object[]
            {
                deltaSeconds,
                requestedSpeed,
                targetProvider,
                applyBuildingDamage,
            });
        }

        private static Delegate CreateTargetProvider(
            Type providerType,
            Type targetArrayType,
            Func<Array> source)
        {
            MethodInfo invoke = typeof(Func<Array>).GetMethod("Invoke");
            Expression sourceCall = Expression.Call(
                Expression.Constant(source),
                invoke);
            return Expression.Lambda(
                    providerType,
                    Expression.Convert(sourceCall, targetArrayType))
                .Compile();
        }

        private static Array CombatTargets(params CombatTargetSeed[] targets)
        {
            Type type = RequireCombatTargetType();
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(string),
                typeof(string),
                typeof(float),
                typeof(float),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
            });
            Assert.That(constructor, Is.Not.Null,
                "DefenseBuildingCombatTarget needs stable identity, position " +
                "and existing targeting eligibility flags.");
            Array result = Array.CreateInstance(type, targets.Length);
            for (var index = 0; index < targets.Length; index++)
            {
                CombatTargetSeed target = targets[index];
                result.SetValue(constructor.Invoke(new object[]
                {
                    target.StableId,
                    target.BuildingId,
                    target.X,
                    target.Z,
                    true,
                    true,
                    target.IsDestroyed,
                    false,
                    target.IsProduction,
                }), index);
            }
            return result;
        }

        private static Type RequireCombatTargetType()
        {
            Type type = typeof(SingleCityDefenseCampaignModel).Assembly.GetType(
                CombatTargetTypeName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Missing transient pure combat target projection " +
                CombatTargetTypeName + ".");
            return type;
        }

        private static string ReadSnapshotTarget(
            SingleCityDefenseCampaignModel model,
            string stableEnemyId)
        {
            SingleCityDefenseEnemySnapshot enemy = model.Snapshot.Enemies
                .Single(value => value.StableId == stableEnemyId);
            PropertyInfo property = enemy.GetType().GetProperty(
                "TargetStableId",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                "Enemy snapshots must expose the campaign-owned target lock.");
            return property.GetValue(enemy) as string;
        }

        private static float ReadEnemyX(
            SingleCityDefenseCampaignModel model,
            string stableEnemyId)
        {
            object state = RuntimeEnemy(model, stableEnemyId);
            PropertyInfo property = state.GetType().GetProperty(
                "X",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null);
            return Convert.ToSingle(property.GetValue(state));
        }

        private static void SetEnemyHealth(
            SingleCityDefenseCampaignModel model,
            string stableEnemyId,
            int health)
        {
            object state = RuntimeEnemy(model, stableEnemyId);
            PropertyInfo property = state.GetType().GetProperty(
                "CurrentHealth",
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null.And.Property("CanWrite").True);
            property.SetValue(state, health);
        }

        private static object RuntimeEnemy(
            SingleCityDefenseCampaignModel model,
            string stableEnemyId)
        {
            IEnumerable states = (IEnumerable)RequireField(
                typeof(SingleCityDefenseCampaignModel),
                "enemies").GetValue(model);
            foreach (object state in states)
            {
                PropertyInfo stableId = state.GetType().GetProperty("StableId");
                if (string.Equals(
                        stableId?.GetValue(state) as string,
                        stableEnemyId,
                        StringComparison.Ordinal))
                {
                    return state;
                }
            }
            Assert.Fail("Missing injected campaign enemy " + stableEnemyId);
            return null;
        }

        private static EnemyDefinition Enemy(string stableId)
        {
            EnemyDefinition definition = EnemyCatalog.All.SingleOrDefault(
                value => string.Equals(
                    value.Id.Value,
                    stableId,
                    StringComparison.Ordinal));
            Assert.That(definition, Is.Not.Null, stableId);
            return definition;
        }

        private static FieldInfo RequireField(Type owner, string name)
        {
            FieldInfo field = owner.GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, owner.Name + "." + name);
            return field;
        }

        private static void SetField(object owner, string name, object value)
        {
            RequireField(owner.GetType(), name).SetValue(owner, value);
        }

        private readonly struct EnemySeed
        {
            public EnemySeed(
                string stableId,
                EnemyDefinition definition,
                int spawnOrder,
                float x,
                float z)
            {
                StableId = stableId;
                Definition = definition;
                SpawnOrder = spawnOrder;
                X = x;
                Z = z;
            }

            public string StableId { get; }
            public EnemyDefinition Definition { get; }
            public int SpawnOrder { get; }
            public float X { get; }
            public float Z { get; }
        }

        private readonly struct CombatTargetSeed
        {
            public CombatTargetSeed(
                string stableId,
                string buildingId,
                float x,
                float z,
                bool isProduction,
                bool isDestroyed = false)
            {
                StableId = stableId;
                BuildingId = buildingId;
                X = x;
                Z = z;
                IsProduction = isProduction;
                IsDestroyed = isDestroyed;
            }

            public string StableId { get; }
            public string BuildingId { get; }
            public float X { get; }
            public float Z { get; }
            public bool IsProduction { get; }
            public bool IsDestroyed { get; }
        }
    }
}
