using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;

namespace WasteCity.Tests
{
    public sealed class SingleCityDefenseCampaignPersistenceTests
    {
        private const string StateTypeName =
            "WasteCity.Defense.SingleCityDefenseCampaignPersistenceState";
        private const string EnemyStateTypeName =
            "WasteCity.Defense.SingleCityDefenseCampaignEnemyPersistenceState";
        private const string StatisticsStateTypeName =
            "WasteCity.Defense.SingleCityDefenseCampaignStatisticsPersistenceState";
        private const string RestorePlanTypeName =
            "WasteCity.Defense.SingleCityDefenseCampaignRestorePlan";
        private const float Tolerance = .001f;

        [Test]
        public void PublicContractExposesLosslessStateAndAtomicRestorePlan()
        {
            Type stateType = RequireType(StateTypeName);
            Type enemyType = RequireType(EnemyStateTypeName);
            Type statisticsType = RequireType(StatisticsStateTypeName);
            Type planType = RequireType(RestorePlanTypeName);

            RequireProperty(stateType, "CampaignId", typeof(string));
            RequireProperty(
                stateType,
                "Phase",
                typeof(SingleCityDefenseCampaignPhase));
            RequireProperty(stateType, "CurrentWaveNumber", typeof(int));
            RequireProperty(stateType, "WarningRemainingSeconds", typeof(float));
            RequireProperty(stateType, "SpawnClockSeconds", typeof(float));
            RequireProperty(
                stateType,
                "FixedStepAccumulatorSeconds",
                typeof(float));
            RequireProperty(stateType, "NextEnemyOrdinal", typeof(int));
            RequireProperty(stateType, "CoreCurrentHealth", typeof(int));
            RequireProperty(
                stateType,
                "Result",
                typeof(SingleCityDefenseCampaignResult));
            RequireCollectionProperty(stateType, "PlannedEnemyCountsByEnemyId");
            RequireCollectionProperty(stateType, "SpawnedEnemyCountsByEnemyId");
            RequireCollectionProperty(stateType, "DefeatedEnemyCountsByEnemyId");
            RequireCollectionProperty(stateType, "FrozenSpawnAnchors");
            RequireCollectionProperty(stateType, "Enemies");
            RequireProperty(stateType, "Statistics", statisticsType);

            RequireProperty(enemyType, "StableId", typeof(string));
            RequireProperty(enemyType, "EnemyDefinitionId", typeof(string));
            RequireProperty(enemyType, "SpawnOrder", typeof(int));
            RequireProperty(enemyType, "X", typeof(float));
            RequireProperty(enemyType, "Z", typeof(float));
            RequireProperty(enemyType, "CurrentHealth", typeof(int));
            RequireProperty(enemyType, "MovementRemainder", typeof(float));
            RequireProperty(enemyType, "AttackDamageRemainder", typeof(float));
            RequireProperty(enemyType, "TargetStableId", typeof(string));

            RequireProperty(statisticsType, "ElapsedRuleSeconds", typeof(float));
            RequireProperty(statisticsType, "SpawnedEnemyCount", typeof(int));
            RequireProperty(statisticsType, "DefeatedEnemyCount", typeof(int));
            RequireProperty(statisticsType, "CompletedWaveCount", typeof(int));
            RequireCollectionProperty(statisticsType, "KillsByEnemyId");
            RequireCollectionProperty(statisticsType, "DamageByTowerBuildingId");
            RequireCollectionProperty(statisticsType, "KillsByTowerBuildingId");
            RequireCollectionProperty(statisticsType, "ConsumablesSpentByResourceId");
            RequireCollectionProperty(statisticsType, "BuildingLossesByBuildingId");
            RequireProperty(statisticsType, "BuildingLossCount", typeof(int));
            RequireProperty(statisticsType, "CoreDamageTaken", typeof(int));
            RequireProperty(statisticsType, "HighestAliveEnemyCount", typeof(int));

            RequireMethod(
                typeof(SingleCityDefenseCampaignModel),
                "CaptureForPersistence",
                stateType,
                Type.EmptyTypes);
            RequireMethod(
                typeof(SingleCityDefenseCampaignModel),
                "TryPrepareRestore",
                typeof(bool),
                stateType,
                planType.MakeByRefType(),
                typeof(string).MakeByRefType());
            RequireMethod(
                typeof(SingleCityDefenseCampaignModel),
                "TryCommitRestore",
                typeof(bool),
                planType,
                typeof(string).MakeByRefType());
        }

