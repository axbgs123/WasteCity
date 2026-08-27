using System;
using UnityEngine;
using WasteCity.Core;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxFormalRuleClock3D
    {
        private readonly GameSpeedModel speed;
        private bool terminal;
        private float developmentAcceleration = 1f;

        public GrayboxFormalRuleClock3D(GameSpeedModel speed)
        {
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
        }

        public float EffectiveSpeed => terminal
            ? 0f
            : NormalizeSpeed(speed.Speed);

        public float DevelopmentAcceleration => developmentAcceleration;

        public void SetDevelopmentAcceleration(float multiplier)
        {
            developmentAcceleration = float.IsNaN(multiplier) ||
                                      float.IsInfinity(multiplier)
                ? 1f
                : Math.Max(1f, Math.Min(100f, multiplier));
        }

        public void SetTerminal(bool value)
        {
            terminal = value;
        }

        public float ResolveRuleDelta(float unscaledDeltaSeconds)
        {
            return ResolveDelta(unscaledDeltaSeconds, EffectiveSpeed);
        }

        public float ResolvePresentationDelta(float unscaledDeltaSeconds)
        {
            float presentationSpeed = terminal
                ? 0f
                : NormalizeSpeed(speed.SpeedIgnoring(
                    GamePauseReason.Advancement));
            return ResolveDelta(unscaledDeltaSeconds, presentationSpeed);
        }

        public static float ResolveCompatibilityRuleDelta(
            float unscaledDeltaSeconds)
        {
            if (!IsFinitePositive(unscaledDeltaSeconds)) return 0f;
            double resolved = (double)unscaledDeltaSeconds *
                NormalizeSpeed(Time.timeScale);
            return resolved >= float.MaxValue
                ? float.MaxValue
                : (float)resolved;
        }

        private static float NormalizeSpeed(float value)
        {
            if (!IsFinitePositive(value)) return 0f;
            return value < 1.5f ? 1f : 2f;
        }

        private float ResolveDelta(
            float unscaledDeltaSeconds,
            float effectiveSpeed)
        {
            if (!IsFinitePositive(unscaledDeltaSeconds)) return 0f;
            double resolved =
                (double)unscaledDeltaSeconds * effectiveSpeed *
                developmentAcceleration;
            return resolved >= float.MaxValue
                ? float.MaxValue
                : (float)resolved;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f &&
                   !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }
    }
}
