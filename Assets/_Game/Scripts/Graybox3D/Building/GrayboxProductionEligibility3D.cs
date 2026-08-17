using WasteCity.Building;

namespace WasteCity.Graybox3D.Building
{
    public static class GrayboxProductionEligibility3D
    {
        public static bool IsActiveWarehouse(
            GrayboxBuildingInstance3D instance)
        {
            return instance != null &&
                instance.State == GrayboxBuildingInstanceState.Completed &&
                instance.IsPlayerOwned &&
                !instance.IsEvacuationLocked &&
                instance.Placement?.Definition != null &&
                string.Equals(
                    instance.Placement.Definition.Id.Value,
                    BuildingCatalog.Warehouse.Id.Value,
                    System.StringComparison.Ordinal);
        }
    }
}
