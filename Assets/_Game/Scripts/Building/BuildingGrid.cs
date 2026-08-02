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
        public float BuildSeconds { get; }
        public int MaximumHealth { get; }
        public int MinimumPopulation { get; }
        public string RequiredResearchId { get; }
        public string RequiredBuildingId { get; }
        public BuildingDefinition(string id, string name, int width, int height, string costId, int cost, bool requiresNode = false, float buildSeconds = 5f, int maximumHealth = 300, int minimumPopulation = 0, string requiredResearchId = null, string requiredBuildingId = null)
        { Id = new StableId(id); Name = name; Width = Math.Max(1, width); Height = Math.Max(1, height); CostId = costId; Cost = Math.Max(0, cost); RequiresResourceNode = requiresNode; BuildSeconds = Math.Max(.1f, buildSeconds); MaximumHealth = Math.Max(1, maximumHealth); MinimumPopulation=Math.Max(0,minimumPopulation);RequiredResearchId=requiredResearchId;RequiredBuildingId=requiredBuildingId; }
    }

    public static class BuildingCatalog
    {
        public static readonly BuildingDefinition MiningStation=new BuildingDefinition("core.building.mining-station", "采矿站", 2, 2, ResourceIds.Alloy, 4, true, 5f, 220);
        public static readonly BuildingDefinition Housing=new BuildingDefinition("core.building.housing", "住房", 2, 2, ResourceIds.Alloy, 8, false, 5f, 250);
        public static readonly BuildingDefinition Warehouse=new BuildingDefinition("core.building.warehouse", "仓库", 2, 2, ResourceIds.Alloy, 8, false, 6f, 300);
        public static readonly BuildingDefinition Wall=new BuildingDefinition("core.building.wall", "城墙", 1, 1, ResourceIds.Stone, 2, false, 2f, 300);
        public static readonly BuildingDefinition ResearchStation=new BuildingDefinition("core.building.research-station", "研究站", 2, 2, ResourceIds.Iron, 6, false, 10f, 260, 200);
        public static readonly BuildingDefinition Smelter=new BuildingDefinition("core.building.smelter", "冶炼厂", 2, 2, ResourceIds.Stone, 6, false, 8f, 280, 0, "core.research.automated-machinery");
        public static readonly BuildingDefinition Assembler=new BuildingDefinition("core.building.assembler", "装配厂", 2, 2, ResourceIds.Alloy, 8, false, 8f, 260, 0, "core.research.precision-assembly", "core.building.smelter");
        public static readonly BuildingDefinition MachineGunTurret=new BuildingDefinition("core.building.machine-gun-turret", "机枪塔", 1, 1, ResourceIds.Alloy, 10, false, 10f, 250, 0, "core.research.automated-defense", "core.building.assembler");
        public static readonly BuildingDefinition HeavyMachineGunTurret=new BuildingDefinition("core.building.heavy-machine-gun-turret", "重型机枪塔", 1, 1, ResourceIds.Alloy, 20, false, 12f, 420);
        public static readonly BuildingDefinition[] All =
        {
            MiningStation,Housing,Warehouse,Wall,ResearchStation,Smelter,Assembler,MachineGunTurret,HeavyMachineGunTurret
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
        public bool Remove(PlacedBuilding placed)
        {
            if (placed == null) return false; bool found = false;
            for (int x = 0; x < cells.GetLength(0); x++) for (int y = 0; y < cells.GetLength(1); y++)
                if (ReferenceEquals(cells[x, y], placed)) { cells[x, y] = null; found = true; }
            if (found) Count--; return found;
        }
        public bool TryRestore(BuildingDefinition definition, int x, int y, out PlacedBuilding placed)
        {
            placed = null; if (definition == null) return false;
            for (int dx = 0; dx < definition.Width; dx++) for (int dy = 0; dy < definition.Height; dy++)
            { int px = x + dx, py = y + dy; if (px < 0 || py < 0 || px >= cells.GetLength(0) || py >= cells.GetLength(1) || cells[px, py] != null) return false; }
            placed = new PlacedBuilding(definition, x, y);
            for (int dx = 0; dx < definition.Width; dx++) for (int dy = 0; dy < definition.Height; dy++) cells[x + dx, y + dy] = placed;
            Count++; return true;
        }
        public bool TryUpgrade(PlacedBuilding placed,BuildingDefinition target,ResourceInventory inventory,string costId,int cost,out PlacedBuilding upgraded)
        {
            upgraded=null;if(placed==null||target==null||inventory==null||placed.Definition.Width!=target.Width||placed.Definition.Height!=target.Height||!inventory.TrySpend(costId,cost))return false;
            upgraded=new PlacedBuilding(target,placed.X,placed.Y);for(int x=0;x<cells.GetLength(0);x++)for(int y=0;y<cells.GetLength(1);y++)if(ReferenceEquals(cells[x,y],placed))cells[x,y]=upgraded;return true;
        }
        public bool CanPlace(BuildingDefinition definition, int x, int y)
        {
            if (definition == null) return false;
            for (int dx = 0; dx < definition.Width; dx++) for (int dy = 0; dy < definition.Height; dy++)
            { int px=x+dx,py=y+dy;if(px<0||py<0||px>=cells.GetLength(0)||py>=cells.GetLength(1)||cells[px,py]!=null)return false; }
            return true;
        }
    }
}
