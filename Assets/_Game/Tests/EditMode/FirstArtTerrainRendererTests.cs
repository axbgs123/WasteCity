using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using WasteCity.ArtIntegration3D;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class FirstArtTerrainRendererTests
    {
        private const string ProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/" +
            "Profiles/FirstArtTerrainProfile3D.asset";

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
        public void TryPresent_CreatesOneSharedMaterialSurfaceAndHidesSevenFallbacks()
        {
            GrayboxWorldView3D view = CreateCatalogView();
            FirstArtTerrainProfile3D profile = LoadApprovedProfile();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(profile);

            Assert.That(presenter.TryPresent(view), Is.True);

            Assert.That(presenter.Profile, Is.SameAs(profile));
            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(presenter.SurfaceRenderer, Is.Not.Null);
            Assert.That(presenter.ControlMaps, Is.Not.Null);
            Assert.That(
                presenter.SurfaceRenderer.sharedMaterial,
                Is.SameAs(profile.Material));
            Assert.That(
                presenter.GetComponentsInChildren<MeshFilter>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                presenter.GetComponentsInChildren<MeshRenderer>(true).Length,
                Is.EqualTo(1));
            Assert.That(
                presenter.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(RuntimeSurfaceCount(presenter), Is.EqualTo(1));
            AssertFallbackState(view, false);
            AssertPropertyBlock(presenter, profile, 12, 1);
        }

        [Test]
        public void ClearPresentation_IsIdempotentAndDestroysOnlyOwnedRuntimeResources()
        {
            GrayboxWorldView3D view = CreateCatalogView();
            FirstArtTerrainProfile3D profile = LoadApprovedProfile();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(profile);
            Assert.That(presenter.TryPresent(view), Is.True);
            Mesh mesh = presenter.SurfaceRenderer
                .GetComponent<MeshFilter>().sharedMesh;
            Texture2D controlA = presenter.ControlMaps.ControlA;
            Texture2D controlB = presenter.ControlMaps.ControlB;
            Material sharedMaterial = profile.Material;
            Texture2DArray sharedBaseColor = profile.BaseColorArray;

            presenter.ClearPresentation();
            presenter.ClearPresentation();

            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(presenter.SurfaceRenderer, Is.Null);
            Assert.That(presenter.ControlMaps, Is.Null);
            Assert.That(RuntimeSurfaceCount(presenter), Is.Zero);
            Assert.That(mesh == null, Is.True);
            Assert.That(controlA == null, Is.True);
            Assert.That(controlB == null, Is.True);
            Assert.That(profile.Material, Is.SameAs(sharedMaterial));
            Assert.That(profile.BaseColorArray, Is.SameAs(sharedBaseColor));
            Assert.That(sharedMaterial, Is.Not.Null);
            Assert.That(sharedBaseColor, Is.Not.Null);
            AssertFallbackState(view, true);
        }

        [Test]
        public void DisableThenEnable_RestoresFallbackAndRecreatesExactlyOneSurface()
        {
            GrayboxWorldView3D view = CreateCatalogView();
            FirstArtTerrainProfile3D profile = LoadApprovedProfile();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(profile);
            Assert.That(presenter.TryPresent(view), Is.True);

            presenter.enabled = false;

            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(RuntimeSurfaceCount(presenter), Is.Zero);
            AssertFallbackState(view, true);

            presenter.enabled = true;

            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(RuntimeSurfaceCount(presenter), Is.EqualTo(1));
            Assert.That(
                presenter.SurfaceRenderer.sharedMaterial,
                Is.SameAs(profile.Material));
            AssertFallbackState(view, false);
        }

        [Test]
        public void DestroyPresenter_RestoresAllFallbacksWithoutResurrection()
        {
            GrayboxWorldView3D view = CreateCatalogView();
            FirstArtTerrainRenderer3D presenter =
                CreatePresenter(LoadApprovedProfile());
            GameObject owner = presenter.gameObject;
            Assert.That(presenter.TryPresent(view), Is.True);

            UnityEngine.Object.DestroyImmediate(presenter);

            Assert.That(
                owner.GetComponentsInChildren<MeshRenderer>(true),
                Is.Empty);
            Assert.That(
                owner.transform.Cast<Transform>()
                    .Count(child => child.name == "RuntimeSurface"),
                Is.Zero);
            AssertFallbackState(view, true);
        }

        [Test]
        public void TryPresent_MissingProfileRollsBackWithoutFormalObject()
        {
            AssertFailedPresentation(null);
        }

        [Test]
        public void TryPresent_WrongShaderRollsBackWithoutFormalObject()
        {
            FirstArtTerrainProfile3D approved = LoadApprovedProfile();
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material wrongMaterial = Track(new Material(shader));
            FirstArtTerrainProfile3D profile =
                Track(ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>());
            profile.Configure(
                wrongMaterial,
                approved.BaseColorArray,
                approved.NormalArray,
                approved.MaskArray,
                approved.HeightArray);

            AssertFailedPresentation(profile);
        }

        [Test]
        public void TryPresent_WrongArrayDepthRollsBackWithoutFormalObject()
        {
            FirstArtTerrainProfile3D approved = LoadApprovedProfile();
            Texture2DArray wrongDepth = Track(
                new Texture2DArray(
                    1,
                    1,
                    FirstArtTerrainCatalog3D.LayerCount - 1,
                    TextureFormat.RGBA32,
                    false,
                    false));
            FirstArtTerrainProfile3D profile =
                Track(ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>());
            profile.Configure(
                approved.Material,
                wrongDepth,
                approved.NormalArray,
                approved.MaskArray,
                approved.HeightArray);

            AssertFailedPresentation(profile);
        }

        [Test]
        public void TryPresent_ControlGenerationExceptionDestroysLocalMeshAndRollsBack()
        {
            FirstArtTerrainProfile3D approved = LoadApprovedProfile();
            FirstArtTerrainProfile3D profile =
                Track(ScriptableObject.CreateInstance<FirstArtTerrainProfile3D>());
            profile.Configure(
                approved.Material,
                approved.BaseColorArray,
                approved.NormalArray,
                approved.MaskArray,
                approved.HeightArray);
            FieldInfo pixelsPerCell =
                typeof(FirstArtTerrainProfile3D).GetField(
                    "controlPixelsPerCell",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(pixelsPerCell, Is.Not.Null);
            pixelsPerCell.SetValue(profile, 5);

            AssertFailedPresentation(profile);
        }

        private void AssertFailedPresentation(
            FirstArtTerrainProfile3D profile)
        {
            GrayboxWorldView3D view = CreateCatalogView();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(profile);
            int surfaceMeshCount = RuntimeSurfaceMeshCount();
            LogAssert.Expect(
                LogType.Error,
                new Regex("First-art terrain presentation failed:"));

            Assert.That(presenter.TryPresent(view), Is.False);

            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(presenter.SurfaceRenderer, Is.Null);
            Assert.That(presenter.ControlMaps, Is.Null);
            Assert.That(RuntimeSurfaceCount(presenter), Is.Zero);
            Assert.That(
                RuntimeSurfaceMeshCount(),
                Is.EqualTo(surfaceMeshCount));
            AssertFallbackState(view, true);
        }

        private FirstArtTerrainRenderer3D CreatePresenter(
            FirstArtTerrainProfile3D profile)
        {
            var owner = Track(new GameObject("FirstArtTerrainRenderer"));
            FirstArtTerrainRenderer3D presenter =
                owner.AddComponent<FirstArtTerrainRenderer3D>();
            presenter.runInEditMode = true;
            presenter.Configure(profile);
            return presenter;
        }

        private GrayboxWorldView3D CreateCatalogView()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());
            return view;
        }

        private GrayboxWorldView3D CreateView()
        {
            GameObject root = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            GrayboxWorldView3D view =
                root.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            return view;
        }

        private static FirstArtTerrainProfile3D LoadApprovedProfile()
        {
            FirstArtTerrainProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(
                    ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(
                profile.TryValidate(out string error),
                Is.True,
                error);
            return profile;
        }

        private static void AssertPropertyBlock(
            FirstArtTerrainRenderer3D presenter,
            FirstArtTerrainProfile3D profile,
            int width,
            int height)
        {
            var block = new MaterialPropertyBlock();
            presenter.SurfaceRenderer.GetPropertyBlock(block);
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_BaseColorArray")),
                Is.SameAs(profile.BaseColorArray));
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_NormalArray")),
                Is.SameAs(profile.NormalArray));
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_MaskArray")),
                Is.SameAs(profile.MaskArray));
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_HeightArray")),
                Is.SameAs(profile.HeightArray));
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_ControlA")),
                Is.SameAs(presenter.ControlMaps.ControlA));
            Assert.That(
                block.GetTexture(Shader.PropertyToID("_ControlB")),
                Is.SameAs(presenter.ControlMaps.ControlB));
            Assert.That(
                block.GetVector(Shader.PropertyToID("_WorldOriginXZ")),
                Is.EqualTo(new Vector4(
                    -width * .5f - .5f,
                    -height * .5f - .5f,
                    0f,
                    0f)));
            Assert.That(
                block.GetVector(Shader.PropertyToID("_WorldSizeXZ")),
                Is.EqualTo(new Vector4(width, height, 0f, 0f)));
            Assert.That(
                block.GetFloat(Shader.PropertyToID("_CellsPerTexture")),
                Is.EqualTo(profile.CellsPerTexture));
            Assert.That(
                block.GetFloat(Shader.PropertyToID("_HeightBlendStrength")),
                Is.EqualTo(profile.HeightBlendStrength));
            Assert.That(
                block.GetVector(Shader.PropertyToID("_WaterVelocityA")),
                Is.EqualTo(new Vector4(
                    profile.WaterNormalVelocityA.x,
                    profile.WaterNormalVelocityA.y,
                    0f,
                    0f)));
            Assert.That(
                block.GetVector(Shader.PropertyToID("_WaterVelocityB")),
                Is.EqualTo(new Vector4(
                    profile.WaterNormalVelocityB.x,
                    profile.WaterNormalVelocityB.y,
                    0f,
                    0f)));
        }

        private static void AssertFallbackState(
            GrayboxWorldView3D view,
            bool surfaceVisible)
        {
            int surfaceCount = 0;
            foreach (GrayboxVisualSlot slot in
                     view.GetComponentsInChildren<GrayboxVisualSlot>(true))
            {
                bool isSurface =
                    FirstArtTerrainCatalog3D.IsSurfaceStableId(slot.StableId);
                if (isSurface)
                {
                    surfaceCount++;
                    Assert.That(
                        slot.Renderer.enabled,
                        Is.EqualTo(surfaceVisible),
                        slot.StableId);
                }
                else
                {
                    Assert.That(
                        slot.Renderer.enabled,
                        Is.True,
                        slot.StableId);
                }
            }
            Assert.That(surfaceCount, Is.EqualTo(7));
            Assert.That(
                view.SurfaceFallbackVisible,
                Is.EqualTo(surfaceVisible));
        }

        private static int RuntimeSurfaceCount(
            FirstArtTerrainRenderer3D presenter)
        {
            return presenter.transform.Cast<Transform>()
                .Count(child => child.name == "RuntimeSurface");
        }

        private static int RuntimeSurfaceMeshCount()
        {
            return Resources.FindObjectsOfTypeAll<Mesh>()
                .Count(mesh =>
                    mesh != null &&
                    mesh.name == "first-art.terrain.surface");
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private static WorldMapModel CreateCatalogMap()
        {
            var cells = new WorldCell[12, 1];
            cells[0, 0] = new WorldCell(TerrainKind.Wasteland, null, 0);
            cells[1, 0] = new WorldCell(TerrainKind.Rocky, null, 0);
            cells[2, 0] = new WorldCell(TerrainKind.Crystal, null, 0);
            cells[3, 0] = new WorldCell(TerrainKind.Wetland, null, 0);
            cells[4, 0] = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Ruins);
            cells[5, 0] = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.DeepWater);
            cells[6, 0] = new WorldCell(
                TerrainKind.Wasteland,
                null,
                0,
                WorldTraversalKind.Cliff);
            cells[7, 0] = new WorldCell(
                TerrainKind.Wasteland,
                ResourceIds.Iron,
                100);
            cells[8, 0] = new WorldCell(
                TerrainKind.Wasteland,
                ResourceIds.EnergyCrystal,
                100);
            cells[9, 0] = new WorldCell(
                TerrainKind.Wasteland,
                ResourceIds.Stone,
                100);
            cells[10, 0] = new WorldCell(
                TerrainKind.Wasteland,
                ResourceIds.Biomass,
                100);
            cells[11, 0] = new WorldCell(
                TerrainKind.Wasteland,
                ResourceIds.Water,
                100);
            return new WorldMapModel(cells);
        }
    }
}
