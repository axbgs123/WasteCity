using System;

namespace WasteCity.Core
{
    public sealed class GameSpeedModel
    {
        public float Speed { get; private set; } = 1f;
        private float speedBeforePause = 1f;
        public void Set(float speed) { Speed = Math.Max(0f, Math.Min(2f, speed)); if (Speed > 0f) speedBeforePause = Speed; }
        public void TogglePause() => Speed = Speed > 0f ? 0f : speedBeforePause;
    }
}
