using System;
using UnityEngine;

namespace WasteCity.ArtIntegration3D
{
    [CreateAssetMenu(menuName = "WasteCity/Art/First Terrain Profile")]
    public sealed class FirstArtTerrainProfile3D : ScriptableObject
    {
        public const string RequiredShaderName =
            "WasteCity/Terrain/FirstPassBlend";
        public const int DefaultControlPixelsPerCell = 4;
        public const float DefaultCellsPerTexture = 4f;

        [SerializeField] private Material material;
        [SerializeField] private Texture2DArray baseColorArray;
        [SerializeField] private Texture2DArray normalArray;
        [SerializeField] private Texture2DArray maskArray;
        [SerializeField] private Texture2DArray heightArray;
        [SerializeField] private int controlPixelsPerCell = DefaultControlPixelsPerCell;
        [SerializeField] private float cellsPerTexture = DefaultCellsPerTexture;
        [SerializeField] private float heightBlendStrength = 0.5f;
        [SerializeField] private Vector2 waterNormalVelocityA = new Vector2(0.04f, 0.02f);
        [SerializeField] private Vector2 waterNormalVelocityB = new Vector2(-0.025f, 0.05f);

        public Material Material => material;
        public Texture2DArray BaseColorArray => baseColorArray;
        public Texture2DArray NormalArray => normalArray;
        public Texture2DArray MaskArray => maskArray;
        public Texture2DArray HeightArray => heightArray;
        public int ControlPixelsPerCell => controlPixelsPerCell;
        public float CellsPerTexture => cellsPerTexture;
        public float HeightBlendStrength => heightBlendStrength;
        public Vector2 WaterNormalVelocityA => waterNormalVelocityA;
        public Vector2 WaterNormalVelocityB => waterNormalVelocityB;

        public float BlendWidth(
            FirstArtTerrainLayer3D left,
            FirstArtTerrainLayer3D right)
        {
            ValidateLayer(left, nameof(left));
            ValidateLayer(right, nameof(right));
            if (left == right)
                return 0f;

            float leftSpecialWidth = SpecialBlendWidth(left);
            float rightSpecialWidth = SpecialBlendWidth(right);
            if (leftSpecialWidth > 0f && rightSpecialWidth > 0f)
                return Mathf.Min(leftSpecialWidth, rightSpecialWidth);
            if (leftSpecialWidth > 0f)
                return leftSpecialWidth;
            if (rightSpecialWidth > 0f)
                return rightSpecialWidth;

            return Mathf.Min(BaseBlendWidth(left), BaseBlendWidth(right));
        }

        public void Configure(
            Material material,
            Texture2DArray baseColorArray,
            Texture2DArray normalArray,
            Texture2DArray maskArray,
            Texture2DArray heightArray)
        {
            this.material = material;
            this.baseColorArray = baseColorArray;
            this.normalArray = normalArray;
            this.maskArray = maskArray;
            this.heightArray = heightArray;
        }

        public bool TryValidateControlSettings(out string error)
        {
            if (controlPixelsPerCell <= 0)
            {
                error = "Control pixels per cell must be positive.";
                return false;
            }

            if (cellsPerTexture <= 0f || float.IsNaN(cellsPerTexture) || float.IsInfinity(cellsPerTexture))
            {
                error = "Cells per texture must be a positive finite value.";
                return false;
            }

            for (int left = 0; left < FirstArtTerrainCatalog3D.LayerCount; left++)
            {
                for (int right = left + 1; right < FirstArtTerrainCatalog3D.LayerCount; right++)
                {
                    if (BlendWidth((FirstArtTerrainLayer3D)left, (FirstArtTerrainLayer3D)right) <= 0f)
                    {
                        error = "Every distinct terrain-layer pair must have a positive blend width.";
                        return false;
                    }
                }
            }

            if (heightBlendStrength < 0f || float.IsNaN(heightBlendStrength) || float.IsInfinity(heightBlendStrength))
            {
                error = "Height blend strength must be non-negative and finite.";
                return false;
            }

            if (waterNormalVelocityA.sqrMagnitude <= 0f ||
                waterNormalVelocityB.sqrMagnitude <= 0f ||
                Mathf.Abs(
                    waterNormalVelocityA.x * waterNormalVelocityB.y -
                    waterNormalVelocityA.y * waterNormalVelocityB.x) <= 0.000001f)
            {
                error = "Water normal velocities must be non-zero and non-parallel.";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryValidate(out string error)
        {
            if (!TryValidateControlSettings(out error))
                return false;
            if (material == null)
            {
                error = "Material is required.";
                return false;
            }
            if (material.shader == null || material.shader.name != RequiredShaderName)
            {
                error = "Material must use shader " + RequiredShaderName + ".";
                return false;
            }
            if (baseColorArray == null)
            {
                error = "BaseColor array is required.";
                return false;
            }
            if (normalArray == null)
            {
                error = "Normal array is required.";
                return false;
            }
            if (maskArray == null)
            {
                error = "Mask array is required.";
                return false;
            }
            if (heightArray == null)
            {
                error = "Height array is required.";
                return false;
            }
            if (baseColorArray.depth != FirstArtTerrainCatalog3D.LayerCount ||
                normalArray.depth != FirstArtTerrainCatalog3D.LayerCount ||
                maskArray.depth != FirstArtTerrainCatalog3D.LayerCount ||
                heightArray.depth != FirstArtTerrainCatalog3D.LayerCount)
            {
                error = "Each terrain channel array depth must be 7.";
                return false;
            }
            if (normalArray.width != baseColorArray.width || normalArray.height != baseColorArray.height ||
                maskArray.width != baseColorArray.width || maskArray.height != baseColorArray.height ||
                heightArray.width != baseColorArray.width || heightArray.height != baseColorArray.height)
            {
                error = "Terrain channel array dimensions must match.";
                return false;
            }

            error = null;
            return true;
        }

        private static float BaseBlendWidth(FirstArtTerrainLayer3D layer)
        {
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Wasteland:
                    return float.MaxValue;
                case FirstArtTerrainLayer3D.Rocky:
                    return 1.25f;
                case FirstArtTerrainLayer3D.Wetland:
                    return 1.15f;
                case FirstArtTerrainLayer3D.Crystal:
                    return 1f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "Layer is not a base terrain layer.");
            }
        }

        private static float SpecialBlendWidth(FirstArtTerrainLayer3D layer)
        {
            switch (layer)
            {
                case FirstArtTerrainLayer3D.Ruins:
                    return 0.75f;
                case FirstArtTerrainLayer3D.DeepWater:
                    return 0.425f;
                case FirstArtTerrainLayer3D.Cliff:
                    return 0.35f;
                case FirstArtTerrainLayer3D.Wasteland:
                case FirstArtTerrainLayer3D.Rocky:
                case FirstArtTerrainLayer3D.Wetland:
                case FirstArtTerrainLayer3D.Crystal:
                    return 0f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown first-art terrain layer.");
            }
        }

        private static void ValidateLayer(FirstArtTerrainLayer3D layer, string parameterName)
        {
            if ((int)layer < 0 || (int)layer >= FirstArtTerrainCatalog3D.LayerCount)
                throw new ArgumentOutOfRangeException(parameterName, layer, "Unknown first-art terrain layer.");
        }
    }
}
