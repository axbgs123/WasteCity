using System;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Combat;
namespace WasteCity.Persistence
{
    [Serializable] public sealed class FormalSaveData
    { public int schema=28, worldSeed, iron, energyCrystal, stone, biomass, water, alloy, ammunition, spiritIron, flyingSword, boneSteel, biomassConcentrate, biologicalWeapon, resonanceMetal, psionicAmplifier, elixir, population=100, populationCapacity=150, civilizationLevel=1, cityHealth=2000, day=1, foresightFlashedDay, hastePoolDay=1, hasteTargetX=-1, hasteTargetY=-1, cityMode, guidanceStage, legacyLevel=1, advancementStage, statsKills, statsProductionCycles, statsBuildingLosses, statsRescues, statsDelayedRescues, puppetLosses, behemothLosses, controlledLosses; public float cityX, cityY, observation, secondsIntoDay, hasteRemaining=60f, territoryProgress, researchRemaining, deploymentRemaining, advancementRemaining, leaderCooldown, leaderBoost, leaderLockout, technologyOverloadCooldown, technologyOverloadBoost, technologyOverloadLockout, statsElapsed, statsHighestObservation, puppetProgress, behemothProgress, rallyX, rallyY; public bool hasteActive, territoryActivated, bossDefeated, leaderRecruited, leaderInjured, statsRetreated, rallyFixed; public string legacyPathId, activeResearchId; public string[] completedResearchIds; public float[] productionProgress; public BuildingSnapshot[] buildings; public bool[] rescuedSites, worldRevealed; public int[] worldResourceAmounts, territoryLocalResources; public Legacy.SpatialTemplateEntry[] spatialTemplate; public WaveDirectorSnapshot wave; public EnemySnapshot[] enemies; public FriendlyUnitSnapshot[] puppets, behemoths; }
    public static class FormalSaveCodec
    {
        public static string Encode(FormalSaveData data) => JsonUtility.ToJson(data, true);
        public static FormalSaveData Decode(string json)
        { if(string.IsNullOrWhiteSpace(json)) return null; try { var d=JsonUtility.FromJson<FormalSaveData>(json); return d!=null&&(d.schema>=1&&d.schema<=28)?d:null; } catch { return null; } }
    }
}
