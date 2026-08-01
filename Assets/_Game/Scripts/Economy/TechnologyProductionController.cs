using UnityEngine;
using WasteCity.Building;
using WasteCity.Legacy;

namespace WasteCity.Economy
{
    public sealed class TechnologyProductionController : MonoBehaviour
    {
        [SerializeField] FormalEconomyController economy; [SerializeField] PlaceholderBuildingController buildings;
        [SerializeField] LegacyEffectsController legacyEffects;
        [SerializeField] LocalHasteController localHaste;
        private readonly ProductionProcess smelter=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.Alloy,1,6f));
        private readonly ProductionProcess assembler=new ProductionProcess(new ProductionRecipe(ResourceIds.Alloy,2,ResourceIds.Ammunition,2,6f));
        private void Update(){int smelters=0,assemblers=0;foreach(var runtime in Object.FindObjectsOfType<BuildingRuntime>()){if(!runtime.Construction.IsComplete)continue;int units=Mathf.RoundToInt(localHaste?.MultiplierFor(runtime)??1f);if(runtime.Definition.Id.Value=="core.building.smelter")smelters+=units;if(runtime.Definition.Id.Value=="core.building.assembler")assemblers+=units;}smelter.Tick(Time.deltaTime,economy.Inventory,legacyEffects?.Model?.ProductionUnits(smelters)??smelters);assembler.Tick(Time.deltaTime,economy.Inventory,legacyEffects?.Model?.ProductionUnits(assemblers)??assemblers);}
    }
}
