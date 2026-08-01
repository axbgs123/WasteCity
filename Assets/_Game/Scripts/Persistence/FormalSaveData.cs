using System;
using UnityEngine;
namespace WasteCity.Persistence
{
    [Serializable] public sealed class FormalSaveData
    { public int schema=1, worldSeed, iron, energyCrystal, stone, biomass, water; public float cityX, cityY; public string legacyPathId; }
    public static class FormalSaveCodec
    {
        public static string Encode(FormalSaveData data) => JsonUtility.ToJson(data, true);
        public static FormalSaveData Decode(string json)
        { if(string.IsNullOrWhiteSpace(json)) return null; try { var d=JsonUtility.FromJson<FormalSaveData>(json); return d!=null&&d.schema==1?d:null; } catch { return null; } }
    }
}
