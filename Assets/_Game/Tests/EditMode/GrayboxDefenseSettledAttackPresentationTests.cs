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

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseSettledAttackPresentationTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void TutorialTickPublishesAppliedDamageOnceAndNextTickClearsBatch()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.settled-tutorial",
                BuildingCatalog.MachineGunTurret,
                0,
                0);
            var runtime = new GrayboxDefenseRuntime3D(0f, 0f, 9f, 0f);
            runtime.Synchronize(
                new[] { turret },
                CityMode.Fortress,
                0,
                0,
                BuildingRangeRules.InitialGroundRadius);
            using CityResourceStorageModel storage = Storage(
                ResourceIds.Ammunition,
                60);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.Snapshot.Enemies, Is.Not.Empty);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: false), Is.True);

            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseRuntimeSnapshot3D attacked = runtime.Snapshot;
            Assert.That(attacked.SpawnDirections, Is.Empty);
            Assert.That(attacked.Enemies,
                Is.All.Matches<GrayboxDefenseEnemySnapshot3D>(enemy =>
                    enemy.TargetStableId ==
                        SingleCityDefenseCampaignModel.CityCoreTargetId &&
                    enemy.TargetDisplayName == "城市核心" &&
                    enemy.DistanceToTarget == enemy.DistanceToCore &&
                    enemy.IsAttackingTarget == enemy.IsAttackingCore));
            Assert.That(attacked.SettledAttackEvents.Count, Is.EqualTo(1));
            GrayboxDefenseSettledAttackEvent3D attack =
                attacked.SettledAttackEvents[0];
            Assert.That(attack.TowerStableId,
                Is.EqualTo(turret.StableInstanceId));
            Assert.That(attack.TargetStableId, Is.Not.Null.And.Not.Empty);
            Assert.That(attack.AppliedDamage, Is.GreaterThan(0));
            Assert.That(attack.EventSequence, Is.GreaterThan(0ul));
            Assert.That(attack.SettlementSequence, Is.GreaterThan(0ul));

            runtime.Tick(.01f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseRuntimeSnapshot3D cleared = runtime.Snapshot;
            Assert.That(cleared, Is.Not.SameAs(attacked));
            Assert.That(cleared.SettledAttackEvents, Is.Empty);
            Assert.That(attacked.SettledAttackEvents.Count, Is.EqualTo(1),
                "Published attack batches must remain immutable.");

            runtime.Tick(.09f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseSettledAttackEvent3D next =
                runtime.Snapshot.SettledAttackEvents.Single();
            Assert.That(next.EventSequence,
                Is.GreaterThan(attack.EventSequence));
            Assert.That(next.SettlementSequence,
                Is.GreaterThan(attack.SettlementSequence));
        }

        [Test]
        public void CampaignTickPublishesOnlyActualAppliedDamageAndClearsBatch()
        {
            GrayboxBuildingSession3D session = Track(
                new GameObject("SettledCampaignSession"))
                .AddComponent<GrayboxBuildingSession3D>();
            session.ConfigureDevelopmentFixture();
            const string towerId = "building.instance.000091";
            RestoreTurret(
                session,
                towerId,
                BuildingCatalog.LaserTower,
                10,
                10);
            session.Inventory.Set(ResourceIds.EnergyCrystal, 60);

            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(session.Instances);
            var production = new GrayboxProductionRuntime3D();
            var runtime = new GrayboxDefenseRuntime3D(10f, 10f, 30f, 10f);
            var campaign = new SingleCityDefenseCampaignModel(10f, 10f);
            var destruction = new GrayboxCombatDestructionCoordinator3D(
                session,
                health,
                production,
                runtime,
                campaign,
                NullPresentation.Instance);
            runtime.ConfigureFormalCampaign(campaign, health, destruction);
            runtime.Synchronize(
                session.Instances,
                CityMode.Fortress,
                10,
                10,
                session.GroundBuildRadius);
            Assert.That(runtime.TrySetPlayerPaused(towerId, true), Is.True);
            runtime.Tick(
                20f,
                globallyPaused: false,
                cityStorage: session.CityStorage);
            GrayboxDefenseRuntimeSnapshot3D waiting = runtime.Snapshot;
            Assert.That(waiting.SettledAttackEvents, Is.Empty);
            CollectionAssert.AreEqual(
                CampaignWaveCatalog.All[0].Directions,
                waiting.SpawnDirections);
            SingleCityDefenseEnemySnapshot sourceEnemy =
                campaign.Snapshot.Enemies.First();
            GrayboxDefenseEnemySnapshot3D projectedEnemy =
                waiting.Enemies.Single(enemy =>
                    enemy.StableId == sourceEnemy.StableId);
            string expectedTargetId = string.IsNullOrEmpty(
                    sourceEnemy.TargetStableId)
                ? SingleCityDefenseCampaignModel.CityCoreTargetId
                : sourceEnemy.TargetStableId;
            Assert.That(projectedEnemy.TargetStableId,
                Is.EqualTo(expectedTargetId));
            Assert.That(projectedEnemy.TargetDisplayName,
                Is.EqualTo(expectedTargetId == towerId
                    ? BuildingCatalog.LaserTower.Name
                    : "城市核心"));
            Assert.That(projectedEnemy.DistanceToTarget,
                Is.GreaterThanOrEqualTo(0f));
            Assert.That(runtime.TrySetPlayerPaused(towerId, false), Is.True);

            GrayboxDefenseRuntimeSnapshot3D attacked = null;
            for (int step = 0; step < 200; step++)
            {
                runtime.Tick(
                    .1f,
                    globallyPaused: false,
                    cityStorage: session.CityStorage);
                attacked = runtime.Snapshot;
                if (attacked.SettledAttackEvents.Count > 0)
                    break;
            }

            Assert.That(attacked, Is.Not.Null);
            Assert.That(attacked.SettledAttackEvents, Is.Not.Empty);
            Assert.That(
                attacked.Towers.Single().ActiveConsumableSeconds,
                Is.GreaterThan(0f).And.LessThanOrEqualTo(4f));
            Assert.That(attacked.SettledAttackEvents,
                Is.All.Matches<GrayboxDefenseSettledAttackEvent3D>(attack =>
                    attack.TowerStableId == towerId &&
                    !string.IsNullOrEmpty(attack.TargetStableId) &&
                    attack.AppliedDamage > 0));
            ulong previous = 0ul;
            ulong settlement = attacked.SettledAttackEvents[0]
                .SettlementSequence;
            for (int index = 0;
                 index < attacked.SettledAttackEvents.Count;
                 index++)
            {
                GrayboxDefenseSettledAttackEvent3D attack =
                    attacked.SettledAttackEvents[index];
                Assert.That(attack.EventSequence, Is.GreaterThan(previous));
                Assert.That(attack.SettlementSequence, Is.EqualTo(settlement));
                previous = attack.EventSequence;
            }

            runtime.Tick(
                .01f,
                globallyPaused: false,
                cityStorage: session.CityStorage);
            Assert.That(runtime.Snapshot.SettledAttackEvents, Is.Empty);
            Assert.That(attacked.SettledAttackEvents, Is.Not.Empty,
                "Clearing the live batch cannot mutate an older snapshot.");
        }

        [Test]
        public void WorldViewConsumesSettledEventOnceAndNeverInfersFromFiring()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.event-view",
                BuildingCatalog.MachineGunTurret,
                6,
                7);
            const string enemyId = "enemy.event-view.000";
            GrayboxDefenseTowerSnapshot3D firingTower = Tower(
                turret.StableInstanceId,
                enemyId,
                GrayboxDefenseTowerStatus3D.Firing);
            GrayboxDefenseEnemySnapshot3D enemy = Enemy(enemyId, 10, 7);

            fixture.View.Apply(
                Snapshot(new[] { firingTower }, new[] { enemy }),
                new[] { turret });
            Assert.That(fixture.View.VisibleTracerCount, Is.Zero,
                "Firing status is observability only, not an attack event.");

            GrayboxDefenseRuntimeSnapshot3D settled = Snapshot(
                new[]
                {
                    Tower(
                        turret.StableInstanceId,
                        enemyId,
                        GrayboxDefenseTowerStatus3D.NoTarget),
                },
                new[] { enemy },
                new[]
                {
                    new GrayboxDefenseSettledAttackEvent3D(
                        1ul,
                        1ul,
                        turret.StableInstanceId,
                        enemyId,
                        2),
                });
            fixture.View.Apply(settled, new[] { turret });
            Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(1));

            fixture.View.Apply(settled, new[] { turret });
            Assert.That(fixture.View.VisibleTracerCount, Is.Zero,
                "Reapplying one immutable snapshot cannot replay its attack.");

            GrayboxDefenseRuntimeSnapshot3D next = Snapshot(
                settled.Towers,
                settled.Enemies,
                new[]
                {
                    new GrayboxDefenseSettledAttackEvent3D(
                        2ul,
                        2ul,
                        turret.StableInstanceId,
                        enemyId,
                        2),
                });
            fixture.View.Apply(next, new[] { turret });
            Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(1));
        }

        [Test]
        public void WorldViewProjectsKillingBlowToLastKnownEnemyPositionOnce()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.event-killing-blow",
                BuildingCatalog.MachineGunTurret,
                6,
                7);
            const string enemyId = "enemy.event-killing-blow.000";
            GrayboxDefenseTowerSnapshot3D tower = Tower(
                turret.StableInstanceId,
                enemyId,
                GrayboxDefenseTowerStatus3D.Firing);
            GrayboxDefenseEnemySnapshot3D enemy = Enemy(enemyId, 10, 7);

            fixture.View.Apply(
                Snapshot(new[] { tower }, new[] { enemy }),
                new[] { turret });
            Assert.That(fixture.View.TryGetEnemyObject(
                enemyId,
                out GameObject enemyObject), Is.True);
            Vector3 lastKnownEnemyPosition = enemyObject.transform.position;

            GrayboxDefenseRuntimeSnapshot3D killingBlow = Snapshot(
                new[] { tower },
                Array.Empty<GrayboxDefenseEnemySnapshot3D>(),
                new[]
                {
                    new GrayboxDefenseSettledAttackEvent3D(
                        1ul,
                        1ul,
                        turret.StableInstanceId,
                        enemyId,
                        1),
                });
            fixture.View.Apply(killingBlow, new[] { turret });

            Assert.That(fixture.View.EnemyVisualCount, Is.Zero);
            Assert.That(fixture.View.TryGetEnemyObject(enemyId, out _), Is.False);
            Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(1),
                "The settled killing blow remains visible after the dead " +
                "enemy leaves the live combat snapshot.");
            Assert.That(fixture.View.TryGetTowerObject(
                turret.StableInstanceId,
                out GameObject towerObject), Is.True);
            LineRenderer tracer = towerObject
                .GetComponentInChildren<LineRenderer>(includeInactive: true);
            Assert.That(tracer.GetPosition(tracer.positionCount - 1),
                Is.EqualTo(lastKnownEnemyPosition));

            fixture.View.Apply(killingBlow, new[] { turret });
            Assert.That(fixture.View.VisibleTracerCount, Is.Zero,
                "The retained endpoint cannot replay an already-consumed hit.");
        }

        [Test]
        public void EventPoolOverflowClipsPresentationAndConsumesOverflowOnce()
        {
            WorldFixture fixture = CreateWorldFixture();
            GrayboxBuildingInstance3D[] instances = Enumerable.Range(0, 25)
                .Select(index => CompletedTurret(
                    "building.instance.event-overflow-" + index,
                    TowerDefinition(index),
                    2 + index % 10,
                    2 + index / 10))
                .ToArray();
            GrayboxDefenseEnemySnapshot3D[] enemies = Enumerable.Range(0, 25)
                .Select(index => Enemy(
                    "enemy.event-overflow-" + index,
                    12 + index * .1f,
                    10))
                .ToArray();
            GrayboxDefenseTowerSnapshot3D[] towers = instances
                .Select((instance, index) => Tower(
                    instance.StableInstanceId,
                    enemies[index].StableId,
                    GrayboxDefenseTowerStatus3D.NoTarget))
                .ToArray();
            GrayboxDefenseSettledAttackEvent3D[] events = instances
                .Select((instance, index) =>
                    new GrayboxDefenseSettledAttackEvent3D(
                        (ulong)(index + 1),
                        1ul,
                        instance.StableInstanceId,
                        enemies[index].StableId,
                        1))
                .ToArray();
            GrayboxDefenseRuntimeSnapshot3D settled = Snapshot(
                towers,
                enemies,
                events);

            fixture.View.Apply(settled, instances);
            Assert.That(fixture.View.VisibleTracerCount, Is.EqualTo(24));
            Assert.That(settled.SettledAttackEvents.Count, Is.EqualTo(25));
            Assert.That(settled.SettledAttackEvents.Sum(value =>
                value.AppliedDamage), Is.EqualTo(25));

            fixture.View.Apply(settled, instances);
            Assert.That(fixture.View.VisibleTracerCount, Is.Zero,
                "A clipped event is consumed instead of retried forever.");
            Assert.That(fixture.View.LastSnapshot, Is.SameAs(settled));
            Assert.That(settled.SettledAttackEvents.Count, Is.EqualTo(25));
        }

        private WorldFixture CreateWorldFixture()
        {
            GameObject root = Track(new GameObject("SettledAttackWorld"));
            Transform enemyRoot = NewChild(root.transform, "Enemies");
            Transform towerRoot = NewChild(root.transform, "Towers");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            GrayboxDefenseWorldView3D view =
                root.AddComponent<GrayboxDefenseWorldView3D>();
            view.Configure(
                enemyRoot,
                towerRoot,
                material,
                new PlanarCoordinateMapper3D(32, 24));
            return new WorldFixture(view);
        }

        private static GrayboxDefenseRuntimeSnapshot3D Snapshot(
            IReadOnlyList<GrayboxDefenseTowerSnapshot3D> towers,
            IReadOnlyList<GrayboxDefenseEnemySnapshot3D> enemies,
            IReadOnlyList<GrayboxDefenseSettledAttackEvent3D> events = null)
        {
            return new GrayboxDefenseRuntimeSnapshot3D(
                1,
                WavePhase.Active,
                0f,
                enemies.Count,
                enemies.Count,
                0,
                2000,
                2000,
                towers,
                enemies,
                settledAttackEvents: events);
        }

        private static GrayboxDefenseTowerSnapshot3D Tower(
            string stableId,
            string targetId,
            GrayboxDefenseTowerStatus3D status)
        {
            return new GrayboxDefenseTowerSnapshot3D(
                stableId,
                30,
                30,
                connected: true,
                playerPaused: false,
                targetId,
                status);
        }

        private static GrayboxDefenseEnemySnapshot3D Enemy(
            string stableId,
            float x,
            float z)
        {
            return new GrayboxDefenseEnemySnapshot3D(
                stableId,
                spawnOrder: 0,
                x,
                z,
                currentHealth: 60,
                isAttackingCore: false);
        }

        private static CityResourceStorageModel Storage(
            string resourceId,
            int amount)
        {
            var inventory = new ResourceInventory(500);
            inventory.Add(resourceId, amount);
            return new CityResourceStorageModel(inventory, 150);
        }

        private static void RestoreTurret(
            GrayboxBuildingSession3D session,
            string stableId,
            BuildingDefinition definition,
            int x,
            int y)
        {
            Assert.That(session.TryRestoreBuildings(
                new[]
                {
                    new GrayboxBuildingRestoreEntry3D(
                        stableId,
                        definition,
                        BuildingSite.Ground,
                        x,
                        y,
                        BuildingOrientation.North,
                        GrayboxBuildingInstanceState.Completed,
                        0f,
                        isPlayerOwned: true,
                        isEvacuationLocked: false,
                        ResourceNodeBinding.None),
                },
                100,
                NullPresentation.Instance,
                out string error), Is.True, error);
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
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

        private static BuildingDefinition TowerDefinition(int index)
        {
            switch (index % 3)
            {
                case 1:
                    return BuildingCatalog.LaserTower;
                case 2:
                    return BuildingCatalog.SporeTower;
                default:
                    return BuildingCatalog.MachineGunTurret;
            }
        }

        private Transform NewChild(Transform parent, string name)
        {
            GameObject child = Track(new GameObject(name));
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private sealed class WorldFixture
        {
            public WorldFixture(GrayboxDefenseWorldView3D view)
            {
                View = view;
            }

            public GrayboxDefenseWorldView3D View { get; }
        }

        private sealed class NullPresentation :
            IGrayboxBuildingPresentation3D
        {
            public static NullPresentation Instance { get; } =
                new NullPresentation();

            public bool TryCreate(GrayboxBuildingInstance3D instance) => true;
            public void UpdateInstance(GrayboxBuildingInstance3D instance) { }
            public void Remove(GrayboxBuildingInstance3D instance) { }
        }
    }
}
