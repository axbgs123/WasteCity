using System;
using UnityEngine;
using WasteCity.Building;
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
        private string[] resourceNodeVisualIds;
        private int resourceNodeVisualWidth;
        private int resourceNodeVisualHeight;

        public BuildingPlacementEvaluation CurrentEvaluation
        {
            get;
            private set;
        }
        public BuildingSurfaceHit CurrentHit { get; private set; } =
            BuildingSurfaceHit.Invalid;

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
            evaluationWorkspace =
                new BuildingPlacementEvaluationWorkspace();
            researchCompleted =
                session == null
                    ? null
                    : session.IsResearchCompleted;
            completedBuildings =
                session == null
                    ? null
                    : session.CompletedBuildingCount;
            ConfigureResourceNodeIdentityWorkspace(world);
        }

        public static string CreateResourceNodeVisualId(
            int worldX,
            int worldY)
        {
            return $"world.resource-node.{worldX}.{worldY}";
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
            presentation?.HidePreview();
            CurrentHit = BuildingSurfaceHit.Invalid;
        }

        private bool EvaluatePointer(Vector2 screenPosition)
        {
            if (evaluationWorkspace == null)
            {
                HidePreview();
                CurrentEvaluation = default;
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
                return false;
            }

            if (!projector.TryProject(screenPosition, out BuildingSurfaceHit hit))
            {
                CurrentHit = BuildingSurfaceHit.Invalid;
                CurrentEvaluation = BuildingPlacementRules.Evaluate(
                    ProjectionFailedRequest(definition),
                    evaluationWorkspace);
                ClearNodeHighlight();
                presentation.HidePreview();
                return false;
            }

            CurrentHit = hit;
            CurrentEvaluation = BuildingPlacementRules.Evaluate(
                CreateRequest(definition, hit),
                evaluationWorkspace);
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
            bool coversCompatibleNode = false;
            string compatibleNodeId = null;

            if (definition != null &&
                hit.Site == BuildingSite.Ground &&
                world.Model != null &&
                world.Coordinates != null)
            {
                int width = BuildingOrientationRules.Width(
                    definition,
                    interaction.Orientation);
                int height = BuildingOrientationRules.Height(
                    definition,
                    interaction.Orientation);
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
                        Math.Abs(x - cityX) <= 1 &&
                        Math.Abs(y - cityY) <= 1)
                        touchesCity = true;
                    if (!coversCompatibleNode &&
                        IsCompatibleResourceNode(definition, cell))
                    {
                        coversCompatibleNode = true;
                        compatibleNodeId =
                            ResourceNodeVisualId(x, y);
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
                session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost);
            bool projectionSucceeded =
                hit.IsValid &&
                (hit.Site != BuildingSite.Ground || cityMapped);
            return new BuildingPlacementRequest(
                definition,
                grid,
                hit.Site,
                interaction.Orientation,
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
                coversCompatibleNode,
                compatibleNodeId,
                contentVisible,
                unlock,
                canAfford);
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
                session.Inventory.CanSpend(
                    definition.CostId,
                    definition.Cost));
        }

        private static bool IsCompatibleResourceNode(
            BuildingDefinition definition,
            WorldCell cell)
        {
            return ReferenceEquals(definition, BuildingCatalog.MiningStation) &&
                cell.HasResource &&
                (string.Equals(
                     cell.ResourceId,
                     ResourceIds.Iron,
                     StringComparison.Ordinal) ||
                 string.Equals(
                     cell.ResourceId,
                     ResourceIds.EnergyCrystal,
                     StringComparison.Ordinal));
        }

        private void UpdateNodeHighlight(
            in BuildingPlacementEvaluation evaluation)
        {
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
            presentation?.HidePreview();
            hasPointer = false;
            lastScreenPosition = default;
            CurrentHit = BuildingSurfaceHit.Invalid;
            CurrentEvaluation = default;
            evaluationWorkspace = null;
        }
    }
}
