using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingWorldView3D :
        MonoBehaviour,
        IGrayboxBuildingPresentation3D
    {
        private const int GroundWidth = GrayboxWorldLayout3D.WorldWidth;
        private const int GroundHeight = GrayboxWorldLayout3D.WorldHeight;
        private const float InnerCellSize =
            FormalWorldPresentationScaleProfile3D.InnerCellSize;
        private const float GroundCellSize =
            FormalWorldPresentationScaleProfile3D.GroundCellSize;

        private static readonly Color GridColor =
            new Color(.45f, .48f, .42f, .35f);
        private static readonly Color InnerGridColor =
            new Color(.3f, .68f, .9f, .45f);
        private static readonly Color GroundBoundaryColor =
            new Color(.92f, .76f, .2f, .78f);
        private static readonly Color ValidPreviewColor =
            new Color(.18f, .85f, .32f, .55f);
        private static readonly Color InvalidPreviewColor =
            new Color(.9f, .16f, .12f, .55f);
        private static readonly Color ConstructionColor =
            new Color(.82f, .68f, .26f, .65f);
        private static readonly Color CompletedColor =
            new Color(.62f, .68f, .72f, 1f);
        private static readonly Color RuinColor =
            new Color(.34f, .28f, .23f, 1f);
        private static readonly Color LegalGuidanceColor =
            new Color(.2f, .9f, .35f, .45f);
        private static readonly Color BlockedNodeColor =
            new Color(.85f, .62f, .12f, .55f);

        private sealed class Visual
        {
            public GameObject Root;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public BoxCollider Collider;
            public Mesh Mesh;
            public GrayboxVisualSlot SingleSlot;
            public GrayboxBuildingInstance3D Instance;
            public GrayboxBuildingInstanceState State;
            public SpriteRenderer IconRenderer;
            public string BuildingDefinitionId;
        }

        private readonly struct BuildingVisualDimensions
        {
            public BuildingVisualDimensions(
                float xSize,
                float zSize,
                float visualHeight,
                float cellSize,
                FormalBuildingVisualMetrics3D metrics)
            {
                XSize = xSize;
                ZSize = zSize;
                VisualHeight = visualHeight;
                CellSize = cellSize;
                Metrics = metrics;
            }

            public float XSize { get; }
            public float ZSize { get; }
            public float VisualHeight { get; }
            public float CellSize { get; }
            public FormalBuildingVisualMetrics3D Metrics { get; }
        }

        [SerializeField] private Transform instanceRoot;
        [SerializeField] private Transform infrastructureRoot;
        [SerializeField] private Material sharedMaterial;
        [SerializeField] private Material previewMaterial;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField]
        private FormalWorldPresentationScaleProfile3D presentationScaleProfile;
        [SerializeField, HideInInspector]
        private List<GameObject> ownedVisualRoots =
            new List<GameObject>();

        private readonly Dictionary<string, Visual> instances =
            new Dictionary<string, Visual>(StringComparer.Ordinal);
        private readonly Dictionary<string, Visual> nodeHighlights =
            new Dictionary<string, Visual>(StringComparer.Ordinal);
        private readonly Dictionary<string, Visual> anchorHighlights =
            new Dictionary<string, Visual>(StringComparer.Ordinal);
        private readonly List<Visual> infrastructure =
            new List<Visual>();
        private Visual preview;
        private Visual groundGrid;
        private Visual groundBoundary;
        private Visual innerGrid;
        private int groundRangeCityX;
        private int groundRangeCityY;
        private int groundRangeRadius;
        private int groundRangeWorldWidth;
        private int groundRangeWorldHeight;
        private bool hasGroundRangeKey;
        private bool groundRangeAvailable;
        private int previewWidth;
        private int previewHeight;
        private BuildingSite previewSite;
        private string previewGeometryStableId;
        private bool hasPreviewGeometry;
        private BuildingDefinition previewDefinition;
        private string previewStableId;
        private bool runtimeInitialized;
        private bool buildGridVisible;

        public int InfrastructureRendererCount =>
            infrastructure.Count +
            nodeHighlights.Count +
            anchorHighlights.Count +
            (preview == null ? 0 : 1);
        public int InstanceVisualCount => instances.Count;
        public int InstanceRendererCount
        {
            get
            {
                var count = 0;
                foreach (Visual visual in instances.Values)
                {
                    if (visual.Renderer != null) count++;
                    if (visual.IconRenderer != null) count++;
                }
                return count;
            }
        }
        public bool IsBuildGridVisible => buildGridVisible;
        public int ActiveMiningNodeHighlightCount =>
            ActiveVisualCount(nodeHighlights);
        public int ActiveMiningAnchorHighlightCount =>
            ActiveVisualCount(anchorHighlights);

        public void SetBuildGridVisible(bool visible)
        {
            buildGridVisible = visible;
            ApplyBuildGridVisibility();
        }

        public void SetGroundBuildRange(
            int cityX,
            int cityY,
            int radius,
            int worldWidth,
            int worldHeight)
        {
            EnsureConfigured();
            if (worldWidth <= 0 || worldHeight <= 0)
            {
                ClearGroundBuildRange();
                return;
            }

            if (hasGroundRangeKey &&
                groundRangeCityX == cityX &&
                groundRangeCityY == cityY &&
                groundRangeRadius == radius &&
                groundRangeWorldWidth == worldWidth &&
                groundRangeWorldHeight == worldHeight)
            {
                groundRangeAvailable = true;
                ApplyBuildGridVisibility();
                return;
            }

            CreateGroundRangeMeshes(
                cityX,
                cityY,
                radius,
                worldWidth,
                worldHeight,
                out Mesh gridMesh,
                out Mesh boundaryMesh);
            ReplaceMesh(groundGrid, gridMesh);
            ReplaceMesh(groundBoundary, boundaryMesh);
            groundRangeCityX = cityX;
            groundRangeCityY = cityY;
            groundRangeRadius = radius;
            groundRangeWorldWidth = worldWidth;
            groundRangeWorldHeight = worldHeight;
            hasGroundRangeKey = true;
            groundRangeAvailable = true;
            ApplyBuildGridVisibility();
        }

        public void ClearGroundBuildRange()
        {
            groundRangeAvailable = false;
            ApplyBuildGridVisibility();
        }

        public void Configure(
            Transform instanceRoot,
            Transform infrastructureRoot,
            Material sharedMaterial,
            GrayboxMobileCityController3D city)
        {
            Configure(
                instanceRoot,
                infrastructureRoot,
                sharedMaterial,
                sharedMaterial,
                city);
        }

        public void Configure(
            Transform instanceRoot,
            Transform infrastructureRoot,
            Material sharedMaterial,
            Material previewMaterial,
            GrayboxMobileCityController3D city)
        {
            if (instanceRoot == null)
                throw new ArgumentNullException(nameof(instanceRoot));
            if (infrastructureRoot == null)
                throw new ArgumentNullException(nameof(infrastructureRoot));
            if (sharedMaterial == null)
                throw new ArgumentNullException(nameof(sharedMaterial));
            if (previewMaterial == null)
                throw new ArgumentNullException(nameof(previewMaterial));
            if (city == null)
                throw new ArgumentNullException(nameof(city));

            ClearGenerated();
            this.instanceRoot = instanceRoot;
            this.infrastructureRoot = infrastructureRoot;
            this.sharedMaterial = sharedMaterial;
            this.previewMaterial = previewMaterial;
            this.city = city;
            TryRehydrate();
        }

        public bool TryCreate(GrayboxBuildingInstance3D instance)
        {
            EnsureConfigured();
            if (instance == null ||
                string.IsNullOrEmpty(instance.StableInstanceId) ||
                instances.ContainsKey(instance.StableInstanceId))
                return false;

            Visual visual = CreateVisual(
                instance.StableInstanceId,
                instanceRoot,
                CreateInstanceMesh(instance),
                ConstructionColor,
                true);
            visual.Instance = instance;
            visual.State = instance.State;
            ConfigureInstanceSlots(visual, instance);
            ConfigureInstanceIcon(visual, instance);
            ApplyInstanceTransform(visual);
            ConfigureCollider(visual);
            instances.Add(instance.StableInstanceId, visual);
            return true;
        }

        public void UpdateInstance(GrayboxBuildingInstance3D instance)
        {
            EnsureConfigured();
            if (instance == null ||
                !instances.TryGetValue(
                    instance.StableInstanceId,
                    out Visual visual))
                return;

            string definitionId = instance.Placement.Definition.Id.Value;
            if (visual.State != instance.State || !string.Equals(
                    visual.BuildingDefinitionId,
                    definitionId,
                    StringComparison.Ordinal))
            {
                ReplaceMesh(visual, CreateInstanceMesh(instance));
                visual.State = instance.State;
                ConfigureInstanceSlots(visual, instance);
                ConfigureInstanceIcon(visual, instance);
                ConfigureCollider(visual);
            }
            ApplyInstanceTransform(visual);
        }

        public void Remove(GrayboxBuildingInstance3D instance)
        {
            if (instance == null ||
                !instances.TryGetValue(
                    instance.StableInstanceId,
                    out Visual visual))
                return;
            instances.Remove(instance.StableInstanceId);
            DestroyVisual(visual);
        }

        public void ShowPreview(
            BuildingDefinition definition,
            in BuildingSurfaceHit hit,
            BuildingOrientation orientation,
            in BuildingPlacementEvaluation evaluation)
        {
            EnsureConfigured();
            if (definition == null || !hit.IsValid)
            {
                HidePreview();
                return;
            }

            int rotatedWidth = BuildingOrientationRules.Width(
                definition,
                orientation);
            int rotatedHeight = BuildingOrientationRules.Height(
                definition,
                orientation);
            int meshWidth = definition.Width;
            int meshHeight = definition.Height;
            string stableId = PreviewStableId(definition);
            Color color = evaluation.IsValid
                ? ValidPreviewColor
                : InvalidPreviewColor;
            if (preview == null)
            {
                preview = CreateVisual(
                    stableId,
                    infrastructureRoot,
                    CreatePreviewMesh(
                        definition,
                        meshWidth,
                        meshHeight,
                        hit.Site,
                        stableId),
                    color,
                    false,
                    previewMaterial);
                SetPreviewGeometry(
                    meshWidth,
                    meshHeight,
                    hit.Site,
                    stableId);
            }
            else
            {
                preview.Root.SetActive(true);
                if (!MatchesPreviewGeometry(
                        meshWidth,
                        meshHeight,
                        hit.Site,
                        stableId))
                {
                    ReplaceMesh(
                        preview,
                        CreatePreviewMesh(
                            definition,
                            meshWidth,
                            meshHeight,
                            hit.Site,
                            stableId));
                    SetPreviewGeometry(
                        meshWidth,
                        meshHeight,
                        hit.Site,
                        stableId);
                }
                ConfigureSingleSlot(
                    preview,
                    stableId,
                    color,
                    previewMaterial);
            }

            float cellSize = hit.Site == BuildingSite.InnerCity
                ? InnerCellSize
                : GroundCellSize;
            Vector3 right = hit.Site == BuildingSite.InnerCity
                ? city.transform.right
                : Vector3.right;
            Vector3 forward = hit.Site == BuildingSite.InnerCity
                ? city.transform.forward
                : Vector3.forward;
            preview.Root.transform.position =
                hit.WorldPoint +
                right * ((rotatedWidth - 1) * cellSize * .5f) +
                forward * ((rotatedHeight - 1) * cellSize * .5f) +
                Vector3.up * .06f;
            preview.Root.transform.rotation = SiteRotation(
                hit.Site,
                orientation);
        }

        public void HidePreview()
        {
            if (preview != null)
                preview.Root.SetActive(false);
        }

        public void ShowCompatibleResourceNode(
            string stableNodeVisualId,
            int worldX,
            int worldY,
            bool visible)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(stableNodeVisualId))
                return;

            if (!visible)
            {
                if (nodeHighlights.TryGetValue(
                        stableNodeVisualId,
                        out Visual pooled))
                    SetVisualActive(pooled, false);
                return;
            }

            ShowMiningResourceNode(
                stableNodeVisualId,
                worldX,
                worldY,
                GroundWidth,
                GroundHeight,
                true);
        }

        public void ShowMiningResourceNode(
            string stableNodeVisualId,
            int worldX,
            int worldY,
            int worldWidth,
            int worldHeight,
            bool hasLegalAnchor)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(stableNodeVisualId))
                return;

            if (!nodeHighlights.TryGetValue(
                    stableNodeVisualId,
                    out Visual visual))
            {
                string visualId =
                    "building.node-highlight." + stableNodeVisualId;
                visual = CreateVisual(
                    visualId,
                    infrastructureRoot,
                    CreateNodeHighlightMesh(visualId),
                    hasLegalAnchor
                        ? LegalGuidanceColor
                        : BlockedNodeColor,
                    false);
                nodeHighlights.Add(stableNodeVisualId, visual);
            }
            else
            {
                ConfigureSingleSlot(
                    visual,
                    visual.SingleSlot.StableId,
                    hasLegalAnchor
                        ? LegalGuidanceColor
                        : BlockedNodeColor);
                SetVisualActive(visual, true);
            }
            visual.Root.transform.position = new Vector3(
                worldX - worldWidth * .5f,
                .035f,
                worldY - worldHeight * .5f);
        }

        public void ShowMiningAnchor(
            string stableAnchorId,
            int anchorX,
            int anchorY,
            int width,
            int height,
            int worldWidth,
            int worldHeight)
        {
            EnsureConfigured();
            if (string.IsNullOrWhiteSpace(stableAnchorId))
                return;
            if (!anchorHighlights.TryGetValue(
                    stableAnchorId,
                    out Visual visual))
            {
                visual = CreateVisual(
                    stableAnchorId,
                    infrastructureRoot,
                    CreateAnchorHighlightMesh(
                        width,
                        height,
                        stableAnchorId),
                    LegalGuidanceColor,
                    false);
                anchorHighlights.Add(stableAnchorId, visual);
            }
            else
            {
                SetVisualActive(visual, true);
            }
            visual.Root.transform.position = new Vector3(
                anchorX - worldWidth * .5f + (width - 1) * .5f,
                .045f,
                anchorY - worldHeight * .5f + (height - 1) * .5f);
        }

        public void HideMiningGuidance()
        {
            SetAllVisualsActive(nodeHighlights, false);
            SetAllVisualsActive(anchorHighlights, false);
        }

        public bool TryPickInstance(
            Ray ray,
            out string stableInstanceId)
        {
            stableInstanceId = null;
            var nearest = float.PositiveInfinity;
            foreach (KeyValuePair<string, Visual> pair in instances)
            {
                if (pair.Value.Collider == null ||
                    !pair.Value.Collider.Raycast(
                        ray,
                        out RaycastHit hit,
                        float.PositiveInfinity) ||
                    hit.distance >= nearest)
                    continue;
                nearest = hit.distance;
                stableInstanceId = pair.Key;
            }
            return stableInstanceId != null;
        }

        private void LateUpdate()
        {
            AlignCityVisuals();
            Camera mainCamera = Camera.main;
            foreach (Visual visual in instances.Values)
            {
                if (visual.Instance.Placement.Site ==
                    BuildingSite.InnerCity)
                    ApplyInstanceTransform(visual);
                if (mainCamera != null && visual.IconRenderer != null &&
                    visual.IconRenderer.enabled)
                    FormalInnerCityPresentationPolicy3D
                        .OrientVerticalBillboard(
                            visual.IconRenderer.transform,
                            mainCamera.transform.position);
            }
        }

        private void Awake()
        {
            TryRehydrate();
        }

        private void OnEnable()
        {
            if (!runtimeInitialized)
                TryRehydrate();
        }

        private void CreateGridVisuals()
        {
            Mesh groundMesh;
            Mesh boundaryMesh;
            if (hasGroundRangeKey)
            {
                CreateGroundRangeMeshes(
                    groundRangeCityX,
                    groundRangeCityY,
                    groundRangeRadius,
                    groundRangeWorldWidth,
                    groundRangeWorldHeight,
                    out groundMesh,
                    out boundaryMesh);
            }
            else
            {
                groundMesh = CreateEmptyMesh(
                    "building.grid.ground.mesh");
                boundaryMesh = CreateEmptyMesh(
                    "building.range.ground-boundary.mesh");
            }
            groundGrid = CreateVisual(
                "building.grid.ground",
                infrastructureRoot,
                groundMesh,
                GridColor,
                false);
            infrastructure.Add(groundGrid);
            groundBoundary = CreateVisual(
                "building.range.ground-boundary",
                infrastructureRoot,
                boundaryMesh,
                GroundBoundaryColor,
                false);
            infrastructure.Add(groundBoundary);
            innerGrid = CreateVisual(
                "building.grid.inner-city",
                infrastructureRoot,
                CreateInnerGridMesh(),
                InnerGridColor,
                false);
            infrastructure.Add(innerGrid);
            ApplyBuildGridVisibility();
        }

        private void ApplyBuildGridVisibility()
        {
            bool showGround =
                buildGridVisible && groundRangeAvailable;
            SetVisualActive(groundGrid, showGround);
            SetVisualActive(groundBoundary, showGround);
            SetVisualActive(innerGrid, buildGridVisible);
        }

        private static void SetVisualActive(
            Visual visual,
            bool active)
        {
            if (visual == null || visual.Root == null ||
                visual.Root.activeSelf == active)
                return;
            visual.Root.SetActive(active);
        }

        private static int ActiveVisualCount(
            Dictionary<string, Visual> visuals)
        {
            var count = 0;
            foreach (Visual visual in visuals.Values)
                if (visual.Root != null && visual.Root.activeSelf)
                    count++;
            return count;
        }

        private static void SetAllVisualsActive(
            Dictionary<string, Visual> visuals,
            bool active)
        {
            foreach (Visual visual in visuals.Values)
                SetVisualActive(visual, active);
        }

        private Visual CreateVisual(
            string stableId,
            Transform parent,
            Mesh mesh,
            Color color,
            bool withCollider,
            Material material = null)
        {
            var root = new GameObject(stableId);
            root.transform.SetParent(parent, false);
            RegisterOwnedRoot(root);
            var filter = root.AddComponent<MeshFilter>();
            var renderer = root.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            var visual = new Visual
            {
                Root = root,
                Filter = filter,
                Renderer = renderer,
                Mesh = mesh
            };
            ConfigureSingleSlot(
                visual,
                stableId,
                color,
                material == null ? sharedMaterial : material);
            if (withCollider)
                visual.Collider = root.AddComponent<BoxCollider>();
            return visual;
        }

        private void ConfigureSingleSlot(
            Visual visual,
            string stableId,
            Color color)
        {
            ConfigureSingleSlot(
                visual,
                stableId,
                color,
                sharedMaterial);
        }

        private static void ConfigureSingleSlot(
            Visual visual,
            string stableId,
            Color color,
            Material material)
        {
            if (visual.SingleSlot == null)
                visual.SingleSlot =
                    visual.Root.AddComponent<GrayboxVisualSlot>();
            visual.Root.name = stableId;
            visual.SingleSlot.Configure(
                stableId,
                visual.Renderer,
                color);
            visual.SingleSlot.ApplyFallback(material);
        }

        private void ConfigureInstanceSlots(
            Visual visual,
            GrayboxBuildingInstance3D instance)
        {
            ClearSlots(visual);
            switch (instance.State)
            {
                case GrayboxBuildingInstanceState.Completed:
                    AddSlot(
                        visual,
                        "building.complete." + instance.StableInstanceId,
                        CompletedColor);
                    break;
                case GrayboxBuildingInstanceState.AbandonedRuin:
                case GrayboxBuildingInstanceState.DestroyedRuin:
                    AddSlot(
                        visual,
                        "building.ruin." + instance.StableInstanceId,
                        RuinColor);
                    break;
                default:
                    AddSlot(
                        visual,
                        "building.construction.foundation." +
                        instance.StableInstanceId,
                        ConstructionColor);
                    AddSlot(
                        visual,
                        "building.construction.frame." +
                        instance.StableInstanceId,
                        ConstructionColor);
                    break;
            }
        }

        private void ConfigureInstanceIcon(
            Visual visual,
            GrayboxBuildingInstance3D instance)
        {
            if (visual.IconRenderer == null)
            {
                var iconObject = new GameObject(
                    "BuildingIcon",
                    typeof(SpriteRenderer));
                iconObject.transform.SetParent(visual.Root.transform, false);
                visual.IconRenderer =
                    iconObject.GetComponent<SpriteRenderer>();
                visual.IconRenderer.sortingOrder = 40;
            }
            BuildingDefinition definition = instance.Placement.Definition;
            BuildingVisualDimensions dimensions = ResolveDimensions(
                definition,
                instance.Placement.Site);
            Sprite sprite = Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.Building,
                definition.Id.Value);
            visual.IconRenderer.sprite = sprite;
            visual.IconRenderer.enabled =
                instance.State == GrayboxBuildingInstanceState.Completed &&
                sprite != null;
            visual.BuildingDefinitionId = definition.Id.Value;
            Transform icon = visual.IconRenderer.transform;
            icon.localPosition = new Vector3(
                0f,
                dimensions.VisualHeight * .5f + dimensions.CellSize * .08f,
                0f);
            Rect visibleBounds = Production2DVisualCatalog3D
                .ResolveVisibleBounds(
                    Production2DVisualClass.Building,
                    definition.Id.Value);
            FormalWorldPresentationScaleProfile3D profile =
                ResolvePresentationScaleProfile();
            float widthRatio = profile == null
                ? .78f
                : profile.BuildingIconWidthRatio;
            float roofClearance = profile == null
                ? .08f
                : profile.BuildingIconRoofClearance;
            float targetSize = Mathf.Clamp(
                Mathf.Max(dimensions.XSize, dimensions.ZSize) * widthRatio,
                dimensions.CellSize * .7f,
                dimensions.CellSize * 2.4f);
            float scale = Production2DVisualScalePolicy3D
                .ResolveSpriteWorldScale(
                    sprite,
                    visibleBounds,
                    targetSize);
            float visibleBottom = Production2DVisualScalePolicy3D
                .ResolveVisibleBottomLocal(
                    sprite,
                    visibleBounds,
                    scale);
            icon.localScale = Vector3.one * scale;
            icon.localPosition = new Vector3(
                0f,
                dimensions.VisualHeight + roofClearance - visibleBottom,
                0f);
        }

        private void AddSlot(
            Visual visual,
            string stableId,
            Color color)
        {
            GrayboxVisualSlot slot =
                visual.Root.AddComponent<GrayboxVisualSlot>();
            slot.Configure(stableId, visual.Renderer, color);
            slot.ApplyFallback(sharedMaterial);
        }

        private static void ClearSlots(Visual visual)
        {
            GrayboxVisualSlot[] slots =
                visual.Root.GetComponents<GrayboxVisualSlot>();
            for (var index = 0; index < slots.Length; index++)
                DestroyOwned(slots[index]);
            visual.SingleSlot = null;
        }

        private Mesh CreateInstanceMesh(
            GrayboxBuildingInstance3D instance)
        {
            BuildingDefinition definition =
                instance.Placement.Definition;
            BuildingVisualDimensions dimensions = ResolveDimensions(
                definition,
                instance.Placement.Site);
            switch (instance.State)
            {
                case GrayboxBuildingInstanceState.Completed:
                    return CreateCompletedMesh(
                        dimensions,
                        instance.StableInstanceId);
                case GrayboxBuildingInstanceState.AbandonedRuin:
                case GrayboxBuildingInstanceState.DestroyedRuin:
                    return CreateRuinMesh(
                        dimensions,
                        instance.StableInstanceId);
                default:
                    return CreateConstructionMesh(
                        dimensions,
                        instance.StableInstanceId);
            }
        }

        private BuildingVisualDimensions ResolveDimensions(
            BuildingDefinition definition,
            BuildingSite site)
        {
            FormalWorldPresentationScaleProfile3D profile =
                ResolvePresentationScaleProfile();
            float cellSize = profile == null
                ? site == BuildingSite.InnerCity
                    ? InnerCellSize
                    : GroundCellSize
                : profile.CellSize(site);
            FormalBuildingVisualMetrics3D metrics = default;
            bool usesFormalMetrics = profile != null &&
                profile.TryResolveBuilding(definition, out metrics);
            if (!usesFormalMetrics)
            {
                metrics = new FormalBuildingVisualMetrics3D(
                    FormalBuildingVisualArchetype3D.Processor,
                    .92f,
                    .9f,
                    2f / 3f,
                    .65f,
                    .18f,
                    false);
            }

            float xSize = usesFormalMetrics
                ? definition.Width * cellSize * metrics.FootprintFillRatio
                : definition.Width * cellSize - cellSize * .08f;
            float zSize = usesFormalMetrics
                ? definition.Height * cellSize * metrics.FootprintFillRatio
                : definition.Height * cellSize - cellSize * .08f;
            float verticalEmphasis = usesFormalMetrics &&
                site == BuildingSite.InnerCity
                    ? profile.InnerVerticalEmphasis
                    : 1f;
            float visualHeight = usesFormalMetrics
                ? metrics.VisualHeightInCells * cellSize * verticalEmphasis
                : .9f;
            return new BuildingVisualDimensions(
                xSize,
                zSize,
                visualHeight,
                cellSize,
                metrics);
        }

        private FormalWorldPresentationScaleProfile3D
            ResolvePresentationScaleProfile()
        {
            if (presentationScaleProfile == null)
            {
                presentationScaleProfile = Resources.Load<
                    FormalWorldPresentationScaleProfile3D>(
                    FormalWorldPresentationScaleProfile3D.ResourcesPath);
            }
            return presentationScaleProfile;
        }

        private static Mesh CreateConstructionMesh(
            in BuildingVisualDimensions dimensions,
            string stableId)
        {
            float baseHeight = Mathf.Min(
                dimensions.VisualHeight,
                Mathf.Max(
                    dimensions.CellSize * .04f,
                    dimensions.VisualHeight * .12f));
            float frameHeight = Mathf.Max(
                0f,
                dimensions.VisualHeight - baseHeight);
            float post = Mathf.Min(
                Mathf.Min(dimensions.XSize, dimensions.ZSize) * .15f,
                Mathf.Max(
                    dimensions.CellSize * .045f,
                    dimensions.XSize * .055f));
            var matrices = new List<Matrix4x4>(6)
            {
                Matrix4x4.TRS(
                    new Vector3(0f, baseHeight * .5f, 0f),
                    Quaternion.identity,
                    new Vector3(
                        dimensions.XSize,
                        baseHeight,
                        dimensions.ZSize))
            };
            if (frameHeight > 0f)
            {
                float x = Math.Max(
                    0f,
                    dimensions.XSize * .5f - post * .5f);
                float z = Math.Max(
                    0f,
                    dimensions.ZSize * .5f - post * .5f);
                for (var signX = -1; signX <= 1; signX += 2)
                for (var signZ = -1; signZ <= 1; signZ += 2)
                {
                    matrices.Add(
                        Matrix4x4.TRS(
                            new Vector3(
                                signX * x,
                                baseHeight + frameHeight * .5f,
                                signZ * z),
                            Quaternion.identity,
                            new Vector3(post, frameHeight, post)));
                }
                matrices.Add(
                    Matrix4x4.TRS(
                        new Vector3(
                            0f,
                            dimensions.VisualHeight - post * .5f,
                            z),
                        Quaternion.identity,
                        new Vector3(
                            dimensions.XSize,
                            Mathf.Min(post, frameHeight),
                            post)));
            }
            return GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                matrices,
                "building.construction.mesh." + stableId);
        }

        private static Mesh CreateCompletedMesh(
            in BuildingVisualDimensions dimensions,
            string stableId)
        {
            FormalBuildingVisualMetrics3D metrics = dimensions.Metrics;
            float foundationHeight = dimensions.VisualHeight *
                metrics.FoundationHeightRatio;
            float crownHeight = dimensions.VisualHeight *
                metrics.CrownHeightRatio;
            float bodyHeight = Mathf.Max(
                0f,
                dimensions.VisualHeight - foundationHeight - crownHeight);
            var matrices = new List<Matrix4x4>(3)
            {
                Matrix4x4.TRS(
                    new Vector3(0f, foundationHeight * .5f, 0f),
                    Quaternion.identity,
                    new Vector3(
                        dimensions.XSize,
                        foundationHeight,
                        dimensions.ZSize))
            };
            if (bodyHeight > 0f)
            {
                matrices.Add(
                    Matrix4x4.TRS(
                        new Vector3(
                            0f,
                            foundationHeight + bodyHeight * .5f,
                            0f),
                        Quaternion.identity,
                        new Vector3(
                            dimensions.XSize * metrics.UpperBodyWidthRatio,
                            bodyHeight,
                            dimensions.ZSize * metrics.UpperBodyWidthRatio)));
            }
            if (crownHeight > 0f)
            {
                float crownWidth = Mathf.Max(
                    metrics.UpperBodyWidthRatio * .62f,
                    .18f);
                matrices.Add(
                    Matrix4x4.TRS(
                        new Vector3(
                            0f,
                            dimensions.VisualHeight - crownHeight * .5f,
                            dimensions.ZSize * .12f),
                        Quaternion.identity,
                        new Vector3(
                            dimensions.XSize * crownWidth,
                            crownHeight,
                            dimensions.ZSize * crownWidth)));
            }
            return GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                matrices,
                "building.complete.mesh." + stableId);
        }

        private static Mesh CreateRuinMesh(
            in BuildingVisualDimensions dimensions,
            string stableId)
        {
            float baseHeight = dimensions.VisualHeight * .32f;
            float debrisHeight = dimensions.VisualHeight * .28f;
            return GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                new[]
                {
                    Matrix4x4.TRS(
                        new Vector3(0f, baseHeight * .5f, 0f),
                        Quaternion.identity,
                        new Vector3(
                            dimensions.XSize,
                            baseHeight,
                            dimensions.ZSize)),
                    Matrix4x4.TRS(
                        new Vector3(
                            dimensions.XSize * .12f,
                            baseHeight + debrisHeight * .5f,
                            -dimensions.ZSize * .1f),
                        Quaternion.Euler(0f, -15f, 0f),
                        new Vector3(
                            dimensions.XSize * .32f,
                            debrisHeight,
                            dimensions.ZSize * .28f))
                },
                "building.ruin.mesh." + stableId);
        }

        private Mesh CreatePreviewMesh(
            BuildingDefinition definition,
            int width,
            int height,
            BuildingSite site,
            string stableId)
        {
            float cellSize = site == BuildingSite.InnerCity
                ? InnerCellSize
                : GroundCellSize;
            float xSize = width * cellSize - cellSize * .08f;
            float zSize = height * cellSize - cellSize * .08f;
            BuildingVisualDimensions dimensions = ResolveDimensions(
                definition,
                site);
            float baseHeight = Mathf.Min(
                dimensions.VisualHeight,
                Mathf.Max(cellSize * .04f, dimensions.VisualHeight * .1f));
            float shaftWidth = Mathf.Min(xSize * .18f, cellSize * .28f);
            float shaftLength = Mathf.Min(zSize * .46f, cellSize * .72f);
            float headWidth = Mathf.Min(xSize * .48f, cellSize * .62f);
            float headDepth = Mathf.Min(zSize * .18f, cellSize * .24f);
            float headZ = zSize * .5f - headDepth * .7f;
            float shaftZ = headZ - headDepth * .5f - shaftLength * .5f;
            return GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                new[]
                {
                    Matrix4x4.TRS(
                        new Vector3(0f, baseHeight * .5f, 0f),
                        Quaternion.identity,
                        new Vector3(xSize, baseHeight, zSize)),
                    Matrix4x4.TRS(
                        new Vector3(
                            0f,
                            dimensions.VisualHeight * .5f,
                            0f),
                        Quaternion.identity,
                        new Vector3(
                            dimensions.XSize,
                            dimensions.VisualHeight,
                            dimensions.ZSize)),
                    Matrix4x4.TRS(
                        new Vector3(
                            0f,
                            dimensions.VisualHeight + cellSize * .025f,
                            shaftZ),
                        Quaternion.identity,
                        new Vector3(
                            shaftWidth,
                            cellSize * .05f,
                            shaftLength)),
                    Matrix4x4.TRS(
                        new Vector3(
                            0f,
                            dimensions.VisualHeight + cellSize * .025f,
                            headZ),
                        Quaternion.identity,
                        new Vector3(
                            headWidth,
                            cellSize * .05f,
                            headDepth))
                },
                stableId + ".mesh");
        }

        private bool MatchesPreviewGeometry(
            int width,
            int height,
            BuildingSite site,
            string stableId)
        {
            return hasPreviewGeometry &&
                previewWidth == width &&
                previewHeight == height &&
                previewSite == site &&
                string.Equals(
                    previewGeometryStableId,
                    stableId,
                    StringComparison.Ordinal);
        }

        private void SetPreviewGeometry(
            int width,
            int height,
            BuildingSite site,
            string stableId)
        {
            previewWidth = width;
            previewHeight = height;
            previewSite = site;
            previewGeometryStableId = stableId;
            hasPreviewGeometry = true;
        }

        private static Mesh CreateNodeHighlightMesh(string stableId)
        {
            const float thickness = .06f;
            const float extent = .48f;
            return GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                new[]
                {
                    Matrix4x4.TRS(
                        new Vector3(0f, 0f, extent),
                        Quaternion.identity,
                        new Vector3(1f, .025f, thickness)),
                    Matrix4x4.TRS(
                        new Vector3(0f, 0f, -extent),
                        Quaternion.identity,
                        new Vector3(1f, .025f, thickness)),
                    Matrix4x4.TRS(
                        new Vector3(extent, 0f, 0f),
                        Quaternion.identity,
                        new Vector3(thickness, .025f, 1f)),
                    Matrix4x4.TRS(
                        new Vector3(-extent, 0f, 0f),
                        Quaternion.identity,
                        new Vector3(thickness, .025f, 1f))
                },
                stableId + ".mesh");
        }

        private static Mesh CreateAnchorHighlightMesh(
            int width,
            int height,
            string stableId)
        {
            const float thickness = .075f;
            float extentX = width * .5f - thickness * .5f;
            float extentZ = height * .5f - thickness * .5f;
            return GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                new[]
                {
                    Matrix4x4.TRS(
                        new Vector3(0f, 0f, extentZ),
                        Quaternion.identity,
                        new Vector3(width, .025f, thickness)),
                    Matrix4x4.TRS(
                        new Vector3(0f, 0f, -extentZ),
                        Quaternion.identity,
                        new Vector3(width, .025f, thickness)),
                    Matrix4x4.TRS(
                        new Vector3(extentX, 0f, 0f),
                        Quaternion.identity,
                        new Vector3(thickness, .025f, height)),
                    Matrix4x4.TRS(
                        new Vector3(-extentX, 0f, 0f),
                        Quaternion.identity,
                        new Vector3(thickness, .025f, height))
                },
                stableId + ".mesh");
        }

        private static void CreateGroundRangeMeshes(
            int cityX,
            int cityY,
            int radius,
            int worldWidth,
            int worldHeight,
            out Mesh gridMesh,
            out Mesh boundaryMesh)
        {
            var accepted = new bool[worldWidth, worldHeight];
            var acceptedCount = 0;
            for (var x = 0; x < worldWidth; x++)
            for (var y = 0; y < worldHeight; y++)
            {
                bool inRange = BuildingRangeRules.IsGroundCellInRange(
                    cityX,
                    cityY,
                    x,
                    y,
                    radius);
                accepted[x, y] = inRange;
                if (inRange)
                    acceptedCount++;
            }

            var gridLines = new List<Matrix4x4>(acceptedCount * 2 + 2);
            var boundaryLines = new List<Matrix4x4>();
            for (var x = 0; x < worldWidth; x++)
            for (var y = 0; y < worldHeight; y++)
            {
                if (!accepted[x, y])
                    continue;

                AddVerticalGroundLine(
                    gridLines,
                    x,
                    y,
                    worldWidth,
                    worldHeight,
                    .025f,
                    .012f);
                AddHorizontalGroundLine(
                    gridLines,
                    x,
                    y,
                    worldWidth,
                    worldHeight,
                    .025f,
                    .012f);
                if (x == worldWidth - 1 || !accepted[x + 1, y])
                    AddVerticalGroundLine(
                        gridLines,
                        x + 1,
                        y,
                        worldWidth,
                        worldHeight,
                        .025f,
                        .012f);
                if (y == worldHeight - 1 || !accepted[x, y + 1])
                    AddHorizontalGroundLine(
                        gridLines,
                        x,
                        y + 1,
                        worldWidth,
                        worldHeight,
                        .025f,
                        .012f);

                if (x == 0 || !accepted[x - 1, y])
                    AddVerticalGroundLine(
                        boundaryLines,
                        x,
                        y,
                        worldWidth,
                        worldHeight,
                        .075f,
                        .025f);
                if (x == worldWidth - 1 || !accepted[x + 1, y])
                    AddVerticalGroundLine(
                        boundaryLines,
                        x + 1,
                        y,
                        worldWidth,
                        worldHeight,
                        .075f,
                        .025f);
                if (y == 0 || !accepted[x, y - 1])
                    AddHorizontalGroundLine(
                        boundaryLines,
                        x,
                        y,
                        worldWidth,
                        worldHeight,
                        .075f,
                        .025f);
                if (y == worldHeight - 1 || !accepted[x, y + 1])
                    AddHorizontalGroundLine(
                        boundaryLines,
                        x,
                        y + 1,
                        worldWidth,
                        worldHeight,
                        .075f,
                        .025f);
            }

            gridMesh = GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                gridLines,
                "building.grid.ground.mesh");
            boundaryMesh = GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                boundaryLines,
                "building.range.ground-boundary.mesh");
        }

        private static void AddVerticalGroundLine(
            ICollection<Matrix4x4> lines,
            int edgeX,
            int cellY,
            int worldWidth,
            int worldHeight,
            float thickness,
            float visualY)
        {
            lines.Add(
                Matrix4x4.TRS(
                    new Vector3(
                        edgeX - worldWidth * .5f - .5f,
                        visualY,
                        cellY - worldHeight * .5f),
                    Quaternion.identity,
                    new Vector3(thickness, .015f, 1f)));
        }

        private static void AddHorizontalGroundLine(
            ICollection<Matrix4x4> lines,
            int cellX,
            int edgeY,
            int worldWidth,
            int worldHeight,
            float thickness,
            float visualY)
        {
            lines.Add(
                Matrix4x4.TRS(
                    new Vector3(
                        cellX - worldWidth * .5f,
                        visualY,
                        edgeY - worldHeight * .5f - .5f),
                    Quaternion.identity,
                    new Vector3(1f, .015f, thickness)));
        }

        private static Mesh CreateEmptyMesh(string meshName)
        {
            return new Mesh
            {
                name = meshName
            };
        }

        private Mesh CreateInnerGridMesh()
        {
            FormalWorldPresentationScaleProfile3D profile =
                ResolvePresentationScaleProfile();
            int width = profile?.InnerGridWidth ??
                FormalWorldPresentationScaleProfile3D.InnerGridWidthCells;
            int height = profile?.InnerGridHeight ??
                FormalWorldPresentationScaleProfile3D.InnerGridHeightCells;
            float cellSize = profile?.CellSize(BuildingSite.InnerCity) ??
                InnerCellSize;
            Vector2 anchor = profile?.InnerGridAnchor ??
                new Vector2(-width * cellSize * .5f,
                    -height * cellSize * .5f);
            const float line = .012f;
            var matrices = new List<Matrix4x4>(width + height + 2);
            for (var x = 0; x <= width; x++)
                matrices.Add(
                    Matrix4x4.TRS(
                        new Vector3(
                            anchor.x + x * cellSize,
                            0f,
                            0f),
                        Quaternion.identity,
                        new Vector3(
                            line,
                            .008f,
                            height * cellSize)));
            for (var y = 0; y <= height; y++)
                matrices.Add(
                    Matrix4x4.TRS(
                        new Vector3(
                            0f,
                            0f,
                            anchor.y + y * cellSize),
                        Quaternion.identity,
                        new Vector3(
                            width * cellSize,
                            .008f,
                            line)));
            return GrayboxMeshBuilder.CombinePrimitive(
                PrimitiveType.Cube,
                matrices,
                "building.grid.inner-city.mesh");
        }

        private void ApplyInstanceTransform(Visual visual)
        {
            PlacedBuilding placement = visual.Instance.Placement;
            int width = BuildingOrientationRules.Width(
                placement.Definition,
                placement.Orientation);
            int height = BuildingOrientationRules.Height(
                placement.Definition,
                placement.Orientation);
            if (placement.Site == BuildingSite.InnerCity)
            {
                Vector3 local =
                    FormalInnerCityPresentationPolicy3D
                        .FootprintCenterLocal(
                            placement.X,
                            placement.Y,
                            width,
                            height,
                            city.InnerContentLocalY);
                visual.Root.transform.position =
                    city.transform.TransformPoint(local);
                visual.Root.transform.rotation = SiteRotation(
                    placement.Site,
                    placement.Orientation);
                return;
            }

            visual.Root.transform.position = new Vector3(
                placement.X - GroundWidth * .5f + (width - 1) * .5f,
                .02f,
                placement.Y - GroundHeight * .5f + (height - 1) * .5f);
            visual.Root.transform.rotation = SiteRotation(
                placement.Site,
                placement.Orientation);
        }

        private Quaternion SiteRotation(
            BuildingSite site,
            BuildingOrientation orientation)
        {
            Quaternion direction = Quaternion.Euler(
                0f,
                (int)orientation * 90f,
                0f);
            return site == BuildingSite.InnerCity
                ? city.transform.rotation * direction
                : direction;
        }

        private static void ConfigureCollider(Visual visual)
        {
            if (visual.Collider == null || visual.Mesh == null)
                return;
            visual.Collider.center = visual.Mesh.bounds.center;
            visual.Collider.size = visual.Mesh.bounds.size;
        }

        private static void ReplaceMesh(Visual visual, Mesh next)
        {
            Mesh previous = visual.Mesh;
            visual.Mesh = next;
            visual.Filter.sharedMesh = next;
            DestroyOwned(previous);
        }

        private void AlignCityVisuals()
        {
            if (innerGrid == null || city == null)
                return;
            innerGrid.Root.transform.SetPositionAndRotation(
                city.transform.TransformPoint(new Vector3(
                    0f,
                    city.InnerContentLocalY,
                    0f)),
                city.transform.rotation);
        }

        private void EnsureConfigured()
        {
            if (instanceRoot == null ||
                infrastructureRoot == null ||
                sharedMaterial == null ||
                previewMaterial == null ||
                city == null)
                throw new InvalidOperationException(
                    "Configure the graybox building view before use.");
        }

        private bool TryRehydrate()
        {
            if (instanceRoot == null ||
                infrastructureRoot == null ||
                sharedMaterial == null ||
                city == null)
                return false;

            ClearGenerated();
            CreateGridVisuals();
            AlignCityVisuals();
            runtimeInitialized = true;
            return true;
        }

        private void ClearGenerated()
        {
            foreach (Visual visual in instances.Values)
                DestroyVisual(visual);
            foreach (Visual visual in nodeHighlights.Values)
                DestroyVisual(visual);
            foreach (Visual visual in anchorHighlights.Values)
                DestroyVisual(visual);
            for (var index = infrastructure.Count - 1;
                 index >= 0;
                 index--)
                DestroyVisual(infrastructure[index]);
            DestroyVisual(preview);
            ClearSerializedOwnedRoots();
            instances.Clear();
            nodeHighlights.Clear();
            anchorHighlights.Clear();
            infrastructure.Clear();
            preview = null;
            hasPreviewGeometry = false;
            previewGeometryStableId = null;
            previewDefinition = null;
            previewStableId = null;
            groundGrid = null;
            groundBoundary = null;
            innerGrid = null;
            runtimeInitialized = false;
        }

        private string PreviewStableId(BuildingDefinition definition)
        {
            if (ReferenceEquals(previewDefinition, definition) &&
                previewStableId != null)
                return previewStableId;
            previewDefinition = definition;
            previewStableId =
                "building.preview." + definition.Id.Value;
            return previewStableId;
        }

        private void RegisterOwnedRoot(GameObject root)
        {
            if (ownedVisualRoots == null)
                ownedVisualRoots = new List<GameObject>();
            ownedVisualRoots.Add(root);
        }

        private void ClearSerializedOwnedRoots()
        {
            if (ownedVisualRoots == null)
                return;
            for (var index = ownedVisualRoots.Count - 1;
                 index >= 0;
                 index--)
            {
                GameObject owned = ownedVisualRoots[index];
                if (owned == null)
                    continue;
                owned.SetActive(false);
                DestroyOwned(owned);
            }
            ownedVisualRoots.Clear();
        }

        private void DestroyVisual(Visual visual)
        {
            if (visual == null)
                return;
            if (ownedVisualRoots != null)
                ownedVisualRoots.Remove(visual.Root);
            if (visual.Root != null)
                visual.Root.SetActive(false);
            DestroyOwned(visual.Root);
            DestroyOwned(visual.Mesh);
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
