using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Narrative;
using WasteCity.Progression;
using WasteCity.World;

namespace WasteCity.Core
{
    public sealed class FormalSessionStatisticsController:MonoBehaviour
    {
        [SerializeField] private FormalCombatController combat;
        [SerializeField] private PlaceholderBuildingController buildings;
        [SerializeField] private TechnologyProductionController production;
        [SerializeField] private RescueSiteController rescue;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalGuidanceController guidance;
        [SerializeField] private FormalProgressionController progression;
        public SessionStatisticsModel Model { get; }=new SessionStatisticsModel();
        private void Start(){combat.EnemyDefeated+=_=>Model.AddKill();buildings.BuildingRemoved+=_=>Model.AddBuildingLoss();production.ProductionCompleted+=Model.AddProduction;rescue.Rescued+=(index,immediate)=>Model.AddRescue(immediate);city.Deployment.Changed+=OnCityModeChanged;}
        private void Update()=>Model.Tick(Time.deltaTime,progression.Observation.Value);
        private void OnCityModeChanged(CityMode mode){if(guidance.Model.Stage==GuidanceStage.Broodmother&&(mode==CityMode.Packing||mode==CityMode.Mobile))Model.MarkRetreat();}
        public void Restore(float elapsed,int kills,float highest,int productionCycles,int buildingLosses,int rescues,int delayedRescues,bool retreated)=>Model.Restore(elapsed,kills,highest,productionCycles,buildingLosses,rescues,delayedRescues,retreated);
    }
}
