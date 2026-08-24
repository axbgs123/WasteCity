using System;
using UnityEngine;
using UnityEngine.UI;

namespace WasteCity.Graybox3D.Building
{
    public readonly struct FormalUiCanvasMetrics3D
    {
        public FormalUiCanvasMetrics3D(
            Vector2 screenSize,
            Vector2 canvasSize,
            float scaleFactor)
        {
            ScreenSize = screenSize;
            CanvasSize = canvasSize;
            ScaleFactor = scaleFactor;
        }

        public Vector2 ScreenSize { get; }
        public Vector2 CanvasSize { get; }
        public float ScaleFactor { get; }
    }

    [DisallowMultipleComponent]
    public sealed class FormalUiReadableText3D : MonoBehaviour
    {
        [SerializeField] private float designFontSize;
        private Text target;

        public void Configure(float value)
        {
            designFontSize = value;
            Refresh();
        }

        public void Refresh()
        {
            if (designFontSize <= 0f) return;
            target = target ?? GetComponent<Text>();
            if (target == null) return;
            target.fontSize = FormalUiCanvasConfiguration3D
                .ResolveReadableFontSize(transform, designFontSize);
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnRectTransformDimensionsChange()
        {
            Refresh();
        }

        private void OnCanvasHierarchyChanged()
        {
            Refresh();
        }
    }

    public static class FormalUiCanvasConfiguration3D
    {
        public static CanvasScaler Apply(
            Canvas canvas,
            int sortingOrder,
            FormalUiLayoutProfile3D profile = null)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            profile = profile ?? FormalUiLayoutProfile3D.Standard;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler =
                canvas.GetComponent<CanvasScaler>() ??
                canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = profile.ScaleMode;
            scaler.referenceResolution = profile.ReferenceResolution;
            scaler.screenMatchMode = profile.ScreenMatchMode;
            scaler.matchWidthOrHeight = profile.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = profile.ReferencePixelsPerUnit;
            return scaler;
        }

        public static FormalUiCanvasMetrics3D CalculateMetrics(
            Vector2 screenSize,
            FormalUiLayoutProfile3D profile = null)
        {
            if (!IsFinitePositive(screenSize.x) ||
                !IsFinitePositive(screenSize.y))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(screenSize),
                    "Screen size must be finite and positive.");
            }

            profile = profile ?? FormalUiLayoutProfile3D.Standard;
            Vector2 reference = profile.ReferenceResolution;
            float scaleFactor = Mathf.Min(
                screenSize.x / reference.x,
                screenSize.y / reference.y);
            return new FormalUiCanvasMetrics3D(
                screenSize,
                screenSize / scaleFactor,
                scaleFactor);
        }

        public static int ResolveReadableFontSize(
            float designFontSize,
            float scaleFactor,
            FormalUiLayoutProfile3D profile = null)
        {
            if (!IsFinitePositive(designFontSize))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(designFontSize),
                    "Font size must be finite and positive.");
            }

            if (!IsFinitePositive(scaleFactor))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scaleFactor),
                    "Scale factor must be finite and positive.");
            }

            profile = profile ?? FormalUiLayoutProfile3D.Standard;
            return Mathf.CeilToInt(Mathf.Max(
                designFontSize,
                profile.MinimumPhysicalFontPixels / scaleFactor));
        }

        public static int ResolveReadableFontSize(
            Transform parent,
            float designFontSize,
            FormalUiLayoutProfile3D profile = null)
        {
            profile = profile ?? FormalUiLayoutProfile3D.Standard;
            Canvas canvas = parent == null
                ? null
                : parent.GetComponentInParent<Canvas>();
            float scaleFactor = 1f;
            if (canvas != null &&
                canvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                IsFinitePositive(canvas.pixelRect.width) &&
                IsFinitePositive(canvas.pixelRect.height))
            {
                scaleFactor = CalculateMetrics(
                    canvas.pixelRect.size,
                    profile).ScaleFactor;
            }
            else if (canvas != null && IsFinitePositive(canvas.scaleFactor))
            {
                scaleFactor = canvas.scaleFactor;
            }

            return ResolveReadableFontSize(
                designFontSize,
                scaleFactor,
                profile);
        }

        public static FormalUiReadableText3D ApplyReadableFontSize(
            Text text,
            float designFontSize)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            FormalUiReadableText3D readable =
                text.GetComponent<FormalUiReadableText3D>() ??
                text.gameObject.AddComponent<FormalUiReadableText3D>();
            readable.Configure(designFontSize);
            return readable;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
