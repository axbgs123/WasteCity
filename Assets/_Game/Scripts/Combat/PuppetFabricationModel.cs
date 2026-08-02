using System;
using WasteCity.Economy;

namespace WasteCity.Combat
{
    public sealed class PuppetFabricationModel
    {
        public const float SecondsPerUnit = 20f;
        public const int UnitsPerWorkshop = 3;
        public float Progress { get; private set; }

        public int Capacity(int completedWorkshops) => Math.Max(0, completedWorkshops) * UnitsPerWorkshop;

        public int Tick(float deltaSeconds, int completedWorkshops, int currentUnits, ResourceInventory inventory)
        {
            if (completedWorkshops <= 0 || inventory == null) return 0;
            int capacity = Capacity(completedWorkshops);
            if (currentUnits >= capacity) return 0;
            Progress = Math.Min(SecondsPerUnit, Progress + Math.Max(0f, deltaSeconds) * completedWorkshops);
            int produced = 0;
            while (Progress >= SecondsPerUnit && currentUnits + produced < capacity)
            {
                if (!inventory.CanSpend(ResourceIds.Alloy, 1) || !inventory.CanSpend(ResourceIds.SpiritIron, 1)) break;
                inventory.TrySpend(ResourceIds.Alloy, 1);
                inventory.TrySpend(ResourceIds.SpiritIron, 1);
                Progress -= SecondsPerUnit;
                produced++;
            }
            return produced;
        }

        public void Restore(float progress) => Progress = Math.Max(0f, Math.Min(SecondsPerUnit, progress));
    }
}
