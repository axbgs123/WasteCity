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
        public BuildingPlacement Placement { get; }
        public BuildingOperation Operation { get; }
        public BuildingDefinition(string id, string name, int width, int height, string costId, int cost, bool requiresNode = false, float buildSeconds = 5f, int maximumHealth = 300, int minimumPopulation = 0, string requiredResearchId = null, string requiredBuildingId = null, BuildingPlacement placement = BuildingPlacement.Ground, BuildingOperation operation = BuildingOperation.FortressOnly)
        { Id = new StableId(id); Name = name; Width = Math.Max(1, width); Height = Math.Max(1, height); CostId = costId; Cost = Math.Max(0, cost); RequiresResourceNode = requiresNode; BuildSeconds = Math.Max(.1f, buildSeconds); MaximumHealth = Math.Max(1, maximumHealth); MinimumPopulation=Math.Max(0,minimumPopulation);RequiredResearchId=requiredResearchId;RequiredBuildingId=requiredBuildingId;Placement=placement;Operation=operation; }
    }

    public static class BuildingCatalog
    {
        public static readonly BuildingDefinition MiningStation=new BuildingDefinition("core.building.mining-station", "采矿站", 2, 2, ResourceIds.Alloy, 4, true, 5f, 220, operation:BuildingOperation.TerrainDependent);
        public static readonly BuildingDefinition Housing=new BuildingDefinition("core.building.housing", "住房", 2, 2, ResourceIds.Alloy, 8, false, 5f, 250, placement:BuildingPlacement.Either, operation:BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition Warehouse=new BuildingDefinition("core.building.warehouse", "仓库", 2, 2, ResourceIds.Alloy, 8, false, 6f, 300, placement:BuildingPlacement.Either, operation:BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition Wall=new BuildingDefinition("core.building.wall", "城墙", 1, 1, ResourceIds.Stone, 2, false, 2f, 300);
        public static readonly BuildingDefinition ResearchStation=new BuildingDefinition("core.building.research-station", "研究站", 2, 2, ResourceIds.Iron, 6, false, 10f, 260, 200, placement:BuildingPlacement.Either, operation:BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition Smelter=new BuildingDefinition("core.building.smelter", "冶炼厂", 2, 2, ResourceIds.Stone, 6, false, 8f, 280, 0, "core.research.automated-machinery");
        public static readonly BuildingDefinition Assembler=new BuildingDefinition("core.building.assembler", "装配厂", 2, 2, ResourceIds.Alloy, 8, false, 8f, 260, 0, "core.research.precision-assembly", "core.building.smelter", BuildingPlacement.Either, BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition MachineGunTurret=new BuildingDefinition("core.building.machine-gun-turret", "机枪塔", 1, 1, ResourceIds.Alloy, 10, false, 10f, 250, 0, "core.research.automated-defense", "core.building.assembler");
        public static readonly BuildingDefinition HeavyMachineGunTurret=new BuildingDefinition("core.building.heavy-machine-gun-turret", "重型机枪塔", 1, 1, ResourceIds.Alloy, 20, false, 12f, 420);
        public static readonly BuildingDefinition PowerPlant=new BuildingDefinition("technology.building.power-plant","发电站",2,2,ResourceIds.Alloy,14,false,12f,320,1000,"core.research.thermal-engineering");
        public static readonly BuildingDefinition SpiritFireFurnace=new BuildingDefinition("cultivation.building.spirit-fire-furnace","灵火炉",2,2,ResourceIds.Stone,8,false,8f,280,0,"core.research.spirit-sensing");
        public static readonly BuildingDefinition ArtifactWorkshop=new BuildingDefinition("cultivation.building.artifact-workshop","炼器坊",2,2,ResourceIds.SpiritIron,6,false,10f,260,0,"core.research.artifact-crafting","cultivation.building.spirit-fire-furnace");
        public static readonly BuildingDefinition SwordArrayTower=new BuildingDefinition("cultivation.building.sword-array-tower","剑阵台",1,1,ResourceIds.SpiritIron,10,false,10f,240,0,"core.research.sword-array","cultivation.building.artifact-workshop");
        public static readonly BuildingDefinition SwordRidingPlatform=new BuildingDefinition("cultivation.building.sword-riding-platform","御剑台",1,1,ResourceIds.SpiritIron,20,false,12f,300);
        public static readonly BuildingDefinition ColonyPool=new BuildingDefinition("biological.building.colony-pool","菌落池",2,2,ResourceIds.Biomass,10,false,8f,320,0,"core.research.adaptive-tissue");
        public static readonly BuildingDefinition BreedingChamber=new BuildingDefinition("biological.building.breeding-chamber","培育室",2,2,ResourceIds.BoneSteel,6,false,10f,300,0,"core.research.bio-cultivation","biological.building.colony-pool");
        public static readonly BuildingDefinition SporeTower=new BuildingDefinition("biological.building.spore-tower","孢子塔",1,1,ResourceIds.BiomassConcentrate,10,false,10f,280,0,"core.research.spore-dispersal","biological.building.breeding-chamber");
        public static readonly BuildingDefinition MetabolicFurnace=new BuildingDefinition("biological.building.metabolic-furnace","代谢炉",2,2,ResourceIds.BoneSteel,12,false,12f,360,1000,"core.research.metabolic-acceleration");
        public static readonly BuildingDefinition ResonanceFurnace=new BuildingDefinition("psionics.building.resonance-furnace","共振炉",2,2,ResourceIds.Stone,8,false,8f,260,0,"core.research.mind-resonance");
        public static readonly BuildingDefinition PsionicWorkshop=new BuildingDefinition("psionics.building.workshop","灵能工坊",2,2,ResourceIds.ResonanceMetal,6,false,10f,240,0,"core.research.psionic-workshop","psionics.building.resonance-furnace",BuildingPlacement.Either,BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition MindSpire=new BuildingDefinition("psionics.building.mind-spire","心灵尖塔",1,1,ResourceIds.ResonanceMetal,10,false,12f,220,0,"core.research.mind-spire","psionics.building.workshop");
        public static readonly BuildingDefinition ConsciousnessNetwork=new BuildingDefinition("psionics.building.consciousness-network","意识网络",2,2,ResourceIds.ResonanceMetal,12,false,14f,300,1000,"core.research.consciousness-network",placement:BuildingPlacement.Either,operation:BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition LaserTower=new BuildingDefinition("core.building.laser-tower","激光塔",1,1,ResourceIds.Alloy,18,false,12f,280,0,"core.research.energy-weapons","core.building.assembler");
        public static readonly BuildingDefinition AcidTower=new BuildingDefinition("biological.building.acid-tower","酸液塔",1,1,ResourceIds.BiomassConcentrate,16,false,12f,320,0,"core.research.acid-spit","biological.building.breeding-chamber");
        public static readonly BuildingDefinition ShieldGenerator=new BuildingDefinition("psionics.building.shield-generator","护盾发生器",2,2,ResourceIds.PsionicAmplifier,12,false,14f,300,0,"core.research.mind-shield","psionics.building.workshop",BuildingPlacement.Either,BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition SpiritGatheringArray=new BuildingDefinition("cultivation.building.spirit-gathering-array","聚灵阵",2,2,ResourceIds.Stone,12,false,10f,260,1000,"core.research.spirit-gathering",operation:BuildingOperation.TerrainDependent);
        public static readonly BuildingDefinition AutomatedRepairBay=new BuildingDefinition("core.building.automated-repair-bay","自动维修机甲站",2,2,ResourceIds.Alloy,16,false,12f,300,0,"core.research.unmanned-systems","core.building.assembler",BuildingPlacement.Either,BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition AlchemyChamber=new BuildingDefinition("cultivation.building.alchemy-chamber","炼丹房",2,2,ResourceIds.SpiritIron,14,false,12f,280,0,"core.research.alchemy","cultivation.building.artifact-workshop",BuildingPlacement.Either,BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition PuppetWorkshop=new BuildingDefinition("cultivation.building.puppet-workshop","傀儡工坊",2,2,ResourceIds.Alloy,18,false,12f,300,0,"core.research.puppetry","cultivation.building.artifact-workshop",BuildingPlacement.Either,BuildingOperation.MobileAllowed);
        public static readonly BuildingDefinition BehemothPen=new BuildingDefinition("biological.building.behemoth-pen","巨兽栏",3,2,ResourceIds.BoneSteel,16,false,16f,420,0,"core.research.behemoth-breeding","biological.building.breeding-chamber");
        public static readonly BuildingDefinition[] All =
        {
            MiningStation,Housing,Warehouse,Wall,ResearchStation,Smelter,Assembler,MachineGunTurret,HeavyMachineGunTurret,PowerPlant,SpiritFireFurnace,ArtifactWorkshop,SwordArrayTower,SwordRidingPlatform,ColonyPool,BreedingChamber,SporeTower,MetabolicFurnace,ResonanceFurnace,PsionicWorkshop,MindSpire,ConsciousnessNetwork,LaserTower,AcidTower,ShieldGenerator,SpiritGatheringArray,AutomatedRepairBay,AlchemyChamber,PuppetWorkshop,BehemothPen
        };
        public static readonly BuildingDefinition[] BuildMenu={MiningStation,Housing,Warehouse,Wall,ResearchStation,Smelter,Assembler,MachineGunTurret,PowerPlant,SpiritFireFurnace,ArtifactWorkshop,SwordArrayTower,ColonyPool,BreedingChamber,SporeTower,MetabolicFurnace,ResonanceFurnace,PsionicWorkshop,MindSpire,ConsciousnessNetwork,LaserTower,AcidTower,ShieldGenerator,SpiritGatheringArray,AutomatedRepairBay,AlchemyChamber,PuppetWorkshop,BehemothPen};
    }

    public sealed class PlacedBuilding
    {
        public BuildingDefinition Definition { get; }
        public int X { get; }
        public int Y { get; }
        public BuildingSite Site { get; }
        public BuildingOrientation Orientation { get; }
        public PlacedBuilding(BuildingDefinition definition, int x, int y, BuildingSite site = BuildingSite.Ground)
            : this(definition, x, y, site, BuildingOrientation.North) { }
        public PlacedBuilding(BuildingDefinition definition, int x, int y, BuildingSite site, BuildingOrientation orientation)
        { Definition = definition; X = x; Y = y; Site = site; Orientation = orientation; }
    }

    public sealed class BuildingGrid
    {
        private readonly PlacedBuilding[,] cells;
        public int Count { get; private set; }
        public int Width { get; }
        public int Height { get; }
        public BuildingGrid(int width, int height)
        {
            Width = Math.Max(1, width);
            Height = Math.Max(1, height);
            cells = new PlacedBuilding[Width, Height];
        }
        public bool TryPlace(BuildingDefinition definition, int x, int y, ResourceInventory inventory, bool coversResource, out PlacedBuilding placed, BuildingSite site = BuildingSite.Ground)
        {
            return TryPlace(definition, x, y, inventory, coversResource, out placed, site, BuildingOrientation.North);
        }
        public bool TryPlace(BuildingDefinition definition, int x, int y, ResourceInventory inventory, bool coversResource, out PlacedBuilding placed, BuildingSite site, BuildingOrientation orientation)
        {
            placed = null; if (definition == null || inventory == null || !BuildingMobilityRules.SupportsSite(definition, site) || (definition.RequiresResourceNode && !coversResource)) return false;
            if (!CanOccupy(definition, x, y, orientation)) return false;
            if (!inventory.TrySpend(definition.CostId, definition.Cost)) return false;
            placed = new PlacedBuilding(definition, x, y, site, orientation);
            Occupy(placed);
            Count++; return true;
        }
        public bool Remove(PlacedBuilding placed)
        {
            if (placed == null) return false; bool found = false;
            for (int x = 0; x < cells.GetLength(0); x++) for (int y = 0; y < cells.GetLength(1); y++)
                if (ReferenceEquals(cells[x, y], placed)) { cells[x, y] = null; found = true; }
            if (found) Count--; return found;
        }
        public bool TryRestore(BuildingDefinition definition, int x, int y, out PlacedBuilding placed, BuildingSite site = BuildingSite.Ground)
        {
            return TryRestore(definition, x, y, out placed, site, BuildingOrientation.North);
        }
        public bool TryRestore(BuildingDefinition definition, int x, int y, out PlacedBuilding placed, BuildingSite site, BuildingOrientation orientation)
        {
            placed = null; if (definition == null || !BuildingMobilityRules.SupportsSite(definition, site)) return false;
            if (!CanOccupy(definition, x, y, orientation)) return false;
            placed = new PlacedBuilding(definition, x, y, site, orientation);
            Occupy(placed);
            Count++; return true;
        }
        public bool TryUpgrade(PlacedBuilding placed,BuildingDefinition target,ResourceInventory inventory,string costId,int cost,out PlacedBuilding upgraded)
        {
            upgraded=null;if(placed==null||target==null||inventory==null||!BuildingMobilityRules.SupportsSite(target,placed.Site)||placed.Definition.Width!=target.Width||placed.Definition.Height!=target.Height||!inventory.TrySpend(costId,cost))return false;
            upgraded=new PlacedBuilding(target,placed.X,placed.Y,placed.Site,placed.Orientation);for(int x=0;x<cells.GetLength(0);x++)for(int y=0;y<cells.GetLength(1);y++)if(ReferenceEquals(cells[x,y],placed))cells[x,y]=upgraded;return true;
        }
        public bool CanPlace(BuildingDefinition definition, int x, int y, BuildingSite site = BuildingSite.Ground)
        {
            return CanPlace(definition, x, y, site, BuildingOrientation.North);
        }
        public bool CanPlace(BuildingDefinition definition, int x, int y, BuildingSite site, BuildingOrientation orientation)
        {
            if (definition == null || !BuildingMobilityRules.SupportsSite(definition, site)) return false;
            return CanOccupy(definition, x, y, orientation);
        }
        public bool ContainsFootprint(BuildingDefinition definition, int x, int y, BuildingOrientation orientation = BuildingOrientation.North)
        {
            if (definition == null || !BuildingOrientationRules.IsValid(orientation)) return false;
            return x >= 0 && y >= 0
                && (long)x + BuildingOrientationRules.Width(definition, orientation) <= Width
                && (long)y + BuildingOrientationRules.Height(definition, orientation) <= Height;
        }
        public bool IsOccupied(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height && cells[x, y] != null;
        }
        private bool CanOccupy(BuildingDefinition definition, int x, int y, BuildingOrientation orientation)
        {
            if (!ContainsFootprint(definition, x, y, orientation)) return false;
            var width = BuildingOrientationRules.Width(definition, orientation);
            var height = BuildingOrientationRules.Height(definition, orientation);
            for (int dx = 0; dx < width; dx++) for (int dy = 0; dy < height; dy++)
                if (IsOccupied(x + dx, y + dy)) return false;
            return true;
        }
        private void Occupy(PlacedBuilding placed)
        {
            var width = BuildingOrientationRules.Width(placed.Definition, placed.Orientation);
            var height = BuildingOrientationRules.Height(placed.Definition, placed.Orientation);
            for (int dx = 0; dx < width; dx++) for (int dy = 0; dy < height; dy++)
                cells[placed.X + dx, placed.Y + dy] = placed;
        }
    }
}
