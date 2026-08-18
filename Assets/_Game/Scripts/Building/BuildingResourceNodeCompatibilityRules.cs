using System;
using WasteCity.Economy;

namespace WasteCity.Building
{
    public static class BuildingResourceNodeCompatibilityRules
    {
        public static bool IsCompatible(
            BuildingDefinition definition,
            string resourceId)
        {
            return definition != null &&
                string.Equals(
                    definition.Id.Value,
                    BuildingCatalog.MiningStation.Id.Value,
                    StringComparison.Ordinal) &&
                (string.Equals(
                     resourceId,
                     ResourceIds.Iron,
                     StringComparison.Ordinal) ||
                 string.Equals(
                     resourceId,
                     ResourceIds.EnergyCrystal,
                     StringComparison.Ordinal) ||
                 string.Equals(
                     resourceId,
                     ResourceIds.Stone,
                     StringComparison.Ordinal));
        }
    }
}
