using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public sealed class ResourceInventory
    {
        private readonly Dictionary<string, int> values = new Dictionary<string, int>();
        private readonly int capacityPerResource;
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
            if (amount < 0 || Get(id) < amount) return false;
            values[id] = Get(id) - amount; return true;
        }
        public void Set(string id, int amount) => values[id] = Math.Max(0, Math.Min(capacityPerResource, amount));
    }
}
