namespace WasteCity.Graybox3D.Usability
{
    public sealed class GrayboxGameSpeedPersistenceState3D
    {
        public GrayboxGameSpeedPersistenceState3D(
            float requestedSpeed,
            float lastNonZeroSpeed)
        {
            RequestedSpeed = requestedSpeed;
            LastNonZeroSpeed = lastNonZeroSpeed;
        }

        public float RequestedSpeed { get; }
        public float LastNonZeroSpeed { get; }
    }

    public sealed class GrayboxGameSpeedRestorePlan3D
    {
        internal GrayboxGameSpeedRestorePlan3D(
            GrayboxGameSpeedCommandFacade3D owner,
            ulong expectedRevision,
            float expectedRequestedSpeed,
            float expectedLastNonZeroSpeed,
            float requestedSpeed,
            float lastNonZeroSpeed)
        {
            Owner = owner;
            ExpectedRevision = expectedRevision;
            ExpectedRequestedSpeed = expectedRequestedSpeed;
            ExpectedLastNonZeroSpeed = expectedLastNonZeroSpeed;
            RequestedSpeed = requestedSpeed;
            LastNonZeroSpeed = lastNonZeroSpeed;
        }

        internal GrayboxGameSpeedCommandFacade3D Owner { get; }
        internal ulong ExpectedRevision { get; }
        internal float ExpectedRequestedSpeed { get; }
        internal float ExpectedLastNonZeroSpeed { get; }
        internal float RequestedSpeed { get; }
        internal float LastNonZeroSpeed { get; }
        internal bool Consumed { get; set; }
    }
}
