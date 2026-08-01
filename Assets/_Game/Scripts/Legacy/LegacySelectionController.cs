using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.World;

namespace WasteCity.Legacy
{
    public sealed class LegacySelectionController : MonoBehaviour
    {
        [SerializeField] private int worldSeed = 8128;
        public LegacySelectionModel Model { get; private set; }
        private void Awake() => Model = new LegacySelectionModel(new WorldSeed(worldSeed));
        private void Update()
        {
            if (Keyboard.current == null || Model.Selected != null) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) Model.Select(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) Model.Select(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) Model.Select(2);
        }
        private void OnGUI()
        {
            if (Model.Selected != null)
            {
                GUI.Box(new Rect(Screen.width - 330f, 18f, 312f, 70f), $"命轨 Lv.1：{Model.Selected.DisplayName}\n{Model.Selected.RuleSummary}"); return;
            }
            GUI.Box(new Rect(Screen.width * 0.18f, Screen.height * 0.22f, Screen.width * 0.64f, 245f), "遗产响应 · 命轨三选一\n\n" +
                $"[1] {Model.Choices[0].DisplayName} — {Model.Choices[0].RuleSummary}\n\n" +
                $"[2] {Model.Choices[1].DisplayName} — {Model.Choices[1].RuleSummary}\n\n" +
                $"[3] {Model.Choices[2].DisplayName} — {Model.Choices[2].RuleSummary}");
        }
    }
}
