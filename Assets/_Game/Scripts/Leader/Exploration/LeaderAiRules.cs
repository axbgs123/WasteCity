using System;
using WasteCity.City;

namespace WasteCity.Leader.Exploration
{
    public readonly struct LeaderAiContext
    {
        public LeaderAiContext(
            CityMode cityMode,
            LeaderControlMode controlMode,
            float leaderX,
            float leaderY,
            float dockX,
            float dockY,
            bool pathAvailable)
        {
            CityMode = cityMode;
            ControlMode = controlMode;
            LeaderX = leaderX;
            LeaderY = leaderY;
            DockX = dockX;
            DockY = dockY;
            PathAvailable = pathAvailable;
        }

        public CityMode CityMode { get; }
        public LeaderControlMode ControlMode { get; }
        public float LeaderX { get; }
        public float LeaderY { get; }
        public float DockX { get; }
        public float DockY { get; }
        public bool PathAvailable { get; }
    }

    public static class LeaderAiRules
    {
        public const float DockArrivalDistance = .1f;

        public static LeaderIntent Resolve(LeaderAiContext context)
        {
            if (context.ControlMode != LeaderControlMode.AI ||
                !IsFinite(context.LeaderX) ||
                !IsFinite(context.LeaderY) ||
                !IsFinite(context.DockX) ||
                !IsFinite(context.DockY))
            {
                return LeaderIntent.None;
            }

            if (context.CityMode != CityMode.Fortress)
            {
                return LeaderIntent.FollowCity(
                    context.DockX,
                    context.DockY);
            }

            float deltaX = context.LeaderX - context.DockX;
            float deltaY = context.LeaderY - context.DockY;
            if (deltaX * deltaX + deltaY * deltaY <=
                DockArrivalDistance * DockArrivalDistance)
            {
                return LeaderIntent.Hold(
                    context.LeaderX,
                    context.LeaderY);
            }

            return context.PathAvailable
                ? LeaderIntent.ReturnToDock(context.DockX, context.DockY)
                : LeaderIntent.Hold(context.LeaderX, context.LeaderY);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
