using System;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public static class GrayboxWorldLayout3D
    {
        public const int DefaultSeed = 8128;
        public const int LegacyWidth = 32;
        public const int LegacyHeight = 24;
        public const int WorldWidth = 64;
        public const int WorldHeight = 48;
        public const int LegacyOffsetX = 16;
        public const int LegacyOffsetY = 12;

        public static WorldMapModel CreateDefault()
        {
            return Create(DefaultSeed);
        }

        public static WorldMapModel Create(int seedValue)
        {
            var legacy = new WorldMapModel(
                LegacyWidth,
                LegacyHeight,
                new WorldSeed(seedValue));
            var expanded = new WorldCell[WorldWidth, WorldHeight];
            var sparseCell = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Open);

            for (var x = 0; x < WorldWidth; x++)
            for (var y = 0; y < WorldHeight; y++)
                expanded[x, y] = sparseCell;

            for (var x = 0; x < LegacyWidth; x++)
            for (var y = 0; y < LegacyHeight; y++)
                expanded[ToExpandedX(x), ToExpandedY(y)] = legacy.Get(x, y);

            return new WorldMapModel(expanded);
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
