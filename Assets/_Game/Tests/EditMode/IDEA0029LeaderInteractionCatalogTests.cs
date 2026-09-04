using System.Linq;
using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Leader.CivilizationExpansion;
using WasteCity.Leader.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029LeaderInteractionCatalogTests
    {
        [Test]
        public void ManualGatherUsesApprovedEmergencyValuesAndBaseResources()
        {
            Assert.That(
                LeaderInteractionCatalog.ManualGatherMaximumDistance,
                Is.EqualTo(1.5f));
            Assert.That(
                LeaderInteractionCatalog.ManualGatherCycleSeconds,
                Is.EqualTo(6f));
            Assert.That(
                LeaderInteractionCatalog.ManualGatherAmount,
                Is.EqualTo(1));
            CollectionAssert.AreEqual(
                ResourceIds.Base,
                LeaderInteractionCatalog.ManualGatherResourceIds.ToArray());
            Assert.That(
                LeaderInteractionCatalog.IsManualGatherResource(
                    ResourceIds.Alloy),
                Is.False);
        }

        [Test]
        public void CenJinDistressUsesExistingCharacterAndAttentionIds()
        {
            Assert.That(
                LeaderInteractionCatalog.CenJinCharacterId,
                Is.EqualTo(CharacterCatalog.CenJinId));
            Assert.That(
                LeaderInteractionCatalog.CenJinDistressSiteId,
                Is.EqualTo("core.exploration.site.cen-jin-distress"));
            Assert.That(
                LeaderInteractionCatalog.CenJinAttentionReasonId,
                Is.EqualTo("core.attention.rescue.cen-jin"));
            Assert.That(
                LeaderInteractionCatalog.CenJinRescueMaximumDistance,
                Is.EqualTo(3f));
            Assert.That(
                LeaderInteractionCatalog.CenJinRescueSeconds,
                Is.EqualTo(12f));
            Assert.That(
                LeaderInteractionCatalog.CenJinBiomassCost,
                Is.EqualTo(10));
            Assert.That(
                LeaderInteractionCatalog.CenJinTimelyThresholdSeconds,
                Is.EqualTo(90f));
            Assert.That(
                LeaderInteractionCatalog.CenJinCriticalThresholdSeconds,
                Is.EqualTo(180f));
            Assert.That(
                LeaderInteractionCatalog.CenJinPopulationReward,
                Is.EqualTo(40));
        }
    }
}
