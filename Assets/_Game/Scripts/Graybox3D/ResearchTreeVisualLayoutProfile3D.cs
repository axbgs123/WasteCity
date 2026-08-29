using UnityEngine;

namespace WasteCity.Graybox3D.Usability
{
    /// <summary>
    /// Pure IDEA-0024 presentation geometry. Research truth and progression
    /// remain owned by ResearchCatalog and the formal research runtime.
    /// </summary>
    public static class ResearchTreeVisualLayoutProfile3D
    {
        private static readonly float[] RouteLanes =
        {
            -750f,
            -250f,
            250f,
            750f,
        };

        private static readonly float[] Subcolumns =
        {
            -86f,
            86f,
        };

        private static readonly float[] BridgeGutters =
        {
            -500f,
            0f,
            500f,
        };

        private static readonly float[] Rows =
        {
            0f,
            250f,
            540f,
            850f,
            1190f,
        };

        public static Vector2 ReferenceResolution =>
            new Vector2(1920f, 1080f);
        public static Rect HeaderRect => new Rect(0f, 968f, 1920f, 112f);
        public static Rect TreeRect => new Rect(0f, 216f, 1920f, 752f);
        public static Rect FooterRect => new Rect(0f, 0f, 1920f, 216f);
        public static Vector2 CompactNodeSize => new Vector2(156f, 74f);
        public static Vector2 CompactNodeIconSize => new Vector2(34f, 34f);
        public static float[] RouteLaneCenters => (float[])RouteLanes.Clone();
        public static float[] SubcolumnOffsets => (float[])Subcolumns.Clone();
        public static float[] BridgeGutterCenters =>
            (float[])BridgeGutters.Clone();
        public static float[] RowCenters => (float[])Rows.Clone();
        public const float NodeSublaneStep = 96f;
        public const float BridgeLevelStep = 108f;
    }
}
