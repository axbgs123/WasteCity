using UnityEngine;
using WasteCity.Building;
using WasteCity.Legacy;
using WasteCity.City;
using WasteCity.World;
using System.Collections.Generic;
using System.Linq;
using WasteCity.Leader;
using System;
using WasteCity.Content;

namespace WasteCity.Economy
{
    public sealed class TechnologyProductionController : MonoBehaviour
    {
        [SerializeField] FormalEconomyController economy; [SerializeField] PlaceholderBuildingController buildings;
        [SerializeField] LegacyEffectsController legacyEffects;
        [SerializeField] LocalHasteController localHaste;
        [SerializeField] PlaceholderMobileCity city;
        [SerializeField] PlaceholderWorldView world;
        [SerializeField] FormalLeaderController leader;
        private readonly ProductionProcess smelter=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.Alloy,1,6f));
        private readonly ProductionProcess assembler=new ProductionProcess(new ProductionRecipe(ResourceIds.Alloy,2,ResourceIds.Ammunition,2,6f));
        private readonly ProductionProcess spiritFire=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.SpiritIron,1,8f));
        private readonly ProductionProcess artifactWorkshop=new ProductionProcess(new ProductionRecipe(ResourceIds.SpiritIron,2,ResourceIds.FlyingSword,2,8f));
        private readonly ProductionProcess boneSteel=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.BoneSteel,1,8f));
        private readonly ProductionProcess concentrate=new ProductionProcess(new ProductionRecipe(ResourceIds.Biomass,2,ResourceIds.BiomassConcentrate,1,8f));
        private readonly DualInputProductionProcess breeding=new DualInputProductionProcess(new DualInputProductionRecipe(ResourceIds.BoneSteel,1,ResourceIds.BiomassConcentrate,1,ResourceIds.BiologicalWeapon,1,8f));
        private readonly ProductionProcess resonance=new ProductionProcess(new ProductionRecipe(ResourceIds.Iron,2,ResourceIds.ResonanceMetal,1,8f));
        private readonly ProductionProcess psionicWorkshop=new ProductionProcess(new ProductionRecipe(ResourceIds.ResonanceMetal,2,ResourceIds.PsionicAmplifier,2,8f));
        private readonly PassiveProductionProcess spiritGathering=new PassiveProductionProcess(ResourceIds.EnergyCrystal,1,6f);
        private readonly DualInputProductionProcess alchemy=new DualInputProductionProcess(new DualInputProductionRecipe(ResourceIds.Biomass,1,ResourceIds.EnergyCrystal,1,ResourceIds.Elixir,1,10f));
        private readonly PassiveProductionProcess powerPlant=RouteCapstoneProductionCatalog.CreatePowerPlant();
        private readonly ProductionProcess metabolicFurnace=RouteCapstoneProductionCatalog.CreateMetabolicFurnace();
        private readonly PassiveProductionProcess consciousnessNetwork=RouteCapstoneProductionCatalog.CreateConsciousnessNetwork();
        private readonly Dictionary<BuildingRuntime,ResourceExtractionProcess> mines=new Dictionary<BuildingRuntime,ResourceExtractionProcess>();
        public int ActiveMines { get; private set; }
        public int DepletedMines { get; private set; }
        public int FullMines { get; private set; }
        public bool HasRunningProduction=>ActiveMines>0||new[]{smelter,assembler,spiritFire,artifactWorkshop,boneSteel,concentrate,resonance,psionicWorkshop,metabolicFurnace}.Any(value=>value.Status==ProductionStatus.Running)||breeding.Status==ProductionStatus.Running||alchemy.Status==ProductionStatus.Running||spiritGathering.Status==ProductionStatus.Running||powerPlant.Status==ProductionStatus.Running||consciousnessNetwork.Status==ProductionStatus.Running;
        public event Action<int> ProductionCompleted;

        private void Update()
        {
            var runtimes=UnityEngine.Object.FindObjectsOfType<BuildingRuntime>().Where(value=>value.Construction.IsComplete&&value.HasLogistics).ToArray();
            var activeMining=runtimes.Where(value=>value.Definition.Id.Value=="core.building.mining-station").ToArray();
            int productionUnits=legacyEffects?.Model?.ProductionUnits(activeMining.Length)??activeMining.Length;
            int bonusUnits=Mathf.Max(0,productionUnits-activeMining.Length);ActiveMines=DepletedMines=FullMines=0;
            float cityMultiplier=CityOperationalRules.ProductionMultiplier(city.Deployment.Mode);
            int completedCycles=0;for(int index=0;index<activeMining.Length;index++)
            {
                BuildingRuntime runtime=activeMining[index];
                if(!mines.TryGetValue(runtime,out var process)){process=new ResourceExtractionProcess(3f);mines.Add(runtime,process);}
                buildings.TryGetWorldCell(runtime,out int x,out int y);
                float delta=Time.deltaTime*cityMultiplier*(localHaste?.MultiplierFor(runtime)??1f);
                completedCycles+=process.Tick(delta,world.Model,x,y,economy.Inventory,1+(index==0?bonusUnits:0));
                if(process.Status==ExtractionStatus.Running)ActiveMines++;
                else if(process.Status==ExtractionStatus.Depleted)DepletedMines++;
                else if(process.Status==ExtractionStatus.OutputFull)FullMines++;
            }
            foreach(var stale in mines.Keys.Where(value=>value==null||!activeMining.Contains(value)).ToArray())mines.Remove(stale);
            int smelters=0,assemblers=0;
            int spiritFires=0,artifactWorkshops=0,spiritGatheringArrays=0,alchemyChambers=0,colonyPools=0,breedingChambers=0,resonanceFurnaces=0,psionicWorkshops=0,powerPlants=0,metabolicFurnaces=0,consciousnessNetworks=0;
            foreach(var runtime in runtimes)
            {
                int units=Mathf.RoundToInt(localHaste?.MultiplierFor(runtime)??1f);
                if(runtime.Definition.Id.Value=="core.building.smelter")smelters+=units;
                if(runtime.Definition.Id.Value=="core.building.assembler")assemblers+=units;
                if(runtime.Definition.Id.Value=="cultivation.building.spirit-fire-furnace")spiritFires+=units;
                if(runtime.Definition.Id.Value=="cultivation.building.artifact-workshop")artifactWorkshops+=units;
                if(runtime.Definition.Id.Value=="cultivation.building.spirit-gathering-array")spiritGatheringArrays+=units;
                if(runtime.Definition.Id.Value=="cultivation.building.alchemy-chamber")alchemyChambers+=units;
                if(runtime.Definition.Id.Value=="biological.building.colony-pool")colonyPools+=units;
                if(runtime.Definition.Id.Value=="biological.building.breeding-chamber")breedingChambers+=units;
                if(runtime.Definition.Id.Value=="psionics.building.resonance-furnace")resonanceFurnaces+=units;
                if(runtime.Definition.Id.Value=="psionics.building.workshop")psionicWorkshops+=units;
                if(runtime.Definition.Id.Value==BuildingCatalog.PowerPlant.Id.Value)powerPlants+=units;
                if(runtime.Definition.Id.Value==BuildingCatalog.MetabolicFurnace.Id.Value)metabolicFurnaces+=units;
                if(runtime.Definition.Id.Value==BuildingCatalog.ConsciousnessNetwork.Id.Value)consciousnessNetworks+=units;
            }
            float industryDelta=Time.deltaTime*cityMultiplier;
            completedCycles+=smelter.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(smelters)??smelters);
            completedCycles+=assembler.Tick(industryDelta*(leader?.AssemblerEfficiency??1f),economy.Inventory,legacyEffects?.Model?.ProductionUnits(assemblers)??assemblers);
            completedCycles+=spiritFire.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(spiritFires)??spiritFires);
            completedCycles+=artifactWorkshop.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(artifactWorkshops)??artifactWorkshops);
            completedCycles+=spiritGathering.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(spiritGatheringArrays)??spiritGatheringArrays,buildings.Research!=null&&buildings.Research.HasFormationReinforcement?1.5f:1f);
            completedCycles+=alchemy.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(alchemyChambers)??alchemyChambers);
            int colonyUnits=legacyEffects?.Model?.ProductionUnits(colonyPools)??colonyPools;completedCycles+=boneSteel.Tick(industryDelta,economy.Inventory,colonyUnits);completedCycles+=concentrate.Tick(industryDelta,economy.Inventory,colonyUnits);
            completedCycles+=breeding.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(breedingChambers)??breedingChambers);
            completedCycles+=resonance.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(resonanceFurnaces)??resonanceFurnaces);
            completedCycles+=psionicWorkshop.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(psionicWorkshops)??psionicWorkshops);
            completedCycles+=powerPlant.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(powerPlants)??powerPlants);
            completedCycles+=metabolicFurnace.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(metabolicFurnaces)??metabolicFurnaces);
            completedCycles+=consciousnessNetwork.Tick(industryDelta,economy.Inventory,legacyEffects?.Model?.ProductionUnits(consciousnessNetworks)??consciousnessNetworks);
            if(completedCycles>0)ProductionCompleted?.Invoke(completedCycles);
        }

        private void OnGUI()
        {
            GUI.Box(new Rect(Screen.width-520f,18f,500f,145f),$"多路线生产监控\n{BuildingCatalog.MiningStation.Name}：运行 {ActiveMines} / 枯竭 {DepletedMines} / 满仓 {FullMines}\n{RouteContentDisplayCatalog.RouteName(ContentRoute.Technology)} {BuildingCatalog.Smelter.Name} {StatusText(smelter.Status)} · {BuildingCatalog.Assembler.Name} {StatusText(assembler.Status)} · {BuildingCatalog.PowerPlant.Name} {StatusText(powerPlant.Status)}\n{RouteContentDisplayCatalog.RouteName(ContentRoute.Cultivation)} {BuildingCatalog.SpiritFireFurnace.Name} {StatusText(spiritFire.Status)} · {BuildingCatalog.ArtifactWorkshop.Name} {StatusText(artifactWorkshop.Status)} · {BuildingCatalog.SpiritGatheringArray.Name} {StatusText(spiritGathering.Status)}\n{RouteContentDisplayCatalog.RouteName(ContentRoute.BiologicalAscension)} {BuildingCatalog.ColonyPool.Name} {StatusText(boneSteel.Status)}/{StatusText(concentrate.Status)} · {BuildingCatalog.BreedingChamber.Name} {StatusText(breeding.Status)} · {BuildingCatalog.MetabolicFurnace.Name} {StatusText(metabolicFurnace.Status)}\n{RouteContentDisplayCatalog.RouteName(ContentRoute.Psionics)} {BuildingCatalog.ResonanceFurnace.Name} {StatusText(resonance.Status)} · {BuildingCatalog.PsionicWorkshop.Name} {StatusText(psionicWorkshop.Status)} · {BuildingCatalog.ConsciousnessNetwork.Name} {StatusText(consciousnessNetwork.Status)}");
        }

        public float[] CaptureProgress()=>new[]{smelter.Progress,assembler.Progress,spiritFire.Progress,artifactWorkshop.Progress,boneSteel.Progress,concentrate.Progress,breeding.Progress,resonance.Progress,psionicWorkshop.Progress,spiritGathering.Progress,alchemy.Progress,powerPlant.Progress,metabolicFurnace.Progress,consciousnessNetwork.Progress};
        public void RestoreProgress(float[] values){if(values==null)return;ProductionProcess[] singles={smelter,assembler,spiritFire,artifactWorkshop,boneSteel,concentrate};for(int i=0;i<singles.Length&&i<values.Length;i++)singles[i].Restore(values[i]);if(values.Length>6)breeding.Restore(values[6]);if(values.Length>7)resonance.Restore(values[7]);if(values.Length>8)psionicWorkshop.Restore(values[8]);if(values.Length>9)spiritGathering.Restore(values[9]);if(values.Length>10)alchemy.Restore(values[10]);if(values.Length>11)powerPlant.Restore(values[11]);if(values.Length>12)metabolicFurnace.Restore(values[12]);if(values.Length>13)consciousnessNetwork.Restore(values[13]);}

        private static string StatusText(ProductionStatus status)=>status==ProductionStatus.Running?"运行":status==ProductionStatus.MissingInput?"缺少输入":status==ProductionStatus.OutputFull?"输出已满":"无联网建筑";
    }
}
