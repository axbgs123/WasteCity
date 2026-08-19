using NUnit.Framework;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class CityDeploymentRulesTests
    {
        [Test]
        public void FormalDeploymentDurationsAreCentralizedAtFiveAndEightSeconds()
        {
            Assert.That(
                CityDeploymentRules.FormalDeployDurationSeconds,
                Is.EqualTo(5f));
            Assert.That(
                CityDeploymentRules.FormalPackDurationSeconds,
                Is.EqualTo(8f));
        }

        [Test]
        public void ThreeByThreeDeploymentRejectsBlockedCell()
        {
            WorldCell open = Open();
            WorldCell cliff = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Cliff);
            var cells = new[,]
            {
                { open, open, open },
                { open, open, open },
                { open, cliff, open }
            };

            Assert.That(
                CityDeploymentRules.Validate(
                    new WorldMapModel(cells),
                    1,
                    1),
                Is.EqualTo(CityDeploymentFailure.Blocked));
        }

        [Test]
        public void ThreeByThreeDeploymentAllowsRockyResourceGround()
        {
            var cells = new WorldCell[3, 3];
            for (int x = 0; x < 3; x++)
                for (int y = 0; y < 3; y++)
                    cells[x, y] = new WorldCell(
                        TerrainKind.Rocky,
                        x == 1 && y == 1 ? ResourceIds.Iron : null,
                        x == 1 && y == 1 ? 100 : 0);

            Assert.That(
                CityDeploymentRules.Validate(
                    new WorldMapModel(cells),
                    1,
                    1),
                Is.EqualTo(CityDeploymentFailure.None));
        }

        [Test]
        public void DeploymentAtWorldEdgeReportsInsufficientSpace()
        {
            Assert.That(
                CityDeploymentRules.Validate(AllOpen(3, 3), 0, 1),
                Is.EqualTo(CityDeploymentFailure.OutsideWorld));
        }

        [TestCase(TerrainKind.Wetland, WorldTraversalKind.Open)]
        [TestCase(TerrainKind.Wasteland, WorldTraversalKind.Ruins)]
        public void WetlandOrRuinsReportsUnstableGround(
            TerrainKind terrain,
            WorldTraversalKind traversal)
        {
            var cells = OpenCells(3, 3);
            cells[2, 2] = new WorldCell(terrain, null, 0, traversal);

            Assert.That(
                CityDeploymentRules.Validate(
                    new WorldMapModel(cells),
                    1,
                    1),
                Is.EqualTo(CityDeploymentFailure.UnstableGround));
        }

        [Test]
        public void BlockedFailureTakesPrecedenceOverUnstableGround()
        {
            var cells = OpenCells(3, 3);
            cells[0, 0] = new WorldCell(TerrainKind.Wetland, null, 0);
            cells[2, 2] = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.DeepWater);

            Assert.That(
                CityDeploymentRules.Validate(
                    new WorldMapModel(cells),
                    1,
                    1),
                Is.EqualTo(CityDeploymentFailure.Blocked));
        }

        [Test]
        public void EveryFailureHasStableChineseReason()
        {
            Assert.That(
                CityDeploymentRules.FailureReason(
                    CityDeploymentFailure.None),
                Is.Empty);
            Assert.That(
                CityDeploymentRules.FailureReason(
                    CityDeploymentFailure.OutsideWorld),
                Is.EqualTo("展开失败：空间不足"));
            Assert.That(
                CityDeploymentRules.FailureReason(
                    CityDeploymentFailure.Blocked),
                Is.EqualTo("展开失败：范围内存在深水或悬崖"));
            Assert.That(
                CityDeploymentRules.FailureReason(
                    CityDeploymentFailure.UnstableGround),
                Is.EqualTo("展开失败：地面不稳定或有大型废墟"));
        }

        private static WorldMapModel AllOpen(int width, int height)
        {
            return new WorldMapModel(OpenCells(width, height));
        }

        private static WorldCell[,] OpenCells(int width, int height)
        {
            var cells = new WorldCell[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    cells[x, y] = Open();
            return cells;
        }

        private static WorldCell Open()
        {
            return new WorldCell(TerrainKind.Wasteland, null, 0);
        }
    }
}
