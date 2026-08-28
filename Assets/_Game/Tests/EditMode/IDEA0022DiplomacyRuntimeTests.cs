using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Tests.EditMode
{
    public sealed class IDEA0022DiplomacyRuntimeTests
    {
        [Test]
        public void CatalogInitialRelationsAndFiveRelationshipStatesMatchSpec()
        {
            Assert.That(ExternalFactionCatalog.All.Count, Is.EqualTo(2));
            Assert.That(ExternalFactionCatalog.AshCaravan.InitialRelation,
                Is.EqualTo(10));
            Assert.That(ExternalFactionCatalog.CrystalAccord.InitialRelation,
                Is.EqualTo(-5));

            var runtime = new DiplomacyRuntime("test.session.relationships");
            string ash = ExternalFactionCatalog.AshCaravan.Id.Value;
            Assert.That(runtime.GetFaction(ash).State,
                Is.EqualTo(DiplomacyRelationshipState.Unknown));
            runtime.EstablishContact(ash, out _);
            Assert.That(runtime.GetFaction(ash).State,
                Is.EqualTo(DiplomacyRelationshipState.Contacted));
            runtime.AdjustRelation(ash, 30, out _);
            Assert.That(runtime.TrySignTradeAgreement(ash, out string error),
                Is.True, error);
            Assert.That(runtime.GetFaction(ash).State,
                Is.EqualTo(DiplomacyRelationshipState.TradeAgreement));
            runtime.AdjustRelation(ash, 30, out _);
            Assert.That(runtime.TrySignDefensePact(ash, out error), Is.True, error);
            Assert.That(runtime.GetFaction(ash).State,
                Is.EqualTo(DiplomacyRelationshipState.DefensePact));
            runtime.AdjustRelation(ash, -111, out _);
            Assert.That(runtime.GetFaction(ash).State,
                Is.EqualTo(DiplomacyRelationshipState.Hostile));
            Assert.That(runtime.GetFaction(ash).Relation, Is.EqualTo(-41));
        }

        [Test]
        public void DeterministicSixtySecondOffersCoverThreeFormalTransactions()
        {
            var runtime = new DiplomacyRuntime("test.session.three-offers");
            string factionId = ExternalFactionCatalog.AshCaravan.Id.Value;
            runtime.EstablishContact(factionId, out _);
            var offers = new List<DiplomacyOfferSnapshot>();
            for (var index = 0; index < 3; index++)
            {
                Assert.That(runtime.TryRefreshOffer(
                    factionId,
                    out DiplomacyOfferSnapshot offer,
                    out string error), Is.True, error);
                offers.Add(offer);
                Assert.That(offer.RemainingSeconds,
                    Is.EqualTo(DiplomacyRuntime.OfferRefreshSeconds));
                Assert.That(runtime.TryRefreshOffer(factionId, out _, out _),
                    Is.False);
                runtime.Tick(60f, false);
            }

            Assert.That(offers.Select(item => item.Kind).Distinct().Count(),
                Is.EqualTo(3));
            AssertOffer(
                offers.Single(item => item.Kind == DiplomacyOfferKind.AlloyForStone),
                ResourceIds.Alloy,
                10,
                ResourceIds.Stone,
                20);
            AssertOffer(
                offers.Single(item =>
                    item.Kind == DiplomacyOfferKind.BiomassForEnergyCrystal),
                ResourceIds.Biomass,
                12,
                ResourceIds.EnergyCrystal,
                8);
            AssertOffer(
                offers.Single(item =>
                    item.Kind == DiplomacyOfferKind.ConvoyInterceptionImmunity),
                ResourceIds.Ammunition,
                15,
                string.Empty,
                0);
        }

        [Test]
        public void AcceptSettlesAtomicallyAddsFiveAndImmunityIsConsumedOnce()
        {
            var runtime = new DiplomacyRuntime("test.session.immunity");
            var wallet = new TestWallet();
            string factionId = ExternalFactionCatalog.CrystalAccord.Id.Value;
            runtime.EstablishContact(factionId, out _);
            DiplomacyOfferSnapshot offer = FindOffer(
                runtime,
                factionId,
                DiplomacyOfferKind.ConvoyInterceptionImmunity);

            wallet.Set(offer.CostResourceId, offer.CostAmount - 1);
            Assert.That(runtime.TryAcceptOffer(factionId, wallet, out _, out _),
                Is.False);
            Assert.That(runtime.GetFaction(factionId).ActiveOffer, Is.Not.Null);

            wallet.Set(offer.CostResourceId, offer.CostAmount);
            int relationBefore = runtime.GetFaction(factionId).Relation;
            Assert.That(runtime.TryAcceptOffer(
                factionId,
                wallet,
                out DiplomacySettlement settlement,
                out string error), Is.True, error);
            Assert.That(runtime.GetFaction(factionId).Relation,
                Is.EqualTo(relationBefore + 5));
            Assert.That(settlement.ConvoyInterceptionImmunityDelta, Is.EqualTo(1));
            Assert.That(runtime.ConvoyInterceptionImmunityCharges, Is.EqualTo(1));
            Assert.That(runtime.TryConsumeConvoyInterceptionImmunity(), Is.True);
            Assert.That(runtime.TryConsumeConvoyInterceptionImmunity(), Is.False);
        }

        [Test]
        public void RejectChangesOnlyRelationByMinusOneAndPauseFreezesRefresh()
        {
            var runtime = new DiplomacyRuntime("test.session.reject");
            string factionId = ExternalFactionCatalog.AshCaravan.Id.Value;
            runtime.EstablishContact(factionId, out _);
            runtime.TryRefreshOffer(factionId, out _, out _);
            int relationBefore = runtime.GetFaction(factionId).Relation;
            float refreshBefore = runtime.GetFaction(factionId)
                .CooldownRemainingSeconds;
            runtime.Tick(30f, true);
            Assert.That(runtime.GetFaction(factionId).CooldownRemainingSeconds,
                Is.EqualTo(refreshBefore));
            Assert.That(runtime.TryRejectOffer(factionId, out string error),
                Is.True, error);
            Assert.That(runtime.GetFaction(factionId).Relation,
                Is.EqualTo(relationBefore - 1));
        }

        [Test]
        public void CaptureRestorePreservesOfferStatusCooldownAndImmunityAtomically()
        {
            var runtime = new DiplomacyRuntime("test.session.restore");
            var wallet = new TestWallet();
            string ash = ExternalFactionCatalog.AshCaravan.Id.Value;
            string crystal = ExternalFactionCatalog.CrystalAccord.Id.Value;
            runtime.EstablishContact(crystal, out _);
            DiplomacyOfferSnapshot immunity = FindOffer(
                runtime,
                crystal,
                DiplomacyOfferKind.ConvoyInterceptionImmunity);
            wallet.Set(immunity.CostResourceId, immunity.CostAmount);
            runtime.TryAcceptOffer(crystal, wallet, out _, out _);
            runtime.EstablishContact(ash, out _);
            runtime.TryRefreshOffer(ash, out DiplomacyOfferSnapshot ashOffer, out _);
            runtime.Tick(7f, false);
            DiplomacyRuntimeSnapshot saved = runtime.Capture();

            runtime.TryRejectOffer(ash, out _);
            runtime.TryConsumeConvoyInterceptionImmunity();
            Assert.That(runtime.TryRestore(saved, out string error), Is.True, error);
            Assert.That(runtime.GetFaction(ash).ActiveOffer.StableOfferId,
                Is.EqualTo(ashOffer.StableOfferId));
            Assert.That(runtime.GetFaction(ash).ActiveOffer.RemainingSeconds,
                Is.EqualTo(saved.Factions[0].ActiveOffer.RemainingSeconds));
            Assert.That(runtime.ConvoyInterceptionImmunityCharges,
                Is.EqualTo(saved.ConvoyInterceptionImmunityCharges));

            DiplomacyRuntimeSnapshot beforeInvalid = runtime.Capture();
            var invalidFaction = new DiplomacyFactionStateSnapshot(
                ash,
                DiplomacyRelationshipState.Contacted,
                101,
                0f,
                ashOffer);
            var invalid = new DiplomacyRuntimeSnapshot(
                saved.SessionId,
                saved.NextOfferOrdinal,
                new[] { invalidFaction },
                saved.ConvoyInterceptionImmunityCharges);
            Assert.That(runtime.TryRestore(invalid, out _), Is.False);
            AssertDiplomacySnapshotsEqual(beforeInvalid, runtime.Capture());
        }

        [Test]
        public void RestoredOfferCanStillSettleExactlyOnce()
        {
            var source = new DiplomacyRuntime("test.session.once");
            string ash = ExternalFactionCatalog.AshCaravan.Id.Value;
            source.EstablishContact(ash, out _);
            source.TryRefreshOffer(ash, out DiplomacyOfferSnapshot offer, out _);
            DiplomacyRuntimeSnapshot saved = source.Capture();
            var restored = new DiplomacyRuntime("test.session.once");
            Assert.That(restored.TryRestore(saved, out string error), Is.True, error);

            var wallet = new TestWallet();
            wallet.Set(offer.CostResourceId, offer.CostAmount);
            Assert.That(restored.TryAcceptOffer(ash, wallet, out _, out error),
                Is.True, error);
            Assert.That(restored.TryAcceptOffer(ash, wallet, out _, out _), Is.False);
        }

        private static DiplomacyOfferSnapshot FindOffer(
            DiplomacyRuntime runtime,
            string factionId,
            DiplomacyOfferKind kind)
        {
            for (var index = 0; index < 3; index++)
            {
                runtime.TryRefreshOffer(
                    factionId,
                    out DiplomacyOfferSnapshot offer,
                    out _);
                if (offer.Kind == kind) return offer;
                runtime.Tick(60f, false);
            }
            Assert.Fail("三次确定报价未覆盖目标类型");
            return null;
        }

        private static void AssertOffer(
            DiplomacyOfferSnapshot offer,
            string costResourceId,
            int costAmount,
            string rewardResourceId,
            int rewardAmount)
        {
            Assert.That(offer.CostResourceId, Is.EqualTo(costResourceId));
            Assert.That(offer.CostAmount, Is.EqualTo(costAmount));
            Assert.That(offer.RewardResourceId, Is.EqualTo(rewardResourceId));
            Assert.That(offer.RewardAmount, Is.EqualTo(rewardAmount));
        }

        private static void AssertDiplomacySnapshotsEqual(
            DiplomacyRuntimeSnapshot expected,
            DiplomacyRuntimeSnapshot actual)
        {
            Assert.That(actual.SessionId, Is.EqualTo(expected.SessionId));
            Assert.That(actual.NextOfferOrdinal,
                Is.EqualTo(expected.NextOfferOrdinal));
            Assert.That(actual.ConvoyInterceptionImmunityCharges,
                Is.EqualTo(expected.ConvoyInterceptionImmunityCharges));
            Assert.That(actual.Factions.Count, Is.EqualTo(expected.Factions.Count));
            for (var index = 0; index < actual.Factions.Count; index++)
            {
                Assert.That(actual.Factions[index].FactionId,
                    Is.EqualTo(expected.Factions[index].FactionId));
                Assert.That(actual.Factions[index].State,
                    Is.EqualTo(expected.Factions[index].State));
                Assert.That(actual.Factions[index].Relation,
                    Is.EqualTo(expected.Factions[index].Relation));
                Assert.That(actual.Factions[index].ActiveOffer?.StableOfferId,
                    Is.EqualTo(expected.Factions[index].ActiveOffer?.StableOfferId));
            }
        }

        private sealed class TestWallet : IDiplomacyResourceWallet
        {
            private readonly Dictionary<string, int> amounts =
                new Dictionary<string, int>(StringComparer.Ordinal);

            public void Set(string resourceId, int amount)
            {
                amounts[resourceId] = Math.Max(0, amount);
            }

            public int Amount(string resourceId)
            {
                return !string.IsNullOrEmpty(resourceId) &&
                    amounts.TryGetValue(resourceId, out int amount)
                        ? amount
                        : 0;
            }

            public bool TryExchange(
                string costResourceId,
                int costAmount,
                string rewardResourceId,
                int rewardAmount,
                out string error)
            {
                if (Amount(costResourceId) < costAmount)
                {
                    error = "资源不足";
                    return false;
                }
                amounts[costResourceId] = Amount(costResourceId) - costAmount;
                if (!string.IsNullOrEmpty(rewardResourceId) && rewardAmount > 0)
                {
                    amounts[rewardResourceId] =
                        Amount(rewardResourceId) + rewardAmount;
                }
                error = string.Empty;
                return true;
            }
        }
    }
}
