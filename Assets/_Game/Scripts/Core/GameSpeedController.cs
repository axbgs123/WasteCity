using UnityEngine;
using UnityEngine.InputSystem;

namespace WasteCity.Core
{
    public sealed class GameSpeedController : MonoBehaviour
    {
        public GameSpeedModel Model { get; } = new GameSpeedModel();
        private void Update()
        {
            if (Keyboard.current == null) return;
            if (Keyboard.current.spaceKey.wasPressedThisFrame) Model.TogglePause();
            if (Keyboard.current.leftBracketKey.wasPressedThisFrame) Model.Set(1f);
            if (Keyboard.current.rightBracketKey.wasPressedThisFrame) Model.Set(2f);
            Time.timeScale = Model.Speed;
        }
        private void OnDestroy() { Time.timeScale = 1f; }
    }
}
