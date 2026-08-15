using System;
using System.Globalization;

namespace WasteCity.Graybox3D.Production
{
    public static class GrayboxResourceNodeIdentity3D
    {
        private const string Prefix = "world.resource-node.";

        public static string Create(int worldX, int worldY)
        {
            if (worldX < 0)
                throw new ArgumentOutOfRangeException(nameof(worldX));
            if (worldY < 0)
                throw new ArgumentOutOfRangeException(nameof(worldY));
            return Prefix +
                   worldX.ToString(CultureInfo.InvariantCulture) +
                   "." +
                   worldY.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParse(
            string stableId,
            int worldWidth,
            int worldHeight,
            out int worldX,
            out int worldY)
        {
            worldX = 0;
            worldY = 0;
            if (worldWidth <= 0 || worldHeight <= 0 ||
                string.IsNullOrWhiteSpace(stableId) ||
                !stableId.StartsWith(Prefix, StringComparison.Ordinal))
                return false;

            string coordinates = stableId.Substring(Prefix.Length);
            int separator = coordinates.IndexOf('.');
            if (separator <= 0 ||
                separator == coordinates.Length - 1 ||
                coordinates.IndexOf('.', separator + 1) >= 0)
                return false;

            if (!int.TryParse(
                    coordinates.Substring(0, separator),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int parsedX) ||
                !int.TryParse(
                    coordinates.Substring(separator + 1),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int parsedY) ||
                parsedX < 0 ||
                parsedY < 0 ||
                parsedX >= worldWidth ||
                parsedY >= worldHeight)
                return false;

            worldX = parsedX;
            worldY = parsedY;
            return true;
        }
    }
}
