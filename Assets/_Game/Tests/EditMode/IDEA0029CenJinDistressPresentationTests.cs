using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Exploration;
using WasteCity.Leader.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029CenJinDistressPresentationTests
    {
        private readonly List<Object> cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null)
                    Object.DestroyImmediate(cleanup[index]);
            cleanup.Clear();
        }

        [Test]
        public void DistressMarkerKeepsStableObjectAndHidesUndiscoveredSite()
        {
            GameObject root = Track(new GameObject("DistressRoot"));
            Texture2D texture = Track(new Texture2D(8, 8));
            Sprite sprite = Track(Sprite.Create(
                texture,
                new Rect(0, 0, 8, 8),
                new Vector2(.5f, .5f),
                8f));
            var presenter = root.AddComponent<
                GrayboxCenJinDistressPresenter3D>();
            presenter.Configure(
                root.transform,
                new PlanarCoordinateMapper3D(64, 48),
                sprite,
                new Rect(.25f, .1f, .5f, .8f));
            GameObject marker = presenter.MarkerObject;

            Assert.That(marker.transform.localScale.x,
                Is.EqualTo(2.7f).Within(.001f));

            presenter.Apply(CenJinDistressState.Undiscovered);
            Assert.That(marker.activeSelf, Is.False);

            presenter.Apply(CenJinDistressState.Discovered);
            Assert.That(marker.activeSelf, Is.True);
            Assert.That(presenter.MarkerObject, Is.SameAs(marker));
            Assert.That(presenter.Renderer.sprite, Is.SameAs(sprite));

            presenter.Apply(CenJinDistressState.RescuedTimely);
            Assert.That(marker.activeSelf, Is.False);
            Assert.That(presenter.MarkerObject, Is.SameAs(marker));
        }

        private T Track<T>(T value) where T : Object
        {
            cleanup.Add(value);
            return value;
        }
    }
}
