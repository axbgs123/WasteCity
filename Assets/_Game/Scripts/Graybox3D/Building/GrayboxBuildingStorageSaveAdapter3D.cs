using System;
using System.Collections.Generic;
using WasteCity.Building;
using WasteCity.Content;
using WasteCity.Economy;
using WasteCity.Persistence.ThreeD;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxBuildingStorageSaveAdapter3D
    {
        public const string StorageConfigurationSignature =
            "core.storage.formal-3d.v1.core-150.warehouse-150";
        public const string PreservedOverCapacityConfigurationSignature =
            StorageConfigurationSignature + ".preserved-over-capacity";

        private readonly GrayboxBuildingSession3D session;
        private readonly IGrayboxBuildingPresentation3D presentation;
        private readonly Func<WorldMapModel> worldProvider;

        public GrayboxBuildingStorageSaveAdapter3D(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation)
            : this(session, presentation, null)
        {
        }

        public GrayboxBuildingStorageSaveAdapter3D(
            GrayboxBuildingSession3D session,
            IGrayboxBuildingPresentation3D presentation,
            Func<WorldMapModel> worldProvider)
        {
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.presentation = presentation ??
                throw new ArgumentNullException(nameof(presentation));
            this.worldProvider = worldProvider;
        }

        public FormalThreeDBuildingsSaveData CaptureBuildings()
        {
            var source = new List<GrayboxBuildingInstance3D>(
                session.Instances);
            source.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
            var instances = new FormalThreeDBuildingInstanceSaveData[
                source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                GrayboxBuildingInstance3D instance = source[index];
                ResourceNodeBinding binding = instance.BoundResourceNode;
                instances[index] = new FormalThreeDBuildingInstanceSaveData
                {
                    stableInstanceId = instance.StableInstanceId,
                    definitionId = instance.Placement.Definition.Id.Value,
                    site = (int)instance.Placement.Site,
                    x = instance.Placement.X,
                    y = instance.Placement.Y,
                    orientation = (int)instance.Placement.Orientation,
                    state = (int)instance.State,
                    constructionRemainingSeconds =
                        instance.Progress.Remaining,
                    isPlayerOwned = instance.IsPlayerOwned,
                    boundResourceNodeId = binding.IsValid
                        ? binding.StableId
                        : string.Empty,
                    boundNodeX = binding.IsValid ? binding.X : -1,
                    boundNodeY = binding.IsValid ? binding.Y : -1,
                    footprintWidth = instance.Placement.Definition.Width,
                    footprintHeight = instance.Placement.Definition.Height,
                    evacuationLockedCrossCheck =
                        instance.IsEvacuationLocked,
                };
            }

            return new FormalThreeDBuildingsSaveData
            {
                nextStableInstanceOrdinal =
                    session.NextStableInstanceOrdinal,
                instances = instances,
            };
        }

        public FormalThreeDStorageSaveData CaptureStorage()
        {
            CityResourceStorageSnapshot snapshot =
                session.CityStorage.CaptureSnapshot();
            string[] resourceIds = SortedResourceIds();
            var core = new List<FormalThreeDResourceAmountSaveData>();
            for (var index = 0; index < resourceIds.Length; index++)
            {
                int amount = snapshot.GetCoreAmount(resourceIds[index]);
                if (amount <= 0) continue;
                core.Add(new FormalThreeDResourceAmountSaveData
                {
                    resourceId = resourceIds[index],
                    amount = amount,
                });
            }

            var warehouseSnapshots = new List<WarehouseStorageSnapshot>(
                snapshot.Warehouses);
            warehouseSnapshots.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
            var warehouses = new FormalThreeDWarehouseSaveData[
                warehouseSnapshots.Count];
            for (var warehouseIndex = 0;
                 warehouseIndex < warehouseSnapshots.Count;
                 warehouseIndex++)
            {
                WarehouseStorageSnapshot warehouse =
                    warehouseSnapshots[warehouseIndex];
                var amounts = new List<FormalThreeDResourceAmountSaveData>();
                for (var resourceIndex = 0;
                     resourceIndex < resourceIds.Length;
                     resourceIndex++)
                {
                    int amount = warehouse.Get(resourceIds[resourceIndex]);
                    if (amount <= 0) continue;
                    amounts.Add(new FormalThreeDResourceAmountSaveData
                    {
                        resourceId = resourceIds[resourceIndex],
                        amount = amount,
                    });
                }
                warehouses[warehouseIndex] = new FormalThreeDWarehouseSaveData
                {
                    stableInstanceId = warehouse.StableInstanceId,
                    filterResourceId = warehouse.FilterResourceId,
                    amounts = amounts.ToArray(),
                };
            }

            CityStorageOrphanResource[] orphanSource =
                session.CityStorage.CaptureOrphanResources();
            Array.Sort(orphanSource, CompareOrphans);
            var orphans = new FormalThreeDOrphanResourceSaveData[
                orphanSource.Length];
            for (var index = 0; index < orphanSource.Length; index++)
            {
                CityStorageOrphanResource orphan = orphanSource[index];
                orphans[index] = new FormalThreeDOrphanResourceSaveData
                {
                    resourceId = orphan.ResourceId,
                    amount = orphan.Amount,
                    ownerKind = orphan.OwnerKind,
                    ownerStableId = orphan.OwnerStableId,
                };
            }

            return new FormalThreeDStorageSaveData
            {
                configurationSignature = HasOverCapacityWarehouse(
                        warehouseSnapshots)
                    ? PreservedOverCapacityConfigurationSignature
                    : StorageConfigurationSignature,
                coreAmounts = core.ToArray(),
                warehouses = warehouses,
                orphanResources = orphans,
            };
        }

        public bool TryRestore(
            FormalThreeDBuildingsSaveData buildings,
            FormalThreeDStorageSaveData storage,
            out string error)
        {
            if (buildings?.instances == null ||
                storage?.coreAmounts == null ||
                storage.warehouses == null ||
                storage.orphanResources == null)
            {
                error = "建筑或仓储存档数据不完整";
                return false;
            }
            if (string.IsNullOrWhiteSpace(storage.configurationSignature))
            {
                error = "仓储配置签名不能为空";
                return false;
            }
            if (!session.CanRestoreBuildings(out error)) return false;

            if (!TryBuildRestoreEntries(
                    buildings,
                    out List<GrayboxBuildingRestoreEntry3D> entries,
                    out Dictionary<string, BuildingIdentity> identities,
                    out error))
                return false;
            if (!TryBuildStorageRestore(
                    storage,
                    identities,
                    out List<ResourceAmount> core,
                    out List<CityWarehouseRestoreEntry> warehouses,
                    out List<CityStorageOrphanResource> orphans,
                    out error))
                return false;

            bool allowOverCapacity = !string.Equals(
                storage.configurationSignature,
                StorageConfigurationSignature,
                StringComparison.Ordinal);
            if (!session.CityStorage.TryPrepareRestore(
                    core,
                    warehouses,
                    orphans,
                    allowOverCapacity,
                    out CityResourceStorageRestorePlan storagePlan,
                    out error))
                return false;

            if (!session.TryRestoreBuildings(
                    entries,
                    buildings.nextStableInstanceOrdinal,
                    presentation,
                    out error))
                return false;
            if (!session.CityStorage.TryCommitRestore(storagePlan, out error))
                return false;

            error = string.Empty;
            return true;
        }

        private bool TryBuildRestoreEntries(
            FormalThreeDBuildingsSaveData buildings,
            out List<GrayboxBuildingRestoreEntry3D> entries,
            out Dictionary<string, BuildingIdentity> identities,
            out string error)
        {
            entries = new List<GrayboxBuildingRestoreEntry3D>(
                buildings.instances.Length);
            identities = new Dictionary<string, BuildingIdentity>(
                StringComparer.Ordinal);
            WorldMapModel currentWorld = worldProvider?.Invoke();
            for (var index = 0; index < buildings.instances.Length; index++)
            {
                FormalThreeDBuildingInstanceSaveData saved =
                    buildings.instances[index];
                if (saved == null ||
                    identities.ContainsKey(saved.stableInstanceId))
                {
                    error = "建筑实例记录为空或重复";
                    return false;
                }
                if (!Enum.IsDefined(typeof(BuildingSite), saved.site) ||
                    !Enum.IsDefined(
                        typeof(BuildingOrientation),
                        saved.orientation) ||
                    !Enum.IsDefined(
                        typeof(GrayboxBuildingInstanceState),
                        saved.state))
                {
                    error = "建筑站点、方向或状态无效";
                    return false;
                }
                var site = (BuildingSite)saved.site;
                var orientation = (BuildingOrientation)saved.orientation;
                if (!IsStableId(saved.definitionId))
                {
                    error = "建筑定义 ID 语法无效";
                    return false;
                }
                bool known = TryFindDefinition(
                    saved.definitionId,
                    out BuildingDefinition definition);
                if (saved.footprintWidth <= 0 ||
                    saved.footprintHeight <= 0)
                {
                    error = "建筑占地尺寸无效";
                    return false;
                }
                if (known &&
                    (definition.Width != saved.footprintWidth ||
                     definition.Height != saved.footprintHeight))
                {
                    error = "建筑占地与当前正式定义不一致";
                    return false;
                }
                if (!known)
                {
                    if (!IsStableId(saved.definitionId))
                    {
                        error = "缺失建筑定义 ID 语法无效";
                        return false;
                    }
                    definition = CreateMissingDefinition(saved, site);
                }

                if (!TryValidateBinding(
                        saved,
                        definition,
                        known,
                        site,
                        orientation,
                        currentWorld,
                        out ResourceNodeBinding binding,
                        out error))
                    return false;

                var entry = new GrayboxBuildingRestoreEntry3D(
                    saved.stableInstanceId,
                    definition,
                    site,
                    saved.x,
                    saved.y,
                    orientation,
                    (GrayboxBuildingInstanceState)saved.state,
                    saved.constructionRemainingSeconds,
                    saved.isPlayerOwned,
                    saved.evacuationLockedCrossCheck,
                    binding);
                entries.Add(entry);
                identities.Add(
                    saved.stableInstanceId,
                    new BuildingIdentity(
                        definition,
                        known,
                        (GrayboxBuildingInstanceState)saved.state,
                        saved.isPlayerOwned));
            }

            error = string.Empty;
            return true;
        }

        private bool TryValidateBinding(
            FormalThreeDBuildingInstanceSaveData saved,
            BuildingDefinition definition,
            bool known,
            BuildingSite site,
            BuildingOrientation orientation,
            WorldMapModel currentWorld,
            out ResourceNodeBinding binding,
            out string error)
        {
            binding = ResourceNodeBinding.None;
            bool hasBinding = !string.IsNullOrWhiteSpace(
                saved.boundResourceNodeId);
            if (known && definition.RequiresResourceNode != hasBinding)
            {
                error = definition.RequiresResourceNode
                    ? "采矿建筑缺少资源节点绑定"
                    : "普通建筑不能绑定资源节点";
                return false;
            }
            if (!hasBinding)
            {
                if (saved.boundNodeX != -1 || saved.boundNodeY != -1)
                {
                    error = "未绑定建筑的资源节点坐标必须为空";
                    return false;
                }
                error = string.Empty;
                return true;
            }
            if (site != BuildingSite.Ground || currentWorld == null ||
                saved.boundNodeX < 0 ||
                saved.boundNodeX >= currentWorld.Width ||
                saved.boundNodeY < 0 ||
                saved.boundNodeY >= currentWorld.Height)
            {
                error = "资源节点绑定不在当前正式世界内";
                return false;
            }
            string expectedId = GrayboxResourceNodeIdentity3D.Create(
                saved.boundNodeX,
                saved.boundNodeY);
            if (!string.Equals(
                    expectedId,
                    saved.boundResourceNodeId,
                    StringComparison.Ordinal))
            {
                error = "资源节点稳定 ID 与坐标不一致";
                return false;
            }
            WorldCell cell = currentWorld.Get(
                saved.boundNodeX,
                saved.boundNodeY);
            if (!cell.HasResource ||
                (known && !BuildingResourceNodeCompatibilityRules.IsCompatible(
                    definition,
                    cell.ResourceId)))
            {
                error = "资源节点类型与建筑不兼容";
                return false;
            }
            int width = BuildingOrientationRules.Width(
                definition,
                orientation);
            int height = BuildingOrientationRules.Height(
                definition,
                orientation);
            if (saved.boundNodeX < saved.x ||
                saved.boundNodeX >= saved.x + width ||
                saved.boundNodeY < saved.y ||
                saved.boundNodeY >= saved.y + height)
            {
                error = "资源节点不在建筑占地内";
                return false;
            }

            binding = new ResourceNodeBinding(
                saved.boundResourceNodeId,
                saved.boundNodeX,
                saved.boundNodeY);
            error = string.Empty;
            return true;
        }

        private static bool TryBuildStorageRestore(
            FormalThreeDStorageSaveData storage,
            IReadOnlyDictionary<string, BuildingIdentity> identities,
            out List<ResourceAmount> core,
            out List<CityWarehouseRestoreEntry> warehouses,
            out List<CityStorageOrphanResource> orphans,
            out string error)
        {
            core = new List<ResourceAmount>();
            warehouses = new List<CityWarehouseRestoreEntry>();
            orphans = new List<CityStorageOrphanResource>();
            if (!NormalizeAmounts(
                    storage.coreAmounts,
                    CityStorageOrphanResource.CoreOwnerKind,
                    CityStorageOrphanResource.CoreOwnerStableId,
                    core,
                    orphans,
                    out error))
                return false;

            var warehouseIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < storage.warehouses.Length; index++)
            {
                FormalThreeDWarehouseSaveData saved =
                    storage.warehouses[index];
                if (saved == null || saved.amounts == null ||
                    !warehouseIds.Add(saved.stableInstanceId) ||
                    !identities.TryGetValue(
                        saved.stableInstanceId,
                        out BuildingIdentity identity))
                {
                    error = "仓库记录为空、重复或引用不存在";
                    return false;
                }
                bool isFormalWarehouse = identity.IsKnown &&
                    string.Equals(
                        identity.Definition.Id.Value,
                        BuildingCatalog.Warehouse.Id.Value,
                        StringComparison.Ordinal);
                if (identity.IsKnown && !isFormalWarehouse)
                {
                    error = "仓储记录引用的正式建筑不是仓库";
                    return false;
                }
                if (identity.State !=
                        GrayboxBuildingInstanceState.Completed ||
                    !identity.IsPlayerOwned)
                {
                    error = "仓储记录只能属于已完成且归玩家所有的仓库";
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(saved.filterResourceId) &&
                    !IsStableId(saved.filterResourceId))
                {
                    error = "仓库过滤资源 ID 语法无效";
                    return false;
                }

                var amounts = new List<ResourceAmount>();
                if (!NormalizeAmounts(
                        saved.amounts,
                        CityStorageOrphanResource.WarehouseOwnerKind,
                        saved.stableInstanceId,
                        amounts,
                        orphans,
                        out error))
                    return false;
                warehouses.Add(new CityWarehouseRestoreEntry(
                    saved.stableInstanceId,
                    saved.filterResourceId,
                    amounts,
                    preserveWhenDisconnected: !identity.IsKnown));
            }

            foreach (KeyValuePair<string, BuildingIdentity> item in
                     identities)
            {
                BuildingIdentity identity = item.Value;
                if (!identity.IsKnown ||
                    identity.State !=
                        GrayboxBuildingInstanceState.Completed ||
                    !identity.IsPlayerOwned ||
                    !string.Equals(
                        identity.Definition.Id.Value,
                        BuildingCatalog.Warehouse.Id.Value,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (!warehouseIds.Contains(item.Key))
                {
                    error = "已完成且归玩家所有的仓库缺少仓储记录";
                    return false;
                }
            }

            for (var index = 0;
                 index < storage.orphanResources.Length;
                 index++)
            {
                FormalThreeDOrphanResourceSaveData saved =
                    storage.orphanResources[index];
                if (saved == null || !IsStableId(saved.resourceId))
                {
                    error = "孤立资源记录为空或资源 ID 语法无效";
                    return false;
                }
                if (string.Equals(
                        saved.ownerKind,
                        CityStorageOrphanResource.WarehouseOwnerKind,
                        StringComparison.Ordinal) &&
                    !warehouseIds.Contains(saved.ownerStableId))
                {
                    error = "孤立资源引用的仓库不存在";
                    return false;
                }
                if (string.Equals(
                        saved.ownerKind,
                        CityStorageOrphanResource.CoreOwnerKind,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        saved.ownerStableId,
                        CityStorageOrphanResource.CoreOwnerStableId,
                        StringComparison.Ordinal))
                {
                    error = "孤立城市核心资源归属无效";
                    return false;
                }
                orphans.Add(new CityStorageOrphanResource(
                    saved.resourceId,
                    saved.amount,
                    saved.ownerKind,
                    saved.ownerStableId));
            }

            core.Sort(CompareAmounts);
            warehouses.Sort((left, right) => string.CompareOrdinal(
                left.StableInstanceId,
                right.StableInstanceId));
            orphans.Sort(CompareOrphans);
            error = string.Empty;
            return true;
        }

        private static bool NormalizeAmounts(
            FormalThreeDResourceAmountSaveData[] source,
            string ownerKind,
            string ownerStableId,
            List<ResourceAmount> known,
            List<CityStorageOrphanResource> orphans,
            out string error)
        {
            if (source == null)
            {
                error = "资源数量数组不能为空";
                return false;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < source.Length; index++)
            {
                FormalThreeDResourceAmountSaveData saved = source[index];
                if (saved == null || !IsStableId(saved.resourceId) ||
                    saved.amount < 0 || !ids.Add(saved.resourceId))
                {
                    error = "资源数量记录无效或重复";
                    return false;
                }
                if (IsKnownResource(saved.resourceId))
                {
                    known.Add(new ResourceAmount(
                        saved.resourceId,
                        saved.amount));
                }
                else
                {
                    orphans.Add(new CityStorageOrphanResource(
                        saved.resourceId,
                        saved.amount,
                        ownerKind,
                        ownerStableId));
                }
            }
            error = string.Empty;
            return true;
        }

        private static BuildingDefinition CreateMissingDefinition(
            FormalThreeDBuildingInstanceSaveData saved,
            BuildingSite site)
        {
            return new BuildingDefinition(
                saved.definitionId,
                "缺失内容",
                saved.footprintWidth,
                saved.footprintHeight,
                ResourceIds.Iron,
                0,
                requiresNode: false,
                buildSeconds: Math.Max(
                    .1f,
                    saved.constructionRemainingSeconds),
                placement: site == BuildingSite.InnerCity
                    ? BuildingPlacement.InnerCity
                    : BuildingPlacement.Ground,
                operation: BuildingOperation.FortressOnly);
        }

        private static bool TryFindDefinition(
            string definitionId,
            out BuildingDefinition definition)
        {
            for (var index = 0; index < BuildingCatalog.All.Length; index++)
            {
                BuildingDefinition candidate = BuildingCatalog.All[index];
                if (!string.Equals(
                        candidate.Id.Value,
                        definitionId,
                        StringComparison.Ordinal))
                    continue;
                definition = candidate;
                return true;
            }
            definition = null;
            return false;
        }

        private static string[] SortedResourceIds()
        {
            var result = (string[])ResourceIds.All.Clone();
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static bool HasOverCapacityWarehouse(
            IReadOnlyList<WarehouseStorageSnapshot> warehouses)
        {
            for (var index = 0; index < warehouses.Count; index++)
                if (warehouses[index].TotalAmount >
                    warehouses[index].Capacity)
                    return true;
            return false;
        }

        private static bool IsKnownResource(string resourceId)
        {
            for (var index = 0; index < ResourceIds.All.Length; index++)
                if (string.Equals(
                        ResourceIds.All[index],
                        resourceId,
                        StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool IsStableId(string value)
        {
            try
            {
                _ = new StableId(value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static int CompareAmounts(
            ResourceAmount left,
            ResourceAmount right)
        {
            return string.CompareOrdinal(left.ResourceId, right.ResourceId);
        }

        private static int CompareOrphans(
            CityStorageOrphanResource left,
            CityStorageOrphanResource right)
        {
            int comparison = string.CompareOrdinal(
                left.OwnerKind,
                right.OwnerKind);
            if (comparison != 0) return comparison;
            comparison = string.CompareOrdinal(
                left.OwnerStableId,
                right.OwnerStableId);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left.ResourceId, right.ResourceId);
        }

        private readonly struct BuildingIdentity
        {
            public BuildingIdentity(
                BuildingDefinition definition,
                bool isKnown,
                GrayboxBuildingInstanceState state,
                bool isPlayerOwned)
            {
                Definition = definition;
                IsKnown = isKnown;
                State = state;
                IsPlayerOwned = isPlayerOwned;
            }

            public BuildingDefinition Definition { get; }
            public bool IsKnown { get; }
            public GrayboxBuildingInstanceState State { get; }
            public bool IsPlayerOwned { get; }
        }
    }
}
