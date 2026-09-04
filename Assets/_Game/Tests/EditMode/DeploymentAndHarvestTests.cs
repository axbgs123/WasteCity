using NUnit.Framework;
using WasteCity.City;
using WasteCity.Economy;
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

        [Test]
        public void HarvestPreflightAndExactCommitUseWorldNodeTruth()
        {
            var cells = new WorldCell[1, 1];
            cells[0, 0] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                2);
            var map = new WorldMapModel(cells);

            Assert.That(map.GetHarvestableAmount(
                0, 0, 5, out string resourceId), Is.EqualTo(2));
            Assert.That(resourceId, Is.EqualTo(ResourceIds.Iron));
            Assert.That(map.Get(0, 0).ResourceAmount, Is.EqualTo(2),
                "Preflight must not mutate the authoritative node.");
            Assert.That(map.TryHarvestExact(
                0, 0, ResourceIds.Stone, 1), Is.False);
            Assert.That(map.TryHarvestExact(
                0, 0, ResourceIds.Iron, 3), Is.False);
            Assert.That(map.Get(0, 0).ResourceAmount, Is.EqualTo(2));

            Assert.That(map.TryHarvestExact(
                0, 0, ResourceIds.Iron, 1), Is.True);
            Assert.That(map.Get(0, 0).ResourceAmount, Is.EqualTo(1));
        }

        [Test]
        public void FailedBackpackCommitRollsActualWorldHarvestBackWithoutLoss()
        {
            var cells = new WorldCell[1, 1];
            cells[0, 0] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                2);
            var map = new WorldMapModel(cells);
            var backpack = new PlayerBackpackModel();
            int before = map.Get(0, 0).ResourceAmount;

            WorldHarvestTransactionResult result =
                WorldHarvestTransaction.TryCommitOne(
                    ResourceIds.Iron,
                    (id, amount) =>
                        backpack.GetAcceptableAmount(id, amount) == amount,
                    (id, amount) =>
                        map.TryHarvestExact(0, 0, id, amount),
                    (_, __) => false,
                    (id, amount) => Assert.That(
                        map.TryRollbackHarvest(0, 0, id, amount),
                        Is.True));

            Assert.That(result.Status,
                Is.EqualTo(WorldHarvestTransactionStatus.CommitFailed));
            Assert.That(map.Get(0, 0).ResourceAmount, Is.EqualTo(before));
            Assert.That(backpack.GetAcceptableAmount(
                ResourceIds.Iron, 1), Is.EqualTo(1));
        }
    }
}
