using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Legacy;
using WasteCity.Population;
using WasteCity.Progression;
using WasteCity.Combat;
using WasteCity.Building;
using WasteCity.World;
using WasteCity.Core;
using WasteCity.Research;
using WasteCity.Narrative;
namespace WasteCity.Persistence
{
    public sealed class FormalSaveController : MonoBehaviour
    {
        [SerializeField] PlaceholderMobileCity city; [SerializeField] FormalEconomyController economy; [SerializeField] LegacySelectionController legacy;
        [SerializeField] FormalPopulationController population; [SerializeField] FormalProgressionController progression; [SerializeField] HealthComponent cityHealth;
        [SerializeField] PlaceholderBuildingController buildings;
        [SerializeField] RescueSiteController rescueSites;
        [SerializeField] FormalGameClockController clock; [SerializeField] ForesightFlashController foresight;
        [SerializeField] LocalHasteController localHaste;
        [SerializeField] SpatialTemplateController spatialTemplate;
        [SerializeField] PlaceholderWorldView worldView; [SerializeField] TerritoryCacheController territory;
        [SerializeField] ResearchController research;
        [SerializeField] FormalCombatController combat;
        [SerializeField] FormalGuidanceController guidance;
        [SerializeField] FormalAdvancementController advancement;
        string SavePath => Path.Combine(Application.persistentDataPath,"formal-world.json");
        public bool HasSave=>File.Exists(SavePath)||File.Exists(SavePath+".bak");
        void Update(){if(Keyboard.current==null)return;if(Keyboard.current.f5Key.wasPressedThisFrame)Save();if(Keyboard.current.f9Key.wasPressedThisFrame)Load();}
        public void Save(){Directory.CreateDirectory(Application.persistentDataPath);if(File.Exists(SavePath))File.Copy(SavePath,SavePath+".bak",true);File.WriteAllText(SavePath,FormalSaveCodec.Encode(Capture()));}
        public FormalSaveData Capture(){var i=economy.Inventory;int hx=-1,hy=-1;buildings.TryGetGrid(localHaste.Target,out hx,out hy);return new FormalSaveData{worldSeed=8128,cityX=city.transform.position.x,cityY=city.transform.position.y,iron=i.Get(ResourceIds.Iron),energyCrystal=i.Get(ResourceIds.EnergyCrystal),stone=i.Get(ResourceIds.Stone),biomass=i.Get(ResourceIds.Biomass),water=i.Get(ResourceIds.Water),alloy=i.Get(ResourceIds.Alloy),ammunition=i.Get(ResourceIds.Ammunition),population=population.Model.Current,populationCapacity=population.Model.Capacity,observation=progression.Observation.Value,civilizationLevel=progression.Civilization.Level,cityHealth=cityHealth.Value.Current,legacyPathId=legacy.Model.Selected?.Id.Value,legacyLevel=legacy.Model.Level,buildings=buildings.CaptureSnapshots(),rescuedSites=rescueSites.Capture(),day=clock.Model.Day,secondsIntoDay=clock.Model.SecondsIntoDay,foresightFlashedDay=foresight.Model?.LastFlashedDay??0,hastePoolDay=localHaste.Model.PoolDay,hasteRemaining=localHaste.Model.Remaining,hasteActive=localHaste.Model.Active,hasteTargetX=hx,hasteTargetY=hy,spatialTemplate=spatialTemplate.Model.Capture(),worldResourceAmounts=worldView.Model.CaptureResourceAmounts(),worldRevealed=worldView.Model.CaptureRevealed(),territoryActivated=territory.Activated,territoryProgress=territory.Extraction.Progress,territoryLocalResources=territory.CaptureLocal(),completedResearchIds=research.Model.CaptureCompleted(),activeResearchId=research.Model.Active?.Id.Value,researchRemaining=research.Model.Remaining,cityMode=(int)city.Deployment.Mode,deploymentRemaining=city.Deployment.Remaining,wave=combat.CaptureWave(),enemies=combat.CaptureEnemies(),guidanceStage=(int)guidance.Model.Stage,bossDefeated=progression.BossDefeated,advancementStage=(int)advancement.Model.Stage,advancementRemaining=advancement.Model.Remaining};}
        public bool Load(){var d=Read(SavePath)??Read(SavePath+".bak");return d!=null&&Apply(d,false);}
        public bool Apply(FormalSaveData d,bool preserveObservation){if(d==null)return false;city.transform.position=new Vector3(d.cityX,d.cityY,city.transform.position.z);if(d.schema>=11)city.RestoreDeployment((CityMode)d.cityMode,d.deploymentRemaining);if(!string.IsNullOrEmpty(d.legacyPathId))legacy.Model.Restore(d.legacyPathId,d.schema>=16?d.legacyLevel:1);var i=economy.Inventory;if(d.legacyPathId==LegacyEffectModel.VoidDebt)i.SetDebtLimit(1000000);i.Restore(ResourceIds.Iron,d.iron);i.Restore(ResourceIds.EnergyCrystal,d.energyCrystal);i.Restore(ResourceIds.Stone,d.stone);i.Restore(ResourceIds.Biomass,d.biomass);i.Restore(ResourceIds.Water,d.water);i.Restore(ResourceIds.Alloy,d.alloy);i.Restore(ResourceIds.Ammunition,d.ammunition);if(d.schema>=2){population.Restore(d.population,d.populationCapacity);if(!preserveObservation)progression.Observation.Restore(d.observation);progression.Civilization.Restore(d.civilizationLevel);cityHealth.Value.Restore(d.cityHealth);}if(d.schema>=10)research.Model.Restore(d.completedResearchIds,d.activeResearchId,d.researchRemaining);if(d.schema>=3)buildings.RestoreSnapshots(d.buildings);if(d.schema>=4)rescueSites.Restore(d.rescuedSites);if(d.schema>=6){clock.Model.Restore(d.day,d.secondsIntoDay);foresight.Restore(d.foresightFlashedDay);}if(d.schema>=7)localHaste.Restore(d.hastePoolDay,d.hasteRemaining,d.hasteActive,d.hasteTargetX,d.hasteTargetY);if(d.schema>=8)spatialTemplate.Model.Restore(d.spatialTemplate);if(d.schema>=9){worldView.Restore(d.worldResourceAmounts,d.worldRevealed);territory.Restore(d.territoryActivated,d.territoryProgress,d.territoryLocalResources);}if(d.schema>=12)combat.Restore(d.wave,d.enemies);if(d.schema>=15)progression.RestoreBossDefeated(d.bossDefeated);if(d.schema>=14){int stage=d.guidanceStage;if(d.schema<16&&stage==(int)GuidanceStage.Complete&&d.civilizationLevel<2)stage=(int)GuidanceStage.Advancement;guidance.Restore(stage);}if(d.schema>=16)advancement.Restore(d.advancementStage,d.advancementRemaining);return true;}
        static FormalSaveData Read(string path)=>File.Exists(path)?FormalSaveCodec.Decode(File.ReadAllText(path)):null;
    }
}
