using System;
using System.Reflection;
using NUnit.Framework;
using WasteCity.Graybox3D.Building;
using WasteCity.Graybox3D.Exploration;
using WasteCity.Graybox3D.Usability;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.World.CivilizationExpansion;

namespace WasteCity.Tests
{
    public sealed class IDEA0029ExplorationUiProjectionTests
    {
        [Test]
        public void FormalExplorationViewExposesProjectionAndCommandContract()
        {
            Assembly assembly = typeof(GrayboxExplorationView3D).Assembly;
            Type view = assembly.GetType(
                "WasteCity.Graybox3D.Exploration.GrayboxExplorationView3D");
            Type presentation = assembly.GetType(
                "WasteCity.Graybox3D.Exploration." +
                "GrayboxExplorationPresentation3D");

            Assert.That(view, Is.Not.Null);
            Assert.That(presentation, Is.Not.Null);
            Assert.That(view.GetMethod("Apply", new[] { presentation }),
                Is.Not.Null);
            Assert.That(view.GetEvent("LeaderControlToggleRequested"),
                Is.Not.Null);
            Assert.That(view.GetEvent("ManualGatherToggleRequested"),
                Is.Not.Null);
            Assert.That(view.GetEvent("CenJinRescueToggleRequested"),
                Is.Not.Null);
            Assert.That(view.GetEvent("OutpostAlertFocusRequested"),
                Is.Not.Null);
        }

        [Test]
        public void FormalInputCoordinatorAcceptsExplorationViewAndCommands()
        {
            Assert.That(
                typeof(GrayboxUsabilityInputCoordinator3D).GetMethod(
                    "ConfigureExploration",
                    new[]
                    {
                        typeof(GrayboxExplorationView3D),
                        typeof(GrayboxFormalSaveRuntimeHost3D),
                    }),
                Is.Not.Null);
            Assert.That(
                typeof(GrayboxFormalSaveRuntimeHost3D).GetMethod(
                    "CaptureExplorationPresentation"),
                Is.Not.Null);
        }

        [Test]
        public void DisabledActionsExposeChineseReasonWithoutChangingLabel()
        {
            var blocked = new GrayboxExplorationActionPresentation3D(
                "接管领袖",
                false,
                "岑烬尚未招募");
            var enabled = new GrayboxExplorationActionPresentation3D(
                "交还 AI",
                true,
                "不应显示");

            Assert.That(blocked.Label, Is.EqualTo("接管领袖"));
            Assert.That(blocked.DisplayText,
                Is.EqualTo("接管领袖\n不可用：岑烬尚未招募"));
            Assert.That(enabled.DisplayText, Is.EqualTo("交还 AI"));
            Assert.That(enabled.DisabledReason, Is.Empty);
        }

        [Test]
        public void LeaderProjectionClampsProgressAndProvidesReadableFallbacks()
        {
            var projection = new GrayboxExplorationPresentation3D(
                null,
                null,
                null,
                null,
                1.5f,
                null,
                "背包已满",
                null,
                null,
                null,
                null);

            Assert.That(projection.LeaderName, Is.EqualTo("尚未招募"));
            Assert.That(projection.StatusSummary,
                Is.EqualTo("暂无领袖状态"));
            Assert.That(projection.ControlModeText, Is.EqualTo("AI 控制"));
            Assert.That(projection.ManualGatherTargetText,
                Is.EqualTo("未选择资源节点"));
            Assert.That(projection.ManualGatherProgress, Is.EqualTo(1f));
            Assert.That(projection.ManualGatherFailureReason,
                Is.EqualTo("背包已满"));
            Assert.That(projection.ControlAction.Enabled, Is.False);
            Assert.That(projection.GatherAction.DisplayText,
                Does.Contain("不可用："));
            Assert.That(projection.RescueAction.DisplayText,
                Does.Contain("尚未发现求救信号"));
        }

