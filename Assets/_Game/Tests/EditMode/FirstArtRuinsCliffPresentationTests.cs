using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class FirstArtRuinsCliffPresentationTests
    {
        private const string TerrainProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtTerrainProfile3D.asset";
        private const string GeometryProfilePath =
            "Assets/_Game/Art/FirstPass/Environment/Terrain/Runtime/Profiles/FirstArtRuinsCliffProfile3D.asset";
        private readonly List<UnityEngine.Object> cleanup =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            FirstArtRuinsCliffGeometry3D.ResetTestConfiguration();
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
            }
            cleanup.Clear();
        }

        [Test]
        public void IDEA0004_PresenterExposesGeometryConfigurationAndFamilyResults()
        {
            Type presenterType = typeof(FirstArtTerrainRenderer3D);
            MethodInfo configure = presenterType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(method =>
                {
                    if (method.Name != "Configure")
                        return false;
                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length == 2 &&
                        parameters[0].ParameterType ==
                            typeof(FirstArtTerrainProfile3D) &&
                        parameters[1].ParameterType ==
                            typeof(FirstArtRuinsCliffProfile3D);
                });

            Assert.That(configure, Is.Not.Null,
                "Task 6 requires the approved two-profile Configure overload.");
            Assert.That(
                presenterType.GetProperty("GeometryProfile"),
                Is.Not.Null);

            AssertFamilyProperty(presenterType, "RuinsStatus");
            AssertFamilyProperty(presenterType, "CliffStatus");
            Assert.That(
                presenterType.GetProperty("RuinsError")?.PropertyType,
                Is.EqualTo(typeof(string)));
            Assert.That(
                presenterType.GetProperty("CliffError")?.PropertyType,
                Is.EqualTo(typeof(string)));
        }

        [Test]
        public void IDEA0004_BothCategoriesPresentAsOneRuntimeRootAndClearRestoresFallbacks()
        {
            GrayboxWorldView3D view = CreateViewWithMap();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(
                LoadTerrainProfile(),
                LoadGeometryProfile());

            Assert.That(presenter.TryPresent(view), Is.True);

            Assert.That(presenter.LastPresentationError, Is.Null);
            Assert.That(presenter.RuinsStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Presented));
            Assert.That(presenter.CliffStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Presented));
            Assert.That(presenter.RuinsError, Is.Null);
            Assert.That(presenter.CliffError, Is.Null);
            Transform runtime = DirectChild(presenter.transform, "RuntimeGeometry");
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.Cast<Transform>().Select(child => child.name),
                Is.EquivalentTo(new[] { "RuinsGeometry", "CliffGeometry" }));
            Assert.That(presenter.GetComponentsInChildren<MeshFilter>(true).Length,
                Is.EqualTo(3), "One surface plus two category meshes are allowed.");
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.ruins"), Is.False);
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.cliff"), Is.False);
            AssertResourceStillVisible(view);

            Mesh[] categoryMeshes = runtime.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh).ToArray();
            presenter.ClearPresentation();
            presenter.ClearPresentation();

            Assert.That(DirectChild(presenter.transform, "RuntimeGeometry"), Is.Null);
            Assert.That(categoryMeshes.All(mesh => mesh == null), Is.True);
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.ruins"), Is.True);
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.cliff"), Is.True);
            Assert.That(presenter.RuinsStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Fallback));
            Assert.That(presenter.CliffStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Fallback));
        }

        [TestCase(FirstArtRuinsCliffFamily3D.Ruins)]
        [TestCase(FirstArtRuinsCliffFamily3D.Cliff)]
        public void IDEA0004_OneCategoryFailureKeepsSurfaceAndOtherCategory(
            FirstArtRuinsCliffFamily3D failedFamily)
        {
            GrayboxWorldView3D view = CreateViewWithMap();
            FirstArtRuinsCliffProfile3D profile =
                CreateProfileWithMissingNormals(failedFamily);
            FirstArtTerrainRenderer3D presenter = CreatePresenter(
                LoadTerrainProfile(),
                profile);
            LogAssert.Expect(
                LogType.Error,
                new Regex("First-art " + failedFamily +
                    " geometry presentation failed:"));

            Assert.That(presenter.TryPresent(view), Is.True);

            FirstArtRuinsCliffFamily3D successfulFamily = failedFamily ==
                FirstArtRuinsCliffFamily3D.Ruins
                    ? FirstArtRuinsCliffFamily3D.Cliff
                    : FirstArtRuinsCliffFamily3D.Ruins;
            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(presenter.LastPresentationError, Is.Null);
            AssertFamilyResult(presenter, failedFamily, false);
            AssertFamilyResult(presenter, successfulFamily, true);
            Assert.That(view.IsSurfaceFallbackVisible(SurfaceStableId(failedFamily)), Is.True);
            Assert.That(view.IsSurfaceFallbackVisible(SurfaceStableId(successfulFamily)), Is.False);
            Transform runtime = DirectChild(presenter.transform, "RuntimeGeometry");
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.childCount, Is.EqualTo(1));
            Assert.That(runtime.GetChild(0).name,
                Is.EqualTo(successfulFamily + "Geometry"));
            AssertResourceStillVisible(view);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void IDEA0004_BothCategoryFailuresReturnSurfaceSuccessAndLogOncePerFamily()
        {
            GrayboxWorldView3D view = CreateViewWithMap();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(
                LoadTerrainProfile(),
                CreateProfileWithMissingNormals(null));
            LogAssert.Expect(LogType.Error,
                new Regex("First-art Ruins geometry presentation failed:"));
            LogAssert.Expect(LogType.Error,
                new Regex("First-art Cliff geometry presentation failed:"));

            Assert.That(presenter.TryPresent(view), Is.True);

            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(presenter.LastPresentationError, Is.Null);
            AssertFamilyResult(presenter, FirstArtRuinsCliffFamily3D.Ruins, false);
            AssertFamilyResult(presenter, FirstArtRuinsCliffFamily3D.Cliff, false);
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.ruins"), Is.True);
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.cliff"), Is.True);
            Assert.That(DirectChild(presenter.transform, "RuntimeGeometry"), Is.Null);
            AssertResourceStillVisible(view);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void IDEA0004_SurfaceFailureWinsAndDoesNotRunOrLogCategories()
        {
            GrayboxWorldView3D view = CreateViewWithMap();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(
                null,
                LoadGeometryProfile());
            LogAssert.Expect(LogType.Error,
                new Regex("First-art terrain presentation failed:"));

            Assert.That(presenter.TryPresent(view), Is.False);

            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(presenter.LastPresentationError, Is.Not.Null);
            Assert.That(presenter.RuinsStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Fallback));
            Assert.That(presenter.CliffStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.Fallback));
            Assert.That(presenter.RuinsError, Is.Null);
            Assert.That(presenter.CliffError, Is.Null);
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(DirectChild(presenter.transform, "RuntimeGeometry"), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void IDEA0004_RebuildDisableReconfigureAndDestroyRestoreBeforeReplacement()
        {
            GrayboxWorldView3D view = CreateViewWithMap();
            FirstArtTerrainProfile3D terrain = LoadTerrainProfile();
            FirstArtRuinsCliffProfile3D geometry = LoadGeometryProfile();
            FirstArtTerrainRenderer3D presenter = CreatePresenter(terrain, geometry);
            Assert.That(presenter.TryPresent(view), Is.True);
            Mesh firstRuins = CategoryMesh(presenter, "RuinsGeometry");
            Mesh firstCliff = CategoryMesh(presenter, "CliffGeometry");

            view.Generate(CreateMap());

            Assert.That(firstRuins == null, Is.True);
            Assert.That(firstCliff == null, Is.True);
            Assert.That(presenter.IsPresented, Is.True);
            Assert.That(DirectChild(presenter.transform, "RuntimeGeometry").childCount,
                Is.EqualTo(2));
            Mesh rebuiltRuins = CategoryMesh(presenter, "RuinsGeometry");
            presenter.enabled = false;
            Assert.That(rebuiltRuins == null, Is.True);
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.ruins"), Is.True);
            Assert.That(view.IsSurfaceFallbackVisible("world.obstacle.cliff"), Is.True);
            Assert.That(DirectChild(presenter.transform, "RuntimeGeometry"), Is.Null);

            presenter.enabled = true;
            Assert.That(presenter.IsPresented, Is.True);
            AssertFamilyResult(presenter, FirstArtRuinsCliffFamily3D.Ruins, true);
            AssertFamilyResult(presenter, FirstArtRuinsCliffFamily3D.Cliff, true);
            Mesh enabledRuins = CategoryMesh(presenter, "RuinsGeometry");

            presenter.Configure(terrain);

            Assert.That(enabledRuins == null, Is.True);
            Assert.That(presenter.GeometryProfile, Is.Null);
            Assert.That(presenter.IsPresented, Is.False);
            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(presenter.RuinsStatus,
                Is.EqualTo(FirstArtRuinsCliffPresentationStatus3D.NotConfigured));
            Assert.That(presenter.TryPresent(view), Is.True);
            Assert.That(view.SurfaceFallbackVisible, Is.False,
                "The legacy one-profile overload must retain its all-surface behavior.");
            Assert.That(DirectChild(presenter.transform, "RuntimeGeometry"), Is.Null);

            GameObject owner = presenter.gameObject;
            UnityEngine.Object.DestroyImmediate(presenter);
            Assert.That(owner.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
            Assert.That(view.SurfaceFallbackVisible, Is.True);
        }

        private GrayboxWorldView3D CreateViewWithMap()
        {
            var root = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Shader shader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            Material material = Track(new Material(shader));
            GrayboxWorldView3D view = root.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            view.Generate(CreateMap());
            return view;
        }

        private FirstArtTerrainRenderer3D CreatePresenter(
            FirstArtTerrainProfile3D terrain,
            FirstArtRuinsCliffProfile3D geometry)
        {
            var owner = Track(new GameObject("FirstArtTerrainRenderer"));
            FirstArtTerrainRenderer3D presenter =
                owner.AddComponent<FirstArtTerrainRenderer3D>();
            presenter.runInEditMode = true;
            presenter.Configure(terrain, geometry);
            return presenter;
        }

        private FirstArtRuinsCliffProfile3D CreateProfileWithMissingNormals(
            FirstArtRuinsCliffFamily3D? failedFamily)
        {
            FirstArtRuinsCliffProfile3D approved = LoadGeometryProfile();
            var prefabBindings = new FirstArtRuinsCliffPrefabBinding3D[
                FirstArtRuinsCliffCatalog3D.EntryCount];
            for (int index = 0; index < prefabBindings.Length; index++)
            {
                FirstArtRuinsCliffCatalogEntry3D entry =
                    FirstArtRuinsCliffCatalog3D.Entries[index];
                bool shouldFail = !failedFamily.HasValue ||
                    entry.Family == failedFamily.Value;
                GameObject prefab;
                if (shouldFail)
                {
                    prefab = Track(new GameObject(
                        Path.GetFileNameWithoutExtension(entry.PrefabPath)));
                    Mesh mesh = Track(CreateMissingNormalMesh(entry.MaterialRoles.Count));
                    prefab.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = prefab.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = entry.MaterialRoles.Select(role =>
                    {
                        Assert.That(approved.TryResolveMaterial(role, out Material material),
                            Is.True);
                        return material;
                    }).ToArray();
                }
                else
                {
                    Assert.That(approved.TryResolvePrefab(entry.StableId, out prefab), Is.True);
                }
                prefabBindings[index] = new FirstArtRuinsCliffPrefabBinding3D(
                    entry.StableId,
                    prefab);
            }

            FirstArtRuinsCliffMaterialBinding3D[] materialBindings =
                approved.MaterialBindings.Select(binding =>
                    new FirstArtRuinsCliffMaterialBinding3D(
                        binding.Role,
                        binding.Material)).ToArray();
            FirstArtRuinsCliffProfile3D profile = Track(
                ScriptableObject.CreateInstance<FirstArtRuinsCliffProfile3D>());
            profile.Configure(approved.GeometryShader, prefabBindings, materialBindings);
            Assert.That(profile.TryValidate(out string error), Is.True, error);
            return profile;
        }

        private static Mesh CreateMissingNormalMesh(int subMeshCount)
        {
            var mesh = new Mesh { name = "Task6MissingNormalSource" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.tangents = Enumerable.Repeat(new Vector4(1f, 0f, 0f, 1f), 3)
                .ToArray();
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
            mesh.subMeshCount = subMeshCount;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0, false);
            for (int index = 1; index < subMeshCount; index++)
                mesh.SetTriangles(Array.Empty<int>(), index, false);
            return mesh;
        }

        private static WorldMapModel CreateMap()
        {
            var cells = new WorldCell[4, 1];
            cells[0, 0] = new WorldCell(
                TerrainKind.Wasteland, null, 0, WorldTraversalKind.Ruins);
            cells[1, 0] = new WorldCell(
                TerrainKind.Wasteland, null, 0, WorldTraversalKind.Cliff);
            cells[2, 0] = new WorldCell(TerrainKind.Wasteland, ResourceIds.Iron, 100);
            cells[3, 0] = new WorldCell(TerrainKind.Rocky, null, 0);
            return new WorldMapModel(cells);
        }

        private static FirstArtTerrainProfile3D LoadTerrainProfile()
        {
            FirstArtTerrainProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtTerrainProfile3D>(TerrainProfilePath);
            Assert.That(profile, Is.Not.Null);
            return profile;
        }

        private static FirstArtRuinsCliffProfile3D LoadGeometryProfile()
        {
            FirstArtRuinsCliffProfile3D profile =
                AssetDatabase.LoadAssetAtPath<FirstArtRuinsCliffProfile3D>(GeometryProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TryValidate(out string error), Is.True, error);
            return profile;
        }

        private static void AssertFamilyResult(
            FirstArtTerrainRenderer3D presenter,
            FirstArtRuinsCliffFamily3D family,
            bool presented)
        {
            FirstArtRuinsCliffPresentationStatus3D status = family ==
                FirstArtRuinsCliffFamily3D.Ruins
                    ? presenter.RuinsStatus
                    : presenter.CliffStatus;
            string error = family == FirstArtRuinsCliffFamily3D.Ruins
                ? presenter.RuinsError
                : presenter.CliffError;
            Assert.That(status, Is.EqualTo(presented
                ? FirstArtRuinsCliffPresentationStatus3D.Presented
                : FirstArtRuinsCliffPresentationStatus3D.Fallback));
            Assert.That(string.IsNullOrEmpty(error), Is.EqualTo(presented));
        }

        private static void AssertResourceStillVisible(GrayboxWorldView3D view)
        {
            GrayboxVisualSlot slot = view.GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Single(value => value.StableId == ResourceIds.Iron);
            Assert.That(slot.Renderer.enabled, Is.True);
        }

        private static string SurfaceStableId(FirstArtRuinsCliffFamily3D family)
        {
            return family == FirstArtRuinsCliffFamily3D.Ruins
                ? "world.obstacle.ruins"
                : "world.obstacle.cliff";
        }

        private static Mesh CategoryMesh(
            FirstArtTerrainRenderer3D presenter,
            string categoryName)
        {
            Transform root = DirectChild(presenter.transform, "RuntimeGeometry");
            Assert.That(root, Is.Not.Null);
            Transform category = DirectChild(root, categoryName);
            Assert.That(category, Is.Not.Null);
            return category.GetComponent<MeshFilter>().sharedMesh;
        }

        private static Transform DirectChild(Transform parent, string name)
        {
            if (parent == null)
                return null;
            return parent.Cast<Transform>().SingleOrDefault(child => child.name == name);
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

        private static void AssertFamilyProperty(
            Type presenterType,
            string propertyName)
        {
            PropertyInfo property = presenterType.GetProperty(propertyName);
            Assert.That(property, Is.Not.Null);
            Assert.That(property.PropertyType.IsEnum, Is.True);
            CollectionAssert.AreEquivalent(
                new[] { "NotConfigured", "Presented", "Fallback" },
                Enum.GetNames(property.PropertyType));
        }
    }
}
