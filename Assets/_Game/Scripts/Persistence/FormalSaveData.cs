using System;
using UnityEngine;
using WasteCity.Building;
namespace WasteCity.Persistence
{
    [Serializable] public sealed class FormalSaveData
    { public int schema=5, worldSeed, iron, energyCrystal, stone, biomass, water, alloy, ammunition, population=100, populationCapacity=150, civilizationLevel=1, cityHealth=2000; public float cityX, cityY, observation; public string legacyPathId; public BuildingSnapshot[] buildings; public bool[] rescuedSites; }
    public static class FormalSaveCodec
    {
        public static string Encode(FormalSaveData data) => JsonUtility.ToJson(data, true);
        public static FormalSaveData Decode(string json)
        { if(string.IsNullOrWhiteSpace(json)) return null; try { var d=JsonUtility.FromJson<FormalSaveData>(json); return d!=null&&(d.schema>=1&&d.schema<=5)?d:null; } catch { return null; } }
    }
}
