using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Persistence;
using WasteCity.Progression;

namespace WasteCity.Legacy
{
    public static class RewindAnchorRules
    {
        public static float AttentionAfterLoad(float current) => Mathf.Min(100f, Mathf.Max(0f, current) + 3f);
    }
    public sealed class RewindAnchorController : MonoBehaviour
    {
        [SerializeField] private LegacySelectionController legacy;
        [SerializeField] private FormalSaveController saves;
        [SerializeField] private FormalProgressionController progression;
        private FormalSaveData anchor;
        public bool HasAnchor => anchor != null;
        private bool Active => legacy?.Model?.Selected?.Id.Value == "core.legacy.rewind-anchor";
        private void Update()
        {
            if (!Active || Keyboard.current == null) return;
            if (Keyboard.current.f6Key.wasPressedThisFrame) anchor = saves.CaptureComplete();
            if (Keyboard.current.f10Key.wasPressedThisFrame && anchor != null) { float attention = RewindAnchorRules.AttentionAfterLoad(progression.Observation.Value); saves.ApplyComplete(anchor, true); progression.Observation.Restore(attention); }
        }
        private void OnGUI() { if (Active) GUI.Box(new Rect(Screen.width - 390f, 400f, 370f, 48f), HasAnchor ? "回溯锚点：F6 覆盖锚点 · F10 回溯（观测值 +3）" : "回溯锚点：按 F6 创建本局唯一锚点"); }
    }
}
