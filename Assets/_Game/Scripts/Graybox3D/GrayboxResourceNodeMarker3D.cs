using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Economy;
using WasteCity.World;
using WasteCity.World.Exploration;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxResourceNodeMarker3D : MonoBehaviour
    {
        private const float TextMeshVisualHeightFactor = 5.4f;
        private const float LabelGapRatio = .12f;

        private static readonly Dictionary<Sprite, Mesh> IconMeshes =
            new Dictionary<Sprite, Mesh>();
        private static readonly Dictionary<Sprite, Mesh> FrameMeshes =
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
        private TextMesh shadowLabel;
        private string resourceDisplayName;
        private string intelStatusText = string.Empty;
        private bool suppressAmountForIntel;
        private bool labelRequestedByLod = true;
        private bool labelLayoutVisible = true;
        private bool hasIntelVisualState;
        private WorldIntelState intelVisualState;

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
        public bool HasIntelVisualState => hasIntelVisualState;
        public WorldIntelState IntelVisualState => intelVisualState;
        public Color IconTint { get; private set; } = Color.white;
        public ResourceNodeMarkerLod3D DisplayLod { get; private set; } =
            ResourceNodeMarkerLod3D.Near;
        public bool GuidanceOverride { get; private set; }
        internal bool HasLabelContent =>
            labelRequestedByLod && !string.IsNullOrEmpty(DisplayText);
        internal Bounds LabelWorldBounds => amountLabel == null
            ? default
            : amountLabel.GetComponent<MeshRenderer>().bounds;

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
            if (!hasIntelVisualState)
                ApplyLiveVisualPalette(amount);
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
            labelRequestedByLod = effective != ResourceNodeMarkerLod3D.Far;
            labelLayoutVisible = true;
            ApplyLabelRendererVisibility();
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
            labelRequestedByLod = metrics.ShowName || metrics.ShowAmount;
            labelLayoutVisible = layoutVisible;
            ApplyLabelRendererVisibility();

            frameRenderer.transform.localScale = Vector3.one *
                FiniteNonNegative(frameWorldHeight);
            iconRenderer.transform.localScale = Vector3.one *
                FiniteNonNegative(iconWorldHeight);
            amountLabel.characterSize = TextCharacterSizeForVisualHeight(
                textWorldHeight);
            shadowLabel.characterSize = amountLabel.characterSize;
            float markerWorldHeight = metrics.ShowFrame
                ? FiniteNonNegative(frameWorldHeight)
                : FiniteNonNegative(iconWorldHeight);
            float markerCenterY = metrics.ShowFrame ? .08f : .12f;
            Vector3 labelPosition = new Vector3(
                0f,
                markerCenterY - markerWorldHeight * .5f -
                FiniteNonNegative(textWorldHeight) * LabelGapRatio,
                -.01f);
            amountLabel.transform.localPosition = labelPosition;
            float shadowPixelWorldSize = metrics.TextReferencePixels > 0f
                ? FiniteNonNegative(textWorldHeight) /
                  metrics.TextReferencePixels
                : 0f;
            float shadowOffset = shadowPixelWorldSize * 1.25f;
            shadowLabel.transform.localPosition = labelPosition +
                new Vector3(shadowOffset, -shadowOffset, .001f);
            UpdateDisplayText();
        }

        internal bool SetLabelLayoutVisible(bool visible)
        {
            if (labelLayoutVisible == visible)
                return false;
            labelLayoutVisible = visible;
            ApplyLabelRendererVisibility();
            return true;
        }

        public void SetIcon(Sprite icon)
        {
            EnsurePresentation();
            this.icon = icon;
            iconFilter.sharedMesh = icon == null
                ? null
                : ResolveIconMesh(
                    icon,
                    Production2DVisualCatalog3D.ResolveVisibleBounds(
                        Production2DVisualClass.Item,
                        ResourceId),
                    IconMeshes);
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
                : ResolveIconMesh(
                    frame,
                    new Rect(0f, 0f, 1f, 1f),
                    FrameMeshes);
            frameRenderer.sharedMaterial = frame == null
                ? null
                : ResolveIconMaterial(frame.texture);
        }

        public void ApplyIntelPresentation(
            bool suppressAmount,
            string statusText,
            bool hasIntelState,
            WorldIntelState intelState)
        {
            suppressAmountForIntel = suppressAmount;
            intelStatusText = statusText ?? string.Empty;
            hasIntelVisualState = hasIntelState;
            intelVisualState = intelState;
            if (hasIntelState)
                ApplyIntelVisualPalette(intelState);
            else
                ApplyLiveVisualPalette(Math.Max(0, DisplayedAmount));
            UpdateDisplayText();
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
            if (shadowLabel == null)
            {
                var shadowObject = new GameObject("NameAndAmountShadow");
                shadowObject.transform.SetParent(transform, false);
                shadowLabel = shadowObject.AddComponent<TextMesh>();
                ConfigureLabel(shadowLabel);
                shadowLabel.color = ShadowColor();
                MeshRenderer renderer =
                    shadowObject.GetComponent<MeshRenderer>();
                renderer.sortingOrder = 31;
            }
            if (amountLabel == null)
            {
                var labelObject = new GameObject("NameAndAmount");
                labelObject.transform.SetParent(transform, false);
                labelObject.transform.localPosition =
                    new Vector3(0f, -.42f, -.01f);
                amountLabel = labelObject.AddComponent<TextMesh>();
                ConfigureLabel(amountLabel);
                amountLabel.color = Color.white;
                MeshRenderer renderer =
                    labelObject.GetComponent<MeshRenderer>();
                renderer.sortingOrder = 32;
            }
        }

        private static void ConfigureLabel(TextMesh label)
        {
            label.anchor = TextAnchor.UpperCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .075f;
            label.fontSize = 48;
            label.fontStyle = FontStyle.Bold;
            label.lineSpacing = .88f;
        }

        private void UpdateDisplayText()
        {
            if (amountLabel == null || DisplayedAmount < 0)
                return;
            string text;
            switch (DisplayLod)
            {
                case ResourceNodeMarkerLod3D.Near:
                    text = resourceDisplayName;
                    if (!suppressAmountForIntel)
                        text += "\n" + DisplayedAmount;
                    if (!string.IsNullOrWhiteSpace(intelStatusText))
                        text += "\n" + intelStatusText;
                    break;
                case ResourceNodeMarkerLod3D.Mid:
                    text = suppressAmountForIntel
                        ? intelStatusText
                        : DisplayedAmount +
                          (string.IsNullOrWhiteSpace(intelStatusText)
                              ? string.Empty
                              : "\n" + intelStatusText);
                    break;
                default:
                    text = string.Empty;
                    break;
            }
            amountLabel.text = text;
            if (shadowLabel != null)
                shadowLabel.text = text;
        }

        private void ApplyLabelRendererVisibility()
        {
            if (amountLabel == null || shadowLabel == null)
                return;
            bool visible = labelRequestedByLod && labelLayoutVisible;
            amountLabel.GetComponent<MeshRenderer>().enabled = visible;
            shadowLabel.GetComponent<MeshRenderer>().enabled = visible;
        }

        private static Mesh ResolveIconMesh(
            Sprite sprite,
            Rect visibleBounds,
            IDictionary<Sprite, Mesh> cache)
        {
            if (cache.TryGetValue(sprite, out Mesh cached))
                return cached;
            Rect rect = sprite.textureRect;
            Rect crop = Production2DVisualScalePolicy3D.IsValid(visibleBounds)
                ? visibleBounds
                : new Rect(0f, 0f, 1f, 1f);
            rect = new Rect(
                rect.x + crop.x * rect.width,
                rect.y + crop.y * rect.height,
                crop.width * rect.width,
                crop.height * rect.height);
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
            cache.Add(sprite, mesh);
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

        private static float TextCharacterSizeForVisualHeight(
            float textWorldHeight)
        {
            return FiniteNonNegative(textWorldHeight) /
                TextMeshVisualHeightFactor;
        }

        private void ApplyLabelColor(int amount)
        {
            if (amountLabel == null)
                return;
            if (amount <= 0)
            {
                amountLabel.color = new Color(.58f, .58f, .58f, 1f);
                return;
            }
            Color resourceColor =
                ResourceIconCatalog3D.FallbackColor(ResourceId);
            amountLabel.color = Color.Lerp(resourceColor, Color.white, .7f);
            if (shadowLabel != null)
                shadowLabel.color = ShadowColor();
        }

        private void ApplyLiveVisualPalette(int amount)
        {
            ApplyLabelColor(amount);
            ApplyIconTint(amount > 0
                ? Color.white
                : new Color(.48f, .48f, .48f, .8f));
        }

        private void ApplyIntelVisualPalette(WorldIntelState state)
        {
            Color tint;
            switch (state)
            {
                case WorldIntelState.Stale:
                    tint = new Color(1f, .68f, .22f, .92f);
                    break;
                case WorldIntelState.Expired:
                    tint = new Color(.58f, .61f, .64f, .82f);
                    break;
                default:
                    tint = new Color(.65f, .88f, 1f, .96f);
                    break;
            }
            if (amountLabel != null)
                amountLabel.color = tint;
            if (shadowLabel != null)
                shadowLabel.color = ShadowColor();
            ApplyIconTint(tint);
        }

        private void ApplyIconTint(Color tint)
        {
            EnsurePresentation();
            IconTint = tint;
            iconRenderer.GetPropertyBlock(iconProperties);
            iconProperties.SetColor("_Color", tint);
            iconProperties.SetColor("_BaseColor", tint);
            iconRenderer.SetPropertyBlock(iconProperties);
        }

        private static Color ShadowColor()
        {
            return new Color(
                16f / 255f,
                24f / 255f,
                32f / 255f,
                .88f);
        }
    }
}
