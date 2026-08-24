using UnityEngine;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    public sealed class FormalUiLayoutProfile3D
    {
        public static FormalUiLayoutProfile3D Standard { get; } =
            new FormalUiLayoutProfile3D();

        private FormalUiLayoutProfile3D()
        {
        }

        public Vector2 ReferenceResolution => new Vector2(1920f, 1080f);
        public CanvasScaler.ScaleMode ScaleMode =>
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        public CanvasScaler.ScreenMatchMode ScreenMatchMode =>
            CanvasScaler.ScreenMatchMode.Expand;
        public float MatchWidthOrHeight => .5f;
        public float ReferencePixelsPerUnit => 100f;

        public int BuildingSortingOrder => 0;
        public int OperationsSortingOrder => 50;
        public int SystemMenuSortingOrder => 100;

        public float DesktopSafeMargin => 24f;
        public float MinimumPhysicalFontPixels => 12f;

        public float SpaceExtraSmall => 4f;
        public float SpaceSmall => 8f;
        public float SpaceMedium => 12f;
        public float SpaceLarge => 16f;
        public float SpaceExtraLarge => 24f;
        public float SpaceDoubleExtraLarge => 32f;

        public float FontDescription => 14f;
        public float FontBody => 16f;
        public float FontEmphasis => 18f;
        public float FontSubtitle => 20f;
        public float FontTitle => 28f;

        public float IconInline => 16f;
        public float IconCompact => 20f;
        public float IconRow => 24f;
        public float IconSlot => 32f;
        public float IconNode => 48f;
        public float IconHero => 64f;

        public float StandardButtonMinimumHeight => 36f;
        public float PrimaryButtonMinimumHeight => 44f;

        public float PersistentTopHeight => 64f;
        public float DangerSlotWidthRatio => .24f;
        public float DangerSlotMaximumWidth => 420f;
        public float SpeedSlotWidthRatio => .18f;
        public float SpeedSlotMaximumWidth => 320f;
        public float BuildBarWidthRatio => .42f;
        public float BuildBarMaximumWidth => 620f;
        public float BuildBarHeight => 54f;
        public float BuildFeedbackWidthRatio => .36f;
        public float BuildFeedbackMaximumWidth => 640f;
        public float BuildFeedbackHeight => 36f;
        public float SelectionDrawerWidthRatio => .26f;
        public float SelectionDrawerMinimumWidth => 240f;
        public float SelectionDrawerMaximumWidth => 480f;
        public float MainModalMaximumWidthRatio => .92f;
        public float MainModalMaximumHeightRatio => .88f;
    }
}
