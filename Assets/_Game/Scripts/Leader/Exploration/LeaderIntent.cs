namespace WasteCity.Leader.Exploration
{
    public enum LeaderIntentKind
    {
        None = 0,
        HoldPosition = 1,
        FollowCity = 2,
        ReturnToDock = 3,
    }

    public readonly struct LeaderIntent
    {
        internal LeaderIntent(
            LeaderIntentKind kind,
            float targetX,
            float targetY)
        {
            Kind = kind;
            TargetX = targetX;
            TargetY = targetY;
        }

        public LeaderIntentKind Kind { get; }
        public float TargetX { get; }
        public float TargetY { get; }

        public static LeaderIntent None { get; } =
            new LeaderIntent(LeaderIntentKind.None, 0f, 0f);

        public static LeaderIntent Hold(float x, float y)
        {
            return new LeaderIntent(LeaderIntentKind.HoldPosition, x, y);
        }

        public static LeaderIntent FollowCity(float x, float y)
        {
            return new LeaderIntent(LeaderIntentKind.FollowCity, x, y);
        }

        public static LeaderIntent ReturnToDock(float x, float y)
        {
            return new LeaderIntent(LeaderIntentKind.ReturnToDock, x, y);
        }
    }
}
