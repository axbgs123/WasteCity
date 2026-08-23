using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxCombatDestructionCoordinator3DTests
    {
        private const string CoordinatorTypeName =
            "WasteCity.Graybox3D.Building.GrayboxCombatDestructionCoordinator3D";
        private const string ResultTypeName =
            "WasteCity.Graybox3D.Building.GrayboxCombatDestructionResult3D";

        private readonly List<GameObject> cleanup = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void PublicContractUsesTheSixExistingTruthOwnersAndImmutableResult()
        {
            Type coordinatorType = RequireType(CoordinatorTypeName);
            Type resultType = RequireType(ResultTypeName);
            ConstructorInfo constructor = coordinatorType.GetConstructor(new[]
            {
                typeof(GrayboxBuildingSession3D),
                typeof(GrayboxBuildingHealthRuntime3D),
                typeof(GrayboxProductionRuntime3D),
                typeof(GrayboxDefenseRuntime3D),
                typeof(SingleCityDefenseCampaignModel),
                typeof(IGrayboxBuildingPresentation3D),
            });
            Assert.That(constructor, Is.Not.Null,
                "IDEA-0017 requires the frozen six-owner constructor.");

            MethodInfo commit = coordinatorType.GetMethod(
                "Commit",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(commit, Is.Not.Null,
                "IDEA-0017 requires Commit(string).");
            Assert.That(commit.ReturnType, Is.EqualTo(resultType));
            AssertReadOnlyProperty(resultType, "Status");
            AssertReadOnlyProperty(resultType, "StableInstanceId");
            AssertReadOnlyProperty(resultType, "BuildingDefinitionId");
            AssertLossProperty(resultType, "ProductionLostResources");
            AssertLossProperty(resultType, "WarehouseLostResources");
            AssertLossProperty(resultType, "TowerLocalLostResources");
            AssertLossProperty(resultType, "TotalLostResources");
            AssertReadOnlyProperty(resultType, "CommittedNow");
            AssertReadOnlyProperty(resultType, "IsCommitted");
            AssertReadOnlyProperty(resultType, "RequiresPresentationRebuild");

            Type statusType = resultType.GetProperty("Status").PropertyType;
            Assert.That(statusType.IsEnum, Is.True);
            Assert.That(Enum.GetNames(statusType), Is.SupersetOf(new[]
            {
                "Committed",
                "CommittedPresentationRebuildRequired",
                "AlreadyCommitted",
                "NotFound",
                "NotEligible",
                "HealthNotDestroyed",
            }));

            EventInfo committed = coordinatorType.GetEvent(
                "DestructionCommitted",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(committed, Is.Not.Null,
                "The first committed transition requires one public event.");
            Assert.That(committed.EventHandlerType.IsGenericType, Is.True);
            Assert.That(
                committed.EventHandlerType.GetGenericTypeDefinition(),
                Is.EqualTo(typeof(Action<>)));
            Assert.That(
                committed.EventHandlerType.GetGenericArguments()[0],
                Is.EqualTo(resultType));
        }

        [Test]
        public void ProductionBuildingFirstCommitClearsLocalPayloadAndRepeatIsNoOp()
        {
            Fixture fixture = CreateFixture(
                "building.instance.001701",
                BuildingCatalog.Smelter);
            fixture.Production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                fixture.Session.CityStorage);
            Assert.That(fixture.Production.TryGetState(
                fixture.StableId,
                out BuildingProductionState state), Is.True);
            Assert.That(state.Input.Add(ResourceIds.Iron, 4), Is.EqualTo(4));
            new FormalProductionSimulation().Tick(
                new[] { state },
                1f,
                null,
                new ResourceInventory(100),
                new ResourceCapacityPolicy(),
                0,
                globallyPaused: false);
            Assert.That(state.HasReservedInputs, Is.True);
            Assert.That(state.Output.Add(ResourceIds.Alloy, 3), Is.EqualTo(3));
            int cityIronBefore = fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.Iron);

            DestroyHealth(fixture);
            object first = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(first, "Committed", committedNow: true,
                isCommitted: true, rebuild: false);
            AssertAmounts(ReadLoss(first, "ProductionLostResources"),
                new ResourceAmount(ResourceIds.Iron, 4),
                new ResourceAmount(ResourceIds.Alloy, 3));
            AssertAmounts(ReadLoss(first, "WarehouseLostResources"));
            AssertAmounts(ReadLoss(first, "TowerLocalLostResources"));
            AssertAmounts(ReadLoss(first, "TotalLostResources"),
                new ResourceAmount(ResourceIds.Iron, 4),
                new ResourceAmount(ResourceIds.Alloy, 3));
            Assert.That(fixture.Production.TryGetState(
                fixture.StableId, out _), Is.False);
            Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.Iron), Is.EqualTo(cityIronBefore),
                "Destroyed internal contents must not be refunded or transferred.");
            AssertCommittedOnce(fixture);

            object repeated = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(repeated, "AlreadyCommitted", committedNow: false,
                isCommitted: true, rebuild: false);
            AssertAmounts(ReadLoss(repeated, "TotalLostResources"));
            AssertCommittedOnce(fixture);
        }

        [Test]
        public void NonEmptyWarehouseFirstCommitRemovesCapacityAndRepeatIsNoOp()
        {
            Fixture fixture = CreateFixture(
                "building.instance.001702",
                BuildingCatalog.Warehouse);
            fixture.Production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                fixture.Session.CityStorage);
            Assert.That(fixture.Session.CityStorage.ContainsWarehouse(
                fixture.StableId), Is.True);
            Assert.That(fixture.Session.CityStorage.AddToWarehouse(
                fixture.StableId,
                ResourceIds.Iron,
                9), Is.EqualTo(9));
            Assert.That(fixture.Session.CityStorage.AddToWarehouse(
                fixture.StableId,
                ResourceIds.EnergyCrystal,
                6), Is.EqualTo(6));

            DestroyHealth(fixture);
            object first = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(first, "Committed", committedNow: true,
                isCommitted: true, rebuild: false);
            AssertAmounts(ReadLoss(first, "ProductionLostResources"));
            AssertAmounts(ReadLoss(first, "WarehouseLostResources"),
                new ResourceAmount(ResourceIds.Iron, 9),
                new ResourceAmount(ResourceIds.EnergyCrystal, 6));
            AssertAmounts(ReadLoss(first, "TowerLocalLostResources"));
            AssertAmounts(ReadLoss(first, "TotalLostResources"),
                new ResourceAmount(ResourceIds.Iron, 9),
                new ResourceAmount(ResourceIds.EnergyCrystal, 6));
            Assert.That(fixture.Session.CityStorage.ContainsWarehouse(
                fixture.StableId), Is.False,
                "The destroyed warehouse must stop contributing capacity.");
            AssertCommittedOnce(fixture);

            object repeated = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(repeated, "AlreadyCommitted", committedNow: false,
                isCommitted: true, rebuild: false);
            AssertAmounts(ReadLoss(repeated, "TotalLostResources"));
            AssertCommittedOnce(fixture);
        }

        [Test]
        public void ConnectedWarehousePublishesLossOnlyAfterDurableRuinMarker()
        {
            Fixture fixture = CreateFixture(
                "building.instance.001707",
                BuildingCatalog.Warehouse);
            fixture.Production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                fixture.Session.CityStorage);
            Assert.That(fixture.Session.CityStorage.AddToWarehouse(
                fixture.StableId,
                ResourceIds.Iron,
                7), Is.EqualTo(7));
            var statesObservedByLossNotification =
                new List<GrayboxBuildingInstanceState>();
            fixture.Session.CityStorage.AttributedChanged +=
                (_, delta, __) =>
                {
                    if (delta < 0)
                    {
                        statesObservedByLossNotification.Add(
                            fixture.Instance.State);
                    }
                };
            DestroyHealth(fixture);

            object result = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(result, "Committed", committedNow: true,
                isCommitted: true, rebuild: false);
            Assert.That(statesObservedByLossNotification, Is.Not.Empty,
                "The connected warehouse must publish its network loss.");
            Assert.That(statesObservedByLossNotification,
                Is.All.EqualTo(GrayboxBuildingInstanceState.DestroyedRuin),
                "External observers must never see warehouse loss before the " +
                "durable building lifecycle marker has committed.");
            AssertCommittedOnce(fixture);
        }

        [Test]
        public void MachineGunTowerFirstCommitClearsLocalAmmoAndRepeatIsNoOp()
        {
            Fixture fixture = CreateFixture(
                "building.instance.001703",
                BuildingCatalog.MachineGunTurret);
            fixture.Defense.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius);
            fixture.Session.Inventory.Set(ResourceIds.Ammunition, 30);
            fixture.Defense.Tick(
                .1f,
                globallyPaused: false,
                cityStorage: fixture.Session.CityStorage);
            Assert.That(fixture.Defense.TryGetTowerState(
                fixture.StableId,
                out GrayboxDefenseTowerRuntimeState3D tower), Is.True);
            Assert.That(tower.Combat.Ammo, Is.EqualTo(30));

            DestroyHealth(fixture);
            object first = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(first, "Committed", committedNow: true,
                isCommitted: true, rebuild: false);
            AssertAmounts(ReadLoss(first, "ProductionLostResources"));
            AssertAmounts(ReadLoss(first, "WarehouseLostResources"));
            AssertAmounts(ReadLoss(first, "TowerLocalLostResources"),
                new ResourceAmount(ResourceIds.Ammunition, 30));
            AssertAmounts(ReadLoss(first, "TotalLostResources"),
                new ResourceAmount(ResourceIds.Ammunition, 30));
            Assert.That(fixture.Defense.TryGetTowerState(
                fixture.StableId, out _), Is.False);
            AssertCommittedOnce(fixture);

            object repeated = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(repeated, "AlreadyCommitted", committedNow: false,
                isCommitted: true, rebuild: false);
            AssertAmounts(ReadLoss(repeated, "TotalLostResources"));
            AssertCommittedOnce(fixture);
        }

        [Test]
        public void OpaqueProductionLossKeepsCanonicalOrderAcrossAllResultViews()
        {
            const string unknownAlpha = "aaa.resource.legacy-alpha";
            const string unknownOmega = "zzz.resource.legacy-omega";
            Fixture fixture = CreateFixture(
                "building.instance.001708",
                BuildingCatalog.Smelter);
            fixture.Production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                fixture.Session.CityStorage);
            var opaque = new GrayboxProductionPersistenceState3D(
                fixture.StableId,
                "mod.production.removed-recipe",
                new[]
                {
                    new ResourceAmount(ResourceIds.EnergyCrystal, 2),
                    new ResourceAmount(unknownOmega, 5),
                    new ResourceAmount(ResourceIds.Iron, 1),
                    new ResourceAmount(unknownAlpha, 4),
                },
                hasReservedInputs: false,
                Array.Empty<ResourceAmount>(),
                Array.Empty<ResourceAmount>(),
                progressSeconds: 0f,
                isPlayerPaused: false,
                boundResourceNodeId: null,
                boundNodeX: -1,
                boundNodeY: -1);
            Assert.That(fixture.Production.TryPrepareRestore(
                new[] { opaque },
                fixture.Session.Instances,
                world: null,
                out GrayboxProductionRestorePlan3D productionPlan,
                out string prepareError), Is.True, prepareError);
            Assert.That(fixture.Production.TryCommitRestore(
                productionPlan,
                out string commitError), Is.True, commitError);

            GrayboxBuildingInstance3D towerInstance = CompletedRuntimeInstance(
                fixture.StableId,
                BuildingCatalog.MachineGunTurret,
                10,
                10);
            fixture.Defense.Synchronize(
                new[] { towerInstance },
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius);
            fixture.Session.Inventory.Set(ResourceIds.Ammunition, 30);
            fixture.Defense.Tick(
                .1f,
                globallyPaused: false,
                cityStorage: fixture.Session.CityStorage);
            Assert.That(fixture.Session.CityStorage.TryRegisterWarehouse(
                fixture.StableId,
                connected: true), Is.True);
            Assert.That(fixture.Session.CityStorage.AddToWarehouse(
                fixture.StableId,
                ResourceIds.Stone,
                3), Is.EqualTo(3));
            DestroyHealth(fixture);

            object result = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(result, "Committed", committedNow: true,
                isCommitted: true, rebuild: false);
            AssertAmounts(ReadLoss(result, "ProductionLostResources"),
                new ResourceAmount(ResourceIds.Iron, 1),
                new ResourceAmount(ResourceIds.EnergyCrystal, 2),
                new ResourceAmount(unknownAlpha, 4),
                new ResourceAmount(unknownOmega, 5));
            AssertAmounts(ReadLoss(result, "WarehouseLostResources"),
                new ResourceAmount(ResourceIds.Stone, 3));
            AssertAmounts(ReadLoss(result, "TowerLocalLostResources"),
                new ResourceAmount(ResourceIds.Ammunition, 30));
            AssertAmounts(ReadLoss(result, "TotalLostResources"),
                new ResourceAmount(ResourceIds.Iron, 1),
                new ResourceAmount(ResourceIds.EnergyCrystal, 2),
                new ResourceAmount(ResourceIds.Stone, 3),
                new ResourceAmount(ResourceIds.Ammunition, 30),
                new ResourceAmount(unknownAlpha, 4),
                new ResourceAmount(unknownOmega, 5));
            AssertCommittedOnce(fixture);
        }

        [Test]
        public void MissingIneligibleAndPositiveHealthAreRejectedWithoutMutation()
        {
            Fixture healthy = CreateFixture(
                "building.instance.001704",
                BuildingCatalog.Smelter);
            uint catalogBefore = healthy.Session.CatalogRevision;
            uint placementBefore = healthy.Session.PlacementRevision;

            object missing = Commit(
                healthy.Coordinator,
                "building.instance.999999");
            AssertResult(missing, "NotFound", committedNow: false,
                isCommitted: false, rebuild: false);

            object positiveHealth = Commit(
                healthy.Coordinator,
                healthy.StableId);
            AssertResult(positiveHealth, "HealthNotDestroyed",
                committedNow: false, isCommitted: false, rebuild: false);
            Assert.That(healthy.Instance.State,
                Is.EqualTo(GrayboxBuildingInstanceState.Completed));
            Assert.That(healthy.Session.CatalogRevision,
                Is.EqualTo(catalogBefore));
            Assert.That(healthy.Session.PlacementRevision,
                Is.EqualTo(placementBefore));
            Assert.That(healthy.Campaign.Snapshot.Statistics.BuildingLossCount,
                Is.Zero);
            Assert.That(healthy.Events.Values, Is.Empty);

            Fixture ineligible = CreateFixture(
                "building.instance.001705",
                BuildingCatalog.Wall,
                GrayboxBuildingInstanceState.UnderConstruction,
                playerOwned: true,
                remainingSeconds: 1f);
            object rejected = Commit(
                ineligible.Coordinator,
                ineligible.StableId);
            AssertResult(rejected, "NotEligible", committedNow: false,
                isCommitted: false, rebuild: false);
            Assert.That(ineligible.Campaign.Snapshot.Statistics.BuildingLossCount,
                Is.Zero);
            Assert.That(ineligible.Events.Values, Is.Empty);
        }

        [Test]
        public void PresentationFailureStillCommitsTruthStatisticsAndOneEvent()
        {
            var presentation = new RecordingPresentation
            {
                ThrowOnUpdate = true,
            };
            Fixture fixture = CreateFixture(
                "building.instance.001706",
                BuildingCatalog.Wall,
                presentation: presentation);
            DestroyHealth(fixture);

            object first = null;
            Assert.DoesNotThrow(() =>
                first = Commit(fixture.Coordinator, fixture.StableId),
                "Presentation failure must not escape after truth committed.");

            AssertResult(first, "CommittedPresentationRebuildRequired",
                committedNow: true, isCommitted: true, rebuild: true);
            Assert.That(fixture.Instance.State.ToString(),
                Is.EqualTo("DestroyedRuin"));
            Assert.That(fixture.Instance.IsPlayerOwned, Is.False);
            Assert.That(presentation.UpdateAttempts, Is.EqualTo(1));
            AssertCommittedOnce(fixture);

            object repeated = Commit(fixture.Coordinator, fixture.StableId);

            AssertResult(repeated, "AlreadyCommitted", committedNow: false,
                isCommitted: true, rebuild: false);
            Assert.That(presentation.UpdateAttempts, Is.EqualTo(1));
            AssertCommittedOnce(fixture);
        }

        private Fixture CreateFixture(
            string stableId,
            BuildingDefinition definition,
            GrayboxBuildingInstanceState state =
                GrayboxBuildingInstanceState.Completed,
            bool playerOwned = true,
            float remainingSeconds = 0f,
            RecordingPresentation presentation = null)
        {
            presentation = presentation ?? new RecordingPresentation();
            var root = new GameObject("combat-destruction-coordinator-test");
            cleanup.Add(root);
            GrayboxBuildingSession3D session =
                root.AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            var entries = new[]
            {
                new GrayboxBuildingRestoreEntry3D(
                    stableId,
                    definition,
                    BuildingSite.Ground,
                    10,
                    10,
                    BuildingOrientation.North,
                    state,
                    remainingSeconds,
                    playerOwned,
                    isEvacuationLocked: false,
                    ResourceNodeBinding.None),
            };
            Assert.That(session.TryRestoreBuildings(
                entries,
                NextOrdinal(stableId),
                presentation,
                out string error), Is.True, error);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(session.Instances);
            var production = new GrayboxProductionRuntime3D();
            var defense = new GrayboxDefenseRuntime3D(
                coreX: 10f,
                coreZ: 10f,
                spawnX: 20f,
                spawnZ: 10f);
            var campaign = new SingleCityDefenseCampaignModel(10f, 10f);
            object coordinator = CreateCoordinator(
                session,
                health,
                production,
                defense,
                campaign,
                presentation);
            var recorder = new EventRecorder();
            SubscribeCommitted(coordinator, recorder);
            return new Fixture(
                stableId,
                session.Instances[0],
                session,
                health,
                production,
                defense,
                campaign,
                presentation,
                coordinator,
                recorder);
        }

        private static object CreateCoordinator(
            GrayboxBuildingSession3D session,
            GrayboxBuildingHealthRuntime3D health,
            GrayboxProductionRuntime3D production,
            GrayboxDefenseRuntime3D defense,
            SingleCityDefenseCampaignModel campaign,
            IGrayboxBuildingPresentation3D presentation)
        {
            Type type = RequireType(CoordinatorTypeName);
            ConstructorInfo constructor = type.GetConstructor(new[]
            {
                typeof(GrayboxBuildingSession3D),
                typeof(GrayboxBuildingHealthRuntime3D),
                typeof(GrayboxProductionRuntime3D),
                typeof(GrayboxDefenseRuntime3D),
                typeof(SingleCityDefenseCampaignModel),
                typeof(IGrayboxBuildingPresentation3D),
            });
            Assert.That(constructor, Is.Not.Null,
                "IDEA-0017 requires the frozen six-owner constructor.");
            return constructor.Invoke(new object[]
            {
                session,
                health,
                production,
                defense,
                campaign,
                presentation,
            });
        }

        private static object Commit(object coordinator, string stableId)
        {
            MethodInfo method = coordinator.GetType().GetMethod(
                "Commit",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(method, Is.Not.Null, "Missing Commit(string).");
            return method.Invoke(coordinator, new object[] { stableId });
        }

        private static void DestroyHealth(Fixture fixture)
        {
            Assert.That(fixture.Health.TryApplyDamage(
                fixture.StableId,
                int.MaxValue,
                out int applied,
                out bool destroyedNow), Is.True);
            Assert.That(applied, Is.EqualTo(
                fixture.Instance.Placement.Definition.MaximumHealth));
            Assert.That(destroyedNow, Is.True);
        }

        private static void SubscribeCommitted(
            object coordinator,
            EventRecorder recorder)
        {
            EventInfo eventInfo = coordinator.GetType().GetEvent(
                "DestructionCommitted",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(eventInfo, Is.Not.Null,
                "Missing DestructionCommitted event.");
            Assert.That(eventInfo.EventHandlerType.IsGenericType, Is.True);
            Type resultType = eventInfo.EventHandlerType
                .GetGenericArguments()[0];
            MethodInfo record = typeof(EventRecorder).GetMethod(
                nameof(EventRecorder.Record),
                BindingFlags.Instance | BindingFlags.Public)
                .MakeGenericMethod(resultType);
            Delegate handler = Delegate.CreateDelegate(
                eventInfo.EventHandlerType,
                recorder,
                record);
            eventInfo.AddEventHandler(coordinator, handler);
        }

        private static void AssertCommittedOnce(Fixture fixture)
        {
            Assert.That(fixture.Instance.State.ToString(),
                Is.EqualTo("DestroyedRuin"));
            Assert.That(fixture.Instance.IsPlayerOwned, Is.False);
            Assert.That(fixture.Campaign.Snapshot.Statistics.BuildingLossCount,
                Is.EqualTo(1));
            Assert.That(fixture.Events.Values, Has.Count.EqualTo(1),
                "Only the first transition may publish the committed fact.");
        }

        private static void AssertResult(
            object result,
            string expectedStatus,
            bool committedNow,
            bool isCommitted,
            bool rebuild)
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(Read(result, "Status").ToString(),
                Is.EqualTo(expectedStatus));
            Assert.That(Read<bool>(result, "CommittedNow"),
                Is.EqualTo(committedNow));
            Assert.That(Read<bool>(result, "IsCommitted"),
                Is.EqualTo(isCommitted));
            Assert.That(Read<bool>(result, "RequiresPresentationRebuild"),
                Is.EqualTo(rebuild));
        }

        private static IReadOnlyList<ResourceAmount> ReadLoss(
            object result,
            string propertyName)
        {
            object value = Read(result, propertyName);
            Assert.That(value,
                Is.InstanceOf<IReadOnlyList<ResourceAmount>>(),
                propertyName);
            return (IReadOnlyList<ResourceAmount>)value;
        }

        private static object Read(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return property.GetValue(target, null);
        }

        private static T Read<T>(object target, string propertyName)
        {
            object value = Read(target, propertyName);
            Assert.That(value, Is.InstanceOf<T>(), propertyName);
            return (T)value;
        }

        private static void AssertAmounts(
            IReadOnlyList<ResourceAmount> actual,
            params ResourceAmount[] expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.Count, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].ResourceId,
                    Is.EqualTo(expected[index].ResourceId),
                    "Registered resources must follow ResourceIds.All; " +
                    "unknown IDs must follow in ordinal order.");
                Assert.That(actual[index].Amount,
                    Is.EqualTo(expected[index].Amount),
                    actual[index].ResourceId);
            }
        }

        private static GrayboxBuildingInstance3D CompletedRuntimeInstance(
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
            MethodInfo complete = typeof(GrayboxBuildingInstance3D).GetMethod(
                "Complete",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(constructor, Is.Not.Null);
            Assert.That(complete, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
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
            complete.Invoke(instance, null);
            return instance;
        }

        private static void AssertReadOnlyProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.CanRead, Is.True, name);
            Assert.That(property.SetMethod, Is.Null,
                name + " must not expose a setter.");
        }

        private static void AssertLossProperty(Type type, string name)
        {
            AssertReadOnlyProperty(type, name);
            Type propertyType = type.GetProperty(name).PropertyType;
            Assert.That(
                typeof(IReadOnlyList<ResourceAmount>)
                    .IsAssignableFrom(propertyType),
                Is.True,
                name + " must expose a read-only resource list.");
            Assert.That(propertyType.IsArray, Is.False,
                name + " must not expose a mutable array.");
        }

        private static Type RequireType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null,
                "IDEA-0017 RED: missing " + fullName + ".");
            return type;
        }

        private static int NextOrdinal(string stableId)
        {
            int separator = stableId.LastIndexOf('.');
            Assert.That(separator, Is.GreaterThanOrEqualTo(0));
            Assert.That(int.TryParse(
                stableId.Substring(separator + 1),
                out int ordinal), Is.True);
            return ordinal + 1;
        }

        private sealed class EventRecorder
        {
            public List<object> Values { get; } = new List<object>();

            public void Record<T>(T value)
            {
                Values.Add(value);
            }
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            public bool ThrowOnUpdate { get; set; }
            public int UpdateAttempts { get; private set; }

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                UpdateAttempts++;
                if (ThrowOnUpdate)
                    throw new InvalidOperationException(
                        "Injected presentation failure.");
            }

            public void Remove(GrayboxBuildingInstance3D instance) { }
        }

        private sealed class Fixture
        {
            public Fixture(
                string stableId,
                GrayboxBuildingInstance3D instance,
                GrayboxBuildingSession3D session,
                GrayboxBuildingHealthRuntime3D health,
                GrayboxProductionRuntime3D production,
                GrayboxDefenseRuntime3D defense,
                SingleCityDefenseCampaignModel campaign,
                RecordingPresentation presentation,
                object coordinator,
                EventRecorder events)
            {
                StableId = stableId;
                Instance = instance;
                Session = session;
                Health = health;
                Production = production;
                Defense = defense;
                Campaign = campaign;
                Presentation = presentation;
                Coordinator = coordinator;
                Events = events;
            }

            public string StableId { get; }
            public GrayboxBuildingInstance3D Instance { get; }
            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingHealthRuntime3D Health { get; }
            public GrayboxProductionRuntime3D Production { get; }
            public GrayboxDefenseRuntime3D Defense { get; }
            public SingleCityDefenseCampaignModel Campaign { get; }
            public RecordingPresentation Presentation { get; }
            public object Coordinator { get; }
            public EventRecorder Events { get; }
        }
    }
}
