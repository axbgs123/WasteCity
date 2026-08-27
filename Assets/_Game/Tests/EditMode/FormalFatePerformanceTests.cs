using System;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class FormalFatePerformanceTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void StaticAscensionPresenterRefreshesThreeHundredTimesAtZeroB()
        {
            root = new GameObject(
                "Fate.Performance.Presenter",
                typeof(RectTransform), typeof(Canvas));
            Canvas canvas = root.GetComponent<Canvas>();
            var view = root.AddComponent<
                GrayboxCivilizationAdvancementView3D>();
            view.Configure(canvas);
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.RewindAnchorId,
                out _, out _, out string error), Is.True, error);
            var requirements = new FormalCivilizationAscensionRequirements(
                true, 2, true, true);
            var presenter =
                new GrayboxCivilizationAdvancementPresentationController3D(
                    new FormalCivilizationAscensionRuntime(
                        FormalFateCatalog.RewindAnchorId),
                    fate,
                    new AdvancementSequenceModel(),
                    view,
                    () => requirements);
            Assert.That(presenter.RefreshIfChanged(), Is.True);
            Assert.That(presenter.RefreshIfChanged(), Is.False);

            bool unchanged = true;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 300; index++)
                unchanged &= !presenter.RefreshIfChanged();
            long allocated =
                GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(unchanged, Is.True);
            Assert.That(allocated, Is.Zero);
            presenter.Dispose();
        }

        [TestCase("core.legacy.pocket-universe")]
        [TestCase("core.legacy.void-debt")]
        [TestCase("core.legacy.rewind-anchor")]
        public void TwentyCaptureRestoreCyclesDoNotGrowFateState(
            string fateId)
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                fateId, out _, out _, out string error), Is.True, error);
            FormalFateSnapshot fateSnapshot = fate.Capture();
            var pocket = new PocketUniverseFateEffect();
            PocketUniverseFateSnapshot pocketSnapshot = pocket.Capture();
            var debt = new FormalVoidDebtRuntime();
            FormalVoidDebtSnapshot debtSnapshot = debt.Capture();
            var rewind = new FormalRewindAnchorMetadataRuntime();
            FormalRewindAnchorMetadataSnapshot rewindSnapshot =
                rewind.Capture();

            for (var index = 0; index < 20; index++)
            {
                Assert.That(fate.TryRestore(fateSnapshot, out error),
                    Is.True, error);
                Assert.That(pocket.TryRestore(pocketSnapshot, out error),
                    Is.True, error);
                Assert.That(debt.TryRestore(debtSnapshot, out error),
                    Is.True, error);
                Assert.That(rewind.TryRestore(rewindSnapshot, out error),
                    Is.True, error);
                fateSnapshot = fate.Capture();
                pocketSnapshot = pocket.Capture();
                debtSnapshot = debt.Capture();
                rewindSnapshot = rewind.Capture();
            }

            Assert.That(fateSnapshot.OfferedIds,
                Has.Count.EqualTo(FormalFateCatalog.FixedOffers.Count));
            Assert.That(fateSnapshot.SelectedId, Is.EqualTo(fateId));
            Assert.That(fateSnapshot.Level, Is.EqualTo(1));
            Assert.That(pocketSnapshot.Flagships, Is.Empty);
            Assert.That(pocketSnapshot.CollapsedFlagshipIds, Is.Empty);
            Assert.That(debtSnapshot.Debts, Is.Empty);
            Assert.That(rewindSnapshot.Entries, Is.Empty);
            Assert.That(rewindSnapshot.NextCreationOrdinal, Is.EqualTo(1L));
        }
    }
}
