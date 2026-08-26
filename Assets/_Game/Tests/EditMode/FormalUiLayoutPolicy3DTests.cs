using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Graybox3D.Building;
using Object = UnityEngine.Object;

namespace WasteCity.Tests
{
    public sealed class FormalUiLayoutPolicy3DTests
    {
        [Test]
        public void IDEA0018_ProfileOwnsTheApprovedFormalCanvasContract()
        {
            FormalUiLayoutProfile3D profile =
                FormalUiLayoutProfile3D.Standard;

            Assert.That(
                profile.ReferenceResolution,
                Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(
                profile.ScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(
                profile.ScreenMatchMode,
                Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
            Assert.That(profile.MatchWidthOrHeight, Is.EqualTo(.5f));
            Assert.That(profile.ReferencePixelsPerUnit, Is.EqualTo(100f));
            Assert.That(profile.DesktopSafeMargin, Is.EqualTo(24f));
            Assert.That(profile.MinimumPhysicalFontPixels, Is.EqualTo(12f));
            Assert.That(profile.BuildingSortingOrder, Is.Zero);
            Assert.That(profile.OperationsSortingOrder, Is.EqualTo(50));
            Assert.That(profile.SystemMenuSortingOrder, Is.EqualTo(100));
            Assert.That(
                new[]
                {
                    profile.SpaceExtraSmall,
                    profile.SpaceSmall,
                    profile.SpaceMedium,
                    profile.SpaceLarge,
                    profile.SpaceExtraLarge,
                    profile.SpaceDoubleExtraLarge,
                },
                Is.EqualTo(new[] { 4f, 8f, 12f, 16f, 24f, 32f }));
            Assert.That(
                new[]
                {
                    profile.FontDescription,
                    profile.FontBody,
                    profile.FontEmphasis,
                    profile.FontSubtitle,
                    profile.FontTitle,
                },
                Is.EqualTo(new[] { 14f, 16f, 18f, 20f, 28f }));
            Assert.That(profile.StandardButtonMinimumHeight, Is.EqualTo(36f));
            Assert.That(profile.PrimaryButtonMinimumHeight, Is.EqualTo(44f));
            Assert.That(profile.BuildBarMaximumWidth, Is.EqualTo(620f));
            Assert.That(profile.BuildBarHeight, Is.EqualTo(54f));
            Assert.That(profile.AttentionStatusWidth, Is.EqualTo(260f));
        }

        [TestCase(1280f, 720f, 24f)]
        [TestCase(1366f, 768f, 24f)]
        [TestCase(1600f, 900f, 24f)]
        [TestCase(1920f, 1080f, 24f)]
        [TestCase(2560f, 1440f, 24f)]
        [TestCase(3440f, 1440f, 24f)]
        [TestCase(3840f, 2160f, 24f)]
        public void IDEA0018_PolicyKeepsPersistentSlotsInsideSafeArea(
            float width,
            float height,
            float expectedMargin)
        {
            var canvas = new Rect(0f, 0f, width, height);
            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                canvas,
                FormalUiLayoutProfile3D.Standard);

            Assert.That(layout.SafeArea.xMin, Is.EqualTo(expectedMargin));
            Assert.That(layout.SafeArea.yMin, Is.EqualTo(expectedMargin));
            Assert.That(
                layout.SafeArea.xMax,
                Is.EqualTo(width - expectedMargin));
            Assert.That(
                layout.SafeArea.yMax,
                Is.EqualTo(height - expectedMargin));

            Rect[] persistentSlots =
            {
                layout.DangerAndCoreSlot,
                layout.ResourceStatusSlot,
                layout.AttentionStatusSlot,
                layout.SpeedAndMenuSlot,
                layout.BuildFeedbackSlot,
                layout.BuildBarSlot,
                layout.SelectionDrawerSlot,
            };
            foreach (Rect slot in persistentSlots)
            {
                AssertRectHasPositiveSize(slot);
                AssertRectContains(layout.SafeArea, slot);
            }

            Assert.That(
                layout.DangerAndCoreSlot.Overlaps(
                    layout.ResourceStatusSlot),
                Is.False);
            Assert.That(
                layout.ResourceStatusSlot.Overlaps(
                    layout.AttentionStatusSlot),
                Is.False);
            Assert.That(
                layout.AttentionStatusSlot.Overlaps(
                    layout.SpeedAndMenuSlot),
                Is.False);
            Assert.That(
                layout.DangerAndCoreSlot.Overlaps(
                    layout.SpeedAndMenuSlot),
                Is.False);
            Assert.That(
                layout.BuildFeedbackSlot.Overlaps(layout.BuildBarSlot),
                Is.False);
            Assert.That(
                layout.SelectionDrawerSlot.Overlaps(layout.BuildBarSlot),
                Is.False);
            Assert.That(
                layout.SelectionDrawerSlot.Overlaps(
                    layout.BuildFeedbackSlot),
                Is.False);
            Assert.That(
                layout.SelectionDrawerSlot.Overlaps(
                    layout.SpeedAndMenuSlot),
                Is.False);
        }

        [TestCase(1280f, 720f)]
        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1440f)]
        public void IDEA0020_AttentionOwnsAFormalTopSlotWithoutOverlap(
            float width,
            float height)
        {
            var canvas = new Rect(0f, 0f, width, height);
            FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                canvas,
                FormalUiLayoutProfile3D.Standard);
            var property = typeof(FormalUiLayout3D).GetProperty(
                "AttentionStatusSlot");
            Assert.That(property, Is.Not.Null);
            var attention = (Rect)property.GetValue(layout);

