using System;
using System.Collections.Generic;
using WasteCity.Content;
using WasteCity.Economy;

namespace WasteCity.Building
{
    public sealed class BuildingDefinition
    {
        public StableId Id { get; }
        public string Name { get; }
        public int Width { get; }
        public int Height { get; }
        public string CostId { get; }
        public int Cost { get; }
        public bool RequiresResourceNode { get; }
        public BuildingDefinition(string id, string name, int width, int height, string costId, int cost, bool requiresNode = false)
        { Id = new StableId(id); Name = name; Width = Math.Max(1, width); Height = Math.Max(1, height); CostId = costId; Cost = Math.Max(0, cost); RequiresResourceNode = requiresNode; }
    }

    public static class BuildingCatalog
    {
        public static readonly BuildingDefinition[] All =
        {
            new BuildingDefinition("core.building.mining-station", "采矿站", 2, 2, ResourceIds.Iron, 4, true),
            new BuildingDefinition("core.building.housing", "住房", 2, 2, ResourceIds.Alloy, 8),
            new BuildingDefinition("core.building.warehouse", "仓库", 2, 2, ResourceIds.Alloy, 8),
            new BuildingDefinition("core.building.wall", "城墙", 1, 1, ResourceIds.Stone, 2),
            new BuildingDefinition("core.building.research-station", "研究站", 2, 2, ResourceIds.Iron, 6),
            new BuildingDefinition("core.building.smelter", "冶炼厂", 2, 2, ResourceIds.Stone, 6),
            new BuildingDefinition("core.building.assembler", "装配厂", 2, 2, ResourceIds.Alloy, 8),
            new BuildingDefinition("core.building.machine-gun-turret", "机枪塔", 1, 1, ResourceIds.Alloy, 10)
        };
    }

    public sealed class PlacedBuilding
    {
        public BuildingDefinition Definition { get; }
        public int X { get; }
        public int Y { get; }
        public PlacedBuilding(BuildingDefinition definition, int x, int y) { Definition = definition; X = x; Y = y; }
    }

    public sealed class BuildingGrid
    {
        private readonly PlacedBuilding[,] cells;
        public int Count { get; private set; }
        public BuildingGrid(int width, int height) => cells = new PlacedBuilding[Math.Max(1, width), Math.Max(1, height)];
        public bool TryPlace(BuildingDefinition definition, int x, int y, ResourceInventory inventory, bool coversResource, out PlacedBuilding placed)
        {
            placed = null; if (definition == null || inventory == null || (definition.RequiresResourceNode && !coversResource)) return false;
            for (int dx = 0; dx < definition.Width; dx++) for (int dy = 0; dy < definition.Height; dy++)
            { int px = x + dx, py = y + dy; if (px < 0 || py < 0 || px >= cells.GetLength(0) || py >= cells.GetLength(1) || cells[px, py] != null) return false; }
            if (!inventory.TrySpend(definition.CostId, definition.Cost)) return false;
            placed = new PlacedBuilding(definition, x, y);
            for (int dx = 0; dx < definition.Width; dx++) for (int dy = 0; dy < definition.Height; dy++) cells[x + dx, y + dy] = placed;
            Count++; return true;
        }
    }
}