        [Test]
        public void CaptureDeepCopiesAndCanonicalizesEveryCollection()
        {
            SingleCityDefenseCampaignModel model = TriggeredModel();
            AdvanceToCleanup(model);
            ReversePrivateEnemyStorage(model);
            model.RegisterConsumableSpent("resource.zeta", 2);
            model.RegisterConsumableSpent("resource.alpha", 1);
            string[] runtimeOrderBefore = PrivateEnemyIds(model);

            object first = Capture(model);
            object second = Capture(model);

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(Fingerprint(second), Is.EqualTo(Fingerprint(first)));
            Assert.That(PrivateEnemyIds(model), Is.EqualTo(runtimeOrderBefore),
                "Canonical capture must not reorder live campaign truth.");
            AssertDeepCopiedCollection(first, second, "Enemies");
            AssertSortedByString(ReadItems(first, "Enemies"), "StableId");
            AssertSortedByString(
                ReadItems(first, "PlannedEnemyCountsByEnemyId"),
                "EnemyDefinitionId");
            AssertSortedByString(
                ReadItems(first, "SpawnedEnemyCountsByEnemyId"),
                "EnemyDefinitionId");
            AssertSortedByString(
                ReadItems(first, "DefeatedEnemyCountsByEnemyId"),
                "EnemyDefinitionId");
            AssertSortedByInteger(
                ReadItems(first, "FrozenSpawnAnchors"),
                "Direction");

            object firstStatistics = Read(first, "Statistics");
            object secondStatistics = Read(second, "Statistics");
            Assert.That(secondStatistics, Is.Not.SameAs(firstStatistics));
            AssertDeepCopiedCollection(
                firstStatistics,
                secondStatistics,
                "ConsumablesSpentByResourceId");
            AssertSortedByString(
                ReadItems(firstStatistics, "ConsumablesSpentByResourceId"),
                "StableId");
        }

        [Test]
        public void CapturePreservesWarningSpawningCleanupAndBothTerminals()
        {
            SingleCityDefenseCampaignModel warning = TriggeredModel();
            AssertPhase(
                Capture(warning),
                SingleCityDefenseCampaignPhase.Warning,
                SingleCityDefenseCampaignResult.None);

            SingleCityDefenseCampaignModel spawning = TriggeredModel();
            CampaignWaveDefinition firstWave = CampaignWaveCatalog.All[0];
            spawning.Advance(firstWave.WarningSeconds + .1f, 1);
            AssertPhase(
                Capture(spawning),
                SingleCityDefenseCampaignPhase.SpawningAndCombat,
                SingleCityDefenseCampaignResult.None);

            SingleCityDefenseCampaignModel cleanup = TriggeredModel();
            AdvanceToCleanup(cleanup);
            AssertPhase(
                Capture(cleanup),
                SingleCityDefenseCampaignPhase.CombatCleanup,
                SingleCityDefenseCampaignResult.None);

            SingleCityDefenseCampaignModel defeat = TriggeredModel();
            defeat.ApplyCoreDamage(CityCoreCombatModel.FormalMaximumHealth);
            AssertPhase(
                Capture(defeat),
                SingleCityDefenseCampaignPhase.Defeat,
                SingleCityDefenseCampaignResult.Defeat);

            SingleCityDefenseCampaignModel victory = TriggeredModel();
            AdvanceToVictory(victory);
            AssertPhase(
                Capture(victory),
                SingleCityDefenseCampaignPhase.Victory,
                SingleCityDefenseCampaignResult.Victory);
        }

