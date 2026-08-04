using System;
using WasteCity.Economy;

namespace WasteCity.Combat
{
    public sealed class PuppetMaintenanceModel
    {
        public const float CycleSeconds = 60f;

        public float Elapsed { get; private set; }
        public bool Active { get; private set; } = true;

        public bool Tick(float deltaSeconds, ResourceInventory inventory)
        {
            if (inventory == null)
            {
                Elapsed = 0f;
                Active = true;
                return false;
            }

            Elapsed += Math.Max(0f, deltaSeconds);
            bool paid = false;
            while (Elapsed + .00001f >= CycleSeconds)
            {
                if (!inventory.TrySpend(ResourceIds.EnergyCrystal, 1))
                {
                    Elapsed = CycleSeconds;
                    Active = false;
                    return paid;
                }

                Elapsed -= CycleSeconds;
                Active = true;
                paid = true;
            }

            return paid;
        }

        public void Restore(float elapsed, bool active)
        {
            Elapsed = Math.Max(0f, Math.Min(CycleSeconds, elapsed));
            Active = active;
            if (!Active) Elapsed = CycleSeconds;
        }
    }
}
