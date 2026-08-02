using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;

namespace WasteCity.Narrative
{
    public sealed class FormalGuidanceController:MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private PlaceholderBuildingController buildings;
        [SerializeField] private FormalCombatController combat;
        private Vector2 startPosition;
        public GuidanceFlowModel Model { get; }=new GuidanceFlowModel();
        public string CurrentObjective=>Model.Objective;
        private void Start()
        {
            startPosition=city.transform.position;city.Deployment.Changed+=OnCityModeChanged;buildings.BuildingPlaced+=OnBuildingPlaced;combat.WaveCompleted+=Model.SignalWaveCompleted;combat.EnemyArchetypeDefeated+=OnEnemyDefeated;
        }
        private void Update(){if(Model.Stage==GuidanceStage.Awakening&&Vector2.Distance(startPosition,city.transform.position)>=3)Model.SignalMoved();}
        private void OnCityModeChanged(CityMode mode){if(mode==CityMode.Fortress)Model.SignalFortress();}
        private void OnBuildingPlaced(BuildingDefinition definition){if(definition.Id.Value=="core.building.mining-station")Model.SignalMiningBuilt();if(definition.Id.Value=="core.building.machine-gun-turret")Model.SignalTurretBuilt();}
        private void OnEnemyDefeated(EnemyArchetype archetype){if(archetype==EnemyArchetype.CrystalBroodmother)Model.SignalBossDefeated();}
        public void Restore(int stage)=>Model.Restore(stage);
        private void OnGUI(){GUI.Box(new Rect(18,105,430,78),$"当前目标 · {Model.Title}\n{Model.Objective}\n移动 WASD · 展开/收起 F · 建造 B · 研究 K · 存档 F5 / 读档 F9");}
    }
}