        [Test]
        public void EnemyCapturePreservesPositionHealthTargetAndRemainders()
        {
            SingleCityDefenseCampaignModel model = TriggeredModel(10f, 10f);
            CampaignWaveDefinition wave = CampaignWaveCatalog.All[0];
            float firstSpawnSeconds = wave.SpawnSeconds / wave.TotalCount;
            model.Advance(wave.WarningSeconds + firstSpawnSeconds + .1f, 1);
            SingleCityDefenseEnemySnapshot spawned = model.Snapshot.Enemies[0];
            SetPrivateEnemyPosition(model, spawned.StableId, 12f, 10f);
            int applied = model.ApplyTowerDamage(
                spawned.StableId,
                BuildingCatalog.MachineGunTurret.Id.Value,
                5);
            Assert.That(applied, Is.GreaterThan(0));
            model.Advance(
                .1f,
                1,
                () => Array.Empty<DefenseBuildingCombatTarget>(),
                null);

            SingleCityDefenseEnemySnapshot expected = FindEnemy(
                model.Snapshot,
                spawned.StableId);
            object persisted = FindByString(
                ReadItems(Capture(model), "Enemies"),
                "StableId",
                spawned.StableId);

            Assert.That(persisted, Is.Not.Null);
            Assert.That(ReadString(persisted, "EnemyDefinitionId"),
                Is.EqualTo(expected.EnemyDefinitionId));
            Assert.That(ReadInt(persisted, "SpawnOrder"),
                Is.EqualTo(expected.SpawnOrder));
            Assert.That(ReadFloat(persisted, "X"),
                Is.EqualTo(expected.X).Within(Tolerance));
            Assert.That(ReadFloat(persisted, "Z"),
                Is.EqualTo(expected.Z).Within(Tolerance));
            Assert.That(ReadInt(persisted, "CurrentHealth"),
                Is.EqualTo(expected.CurrentHealth));
            Assert.That(ReadString(persisted, "TargetStableId"),
                Is.EqualTo(SingleCityDefenseCampaignModel.CityCoreTargetId));
            Assert.That(ReadFloat(persisted, "AttackDamageRemainder"),
                Is.EqualTo(.8f).Within(Tolerance));
            Assert.That(ReadFloat(persisted, "MovementRemainder"),
                Is.Zero.Within(Tolerance));
        }

        [Test]
        public void CapturePreservesClocksOrdinalsCoreAndStatistics()
        {
            SingleCityDefenseCampaignModel model = TriggeredModel();
            CampaignWaveDefinition wave = CampaignWaveCatalog.All[0];
            float firstSpawnSeconds = wave.SpawnSeconds / wave.TotalCount;
            model.Advance(wave.WarningSeconds + firstSpawnSeconds + .1f, 1);
            string enemyId = model.Snapshot.Enemies[0].StableId;
            model.ApplyTowerDamage(
                enemyId,
                BuildingCatalog.LaserTower.Id.Value,
                3);
            Assert.That(model.DefeatEnemy(
                enemyId,
                BuildingCatalog.MachineGunTurret.Id.Value), Is.True);
            model.RegisterConsumableSpent("resource.beta", 4);
            model.RegisterConsumableSpent("resource.alpha", 2);
            model.RegisterBuildingLoss(BuildingCatalog.Warehouse.Id.Value);
            Assert.That(model.ApplyCoreDamage(7), Is.EqualTo(7));
            model.Advance(.06f, 1);

            object state = Capture(model);
            object statistics = Read(state, "Statistics");
            Assert.That(ReadString(state, "CampaignId"),
                Is.EqualTo(CampaignWaveCatalog.Id));
            Assert.That(ReadFloat(state, "SpawnClockSeconds"),
                Is.GreaterThan(0f));
            Assert.That(ReadFloat(state, "FixedStepAccumulatorSeconds"),
                Is.EqualTo(.06f).Within(Tolerance));
            Assert.That(ReadInt(state, "NextEnemyOrdinal"),
                Is.GreaterThanOrEqualTo(1));
            Assert.That(ReadInt(state, "CoreCurrentHealth"),
                Is.EqualTo(CityCoreCombatModel.FormalMaximumHealth - 7));
            Assert.That(ReadFloat(statistics, "ElapsedRuleSeconds"),
                Is.GreaterThan(0f));
            Assert.That(ReadInt(statistics, "SpawnedEnemyCount"),
                Is.GreaterThanOrEqualTo(1));
            Assert.That(ReadInt(statistics, "DefeatedEnemyCount"),
                Is.EqualTo(1));
            Assert.That(ReadInt(statistics, "BuildingLossCount"),
                Is.EqualTo(1));
            Assert.That(ReadInt(statistics, "CoreDamageTaken"),
                Is.EqualTo(7));
            Assert.That(ReadMetric(
                statistics,
                "KillsByEnemyId",
                EnemyCatalog.Gnawer.Id.Value), Is.EqualTo(1));
            Assert.That(ReadMetric(
                statistics,
                "ConsumablesSpentByResourceId",
                "resource.alpha"), Is.EqualTo(2));
            Assert.That(ReadMetric(
                statistics,
                "ConsumablesSpentByResourceId",
                "resource.beta"), Is.EqualTo(4));
            Assert.That(ReadMetric(
                statistics,
                "DamageByTowerBuildingId",
                BuildingCatalog.LaserTower.Id.Value), Is.GreaterThan(0));
        }

