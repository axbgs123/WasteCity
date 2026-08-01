using NUnit.Framework;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class DeploymentAndHarvestTests
    {
        [Test]
        public void DeploymentRequiresConfiguredTransitionTime()
        {
            var model = new CityDeploymentModel(3f, 5f); model.Toggle(); model.Tick(2.9f);
            Assert.That(model.Mode, Is.EqualTo(CityMode.Deploying)); model.Tick(0.1f);
            Assert.That(model.Mode, Is.EqualTo(CityMode.Fortress)); model.Toggle(); model.Tick(5f);
            Assert.That(model.Mode, Is.EqualTo(CityMode.Mobile));
        }

        [Test]
        public void ResourceHarvestDepletesFiniteNode()
        {
            var map = new WorldMapModel(12, 12, new WorldSeed(77));
            bool found = false;
            for (int x = 0; x < 12 && !found; x++) for (int y = 0; y < 12 && !found; y++)
                if (map.Get(x, y).HasResource)
                {
                    int before = map.Get(x, y).ResourceAmount; int result = map.Harvest(x, y, 10, out string id);
                    Assert.That(result, Is.EqualTo(10)); Assert.That(id, Is.Not.Empty); Assert.That(map.Get(x, y).ResourceAmount, Is.EqualTo(before - 10)); found = true;
                }
            Assert.That(found, Is.True);
        }
    }
}
