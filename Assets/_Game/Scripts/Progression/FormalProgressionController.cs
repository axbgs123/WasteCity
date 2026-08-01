using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;
using WasteCity.Research;

namespace WasteCity.Progression
{
    public sealed class FormalProgressionController : MonoBehaviour
    {
        [SerializeField] private ResearchController research;
        [SerializeField] private PlaceholderBuildingController buildings;
        public ObservationModel Observation { get; } = new ObservationModel();
        public EraTrackModel EraTracks { get; } = new EraTrackModel();
        public CivilizationModel Civilization { get; } = new CivilizationModel();
        private void Start()
        {
            research.Model.Completed += OnResearchCompleted;
            buildings.BuildingPlaced += _ => Observation.Add("建造活动", 3f);
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
                if (Civilization.TryAdvance(research.Model.CompletedCount, buildings.PlacedCount)) Observation.Add("文明升阶", 15f);
        }
        private void OnResearchCompleted(ResearchDefinition definition)
        {
            Observation.Add($"完成研究：{definition.Name}", 6f); EraTracks.Add(definition.Route, 10);
            EraTracks.TryTrigger(definition.Route, research.Model.CompletedCount);
        }
        private void OnGUI()
        {
            GUI.Box(new Rect(Screen.width - 350f, 100f, 330f, 125f), $"文明等级 {Civilization.Level}\n异常观测值 {Observation.Value:0}/100\n时代轨道：机械 {EraTracks.Get(DevelopmentRoute.Technology)} · 修仙 {EraTracks.Get(DevelopmentRoute.Cultivation)}\n血肉 {EraTracks.Get(DevelopmentRoute.BiologicalAscension)} · 灵能 {EraTracks.Get(DevelopmentRoute.Psionics)}\nU 尝试主动升阶");
        }
    }
}
