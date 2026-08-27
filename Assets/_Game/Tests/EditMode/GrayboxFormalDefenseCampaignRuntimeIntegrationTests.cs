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
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxFormalDefenseCampaignRuntimeIntegrationTests
    {
        private const string MachineGunId = "building.instance.009101";
        private const string LaserId = "building.instance.009102";
        private const string SporeId = "building.instance.009103";
        private const string SmelterId = "building.instance.009104";
        private const string HeavyMachineGunId = "building.instance.009105";
        private const long ProjectionAllocationBudgetBytes = 512;

        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

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
        public void PublicContractUpgradesExistingRuntimeAndControllerInPlace()
        {
            Type runtimeType = typeof(GrayboxDefenseRuntime3D);
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(runtimeType),
                Is.False,
                "The formal campaign must upgrade the existing pure runtime, " +
                "not add a second MonoBehaviour truth owner.");
            Assert.That(runtimeType.GetConstructor(new[]
            {
                typeof(float),
                typeof(float),
                typeof(float),
                typeof(float),
            }), Is.Not.Null, "The schema-31 constructor is a compatibility API.");
            Assert.That(FindLegacySynchronize(), Is.Not.Null);
            Assert.That(FindLegacyTick(), Is.Not.Null);
            Assert.That(FindFormalConfigure(), Is.Not.Null,
                "IDEA-0017 RED: GrayboxDefenseRuntime3D must expose " +
                "ConfigureFormalCampaign(campaign, health, destruction). " +
                "The existing Tick/Synchronize APIs remain the only clock and " +
                "topology entry points.");
            Assert.That(FindFormalTowerLookup(), Is.Not.Null,
                "IDEA-0017 RED: the upgraded runtime must expose its generic " +
                "three-tower local state through " +
                "TryGetCampaignTowerState(string, out state).");

            PropertyInfo runtimeCampaignSnapshot = runtimeType.GetProperty(
                "CampaignSnapshot",
                BindingFlags.Instance | BindingFlags.Public);
            AssertReadOnlyProperty(
                runtimeCampaignSnapshot,
                typeof(SingleCityDefenseCampaignSnapshot));

            Type controllerType = typeof(GrayboxDefenseController3D);
            Type[] legacyConfigureParameters = ControllerConfigureParameters()
                .Take(6)
                .ToArray();
            Assert.That(controllerType.GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                legacyConfigureParameters,
                null), Is.Not.Null,
                "Existing controller callers must remain source compatible.");
            Assert.That(controllerType.GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                ControllerConfigureParameters(),
                null), Is.Not.Null,
                "IDEA-0017 RED: the formal controller overload must receive " +
                "the existing production controller instead of constructing " +
                "a parallel production runtime.");
            AssertReadOnlyProperty(
                controllerType.GetProperty(
                    "CampaignSnapshot",
                    BindingFlags.Instance | BindingFlags.Public),
                typeof(SingleCityDefenseCampaignSnapshot));
            AssertReadOnlyProperty(
                controllerType.GetProperty(
                    "BuildingHealth",
                    BindingFlags.Instance | BindingFlags.Public),
                typeof(GrayboxBuildingHealthRuntime3D));
        }

        [Test]
        public void FormalControllerCreatesOneObservableCampaignAndHealthOwner()
        {
            ControllerFixture fixture = CreateControllerFixture();
            RestoreBuildings(
                fixture.Session,
                new BuildingEntry(
                    MachineGunId,
                    BuildingCatalog.MachineGunTurret,
                    12,
                    12));
            fixture.Production.Configure(
                fixture.Session,
                fixture.City,
                fixture.World);
            InvokeFormalControllerConfigure(fixture);

            Assert.That(fixture.Controller.Tick(.1f, paused: false), Is.True);

            SingleCityDefenseCampaignSnapshot campaignSnapshot =
                ReadProperty<SingleCityDefenseCampaignSnapshot>(
                    fixture.Controller,
                    "CampaignSnapshot");
            GrayboxBuildingHealthRuntime3D health =
                ReadProperty<GrayboxBuildingHealthRuntime3D>(
                    fixture.Controller,
                    "BuildingHealth");
            Assert.That(campaignSnapshot, Is.Not.Null);
            Assert.That(campaignSnapshot.CurrentWaveNumber, Is.EqualTo(1));
            Assert.That(campaignSnapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Warning));
            Assert.That(campaignSnapshot.WarningRemainingSeconds,
                Is.EqualTo(14.9f).Within(.0001f));
            Assert.That(health, Is.Not.Null);
            Assert.That(health.TryGetHealth(
                MachineGunId,
                out int current,
                out int maximum,
                out bool destroyed), Is.True);
            Assert.That(current, Is.EqualTo(maximum));
            Assert.That(destroyed, Is.False);
            Assert.That(fixture.Controller.Runtime, Is.Not.Null);
            Assert.That(ReadCampaignSnapshot(fixture.Controller.Runtime),
                Is.SameAs(campaignSnapshot));
            Assert.That(
                fixture.Controller.Runtime.CaptureForPersistence()
                    .Tutorial.TutorialTriggered,
                Is.False,
                "The formal controller must not trigger the retired tutorial " +
                "model beside the campaign.");
        }

        [Test]
        public void PersistenceSuppressedSynchronizeDefersCampaignStartUntilGameplay()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(
                includeThreeTowers: false,
                includeSmelter: false);
            var announcedWaves = new List<int>();
            fixture.Campaign.WaveWarningStarted += announcedWaves.Add;

            fixture.Runtime.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                allowCampaignStart: false);

            Assert.That(fixture.Campaign.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Idle),
                "Persistence rebuild adopts restored Idle truth instead of " +
                "turning topology synchronization into gameplay.");
            Assert.That(fixture.Campaign.Snapshot.CurrentWaveNumber, Is.Zero);
            Assert.That(announcedWaves, Is.Empty,
                "A suppressed restore must not publish a warning checkpoint.");
            Assert.That(fixture.Health.TryGetHealth(
                MachineGunId,
                out int currentHealth,
                out int maximumHealth,
                out bool destroyed), Is.True,
                "Restore rebuild must still synchronize building health.");
            Assert.That(currentHealth, Is.EqualTo(maximumHealth));
            Assert.That(destroyed, Is.False);
            Assert.That(CampaignTower(fixture.Runtime, MachineGunId)
                .IsLogisticsConnected, Is.True,
                "Restore rebuild must still synchronize tower logistics.");

            fixture.Runtime.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                allowCampaignStart: false);
            Assert.That(announcedWaves, Is.Empty,
                "Repeated suppressed rebuilds remain side-effect free.");

            fixture.Runtime.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                allowCampaignStart: true);
            Assert.That(fixture.Campaign.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Warning));
            Assert.That(fixture.Campaign.Snapshot.CurrentWaveNumber,
                Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { 1 }, announcedWaves,
                "The first real gameplay synchronization publishes the " +
                "deferred checkpoint exactly once.");

            fixture.Runtime.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                allowCampaignStart: true);
            CollectionAssert.AreEqual(new[] { 1 }, announcedWaves,
                "Stable topology synchronization cannot duplicate the event.");
        }

        [Test]
        public void StableFormalControllerTickReusesSnapshotsAndAllocatesZero()
        {
            ControllerFixture fixture = CreateControllerFixture();
            RestoreBuildings(
                fixture.Session,
                new BuildingEntry(
                    MachineGunId,
                    BuildingCatalog.MachineGunTurret,
                    12,
                    12));
            fixture.Production.Configure(
                fixture.Session,
                fixture.City,
                fixture.World);
            InvokeFormalControllerConfigure(fixture);
            Assert.That(fixture.Controller.Tick(.1f, paused: false), Is.True);
            Assert.That(fixture.Controller.Tick(0f, paused: false), Is.True);

            GrayboxDefenseRuntimeSnapshot3D runtimeSnapshot =
                fixture.Controller.Snapshot;
            SingleCityDefenseCampaignSnapshot campaignSnapshot =
                fixture.Controller.CampaignSnapshot;
            bool ticked = true;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 128; index++)
                ticked &= fixture.Controller.Tick(0f, paused: false);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "FormalControllerStableTickAllocationBytes=" + allocated);
            Assert.That(ticked, Is.True);
            Assert.That(allocated, Is.Zero,
                "Stable controller synchronization must reuse health buffers " +
                "and must not rebuild either observable snapshot.");
            Assert.That(fixture.Controller.Snapshot,
                Is.SameAs(runtimeSnapshot));
            Assert.That(fixture.Controller.CampaignSnapshot,
                Is.SameAs(campaignSnapshot));
        }

        [Test]
        public void ControllerConfigureTransitionsRebuildAllFormalOwners()
        {
            ControllerFixture first = CreateControllerFixture();
            RestoreBuildings(
                first.Session,
                new BuildingEntry(
                    MachineGunId,
                    BuildingCatalog.MachineGunTurret,
                    12,
                    12),
                new BuildingEntry(
                    SmelterId,
                    BuildingCatalog.Smelter,
                    18,
                    12));
            first.Production.Configure(first.Session, first.City, first.World);

            first.Controller.Configure(
                first.Session,
                first.City,
                first.World,
                first.BuildingPresentation,
                first.DefenseWorldView,
                first.Hud);
            Assert.That(first.Controller.Tick(.1f, paused: false), Is.True);
            GrayboxDefenseRuntime3D legacyRuntime = first.Controller.Runtime;
            Assert.That(first.Controller.CampaignSnapshot, Is.Null);

            InvokeFormalControllerConfigure(first);
            Assert.That(first.Controller.Tick(.1f, paused: false), Is.True);
            GrayboxDefenseRuntime3D firstFormalRuntime =
                first.Controller.Runtime;
            GrayboxBuildingHealthRuntime3D firstHealth =
                first.Controller.BuildingHealth;
            Assert.That(firstFormalRuntime, Is.Not.SameAs(legacyRuntime));
            Assert.That(first.Controller.CampaignSnapshot, Is.Not.Null);
            Assert.That(ApplyCampaignBuildingDamage(
                firstFormalRuntime,
                SmelterId,
                BuildingCatalog.Smelter.MaximumHealth),
                Is.EqualTo(BuildingCatalog.Smelter.MaximumHealth));
            Assert.That(firstFormalRuntime.PendingPresentationRebuildCount,
                Is.EqualTo(1),
                "The unconfigured old presentation intentionally leaves a " +
                "real pending rebuild before ownership changes.");

            ControllerFixture second = CreateControllerFixture();
            RestoreBuildings(
                second.Session,
                new BuildingEntry(
                    LaserId,
                    BuildingCatalog.LaserTower,
                    12,
                    12));
            second.Production.Configure(
                second.Session,
                second.City,
                second.World);
            InvokeFormalControllerConfigure(first.Controller, second);
            Assert.That(first.Controller.Tick(.1f, paused: false), Is.True);
            GrayboxDefenseRuntime3D secondFormalRuntime =
                first.Controller.Runtime;
            Assert.That(secondFormalRuntime,
                Is.Not.SameAs(firstFormalRuntime));
            Assert.That(ReadProperty<bool>(
                firstFormalRuntime,
                "HasPresentationRecovery"), Is.False,
                "A discarded runtime must release the old controller-bound " +
                "recovery callback before ownership changes.");
            Assert.That(firstFormalRuntime.PendingPresentationRebuildCount,
                Is.Zero,
                "Discarding the owner must clear its old pending rebuilds.");
            int newPresentationCount =
                second.BuildingPresentation.InstanceRendererCount;
            firstFormalRuntime.Tick(
                .1f,
                globallyPaused: false,
                first.Session.CityStorage);
            Assert.That(second.BuildingPresentation.InstanceRendererCount,
                Is.EqualTo(newPresentationCount),
                "Ticking a discarded runtime must never touch the new " +
                "presentation owner.");
            Assert.That(first.Controller.BuildingHealth,
                Is.Not.SameAs(firstHealth));
            Assert.That(first.Controller.BuildingHealth.TryGetHealth(
                LaserId,
                out _,
                out _,
                out _), Is.True);
            Assert.That(first.Controller.BuildingHealth.TryGetHealth(
                MachineGunId,
                out _,
                out _,
                out _), Is.False,
                "The new formal owner must not retain the old session.");

            first.Controller.Configure(
                second.Session,
                second.City,
                second.World,
                second.BuildingPresentation,
                second.DefenseWorldView,
                second.Hud);
            Assert.That(first.Controller.Tick(.1f, paused: false), Is.True);
            Assert.That(first.Controller.Runtime,
                Is.Not.SameAs(secondFormalRuntime));
            Assert.That(ReadProperty<bool>(
                secondFormalRuntime,
                "HasPresentationRecovery"), Is.False);
            Assert.That(first.Controller.CampaignSnapshot, Is.Null,
                "The six-argument Configure API must explicitly return the " +
                "controller to legacy mode.");
        }

        [Test]
        public void FormalRuntimeSynchronizesFourCatalogTowersAndCitySupply()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(
                includeThreeTowers: true,
                includeSmelter: true);
            fixture.Session.Inventory.Set(ResourceIds.Ammunition, 60);
            fixture.Session.Inventory.Set(ResourceIds.EnergyCrystal, 30);
            fixture.Session.Inventory.Set(ResourceIds.BiologicalWeapon, 30);

            SynchronizeFormal(fixture, CityMode.Fortress, 10, 10);
            fixture.Runtime.Tick(
                .1f,
                globallyPaused: false,
                fixture.Session.CityStorage);
            SynchronizeFormal(fixture, CityMode.Fortress, 10, 10);

            SingleCityDefenseCampaignSnapshot campaign =
                ReadCampaignSnapshot(fixture.Runtime);
            Assert.That(campaign.CurrentWaveNumber, Is.EqualTo(1));
            Assert.That(campaign.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Warning));
            Assert.That(campaign.WarningRemainingSeconds,
                Is.EqualTo(14.9f).Within(.0001f),
                "Repeated synchronization must not trigger or advance the " +
                "first wave twice.");
            AssertTowerSupply(
                fixture.Runtime,
                MachineGunId,
                BuildingCatalog.MachineGunTurret.Id.Value,
                ResourceIds.Ammunition);
            AssertTowerSupply(
                fixture.Runtime,
                LaserId,
                BuildingCatalog.LaserTower.Id.Value,
                ResourceIds.EnergyCrystal);
            AssertTowerSupply(
                fixture.Runtime,
                SporeId,
                BuildingCatalog.SporeTower.Id.Value,
                ResourceIds.BiologicalWeapon);
            AssertTowerSupply(
                fixture.Runtime,
                HeavyMachineGunId,
                BuildingCatalog.HeavyMachineGunTurret.Id.Value,
                ResourceIds.Ammunition);
            Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.Ammunition), Is.Zero);
            Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.BiologicalWeapon), Is.Zero);

            int machineGunBefore = CampaignTower(
                fixture.Runtime,
                MachineGunId).LocalConsumableAmount;
            int laserBefore = CampaignTower(
                fixture.Runtime,
                LaserId).LocalConsumableAmount;
            int sporeBefore = CampaignTower(
                fixture.Runtime,
                SporeId).LocalConsumableAmount;
            int heavyBefore = CampaignTower(
                fixture.Runtime,
                HeavyMachineGunId).LocalConsumableAmount;
            fixture.Session.Inventory.Set(ResourceIds.Ammunition, 20);
            fixture.Session.Inventory.Set(ResourceIds.EnergyCrystal, 20);
            fixture.Session.Inventory.Set(ResourceIds.BiologicalWeapon, 20);

            SynchronizeFormal(fixture, CityMode.Mobile, 10, 10);
            fixture.Runtime.Tick(
                .1f,
                globallyPaused: false,
                fixture.Session.CityStorage);

            Assert.That(CampaignTower(fixture.Runtime, MachineGunId)
                .LocalConsumableAmount, Is.EqualTo(machineGunBefore));
            Assert.That(CampaignTower(fixture.Runtime, LaserId)
                .LocalConsumableAmount, Is.EqualTo(laserBefore));
            Assert.That(CampaignTower(fixture.Runtime, SporeId)
                .LocalConsumableAmount, Is.EqualTo(sporeBefore));
            Assert.That(CampaignTower(fixture.Runtime, HeavyMachineGunId)
                .LocalConsumableAmount, Is.EqualTo(heavyBefore));
            Assert.That(CampaignTower(fixture.Runtime, MachineGunId)
                .IsLogisticsConnected, Is.False);
            Assert.That(CampaignTower(fixture.Runtime, LaserId)
                .IsLogisticsConnected, Is.False);
            Assert.That(CampaignTower(fixture.Runtime, SporeId)
                .IsLogisticsConnected, Is.False);
            Assert.That(CampaignTower(fixture.Runtime, HeavyMachineGunId)
                .IsLogisticsConnected, Is.False);
            Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.Ammunition), Is.EqualTo(20));
            Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.EnergyCrystal), Is.EqualTo(20));
            Assert.That(fixture.Session.CityStorage.GetNetworkAmount(
                ResourceIds.BiologicalWeapon), Is.EqualTo(20));
            Assert.That(fixture.Runtime.Snapshot.Towers.Select(value =>
                    value.StableId),
                Is.EqualTo(new[]
                {
                    MachineGunId,
                    LaserId,
                    SporeId,
                    HeavyMachineGunId
                }));
        }

        [Test]
        public void FormalCampaignIsExclusiveWhileLegacyTutorialStillWorks()
        {
            RuntimeFixture formal = CreateRuntimeFixture(
                includeThreeTowers: false,
                includeSmelter: false);
            SynchronizeFormal(formal, CityMode.Fortress, 10, 10);

            formal.Runtime.Tick(
                20f,
                globallyPaused: false,
                formal.Session.CityStorage);

            SingleCityDefenseCampaignSnapshot campaign =
                ReadCampaignSnapshot(formal.Runtime);
            Assert.That(campaign.SpawnedEnemyCount, Is.GreaterThan(0));
            Assert.That(campaign.Enemies, Is.Not.Empty);
            Assert.That(campaign.Enemies.All(enemy =>
                enemy.StableId.StartsWith(
                    "campaign.enemy.wave-",
                    StringComparison.Ordinal)), Is.True);
            Assert.That(formal.Runtime.Snapshot.SpawnedEnemyCount,
                Is.EqualTo(campaign.SpawnedEnemyCount));
            Assert.That(formal.Runtime.Snapshot.Enemies.Select(enemy =>
                    enemy.StableId),
                Is.EqualTo(campaign.Enemies.Select(enemy => enemy.StableId)));
            GrayboxDefensePersistenceState3D oldPersistence =
                formal.Runtime.CaptureForPersistence();
            Assert.That(oldPersistence.Tutorial.TutorialTriggered, Is.False);
            Assert.That(oldPersistence.Tutorial.Enemies, Is.Empty,
                "Formal campaign ticks must not also tick the retired tutorial.");

            GrayboxBuildingInstance3D legacyTower = CompletedInstance(
                "building.instance.009199",
                BuildingCatalog.MachineGunTurret,
                10,
                10);
            var legacy = new GrayboxDefenseRuntime3D(10f, 10f, 30f, 10f);
            legacy.Synchronize(
                new[] { legacyTower },
                CityMode.Fortress,
                10,
                10,
                BuildingRangeRules.InitialGroundRadius);
            legacy.Tick(
                .1f,
                globallyPaused: false,
                formal.Session.CityStorage);

            Assert.That(legacy.Snapshot.TutorialWaveTriggerCount,
                Is.EqualTo(1));
            Assert.That(legacy.Snapshot.WarningRemainingSeconds,
                Is.EqualTo(14.9f).Within(.0001f));
            Assert.That(legacy.CaptureForPersistence().Tutorial
                .TutorialTriggered, Is.True,
                "The schema-31 compatibility path must remain operational.");
        }

        [Test]
        public void FormalCoreRelocationUpdatesCampaignTruthAndProjection()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(
                includeThreeTowers: false,
                includeSmelter: false);
            SynchronizeFormal(fixture, CityMode.Fortress, 10, 10);
            Assert.That(fixture.Runtime.TrySetPlayerPaused(
                MachineGunId,
                paused: true), Is.True);
            fixture.Runtime.Tick(
                20f,
                globallyPaused: false,
                fixture.Session.CityStorage);
            SingleCityDefenseEnemySnapshot gnawer = fixture.Campaign.Snapshot
                .Enemies.First(enemy => string.Equals(
                    enemy.EnemyDefinitionId,
                    EnemyCatalog.Gnawer.Id.Value,
                    StringComparison.Ordinal));
            int coreBefore = fixture.Campaign.Snapshot.CoreCurrentHealth;

            fixture.Runtime.SetCorePosition(gnawer.X + 10f, gnawer.Z);
            fixture.Runtime.Tick(
                .1f,
                globallyPaused: false,
                fixture.Session.CityStorage);
            SingleCityDefenseEnemySnapshot movedGnawer =
                fixture.Campaign.Snapshot.Enemies.Single(enemy =>
                    string.Equals(
                        enemy.StableId,
                        gnawer.StableId,
                        StringComparison.Ordinal));
            Assert.That(movedGnawer.X, Is.GreaterThan(gnawer.X),
                "Core-targeting movement must immediately follow the new " +
                "core position instead of the construction-time coordinate.");

            fixture.Runtime.SetCorePosition(movedGnawer.X, movedGnawer.Z);
            fixture.Runtime.Tick(
                .3f,
                globallyPaused: false,
                fixture.Session.CityStorage);

            SingleCityDefenseCampaignSnapshot campaign =
                fixture.Runtime.CampaignSnapshot;
            GrayboxDefenseRuntimeSnapshot3D projection =
                fixture.Runtime.Snapshot;
            GrayboxDefenseEnemySnapshot3D projectedGnawer =
                projection.Enemies.Single(enemy => string.Equals(
                    enemy.StableId,
                    gnawer.StableId,
                    StringComparison.Ordinal));
            Assert.That(campaign.CoreCurrentHealth, Is.LessThan(coreBefore),
                "Enemy combat truth must target the relocated core.");
            Assert.That(projection.CoreCurrentHealth,
                Is.EqualTo(campaign.CoreCurrentHealth));
            Assert.That(projectedGnawer.DistanceToCore,
                Is.LessThanOrEqualTo(EnemyCatalog.Gnawer.AttackRange));
            Assert.That(projectedGnawer.IsAttackingCore, Is.True);
        }

        [Test]
        public void EnemyDamageCommitsRealProductionBuildingDestructionOnce()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(
                includeThreeTowers: false,
                includeSmelter: true,
                throwOnPresentationUpdate: true);
            ConfigurePresentationRecovery(
                fixture.Runtime,
                fixture.Presentation);
            fixture.Production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                fixture.Session.CityStorage);
            Assert.That(fixture.Production.TryGetState(
                SmelterId,
                out BuildingProductionState productionState), Is.True);
            Assert.That(productionState.Input.Add(ResourceIds.Iron, 4),
                Is.EqualTo(4));
            Assert.That(productionState.Output.Add(ResourceIds.Alloy, 3),
                Is.EqualTo(3));
            var committed = new List<GrayboxCombatDestructionResult3D>();
            fixture.Coordinator.DestructionCommitted += committed.Add;

            SynchronizeFormal(fixture, CityMode.Fortress, 10, 10);
            Assert.That(fixture.Runtime.TrySetPlayerPaused(
                MachineGunId,
                paused: true), Is.True);
            AdvanceToWaveFive(fixture.Campaign);
            fixture.Campaign.Advance(33f, requestedSpeed: 1);
            Assert.That(fixture.Campaign.Snapshot.Enemies.Any(enemy =>
                string.Equals(
                    enemy.EnemyDefinitionId,
                    EnemyCatalog.Howler.Id.Value,
                    StringComparison.Ordinal)), Is.True);

            for (var step = 0; step < 400; step++)
            {
                fixture.Runtime.Tick(
                    .1f,
                    globallyPaused: false,
                    fixture.Session.CityStorage);
                DefeatNonHowlers(fixture.Campaign);
                if (fixture.Instance(SmelterId).State ==
                    GrayboxBuildingInstanceState.DestroyedRuin)
                {
                    break;
                }
            }

            Assert.That(fixture.Health.TryGetHealth(
                SmelterId,
                out int current,
                out int maximum,
                out bool destroyed), Is.True);
            Assert.That(maximum,
                Is.EqualTo(BuildingCatalog.Smelter.MaximumHealth));
            Assert.That(current, Is.Zero);
            Assert.That(destroyed, Is.True);
            Assert.That(fixture.Instance(SmelterId).State,
                Is.EqualTo(GrayboxBuildingInstanceState.DestroyedRuin));
            Assert.That(fixture.Instance(SmelterId).IsPlayerOwned, Is.False);
            Assert.That(fixture.Production.TryGetState(SmelterId, out _),
                Is.False,
                "The destruction coordinator must clear the real production " +
                "state in the same committed transition.");
            Assert.That(committed, Has.Count.EqualTo(1));
            Assert.That(committed[0].StableInstanceId,
                Is.EqualTo(SmelterId));
            GrayboxCombatDestructionResult3D lastResult =
                ReadProperty<GrayboxCombatDestructionResult3D>(
                    fixture.Runtime,
                    "LastDestructionResult");
            Assert.That(lastResult, Is.SameAs(committed[0]));
            Assert.That(lastResult.Status,
                Is.EqualTo(GrayboxCombatDestructionStatus3D
                    .CommittedPresentationRebuildRequired));
            Assert.That(lastResult.IsCommitted, Is.True);
            Assert.That(lastResult.RequiresPresentationRebuild, Is.True,
                "A recoverable presentation failure must remain observable " +
                "instead of being discarded by the runtime callback.");
            Assert.That(ReadProperty<int>(
                fixture.Runtime,
                "PendingPresentationRebuildCount"), Is.EqualTo(1));
            Assert.That(committed[0].ProductionLostResources.Select(value =>
                    new { value.ResourceId, value.Amount }),
                Is.EquivalentTo(new[]
                {
                    new { ResourceId = ResourceIds.Iron, Amount = 4 },
                    new { ResourceId = ResourceIds.Alloy, Amount = 3 },
                }));
            Assert.That(fixture.Campaign.Snapshot.Statistics
                .BuildingLossCount, Is.EqualTo(1));

            fixture.Runtime.Tick(
                .1f,
                globallyPaused: false,
                fixture.Session.CityStorage);
            Assert.That(ReadProperty<int>(
                fixture.Runtime,
                "PendingPresentationRebuildCount"), Is.Zero,
                "The next normal Tick must retry the latched presentation " +
                "rebuild and clear it only after success.");
            Assert.That(committed, Has.Count.EqualTo(1),
                "destroyedNow must gate the coordinator to one commit.");
            Assert.That(fixture.Campaign.Snapshot.Statistics
                .BuildingLossCount, Is.EqualTo(1));
        }

        [Test]
        public void DestructionHistoryKeepsEachRuinLossAfterLaterBuildingsFall()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(
                includeThreeTowers: false,
                includeSmelter: true);
            fixture.Production.Synchronize(
                fixture.Session.Instances,
                CityMode.Fortress,
                10,
                10,
                fixture.Session.GroundBuildRadius,
                fixture.Session.CityStorage);
            Assert.That(fixture.Production.TryGetState(
                SmelterId,
                out BuildingProductionState productionState), Is.True);
            Assert.That(productionState.Input.Add(ResourceIds.Iron, 4),
                Is.EqualTo(4));

            Assert.That(ApplyCampaignBuildingDamage(
                fixture.Runtime,
                SmelterId,
                BuildingCatalog.Smelter.MaximumHealth),
                Is.EqualTo(BuildingCatalog.Smelter.MaximumHealth));
            Assert.That(ApplyCampaignBuildingDamage(
                fixture.Runtime,
                MachineGunId,
                BuildingCatalog.MachineGunTurret.MaximumHealth),
                Is.EqualTo(BuildingCatalog.MachineGunTurret.MaximumHealth));

            Assert.That(fixture.Runtime.TryGetDestructionResult(
                SmelterId,
                out GrayboxCombatDestructionResult3D smelterLoss), Is.True);
            Assert.That(smelterLoss.StableInstanceId, Is.EqualTo(SmelterId));
            Assert.That(smelterLoss.TotalLostResources.Single().ResourceId,
                Is.EqualTo(ResourceIds.Iron));
            Assert.That(smelterLoss.TotalLostResources.Single().Amount,
                Is.EqualTo(4));
            Assert.That(fixture.Runtime.TryGetDestructionResult(
                MachineGunId,
                out GrayboxCombatDestructionResult3D towerLoss), Is.True);
            Assert.That(towerLoss.StableInstanceId, Is.EqualTo(MachineGunId));
            Assert.That(fixture.Runtime.LastDestructionResult,
                Is.SameAs(towerLoss),
                "The legacy latest-result view remains compatible while " +
                "older ruins keep their own loss summaries.");
        }

        [Test]
        public void BuildingTargetProjectionIsReusedAcrossFixedSteps()
        {
            RuntimeFixture fixture = CreateRuntimeFixture(
                includeThreeTowers: false,
                includeSmelter: true);
            SynchronizeFormal(fixture, CityMode.Fortress, 10, 10);
            Assert.That(fixture.Runtime.TrySetPlayerPaused(
                MachineGunId,
                paused: true), Is.True);
            AdvanceToWaveFive(fixture.Campaign);
            fixture.Campaign.Advance(33f, requestedSpeed: 1);
            fixture.Runtime.Tick(
                .1f,
                globallyPaused: false,
                fixture.Session.CityStorage);

            long before = GC.GetAllocatedBytesForCurrentThread();
            fixture.Runtime.Tick(
                1f,
                globallyPaused: false,
                fixture.Session.CityStorage);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            TestContext.WriteLine(
                "FormalBuildingTargetProjectionAllocationBytes=" + allocated);
            Assert.That(allocated,
                Is.LessThanOrEqualTo(ProjectionAllocationBudgetBytes),
                "The per-0.1-step provider must read the synchronized session " +
                "instances plus health truth through a reusable projection, " +
                "not allocate a new target array/object graph each step.");
            Assert.That(fixture.Health.TryGetHealth(
                SmelterId,
                out int current,
                out int maximum,
                out bool destroyed), Is.True);
            Assert.That(current, Is.LessThan(maximum));
            Assert.That(destroyed, Is.False);
        }

        private RuntimeFixture CreateRuntimeFixture(
            bool includeThreeTowers,
            bool includeSmelter,
            bool throwOnPresentationUpdate = false)
        {
            GrayboxBuildingSession3D session =
                AddComponent<GrayboxBuildingSession3D>("Session");
            session.ConfigureDevelopmentFixture();
            var entries = new List<BuildingEntry>
            {
                new BuildingEntry(
                    MachineGunId,
                    BuildingCatalog.MachineGunTurret,
                    10,
                    10),
            };
            if (includeThreeTowers)
            {
                entries.Add(new BuildingEntry(
                    LaserId,
                    BuildingCatalog.LaserTower,
                    12,
                    10));
                entries.Add(new BuildingEntry(
                    SporeId,
                    BuildingCatalog.SporeTower,
                    14,
                    10));
                entries.Add(new BuildingEntry(
                    HeavyMachineGunId,
                    BuildingCatalog.HeavyMachineGunTurret,
                    16,
                    10));
                entries.Add(new BuildingEntry(
                    "building.instance.009106",
                    BuildingCatalog.AcidTower,
                    18,
                    10));
            }
            if (includeSmelter)
            {
                entries.Add(new BuildingEntry(
                    SmelterId,
                    BuildingCatalog.Smelter,
                    30,
                    10));
            }
            RestoreBuildings(session, entries.ToArray());

            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(session.Instances);
            var production = new GrayboxProductionRuntime3D();
            var runtime = new GrayboxDefenseRuntime3D(
                coreX: 10f,
                coreZ: 10f,
                spawnX: 30f,
                spawnZ: 10f);
            var campaign = new SingleCityDefenseCampaignModel(10f, 10f);
            var presentation = new RecordingPresentation
            {
                ThrowOnUpdate = throwOnPresentationUpdate,
            };
            var coordinator = new GrayboxCombatDestructionCoordinator3D(
                session,
                health,
                production,
                runtime,
                campaign,
                presentation);
            InvokeFormalConfigure(runtime, campaign, health, coordinator);
            return new RuntimeFixture(
                session,
                health,
                production,
                runtime,
                campaign,
                coordinator,
                presentation);
        }

        private ControllerFixture CreateControllerFixture()
        {
            GrayboxBuildingSession3D session =
                AddComponent<GrayboxBuildingSession3D>("Session");
            session.ConfigureDevelopmentFixture();
            GameObject worldObject = Track(new GameObject("World"));
            GrayboxWorldView3D world =
                worldObject.AddComponent<GrayboxWorldView3D>();
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            world.Configure(
                NewChild(worldObject.transform, "Terrain"),
                NewChild(worldObject.transform, "Resources"),
                NewChild(worldObject.transform, "Obstacles"),
                material);
            world.Generate(new WorldMapModel(FilledOpenMap(24, 24)));

            GameObject cityObject = Track(new GameObject("City"));
            Assert.That(world.Coordinates.TryCellToWorld(
                12,
                12,
                .5f,
                out Vector3 cityPosition), Is.True);
            cityObject.transform.position = cityPosition;
            Rigidbody body = cityObject.AddComponent<Rigidbody>();
            BoxCollider collider = cityObject.AddComponent<BoxCollider>();
            GrayboxMobileCityController3D city =
                cityObject.AddComponent<GrayboxMobileCityController3D>();
            city.Configure(world, body, collider);
            Assert.That(city.RestoreDeploymentForDevelopment(
                CityMode.Fortress), Is.True);

            return new ControllerFixture(
                session,
                city,
                world,
                AddComponent<GrayboxBuildingWorldView3D>("Buildings"),
                AddComponent<GrayboxDefenseWorldView3D>("DefenseWorld"),
                AddComponent<GrayboxDefenseHud3D>("DefenseHud"),
                AddComponent<GrayboxProductionController3D>("Production"),
                AddComponent<GrayboxDefenseController3D>("Defense"));
        }

        private static void RestoreBuildings(
            GrayboxBuildingSession3D session,
            params BuildingEntry[] entries)
        {
            var restored = new GrayboxBuildingRestoreEntry3D[entries.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                BuildingEntry entry = entries[index];
                restored[index] = new GrayboxBuildingRestoreEntry3D(
                    entry.StableId,
                    entry.Definition,
                    BuildingSite.Ground,
                    entry.X,
                    entry.Y,
                    BuildingOrientation.North,
                    GrayboxBuildingInstanceState.Completed,
                    constructionRemainingSeconds: 0f,
                    isPlayerOwned: true,
                    isEvacuationLocked: false,
                    ResourceNodeBinding.None);
            }
            Assert.That(session.TryRestoreBuildings(
                restored,
                restoredNextStableInstanceOrdinal: 9200,
                new RecordingPresentation(),
                out string error), Is.True, error);
        }

        private static void SynchronizeFormal(
            RuntimeFixture fixture,
            CityMode cityMode,
            int cityX,
            int cityY)
        {
            fixture.Health.Synchronize(fixture.Session.Instances);
            fixture.Runtime.Synchronize(
                fixture.Session.Instances,
                cityMode,
                cityX,
                cityY,
                fixture.Session.GroundBuildRadius);
        }

        private static void AdvanceToWaveFive(
            SingleCityDefenseCampaignModel campaign)
        {
            Assert.That(campaign.Snapshot.CurrentWaveNumber, Is.EqualTo(1));
            for (var waveIndex = 0; waveIndex < 4; waveIndex++)
            {
                CampaignWaveDefinition wave =
                    CampaignWaveCatalog.All[waveIndex];
                campaign.Advance(
                    wave.WarningSeconds + wave.SpawnSeconds + .2f,
                    requestedSpeed: 1);
                string[] enemies = campaign.Snapshot.Enemies.Select(enemy =>
                        enemy.StableId)
                    .ToArray();
                Assert.That(enemies, Has.Length.EqualTo(wave.TotalCount));
                for (var index = 0; index < enemies.Length; index++)
                {
                    Assert.That(campaign.DefeatEnemy(
                        enemies[index],
                        BuildingCatalog.MachineGunTurret.Id.Value), Is.True);
                }
                campaign.Advance(.1f, requestedSpeed: 1);
            }
            Assert.That(campaign.Snapshot.CurrentWaveNumber, Is.EqualTo(5));
            Assert.That(campaign.Snapshot.Phase,
                Is.EqualTo(SingleCityDefenseCampaignPhase.Warning));
        }

        private static void DefeatNonHowlers(
            SingleCityDefenseCampaignModel campaign)
        {
            string[] stableIds = campaign.Snapshot.Enemies
                .Where(enemy => !string.Equals(
                    enemy.EnemyDefinitionId,
                    EnemyCatalog.Howler.Id.Value,
                    StringComparison.Ordinal))
                .Select(enemy => enemy.StableId)
                .ToArray();
            for (var index = 0; index < stableIds.Length; index++)
            {
                campaign.DefeatEnemy(
                    stableIds[index],
                    BuildingCatalog.MachineGunTurret.Id.Value);
            }
        }

        private static void AssertTowerSupply(
            GrayboxDefenseRuntime3D runtime,
            string stableId,
            string buildingId,
            string resourceId)
        {
            SingleCityDefenseTowerCombatModel tower =
                CampaignTower(runtime, stableId);
            Assert.That(tower.BuildingId, Is.EqualTo(buildingId));
            Assert.That(tower.ConsumableId, Is.EqualTo(resourceId));
            Assert.That(tower.LocalCapacity, Is.EqualTo(30));
            Assert.That(tower.LocalConsumableAmount, Is.EqualTo(30));
            Assert.That(tower.IsLogisticsConnected, Is.True);
        }

        private static SingleCityDefenseTowerCombatModel CampaignTower(
            GrayboxDefenseRuntime3D runtime,
            string stableId)
        {
            MethodInfo lookup = FindFormalTowerLookup();
            Assert.That(lookup, Is.Not.Null,
                "Missing TryGetCampaignTowerState formal wiring.");
            object[] arguments = { stableId, null };
            Assert.That((bool)lookup.Invoke(runtime, arguments), Is.True,
                stableId);
            Assert.That(arguments[1],
                Is.InstanceOf<SingleCityDefenseTowerCombatModel>());
            return (SingleCityDefenseTowerCombatModel)arguments[1];
        }

        private static SingleCityDefenseCampaignSnapshot ReadCampaignSnapshot(
            GrayboxDefenseRuntime3D runtime)
        {
            return ReadProperty<SingleCityDefenseCampaignSnapshot>(
                runtime,
                "CampaignSnapshot");
        }

        private static void InvokeFormalConfigure(
            GrayboxDefenseRuntime3D runtime,
            SingleCityDefenseCampaignModel campaign,
            GrayboxBuildingHealthRuntime3D health,
            GrayboxCombatDestructionCoordinator3D coordinator)
        {
            MethodInfo configure = FindFormalConfigure();
            Assert.That(configure, Is.Not.Null,
                "Missing ConfigureFormalCampaign formal wiring.");
            configure.Invoke(runtime, new object[]
            {
                campaign,
                health,
                coordinator,
            });
        }

        private static void ConfigurePresentationRecovery(
            GrayboxDefenseRuntime3D runtime,
            RecordingPresentation presentation)
        {
            MethodInfo configure = typeof(GrayboxDefenseRuntime3D).GetMethod(
                "ConfigurePresentationRecovery",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(Func<string, bool>) },
                null);
            Assert.That(configure, Is.Not.Null,
                "A failed destruction presentation must be recoverable from " +
                "the normal runtime clock.");
            configure.Invoke(runtime, new object[]
            {
                new Func<string, bool>(presentation.TryRecover),
            });
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
                "campaign.enemy.review-probe",
                stableInstanceId,
                damage,
            });
        }

        private static void InvokeFormalControllerConfigure(
            ControllerFixture fixture)
        {
            InvokeFormalControllerConfigure(fixture.Controller, fixture);
        }

        private static void InvokeFormalControllerConfigure(
            GrayboxDefenseController3D controller,
            ControllerFixture dependencies)
        {
            MethodInfo configure = typeof(GrayboxDefenseController3D).GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                ControllerConfigureParameters(),
                null);
            Assert.That(configure, Is.Not.Null,
                "Missing formal GrayboxDefenseController3D.Configure overload.");
            configure.Invoke(controller, new object[]
            {
                dependencies.Session,
                dependencies.City,
                dependencies.World,
                dependencies.BuildingPresentation,
                dependencies.DefenseWorldView,
                dependencies.Hud,
                dependencies.Production,
            });
        }

        private static MethodInfo FindFormalConfigure()
        {
            return typeof(GrayboxDefenseRuntime3D).GetMethod(
                "ConfigureFormalCampaign",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(SingleCityDefenseCampaignModel),
                    typeof(GrayboxBuildingHealthRuntime3D),
                    typeof(GrayboxCombatDestructionCoordinator3D),
                },
                null);
        }

        private static MethodInfo FindFormalTowerLookup()
        {
            return typeof(GrayboxDefenseRuntime3D).GetMethod(
                "TryGetCampaignTowerState",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string),
                    typeof(SingleCityDefenseTowerCombatModel).MakeByRefType(),
                },
                null);
        }

        private static MethodInfo FindLegacySynchronize()
        {
            return typeof(GrayboxDefenseRuntime3D).GetMethod(
                "Synchronize",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(IReadOnlyList<GrayboxBuildingInstance3D>),
                    typeof(CityMode),
                    typeof(int),
                    typeof(int),
                    typeof(int),
                },
                null);
        }

        private static MethodInfo FindLegacyTick()
        {
            return typeof(GrayboxDefenseRuntime3D).GetMethod(
                "Tick",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(float),
                    typeof(bool),
                    typeof(CityResourceStorageModel),
                },
                null);
        }

        private static Type[] ControllerConfigureParameters()
        {
            return new[]
            {
                typeof(GrayboxBuildingSession3D),
                typeof(GrayboxMobileCityController3D),
                typeof(GrayboxWorldView3D),
                typeof(GrayboxBuildingWorldView3D),
                typeof(GrayboxDefenseWorldView3D),
                typeof(GrayboxDefenseHudView3D),
                typeof(GrayboxProductionController3D),
            };
        }

        private static void AssertReadOnlyProperty(
            PropertyInfo property,
            Type expectedType)
        {
            Assert.That(property, Is.Not.Null,
                "IDEA-0017 RED: missing formal read-only projection.");
            Assert.That(property.PropertyType, Is.EqualTo(expectedType));
            Assert.That(property.CanRead, Is.True);
            Assert.That(property.SetMethod, Is.Null);
        }

        private static T ReadProperty<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Missing " + name + ".");
            object value = property.GetValue(target, null);
            Assert.That(value, Is.InstanceOf<T>(), name);
            return (T)value;
        }

        private static GrayboxBuildingInstance3D CompletedInstance(
            string stableId,
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
                    stableId,
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

        private T AddComponent<T>(string name) where T : Component
        {
            return Track(new GameObject(name)).AddComponent<T>();
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static WorldCell[,] FilledOpenMap(int width, int height)
        {
            var cells = new WorldCell[width, height];
            var open = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                cells[x, y] = open;
            return cells;
        }

        private sealed class BuildingEntry
        {
            public BuildingEntry(
                string stableId,
                BuildingDefinition definition,
                int x,
                int y)
            {
                StableId = stableId;
                Definition = definition;
                X = x;
                Y = y;
            }

            public string StableId { get; }
            public BuildingDefinition Definition { get; }
            public int X { get; }
            public int Y { get; }
        }

        private sealed class RecordingPresentation :
            IGrayboxBuildingPresentation3D
        {
            private int updateFailuresRemaining;

            public bool ThrowOnUpdate
            {
                set => updateFailuresRemaining = value ? 1 : 0;
            }

            public bool TryCreate(GrayboxBuildingInstance3D instance)
            {
                return true;
            }

            public void UpdateInstance(GrayboxBuildingInstance3D instance)
            {
                if (updateFailuresRemaining > 0)
                {
                    updateFailuresRemaining--;
                    throw new InvalidOperationException(
                        "Injected presentation failure.");
                }
            }
            public void Remove(GrayboxBuildingInstance3D instance) { }

            public bool TryRecover(string stableInstanceId)
            {
                UpdateInstance(null);
                return true;
            }
        }

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(
                GrayboxBuildingSession3D session,
                GrayboxBuildingHealthRuntime3D health,
                GrayboxProductionRuntime3D production,
                GrayboxDefenseRuntime3D runtime,
                SingleCityDefenseCampaignModel campaign,
                GrayboxCombatDestructionCoordinator3D coordinator,
                RecordingPresentation presentation)
            {
                Session = session;
                Health = health;
                Production = production;
                Runtime = runtime;
                Campaign = campaign;
                Coordinator = coordinator;
                Presentation = presentation;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxBuildingHealthRuntime3D Health { get; }
            public GrayboxProductionRuntime3D Production { get; }
            public GrayboxDefenseRuntime3D Runtime { get; }
            public SingleCityDefenseCampaignModel Campaign { get; }
            public GrayboxCombatDestructionCoordinator3D Coordinator { get; }
            public RecordingPresentation Presentation { get; }

            public GrayboxBuildingInstance3D Instance(string stableId)
            {
                return Session.Instances.Single(instance => string.Equals(
                    instance.StableInstanceId,
                    stableId,
                    StringComparison.Ordinal));
            }
        }

        private sealed class ControllerFixture
        {
            public ControllerFixture(
                GrayboxBuildingSession3D session,
                GrayboxMobileCityController3D city,
                GrayboxWorldView3D world,
                GrayboxBuildingWorldView3D buildingPresentation,
                GrayboxDefenseWorldView3D defenseWorldView,
                GrayboxDefenseHudView3D hud,
                GrayboxProductionController3D production,
                GrayboxDefenseController3D controller)
            {
                Session = session;
                City = city;
                World = world;
                BuildingPresentation = buildingPresentation;
                DefenseWorldView = defenseWorldView;
                Hud = hud;
                Production = production;
                Controller = controller;
            }

            public GrayboxBuildingSession3D Session { get; }
            public GrayboxMobileCityController3D City { get; }
            public GrayboxWorldView3D World { get; }
            public GrayboxBuildingWorldView3D BuildingPresentation { get; }
            public GrayboxDefenseWorldView3D DefenseWorldView { get; }
            public GrayboxDefenseHudView3D Hud { get; }
            public GrayboxProductionController3D Production { get; }
            public GrayboxDefenseController3D Controller { get; }
        }
    }
}
