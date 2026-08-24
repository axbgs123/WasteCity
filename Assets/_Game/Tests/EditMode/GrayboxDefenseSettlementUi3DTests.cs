using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Economy;
using WasteCity.Graybox3D.Building;

namespace WasteCity.Tests
{
    public sealed class GrayboxDefenseSettlementUi3DTests
    {
        private readonly List<GameObject> cleanup =
            new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void EditModeOpenBuildsNoObjectsAndFormatsCompleteVictory()
        {
            Fixture fixture = CreateFixture();
            int childCount = fixture.Canvas.transform.childCount;

            Assert.That(fixture.Controller.Open(Snapshot(
                101ul,
                SingleCityDefenseCampaignResult.Victory,
                partial: true,
                modifierUsed: true,
                mobileDefense: true,
                eligibleSeconds: 100f)), Is.True);

            Assert.That(Application.isPlaying, Is.False);
            Assert.That(fixture.View.IsOpen, Is.True);
            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.View.BlocksInput, Is.True);
            Assert.That(fixture.View.BlockerRoot, Is.Null,
                "EditMode must not generate runtime UI objects.");
            Assert.That(fixture.Canvas.transform.childCount,
                Is.EqualTo(childCount));
            Assert.That(fixture.View.TitleText,
                Is.EqualTo("防御战役完成"));
            StringAssert.DoesNotContain("游戏通关", fixture.View.TitleText);
            AssertCompleteStatistics(fixture.View.StatisticsText);
            StringAssert.Contains("生产运转效率：25.0%",
                fixture.View.StatisticsText);
            StringAssert.Contains("机动防守", fixture.View.StatisticsText);
            StringAssert.Contains("迁移前统计不完整",
                fixture.View.StatisticsText);
            StringAssert.Contains("使用过开发修改器",
                fixture.View.StatisticsText);
            CollectionAssert.AreEqual(new[]
            {
                SingleCityDefenseSettlementAction.ContinueSandbox,
            }, fixture.View.VisibleActions);
        }

        [Test]
        public void DefeatShowsWaveRetryTitleActionsAndNoEfficiencyData()
        {
            Fixture fixture = CreateFixture();

            Assert.That(fixture.Controller.Open(Snapshot(
                202ul,
                SingleCityDefenseCampaignResult.Defeat,
                eligibleSeconds: 0f)), Is.True);

            Assert.That(fixture.View.TitleText,
                Is.EqualTo("城市核心失守"));
            StringAssert.DoesNotContain("游戏结束", fixture.View.TitleText);
            StringAssert.Contains("生产运转效率：无可用数据",
                fixture.View.StatisticsText);
            StringAssert.Contains("坚守防御", fixture.View.StatisticsText);
            CollectionAssert.AreEqual(new[]
            {
                SingleCityDefenseSettlementAction.RetryWaveCheckpoint,
                SingleCityDefenseSettlementAction.ReturnToTitle,
            }, fixture.View.VisibleActions);
        }

