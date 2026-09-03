using NUnit.Framework;
using WasteCity.Graybox3D.Building;
using WasteCity.Progression;

namespace WasteCity.Tests
{
    public sealed class GrayboxCivilizationAdvancementControllerTests
    {
        [TestCase("core.legacy.pocket-universe")]
        [TestCase("core.legacy.void-debt")]
        [TestCase("core.legacy.rewind-anchor")]
        [TestCase("core.legacy.quantum-entanglement")]
        [TestCase("core.legacy.spatial-template")]
        [TestCase("core.legacy.local-haste")]
        [TestCase("core.legacy.foresight-delay")]
        [TestCase("core.legacy.causal-transparency")]
        [TestCase("core.legacy.void-chest")]
        public void IDEA0028_AtomicCommandPromotesAllNineFatesAndAddsTwentyFive(
            string fateId)
        {
            Fixture fixture = Create(fateId);
            GrayboxCivilizationAdvancementResult3D result =
                fixture.Controller.Execute(new
                    FormalCivilizationAscensionRequirements(true, 2, true, true));
            Assert.That(result.Success, Is.True, result.Message);
            Assert.That(fixture.Ascension.Capture().CivilizationLevel, Is.EqualTo(2));
            Assert.That(fixture.Fate.Capture().Level, Is.EqualTo(2));
            Assert.That(fixture.Attention.Value, Is.EqualTo(35));
            Assert.That(result.CheckpointReasonId,
                Is.EqualTo("first-civilization-ascension"));
            Assert.That(result.RequestedSequenceStage,
                Is.EqualTo(AdvancementSequenceStage.Scanning));
            if (fateId == FormalFateCatalog.PocketUniverseId)
                Assert.That(fixture.Pocket.Level, Is.EqualTo(2));
            if (fateId == FormalFateCatalog.VoidDebtId)
                Assert.That(fixture.Debt.Level, Is.EqualTo(2));
            if (fateId == FormalFateCatalog.RewindAnchorId)
                Assert.That(fixture.Rewind.MaximumAnchors, Is.EqualTo(2));
        }

        [TestCase("core.legacy.quantum-entanglement")]
        [TestCase("core.legacy.spatial-template")]
        [TestCase("core.legacy.local-haste")]
        [TestCase("core.legacy.foresight-delay")]
        [TestCase("core.legacy.causal-transparency")]
        [TestCase("core.legacy.void-chest")]
        public void IDEA0028_NewFatesRollbackWhenAttentionCommitFails(
            string fateId)
        {
            Fixture fixture = Create(fateId);
            Assert.That(fixture.Attention.TryApply(
                "core.attention.fate.void-debt-periodic",
                "first-civilization-ascension", out _), Is.True);
            FormalAttentionSnapshot attention = fixture.Attention.Capture();
            GrayboxCivilizationAdvancementResult3D result =
                fixture.Controller.Execute(new
                    FormalCivilizationAscensionRequirements(
                        true, 2, true, true));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Code,
                Is.EqualTo(
                    GrayboxCivilizationAdvancementCode3D.OwnerCommitFailed));
            Assert.That(result.Message,
                Is.EqualTo("advancement.owner-commit-failed"));
            Assert.That(fixture.Fate.Capture().Level, Is.EqualTo(1));
            Assert.That(fixture.Ascension.Capture().CivilizationLevel,
                Is.EqualTo(1));
            Assert.That(fixture.Attention.Capture().Value,
                Is.EqualTo(attention.Value));
            Assert.That(fixture.Attention.Capture().Revision,
                Is.EqualTo(attention.Revision));
            Assert.That(fixture.Pocket.Level, Is.EqualTo(1));
            Assert.That(fixture.Debt.Level, Is.EqualTo(1));
            Assert.That(fixture.Rewind.MaximumAnchors, Is.EqualTo(1));
        }

