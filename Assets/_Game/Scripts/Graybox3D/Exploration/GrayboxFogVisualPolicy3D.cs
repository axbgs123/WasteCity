using UnityEngine;
using WasteCity.World.Exploration;

namespace WasteCity.Graybox3D.Exploration
{
    public static class GrayboxFogVisualPolicy3D
    {
        public const byte HiddenAlpha = 255;
        public const byte ExploredAlpha = 150;

        private static readonly Color32 HiddenColor =
            new Color32(8, 12, 15, HiddenAlpha);
        private static readonly Color32 ExploredColor =
            new Color32(14, 20, 24, ExploredAlpha);
        private static readonly Color32 VisibleColor =
            new Color32(0, 0, 0, 0);

        public static Color32 Resolve(WorldVisibilityState state)
        {
            switch (state)
            {
                case WorldVisibilityState.Hidden:
                    return HiddenColor;
                case WorldVisibilityState.Explored:
                    return ExploredColor;
                case WorldVisibilityState.Visible:
                    return VisibleColor;
                default:
                    return HiddenColor;
            }
        }
    }
}
