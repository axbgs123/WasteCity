namespace WasteCity.Building
{
    public enum BuildingOrientation
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class BuildingOrientationRules
    {
        public static BuildingOrientation RotateClockwise(BuildingOrientation value)
        {
            return value == BuildingOrientation.West
                ? BuildingOrientation.North
                : value + 1;
        }

        public static int Width(BuildingDefinition definition, BuildingOrientation orientation)
        {
            return orientation == BuildingOrientation.East || orientation == BuildingOrientation.West
                ? definition.Height
                : definition.Width;
        }

        public static int Height(BuildingDefinition definition, BuildingOrientation orientation)
        {
            return orientation == BuildingOrientation.East || orientation == BuildingOrientation.West
                ? definition.Width
                : definition.Height;
        }
    }
}
