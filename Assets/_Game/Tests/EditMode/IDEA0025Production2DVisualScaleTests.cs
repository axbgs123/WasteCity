using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using WasteCity.Graybox3D;

namespace WasteCity.Tests
{
    public sealed class IDEA0025Production2DVisualScaleTests
    {
        [Test]
        public void SameSemanticSlotNormalizesDifferentTransparentMargins()
        {
            Rect compact = new Rect(.15f, .18f, .7f, .64f);
            Rect padded = new Rect(.365f, .2f, .27f, .6f);

            Production2DVisualFraming3D compactFrame =
                Production2DVisualScalePolicy3D.Resolve(
                    Production2DVisualClass.Technology,
                    compact);
            Production2DVisualFraming3D paddedFrame =
                Production2DVisualScalePolicy3D.Resolve(
                    Production2DVisualClass.Technology,
                    padded);

            Assert.That(compactFrame.VisibleMaxFill,
                Is.EqualTo(paddedFrame.VisibleMaxFill).Within(.001f));
            Assert.That(compactFrame.VisibleMaxFill,
                Is.EqualTo(.78f).Within(.001f));
            Assert.That(paddedFrame.Scale, Is.GreaterThan(compactFrame.Scale));
        }

        [Test]
        public void FramingCentersTheVisibleSubjectInsteadOfThePngCanvas()
        {
            Rect visible = new Rect(.1f, .2f, .4f, .6f);
            Production2DVisualFraming3D frame =
                Production2DVisualScalePolicy3D.Resolve(
                    Production2DVisualClass.Item,
                    visible);

            Vector2 transformedCenter =
                visible.center * frame.Scale + frame.NormalizedOffset;
            Assert.That(transformedCenter.x, Is.EqualTo(.5f).Within(.001f));
            Assert.That(transformedCenter.y, Is.EqualTo(.5f).Within(.001f));
        }

        [Test]
        public void CatalogEntryRejectsInvalidVisibleBounds()
        {
            var texture = new Texture2D(8, 8);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                Vector2.one * .5f);
            try
            {
                var catalog = ScriptableObject.CreateInstance<
                    Production2DVisualCatalog3D>();
                catalog.Configure(
                    new[]
                    {
                        new Production2DVisualEntry3D(
                            Production2DVisualClass.Item,
                            "test.invalid-bounds",
                            Production2DVisualCatalog3D.DefaultVariant,
                            sprite,
                            new Rect(0f, 0f, 0f, 0f)),
                    },
                    null);

                Assert.That(catalog.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("visible Alpha bounds"));
                Object.DestroyImmediate(catalog);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void WorldScaleUsesVisibleSubjectWidthInsteadOfCanvasWidth()
        {
            var texture = new Texture2D(100, 100);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 100f, 100f),
                Vector2.one * .5f,
                100f);
            try
            {
                Rect visible = new Rect(.3f, .2f, .4f, .6f);
                float scale = Production2DVisualScalePolicy3D
                    .ResolveSpriteWorldScale(sprite, visible, .8f);
                Assert.That(sprite.bounds.size.x * visible.width * scale,
                    Is.EqualTo(.8f).Within(.001f));
                Assert.That(Production2DVisualScalePolicy3D
                        .ResolveVisibleBottomLocal(sprite, visible, scale),
                    Is.EqualTo(-.6f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void StretchUiSlotCentersUsingItsRenderedRect()
        {
            var parentObject = new GameObject(
                "VisualScale.Parent",
                typeof(RectTransform));
            var imageObject = new GameObject(
                "VisualScale.Image",
                typeof(RectTransform),
                typeof(Image));
            try
            {
                RectTransform parent = parentObject.GetComponent<RectTransform>();
                parent.sizeDelta = new Vector2(200f, 100f);
                RectTransform rect = imageObject.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                var basePosition = new Vector2(7f, -3f);
                Rect visible = new Rect(.1f, .2f, .4f, .6f);

                Production2DVisualScalePolicy3D.ApplyToUiImage(
                    imageObject.GetComponent<Image>(),
                    Production2DVisualClass.Item,
                    visible,
                    basePosition);

                Production2DVisualFraming3D frame =
                    Production2DVisualScalePolicy3D.Resolve(
                        Production2DVisualClass.Item,
                        visible);
                Vector2 expected = basePosition -
                    (visible.center - Vector2.one * .5f) *
                    frame.Scale * rect.rect.size;
                Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));
                Assert.That(rect.anchoredPosition.x,
                    Is.EqualTo(expected.x).Within(.001f));
                Assert.That(rect.anchoredPosition.y,
                    Is.EqualTo(expected.y).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(imageObject);
                Object.DestroyImmediate(parentObject);
            }
        }

        [Test]
        public void WorldMarkerAnchorUsesVisibleSubjectBounds()
        {
            var texture = new Texture2D(100, 100);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 100f, 100f),
                Vector2.one * .5f,
                100f);
            try
            {
                Rect visible = new Rect(.2f, .25f, .5f, .6f);
                Vector2 anchor = Production2DVisualScalePolicy3D
                    .ResolveVisibleAnchorLocal(
                        sprite,
                        visible,
                        new Vector2(.5f, 0f),
                        2f);
                Assert.That(anchor.x, Is.EqualTo(-.1f).Within(.001f));
                Assert.That(anchor.y, Is.EqualTo(-.5f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
