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
            ConstructionProgress progress,
            ResourceNodeBinding boundResourceNode)
        {
            StableInstanceId = stableInstanceId;
            Placement = placement;
            Progress = progress;
            BoundResourceNode = boundResourceNode;
            State = GrayboxBuildingInstanceState.UnderConstruction;
            IsPlayerOwned = true;
            IsEvacuationLocked = false;
        }

        public string StableInstanceId { get; }
        public PlacedBuilding Placement { get; }
        public ConstructionProgress Progress { get; }
        public ResourceNodeBinding BoundResourceNode { get; }
        public GrayboxBuildingInstanceState State { get; private set; }
        public bool IsPlayerOwned { get; private set; }
        public bool IsEvacuationLocked { get; private set; }

        internal void Complete()
        {
            State = GrayboxBuildingInstanceState.Completed;
        }

        internal void RestoreConstruction(float remaining)
        {
            Progress.Restore(remaining);
            State = GrayboxBuildingInstanceState.UnderConstruction;
        }

        internal void SetEvacuationLocked(bool value)
        {
            IsEvacuationLocked = value;
        }

        internal void Abandon()
        {
            IsPlayerOwned = false;
            State = GrayboxBuildingInstanceState.AbandonedRuin;
        }

        internal void RestoreEvacuationState(
            bool playerOwned,
            GrayboxBuildingInstanceState state)
        {
            IsPlayerOwned = playerOwned;
            State = state;
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
        private const int InnerGridWidth = 8;
        private const int InnerGridHeight = 6;
        private const int DevelopmentGroundBuildRadius = 8;
        private const string PresentationCleanupFailureDataKey =
            "WasteCity.Graybox3D.Building.PresentationCleanupFailure";

        [SerializeField] private bool developmentFixtureEnabled;

        private readonly HashSet<ContentRoute> contactedRoutes =
            new HashSet<ContentRoute>();
        private List<GrayboxBuildingInstance3D> instances;
        private readonly Dictionary<string, BuildingEvacuationWork> evacuationLocks =
            new Dictionary<string, BuildingEvacuationWork>(StringComparer.Ordinal);
        private readonly Dictionary<string, BuildingEvacuationWork> evacuationSnapshots =
            new Dictionary<string, BuildingEvacuationWork>(StringComparer.Ordinal);
        private IReadOnlyList<GrayboxBuildingInstance3D> readOnlyInstances;
        private int nextStableInstanceOrdinal;
        private uint catalogRevision;
        private uint placementRevision;

        public bool DevelopmentFixtureEnabled => developmentFixtureEnabled;
        public ResourceInventory Inventory { get; private set; }
        public ResearchModel Research { get; private set; }
        public BuildingGrid GroundGrid { get; private set; }
        public BuildingGrid InnerGrid { get; private set; }
        public int Population { get; private set; }
        public int GroundBuildRadius { get; private set; }
        public float ConstructionMultiplier { get; private set; }
        public uint CatalogRevision => catalogRevision;
        public uint PlacementRevision => placementRevision;
        public IReadOnlyList<GrayboxBuildingInstance3D> Instances =>
            readOnlyInstances;

        private void Awake()
        {
            if (developmentFixtureEnabled)
                ConfigureDevelopmentFixture();
            else
                ConfigureFormalSession();
        }

        public void Configure(bool developmentFixtureEnabled)
        {
            this.developmentFixtureEnabled = developmentFixtureEnabled;
        }

        public void ConfigureDevelopmentFixture()
        {
            var inventory = new ResourceInventory(ResourceCapacity);
            inventory.Set(ResourceIds.Iron, 30);
            inventory.Set(ResourceIds.EnergyCrystal, 10);
            inventory.Set(ResourceIds.Stone, 30);
            inventory.Set(ResourceIds.Biomass, 20);
            inventory.Set(ResourceIds.Water, 20);
            inventory.Set(ResourceIds.Alloy, 30);
            ConfigureSession(inventory);
        }

        public void ConfigureFormalSession()
        {
            ConfigureSession(
                ResourceDefinitionCatalog.CreateFormalCityInventory());
        }

        private void ConfigureSession(ResourceInventory inventory)
        {
            if (Research != null)
                Research.Completed -= HandleResearchCompleted;
            Inventory = inventory ??
                throw new ArgumentNullException(nameof(inventory));
            Research = new ResearchModel();
            Research.Completed += HandleResearchCompleted;
            GroundGrid = new BuildingGrid(
                GrayboxWorldLayout3D.WorldWidth,
                GrayboxWorldLayout3D.WorldHeight);
            InnerGrid = new BuildingGrid(InnerGridWidth, InnerGridHeight);
            Population = DevelopmentPopulation;
            GroundBuildRadius = DevelopmentGroundBuildRadius;
            ConstructionMultiplier = 1f;
            contactedRoutes.Clear();
            instances = new List<GrayboxBuildingInstance3D>();
            evacuationLocks.Clear();
            evacuationSnapshots.Clear();
            readOnlyInstances =
                new ReadOnlyCollection<GrayboxBuildingInstance3D>(instances);
            nextStableInstanceOrdinal = 1;
            AdvanceCatalogRevision();
            AdvancePlacementRevision();
        }

        private void OnDestroy()
        {
            if (Research != null)
                Research.Completed -= HandleResearchCompleted;
        }

        private void HandleResearchCompleted(ResearchDefinition definition)
        {
            if (definition == null) return;
            AdvanceCatalogRevision();
            AdvancePlacementRevision();
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
            if (refreshed.Definition.RequiresResourceNode &&
                !evaluation.CompatibleResourceNode.IsValid)
                return false;

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
                new ConstructionProgress(refreshed.Definition.BuildSeconds),
                evaluation.CompatibleResourceNode);
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
            AdvancePlacementRevision();
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
            if (instance.State != GrayboxBuildingInstanceState.UnderConstruction ||
                instance.IsEvacuationLocked)
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
            AdvancePlacementRevision();
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
                    instance.IsEvacuationLocked ||
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
                if (completed)
                {
                    AdvanceCatalogRevision();
                    AdvancePlacementRevision();
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

        public bool HasPlayerOwnedGroundInstances
        {
            get
            {
                EnsureConfigured();
                for (var index = 0; index < instances.Count; index++)
                    if (instances[index].IsPlayerOwned &&
                        instances[index].Placement.Site == BuildingSite.Ground)
                        return true;
                return false;
            }
        }

        public void CopyPlayerOwnedGroundInstances(
            List<GrayboxBuildingInstance3D> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            EnsureConfigured();
            destination.Clear();
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance.IsPlayerOwned &&
                    instance.Placement.Site == BuildingSite.Ground)
                    destination.Add(instance);
            }
            destination.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
        }

        public bool TryLockEvacuationWork(
            IReadOnlyList<BuildingEvacuationWork> fullDismantleWork,
            out string failureReason)
        {
            EnsureConfigured();
            failureReason = string.Empty;
            if (fullDismantleWork == null || fullDismantleWork.Count == 0)
            {
                failureReason = "完整拆除队列为空";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < fullDismantleWork.Count; index++)
            {
                BuildingEvacuationWork work = fullDismantleWork[index];
                int instanceIndex = FindInstanceIndex(work.StableInstanceId);
                if (work.Treatment != BuildingEvacuationTreatment.FullDismantle ||
                    !seen.Add(work.StableInstanceId) || instanceIndex < 0 ||
                    !IsEligibleGroundInstance(instances[instanceIndex]) ||
                    instances[instanceIndex].IsEvacuationLocked ||
                    evacuationLocks.ContainsKey(work.StableInstanceId) ||
                    !evacuationSnapshots.TryGetValue(
                        work.StableInstanceId,
                        out BuildingEvacuationWork captured) ||
                    !captured.Equals(work))
                {
                    failureReason = "完整拆除项目无效";
                    return false;
                }
            }

            var changedCompleted = new List<GrayboxBuildingInstance3D>();
            try
            {
                for (var index = 0; index < fullDismantleWork.Count; index++)
                {
                    BuildingEvacuationWork work = fullDismantleWork[index];
                    GrayboxBuildingInstance3D instance =
                        instances[FindInstanceIndex(work.StableInstanceId)];
                    bool countedBefore = IsCountedCompleted(instance);
                    evacuationLocks.Add(work.StableInstanceId, work);
                    instance.SetEvacuationLocked(true);
                    if (countedBefore) changedCompleted.Add(instance);
                }
            }
            catch
            {
                for (var index = 0; index < fullDismantleWork.Count; index++)
                {
                    BuildingEvacuationWork work = fullDismantleWork[index];
                    if (!evacuationLocks.Remove(work.StableInstanceId)) continue;
                    int instanceIndex = FindInstanceIndex(work.StableInstanceId);
                    if (instanceIndex >= 0)
                        instances[instanceIndex].SetEvacuationLocked(false);
                }
                throw;
            }
            for (var index = 0; index < changedCompleted.Count; index++)
                AdvanceCatalogRevision();
            AdvancePlacementRevision();
            return true;
        }

        public bool TryCaptureEvacuationWork(
            IReadOnlyList<BuildingEvacuationWork> evacuationWork,
            out string failureReason)
        {
            EnsureConfigured();
            failureReason = string.Empty;
            if (evacuationWork == null || evacuationWork.Count == 0)
            {
                failureReason = "撤离快照为空";
                return false;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < evacuationWork.Count; index++)
            {
                BuildingEvacuationWork work = evacuationWork[index];
                int instanceIndex = FindInstanceIndex(work.StableInstanceId);
                GrayboxBuildingInstance3D instance = instanceIndex < 0
                    ? null
                    : instances[instanceIndex];
                BuildingEvacuationWork expected = instance == null
                    ? default(BuildingEvacuationWork)
                    : BuildingEvacuationRules.Create(
                        instance.StableInstanceId,
                        instance.Placement.Definition.Cost,
                        instance.Progress.BaseDuration,
                        EvacuationRemainingRatio(instance),
                        work.Treatment);
                if (work.Treatment == BuildingEvacuationTreatment.Unassigned ||
                    !Enum.IsDefined(typeof(BuildingEvacuationTreatment),
                        work.Treatment) ||
                    !seen.Add(work.StableInstanceId) || instance == null ||
                    !IsEligibleGroundInstance(instance) ||
                    instance.IsEvacuationLocked ||
                    evacuationSnapshots.ContainsKey(work.StableInstanceId) ||
                    !expected.Equals(work))
                {
                    failureReason = "撤离快照无效";
                    return false;
                }
            }
            for (var index = 0; index < evacuationWork.Count; index++)
                evacuationSnapshots.Add(
                    evacuationWork[index].StableInstanceId,
                    evacuationWork[index]);
            return true;
        }

        public void RollbackEvacuationLocksAfterFailure(
            IReadOnlyList<BuildingEvacuationWork> fullDismantleWork)
        {
            if (fullDismantleWork == null) return;
            EnsureConfigured();
            var placementChanged = false;
            for (var index = 0; index < fullDismantleWork.Count; index++)
            {
                BuildingEvacuationWork work = fullDismantleWork[index];
                if (!evacuationSnapshots.TryGetValue(
                        work.StableInstanceId,
                        out BuildingEvacuationWork captured) ||
                    !captured.Equals(work))
                    continue;
                evacuationSnapshots.Remove(work.StableInstanceId);
                bool wasLocked = evacuationLocks.Remove(work.StableInstanceId);
                int instanceIndex = FindInstanceIndex(work.StableInstanceId);
                if (instanceIndex < 0 || !wasLocked) continue;
                GrayboxBuildingInstance3D instance = instances[instanceIndex];
                bool countedAfter = instance.State ==
                    GrayboxBuildingInstanceState.Completed &&
                    instance.IsPlayerOwned;
                instance.SetEvacuationLocked(false);
                placementChanged = true;
                if (countedAfter) AdvanceCatalogRevision();
            }
            if (placementChanged) AdvancePlacementRevision();
        }

        public bool TryCommitEvacuation(
            in BuildingEvacuationWork work,
            IGrayboxBuildingPresentation3D presentation,
            out int acceptedRefund,
            out string failureReason)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            EnsureConfigured();
            acceptedRefund = 0;
            failureReason = string.Empty;
            int instanceIndex = FindInstanceIndex(work.StableInstanceId);
            if (instanceIndex < 0 ||
                work.Treatment == BuildingEvacuationTreatment.Unassigned ||
                !IsEligibleGroundInstance(instances[instanceIndex]))
            {
                failureReason = "撤离项目无效";
                return false;
            }

            GrayboxBuildingInstance3D instance = instances[instanceIndex];
            if (!Enum.IsDefined(typeof(BuildingEvacuationTreatment),
                    work.Treatment) ||
                !evacuationSnapshots.TryGetValue(
                    work.StableInstanceId,
                    out BuildingEvacuationWork snapshot) ||
                !snapshot.Equals(work))
            {
                failureReason = "撤离快照不匹配";
                return false;
            }
            if (work.Treatment == BuildingEvacuationTreatment.FullDismantle &&
                (!evacuationLocks.TryGetValue(
                    work.StableInstanceId,
                    out BuildingEvacuationWork captured) ||
                 !captured.Equals(work)))
            {
                failureReason = "完整拆除快照不匹配";
                return false;
            }
            if (instance.IsEvacuationLocked &&
                work.Treatment != BuildingEvacuationTreatment.FullDismantle)
            {
                failureReason = "撤离项目已锁定";
                return false;
            }
            if (work.Treatment == BuildingEvacuationTreatment.Abandon)
                return TryAbandon(instance, presentation, out failureReason);
            return TryRemoveEvacuatedInstance(
                instanceIndex,
                work,
                presentation,
                out acceptedRefund,
                out failureReason);
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
            bool changed = contacted
                ? contactedRoutes.Add(route)
                : contactedRoutes.Remove(route);
            if (changed)
            {
                AdvanceCatalogRevision();
                AdvancePlacementRevision();
            }
        }

        public void UnlockResearchForDevelopment(string researchId)
        {
            EnsureConfigured();
            ResearchDefinition definition = ResearchCatalog.Find(researchId);
            if (definition == null || Research.IsCompleted(definition.Id)) return;

            Research.GrantCompletedForDevelopment(definition);
            AdvanceCatalogRevision();
            AdvancePlacementRevision();
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
            for (var index = 0; index < ResearchCatalog.All.Length; index++)
                UnlockResearchForDevelopment(
                    ResearchCatalog.All[index].Id.Value);
        }

        public void SetPopulationForDevelopment(int value)
        {
            EnsureConfigured();
            int population = Math.Max(0, value);
            if (Population == population) return;
            Population = population;
            AdvanceCatalogRevision();
            AdvancePlacementRevision();
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
                if (instance.State != GrayboxBuildingInstanceState.UnderConstruction ||
                    instance.IsEvacuationLocked)
                    continue;
                float remainingBefore = instance.Progress.Remaining;
                instance.Progress.Restore(0f);
                instance.Complete();
                try
                {
                    presentation.UpdateInstance(instance);
                }
                catch
                {
                    instance.RestoreConstruction(remainingBefore);
                    throw;
                }
                AdvanceCatalogRevision();
                AdvancePlacementRevision();
            }
        }

        private void AdvanceCatalogRevision()
        {
            unchecked { catalogRevision++; }
        }

        private void AdvancePlacementRevision()
        {
            unchecked { placementRevision++; }
        }

        private bool TryAbandon(
            GrayboxBuildingInstance3D instance,
            IGrayboxBuildingPresentation3D presentation,
            out string failureReason)
        {
            bool wasCounted = IsCountedCompleted(instance);
            bool playerOwned = instance.IsPlayerOwned;
            GrayboxBuildingInstanceState state = instance.State;
            instance.Abandon();
            try
            {
                presentation.UpdateInstance(instance);
            }
            catch (Exception operationFailure)
            {
                instance.RestoreEvacuationState(playerOwned, state);
                try
                {
                    presentation.UpdateInstance(instance);
                }
                catch (Exception restoreFailure)
                {
                    throw new InvalidOperationException(
                        "Failed to restore presentation after evacuation failure.",
                        new AggregateException(operationFailure, restoreFailure));
                }
                throw;
            }
            if (wasCounted) AdvanceCatalogRevision();
            AdvancePlacementRevision();
            evacuationSnapshots.Remove(instance.StableInstanceId);
            failureReason = string.Empty;
            return true;
        }

        private bool TryRemoveEvacuatedInstance(
            int instanceIndex,
            in BuildingEvacuationWork work,
            IGrayboxBuildingPresentation3D presentation,
            out int acceptedRefund,
            out string failureReason)
        {
            GrayboxBuildingInstance3D instance = instances[instanceIndex];
            BuildingGrid grid = instance.Placement.Site == BuildingSite.InnerCity
                ? InnerGrid
                : GroundGrid;
            int inventoryBefore = Inventory.Get(instance.Placement.Definition.CostId);
            bool wasCounted = IsCountedCompleted(instance);
            acceptedRefund = 0;
            failureReason = string.Empty;
            try { presentation.Remove(instance); }
            catch (Exception removeFailure)
            {
                Exception restoreFailure = TryRestorePresentation(
                    presentation,
                    instance);
                if (restoreFailure != null)
                    throw new InvalidOperationException(
                        "Failed to restore presentation after evacuation failure.",
                        new AggregateException(removeFailure, restoreFailure));
                throw;
            }
            if (!grid.Remove(instance.Placement))
            {
                Exception restoreFailure = TryRestorePresentation(presentation, instance);
                if (restoreFailure != null)
                    throw new InvalidOperationException(
                        "Failed to restore presentation after evacuation grid failure.",
                        restoreFailure);
                failureReason = "撤离占格移除失败";
                return false;
            }

            try
            {
                acceptedRefund = Inventory.Add(
                    instance.Placement.Definition.CostId,
                    work.Refund);
                instance.SetEvacuationLocked(false);
                instances.RemoveAt(instanceIndex);
                evacuationLocks.Remove(work.StableInstanceId);
                evacuationSnapshots.Remove(work.StableInstanceId);
            }
            catch
            {
                Inventory.Restore(instance.Placement.Definition.CostId, inventoryBefore);
                grid.TryRestore(
                    instance.Placement.Definition,
                    instance.Placement.X,
                    instance.Placement.Y,
                    out _,
                    instance.Placement.Site,
                    instance.Placement.Orientation);
                Exception restoreFailure = TryRestorePresentation(presentation, instance);
                if (restoreFailure != null)
                    throw new InvalidOperationException(
                        "Failed to restore presentation after evacuation failure.",
                        restoreFailure);
                throw;
            }
            if (wasCounted) AdvanceCatalogRevision();
            AdvancePlacementRevision();
            return true;
        }

        private static bool IsEligibleGroundInstance(
            GrayboxBuildingInstance3D instance)
        {
            return instance != null &&
                   instance.IsPlayerOwned &&
                   instance.Placement.Site == BuildingSite.Ground;
        }

        private static bool IsCountedCompleted(
            GrayboxBuildingInstance3D instance)
        {
            return instance != null &&
                   instance.State == GrayboxBuildingInstanceState.Completed &&
                   instance.IsPlayerOwned &&
                   !instance.IsEvacuationLocked;
        }

        private static double EvacuationRemainingRatio(
            GrayboxBuildingInstance3D instance)
        {
            return instance.State == GrayboxBuildingInstanceState.Completed
                ? 1d
                : instance.Progress.Remaining /
                  instance.Progress.BaseDuration;
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
                request.CompatibleResourceNode,
                request.RequiresValidResourceNodeBinding,
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
