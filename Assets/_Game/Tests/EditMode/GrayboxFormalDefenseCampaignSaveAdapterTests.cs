using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Graybox3D.Building;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalDefenseCampaignSaveAdapterTests
    {
        private const string RuntimeCaptureMethod =
            "CaptureFormalCampaignForPersistence";
        private const string AdapterCaptureMethod = "CaptureCampaign";
        private const string AdapterPrepareMethod =
            "TryPrepareCampaignRestore";
        private const string AdapterCommitMethod =
            "TryCommitCampaignRestore";

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void FormalRuntimeCaptureReturnsCampaignThreeTowersAndBuildingHealthWithoutTutorial()
        {
            MethodInfo capture = typeof(GrayboxDefenseRuntime3D).GetMethod(
                RuntimeCaptureMethod,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(capture, Is.Not.Null,
                "IDEA-0017 schema 32 requires a formal runtime aggregate " +
                "capture separate from the retired tutorial persistence API.");
            Assert.That(capture.GetParameters(), Is.Empty);

            Type aggregate = capture.ReturnType;
            AssertReadOnlyProperty(aggregate, "Campaign");
            AssertReadOnlyProperty(aggregate, "Towers");
            AssertReadOnlyProperty(aggregate, "BuildingHealth");
            Assert.That(aggregate.GetProperty(
                "Tutorial",
                BindingFlags.Instance | BindingFlags.Public), Is.Null,
                "Legacy tutorial state must not remain schema 32 truth.");
        }

        [Test]
        public void SchemaThirtyTwoCaptureIsCanonicalAndDeepCopied()
        {
            Fixture fixture = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            object adapter = new GrayboxDefenseSaveAdapter3D(fixture.Runtime);

            FormalThreeDDefenseCampaignSaveData first = Capture(adapter);
            CollectionAssert.AreEqual(
                new[]
                {
                    "building.instance.000010",
                    "building.instance.000020",
                    "building.instance.000030",
                },
                first.towerCombatStates
                    .Select(item => item.stableInstanceId)
                    .ToArray());
            CollectionAssert.AreEqual(
                first.buildingHealthStates
                    .Select(item => item.stableInstanceId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                first.buildingHealthStates
                    .Select(item => item.stableInstanceId)
                    .ToArray());
            CollectionAssert.AreEqual(
                first.enemyStates
                    .Select(item => item.stableEnemyId)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                first.enemyStates
                    .Select(item => item.stableEnemyId)
                    .ToArray());

            first.towerCombatStates[0].amount = 999;
            first.buildingHealthStates[0].currentHealth = 1;
            FormalThreeDDefenseCampaignSaveData second = Capture(adapter);
            Assert.That(second.towerCombatStates[0].amount, Is.Not.EqualTo(999));
            Assert.That(second.buildingHealthStates[0].currentHealth,
                Is.Not.EqualTo(1));
        }

        [Test]
        public void SchemaThirtyTwoBuildingLossesRoundTripByStableBuildingDefinitionId()
        {
            Fixture source = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            source.Campaign.RegisterBuildingLoss(
                BuildingCatalog.Wall.Id.Value);
            source.Campaign.RegisterBuildingLoss(
                BuildingCatalog.Housing.Id.Value);
            source.Campaign.RegisterBuildingLoss(
                BuildingCatalog.Wall.Id.Value);

            var sourceAdapter = new GrayboxDefenseSaveAdapter3D(source.Runtime);
            FormalThreeDDefenseCampaignSaveData first =
                sourceAdapter.CaptureCampaign();
            CollectionAssert.AreEqual(
                new[]
                    {
                        BuildingCatalog.Housing.Id.Value,
                        BuildingCatalog.Wall.Id.Value,
                    }
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray(),
                first.statistics.buildingLossesByBuildingId
                    .Select(item => item.stableId)
                    .ToArray(),
                "Building loss metrics must be canonical BuildingCatalog IDs, " +
                "not a synthetic aggregate placeholder.");
            Assert.That(first.statistics.buildingLossesByBuildingId.Single(
                    item => item.stableId == BuildingCatalog.Housing.Id.Value)
                .amount, Is.EqualTo(1));
            Assert.That(first.statistics.buildingLossesByBuildingId.Single(
                    item => item.stableId == BuildingCatalog.Wall.Id.Value)
                .amount, Is.EqualTo(2));
            Assert.That(first.statistics.buildingLossesByBuildingId.Any(
                    item => item.stableId == "building.loss.total"), Is.False);

            first.statistics.buildingLossesByBuildingId[0].amount = 999;
            FormalThreeDDefenseCampaignSaveData detached =
                sourceAdapter.CaptureCampaign();
            Assert.That(detached.statistics.buildingLossesByBuildingId[0].amount,
                Is.Not.EqualTo(999),
                "Mutating a DTO must not mutate live campaign statistics.");

            Fixture target = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            var targetAdapter = new GrayboxDefenseSaveAdapter3D(target.Runtime);
            Assert.That(targetAdapter.TryRestoreCampaign(
                detached,
                target.Session.Instances,
                out string restoreError), Is.True, restoreError);
            CollectionAssert.AreEqual(
                detached.statistics.buildingLossesByBuildingId
                    .Select(item => item.stableId + ":" + item.amount)
                    .ToArray(),
                targetAdapter.CaptureCampaign().statistics
                    .buildingLossesByBuildingId
                    .Select(item => item.stableId + ":" + item.amount)
                    .ToArray(),
                "Schema 32 restore must preserve every building loss bucket.");
        }

        [Test]
        public void SchemaThirtyFiveControlledFriendlyAndLossCountRoundTrip()
        {
            Fixture source = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            CampaignWaveDefinition wave = CampaignWaveCatalog.All[0];
            source.Campaign.Advance(
                wave.WarningSeconds +
                wave.SpawnSeconds / wave.TotalCount + .1f,
                1);
            SingleCityDefenseEnemySnapshot enemy =
                source.Campaign.Snapshot.Enemies[0];
            Assert.That(source.Campaign.TryControlEnemy(
                enemy.StableId,
                BuildingCatalog.MindSpire.Id.Value), Is.True);
            var sourceAdapter = new GrayboxDefenseSaveAdapter3D(source.Runtime);
            FormalThreeDDefenseCampaignSaveData saved =
                sourceAdapter.CaptureCampaign();
            Assert.That(saved.enemyStates[0].isControlled, Is.True);
            Assert.That(saved.enemyStates[0].currentHealth,
                Is.EqualTo(enemy.CurrentHealth));
            saved.statistics.controlledUnitLossCount = 2;

            Fixture target = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            var targetAdapter = new GrayboxDefenseSaveAdapter3D(target.Runtime);
            Assert.That(targetAdapter.TryRestoreCampaign(
                saved,
                target.Session.Instances,
                out string error), Is.True, error);
            FormalThreeDDefenseCampaignSaveData recaptured =
                targetAdapter.CaptureCampaign();
            Assert.That(recaptured.enemyStates[0].isControlled, Is.True);
            Assert.That(recaptured.enemyStates[0].currentHealth,
                Is.EqualTo(enemy.CurrentHealth));
            Assert.That(recaptured.statistics.controlledUnitLossCount,
                Is.EqualTo(2));
        }

        [Test]
        public void PartialMigrationStatisticsRoundTripThroughFormalAdapter()
        {
            Fixture source = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            var sourceAdapter = new GrayboxDefenseSaveAdapter3D(source.Runtime);
            FormalThreeDDefenseCampaignSaveData saved =
                sourceAdapter.CaptureCampaign();
            Assert.That(saved.statistics.partialFromMigration, Is.False,
                "A newly-created formal campaign must own complete stats.");
            saved.statistics.partialFromMigration = true;

            Fixture target = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            var targetAdapter = new GrayboxDefenseSaveAdapter3D(target.Runtime);

            Assert.That(targetAdapter.TryRestoreCampaign(
                saved,
                target.Session.Instances,
                out string error), Is.True, error);
            Assert.That(
                targetAdapter.CaptureCampaign().statistics
                    .partialFromMigration,
                Is.True,
                "DTO restore and recapture must preserve partial history.");
            Assert.That(target.Campaign.Snapshot.Statistics
                    .PartialFromMigration,
                Is.True,
                "The live campaign snapshot must expose the restored truth.");
        }

        [Test]
        public void StatisticsAdapterRoundTripsTowerKillsAndSessionFields()
        {
            Type metricStateType = typeof(
                SingleCityDefenseCampaignMetricPersistenceState);
            Type metricEnumerableType = typeof(IEnumerable<>).MakeGenericType(
                metricStateType);
            Type[] constructorTypes =
            {
                typeof(float), typeof(int), typeof(int), typeof(int),
                metricEnumerableType, typeof(int), typeof(int),
                metricEnumerableType, metricEnumerableType,
                metricEnumerableType, typeof(int), metricEnumerableType,
                typeof(bool), typeof(int), typeof(float), typeof(float),
                typeof(bool), typeof(bool), typeof(int),
            };
            ConstructorInfo constructor = typeof(
                    SingleCityDefenseCampaignStatisticsPersistenceState)
                .GetConstructor(constructorTypes);
            Assert.That(constructor, Is.Not.Null,
                "Campaign statistics persistence must carry all schema 32 " +
                "session fields without a second DTO-only truth.");

            var towerKills = new[]
            {
                new SingleCityDefenseCampaignMetricPersistenceState(
                    BuildingCatalog.MachineGunTurret.Id.Value,
                    2),
            };
            var enemyKills = new[]
            {
                new SingleCityDefenseCampaignMetricPersistenceState(
                    EnemyCatalog.Gnawer.Id.Value,
                    2),
            };
            object persistence = constructor.Invoke(new object[]
            {
                12f, 2, 2, 1, enemyKills, 2, 7,
                Array.Empty<
                    SingleCityDefenseCampaignMetricPersistenceState>(),
                towerKills,
                Array.Empty<
                    SingleCityDefenseCampaignMetricPersistenceState>(),
                0,
                Array.Empty<
                    SingleCityDefenseCampaignMetricPersistenceState>(),
                false, 9, 4.5f, 6f, true, true, 3,
            });

            MethodInfo toDto = typeof(GrayboxDefenseSaveAdapter3D).GetMethod(
                "Statistics",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(toDto, Is.Not.Null);
            object dto = toDto.Invoke(null, new[] { persistence });
            AssertStatisticFields(dto, 9, 4.5f, 6f, true, true, 3);
            AssertMetric(
                ReadArrayField(dto, "killsByTowerBuildingId"),
                BuildingCatalog.MachineGunTurret.Id.Value,
                2);

            var campaignDto = new FormalThreeDDefenseCampaignSaveData
            {
                campaignId = CampaignWaveCatalog.Id,
                statistics = (FormalThreeDDefenseCampaignStatisticsSaveData)dto,
            };
            MethodInfo toPersistence = typeof(GrayboxDefenseSaveAdapter3D)
                .GetMethod("Campaign", BindingFlags.Static |
                    BindingFlags.NonPublic);
            Assert.That(toPersistence, Is.Not.Null);
            object restoredCampaign = toPersistence.Invoke(
                null,
                new object[] { campaignDto });
            object restoredStatistics = restoredCampaign.GetType()
                .GetProperty("Statistics")?.GetValue(restoredCampaign);
            Assert.That(restoredStatistics, Is.Not.Null);
            AssertStatisticProperties(
                restoredStatistics,
                9,
                4.5f,
                6f,
                true,
                true,
                3);
            AssertMetric(
                ReadMetricProperty(
                    restoredStatistics,
                    "KillsByTowerBuildingId"),
                BuildingCatalog.MachineGunTurret.Id.Value,
                2);
        }

        [Test]
        public void PrepareValidatesWholePayloadWithoutWritingAndCommitAppliesOnce()
        {
            Fixture source = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            Assert.That(source.Health.TryApplyDamage(
                "building.instance.000010",
                7,
                out _,
                out _), Is.True);
            FormalThreeDDefenseCampaignSaveData saved = Capture(
                new GrayboxDefenseSaveAdapter3D(source.Runtime));

            Fixture target = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            object adapter = new GrayboxDefenseSaveAdapter3D(target.Runtime);
            string before = JsonUtility.ToJson(Capture(adapter), false);

            Assert.That(TryPrepare(
                adapter,
                saved,
                target.Session.Instances,
                out object plan,
                out string prepareError), Is.True, prepareError);
            Assert.That(JsonUtility.ToJson(Capture(adapter), false),
                Is.EqualTo(before),
                "Prepare must perform complete validation with zero writes.");
            Assert.That(TryCommit(adapter, plan, out string commitError),
                Is.True, commitError);
            Assert.That(JsonUtility.ToJson(Capture(adapter), false),
                Is.EqualTo(JsonUtility.ToJson(saved, false)));
            Assert.That(TryCommit(adapter, plan, out _), Is.False,
                "A committed restore plan must be consumed exactly once.");
        }

        [Test]
        public void JsonEmptyTowerTargetRestoresAsUnlockedTarget()
        {
            Fixture source = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            FormalThreeDDefenseCampaignSaveData saved = Capture(
                new GrayboxDefenseSaveAdapter3D(source.Runtime));
            for (var index = 0;
                 index < saved.towerCombatStates.Length;
                 index++)
            {
                saved.towerCombatStates[index].targetStableEnemyId =
                    string.Empty;
            }

            Fixture target = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            var adapter = new GrayboxDefenseSaveAdapter3D(target.Runtime);

            Assert.That(adapter.TryRestoreCampaign(
                saved,
                target.Session.Instances,
                out string error), Is.True, error);
            Assert.That(adapter.CaptureCampaign().towerCombatStates.All(
                tower => string.IsNullOrEmpty(
                    tower.targetStableEnemyId)), Is.True);
        }

        [Test]
        public void RestorePlanRejectsStaleAndForeignRuntimeCommits()
        {
            Fixture source = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            FormalThreeDDefenseCampaignSaveData saved = Capture(
                new GrayboxDefenseSaveAdapter3D(source.Runtime));

            Fixture staleTarget = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            object staleAdapter =
                new GrayboxDefenseSaveAdapter3D(staleTarget.Runtime);
            Assert.That(TryPrepare(
                staleAdapter,
                saved,
                staleTarget.Session.Instances,
                out object stalePlan,
                out string stalePrepareError), Is.True, stalePrepareError);
            staleTarget.Runtime.SetCorePosition(11f, 10f);
            Assert.That(TryCommit(staleAdapter, stalePlan, out _), Is.False,
                "A plan must reject a runtime changed after Prepare.");

            Fixture owner = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            Fixture foreign = CreateFixture(
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020");
            object ownerAdapter = new GrayboxDefenseSaveAdapter3D(owner.Runtime);
            Assert.That(TryPrepare(
                ownerAdapter,
                saved,
                owner.Session.Instances,
                out object ownerPlan,
                out string ownerPrepareError), Is.True, ownerPrepareError);
            Assert.That(TryCommit(
                new GrayboxDefenseSaveAdapter3D(foreign.Runtime),
                ownerPlan,
                out _), Is.False,
                "A plan created by one runtime cannot commit into another.");
        }

        [Test]
        public void SuccessfulRestoreClearsFutureTimelineRuinLossDetails()
        {
            string[] towerIds =
            {
                "building.instance.000030",
                "building.instance.000010",
                "building.instance.000020",
            };
            Fixture fixture = CreateFixture(towerIds);
            var adapter = new GrayboxDefenseSaveAdapter3D(fixture.Runtime);
            FormalThreeDDefenseCampaignSaveData checkpoint =
                adapter.CaptureCampaign();

            Assert.That(ApplyCampaignBuildingDamage(
                fixture.Runtime,
                towerIds[0],
                BuildingCatalog.MachineGunTurret.MaximumHealth),
                Is.EqualTo(BuildingCatalog.MachineGunTurret.MaximumHealth));
            Assert.That(fixture.Runtime.TryGetDestructionResult(
                towerIds[0],
                out _), Is.True);
            Assert.That(fixture.Runtime.LastDestructionResult, Is.Not.Null);

            RestoreCompletedTowers(fixture.Session, towerIds);
            fixture.Runtime.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius);
            Assert.That(adapter.TryRestoreCampaign(
                checkpoint,
                fixture.Session.Instances,
                out string error), Is.True, error);

            Assert.That(fixture.Runtime.TryGetDestructionResult(
                towerIds[0],
                out _), Is.False,
                "A checkpoint restore must not leak ruin losses from the " +
                "discarded future timeline.");
            Assert.That(fixture.Runtime.LastDestructionResult, Is.Null);
            Assert.That(fixture.Runtime.PendingPresentationRebuildCount,
                Is.Zero);
        }

        [Test]
        public void SchemaThirtyTwoCoordinatorDoesNotRetainCampaignShadowTruth()
        {
            FieldInfo retained = typeof(GrayboxFormalSaveCoordinator3D)
                .GetField(
                    "retainedCampaign",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(retained, Is.Null,
                "After restore, schema 32 capture must read the live formal " +
                "defense runtime through its adapter, not replay a retained DTO.");

            MethodInfo capture = typeof(GrayboxDefenseSaveAdapter3D).GetMethod(
                AdapterCaptureMethod,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(capture, Is.Not.Null);
            Assert.That(capture.ReturnType,
                Is.EqualTo(typeof(FormalThreeDDefenseCampaignSaveData)));
        }

        private Fixture CreateFixture(params string[] towerIds)
        {
            GameObject owner = new GameObject("FormalDefenseSaveFixture");
            created.Add(owner);
            GrayboxBuildingSession3D session =
                owner.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();

            RestoreCompletedTowers(session, towerIds);

            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(session.Instances);
            var runtime = new GrayboxDefenseRuntime3D(10f, 10f, 30f, 10f);
            var campaign = new SingleCityDefenseCampaignModel(10f, 10f);
            var destruction = new GrayboxCombatDestructionCoordinator3D(
                session,
                health,
                new GrayboxProductionRuntime3D(),
                runtime,
                campaign,
                new NoOpPresentation());
            runtime.ConfigureFormalCampaign(campaign, health, destruction);
            runtime.Synchronize(
                session.Instances,
                CityMode.Fortress,
                10,
                10,
                session.GroundBuildRadius);
            return new Fixture(session, runtime, health, campaign);
        }

        private static void RestoreCompletedTowers(
            GrayboxBuildingSession3D session,
            IReadOnlyList<string> towerIds)
        {

            BuildingDefinition[] definitions =
            {
                BuildingCatalog.MachineGunTurret,
                BuildingCatalog.LaserTower,
                BuildingCatalog.SporeTower,
            };
            var restored = new GrayboxBuildingRestoreEntry3D[towerIds.Count];
            for (var index = 0; index < towerIds.Count; index++)
            {
                restored[index] = new GrayboxBuildingRestoreEntry3D(
                    towerIds[index],
                    definitions[index],
                    BuildingSite.Ground,
                    10 + index * 2,
                    10,
                    BuildingOrientation.North,
                    GrayboxBuildingInstanceState.Completed,
                    0f,
                    isPlayerOwned: true,
                    isEvacuationLocked: false,
                    ResourceNodeBinding.None);
            }
            Assert.That(session.TryRestoreBuildings(
                restored,
                100,
                new NoOpPresentation(),
                out string restoreError), Is.True, restoreError);
        }

        private static int ApplyCampaignBuildingDamage(
            GrayboxDefenseRuntime3D runtime,
            string stableInstanceId,
            int damage)
        {
            MethodInfo apply = typeof(GrayboxDefenseRuntime3D).GetMethod(
                "ApplyCampaignBuildingDamage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null);
            return (int)apply.Invoke(runtime, new object[]
            {
                "campaign.enemy.restore-history-probe",
                stableInstanceId,
                damage,
            });
        }

        private static FormalThreeDDefenseCampaignSaveData Capture(
            object adapter)
        {
            MethodInfo method = adapter.GetType().GetMethod(
                AdapterCaptureMethod,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null,
                "The formal adapter must expose schema 32 CaptureCampaign().");
            Assert.That(method.ReturnType,
                Is.EqualTo(typeof(FormalThreeDDefenseCampaignSaveData)));
            return (FormalThreeDDefenseCampaignSaveData)Invoke(
                method,
                adapter,
                Array.Empty<object>());
        }

        private static bool TryPrepare(
            object adapter,
            FormalThreeDDefenseCampaignSaveData data,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            out object plan,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, AdapterPrepareMethod, 4);
            object[] arguments = { data, instances, null, null };
            bool result = (bool)Invoke(method, adapter, arguments);
            plan = arguments[2];
            error = arguments[3] as string;
            return result;
        }

        private static bool TryCommit(
            object adapter,
            object plan,
            out string error)
        {
            MethodInfo method = FindMethod(adapter, AdapterCommitMethod, 2);
            object[] arguments = { plan, null };
            bool result = (bool)Invoke(method, adapter, arguments);
            error = arguments[1] as string;
            return result;
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
                "Missing IDEA-0017 schema 32 adapter contract: " + name);
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

        private static Array ReadArrayField(object owner, string fieldName)
        {
            FieldInfo field = owner?.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, fieldName);
            object value = field.GetValue(owner);
            Assert.That(value, Is.InstanceOf<Array>(), fieldName);
            return (Array)value;
        }

        private static System.Collections.IEnumerable ReadMetricProperty(
            object owner,
            string propertyName)
        {
            PropertyInfo property = owner?.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            object value = property.GetValue(owner);
            Assert.That(value, Is.InstanceOf<System.Collections.IEnumerable>(),
                propertyName);
            return (System.Collections.IEnumerable)value;
        }

        private static void AssertStatisticFields(
            object owner,
            int completedBatches,
            float activeSeconds,
            float eligibleSeconds,
            bool packed,
            bool modifierUsed,
            int controlledUnitLossCount)
        {
            Assert.That(ReadPublicField<int>(owner,
                "completedProductionBatchCount"), Is.EqualTo(completedBatches));
            Assert.That(ReadPublicField<float>(owner,
                "productionActiveProgressSeconds"), Is.EqualTo(activeSeconds));
            Assert.That(ReadPublicField<float>(owner,
                "productionEligibleSeconds"), Is.EqualTo(eligibleSeconds));
            Assert.That(ReadPublicField<bool>(owner,
                "cityWasPackedAfterCampaignStart"), Is.EqualTo(packed));
            Assert.That(ReadPublicField<bool>(owner,
                "developmentModifierUsed"), Is.EqualTo(modifierUsed));
            Assert.That(ReadPublicField<int>(owner,
                "controlledUnitLossCount"),
                Is.EqualTo(controlledUnitLossCount));
        }

        private static void AssertStatisticProperties(
            object owner,
            int completedBatches,
            float activeSeconds,
            float eligibleSeconds,
            bool packed,
            bool modifierUsed,
            int controlledUnitLossCount)
        {
            Assert.That(ReadPublicProperty<int>(owner,
                "CompletedProductionBatchCount"), Is.EqualTo(completedBatches));
            Assert.That(ReadPublicProperty<float>(owner,
                "ProductionActiveProgressSeconds"), Is.EqualTo(activeSeconds));
            Assert.That(ReadPublicProperty<float>(owner,
                "ProductionEligibleSeconds"), Is.EqualTo(eligibleSeconds));
            Assert.That(ReadPublicProperty<bool>(owner,
                "CityWasPackedAfterCampaignStart"), Is.EqualTo(packed));
            Assert.That(ReadPublicProperty<bool>(owner,
                "DevelopmentModifierUsed"), Is.EqualTo(modifierUsed));
            Assert.That(ReadPublicProperty<int>(owner,
                "ControlledUnitLossCount"),
                Is.EqualTo(controlledUnitLossCount));
        }

        private static T ReadPublicField<T>(object owner, string fieldName)
        {
            FieldInfo field = owner?.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(owner);
        }

        private static T ReadPublicProperty<T>(object owner, string propertyName)
        {
            PropertyInfo property = owner?.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(owner);
        }

        private static void AssertMetric(
            System.Collections.IEnumerable values,
            string stableId,
            int amount)
        {
            object match = null;
            foreach (object item in values)
            {
                MemberInfo stableIdMember = item.GetType().GetField(
                        "stableId",
                        BindingFlags.Instance | BindingFlags.Public) ??
                    (MemberInfo)item.GetType().GetProperty(
                        "StableId",
                        BindingFlags.Instance | BindingFlags.Public);
                Assert.That(stableIdMember, Is.Not.Null);
                string actualId = stableIdMember is FieldInfo field
                    ? field.GetValue(item) as string
                    : ((PropertyInfo)stableIdMember).GetValue(item) as string;
                if (string.Equals(actualId, stableId, StringComparison.Ordinal))
                {
                    match = item;
                    break;
                }
            }
            Assert.That(match, Is.Not.Null, stableId);
            MemberInfo amountMember = match.GetType().GetField(
                    "amount",
                    BindingFlags.Instance | BindingFlags.Public) ??
                (MemberInfo)match.GetType().GetProperty(
                    "Amount",
                    BindingFlags.Instance | BindingFlags.Public);
            Assert.That(amountMember, Is.Not.Null);
            int actualAmount = amountMember is FieldInfo amountField
                ? (int)amountField.GetValue(match)
                : (int)((PropertyInfo)amountMember).GetValue(match);
            Assert.That(actualAmount, Is.EqualTo(amount));
        }

        private static void AssertReadOnlyProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null,
                type.Name + " must expose " + name + ".");
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.GetSetMethod(nonPublic: false), Is.Null,
                name + " must not expose a public setter.");
        }

        private sealed class NoOpPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }

        private sealed class Fixture
        {
            public Fixture(
                GrayboxBuildingSession3D session,
                GrayboxDefenseRuntime3D runtime,
                GrayboxBuildingHealthRuntime3D health,
                SingleCityDefenseCampaignModel campaign)
            {
                Session = session;
                Runtime = runtime;
                Health = health;
                Campaign = campaign;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxDefenseRuntime3D Runtime { get; }
            public GrayboxBuildingHealthRuntime3D Health { get; }
            public SingleCityDefenseCampaignModel Campaign { get; }
        }
    }
}
