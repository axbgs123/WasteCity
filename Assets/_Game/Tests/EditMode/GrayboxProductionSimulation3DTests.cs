using NUnit.Framework;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Graybox3D.Production;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxProductionSimulation3DTests
    {
        [Test]
        public void IDEA0011_Instances_OwnIndependentProgressAndPause()
        {
            GrayboxBuildingProductionState3D first = Recipe(
                "building.instance.000001",
                BuildingCatalog.Smelter.Id.Value);
            GrayboxBuildingProductionState3D second = Recipe(
                "building.instance.000002",
                BuildingCatalog.Smelter.Id.Value);
            first.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                4);
            second.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                4);
            second.SetManuallyPaused(true);
            var simulation = new GrayboxProductionSimulation3D();

            simulation.Tick(
                3f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { first, second });

            Assert.That(first.ProgressSeconds, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(second.ProgressSeconds, Is.Zero);
            Assert.That(
                second.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.PlayerPaused));

            second.SetManuallyPaused(false);
            simulation.Tick(
                3f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { first, second });

            Assert.That(first.Cache.OutputAmount, Is.EqualTo(1));
            Assert.That(
                first.ProgressSeconds,
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(second.Cache.OutputAmount, Is.Zero);
            Assert.That(second.ProgressSeconds, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void IDEA0011_Mining_UsesThreeBaseSecondsAndFortressMultiplier()
        {
            WorldMapModel world = SingleNode(ResourceIds.Iron, 10);
            GrayboxBuildingProductionState3D mine = Mine(
                "building.instance.000001",
                world,
                0,
                0);
            var simulation = new GrayboxProductionSimulation3D();

            simulation.Tick(
                2.39f,
                CityMode.Fortress,
                world,
                EmptyCityInventory(),
                new[] { mine });
            Assert.That(mine.Cache.OutputAmount, Is.Zero);

            simulation.Tick(
                0.01f,
                CityMode.Fortress,
                world,
                EmptyCityInventory(),
                new[] { mine });

            Assert.That(
                CityOperationalRules.ProductionMultiplier(CityMode.Fortress),
                Is.EqualTo(1.25f));
            Assert.That(mine.Cache.OutputAmount, Is.EqualTo(1));
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(9));
        }

        [TestCase(ResourceIds.Iron)]
        [TestCase(ResourceIds.EnergyCrystal)]
        public void IDEA0011_Mining_ProducesItsBoundNodeResource(
            string resourceId)
        {
            WorldMapModel world = SingleNode(resourceId, 2);
            GrayboxBuildingProductionState3D mine = Mine(
                "building.instance.000001",
                world,
                0,
                0);

            new GrayboxProductionSimulation3D().Tick(
                3f,
                CityMode.Mobile,
                world,
                EmptyCityInventory(),
                new[] { mine });

            Assert.That(mine.Cache.OutputResourceId, Is.EqualTo(resourceId));
            Assert.That(mine.Cache.OutputAmount, Is.EqualTo(1));
            Assert.That(world.Get(0, 0).ResourceAmount, Is.EqualTo(1));
        }

        [Test]
        public void IDEA0011_Recipes_UseApprovedAtomicResults()
        {
            GrayboxBuildingProductionState3D smelter = Recipe(
                "building.instance.000001",
                BuildingCatalog.Smelter.Id.Value);
            GrayboxBuildingProductionState3D assembler = Recipe(
                "building.instance.000002",
                BuildingCatalog.Assembler.Id.Value);
            smelter.SetLogisticsConnected(false);
            assembler.SetLogisticsConnected(false);
            smelter.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                2);
            assembler.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Alloy,
                2);

            new GrayboxProductionSimulation3D().Tick(
                6f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { smelter, assembler });

            Assert.That(smelter.Cache.InputAmount, Is.Zero);
            Assert.That(
                smelter.Cache.OutputResourceId,
                Is.EqualTo(ResourceIds.Alloy));
            Assert.That(smelter.Cache.OutputAmount, Is.EqualTo(1));
            Assert.That(assembler.Cache.InputAmount, Is.Zero);
            Assert.That(
                assembler.Cache.OutputResourceId,
                Is.EqualTo(ResourceIds.Ammunition));
            Assert.That(assembler.Cache.OutputAmount, Is.EqualTo(2));
        }

        [Test]
        public void IDEA0011_RecipeInputAndOutputChecks_AreAtomic()
        {
            GrayboxBuildingProductionState3D smelter = Recipe(
                "building.instance.000001",
                BuildingCatalog.Smelter.Id.Value);
            smelter.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                1);
            var simulation = new GrayboxProductionSimulation3D();

            simulation.Tick(
                6f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { smelter });

            Assert.That(smelter.Cache.InputAmount, Is.EqualTo(1));
            Assert.That(smelter.Cache.OutputAmount, Is.Zero);
            Assert.That(
                smelter.ProgressSeconds,
                Is.EqualTo(0f).Within(0.0001f));
            Assert.That(
                smelter.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.MissingInput));

            smelter.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                1);
            smelter.Cache.Add(
                GrayboxBuildingCachePort3D.Output,
                ResourceIds.Alloy,
                smelter.Cache.OutputCapacity);
            simulation.Tick(
                6f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { smelter });

            Assert.That(smelter.Cache.InputAmount, Is.EqualTo(2));
            Assert.That(
                smelter.Cache.OutputAmount,
                Is.EqualTo(smelter.Cache.OutputCapacity));
            Assert.That(
                smelter.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.OutputFull));
        }

        [Test]
        public void IDEA0011_StopReasonPriority_IsPauseDepletedFullOutsideThenMissing()
        {
            WorldMapModel depletedWorld = SingleNode(ResourceIds.Iron, 0);
            GrayboxBuildingProductionState3D mine = Mine(
                "building.instance.000001",
                depletedWorld,
                0,
                0);
            mine.Cache.Add(
                GrayboxBuildingCachePort3D.Output,
                ResourceIds.Iron,
                mine.Cache.OutputCapacity);
            mine.SetManuallyPaused(true);
            var simulation = new GrayboxProductionSimulation3D();

            simulation.Tick(
                0.1f,
                CityMode.Mobile,
                depletedWorld,
                EmptyCityInventory(),
                new[] { mine });
            Assert.That(
                mine.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.PlayerPaused));

            mine.SetManuallyPaused(false);
            simulation.Tick(
                0.1f,
                CityMode.Mobile,
                depletedWorld,
                EmptyCityInventory(),
                new[] { mine });
            Assert.That(
                mine.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.ResourceDepleted));

            GrayboxBuildingProductionState3D smelter = Recipe(
                "building.instance.000002",
                BuildingCatalog.Smelter.Id.Value);
            smelter.SetLogisticsConnected(false);
            smelter.Cache.Add(
                GrayboxBuildingCachePort3D.Output,
                ResourceIds.Alloy,
                smelter.Cache.OutputCapacity);
            simulation.Tick(
                0.1f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { smelter });
            Assert.That(
                smelter.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.OutputFull));

            smelter.Cache.Remove(
                GrayboxBuildingCachePort3D.Output,
                smelter.Cache.OutputCapacity);
            simulation.Tick(
                0.1f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { smelter });
            Assert.That(
                smelter.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.OutsideLogistics));

            smelter.SetLogisticsConnected(true);
            simulation.Tick(
                0.1f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { smelter });
            Assert.That(
                smelter.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.MissingInput));
        }

        [Test]
        public void IDEA0011_DepletedNode_StopsWithoutExtraHarvest()
        {
            WorldMapModel world = SingleNode(ResourceIds.Iron, 1);
            GrayboxBuildingProductionState3D mine = Mine(
                "building.instance.000001",
                world,
                0,
                0);
            var simulation = new GrayboxProductionSimulation3D();

            simulation.Tick(
                3f,
                CityMode.Mobile,
                world,
                EmptyCityInventory(),
                new[] { mine });
            simulation.Tick(
                30f,
                CityMode.Mobile,
                world,
                EmptyCityInventory(),
                new[] { mine });

            Assert.That(world.Get(0, 0).ResourceAmount, Is.Zero);
            Assert.That(mine.Cache.OutputAmount, Is.EqualTo(1));
            Assert.That(
                mine.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.ResourceDepleted));
        }

        [Test]
        public void IDEA0011_DisconnectedBuilding_UsesLocalInputButNotRemoteSupply()
        {
            WorldMapModel world = SingleNode(ResourceIds.Iron, 20);
            GrayboxBuildingProductionState3D source = Mine(
                "building.instance.000001",
                world,
                0,
                0);
            source.Cache.Add(
                GrayboxBuildingCachePort3D.Output,
                ResourceIds.Iron,
                4);
            source.SetManuallyPaused(true);
            GrayboxBuildingProductionState3D smelter = Recipe(
                "building.instance.000002",
                BuildingCatalog.Smelter.Id.Value);
            smelter.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                2);
            smelter.SetLogisticsConnected(false);
            var simulation = new GrayboxProductionSimulation3D();

            simulation.Tick(
                6f,
                CityMode.Mobile,
                world,
                EmptyCityInventory(),
                new[] { source, smelter });

            Assert.That(smelter.Cache.OutputAmount, Is.EqualTo(1));
            Assert.That(smelter.Cache.InputAmount, Is.Zero);
            Assert.That(source.Cache.OutputAmount, Is.EqualTo(4));
            Assert.That(
                smelter.StopReason,
                Is.EqualTo(GrayboxProductionStopReason3D.OutsideLogistics));

            smelter.SetLogisticsConnected(true);
            simulation.Tick(
                6f,
                CityMode.Mobile,
                world,
                EmptyCityInventory(),
                new[] { source, smelter });

            Assert.That(smelter.Cache.OutputAmount, Is.EqualTo(2));
            Assert.That(source.Cache.OutputAmount, Is.LessThan(4));
        }

        [Test]
        public void IDEA0011_TickChunking_IsDeterministic()
        {
            GrayboxBuildingProductionState3D singleTick = Recipe(
                "building.instance.000001",
                BuildingCatalog.Smelter.Id.Value);
            GrayboxBuildingProductionState3D splitTicks = Recipe(
                "building.instance.000001",
                BuildingCatalog.Smelter.Id.Value);
            singleTick.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                20);
            splitTicks.Cache.Add(
                GrayboxBuildingCachePort3D.Input,
                ResourceIds.Iron,
                20);
            var firstSimulation = new GrayboxProductionSimulation3D();
            var secondSimulation = new GrayboxProductionSimulation3D();

            firstSimulation.Tick(
                12f,
                CityMode.Mobile,
                null,
                EmptyCityInventory(),
                new[] { singleTick });
            for (var index = 0; index < 12; index++)
                secondSimulation.Tick(
                    1f,
                    CityMode.Mobile,
                    null,
                    EmptyCityInventory(),
                    new[] { splitTicks });

            Assert.That(
                splitTicks.Cache.InputAmount,
                Is.EqualTo(singleTick.Cache.InputAmount));
            Assert.That(
                splitTicks.Cache.OutputAmount,
                Is.EqualTo(singleTick.Cache.OutputAmount));
            Assert.That(
                splitTicks.ProgressSeconds,
                Is.EqualTo(singleTick.ProgressSeconds).Within(0.0001f));
            Assert.That(
                splitTicks.StopReason,
                Is.EqualTo(singleTick.StopReason));
            Assert.That(
                splitTicks.CompletedCycles,
                Is.EqualTo(singleTick.CompletedCycles));
        }

        private static GrayboxBuildingProductionState3D Recipe(
            string stableInstanceId,
            string buildingId)
        {
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    buildingId,
                    out GrayboxProductionDefinition3D definition),
                Is.True);
            GrayboxBuildingProductionState3D state =
                GrayboxBuildingProductionState3D.CreateRecipe(
                    stableInstanceId,
                    definition);
            state.SetLogisticsConnected(true);
            return state;
        }

        private static GrayboxBuildingProductionState3D Mine(
            string stableInstanceId,
            WorldMapModel world,
            int nodeX,
            int nodeY)
        {
            Assert.That(
                GrayboxProductionCatalog3D.TryGet(
                    BuildingCatalog.MiningStation.Id.Value,
                    out GrayboxProductionDefinition3D definition),
                Is.True);
            WorldCell node = world.Get(nodeX, nodeY);
            GrayboxBuildingProductionState3D state =
                GrayboxBuildingProductionState3D.CreateExtraction(
                    stableInstanceId,
                    definition,
                    GrayboxResourceNodeIdentity3D.Create(nodeX, nodeY),
                    nodeX,
                    nodeY,
                    node.ResourceId);
            state.SetLogisticsConnected(true);
            return state;
        }

        private static ResourceInventory EmptyCityInventory()
        {
            return new ResourceInventory(150);
        }

        private static WorldMapModel SingleNode(
            string resourceId,
            int resourceAmount)
        {
            return new WorldMapModel(
                new[,]
                {
                    {
                        new WorldCell(
                            TerrainKind.Rocky,
                            resourceId,
                            resourceAmount)
                    }
                });
        }
    }
}
