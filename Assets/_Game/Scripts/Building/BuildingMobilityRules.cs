using System;
using WasteCity.City;

namespace WasteCity.Building
{
    public enum BuildingPlacement
    {
        Ground = 0,
        InnerCity = 1,
        Either = 2
    }

    public enum BuildingOperation
    {
        MobileAllowed = 0,
        FortressOnly = 1,
        TerrainDependent = 2
    }

    public enum BuildingSite
    {
        Ground = 0,
        InnerCity = 1
    }

    public static class BuildingMobilityRules
    {
        public static bool SupportsSite(BuildingDefinition definition, BuildingSite site)
        {
            if (definition == null || !Enum.IsDefined(typeof(BuildingSite), site)) return false;
            return definition.Placement == BuildingPlacement.Either ||
                   (definition.Placement == BuildingPlacement.Ground && site == BuildingSite.Ground) ||
                   (definition.Placement == BuildingPlacement.InnerCity && site == BuildingSite.InnerCity);
        }

        public static bool CanConstruct(BuildingDefinition definition, BuildingSite site, CityMode mode)
        {
            if (!SupportsSite(definition, site)) return false;
            if (mode == CityMode.Fortress) return true;
            return mode == CityMode.Mobile &&
                   site == BuildingSite.InnerCity &&
                   definition.Operation == BuildingOperation.MobileAllowed;
        }

        public static bool CanOperate(BuildingDefinition definition, BuildingSite site, CityMode mode)
        {
            return CanConstruct(definition, site, mode);
        }

        public static string PlacementName(BuildingPlacement placement)
        {
            switch (placement)
            {
                case BuildingPlacement.InnerCity: return "内城";
                case BuildingPlacement.Either: return "两者皆可";
                default: return "地面";
            }
        }

        public static string OperationName(BuildingOperation operation)
        {
            switch (operation)
            {
                case BuildingOperation.MobileAllowed: return "移动可运行";
                case BuildingOperation.TerrainDependent: return "地形依赖";
                default: return "仅展开运行";
            }
        }
    }
}
