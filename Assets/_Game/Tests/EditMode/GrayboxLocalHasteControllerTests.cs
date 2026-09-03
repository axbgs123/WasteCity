using NUnit.Framework;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxLocalHasteControllerTests
    {
        [Test]
        public void IDEA0028_SelectedDomainReceivesFiveTimesRuleDeltaOnly()
        {
            FormalFateRuntime fate = SelectedLocalHaste();
            var runtime = new LocalHasteRuntime();
            var controller = new GrayboxLocalHasteController3D(fate, runtime);

            Assert.That(controller.TrySelectDomain(
                GrayboxLocalHasteDomain3D.Production, out string error),
                Is.True, error);
            Assert.That(controller.TryStart(out error), Is.True, error);
            Assert.That(controller.MultiplierFor(
                GrayboxLocalHasteDomain3D.Production), Is.EqualTo(5f));
            Assert.That(controller.MultiplierFor(
                GrayboxLocalHasteDomain3D.Research), Is.EqualTo(1f));
            Assert.That(controller.MultiplierFor(
                GrayboxLocalHasteDomain3D.Defense), Is.EqualTo(1f));

            Assert.That(controller.Tick(
                2f, globallyPaused: false, targetEligible: true,
                out LocalHasteTickProjection projection,
                out error), Is.True, error);
            Assert.That(projection.ConsumedBudgetSeconds, Is.EqualTo(2f));
            Assert.That(projection.EffectiveRuleSeconds, Is.EqualTo(10f));
            Assert.That(runtime.Capture().RemainingBudgetSeconds,
                Is.EqualTo(58f));
        }

        [Test]
        public void IDEA0028_PauseFreezesBudgetAndLostTargetStopsEffect()
        {
            FormalFateRuntime fate = SelectedLocalHaste();
            var runtime = new LocalHasteRuntime();
            var controller = new GrayboxLocalHasteController3D(fate, runtime);
            Assert.That(controller.TrySelectDomain(
                GrayboxLocalHasteDomain3D.Research, out _), Is.True);
            Assert.That(controller.TryStart(out _), Is.True);

            Assert.That(controller.Tick(
                4f, globallyPaused: true, targetEligible: true,
                out _, out string error), Is.True, error);
            Assert.That(runtime.Capture().RemainingBudgetSeconds,
                Is.EqualTo(60f));
            Assert.That(controller.Tick(
                1f, globallyPaused: false, targetEligible: false,
                out _, out error), Is.True, error);
            Assert.That(runtime.Capture().Active, Is.False);
            Assert.That(runtime.Capture().RemainingBudgetSeconds,
                Is.EqualTo(60f));
        }

        [Test]
        public void IDEA0028_UnselectedFateCannotActivateHaste()
        {
            FormalFateRuntime fate = SelectedPocketUniverse();
            var controller = new GrayboxLocalHasteController3D(
                fate, new LocalHasteRuntime());

            Assert.That(controller.TrySelectDomain(
                GrayboxLocalHasteDomain3D.Production, out string error),
                Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(controller.MultiplierFor(
                GrayboxLocalHasteDomain3D.Production), Is.EqualTo(1f));
        }

        [TestCase(GrayboxLocalHasteDomain3D.None, true, true, true, false)]
        [TestCase(GrayboxLocalHasteDomain3D.Production, true, false, false,
            true)]
        [TestCase(GrayboxLocalHasteDomain3D.Production, false, true, true,
            false)]
        [TestCase(GrayboxLocalHasteDomain3D.Research, false, true, false,
            true)]
        [TestCase(GrayboxLocalHasteDomain3D.Research, true, false, true,
            false)]
        [TestCase(GrayboxLocalHasteDomain3D.Defense, false, false, true,
            true)]
        [TestCase(GrayboxLocalHasteDomain3D.Defense, true, true, false,
            false)]
        public void IDEA0028_TargetEligibilityUsesOnlyTheSelectedDomain(
            GrayboxLocalHasteDomain3D domain,
            bool productionEligible,
            bool researchEligible,
            bool defenseEligible,
            bool expected)
        {
            Assert.That(GrayboxLocalHasteController3D.IsTargetEligible(
                domain,
                productionEligible,
                researchEligible,
                defenseEligible), Is.EqualTo(expected));
        }

        private static FormalFateRuntime SelectedLocalHaste()
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TryRestore(
                new FormalFateSnapshot(0ul, new[]
                {
                    FormalFateCatalog.LocalHasteId,
                    FormalFateCatalog.PocketUniverseId,
                    FormalFateCatalog.VoidChestId,
                }, string.Empty, 0), out string restoreError),
                Is.True, restoreError);
            Assert.That(fate.TrySelect(
                FormalFateCatalog.LocalHasteId,
                out _, out _, out string selectError),
                Is.True, selectError);
            return fate;
        }

        private static FormalFateRuntime SelectedPocketUniverse()
        {
            var fate = new FormalFateRuntime();
            Assert.That(fate.TrySelect(
                FormalFateCatalog.PocketUniverseId,
                out _, out _, out string selectError),
                Is.True, selectError);
            return fate;
        }
    }
}
