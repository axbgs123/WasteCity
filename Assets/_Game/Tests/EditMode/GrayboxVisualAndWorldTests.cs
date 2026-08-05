using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
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
        public void ClearGenerated_ReleasesObjectsAndMeshes()
        {
            GrayboxWorldView3D view = CreateView();
            view.Generate(CreateCatalogMap());

            view.ClearGenerated();

            Assert.That(view.WorldRendererCount, Is.Zero);
            Assert.That(view.PersistentGeneratedObjectCount, Is.Zero);
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

        private static Bounds MeshBounds(GrayboxVisualSlot slot)
        {
            return slot.GetComponent<MeshFilter>().sharedMesh.bounds;
        }
    }
}
