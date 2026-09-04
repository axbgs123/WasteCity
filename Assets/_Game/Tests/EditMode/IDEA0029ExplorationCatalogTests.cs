using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029ExplorationCatalogTests
    {
        [Test]
        public void FormalSightRadiiAndIntelAgesMatchApprovedDesign()
        {
            Assert.That(FormalExplorationCatalog3D.ResolveSightRadius(
                WorldVisionSourceKind.PrimaryCity), Is.EqualTo(7));
            Assert.That(FormalExplorationCatalog3D.ResolveSightRadius(
                WorldVisionSourceKind.SecondaryCity), Is.EqualTo(5));
            Assert.That(FormalExplorationCatalog3D.ResolveSightRadius(
                WorldVisionSourceKind.Leader), Is.EqualTo(4));
            Assert.That(FormalExplorationCatalog3D.ResolveSightRadius(
                WorldVisionSourceKind.Outpost), Is.EqualTo(3));
            Assert.That(FormalExplorationCatalog3D.ResolveSightRadius(
                WorldVisionSourceKind.ScoutDrone), Is.EqualTo(6));
            Assert.That(FormalExplorationCatalog3D.IntelStaleSeconds,
                Is.EqualTo(60f));
            Assert.That(FormalExplorationCatalog3D.IntelExpiredSeconds,
                Is.EqualTo(180f));
        }

        [Test]
        public void FormalScanZonesReuseStableNodeFamiliesAndAttentionReasons()
        {
            Assert.That(FormalExplorationCatalog3D.ScanZones,
                Has.Count.EqualTo(2));
            ExplorationScanZoneDefinition safe =
                FormalExplorationCatalog3D.FindScanZoneForNode(
                    "world.deposit.safe-iron.01");
            ExplorationScanZoneDefinition rift =
                FormalExplorationCatalog3D.FindScanZoneForNode(
                    "world.deposit.rift-energy.02");

            Assert.That(safe.StableId,
                Is.EqualTo("core.exploration.zone.safe-mining"));
            Assert.That(safe.RevealRadius, Is.EqualTo(7));
            Assert.That(safe.AttentionReasonId,
                Is.EqualTo("core.attention.scan.safe-mining-zone"));
            Assert.That(rift.StableId,
                Is.EqualTo("core.exploration.zone.crystal-rift"));
            Assert.That(rift.RevealRadius, Is.EqualTo(8));
            Assert.That(rift.AttentionReasonId,
                Is.EqualTo("core.attention.scan.crystal-rift"));
            Assert.That(FormalExplorationCatalog3D.FindScanZoneForNode(
                "world.deposit.remote-iron.01"), Is.Null);
        }
    }
}