        [Test]
        public void ScanFeedbackIsExplicitlyNonModalAndRequiresText()
        {
            var visible = new GrayboxExplorationScanFeedback3D(
                true,
                "东部矿区扫描完成 · 发现铁矿 2 处",
                false);
            var empty = new GrayboxExplorationScanFeedback3D(
                true,
                " ",
                true);

            Assert.That(visible.Visible, Is.True);
            Assert.That(visible.BlocksWorldInput, Is.False);
            Assert.That(visible.Summary, Does.Contain("扫描完成"));
            Assert.That(empty.Visible, Is.False);
        }

        [Test]
        public void CompletedScanFeedbackShowsRevealAndAttentionReward()
        {
            GrayboxExplorationScanFeedback3D feedback =
                GrayboxExplorationScanFeedback3D.ForCompletedScan(
                    "安全矿区",
                    "发现铁矿矿点",
                    23,
                    2);

            Assert.That(feedback.Visible, Is.True);
            Assert.That(feedback.Warning, Is.False);
            Assert.That(feedback.Summary,
                Is.EqualTo("自动扫描完成：安全矿区 · 发现铁矿矿点 " +
                    "· 新增情报格 23 · 关注度 +2"));
        }

        [Test]
        public void LeaderProjectionShowsFormalVisionRangesWithoutHardcodedUiValues()
        {
            var projection = new GrayboxExplorationPresentation3D(
                "岑烬",
                "正常",
                "AI 控制",
                null,
                0f,
                null,
                null,
                null,
                null,
                null,
                null,
                visionRangeText: "主城 7 · 次城 5 · 领袖 4 · 前哨 3 · 侦察无人机 6");

            Assert.That(projection.VisionRangeText,
                Is.EqualTo("主城 7 · 次城 5 · 领袖 4 · 前哨 3 · 侦察无人机 6"));
        }

        [Test]
        public void OutpostAlertProjectionKeepsStableFocusIdentity()
        {
            var alert = new GrayboxExplorationOutpostAlertPresentation3D(
                true,
                "attack.000042",
                "北部前哨遭到啮噬者攻击",
                "受袭");
            var invalid = new GrayboxExplorationOutpostAlertPresentation3D(
                true,
                string.Empty,
                "无稳定目标",
                "警戒");

            Assert.That(alert.CanFocus, Is.True);
            Assert.That(alert.StableAlertId, Is.EqualTo("attack.000042"));
            Assert.That(alert.DisplayText,
                Is.EqualTo("前哨受袭 · 北部前哨遭到啮噬者攻击"));
            Assert.That(invalid.Visible, Is.False);
            Assert.That(invalid.CanFocus, Is.False);
        }

        [Test]
        public void ExplorationVisualKeysReuseFormalProduction2DCatalogIds()
        {
            Assert.That(GrayboxExplorationView3D.CenJinCharacterVisualId,
                Is.EqualTo(CharacterCatalog.CenJinId));
            Assert.That(GrayboxExplorationView3D.FollowStatusVisualId,
                Is.EqualTo("core.ui.status.follow"));
            Assert.That(GrayboxExplorationView3D.RescueStatusVisualId,
                Is.EqualTo("core.ui.status.rescue"));
            Assert.That(GrayboxExplorationView3D.OutpostMarkerVisualId,
                Is.EqualTo("core.world-marker.outpost"));
        }

        [Test]
        public void OutpostWorldCardShowsCommunicationSupplyAndMaintenance()
        {
            var outpost = new SettlementRuntimeSnapshot(
                "core.settlement.outpost",
                SettlementKind.Outpost,
                4,
                5,
                SettlementAutonomyTemplate.Industrial,
                0,
                0,
                70,
                false,
                true,
                false,
                0f,
                Array.Empty<WasteCity.Economy.ResourceAmount>());

            string text = GrayboxCivilizationExpansionController3D
                .FormatSettlementStatus(outpost);

            Assert.That(text,
                Does.Contain("通信：中断")
                    .And.Contain("补给：正常")
                    .And.Contain("维护：中断")
                    .And.Contain("总状态：失联"));
        }
    }
}
