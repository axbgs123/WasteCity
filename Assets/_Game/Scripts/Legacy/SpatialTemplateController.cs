using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;

namespace WasteCity.Legacy
{
    public sealed class SpatialTemplateController : MonoBehaviour
    {
        [SerializeField] private LegacySelectionController legacy;
        [SerializeField] private PlaceholderBuildingController buildings;
        public SpatialTemplateModel Model { get; } = new SpatialTemplateModel();
        public string LastResult { get; private set; }
        private bool Active => legacy?.Model?.Selected?.Id.Value == "core.legacy.spatial-template";
        private void Update()
        {
            if (!Active || Keyboard.current == null || Mouse.current == null || !Keyboard.current.tKey.wasPressedThisFrame) return;
            Vector2 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()); buildings.WorldToGrid(point, out int x, out int y);
            if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed) LastResult = Model.Record(buildings.CaptureTemplate(x, y)) ? $"已录制 3×3 模板：{Model.Entries.Count} 座建筑" : "录制失败：区域内没有完整包含的建筑";
            else LastResult = !Model.HasTemplate ? "尚未录制模板：按 Ctrl+T 录制" : buildings.TryStampTemplate(Model.Entries, x, y) ? "空间模板复刻成功" : "复刻失败：空间、资源或矿脉条件不足";
        }
        private void OnGUI() { if (Active) GUI.Box(new Rect(Screen.width - 390f, 520f, 370f, 66f), $"空间模板 Lv.1：Ctrl+T 录制 3×3 · T 复刻\n{(string.IsNullOrEmpty(LastResult) ? "鼠标位置作为区域左下角" : LastResult)}"); }
    }
}
