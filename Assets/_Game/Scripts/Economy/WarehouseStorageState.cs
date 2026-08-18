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

        internal WarehouseStorageState(
            string stableInstanceId,
            bool connected,
            int capacity = FormalCapacity)
        {
            if (string.IsNullOrWhiteSpace(stableInstanceId))
                throw new ArgumentException(
                    "A stable warehouse instance ID is required.",
                    nameof(stableInstanceId));
            StableInstanceId = stableInstanceId;
            IsConnected = connected;
            Capacity = Math.Max(0, capacity);
        }

        public string StableInstanceId { get; }
        public int Capacity { get; }
        public int TotalAmount { get; private set; }
        public int FreeSpace => Math.Max(0, Capacity - TotalAmount);
        public string FilterResourceId { get; private set; }
        public bool IsConnected { get; private set; }

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
            TotalAmount = 0;
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
            if (TotalAmount > Capacity)
                throw new InvalidOperationException(
                    "Warehouse contents exceed shared capacity.");
            return true;
        }

        internal WarehouseStorageSnapshot CaptureSnapshot()
        {
            return new WarehouseStorageSnapshot(
                StableInstanceId,
                Capacity,
                FilterResourceId,
                IsConnected,
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
            IReadOnlyDictionary<string, int> source)
        {
            StableInstanceId = stableInstanceId;
            Capacity = Math.Max(0, capacity);
            FilterResourceId = filterResourceId;
            IsConnected = isConnected;
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            TotalAmount = 0;
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
