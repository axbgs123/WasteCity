using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Graybox3D.Usability
{
    public enum GrayboxWindowMode3D
    {
        Windowed = 0,
        FullScreenWindow = 1
    }

    public readonly struct GrayboxDisplayResolution3D :
        IEquatable<GrayboxDisplayResolution3D>
    {
        public GrayboxDisplayResolution3D(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }

        public bool Equals(GrayboxDisplayResolution3D other)
        {
            return Width == other.Width && Height == other.Height;
        }

        public override bool Equals(object value)
        {
            return value is GrayboxDisplayResolution3D other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked { return (Width * 397) ^ Height; }
        }

        public override string ToString()
        {
            return Width + "×" + Height;
        }
    }

    public readonly struct GrayboxDisplaySettings3D :
        IEquatable<GrayboxDisplaySettings3D>
    {
        public GrayboxDisplaySettings3D(
            int width,
            int height,
            GrayboxWindowMode3D windowMode)
        {
            Width = width;
            Height = height;
            WindowMode = windowMode;
        }

        public int Width { get; }
        public int Height { get; }
        public GrayboxWindowMode3D WindowMode { get; }

        public bool Equals(GrayboxDisplaySettings3D other)
        {
            return Width == other.Width &&
                   Height == other.Height &&
                   WindowMode == other.WindowMode;
        }

        public override bool Equals(object value)
        {
            return value is GrayboxDisplaySettings3D other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Width;
                hashCode = (hashCode * 397) ^ Height;
                hashCode = (hashCode * 397) ^ (int)WindowMode;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return Width + "×" + Height + " " + WindowMode;
        }
    }

    public interface IGrayboxDisplaySettingsStore
    {
        bool TryLoad(
            out int version,
            out GrayboxDisplaySettings3D settings);

        void Save(int version, GrayboxDisplaySettings3D settings);
    }

    public interface IGrayboxDisplaySettingsPlatform
    {
        IReadOnlyList<GrayboxDisplayResolution3D> AvailableResolutions
        {
            get;
        }

        GrayboxDisplaySettings3D Current { get; }

        bool TryApply(GrayboxDisplaySettings3D settings);
    }

    public sealed class GrayboxDisplaySettingsModel3D
    {
        public const int CurrentVersion = 1;
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        private readonly IGrayboxDisplaySettingsPlatform platform;
        private readonly IGrayboxDisplaySettingsStore store;
        private readonly ReadOnlyCollection<GrayboxDisplayResolution3D>
            availableResolutions;
        private readonly GrayboxDisplaySettings3D platformCurrent;

        public GrayboxDisplaySettingsModel3D(
            IGrayboxDisplaySettingsPlatform platform,
            IGrayboxDisplaySettingsStore store)
        {
            this.platform = platform ??
                throw new ArgumentNullException(nameof(platform));
            this.store = store ??
                throw new ArgumentNullException(nameof(store));

            var resolutions = new List<GrayboxDisplayResolution3D>();
            var seen = new HashSet<long>();
            IReadOnlyList<GrayboxDisplayResolution3D> enumerated =
                platform.AvailableResolutions;
            if (enumerated != null)
            {
                for (var index = 0; index < enumerated.Count; index++)
                    AddResolution(enumerated[index], resolutions, seen);
            }

            GrayboxDisplaySettings3D current = platform.Current;
            if (IsPositive(current.Width, current.Height))
            {
                AddResolution(
                    new GrayboxDisplayResolution3D(
                        current.Width,
                        current.Height),
                    resolutions,
                    seen);
            }
            if (resolutions.Count == 0)
            {
                AddResolution(
                    new GrayboxDisplayResolution3D(
                        DefaultWidth,
                        DefaultHeight),
                    resolutions,
                    seen);
            }
            resolutions.Sort(CompareResolutions);
            availableResolutions = resolutions.AsReadOnly();

            GrayboxDisplayResolution3D currentResolution =
                IsPositive(current.Width, current.Height)
                    ? new GrayboxDisplayResolution3D(
                        current.Width,
                        current.Height)
                    : resolutions[0];
            GrayboxWindowMode3D currentMode = IsWindowModeValid(
                current.WindowMode)
                ? current.WindowMode
                : GrayboxWindowMode3D.Windowed;
            platformCurrent = new GrayboxDisplaySettings3D(
                currentResolution.Width,
                currentResolution.Height,
                currentMode);

            LastApplied = platformCurrent;
            if (store.TryLoad(
                    out int version,
                    out GrayboxDisplaySettings3D stored) &&
                version == CurrentVersion &&
                IsSupported(stored) &&
                platform.TryApply(stored))
                LastApplied = stored;
            Staged = LastApplied;
        }

        public IReadOnlyList<GrayboxDisplayResolution3D>
            AvailableResolutions => availableResolutions;
        public GrayboxDisplaySettings3D LastApplied { get; private set; }
        public GrayboxDisplaySettings3D Staged { get; private set; }

        public void StageResolution(GrayboxDisplayResolution3D resolution)
        {
            if (!ContainsResolution(resolution))
                throw new ArgumentException(
                    "Resolution is unavailable on the current platform.",
                    nameof(resolution));
            Staged = new GrayboxDisplaySettings3D(
                resolution.Width,
                resolution.Height,
                Staged.WindowMode);
        }

        public void StageWindowMode(GrayboxWindowMode3D windowMode)
        {
            if (!IsWindowModeValid(windowMode))
                throw new ArgumentOutOfRangeException(nameof(windowMode));
            Staged = new GrayboxDisplaySettings3D(
                Staged.Width,
                Staged.Height,
                windowMode);
        }

        public bool Apply()
        {
            GrayboxDisplaySettings3D applied = Staged;
            if (!platform.TryApply(applied))
                return false;
            store.Save(CurrentVersion, applied);
            LastApplied = applied;
            Staged = applied;
            return true;
        }

        public void Cancel()
        {
            Staged = LastApplied;
        }

        public void RestoreDefaults()
        {
            GrayboxDisplayResolution3D resolution =
                SelectDefaultResolution();
            Staged = new GrayboxDisplaySettings3D(
                resolution.Width,
                resolution.Height,
                GrayboxWindowMode3D.FullScreenWindow);
        }

        private GrayboxDisplayResolution3D SelectDefaultResolution()
        {
            var preferred = new GrayboxDisplayResolution3D(
                DefaultWidth,
                DefaultHeight);
            if (ContainsResolution(preferred))
                return preferred;

            GrayboxDisplayResolution3D best =
                new GrayboxDisplayResolution3D(
                    platformCurrent.Width,
                    platformCurrent.Height);
            long bestDistance = DistanceFromDefault(best);
            for (var index = 0;
                 index < availableResolutions.Count;
                 index++)
            {
                GrayboxDisplayResolution3D candidate =
                    availableResolutions[index];
                if (candidate.Width > platformCurrent.Width ||
                    candidate.Height > platformCurrent.Height)
                    continue;
                long distance = DistanceFromDefault(candidate);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private bool IsSupported(GrayboxDisplaySettings3D settings)
        {
            return IsWindowModeValid(settings.WindowMode) &&
                   IsPositive(settings.Width, settings.Height) &&
                   ContainsResolution(new GrayboxDisplayResolution3D(
                       settings.Width,
                       settings.Height));
        }

        private bool ContainsResolution(
            GrayboxDisplayResolution3D resolution)
        {
            for (var index = 0;
                 index < availableResolutions.Count;
                 index++)
                if (availableResolutions[index].Equals(resolution))
                    return true;
            return false;
        }

        private static void AddResolution(
            GrayboxDisplayResolution3D resolution,
            ICollection<GrayboxDisplayResolution3D> destination,
            ISet<long> seen)
        {
            if (!IsPositive(resolution.Width, resolution.Height))
                return;
            long key = ((long)resolution.Width << 32) ^
                       (uint)resolution.Height;
            if (seen.Add(key))
                destination.Add(resolution);
        }

        private static int CompareResolutions(
            GrayboxDisplayResolution3D left,
            GrayboxDisplayResolution3D right)
        {
            int width = left.Width.CompareTo(right.Width);
            return width != 0
                ? width
                : left.Height.CompareTo(right.Height);
        }

        private static long DistanceFromDefault(
            GrayboxDisplayResolution3D resolution)
        {
            long width = resolution.Width - (long)DefaultWidth;
            long height = resolution.Height - (long)DefaultHeight;
            return width * width + height * height;
        }

        private static bool IsPositive(int width, int height)
        {
            return width > 0 && height > 0;
        }

        private static bool IsWindowModeValid(GrayboxWindowMode3D mode)
        {
            return mode == GrayboxWindowMode3D.Windowed ||
                   mode == GrayboxWindowMode3D.FullScreenWindow;
        }
    }
}
