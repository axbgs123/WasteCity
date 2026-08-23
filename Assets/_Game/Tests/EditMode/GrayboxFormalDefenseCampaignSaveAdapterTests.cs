using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
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

            BuildingDefinition[] definitions =
            {
                BuildingCatalog.MachineGunTurret,
                BuildingCatalog.LaserTower,
                BuildingCatalog.SporeTower,
            };
            var restored = new GrayboxBuildingRestoreEntry3D[towerIds.Length];
            for (var index = 0; index < towerIds.Length; index++)
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
