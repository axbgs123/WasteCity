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
            -720f,
            -240f,
            240f,
            720f,
        };

        private static readonly float[] Subcolumns =
        {
            -96f,
            96f,
        };

        private static readonly float[] BridgeGutters =
        {
            -480f,
            0f,
            480f,
        };

        private static readonly float[] Rows =
        {
            0f,
            140f,
            250f,
            410f,
            580f,
        };

        private static readonly float[] BridgeShelves =
        {
            590f,
            720f,
        };

        public static Vector2 ReferenceResolution =>
            new Vector2(1920f, 1080f);
        public static Rect HeaderRect => new Rect(0f, 968f, 1920f, 112f);
        public static Rect TreeRect => new Rect(0f, 216f, 1920f, 752f);
        public static Rect FooterRect => new Rect(0f, 0f, 1920f, 216f);
        public static Rect TitleSlotRect =>
            new Rect(18f, 982f, 322f, 74f);
        public static Rect SearchSlotRect =>
            new Rect(365f, 987f, 210f, 62f);
        public static Rect RouteFilterSlotRect =>
            new Rect(596f, 984f, 520f, 68f);
        public static Rect StatusFilterSlotRect =>
            new Rect(1150f, 984f, 440f, 68f);
        public static Rect FocusSlotRect =>
            new Rect(1612f, 984f, 290f, 68f);
        public static Rect[] FooterSlots => new[]
        {
            new Rect(8f, 10f, 535f, 196f),
            new Rect(548f, 10f, 402f, 196f),
            new Rect(960f, 10f, 205f, 196f),
            new Rect(1175f, 10f, 255f, 196f),
            new Rect(1440f, 10f, 275f, 196f),
            new Rect(1725f, 10f, 187f, 196f),
        };
        public static Vector2 CompactNodeSize => new Vector2(180f, 58f);
        public static Vector2 BridgeNodeSize => new Vector2(90f, 112f);
        public static Vector2 CommonNodeSize => new Vector2(350f, 74f);
        public static Vector2 RouteHeaderSize => new Vector2(200f, 54f);
        public static float RouteHeaderBoundsPadding => 82f;
        public static Vector2 CompactNodeIconSize => new Vector2(40f, 40f);
        public static Vector2 CostIconSize => new Vector2(22f, 22f);
        public static float[] RouteLaneCenters => (float[])RouteLanes.Clone();
        public static float[] SubcolumnOffsets => (float[])Subcolumns.Clone();
        public static float[] BridgeGutterCenters =>
            (float[])BridgeGutters.Clone();
        public static float[] RowCenters => (float[])Rows.Clone();
        public static float[] BridgeRows => (float[])BridgeShelves.Clone();
        public const float NodeSublaneStep = 80f;
    }
}
