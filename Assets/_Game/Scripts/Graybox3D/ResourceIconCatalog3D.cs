using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Economy;

namespace WasteCity.Graybox3D
{
    [Serializable]
    public sealed class ResourceIconOverride3D
    {
        [SerializeField] private string resourceId;
        [SerializeField] private Sprite sprite;

        public ResourceIconOverride3D(string resourceId, Sprite sprite)
        {
            this.resourceId = resourceId;
            this.sprite = sprite;
        }

        public string ResourceId => resourceId;
        public Sprite Sprite => sprite;
    }

    [CreateAssetMenu(
        fileName = "ResourceIconCatalog3D",
        menuName = "WasteCity/Presentation/Resource Icon Catalog 3D")]
    public sealed class ResourceIconCatalog3D : ScriptableObject
    {
        private const int IconSize = 64;
        private const float HalfPixel = .5f / IconSize;

        private static readonly Color32[] FallbackColors =
        {
            new Color32(205, 112, 54, 255),
            new Color32(65, 220, 238, 255),
            new Color32(154, 160, 168, 255),
            new Color32(88, 190, 88, 255),
            new Color32(66, 137, 235, 255),
            new Color32(190, 205, 220, 255),
            new Color32(226, 182, 64, 255),
            new Color32(174, 94, 226, 255),
            new Color32(95, 217, 225, 255),
            new Color32(224, 214, 178, 255),
            new Color32(84, 211, 145, 255),
            new Color32(202, 73, 92, 255),
            new Color32(111, 135, 223, 255),
            new Color32(219, 102, 216, 255),
            new Color32(238, 160, 83, 255)
        };

        private static readonly string[] FallbackShapeKeys =
        {
            "ore-cluster",
            "crystal",
            "stone-chunk",
            "leaf",
            "water-drop",
            "alloy-ingot",
            "ammunition",
            "spirit-rune",
            "flying-sword",
            "bone-steel",
            "concentrate-vial",
            "biological-spore",
            "resonance-rings",
            "psionic-amplifier",
            "elixir-capsule"
        };

        private static readonly Dictionary<string, int> IndexByResourceId =
            BuildIndex();
        private static readonly Sprite[] FallbackSprites =
            new Sprite[FallbackColors.Length];

        [SerializeField] private ResourceIconOverride3D[] overrides =
            Array.Empty<ResourceIconOverride3D>();

        public Sprite ResolveIcon(string resourceId)
        {
            if (!IndexByResourceId.ContainsKey(resourceId ?? string.Empty))
                return null;
            for (var index = 0;
                 overrides != null && index < overrides.Length;
                 index++)
            {
                ResourceIconOverride3D candidate = overrides[index];
                if (candidate != null && candidate.Sprite != null &&
                    string.Equals(
                        candidate.ResourceId,
                        resourceId,
                        StringComparison.Ordinal))
                    return candidate.Sprite;
            }
            return Resolve(resourceId);
        }

        public void ConfigureOverrides(ResourceIconOverride3D[] values)
        {
            overrides = values == null
                ? Array.Empty<ResourceIconOverride3D>()
                : (ResourceIconOverride3D[])values.Clone();
        }

