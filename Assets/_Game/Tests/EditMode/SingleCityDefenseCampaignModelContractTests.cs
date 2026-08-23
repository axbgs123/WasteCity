using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseCampaignModelContractTests
    {
        private const string ModelTypeName =
            "WasteCity.Defense.SingleCityDefenseCampaignModel";
        private const string SnapshotTypeName =
            "WasteCity.Defense.SingleCityDefenseCampaignSnapshot";
        private const string CandidateTypeName =
            "WasteCity.Defense.DefenseBuildingTargetCandidate";
        private const float Tolerance = .001f;

        private static readonly string[] FormalTowerIds =
        {
            "core.building.machine-gun-turret",
            "core.building.laser-tower",
            "biological.building.spore-tower",
        };

        [Test]
        public void PublicPureModelContractExposesFixedStepCommandsAndSnapshot()
        {
            Type modelType = RequireType(ModelTypeName);
            Type snapshotType = RequireType(SnapshotTypeName);
            Type candidateType = RequireType(CandidateTypeName);

            FieldInfo fixedStep = RequirePublicStaticField(
                modelType,
                "FormalFixedStepSeconds");
            Assert.That(
                Convert.ToSingle(fixedStep.GetValue(null)),
                Is.EqualTo(.1f).Within(Tolerance));

            RequireConstructor(modelType, typeof(float), typeof(float));
            RequireProperty(modelType, "Snapshot", snapshotType);
            RequireInstanceMethod(
                modelType,
                "NotifyDefenseTowerCompleted",
                typeof(bool),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool));
            RequireInstanceMethod(
                modelType,
                "Advance",
                typeof(void),
                typeof(float),
                typeof(int));
            RequireInstanceMethod(
                modelType,
                "DefeatEnemy",
                typeof(bool),
                typeof(string),
                typeof(string));
            RequireInstanceMethod(
                modelType,
                "ResolveEnemyTarget",
                typeof(string),
                typeof(string),
                candidateType.MakeArrayType());
            RequireInstanceMethod(
                modelType,
                "ResolveTowerDamage",
                typeof(int),
                typeof(string),
                typeof(string),
                typeof(int));

            MethodInfo terminal = modelType.GetMethod(
                "ResolveTerminalResult",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(bool), typeof(int), typeof(int) },
                null);
            Assert.That(
                terminal,
                Is.Not.Null,
                "The pure terminal rule must expose " +
                "ResolveTerminalResult(wave, allSpawned, alive, coreHealth).");
        }

        [Test]
        public void FixedPointOneSecondStepIsIndependentOfAdvancePartition()
        {
            object whole = CreateTriggeredModel(FormalTowerIds[0]);
            object split = CreateTriggeredModel(FormalTowerIds[0]);

            Advance(whole, 55.5f, 1);
            for (var index = 0; index < 555; index++)
                Advance(split, .1f, 1);

            AssertEquivalentCombatSnapshot(
                ReadSnapshot(whole),
                ReadSnapshot(split));
        }

        [Test]
        public void AnyCompletedPlayerFormalTowerTriggersCampaignExactlyOnce()
        {
            for (var index = 0; index < FormalTowerIds.Length; index++)
            {
                object model = CreateModel();
                string towerId = FormalTowerIds[index];

                Assert.That(
                    NotifyTower(
                        model,
                        "tower.incomplete." + index,
                        towerId,
                        isCompleted: false,
                        isPlayerOwned: true),
                    Is.False);
                Assert.That(
                    NotifyTower(
                        model,
                        "tower.foreign." + index,
                        towerId,
                        isCompleted: true,
                        isPlayerOwned: false),
                    Is.False);
                Assert.That(
                    NotifyTower(
                        model,
                        "tower.valid." + index,
                        towerId,
                        isCompleted: true,
                        isPlayerOwned: true),
                    Is.True,
                    towerId);
                Assert.That(
                    ReadString(ReadSnapshot(model), "Phase"),
                    Is.EqualTo("Warning"));
                Assert.That(
                    NotifyTower(
                        model,
                        "tower.second." + index,
                        FormalTowerIds[(index + 1) % FormalTowerIds.Length],
                        isCompleted: true,
                        isPlayerOwned: true),
                    Is.False,
                    "A campaign already triggered by one formal tower " +
                    "must not trigger again.");
            }
        }

        [Test]
        public void WaveAdvancesOnlyAfterAllPlannedEnemiesSpawnAndDie()
        {
            object model = CreateTriggeredModel(FormalTowerIds[0]);
            Advance(model, 55f, 1);

            object waveOne = ReadSnapshot(model);
            Assert.That(ReadInt(waveOne, "CurrentWaveNumber"), Is.EqualTo(1));
            Assert.That(ReadInt(waveOne, "SpawnedEnemyCount"), Is.EqualTo(8));
            Assert.That(ReadInt(waveOne, "AliveEnemyCount"), Is.EqualTo(8));

            List<string> waveOneIds = ReadEnemyIds(waveOne);
            for (var index = 0; index < waveOneIds.Count - 1; index++)
                Assert.That(DefeatEnemy(model, waveOneIds[index]), Is.True);
            Advance(model, .1f, 1);

            object oneAlive = ReadSnapshot(model);
            Assert.That(ReadInt(oneAlive, "CurrentWaveNumber"), Is.EqualTo(1));
            Assert.That(ReadInt(oneAlive, "AliveEnemyCount"), Is.EqualTo(1));

            Assert.That(DefeatEnemy(model, waveOneIds[waveOneIds.Count - 1]),
                Is.True);
            Advance(model, .1f, 1);
            object waveTwoWarning = ReadSnapshot(model);
            Assert.That(
                ReadInt(waveTwoWarning, "CurrentWaveNumber"),
                Is.EqualTo(2));
            Assert.That(ReadString(waveTwoWarning, "Phase"),
                Is.EqualTo("Warning"));
        }

        [Test]
        public void ExactlyNinetyPercentDefeatedCannotCompleteWave()
        {
            object model = CreateTriggeredModel(FormalTowerIds[0]);
            Advance(model, 55f, 1);
            DefeatAllVisibleEnemies(model);
            Advance(model, .1f, 1);

            Assert.That(ReadInt(ReadSnapshot(model), "CurrentWaveNumber"),
                Is.EqualTo(2));
            Advance(model, 65f, 1);

            object waveTwo = ReadSnapshot(model);
            Assert.That(ReadInt(waveTwo, "SpawnedEnemyCount"), Is.EqualTo(10));
            List<string> ids = ReadEnemyIds(waveTwo);
            for (var index = 0; index < 9; index++)
                Assert.That(DefeatEnemy(model, ids[index]), Is.True);
            Advance(model, .1f, 1);

            object ninetyPercent = ReadSnapshot(model);
            Assert.That(ReadInt(ninetyPercent, "CurrentWaveNumber"),
                Is.EqualTo(2));
            Assert.That(ReadInt(ninetyPercent, "AliveEnemyCount"),
                Is.EqualTo(1));
            Assert.That(ReadString(ninetyPercent, "Phase"),
                Is.Not.EqualTo("Warning"),
                "Defeating 9/10 enemies must not start wave three.");
        }

        [Test]
        public void EnemyTargetRulesAreCoreWallsAndProductionWithStableFallbacks()
        {
            object model = CreateModel();
            Array candidates = CreateCandidates(
                Candidate(
                    "building.wall.b",
                    BuildingCatalog.Wall.Id.Value,
                    3f,
                    production: false),
                Candidate(
                    "building.wall.a",
                    BuildingCatalog.Wall.Id.Value,
                    3f,
                    production: false),
                Candidate(
                    "building.smelter",
                    BuildingCatalog.Smelter.Id.Value,
                    2f,
                    production: true),
                Candidate(
                    "building.warehouse",
                    BuildingCatalog.Warehouse.Id.Value,
                    1f,
                    production: false));

            string coreTargetId = ReadPublicStaticString(
                RequireType(ModelTypeName),
                "CityCoreTargetId");
            Assert.That(
                ResolveTarget(model, EnemyCatalog.Gnawer.Id.Value, candidates),
                Is.EqualTo(coreTargetId),
                "Gnawers always target the core.");
            Assert.That(
                ResolveTarget(
                    model,
                    EnemyCatalog.CrystalBeast.Id.Value,
                    candidates),
                Is.EqualTo("building.wall.a"),
                "Crystal beasts choose the nearest wall, then stable ID.");
            Assert.That(
                ResolveTarget(model, EnemyCatalog.Howler.Id.Value, candidates),
                Is.EqualTo("building.smelter"),
                "Howlers choose the nearest production building.");

            Array noPreferredTargets = CreateCandidates(
                Candidate(
                    "building.warehouse.only",
                    BuildingCatalog.Warehouse.Id.Value,
                    1f,
                    production: false));
            Assert.That(
                ResolveTarget(
                    model,
                    EnemyCatalog.CrystalBeast.Id.Value,
                    noPreferredTargets),
                Is.EqualTo(coreTargetId));
            Assert.That(
                ResolveTarget(
                    model,
                    EnemyCatalog.Howler.Id.Value,
                    noPreferredTargets),
                Is.EqualTo(coreTargetId));
        }

        [Test]
        public void ThreeTowerDamageUsesTheExistingDamageMatrix()
        {
            object model = CreateModel();
            AssertTowerDamage(
                model,
                FormalTowerIds[0],
                EnemyCatalog.CrystalBeast,
                rawDamage: 100);
            AssertTowerDamage(
                model,
                FormalTowerIds[1],
                EnemyCatalog.CrystalBeast,
                rawDamage: 100);
            AssertTowerDamage(
                model,
                FormalTowerIds[2],
                EnemyCatalog.Gnawer,
                rawDamage: 100);
        }

        [Test]
        public void ZeroOneAndTwoSpeedShareOneRuleClock()
        {
            object paused = CreateTriggeredModel(FormalTowerIds[0]);
            object oneSpeed = CreateTriggeredModel(FormalTowerIds[0]);
            object twoSpeed = CreateTriggeredModel(FormalTowerIds[0]);
            object pausedBefore = ReadSnapshot(paused);

            Advance(paused, 100f, 0);
            AssertEquivalentCombatSnapshot(pausedBefore, ReadSnapshot(paused));

            Advance(oneSpeed, 10f, 1);
            Advance(twoSpeed, 5f, 2);
            AssertEquivalentCombatSnapshot(
                ReadSnapshot(oneSpeed),
                ReadSnapshot(twoSpeed));
        }

        [Test]
        public void SynchronousCoreDestructionWinsTerminalPriorityOverWaveTenClear()
        {
            Type modelType = RequireType(ModelTypeName);
            MethodInfo resolve = modelType.GetMethod(
                "ResolveTerminalResult",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(bool), typeof(int), typeof(int) },
                null);
            Assert.That(resolve, Is.Not.Null);

            object result = resolve.Invoke(
                null,
                new object[]
                {
                    10,
                    true,
                    0,
                    0,
                });
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToString(), Is.EqualTo("Defeat"),
                "Core destruction has priority when the final enemy dies " +
                "in the same fixed step.");

            object survivingCore = resolve.Invoke(
                null,
                new object[]
                {
                    10,
                    true,
                    0,
                    1,
                });
            Assert.That(survivingCore.ToString(), Is.EqualTo("Victory"));
        }

        [Test]
        public void FinalEnemyKillDoesNotCommitVictoryBeforeSameStepCoreDamage()
        {
            object model = CreateTriggeredModel(FormalTowerIds[0]);
            for (var wave = 1; wave < 10; wave++)
            {
                Advance(model, 200f, 1);
                DefeatAllVisibleEnemies(model);
                Advance(model, .1f, 1);
            }

            Advance(model, 200f, 1);
            List<string> finalEnemies = ReadEnemyIds(ReadSnapshot(model));
            Assert.That(finalEnemies, Has.Count.EqualTo(46));
            for (var index = 0; index < finalEnemies.Count; index++)
                Assert.That(DefeatEnemy(model, finalEnemies[index]), Is.True);

            MethodInfo applyCoreDamage = RequireInstanceMethod(
                model.GetType(),
                "ApplyCoreDamage",
                typeof(int),
                typeof(int));
            applyCoreDamage.Invoke(model, new object[] { 2000 });
            Advance(model, .1f, 1);

            Assert.That(
                ReadString(ReadSnapshot(model), "Result"),
                Is.EqualTo("Defeat"),
                "Terminal arbitration must occur after all same-step damage " +
                "and must prefer core destruction over wave-ten clear.");
        }

        [Test]
        public void ImmutableSnapshotPublishesRequiredCampaignStatistics()
        {
            Type snapshotType = RequireType(SnapshotTypeName);
            PropertyInfo statisticsProperty = RequireProperty(
                snapshotType,
                "Statistics");
            Type statisticsType = statisticsProperty.PropertyType;

            RequireProperty(statisticsType, "ElapsedRuleSeconds");
            RequireProperty(statisticsType, "CompletedWaveCount");
            RequireProperty(statisticsType, "TotalKillCount");
            RequireProperty(statisticsType, "KillsByEnemyId");
            RequireProperty(statisticsType, "DamageByTowerBuildingId");
            RequireProperty(statisticsType, "KillsByTowerBuildingId");
            RequireProperty(statisticsType, "ConsumablesSpentByResourceId");
            RequireProperty(statisticsType, "BuildingLossCount");
            RequireProperty(statisticsType, "CoreCurrentHealth");
            RequireProperty(statisticsType, "CoreMaximumHealth");
            RequireProperty(statisticsType, "HighestAliveEnemyCount");
        }

        private static object CreateModel()
        {
            Type type = RequireType(ModelTypeName);
            ConstructorInfo constructor = RequireConstructor(
                type,
                typeof(float),
                typeof(float));
            return constructor.Invoke(new object[] { 0f, 0f });
        }

        private static object CreateTriggeredModel(string towerBuildingId)
        {
            object model = CreateModel();
            Assert.That(
                NotifyTower(
                    model,
                    "building.instance.trigger",
                    towerBuildingId,
                    isCompleted: true,
                    isPlayerOwned: true),
                Is.True);
            return model;
        }

        private static bool NotifyTower(
            object model,
            string stableInstanceId,
            string buildingId,
            bool isCompleted,
            bool isPlayerOwned)
        {
            MethodInfo method = RequireInstanceMethod(
                model.GetType(),
                "NotifyDefenseTowerCompleted",
                typeof(bool),
                typeof(string),
                typeof(string),
                typeof(bool),
                typeof(bool));
            return (bool)method.Invoke(
                model,
                new object[]
                {
                    stableInstanceId,
                    buildingId,
                    isCompleted,
                    isPlayerOwned,
                });
        }

        private static void Advance(object model, float delta, int speed)
        {
            MethodInfo method = RequireInstanceMethod(
                model.GetType(),
                "Advance",
                typeof(void),
                typeof(float),
                typeof(int));
            method.Invoke(model, new object[] { delta, speed });
        }

        private static bool DefeatEnemy(object model, string stableEnemyId)
        {
            MethodInfo method = RequireInstanceMethod(
                model.GetType(),
                "DefeatEnemy",
                typeof(bool),
                typeof(string),
                typeof(string));
            return (bool)method.Invoke(
                model,
                new object[] { stableEnemyId, FormalTowerIds[0] });
        }

        private static void DefeatAllVisibleEnemies(object model)
        {
            List<string> ids = ReadEnemyIds(ReadSnapshot(model));
            for (var index = 0; index < ids.Count; index++)
                Assert.That(DefeatEnemy(model, ids[index]), Is.True);
        }

        private static object ReadSnapshot(object model)
        {
            return RequireProperty(model.GetType(), "Snapshot").GetValue(model);
        }

        private static List<string> ReadEnemyIds(object snapshot)
        {
            IEnumerable enemies = (IEnumerable)ReadProperty(snapshot, "Enemies");
            var result = new List<string>();
            foreach (object enemy in enemies)
                result.Add(ReadString(enemy, "StableId"));
            return result;
        }

        private static void AssertEquivalentCombatSnapshot(
            object expected,
            object actual)
        {
            Assert.That(ReadInt(actual, "CurrentWaveNumber"),
                Is.EqualTo(ReadInt(expected, "CurrentWaveNumber")));
            Assert.That(ReadString(actual, "Phase"),
                Is.EqualTo(ReadString(expected, "Phase")));
            Assert.That(ReadInt(actual, "SpawnedEnemyCount"),
                Is.EqualTo(ReadInt(expected, "SpawnedEnemyCount")));
            Assert.That(ReadInt(actual, "AliveEnemyCount"),
                Is.EqualTo(ReadInt(expected, "AliveEnemyCount")));
            Assert.That(ReadInt(actual, "CoreCurrentHealth"),
                Is.EqualTo(ReadInt(expected, "CoreCurrentHealth")));

            IList expectedEnemies = ToList(
                (IEnumerable)ReadProperty(expected, "Enemies"));
            IList actualEnemies = ToList(
                (IEnumerable)ReadProperty(actual, "Enemies"));
            Assert.That(actualEnemies.Count, Is.EqualTo(expectedEnemies.Count));
            for (var index = 0; index < expectedEnemies.Count; index++)
            {
                object expectedEnemy = expectedEnemies[index];
                object actualEnemy = actualEnemies[index];
                Assert.That(ReadString(actualEnemy, "StableId"),
                    Is.EqualTo(ReadString(expectedEnemy, "StableId")));
                Assert.That(ReadString(actualEnemy, "EnemyDefinitionId"),
                    Is.EqualTo(ReadString(expectedEnemy, "EnemyDefinitionId")));
                Assert.That(ReadInt(actualEnemy, "CurrentHealth"),
                    Is.EqualTo(ReadInt(expectedEnemy, "CurrentHealth")));
                Assert.That(ReadFloat(actualEnemy, "X"),
                    Is.EqualTo(ReadFloat(expectedEnemy, "X")).Within(Tolerance));
                Assert.That(ReadFloat(actualEnemy, "Z"),
                    Is.EqualTo(ReadFloat(expectedEnemy, "Z")).Within(Tolerance));
            }
        }

        private static void AssertTowerDamage(
            object model,
            string towerBuildingId,
            EnemyDefinition enemy,
            int rawDamage)
        {
            DefenseTowerDefinition tower = DefenseTowerCatalog.For(
                towerBuildingId);
            Assert.That(tower, Is.Not.Null, towerBuildingId);
            int expected = DamageMatrix.Apply(
                rawDamage,
                tower.DamageType,
                enemy.Armor);
            MethodInfo method = RequireInstanceMethod(
                model.GetType(),
                "ResolveTowerDamage",
                typeof(int),
                typeof(string),
                typeof(string),
                typeof(int));
            int actual = (int)method.Invoke(
                model,
                new object[] { towerBuildingId, enemy.Id.Value, rawDamage });
            Assert.That(actual, Is.EqualTo(expected));
        }

        private sealed class CandidateValues
        {
            public CandidateValues(
                string stableId,
                string buildingId,
                float distance,
                bool production)
            {
                StableId = stableId;
                BuildingId = buildingId;
                Distance = distance;
                Production = production;
            }

            public string StableId { get; }
            public string BuildingId { get; }
            public float Distance { get; }
            public bool Production { get; }
        }

        private static CandidateValues Candidate(
            string stableId,
            string buildingId,
            float distance,
            bool production)
        {
            return new CandidateValues(
                stableId,
                buildingId,
                distance,
                production);
        }

        private static Array CreateCandidates(params CandidateValues[] values)
        {
            Type candidateType = RequireType(CandidateTypeName);
            ConstructorInfo constructor = RequireConstructor(
                candidateType,
                typeof(string),
                typeof(string),
                typeof(float),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool));
            Array result = Array.CreateInstance(candidateType, values.Length);
            for (var index = 0; index < values.Length; index++)
            {
                CandidateValues value = values[index];
                result.SetValue(
                    constructor.Invoke(
                        new object[]
                        {
                            value.StableId,
                            value.BuildingId,
                            value.Distance,
                            true,
                            true,
                            false,
                            value.Production,
                        }),
                    index);
            }
            return result;
        }

        private static string ResolveTarget(
            object model,
            string enemyDefinitionId,
            Array candidates)
        {
            MethodInfo method = RequireInstanceMethod(
                model.GetType(),
                "ResolveEnemyTarget",
                typeof(string),
                typeof(string),
                candidates.GetType());
            return (string)method.Invoke(
                model,
                new object[] { enemyDefinitionId, candidates });
        }

        private static IList ToList(IEnumerable values)
        {
            var result = new ArrayList();
            foreach (object value in values) result.Add(value);
            return result;
        }

        private static int ReadInt(object owner, string name)
        {
            return Convert.ToInt32(ReadProperty(owner, name));
        }

        private static float ReadFloat(object owner, string name)
        {
            return Convert.ToSingle(ReadProperty(owner, name));
        }

        private static string ReadString(object owner, string name)
        {
            object value = ReadProperty(owner, name);
            return value == null ? null : value.ToString();
        }

        private static object ReadProperty(object owner, string name)
        {
            PropertyInfo property = RequireProperty(owner.GetType(), name);
            return property.GetValue(owner);
        }

        private static string ReadPublicStaticString(Type owner, string name)
        {
            FieldInfo field = RequirePublicStaticField(owner, name);
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)));
            return (string)field.GetValue(null);
        }

        private static Type RequireType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            Assert.Fail(
                "IDEA-0017 RED: missing public pure-model type " + fullName);
            return null;
        }

        private static ConstructorInfo RequireConstructor(
            Type owner,
            params Type[] parameters)
        {
            ConstructorInfo constructor = owner.GetConstructor(parameters);
            Assert.That(
                constructor,
                Is.Not.Null,
                "IDEA-0017 RED: missing public constructor " + owner.FullName);
            return constructor;
        }

        private static FieldInfo RequirePublicStaticField(
            Type owner,
            string name)
        {
            FieldInfo field = owner.GetField(
                name,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(
                field,
                Is.Not.Null,
                "IDEA-0017 RED: missing public static field " +
                owner.FullName + "." + name);
            return field;
        }

        private static PropertyInfo RequireProperty(Type owner, string name)
        {
            PropertyInfo property = owner.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(
                property,
                Is.Not.Null,
                "IDEA-0017 RED: missing public property " +
                owner.FullName + "." + name);
            return property;
        }

        private static PropertyInfo RequireProperty(
            Type owner,
            string name,
            Type propertyType)
        {
            PropertyInfo property = RequireProperty(owner, name);
            Assert.That(property.PropertyType, Is.EqualTo(propertyType));
            return property;
        }

        private static MethodInfo RequireInstanceMethod(
            Type owner,
            string name,
            Type returnType,
            params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameters,
                null);
            Assert.That(
                method,
                Is.Not.Null,
                "IDEA-0017 RED: missing public method " +
                owner.FullName + "." + name);
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
            return method;
        }
    }
}
