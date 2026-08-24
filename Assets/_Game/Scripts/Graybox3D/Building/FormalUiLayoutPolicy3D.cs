using System;
using UnityEngine;

namespace WasteCity.Graybox3D.Building
{
    public readonly struct FormalUiLayout3D
    {
        public FormalUiLayout3D(
            Rect canvas,
            Rect safeArea,
            Rect dangerAndCoreSlot,
            Rect resourceStatusSlot,
            Rect speedAndMenuSlot,
            Rect buildFeedbackSlot,
            Rect buildBarSlot,
            Rect selectionDrawerSlot,
            Rect mainModalArea)
        {
            Canvas = canvas;
            SafeArea = safeArea;
            DangerAndCoreSlot = dangerAndCoreSlot;
            ResourceStatusSlot = resourceStatusSlot;
            SpeedAndMenuSlot = speedAndMenuSlot;
            BuildFeedbackSlot = buildFeedbackSlot;
            BuildBarSlot = buildBarSlot;
            SelectionDrawerSlot = selectionDrawerSlot;
            MainModalArea = mainModalArea;
        }

        public Rect Canvas { get; }
        public Rect SafeArea { get; }
        public Rect DangerAndCoreSlot { get; }
        public Rect ResourceStatusSlot { get; }
        public Rect SpeedAndMenuSlot { get; }
        public Rect BuildFeedbackSlot { get; }
        public Rect BuildBarSlot { get; }
        public Rect SelectionDrawerSlot { get; }
        public Rect MainModalArea { get; }
    }

    public static class FormalUiLayoutPolicy3D
    {
        public static FormalUiLayout3D Calculate(
            Rect canvas,
            FormalUiLayoutProfile3D profile = null)
        {
            profile = profile ?? FormalUiLayoutProfile3D.Standard;
            ValidateCanvas(canvas);

            float margin = profile.DesktopSafeMargin;
            Rect safeArea = Rect.MinMaxRect(
                canvas.xMin + margin,
                canvas.yMin + margin,
                canvas.xMax - margin,
                canvas.yMax - margin);
            float gap = profile.SpaceMedium;
            float topHeight = profile.PersistentTopHeight;

            float dangerWidth = Mathf.Min(
                safeArea.width * profile.DangerSlotWidthRatio,
                profile.DangerSlotMaximumWidth);
            float speedWidth = Mathf.Min(
                safeArea.width * profile.SpeedSlotWidthRatio,
                profile.SpeedSlotMaximumWidth);
            float resourceWidth =
                safeArea.width - dangerWidth - speedWidth - gap * 2f;
            Rect danger = new Rect(
                safeArea.xMin,
                safeArea.yMax - topHeight,
                dangerWidth,
                topHeight);
            Rect resources = new Rect(
                danger.xMax + gap,
                danger.y,
                resourceWidth,
                topHeight);
            Rect speed = new Rect(
                resources.xMax + gap,
                danger.y,
                speedWidth,
                topHeight);

            float buildWidth = Mathf.Min(
                safeArea.width * profile.BuildBarWidthRatio,
                profile.BuildBarMaximumWidth);
            Rect buildBar = new Rect(
                safeArea.center.x - buildWidth * .5f,
                safeArea.yMin,
                buildWidth,
                profile.BuildBarHeight);
            float feedbackWidth = Mathf.Min(
                safeArea.width * profile.BuildFeedbackWidthRatio,
                profile.BuildFeedbackMaximumWidth);
            Rect feedback = new Rect(
                safeArea.center.x - feedbackWidth * .5f,
                buildBar.yMax + gap,
                feedbackWidth,
                profile.BuildFeedbackHeight);

            float drawerWidth = Mathf.Clamp(
                safeArea.width * profile.SelectionDrawerWidthRatio,
                profile.SelectionDrawerMinimumWidth,
                profile.SelectionDrawerMaximumWidth);
            float drawerBottom = Mathf.Max(
                buildBar.yMax,
                feedback.yMax) + gap;
            float drawerTop = speed.yMin - gap;
            Rect drawer = new Rect(
                safeArea.xMax - drawerWidth,
                drawerBottom,
                drawerWidth,
                drawerTop - drawerBottom);

            float modalWidth =
                safeArea.width * profile.MainModalMaximumWidthRatio;
            float modalHeight =
                safeArea.height * profile.MainModalMaximumHeightRatio;
            Rect modal = new Rect(
                safeArea.center.x - modalWidth * .5f,
                safeArea.center.y - modalHeight * .5f,
                modalWidth,
                modalHeight);

            return new FormalUiLayout3D(
                canvas,
                safeArea,
                danger,
                resources,
                speed,
                feedback,
                buildBar,
                drawer,
                modal);
        }

        private static void ValidateCanvas(Rect canvas)
        {
            if (!IsFinite(canvas.x) ||
                !IsFinite(canvas.y) ||
                !IsFinite(canvas.width) ||
                !IsFinite(canvas.height) ||
                canvas.width <= 0f ||
                canvas.height <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(canvas),
                    "Canvas must have finite coordinates and positive size.");
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
