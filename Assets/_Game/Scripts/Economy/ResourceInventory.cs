using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public sealed class ResourceInventory
    {
        private readonly Dictionary<string, int> values = new Dictionary<string, int>();
        private int capacityPerResource;
        private int debtLimit;
        public event Action<int> DebtIncreased;
        public int CapacityPerResource => capacityPerResource;
        public ResourceInventory(int capacityPerResource) => this.capacityPerResource = Math.Max(0, capacityPerResource);
        public int Get(string id) => values.TryGetValue(id, out int value) ? value : 0;
        public int Add(string id, int amount)
        {
            if (string.IsNullOrWhiteSpace(id) || amount <= 0) return 0;
            int accepted = Math.Min(amount, capacityPerResource - Get(id));
            values[id] = Get(id) + accepted; return accepted;
        }
        public bool TrySpend(string id, int amount)
        {
            if (amount < 0 || Get(id) - amount < -debtLimit) return false;
            int before = Get(id); values[id] = before - amount;
            if (values[id] < before && values[id] < 0) DebtIncreased?.Invoke(Math.Min(amount, -values[id]));
            return true;
        }
        public bool CanSpend(string id, int amount) => amount >= 0 && Get(id) - amount >= -debtLimit;
        public void Set(string id, int amount) => values[id] = Math.Max(0, Math.Min(capacityPerResource, amount));
        public void AddCapacity(int amount)
        {
            capacityPerResource = Math.Max(0, capacityPerResource + amount);
            if (amount >= 0) return;
            foreach (string id in new List<string>(values.Keys)) values[id] = Math.Min(values[id], capacityPerResource);
        }
        public void SetDebtLimit(int amount) => debtLimit = Math.Max(0, amount);
        public void Restore(string id, int amount) => values[id] = Math.Max(-debtLimit, Math.Min(capacityPerResource, amount));
    }
}
