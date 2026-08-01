using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;
using WasteCity.Core;

namespace WasteCity.Legacy
{
    public sealed class LocalHasteController : MonoBehaviour, ILocalTimeScaleSource
    {
        [SerializeField] private LegacySelectionController legacy;
        [SerializeField] private FormalGameClockController clock;
        [SerializeField] private PlaceholderBuildingController buildings;
        public LocalHasteModel Model { get; } = new LocalHasteModel();
        public BuildingRuntime Target { get; private set; }
        private bool LegacyActive => legacy?.Model?.Selected?.Id.Value == "core.legacy.local-haste";
        private void Update()
        {
            Model.Tick(Time.deltaTime, clock.Model.Day);
            if (!LegacyActive) { Model.SetActive(false); Target = null; return; }
            if (Target == null) Model.SetActive(false);
            if (Keyboard.current != null && Mouse.current != null && Keyboard.current.hKey.wasPressedThisFrame) ToggleAtMouse();
        }
        private void ToggleAtMouse()
        {
            BuildingRuntime picked = buildings.FindNearest(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()), 1.5f);
            if (picked == null) { Target = null; Model.SetActive(false); return; }
            if (Target == picked && Model.Active) { Model.SetActive(false); return; }
            Target = picked; Model.SetActive(true);
        }
        public float MultiplierFor(BuildingRuntime runtime) => LegacyActive && runtime == Target ? Model.Multiplier : 1f;
        public void Restore(int day, float remaining, bool active, int x, int y) { Target = buildings.FindAtGrid(x, y); Model.Restore(day, remaining, active && Target != null); }
        private void OnGUI() { if (LegacyActive) GUI.Box(new Rect(Screen.width - 390f, 458f, 370f, 55f), $"局部时加：{Model.Remaining:0.0}/60 秒 · 5×\n{(Target == null ? "鼠标指向建筑按 H" : $"目标 {Target.Definition.Name} · H 切换")}"); }
    }
}
