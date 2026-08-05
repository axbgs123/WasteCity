using WasteCity.World;

namespace WasteCity.City
{
    public static class CityTerrainRules
    {
        public static bool IsPassable(WorldCell cell)
        {
            return cell.Traversal != WorldTraversalKind.DeepWater &&
                   cell.Traversal != WorldTraversalKind.Cliff;
        }

        public static float SpeedMultiplier(WorldCell cell)
        {
            if (!IsPassable(cell)) return 0f;
            if (cell.Traversal == WorldTraversalKind.Ruins) return .65f;
            if (cell.Terrain == TerrainKind.Wetland) return .55f;
            if (cell.Terrain == TerrainKind.Rocky) return .8f;
            return 1f;
        }

        public static bool SupportsDeployment(WorldCell cell)
        {
            return IsPassable(cell) &&
                   cell.Traversal != WorldTraversalKind.Ruins &&
                   cell.Terrain != TerrainKind.Wetland;
        }

        public static string TraversalName(WorldTraversalKind traversal)
        {
            switch (traversal)
            {
                case WorldTraversalKind.Ruins: return "大型废墟";
                case WorldTraversalKind.DeepWater: return "深水";
                case WorldTraversalKind.Cliff: return "悬崖";
                default: return "开放";
            }
        }
    }
}
