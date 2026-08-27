using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxCivilizationAdvancementPresentationTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void IDEA0020_EligiblePreviewShowsFourRequirementsAndLv2Reward()
        {
            Fixture fixture = Create();

            Assert.That(fixture.Presenter.RefreshIfChanged(), Is.True);
            Assert.That(fixture.View.IsPromptVisible, Is.True);
            Assert.That(fixture.View.HintText.text, Does.Contain("U"));
            Assert.That(fixture.Presenter.TryOpen(), Is.True);
            Assert.That(fixture.View.IsOpen, Is.True);
            Assert.That(fixture.View.RequirementsText.text,
                Does.Contain("遗产解析")
                    .And.Contain("机枪塔防线")
                    .And.Contain("晶壳母体")
                    .And.Contain("生产运行"));
            Assert.That(fixture.View.SummaryText.text,
                Does.Contain("文明 Lv.1 → Lv.2").And.Contain("关注度 +25"));
            Assert.That(fixture.View.FatePreviewText.text,
                Does.Contain("回溯锚点").And.Contain("可保留2个锚点"));
            Assert.That(fixture.View.AdvanceButton.interactable, Is.True);
            Assert.That(fixture.View.ContinueButton.gameObject.activeSelf,
                Is.False);
        }

        [Test]
        public void IDEA0020_ViewButtonsPublishOnlyAndSequenceStagesAreVisible()
        {
            Fixture fixture = Create();
            int advances = 0;
            int continues = 0;
            fixture.Presenter.AdvanceRequested += () => advances++;
            fixture.Presenter.ContinueRequested += () => continues++;
            fixture.Presenter.RefreshIfChanged();
            fixture.Presenter.TryOpen();

            fixture.View.AdvanceButton.onClick.Invoke();
            Assert.That(advances, Is.EqualTo(1));
            Assert.That(fixture.Civilization.Capture().CivilizationLevel,
                Is.EqualTo(1), "Presentation must not mutate domain owners.");

            Assert.That(fixture.Sequence.Start(), Is.True);
            AssertStage(fixture, AdvancementSequenceStage.Scanning, "扫描");
            fixture.Sequence.Tick(2.5f);
            AssertStage(fixture, AdvancementSequenceStage.Confirmed, "确认");
            fixture.Sequence.Tick(3f);
            AssertStage(fixture, AdvancementSequenceStage.Warning, "警告");
            fixture.Sequence.Tick(4f);
            AssertStage(fixture, AdvancementSequenceStage.Results, "完成");
            Assert.That(fixture.View.ContinueButton.gameObject.activeSelf,
                Is.True);
            fixture.View.ContinueButton.onClick.Invoke();
            Assert.That(continues, Is.EqualTo(1));
            Assert.That(fixture.Sequence.Stage,
                Is.EqualTo(AdvancementSequenceStage.Results),
                "Presentation only publishes ContinueRequested.");
        }

        [Test]
        public void IDEA0020_SequenceCaptureIsStableAndPresentationDoesNotOpenEarly()
        {
            AdvancementSequenceModel sequence = new AdvancementSequenceModel();
            AdvancementSequenceSnapshot stable = sequence.Capture();
            Assert.That(sequence.Capture(), Is.SameAs(stable));

            Fixture fixture = Create(productionRunning: false);
            fixture.Presenter.RefreshIfChanged();
            Assert.That(fixture.View.IsPromptVisible, Is.False);
            Assert.That(fixture.Presenter.TryOpen(), Is.False);
            Assert.That(fixture.View.IsOpen, Is.False);
        }

        [Test]
        public void IDEA0020_StaticRefreshIsFalseAndAllocationFree()
        {
            Fixture fixture = Create();
            Assert.That(fixture.Presenter.RefreshIfChanged(), Is.True);
            Assert.That(fixture.Presenter.RefreshIfChanged(), Is.False);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool unchanged = true;
            for (var index = 0; index < 300; index++)
                unchanged &= !fixture.Presenter.RefreshIfChanged();
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(unchanged, Is.True);
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void IDEA0020_PresenterUsesDomainPreparationAndRewardProjection()
        {
            string source = System.IO.File.ReadAllText(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "_Game/Scripts/Graybox3D/Building/" +
                    "GrayboxCivilizationAdvancementPresentationController3D.cs"));

            StringAssert.Contains(
                "civilization.CanPrepareAscension(requirements)",
                source);
            StringAssert.Contains(
                "civilization.TargetCivilizationLevel",
                source);
            StringAssert.Contains("civilization.TargetFateLevel", source);
            StringAssert.Contains("civilization.AttentionReward", source);
            StringAssert.DoesNotContain(
                "civilization.CivilizationLevel == 1",
                source);
            StringAssert.DoesNotContain("fate.HasSelection && fate.Level == 1",
                source);
            StringAssert.DoesNotContain("关注度 +25", source);
            StringAssert.DoesNotContain("关注度将增加 25", source);
        }

        private static void AssertStage(
            Fixture fixture,
            AdvancementSequenceStage expected,
            string chinese)
        {
            fixture.Presenter.RefreshIfChanged();
            Assert.That(fixture.Sequence.Capture().Stage, Is.EqualTo(expected));
            Assert.That(fixture.View.StageText.text, Does.Contain(chinese));
            Assert.That(fixture.View.IsOpen, Is.True);
            Assert.That(fixture.View.AdvanceButton.interactable, Is.False);
        }

        private Fixture Create(bool productionRunning = true)
        {
            root = new GameObject(
                "CivilizationAdvancement.Presentation.Test",
                typeof(RectTransform), typeof(Canvas));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var view = root.AddComponent<
                GrayboxCivilizationAdvancementView3D>();
            view.Configure(canvas);
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.RewindAnchorId,
                out _, out _, out string error), Is.True, error);
            var civilization = new FormalCivilizationAscensionRuntime(
                FormalFateCatalog.RewindAnchorId);
            var sequence = new AdvancementSequenceModel();
            var requirements = new FormalCivilizationAscensionRequirements(
                true, 2, true, productionRunning);
            var presenter = new
                GrayboxCivilizationAdvancementPresentationController3D(
                    civilization,
                    fate,
                    sequence,
                    view,
                    () => requirements);
            return new Fixture(
                view, presenter, civilization, sequence);
        }

        private sealed class Fixture
        {
            public Fixture(
                GrayboxCivilizationAdvancementView3D view,
                GrayboxCivilizationAdvancementPresentationController3D presenter,
                FormalCivilizationAscensionRuntime civilization,
                AdvancementSequenceModel sequence)
            {
                View = view;
                Presenter = presenter;
                Civilization = civilization;
                Sequence = sequence;
            }

            public GrayboxCivilizationAdvancementView3D View { get; }
            public GrayboxCivilizationAdvancementPresentationController3D
                Presenter { get; }
            public FormalCivilizationAscensionRuntime Civilization { get; }
            public AdvancementSequenceModel Sequence { get; }
        }
    }
}
