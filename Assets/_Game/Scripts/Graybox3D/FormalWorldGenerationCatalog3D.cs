using System;
using System.Collections.Generic;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public readonly struct FormalWorldCellPoint3D
    {
        public FormalWorldCellPoint3D(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }
    }

    public readonly struct FormalTraversalRegionSpec3D
    {
        public FormalTraversalRegionSpec3D(
            string stableId,
            WorldTraversalKind traversal,
            int minimumX,
            int minimumY,
            int width,
            int height)
        {
            StableId = stableId;
            Traversal = traversal;
            MinimumX = minimumX;
            MinimumY = minimumY;
            Width = width;
            Height = height;
        }

        public string StableId { get; }
        public WorldTraversalKind Traversal { get; }
        public int MinimumX { get; }
        public int MinimumY { get; }
        public int Width { get; }
        public int Height { get; }
    }

    public readonly struct FormalWorldCorridorSpec3D
    {
        public FormalWorldCorridorSpec3D(
            string stableId,
            params FormalWorldCellPoint3D[] points)
        {
            StableId = stableId;
            Points = Array.AsReadOnly(
                points ?? Array.Empty<FormalWorldCellPoint3D>());
        }

        public string StableId { get; }
        public IReadOnlyList<FormalWorldCellPoint3D> Points { get; }
    }

    public readonly struct FormalResourceNodeSpec3D
    {
        public FormalResourceNodeSpec3D(
            string stableId,
            string resourceId,
            int x,
            int y,
            int amount)
        {
            StableId = stableId;
            ResourceId = resourceId;
            X = x;
            Y = y;
            Amount = amount;
        }

        public string StableId { get; }
        public string ResourceId { get; }
        public int X { get; }
        public int Y { get; }
        public int Amount { get; }
    }

    public static class FormalWorldGenerationCatalog3D
    {
        public const int WorldGenerationVersion = 2;
        public const string WorldConfigurationSignature =
            "core.world.formal-3d.v2.64x48";
        public const int Width = 64;
        public const int Height = 48;
        public const int DefaultSeed = 8128;
        public const int StartCellX = 10;
        public const int StartCellY = 9;
        public const int StartProtectionRadius = 4;
        public const int MacroCellSize = 8;
        public const int BoundaryWarpAmplitude = 2;
        public const int TerrainCleanupPasses = 1;
        public const int ExpectedResourceNodeCount = 24;

        private static readonly TerrainKind[] macroTerrain =
        {
            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wetland,
            TerrainKind.Wetland, TerrainKind.Wetland,

            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wetland, TerrainKind.Wetland,

            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wasteland,

            TerrainKind.Wasteland, TerrainKind.Rocky,
            TerrainKind.Rocky, TerrainKind.Rocky,
            TerrainKind.Crystal, TerrainKind.Wasteland,
            TerrainKind.Wasteland, TerrainKind.Wasteland,

            TerrainKind.Rocky, TerrainKind.Rocky,
            TerrainKind.Rocky, TerrainKind.Crystal,
            TerrainKind.Crystal, TerrainKind.Rocky,
            TerrainKind.Wasteland, TerrainKind.Wasteland,

            TerrainKind.Rocky, TerrainKind.Rocky,
            TerrainKind.Rocky, TerrainKind.Crystal,
            TerrainKind.Crystal, TerrainKind.Crystal,
            TerrainKind.Rocky, TerrainKind.Wasteland,
        };

        private static readonly FormalTraversalRegionSpec3D[]
            traversalRegions =
            {
                new FormalTraversalRegionSpec3D(
                    "world.region.water.south-basin",
                    WorldTraversalKind.DeepWater,
                    48, 4, 8, 4),
                new FormalTraversalRegionSpec3D(
                    "world.region.water.east-basin",
                    WorldTraversalKind.DeepWater,
                    56, 11, 7, 3),

                new FormalTraversalRegionSpec3D(
                    "world.region.ruins.southwest",
                    WorldTraversalKind.Ruins,
                    20, 16, 3, 3),
                new FormalTraversalRegionSpec3D(
                    "world.region.ruins.central",
                    WorldTraversalKind.Ruins,
                    39, 19, 3, 3),
                new FormalTraversalRegionSpec3D(
                    "world.region.ruins.east",
                    WorldTraversalKind.Ruins,
                    50, 29, 3, 3),
                new FormalTraversalRegionSpec3D(
                    "world.region.ruins.northwest",
                    WorldTraversalKind.Ruins,
                    7, 34, 3, 3),

                new FormalTraversalRegionSpec3D(
                    "world.region.cliff.northwest-ridge",
                    WorldTraversalKind.Cliff,
                    3, 40, 13, 1),
                new FormalTraversalRegionSpec3D(
                    "world.region.cliff.rift-east",
                    WorldTraversalKind.Cliff,
                    43, 34, 1, 12),
                new FormalTraversalRegionSpec3D(
                    "world.region.cliff.northeast-ridge",
                    WorldTraversalKind.Cliff,
                    50, 38, 12, 1),
            };

        private static readonly FormalWorldCorridorSpec3D[] corridors =
        {
            new FormalWorldCorridorSpec3D(
                "world.corridor.rift-south",
                new FormalWorldCellPoint3D(StartCellX, StartCellY),
                new FormalWorldCellPoint3D(31, StartCellY),
                new FormalWorldCellPoint3D(31, 35),
                new FormalWorldCellPoint3D(32, 35)),
            new FormalWorldCorridorSpec3D(
                "world.corridor.rift-northwest",
                new FormalWorldCellPoint3D(StartCellX, StartCellY),
                new FormalWorldCellPoint3D(StartCellX, 26),
                new FormalWorldCellPoint3D(34, 26),
                new FormalWorldCellPoint3D(34, 35),
                new FormalWorldCellPoint3D(32, 35)),
        };

        private static readonly FormalResourceNodeSpec3D[] resourceNodes =
        {
            new FormalResourceNodeSpec3D(
                "world.deposit.safe-iron.01", ResourceIds.Iron,
                16, 15, 240),
            new FormalResourceNodeSpec3D(
                "world.deposit.safe-iron.02", ResourceIds.Iron,
                13, 15, 240),
            new FormalResourceNodeSpec3D(
                "world.deposit.safe-stone.01", ResourceIds.Stone,
                11, 16, 240),

            new FormalResourceNodeSpec3D(
                "world.deposit.rift-iron.01", ResourceIds.Iron,
                32, 38, 480),
            new FormalResourceNodeSpec3D(
                "world.deposit.rift-iron.02", ResourceIds.Iron,
                29, 38, 480),
            new FormalResourceNodeSpec3D(
                "world.deposit.rift-iron.03", ResourceIds.Iron,
                32, 41, 480),
            new FormalResourceNodeSpec3D(
                "world.deposit.remote-iron.01", ResourceIds.Iron,
                49, 30, 343),
            new FormalResourceNodeSpec3D(
                "world.deposit.remote-iron.02", ResourceIds.Iron,
                52, 28, 377),
            new FormalResourceNodeSpec3D(
                "world.deposit.remote-iron.03", ResourceIds.Iron,
                52, 32, 380),

            new FormalResourceNodeSpec3D(
                "world.deposit.highland-stone.01", ResourceIds.Stone,
                18, 39, 339),
            new FormalResourceNodeSpec3D(
                "world.deposit.highland-stone.02", ResourceIds.Stone,
                18, 36, 376),
            new FormalResourceNodeSpec3D(
                "world.deposit.ruin-stone.01", ResourceIds.Stone,
                42, 23, 307),

            new FormalResourceNodeSpec3D(
                "world.deposit.rift-energy.01", ResourceIds.EnergyCrystal,
                38, 38, 402),
            new FormalResourceNodeSpec3D(
                "world.deposit.rift-energy.02", ResourceIds.EnergyCrystal,
                38, 35, 410),
            new FormalResourceNodeSpec3D(
                "world.deposit.south-energy.01", ResourceIds.EnergyCrystal,
                29, 32, 384),
            new FormalResourceNodeSpec3D(
                "world.deposit.south-energy.02", ResourceIds.EnergyCrystal,
                32, 30, 333),

            new FormalResourceNodeSpec3D(
                "world.deposit.wetland-water.01", ResourceIds.Water,
                54, 10, 303),
            new FormalResourceNodeSpec3D(
                "world.deposit.wetland-water.02", ResourceIds.Water,
                54, 13, 226),
            new FormalResourceNodeSpec3D(
                "world.deposit.wetland-water.03", ResourceIds.Water,
                51, 10, 229),
            new FormalResourceNodeSpec3D(
                "world.deposit.wetland-water.04", ResourceIds.Water,
                57, 10, 232),

            new FormalResourceNodeSpec3D(
                "world.deposit.wetland-biomass.01", ResourceIds.Biomass,
                50, 15, 223),
            new FormalResourceNodeSpec3D(
                "world.deposit.wetland-biomass.02", ResourceIds.Biomass,
                53, 15, 186),
            new FormalResourceNodeSpec3D(
                "world.deposit.ruin-biomass.01", ResourceIds.Biomass,
                38, 19, 239),
            new FormalResourceNodeSpec3D(
                "world.deposit.ruin-biomass.02", ResourceIds.Biomass,
                41, 18, 271),
        };

        public static IReadOnlyList<TerrainKind> MacroTerrain => macroTerrain;
        public static IReadOnlyList<FormalTraversalRegionSpec3D>
            TraversalRegions => traversalRegions;
        public static IReadOnlyList<FormalWorldCorridorSpec3D>
            Corridors => corridors;
        public static IReadOnlyList<FormalResourceNodeSpec3D>
            ResourceNodes => resourceNodes;
    }
}