        [Test]
        public void BuildingLossesAreCanonicalAndRestoredByBuildingId()
        {
            SingleCityDefenseCampaignModel model = TriggeredModel();
            model.RegisterBuildingLoss(BuildingCatalog.Warehouse.Id.Value);
            model.RegisterBuildingLoss(BuildingCatalog.Wall.Id.Value);
            model.RegisterBuildingLoss("building.unknown.invalid");

            var captured = (SingleCityDefenseCampaignPersistenceState)
                Capture(model);
            Assert.That(captured.Statistics.BuildingLossCount, Is.EqualTo(2));
            Assert.That(ReadMetric(
                captured.Statistics,
                "BuildingLossesByBuildingId",
                BuildingCatalog.Wall.Id.Value), Is.EqualTo(1));
            Assert.That(ReadMetric(
                captured.Statistics,
                "BuildingLossesByBuildingId",
                BuildingCatalog.Warehouse.Id.Value), Is.EqualTo(1));
            AssertDeepCopiedCollection(
                captured.Statistics,
                ((SingleCityDefenseCampaignPersistenceState)
                    Capture(model)).Statistics,
                "BuildingLossesByBuildingId");
            AssertSortedByString(
                ReadItems(
                    captured.Statistics,
                    "BuildingLossesByBuildingId"),
                "StableId");

            var restored = new SingleCityDefenseCampaignModel(10f, 10f);
            Assert.That(TryPrepare(
                restored,
                captured,
                out object plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(TryCommit(restored, plan, out string commitError),
                Is.True, commitError);
            restored.RegisterBuildingLoss(BuildingCatalog.Wall.Id.Value);
            Assert.That(((SingleCityDefenseCampaignPersistenceState)
                    Capture(restored)).Statistics.BuildingLossCount,
                Is.EqualTo(3));
        }

        [Test]
        public void RestoreRejectsDuplicateOrUnknownBuildingLossIdsWithoutWriting()
        {
            SingleCityDefenseCampaignModel source = TriggeredModel();
            source.RegisterBuildingLoss(BuildingCatalog.Wall.Id.Value);
            var captured = (SingleCityDefenseCampaignPersistenceState)
                Capture(source);
            var duplicate = new[]
            {
                new SingleCityDefenseCampaignMetricPersistenceState(
                    BuildingCatalog.Wall.Id.Value,
                    1),
                new SingleCityDefenseCampaignMetricPersistenceState(
                    BuildingCatalog.Wall.Id.Value,
                    1),
            };
            var unknown = new[]
            {
                new SingleCityDefenseCampaignMetricPersistenceState(
                    "building.unknown.invalid",
                    1),
            };

            var target = new SingleCityDefenseCampaignModel(10f, 10f);
            string before = Fingerprint(Capture(target));
            Assert.That(TryPrepare(
                target,
                WithBuildingLosses(captured, duplicate, 2),
                out _,
                out _), Is.False);
            Assert.That(Fingerprint(Capture(target)), Is.EqualTo(before));
            Assert.That(TryPrepare(
                target,
                WithBuildingLosses(captured, unknown, 1),
                out _,
                out _), Is.False);
            Assert.That(Fingerprint(Capture(target)), Is.EqualTo(before));
        }

        [Test]
        public void TryPrepareRestoreIsZeroWrite()
        {
            SingleCityDefenseCampaignModel source = TriggeredModel();
            source.Advance(3.16f, 1);
            object state = Capture(source);
            string before = Fingerprint(Capture(source));

            bool prepared = TryPrepare(
                source,
                state,
                out object plan,
                out string error);

            Assert.That(prepared, Is.True, error);
            Assert.That(plan, Is.Not.Null);
            Assert.That(Fingerprint(Capture(source)), Is.EqualTo(before),
                "Restore preparation must not mutate campaign truth.");
        }

        [Test]
        public void CommitRejectsStaleForeignAndConsumedPlansWithoutPartialWrite()
        {
            SingleCityDefenseCampaignModel model = TriggeredModel();
            model.Advance(2.16f, 1);
            object oldState = Capture(model);
            Assert.That(TryPrepare(
                model,
                oldState,
                out object stalePlan,
                out string prepareError), Is.True, prepareError);
            model.Advance(.1f, 1);
            string advanced = Fingerprint(Capture(model));

            Assert.That(TryCommit(model, stalePlan, out _), Is.False,
                "A plan prepared against an older generation must fail.");
            Assert.That(Fingerprint(Capture(model)), Is.EqualTo(advanced));

            Assert.That(TryPrepare(
                model,
                oldState,
                out object validPlan,
                out prepareError), Is.True, prepareError);
            SingleCityDefenseCampaignModel foreign = TriggeredModel();
            string foreignBefore = Fingerprint(Capture(foreign));
            Assert.That(TryCommit(foreign, validPlan, out _), Is.False,
                "A restore plan must be owned by the model that prepared it.");
            Assert.That(Fingerprint(Capture(foreign)),
                Is.EqualTo(foreignBefore));

            Assert.That(TryCommit(model, validPlan, out string commitError),
                Is.True, commitError);
            string committed = Fingerprint(Capture(model));
            Assert.That(committed, Is.EqualTo(Fingerprint(oldState)));
            Assert.That(TryCommit(model, validPlan, out _), Is.False,
                "A committed restore plan must be single-use.");
            Assert.That(Fingerprint(Capture(model)), Is.EqualTo(committed));
        }

        [Test]
        public void RestoredCampaignContinuesIdenticallyAcrossFramePartitions()
        {
            SingleCityDefenseCampaignModel baseline = TriggeredModel();
            CampaignWaveDefinition wave = CampaignWaveCatalog.All[0];
            baseline.Advance(wave.WarningSeconds + 7.16f, 1);
            object saved = Capture(baseline);

            var restored = new SingleCityDefenseCampaignModel(10f, 10f);
            Assert.That(TryPrepare(
                restored,
                saved,
                out object plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(TryCommit(restored, plan, out string commitError),
                Is.True, commitError);
            Assert.That(Fingerprint(Capture(restored)),
                Is.EqualTo(Fingerprint(saved)));

            baseline.Advance(.34f, 1);
            restored.Advance(.07f, 1);
            restored.Advance(.11f, 1);
            restored.Advance(.16f, 1);

            Assert.That(Fingerprint(Capture(restored)),
                Is.EqualTo(Fingerprint(Capture(baseline))),
                "Restore must preserve both fixed-step and spawn-clock " +
                "remainders so frame partitioning cannot change the result.");
        }

        private static SingleCityDefenseCampaignModel TriggeredModel(
            float coreX = 10f,
            float coreZ = 10f)
        {
            var model = new SingleCityDefenseCampaignModel(coreX, coreZ);
            Assert.That(model.NotifyDefenseTowerCompleted(
                "building.instance.tower-001",
                BuildingCatalog.MachineGunTurret.Id.Value,
                isCompleted: true,
                isPlayerOwned: true), Is.True);
            return model;
        }

        private static void AdvanceToCleanup(
            SingleCityDefenseCampaignModel model)
        {
            CampaignWaveDefinition wave = CampaignWaveCatalog.All[
                model.Snapshot.CurrentWaveNumber - 1];
            model.Advance(
                wave.WarningSeconds + wave.SpawnSeconds + .2f,
                1);
            Assert.That(model.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.CombatCleanup));
        }

        private static void AdvanceToVictory(
            SingleCityDefenseCampaignModel model)
        {
            for (var waveIndex = 0;
                 waveIndex < CampaignWaveCatalog.All.Count;
                 waveIndex++)
            {
                AdvanceToCleanup(model);
                var ids = new List<string>();
                for (var index = 0;
                     index < model.Snapshot.Enemies.Count;
                     index++)
                {
                    ids.Add(model.Snapshot.Enemies[index].StableId);
                }
                for (var index = 0; index < ids.Count; index++)
                {
                    Assert.That(model.DefeatEnemy(
                        ids[index],
                        BuildingCatalog.MachineGunTurret.Id.Value), Is.True);
                }
                model.Advance(.1f, 1);
            }
            Assert.That(model.Snapshot.Result,
                Is.EqualTo(SingleCityDefenseCampaignResult.Victory));
        }

        private static object Capture(SingleCityDefenseCampaignModel model)
        {
            Type stateType = RequireType(StateTypeName);
            MethodInfo method = RequireMethod(
                typeof(SingleCityDefenseCampaignModel),
                "CaptureForPersistence",
                stateType,
                Type.EmptyTypes);
            object state = method.Invoke(model, null);
            Assert.That(state, Is.Not.Null);
            return state;
        }

        private static SingleCityDefenseCampaignPersistenceState
            WithBuildingLosses(
                SingleCityDefenseCampaignPersistenceState source,
                IEnumerable<SingleCityDefenseCampaignMetricPersistenceState>
                    losses,
                int total)
        {
            SingleCityDefenseCampaignStatisticsPersistenceState statistics =
                source.Statistics;
            var replacedStatistics = new
                SingleCityDefenseCampaignStatisticsPersistenceState(
                    statistics.ElapsedRuleSeconds,
                    statistics.SpawnedEnemyCount,
                    statistics.DefeatedEnemyCount,
                    statistics.CompletedWaveCount,
                    statistics.KillsByEnemyId,
                    statistics.HighestAliveEnemyCount,
                    statistics.CoreDamageTaken,
                    statistics.DamageByTowerBuildingId,
                    statistics.KillsByTowerBuildingId,
                    statistics.ConsumablesSpentByResourceId,
                    total,
                    losses,
                    statistics.PartialFromMigration);
            return new SingleCityDefenseCampaignPersistenceState(
                source.CampaignId,
                source.Phase,
                source.CurrentWaveNumber,
                source.WarningRemainingSeconds,
                source.SpawnClockSeconds,
                source.FixedStepAccumulatorSeconds,
                source.NextEnemyOrdinal,
                source.CoreCurrentHealth,
                source.Result,
                source.PlannedEnemyCountsByEnemyId,
                source.SpawnedEnemyCountsByEnemyId,
                source.DefeatedEnemyCountsByEnemyId,
                source.FrozenSpawnAnchors,
                source.Enemies,
                replacedStatistics);
        }

        private static bool TryPrepare(
            SingleCityDefenseCampaignModel model,
            object state,
            out object plan,
            out string error)
        {
            Type stateType = RequireType(StateTypeName);
            Type planType = RequireType(RestorePlanTypeName);
            MethodInfo method = RequireMethod(
                typeof(SingleCityDefenseCampaignModel),
                "TryPrepareRestore",
                typeof(bool),
                stateType,
                planType.MakeByRefType(),
                typeof(string).MakeByRefType());
            object[] arguments = { state, null, null };
            bool result = (bool)method.Invoke(model, arguments);
            plan = arguments[1];
            error = arguments[2] as string;
            return result;
        }

        private static bool TryCommit(
            SingleCityDefenseCampaignModel model,
            object plan,
            out string error)
        {
            Type planType = RequireType(RestorePlanTypeName);
            MethodInfo method = RequireMethod(
                typeof(SingleCityDefenseCampaignModel),
                "TryCommitRestore",
                typeof(bool),
                planType,
                typeof(string).MakeByRefType());
            object[] arguments = { plan, null };
            bool result = (bool)method.Invoke(model, arguments);
            error = arguments[1] as string;
            return result;
        }

        private static void AssertPhase(
            object state,
            SingleCityDefenseCampaignPhase phase,
            SingleCityDefenseCampaignResult result)
        {
            Assert.That(Read(state, "Phase"), Is.EqualTo(phase));
            Assert.That(Read(state, "Result"), Is.EqualTo(result));
            Assert.That(ReadInt(state, "CurrentWaveNumber"),
                Is.GreaterThanOrEqualTo(1));
        }

        private static SingleCityDefenseEnemySnapshot FindEnemy(
            SingleCityDefenseCampaignSnapshot snapshot,
            string stableId)
        {
            for (var index = 0; index < snapshot.Enemies.Count; index++)
            {
                if (string.Equals(
                    snapshot.Enemies[index].StableId,
                    stableId,
                    StringComparison.Ordinal))
                {
                    return snapshot.Enemies[index];
                }
            }
            Assert.Fail("Missing enemy snapshot " + stableId + ".");
            return null;
        }

        private static Type RequireType(string fullName)
        {
            Type type = typeof(SingleCityDefenseCampaignModel).Assembly.GetType(
                fullName,
                throwOnError: false);
            Assert.That(type, Is.Not.Null,
                "Task 6 requires pure persistence type " + fullName + ".");
            return type;
        }

        private static PropertyInfo RequireProperty(
            Type owner,
            string propertyName,
            Type expectedType = null)
        {
            PropertyInfo property = owner.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null,
                owner.FullName + " must expose " + propertyName + ".");
            Assert.That(property.CanRead, Is.True);
            if (expectedType != null)
                Assert.That(property.PropertyType, Is.EqualTo(expectedType));
            return property;
        }

        private static void RequireCollectionProperty(
            Type owner,
            string propertyName)
        {
            PropertyInfo property = RequireProperty(owner, propertyName);
            Assert.That(typeof(IEnumerable).IsAssignableFrom(
                property.PropertyType), Is.True,
                owner.FullName + "." + propertyName +
                " must expose an immutable enumerable projection.");
        }

        private static MethodInfo RequireMethod(
            Type owner,
            string name,
            Type returnType,
            params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Instance,
                null,
                parameters,
                null);
            Assert.That(method, Is.Not.Null,
                owner.FullName + " must expose " + name + ".");
            Assert.That(method.ReturnType, Is.EqualTo(returnType));
            return method;
        }

        private static object Read(object owner, string propertyName)
        {
            Assert.That(owner, Is.Not.Null);
            return RequireProperty(owner.GetType(), propertyName).GetValue(owner);
        }

        private static string ReadString(object owner, string propertyName)
        {
            return Read(owner, propertyName) as string;
        }

        private static int ReadInt(object owner, string propertyName)
        {
            return Convert.ToInt32(
                Read(owner, propertyName),
                CultureInfo.InvariantCulture);
        }

        private static float ReadFloat(object owner, string propertyName)
        {
            return Convert.ToSingle(
                Read(owner, propertyName),
                CultureInfo.InvariantCulture);
        }

        private static List<object> ReadItems(
            object owner,
            string propertyName)
        {
            object value = Read(owner, propertyName);
            Assert.That(value, Is.InstanceOf<IEnumerable>());
            var result = new List<object>();
            foreach (object item in (IEnumerable)value)
                result.Add(item);
            return result;
        }

        private static object FindByString(
            IReadOnlyList<object> values,
            string propertyName,
            string expected)
        {
            for (var index = 0; index < values.Count; index++)
            {
                object value = values[index];
                if (value != null && string.Equals(
                    ReadString(value, propertyName),
                    expected,
                    StringComparison.Ordinal))
                {
                    return value;
                }
            }
            return null;
        }

        private static int ReadMetric(
            object statistics,
            string propertyName,
            string stableId)
        {
            object value = FindByString(
                ReadItems(statistics, propertyName),
                "StableId",
                stableId);
            return value == null ? 0 : ReadInt(value, "Amount");
        }

        private static void AssertDeepCopiedCollection(
            object firstOwner,
            object secondOwner,
            string propertyName)
        {
            object firstCollection = Read(firstOwner, propertyName);
            object secondCollection = Read(secondOwner, propertyName);
            Assert.That(secondCollection, Is.Not.SameAs(firstCollection));
            List<object> first = ReadItems(firstOwner, propertyName);
            List<object> second = ReadItems(secondOwner, propertyName);
            Assert.That(second.Count, Is.EqualTo(first.Count));
            if (first.Count > 0 && first[0] != null)
                Assert.That(second[0], Is.Not.SameAs(first[0]));
        }

        private static void AssertSortedByString(
            IReadOnlyList<object> values,
            string propertyName)
        {
            string previous = null;
            for (var index = 0; index < values.Count; index++)
            {
                Assert.That(values[index], Is.Not.Null);
                string current = ReadString(values[index], propertyName);
                Assert.That(current, Is.Not.Null.And.Not.Empty);
                if (previous != null)
                {
                    Assert.That(
                        string.CompareOrdinal(previous, current),
                        Is.LessThan(0),
                        propertyName + " collection is not canonical.");
                }
                previous = current;
            }
        }

        private static void AssertSortedByInteger(
            IReadOnlyList<object> values,
            string propertyName)
        {
            var previous = int.MinValue;
            for (var index = 0; index < values.Count; index++)
            {
                Assert.That(values[index], Is.Not.Null);
                int current = Convert.ToInt32(
                    Read(values[index], propertyName),
                    CultureInfo.InvariantCulture);
                Assert.That(current, Is.GreaterThan(previous));
                previous = current;
            }
        }

        private static void ReversePrivateEnemyStorage(
            SingleCityDefenseCampaignModel model)
        {
            IList enemies = PrivateEnemies(model);
            for (var left = 0; left < enemies.Count / 2; left++)
            {
                int right = enemies.Count - 1 - left;
                object temporary = enemies[left];
                enemies[left] = enemies[right];
                enemies[right] = temporary;
            }
        }

        private static string[] PrivateEnemyIds(
            SingleCityDefenseCampaignModel model)
        {
            IList enemies = PrivateEnemies(model);
            var result = new string[enemies.Count];
            for (var index = 0; index < enemies.Count; index++)
                result[index] = ReadString(enemies[index], "StableId");
            return result;
        }

        private static IList PrivateEnemies(
            SingleCityDefenseCampaignModel model)
        {
            FieldInfo field = typeof(SingleCityDefenseCampaignModel).GetField(
                "enemies",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            IList enemies = field.GetValue(model) as IList;
            Assert.That(enemies, Is.Not.Null);
            return enemies;
        }

        private static void SetPrivateEnemyPosition(
            SingleCityDefenseCampaignModel model,
            string stableId,
            float x,
            float z)
        {
            IList enemies = PrivateEnemies(model);
            for (var index = 0; index < enemies.Count; index++)
            {
                object enemy = enemies[index];
                if (!string.Equals(
                    ReadString(enemy, "StableId"),
                    stableId,
                    StringComparison.Ordinal))
                {
                    continue;
                }
                RequireProperty(enemy.GetType(), "X").SetValue(enemy, x);
                RequireProperty(enemy.GetType(), "Z").SetValue(enemy, z);
                return;
            }
            Assert.Fail("Missing private enemy state " + stableId + ".");
        }

        private static string Fingerprint(object value)
        {
            var builder = new StringBuilder();
            AppendFingerprint(builder, value);
            return builder.ToString();
        }

        private static void AppendFingerprint(
            StringBuilder builder,
            object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }
            Type type = value.GetType();
            if (value is string text)
            {
                builder.Append('"').Append(text).Append('"');
                return;
            }
            if (type.IsPrimitive || type.IsEnum || value is decimal)
            {
                builder.Append(Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture));
                return;
            }
            if (value is IEnumerable sequence)
            {
                builder.Append('[');
                foreach (object item in sequence)
                {
                    AppendFingerprint(builder, item);
                    builder.Append(';');
                }
                builder.Append(']');
                return;
            }

            builder.Append(type.FullName).Append('{');
            PropertyInfo[] properties = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(properties, (left, right) => string.CompareOrdinal(
                left.Name,
                right.Name));
            for (var index = 0; index < properties.Length; index++)
            {
                PropertyInfo property = properties[index];
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                builder.Append(property.Name).Append('=');
                AppendFingerprint(builder, property.GetValue(value));
                builder.Append(';');
            }
            builder.Append('}');
        }
    }
}
