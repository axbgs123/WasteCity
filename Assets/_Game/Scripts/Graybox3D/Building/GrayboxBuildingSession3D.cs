using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Population;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    public enum GrayboxBuildingInstanceState
    {
        UnderConstruction,
        Completed,
        AbandonedRuin
    }

    public enum GrayboxEvacuationCommitCode3D
    {
        Completed,
        CapacityInsufficient,
        Invalid
    }

    public readonly struct GrayboxBuildingRestoreEntry3D
    {
        public GrayboxBuildingRestoreEntry3D(
            string stableInstanceId,
            BuildingDefinition definition,
            BuildingSite site,
            int x,
            int y,
            BuildingOrientation orientation,
            GrayboxBuildingInstanceState state,
            float constructionRemainingSeconds,
            bool isPlayerOwned,
            bool isEvacuationLocked,
            ResourceNodeBinding boundResourceNode)
        {
            StableInstanceId = stableInstanceId;
            Definition = definition;
            Site = site;
            X = x;
            Y = y;
            Orientation = orientation;
            State = state;
            ConstructionRemainingSeconds = constructionRemainingSeconds;
            IsPlayerOwned = isPlayerOwned;
            IsEvacuationLocked = isEvacuationLocked;
            BoundResourceNode = boundResourceNode;
        }

        public string StableInstanceId { get; }
        public BuildingDefinition Definition { get; }
        public BuildingSite Site { get; }
        public int X { get; }
        public int Y { get; }
        public BuildingOrientation Orientation { get; }
        public GrayboxBuildingInstanceState State { get; }
        public float ConstructionRemainingSeconds { get; }
        public bool IsPlayerOwned { get; }
        public bool IsEvacuationLocked { get; }
        public ResourceNodeBinding BoundResourceNode { get; }
    }

    public sealed class GrayboxBuildingEvacuationRestorePlan3D
    {
        internal GrayboxBuildingEvacuationRestorePlan3D(
            GrayboxBuildingSession3D owner,
            uint catalogRevision,
            uint placementRevision,
            ulong storageRevision,
            BuildingEvacuationWork[] pendingWork,
            GrayboxBuildingInstance3D[] instances,
            bool[] originalLocks)
        {
            Owner = owner;
            CatalogRevision = catalogRevision;
            PlacementRevision = placementRevision;
            StorageRevision = storageRevision;
            PendingWork = pendingWork;
            Instances = instances;
            OriginalLocks = originalLocks;
        }

        internal GrayboxBuildingSession3D Owner { get; }
        internal uint CatalogRevision { get; }
        internal uint PlacementRevision { get; }
        internal ulong StorageRevision { get; }
        internal BuildingEvacuationWork[] PendingWork { get; }
        internal GrayboxBuildingInstance3D[] Instances { get; }
        internal bool[] OriginalLocks { get; }
        internal bool Consumed { get; set; }
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
        IGrayboxBuildingCatalogContext3D,
        IGrayboxRuleTimeSource3D
    {
        public const int ResourceCapacity = 5000;
        private const int DevelopmentPopulation = 200;
        private const int FormalPopulation = 100;
        private const int FormalPopulationCapacity = 150;
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
        private readonly Dictionary<string, bool> evacuationWarehouseConnectivity =
            new Dictionary<string, bool>(StringComparer.Ordinal);
        private IReadOnlyList<GrayboxBuildingInstance3D> readOnlyInstances;
        private int nextStableInstanceOrdinal;
        private uint catalogRevision;
        private uint placementRevision;
        private PopulationModel population;

        public bool DevelopmentFixtureEnabled => developmentFixtureEnabled;
        public ResourceInventory Inventory { get; private set; }
        public CityResourceStorageModel CityStorage { get; private set; }
        public ResearchModel Research { get; private set; }
        public BuildingGrid GroundGrid { get; private set; }
        public BuildingGrid InnerGrid { get; private set; }
        public int Population => population?.Current ?? FormalPopulation;
        public int PopulationCapacity =>
            population?.Capacity ?? FormalPopulationCapacity;
        public float ProductivityMultiplier =>
            population?.ProductivityMultiplier ?? 1f;
        public int GroundBuildRadius { get; private set; }
        public float ConstructionMultiplier => DevelopmentRuleTimeMultiplier;
        public float DevelopmentRuleTimeMultiplier { get; private set; }
        public GrayboxRuleTimeContext3D RuleTimeContext =>
            new GrayboxRuleTimeContext3D(
                ProductivityMultiplier,
                DevelopmentRuleTimeMultiplier);
        public uint CatalogRevision => catalogRevision;
        public uint PlacementRevision => placementRevision;
        public int NextStableInstanceOrdinal => nextStableInstanceOrdinal;
        public IReadOnlyList<GrayboxBuildingInstance3D> Instances =>
            readOnlyInstances;

        public int GetCityResourceAmount(string resourceId)
        {
            return CityStorage == null
                ? 0
                : CityStorage.GetNetworkAmount(resourceId);
        }

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
            ConfigureSession(
                inventory,
                ResourceCapacity,
                DevelopmentPopulation,
                FormalPopulationCapacity);
        }

        public void ConfigureFormalSession()
        {
            ConfigureSession(
                ResourceDefinitionCatalog.CreateFormalCityInventory(),
                ResourceCapacityPolicy.FormalBaseCapacityPerResource,
                FormalPopulation,
                FormalPopulationCapacity);
        }

        private void ConfigureSession(
            ResourceInventory inventory,
            int coreCapacityPerResource,
            int initialPopulation,
            int initialPopulationCapacity)
        {
            if (Research != null)
                Research.Completed -= HandleResearchCompleted;
            Inventory = inventory ??
                throw new ArgumentNullException(nameof(inventory));
            CityStorage?.Dispose();
            CityStorage = new CityResourceStorageModel(
                Inventory,
                coreCapacityPerResource);
            Research = new ResearchModel();
            Research.Completed += HandleResearchCompleted;
            GroundGrid = new BuildingGrid(
                GrayboxWorldLayout3D.WorldWidth,
                GrayboxWorldLayout3D.WorldHeight);
            InnerGrid = new BuildingGrid(InnerGridWidth, InnerGridHeight);
            population = new PopulationModel(
                initialPopulation,
                initialPopulationCapacity);
            GroundBuildRadius = DevelopmentGroundBuildRadius;
            DevelopmentRuleTimeMultiplier = 1f;
            contactedRoutes.Clear();
            instances = new List<GrayboxBuildingInstance3D>();
            evacuationLocks.Clear();
            evacuationSnapshots.Clear();
            evacuationWarehouseConnectivity.Clear();
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
            CityStorage?.Dispose();
            CityStorage = null;
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

            if (!CityStorage.TrySpendFromNetwork(
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
                CityStorage.AddToNetwork(
                    refreshed.Definition.CostId,
                    refreshed.Definition.Cost);
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
                    refreshed.Definition.Cost);
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
                    refreshed.Definition.Cost);
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

            acceptedRefund = CityStorage.AddToNetwork(
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
                    RuleTimeContext.EffectiveMultiplier);
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
                    RegisterCompletedWarehouse(instance);
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

        public bool TryPrepareEvacuationRestore(
            IReadOnlyList<BuildingEvacuationWork> work,
            IReadOnlyList<string> lockedIds,
            IReadOnlyList<string> pendingRollbackIds,
            out GrayboxBuildingEvacuationRestorePlan3D plan,
            out string error)
        {
            EnsureConfigured();
            plan = null;
            error = string.Empty;
            if (work == null || lockedIds == null ||
                pendingRollbackIds == null)
            {
                error = "撤离恢复集合不能为空";
                return false;
            }
            if (evacuationSnapshots.Count != 0 ||
                evacuationLocks.Count != 0 ||
                evacuationWarehouseConnectivity.Count != 0)
            {
                error = "建筑会话已有活动撤离事务";
                return false;
            }

            var workById = new Dictionary<string, BuildingEvacuationWork>(
                StringComparer.Ordinal);
            for (var index = 0; index < work.Count; index++)
            {
                BuildingEvacuationWork item = work[index];
                if (string.IsNullOrWhiteSpace(item.StableInstanceId) ||
                    item.Treatment == BuildingEvacuationTreatment.Unassigned ||
                    !Enum.IsDefined(
                        typeof(BuildingEvacuationTreatment),
                        item.Treatment) ||
                    !workById.TryAdd(item.StableInstanceId, item))
                {
                    error = "冻结撤离项目为空、重复或无效";
                    return false;
                }
            }

            var locked = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < lockedIds.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(lockedIds[index]) ||
                    !locked.Add(lockedIds[index]) ||
                    !workById.ContainsKey(lockedIds[index]))
                {
                    error = "撤离锁引用为空、重复或缺少冻结项目";
                    return false;
                }
            }
            for (var index = 0; index < instances.Count; index++)
            {
                GrayboxBuildingInstance3D instance = instances[index];
                if (instance.IsEvacuationLocked &&
                    !locked.Contains(instance.StableInstanceId))
                {
                    error = "建筑会话包含未归属当前撤离批次的锁";
                    return false;
                }
            }

            var pending = new HashSet<string>(StringComparer.Ordinal);
            var pendingWork = new BuildingEvacuationWork[
                pendingRollbackIds.Count];
            var pendingInstances = new GrayboxBuildingInstance3D[
                pendingRollbackIds.Count];
            var originalLocks = new bool[pendingRollbackIds.Count];
            for (var index = 0; index < pendingRollbackIds.Count; index++)
            {
                string stableId = pendingRollbackIds[index];
                int instanceIndex = FindInstanceIndex(stableId);
                if (string.IsNullOrWhiteSpace(stableId) ||
                    !pending.Add(stableId) || !locked.Contains(stableId) ||
                    !workById.TryGetValue(
                        stableId,
                        out BuildingEvacuationWork item) ||
                    instanceIndex < 0 ||
                    !IsEligibleGroundInstance(instances[instanceIndex]))
                {
                    error = "待回滚撤离项目缺少建筑、锁或冻结项目";
                    return false;
                }
                pendingWork[index] = item;
                pendingInstances[index] = instances[instanceIndex];
                originalLocks[index] = instances[instanceIndex]
                    .IsEvacuationLocked;
            }
            if (locked.Count != pending.Count)
            {
                error = "撤离锁必须与待回滚项目完全一致";
                return false;
            }

            plan = new GrayboxBuildingEvacuationRestorePlan3D(
                this,
                catalogRevision,
                placementRevision,
                CityStorage?.Revision ?? 0ul,
                pendingWork,
                pendingInstances,
                originalLocks);
            return true;
        }

        public bool TryCommitEvacuationRestore(
            GrayboxBuildingEvacuationRestorePlan3D plan,
            out string error)
        {
            EnsureConfigured();
            error = string.Empty;
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "撤离恢复计划不属于当前建筑会话";
                return false;
            }
            if (plan.Consumed)
            {
                error = "撤离恢复计划已经使用";
                return false;
            }
            if (catalogRevision != plan.CatalogRevision ||
                placementRevision != plan.PlacementRevision ||
                (CityStorage?.Revision ?? 0ul) != plan.StorageRevision ||
                evacuationSnapshots.Count != 0 ||
                evacuationLocks.Count != 0 ||
                evacuationWarehouseConnectivity.Count != 0)
            {
                error = "建筑或仓储状态在撤离恢复前发生变化";
                return false;
            }
            for (var index = 0; index < plan.PendingWork.Length; index++)
            {
                GrayboxBuildingInstance3D instance = plan.Instances[index];
                int instanceIndex = FindInstanceIndex(
                    plan.PendingWork[index].StableInstanceId);
                if (instanceIndex < 0 ||
                    !ReferenceEquals(instances[instanceIndex], instance) ||
                    instance.IsEvacuationLocked != plan.OriginalLocks[index] ||
                    !IsEligibleGroundInstance(instance))
                {
                    error = "待恢复撤离建筑已经变化";
                    return false;
                }
            }

            var applied = 0;
            var changedLockCount = 0;
            try
            {
                for (var index = 0; index < plan.PendingWork.Length; index++)
                {
                    BuildingEvacuationWork item = plan.PendingWork[index];
                    GrayboxBuildingInstance3D instance = plan.Instances[index];
                    bool countedBefore = IsCountedCompleted(instance);
                    evacuationSnapshots.Add(item.StableInstanceId, item);
                    evacuationLocks.Add(item.StableInstanceId, item);
                    if (!instance.IsEvacuationLocked)
                    {
                        instance.SetEvacuationLocked(true);
                        if (countedBefore) changedLockCount++;
                    }
                    applied++;
                    if (CityStorage != null &&
                        CityStorage.TryGetWarehouseSnapshot(
                            item.StableInstanceId,
                            out WarehouseStorageSnapshot warehouse))
                    {
                        evacuationWarehouseConnectivity.Add(
                            item.StableInstanceId,
                            warehouse.IsConnected);
                        if (!CityStorage.TrySetWarehouseConnected(
                                item.StableInstanceId,
                                connected: false))
                        {
                            throw new InvalidOperationException(
                                "无法恢复撤离仓库断连状态");
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                for (var index = applied - 1; index >= 0; index--)
                {
                    string stableId = plan.PendingWork[index].StableInstanceId;
                    evacuationSnapshots.Remove(stableId);
                    evacuationLocks.Remove(stableId);
                    plan.Instances[index].SetEvacuationLocked(
                        plan.OriginalLocks[index]);
                    RestoreWarehouseConnectivity(stableId);
                }
                error = exception.Message;
                return false;
            }

            for (var index = 0; index < changedLockCount; index++)
                AdvanceCatalogRevision();
            if (plan.PendingWork.Length > 0) AdvancePlacementRevision();
            plan.Consumed = true;
            return true;
        }

        public bool TryLockEvacuationWork(
            IReadOnlyList<BuildingEvacuationWork> evacuationWork,
            out string failureReason)
        {
            EnsureConfigured();
            failureReason = string.Empty;
            if (evacuationWork == null || evacuationWork.Count == 0)
            {
                failureReason = "撤离锁定队列为空";
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < evacuationWork.Count; index++)
            {
                BuildingEvacuationWork work = evacuationWork[index];
                int instanceIndex = FindInstanceIndex(work.StableInstanceId);
                if (work.Treatment == BuildingEvacuationTreatment.Unassigned ||
                    !Enum.IsDefined(typeof(BuildingEvacuationTreatment),
                        work.Treatment) ||
                    !seen.Add(work.StableInstanceId) || instanceIndex < 0 ||
                    !IsEligibleGroundInstance(instances[instanceIndex]) ||
                    instances[instanceIndex].IsEvacuationLocked ||
                    evacuationLocks.ContainsKey(work.StableInstanceId) ||
                    !evacuationSnapshots.TryGetValue(
                        work.StableInstanceId,
                        out BuildingEvacuationWork captured) ||
                    !captured.Equals(work))
                {
                    failureReason = "撤离锁定项目无效";
                    return false;
                }
            }

            var changedCompleted = new List<GrayboxBuildingInstance3D>();
            try
            {
                for (var index = 0; index < evacuationWork.Count; index++)
                {
                    BuildingEvacuationWork work = evacuationWork[index];
                    GrayboxBuildingInstance3D instance =
                        instances[FindInstanceIndex(work.StableInstanceId)];
                    bool countedBefore = IsCountedCompleted(instance);
                    evacuationLocks.Add(work.StableInstanceId, work);
                    instance.SetEvacuationLocked(true);
                    if (CityStorage != null &&
                        CityStorage.TryGetWarehouseSnapshot(
                            instance.StableInstanceId,
                            out WarehouseStorageSnapshot warehouse))
                    {
                        evacuationWarehouseConnectivity.Add(
                            instance.StableInstanceId,
                            warehouse.IsConnected);
                        CityStorage.TrySetWarehouseConnected(
                            instance.StableInstanceId,
                            connected: false);
                    }
                    if (countedBefore) changedCompleted.Add(instance);
                }
            }
            catch
            {
                for (var index = 0; index < evacuationWork.Count; index++)
                {
                    BuildingEvacuationWork work = evacuationWork[index];
                    if (!evacuationLocks.Remove(work.StableInstanceId)) continue;
                    int instanceIndex = FindInstanceIndex(work.StableInstanceId);
                    if (instanceIndex >= 0)
                        instances[instanceIndex].SetEvacuationLocked(false);
                    RestoreWarehouseConnectivity(work.StableInstanceId);
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
                        work.Treatment,
                        work.BatchContext);
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
            IReadOnlyList<BuildingEvacuationWork> evacuationWork)
        {
            if (evacuationWork == null) return;
            EnsureConfigured();
            var placementChanged = false;
            for (var index = 0; index < evacuationWork.Count; index++)
            {
                BuildingEvacuationWork work = evacuationWork[index];
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
                RestoreWarehouseConnectivity(work.StableInstanceId);
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
            return TryCommitEvacuationWithPayload(
                work,
                Array.Empty<ResourceAmount>(),
                presentation,
                out acceptedRefund,
                out failureReason);
        }

        public bool TryCommitEvacuationWithPayload(
            BuildingEvacuationWork work,
            IReadOnlyList<ResourceAmount> additionalPayload,
            IGrayboxBuildingPresentation3D presentation,
            out int acceptedRefund,
            out string failureReason)
        {
            return TryCommitEvacuationWithPayload(
                work,
                additionalPayload,
                presentation,
                out acceptedRefund,
                out failureReason,
                out _);
        }

        public bool TryCommitEvacuationWithPayload(
            BuildingEvacuationWork work,
            IReadOnlyList<ResourceAmount> additionalPayload,
            IGrayboxBuildingPresentation3D presentation,
            out int acceptedRefund,
            out string failureReason,
            out GrayboxEvacuationCommitCode3D commitCode)
        {
            if (presentation == null)
                throw new ArgumentNullException(nameof(presentation));
            EnsureConfigured();
            acceptedRefund = 0;
            failureReason = string.Empty;
            commitCode = GrayboxEvacuationCommitCode3D.Invalid;
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
            bool hasLock = evacuationLocks.TryGetValue(
                work.StableInstanceId,
                out BuildingEvacuationWork captured);
            bool hasMatchingLock = instance.IsEvacuationLocked &&
                hasLock && captured.Equals(work);
            bool requiresLock = work.Treatment ==
                BuildingEvacuationTreatment.FullDismantle;
            if (requiresLock && !hasMatchingLock)
            {
                failureReason = "完整拆除快照不匹配";
                return false;
            }
            if (!requiresLock &&
                (instance.IsEvacuationLocked || hasLock) &&
                !hasMatchingLock)
            {
                failureReason = "撤离锁定快照不匹配";
                return false;
            }
            if (!HasExpectedPlacementFootprint(instance))
            {
                failureReason = "撤离占格状态无效";
                return false;
            }

            CityResourceEvacuationPlan storagePlan =
                CreateEvacuationStoragePlan(
                    instance,
                    work,
                    additionalPayload);
            if (storagePlan != null && !storagePlan.CanCommit)
            {
                if (storagePlan.IsValid)
                {
                    commitCode =
                        GrayboxEvacuationCommitCode3D.CapacityInsufficient;
                    failureReason = EvacuationCapacityFailure(storagePlan);
                }
                else
                {
                    failureReason = "撤离资源载荷无效";
                }
                return false;
            }
            if (work.Treatment == BuildingEvacuationTreatment.Abandon)
                return TryAbandon(
                    instance,
                    presentation,
                    storagePlan,
                    out failureReason,
                    out commitCode);
            return TryRemoveEvacuatedInstance(
                instanceIndex,
                work,
                presentation,
                storagePlan,
                out acceptedRefund,
                out failureReason,
                out commitCode);
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
            int nextPopulation = Math.Max(0, value);
            if (Population == nextPopulation) return;
            population.Restore(nextPopulation, PopulationCapacity);
            AdvanceCatalogRevision();
            AdvancePlacementRevision();
        }

        public bool TryRestorePopulation(
            int current,
            int capacity,
            out string error)
        {
            if (!CanRestorePopulation(out error))
                return false;
            if (current < 0 || capacity < 0)
            {
                error = "人口和人口容量不能为负数";
                return false;
            }
            if (Population == current && PopulationCapacity == capacity)
            {
                error = string.Empty;
                return true;
            }

            population.Restore(current, capacity);
            AdvanceCatalogRevision();
            AdvancePlacementRevision();
            error = string.Empty;
            return true;
        }

        public bool CanRestorePopulation(out string error)
        {
            if (Inventory == null || Research == null ||
                GroundGrid == null || InnerGrid == null ||
                instances == null || CityStorage == null ||
                population == null)
            {
                error = "建筑会话尚未完成正式初始化";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public bool CanRestoreBuildings(out string error)
        {
            if (Inventory == null || Research == null ||
                GroundGrid == null || InnerGrid == null ||
                instances == null || CityStorage == null)
            {
                error = "建筑会话尚未完成正式初始化";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public bool TryRestoreBuildings(
            IReadOnlyList<GrayboxBuildingRestoreEntry3D> entries,
            int restoredNextStableInstanceOrdinal,
            IGrayboxBuildingPresentation3D presentation,
            out string error)
        {
            if (!CanRestoreBuildings(out error)) return false;
            if (entries == null)
            {
                error = "建筑恢复数据不能为空";
                return false;
            }
            if (presentation == null)
            {
                error = "建筑表现不能为空";
                return false;
            }
            if (restoredNextStableInstanceOrdinal <= 0 ||
                restoredNextStableInstanceOrdinal > 999999)
            {
                error = "建筑稳定实例高水位无效";
                return false;
            }

            var restoredGroundGrid = new BuildingGrid(
                GrayboxWorldLayout3D.WorldWidth,
                GrayboxWorldLayout3D.WorldHeight);
            var restoredInnerGrid = new BuildingGrid(
                InnerGridWidth,
                InnerGridHeight);
            var restoredInstances =
                new List<GrayboxBuildingInstance3D>(entries.Count);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var maximumOrdinal = 0;

            for (var index = 0; index < entries.Count; index++)
            {
                GrayboxBuildingRestoreEntry3D entry = entries[index];
                if (!TryParseStableInstanceOrdinal(
                        entry.StableInstanceId,
                        out int ordinal) ||
                    !stableIds.Add(entry.StableInstanceId))
                {
                    error = "建筑稳定实例 ID 为空、重复或格式无效";
                    return false;
                }
                maximumOrdinal = Math.Max(maximumOrdinal, ordinal);
                if (entry.Definition == null)
                {
                    error = "建筑定义不能为空";
                    return false;
                }
                if (!Enum.IsDefined(typeof(BuildingSite), entry.Site) ||
                    !Enum.IsDefined(
                        typeof(BuildingOrientation),
                        entry.Orientation) ||
                    !Enum.IsDefined(
                        typeof(GrayboxBuildingInstanceState),
                        entry.State))
                {
                    error = "建筑站点、方向或状态无效";
                    return false;
                }
                float remaining = entry.ConstructionRemainingSeconds;
                if (float.IsNaN(remaining) || float.IsInfinity(remaining) ||
                    remaining < 0f ||
                    remaining > entry.Definition.BuildSeconds)
                {
                    error = "建筑施工剩余时间无效";
                    return false;
                }
                if ((entry.State ==
                         GrayboxBuildingInstanceState.UnderConstruction &&
                     remaining <= 0f) ||
                    (entry.State == GrayboxBuildingInstanceState.Completed &&
                     remaining != 0f))
                {
                    error = "建筑状态与施工剩余时间不一致";
                    return false;
                }
                bool isRuin = entry.State ==
                    GrayboxBuildingInstanceState.AbandonedRuin;
                if (isRuin && entry.IsPlayerOwned)
                {
                    error = "建筑状态与玩家所有权不一致";
                    return false;
                }
                if (entry.IsEvacuationLocked &&
                    (!entry.IsPlayerOwned ||
                     entry.Site != BuildingSite.Ground || isRuin))
                {
                    error = "建筑撤离锁状态无效";
                    return false;
                }

                BuildingGrid restoredGrid =
                    entry.Site == BuildingSite.InnerCity
                        ? restoredInnerGrid
                        : restoredGroundGrid;
                if (!restoredGrid.TryRestore(
                        entry.Definition,
                        entry.X,
                        entry.Y,
                        out PlacedBuilding placement,
                        entry.Site,
                        entry.Orientation))
                {
                    error = "建筑占格越界、重叠或站点不兼容";
                    return false;
                }

                var progress = new ConstructionProgress(
                    entry.Definition.BuildSeconds);
                progress.Restore(remaining);
                var candidate = new GrayboxBuildingInstance3D(
                    entry.StableInstanceId,
                    placement,
                    progress,
                    entry.BoundResourceNode);
                candidate.RestoreEvacuationState(
                    entry.IsPlayerOwned,
                    entry.State);
                candidate.SetEvacuationLocked(entry.IsEvacuationLocked);
                restoredInstances.Add(candidate);
            }

            if (restoredNextStableInstanceOrdinal <= maximumOrdinal)
            {
                error = "建筑稳定实例高水位必须大于全部现有实例序号";
                return false;
            }

            restoredInstances.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
            if (!TryReplacePresentation(
                    instances,
                    restoredInstances,
                    presentation,
                    out error))
                return false;

            GroundGrid = restoredGroundGrid;
            InnerGrid = restoredInnerGrid;
            instances = restoredInstances;
            readOnlyInstances =
                new ReadOnlyCollection<GrayboxBuildingInstance3D>(instances);
            nextStableInstanceOrdinal = restoredNextStableInstanceOrdinal;
            evacuationLocks.Clear();
            evacuationSnapshots.Clear();
            evacuationWarehouseConnectivity.Clear();
            AdvanceCatalogRevision();
            AdvancePlacementRevision();
            error = string.Empty;
            return true;
        }

        public void SetConstructionMultiplierForDevelopment(float value)
        {
            EnsureConfigured();
            DevelopmentRuleTimeMultiplier = Math.Max(0f, value);
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
                RegisterCompletedWarehouse(instance);
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
            CityResourceEvacuationPlan storagePlan,
            out string failureReason,
            out GrayboxEvacuationCommitCode3D commitCode)
        {
            bool wasCounted = IsCountedCompleted(instance);
            if (!TryCommitEvacuationStorage(
                    storagePlan,
                    out failureReason,
                    out commitCode))
            {
                return false;
            }

            instance.SetEvacuationLocked(false);
            instance.Abandon();
            evacuationLocks.Remove(instance.StableInstanceId);
            if (wasCounted) AdvanceCatalogRevision();
            AdvancePlacementRevision();
            evacuationSnapshots.Remove(instance.StableInstanceId);
            evacuationWarehouseConnectivity.Remove(instance.StableInstanceId);
            failureReason = string.Empty;
            try
            {
                presentation.UpdateInstance(instance);
            }
            catch (Exception)
            {
                failureReason = "撤离已提交，但表现需重建";
            }
            return true;
        }

        private bool TryRemoveEvacuatedInstance(
            int instanceIndex,
            in BuildingEvacuationWork work,
            IGrayboxBuildingPresentation3D presentation,
            CityResourceEvacuationPlan storagePlan,
            out int acceptedRefund,
            out string failureReason,
            out GrayboxEvacuationCommitCode3D commitCode)
        {
            GrayboxBuildingInstance3D instance = instances[instanceIndex];
            BuildingGrid grid = instance.Placement.Site == BuildingSite.InnerCity
                ? InnerGrid
                : GroundGrid;
            bool wasCounted = IsCountedCompleted(instance);
            acceptedRefund = 0;
            failureReason = string.Empty;
            if (!TryCommitEvacuationStorage(
                    storagePlan,
                    out failureReason,
                    out commitCode))
            {
                return false;
            }
            if (!grid.Remove(instance.Placement))
                throw new InvalidOperationException(
                    "Evacuation grid invariant failed after storage commit.");

            acceptedRefund = work.Refund;
            instance.SetEvacuationLocked(false);
            instances.RemoveAt(instanceIndex);
            evacuationLocks.Remove(work.StableInstanceId);
            evacuationSnapshots.Remove(work.StableInstanceId);
            evacuationWarehouseConnectivity.Remove(work.StableInstanceId);
            if (wasCounted) AdvanceCatalogRevision();
            AdvancePlacementRevision();
            try
            {
                presentation.Remove(instance);
            }
            catch (Exception)
            {
                failureReason = "撤离已提交，但表现需重建";
            }
            return true;
        }

        private bool HasExpectedPlacementFootprint(
            GrayboxBuildingInstance3D instance)
        {
            if (instance?.Placement?.Definition == null) return false;
            BuildingGrid grid = instance.Placement.Site == BuildingSite.InnerCity
                ? InnerGrid
                : GroundGrid;
            int width = BuildingOrientationRules.Width(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            int height = BuildingOrientationRules.Height(
                instance.Placement.Definition,
                instance.Placement.Orientation);
            for (var offsetX = 0; offsetX < width; offsetX++)
            {
                for (var offsetY = 0; offsetY < height; offsetY++)
                {
                    if (!grid.IsOccupied(
                            instance.Placement.X + offsetX,
                            instance.Placement.Y + offsetY))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool IsEligibleGroundInstance(
            GrayboxBuildingInstance3D instance)
        {
            return instance != null &&
                   instance.IsPlayerOwned &&
                   instance.Placement.Site == BuildingSite.Ground;
        }

        private void RegisterCompletedWarehouse(
            GrayboxBuildingInstance3D instance)
        {
            if (CityStorage == null ||
                !IsWarehouse(instance) ||
                CityStorage.TryGetWarehouseSnapshot(
                    instance.StableInstanceId,
                    out _))
            {
                return;
            }
            CityStorage.TryRegisterWarehouse(
                instance.StableInstanceId,
                connected: false);
        }

        private CityResourceEvacuationPlan CreateEvacuationStoragePlan(
            GrayboxBuildingInstance3D instance,
            in BuildingEvacuationWork work,
            IReadOnlyList<ResourceAmount> additionalPayload)
        {
            bool removesWarehouse = IsWarehouse(instance) &&
                CityStorage.ContainsWarehouse(instance.StableInstanceId);
            bool includesRefund =
                work.Treatment != BuildingEvacuationTreatment.Abandon &&
                work.Refund > 0;
            int payloadCount = work.Treatment ==
                BuildingEvacuationTreatment.Abandon
                    ? 0
                    : additionalPayload?.Count ?? 0;
            if (!removesWarehouse && !includesRefund && payloadCount == 0)
                return null;

            var additions = new List<ResourceAmount>(
                payloadCount + (includesRefund ? 1 : 0));
            for (int index = 0; index < payloadCount; index++)
                additions.Add(additionalPayload[index]);
            if (includesRefund)
            {
                additions.Add(new ResourceAmount(
                    instance.Placement.Definition.CostId,
                    work.Refund));
            }
            return CityStorage.CreateEvacuationPlan(
                removesWarehouse ? instance.StableInstanceId : null,
                additions);
        }

        private bool TryCommitEvacuationStorage(
            CityResourceEvacuationPlan plan,
            out string failureReason,
            out GrayboxEvacuationCommitCode3D commitCode)
        {
            failureReason = string.Empty;
            commitCode = GrayboxEvacuationCommitCode3D.Invalid;
            if (plan == null)
            {
                commitCode = GrayboxEvacuationCommitCode3D.Completed;
                return true;
            }
            if (CityStorage.TryCommitEvacuationPlan(
                    plan,
                    out CityResourceEvacuationCommitStatus status))
            {
                commitCode = GrayboxEvacuationCommitCode3D.Completed;
                return true;
            }

            if (status ==
                CityResourceEvacuationCommitStatus.CapacityInsufficient)
            {
                commitCode =
                    GrayboxEvacuationCommitCode3D.CapacityInsufficient;
                failureReason = EvacuationCapacityFailure(plan);
            }
            else
            {
                failureReason = "撤离仓储计划失效：" + status;
            }
            return false;
        }

        private static string EvacuationCapacityFailure(
            CityResourceEvacuationPlan plan)
        {
            int total = plan?.TotalShortfall ?? 0;
            return "城市仓储容量不足，还缺 " + total + " 格";
        }

        private static bool IsWarehouse(
            GrayboxBuildingInstance3D instance)
        {
            return instance?.Placement?.Definition != null &&
                string.Equals(
                    instance.Placement.Definition.Id.Value,
                    BuildingCatalog.Warehouse.Id.Value,
                    StringComparison.Ordinal);
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
                CityStorage.CanSpendFromNetwork(
                    definition.CostId,
                    definition.Cost);
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
                InnerGrid == null || instances == null || CityStorage == null)
                ConfigureDevelopmentFixture();
        }

        private void RestoreWarehouseConnectivity(string stableInstanceId)
        {
            if (!evacuationWarehouseConnectivity.TryGetValue(
                    stableInstanceId,
                    out bool connected))
            {
                return;
            }
            evacuationWarehouseConnectivity.Remove(stableInstanceId);
            CityStorage?.TrySetWarehouseConnected(
                stableInstanceId,
                connected);
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

        private static bool TryParseStableInstanceOrdinal(
            string stableInstanceId,
            out int ordinal)
        {
            const string prefix = "building.instance.";
            ordinal = 0;
            if (string.IsNullOrEmpty(stableInstanceId) ||
                stableInstanceId.Length != prefix.Length + 6 ||
                !stableInstanceId.StartsWith(prefix, StringComparison.Ordinal))
                return false;
            for (var index = prefix.Length;
                 index < stableInstanceId.Length;
                 index++)
            {
                char digit = stableInstanceId[index];
                if (digit < '0' || digit > '9') return false;
                ordinal = ordinal * 10 + digit - '0';
            }
            return ordinal > 0;
        }

        private static bool TryReplacePresentation(
            IReadOnlyList<GrayboxBuildingInstance3D> currentInstances,
            IReadOnlyList<GrayboxBuildingInstance3D> restoredInstances,
            IGrayboxBuildingPresentation3D presentation,
            out string error)
        {
            try
            {
                for (var index = 0; index < currentInstances.Count; index++)
                    presentation.Remove(currentInstances[index]);
            }
            catch
            {
                RestorePresentationSet(currentInstances, presentation);
                error = "无法清理现有建筑表现";
                return false;
            }

            var attemptedRestoredCount = 0;
            try
            {
                for (var index = 0; index < restoredInstances.Count; index++)
                {
                    attemptedRestoredCount = index + 1;
                    if (!presentation.TryCreate(restoredInstances[index]))
                    {
                        CleanupPresentationSet(
                            restoredInstances,
                            attemptedRestoredCount,
                            presentation);
                        RestorePresentationSet(currentInstances, presentation);
                        error = "无法重建建筑表现";
                        return false;
                    }
                }
            }
            catch
            {
                CleanupPresentationSet(
                    restoredInstances,
                    attemptedRestoredCount,
                    presentation);
                RestorePresentationSet(currentInstances, presentation);
                error = "重建建筑表现时发生错误";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void CleanupPresentationSet(
            IReadOnlyList<GrayboxBuildingInstance3D> candidates,
            int count,
            IGrayboxBuildingPresentation3D presentation)
        {
            for (var index = Math.Min(count, candidates.Count) - 1;
                 index >= 0;
                 index--)
            {
                try
                {
                    presentation.Remove(candidates[index]);
                }
                catch
                {
                    // The authoritative model has not changed; the outer
                    // restore flow can rebuild derived presentation again.
                }
            }
        }

        private static void RestorePresentationSet(
            IReadOnlyList<GrayboxBuildingInstance3D> candidates,
            IGrayboxBuildingPresentation3D presentation)
        {
            for (var index = 0; index < candidates.Count; index++)
                TryRestorePresentation(presentation, candidates[index]);
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
            int spentCost)
        {
            grid.Remove(placement);
            CityStorage.AddToNetwork(costId, spentCost);
        }
    }
}
