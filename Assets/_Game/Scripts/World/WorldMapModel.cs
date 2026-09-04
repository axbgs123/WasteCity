using System;
using WasteCity.Economy;

namespace WasteCity.World
{
    public enum TerrainKind { Wasteland, Rocky, Crystal, Wetland }
    public enum WorldTraversalKind { Open, Ruins, DeepWater, Cliff }

    public readonly struct WorldCell
    {
        public TerrainKind Terrain { get; }
        public string ResourceId { get; }
        public int ResourceAmount { get; }
        public WorldTraversalKind Traversal { get; }
        public bool HasResource => !string.IsNullOrEmpty(ResourceId);
        public WorldCell(TerrainKind terrain, string resourceId, int amount, WorldTraversalKind traversal = WorldTraversalKind.Open)
        { Terrain = terrain; ResourceId = resourceId; ResourceAmount = amount; Traversal = traversal; }
    }

    public readonly struct WorldOrphanResource
    {
        public WorldOrphanResource(
            string resourceId,
            int amount,
            string ownerKind,
            string ownerStableId)
        {
            ResourceId = resourceId;
            Amount = amount;
            OwnerKind = ownerKind;
            OwnerStableId = ownerStableId;
        }

        public string ResourceId { get; }
        public int Amount { get; }
        public string OwnerKind { get; }
        public string OwnerStableId { get; }
    }