        [Test]
        public void IDEA0020_AttentionFailureRollsBackEveryOwner()
        {
            Fixture fixture = Create(FormalFateCatalog.PocketUniverseId);
            Assert.That(fixture.Attention.TryApply(
                "core.attention.fate.void-debt-periodic",
                "first-civilization-ascension", out _), Is.True);
            FormalAttentionSnapshot attention = fixture.Attention.Capture();
            GrayboxCivilizationAdvancementResult3D result =
                fixture.Controller.Execute(new
                    FormalCivilizationAscensionRequirements(true, 2, true, true));
            Assert.That(result.Success, Is.False);
            Assert.That(fixture.Fate.Capture().Level, Is.EqualTo(1));
            Assert.That(fixture.Pocket.Level, Is.EqualTo(1));
            Assert.That(fixture.Ascension.Capture().CivilizationLevel, Is.EqualTo(1));
            Assert.That(fixture.Attention.Capture().Value,
                Is.EqualTo(attention.Value));
            Assert.That(fixture.Attention.Capture().Revision,
                Is.EqualTo(attention.Revision));
        }

        [Test]
        public void IDEA0020_RewindCapacityRollsBackWhenAttentionCommitFails()
        {
            Fixture fixture = Create(FormalFateCatalog.RewindAnchorId);
            Assert.That(fixture.Attention.TryApply(
                "core.attention.fate.void-debt-periodic",
                "first-civilization-ascension", out _), Is.True);
            GrayboxCivilizationAdvancementResult3D result =
                fixture.Controller.Execute(new
                    FormalCivilizationAscensionRequirements(true, 2, true, true));
            Assert.That(result.Success, Is.False);
            Assert.That(result.Message,
                Is.EqualTo("advancement.owner-commit-failed"));
            Assert.That(result.Diagnostic, Is.Not.Empty);
            Assert.That(fixture.Rewind.MaximumAnchors, Is.EqualTo(1));
            Assert.That(fixture.Fate.Capture().Level, Is.EqualTo(1));
        }

        private static Fixture Create(string fateId)
        {
            var fate = new FormalFateRuntime();
            if (!Contains(fate.Capture().OfferedIds, fateId))
            {
                Assert.That(fate.TryRestore(
                    new FormalFateSnapshot(
                        1ul,
                        new[]
                        {
                            fateId,
                            FormalFateCatalog.PocketUniverseId,
                            FormalFateCatalog.VoidDebtId,
                        },
                        null,
                        0,
                        1),
                    out string offerError), Is.True, offerError);
            }
            Assert.That(fate.TrySelect(fateId, out _, out _, out string error),
                Is.True, error);
            var ascension = new FormalCivilizationAscensionRuntime(fateId);
            var attention = new FormalAttentionRuntime();
            var pocket = new PocketUniverseFateEffect();
            var debt = new FormalVoidDebtRuntime();
            var rewind = new FormalRewindAnchorMetadataRuntime();
            return new Fixture(ascension, fate, attention, pocket, debt,
                rewind, new GrayboxCivilizationAdvancementController3D(
                    ascension, fate, attention, pocket, debt, rewind));
        }

        private static bool Contains(
            System.Collections.Generic.IReadOnlyList<string> values,
            string expected)
        {
            for (var index = 0; index < values.Count; index++)
                if (values[index] == expected) return true;
            return false;
        }

        private sealed class Fixture
        {
            public Fixture(FormalCivilizationAscensionRuntime ascension,
                FormalFateRuntime fate, FormalAttentionRuntime attention,
                PocketUniverseFateEffect pocket, FormalVoidDebtRuntime debt,
                FormalRewindAnchorMetadataRuntime rewind,
                GrayboxCivilizationAdvancementController3D controller)
            { Ascension=ascension;Fate=fate;Attention=attention;Pocket=pocket;Debt=debt;Rewind=rewind;Controller=controller; }
            public FormalCivilizationAscensionRuntime Ascension { get; }
            public FormalFateRuntime Fate { get; }
            public FormalAttentionRuntime Attention { get; }
            public PocketUniverseFateEffect Pocket { get; }
            public FormalVoidDebtRuntime Debt { get; }
            public FormalRewindAnchorMetadataRuntime Rewind { get; }
            public GrayboxCivilizationAdvancementController3D Controller { get; }
        }
    }
}
