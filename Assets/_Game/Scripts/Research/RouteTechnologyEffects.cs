using System;
using UnityEngine;
using WasteCity.Combat;
using WasteCity.Economy;

namespace WasteCity.Research
{
    public static class RouteTechnologyEffects
    {
        public static int BiomassDrop(int baseDrop, float qualityMultiplier, bool metabolicAcceleration)
        {
            int resolvedDrop = Mathf.RoundToInt(Math.Max(0, baseDrop) * Math.Max(0f, qualityMultiplier));
            return metabolicAcceleration ? resolvedDrop + Mathf.CeilToInt(resolvedDrop * .5f) : resolvedDrop;
        }

        public static float WarningMultiplier(bool precognitiveSense) => precognitiveSense ? 1.5f : 1f;
    }

    public sealed class BuildingRegenerationModel
    {
        private const float CarapaceIntervalSeconds = 5f;
        private float tissueRemainder;
        private float carapaceClock;

        public int Tick(float deltaTime, bool isWall, bool tissueRegeneration, bool carapaceGrowth, HealthModel health, ResourceInventory inventory)
        {
            if (health == null || health.IsDead || health.Current >= health.Maximum) return 0;
            int healed = 0;
            float delta = Math.Max(0f, deltaTime);
            if (tissueRegeneration)
            {
                tissueRemainder += delta;
                int amount = Mathf.FloorToInt(tissueRemainder);
                if (amount > 0) { healed += health.Heal(amount); tissueRemainder -= amount; }
            }
            if (!isWall || !carapaceGrowth || inventory == null || health.Current >= health.Maximum) return healed;
            carapaceClock += delta;
            while (carapaceClock >= CarapaceIntervalSeconds && health.Current < health.Maximum)
            {
                if (!inventory.TrySpend(ResourceIds.Biomass, 1)) { carapaceClock = CarapaceIntervalSeconds; break; }
                carapaceClock -= CarapaceIntervalSeconds;
                healed += health.Heal(10);
            }
            return healed;
        }
    }
}
