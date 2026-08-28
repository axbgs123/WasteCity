using System.Collections.Generic;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Tests.EditMode
{
    public sealed class IDEA0022LeadershipPoliticsRuntimeTests
    {
        [Test]
        public void CatalogHasThreeInternalFactionsAndCandidateScoreUsesAllFactors()
        {
            Assert.That(InternalFactionCatalog.All.Count, Is.EqualTo(3));
            Assert.That(InternalFactionCatalog.EngineeringCouncil.DisplayName,
                Is.EqualTo("工程议会"));
            Assert.That(InternalFactionCatalog.GarrisonCorps.DisplayName,
                Is.EqualTo("守备团"));
            Assert.That(InternalFactionCatalog.MigrantMilitia.DisplayName,
                Is.EqualTo("迁徙民团"));

            LeadershipPoliticsRuntime politics = CreatePolitics(out _, out CharacterLifeRuntime linXi, out _);
            CandidateSupportProjection assigned = politics.EvaluateCandidateSupport(
                linXi.Definition.Id.Value);
            linXi.AssignSettlement(string.Empty);
            CandidateSupportProjection unassigned = politics.EvaluateCandidateSupport(
                linXi.Definition.Id.Value);
            linXi.AdjustLoyalty(-20);
            CandidateSupportProjection disloyal = politics.EvaluateCandidateSupport(
                linXi.Definition.Id.Value);

            Assert.That(assigned.AssignmentContribution, Is.GreaterThan(0f));
            Assert.That(assigned.Total, Is.GreaterThan(unassigned.Total));
            Assert.That(unassigned.Total, Is.GreaterThan(disloyal.Total));
            Assert.That(assigned.PrestigeContribution, Is.GreaterThan(0f));
            Assert.That(assigned.FactionContribution, Is.GreaterThan(0f));
            Assert.That(assigned.PrestigeContribution, Is.EqualTo(55f));
            Assert.That(assigned.LoyaltyContribution, Is.EqualTo(12.5f));
            Assert.That(assigned.FactionContribution, Is.EqualTo(22.5f));
            Assert.That(assigned.Total, Is.EqualTo(100f));
            Assert.That(LeadershipPoliticsRuntime.SupportedSuccessionThreshold,
                Is.EqualTo(60f));
        }

        [Test]
        public void LeaderDeathStartsCouncilAndSupportedSuccessorCommitsAtomically()
        {
            LeadershipPoliticsRuntime politics = CreatePolitics(
                out CharacterLifeRuntime cenJin,
                out CharacterLifeRuntime linXi,
                out _);
            Assert.That(politics.TryDesignateSuccessor(
                linXi.Definition.Id.Value,
                out string error), Is.True, error);
            Kill(cenJin);
            Assert.That(politics.TryHandleCurrentLeaderDeath(out error), Is.True, error);
            Assert.That(politics.IsInterimCouncilActive, Is.True);
            Assert.That(politics.EfficiencyMultiplier, Is.EqualTo(.75f));

            Assert.That(politics.TryChooseSuccessor(
                linXi.Definition.Id.Value,
                forceLowSupport: false,
                out SuccessionCommandResult result,
                out error), Is.True, error);
            Assert.That(result, Is.EqualTo(SuccessionCommandResult.Committed));
            Assert.That(politics.CurrentLeaderId, Is.EqualTo(linXi.Definition.Id.Value));
            Assert.That(politics.IsInterimCouncilActive, Is.False);
            Assert.That(politics.EfficiencyMultiplier, Is.EqualTo(1f));
            Assert.That(politics.Crisis, Is.Null);
        }

        [TestCase(CoupResolution.Concession)]
        [TestCase(CoupResolution.Suppression)]
        public void LowSupportForcedCandidateCreatesOneCoupAndBothResolutionsCommit(
            CoupResolution resolution)
        {
            LeadershipPoliticsRuntime politics = CreatePolitics(
                out CharacterLifeRuntime cenJin,
                out _,
                out CharacterLifeRuntime hanGu);
            foreach (InternalFactionDefinition faction in InternalFactionCatalog.All)
            {
                Assert.That(politics.TrySetCandidateSupport(
                    faction.Id.Value,
                    hanGu.Definition.Id.Value,
                    0,
                    out string supportError), Is.True, supportError);
            }
            hanGu.AdjustLoyalty(-60);
            Kill(cenJin);
            politics.TryHandleCurrentLeaderDeath(out _);

            Assert.That(politics.TryChooseSuccessor(
                hanGu.Definition.Id.Value,
                forceLowSupport: true,
                out SuccessionCommandResult result,
                out string error), Is.True, error);
            Assert.That(result, Is.EqualTo(SuccessionCommandResult.CoupCrisisStarted));
            Assert.That(politics.CurrentLeaderId, Is.EqualTo(cenJin.Definition.Id.Value));
            Assert.That(politics.Crisis, Is.Not.Null);

            var authority = new TestPoliticsAuthority();
            Assert.That(politics.TryResolveCoup(
                resolution,
                authority,
                CharacterCatalog.MainCityId,
                out CoupResolutionOutcome outcome,
                out error), Is.True, error);
            Assert.That(politics.CurrentLeaderId, Is.EqualTo(hanGu.Definition.Id.Value));
            Assert.That(politics.Crisis, Is.Null);
            Assert.That(politics.IsInterimCouncilActive, Is.False);
            if (resolution == CoupResolution.Concession)
            {
                Assert.That(authority.AlloySpent, Is.EqualTo(10));
                Assert.That(outcome.ResourceCostId, Is.EqualTo(ResourceIds.Alloy));
                Assert.That(politics.Factions[0].Influence, Is.EqualTo(55));
                Assert.That(politics.Factions[1].Influence, Is.EqualTo(45));
                Assert.That(politics.Factions[2].Influence, Is.EqualTo(20));
            }
            else
            {
                Assert.That(authority.SettlementLoyaltyDelta, Is.EqualTo(-20));
                Assert.That(outcome.SettlementId,
                    Is.EqualTo(CharacterCatalog.MainCityId));
                Assert.That(politics.Factions[0].Loyalty, Is.EqualTo(50));
                Assert.That(politics.Factions[1].Loyalty, Is.EqualTo(45));
                Assert.That(politics.Factions[2].Loyalty, Is.EqualTo(55));
            }
            Assert.That(politics.TryResolveCoup(
                resolution,
                authority,
                CharacterCatalog.MainCityId,
                out _,
                out _), Is.False);
        }

        [Test]
        public void CaptureRestorePreservesCouncilCoupAndFactionSupportAtomically()
        {
            LeadershipPoliticsRuntime politics = CreatePolitics(
                out CharacterLifeRuntime cenJin,
                out _,
                out CharacterLifeRuntime hanGu);
            foreach (InternalFactionDefinition faction in InternalFactionCatalog.All)
            {
                politics.TrySetCandidateSupport(
                    faction.Id.Value,
                    hanGu.Definition.Id.Value,
                    0,
                    out _);
            }
            hanGu.AdjustLoyalty(-60);
            Kill(cenJin);
            politics.TryHandleCurrentLeaderDeath(out _);
            politics.TryChooseSuccessor(
                hanGu.Definition.Id.Value,
                true,
                out _,
                out _);
            LeadershipPoliticsSnapshot saved = politics.Capture();

            politics.TryResolveCoup(
                CoupResolution.Suppression,
                new TestPoliticsAuthority(),
                CharacterCatalog.MainCityId,
                out _,
                out _);
            Assert.That(politics.TryRestore(saved, out string error), Is.True, error);
            Assert.That(politics.IsInterimCouncilActive, Is.True);
            Assert.That(politics.Crisis.CandidateId,
                Is.EqualTo(hanGu.Definition.Id.Value));
            Assert.That(politics.CurrentLeaderId,
                Is.EqualTo(cenJin.Definition.Id.Value));
            Assert.That(politics.EvaluateCandidateSupport(hanGu.Definition.Id.Value).Total,
                Is.EqualTo(saved.Crisis.Support).Within(.001f));

            LeadershipPoliticsSnapshot beforeInvalid = politics.Capture();
            var invalidFaction = new InternalFactionStateSnapshot(
                InternalFactionCatalog.EngineeringCouncil.Id.Value,
                101,
                50,
                new[]
                {
                    new FactionCandidateSupportSnapshot(
                        CharacterCatalog.HanGuId,
                        50),
                });
            var invalid = new LeadershipPoliticsSnapshot(
                saved.CurrentLeaderId,
                saved.DesignatedSuccessorId,
                saved.IsInterimCouncilActive,
                saved.Crisis,
                new[] { invalidFaction });
            Assert.That(politics.TryRestore(invalid, out _), Is.False);
            AssertPoliticsSnapshotsEqual(beforeInvalid, politics.Capture());
        }

        [Test]
        public void CaptureRestorePreservesCommittedSuccessorWithoutCouncil()
        {
            LeadershipPoliticsRuntime politics = CreatePolitics(
                out CharacterLifeRuntime cenJin,
                out CharacterLifeRuntime linXi,
                out _);
            politics.TryDesignateSuccessor(linXi.Definition.Id.Value, out _);
            Kill(cenJin);
            politics.TryHandleCurrentLeaderDeath(out _);
            politics.TryChooseSuccessor(
                linXi.Definition.Id.Value,
                false,
                out _,
                out _);
            LeadershipPoliticsSnapshot saved = politics.Capture();

            var fresh = new LeadershipPoliticsRuntime(
                politics.Characters,
                linXi.Definition.Id.Value);
            Assert.That(fresh.TryRestore(saved, out string error), Is.True, error);
            Assert.That(fresh.CurrentLeaderId, Is.EqualTo(CharacterCatalog.LinXiId));
            Assert.That(fresh.IsInterimCouncilActive, Is.False);
            Assert.That(fresh.Crisis, Is.Null);
        }

        [Test]
        public void FailedExternalCoupCostLeavesPoliticsUnchanged()
        {
            LeadershipPoliticsRuntime politics = CreatePolitics(
                out CharacterLifeRuntime cenJin,
                out _,
                out CharacterLifeRuntime hanGu);
            foreach (InternalFactionDefinition faction in InternalFactionCatalog.All)
            {
                politics.TrySetCandidateSupport(
                    faction.Id.Value,
                    hanGu.Definition.Id.Value,
                    0,
                    out _);
            }
            hanGu.AdjustLoyalty(-55);
            Kill(cenJin);
            politics.TryHandleCurrentLeaderDeath(out _);
            politics.TryChooseSuccessor(hanGu.Definition.Id.Value, true, out _, out _);
            LeadershipPoliticsSnapshot before = politics.Capture();

            var authority = new TestPoliticsAuthority { RejectCommands = true };
            Assert.That(politics.TryResolveCoup(
                CoupResolution.Concession,
                authority,
                CharacterCatalog.MainCityId,
                out _,
                out _), Is.False);
            AssertPoliticsSnapshotsEqual(before, politics.Capture());
        }

        private static LeadershipPoliticsRuntime CreatePolitics(
            out CharacterLifeRuntime cenJin,
            out CharacterLifeRuntime linXi,
            out CharacterLifeRuntime hanGu)
        {
            cenJin = new CharacterLifeRuntime(CharacterCatalog.CenJin);
            linXi = new CharacterLifeRuntime(CharacterCatalog.LinXi);
            hanGu = new CharacterLifeRuntime(CharacterCatalog.HanGu);
            return new LeadershipPoliticsRuntime(
                new List<CharacterLifeRuntime> { cenJin, linXi, hanGu },
                cenJin.Definition.Id.Value);
        }

        private static void Kill(CharacterLifeRuntime character)
        {
            character.TryApplyDamage(1000, "combat.enemy.hit", out _);
            character.Tick(60f, false, true, false);
        }

        private static void AssertPoliticsSnapshotsEqual(
            LeadershipPoliticsSnapshot expected,
            LeadershipPoliticsSnapshot actual)
        {
            Assert.That(actual.CurrentLeaderId, Is.EqualTo(expected.CurrentLeaderId));
            Assert.That(actual.DesignatedSuccessorId,
                Is.EqualTo(expected.DesignatedSuccessorId));
            Assert.That(actual.IsInterimCouncilActive,
                Is.EqualTo(expected.IsInterimCouncilActive));
            Assert.That(actual.Crisis?.CandidateId,
                Is.EqualTo(expected.Crisis?.CandidateId));
            Assert.That(actual.Factions.Count, Is.EqualTo(expected.Factions.Count));
            for (var index = 0; index < actual.Factions.Count; index++)
            {
                Assert.That(actual.Factions[index].FactionId,
                    Is.EqualTo(expected.Factions[index].FactionId));
                Assert.That(actual.Factions[index].Influence,
                    Is.EqualTo(expected.Factions[index].Influence));
                Assert.That(actual.Factions[index].Loyalty,
                    Is.EqualTo(expected.Factions[index].Loyalty));
            }
        }

        private sealed class TestPoliticsAuthority :
            ILeadershipPoliticsResolutionAuthority
        {
            public int AlloySpent { get; private set; }
            public int SettlementLoyaltyDelta { get; private set; }
            public bool RejectCommands { get; set; }

            public bool TrySpendResource(
                string resourceId,
                int amount,
                out string error)
            {
                if (RejectCommands)
                {
                    error = "原子权威拒绝";
                    return false;
                }
                if (resourceId != ResourceIds.Alloy)
                {
                    error = "错误资源";
                    return false;
                }
                AlloySpent += amount;
                error = string.Empty;
                return true;
            }

            public bool TryAdjustSettlementLoyalty(
                string settlementId,
                int delta,
                out string error)
            {
                if (RejectCommands)
                {
                    error = "原子权威拒绝";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(settlementId))
                {
                    error = "城市不存在";
                    return false;
                }
                SettlementLoyaltyDelta += delta;
                error = string.Empty;
                return true;
            }
        }
    }
}
