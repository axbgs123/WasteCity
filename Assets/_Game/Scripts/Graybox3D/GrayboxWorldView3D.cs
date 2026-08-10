using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxWorldView3D : MonoBehaviour
    {
        private sealed class Group
        {
            public string StableId { get; }
            public PrimitiveType Primitive { get; }
            public Color Color { get; }
            public Transform Parent { get; }
            public List<Matrix4x4> Instances { get; } =
                new List<Matrix4x4>();

            public Group(
                string stableId,
                PrimitiveType primitive,
                Color color,
                Transform parent)
            {
                StableId = stableId;
                Primitive = primitive;
                Color = color;
                Parent = parent;
            }
        }

        [SerializeField] private Transform terrainRoot;
        [SerializeField] private Transform resourceRoot;
        [SerializeField] private Transform obstacleRoot;
        [SerializeField] private Material sharedMaterial;

        private readonly Dictionary<string, Group> groups =
            new Dictionary<string, Group>();
        private readonly List<GameObject> generatedObjects =
            new List<GameObject>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<GrayboxVisualSlot> surfaceSlots =
            new List<GrayboxVisualSlot>();
        private IGrayboxTerrainPresentation3D activeTerrainPresentation;

        public WorldMapModel Model { get; private set; }
        public PlanarCoordinateMapper3D Coordinates { get; private set; }
        public bool SurfaceFallbackVisible { get; private set; } = true;
        public int WorldRendererCount => generatedObjects.Count;
        public int PersistentGeneratedObjectCount => generatedObjects.Count;

        public void Configure(
            Transform terrainRoot,
            Transform resourceRoot,
            Transform obstacleRoot,
            Material sharedMaterial)
        {
            if (terrainRoot == null)
                throw new ArgumentNullException(nameof(terrainRoot));
            if (resourceRoot == null)
                throw new ArgumentNullException(nameof(resourceRoot));
            if (obstacleRoot == null)
                throw new ArgumentNullException(nameof(obstacleRoot));
            if (sharedMaterial == null)
                throw new ArgumentNullException(nameof(sharedMaterial));

            ClearGenerated();
            this.terrainRoot = terrainRoot;
            this.resourceRoot = resourceRoot;
            this.obstacleRoot = obstacleRoot;
            this.sharedMaterial = sharedMaterial;
        }

        public void Generate(WorldMapModel model)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (terrainRoot == null ||
                resourceRoot == null ||
                obstacleRoot == null ||
                sharedMaterial == null)
                throw new InvalidOperationException(
                    "Configure the graybox world view before generation.");

            IGrayboxTerrainPresentation3D presentationToRestore =
                ReleaseActiveTerrainPresentation();
            ClearGeneratedObjects();
            Model = model;
            Coordinates =
                new PlanarCoordinateMapper3D(model.Width, model.Height);

            for (int x = 0; x < model.Width; x++)
            for (int y = 0; y < model.Height; y++)
            {
                WorldCell cell = model.Get(x, y);
                Coordinates.TryCellToWorld(x, y, 0f, out Vector3 origin);
                AddTerrain(cell.Terrain, origin);
                AddTraversal(cell.Traversal, origin);
                if (cell.HasResource)
                    AddResource(cell.ResourceId, origin);
            }

            foreach (Group group in groups.Values)
                BuildGroup(group);
            groups.Clear();

            if (IsPresentationAlive(presentationToRestore))
                RestoreTerrainPresentation(presentationToRestore);
        }

        public void AttachTerrainPresentation(
            IGrayboxTerrainPresentation3D presentation)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            if (IsPresentationAlive(activeTerrainPresentation) &&
                !ReferenceEquals(activeTerrainPresentation, presentation))
            {
                throw new InvalidOperationException(
                    "A terrain presentation is already attached.");
            }

            activeTerrainPresentation = presentation;
        }

        public void DetachTerrainPresentation(
            IGrayboxTerrainPresentation3D presentation)
        {
            if (ReferenceEquals(activeTerrainPresentation, presentation))
                activeTerrainPresentation = null;
        }

        public void SetSurfaceFallbackVisible(bool visible)
        {
            SurfaceFallbackVisible = visible;
            for (int index = 0; index < surfaceSlots.Count; index++)
            {
                GrayboxVisualSlot slot = surfaceSlots[index];
                if (slot != null && slot.Renderer != null)
                    slot.Renderer.enabled = visible;
            }
        }

        public void ClearGenerated()
        {
            ReleaseActiveTerrainPresentation();
            ClearGeneratedObjects();
        }

        private void ClearGeneratedObjects()
        {
            for (int index = generatedObjects.Count - 1; index >= 0; index--)
                DestroyOwned(generatedObjects[index]);
            for (int index = generatedMeshes.Count - 1; index >= 0; index--)
                DestroyOwned(generatedMeshes[index]);

            generatedObjects.Clear();
            generatedMeshes.Clear();
            surfaceSlots.Clear();
            groups.Clear();
            Model = null;
            Coordinates = null;
        }

        private IGrayboxTerrainPresentation3D
            ReleaseActiveTerrainPresentation()
        {
            IGrayboxTerrainPresentation3D presentation =
                activeTerrainPresentation;
            activeTerrainPresentation = null;
            if (presentation == null)
                return null;

            SetSurfaceFallbackVisible(true);
            if (IsPresentationAlive(presentation))
                presentation.ClearPresentation();
            return presentation;
        }

        private static bool IsPresentationAlive(
            IGrayboxTerrainPresentation3D presentation)
        {
            if (presentation == null)
                return false;
            var unityObject = presentation as UnityEngine.Object;
            return ReferenceEquals(unityObject, null) || unityObject != null;
        }

        private void RestoreTerrainPresentation(
            IGrayboxTerrainPresentation3D presentation)
        {
            try
            {
                if (presentation.TryPresent(this))
                    return;
            }
            catch (Exception presentationException)
            {
                Exception cleanupException =
                    CleanupFailedTerrainPresentation(presentation);
                if (cleanupException != null)
                {
                    throw new AggregateException(
                        "Terrain presentation and cleanup failed.",
                        presentationException,
                        cleanupException);
                }
                throw;
            }

            Exception failedCleanup =
                CleanupFailedTerrainPresentation(presentation);
            if (failedCleanup != null)
            {
                throw new InvalidOperationException(
                    "Terrain presentation returned false and cleanup " +
                    "failed.",
                    failedCleanup);
            }
        }

        private Exception CleanupFailedTerrainPresentation(
            IGrayboxTerrainPresentation3D presentation)
        {
            Exception cleanupException = null;
            try
            {
                if (IsPresentationAlive(presentation))
                    presentation.ClearPresentation();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }
            finally
            {
                DetachTerrainPresentation(presentation);
                SetSurfaceFallbackVisible(true);
            }
            return cleanupException;
        }

        public bool TryWorldToCell(
            Vector3 world,
            out int cellX,
            out int cellY)
        {
            if (Coordinates == null)
            {
                cellX = -1;
                cellY = -1;
                return false;
            }

            return Coordinates.TryWorldToCell(world, out cellX, out cellY);
        }

        private void AddTerrain(TerrainKind terrain, Vector3 origin)
        {
            string stableId;
            Color color;
            switch (terrain)
            {
                case TerrainKind.Rocky:
                    stableId = "world.terrain.rocky";
                    color = new Color(.31f, .24f, .16f);
                    break;
                case TerrainKind.Crystal:
                    stableId = "world.terrain.crystal";
                    color = new Color(.16f, .3f, .34f);
                    break;
                case TerrainKind.Wetland:
                    stableId = "world.terrain.wetland";
                    color = new Color(.13f, .28f, .22f);
                    break;
                default:
                    stableId = "world.terrain.wasteland";
                    color = new Color(.2f, .22f, .18f);
                    break;
            }

            AddInstance(
                stableId,
                PrimitiveType.Plane,
                color,
                Matrix4x4.TRS(
                    origin,
                    Quaternion.identity,
                    new Vector3(.1f, 1f, .1f)),
                terrainRoot);
        }

        private void AddTraversal(
            WorldTraversalKind traversal,
            Vector3 origin)
        {
            switch (traversal)
            {
                case WorldTraversalKind.Ruins:
                    AddInstance(
                        "world.obstacle.ruins",
                        PrimitiveType.Cube,
                        new Color(.2f, .2f, .2f),
                        Matrix4x4.TRS(
                            origin + Vector3.up * .5f,
                            Quaternion.identity,
                            new Vector3(.8f, 1f, .8f)),
                        obstacleRoot);
                    break;
                case WorldTraversalKind.DeepWater:
                    AddInstance(
                        "world.obstacle.deep-water",
                        PrimitiveType.Cube,
                        new Color(.03f, .12f, .28f),
                        Matrix4x4.TRS(
                            origin + Vector3.down * .15f,
                            Quaternion.identity,
                            new Vector3(.9f, .1f, .9f)),
                        obstacleRoot);
                    break;
                case WorldTraversalKind.Cliff:
                    AddInstance(
                        "world.obstacle.cliff",
                        PrimitiveType.Cube,
                        new Color(.12f, .08f, .05f),
                        Matrix4x4.TRS(
                            origin + Vector3.up * .75f,
                            Quaternion.identity,
                            new Vector3(.9f, 1.5f, .9f)),
                        obstacleRoot);
                    break;
            }
        }

        private void AddResource(string resourceId, Vector3 origin)
        {
            if (resourceId == ResourceIds.Iron)
            {
                AddResourceCube(
                    ResourceIds.Iron,
                    new Color(.75f, .45f, .2f),
                    origin,
                    new Vector3(.35f, .35f, .35f),
                    .2f);
            }
            else if (resourceId == ResourceIds.EnergyCrystal)
            {
                AddResourceCapsule(
                    ResourceIds.EnergyCrystal,
                    Color.cyan,
                    origin,
                    new Vector3(.25f, .4f, .25f),
                    .4f);
            }
            else if (resourceId == ResourceIds.Stone)
            {
                AddResourceCube(
                    ResourceIds.Stone,
                    Color.gray,
                    origin,
                    new Vector3(.32f, .32f, .32f),
                    .18f);
            }
            else if (resourceId == ResourceIds.Biomass)
            {
                AddResourceCapsule(
                    ResourceIds.Biomass,
                    Color.green,
                    origin,
                    new Vector3(.22f, .35f, .22f),
                    .35f);
            }
            else if (resourceId == ResourceIds.Water)
            {
                AddResourceCube(
                    ResourceIds.Water,
                    Color.blue,
                    origin,
                    new Vector3(.4f, .15f, .4f),
                    .08f);
            }
        }

        private void AddResourceCube(
            string stableId,
            Color color,
            Vector3 origin,
            Vector3 scale,
            float visualY)
        {
            AddInstance(
                stableId,
                PrimitiveType.Cube,
                color,
                Matrix4x4.TRS(
                    origin + Vector3.up * visualY,
                    Quaternion.identity,
                    scale),
                resourceRoot);
        }

        private void AddResourceCapsule(
            string stableId,
            Color color,
            Vector3 origin,
            Vector3 scale,
            float visualY)
        {
            AddInstance(
                stableId,
                PrimitiveType.Capsule,
                color,
                Matrix4x4.TRS(
                    origin + Vector3.up * visualY,
                    Quaternion.identity,
                    scale),
                resourceRoot);
        }

        private void AddInstance(
            string stableId,
            PrimitiveType primitive,
            Color color,
            Matrix4x4 matrix,
            Transform parent)
        {
            if (!groups.TryGetValue(stableId, out Group group))
            {
                group = new Group(stableId, primitive, color, parent);
                groups.Add(stableId, group);
            }
            group.Instances.Add(matrix);
        }

        private void BuildGroup(Group group)
        {
            var go = new GameObject(group.StableId);
            go.transform.SetParent(group.Parent, false);
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            filter.sharedMesh = GrayboxMeshBuilder.CombinePrimitive(
                group.Primitive,
                group.Instances,
                group.StableId);
            var slot = go.AddComponent<GrayboxVisualSlot>();
            slot.Configure(group.StableId, renderer, group.Color);
            slot.ApplyFallback(sharedMaterial);
            if (IsSurfaceSlot(group.StableId))
            {
                surfaceSlots.Add(slot);
                renderer.enabled = SurfaceFallbackVisible;
            }
            generatedMeshes.Add(filter.sharedMesh);
            generatedObjects.Add(go);
        }

        private static bool IsSurfaceSlot(string stableId)
        {
            switch (stableId)
            {
                case "world.terrain.wasteland":
                case "world.terrain.rocky":
                case "world.terrain.wetland":
                case "world.terrain.crystal":
                case "world.obstacle.ruins":
                case "world.obstacle.deep-water":
                case "world.obstacle.cliff":
                    return true;
                default:
                    return false;
            }
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            ClearGenerated();
        }
    }
}
