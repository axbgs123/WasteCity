using System;

namespace WasteCity.World.Exploration
{
    public enum WorldVisionSourceKind
    {
        PrimaryCity = 0,
        SecondaryCity = 1,
        Leader = 2,
        Outpost = 3,
        ScoutDrone = 4,
    }

    public readonly struct WorldVisionSource :
        IEquatable<WorldVisionSource>
    {
        public WorldVisionSource(
            string stableId,
            WorldVisionSourceKind kind,
            int x,
            int y,
            bool active,
            ulong sourceRevision = 0ul)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException(
                    "Vision source stable ID is required.",
                    nameof(stableId));
            if (!Enum.IsDefined(typeof(WorldVisionSourceKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            StableId = stableId;
            Kind = kind;
            X = x;
            Y = y;
            Active = active;
            SourceRevision = sourceRevision;
        }

        public string StableId { get; }
        public WorldVisionSourceKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public bool Active { get; }
        public ulong SourceRevision { get; }
        public int Radius =>
            FormalExplorationCatalog3D.ResolveSightRadius(Kind);

        public bool Equals(WorldVisionSource other)
        {
            return string.Equals(
                       StableId,
                       other.StableId,
                       StringComparison.Ordinal) &&
                Kind == other.Kind &&
                X == other.X &&
                Y == other.Y &&
                Active == other.Active &&
                SourceRevision == other.SourceRevision;
        }

        public override bool Equals(object value)
        {
            return value is WorldVisionSource other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StableId == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(StableId);
                hash = hash * 397 ^ (int)Kind;
                hash = hash * 397 ^ X;
                hash = hash * 397 ^ Y;
                hash = hash * 397 ^ Active.GetHashCode();
                hash = hash * 397 ^ SourceRevision.GetHashCode();
                return hash;
            }
        }
    }
}
