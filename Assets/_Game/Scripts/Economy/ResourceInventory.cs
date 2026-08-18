using System;
using System.Collections.Generic;

namespace WasteCity.Economy
{
    public enum ResourceChangeAttributionKind
    {
        Unspecified,
        Production,
        Backpack,
        Research,
    }

    public readonly struct ResourceChangeAttribution
    {
        public ResourceChangeAttribution(
            ResourceChangeAttributionKind kind,
            string referenceId = null)
        {
            Kind = kind;
            ReferenceId = referenceId;
        }

        public ResourceChangeAttributionKind Kind { get; }
        public string ReferenceId { get; }
    }

    public readonly struct ResourceChangeAttributionScope : IDisposable
    {
        private readonly ResourceInventory owner;
        private readonly ResourceChangeAttribution previous;

        internal ResourceChangeAttributionScope(
            ResourceInventory owner,
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

    public sealed class ResourceInventory
    {
        private readonly Dictionary<string, int> values = new Dictionary<string, int>();
        private int capacityPerResource;
        private int debtLimit;
        private ResourceChangeAttribution changeAttribution;
        public event Action<int> DebtIncreased;
        public event Action<string, int> Changed;
        public event Action<string, int, ResourceChangeAttribution>
            AttributedChanged;
        public int CapacityPerResource => capacityPerResource;
        public ResourceInventory(int capacityPerResource) => this.capacityPerResource = Math.Max(0, capacityPerResource);
        public int Get(string id) => values.TryGetValue(id, out int value) ? value : 0;
        public int Add(string id, int amount)
        {
            if (string.IsNullOrWhiteSpace(id) || amount <= 0) return 0;
            int before = Get(id);
            int accepted = Math.Min(amount, capacityPerResource - before);
            values[id] = before + accepted;
            PublishChange(id, accepted);
            return accepted;
        }
        public bool TrySpend(string id, int amount)
        {
            if (amount < 0 || Get(id) - amount < -debtLimit) return false;
            int before = Get(id); values[id] = before - amount;
            if (values[id] < before && values[id] < 0) DebtIncreased?.Invoke(Math.Min(amount, -values[id]));
            PublishChange(id, values[id] - before);
            return true;
        }
        public bool CanSpend(string id, int amount) => amount >= 0 && Get(id) - amount >= -debtLimit;
        public void Set(string id, int amount)
        {
            int before = Get(id);
            values[id] = Math.Max(0, Math.Min(capacityPerResource, amount));
            PublishChange(id, values[id] - before);
        }
        public void AddCapacity(int amount)
        {
            capacityPerResource = Math.Max(0, capacityPerResource + amount);
            if (amount >= 0) return;
            foreach (string id in new List<string>(values.Keys))
            {
                int before = values[id];
                values[id] = Math.Min(before, capacityPerResource);
                PublishChange(id, values[id] - before);
            }
        }
        public void SetDebtLimit(int amount) => debtLimit = Math.Max(0, amount);
        public void Restore(string id, int amount)
        {
            int before = Get(id);
            values[id] = Math.Max(-debtLimit, Math.Min(capacityPerResource, amount));
            PublishChange(id, values[id] - before);
        }

        public ResourceChangeAttributionScope AttributeChanges(
            ResourceChangeAttribution attribution)
        {
            ResourceChangeAttribution previous = changeAttribution;
            changeAttribution = attribution;
            return new ResourceChangeAttributionScope(this, previous);
        }

        internal void RestoreChangeAttribution(
            ResourceChangeAttribution attribution)
        {
            changeAttribution = attribution;
        }

        private void PublishChange(string id, int delta)
        {
            if (delta == 0) return;
            Changed?.Invoke(id, delta);
            AttributedChanged?.Invoke(id, delta, changeAttribution);
        }
    }
}
