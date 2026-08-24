using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingPlacementController3D :
        MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxWorldView3D world;
        [SerializeField] private GrayboxBuildingSurfaceProjector3D projector;
        [SerializeField] private GrayboxBuildingWorldView3D presentation;
        [SerializeField] private GrayboxBuildingInteractionModel3D interaction;

        private Vector2 lastScreenPosition;
        private bool hasPointer;
        private string highlightedNodeId;
        private BuildingPlacementEvaluationWorkspace
            evaluationWorkspace;
        private Func<string, bool> researchCompleted;
        private Func<string, int> completedBuildings;
        private Func<string, int> availableResourceAmount;
        private readonly ResourceShortfall[] materialShortfallBuffer =
            new ResourceShortfall[1];
        private string[] resourceNodeVisualIds;
        private int resourceNodeVisualWidth;
        private int resourceNodeVisualHeight;
        private BuildingPlacementEvaluationWorkspace
            miningGuidanceWorkspace;
        private List<Vector2Int> miningCandidateWorkspace;
        private List<MiningGuidanceNode> miningNodes;
        private List<MiningGuidanceAnchor> miningAnchors;
        private Dictionary<long, int> miningAnchorIndices;
        private bool hasMiningGuidanceKey;
        private BuildingOrientation miningGuidanceOrientation;
        private int miningGuidanceCityX;
        private int miningGuidanceCityY;
        private int miningGuidanceRadius;
        private int miningGuidanceWorldWidth;
        private int miningGuidanceWorldHeight;
        private CityMode miningGuidanceCityMode;
        private uint miningGuidancePlacementRevision;
        private uint miningGuidanceCatalogRevision;
        private int miningGuidanceInventory;
        private int miningGuidancePopulation;
        private int miningGuidanceGridCount;

        private struct MiningGuidanceNode
        {
            public string StableId;
            public int X;
            public int Y;
            public bool HasLegalAnchor;
        }

        private readonly struct MiningGuidanceAnchor
        {
            public MiningGuidanceAnchor(int x, int y, bool isValid)
            {
                X = x;
                Y = y;
                IsValid = isValid;
            }

            public int X { get; }
            public int Y { get; }
            public bool IsValid { get; }
        }

        public BuildingPlacementEvaluation CurrentEvaluation
        {
            get;
            private set;
        }
        public BuildingSurfaceHit CurrentHit { get; private set; } =
            BuildingSurfaceHit.Invalid;
        public uint MiningGuidanceRefreshCount { get; private set; }
        public IReadOnlyList<ResourceShortfall> CurrentMaterialShortfalls
            { get; private set; } = Array.Empty<ResourceShortfall>();

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D world,
            GrayboxBuildingSurfaceProjector3D projector,
            GrayboxBuildingWorldView3D presentation,
            GrayboxBuildingInteractionModel3D interaction)
        {
            DiscardConfiguredLifetime();
            this.session = session;
            this.city = city;
            this.world = world;
            this.projector = projector;
            this.presentation = presentation;
            this.interaction = interaction;
            BeginConfiguredLifetime();
        }

        private void BeginConfiguredLifetime()
        {
            evaluationWorkspace =
                new BuildingPlacementEvaluationWorkspace();
            miningGuidanceWorkspace =
                new BuildingPlacementEvaluationWorkspace();
            miningCandidateWorkspace = new List<Vector2Int>(9);
            miningNodes = new List<MiningGuidanceNode>();
            miningAnchors = new List<MiningGuidanceAnchor>();
            miningAnchorIndices = new Dictionary<long, int>();
            researchCompleted =
                session == null
                    ? null
                    : session.IsResearchCompleted;
            completedBuildings =
                session == null
                    ? null
                    : session.CompletedBuildingCount;
            availableResourceAmount = session?.CityStorage == null
                ? null
                : session.GetCityResourceAmount;
            ConfigureResourceNodeIdentityWorkspace(world);
        }

        public static string CreateResourceNodeVisualId(
            int worldX,
            int worldY)
        {
            return GrayboxResourceNodeIdentity3D.Create(worldX, worldY);
        }

        public void UpdatePointer(Vector2 screenPosition)
        {
            lastScreenPosition = screenPosition;
            hasPointer = true;
            EvaluatePointer(screenPosition);
        }

        public bool ConfirmCurrentPlacement(
            out GrayboxBuildingInstance3D instance)
        {
            instance = null;
            if (!hasPointer || !EvaluatePointer(lastScreenPosition))
                return false;

            BuildingPlacementRequest request = CreateRequest(
                interaction.Selected,
                CurrentHit);
            bool confirmed = session.TryBeginConstruction(
                request,
                presentation,
                out instance,
                out BuildingPlacementEvaluation evaluation);
            CurrentEvaluation = evaluation;
            EvaluatePointer(lastScreenPosition);
            return confirmed;
        }

        public void HidePreview()
        {
            if (!string.IsNullOrEmpty(highlightedNodeId) &&
                presentation != null)
                presentation.ShowCompatibleResourceNode(
                    highlightedNodeId,
                    0,
                    0,
                    false);
            highlightedNodeId = null;
            presentation?.HideMiningGuidance();
            presentation?.HidePreview();
            CurrentHit = BuildingSurfaceHit.Invalid;
        }

        public void SetBuildGridVisible(bool visible)
        {
            if (presentation == null)
                return;
            if (visible)
            {
                if (session != null &&
                    city != null &&
                    world != null &&
                    world.Model != null &&
                    world.Coordinates != null &&
                    world.Coordinates.TryWorldToCell(
                        city.transform.position,
                        out int cityX,
                        out int cityY))
                {
                    presentation.SetGroundBuildRange(
                        cityX,
                        cityY,
                        session.GroundBuildRadius,
                        world.Model.Width,
                        world.Model.Height);
                }
                else
                {
                    presentation.ClearGroundBuildRange();
                }
            }
            presentation.SetBuildGridVisible(visible);
        }

        public void RefreshMiningGuidance()
        {
            if (!ReferenceEquals(
                    interaction?.Selected,
                    BuildingCatalog.MiningStation) ||
                session == null ||
                city == null ||
                world?.Model == null ||
                world.Coordinates == null ||
                presentation == null ||
                miningGuidanceWorkspace == null ||
                !world.Coordinates.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY))
            {
                hasMiningGuidanceKey = false;
                world?.ClearResourceMarkerGuidanceOverrides();
                presentation?.HideMiningGuidance();
                return;
            }

            BuildingOrientation orientation = interaction.Orientation;
            int radius = session.GroundBuildRadius;
            int worldWidth = world.Model.Width;
            int worldHeight = world.Model.Height;
            CityMode cityMode = city.Mode;
            uint placementRevision = session.PlacementRevision;
            uint catalogRevision = session.CatalogRevision;
            int inventory = session.CityStorage.GetNetworkAmount(
                BuildingCatalog.MiningStation.CostId);
            int population = session.Population;
            int gridCount = session.GroundGrid.Count;
            if (hasMiningGuidanceKey &&
                miningGuidanceOrientation == orientation &&
                miningGuidanceCityX == cityX &&
                miningGuidanceCityY == cityY &&
                miningGuidanceRadius == radius &&
                miningGuidanceWorldWidth == worldWidth &&
                miningGuidanceWorldHeight == worldHeight &&
                miningGuidanceCityMode == cityMode &&
                miningGuidancePlacementRevision == placementRevision &&
                miningGuidanceCatalogRevision == catalogRevision &&
                miningGuidanceInventory == inventory &&
                miningGuidancePopulation == population &&
                miningGuidanceGridCount == gridCount)
                return;

            BuildMiningGuidance(
                orientation,
                cityX,
                cityY,
                radius,
                worldWidth,
                worldHeight);
            miningGuidanceOrientation = orientation;
            miningGuidanceCityX = cityX;
            miningGuidanceCityY = cityY;
            miningGuidanceRadius = radius;
            miningGuidanceWorldWidth = worldWidth;
            miningGuidanceWorldHeight = worldHeight;
            miningGuidanceCityMode = cityMode;
            miningGuidancePlacementRevision = placementRevision;
            miningGuidanceCatalogRevision = catalogRevision;
            miningGuidanceInventory = inventory;
            miningGuidancePopulation = population;
            miningGuidanceGridCount = gridCount;
            hasMiningGuidanceKey = true;
            unchecked { MiningGuidanceRefreshCount++; }
        }

        private bool EvaluatePointer(Vector2 screenPosition)
        {
            if (evaluationWorkspace == null)
            {
                HidePreview();
                CurrentEvaluation = default;
                CurrentMaterialShortfalls = Array.Empty<ResourceShortfall>();
                return false;
            }

            BuildingDefinition definition = interaction?.Selected;
            if (definition == null ||
                session == null ||
                city == null ||
                world == null ||
                projector == null ||
                presentation == null)
            {
                HidePreview();
                CurrentEvaluation = BuildingPlacementRules.Evaluate(
                    MissingRequest(definition),
                    evaluationWorkspace);
                RefreshMaterialShortfalls(definition);
                return false;
            }

            if (!projector.TryProject(screenPosition, out BuildingSurfaceHit hit))
            {
                CurrentHit = BuildingSurfaceHit.Invalid;
                CurrentEvaluation = BuildingPlacementRules.Evaluate(
                    ProjectionFailedRequest(definition),
                    evaluationWorkspace);
                RefreshMaterialShortfalls(definition);
                ClearNodeHighlight();
                presentation.HidePreview();
                return false;
            }

            CurrentHit = hit;
            CurrentEvaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(definition, hit),
                evaluationWorkspace);
            RefreshMaterialShortfalls(definition);
            presentation.ShowPreview(
                definition,
                hit,
                interaction.Orientation,
                CurrentEvaluation);
            UpdateNodeHighlight(CurrentEvaluation);
            return CurrentEvaluation.IsValid;
        }

        private BuildingPlacementRequest CreateRequest(
            BuildingDefinition definition,
            in BuildingSurfaceHit hit)
        {
            return CreateRequest(
                definition,
                hit,
                interaction.Orientation);
        }

        private BuildingPlacementRequest CreateRequest(
            BuildingDefinition definition,
            in BuildingSurfaceHit hit,
            BuildingOrientation orientation)
        {
            BuildingGrid grid = hit.Site == BuildingSite.InnerCity
                ? session.InnerGrid
                : session.GroundGrid;
            int cityX = -1;
            int cityY = -1;
            bool cityMapped = world.Coordinates != null &&
                world.Coordinates.TryWorldToCell(
                    city.transform.position,
                    out cityX,
                    out cityY);
            bool terrainPassable = true;
            bool obstacleFree = true;
            bool touchesCity = false;
            ResourceNodeBinding compatibleNode =
                ResourceNodeBinding.None;

            if (definition != null &&
                hit.Site == BuildingSite.Ground &&
                world.Model != null &&
                world.Coordinates != null)
            {
                EnsureResourceNodeIdentityWorkspace();
                int width = BuildingOrientationRules.Width(
                    definition,
                    orientation);
                int height = BuildingOrientationRules.Height(
                    definition,
                    orientation);
                for (var dx = 0; dx < width; dx++)
                for (var dy = 0; dy < height; dy++)
                {
                    long candidateX = (long)hit.X + dx;
                    long candidateY = (long)hit.Y + dy;
                    if (candidateX < 0 ||
                        candidateY < 0 ||
                        candidateX >= world.Model.Width ||
                        candidateY >= world.Model.Height)
                        continue;

                    int x = (int)candidateX;
                    int y = (int)candidateY;
                    WorldCell cell = world.Model.Get(x, y);
                    if (cell.Traversal == WorldTraversalKind.DeepWater ||
                        cell.Traversal == WorldTraversalKind.Cliff)
                        terrainPassable = false;
                    if (cell.Traversal == WorldTraversalKind.Ruins)
                        obstacleFree = false;
                    if (cityMapped &&
                        BuildingRangeRules.DoesGroundCellOverlapCity(
                            cityX,
                            cityY,
                            x,
                            y))
                        touchesCity = true;
                    if (!compatibleNode.IsValid &&
                        cell.HasResource &&
                        BuildingResourceNodeCompatibilityRules.IsCompatible(
                            definition,
                            cell.ResourceId))
                    {
                        compatibleNode = new ResourceNodeBinding(
                            ResourceNodeVisualId(x, y),
                            x,
                            y);
                    }
                }
            }

            ContentRoute route =
                RouteContentDisplayCatalog.BuildingRoute(definition);
            bool contentVisible =
                route == ContentRoute.Core ||
                session.HasContactedRoute(route);
            BuildingUnlockEvaluation unlock =
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    researchCompleted,
                    completedBuildings,
                    evaluationWorkspace.Unlock);
            bool canAfford = definition != null &&
                session.CityStorage.CanSpendFromNetwork(
                    definition.CostId,
                    definition.Cost);
            bool projectionSucceeded =
                hit.IsValid &&
                (hit.Site != BuildingSite.Ground || cityMapped);
            return new BuildingPlacementRequest(
                definition,
                grid,
                hit.Site,
                orientation,
                hit.X,
                hit.Y,
                cityX,
                cityY,
                session.GroundBuildRadius,
                city.Mode,
                projectionSucceeded,
                touchesCity,
                terrainPassable,
                obstacleFree,
                compatibleNode,
                contentVisible,
                unlock,
                canAfford);
        }

        private BuildingPlacementRequest CreateGroundRequest(
            BuildingDefinition definition,
            BuildingOrientation orientation,
            int anchorX,
            int anchorY)
        {
            var hit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                anchorX,
                anchorY,
                default,
                "外城");
            return CreateRequest(definition, hit, orientation);
        }

        private void BuildMiningGuidance(
            BuildingOrientation orientation,
            int cityX,
            int cityY,
            int radius,
            int worldWidth,
            int worldHeight)
        {
            miningNodes.Clear();
            miningAnchors.Clear();
            miningAnchorIndices.Clear();
            presentation.HideMiningGuidance();
            world.ClearResourceMarkerGuidanceOverrides();
            EnsureResourceNodeIdentityWorkspace();

            for (var x = 0; x < worldWidth; x++)
            for (var y = 0; y < worldHeight; y++)
            {
                WorldCell cell = world.Model.Get(x, y);
                if (!cell.HasResource ||
                    !BuildingResourceNodeCompatibilityRules.IsCompatible(
                        BuildingCatalog.MiningStation,
                        cell.ResourceId) ||
                    !BuildingRangeRules.IsGroundCellInRange(
                        cityX,
                        cityY,
                        x,
                        y,
                        radius))
                    continue;

                var node = new MiningGuidanceNode
                {
                    StableId = ResourceNodeVisualId(x, y),
                    X = x,
                    Y = y
                };
                CopyFootprintCoveringAnchors(
                    BuildingCatalog.MiningStation,
                    orientation,
                    x,
                    y,
                    miningCandidateWorkspace);
                for (var index = 0;
                     index < miningCandidateWorkspace.Count;
                     index++)
                {
                    Vector2Int candidate =
                        miningCandidateWorkspace[index];
                    long key = AnchorKey(candidate.x, candidate.y);
                    bool isValid;
                    if (miningAnchorIndices.TryGetValue(
                            key,
                            out int existingIndex))
                    {
                        isValid = miningAnchors[existingIndex].IsValid;
                    }
                    else
                    {
                        BuildingPlacementRequest request =
                            CreateGroundRequest(
                                BuildingCatalog.MiningStation,
                                orientation,
                                candidate.x,
                                candidate.y);
                        isValid = BuildingPlacementRules.Evaluate(
                            request,
                            miningGuidanceWorkspace).IsValid;
                        miningAnchorIndices.Add(
                            key,
                            miningAnchors.Count);
                        miningAnchors.Add(
                            new MiningGuidanceAnchor(
                                candidate.x,
                                candidate.y,
                                isValid));
                    }
                    if (isValid)
                        node.HasLegalAnchor = true;
                }
                miningNodes.Add(node);
            }

            for (var index = 0; index < miningNodes.Count; index++)
            {
                MiningGuidanceNode node = miningNodes[index];
                presentation.ShowMiningResourceNode(
                    node.StableId,
                    node.X,
                    node.Y,
                    worldWidth,
                    worldHeight,
                    node.HasLegalAnchor);
                world.SetResourceMarkerGuidanceOverride(
                    node.X,
                    node.Y,
                    true);
            }

            int width = BuildingOrientationRules.Width(
                BuildingCatalog.MiningStation,
                orientation);
            int height = BuildingOrientationRules.Height(
                BuildingCatalog.MiningStation,
                orientation);
            for (var index = 0; index < miningAnchors.Count; index++)
            {
                MiningGuidanceAnchor anchor = miningAnchors[index];
                if (!anchor.IsValid)
                    continue;
                presentation.ShowMiningAnchor(
                    MiningAnchorStableId(
                        anchor.X,
                        anchor.Y,
                        orientation),
                    anchor.X,
                    anchor.Y,
                    width,
                    height,
                    worldWidth,
                    worldHeight);
            }
        }

        private static void CopyFootprintCoveringAnchors(
            BuildingDefinition definition,
            BuildingOrientation orientation,
            int nodeX,
            int nodeY,
            List<Vector2Int> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            int width = BuildingOrientationRules.Width(
                definition,
                orientation);
            int height = BuildingOrientationRules.Height(
                definition,
                orientation);
            for (var x = nodeX - width + 1; x <= nodeX; x++)
            for (var y = nodeY - height + 1; y <= nodeY; y++)
                destination.Add(new Vector2Int(x, y));
        }

        private static long AnchorKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private static string MiningAnchorStableId(
            int x,
            int y,
            BuildingOrientation orientation)
        {
            return "building.anchor-highlight." + x + "." + y + "." +
                   orientation.ToString().ToLowerInvariant();
        }

        private BuildingPlacementRequest MissingRequest(
            BuildingDefinition definition)
        {
            return new BuildingPlacementRequest(
                definition,
                null,
                BuildingSite.Ground,
                interaction?.Orientation ?? BuildingOrientation.North,
                0,
                0,
                0,
                0,
                0,
                city?.Mode ?? default,
                false,
                false,
                true,
                true,
                false,
                null,
                false,
                default,
                false);
        }

        private BuildingPlacementRequest ProjectionFailedRequest(
            BuildingDefinition definition)
        {
            return new BuildingPlacementRequest(
                definition,
                session.GroundGrid,
                BuildingSite.Ground,
                interaction.Orientation,
                0,
                0,
                0,
                0,
                session.GroundBuildRadius,
                city.Mode,
                false,
                false,
                true,
                true,
                false,
                null,
                true,
                BuildingUnlockModel.Evaluate(
                    definition,
                    session.Population,
                    researchCompleted,
                    completedBuildings,
                    evaluationWorkspace.Unlock),
                definition != null &&
                session.CityStorage.CanSpendFromNetwork(
                    definition.CostId,
                    definition.Cost));
        }

        private void RefreshMaterialShortfalls(BuildingDefinition definition)
        {
            if (session?.CityStorage == null)
            {
                CurrentMaterialShortfalls = Array.Empty<ResourceShortfall>();
                return;
            }
            if (availableResourceAmount == null)
                availableResourceAmount = session.GetCityResourceAmount;
            if (ResourceShortfallRules.TryEvaluateBuilding(
                    definition,
                    availableResourceAmount,
                    out ResourceShortfall shortfall))
            {
                materialShortfallBuffer[0] = shortfall;
                CurrentMaterialShortfalls = materialShortfallBuffer;
            }
            else
            {
                CurrentMaterialShortfalls = Array.Empty<ResourceShortfall>();
            }
        }

        private void UpdateNodeHighlight(
            in BuildingPlacementEvaluation evaluation)
        {
            if (hasMiningGuidanceKey)
            {
                highlightedNodeId = null;
                return;
            }
            string next = evaluation.CompatibleResourceNodeId;
            if (string.Equals(
                    highlightedNodeId,
                    next,
                    StringComparison.Ordinal))
                return;

            ClearNodeHighlight();
            if (string.IsNullOrEmpty(next))
                return;
            BuildingCell node = FindNodeCell(evaluation, next);
            presentation.ShowCompatibleResourceNode(
                next,
                node.X,
                node.Y,
                true);
            highlightedNodeId = next;
        }

        private BuildingCell FindNodeCell(
            in BuildingPlacementEvaluation evaluation,
            string nodeId)
        {
            if (world?.Model == null || evaluation.Footprint == null)
                return default;
            for (var index = 0; index < evaluation.Footprint.Count; index++)
            {
                BuildingCell cell = evaluation.Footprint[index];
                if (cell.X < 0 ||
                    cell.Y < 0 ||
                    cell.X >= world.Model.Width ||
                    cell.Y >= world.Model.Height)
                    continue;
                if (string.Equals(
                        ExistingResourceNodeVisualId(
                            cell.X,
                            cell.Y),
                        nodeId,
                        StringComparison.Ordinal))
                    return cell;
            }
            return default;
        }

        private void ClearNodeHighlight()
        {
            if (hasMiningGuidanceKey)
            {
                highlightedNodeId = null;
                return;
            }
            if (string.IsNullOrEmpty(highlightedNodeId) ||
                presentation == null)
                return;
            presentation.ShowCompatibleResourceNode(
                highlightedNodeId,
                0,
                0,
                false);
            highlightedNodeId = null;
        }

        private void ConfigureResourceNodeIdentityWorkspace(
            GrayboxWorldView3D configuredWorld)
        {
            WorldMapModel model = configuredWorld?.Model;
            if (model == null)
            {
                resourceNodeVisualIds = null;
                resourceNodeVisualWidth = 0;
                resourceNodeVisualHeight = 0;
                return;
            }

            resourceNodeVisualWidth = model.Width;
            resourceNodeVisualHeight = model.Height;
            resourceNodeVisualIds =
                new string[
                    resourceNodeVisualWidth *
                    resourceNodeVisualHeight];
        }

        private void EnsureResourceNodeIdentityWorkspace()
        {
            WorldMapModel model = world?.Model;
            if (model == null ||
                (resourceNodeVisualIds != null &&
                 resourceNodeVisualWidth == model.Width &&
                 resourceNodeVisualHeight == model.Height))
                return;
            ConfigureResourceNodeIdentityWorkspace(world);
        }

        private string ResourceNodeVisualId(int x, int y)
        {
            if (!IsResourceNodeIdentityInBounds(x, y))
                return CreateResourceNodeVisualId(x, y);
            int index = y * resourceNodeVisualWidth + x;
            string stableId = resourceNodeVisualIds[index];
            if (stableId != null)
                return stableId;
            stableId = CreateResourceNodeVisualId(x, y);
            resourceNodeVisualIds[index] = stableId;
            return stableId;
        }

        private string ExistingResourceNodeVisualId(int x, int y)
        {
            if (!IsResourceNodeIdentityInBounds(x, y))
                return null;
            return resourceNodeVisualIds[
                y * resourceNodeVisualWidth + x];
        }

        private bool IsResourceNodeIdentityInBounds(int x, int y)
        {
            return resourceNodeVisualIds != null &&
                x >= 0 &&
                y >= 0 &&
                x < resourceNodeVisualWidth &&
                y < resourceNodeVisualHeight;
        }

        private void OnEnable()
        {
            if (evaluationWorkspace == null)
                BeginConfiguredLifetime();
        }

        private void OnDisable()
        {
            DiscardConfiguredLifetime();
        }

        private void OnDestroy()
        {
            DiscardConfiguredLifetime();
        }

        private void DiscardConfiguredLifetime()
        {
            if (!string.IsNullOrEmpty(highlightedNodeId) &&
                presentation != null)
            {
                presentation.ShowCompatibleResourceNode(
                    highlightedNodeId,
                    0,
                    0,
                    false);
            }
            highlightedNodeId = null;
            presentation?.HideMiningGuidance();
            presentation?.HidePreview();
            world?.ClearResourceMarkerGuidanceOverrides();
            hasPointer = false;
            lastScreenPosition = default;
            CurrentHit = BuildingSurfaceHit.Invalid;
            CurrentEvaluation = default;
            evaluationWorkspace = null;
            miningGuidanceWorkspace = null;
            miningCandidateWorkspace = null;
            miningNodes = null;
            miningAnchors = null;
            miningAnchorIndices = null;
            hasMiningGuidanceKey = false;
            researchCompleted = null;
            completedBuildings = null;
            availableResourceAmount = null;
            resourceNodeVisualIds = null;
            resourceNodeVisualWidth = 0;
            resourceNodeVisualHeight = 0;
        }
    }
}
