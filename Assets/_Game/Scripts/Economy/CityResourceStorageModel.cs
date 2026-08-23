using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Economy
{
    public enum WarehouseRemovalStatus
    {
        Completed,
        NotFound,
        InsufficientNetworkSpace,
    }

    public enum CityResourceEvacuationCommitStatus
    {
        Completed,
        StalePlan,
        AlreadyCommitted,
        CapacityInsufficient,
        Invalid,
    }

    public sealed class CityResourceEvacuationPlan
    {
        private readonly IReadOnlyDictionary<string, int> incoming;
        private readonly IReadOnlyDictionary<string, int> shortfalls;

        internal CityResourceEvacuationPlan(
            CityResourceStorageModel owner,
            object preparedStoragePlan,
            string sourceWarehouseId,
            ulong preparedRevision,
            bool isValid,
            IReadOnlyDictionary<string, int> incoming,
            IReadOnlyDictionary<string, int> shortfalls)
        {
            Owner = owner;
            PreparedStoragePlan = preparedStoragePlan;
            SourceWarehouseId = sourceWarehouseId;
            PreparedRevision = preparedRevision;
            IsValid = isValid;
            this.incoming = Copy(incoming);
            this.shortfalls = Copy(shortfalls);
            int total = 0;
            foreach (KeyValuePair<string, int> item in this.shortfalls)
                total += Math.Max(0, item.Value);
            TotalShortfall = total;
        }

        public string SourceWarehouseId { get; }
        public ulong PreparedRevision { get; }
        public bool IsValid { get; }
        public bool CanCommit => IsValid && TotalShortfall == 0;
        public int TotalShortfall { get; }

        internal CityResourceStorageModel Owner { get; }
        internal object PreparedStoragePlan { get; }
        internal bool committed;

        public int GetIncomingAmount(string resourceId)
        {
            return Get(incoming, resourceId);
        }

        public int GetShortfall(string resourceId)
        {
            return Get(shortfalls, resourceId);
        }

        private static IReadOnlyDictionary<string, int> Copy(
            IReadOnlyDictionary<string, int> source)
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source != null)
                foreach (KeyValuePair<string, int> item in source)
                    copy.Add(item.Key, item.Value);
            return new ReadOnlyDictionary<string, int>(copy);
        }

        private static int Get(
            IReadOnlyDictionary<string, int> values,
            string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                   values.TryGetValue(resourceId, out int amount)
                ? amount
                : 0;
        }
    }

    public readonly struct CityResourceChangeAttributionScope : IDisposable
    {
        private readonly CityResourceStorageModel owner;
        private readonly ResourceChangeAttribution previous;

        internal CityResourceChangeAttributionScope(
            CityResourceStorageModel owner,
            ResourceChangeAttribution previous)
        {
            this.owner = owner;
            this.previous = previous;
        }

        public void Dispose()
        {
            owner?.RestoreChangeAttribution(previous);
        }
    }

    public sealed class CityWarehouseRestoreEntry
    {
        private readonly IReadOnlyList<ResourceAmount> amounts;

        public CityWarehouseRestoreEntry(
            string stableInstanceId,
            string filterResourceId,
            IReadOnlyList<ResourceAmount> amounts,
            bool preserveWhenDisconnected = false)
        {
            StableInstanceId = stableInstanceId;
            FilterResourceId = filterResourceId;
            PreserveWhenDisconnected = preserveWhenDisconnected;
            if (amounts == null)
            {
                this.amounts = null;
            }
            else
            {
                var copy = new ResourceAmount[amounts.Count];
                for (int index = 0; index < amounts.Count; index++)
                    copy[index] = amounts[index];
                this.amounts = Array.AsReadOnly(copy);
            }
        }

        public string StableInstanceId { get; }
        public string FilterResourceId { get; }
        public IReadOnlyList<ResourceAmount> Amounts => amounts;
        public bool PreserveWhenDisconnected { get; }
    }

    public readonly struct CityStorageOrphanResource
    {
        public const string CoreOwnerKind = "city-core";
        public const string CoreOwnerStableId = "city.core";
        public const string WarehouseOwnerKind = "warehouse";

        public CityStorageOrphanResource(
            string resourceId,
            int amount,
            string ownerKind,
            string ownerStableId)
        {
            ResourceId = resourceId;
            Amount = amount;
            OwnerKind = ownerKind;
            OwnerStableId = ownerStableId;
        }

        public string ResourceId { get; }
        public int Amount { get; }
        public string OwnerKind { get; }
        public string OwnerStableId { get; }
    }

    public sealed class CityResourceStorageRestorePlan
    {
        internal CityResourceStorageRestorePlan(
            CityResourceStorageModel owner,
            ulong preparedRevision,
            Dictionary<string, int> coreAmounts,
            SortedDictionary<string, WarehouseStorageState> warehouses,
            CityStorageOrphanResource[] orphanResources)
        {
            Owner = owner;
            PreparedRevision = preparedRevision;
            CoreAmounts = coreAmounts;
            Warehouses = warehouses;
            OrphanResources = orphanResources;
        }

        public ulong PreparedRevision { get; }
        public int WarehouseCount => Warehouses.Count;

        internal CityResourceStorageModel Owner { get; }
        internal Dictionary<string, int> CoreAmounts { get; }
        internal SortedDictionary<string, WarehouseStorageState> Warehouses
            { get; }
        internal CityStorageOrphanResource[] OrphanResources { get; }
        internal bool committed;
    }

    public sealed class CityResourceStorageModel : IDisposable
    {
        private readonly ResourceInventory coreInventory;
        private readonly int coreCapacityPerResource;
        private readonly SortedDictionary<string, WarehouseStorageState>
            warehouses = new SortedDictionary<string, WarehouseStorageState>(
                StringComparer.Ordinal);
        private bool suppressCoreChange;
        private bool disposed;
        private ResourceChangeAttribution changeAttribution;
        private Exception lastNotificationFailure;
        private readonly int[] networkBefore = new int[ResourceIds.All.Length];
        private CityStorageOrphanResource[] orphanResources =
            Array.Empty<CityStorageOrphanResource>();

        public CityResourceStorageModel(
            ResourceInventory coreInventory,
            int coreCapacityPerResource =
                ResourceCapacityPolicy.FormalBaseCapacityPerResource)
        {
            this.coreInventory = coreInventory ??
                throw new ArgumentNullException(nameof(coreInventory));
            this.coreCapacityPerResource = Math.Max(
                0,
                coreCapacityPerResource);
            coreInventory.AttributedChanged += HandleCoreChanged;
        }

        public event Action<string, int, ResourceChangeAttribution>
            AttributedChanged;
        public ulong Revision { get; private set; }
        public int CoreCapacityPerResource => coreCapacityPerResource;
        public int WarehouseCount => warehouses.Count;
        public Exception LastNotificationFailure => lastNotificationFailure;

        public bool ContainsWarehouse(string stableInstanceId)
        {
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                warehouses.ContainsKey(stableInstanceId);
        }

        public void CopyWarehouseIds(List<string> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (string stableInstanceId in warehouses.Keys)
                destination.Add(stableInstanceId);
        }

        public bool TryRegisterWarehouse(
            string stableInstanceId,
            bool connected = true)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId) ||
                warehouses.ContainsKey(stableInstanceId))
            {
                return false;
            }
            warehouses.Add(
                stableInstanceId,
                new WarehouseStorageState(stableInstanceId, connected));
            AdvanceRevision();
            return true;
        }

        public bool TryRemoveWarehouse(string stableInstanceId)
        {
            if (!TryGetWarehouse(stableInstanceId, out WarehouseStorageState state) ||
                state.TotalAmount > 0 || state.PreserveWhenDisconnected)
            {
                return false;
            }
            warehouses.Remove(stableInstanceId);
            AdvanceRevision();
            return true;
        }

        public bool TryDestroyWarehouseForCombat(
            string stableInstanceId,
            out ResourceAmount[] lostResources)
        {
            lostResources = Array.Empty<ResourceAmount>();
            if (!TryGetWarehouse(
                    stableInstanceId,
                    out WarehouseStorageState state))
            {
                return false;
            }

            var lost = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            Dictionary<string, int> stored = state.CopyAmounts();
            foreach (KeyValuePair<string, int> item in stored)
            {
                if (item.Value > 0) lost.Add(item.Key, item.Value);
            }

            if (orphanResources.Length > 0)
            {
                var retained = new List<CityStorageOrphanResource>(
                    orphanResources.Length);
                for (var index = 0; index < orphanResources.Length; index++)
                {
                    CityStorageOrphanResource orphan = orphanResources[index];
                    if (string.Equals(
                            orphan.OwnerKind,
                            CityStorageOrphanResource.WarehouseOwnerKind,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            orphan.OwnerStableId,
                            stableInstanceId,
                            StringComparison.Ordinal))
                    {
                        if (orphan.Amount > 0)
                        {
                            lost.TryGetValue(orphan.ResourceId, out int before);
                            lost[orphan.ResourceId] = before + orphan.Amount;
                        }
                        continue;
                    }
                    retained.Add(orphan);
                }
                if (retained.Count != orphanResources.Length)
                    orphanResources = retained.ToArray();
            }

            warehouses.Remove(stableInstanceId);
            var result = new ResourceAmount[lost.Count];
            var resultIndex = 0;
            foreach (KeyValuePair<string, int> item in lost)
                result[resultIndex++] = new ResourceAmount(item.Key, item.Value);
            lostResources = result;
            AdvanceRevision();

            if (state.IsConnected)
            {
                foreach (KeyValuePair<string, int> item in stored)
                {
                    if (item.Value > 0) PublishChange(item.Key, -item.Value);
                }
            }
            return true;
        }

        public bool CanRemoveWarehouseWithMigration(
            string stableInstanceId,
            out WarehouseRemovalStatus status)
        {
            CityResourceEvacuationPlan plan = CreateEvacuationPlan(
                stableInstanceId,
                null);
            status = RemovalStatus(plan);
            return plan.CanCommit;
        }

        public bool TryRemoveWarehouseWithMigration(
            string stableInstanceId,
            out WarehouseRemovalStatus status)
        {
            CityResourceEvacuationPlan plan = CreateEvacuationPlan(
                stableInstanceId,
                null);
            if (!TryCommitEvacuationPlan(plan, out _))
            {
                status = RemovalStatus(plan);
                return false;
            }
            status = WarehouseRemovalStatus.Completed;
            return true;
        }

        public bool TrySetWarehouseConnected(
            string stableInstanceId,
            bool connected)
        {
            if (!TryGetWarehouse(stableInstanceId, out WarehouseStorageState state))
                return false;
            if (state.IsConnected == connected) return true;
            int direction = connected ? 1 : -1;
            state.SetConnected(connected);
            AdvanceRevision();
            foreach (string resourceId in ResourceIds.All)
            {
                int amount = state.Get(resourceId);
                if (amount > 0) PublishChange(resourceId, direction * amount);
            }
            return true;
        }

        public bool TrySetWarehouseFilter(
            string stableInstanceId,
            string resourceId)
        {
            if (!TryGetWarehouse(stableInstanceId, out WarehouseStorageState state))
                return false;
            string normalized = string.IsNullOrWhiteSpace(resourceId)
                ? null
                : resourceId;
            if (normalized != null)
            {
                for (int index = 0; index < orphanResources.Length; index++)
                {
                    CityStorageOrphanResource orphan = orphanResources[index];
                    if (orphan.Amount > 0 &&
                        string.Equals(
                            orphan.OwnerKind,
                            CityStorageOrphanResource.WarehouseOwnerKind,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            orphan.OwnerStableId,
                            stableInstanceId,
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            orphan.ResourceId,
                            normalized,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }
            string before = state.FilterResourceId;
            if (!state.TrySetFilter(normalized)) return false;
            if (!string.Equals(
                    before,
                    state.FilterResourceId,
                    StringComparison.Ordinal))
            {
                AdvanceRevision();
            }
            return true;
        }

        public string GetWarehouseFilter(string stableInstanceId)
        {
            return TryGetWarehouse(stableInstanceId, out WarehouseStorageState state)
                ? state.FilterResourceId
                : null;
        }

        public int GetWarehouseCapacity(string stableInstanceId)
        {
            return TryGetWarehouse(stableInstanceId, out WarehouseStorageState state)
                ? state.Capacity
                : 0;
        }

        public int GetWarehouseFreeSpace(string stableInstanceId)
        {
            return TryGetWarehouse(stableInstanceId, out WarehouseStorageState state)
                ? state.FreeSpace
                : 0;
        }

        public int GetWarehouseAmount(
            string stableInstanceId,
            string resourceId)
        {
            return TryGetWarehouse(stableInstanceId, out WarehouseStorageState state)
                ? state.Get(resourceId)
                : 0;
        }

        public int GetCoreAmount(string resourceId)
        {
            return ResourceCapacityPolicy.IsRegisteredResource(resourceId)
                ? coreInventory.Get(resourceId)
                : 0;
        }

        public int GetNetworkAmount(string resourceId)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId))
                return 0;
            long total = Math.Max(0, coreInventory.Get(resourceId));
            if (warehouses.Count == 0)
                return total >= int.MaxValue ? int.MaxValue : (int)total;
            foreach (KeyValuePair<string, WarehouseStorageState> item in
                     warehouses)
            {
                WarehouseStorageState warehouse = item.Value;
                if (warehouse.IsConnected)
                    total += warehouse.Get(resourceId);
            }
            return total >= int.MaxValue ? int.MaxValue : (int)total;
        }

        public int GetNetworkAcceptableSpace(string resourceId)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId))
                return 0;
            long space = CoreAcceptableSpace(resourceId);
            if (warehouses.Count == 0)
                return space >= int.MaxValue ? int.MaxValue : (int)space;
            foreach (KeyValuePair<string, WarehouseStorageState> item in
                     warehouses)
            {
                WarehouseStorageState warehouse = item.Value;
                if (warehouse.IsConnected && warehouse.CanAccept(resourceId))
                    space += warehouse.FreeSpace;
            }
            return space >= int.MaxValue ? int.MaxValue : (int)space;
        }

        public int GetNetworkCapacityLimit(string resourceId)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId))
                return 0;
            long capacity = coreCapacityPerResource;
            if (warehouses.Count == 0)
                return capacity >= int.MaxValue ? int.MaxValue : (int)capacity;
            foreach (KeyValuePair<string, WarehouseStorageState> item in
                     warehouses)
            {
                WarehouseStorageState warehouse = item.Value;
                if (!warehouse.IsConnected ||
                    !warehouse.CanAccept(resourceId))
                {
                    continue;
                }
                capacity += warehouse.Get(resourceId) + warehouse.FreeSpace;
            }
            return capacity >= int.MaxValue ? int.MaxValue : (int)capacity;
        }

        public int GetNetworkAcceptableAmount(
            string resourceId,
            int requestedAmount)
        {
            return requestedAmount <= 0
                ? 0
                : Math.Min(
                    requestedAmount,
                    GetNetworkAcceptableSpace(resourceId));
        }

        public int AddToWarehouse(
            string stableInstanceId,
            string resourceId,
            int requestedAmount)
        {
            if (!TryGetWarehouse(stableInstanceId, out WarehouseStorageState state))
                return 0;
            int accepted = state.Add(resourceId, requestedAmount);
            if (accepted > 0)
            {
                AdvanceRevision();
                if (state.IsConnected) PublishChange(resourceId, accepted);
            }
            return accepted;
        }

        public bool TrySpendFromWarehouse(
            string stableInstanceId,
            string resourceId,
            int amount)
        {
            if (!TryGetWarehouse(stableInstanceId, out WarehouseStorageState state) ||
                amount <= 0 || !state.TrySpend(resourceId, amount))
            {
                return false;
            }
            AdvanceRevision();
            if (state.IsConnected) PublishChange(resourceId, -amount);
            return true;
        }

        public int AddToNetwork(string resourceId, int requestedAmount)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                requestedAmount <= 0)
            {
                return 0;
            }
            StoragePlan plan = CreatePlan();
            int accepted = plan.Add(resourceId, requestedAmount);
            if (accepted > 0) Commit(plan);
            return accepted;
        }

        public bool TrySpendFromNetwork(string resourceId, int amount)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                amount <= 0)
            {
                return false;
            }
            StoragePlan plan = CreatePlan();
            if (!plan.TrySpend(resourceId, amount)) return false;
            Commit(plan);
            return true;
        }

        public bool CanSpendFromNetwork(string resourceId, int amount)
        {
            return ResourceCapacityPolicy.IsRegisteredResource(resourceId) &&
                amount >= 0 && GetNetworkAmount(resourceId) >= amount;
        }

        public bool TryCommitBatch(
            IReadOnlyList<ResourceAmount> inputs,
            IReadOnlyList<ResourceAmount> outputs)
        {
            if (!TryAggregate(inputs, out SortedDictionary<string, int> spends) ||
                !TryAggregate(outputs, out SortedDictionary<string, int> adds) ||
                spends.Count == 0 && adds.Count == 0)
            {
                return false;
            }

            StoragePlan plan = CreatePlan();
            foreach (KeyValuePair<string, int> item in spends)
            {
                if (!plan.TrySpend(item.Key, item.Value)) return false;
            }
            foreach (KeyValuePair<string, int> item in adds)
            {
                if (plan.Add(item.Key, item.Value) != item.Value) return false;
            }
            Commit(plan);
            return true;
        }

        public CityResourceEvacuationPlan CreateEvacuationPlan(
            string sourceWarehouseId,
            IReadOnlyList<ResourceAmount> additions)
        {
            bool removesWarehouse = !string.IsNullOrWhiteSpace(
                sourceWarehouseId);
            bool valid = TryAggregate(
                additions,
                out SortedDictionary<string, int> additionTotals);
            if (!removesWarehouse && additionTotals.Count == 0)
                valid = false;
            StoragePlan storagePlan = CreatePlan();
            var incoming = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            var sourceIncoming = new SortedDictionary<string, int>(
                StringComparer.Ordinal);
            var shortfalls = new SortedDictionary<string, int>(
                StringComparer.Ordinal);

            if (removesWarehouse)
            {
                if (!TryGetWarehouse(
                        sourceWarehouseId,
                        out WarehouseStorageState sourceWarehouse))
                {
                    valid = false;
                }
                else if (sourceWarehouse.OrphanAmount > 0)
                {
                    valid = false;
                }
                else
                {
                    Dictionary<string, int> sourceAmounts =
                        storagePlan.Warehouses[sourceWarehouseId];
                    foreach (string resourceId in ResourceIds.All)
                    {
                        int amount = sourceAmounts.TryGetValue(
                                resourceId,
                                out int value)
                            ? Math.Max(0, value)
                            : 0;
                        if (amount > 0)
                        {
                            sourceIncoming[resourceId] = amount;
                            incoming[resourceId] = amount;
                        }
                    }
                    sourceAmounts.Clear();
                }
            }

            if (valid)
            {
                foreach (KeyValuePair<string, int> item in additionTotals)
                {
                    incoming.TryGetValue(item.Key, out int before);
                    long total = (long)before + item.Value;
                    if (total > int.MaxValue)
                    {
                        valid = false;
                        break;
                    }
                    incoming[item.Key] = (int)total;
                }
            }

            if (valid)
            {
                RouteEvacuationIncoming(
                    storagePlan,
                    additionTotals,
                    sourceWarehouseId,
                    shortfalls);
                RouteEvacuationIncoming(
                    storagePlan,
                    sourceIncoming,
                    sourceWarehouseId,
                    shortfalls);
            }

            return new CityResourceEvacuationPlan(
                this,
                storagePlan,
                removesWarehouse ? sourceWarehouseId : null,
                Revision,
                valid,
                incoming,
                shortfalls);
        }

        private static void RouteEvacuationIncoming(
            StoragePlan storagePlan,
            IReadOnlyDictionary<string, int> amounts,
            string excludedWarehouseId,
            IDictionary<string, int> shortfalls)
        {
            foreach (KeyValuePair<string, int> item in amounts)
            {
                int accepted = storagePlan.Add(
                    item.Key,
                    item.Value,
                    excludedWarehouseId);
                int shortfall = item.Value - accepted;
                if (shortfall <= 0) continue;
                shortfalls.TryGetValue(item.Key, out int before);
                shortfalls[item.Key] = before + shortfall;
            }
        }

        public bool TryCommitEvacuationPlan(
            CityResourceEvacuationPlan plan,
            out CityResourceEvacuationCommitStatus status)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                !plan.IsValid ||
                !(plan.PreparedStoragePlan is StoragePlan storagePlan))
            {
                status = CityResourceEvacuationCommitStatus.Invalid;
                return false;
            }
            if (plan.committed)
            {
                status = CityResourceEvacuationCommitStatus.AlreadyCommitted;
                return false;
            }
            if (plan.PreparedRevision != Revision)
            {
                status = CityResourceEvacuationCommitStatus.StalePlan;
                return false;
            }
            if (!plan.CanCommit)
            {
                status = CityResourceEvacuationCommitStatus.CapacityInsufficient;
                return false;
            }

            Commit(storagePlan, plan.SourceWarehouseId);
            plan.committed = true;
            status = CityResourceEvacuationCommitStatus.Completed;
            return true;
        }

        public CityResourceStorageSnapshot CaptureSnapshot()
        {
            var core = new Dictionary<string, int>(StringComparer.Ordinal);
            var network = new Dictionary<string, int>(StringComparer.Ordinal);
            var acceptable = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string resourceId in ResourceIds.All)
            {
                core.Add(resourceId, GetCoreAmount(resourceId));
                network.Add(resourceId, GetNetworkAmount(resourceId));
                acceptable.Add(
                    resourceId,
                    GetNetworkAcceptableSpace(resourceId));
            }
            var warehouseSnapshots = new List<WarehouseStorageSnapshot>(
                warehouses.Count);
            foreach (KeyValuePair<string, WarehouseStorageState> item in
                     warehouses)
                warehouseSnapshots.Add(item.Value.CaptureSnapshot());
            return new CityResourceStorageSnapshot(
                Revision,
                coreCapacityPerResource,
                core,
                network,
                acceptable,
                warehouseSnapshots);
        }

        public CityStorageOrphanResource[] CaptureOrphanResources()
        {
            return (CityStorageOrphanResource[])orphanResources.Clone();
        }

        public bool TryPrepareRestore(
            IReadOnlyList<ResourceAmount> coreAmounts,
            IReadOnlyList<CityWarehouseRestoreEntry> warehouseEntries,
            IReadOnlyList<CityStorageOrphanResource> orphanEntries,
            bool allowOverCapacity,
            out CityResourceStorageRestorePlan plan,
            out string error)
        {
            plan = null;
            if (coreAmounts == null || warehouseEntries == null ||
                orphanEntries == null)
            {
                error = "城市仓储恢复数据不完整";
                return false;
            }

            var restoredCore = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string resourceId in ResourceIds.All)
                restoredCore.Add(resourceId, 0);
            var seenCore = new HashSet<string>(StringComparer.Ordinal);
            int effectiveCoreCapacity = Math.Min(
                coreCapacityPerResource,
                coreInventory.CapacityPerResource);
            for (int index = 0; index < coreAmounts.Count; index++)
            {
                ResourceAmount amount = coreAmounts[index];
                if (!ResourceCapacityPolicy.IsRegisteredResource(
                        amount.ResourceId) ||
                    amount.Amount < 0 ||
                    !seenCore.Add(amount.ResourceId))
                {
                    error = "城市核心资源记录无效或重复";
                    return false;
                }
                if ((!allowOverCapacity &&
                     amount.Amount > effectiveCoreCapacity) ||
                    amount.Amount > coreInventory.CapacityPerResource)
                {
                    error = "城市核心资源超过当前容量";
                    return false;
                }
                restoredCore[amount.ResourceId] = amount.Amount;
            }

            var entriesById =
                new SortedDictionary<string, CityWarehouseRestoreEntry>(
                    StringComparer.Ordinal);
            var amountsByWarehouse =
                new SortedDictionary<string, Dictionary<string, int>>(
                    StringComparer.Ordinal);
            for (int index = 0; index < warehouseEntries.Count; index++)
            {
                CityWarehouseRestoreEntry entry = warehouseEntries[index];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.StableInstanceId) ||
                    entry.Amounts == null ||
                    entriesById.ContainsKey(entry.StableInstanceId))
                {
                    error = "仓库恢复记录无效或重复";
                    return false;
                }

                var restoredAmounts = new Dictionary<string, int>(
                    StringComparer.Ordinal);
                for (int amountIndex = 0;
                     amountIndex < entry.Amounts.Count;
                     amountIndex++)
                {
                    ResourceAmount amount = entry.Amounts[amountIndex];
                    if (!ResourceCapacityPolicy.IsRegisteredResource(
                            amount.ResourceId) ||
                        amount.Amount < 0 ||
                        restoredAmounts.ContainsKey(amount.ResourceId))
                    {
                        error = "仓库资源记录无效或重复";
                        return false;
                    }
                    restoredAmounts.Add(amount.ResourceId, amount.Amount);
                }
                entriesById.Add(entry.StableInstanceId, entry);
                amountsByWarehouse.Add(
                    entry.StableInstanceId,
                    restoredAmounts);
            }

            var restoredOrphans = new CityStorageOrphanResource[
                orphanEntries.Count];
            var orphanKeys = new HashSet<string>(StringComparer.Ordinal);
            var orphanTotalsByWarehouse = new Dictionary<string, long>(
                StringComparer.Ordinal);
            for (int index = 0; index < orphanEntries.Count; index++)
            {
                CityStorageOrphanResource orphan = orphanEntries[index];
                if (string.IsNullOrWhiteSpace(orphan.ResourceId) ||
                    string.IsNullOrWhiteSpace(orphan.OwnerKind) ||
                    string.IsNullOrWhiteSpace(orphan.OwnerStableId) ||
                    orphan.Amount < 0)
                {
                    error = "孤立资源记录无效";
                    return false;
                }
                string orphanKey = orphan.OwnerKind + "\n" +
                    orphan.OwnerStableId + "\n" + orphan.ResourceId;
                if (!orphanKeys.Add(orphanKey))
                {
                    error = "孤立资源记录重复";
                    return false;
                }

                if (string.Equals(
                        orphan.OwnerKind,
                        CityStorageOrphanResource.WarehouseOwnerKind,
                        StringComparison.Ordinal))
                {
                    if (!entriesById.TryGetValue(
                            orphan.OwnerStableId,
                            out CityWarehouseRestoreEntry warehouseEntry))
                    {
                        error = "孤立资源引用的仓库不存在";
                        return false;
                    }
                    if (amountsByWarehouse[orphan.OwnerStableId].ContainsKey(
                            orphan.ResourceId))
                    {
                        error = "仓库资源同时出现在正式与孤立记录中";
                        return false;
                    }
                    if (orphan.Amount > 0 &&
                        !string.IsNullOrWhiteSpace(
                            warehouseEntry.FilterResourceId) &&
                        !string.Equals(
                            warehouseEntry.FilterResourceId,
                            orphan.ResourceId,
                            StringComparison.Ordinal))
                    {
                        error = "仓库过滤与孤立内容不兼容";
                        return false;
                    }
                    orphanTotalsByWarehouse.TryGetValue(
                        orphan.OwnerStableId,
                        out long orphanTotal);
                    orphanTotal += orphan.Amount;
                    if (orphanTotal > int.MaxValue)
                    {
                        error = "仓库孤立资源总量溢出";
                        return false;
                    }
                    orphanTotalsByWarehouse[orphan.OwnerStableId] = orphanTotal;
                }
                else if (string.Equals(
                             orphan.OwnerKind,
                             CityStorageOrphanResource.CoreOwnerKind,
                             StringComparison.Ordinal) &&
                         string.Equals(
                             orphan.OwnerStableId,
                             CityStorageOrphanResource.CoreOwnerStableId,
                             StringComparison.Ordinal) &&
                         seenCore.Contains(orphan.ResourceId))
                {
                    error = "城市核心资源同时出现在正式与孤立记录中";
                    return false;
                }
                restoredOrphans[index] = orphan;
            }

            var restoredWarehouses =
                new SortedDictionary<string, WarehouseStorageState>(
                    StringComparer.Ordinal);
            foreach (KeyValuePair<string, CityWarehouseRestoreEntry> item in
                     entriesById)
            {
                CityWarehouseRestoreEntry entry = item.Value;
                int orphanAmount = orphanTotalsByWarehouse.TryGetValue(
                        item.Key,
                        out long total)
                    ? (int)total
                    : 0;
                var restored = new WarehouseStorageState(
                    item.Key,
                    connected: false,
                    WarehouseStorageState.FormalCapacity,
                    entry.PreserveWhenDisconnected);
                if (!restored.TryRestore(
                        entry.FilterResourceId,
                        amountsByWarehouse[item.Key],
                        orphanAmount,
                        allowOverCapacity,
                        entry.PreserveWhenDisconnected,
                        out error))
                {
                    return false;
                }
                restoredWarehouses.Add(item.Key, restored);
            }

            plan = new CityResourceStorageRestorePlan(
                this,
                Revision,
                restoredCore,
                restoredWarehouses,
                restoredOrphans);
            error = string.Empty;
            return true;
        }

        public bool TryCommitRestore(
            CityResourceStorageRestorePlan plan,
            out string error)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this))
            {
                error = "城市仓储恢复计划不属于当前会话";
                return false;
            }
            if (plan.committed)
            {
                error = "城市仓储恢复计划已经提交";
                return false;
            }
            if (plan.PreparedRevision != Revision)
            {
                error = "城市仓储已变化，请重新准备恢复计划";
                return false;
            }

            CaptureNetworkBefore();
            suppressCoreChange = true;
            try
            {
                foreach (string resourceId in ResourceIds.All)
                    coreInventory.Restore(
                        resourceId,
                        plan.CoreAmounts[resourceId]);
            }
            finally
            {
                suppressCoreChange = false;
            }
            warehouses.Clear();
            foreach (KeyValuePair<string, WarehouseStorageState> item in
                     plan.Warehouses)
                warehouses.Add(item.Key, item.Value);
            orphanResources = (CityStorageOrphanResource[])
                plan.OrphanResources.Clone();
            plan.committed = true;
            AdvanceRevision();
            PublishNetworkChanges();
            error = string.Empty;
            return true;
        }

        public CityResourceChangeAttributionScope AttributeChanges(
            ResourceChangeAttribution attribution)
        {
            ResourceChangeAttribution previous = changeAttribution;
            changeAttribution = attribution;
            return new CityResourceChangeAttributionScope(this, previous);
        }

        public bool TryGetWarehouseSnapshot(
            string stableInstanceId,
            out WarehouseStorageSnapshot snapshot)
        {
            snapshot = null;
            if (!TryGetWarehouse(stableInstanceId, out WarehouseStorageState state))
                return false;
            snapshot = state.CaptureSnapshot();
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            coreInventory.AttributedChanged -= HandleCoreChanged;
        }

        internal void RestoreChangeAttribution(
            ResourceChangeAttribution attribution)
        {
            changeAttribution = attribution;
        }

        private int CoreAcceptableSpace(string resourceId)
        {
            int effectiveCapacity = Math.Min(
                coreCapacityPerResource,
                coreInventory.CapacityPerResource);
            return Math.Max(0, effectiveCapacity - coreInventory.Get(resourceId));
        }

        private static WarehouseRemovalStatus RemovalStatus(
            CityResourceEvacuationPlan plan)
        {
            return plan == null || !plan.IsValid
                ? WarehouseRemovalStatus.NotFound
                : plan.CanCommit
                    ? WarehouseRemovalStatus.Completed
                    : WarehouseRemovalStatus.InsufficientNetworkSpace;
        }

        private bool TryGetWarehouse(
            string stableInstanceId,
            out WarehouseStorageState state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                warehouses.TryGetValue(stableInstanceId, out state);
        }

        private StoragePlan CreatePlan()
        {
            var core = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string resourceId in ResourceIds.All)
                core.Add(resourceId, Math.Max(0, coreInventory.Get(resourceId)));
            var warehouseAmounts =
                new SortedDictionary<string, Dictionary<string, int>>(
                    StringComparer.Ordinal);
            foreach (KeyValuePair<string, WarehouseStorageState> item in warehouses)
                warehouseAmounts.Add(item.Key, item.Value.CopyAmounts());
            return new StoragePlan(
                this,
                core,
                warehouseAmounts);
        }

        private void Commit(StoragePlan plan, string removedWarehouseId = null)
        {
            if (!HasChanges(plan) && string.IsNullOrEmpty(removedWarehouseId))
                return;
            CaptureNetworkBefore();
            suppressCoreChange = true;
            try
            {
                foreach (string resourceId in ResourceIds.All)
                    coreInventory.Restore(resourceId, plan.Core[resourceId]);
            }
            finally
            {
                suppressCoreChange = false;
            }
            foreach (KeyValuePair<string, Dictionary<string, int>> item in
                     plan.Warehouses)
            {
                if (string.Equals(
                        item.Key,
                        removedWarehouseId,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                warehouses[item.Key].ReplaceAmounts(item.Value);
            }
            if (!string.IsNullOrEmpty(removedWarehouseId))
                warehouses.Remove(removedWarehouseId);
            AdvanceRevision();
            PublishNetworkChanges();
        }

        private bool HasChanges(StoragePlan plan)
        {
            foreach (string resourceId in ResourceIds.All)
            {
                if (coreInventory.Get(resourceId) != plan.Core[resourceId])
                    return true;
            }
            foreach (KeyValuePair<string, Dictionary<string, int>> item in
                     plan.Warehouses)
            {
                WarehouseStorageState state = warehouses[item.Key];
                foreach (string resourceId in ResourceIds.All)
                {
                    int next = item.Value.TryGetValue(
                            resourceId,
                            out int amount)
                        ? amount
                        : 0;
                    if (state.Get(resourceId) != next) return true;
                }
            }
            return false;
        }

        private static bool TryAggregate(
            IReadOnlyList<ResourceAmount> amounts,
            out SortedDictionary<string, int> totals)
        {
            totals = new SortedDictionary<string, int>(StringComparer.Ordinal);
            if (amounts == null) return true;
            for (int index = 0; index < amounts.Count; index++)
            {
                ResourceAmount item = amounts[index];
                if (!ResourceCapacityPolicy.IsRegisteredResource(item.ResourceId) ||
                    item.Amount <= 0)
                {
                    return false;
                }
                totals.TryGetValue(item.ResourceId, out int before);
                long total = (long)before + item.Amount;
                if (total > int.MaxValue) return false;
                totals[item.ResourceId] = (int)total;
            }
            return true;
        }

        private void HandleCoreChanged(
            string resourceId,
            int delta,
            ResourceChangeAttribution attribution)
        {
            if (suppressCoreChange || delta == 0) return;
            AdvanceRevision();
            NotifyAttributedChanged(resourceId, delta, attribution);
        }

        private void CaptureNetworkBefore()
        {
            for (int index = 0; index < ResourceIds.All.Length; index++)
                networkBefore[index] = GetNetworkAmount(ResourceIds.All[index]);
        }

        private void PublishNetworkChanges()
        {
            for (int index = 0; index < ResourceIds.All.Length; index++)
            {
                string resourceId = ResourceIds.All[index];
                int delta = GetNetworkAmount(resourceId) - networkBefore[index];
                if (delta != 0) PublishChange(resourceId, delta);
            }
        }

        private void PublishChange(string resourceId, int delta)
        {
            if (delta != 0)
                NotifyAttributedChanged(
                    resourceId,
                    delta,
                    changeAttribution);
        }

        private void NotifyAttributedChanged(
            string resourceId,
            int delta,
            ResourceChangeAttribution attribution)
        {
            Action<string, int, ResourceChangeAttribution> handlers =
                AttributedChanged;
            if (handlers == null) return;
            Delegate[] subscribers = handlers.GetInvocationList();
            for (int index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<string, int, ResourceChangeAttribution>)
                        subscribers[index])(resourceId, delta, attribution);
                }
                catch (Exception failure)
                {
                    lastNotificationFailure = failure;
                }
            }
        }

        private void AdvanceRevision()
        {
            unchecked { Revision++; }
        }

        private sealed class StoragePlan
        {
            private readonly CityResourceStorageModel owner;

            public StoragePlan(
                CityResourceStorageModel owner,
                Dictionary<string, int> core,
                SortedDictionary<string, Dictionary<string, int>> warehouses)
            {
                this.owner = owner;
                Core = core;
                Warehouses = warehouses;
            }

            public Dictionary<string, int> Core { get; }
            public SortedDictionary<string, Dictionary<string, int>> Warehouses
                { get; }

            public int Add(
                string resourceId,
                int requestedAmount,
                string excludedWarehouseId = null)
            {
                int remaining = requestedAmount;
                remaining = AddToWarehouses(
                    resourceId,
                    remaining,
                    filtered: true,
                    excludedWarehouseId);
                remaining = AddToWarehouses(
                    resourceId,
                    remaining,
                    filtered: false,
                    excludedWarehouseId);
                int coreSpace = Math.Max(
                    0,
                    Math.Min(
                        owner.coreCapacityPerResource,
                        owner.coreInventory.CapacityPerResource) -
                    Core[resourceId]);
                int coreAccepted = Math.Min(remaining, coreSpace);
                Core[resourceId] += coreAccepted;
                remaining -= coreAccepted;
                return requestedAmount - remaining;
            }

            public bool TrySpend(string resourceId, int amount)
            {
                long total = Core[resourceId];
                foreach (KeyValuePair<string, WarehouseStorageState> item in
                         owner.warehouses)
                {
                    if (item.Value.IsConnected)
                        total += Get(Warehouses[item.Key], resourceId);
                }
                if (total < amount) return false;

                int remaining = amount;
                int fromCore = Math.Min(remaining, Core[resourceId]);
                Core[resourceId] -= fromCore;
                remaining -= fromCore;
                remaining = SpendFromWarehouses(
                    resourceId,
                    remaining,
                    filtered: true);
                remaining = SpendFromWarehouses(
                    resourceId,
                    remaining,
                    filtered: false);
                return remaining == 0;
            }

            private int AddToWarehouses(
                string resourceId,
                int remaining,
                bool filtered,
                string excludedWarehouseId)
            {
                if (remaining <= 0) return 0;
                foreach (KeyValuePair<string, WarehouseStorageState> item in
                         owner.warehouses)
                {
                    WarehouseStorageState state = item.Value;
                    if (string.Equals(
                            item.Key,
                            excludedWarehouseId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    bool hasFilter = !string.IsNullOrEmpty(
                        state.FilterResourceId);
                    if (!state.IsConnected || hasFilter != filtered ||
                        !state.CanAccept(resourceId))
                    {
                        continue;
                    }
                    Dictionary<string, int> values = Warehouses[item.Key];
                    long used = (long)Total(values) + state.OrphanAmount;
                    int accepted = Math.Min(
                        remaining,
                        (int)Math.Max(0L, state.Capacity - used));
                    if (accepted <= 0) continue;
                    values[resourceId] = Get(values, resourceId) + accepted;
                    remaining -= accepted;
                    if (remaining == 0) break;
                }
                return remaining;
            }

            private int SpendFromWarehouses(
                string resourceId,
                int remaining,
                bool filtered)
            {
                if (remaining <= 0) return 0;
                foreach (KeyValuePair<string, WarehouseStorageState> item in
                         owner.warehouses)
                {
                    WarehouseStorageState state = item.Value;
                    bool hasFilter = !string.IsNullOrEmpty(
                        state.FilterResourceId);
                    if (!state.IsConnected || hasFilter != filtered) continue;
                    Dictionary<string, int> values = Warehouses[item.Key];
                    int available = Get(values, resourceId);
                    int spent = Math.Min(remaining, available);
                    if (spent <= 0) continue;
                    int next = available - spent;
                    if (next == 0)
                        values.Remove(resourceId);
                    else
                        values[resourceId] = next;
                    remaining -= spent;
                    if (remaining == 0) break;
                }
                return remaining;
            }

            private static int Get(
                IReadOnlyDictionary<string, int> values,
                string resourceId)
            {
                return values.TryGetValue(resourceId, out int amount)
                    ? amount
                    : 0;
            }

            private static int Total(IReadOnlyDictionary<string, int> values)
            {
                int total = 0;
                foreach (KeyValuePair<string, int> item in values)
                    total += Math.Max(0, item.Value);
                return total;
            }
        }
    }

    public sealed class CityResourceStorageSnapshot
    {
        private readonly IReadOnlyDictionary<string, int> coreAmounts;
        private readonly IReadOnlyDictionary<string, int> networkAmounts;
        private readonly IReadOnlyDictionary<string, int> acceptableSpaces;
        private readonly IReadOnlyDictionary<string, WarehouseStorageSnapshot>
            warehouseById;

        internal CityResourceStorageSnapshot(
            ulong revision,
            int coreCapacityPerResource,
            IReadOnlyDictionary<string, int> core,
            IReadOnlyDictionary<string, int> network,
            IReadOnlyDictionary<string, int> acceptable,
            IList<WarehouseStorageSnapshot> warehouses)
        {
            Revision = revision;
            CoreCapacityPerResource = Math.Max(0, coreCapacityPerResource);
            coreAmounts = Copy(core);
            networkAmounts = Copy(network);
            acceptableSpaces = Copy(acceptable);
            var list = new List<WarehouseStorageSnapshot>(warehouses);
            Warehouses = new ReadOnlyCollection<WarehouseStorageSnapshot>(list);
            var lookup =
                new Dictionary<string, WarehouseStorageSnapshot>(
                    StringComparer.Ordinal);
            for (int index = 0; index < list.Count; index++)
                lookup.Add(list[index].StableInstanceId, list[index]);
            warehouseById =
                new ReadOnlyDictionary<string, WarehouseStorageSnapshot>(lookup);
        }

        public ulong Revision { get; }
        public int CoreCapacityPerResource { get; }
        public IReadOnlyList<WarehouseStorageSnapshot> Warehouses { get; }

        public int GetCoreAmount(string resourceId)
        {
            return Get(coreAmounts, resourceId);
        }

        public int GetNetworkAmount(string resourceId)
        {
            return Get(networkAmounts, resourceId);
        }

        public int GetNetworkAcceptableSpace(string resourceId)
        {
            return Get(acceptableSpaces, resourceId);
        }

        public int GetWarehouseAmount(
            string stableInstanceId,
            string resourceId)
        {
            return TryGetWarehouse(stableInstanceId, out WarehouseStorageSnapshot state)
                ? state.Get(resourceId)
                : 0;
        }

        public bool TryGetWarehouse(
            string stableInstanceId,
            out WarehouseStorageSnapshot state)
        {
            state = null;
            return !string.IsNullOrWhiteSpace(stableInstanceId) &&
                warehouseById.TryGetValue(stableInstanceId, out state);
        }

        private static IReadOnlyDictionary<string, int> Copy(
            IReadOnlyDictionary<string, int> source)
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source != null)
            {
                foreach (KeyValuePair<string, int> item in source)
                    copy.Add(item.Key, item.Value);
            }
            return new ReadOnlyDictionary<string, int>(copy);
        }

        private static int Get(
            IReadOnlyDictionary<string, int> source,
            string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                source.TryGetValue(resourceId, out int amount)
                    ? amount
                    : 0;
        }
    }
}
