using System;
using WasteCity.Economy;

namespace WasteCity.Graybox3D.Production
{
    public static class GrayboxResourcePresentationCatalog3D
    {
        public const string RootAssetPath =
            "Assets/_Game/Art/FirstPass/UI/ResourceIcons/";
        public const string FallbackIconAssetPath =
            RootAssetPath + "ResourceIcon_Unknown.png";

        public static readonly string[] ApprovedResourceIds =
        {
            ResourceIds.Iron,
            ResourceIds.Alloy,
            ResourceIds.Ammunition,
            ResourceIds.Stone,
            ResourceIds.Biomass,
            ResourceIds.EnergyCrystal,
            ResourceIds.Water
        };

        public static string IconAssetPath(string resourceId)
        {
            if (string.Equals(resourceId, ResourceIds.Iron,
                    StringComparison.Ordinal))
                return RootAssetPath + "ResourceIcon_Iron.png";
            if (string.Equals(resourceId, ResourceIds.Alloy,
                    StringComparison.Ordinal))
                return RootAssetPath + "ResourceIcon_Alloy.png";
            if (string.Equals(resourceId, ResourceIds.Ammunition,
                    StringComparison.Ordinal))
                return RootAssetPath + "ResourceIcon_Ammunition.png";
            if (string.Equals(resourceId, ResourceIds.Stone,
                    StringComparison.Ordinal))
                return RootAssetPath + "ResourceIcon_Stone.png";
            if (string.Equals(resourceId, ResourceIds.Biomass,
                    StringComparison.Ordinal))
                return RootAssetPath + "ResourceIcon_Biomass.png";
            if (string.Equals(resourceId, ResourceIds.EnergyCrystal,
                    StringComparison.Ordinal))
                return RootAssetPath + "ResourceIcon_EnergyCrystal.png";
            if (string.Equals(resourceId, ResourceIds.Water,
                    StringComparison.Ordinal))
                return RootAssetPath + "ResourceIcon_Water.png";
            return FallbackIconAssetPath;
        }
    }
}