        public bool TryValidate(out string error)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 overrides != null && index < overrides.Length;
                 index++)
            {
                ResourceIconOverride3D candidate = overrides[index];
                if (candidate == null || candidate.Sprite == null ||
                    !IndexByResourceId.ContainsKey(
                        candidate.ResourceId ?? string.Empty))
                {
                    error = "Resource icon overrides require a registered " +
                        "resource ID and a Sprite.";
                    return false;
                }
                if (!seen.Add(candidate.ResourceId))
                {
                    error = "Duplicate resource icon override: " +
                        candidate.ResourceId;
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        public static Sprite Resolve(string resourceId)
        {
            if (!IndexByResourceId.TryGetValue(
                    resourceId ?? string.Empty,
                    out int index))
                return null;
            Sprite sprite = FallbackSprites[index];
            if (sprite != null) return sprite;
            sprite = CreateFallbackSprite(index);
            FallbackSprites[index] = sprite;
            return sprite;
        }

        public static Color32 FallbackColor(string resourceId)
        {
            return IndexByResourceId.TryGetValue(
                    resourceId ?? string.Empty,
                    out int index)
                ? FallbackColors[index]
                : new Color32(0, 0, 0, 0);
        }

        public static string FallbackShapeKey(string resourceId)
        {
            return IndexByResourceId.TryGetValue(
                    resourceId ?? string.Empty,
                    out int index)
                ? FallbackShapeKeys[index]
                : string.Empty;
        }

        private static Dictionary<string, int> BuildIndex()
        {
            if (ResourceDefinitionCatalog.All.Count !=
                FallbackColors.Length ||
                ResourceDefinitionCatalog.All.Count !=
                FallbackShapeKeys.Length)
            {
                throw new InvalidOperationException(
                    "Every formal resource requires one fallback icon style.");
            }
            var result = new Dictionary<string, int>(
                ResourceDefinitionCatalog.All.Count,
                StringComparer.Ordinal);
            for (var index = 0;
                 index < ResourceDefinitionCatalog.All.Count;
                 index++)
            {
                result.Add(
                    ResourceDefinitionCatalog.All[index].Id,
                    index);
            }
            return result;
        }

        private static Sprite CreateFallbackSprite(int index)
        {
            var texture = new Texture2D(
                IconSize,
                IconSize,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "ResourceIconFallbackTexture." +
                    ResourceDefinitionCatalog.All[index].IconFallbackKey,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[IconSize * IconSize];
            Color32 fill = FallbackColors[index];
            Color32 edge = new Color32(
                (byte)Mathf.RoundToInt(fill.r * .28f),
                (byte)Mathf.RoundToInt(fill.g * .28f),
                (byte)Mathf.RoundToInt(fill.b * .28f),
                255);
            for (var y = 0; y < IconSize; y++)
            for (var x = 0; x < IconSize; x++)
            {
                float nx = (x + .5f) / IconSize * 2f - 1f;
                float ny = (y + .5f) / IconSize * 2f - 1f;
                if (!ContainsShape(index, nx, ny)) continue;
                bool isEdge =
                    !ContainsShape(index, nx - HalfPixel * 4f, ny) ||
                    !ContainsShape(index, nx + HalfPixel * 4f, ny) ||
                    !ContainsShape(index, nx, ny - HalfPixel * 4f) ||
                    !ContainsShape(index, nx, ny + HalfPixel * 4f);
                pixels[y * IconSize + x] = isEdge ? edge : fill;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, IconSize, IconSize),
                Vector2.one * .5f,
                IconSize,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = ResourceDefinitionCatalog.All[index].IconFallbackKey;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool ContainsShape(int index, float x, float y)
        {
            switch (index)
            {
                case 0:
                    return Circle(x + .24f, y + .12f, .28f) ||
                        Circle(x - .22f, y + .16f, .25f) ||
                        Circle(x, y - .2f, .3f);
                case 1:
                    return Mathf.Abs(x) * .78f + Mathf.Abs(y) < .62f;
                case 2:
                    return y > -.48f && y < .42f &&
                        x > -.52f + .18f * (y + .48f) &&
                        x < .5f - .08f * (y + .48f);
                case 3:
                {
                    float u = (x + y) * .7071f;
                    float v = (y - x) * .7071f;
                    return u * u / .34f + v * v / .075f < 1f ||
                        DistanceToSegment(x, y, -.42f, -.42f, .2f, .2f) < .055f;
                }
                case 4:
                    return Circle(x, y + .18f, .38f) ||
                        (y >= .05f && y < .68f &&
                         Mathf.Abs(x) < (.68f - y) * .58f);
                case 5:
                    return y > -.4f && y < .36f &&
                        Mathf.Abs(x) < .54f - .18f * (y + .4f);
                case 6:
                    return (y > -.5f && y < .32f && Mathf.Abs(x) < .24f) ||
                        Circle(x, y - .31f, .24f);
                case 7:
                    return (Mathf.Abs(x) + Mathf.Abs(y) < .62f) &&
                        (Mathf.Abs(x) < .12f || Mathf.Abs(y) < .12f ||
                         Mathf.Abs(Mathf.Abs(x) - Mathf.Abs(y)) < .1f);
                case 8:
                    return DistanceToSegment(
                               x, y, -.42f, -.42f, .42f, .42f) < .11f ||
                        DistanceToSegment(
                            x, y, -.34f, -.08f, .08f, -.34f) < .075f;
                case 9:
                    return DistanceToSegment(
                               x, y, -.42f, 0f, .42f, 0f) < .13f ||
                        Circle(x + .43f, y + .18f, .19f) ||
                        Circle(x + .43f, y - .18f, .19f) ||
                        Circle(x - .43f, y + .18f, .19f) ||
                        Circle(x - .43f, y - .18f, .19f);
                case 10:
                    return (y > -.48f && y < .2f && Mathf.Abs(x) < .35f) ||
                        (y >= .2f && y < .58f && Mathf.Abs(x) < .16f);
                case 11:
                    return Circle(x, y, .34f) ||
                        DistanceToSegment(x, y, -.62f, 0f, .62f, 0f) < .065f ||
                        DistanceToSegment(x, y, 0f, -.62f, 0f, .62f) < .065f ||
                        DistanceToSegment(x, y, -.44f, -.44f, .44f, .44f) < .055f;
                case 12:
                {
                    float radius = Mathf.Sqrt(x * x + y * y);
                    return Mathf.Abs(radius - .25f) < .07f ||
                        Mathf.Abs(radius - .5f) < .065f;
                }
                case 13:
                    return DistanceToSegment(x, y, 0f, -.52f, 0f, .25f) < .075f ||
                        DistanceToSegment(x, y, 0f, .08f, -.42f, .48f) < .075f ||
                        DistanceToSegment(x, y, 0f, .08f, .42f, .48f) < .075f ||
                        Circle(x, y + .5f, .13f);
                default:
                {
                    float u = (x + y) * .7071f;
                    float v = (y - x) * .7071f;
                    return Mathf.Abs(u) < .48f && Mathf.Abs(v) < .24f &&
                        (Circle(u + .25f, v, .24f) ||
                         Circle(u - .25f, v, .24f) ||
                         Mathf.Abs(u) <= .25f);
                }
            }
        }

        private static bool Circle(float x, float y, float radius)
        {
            return x * x + y * y <= radius * radius;
        }

        private static float DistanceToSegment(
            float x,
            float y,
            float ax,
            float ay,
            float bx,
            float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float lengthSquared = dx * dx + dy * dy;
            float t = lengthSquared <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) /
                    lengthSquared);
            float px = ax + dx * t;
            float py = ay + dy * t;
            float offsetX = x - px;
            float offsetY = y - py;
            return Mathf.Sqrt(offsetX * offsetX + offsetY * offsetY);
        }
    }
}
