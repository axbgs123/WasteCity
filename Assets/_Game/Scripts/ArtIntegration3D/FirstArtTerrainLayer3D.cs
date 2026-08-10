using System;
using WasteCity.World;

namespace WasteCity.ArtIntegration3D
{
    public enum FirstArtTerrainLayer3D
    {
        Wasteland = 0,
        Rocky = 1,
        Wetland = 2,
        Crystal = 3,
        Ruins = 4,
        DeepWater = 5,
        Cliff = 6,
    }

    public static class FirstArtTerrainCatalog3D
    {
        public const int LayerCount = 7;

        public static FirstArtTerrainLayer3D LayerOf(WorldCell cell)
        {
            switch (cell.Traversal)
            {
                case WorldTraversalKind.Ruins:
                    return FirstArtTerrainLayer3D.Ruins;
                case WorldTraversalKind.DeepWater:
                    return FirstArtTerrainLayer3D.DeepWater;
                case WorldTraversalKind.Cliff:
                    return FirstArtTerrainLayer3D.Cliff;
                case WorldTraversalKind.Open:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cell), cell.Traversal, "Unknown world traversal kind.");
            }

            switch (cell.Terrain)
            {
                case TerrainKind.Wasteland:
                    return FirstArtTerrainLayer3D.Wasteland;
                case TerrainKind.Rocky:
                    return FirstArtTerrainLayer3D.Rocky;
                case TerrainKind.Wetland:
                    return FirstArtTerrainLayer3D.Wetland;
                case TerrainKind.Crystal:
                    return FirstArtTerrainLayer3D.Crystal;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cell), cell.Terrain, "Unknown terrain kind.");
            }
        }

        public static string StableIdOf(FirstArtTerrainLayer3D layer)
        {
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Wasteland:
                    return "world.terrain.wasteland";
                case FirstArtTerrainLayer3D.Rocky:
                    return "world.terrain.rocky";
                case FirstArtTerrainLayer3D.Wetland:
                    return "world.terrain.wetland";
                case FirstArtTerrainLayer3D.Crystal:
                    return "world.terrain.crystal";
                case FirstArtTerrainLayer3D.Ruins:
                    return "world.obstacle.ruins";
                case FirstArtTerrainLayer3D.DeepWater:
                    return "world.obstacle.deep-water";
                case FirstArtTerrainLayer3D.Cliff:
                    return "world.obstacle.cliff";
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown first-art terrain layer.");
            }
        }

        public static bool IsSurfaceStableId(string stableId)
        {
            return stableId == "world.terrain.wasteland" ||
                   stableId == "world.terrain.rocky" ||
                   stableId == "world.terrain.wetland" ||
                   stableId == "world.terrain.crystal";
        }
    }
}
