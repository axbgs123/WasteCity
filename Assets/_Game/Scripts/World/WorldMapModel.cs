using System;
using WasteCity.Economy;

namespace WasteCity.World
{
    public enum TerrainKind { Wasteland, Rocky, Crystal, Wetland }

    public readonly struct WorldCell
    {
        public TerrainKind Terrain { get; }
        public string ResourceId { get; }
        public int ResourceAmount { get; }
        public bool HasResource => !string.IsNullOrEmpty(ResourceId);
        public WorldCell(TerrainKind terrain, string resourceId, int amount)
        { Terrain = terrain; ResourceId = resourceId; ResourceAmount = amount; }
    }

    public sealed class WorldMapModel
    {
        private readonly WorldCell[,] cells;
        private readonly bool[,] revealed;
        public int Width { get; }
        public int Height { get; }
        public int ResourceNodeCount { get; private set; }

        public WorldMapModel(int width, int height, WorldSeed seed)
        {
            Width = Math.Max(1, width); Height = Math.Max(1, height);
            cells = new WorldCell[Width, Height]; revealed = new bool[Width, Height];
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                int terrainRoll = seed.Sample(x, y, 0) % 100;
                TerrainKind terrain = terrainRoll < 15 ? TerrainKind.Crystal : terrainRoll < 35 ? TerrainKind.Rocky : terrainRoll < 45 ? TerrainKind.Wetland : TerrainKind.Wasteland;
                string resource = RollResource(seed.Sample(x, y, 1) % 100, terrain);
                int amount = resource == null ? 0 : 80 + seed.Sample(x, y, 2) % 321;
                cells[x, y] = new WorldCell(terrain, resource, amount);
                if (resource != null) ResourceNodeCount++;
            }
        }

        public WorldCell Get(int x, int y) => cells[x, y];
        public bool IsRevealed(int x, int y) => revealed[x, y];
        public int Reveal(int centerX, int centerY, int radius)
        {
            int changed = 0; int squared = radius * radius;
            for (int x = Math.Max(0, centerX - radius); x <= Math.Min(Width - 1, centerX + radius); x++)
                for (int y = Math.Max(0, centerY - radius); y <= Math.Min(Height - 1, centerY + radius); y++)
                    if (!revealed[x, y] && (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= squared)
                    { revealed[x, y] = true; changed++; }
            return changed;
        }

        private static string RollResource(int roll, TerrainKind terrain)
        {
            if (roll >= 18) return null;
            if (terrain == TerrainKind.Crystal) return ResourceIds.EnergyCrystal;
            if (terrain == TerrainKind.Rocky) return roll < 8 ? ResourceIds.Stone : ResourceIds.Iron;
            if (terrain == TerrainKind.Wetland) return roll < 9 ? ResourceIds.Water : ResourceIds.Biomass;
            return roll < 7 ? ResourceIds.Iron : roll < 12 ? ResourceIds.Stone : ResourceIds.Biomass;
        }
    }
}
