using System;
using System.Collections.Generic;

namespace WasteCity.World.Exploration
{
    public enum WorldVisibilityState
    {
        Hidden = 0,
        Explored = 1,
        Visible = 2,
    }

    public sealed class WorldVisibilityRuntime
    {
        private readonly bool[,] explored;
        private readonly int[,] visibleSourceCounts;
        private readonly Dictionary<string, WorldVisionSource> sources =
            new Dictionary<string, WorldVisionSource>(StringComparer.Ordinal);

        public WorldVisibilityRuntime(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width));
            if (height < 1)
                throw new ArgumentOutOfRangeException(nameof(height));
            Width = width;
            Height = height;
            explored = new bool[width, height];
            visibleSourceCounts = new int[width, height];
        }

        public int Width { get; }
        public int Height { get; }
        public int SourceCount => sources.Count;
        public ulong Revision { get; private set; }

        public WorldVisibilityState GetState(int x, int y)
        {
            ValidateCell(x, y);
            return visibleSourceCounts[x, y] > 0
                ? WorldVisibilityState.Visible
                : explored[x, y]
                    ? WorldVisibilityState.Explored
                    : WorldVisibilityState.Hidden;
        }

        public bool IsVisible(int x, int y)
        {
            return IsInBounds(x, y) && visibleSourceCounts[x, y] > 0;
        }

        public bool IsExplored(int x, int y)
        {
            return IsInBounds(x, y) && explored[x, y];
        }

        public bool UpsertSource(WorldVisionSource source)
        {
            if (!source.Active)
                return RemoveSource(source.StableId);
            ValidateCell(source.X, source.Y);
            if (sources.TryGetValue(
                    source.StableId,
                    out WorldVisionSource existing))
            {
                if (existing.Equals(source)) return false;
                ApplyVisibleCircle(existing, -1);
            }
            sources[source.StableId] = source;
            ApplyVisibleCircle(source, 1);
            AdvanceRevision();
            return true;
        }

        public bool RemoveSource(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId) ||
                !sources.TryGetValue(stableId, out WorldVisionSource source))
                return false;
            sources.Remove(stableId);
            ApplyVisibleCircle(source, -1);
            AdvanceRevision();
            return true;
        }

        public bool TryGetSource(
            string stableId,
            out WorldVisionSource source)
        {
            source = default;
            return !string.IsNullOrWhiteSpace(stableId) &&
                sources.TryGetValue(stableId, out source);
        }

        public WorldVisionSource[] CaptureSources()
        {
            var result = new WorldVisionSource[sources.Count];
            sources.Values.CopyTo(result, 0);
            Array.Sort(
                result,
                (left, right) => string.Compare(
                    left.StableId,
                    right.StableId,
                    StringComparison.Ordinal));
            return result;
        }

        public int Reveal(int centerX, int centerY, int radius)
        {
            ValidateCell(centerX, centerY);
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            int changed = ApplyCircle(
                centerX,
                centerY,
                radius,
                (x, y) =>
                {
                    if (explored[x, y]) return false;
                    explored[x, y] = true;
                    return true;
                });
            if (changed > 0) AdvanceRevision();
            return changed;
        }

        public bool[] CaptureExplored()
        {
            var result = new bool[Width * Height];
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                result[y * Width + x] = explored[x, y];
            return result;
        }

        public bool TryRestoreExplored(bool[] values, out string error)
        {
            if (values == null || values.Length != Width * Height)
            {
                error = "Explored cell count must match the world.";
                return false;
            }
            bool changed = false;
            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                bool next = values[y * Width + x] ||
                    visibleSourceCounts[x, y] > 0;
                if (explored[x, y] != next)
                {
                    explored[x, y] = next;
                    changed = true;
                }
            }
            if (changed) AdvanceRevision();
            error = string.Empty;
            return true;
        }

        private void ApplyVisibleCircle(WorldVisionSource source, int delta)
        {
            ApplyCircle(
                source.X,
                source.Y,
                source.Radius,
                (x, y) =>
                {
                    int next = visibleSourceCounts[x, y] + delta;
                    if (next < 0)
                    {
                        throw new InvalidOperationException(
                            "Visible source count cannot be negative.");
                    }
                    visibleSourceCounts[x, y] = next;
                    if (delta > 0) explored[x, y] = true;
                    return true;
                });
        }

        private int ApplyCircle(
            int centerX,
            int centerY,
            int radius,
            Func<int, int, bool> apply)
        {
            int changed = 0;
            long squaredRadius = (long)radius * radius;
            int minimumX = Math.Max(0, centerX - radius);
            int maximumX = Math.Min(Width - 1, centerX + radius);
            int minimumY = Math.Max(0, centerY - radius);
            int maximumY = Math.Min(Height - 1, centerY + radius);
            for (var x = minimumX; x <= maximumX; x++)
            for (var y = minimumY; y <= maximumY; y++)
            {
                long dx = x - centerX;
                long dy = y - centerY;
                if (dx * dx + dy * dy <= squaredRadius && apply(x, y))
                    changed++;
            }
            return changed;
        }

        private bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        private void ValidateCell(int x, int y)
        {
            if (!IsInBounds(x, y))
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    "World cell is outside the visibility map.");
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }
    }
}
