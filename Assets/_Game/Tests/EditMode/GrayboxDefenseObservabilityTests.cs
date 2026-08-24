using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseObservabilityTests
    {
        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                    UnityEngine.Object.DestroyImmediate(created[index]);
            }
            created.Clear();
        }

        [Test]
        public void TowerSnapshotOwnsFormalRangeAndHudShowsTenCells()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-range");
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, turret, CityMode.Fortress);

            GrayboxDefenseTowerSnapshot3D tower =
                runtime.Snapshot.Towers.Single();
            Assert.That(
                tower.Range,
                Is.EqualTo(DefenseTowerCatalog.For(
                    BuildingCatalog.MachineGunTurret.Id.Value).Range));
            Assert.That(tower.Range, Is.EqualTo(10f));

            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                runtime.Snapshot,
                GrayboxDefenseSelectionKind3D.Tower,
                turret.StableInstanceId);
            Assert.That(hud.SelectionText.text, Does.Contain("射程 10"));
        }

        [Test]
        public void CampaignHudProjectsCompleteWaveAndSpeedSnapshot()
        {
            GrayboxDefenseRuntimeSnapshot3D snapshot = CampaignSnapshot(
                waveNumber: 3,
                phase: SingleCityDefenseCampaignPhase.Warning,
                warningSeconds: 12.34f,
                planned: 14,
                spawned: 5,
                alive: 4,
                coreHealth: 1750);
            GrayboxDefenseHudView3D hud = CreateHud();

            hud.ApplySpeed(requestedSpeed: 2f, effectiveSpeed: 0f);
            hud.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);

            Assert.That(hud.SummaryText.text, Is.EqualTo(
                "防御 | 第 3/10 波\n" +
                "阶段 预警 | 倒计时 12.3 秒\n" +
                "入口 东 / 北 | 组成 啃噬者×12 / 晶壳兽×2\n" +
                "已生成 5/14 | 存活敌人 4\n" +
                "核心 1750/2000"));
            Assert.That(
                hud.SpeedText.text,
                Is.EqualTo("速度 | 请求 2× | 有效 0×"));
        }

        [TestCase(SingleCityDefenseCampaignPhase.Idle, "未开始")]
        [TestCase(SingleCityDefenseCampaignPhase.Warning, "预警")]
        [TestCase(
            SingleCityDefenseCampaignPhase.SpawningAndCombat,
            "生成与战斗")]
        [TestCase(
            SingleCityDefenseCampaignPhase.CombatCleanup,
            "战斗清理")]
        [TestCase(SingleCityDefenseCampaignPhase.Victory, "胜利")]
        [TestCase(SingleCityDefenseCampaignPhase.Defeat, "失败")]
        public void CampaignHudUsesStableFormalPhaseLabels(
            SingleCityDefenseCampaignPhase phase,
            string expectedLabel)
        {
            GrayboxDefenseRuntimeSnapshot3D snapshot = CampaignSnapshot(
                waveNumber: phase == SingleCityDefenseCampaignPhase.Idle
                    ? 0
                    : 1,
                phase: phase,
                warningSeconds: 0f,
                planned: phase == SingleCityDefenseCampaignPhase.Idle
                    ? 0
                    : 8,
                spawned: 0,
                alive: 0,
                coreHealth: phase == SingleCityDefenseCampaignPhase.Defeat
                    ? 0
                    : 2000);
            GrayboxDefenseHudView3D hud = CreateHud();

            hud.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);

            Assert.That(
                hud.SummaryText.text,
                Does.Contain("阶段 " + expectedLabel));
        }

        [Test]
        public void ReapplyingSameImmutableSnapshotDoesNotReformatText()
        {
            GrayboxDefenseRuntimeSnapshot3D snapshot = CampaignSnapshot(
                waveNumber: 1,
                phase: SingleCityDefenseCampaignPhase.Warning,
                warningSeconds: 15f,
                planned: 8,
                spawned: 0,
                alive: 0,
                coreHealth: 2000);
            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);
            int refreshCount = hud.RefreshCount;
            string summary = hud.SummaryText.text;

            hud.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);

            Assert.That(hud.RefreshCount, Is.EqualTo(refreshCount));
            Assert.That(hud.SummaryText.text, Is.SameAs(summary));

            hud.ApplySpeed(2f, 0f);
            string speed = hud.SpeedText.text;
            hud.ApplySpeed(2f, 0f);
            Assert.That(hud.SpeedText.text, Is.SameAs(speed));
        }

        [Test]
        public void EnemyHudUsesSnapshotDefinitionAndMaximumHealth()
        {
            GrayboxDefenseEnemySnapshot3D enemy = EnemySnapshot(
                EnemyCatalog.CrystalBeast.Id.Value,
                EnemyCatalog.CrystalBeast.MaximumHealth,
                currentHealth: 137);
            var snapshot = new GrayboxDefenseRuntimeSnapshot3D(
                1,
                WavePhase.Active,
                0f,
                1,
                1,
                0,
                2000,
                2000,
                Array.Empty<GrayboxDefenseTowerSnapshot3D>(),
                new[] { enemy });
            GrayboxDefenseHudView3D hud = CreateHud();

            hud.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.Enemy,
                enemy.StableId);

            Assert.That(
                hud.SelectionText.text,
                Does.StartWith("晶壳兽\n生命 137/220"));
        }

        [Test]
        public void SelectionDetailsDriveFormalBuildingAndProductionText()
        {
            GrayboxBuildingInstance3D smelter = CompletedBuilding(
                "building.instance.observability-smelter",
                BuildingCatalog.Smelter);
            var state = new BuildingProductionState(
                smelter.StableInstanceId,
                FormalProductionDefinitionCatalog.Smelting);
            state.Input.Add(ResourceIds.Iron, 5);
            state.Output.Add(ResourceIds.Alloy, 3);
            state.SetLogisticsConnected(false);
            state.SetPlayerPaused(true);
            ProductionObservabilitySnapshot production =
                CaptureProduction(state);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { smelter });
            GrayboxDefenseSelectionSnapshot3D details =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Building,
                    smelter.StableInstanceId,
                    defense: null,
                    new[] { smelter },
                    health,
                    production);
            GrayboxDefenseHudView3D hud = CreateHud();

            hud.Apply(
                null,
                GrayboxDefenseSelectionKind3D.Building,
                smelter.StableInstanceId,
                details);

            Assert.That(hud.SelectionText.text, Is.EqualTo(
                "冶炼厂\n" +
                "生命 280/280\n" +
                "状态 玩家暂停运行\n" +
                "配方 合金冶炼：铁矿×2 → 合金×1（6秒）\n" +
                "内部输入 铁矿 5/20\n" +
                "内部输出 合金 3/10\n" +
                "物流 已断开\n" +
                "停工原因 玩家暂停运行"));
        }

        [Test]
        public void GlobalPauseDoesNotReplaceProductionStopReason()
        {
            GrayboxBuildingInstance3D smelter = CompletedBuilding(
                "building.instance.observability-paused-smelter",
                BuildingCatalog.Smelter);
            var state = new BuildingProductionState(
                smelter.StableInstanceId,
                FormalProductionDefinitionCatalog.Smelting);
            SetPrivateAutoProperty(
                state,
                "StopReason",
                ProductionStopReason.MissingInput);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { smelter });

            GrayboxDefenseSelectionSnapshot3D details =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Building,
                    smelter.StableInstanceId,
                    defense: null,
                    new[] { smelter },
                    health,
                    CaptureProduction(state),
                    globallyPaused: true);
            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                null,
                GrayboxDefenseSelectionKind3D.Building,
                smelter.StableInstanceId,
                details);

            Assert.That(details.StatusText, Is.EqualTo("缺少输入"));
            Assert.That(
                hud.SelectionText.text,
                Does.Contain("停工原因 缺少输入"));
            Assert.That(
                hud.SelectionText.text,
                Does.Not.Contain("游戏暂停"));
        }

        [Test]
        public void LegacyCampaignSnapshotConstructorProjectsStableWaveData()
        {
            ConstructorInfo constructor =
                typeof(GrayboxDefenseRuntimeSnapshot3D).GetConstructor(
                    new[]
                    {
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(SingleCityDefenseCampaignPhase),
                        typeof(WavePhase),
                        typeof(float),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(int),
                        typeof(IReadOnlyList<
                            GrayboxDefenseTowerSnapshot3D>),
                        typeof(IReadOnlyList<
                            GrayboxDefenseEnemySnapshot3D>),
                    });
            Assert.That(constructor, Is.Not.Null);
            var snapshot = (GrayboxDefenseRuntimeSnapshot3D)
                constructor.Invoke(new object[]
                {
                    1,
                    3,
                    CampaignWaveCatalog.All.Count,
                    SingleCityDefenseCampaignPhase.Warning,
                    WavePhase.Warning,
                    20f,
                    14,
                    0,
                    0,
                    0,
                    2000,
                    2000,
                    Array.Empty<GrayboxDefenseTowerSnapshot3D>(),
                    Array.Empty<GrayboxDefenseEnemySnapshot3D>(),
                });

            Assert.That(
                snapshot.SpawnDirections,
                Is.SameAs(CampaignWaveCatalog.All[2].Directions));
            Assert.That(
                snapshot.WaveComposition,
                Is.SameAs(CampaignWaveCatalog.All[2].Entries));
        }

        [Test]
        public void SelectionDetailsShowRuinCleanupWithoutInventedAmounts()
        {
            GrayboxBuildingInstance3D wall = CompletedBuilding(
                "building.instance.observability-ruin",
                BuildingCatalog.Wall);
            InvokeNonPublic(wall, "DestroyForCombat");
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { wall });
            GrayboxCombatDestructionResult3D destruction =
                CreateDestructionResult(
                    wall.StableInstanceId,
                    wall.Placement.Definition.Id.Value,
                    new[]
                    {
                        new ResourceAmount(ResourceIds.Iron, 3),
                        new ResourceAmount(ResourceIds.Alloy, 2),
                    });
            GrayboxDefenseSelectionSnapshot3D details =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Ruin,
                    wall.StableInstanceId,
                    defense: null,
                    new[] { wall },
                    health,
                    ProductionObservabilitySnapshot.Empty,
                    destructionResult: destruction);
            GrayboxDefenseHudView3D hud = CreateHud();

            hud.Apply(
                null,
                GrayboxDefenseSelectionKind3D.Ruin,
                wall.StableInstanceId,
                details);

            Assert.That(hud.SelectionText.text, Is.EqualTo(
                "城墙废墟\n" +
                "生命 0/300\n" +
                "状态 战损废墟\n" +
                "损失 铁矿×3 / 合金×2；内部库存与预留已清空"));
            Assert.That(hud.SelectionText.text, Does.Not.Contain("库存 0"));
            Assert.That(hud.SelectionText.text, Does.Not.Contain("预留 0"));
        }

        [Test]
        public void SelectionDetailsOverrideLegacyTowerIdentityAndPausePermission()
        {
            GrayboxBuildingInstance3D laser = CompletedBuilding(
                "building.instance.observability-laser",
                BuildingCatalog.LaserTower);
            SetEvacuationLocked(laser, locked: true);
            var tower = new GrayboxDefenseTowerSnapshot3D(
                laser.StableInstanceId,
                ammo: 4,
                ammoCapacity: 12,
                range: 13f,
                connected: true,
                canRunLocally: false,
                playerPaused: false,
                targetId: null,
                status: GrayboxDefenseTowerStatus3D.Unavailable,
                activeConsumableSeconds: 2f);
            var defense = new GrayboxDefenseRuntimeSnapshot3D(
                0,
                WavePhase.Idle,
                0f,
                0,
                0,
                0,
                2000,
                2000,
                new[] { tower },
                Array.Empty<GrayboxDefenseEnemySnapshot3D>());
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { laser });
            GrayboxDefenseSelectionSnapshot3D details =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Tower,
                    laser.StableInstanceId,
                    defense,
                    new[] { laser },
                    health,
                    ProductionObservabilitySnapshot.Empty);
            GrayboxDefenseHudView3D hud = CreateHud();

            hud.Apply(
                defense,
                GrayboxDefenseSelectionKind3D.Tower,
                laser.StableInstanceId,
                details);

            Assert.That(hud.SelectionText.text, Does.StartWith(
                "激光塔\n生命 280/280"));
            Assert.That(hud.SelectionText.text,
                Does.Contain("状态 撤离处理中"));
            Assert.That(hud.SelectionText.text,
                Does.Contain("伤害 能量 | DPS 48"));
            Assert.That(hud.SelectionText.text,
                Does.Contain("耗材 能晶 | 每 4 秒 1"));
            Assert.That(hud.SelectionText.text,
                Does.Contain("本地 能晶 4/12"));
            Assert.That(hud.SelectionText.text,
                Does.Contain("预计续航 18 秒"));
            Assert.That(details.CanToggleTowerPause, Is.False);
            Assert.That(PrivateField<Button>(hud, "towerPauseButton")
                .interactable, Is.False);
        }

        [Test]
        public void SelectionDetailsDriveFormalEnemyIdentityHealthAndStatus()
        {
            var enemy = new GrayboxDefenseEnemySnapshot3D(
                "enemy.observability.howler.0001",
                EnemyCatalog.Howler.Id.Value,
                spawnOrder: 2,
                x: 8f,
                z: 4f,
                currentHealth: 73,
                maximumHealth: EnemyCatalog.Howler.MaximumHealth,
                targetName: "城墙",
                distanceToCore: 6.5f,
                isAttackingCore: false,
                targetStableId: "building.instance.enemy-target-wall",
                targetDisplayName: "城墙",
                distanceToTarget: 1.5f,
                isAttackingTarget: true);
            var defense = new GrayboxDefenseRuntimeSnapshot3D(
                0,
                WavePhase.Active,
                0f,
                1,
                1,
                0,
                2000,
                2000,
                Array.Empty<GrayboxDefenseTowerSnapshot3D>(),
                new[] { enemy });
            GrayboxDefenseSelectionSnapshot3D details =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Enemy,
                    enemy.StableId,
                    defense,
                    Array.Empty<GrayboxBuildingInstance3D>(),
                    new GrayboxBuildingHealthRuntime3D(),
                    ProductionObservabilitySnapshot.Empty);
            GrayboxDefenseHudView3D hud = CreateHud();

            hud.Apply(
                defense,
                GrayboxDefenseSelectionKind3D.Enemy,
                enemy.StableId,
                details);

            Assert.That(hud.SelectionText.text, Is.EqualTo(
                "啸叫者\n" +
                "生命 73/100\n" +
                "状态 攻击目标\n" +
                "移速 1.2 | DPS 12 | 射程 7 格\n" +
                "护甲 轻型\n" +
                "当前目标 城墙\n" +
                "距目标 1.5 格"));
        }

        [Test]
        public void ProjectionKeepsStatePriorityAndAllowsUnfinishedFormalTower()
        {
            GrayboxBuildingInstance3D unfinished = NewBuilding(
                "building.instance.observability-unfinished-tower",
                BuildingCatalog.LaserTower);
            var health = new GrayboxBuildingHealthRuntime3D();
            health.Synchronize(new[] { unfinished });

            GrayboxDefenseSelectionSnapshot3D details =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Tower,
                    unfinished.StableInstanceId,
                    defense: null,
                    new[] { unfinished },
                    health,
                    ProductionObservabilitySnapshot.Empty,
                    globallyPaused: true);

            Assert.That(details, Is.Not.Null);
            Assert.That(details.DisplayName, Is.EqualTo("激光塔"));
            Assert.That(details.StatusText, Is.EqualTo("施工中"),
                "Construction state has priority over the global pause.");
            Assert.That(details.Tower, Is.Null);
            Assert.That(details.CanToggleTowerPause, Is.False);

            GrayboxBuildingInstance3D completed = CompletedBuilding(
                "building.instance.observability-global-pause",
                BuildingCatalog.Wall);
            health.Synchronize(new[] { completed });
            GrayboxDefenseSelectionSnapshot3D paused =
                GrayboxDefenseSelectionProjection3D.Capture(
                    GrayboxDefenseSelectionKind3D.Building,
                    completed.StableInstanceId,
                    defense: null,
                    new[] { completed },
                    health,
                    ProductionObservabilitySnapshot.Empty,
                    globallyPaused: true);
            Assert.That(paused.StatusText, Is.EqualTo("游戏暂停"));
        }

        [Test]
        public void EnemySnapshotOwnsCoreTargetAndDistanceAndHudUpdatesAfterMove()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-enemy");
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, turret, CityMode.Fortress);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            using var storage = EmptyStorage();
            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseEnemySnapshot3D before =
                runtime.Snapshot.Enemies.Single();
            float expectedBefore = Distance(before.X, before.Z, 0f, 0f);
            Assert.That(before.TargetName, Is.EqualTo("城市核心"));
            Assert.That(
                before.DistanceToCore,
                Is.EqualTo(expectedBefore).Within(.001f));

            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                runtime.Snapshot,
                GrayboxDefenseSelectionKind3D.Enemy,
                before.StableId);
            Assert.That(hud.SelectionText.text, Does.Contain("目标 城市核心"));
            Assert.That(
                hud.SelectionText.text,
                Does.Contain("距核心 " + FormatDistance(expectedBefore) + " 格"));

            runtime.SetCorePosition(x: 30f, z: 0f);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseEnemySnapshot3D after =
                runtime.Snapshot.Enemies.Single(value =>
                    value.StableId == before.StableId);
            float expectedAfter = Distance(after.X, after.Z, 30f, 0f);
            Assert.That(after.TargetName, Is.EqualTo("城市核心"));
            Assert.That(
                after.DistanceToCore,
                Is.EqualTo(expectedAfter).Within(.001f));
            Assert.That(
                after.DistanceToCore,
                Is.Not.EqualTo(before.DistanceToCore).Within(.001f));

            hud.Apply(
                runtime.Snapshot,
                GrayboxDefenseSelectionKind3D.Enemy,
                after.StableId);
            Assert.That(
                hud.SelectionText.text,
                Does.Contain("距核心 " + FormatDistance(expectedAfter) + " 格"));
        }

        [Test]
        public void DestroyedCoreSnapshotClampsHealthAndHudAnnouncesLoss()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-core-loss");
            GrayboxDefenseRuntime3D runtime = Runtime();
            Synchronize(runtime, turret, CityMode.Fortress);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            using var storage = EmptyStorage();

            runtime.Tick(120f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseRuntimeSnapshot3D snapshot = runtime.Snapshot;
            Assert.That(snapshot.CoreCurrentHealth, Is.Zero);
            Assert.That(snapshot.IsCoreDestroyed, Is.True);
            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                snapshot,
                GrayboxDefenseSelectionKind3D.None,
                stableId: null);
            Assert.That(hud.SummaryText.text, Does.Contain("城市核心失守"));
        }

        [Test]
        public void LocallyUnavailableTowerClearsTargetTracerAndRecovers()
        {
            GrayboxBuildingInstance3D turret = CompletedTurret(
                "building.instance.observability-unavailable");
            GrayboxDefenseRuntime3D runtime = Runtime();
            var inventory = new ResourceInventory(500);
            inventory.Add(ResourceIds.Ammunition, 40);
            using var storage = new CityResourceStorageModel(inventory, 150);
            Synchronize(runtime, turret, CityMode.Fortress);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: true), Is.True);
            runtime.Tick(20f, globallyPaused: false, cityStorage: storage);
            Assert.That(runtime.TrySetPlayerPaused(
                turret.StableInstanceId,
                paused: false), Is.True);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);

            GrayboxDefenseRuntimeSnapshot3D firing = runtime.Snapshot;
            Assert.That(firing.Towers.Single().CanRunLocally, Is.True);
            Assert.That(
                firing.Towers.Single().Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(firing.Towers.Single().TargetId, Is.Not.Null);
            GrayboxDefenseWorldView3D worldView = CreateWorldView();
            worldView.Apply(firing, new[] { turret });
            Assert.That(worldView.VisibleTracerCount, Is.EqualTo(1));

            SetEvacuationLocked(turret, locked: true);
            Synchronize(runtime, turret, CityMode.Fortress);
            GrayboxDefenseRuntimeSnapshot3D unavailable = runtime.Snapshot;
            GrayboxDefenseTowerSnapshot3D unavailableTower =
                unavailable.Towers.Single();
            Assert.That(unavailableTower.CanRunLocally, Is.False);
            Assert.That(unavailableTower.TargetId, Is.Null);
            Assert.That(
                unavailableTower.Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Unavailable));
            worldView.Apply(unavailable, new[] { turret });
            Assert.That(worldView.VisibleTracerCount, Is.Zero);

            GrayboxDefenseHudView3D hud = CreateHud();
            hud.Apply(
                unavailable,
                GrayboxDefenseSelectionKind3D.Tower,
                turret.StableInstanceId);
            Assert.That(hud.SelectionText.text, Does.Contain("建筑未运行"));

            SetEvacuationLocked(turret, locked: false);
            Synchronize(runtime, turret, CityMode.Fortress);
            runtime.Tick(.1f, globallyPaused: false, cityStorage: storage);
            GrayboxDefenseRuntimeSnapshot3D recovered = runtime.Snapshot;
            Assert.That(recovered.Towers.Single().CanRunLocally, Is.True);
            Assert.That(
                recovered.Towers.Single().Status,
                Is.EqualTo(GrayboxDefenseTowerStatus3D.Firing));
            Assert.That(recovered.Towers.Single().TargetId, Is.Not.Null);
            worldView.Apply(recovered, new[] { turret });
            Assert.That(worldView.VisibleTracerCount, Is.EqualTo(1));
        }

        private GrayboxDefenseHudView3D CreateHud()
        {
            GameObject root = Track(new GameObject("DefenseObservabilityHud"));
            return root.AddComponent<GrayboxDefenseHudView3D>();
        }

        private GrayboxDefenseWorldView3D CreateWorldView()
        {
            GameObject root = Track(new GameObject(
                "DefenseObservabilityWorld"));
            Transform enemies = NewChild(root.transform, "Enemies");
            Transform towers = NewChild(root.transform, "Towers");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            GrayboxDefenseWorldView3D view =
                root.AddComponent<GrayboxDefenseWorldView3D>();
            view.Configure(
                enemies,
                towers,
                material,
                new PlanarCoordinateMapper3D(32, 24));
            return view;
        }

        private static GrayboxDefenseRuntime3D Runtime()
        {
            return new GrayboxDefenseRuntime3D(
                coreX: 0f,
                coreZ: 0f,
                spawnX: 9f,
                spawnZ: 0f);
        }

        private static GrayboxDefenseRuntimeSnapshot3D CampaignSnapshot(
            int waveNumber,
            SingleCityDefenseCampaignPhase phase,
            float warningSeconds,
            int planned,
            int spawned,
            int alive,
            int coreHealth)
        {
            CampaignWaveDefinition wave = waveNumber > 0
                ? CampaignWaveCatalog.All[waveNumber - 1]
                : null;
            return new GrayboxDefenseRuntimeSnapshot3D(
                waveNumber > 0 ? 1 : 0,
                waveNumber,
                CampaignWaveCatalog.All.Count,
                phase,
                ToLegacyPhase(phase),
                warningSeconds,
                planned,
                spawned,
                alive,
                Math.Max(0, spawned - alive),
                2000,
                coreHealth,
                Array.Empty<GrayboxDefenseTowerSnapshot3D>(),
                Array.Empty<GrayboxDefenseEnemySnapshot3D>(),
                spawnDirections: wave?.Directions,
                waveComposition: wave?.Entries);
        }

        private static GrayboxDefenseEnemySnapshot3D EnemySnapshot(
            string definitionId,
            int maximumHealth,
            int currentHealth)
        {
            ConstructorInfo constructor =
                typeof(GrayboxDefenseEnemySnapshot3D).GetConstructor(
                    new[]
                    {
                        typeof(string),
                        typeof(string),
                        typeof(int),
                        typeof(float),
                        typeof(float),
                        typeof(int),
                        typeof(int),
                        typeof(string),
                        typeof(float),
                        typeof(bool),
                    });
            Assert.That(constructor, Is.Not.Null,
                "Enemy HUD truth requires definition ID and maximum health " +
                "on the immutable runtime snapshot.");
            return (GrayboxDefenseEnemySnapshot3D)constructor.Invoke(
                new object[]
                {
                    "enemy.observability.crystal-beast.0001",
                    definitionId,
                    0,
                    4f,
                    2f,
                    currentHealth,
                    maximumHealth,
                    "城市核心",
                    4.5f,
                    false,
                });
        }

        private static WavePhase ToLegacyPhase(
            SingleCityDefenseCampaignPhase phase)
        {
            switch (phase)
            {
                case SingleCityDefenseCampaignPhase.Warning:
                    return WavePhase.Warning;
                case SingleCityDefenseCampaignPhase.SpawningAndCombat:
                    return WavePhase.Spawning;
                case SingleCityDefenseCampaignPhase.CombatCleanup:
                    return WavePhase.Active;
                default:
                    return WavePhase.Idle;
            }
        }

        private static CityResourceStorageModel EmptyStorage()
        {
            return new CityResourceStorageModel(
                new ResourceInventory(500),
                coreCapacityPerResource: 150);
        }

        private static void Synchronize(
            GrayboxDefenseRuntime3D runtime,
            GrayboxBuildingInstance3D turret,
            CityMode cityMode)
        {
            runtime.Synchronize(
                new[] { turret },
                cityMode,
                cityX: 0,
                cityY: 0,
                BuildingRangeRules.InitialGroundRadius);
        }

        private static GrayboxBuildingInstance3D CompletedTurret(
            string stableId)
        {
            return CompletedBuilding(
                stableId,
                BuildingCatalog.MachineGunTurret);
        }

        private static GrayboxBuildingInstance3D CompletedBuilding(
            string stableId,
            BuildingDefinition definition)
        {
            GrayboxBuildingInstance3D instance = NewBuilding(
                stableId,
                definition);
            MethodInfo complete = typeof(GrayboxBuildingInstance3D)
                .GetMethod(
                    "Complete",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(complete, Is.Not.Null);
            complete.Invoke(instance, null);
            return instance;
        }

        private static GrayboxBuildingInstance3D NewBuilding(
            string stableId,
            BuildingDefinition definition)
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
                    new PlacedBuilding(
                        definition,
                        0,
                        0,
                        BuildingSite.Ground,
                        BuildingOrientation.North),
                    new ConstructionProgress(
                        definition.BuildSeconds),
                    ResourceNodeBinding.None,
                });
            return instance;
        }

        private static ProductionObservabilitySnapshot CaptureProduction(
            BuildingProductionState state)
        {
            MethodInfo capture = typeof(ProductionObservabilitySnapshot)
                .GetMethod(
                    "Capture",
                    BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(capture, Is.Not.Null);
            return (ProductionObservabilitySnapshot)capture.Invoke(
                null,
                new object[] { 1UL, new[] { state }, null, 0 });
        }

        private static void InvokeNonPublic(
            object target,
            string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static void SetPrivateAutoProperty<T>(
            object target,
            string propertyName,
            T value)
        {
            FieldInfo field = target.GetType().GetField(
                "<" + propertyName + ">k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static GrayboxCombatDestructionResult3D
            CreateDestructionResult(
                string stableId,
                string definitionId,
                IReadOnlyList<ResourceAmount> productionLosses)
        {
            ConstructorInfo constructor =
                typeof(GrayboxCombatDestructionResult3D).GetConstructor(
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[]
                    {
                        typeof(GrayboxCombatDestructionStatus3D),
                        typeof(string),
                        typeof(string),
                        typeof(IReadOnlyList<ResourceAmount>),
                        typeof(IReadOnlyList<ResourceAmount>),
                        typeof(IReadOnlyList<ResourceAmount>),
                        typeof(bool),
                        typeof(bool),
                        typeof(bool),
                    },
                    null);
            Assert.That(constructor, Is.Not.Null);
            return (GrayboxCombatDestructionResult3D)constructor.Invoke(
                new object[]
                {
                    GrayboxCombatDestructionStatus3D.Committed,
                    stableId,
                    definitionId,
                    productionLosses,
                    Array.Empty<ResourceAmount>(),
                    Array.Empty<ResourceAmount>(),
                    true,
                    true,
                    false,
                });
        }

        private static T PrivateField<T>(object target, string fieldName)
            where T : class
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target) as T;
        }

        private static void SetEvacuationLocked(
            GrayboxBuildingInstance3D instance,
            bool locked)
        {
            MethodInfo method = typeof(GrayboxBuildingInstance3D).GetMethod(
                "SetEvacuationLocked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(instance, new object[] { locked });
        }

        private static float Distance(
            float x,
            float z,
            float targetX,
            float targetZ)
        {
            float offsetX = x - targetX;
            float offsetZ = z - targetZ;
            return Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
        }

        private static string FormatDistance(float distance)
        {
            return distance.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }
    }
}
