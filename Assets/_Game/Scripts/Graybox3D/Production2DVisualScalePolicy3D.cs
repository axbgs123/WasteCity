using UnityEngine;
using UnityEngine.UI;

namespace WasteCity.Graybox3D
{
    public readonly struct Production2DVisualFraming3D
    {
        public Production2DVisualFraming3D(
            Rect visibleBounds,
            float scale,
            Vector2 normalizedOffset,
            float visibleMaxFill)
        {
            VisibleBounds = visibleBounds;
            Scale = scale;
            NormalizedOffset = normalizedOffset;
            VisibleMaxFill = visibleMaxFill;
        }

        public Rect VisibleBounds { get; }
        public float Scale { get; }
        public Vector2 NormalizedOffset { get; }
        public float VisibleMaxFill { get; }
    }

    /// <summary>
    /// IDEA-0025 centralized subject framing for transparent production art.
    /// It normalizes the visible Alpha subject, not the source PNG canvas.
    /// </summary>
    public static class Production2DVisualScalePolicy3D
    {
        public const float ItemVisibleFill = .78f;
        public const float TechnologyVisibleFill = .78f;
        public const float BuildingVisibleFill = .82f;
        public const float CharacterVisibleFill = .82f;
        public const float WorldMarkerVisibleFill = .8f;
        public const float UnitVisibleFill = .82f;

        public static Production2DVisualFraming3D Resolve(
            Production2DVisualClass visualClass,
            Rect visibleBounds)
        {
            Rect valid = IsValid(visibleBounds)
                ? visibleBounds
                : new Rect(0f, 0f, 1f, 1f);
            float targetFill = TargetVisibleFill(visualClass);
            float maxDimension = Mathf.Max(valid.width, valid.height);
            float scale = targetFill / Mathf.Max(.001f, maxDimension);
            Vector2 offset = Vector2.one * .5f - valid.center * scale;
            return new Production2DVisualFraming3D(
                valid,
                scale,
                offset,
                maxDimension * scale);
        }

        public static void ApplyToUiImage(
            Image image,
            Production2DVisualClass visualClass,
            Rect visibleBounds,
            Vector2 baseAnchoredPosition)
        {
            if (image == null) return;
            Production2DVisualFraming3D framing = Resolve(
                visualClass,
                visibleBounds);
            RectTransform rect = image.rectTransform;
            rect.localScale = Vector3.one * framing.Scale;
            Vector2 slotSize = rect.rect.size;
            if (slotSize.x <= 0f || slotSize.y <= 0f)
                slotSize = rect.sizeDelta;
            Vector2 centerCorrection =
                -(framing.VisibleBounds.center - Vector2.one * .5f) *
                framing.Scale;
            rect.anchoredPosition = baseAnchoredPosition + new Vector2(
                centerCorrection.x * slotSize.x,
                centerCorrection.y * slotSize.y);
        }

        public static bool IsValid(Rect bounds)
        {
            return IsFinite(bounds.xMin) && IsFinite(bounds.yMin) &&
                IsFinite(bounds.width) && IsFinite(bounds.height) &&
                bounds.xMin >= 0f && bounds.yMin >= 0f &&
                bounds.xMax <= 1f && bounds.yMax <= 1f &&
                bounds.width > 0f && bounds.height > 0f;
        }

        public static float ResolveSpriteWorldScale(
            Sprite sprite,
            Rect visibleBounds,
            float targetVisibleWidth)
        {
            if (sprite == null || !IsValid(visibleBounds)) return 0f;
            float visibleWidth = sprite.bounds.size.x * visibleBounds.width;
            return Mathf.Max(0f, targetVisibleWidth) /
                Mathf.Max(.001f, visibleWidth);
        }

        public static float ResolveVisibleBottomLocal(
            Sprite sprite,
            Rect visibleBounds,
            float scale)
        {
            return ResolveVisibleAnchorLocal(
                sprite,
                visibleBounds,
                new Vector2(.5f, 0f),
                scale).y;
        }

        public static Vector2 ResolveVisibleAnchorLocal(
            Sprite sprite,
            Rect visibleBounds,
            Vector2 normalizedVisibleAnchor,
            float scale)
        {
            if (sprite == null || !IsValid(visibleBounds))
                return Vector2.zero;
            float pivotX = sprite.rect.width <= 0f
                ? .5f
                : sprite.pivot.x / sprite.rect.width;
            float pivotY = sprite.rect.height <= 0f
                ? .5f
                : sprite.pivot.y / sprite.rect.height;
            float anchorX = Mathf.Lerp(
                visibleBounds.xMin,
                visibleBounds.xMax,
                Mathf.Clamp01(normalizedVisibleAnchor.x));
            float anchorY = Mathf.Lerp(
                visibleBounds.yMin,
                visibleBounds.yMax,
                Mathf.Clamp01(normalizedVisibleAnchor.y));
            float validScale = Mathf.Max(0f, scale);
            return new Vector2(
                (anchorX - pivotX) * sprite.bounds.size.x * validScale,
                (anchorY - pivotY) * sprite.bounds.size.y * validScale);
        }

        private static float TargetVisibleFill(
            Production2DVisualClass visualClass)
        {
            switch (visualClass)
            {
                case Production2DVisualClass.Item:
                    return ItemVisibleFill;
                case Production2DVisualClass.Technology:
                    return TechnologyVisibleFill;
                case Production2DVisualClass.Building:
                    return BuildingVisibleFill;
                case Production2DVisualClass.Character:
                    return CharacterVisibleFill;
                case Production2DVisualClass.WorldMarker:
                    return WorldMarkerVisibleFill;
                case Production2DVisualClass.Unit:
                    return UnitVisibleFill;
                default:
                    return 1f;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
