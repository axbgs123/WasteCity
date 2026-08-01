using NUnit.Framework;
using WasteCity.Building;
using WasteCity.Population;

namespace WasteCity.Tests
{
    public sealed class ConstructionProgressTests
    {
        [Test] public void OneHundredPercentProductivityUsesBaseDuration()
        { var job = new ConstructionProgress(5f); Assert.That(job.Tick(4.9f, 1f), Is.False); Assert.That(job.Tick(.1f, 1f), Is.True); }
        [Test] public void ProductivityReducesActualConstructionTime()
        { var job = new ConstructionProgress(10f); Assert.That(job.Tick(5f, 2f), Is.True); Assert.That(job.Normalized, Is.EqualTo(1f)); }
        [Test] public void ProductivityIsCappedAtTwoHundredFiftyPercent()
        { var people = new PopulationModel(1000, 1000); Assert.That(people.ProductivityMultiplier, Is.EqualTo(2.5f)); }
        [Test] public void BuildingCatalogCarriesFormalBuildTimeAndHealth()
        { Assert.That(BuildingCatalog.All[1].BuildSeconds, Is.EqualTo(5f)); Assert.That(BuildingCatalog.All[1].MaximumHealth, Is.EqualTo(250)); Assert.That(BuildingCatalog.All[7].BuildSeconds, Is.EqualTo(10f)); }
        [Test] public void RemainingConstructionTimeCanBeRestored()
        { var job = new ConstructionProgress(10f); job.Restore(2.5f); Assert.That(job.Remaining, Is.EqualTo(2.5f)); Assert.That(job.Normalized, Is.EqualTo(.75f)); }
    }
}
