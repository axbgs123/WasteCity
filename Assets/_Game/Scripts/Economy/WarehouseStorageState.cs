using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Economy
{
    public sealed class WarehouseStorageState
    {
        public const int FormalCapacity = 150;

        private readonly Dictionary<string, int> amounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int orphanAmount;

        internal WarehouseStorageState(
            string stableInstanceId,
            bool connected,
            int capacity = FormalCapacity,
            bool preserveWhenDisconnected = false)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId))
                throw new ArgumentException(
                    "A stable warehouse instance ID is required.",
                    nameof(stableInstanceId));
            StableInstanceId = stableInstanceId;
            IsConnected = connected;
            Capacity = Math.Max(0, capacity);
            PreserveWhenDisconnected = preserveWhenDisconnected;
        }

        public string StableInstanceId { get; }
        public int Capacity { get; }
        public int TotalAmount { get; private set; }
        public int FreeSpace => Math.Max(0, Capacity - TotalAmount);
        public string FilterResourceId { get; private set; }
        public bool IsConnected { get; private set; }
        internal int OrphanAmount => orphanAmount;
        internal bool PreserveWhenDisconnected { get; private set; }

        public int Get(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                amounts.TryGetValue(resourceId, out int amount)
                    ? amount
                    : 0;
        }

        internal bool CanAccept(string resourceId)
        {
            return ResourceCapacityPolicy.IsRegisteredResource(resourceId) &&
                (string.IsNullOrEmpty(FilterResourceId) ||
                 string.Equals(
                     FilterResourceId,
                     resourceId,
                     StringComparison.Ordinal));
        }

        internal int Add(string resourceId, int requestedAmount)
        {
            if (!CanAccept(resourceId) || requestedAmount <= 0)
                return 0;
            int accepted = Math.Min(requestedAmount, FreeSpace);
            if (accepted <= 0) return 0;
            amounts.TryGetValue(resourceId, out int before);
            amounts[resourceId] = before + accepted;
            TotalAmount += accepted;
            return accepted;
        }

        internal bool TrySpend(string resourceId, int amount)
        {
            if (!ResourceCapacityPolicy.IsRegisteredResource(resourceId) ||
                amount < 0 || Get(resourceId) < amount)
            {
                return false;
            }
            if (amount == 0) return true;
            int remaining = Get(resourceId) - amount;
            if (remaining == 0)
                amounts.Remove(resourceId);
            else
                amounts[resourceId] = remaining;
            TotalAmount -= amount;
            return true;
        }

        internal bool TrySetFilter(string resourceId)
        {
            string normalized = string.IsNullOrWhiteSpace(resourceId)
                ? null
                : resourceId;
            if (normalized != null &&
                !ResourceCapacityPolicy.IsRegisteredResource(normalized))
            {
                return false;
            }
            if (string.Equals(
                    FilterResourceId,
                    normalized,
                    StringComparison.Ordinal))
            {
                return true;
            }
            if (normalized != null)
            {
                foreach (KeyValuePair<string, int> item in amounts)
                {
                    if (item.Value > 0 && !string.Equals(
                            item.Key,
                            normalized,
                            StringComparison.Ordinal))
                    {
                        return false;
                    }
                }
            }
            FilterResourceId = normalized;
            return true;
        }

        internal bool TryRestore(
            string filterResourceId,
            IReadOnlyDictionary<string, int> values,
            int restoredOrphanAmount,
            bool allowOverCapacity,
            bool preserveWhenDisconnected,
            out string error)
        {
            if (restoredOrphanAmount < 0)
            {
                error = "仓库孤立资源数量不能为负数";
                return false;
            }

            string normalizedFilter = string.IsNullOrWhiteSpace(filterResourceId)
                ? null
                : filterResourceId;
            var replacement = new Dictionary<string, int>(StringComparer.Ordinal);
            long total = restoredOrphanAmount;
            if (values != null)
            {
                foreach (KeyValuePair<string, int> item in values)
                {
                    if (!ResourceCapacityPolicy.IsRegisteredResource(item.Key) ||
                        item.Value < 0)
                    {
                        error = "仓库资源记录无效";
                        return false;
                    }
                    if (item.Value == 0) continue;
                    if (normalizedFilter != null &&
                        !string.Equals(
                            normalizedFilter,
                            item.Key,
                            StringComparison.Ordinal))
                    {
                        error = "仓库过滤与已有内容不兼容";
                        return false;
                    }
                    replacement.Add(item.Key, item.Value);
                    total += item.Value;
                    if (total > int.MaxValue)
                    {
                        error = "仓库资源总量溢出";
                        return false;
                    }
                }
            }
            if (!allowOverCapacity && total > Capacity)
            {
                error = "仓库内容超过当前共享容量";
                return false;
            }

            amounts.Clear();
            foreach (KeyValuePair<string, int> item in replacement)
                amounts.Add(item.Key, item.Value);
            orphanAmount = restoredOrphanAmount;
            TotalAmount = (int)total;
            FilterResourceId = normalizedFilter;
            IsConnected = false;
            PreserveWhenDisconnected = preserveWhenDisconnected;
            error = string.Empty;
            return true;
        }

        internal bool SetConnected(bool connected)
        {
            if (IsConnected == connected) return false;
            IsConnected = connected;
            return true;
        }

        internal Dictionary<string, int> CopyAmounts()
        {
            return new Dictionary<string, int>(amounts, StringComparer.Ordinal);
        }

        internal bool ReplaceAmounts(IReadOnlyDictionary<string, int> values)
        {
            bool changed = false;
            foreach (string resourceId in ResourceIds.All)
            {
                int next = values != null &&
                    values.TryGetValue(resourceId, out int amount)
                        ? Math.Max(0, amount)
                        : 0;
                if (Get(resourceId) != next)
                {
                    changed = true;
                    break;
                }
            }
            if (!changed) return false;

            amounts.Clear();
            TotalAmount = orphanAmount;
            if (values == null) return true;
            foreach (string resourceId in ResourceIds.All)
            {
                if (!values.TryGetValue(resourceId, out int amount) ||
                    amount <= 0)
                {
                    continue;
                }
                amounts.Add(resourceId, amount);
                TotalAmount += amount;
            }
            return true;
        }

        internal WarehouseStorageSnapshot CaptureSnapshot()
        {
            return new WarehouseStorageSnapshot(
                StableInstanceId,
                Capacity,
                FilterResourceId,
                IsConnected,
                orphanAmount,
                PreserveWhenDisconnected,
                amounts);
        }
    }

    public sealed class WarehouseStorageSnapshot
    {
        private readonly IReadOnlyDictionary<string, int> amounts;

        internal WarehouseStorageSnapshot(
            string stableInstanceId,
            int capacity,
            string filterResourceId,
            bool isConnected,
            int orphanAmount,
            bool preserveWhenDisconnected,
            IReadOnlyDictionary<string, int> source)
        {
            StableInstanceId = stableInstanceId;
            Capacity = Math.Max(0, capacity);
            FilterResourceId = filterResourceId;
            IsConnected = isConnected;
            OrphanAmount = Math.Max(0, orphanAmount);
            PreserveWhenDisconnected = preserveWhenDisconnected;
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            TotalAmount = OrphanAmount;
            if (source != null)
            {
                foreach (KeyValuePair<string, int> item in source)
                {
                    if (item.Value <= 0) continue;
                    copy.Add(item.Key, item.Value);
                    TotalAmount += item.Value;
                }
            }
            amounts = new ReadOnlyDictionary<string, int>(copy);
        }

        public string StableInstanceId { get; }
        public int Capacity { get; }
        public int TotalAmount { get; }
        public int FreeSpace => Math.Max(0, Capacity - TotalAmount);
        public string FilterResourceId { get; }
        public bool IsConnected { get; }
        public int OrphanAmount { get; }
        public bool PreserveWhenDisconnected { get; }
        public IReadOnlyDictionary<string, int> Amounts => amounts;

        public int Get(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) &&
                amounts.TryGetValue(resourceId, out int amount)
                    ? amount
                    : 0;
        }
    }
}
