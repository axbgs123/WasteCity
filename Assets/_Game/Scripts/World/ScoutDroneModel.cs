using System;
using System.Collections.Generic;
using WasteCity.World.Exploration;

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
        public const string StableVisionSourcePrefix =
            "core.exploration.scout-drone|";

        public static int ActiveCount(bool researchCompleted, IReadOnlyList<DroneBayState> bays)
        {
            if (!researchCompleted || bays == null) return 0;
            int count = 0;
            for (int index = 0; index < bays.Count; index++)
                if (IsActive(researchCompleted, bays[index]))
                    count++;
            return count;
        }

        public static bool IsActive(
            bool researchCompleted,
            DroneBayState bay)
        {
            return researchCompleted && bay.Completed && bay.HasLogistics;
        }

        public static string StableVisionSourceId(string stableBayId)
        {
            if (string.IsNullOrWhiteSpace(stableBayId))
            {
                throw new ArgumentException(
                    "Stable drone bay ID is required.",
                    nameof(stableBayId));
            }
            return StableVisionSourcePrefix + stableBayId;
        }

        public static long PatrolStep(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) ||
                float.IsInfinity(elapsedSeconds) ||
                elapsedSeconds <= 0f)
                return 0L;
            return (long)Math.Floor(
                elapsedSeconds / ScoutDronePatrolModel.RevealSeconds);
        }

        public static bool TryCreateVisionSource(
            bool researchCompleted,
            DroneBayState bay,
            string stableBayId,
            float centerX,
            float centerY,
            int index,
            int totalCount,
            float elapsedSeconds,
            int mapWidth,
            int mapHeight,
            out WorldVisionSource source)
        {
            source = default;
            if (!IsActive(researchCompleted, bay) ||
                mapWidth < 1 || mapHeight < 1 ||
                string.IsNullOrWhiteSpace(stableBayId))
                return false;

            ScoutDronePosition position =
                ScoutDronePatrolModel.PositionAtElapsed(
                    centerX,
                    centerY,
                    index,
                    totalCount,
                    elapsedSeconds);
            int x = Clamp(
                (int)Math.Round(
                    position.X,
                    MidpointRounding.AwayFromZero),
                0,
                mapWidth - 1);
            int y = Clamp(
                (int)Math.Round(
                    position.Y,
                    MidpointRounding.AwayFromZero),
                0,
                mapHeight - 1);
            source = new WorldVisionSource(
                StableVisionSourceId(stableBayId),
                WorldVisionSourceKind.ScoutDrone,
                x,
                y,
                true);
            return true;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Min(maximum, Math.Max(minimum, value));
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
            return PositionAtElapsed(
                centerX,
                centerY,
                index,
                totalCount,
                elapsed);
        }

        public static ScoutDronePosition PositionAtElapsed(
            float centerX,
            float centerY,
            int index,
            int totalCount,
            float elapsedSeconds)
        {
            int count = Math.Max(1, totalCount);
            int safeIndex = Math.Max(0, index) % count;
            double phase = Math.PI * 2d * safeIndex / count;
            long step = ScoutDroneDeploymentRules.PatrolStep(elapsedSeconds);
            double quantizedElapsed = step * RevealSeconds;
            double angle = phase +
                quantizedElapsed * DegreesPerSecond * Math.PI / 180d;
            return new ScoutDronePosition(
                centerX + (float)Math.Cos(angle) * PatrolRadius,
                centerY + (float)Math.Sin(angle) * PatrolRadius);
        }
    }
}
