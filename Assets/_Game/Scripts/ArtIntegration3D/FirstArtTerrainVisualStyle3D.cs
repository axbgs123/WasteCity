using System;
using UnityEngine;

namespace WasteCity.ArtIntegration3D
{
    /// <summary>
    /// Presentation-only cartographic palette for the seven frozen terrain layers.
    /// It does not own terrain, traversal, resource, placement, or save truth.
    /// </summary>
    public static class FirstArtTerrainVisualStyleCatalog3D
    {
        public static Color MapColorOf(FirstArtTerrainLayer3D layer)
        {
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Wasteland:
                    return new Color(.58f, .35f, .14f, 1f);
                case FirstArtTerrainLayer3D.Rocky:
                    return new Color(.33f, .37f, .42f, 1f);
                case FirstArtTerrainLayer3D.Wetland:
                    return new Color(.31f, .40f, .20f, 1f);
                case FirstArtTerrainLayer3D.Crystal:
                    return new Color(.38f, .66f, .70f, 1f);
                case FirstArtTerrainLayer3D.Ruins:
                    return new Color(.43f, .40f, .37f, 1f);
                case FirstArtTerrainLayer3D.DeepWater:
                    return new Color(.04f, .12f, .22f, 1f);
                case FirstArtTerrainLayer3D.Cliff:
                    return new Color(.24f, .19f, .15f, 1f);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(layer),
                        layer,
                        "Unknown first-art terrain layer.");
            }
        }

        public static float TintStrengthOf(FirstArtTerrainLayer3D layer)
        {
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Wasteland:
                    return .18f;
                case FirstArtTerrainLayer3D.Rocky:
                    return .26f;
                case FirstArtTerrainLayer3D.Wetland:
                    return .30f;
                case FirstArtTerrainLayer3D.Crystal:
                    return .24f;
                case FirstArtTerrainLayer3D.Ruins:
                    return .22f;
                case FirstArtTerrainLayer3D.DeepWater:
                    return .62f;
                case FirstArtTerrainLayer3D.Cliff:
                    return .30f;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(layer),
                        layer,
                        "Unknown first-art terrain layer.");
            }
        }

        public static Color MaterialTintOf(FirstArtTerrainLayer3D layer)
        {
            Color color = MapColorOf(layer);
            color.a = TintStrengthOf(layer);
            return color;
        }
    }
}
