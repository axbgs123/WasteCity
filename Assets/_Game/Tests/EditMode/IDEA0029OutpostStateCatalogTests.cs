using System.Linq;
using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029OutpostStateCatalogTests
    {
        [Test]
        public void CatalogHasExactlyThreeStablePlayerFacingAlertLevels()
        {
            Assert.That(OutpostAlertCatalog.All.Count, Is.EqualTo(3));
            Assert.That(
                OutpostAlertCatalog.All.Select(value => value.StableId),
                Is.EqualTo(new[]
                {
                    "core.outpost.alert.guard",
                    "core.outpost.alert.under-attack",
                    "core.outpost.alert.critical",
                }));
            Assert.That(
                OutpostAlertCatalog.All.Select(value => value.ChineseName),
                Is.EqualTo(new[] { "警戒", "受袭", "危急" }));
            Assert.That(
                OutpostAlertCatalog.All.Select(value => value.Severity),
                Is.EqualTo(new[]
                {
                    OutpostAlertSeverity.Guard,
                    OutpostAlertSeverity.UnderAttack,
                    OutpostAlertSeverity.Critical,
                }));
        }

        [Test]
        public void CatalogResolvesOnlyKnownIdsAndSeverities()
        {
            Assert.That(OutpostAlertCatalog.Find(
                "core.outpost.alert.under-attack").Severity,
                Is.EqualTo(OutpostAlertSeverity.UnderAttack));
            Assert.That(OutpostAlertCatalog.ForSeverity(
                OutpostAlertSeverity.Critical).ChineseName,
                Is.EqualTo("危急"));
            Assert.That(OutpostAlertCatalog.Find("unknown"), Is.Null);
            Assert.That(OutpostAlertCatalog.Find(null), Is.Null);
            Assert.That(OutpostAlertCatalog.ForSeverity(
                OutpostAlertSeverity.None), Is.Null);
        }
    }
}
