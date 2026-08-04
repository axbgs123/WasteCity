using System;
using System.Collections.Generic;

namespace WasteCity.World
{
    public readonly struct DroneBayState
    {
        public bool Completed { get; }
        public bool HasLogistics { get; }

        public DroneBayState(bool completed, bool hasLogistics)
        {
            Completed = completed;
            HasLogistics = hasLogistics;
        }
    }

    public readonly struct ScoutDronePosition
    {
        public float X { get; }
        public float Y { get; }

        public ScoutDronePosition(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public static class ScoutDroneDeploymentRules
    {
        public static int ActiveCount(bool researchCompleted, IReadOnlyList<DroneBayState> bays)
        {
            if (!researchCompleted || bays == null) return 0;
            int count = 0;
            for (int index = 0; index < bays.Count; index++)
                if (bays[index].Completed && bays[index].HasLogistics)
                    count++;
            return count;
        }
    }

    public sealed class ScoutDronePatrolModel
    {
        public const float PatrolRadius = 6f;
        public const float DegreesPerSecond = 30f;
        public const float RevealSeconds = 1f;

        private float elapsed;
        private float revealElapsed;

        public bool Tick(float deltaSeconds)
        {
            float delta = Math.Max(0f, deltaSeconds);
            elapsed += delta;
            revealElapsed += delta;
            if (revealElapsed + .00001f < RevealSeconds) return false;
            revealElapsed %= RevealSeconds;
            return true;
        }

        public ScoutDronePosition Position(float centerX, float centerY, int index, int totalCount)
        {
            int count = Math.Max(1, totalCount);
            int safeIndex = Math.Max(0, index) % count;
            double phase = Math.PI * 2d * safeIndex / count;
            double angle = phase + elapsed * DegreesPerSecond * Math.PI / 180d;
            return new ScoutDronePosition(
                centerX + (float)Math.Cos(angle) * PatrolRadius,
                centerY + (float)Math.Sin(angle) * PatrolRadius);
        }
    }
}
