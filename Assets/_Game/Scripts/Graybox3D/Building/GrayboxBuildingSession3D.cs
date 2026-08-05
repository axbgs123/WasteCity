using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxBuildingInstanceState
    {
        UnderConstruction,
        Completed,
        AbandonedRuin
    }

    public sealed class GrayboxBuildingInstance3D
    {
        internal GrayboxBuildingInstance3D(
            string stableInstanceId,
            PlacedBuilding placement,
            ConstructionProgress progress)
        {
            StableInstanceId = stableInstanceId;
            Placement = placement;
            Progress = progress;
            State = GrayboxBuildingInstanceState.UnderConstruction;
            IsPlayerOwned = true;
            IsEvacuationLocked = false;
        }

        public string StableInstanceId { get; }
        public PlacedBuilding Placement { get; }
        public ConstructionProgress Progress { get; }
        public GrayboxBuildingInstanceState State { get; private set; }
        public bool IsPlayerOwned { get; }
        public bool IsEvacuationLocked { get; }

        internal void Complete()
        {
            State = GrayboxBuildingInstanceState.Completed;
        }

        internal void RestoreConstruction(float remaining)
        {
            Progress.Restore(remaining);
            State = GrayboxBuildingInstanceState.UnderConstruction;
        }
    }

    public interface IGrayboxBuildingPresentation3D
    {
        bool TryCreate(GrayboxBuildingInstance3D instance);
        void UpdateInstance(GrayboxBuildingInstance3D instance);
        void Remove(GrayboxBuildingInstance3D instance);
    }

    public sealed class GrayboxBuildingSession3D :
        MonoBehaviour,
        IGrayboxBuildingCatalogContext3D
    {
        public const int ResourceCapacity = 5000;
        private const int DevelopmentPopulation = 200;
        private const int GroundGridWidth = 32;
        private const int GroundGridHeight = 24;
        private const int InnerGridWidth = 8;
        private const int InnerGridHeight = 6;
        private const int DevelopmentGroundBuildRadius = 8;
        private const string PresentationCleanupFailureDataKey =
            "WasteCity.Graybox3D.Building.PresentationCleanupFailure";

        [SerializeField] private bool developmentFixtureEnabled;

        private readonly HashSet<ContentRoute> contactedRoutes =
            new HashSet<ContentRoute>();
        private List<GrayboxBuildingInstance3D> instances;
        private IReadOnlyList<GrayboxBuildingInstance3D> readOnlyInstances;
        private int nextStableInstanceOrdinal;

        public bool DevelopmentFixtureEnabled => developmentFixtureEnabled;
        public ResourceInventory Inventory { get; private set; }
        public ResearchModel Research { get; private set; }
        public BuildingGrid GroundGrid { get; private set; }
        public BuildingGrid InnerGrid { get; private set; }
        public int Population { get; private set; }
        public int GroundBuildRadius { get; private set; }
        public float ConstructionMultiplier { get; private set; }
        public IReadOnlyList<GrayboxBuildingInstance3D> Instances =>
            readOnlyInstances;

        private void Awake()
        {
            if (developmentFixtureEnabled)
                ConfigureDevelopmentFixture();
        }

        public void Configure(bool developmentFixtureEnabled)
        {
            this.developmentFixtureEnabled = developmentFixtureEnabled;
        }

        public void ConfigureDevelopmentFixture()
        {
            Inventory = new ResourceInventory(ResourceCapacity);
            Inventory.Set(ResourceIds.Iron, 30);
            Inventory.Set(ResourceIds.EnergyCrystal, 10);
            Inventory.Set(ResourceIds.Stone, 30);
            Inventory.Set(ResourceIds.Biomass, 20);
            Inventory.Set(ResourceIds.Water, 20);
            Inventory.Set(ResourceIds.Alloy, 30);
            Research = new ResearchModel();
            GroundGrid = new BuildingGrid(GroundGridWidth, GroundGridHeight);
            InnerGrid = new BuildingGrid(InnerGridWidth, InnerGridHeight);
            Population = DevelopmentPopulation;
            GroundBuildRadius = DevelopmentGroundBuildRadius;
            ConstructionMultiplier = 1f;
            contactedRoutes.Clear();
            instances = new List<GrayboxBuildingInstance3D>();
            readOnlyInstances =
                new ReadOnlyCollection<GrayboxBuildingInstance3D>(instances);
            nextStableInstanceOrdinal = 1;
        }

        public bool TryBeginConstruction(
            in BuildingPlacementRequest request,
            IGrayboxBuildingPresentation3D presentation,
            out GrayboxBuildingInstance3D instance,
            out BuildingPlacementEvaluation evaluation)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            EnsureConfigured();

            BuildingPlacementRequest refreshed = RefreshRequest(request);
            evaluation = BuildingPlacementRules.Evaluate(refreshed);
            instance = null;
            if (!evaluation.IsValid) return false;

            int inventoryBefore =
                Inventory.Get(refreshed.Definition.CostId);
            if (!Inventory.TrySpend(
                    refreshed.Definition.CostId,
                    refreshed.Definition.Cost))
                return false;

            if (!refreshed.Grid.TryRestore(
                    refreshed.Definition,
                    refreshed.X,
                    refreshed.Y,
                    out PlacedBuilding placement,
                    refreshed.Site,
                    refreshed.Orientation))
            {
                Inventory.Restore(
                    refreshed.Definition.CostId,
                    inventoryBefore);
                return false;
            }

            var candidate = new GrayboxBuildingInstance3D(
                CreateStableInstanceId(nextStableInstanceOrdinal),
                placement,
                new ConstructionProgress(refreshed.Definition.BuildSeconds));
            bool presentationCreated;
            try
            {
                presentationCreated = presentation.TryCreate(candidate);
            }
            catch (Exception createFailure)
            {
                Exception cleanupFailure =
                    TryRemovePresentation(presentation, candidate);
                RollbackPlacement(
                    refreshed.Grid,
                    placement,
                    refreshed.Definition.CostId,
                    inventoryBefore);
                if (cleanupFailure != null)
                    createFailure.Data[PresentationCleanupFailureDataKey] =
                        cleanupFailure;
                throw;
            }
            if (!presentationCreated)
            {
                Exception cleanupFailure =
                    TryRemovePresentation(presentation, candidate);
                RollbackPlacement(
                    refreshed.Grid,
                    placement,
                    refreshed.Definition.CostId,
                    inventoryBefore);
                if (cleanupFailure != null) throw cleanupFailure;
                return false;
            }

            instances.Add(candidate);
            nextStableInstanceOrdinal++;
            instance = candidate;
            return true;
        }

        public bool TryCancelConstruction(
            string stableInstanceId,
            double handlingRatio,
            IGrayboxBuildingPresentation3D presentation,
            out int acceptedRefund)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            EnsureConfigured();
            acceptedRefund = 0;
            int index = FindInstanceIndex(stableInstanceId);
            if (index < 0) return false;

            GrayboxBuildingInstance3D instance = instances[index];
            if (instance.State != GrayboxBuildingInstanceState.UnderConstruction)
                return false;

            BuildingGrid grid = instance.Placement.Site == BuildingSite.InnerCity
                ? InnerGrid
                : GroundGrid;
            double remainingRatio =
                instance.Progress.Remaining / instance.Progress.BaseDuration;
            int refund = ConstructionRefundRules.Calculate(
                instance.Placement.Definition.Cost,
                remainingRatio,
                handlingRatio);
            try
            {
                presentation.Remove(instance);
            }
            catch (Exception removeFailure)
            {
                Exception restoreFailure =
                    TryRestorePresentation(presentation, instance);
                if (restoreFailure != null)
                    throw CreatePresentationRestoreFailure(
                        removeFailure,
                        restoreFailure);
                throw;
            }

            if (!grid.Remove(instance.Placement))
            {
                Exception restoreFailure =
                    TryRestorePresentation(presentation, instance);
                if (restoreFailure != null)
                    throw new InvalidOperationException(
                        "Failed to restore presentation after grid removal failed.",
                        restoreFailure);
                return false;
            }

            acceptedRefund = Inventory.Add(
                instance.Placement.Definition.CostId,
                refund);
            instances.RemoveAt(index);
            return true;
        }

        public void TickConstruction(
            float unscaledDeltaTime,
            CityMode mode,
            bool paused,
            IGrayboxBuildingPresentation3D presentation)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            EnsureConfigured();
            if (paused || unscaledDeltaTime <= 0f) return;

            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance.State != GrayboxBuildingInstanceState.UnderConstruction ||
                    !BuildingMobilityRules.CanConstruct(
                        instance.Placement.Definition,
                        instance.Placement.Site,
                        mode))
                    continue;

                float remainingBefore = instance.Progress.Remaining;
                bool completed = instance.Progress.Tick(
                    unscaledDeltaTime,
                    ConstructionMultiplier);
                if (instance.Progress.Remaining >= remainingBefore) continue;
                if (completed) instance.Complete();
                try
                {
                    presentation.UpdateInstance(instance);
                }
                catch
                {
                    instance.RestoreConstruction(remainingBefore);
                    throw;
                }
            }
        }

        public int CompletedBuildingCount(string id)
        {
            EnsureConfigured();
            if (!IsKnownBuildingId(id)) return 0;

            var count = 0;
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance.State == GrayboxBuildingInstanceState.Completed &&
                    instance.IsPlayerOwned &&
                    !instance.IsEvacuationLocked &&
                    string.Equals(
                        instance.Placement.Definition.Id.Value,
                        id,
                        StringComparison.Ordinal))
                    count++;
            }
            return count;
        }

        public bool IsResearchCompleted(string id)
        {
            EnsureConfigured();
            ResearchDefinition definition = ResearchCatalog.Find(id);
            return definition != null && Research.IsCompleted(definition.Id);
        }

        public bool HasContactedRoute(ContentRoute route)
        {
            EnsureConfigured();
            return contactedRoutes.Contains(route);
        }

        public void SetRouteContact(ContentRoute route, bool contacted)
        {
            EnsureConfigured();
            if (route == ContentRoute.Core ||
                !Enum.IsDefined(typeof(ContentRoute), route))
                return;
            if (contacted)
                contactedRoutes.Add(route);
            else
                contactedRoutes.Remove(route);
        }

        public void UnlockResearchForDevelopment(string researchId)
        {
            EnsureConfigured();
            ResearchDefinition definition = ResearchCatalog.Find(researchId);
            if (definition == null || Research.IsCompleted(definition.Id)) return;

            string[] completed = Research.CaptureCompleted();
            var restored = new string[completed.Length + 1];
            Array.Copy(completed, restored, completed.Length);
            restored[completed.Length] = definition.Id.Value;
            Research.Restore(restored, null, 0f);
        }

        public void UnlockRouteForDevelopment(ContentRoute route)
        {
            EnsureConfigured();
            if (!TryMapRoute(route, out DevelopmentRoute developmentRoute))
                return;

            SetRouteContact(route, true);
            for (var index = 0; index < ResearchCatalog.All.Length; index++)
                if (ResearchCatalog.All[index].Route == developmentRoute)
                    UnlockResearchForDevelopment(
                        ResearchCatalog.All[index].Id.Value);
        }

        public void UnlockAllResearchForDevelopment()
        {
            EnsureConfigured();
            SetRouteContact(ContentRoute.Technology, true);
            SetRouteContact(ContentRoute.Cultivation, true);
            SetRouteContact(ContentRoute.BiologicalAscension, true);
            SetRouteContact(ContentRoute.Psionics, true);
            var completed = new string[ResearchCatalog.All.Length];
            for (var index = 0; index < ResearchCatalog.All.Length; index++)
                completed[index] = ResearchCatalog.All[index].Id.Value;
            Research.Restore(completed, null, 0f);
        }

        public void SetConstructionMultiplierForDevelopment(float value)
        {
            EnsureConfigured();
            ConstructionMultiplier = Math.Max(0f, value);
        }

        public void CompleteAllConstructionForDevelopment(
            IGrayboxBuildingPresentation3D presentation)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            EnsureConfigured();
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance.State != GrayboxBuildingInstanceState.UnderConstruction)
                    continue;
                instance.Progress.Restore(0f);
                instance.Complete();
                presentation.UpdateInstance(instance);
            }
        }

        private BuildingPlacementRequest RefreshRequest(
            in BuildingPlacementRequest request)
        {
            BuildingGrid expectedGrid = request.Site == BuildingSite.InnerCity
                ? InnerGrid
                : GroundGrid;
            BuildingGrid evaluationGrid = ReferenceEquals(request.Grid, expectedGrid)
                ? expectedGrid
                : null;
            BuildingDefinition definition = request.Definition;
            ContentRoute route =
                RouteContentDisplayCatalog.BuildingRoute(definition);
            bool contentVisible =
                route == ContentRoute.Core || contactedRoutes.Contains(route);
            BuildingUnlockEvaluation unlock = BuildingUnlockModel.Evaluate(
                definition,
                Population,
                IsResearchCompleted,
                CompletedBuildingCount);
            bool canAfford = definition != null &&
                Inventory.CanSpend(definition.CostId, definition.Cost);
            return new BuildingPlacementRequest(
                definition,
                evaluationGrid,
                request.Site,
                request.Orientation,
                request.X,
                request.Y,
                request.CityX,
                request.CityY,
                GroundBuildRadius,
                request.CityMode,
                request.ProjectionSucceeded,
                request.FootprintTouchesCity,
                request.TerrainPassable,
                request.ObstacleFree,
                request.CoversCompatibleResourceNode,
                request.CompatibleResourceNodeId,
                contentVisible,
                unlock,
                canAfford);
        }

        private void EnsureConfigured()
        {
            if (Inventory == null || Research == null || GroundGrid == null ||
                InnerGrid == null || instances == null)
                ConfigureDevelopmentFixture();
        }

        private int FindInstanceIndex(string stableInstanceId)
        {
            if (string.IsNullOrEmpty(stableInstanceId)) return -1;
            for (var index = 0; index < instances.Count; index++)
                if (string.Equals(
                    instances[index].StableInstanceId,
                    stableInstanceId,
                    StringComparison.Ordinal))
                    return index;
            return -1;
        }

        private static bool IsKnownBuildingId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
                if (string.Equals(
                    BuildingCatalog.All[index].Id.Value,
                    id,
                    StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool TryMapRoute(
            ContentRoute route,
            out DevelopmentRoute developmentRoute)
        {
            switch (route)
            {
                case ContentRoute.Technology:
                    developmentRoute = DevelopmentRoute.Technology;
                    return true;
                case ContentRoute.Cultivation:
                    developmentRoute = DevelopmentRoute.Cultivation;
                    return true;
                case ContentRoute.BiologicalAscension:
                    developmentRoute = DevelopmentRoute.BiologicalAscension;
                    return true;
                case ContentRoute.Psionics:
                    developmentRoute = DevelopmentRoute.Psionics;
                    return true;
                default:
                    developmentRoute = default(DevelopmentRoute);
                    return false;
            }
        }

        private static string CreateStableInstanceId(int ordinal)
        {
            return $"building.instance.{ordinal:000000}";
        }

        private static Exception TryRemovePresentation(
            IGrayboxBuildingPresentation3D presentation,
            GrayboxBuildingInstance3D candidate)
        {
            try
            {
                presentation.Remove(candidate);
                return null;
            }
            catch (Exception cleanupFailure)
            {
                return cleanupFailure;
            }
        }

        private static Exception TryRestorePresentation(
            IGrayboxBuildingPresentation3D presentation,
            GrayboxBuildingInstance3D instance)
        {
            try
            {
                if (presentation.TryCreate(instance)) return null;
            }
            catch (Exception restoreFailure)
            {
                return restoreFailure;
            }

            try
            {
                presentation.Remove(instance);
            }
            catch (Exception restoreFailure)
            {
                return restoreFailure;
            }

            try
            {
                return presentation.TryCreate(instance)
                    ? null
                    : new InvalidOperationException(
                        "Presentation recreation returned false.");
            }
            catch (Exception restoreFailure)
            {
                return restoreFailure;
            }
        }

        private static InvalidOperationException
            CreatePresentationRestoreFailure(
                Exception operationFailure,
                Exception restoreFailure)
        {
            return new InvalidOperationException(
                "Failed to restore presentation after cancellation failure.",
                new AggregateException(operationFailure, restoreFailure));
        }

        private void RollbackPlacement(
            BuildingGrid grid,
            PlacedBuilding placement,
            string costId,
            int inventoryBefore)
        {
            grid.Remove(placement);
            Inventory.Restore(costId, inventoryBefore);
        }
    }
}
