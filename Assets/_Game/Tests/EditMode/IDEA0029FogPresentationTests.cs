using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.Graybox3D.Exploration;
using WasteCity.World;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029FogPresentationTests
    {
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void IDEA0029_OverlayProjectsHiddenExploredAndVisibleWithoutPerCellObjects()
        {
            const int width = 20;
            const int height = 16;
            GrayboxFogPresenter3D presenter = CreatePresenter(width, height);
            var visibility = new WorldVisibilityRuntime(width, height);

            Assert.That(presenter.PersistentObjectCount, Is.EqualTo(1));
            Assert.That(presenter.GetPresentedState(2, 2),
                Is.EqualTo(WorldVisibilityState.Hidden));
            Assert.That(presenter.GetMaskColor(2, 2).a,
                Is.EqualTo(GrayboxFogVisualPolicy3D.HiddenAlpha));

            visibility.UpsertSource(new WorldVisionSource(
                "test.primary",
                WorldVisionSourceKind.PrimaryCity,
                2,
                2,
                true,
                1));
            Assert.That(presenter.ApplyVisibility(visibility), Is.True);
            Assert.That(presenter.GetPresentedState(2, 2),
                Is.EqualTo(WorldVisibilityState.Visible));
            Assert.That(presenter.GetMaskColor(2, 2).a, Is.EqualTo(0));
            Assert.That(presenter.GetPresentedState(19, 15),
                Is.EqualTo(WorldVisibilityState.Hidden));

            visibility.RemoveSource("test.primary");
            Assert.That(presenter.ApplyVisibility(visibility), Is.True);
            Assert.That(presenter.GetPresentedState(2, 2),
                Is.EqualTo(WorldVisibilityState.Explored));
            Assert.That(presenter.GetMaskColor(2, 2).a,
                Is.EqualTo(GrayboxFogVisualPolicy3D.ExploredAlpha));
            Assert.That(presenter.PersistentObjectCount, Is.EqualTo(1));
        }

        [Test]
        public void IDEA0029_OverlayShaderBindsOnePointFilteredMask()
        {
            GrayboxFogPresenter3D presenter = CreatePresenter(10, 8);
            MeshRenderer renderer =
                presenter.OverlayObject.GetComponent<MeshRenderer>();

            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.Not.Null);
            Assert.That(renderer.sharedMaterial.shader.name,
                Is.EqualTo("WasteCity/World/ExplorationFogOverlay"));
            Assert.That(renderer.sharedMaterial.GetTexture("_FogMask"),
                Is.SameAs(presenter.MaskTexture));
            Assert.That(presenter.MaskTexture.filterMode,
                Is.EqualTo(FilterMode.Point));
            Assert.That(presenter.MaskTexture.wrapMode,
                Is.EqualTo(TextureWrapMode.Clamp));
        }

        [Test]
        public void IDEA0029_RevisionGateOnlyCommitsDirtyPixelsAndKeepsStableObjects()
        {
            const int width = 16;
            const int height = 12;
            GrayboxFogPresenter3D presenter = CreatePresenter(width, height);
            var visibility = new WorldVisibilityRuntime(width, height);
            Texture2D texture = presenter.MaskTexture;
            Mesh mesh = presenter.OverlayMesh;
            GameObject overlay = presenter.OverlayObject;

            Assert.That(presenter.ApplyVisibility(visibility), Is.False);
            Assert.That(presenter.ApplyVisibility(visibility), Is.False);
            Assert.That(presenter.LastDirtyCellCount, Is.Zero);

            visibility.UpsertSource(new WorldVisionSource(
                "test.primary",
                WorldVisionSourceKind.PrimaryCity,
                3,
                3,
                true,
                1));
            Assert.That(presenter.ApplyVisibility(visibility), Is.True);
            Assert.That(presenter.LastDirtyCellCount, Is.GreaterThan(0));
            Assert.That(presenter.LastDirtyCellCount,
                Is.LessThan(width * height));
            Assert.That(presenter.MaskApplyCount, Is.EqualTo(1));

            visibility.UpsertSource(new WorldVisionSource(
                "test.primary",
                WorldVisionSourceKind.PrimaryCity,
                5,
                3,
                true,
                2));
            Assert.That(presenter.ApplyVisibility(visibility), Is.True);
            Assert.That(presenter.LastDirtyCellCount, Is.GreaterThan(0));
            Assert.That(presenter.LastDirtyCellCount,
                Is.LessThan(width * height));
            Assert.That(presenter.MaskApplyCount, Is.EqualTo(2));
            Assert.That(presenter.MaskTexture, Is.SameAs(texture));
            Assert.That(presenter.OverlayMesh, Is.SameAs(mesh));
            Assert.That(presenter.OverlayObject, Is.SameAs(overlay));

            Assert.That(presenter.ApplyVisibility(visibility), Is.False);
            Assert.That(presenter.LastDirtyCellCount, Is.Zero);
            Assert.That(presenter.MaskApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void IDEA0029_ResourceMarkersRequireExplicitLiveOrLastIntelPolicy()
        {
            WorldMapModel model = CreateResourceMap();
            GrayboxWorldView3D view = CreateWorldView();
            view.Generate(model);
            Assert.That(view.TryGetResourceNodeMarker(
                    0,
                    0,
                    out GrayboxResourceNodeMarker3D marker),
                Is.True);

            Assert.That(view.TrySetResourceMarkerFogPresentation(
                    0,
                    0,
                    ResourceMarkerFogPresentation3D.Hidden,
                    out string hiddenError),
                Is.True,
                hiddenError);
            Assert.That(marker.gameObject.activeSelf, Is.False);
            Assert.That(view.SetResourceMarkerGuidanceOverride(0, 0, true),
                Is.False,
                "Hidden resource markers must not leak through guidance.");

            Assert.That(view.TrySetResourceMarkerFogPresentation(
                    0,
                    0,
                    ResourceMarkerFogPresentation3D.LastIntel(42),
                    out string intelError),
                Is.True,
                intelError);
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.DisplayedAmount, Is.EqualTo(42));
            Assert.That(model.Harvest(0, 0, 10, out _), Is.EqualTo(10));
            Assert.That(view.RefreshResourceNodeMarkers(), Is.False);
            Assert.That(marker.DisplayedAmount, Is.EqualTo(42),
                "Live resource mutation must not penetrate last intel.");

            Assert.That(view.TrySetResourceMarkerFogPresentation(
                    0,
                    0,
                    ResourceMarkerFogPresentation3D.LastKnownIdentity,
                    out string identityError),
                Is.True,
                identityError);
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.DisplayText, Is.Empty,
                "Expired intel may retain identity but not mutable amount.");

            Assert.That(view.TrySetResourceMarkerFogPresentation(
                    0,
                    0,
                    ResourceMarkerFogPresentation3D.Live,
                    out string liveError),
                Is.True,
                liveError);
            Assert.That(marker.gameObject.activeSelf, Is.True);
            Assert.That(marker.DisplayedAmount, Is.EqualTo(90));
        }

        [Test]
        public void IDEA0029_ResourceMarkerPolicyRejectsUnknownCellsAndInvalidIntel()
        {
            GrayboxWorldView3D view = CreateWorldView();
            view.Generate(CreateResourceMap());

            Assert.That(view.TrySetResourceMarkerFogPresentation(
                    4,
                    4,
                    ResourceMarkerFogPresentation3D.Live,
                    out string unknownError),
                Is.False);
            Assert.That(unknownError, Does.Contain("resource marker"));
            Assert.That(view.TrySetResourceMarkerFogPresentation(
                    0,
                    0,
                    ResourceMarkerFogPresentation3D.LastIntel(-1),
                    out string amountError),
                Is.False);
            Assert.That(amountError, Does.Contain("amount"));
        }

        private GrayboxFogPresenter3D CreatePresenter(int width, int height)
        {
            var root = Track(new GameObject("FogRoot"));
            Material material = Track(CreateFogMaterial());
            GrayboxFogPresenter3D presenter =
                root.AddComponent<GrayboxFogPresenter3D>();
            presenter.Configure(root.transform, material);
            presenter.Generate(new PlanarCoordinateMapper3D(width, height));
            return presenter;
        }

        private GrayboxWorldView3D CreateWorldView()
        {
            GameObject root = Track(new GameObject("World"));
            Transform terrain = NewChild(root.transform, "Terrain");
            Transform resources = NewChild(root.transform, "Resources");
            Transform obstacles = NewChild(root.transform, "Obstacles");
            GrayboxWorldView3D view =
                root.AddComponent<GrayboxWorldView3D>();
            view.Configure(
                terrain,
                resources,
                obstacles,
                Track(CreateTestMaterial()));
            return view;
        }

        private static WorldMapModel CreateResourceMap()
        {
            var cells = new WorldCell[2, 2];
            cells[0, 0] = new WorldCell(
                TerrainKind.Rocky,
                ResourceIds.Iron,
                100);
            cells[1, 0] = new WorldCell(TerrainKind.Wasteland, null, 0);
            cells[0, 1] = new WorldCell(TerrainKind.Wasteland, null, 0);
            cells[1, 1] = new WorldCell(TerrainKind.Wasteland, null, 0);
            return new WorldMapModel(cells);
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static Material CreateTestMaterial()
        {
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            return new Material(shader);
        }

        private static Material CreateFogMaterial()
        {
            Shader shader = Shader.Find(
                "WasteCity/World/ExplorationFogOverlay");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            return new Material(shader);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }
    }
}
