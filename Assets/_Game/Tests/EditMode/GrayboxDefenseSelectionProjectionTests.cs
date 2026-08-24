using System;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseSelectionProjectionTests
    {
        [Test]
        public void OrdinaryBuildingAndDestroyedRuinKeepCatalogIdentityAndHealth()
        {
            GrayboxBuildingInstance3D wall = Instance(
                "building.instance.selection-wall",
                BuildingCatalog.Wall,
                GrayboxBuildingInstanceState.Completed);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { wall });

            GrayboxDefenseSelectionSnapshot3D selected =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Building,
                    wall.StableInstanceId,
                    defense: null,
                    new[] { wall },
                    health,
                    ProductionObservabilitySnapshot.Empty);

            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.Kind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.Building));
            Assert.That(selected.StableId, Is.EqualTo(wall.StableInstanceId));
            Assert.That(selected.DefinitionId,
                Is.EqualTo(BuildingCatalog.Wall.Id.Value));
            Assert.That(selected.DisplayName, Is.EqualTo("城墙"));
            Assert.That(selected.CurrentHealth,
                Is.EqualTo(BuildingCatalog.Wall.MaximumHealth));
            Assert.That(selected.MaximumHealth,
                Is.EqualTo(BuildingCatalog.Wall.MaximumHealth));
            Assert.That(selected.StatusText, Is.EqualTo("正常运行"));

            Invoke(wall, "DestroyForCombat");
            GrayboxDefenseSelectionSnapshot3D ruin =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Ruin,
                    wall.StableInstanceId,
                    defense: null,
                    new[] { wall },
                    health,
                    ProductionObservabilitySnapshot.Empty);

            Assert.That(ruin.Kind,
                Is.EqualTo(GrayboxDefenseSelectionKind3D.Ruin));
            Assert.That(ruin.DisplayName, Is.EqualTo("城墙废墟"));
            Assert.That(ruin.CurrentHealth, Is.Zero);
            Assert.That(ruin.MaximumHealth,
                Is.EqualTo(BuildingCatalog.Wall.MaximumHealth));
            Assert.That(ruin.StatusText, Is.EqualTo("战损废墟"));
            Assert.That(ruin.CanToggleTowerPause, Is.False);
        }

        [TestCase(ProductionStopReason.None, "正常运行")]
        [TestCase(ProductionStopReason.MissingInput, "缺少输入")]
        [TestCase(ProductionStopReason.OutputFull, "输出已满")]
        [TestCase(ProductionStopReason.OutOfLogistics, "不在物流范围")]
        [TestCase(ProductionStopReason.Depleted, "矿脉已枯竭")]
        [TestCase(ProductionStopReason.PlayerPaused, "玩家暂停运行")]
        public void ProductionStopReasonUsesOneStablePlayerFacingMapping(
            ProductionStopReason reason,
            string expected)
        {
            Assert.That(
                GrayboxDefenseSelectionProjection3D
                    .ProductionStopReasonText(reason),
                Is.EqualTo(expected));
        }

        [Test]
        public void TowerProjectionUsesInstanceCatalogAndLiveBuildingHealth()
        {
            GrayboxBuildingInstance3D tower = Instance(
                "building.instance.selection-laser",
                BuildingCatalog.LaserTower,
                GrayboxBuildingInstanceState.Completed);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { tower });
            Assert.That(health.TryApplyDamage(
                tower.StableInstanceId,
                31,
                out _,
                out _), Is.True);
            var towerSnapshot = new GrayboxDefenseTowerSnapshot3D(
                tower.StableInstanceId,
                ammo: 4,
                ammoCapacity: 12,
                range: 13f,
                connected: true,
                canRunLocally: true,
                playerPaused: false,
                targetId: "enemy.selection.1",
                status: GrayboxDefenseTowerStatus3D.Firing);
            GrayboxDefenseRuntimeSnapshot3D defense = Defense(
                new[] { towerSnapshot },
                Array.Empty<GrayboxDefenseEnemySnapshot3D>());

            GrayboxDefenseSelectionSnapshot3D selected =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Tower,
                    tower.StableInstanceId,
                    defense,
                    new[] { tower },
                    health,
                    ProductionObservabilitySnapshot.Empty);

            Assert.That(selected.DisplayName, Is.EqualTo("激光塔"));
            Assert.That(selected.DefinitionId,
                Is.EqualTo(BuildingCatalog.LaserTower.Id.Value));
            Assert.That(selected.CurrentHealth,
                Is.EqualTo(BuildingCatalog.LaserTower.MaximumHealth - 31));
            Assert.That(selected.MaximumHealth,
                Is.EqualTo(BuildingCatalog.LaserTower.MaximumHealth));
            Assert.That(selected.StatusText, Is.EqualTo("射击"));
            Assert.That(selected.TargetStableId, Is.EqualTo("enemy.selection.1"));
            Assert.That(selected.CanToggleTowerPause, Is.True);
        }

        [Test]
        public void MissingOrMismatchedSelectionDoesNotCreateInventedDetails()
        {
            Assert.That(
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Building,
                    "missing",
                    defense: null,
                    Array.Empty<GrayboxBuildingInstance3D>(),
                    new GrayboxBuildingHealthRuntime3D(),
                    ProductionObservabilitySnapshot.Empty),
                Is.Null);
        }

        [Test]
        public void EnemyProjectionUsesCatalogIdentityAndSnapshotHealth()
        {
            var enemy = new GrayboxDefenseEnemySnapshot3D(
                "enemy.selection.howler",
                EnemyCatalog.Howler.Id.Value,
                spawnOrder: 2,
                x: 8f,
                z: 4f,
                currentHealth: 73,
                maximumHealth: EnemyCatalog.Howler.MaximumHealth,
                targetName: "城墙",
                distanceToCore: 6.5f,
                isAttackingCore: false);
            GrayboxDefenseRuntimeSnapshot3D defense = Defense(
                Array.Empty<GrayboxDefenseTowerSnapshot3D>(),
                new[] { enemy });

            GrayboxDefenseSelectionSnapshot3D selected =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Enemy,
                    enemy.StableId,
                    defense,
                    Array.Empty<GrayboxBuildingInstance3D>(),
                    new GrayboxBuildingHealthRuntime3D(),
                    ProductionObservabilitySnapshot.Empty);

            Assert.That(selected.DisplayName, Is.EqualTo("啸叫者"));
            Assert.That(selected.DefinitionId,
                Is.EqualTo(EnemyCatalog.Howler.Id.Value));
            Assert.That(selected.CurrentHealth, Is.EqualTo(73));
            Assert.That(selected.MaximumHealth,
                Is.EqualTo(EnemyCatalog.Howler.MaximumHealth));
            Assert.That(selected.TargetStableId, Is.Empty);
            Assert.That(selected.TargetDisplayName, Is.EqualTo("城墙"));
            Assert.That(selected.CanToggleTowerPause, Is.False);
        }

        private static GrayboxDefenseRuntimeSnapshot3D Defense(
            GrayboxDefenseTowerSnapshot3D[] towers,
            GrayboxDefenseEnemySnapshot3D[] enemies)
        {
            return new GrayboxDefenseRuntimeSnapshot3D(
                tutorialWaveTriggerCount: 0,
                WavePhase.Idle,
                warningRemainingSeconds: 0f,
                spawnedEnemyCount: enemies.Length,
                aliveEnemyCount: enemies.Length,
                defeatedEnemyCount: 0,
                coreMaximumHealth: 2000,
                coreCurrentHealth: 2000,
                towers,
                enemies);
        }

        private static GrayboxBuildingInstance3D Instance(
            string stableId,
            BuildingDefinition definition,
            GrayboxBuildingInstanceState state)
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
            Assert.That(constructor, Is.Not.Null);
            var instance = (GrayboxBuildingInstance3D)constructor.Invoke(
                new object[]
                {
                    stableId,
                    new PlacedBuilding(definition, 1, 1),
                    new ConstructionProgress(definition.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            switch (state)
            {
                case GrayboxBuildingInstanceState.Completed:
                    Invoke(instance, "Complete");
                    break;
                case GrayboxBuildingInstanceState.AbandonedRuin:
                    Invoke(instance, "Complete");
                    Invoke(instance, "Abandon");
                    break;
                case GrayboxBuildingInstanceState.DestroyedRuin:
                    Invoke(instance, "Complete");
                    Invoke(instance, "DestroyForCombat");
                    break;
            }
            return instance;
        }

        private static void Invoke(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }
    }
}