    public sealed class WorldMapModel
    {
        private readonly WorldCell[,] cells;
        private readonly bool[,] revealed;
        private WorldOrphanResource[] orphanResources =
            Array.Empty<WorldOrphanResource>();
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
                WorldTraversalKind traversal = WorldTraversalKind.Open;
                if (resource == null)
                {
                    int traversalRoll = seed.Sample(x, y, 3) % 100;
                    traversal = traversalRoll < 4
                        ? WorldTraversalKind.Cliff
                        : traversalRoll < 8
                            ? WorldTraversalKind.DeepWater
                            : traversalRoll < 18
                                ? WorldTraversalKind.Ruins
                                : WorldTraversalKind.Open;
                }
                int amount = resource == null ? 0 : 80 + seed.Sample(x, y, 2) % 321;
                cells[x, y] = new WorldCell(terrain, resource, amount, traversal);
                if (resource != null) ResourceNodeCount++;
            }
        }

        public WorldMapModel(WorldCell[,] source)
        {
            if (source == null || source.GetLength(0) < 1 || source.GetLength(1) < 1)
                throw new ArgumentException("World cells are required.", nameof(source));
            Width = source.GetLength(0); Height = source.GetLength(1);
            cells = new WorldCell[Width, Height]; revealed = new bool[Width, Height];
            for (int x = 0; x < Width; x++) for (int y = 0; y < Height; y++)
            {
                cells[x, y] = source[x, y];
                if (cells[x, y].HasResource) ResourceNodeCount++;
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

        public int Harvest(int x, int y, int requested, out string resourceId)
        {
            int harvested = GetHarvestableAmount(
                x,
                y,
                requested,
                out resourceId);
            if (harvested <= 0) return 0;
            WorldCell cell = cells[x, y];
            cells[x, y] = new WorldCell(cell.Terrain, cell.ResourceId, cell.ResourceAmount - harvested, cell.Traversal);
            return harvested;
        }

        public int GetHarvestableAmount(
            int x,
            int y,
            int requested,
            out string resourceId)
        {
            resourceId = null;
            if (x < 0 || y < 0 || x >= Width || y >= Height ||
                requested <= 0)
            {
                return 0;
            }

            WorldCell cell = cells[x, y];
            if (!cell.HasResource || cell.ResourceAmount <= 0) return 0;
            resourceId = cell.ResourceId;
            return Math.Min(requested, cell.ResourceAmount);
        }

        public bool TryHarvestExact(
            int x,
            int y,
            string expectedResourceId,
            int amount)
        {
            int harvestable = GetHarvestableAmount(
                x,
                y,
                amount,
                out string actualResourceId);
            if (harvestable != amount ||
                !string.Equals(
                    actualResourceId,
                    expectedResourceId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            WorldCell cell = cells[x, y];
            cells[x, y] = new WorldCell(
                cell.Terrain,
                cell.ResourceId,
                cell.ResourceAmount - amount,
                cell.Traversal);
            return true;
        }

        public bool TryRollbackHarvest(
            int x,
            int y,
            string expectedResourceId,
            int amount)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height ||
                string.IsNullOrWhiteSpace(expectedResourceId) ||
                amount <= 0)
            {
                return false;
            }

            WorldCell cell = cells[x, y];
            if (!cell.HasResource ||
                !string.Equals(
                    cell.ResourceId,
                    expectedResourceId,
                    StringComparison.Ordinal) ||
                cell.ResourceAmount > int.MaxValue - amount)
            {
                return false;
            }

            cells[x, y] = new WorldCell(
                cell.Terrain,
                cell.ResourceId,
                cell.ResourceAmount + amount,
                cell.Traversal);
            return true;
        }
        public int[] CaptureResourceAmounts() { var result=new int[Width*Height];for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)result[y*Width+x]=cells[x,y].ResourceAmount;return result; }
        public bool[] CaptureRevealed() { var result=new bool[Width*Height];for(int y=0;y<Height;y++)for(int x=0;x<Width;x++)result[y*Width+x]=revealed[x,y];return result; }
        public bool Restore(int[] amounts, bool[] visibility)
        {
            if(amounts==null||visibility==null||amounts.Length!=Width*Height||visibility.Length!=Width*Height)return false;
            for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){int index=y*Width+x;var cell=cells[x,y];cells[x,y]=new WorldCell(cell.Terrain,cell.ResourceId,Math.Max(0,amounts[index]),cell.Traversal);revealed[x,y]=visibility[index];}return true;
        }

        public bool TryRestoreResourceAmounts(int[] amounts, out string error)
        {
            if (amounts == null)
            {
                error = "资源数量数据不能为空";
                return false;
            }

            if (amounts.Length != Width * Height)
            {
                error = "资源数量长度必须与世界格位数量一致";
                return false;
            }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int amount = amounts[y * Width + x];
                    if (amount < 0)
                    {
                        error = "资源数量不能为负数";
                        return false;
                    }

                    if (!cells[x, y].HasResource && amount != 0)
                    {
                        error = "非资源格位的资源数量必须为零";
                        return false;
                    }
                }
            }

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int index = y * Width + x;
                    WorldCell cell = cells[x, y];
                    cells[x, y] = new WorldCell(
                        cell.Terrain,
                        cell.ResourceId,
                        amounts[index],
                        cell.Traversal);
                }
            }

            error = string.Empty;
            return true;
        }

        public WorldOrphanResource[] CaptureOrphanResources()
        {
            return (WorldOrphanResource[])orphanResources.Clone();
        }

        public bool TryRestoreOrphanResources(
            WorldOrphanResource[] resources,
            out string error)
        {
            if (resources == null)
            {
                error = "孤立资源数据不能为空";
                return false;
            }
            var keys = new System.Collections.Generic.HashSet<string>(
                StringComparer.Ordinal);
            for (var index = 0; index < resources.Length; index++)
            {
                WorldOrphanResource resource = resources[index];
                if (string.IsNullOrWhiteSpace(resource.ResourceId) ||
                    string.IsNullOrWhiteSpace(resource.OwnerKind) ||
                    string.IsNullOrWhiteSpace(resource.OwnerStableId) ||
                    resource.Amount < 0)
                {
                    error = "孤立资源字段无效";
                    return false;
                }
                string key = resource.OwnerKind + "\n" +
                             resource.OwnerStableId + "\n" +
                             resource.ResourceId;
                if (!keys.Add(key))
                {
                    error = "孤立资源归属重复";
                    return false;
                }
            }

            orphanResources = (WorldOrphanResource[])resources.Clone();
            error = string.Empty;
            return true;
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
