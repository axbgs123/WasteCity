namespace WasteCity.Graybox3D.Exploration
{
    public enum ResourceMarkerFogMode3D
    {
        Hidden = 0,
        LastKnownIdentity = 1,
        LastIntel = 2,
        Live = 3,
    }

    public readonly struct ResourceMarkerFogPresentation3D
    {
        private ResourceMarkerFogPresentation3D(
            ResourceMarkerFogMode3D mode,
            int lastKnownAmount)
        {
            Mode = mode;
            LastKnownAmount = lastKnownAmount;
        }

        public ResourceMarkerFogMode3D Mode { get; }
        public int LastKnownAmount { get; }

        public static ResourceMarkerFogPresentation3D Hidden =>
            new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.Hidden,
                0);

        public static ResourceMarkerFogPresentation3D LastKnownIdentity =>
            new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.LastKnownIdentity,
                0);

        public static ResourceMarkerFogPresentation3D Live =>
            new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.Live,
                0);

        public static ResourceMarkerFogPresentation3D LastIntel(int amount)
        {
            return new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.LastIntel,
                amount);
        }
    }
}
