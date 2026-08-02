using System;

namespace WasteCity.Building
{
    public static class BuildingUnlockModel
    {
        public static bool IsUnlocked(BuildingDefinition definition,int population,Func<string,bool> researchCompleted,Func<string,int> completedBuildings,out string reason)
        {
            reason=null;if(definition==null){reason="无效建筑";return false;}
            if(population<definition.MinimumPopulation){reason=$"需要人口 {definition.MinimumPopulation}";return false;}
            if(!string.IsNullOrEmpty(definition.RequiredResearchId)&&(researchCompleted==null||!researchCompleted(definition.RequiredResearchId))){reason=$"需要研究 {definition.RequiredResearchId}";return false;}
            if(!string.IsNullOrEmpty(definition.RequiredBuildingId)&&(completedBuildings==null||completedBuildings(definition.RequiredBuildingId)<=0)){reason=$"需要先完成 {definition.RequiredBuildingId}";return false;}
            return true;
        }
    }
}
