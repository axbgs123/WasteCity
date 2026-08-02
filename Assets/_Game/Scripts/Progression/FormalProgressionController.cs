using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;
using WasteCity.Research;
using WasteCity.Legacy;
using System.Linq;
using WasteCity.Combat;
using WasteCity.Economy;
using WasteCity.Content;

namespace WasteCity.Progression
{
    public sealed class FormalProgressionController : MonoBehaviour
    {
        [SerializeField] private ResearchController research;
        [SerializeField] private PlaceholderBuildingController buildings;
        [SerializeField] private LegacySelectionController legacy;
        [SerializeField] private FormalCombatController combat;
        [SerializeField] private TechnologyProductionController production;
        public ObservationModel Observation { get; } = new ObservationModel();
        public EraTrackModel EraTracks { get; } = new EraTrackModel();
        public CivilizationModel Civilization { get; } = new CivilizationModel();
        public bool BossDefeated { get; private set; }
        public bool CanAdvance=>CivilizationAdvanceRequirements.Meets(research.Model.IsCompleted(new StableId("core.research.legacy-analysis")),buildings.CompletedCount("core.building.machine-gun-turret"),BossDefeated,production.HasRunningProduction);
        private void Start()
        {
            research.Model.Completed += OnResearchCompleted;
            buildings.BuildingPlaced += _ => Observation.Add("建造活动", 3f);
            combat.EnemyArchetypeDefeated+=OnEnemyDefeated;
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
                if (Civilization.TryAdvanceFormal(CanAdvance)) Observation.Add("文明升阶", 15f);
        }
        private void OnEnemyDefeated(EnemyArchetype archetype){if(archetype==EnemyArchetype.CrystalBroodmother)BossDefeated=true;}
        public void RestoreBossDefeated(bool value)=>BossDefeated=value;
        private void OnResearchCompleted(ResearchDefinition definition)
        {
            Observation.Add($"完成研究：{definition.Name}", 6f); EraTracks.Add(definition.Route, 10);
            EraTracks.TryTrigger(definition.Route, research.Model.CompletedCount);
        }
        private void OnGUI()
        {
            bool transparent = legacy?.Model?.Selected?.Id.Value == LegacyEffectModel.CausalTransparency;
            string reasons = transparent ? $"\n原因：{string.Join(" / ", Observation.RecentReasons.ToArray())}\n阈值预警：30 / 60 / 90" : "";
            string requirements=$"遗产解析 {(research.Model.IsCompleted(new StableId("core.research.legacy-analysis"))?"✓":"×")} · 炮塔 {buildings.CompletedCount("core.building.machine-gun-turret")}/2 · 母体 {(BossDefeated?"✓":"×")} · 生产 {(production.HasRunningProduction?"✓":"×")}";
            GUI.Box(new Rect(Screen.width - 410f, 100f, 390f, transparent ? 205f : 155f), $"文明等级 {Civilization.Level}\n异常观测值 {Observation.Value:0}/100\n时代轨道：机械 {EraTracks.Get(DevelopmentRoute.Technology)} · 修仙 {EraTracks.Get(DevelopmentRoute.Cultivation)}\n血肉 {EraTracks.Get(DevelopmentRoute.BiologicalAscension)} · 灵能 {EraTracks.Get(DevelopmentRoute.Psionics)}\n升阶条件：{requirements}\nU 主动升阶{reasons}");
        }
    }
}
