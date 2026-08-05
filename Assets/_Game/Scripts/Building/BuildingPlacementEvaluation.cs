using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.City;

namespace WasteCity.Building
{
    public enum BuildingPlacementFailure
    {
        None = 0,
        MissingReference,
        ProjectionFailed,
        OutOfBounds,
        UnsupportedSite,
        InvalidCityMode,
        OutsideBuildRange,
        Overlap,
        CityOccupied,
        InvalidTerrain,
        Obstacle,
        IncompatibleResourceNode,
        ContentUnavailable,
        PopulationRequired,
        PrerequisiteBuildingRequired,
        InsufficientMaterials
    }

    public readonly struct BuildingCell
    {
        public int X { get; }
        public int Y { get; }

        public BuildingCell(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public readonly struct BuildingPlacementRequest
    {
        public BuildingDefinition Definition { get; }
        public BuildingGrid Grid { get; }
        public BuildingSite Site { get; }
        public BuildingOrientation Orientation { get; }
        public int X { get; }
        public int Y { get; }
        public int CityX { get; }
        public int CityY { get; }
        public int GroundRadius { get; }
        public CityMode CityMode { get; }
        public bool ProjectionSucceeded { get; }
        public bool FootprintTouchesCity { get; }
        public bool TerrainPassable { get; }
        public bool ObstacleFree { get; }
        public bool CoversCompatibleResourceNode { get; }
        public string CompatibleResourceNodeId { get; }
        public bool ContentVisible { get; }
        public BuildingUnlockEvaluation Unlock { get; }
        public bool CanAfford { get; }

        public BuildingPlacementRequest(
            BuildingDefinition definition,
            BuildingGrid grid,
            BuildingSite site,
            BuildingOrientation orientation,
            int x,
            int y,
            int cityX,
            int cityY,
            int groundRadius,
            CityMode cityMode,
            bool projectionSucceeded,
            bool footprintTouchesCity,
            bool terrainPassable,
            bool obstacleFree,
            bool coversCompatibleResourceNode,
            string compatibleResourceNodeId,
            bool contentVisible,
            BuildingUnlockEvaluation unlock,
            bool canAfford)
        {
            Definition = definition;
            Grid = grid;
            Site = site;
            Orientation = orientation;
            X = x;
            Y = y;
            CityX = cityX;
            CityY = cityY;
            GroundRadius = groundRadius;
            CityMode = cityMode;
            ProjectionSucceeded = projectionSucceeded;
            FootprintTouchesCity = footprintTouchesCity;
            TerrainPassable = terrainPassable;
            ObstacleFree = obstacleFree;
            CoversCompatibleResourceNode = coversCompatibleResourceNode;
            CompatibleResourceNodeId = compatibleResourceNodeId;
            ContentVisible = contentVisible;
            Unlock = unlock;
            CanAfford = canAfford;
        }
    }

    public readonly struct BuildingPlacementEvaluation
    {
        public bool IsValid { get; }
        public BuildingPlacementFailure PrimaryFailure { get; }
        public IReadOnlyList<BuildingPlacementFailure> Failures { get; }
        public BuildingSite Site { get; }
        public BuildingOrientation Orientation { get; }
        public int RotatedWidth { get; }
        public int RotatedHeight { get; }
        public string CompatibleResourceNodeId { get; }
        public IReadOnlyList<BuildingCell> Footprint { get; }

        internal BuildingPlacementEvaluation(
            IReadOnlyList<BuildingPlacementFailure> failures,
            BuildingSite site,
            BuildingOrientation orientation,
            int rotatedWidth,
            int rotatedHeight,
            string compatibleResourceNodeId,
            IReadOnlyList<BuildingCell> footprint)
        {
            IsValid = failures.Count == 0;
            PrimaryFailure = IsValid ? BuildingPlacementFailure.None : failures[0];
            Failures = failures;
            Site = site;
            Orientation = orientation;
            RotatedWidth = rotatedWidth;
            RotatedHeight = rotatedHeight;
            CompatibleResourceNodeId = compatibleResourceNodeId;
            Footprint = footprint;
        }
    }

    public static class BuildingPlacementRules
    {
        public static BuildingPlacementEvaluation Evaluate(in BuildingPlacementRequest request)
        {
            var failures = new List<BuildingPlacementFailure>();
            var footprint = new List<BuildingCell>();
            var hasDefinition = request.Definition != null;
            var hasGrid = request.Grid != null;
            var hasValidOrientation = Enum.IsDefined(typeof(BuildingOrientation), request.Orientation);
            var rotatedWidth = 0;
            var rotatedHeight = 0;

            if (!hasDefinition || !hasGrid || !hasValidOrientation)
                failures.Add(BuildingPlacementFailure.MissingReference);

            if (hasDefinition && hasValidOrientation)
            {
                rotatedWidth = BuildingOrientationRules.Width(request.Definition, request.Orientation);
                rotatedHeight = BuildingOrientationRules.Height(request.Definition, request.Orientation);
                for (var dx = 0; dx < rotatedWidth; dx++)
                    for (var dy = 0; dy < rotatedHeight; dy++)
                    {
                        var cellX = (long)request.X + dx;
                        var cellY = (long)request.Y + dy;
                        if (cellX < int.MinValue || cellX > int.MaxValue ||
                            cellY < int.MinValue || cellY > int.MaxValue)
                            continue;
                        footprint.Add(new BuildingCell((int)cellX, (int)cellY));
                    }
            }

            if (!request.ProjectionSucceeded) failures.Add(BuildingPlacementFailure.ProjectionFailed);

            var footprintInBounds = hasDefinition && hasGrid && hasValidOrientation &&
                (request.Site == BuildingSite.InnerCity
                    ? BuildingRangeRules.IsInnerFootprintInBounds(request.Definition, request.X, request.Y, request.Orientation)
                    : request.Grid.ContainsFootprint(request.Definition, request.X, request.Y, request.Orientation));
            if (hasDefinition && hasGrid && hasValidOrientation && !footprintInBounds)
                failures.Add(BuildingPlacementFailure.OutOfBounds);

            var supportsSite = hasDefinition && BuildingMobilityRules.SupportsSite(request.Definition, request.Site);
            if (hasDefinition && !supportsSite) failures.Add(BuildingPlacementFailure.UnsupportedSite);
            if (supportsSite && !BuildingMobilityRules.CanConstruct(request.Definition, request.Site, request.CityMode))
                failures.Add(BuildingPlacementFailure.InvalidCityMode);

            if (hasDefinition && hasValidOrientation && request.Site == BuildingSite.Ground && !IsGroundFootprintInRange(request, footprint))
                failures.Add(BuildingPlacementFailure.OutsideBuildRange);
            if (hasGrid && footprintInBounds && IsAnyFootprintCellOccupied(request.Grid, footprint))
                failures.Add(BuildingPlacementFailure.Overlap);
            if (request.FootprintTouchesCity) failures.Add(BuildingPlacementFailure.CityOccupied);
            if (!request.TerrainPassable) failures.Add(BuildingPlacementFailure.InvalidTerrain);
            if (!request.ObstacleFree) failures.Add(BuildingPlacementFailure.Obstacle);
            var hasCompatibleResourceNode = request.CoversCompatibleResourceNode &&
                !string.IsNullOrEmpty(request.CompatibleResourceNodeId);
            if (hasDefinition && request.Definition.RequiresResourceNode && !hasCompatibleResourceNode)
                failures.Add(BuildingPlacementFailure.IncompatibleResourceNode);
            if (!request.ContentVisible || ContainsUnlockFailure(request.Unlock, BuildingUnlockFailure.Research))
                failures.Add(BuildingPlacementFailure.ContentUnavailable);
            if (ContainsUnlockFailure(request.Unlock, BuildingUnlockFailure.Population))
                failures.Add(BuildingPlacementFailure.PopulationRequired);
            if (ContainsUnlockFailure(request.Unlock, BuildingUnlockFailure.RequiredBuilding))
                failures.Add(BuildingPlacementFailure.PrerequisiteBuildingRequired);
            if (!request.CanAfford) failures.Add(BuildingPlacementFailure.InsufficientMaterials);

            var nodeId = hasDefinition && request.Definition.RequiresResourceNode && hasCompatibleResourceNode
                ? request.CompatibleResourceNodeId
                : null;
            return new BuildingPlacementEvaluation(
                Snapshot(failures),
                request.Site,
                request.Orientation,
                rotatedWidth,
                rotatedHeight,
                nodeId,
                Snapshot(footprint));
        }

        private static bool IsGroundFootprintInRange(
            BuildingPlacementRequest request,
            IReadOnlyList<BuildingCell> footprint)
        {
            for (var index = 0; index < footprint.Count; index++)
                if (!BuildingRangeRules.IsGroundCellInRange(
                    request.CityX,
                    request.CityY,
                    footprint[index].X,
                    footprint[index].Y,
                    request.GroundRadius))
                    return false;
            return true;
        }

        private static bool IsAnyFootprintCellOccupied(BuildingGrid grid, IReadOnlyList<BuildingCell> footprint)
        {
            for (var index = 0; index < footprint.Count; index++)
                if (grid.IsOccupied(footprint[index].X, footprint[index].Y)) return true;
            return false;
        }

        private static bool ContainsUnlockFailure(BuildingUnlockEvaluation unlock, BuildingUnlockFailure failure)
        {
            if (unlock.Failures == null) return false;
            for (var index = 0; index < unlock.Failures.Count; index++)
                if (unlock.Failures[index] == failure) return true;
            return false;
        }

        private static IReadOnlyList<T> Snapshot<T>(List<T> values)
        {
            return new ReadOnlyCollection<T>(values.ToArray());
        }
    }
}
