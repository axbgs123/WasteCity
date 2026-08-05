using System;
using UnityEngine;
using WasteCity.Content;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxVisualSlot : MonoBehaviour
    {
        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");

        [SerializeField] private string stableId;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Color fallbackColor = Color.white;

        private MaterialPropertyBlock propertyBlock;

        public string StableId => stableId;
        public MeshRenderer Renderer => meshRenderer;
        public Color FallbackColor => fallbackColor;

        public void Configure(
            string stableId,
            MeshRenderer renderer,
            Color fallbackColor)
        {
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));

            this.stableId = new StableId(stableId).Value;
            meshRenderer = renderer;
            this.fallbackColor = fallbackColor;
        }

        public void ApplyFallback(Material sharedMaterial)
        {
            if (meshRenderer == null)
                throw new InvalidOperationException(
                    "Configure the graybox visual slot before applying it.");
            if (sharedMaterial == null)
                throw new ArgumentNullException(nameof(sharedMaterial));

            meshRenderer.sharedMaterial = sharedMaterial;
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, fallbackColor);
            propertyBlock.SetColor(ColorId, fallbackColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }

        private void OnDestroy()
        {
            propertyBlock = null;
        }
    }
}
