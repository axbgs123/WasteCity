using System;
using WasteCity.Core;

namespace WasteCity.Graybox3D.Usability
{
    public sealed class GrayboxGameSpeedCommandFacade3D
    {
        private readonly GameSpeedModel speed;
        private float lastNonZeroSpeed;

        public GrayboxGameSpeedCommandFacade3D(GameSpeedModel speed)
        {
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
            lastNonZeroSpeed = NormalizeNonZero(speed.RequestedSpeed);
        }

        public float RequestedSpeed =>
            speed.IsPaused(GamePauseReason.User)
                ? 0f
                : Normalize(speed.RequestedSpeed);

        public float EffectiveSpeed =>
            speed.Speed <= 0f
                ? 0f
                : Normalize(speed.Speed);

        public float LastNonZeroSpeed => lastNonZeroSpeed;

        public void RequestSpeed(int requestedSpeed)
        {
            int normalized = Math.Max(0, Math.Min(2, requestedSpeed));
            if (normalized == 0)
            {
                speed.SetPaused(GamePauseReason.User, true);
                return;
            }

            lastNonZeroSpeed = normalized;
            speed.Set(normalized);
            speed.SetPaused(GamePauseReason.User, false);
        }

        public void ToggleTacticalPause()
        {
            if (speed.IsPaused(GamePauseReason.User))
            {
                speed.Set(lastNonZeroSpeed);
                speed.SetPaused(GamePauseReason.User, false);
                return;
            }

            lastNonZeroSpeed = NormalizeNonZero(speed.RequestedSpeed);
            speed.SetPaused(GamePauseReason.User, true);
        }

        public float ResolveRuleDelta(float unscaledDelta)
        {
            if (float.IsNaN(unscaledDelta) ||
                float.IsInfinity(unscaledDelta) ||
                unscaledDelta <= 0f)
                return 0f;
            return unscaledDelta * EffectiveSpeed;
        }

        private static float Normalize(float value)
        {
            if (value <= 0f) return 0f;
            return value < 1.5f ? 1f : 2f;
        }

        private static float NormalizeNonZero(float value)
        {
            return value < 1.5f ? 1f : 2f;
        }
    }
}
