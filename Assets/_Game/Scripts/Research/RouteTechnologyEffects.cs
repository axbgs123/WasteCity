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
            return BiomassDrop(
                baseDrop,
                qualityMultiplier,
                metabolicAcceleration ? 1.5f : 1f);
        }

        public static int BiomassDrop(
            int baseDrop,
            float qualityMultiplier,
            float recoveryMultiplier)
        {
            int resolvedDrop = Mathf.RoundToInt(Math.Max(0, baseDrop) * Math.Max(0f, qualityMultiplier));
            return resolvedDrop + Mathf.CeilToInt(
                resolvedDrop * Math.Max(0f, recoveryMultiplier - 1f));
        }

        public static float WarningMultiplier(bool precognitiveSense) => precognitiveSense ? 1.5f : 1f;
        public static int BuildingMaximumHealth(int baseHealth, bool alloyArmor) => Mathf.RoundToInt(Math.Max(1, baseHealth) * (alloyArmor ? 1.3f : 1f));
        public static int PhysicalDamagePercent(string buildingId, bool talismanBasics) => talismanBasics && buildingId == "core.building.wall" ? 80 : -1;
        public static float TowerRangeMultiplier(string buildingId, bool swordRiding) => swordRiding && (buildingId == "cultivation.building.sword-array-tower" || buildingId == "cultivation.building.sword-riding-platform") ? 1.3f : 1f;
        public static float TowerDamageMultiplier(
            string buildingId,
            bool automatedDefense,
            bool swordArray)
        {
            if (automatedDefense &&
                (buildingId == "core.building.machine-gun-turret" ||
                 buildingId == "core.building.heavy-machine-gun-turret"))
            {
                return 1f / .9f;
            }
            if (swordArray &&
                (buildingId == "cultivation.building.sword-array-tower" ||
                 buildingId == "cultivation.building.sword-riding-platform"))
            {
                return 1.15f;
            }
            return 1f;
        }
        public static int LogisticsRange(bool formationReinforcement, bool orbitalSupply) => orbitalSupply ? 24 : formationReinforcement ? 12 : 8;
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
