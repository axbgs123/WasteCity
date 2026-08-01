using NUnit.Framework;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class ResearchTests
    {
        [Test]
        public void ResearchConsumesRouteResourceAndCompletesAfterDuration()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.EnergyCrystal, 10);
            var model = new ResearchModel(); var definition = ResearchCatalog.Starting[1];
            Assert.That(model.Start(definition, inventory), Is.True); Assert.That(inventory.Get(ResourceIds.EnergyCrystal), Is.Zero);
            Assert.That(model.Tick(19.9f), Is.False); Assert.That(model.Tick(0.1f), Is.True); Assert.That(model.IsCompleted(definition.Id), Is.True);
        }
        [Test]
        public void OnlyOneResearchRunsAtATime()
        {
            var inventory = new ResourceInventory(100); inventory.Add(ResourceIds.Iron, 20); inventory.Add(ResourceIds.Water, 20);
            var model = new ResearchModel(); Assert.That(model.Start(ResearchCatalog.Starting[0], inventory), Is.True);
            Assert.That(model.Start(ResearchCatalog.Starting[3], inventory), Is.False);
        }
    }
}
