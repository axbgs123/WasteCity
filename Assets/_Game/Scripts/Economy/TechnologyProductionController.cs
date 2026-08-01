using UnityEngine;
using WasteCity.Building;

namespace WasteCity.Economy
{
    public sealed class TechnologyProductionController : MonoBehaviour
    {
        [SerializeField] FormalEconomyController economy; [SerializeField] PlaceholderBuildingController buildings;
        private readonly ProductionProcess smelter=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.Alloy,1,6f));
        private readonly ProductionProcess assembler=new ProductionProcess(new ProductionRecipe(ResourceIds.Alloy,2,ResourceIds.Ammunition,2,6f));
        private int smelters,assemblers;
        private void Start()=>buildings.BuildingPlaced+=OnBuilding;
        private void OnBuilding(BuildingDefinition d){if(d.Id.Value=="core.building.smelter")smelters++;if(d.Id.Value=="core.building.assembler")assemblers++;}
        private void Update(){smelter.Tick(Time.deltaTime,economy.Inventory,smelters);assembler.Tick(Time.deltaTime,economy.Inventory,assemblers);}
    }
}
