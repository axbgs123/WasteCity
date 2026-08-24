using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxWorldView3D : MonoBehaviour
    {
        private const float ResourceNodeRefreshIntervalSeconds = .1f;

        private static readonly string[] SurfaceStableIds =
        {
            "world.terrain.wasteland",
            "world.terrain.rocky",
            "world.terrain.wetland",
            "world.terrain.crystal",
            "world.obstacle.ruins",
            "world.obstacle.deep-water",
            "world.obstacle.cliff"
        };

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
        [SerializeField] private ResourceIconCatalog3D resourceIconCatalog;
        [SerializeField]
        private FormalMapNavigationProfile3D mapNavigationProfile;
        [SerializeField]
        private FormalWorldPresentationScaleProfile3D
            worldPresentationScaleProfile;

        private readonly Dictionary<string, Group> groups =
            new Dictionary<string, Group>();
        private readonly List<GameObject> generatedObjects =
            new List<GameObject>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<GrayboxVisualSlot> surfaceSlots =
            new List<GrayboxVisualSlot>();
        private readonly List<GrayboxResourceNodeMarker3D>
            resourceNodeMarkers =
                new List<GrayboxResourceNodeMarker3D>();
        private readonly Dictionary<long, GrayboxResourceNodeMarker3D>
            resourceNodeMarkersByCell =
                new Dictionary<long, GrayboxResourceNodeMarker3D>();
        private readonly List<GrayboxResourceNodeMarker3D>
            guidedResourceNodeMarkers =
                new List<GrayboxResourceNodeMarker3D>();
        private readonly Dictionary<string, bool> surfaceFallbackVisibility =
            CreateSurfaceFallbackVisibility();
        private IGrayboxTerrainPresentation3D activeTerrainPresentation;
        private float nextResourceNodeRefreshAt;
        private Quaternion lastResourceNodeFacingRotation;
        private bool hasResourceNodeFacingRotation;
        private ResourceNodeMarkerLod3D resourceNodeMarkerLod;
        private bool hasResourceNodeMarkerLod;
        private FormalWorldMarkerMetrics3D resourceNodeMarkerMetrics;
        private float resourceNodeFrameWorldHeight;
        private float resourceNodeIconWorldHeight;
        private float resourceNodeTextWorldHeight;
        private float lastMarkerOrthographicSize;
        private int lastMarkerPixelWidth;
        private int lastMarkerPixelHeight;
        private bool hasResourceNodeMarkerPresentation;

        public WorldMapModel Model { get; private set; }
        public PlanarCoordinateMapper3D Coordinates { get; private set; }
        public bool SurfaceFallbackVisible
        {
            get
            {
                for (int index = 0;
                     index < SurfaceStableIds.Length;
                     index++)
                {
                    if (!surfaceFallbackVisibility[SurfaceStableIds[index]])
                        return false;
                }
                return true;
            }
        }
        public int WorldRendererCount => generatedObjects.Count;
        public int PersistentGeneratedObjectCount => generatedObjects.Count;
        public int ResourceNodeMarkerCount => resourceNodeMarkers.Count;
        public int ResourceNodeMarkerRendererCount =>
            resourceNodeMarkers.Count * 3;
        public int TotalGeneratedRendererCount =>
            WorldRendererCount + ResourceNodeMarkerRendererCount;
        public int TotalPersistentGeneratedObjectCount =>
            PersistentGeneratedObjectCount + resourceNodeMarkers.Count * 4;
        public bool HasActiveTerrainPresentation =>
            IsPresentationAlive(activeTerrainPresentation);

        private void Awake()
        {
            if (mapNavigationProfile == null)
                mapNavigationProfile = Resources.Load<
                    FormalMapNavigationProfile3D>(
                    FormalMapNavigationProfile3D.ResourcesPath);
            if (worldPresentationScaleProfile == null)
                worldPresentationScaleProfile = Resources.Load<
                    FormalWorldPresentationScaleProfile3D>(
                    FormalWorldPresentationScaleProfile3D.ResourcesPath);
        }

        public bool IsTerrainPresentationActive(
            IGrayboxTerrainPresentation3D presentation)
        {
            return ReferenceEquals(activeTerrainPresentation, presentation) &&
                IsPresentationAlive(presentation);
        }

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

        public void ConfigureResourceIcons(ResourceIconCatalog3D catalog)
        {
            resourceIconCatalog = catalog;
            for (var index = 0; index < resourceNodeMarkers.Count; index++)
            {
                GrayboxResourceNodeMarker3D marker =
                    resourceNodeMarkers[index];
                marker.SetIcon(ResolveResourceIcon(marker.ResourceId));
                marker.SetFrame(Production2DVisualCatalog3D.Resolve(
                    Production2DVisualClass.WorldMarker,
                    "core.world-marker.resource-node"));
            }
        }

        public void ConfigureMapNavigation(
            FormalMapNavigationProfile3D profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (!profile.TryValidate(out string error))
                throw new ArgumentException(error, nameof(profile));
            mapNavigationProfile = profile;
            hasResourceNodeMarkerLod = false;
            hasResourceNodeMarkerPresentation = false;
        }

        public void ConfigureWorldPresentation(
            FormalWorldPresentationScaleProfile3D profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            if (!profile.TryValidate(out string error))
                throw new ArgumentException(error, nameof(profile));
            worldPresentationScaleProfile = profile;
            hasResourceNodeMarkerLod = false;
            hasResourceNodeMarkerPresentation = false;
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
                {
                    AddResource(cell.ResourceId, origin);
                    AddResourceNodeMarker(x, y, cell, origin);
                }
            }

            foreach (Group group in groups.Values)
                BuildGroup(group);
            groups.Clear();

            if (IsPresentationAlive(presentationToRestore))
                RestoreTerrainPresentation(presentationToRestore);
            nextResourceNodeRefreshAt = Time.unscaledTime +
                ResourceNodeRefreshIntervalSeconds;
            hasResourceNodeFacingRotation = false;
            hasResourceNodeMarkerLod = false;
            hasResourceNodeMarkerPresentation = false;
        }

        public void AttachTerrainPresentation(
            IGrayboxTerrainPresentation3D presentation)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            if (HasActiveTerrainPresentation &&
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
            for (int index = 0;
                 index < SurfaceStableIds.Length;
                 index++)
            {
                surfaceFallbackVisibility[SurfaceStableIds[index]] = visible;
            }

            for (int index = 0; index < surfaceSlots.Count; index++)
            {
                GrayboxVisualSlot slot = surfaceSlots[index];
                if (slot != null && slot.Renderer != null)
                    slot.Renderer.enabled = visible;
            }
        }

        public bool TrySetSurfaceFallbackVisible(
            string stableId,
            bool visible,
            out string error)
        {
            if (!IsSurfaceSlot(stableId))
            {
                error = "Unknown surface fallback stable ID: '" +
                    (stableId ?? "<null>") + "'.";
                return false;
            }

            surfaceFallbackVisibility[stableId] = visible;
            for (int index = 0; index < surfaceSlots.Count; index++)
            {
                GrayboxVisualSlot slot = surfaceSlots[index];
                if (slot != null &&
                    slot.StableId == stableId &&
                    slot.Renderer != null)
                {
                    slot.Renderer.enabled = visible;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool IsSurfaceFallbackVisible(string stableId)
        {
            return !string.IsNullOrEmpty(stableId) &&
                surfaceFallbackVisibility.TryGetValue(
                    stableId,
                    out bool visible) &&
                visible;
        }

        public void ClearGenerated()
        {
            ReleaseActiveTerrainPresentation();
            ClearGeneratedObjects();
        }

        public bool TryGetResourceNodeMarker(
            int worldX,
            int worldY,
            out GrayboxResourceNodeMarker3D marker)
        {
            return resourceNodeMarkersByCell.TryGetValue(
                CellKey(worldX, worldY),
                out marker);
        }

        public bool RefreshResourceNodeMarkers()
        {
            if (Model == null || resourceNodeMarkers.Count == 0)
                return false;
            bool changed = false;
            for (var index = 0; index < resourceNodeMarkers.Count; index++)
            {
                GrayboxResourceNodeMarker3D marker =
                    resourceNodeMarkers[index];
                if (marker != null &&
                    marker.WorldX >= 0 && marker.WorldY >= 0 &&
                    marker.WorldX < Model.Width &&
                    marker.WorldY < Model.Height)
                {
                    changed |= marker.Refresh(
                        Model.Get(marker.WorldX, marker.WorldY));
                }
            }
            return changed;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextResourceNodeRefreshAt)
            {
                RefreshResourceNodeMarkers();
                nextResourceNodeRefreshAt = Time.unscaledTime +
                    ResourceNodeRefreshIntervalSeconds;
            }
            Camera camera = Camera.main;
            if (camera != null)
            {
                FaceResourceNodeMarkers(camera.transform);
                if (camera.orthographic)
                    RefreshResourceNodeMarkerPresentation(
                        camera.orthographicSize,
                        camera.pixelWidth,
                        camera.pixelHeight);
            }
        }

        public bool RefreshResourceNodeMarkerLod(float orthographicSize)
        {
            return RefreshResourceNodeMarkerPresentation(
                orthographicSize,
                EffectivePixelSize(Screen.width, 1920),
                EffectivePixelSize(Screen.height, 1080));
        }

        public bool RefreshResourceNodeMarkerPresentation(
            float orthographicSize,
            int pixelWidth,
            int pixelHeight)
        {
            EnsureWorldPresentationProfile();
            if (worldPresentationScaleProfile == null)
                return false;
            if (!IsFinitePositive(orthographicSize))
            {
                orthographicSize = mapNavigationProfile == null
                    ? FormalMapNavigationProfile3D.DefaultOrthographicSize
                    : mapNavigationProfile.DefaultSize;
            }
            pixelWidth = EffectivePixelSize(pixelWidth, 1920);
            pixelHeight = EffectivePixelSize(pixelHeight, 1080);
            if (hasResourceNodeMarkerPresentation &&
                Mathf.Approximately(
                    lastMarkerOrthographicSize,
                    orthographicSize) &&
                lastMarkerPixelWidth == pixelWidth &&
                lastMarkerPixelHeight == pixelHeight)
            {
                return false;
            }

            resourceNodeMarkerMetrics =
                worldPresentationScaleProfile.ResolveMarker(
                    FormalWorldPresentationScalePolicy3D
                        .WorldUnitScreenHeight(orthographicSize));
            ResolveMarkerWorldHeights(
                resourceNodeMarkerMetrics,
                orthographicSize,
                pixelWidth,
                pixelHeight,
                out resourceNodeFrameWorldHeight,
                out resourceNodeIconWorldHeight,
                out resourceNodeTextWorldHeight);
            resourceNodeMarkerLod = resourceNodeMarkerMetrics.Lod;
            hasResourceNodeMarkerLod = true;
            lastMarkerOrthographicSize = orthographicSize;
            lastMarkerPixelWidth = pixelWidth;
            lastMarkerPixelHeight = pixelHeight;
            hasResourceNodeMarkerPresentation = true;

            for (var index = 0; index < resourceNodeMarkers.Count; index++)
            {
                GrayboxResourceNodeMarker3D marker =
                    resourceNodeMarkers[index];
                if (marker != null)
                    ApplyResourceNodeMarkerPresentation(
                        marker,
                        marker.GuidanceOverride);
            }
            return true;
        }

        public bool SetResourceMarkerGuidanceOverride(
            int worldX,
            int worldY,
            bool enabled)
        {
            if (!TryGetResourceNodeMarker(
                    worldX,
                    worldY,
                    out GrayboxResourceNodeMarker3D marker) ||
                marker == null ||
                marker.GuidanceOverride == enabled)
                return false;
            if (hasResourceNodeMarkerPresentation)
                ApplyResourceNodeMarkerPresentation(marker, enabled);
            else
            {
                ResourceNodeMarkerLod3D lod = hasResourceNodeMarkerLod
                    ? resourceNodeMarkerLod
                    : ResourceNodeMarkerLod3D.Near;
                marker.ApplyDisplayLod(lod, enabled);
            }
            if (enabled)
                guidedResourceNodeMarkers.Add(marker);
            else
                guidedResourceNodeMarkers.Remove(marker);
            return true;
        }

        public bool ClearResourceMarkerGuidanceOverrides()
        {
            if (guidedResourceNodeMarkers.Count == 0)
                return false;
            for (var index = 0;
                 index < guidedResourceNodeMarkers.Count;
                 index++)
            {
                GrayboxResourceNodeMarker3D marker =
                    guidedResourceNodeMarkers[index];
                if (marker != null)
                {
                    if (hasResourceNodeMarkerPresentation)
                        ApplyResourceNodeMarkerPresentation(marker, false);
                    else
                    {
                        ResourceNodeMarkerLod3D lod =
                            hasResourceNodeMarkerLod
                                ? resourceNodeMarkerLod
                                : ResourceNodeMarkerLod3D.Near;
                        marker.ApplyDisplayLod(lod, false);
                    }
                }
            }
            guidedResourceNodeMarkers.Clear();
            return true;
        }

        public bool FaceResourceNodeMarkers(Transform cameraTransform)
        {
            if (cameraTransform == null) return false;
            Quaternion rotation = cameraTransform.rotation;
            if (hasResourceNodeFacingRotation &&
                rotation == lastResourceNodeFacingRotation)
                return false;
            for (var index = 0; index < resourceNodeMarkers.Count; index++)
            {
                GrayboxResourceNodeMarker3D marker =
                    resourceNodeMarkers[index];
                if (marker != null)
                    marker.FaceCamera(cameraTransform);
            }
            lastResourceNodeFacingRotation = rotation;
            hasResourceNodeFacingRotation = true;
            return true;
        }

        private void ClearGeneratedObjects()
        {
            for (int index = resourceNodeMarkers.Count - 1;
                 index >= 0;
                 index--)
            {
                if (resourceNodeMarkers[index] != null)
                    DestroyOwned(resourceNodeMarkers[index].gameObject);
            }
            for (int index = generatedObjects.Count - 1; index >= 0; index--)
                DestroyOwned(generatedObjects[index]);
            for (int index = generatedMeshes.Count - 1; index >= 0; index--)
                DestroyOwned(generatedMeshes[index]);

            generatedObjects.Clear();
            generatedMeshes.Clear();
            surfaceSlots.Clear();
            resourceNodeMarkers.Clear();
            resourceNodeMarkersByCell.Clear();
            guidedResourceNodeMarkers.Clear();
            nextResourceNodeRefreshAt = 0f;
            hasResourceNodeFacingRotation = false;
            hasResourceNodeMarkerLod = false;
            hasResourceNodeMarkerPresentation = false;
            groups.Clear();
            Model = null;
            Coordinates = null;
        }

        private IGrayboxTerrainPresentation3D
            ReleaseActiveTerrainPresentation()
        {
            IGrayboxTerrainPresentation3D presentation =
                activeTerrainPresentation;
            if (presentation == null)
                return null;

            if (!IsPresentationAlive(presentation))
            {
                activeTerrainPresentation = null;
                SetSurfaceFallbackVisible(true);
                return null;
            }

            bool cleanupSucceeded = false;
            try
            {
                presentation.ClearPresentation();
                cleanupSucceeded = true;
                return presentation;
            }
            finally
            {
                SetSurfaceFallbackVisible(true);
                if (cleanupSucceeded)
                {
                    if (ReferenceEquals(
                            activeTerrainPresentation,
                            presentation))
                        activeTerrainPresentation = null;
                }
                else
                {
                    activeTerrainPresentation = presentation;
                }
            }
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

        private void AddResourceNodeMarker(
            int worldX,
            int worldY,
            WorldCell cell,
            Vector3 origin)
        {
            if (!ResourceDefinitionCatalog.TryGet(cell.ResourceId, out _))
                return;
            string stableId = GrayboxResourceNodeIdentity3D.Create(
                worldX,
                worldY);
            var markerObject = new GameObject(stableId);
            markerObject.transform.SetParent(resourceRoot, false);
            GrayboxResourceNodeMarker3D marker =
                markerObject.AddComponent<GrayboxResourceNodeMarker3D>();
            marker.Configure(
                stableId,
                cell.ResourceId,
                worldX,
                worldY,
                origin,
                ResolveResourceIcon(cell.ResourceId));
            marker.Refresh(cell);
            resourceNodeMarkers.Add(marker);
            resourceNodeMarkersByCell.Add(
                CellKey(worldX, worldY),
                marker);
        }

        private Sprite ResolveResourceIcon(string resourceId)
        {
            return resourceIconCatalog == null
                ? ResourceIconCatalog3D.Resolve(resourceId)
                : resourceIconCatalog.ResolveIcon(resourceId);
        }

        private void EnsureWorldPresentationProfile()
        {
            if (worldPresentationScaleProfile == null)
            {
                worldPresentationScaleProfile = Resources.Load<
                    FormalWorldPresentationScaleProfile3D>(
                    FormalWorldPresentationScaleProfile3D.ResourcesPath);
            }
        }

        private void ApplyResourceNodeMarkerPresentation(
            GrayboxResourceNodeMarker3D marker,
            bool guidanceOverride)
        {
            FormalWorldMarkerMetrics3D metrics = resourceNodeMarkerMetrics;
            float frameHeight = resourceNodeFrameWorldHeight;
            float iconHeight = resourceNodeIconWorldHeight;
            float textHeight = resourceNodeTextWorldHeight;
            if (guidanceOverride &&
                worldPresentationScaleProfile != null)
            {
                metrics = worldPresentationScaleProfile.ResolveMarker(
                    worldPresentationScaleProfile.NearUnitScreenHeight);
                ResolveMarkerWorldHeights(
                    metrics,
                    lastMarkerOrthographicSize,
                    lastMarkerPixelWidth,
                    lastMarkerPixelHeight,
                    out frameHeight,
                    out iconHeight,
                    out textHeight);
            }
            marker.ApplyPresentation(
                metrics,
                frameHeight,
                iconHeight,
                textHeight,
                true,
                guidanceOverride);
        }

        private static void ResolveMarkerWorldHeights(
            FormalWorldMarkerMetrics3D metrics,
            float orthographicSize,
            int pixelWidth,
            int pixelHeight,
            out float frameHeight,
            out float iconHeight,
            out float textHeight)
        {
            frameHeight = metrics.ShowFrame
                ? ResolveMarkerWorldHeight(
                    metrics.FrameReferencePixels,
                    metrics,
                    orthographicSize,
                    pixelWidth,
                    pixelHeight)
                : 0f;
            iconHeight = ResolveMarkerWorldHeight(
                metrics.IconReferencePixels,
                metrics,
                orthographicSize,
                pixelWidth,
                pixelHeight);
            textHeight = metrics.ShowName || metrics.ShowAmount
                ? ResolveMarkerWorldHeight(
                    metrics.TextReferencePixels,
                    metrics,
                    orthographicSize,
                    pixelWidth,
                    pixelHeight)
                : 0f;
        }

        private static float ResolveMarkerWorldHeight(
            float referencePixels,
            FormalWorldMarkerMetrics3D metrics,
            float orthographicSize,
            int pixelWidth,
            int pixelHeight)
        {
            float physicalPixels =
                FormalWorldPresentationScalePolicy3D.ResolvePhysicalPixels(
                    referencePixels,
                    Mathf.Min(
                        referencePixels,
                        metrics.MinimumPhysicalPixels),
                    metrics.MaximumPhysicalPixels,
                    pixelWidth,
                    pixelHeight);
            return FormalWorldPresentationScalePolicy3D.WorldUnitsForPixels(
                physicalPixels,
                orthographicSize,
                pixelHeight);
        }

        private static int EffectivePixelSize(int value, int fallback)
        {
            return value > 0 ? value : fallback;
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f &&
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private static long CellKey(int worldX, int worldY)
        {
            return ((long)worldX << 32) | (uint)worldY;
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
                renderer.enabled =
                    IsSurfaceFallbackVisible(group.StableId);
            }
            generatedMeshes.Add(filter.sharedMesh);
            generatedObjects.Add(go);
        }

        private static bool IsSurfaceSlot(string stableId)
        {
            if (string.IsNullOrEmpty(stableId))
                return false;
            for (int index = 0;
                 index < SurfaceStableIds.Length;
                 index++)
            {
                if (SurfaceStableIds[index] == stableId)
                    return true;
            }
            return false;
        }

        private static Dictionary<string, bool>
            CreateSurfaceFallbackVisibility()
        {
            var result = new Dictionary<string, bool>(
                SurfaceStableIds.Length,
                StringComparer.Ordinal);
            for (int index = 0;
                 index < SurfaceStableIds.Length;
                 index++)
            {
                result.Add(SurfaceStableIds[index], true);
            }
            return result;
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
