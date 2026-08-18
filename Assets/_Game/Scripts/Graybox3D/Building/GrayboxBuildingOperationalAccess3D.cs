using WasteCity.Building;
using WasteCity.City;

namespace WasteCity.Graybox3D.Building
{
    public static class GrayboxBuildingOperationalAccess3D
    {
        public static bool CanRetainState(
            GrayboxBuildingInstance3D instance)
        {
            return instance != null &&
                instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned &&
                instance.Placement?.Definition != null;
        }

        public static bool CanRunLocally(
            GrayboxBuildingInstance3D instance,
            CityMode cityMode)
        {
            if (!CanRetainState(instance) || instance.IsEvacuationLocked)
                return false;
            return instance.Placement.Site == BuildingSite.Ground ||
                BuildingMobilityRules.CanOperate(
                    instance.Placement.Definition,
                    instance.Placement.Site,
                    cityMode);
        }

        public static bool IsLogisticsConnected(
            GrayboxBuildingInstance3D instance,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius)
        {
            if (!CanRetainState(instance)) return false;

            PlacedBuilding placement = instance.Placement;
            if (placement.Site == BuildingSite.InnerCity)
                return true;
            if (placement.Site != BuildingSite.Ground ||
                cityMode != CityMode.Fortress)
            {
                return false;
            }

            return BuildingRangeRules.IsGroundFootprintInRange(
                placement.Definition,
                placement.X,
                placement.Y,
                placement.Orientation,
                cityX,
                cityY,
                groundRadius);
        }
    }
}
