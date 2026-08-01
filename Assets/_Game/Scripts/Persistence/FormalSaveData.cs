using System;
using UnityEngine;
using WasteCity.Building;
namespace WasteCity.Persistence
{
    [Serializable] public sealed class FormalSaveData
    { public int schema=10, worldSeed, iron, energyCrystal, stone, biomass, water, alloy, ammunition, population=100, populationCapacity=150, civilizationLevel=1, cityHealth=2000, day=1, foresightFlashedDay, hastePoolDay=1, hasteTargetX=-1, hasteTargetY=-1; public float cityX, cityY, observation, secondsIntoDay, hasteRemaining=60f, territoryProgress, researchRemaining; public bool hasteActive, territoryActivated; public string legacyPathId, activeResearchId; public string[] completedResearchIds; public BuildingSnapshot[] buildings; public bool[] rescuedSites, worldRevealed; public int[] worldResourceAmounts, territoryLocalResources; public Legacy.SpatialTemplateEntry[] spatialTemplate; }
    public static class FormalSaveCodec
    {
        public static string Encode(FormalSaveData data) => JsonUtility.ToJson(data, true);
        public static FormalSaveData Decode(string json)
        { if(string.IsNullOrWhiteSpace(json)) return null; try { var d=JsonUtility.FromJson<FormalSaveData>(json); return d!=null&&(d.schema>=1&&d.schema<=10)?d:null; } catch { return null; } }
    }
}
