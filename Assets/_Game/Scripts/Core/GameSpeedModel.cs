using System;
using System.Collections.Generic;

namespace WasteCity.Core
{
    public enum GamePauseReason
    {
        User,
        Title,
        Session,
        Defeat,
        Advancement,
        SystemMenu
    }

    public sealed class GameSpeedModel
    {
        private readonly HashSet<GamePauseReason> pauseReasons = new HashSet<GamePauseReason>();
        private float requestedSpeed = 1f;
        public float RequestedSpeed => requestedSpeed;
        public float Speed => pauseReasons.Count == 0 ? requestedSpeed : 0f;
        public void Set(float speed) => requestedSpeed = Math.Max(0f, Math.Min(2f, speed));
        public void TogglePause() => SetPaused(GamePauseReason.User, !pauseReasons.Contains(GamePauseReason.User));
        public void SetPaused(GamePauseReason reason, bool paused)
        {
            if (paused) pauseReasons.Add(reason);
            else pauseReasons.Remove(reason);
        }
        public bool IsPaused(GamePauseReason reason) => pauseReasons.Contains(reason);
    }
}
