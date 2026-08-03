using System;
using WasteCity.Economy;

namespace WasteCity.Combat
{
    public sealed class BehemothBreedingModel
    {
        public const float SecondsPerUnit = 35f;
        public float Progress { get; private set; }
        public int Capacity(int completedPens) => Math.Max(0, completedPens);

        public int Tick(float deltaSeconds, int completedPens, int currentUnits, ResourceInventory inventory)
        {
            if (completedPens <= 0 || inventory == null || currentUnits >= Capacity(completedPens)) return 0;
            Progress = Math.Min(SecondsPerUnit, Progress + Math.Max(0f, deltaSeconds) * completedPens);
            if (Progress < SecondsPerUnit || !inventory.CanSpend(ResourceIds.BoneSteel, 2) || !inventory.CanSpend(ResourceIds.BiomassConcentrate, 3)) return 0;
            inventory.TrySpend(ResourceIds.BoneSteel, 2); inventory.TrySpend(ResourceIds.BiomassConcentrate, 3); Progress -= SecondsPerUnit; return 1;
        }

        public void Restore(float progress) => Progress = Math.Max(0f, Math.Min(SecondsPerUnit, progress));
    }
}
