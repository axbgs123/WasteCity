using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WasteCity.ArtIntegration3D;
using WasteCity.Economy;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GrayboxVisualAndWorldTests
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
        public void VisualSlot_RejectsInvalidStableId()
        {
            GameObject go = Track(new GameObject("Visual"));
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            GrayboxVisualSlot slot = go.AddComponent<GrayboxVisualSlot>();

            Assert.Throws<ArgumentException>(
                () => slot.Configure("invalid", renderer, Color.white));
            Assert.Throws<ArgumentException>(
                () => slot.Configure(string.Empty, renderer, Color.white));
        }

        [Test]
        public void VisualSlot_AppliesPropertyBlockWithoutInstantiatingMaterial()
        {
            GameObject go = Track(new GameObject("Visual"));
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            Material shared = Track(CreateTestMaterial());
            var slot = go.AddComponent<GrayboxVisualSlot>();
            Color fallback = new Color(.2f, .22f, .18f);

            slot.Configure(
                "world.terrain.wasteland",
                renderer,
                fallback);
            slot.ApplyFallback(shared);

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(slot.StableId, Is.EqualTo("world.terrain.wasteland"));
            Assert.That(slot.Renderer, Is.SameAs(renderer));
            Assert.That(slot.FallbackColor, Is.EqualTo(fallback));
            Assert.That(renderer.sharedMaterial, Is.SameAs(shared));
            Assert.That(
                block.GetColor(Shader.PropertyToID("_BaseColor")),
                Is.EqualTo(fallback));
        }

        [Test]
        public void CombinePrimitive_LeavesNoTemporaryPrimitiveObject()
        {
            int before = UnityEngine.Object.FindObjectsOfType<Transform>().Length;

            Mesh mesh = GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                new[] { Matrix4x4.identity },
                "combined.cube");
            cleanup.Add(mesh);

            int after = UnityEngine.Object.FindObjectsOfType<Transform>().Length;
            Assert.That(after, Is.EqualTo(before));
            Assert.That(mesh.name, Is.EqualTo("combined.cube"));
            Assert.That(mesh.vertexCount, Is.GreaterThan(0));
        }

        [Test]
        public void Generate_UsesModelWithoutChangingAnyCell()
        {
            WorldMapModel model =
                new WorldMapModel(32, 24, new WorldSeed(8128));
            WorldCell[,] before = Capture(model);
            GrayboxWorldView3D view = CreateView();

            view.Generate(model);

            Assert.That(view.Model, Is.SameAs(model));
            Assert.That(view.Coordinates.Width, Is.EqualTo(32));
            Assert.That(view.Coordinates.Height, Is.EqualTo(24));
            Assert.That(view.ResourceNodeMarkerCount,
                Is.EqualTo(model.ResourceNodeCount));
            Assert.That(view.ResourceNodeMarkerRendererCount,
                Is.EqualTo(model.ResourceNodeCount * 3));
            Assert.That(view.TotalGeneratedRendererCount,
                Is.EqualTo(view.WorldRendererCount +
                    model.ResourceNodeCount * 3));
            Assert.That(view.TotalPersistentGeneratedObjectCount,
                Is.EqualTo(view.PersistentGeneratedObjectCount +
                    model.ResourceNodeCount * 4));
            Assert.That(
                view.GetComponentsInChildren<Renderer>(true).Length,
                Is.EqualTo(view.TotalGeneratedRendererCount));
            Assert.That(
                view.GetComponentsInChildren<Transform>(true).Length - 4,
                Is.EqualTo(view.TotalPersistentGeneratedObjectCount));
            for (int x = 0; x < model.Width; x++)
            for (int y = 0; y < model.Height; y++)
                AssertCellEqual(before[x, y], model.Get(x, y));
        }

        [Test]
        public void Generate_CombinesByStableIdWithinStructuralBudget()
        {
            GrayboxWorldView3D view = CreateView();

            view.Generate(
                new WorldMapModel(32, 24, new WorldSeed(8128)));

            Assert.That(view.WorldRendererCount, Is.LessThanOrEqualTo(16));
            Assert.That(
                view.PersistentGeneratedObjectCount,
                Is.LessThanOrEqualTo(16));
            Assert.That(
                view.PersistentGeneratedObjectCount,
                Is.Not.EqualTo(32 * 24));
            Assert.That(
                view.GetComponentsInChildren<GrayboxVisualSlot>(true).Length,
                Is.EqualTo(view.WorldRendererCount));
        }

        [Test]
        public void Generate_CreatesEveryFrozenStableIdWithExactColor()
        {
            GrayboxWorldView3D view = CreateView();

            view.Generate(CreateCatalogMap());

            Dictionary<string, GrayboxVisualSlot> slots = SlotsById(view);
            Assert.That(slots.Count, Is.EqualTo(12));
            AssertSlot(slots, "world.terrain.wasteland",
                new Color(.2f, .22f, .18f));
            AssertSlot(slots, "world.terrain.rocky",
                new Color(.31f, .24f, .16f));
            AssertSlot(slots, "world.terrain.crystal",
                new Color(.16f, .3f, .34f));
            AssertSlot(slots, "world.terrain.wetland",
                new Color(.13f, .28f, .22f));
            AssertSlot(slots, "world.obstacle.ruins",
                new Color(.2f, .2f, .2f));
            AssertSlot(slots, "world.obstacle.deep-water",
                new Color(.03f, .12f, .28f));
            AssertSlot(slots, "world.obstacle.cliff",
                new Color(.12f, .08f, .05f));
            AssertSlot(slots, ResourceIds.Iron,
                new Color(.75f, .45f, .2f));
            AssertSlot(slots, ResourceIds.EnergyCrystal, Color.cyan);
            AssertSlot(slots, ResourceIds.Stone, Color.gray);
            AssertSlot(slots, ResourceIds.Biomass, Color.green);
            AssertSlot(slots, ResourceIds.Water, Color.blue);
        }

        [Test]
        public void Generate_UsesPlaneCubeAndCapsuleShapesFromFrozenCatalog()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());
            Dictionary<string, GrayboxVisualSlot> slots = SlotsById(view);

            Bounds terrain = MeshBounds(slots["world.terrain.wasteland"]);
            Bounds ruins = MeshBounds(slots["world.obstacle.ruins"]);
            Bounds energy = MeshBounds(slots[ResourceIds.EnergyCrystal]);
            Bounds biomass = MeshBounds(slots[ResourceIds.Biomass]);
            Bounds iron = MeshBounds(slots[ResourceIds.Iron]);

            Assert.That(terrain.size.y, Is.LessThan(.001f));
            Assert.That(ruins.size.y, Is.GreaterThan(.5f));
            Assert.That(energy.size.y, Is.GreaterThan(energy.size.x * 1.5f));
            Assert.That(biomass.size.y, Is.GreaterThan(biomass.size.x * 1.5f));
            Assert.That(
                Mathf.Abs(iron.size.x - iron.size.y),
                Is.LessThan(.001f));
        }

        [Test]
        public void IDEA0012_DefaultResourceIconsCoverAllResourcesWithStableDistinctColorAndShape()
        {
            var colorKeys = new HashSet<Color32>();
            var shapeKeys = new HashSet<string>();

            foreach (ResourceDefinition definition in
                     ResourceDefinitionCatalog.All)
            {
                Sprite first = ResourceIconCatalog3D.Resolve(
                    definition.Id);
                Sprite second = ResourceIconCatalog3D.Resolve(
                    definition.Id);

                Assert.That(first, Is.Not.Null, definition.Id);
                Assert.That(second, Is.SameAs(first), definition.Id);
                Assert.That(first.name,
                    Is.EqualTo(definition.IconFallbackKey), definition.Id);
                Assert.That(first.texture, Is.Not.Null, definition.Id);
                Assert.That(first.texture.width, Is.EqualTo(64));
                Assert.That(first.texture.height, Is.EqualTo(64));
                colorKeys.Add(ResourceIconCatalog3D.FallbackColor(
                    definition.Id));
                shapeKeys.Add(ResourceIconCatalog3D.FallbackShapeKey(
                    definition.Id));
            }

            Assert.That(colorKeys, Has.Count.EqualTo(31));
            Assert.That(shapeKeys, Has.Count.EqualTo(31));
            Assert.That(ResourceIconCatalog3D.Resolve("unknown.resource"),
                Is.Null);
        }

        [Test]
        public void IDEA0012_SerializedIconOverrideWinsWithoutChangingResourceIdentity()
        {
            Texture2D texture = Track(new Texture2D(8, 8));
            Sprite replacement = Track(Sprite.Create(
                texture,
                new Rect(0f, 0f, 8f, 8f),
                Vector2.one * .5f,
                8f));
            ResourceIconCatalog3D catalog =
                Track(ScriptableObject.CreateInstance<ResourceIconCatalog3D>());
            catalog.ConfigureOverrides(new[]
            {
                new ResourceIconOverride3D(ResourceIds.Stone, replacement)
            });

            Assert.That(catalog.ResolveIcon(ResourceIds.Stone),
                Is.SameAs(replacement));
            Assert.That(catalog.ResolveIcon(ResourceIds.Iron),
                Is.SameAs(ResourceIconCatalog3D.Resolve(ResourceIds.Iron)));
        }

        [Test]
        public void IDEA0012_ResourceNodeMarkersReadLiveWorldTruthAndReuseStableObjects()
        {
            WorldMapModel model = CreateCatalogMap();
            GrayboxWorldView3D view = CreateView();

            view.Generate(model);

            Assert.That(view.ResourceNodeMarkerCount, Is.EqualTo(5));
            Assert.That(view.TryGetResourceNodeMarker(
                    9,
                    0,
                    out GrayboxResourceNodeMarker3D marker),
                Is.True);
            Assert.That(marker.StableId,
                Is.EqualTo(GrayboxResourceNodeIdentity3D.Create(9, 0)));
            Assert.That(marker.ResourceId, Is.EqualTo(ResourceIds.Stone));
            Assert.That(marker.DisplayedAmount, Is.EqualTo(100));
            Assert.That(marker.DisplayText, Does.Contain("石料"));
            Assert.That(marker.DisplayText, Does.Contain("100"));
            Assert.That(marker.Icon,
                Is.SameAs(ResourceIconCatalog3D.Resolve(ResourceIds.Stone)));
            Assert.That(marker.Frame, Is.Not.Null);
            Assert.That(marker.Frame.name,
                Is.EqualTo("world-marker-resource-node"));
            Assert.That(marker.transform.Find("Frame"), Is.Not.Null);

            Assert.That(model.Harvest(9, 0, 7, out string resourceId),
                Is.EqualTo(7));
            Assert.That(resourceId, Is.EqualTo(ResourceIds.Stone));
            Assert.That(view.RefreshResourceNodeMarkers(), Is.True);
            Assert.That(view.ResourceNodeMarkerCount, Is.EqualTo(5));
            Assert.That(view.TryGetResourceNodeMarker(
                    9,
                    0,
                    out GrayboxResourceNodeMarker3D refreshed),
                Is.True);
            Assert.That(refreshed, Is.SameAs(marker));
            Assert.That(refreshed.DisplayedAmount, Is.EqualTo(93));
            Assert.That(refreshed.DisplayText, Does.Contain("93"));
            Assert.That(view.RefreshResourceNodeMarkers(), Is.False);
        }

        [Test]
        public void IDEA0012_StableMarkerRefreshAndCameraDoNotAllocateOrRewrite()
        {
            WorldMapModel model = CreateCatalogMap();
            GrayboxWorldView3D view = CreateView();
            GameObject cameraObject = Track(new GameObject("MarkerCamera"));
            Transform camera = cameraObject.transform;
            camera.rotation = Quaternion.Euler(52f, 0f, 0f);
            view.Generate(model);
            Assert.That(view.RefreshResourceNodeMarkers(), Is.False);
            Assert.That(view.FaceResourceNodeMarkers(camera), Is.True);
            Assert.That(view.FaceResourceNodeMarkers(camera), Is.False);

            long before = GC.GetAllocatedBytesForCurrentThread();
            bool changed = false;
            for (var index = 0; index < 300; index++)
            {
                changed |= view.RefreshResourceNodeMarkers();
                changed |= view.FaceResourceNodeMarkers(camera);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(changed, Is.False);
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void IDEA0012_RepeatedGenerateAndClearDoNotLeakMarkerObjects()
        {
            WorldMapModel model = CreateCatalogMap();
            GrayboxWorldView3D view = CreateView();

            view.Generate(model);
            Assert.That(
                view.GetComponentsInChildren<GrayboxResourceNodeMarker3D>(true),
                Has.Length.EqualTo(model.ResourceNodeCount));

            view.Generate(model);
            Assert.That(view.ResourceNodeMarkerCount,
                Is.EqualTo(model.ResourceNodeCount));
            Assert.That(
                view.GetComponentsInChildren<GrayboxResourceNodeMarker3D>(true),
                Has.Length.EqualTo(model.ResourceNodeCount));

            view.ClearGenerated();
            Assert.That(view.ResourceNodeMarkerCount, Is.Zero);
            Assert.That(view.ResourceNodeMarkerRendererCount, Is.Zero);
            Assert.That(view.TotalGeneratedRendererCount, Is.Zero);
            Assert.That(view.TotalPersistentGeneratedObjectCount, Is.Zero);
            Assert.That(
                view.GetComponentsInChildren<GrayboxResourceNodeMarker3D>(true),
                Is.Empty);
        }

        [Test]
        public void ClearGenerated_ReleasesObjectsAndMeshes()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());

            view.ClearGenerated();

            Assert.That(view.WorldRendererCount, Is.Zero);
            Assert.That(view.PersistentGeneratedObjectCount, Is.Zero);
            Assert.That(view.ResourceNodeMarkerCount, Is.Zero);
            Assert.That(
                view.GetComponentsInChildren<GrayboxResourceNodeMarker3D>(true),
                Is.Empty);
            Assert.That(
                view.GetComponentsInChildren<GrayboxVisualSlot>(true),
                Is.Empty);
        }

        [Test]
        public void SetSurfaceFallbackVisible_OnlyChangesSevenSurfaceRenderers()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());

            view.SetSurfaceFallbackVisible(false);

            GrayboxVisualSlot[] slots =
                view.GetComponentsInChildren<GrayboxVisualSlot>(true);
            Assert.That(
                slots.Count(slot =>
                    FirstArtTerrainCatalog3D.IsSurfaceStableId(slot.StableId)),
                Is.EqualTo(7));
            foreach (GrayboxVisualSlot slot in slots)
            {
                bool isSurface =
                    FirstArtTerrainCatalog3D.IsSurfaceStableId(slot.StableId);
                Assert.That(
                    slot.Renderer.enabled,
                    Is.EqualTo(!isSurface),
                    slot.StableId);
            }

            view.SetSurfaceFallbackVisible(true);

            Assert.That(slots.All(slot => slot.Renderer.enabled), Is.True);
        }

        [Test]
        public void SetSurfaceFallbackVisible_BeforeGenerateIsSafeAndRetained()
        {
            GrayboxWorldView3D view = CreateView();

            Assert.DoesNotThrow(
                () => view.SetSurfaceFallbackVisible(false));
            Assert.That(view.SurfaceFallbackVisible, Is.False);

            view.Generate(CreateCatalogMap());

            foreach (GrayboxVisualSlot slot in
                     view.GetComponentsInChildren<GrayboxVisualSlot>(true))
            {
                bool isSurface =
                    FirstArtTerrainCatalog3D.IsSurfaceStableId(slot.StableId);
                Assert.That(
                    slot.Renderer.enabled,
                    Is.EqualTo(!isSurface),
                    slot.StableId);
            }
        }

        [Test]
        public void IDEA0004_TrySetSurfaceFallbackVisible_RestoresOnlyRuins()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());
            view.SetSurfaceFallbackVisible(false);

            bool changed = view.TrySetSurfaceFallbackVisible(
                "world.obstacle.ruins",
                true,
                out string error);

            Assert.That(changed, Is.True, error);
            Assert.That(error, Is.Empty);
            Assert.That(view.SurfaceFallbackVisible, Is.False);
            Assert.That(
                view.IsSurfaceFallbackVisible("world.obstacle.ruins"),
                Is.True);
            AssertOnlySurfaceVisible(view, "world.obstacle.ruins");
        }

        [Test]
        public void IDEA0004_TrySetSurfaceFallbackVisible_RestoresOnlyCliff()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());
            view.SetSurfaceFallbackVisible(false);

            bool changed = view.TrySetSurfaceFallbackVisible(
                "world.obstacle.cliff",
                true,
                out string error);

            Assert.That(changed, Is.True, error);
            Assert.That(error, Is.Empty);
            Assert.That(view.SurfaceFallbackVisible, Is.False);
            Assert.That(
                view.IsSurfaceFallbackVisible("world.obstacle.cliff"),
                Is.True);
            AssertOnlySurfaceVisible(view, "world.obstacle.cliff");
        }

        [Test]
        public void IDEA0004_TrySetSurfaceFallbackVisible_UnknownIdFailsAtomically()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());
            view.SetSurfaceFallbackVisible(false);
            Assert.That(
                view.TrySetSurfaceFallbackVisible(
                    "world.obstacle.ruins",
                    true,
                    out string setupError),
                Is.True,
                setupError);
            Dictionary<string, bool> before = SurfaceRendererStates(view);

            bool changed = view.TrySetSurfaceFallbackVisible(
                "world.resource.iron",
                true,
                out string error);

            Assert.That(changed, Is.False);
            Assert.That(error, Does.Contain("world.resource.iron"));
            Assert.That(
                SurfaceRendererStates(view),
                Is.EqualTo(before));
            Assert.That(view.SurfaceFallbackVisible, Is.False);
            Assert.That(
                view.IsSurfaceFallbackVisible("world.resource.iron"),
                Is.False);
        }

        [Test]
        public void IDEA0004_SelectiveFallback_PersistsBeforeGenerateRebuildAndClear()
        {
            GrayboxWorldView3D view = CreateView();
            view.SetSurfaceFallbackVisible(false);
            Assert.That(
                view.TrySetSurfaceFallbackVisible(
                    "world.obstacle.ruins",
                    true,
                    out string error),
                Is.True,
                error);

            view.Generate(CreateCatalogMap());
            AssertOnlySurfaceVisible(view, "world.obstacle.ruins");

            view.Generate(CreateCatalogMap());
            AssertOnlySurfaceVisible(view, "world.obstacle.ruins");

            view.ClearGenerated();
            Assert.That(view.SurfaceFallbackVisible, Is.False);
            Assert.That(
                view.IsSurfaceFallbackVisible("world.obstacle.ruins"),
                Is.True);
            Assert.That(
                view.IsSurfaceFallbackVisible("world.obstacle.cliff"),
                Is.False);

            view.Generate(CreateCatalogMap());
            AssertOnlySurfaceVisible(view, "world.obstacle.ruins");
        }

        [Test]
        public void IDEA0004_SetSurfaceFallbackVisible_ResetsEverySelectiveState()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());
            view.SetSurfaceFallbackVisible(false);
            Assert.That(
                view.TrySetSurfaceFallbackVisible(
                    "world.obstacle.ruins",
                    true,
                    out string error),
                Is.True,
                error);

            view.SetSurfaceFallbackVisible(true);

            Assert.That(view.SurfaceFallbackVisible, Is.True);
            Assert.That(
                SurfaceRendererStates(view).Values.All(visible => visible),
                Is.True);

            view.SetSurfaceFallbackVisible(false);

            Assert.That(view.SurfaceFallbackVisible, Is.False);
            Assert.That(
                SurfaceRendererStates(view).Values.All(visible => !visible),
                Is.True);
        }

        [Test]
        public void ClearGenerated_DiscardsSurfaceTrackingWithoutTouchingUnrelatedRenderer()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());
            var unrelatedObject = new GameObject("UnrelatedRenderer");
            unrelatedObject.transform.SetParent(view.transform, false);
            MeshRenderer unrelated =
                unrelatedObject.AddComponent<MeshRenderer>();
            unrelated.enabled = false;

            view.ClearGenerated();
            view.SetSurfaceFallbackVisible(true);
            view.SetSurfaceFallbackVisible(false);

            Assert.That(unrelated, Is.Not.Null);
            Assert.That(unrelated.enabled, Is.False);
            Assert.That(
                view.GetComponentsInChildren<GrayboxVisualSlot>(true),
                Is.Empty);
        }

        private GrayboxWorldView3D CreateView()
        {
            GameObject root = Track(new GameObject("GrayboxWorld"));
            Transform terrain = NewChild(root.transform, "TerrainRoot");
            Transform resources = NewChild(root.transform, "ResourceRoot");
            Transform obstacles = NewChild(root.transform, "ObstacleRoot");
            Material material = Track(CreateTestMaterial());
            GrayboxWorldView3D view =
                root.AddComponent<GrayboxWorldView3D>();
            view.Configure(terrain, resources, obstacles, material);
            return view;
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

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            cleanup.Add(value);
            return value;
        }

        private static WorldCell[,] Capture(WorldMapModel model)
        {
            var result = new WorldCell[model.Width, model.Height];
            for (int x = 0; x < model.Width; x++)
            for (int y = 0; y < model.Height; y++)
                result[x, y] = model.Get(x, y);
            return result;
        }

        private static void AssertCellEqual(
            WorldCell expected,
            WorldCell actual)
        {
            Assert.That(actual.Terrain, Is.EqualTo(expected.Terrain));
            Assert.That(actual.ResourceId, Is.EqualTo(expected.ResourceId));
            Assert.That(
                actual.ResourceAmount,
                Is.EqualTo(expected.ResourceAmount));
            Assert.That(actual.Traversal, Is.EqualTo(expected.Traversal));
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

        private static Dictionary<string, GrayboxVisualSlot> SlotsById(
            GrayboxWorldView3D view)
        {
            var result = new Dictionary<string, GrayboxVisualSlot>();
            foreach (GrayboxVisualSlot slot in
                     view.GetComponentsInChildren<GrayboxVisualSlot>(true))
                result.Add(slot.StableId, slot);
            return result;
        }

        private static void AssertSlot(
            IReadOnlyDictionary<string, GrayboxVisualSlot> slots,
            string stableId,
            Color expectedColor)
        {
            Assert.That(slots.ContainsKey(stableId), Is.True, stableId);
            GrayboxVisualSlot slot = slots[stableId];
            var block = new MaterialPropertyBlock();
            slot.Renderer.GetPropertyBlock(block);
            Assert.That(
                slot.FallbackColor,
                Is.EqualTo(expectedColor),
                stableId);
            Assert.That(
                block.GetColor(Shader.PropertyToID("_BaseColor")),
                Is.EqualTo(expectedColor),
                stableId);
        }

        private static void AssertOnlySurfaceVisible(
            GrayboxWorldView3D view,
            string visibleStableId)
        {
            foreach (GrayboxVisualSlot slot in
                     view.GetComponentsInChildren<GrayboxVisualSlot>(true))
            {
                bool isSurface =
                    FirstArtTerrainCatalog3D.IsSurfaceStableId(slot.StableId);
                bool expected = !isSurface || slot.StableId == visibleStableId;
                Assert.That(
                    slot.Renderer.enabled,
                    Is.EqualTo(expected),
                    slot.StableId);
            }
        }

        private static Dictionary<string, bool> SurfaceRendererStates(
            GrayboxWorldView3D view)
        {
            return view.GetComponentsInChildren<GrayboxVisualSlot>(true)
                .Where(slot =>
                    FirstArtTerrainCatalog3D.IsSurfaceStableId(slot.StableId))
                .ToDictionary(
                    slot => slot.StableId,
                    slot => slot.Renderer.enabled);
        }

        private static Bounds MeshBounds(GrayboxVisualSlot slot)
        {
            return slot.GetComponent<MeshFilter>().sharedMesh.bounds;
        }
    }
}
