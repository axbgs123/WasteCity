using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxResourceNodeMarker3D : MonoBehaviour
    {
        private static readonly Dictionary<Sprite, Mesh> IconMeshes =
            new Dictionary<Sprite, Mesh>();
        private static readonly Dictionary<Texture, Material> IconMaterials =
            new Dictionary<Texture, Material>();

        private MeshFilter iconFilter;
        private MeshRenderer iconRenderer;
        private MaterialPropertyBlock iconProperties;
        private MeshFilter frameFilter;
        private MeshRenderer frameRenderer;
        private Sprite icon;
        private Sprite frame;
        private TextMesh amountLabel;
        private string resourceDisplayName;

        public string StableId { get; private set; }
        public string ResourceId { get; private set; }
        public int WorldX { get; private set; }
        public int WorldY { get; private set; }
        public int DisplayedAmount { get; private set; } = -1;
        public string DisplayText => amountLabel == null
            ? string.Empty
            : amountLabel.text;
        public Sprite Icon => icon;
        public Sprite Frame => frame;
        public ResourceNodeMarkerLod3D DisplayLod { get; private set; } =
            ResourceNodeMarkerLod3D.Near;
        public bool GuidanceOverride { get; private set; }

        public void Configure(
            string stableId,
            string resourceId,
            int worldX,
            int worldY,
            Vector3 worldPosition,
            Sprite icon)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                throw new ArgumentException(
                    "A stable resource node ID is required.",
                    nameof(stableId));
            if (!ResourceDefinitionCatalog.TryGet(
                    resourceId,
                    out ResourceDefinition definition))
                throw new ArgumentException(
                    "A registered resource ID is required.",
                    nameof(resourceId));
            StableId = stableId;
            ResourceId = definition.Id;
            resourceDisplayName = definition.ChineseName;
            WorldX = worldX;
            WorldY = worldY;
            transform.position = worldPosition + Vector3.up * 1.05f;
            EnsurePresentation();
            SetFrame(Production2DVisualCatalog3D.Resolve(
                Production2DVisualClass.WorldMarker,
                "core.world-marker.resource-node"));
            SetIcon(icon);
        }

        public bool Refresh(WorldCell cell)
        {
            if (!string.Equals(
                    cell.ResourceId,
                    ResourceId,
                    StringComparison.Ordinal))
                return false;
            int amount = Math.Max(0, cell.ResourceAmount);
            if (amount == DisplayedAmount) return false;
            DisplayedAmount = amount;
            UpdateDisplayText();
            amountLabel.color = amount > 0
                ? Color.white
                : new Color(.58f, .58f, .58f, 1f);
            iconRenderer.GetPropertyBlock(iconProperties);
            Color iconColor = amount > 0
                ? Color.white
                : new Color(.48f, .48f, .48f, .8f);
            iconProperties.SetColor("_Color", iconColor);
            iconProperties.SetColor("_BaseColor", iconColor);
            iconRenderer.SetPropertyBlock(iconProperties);
            return true;
        }

        public bool ApplyDisplayLod(
            ResourceNodeMarkerLod3D lod,
            bool guidanceOverride)
        {
            ResourceNodeMarkerLod3D effective = guidanceOverride
                ? ResourceNodeMarkerLod3D.Near
                : lod;
            if (DisplayLod == effective &&
                GuidanceOverride == guidanceOverride)
                return false;
            DisplayLod = effective;
            GuidanceOverride = guidanceOverride;
            UpdateDisplayText();
            return true;
        }

        public void ApplyPresentation(
            FormalWorldMarkerMetrics3D metrics,
            float frameWorldHeight,
            float iconWorldHeight,
            float textWorldHeight,
            bool layoutVisible,
            bool guidanceOverride)
        {
            EnsurePresentation();
            GuidanceOverride = guidanceOverride;
            DisplayLod = guidanceOverride
                ? ResourceNodeMarkerLod3D.Near
                : metrics.Lod;

            frameRenderer.enabled = layoutVisible &&
                metrics.ShowFrame && frame != null;
            iconRenderer.enabled = layoutVisible && icon != null;
            MeshRenderer textRenderer =
                amountLabel.GetComponent<MeshRenderer>();
            textRenderer.enabled = layoutVisible &&
                (metrics.ShowName || metrics.ShowAmount);

            frameRenderer.transform.localScale = Vector3.one *
                FiniteNonNegative(frameWorldHeight);
            iconRenderer.transform.localScale = Vector3.one *
                FiniteNonNegative(iconWorldHeight);
            amountLabel.characterSize = FiniteNonNegative(textWorldHeight);
            UpdateDisplayText();
        }

        public void SetIcon(Sprite icon)
        {
            EnsurePresentation();
            this.icon = icon;
            iconFilter.sharedMesh = icon == null
                ? null
                : ResolveIconMesh(icon);
            iconRenderer.sharedMaterial = icon == null
                ? null
                : ResolveIconMaterial(icon.texture);
        }

        public void SetFrame(Sprite frame)
        {
            EnsurePresentation();
            this.frame = frame;
            frameFilter.sharedMesh = frame == null
                ? null
                : ResolveIconMesh(frame);
            frameRenderer.sharedMaterial = frame == null
                ? null
                : ResolveIconMaterial(frame.texture);
        }

        public void FaceCamera(Transform cameraTransform)
        {
            if (cameraTransform != null)
                transform.rotation = cameraTransform.rotation;
        }

        private void EnsurePresentation()
        {
            if (frameRenderer == null || frameFilter == null)
            {
                var frameObject = new GameObject("Frame");
                frameObject.transform.SetParent(transform, false);
                frameObject.transform.localPosition =
                    new Vector3(0f, .08f, .01f);
                frameObject.transform.localScale = Vector3.one * .96f;
                frameFilter = frameObject.AddComponent<MeshFilter>();
                frameRenderer = frameObject.AddComponent<MeshRenderer>();
                frameRenderer.sortingOrder = 29;
            }
            if (iconRenderer == null || iconFilter == null)
            {
                var iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(transform, false);
                iconObject.transform.localPosition = new Vector3(0f, .12f, 0f);
                iconObject.transform.localScale = Vector3.one * .72f;
                iconFilter = iconObject.AddComponent<MeshFilter>();
                iconRenderer = iconObject.AddComponent<MeshRenderer>();
                iconProperties = new MaterialPropertyBlock();
                iconRenderer.sortingOrder = 30;
            }
            if (amountLabel == null)
            {
                var labelObject = new GameObject("NameAndAmount");
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition =
                    new Vector3(0f, -.42f, -.01f);
                amountLabel = labelObject.AddComponent<TextMesh>();
                amountLabel.anchor = TextAnchor.UpperCenter;
                amountLabel.alignment = TextAlignment.Center;
                amountLabel.characterSize = .075f;
                amountLabel.fontSize = 48;
                amountLabel.color = Color.white;
                MeshRenderer renderer =
                    labelObject.GetComponent<MeshRenderer>();
                renderer.sortingOrder = 31;
            }
        }

        private void UpdateDisplayText()
        {
            if (amountLabel == null || DisplayedAmount < 0)
                return;
            switch (DisplayLod)
            {
                case ResourceNodeMarkerLod3D.Near:
                    amountLabel.text = resourceDisplayName + "\n" +
                        DisplayedAmount;
                    break;
                case ResourceNodeMarkerLod3D.Mid:
                    amountLabel.text = DisplayedAmount.ToString();
                    break;
                default:
                    amountLabel.text = string.Empty;
                    break;
            }
        }

        private static Mesh ResolveIconMesh(Sprite sprite)
        {
            if (IconMeshes.TryGetValue(sprite, out Mesh cached))
                return cached;
            Rect rect = sprite.textureRect;
            float inverseWidth = 1f / sprite.texture.width;
            float inverseHeight = 1f / sprite.texture.height;
            float left = rect.xMin * inverseWidth;
            float right = rect.xMax * inverseWidth;
            float bottom = rect.yMin * inverseHeight;
            float top = rect.yMax * inverseHeight;
            float halfWidth = .5f * rect.width / rect.height;
            var mesh = new Mesh
            {
                name = "ResourceIconQuad_" + sprite.name,
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-halfWidth, -.5f, 0f),
                    new Vector3(halfWidth, -.5f, 0f),
                    new Vector3(halfWidth, .5f, 0f),
                    new Vector3(-halfWidth, .5f, 0f),
                },
                uv = new[]
                {
                    new Vector2(left, bottom),
                    new Vector2(right, bottom),
                    new Vector2(right, top),
                    new Vector2(left, top),
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                normals = new[]
                {
                    Vector3.back,
                    Vector3.back,
                    Vector3.back,
                    Vector3.back,
                },
            };
            mesh.RecalculateBounds();
            IconMeshes.Add(sprite, mesh);
            return mesh;
        }

        private static Material ResolveIconMaterial(Texture texture)
        {
            if (IconMaterials.TryGetValue(texture, out Material cached))
                return cached;
            Shader shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException(
                    "No supported unlit icon shader is available.");
            var material = new Material(shader)
            {
                name = "ResourceIcon_" + texture.name,
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = texture,
            };
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            IconMaterials.Add(texture, material);
            return material;
        }

        private static float FiniteNonNegative(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0f
                : Mathf.Max(0f, value);
        }
    }
}