            AssertRectHasPositiveSize(attention);
            AssertRectContains(layout.SafeArea, attention);
            Assert.That(attention.Overlaps(layout.ResourceStatusSlot),
                Is.False);
            Assert.That(attention.Overlaps(layout.SpeedAndMenuSlot),
                Is.False);
            Assert.That(attention.yMax, Is.EqualTo(
                layout.ResourceStatusSlot.yMin -
                FormalUiLayoutProfile3D.Standard.SpaceMedium).Within(.01f));
            Assert.That(attention.height, Is.EqualTo(
                layout.ResourceStatusSlot.height).Within(.01f));
            Assert.That(layout.ResourceStatusSlot.width,
                Is.GreaterThanOrEqualTo(680f));
        }

        [Test]
        public void IDEA0018_PolicyRejectsNonFiniteOrNonPositiveCanvas()
        {
            Rect[] invalidCanvases =
            {
                new Rect(0f, 0f, 0f, 1080f),
                new Rect(0f, 0f, 1920f, -1f),
                new Rect(float.NaN, 0f, 1920f, 1080f),
                new Rect(0f, 0f, float.PositiveInfinity, 1080f),
            };

            foreach (Rect invalid in invalidCanvases)
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => FormalUiLayoutPolicy3D.Calculate(
                        invalid,
                        FormalUiLayoutProfile3D.Standard));
            }
        }

        [Test]
        public void IDEA0018_CanvasHelperAppliesTheProfileIdempotently()
        {
            var root = new GameObject(
                "FormalCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                CanvasScaler first = FormalUiCanvasConfiguration3D.Apply(
                    canvas,
                    FormalUiLayoutProfile3D.Standard
                        .OperationsSortingOrder);
                CanvasScaler second = FormalUiCanvasConfiguration3D.Apply(
                    canvas,
                    FormalUiLayoutProfile3D.Standard
                        .OperationsSortingOrder);

                Assert.That(second, Is.SameAs(first));
                Assert.That(
                    root.GetComponents<CanvasScaler>(),
                    Has.Length.EqualTo(1));
                Assert.That(canvas.renderMode,
                    Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(canvas.sortingOrder, Is.EqualTo(50));
                Assert.That(
                    first.uiScaleMode,
                    Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(
                    first.referenceResolution,
                    Is.EqualTo(new Vector2(1920f, 1080f)));
                Assert.That(
                    first.screenMatchMode,
                    Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));
                Assert.That(first.matchWidthOrHeight, Is.EqualTo(.5f));
                Assert.That(first.referencePixelsPerUnit, Is.EqualTo(100f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(1280f, 720f, 2f / 3f, 1920f, 1080f, 18)]
        [TestCase(1920f, 1080f, 1f, 1920f, 1080f, 14)]
        [TestCase(3440f, 1440f, 4f / 3f, 2580f, 1080f, 14)]
        [TestCase(3840f, 2160f, 2f, 1920f, 1080f, 14)]
        public void IDEA0018_ExpandUsesCanvasLocalUnitsAndReadableFonts(
            float screenWidth,
            float screenHeight,
            float expectedScale,
            float expectedCanvasWidth,
            float expectedCanvasHeight,
            int expectedDescriptionFontSize)
        {
            var root = new GameObject(
                "FormalOverlayCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                CanvasScaler scaler = FormalUiCanvasConfiguration3D.Apply(
                    canvas,
                    FormalUiLayoutProfile3D.Standard.OperationsSortingOrder);
                Assert.That(canvas.renderMode,
                    Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Assert.That(scaler.screenMatchMode,
                    Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand));

                FormalUiCanvasMetrics3D metrics =
                    FormalUiCanvasConfiguration3D.CalculateMetrics(
                        new Vector2(screenWidth, screenHeight),
                        FormalUiLayoutProfile3D.Standard);
                Assert.That(metrics.ScaleFactor,
                    Is.EqualTo(expectedScale).Within(.0001f));
                Assert.That(metrics.CanvasSize.x,
                    Is.EqualTo(expectedCanvasWidth).Within(.01f));
                Assert.That(metrics.CanvasSize.y,
                    Is.EqualTo(expectedCanvasHeight).Within(.01f));

                FormalUiLayout3D layout = FormalUiLayoutPolicy3D.Calculate(
                    new Rect(Vector2.zero, metrics.CanvasSize));
                AssertRectContains(layout.SafeArea, layout.ResourceStatusSlot);
                AssertRectContains(layout.SafeArea, layout.BuildBarSlot);
                AssertRectContains(layout.SafeArea, layout.SelectionDrawerSlot);

                int fontSize = FormalUiCanvasConfiguration3D
                    .ResolveReadableFontSize(
                        FormalUiLayoutProfile3D.Standard.FontDescription,
                        metrics.ScaleFactor,
                        FormalUiLayoutProfile3D.Standard);
                Assert.That(fontSize, Is.EqualTo(expectedDescriptionFontSize));
                Assert.That(
                    fontSize * metrics.ScaleFactor,
                    Is.GreaterThanOrEqualTo(
                        FormalUiLayoutProfile3D.Standard
                            .MinimumPhysicalFontPixels));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IDEA0018_ReadableTextRefreshesAfterCanvasScaleChanges()
        {
            var root = new GameObject(
                "ResponsiveFontCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            var labelObject = new GameObject(
                "ResponsiveFontLabel",
                typeof(RectTransform),
                typeof(Text));
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.scaleFactor = 1f;
                labelObject.transform.SetParent(root.transform, false);
                Text label = labelObject.GetComponent<Text>();

                FormalUiCanvasConfiguration3D.ApplyReadableFontSize(
                    label,
                    FormalUiLayoutProfile3D.Standard.FontDescription);
                Assert.That(label.fontSize, Is.EqualTo(14));
                Assert.That(
                    label.GetComponent<FormalUiReadableText3D>(),
                    Is.Not.Null);

                canvas.scaleFactor = .5f;
                label.GetComponent<FormalUiReadableText3D>().Refresh();

                Assert.That(label.fontSize, Is.EqualTo(24));
                Assert.That(label.fontSize * canvas.scaleFactor,
                    Is.GreaterThanOrEqualTo(
                        FormalUiLayoutProfile3D.Standard
                            .MinimumPhysicalFontPixels));
            }
            finally
            {
                Object.DestroyImmediate(labelObject);
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertRectContains(Rect outer, Rect inner)
        {
            const float tolerance = .01f;
            Assert.That(
                inner.xMin,
                Is.GreaterThanOrEqualTo(outer.xMin - tolerance));
            Assert.That(
                inner.yMin,
                Is.GreaterThanOrEqualTo(outer.yMin - tolerance));
            Assert.That(
                inner.xMax,
                Is.LessThanOrEqualTo(outer.xMax + tolerance));
            Assert.That(
                inner.yMax,
                Is.LessThanOrEqualTo(outer.yMax + tolerance));
        }

        private static void AssertRectHasPositiveSize(Rect rect)
        {
            Assert.That(rect.width, Is.GreaterThan(0f));
            Assert.That(rect.height, Is.GreaterThan(0f));
        }
    }
}
