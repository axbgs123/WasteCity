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
        private readonly HashSet<GamePauseReason> pauseReasons =
            new HashSet<GamePauseReason>();
        private float requestedSpeed = 1f;
        private float lastNonZeroSpeed = 1f;
        private ulong revision;

        public float RequestedSpeed => requestedSpeed;
        public float LastNonZeroSpeed => lastNonZeroSpeed;
        public float Speed => pauseReasons.Count == 0 ? requestedSpeed : 0f;
        public ulong Revision => revision;

        public void Set(float speed)
        {
            float normalized = Math.Max(0f, Math.Min(2f, speed));
            float nextLastNonZero = normalized > 0f
                ? normalized
                : lastNonZeroSpeed;
            if (requestedSpeed == normalized &&
                lastNonZeroSpeed == nextLastNonZero)
            {
                return;
            }
            requestedSpeed = normalized;
            lastNonZeroSpeed = nextLastNonZero;
            revision++;
        }

        public void TogglePause()
        {
            SetPaused(
                GamePauseReason.User,
                !pauseReasons.Contains(GamePauseReason.User));
        }

        public void SetPaused(GamePauseReason reason, bool paused)
        {
            bool changed = paused
                ? pauseReasons.Add(reason)
                : pauseReasons.Remove(reason);
            if (changed) revision++;
        }

        public bool IsPaused(GamePauseReason reason) => pauseReasons.Contains(reason);

        public bool TryRestoreSpeedState(
            float requested,
            float lastNonZero,
            bool userPaused,
            out string error)
        {
            if (!IsFinite(requested) || requested < 0f || requested > 2f)
            {
                error = "Requested game speed must be finite from 0x to 2x.";
                return false;
            }
            if (!IsFinite(lastNonZero) ||
                lastNonZero <= 0f || lastNonZero > 2f)
            {
                error = "Last non-zero game speed must be finite and positive.";
                return false;
            }

            requestedSpeed = requested;
            lastNonZeroSpeed = lastNonZero;
            if (userPaused)
                pauseReasons.Add(GamePauseReason.User);
            else
                pauseReasons.Remove(GamePauseReason.User);
            revision++;
            error = string.Empty;
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
