using UnityEngine;
using WasteCity.Building;
using WasteCity.Legacy;
using WasteCity.City;
using WasteCity.World;
using System.Collections.Generic;
using System.Linq;

namespace WasteCity.Economy
{
    public sealed class TechnologyProductionController : MonoBehaviour
    {
        [SerializeField] FormalEconomyController economy; [SerializeField] PlaceholderBuildingController buildings;
        [SerializeField] LegacyEffectsController legacyEffects;
        [SerializeField] LocalHasteController localHaste;
        [SerializeField] PlaceholderMobileCity city;
        [SerializeField] PlaceholderWorldView world;
        private readonly ProductionProcess smelter=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.Alloy,1,6f));
        private readonly ProductionProcess assembler=new ProductionProcess(new ProductionRecipe(ResourceIds.Alloy,2,ResourceIds.Ammunition,2,6f));
        private readonly Dictionary<BuildingRuntime,ResourceExtractionProcess> mines=new Dictionary<BuildingRuntime,ResourceExtractionProcess>();
        public int ActiveMines { get; private set; }
        public int DepletedMines { get; private set; }
        public int FullMines { get; private set; }

        private void Update()
        {
            var runtimes=Object.FindObjectsOfType<BuildingRuntime>().Where(value=>value.Construction.IsComplete&&value.HasLogistics).ToArray();
            var activeMining=runtimes.Where(value=>value.Definition.Id.Value=="core.building.mining-station").ToArray();
            int productionUnits=legacyEffects?.Model?.ProductionUnits(activeMining.Length)??activeMining.Length;
            int bonusUnits=Mathf.Max(0,productionUnits-activeMining.Length);ActiveMines=DepletedMines=FullMines=0;
            float cityMultiplier=CityOperationalRules.ProductionMultiplier(city.Deployment.Mode);
            for(int index=0;index<activeMining.Length;index++)
            {
                BuildingRuntime runtime=activeMining[index];
                if(!mines.TryGetValue(runtime,out var process)){process=new ResourceExtractionProcess(3f);mines.Add(runtime,process);}
                buildings.TryGetWorldCell(runtime,out int x,out int y);
                float delta=Time.deltaTime*cityMultiplier*(localHaste?.MultiplierFor(runtime)??1f);
                process.Tick(delta,world.Model,x,y,economy.Inventory,1+(index==0?bonusUnits:0));
                if(process.Status==ExtractionStatus.Running)ActiveMines++;
                else if(process.Status==ExtractionStatus.Depleted)DepletedMines++;
                else if(process.Status==ExtractionStatus.OutputFull)FullMines++;
            }
            foreach(var stale in mines.Keys.Where(value=>value==null||!activeMining.Contains(value)).ToArray())mines.Remove(stale);
            int smelters=0,assemblers=0;
            foreach(var runtime in runtimes)
            {
                int units=Mathf.RoundToInt(localHaste?.MultiplierFor(runtime)??1f);
                if(runtime.Definition.Id.Value=="core.building.smelter")smelters+=units;
                if(runtime.Definition.Id.Value=="core.building.assembler")assemblers+=units;
            }
            float industryDelta=Time.deltaTime*cityMultiplier;
            smelter.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(smelters)??smelters);
            assembler.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(assemblers)??assemblers);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(Screen.width-410f,18f,390f,76f),$"生产链占位监控\n采矿：运行 {ActiveMines} / 枯竭 {DepletedMines} / 满仓 {FullMines}\n冶炼：{StatusText(smelter.Status)} · 装配：{StatusText(assembler.Status)}");
        }

        private static string StatusText(ProductionStatus status)=>status==ProductionStatus.Running?"运行":status==ProductionStatus.MissingInput?"缺少输入":status==ProductionStatus.OutputFull?"输出已满":"无联网建筑";
    }
}