        [Test]
        public void SuccessfulVictoryCommandClosesAndReturnsChineseFeedback()
        {
            Fixture fixture = CreateFixture();
            fixture.Commands.NextResult =
                new GrayboxDefenseSettlementCommandResult3D(
                    true,
                    "已继续沙盒模式");
            fixture.Controller.Open(Snapshot(
                303ul,
                SingleCityDefenseCampaignResult.Victory));

            GrayboxDefenseSettlementCommandResult3D result =
                fixture.Controller.Execute(
                    SingleCityDefenseSettlementAction.ContinueSandbox);

            Assert.That(result.Success, Is.True);
            StringAssert.Contains("已", result.Message);
            Assert.That(fixture.Commands.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Commands.LastAction, Is.EqualTo(
                SingleCityDefenseSettlementAction.ContinueSandbox));
            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.View.BlocksInput, Is.False);
        }

        [Test]
        public void FailedRetryKeepsModalAndDisplaysChineseFeedback()
        {
            Fixture fixture = CreateFixture();
            fixture.Commands.NextResult =
                new GrayboxDefenseSettlementCommandResult3D(
                    false,
                    "没有可用的最近波前重试档");
            fixture.Controller.Open(Snapshot(
                404ul,
                SingleCityDefenseCampaignResult.Defeat));

            GrayboxDefenseSettlementCommandResult3D result =
                fixture.Controller.Execute(
                    SingleCityDefenseSettlementAction.RetryWaveCheckpoint);

            Assert.That(result.Success, Is.False);
            StringAssert.Contains("波前", result.Message);
            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.View.BlocksInput, Is.True);
            Assert.That(fixture.View.FeedbackText, Is.EqualTo(result.Message));
        }

        [Test]
        public void ExecuteRejectsReentryAndDisallowedActions()
        {
            Fixture fixture = CreateFixture();
            fixture.Commands.NextResult =
                new GrayboxDefenseSettlementCommandResult3D(
                    false,
                    "重试读取失败");
            fixture.Controller.Open(Snapshot(
                505ul,
                SingleCityDefenseCampaignResult.Defeat));
            GrayboxDefenseSettlementCommandResult3D nested = default;
            fixture.Commands.BeforeReturn = () => nested =
                fixture.Controller.Execute(
                    SingleCityDefenseSettlementAction.RetryWaveCheckpoint);

            GrayboxDefenseSettlementCommandResult3D outer =
                fixture.Controller.Execute(
                    SingleCityDefenseSettlementAction.RetryWaveCheckpoint);
            GrayboxDefenseSettlementCommandResult3D disallowed =
                fixture.Controller.Execute(
                    SingleCityDefenseSettlementAction.ContinueSandbox);

            Assert.That(outer.Success, Is.False);
            Assert.That(nested.Success, Is.False);
            StringAssert.Contains("正在执行", nested.Message);
            Assert.That(disallowed.Success, Is.False);
            StringAssert.Contains("不允许", disallowed.Message);
            Assert.That(fixture.Commands.CallCount, Is.EqualTo(1));
            Assert.That(fixture.Controller.IsOpen, Is.True);
        }

        [Test]
        public void SameRevisionIsIdempotentAndCloseReleasesActionListener()
        {
            Fixture fixture = CreateFixture();
            SingleCityDefenseSettlementSnapshot snapshot = Snapshot(
                606ul,
                SingleCityDefenseCampaignResult.Defeat);

            Assert.That(fixture.Controller.Open(snapshot), Is.True);
            Assert.That(fixture.Controller.Open(snapshot), Is.False);
            fixture.Controller.Close();
            fixture.View.RequestAction(
                SingleCityDefenseSettlementAction.ReturnToTitle);

            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.View.BlocksInput, Is.False);
            Assert.That(fixture.Commands.CallCount, Is.Zero,
                "Close must release the view action listener.");
            Assert.That(fixture.Controller.Open(snapshot), Is.False,
                "A closed terminal revision must not publish twice.");
            Assert.That(fixture.Controller.Open(Snapshot(
                607ul,
                SingleCityDefenseCampaignResult.Defeat)), Is.True);
        }

        [Test]
        public void SourceContractCreatesOneRuntimeBlockerUnderCanvas()
        {
            string source = File.ReadAllText(ProjectPath(
                "Assets/_Game/Scripts/Graybox3D/Building/" +
                "GrayboxDefenseSettlementView3D.cs"));

            StringAssert.Contains("Application.isPlaying", source);
            StringAssert.Contains("Defense.Settlement.Blocker", source);
            StringAssert.Contains("canvas.transform", source);
            StringAssert.Contains("anchorMin = Vector2.zero", source);
            StringAssert.Contains("anchorMax = Vector2.one", source);
            StringAssert.Contains("raycastTarget = true", source);
        }

        private Fixture CreateFixture()
        {
            var canvasRoot = new GameObject(
                "Settlement.Canvas",
                typeof(RectTransform),
                typeof(Canvas));
            cleanup.Add(canvasRoot);
            Canvas canvas = canvasRoot.GetComponent<Canvas>();

            var viewRoot = new GameObject("Settlement.View");
            cleanup.Add(viewRoot);
            var view = viewRoot.AddComponent<
                GrayboxDefenseSettlementView3D>();
            view.Configure(canvas);

            var controllerRoot = new GameObject("Settlement.Controller");
            cleanup.Add(controllerRoot);
            var controller = controllerRoot.AddComponent<
                GrayboxDefenseSettlementController3D>();
            var commands = new FakeCommands();
            controller.Configure(view, commands);
            return new Fixture(canvas, view, controller, commands);
        }

        private static void AssertCompleteStatistics(string text)
        {
            foreach (string expected in new[]
            {
                "战斗用时",
                "完成波次",
                "总击杀",
                "同屏敌人峰值",
                "建筑损失",
                "核心耐久",
                "完成生产批次",
                "啃噬者",
                "晶壳兽",
                "啸叫者",
                "机枪塔",
                "激光塔",
                "孢子塔",
                "弹药",
                "能晶",
                "生物武器",
            })
            {
                StringAssert.Contains(expected, text);
            }
        }

        private static SingleCityDefenseSettlementSnapshot Snapshot(
            ulong revision,
            SingleCityDefenseCampaignResult result,
            bool partial = false,
            bool modifierUsed = false,
            bool mobileDefense = false,
            float eligibleSeconds = 100f)
        {
            var statistics = new SingleCityDefenseCampaignStatisticsSnapshot(
                elapsedRuleSeconds: 125f,
                completedWaveCount: result ==
                    SingleCityDefenseCampaignResult.Victory ? 10 : 4,
                totalKillCount: 23,
                killsByEnemyId: new Dictionary<string, int>
                {
                    [EnemyCatalog.Gnawer.Id.Value] = 11,
                    [EnemyCatalog.CrystalBeast.Id.Value] = 5,
                    [EnemyCatalog.Howler.Id.Value] = 7,
                },
                damageByTowerBuildingId: new Dictionary<string, int>
                {
                    [BuildingCatalog.MachineGunTurret.Id.Value] = 20,
                    [BuildingCatalog.LaserTower.Id.Value] = 48,
                    [BuildingCatalog.SporeTower.Id.Value] = 18,
                },
                killsByTowerBuildingId: new Dictionary<string, int>
                {
                    [BuildingCatalog.MachineGunTurret.Id.Value] = 4,
                    [BuildingCatalog.LaserTower.Id.Value] = 5,
                    [BuildingCatalog.SporeTower.Id.Value] = 3,
                },
                consumablesSpentByResourceId:
                    new Dictionary<string, int>
                    {
                        [ResourceIds.Ammunition] = 4,
                        [ResourceIds.EnergyCrystal] = 3,
                        [ResourceIds.BiologicalWeapon] = 2,
                    },
                buildingLossCount: 2,
                coreCurrentHealth: result ==
                    SingleCityDefenseCampaignResult.Defeat ? 0 : 1500,
                coreMaximumHealth: 2000,
                highestAliveEnemyCount: 17,
                partialFromMigration: partial);
            var campaign = new SingleCityDefenseCampaignSnapshot(
                currentWaveNumber: result ==
                    SingleCityDefenseCampaignResult.Victory ? 10 : 5,
                phase: result == SingleCityDefenseCampaignResult.Victory
                    ? SingleCityDefenseCampaignPhase.Victory
                    : SingleCityDefenseCampaignPhase.Defeat,
                warningRemainingSeconds: 0f,
                plannedEnemyCount: 0,
                spawnedEnemyCount: 0,
                aliveEnemyCount: 0,
                coreCurrentHealth: statistics.CoreCurrentHealth,
                coreMaximumHealth: statistics.CoreMaximumHealth,
                result: result,
                enemies: null,
                statistics: statistics);
            var model = new SingleCityDefenseSettlementModel();
            Assert.That(model.TryPublish(
                revision,
                campaign,
                new SingleCityDefenseSettlementSessionStatistics(
                    completedProductionBatchCount: 12,
                    productionActiveProgressSeconds: 25f,
                    productionEligibleSeconds: eligibleSeconds,
                    cityWasPackedAfterCampaignStart: mobileDefense,
                    developerModifierUsed: modifierUsed),
                out SingleCityDefenseSettlementSnapshot snapshot), Is.True);
            return snapshot;
        }

        private static string ProjectPath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                relativePath));
        }

        private sealed class FakeCommands :
            IGrayboxDefenseSettlementCommands3D
        {
            public GrayboxDefenseSettlementCommandResult3D NextResult { get; set; }
                = new GrayboxDefenseSettlementCommandResult3D(
                    true,
                    "操作已完成");
            public Action BeforeReturn { get; set; }
            public int CallCount { get; private set; }
            public SingleCityDefenseSettlementAction LastAction { get; private set; }

            public GrayboxDefenseSettlementCommandResult3D Execute(
                SingleCityDefenseSettlementAction action)
            {
                CallCount++;
                LastAction = action;
                BeforeReturn?.Invoke();
                return NextResult;
            }
        }

        private readonly struct Fixture
        {
            public Fixture(
                Canvas canvas,
                GrayboxDefenseSettlementView3D view,
                GrayboxDefenseSettlementController3D controller,
                FakeCommands commands)
            {
                Canvas = canvas;
                View = view;
                Controller = controller;
                Commands = commands;
            }

            public Canvas Canvas { get; }
            public GrayboxDefenseSettlementView3D View { get; }
            public GrayboxDefenseSettlementController3D Controller { get; }
            public FakeCommands Commands { get; }
        }
    }
}
