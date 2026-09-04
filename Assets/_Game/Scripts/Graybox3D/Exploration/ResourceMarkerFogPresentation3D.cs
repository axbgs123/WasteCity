using System;
using WasteCity.World.Exploration;

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
            int lastKnownAmount,
            WorldIntelState intelState,
            float intelAgeSeconds,
            bool showsIntelAge)
        {
            Mode = mode;
            LastKnownAmount = lastKnownAmount;
            IntelState = intelState;
            IntelAgeSeconds = Math.Max(0, (int)Math.Floor(intelAgeSeconds));
            ShowsIntelAge = showsIntelAge;
        }

        public ResourceMarkerFogMode3D Mode { get; }
        public int LastKnownAmount { get; }
        public WorldIntelState IntelState { get; }
        public int IntelAgeSeconds { get; }
        public bool ShowsIntelAge { get; }
        public string IntelStatusText => !ShowsIntelAge
            ? string.Empty
            : (IntelState == WorldIntelState.Expired
                ? "情报已过期"
                : IntelState == WorldIntelState.Stale
                    ? "陈旧情报"
                    : "最近情报") + " · " + IntelAgeSeconds + " 秒前";

        public static ResourceMarkerFogPresentation3D Hidden =>
            new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.Hidden,
                0,
                WorldIntelState.Fresh,
                0f,
                false);

        public static ResourceMarkerFogPresentation3D LastKnownIdentity =>
            new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.LastKnownIdentity,
                0,
                WorldIntelState.Expired,
                0f,
                false);

        public static ResourceMarkerFogPresentation3D Live =>
            new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.Live,
                0,
                WorldIntelState.Fresh,
                0f,
                false);

        public static ResourceMarkerFogPresentation3D LastIntel(int amount)
        {
            return new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.LastIntel,
                amount,
                WorldIntelState.Fresh,
                0f,
                false);
        }

        public static ResourceMarkerFogPresentation3D LastIntel(
            int amount,
            WorldIntelState state,
            float ageSeconds)
        {
            return new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.LastIntel,
                amount,
                state,
                ageSeconds,
                true);
        }

        public static ResourceMarkerFogPresentation3D LastKnownIdentityAt(
            float ageSeconds)
        {
            return new ResourceMarkerFogPresentation3D(
                ResourceMarkerFogMode3D.LastKnownIdentity,
                0,
                WorldIntelState.Expired,
                ageSeconds,
                true);
        }
    }
}
