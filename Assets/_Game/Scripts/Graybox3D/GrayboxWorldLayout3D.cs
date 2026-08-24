using System;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public static class GrayboxWorldLayout3D
    {
        public const int DefaultSeed =
            FormalWorldGenerationCatalog3D.DefaultSeed;
        public const int LegacyWidth = 32;
        public const int LegacyHeight = 24;
        public const int WorldWidth = FormalWorldGenerationCatalog3D.Width;
        public const int WorldHeight = FormalWorldGenerationCatalog3D.Height;
        public const int StartCellX =
            FormalWorldGenerationCatalog3D.StartCellX;
        public const int StartCellY =
            FormalWorldGenerationCatalog3D.StartCellY;
        public const int LegacyOffsetX = 16;
        public const int LegacyOffsetY = 12;

        public static WorldMapModel CreateDefault()
        {
            return Create(DefaultSeed);
        }

        public static WorldMapModel Create(int seedValue)
        {
            return FormalWorldGenerator3D.Generate(seedValue);
        }

        public static int ToExpandedX(int legacyX)
        {
            if (legacyX < 0 || legacyX >= LegacyWidth)
                throw new ArgumentOutOfRangeException(nameof(legacyX));

            return legacyX + LegacyOffsetX;
        }

        public static int ToExpandedY(int legacyY)
        {
            if (legacyY < 0 || legacyY >= LegacyHeight)
                throw new ArgumentOutOfRangeException(nameof(legacyY));

            return legacyY + LegacyOffsetY;
        }
    }
}
