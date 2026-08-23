using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Research;

namespace WasteCity.Graybox3D
{
    [Serializable]
    public sealed class ResearchIconOverride3D
    {
        [SerializeField] private string researchId;
        [SerializeField] private Sprite sprite;

        public ResearchIconOverride3D(string researchId, Sprite sprite)
        {
            this.researchId = researchId;
            this.sprite = sprite;
        }

        public string ResearchId => researchId;
        public Sprite Sprite => sprite;
    }

    /// <summary>
    /// IDEA-0016 presentation-only lookup for formal technology emblems.
    /// Stable research IDs remain owned by ResearchCatalog; missing art keeps a
    /// deterministic route/tier fallback and never changes research rules.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResearchIconCatalog3D",
        menuName = "WasteCity/Presentation/Research Icon Catalog 3D")]
    public sealed class ResearchIconCatalog3D : ScriptableObject
    {
        public const string ResourcesPath =
            "Production2D/ResearchIconCatalog3D";

        private const int FallbackSize = 64;
        private static readonly Dictionary<string, ResearchDefinition>
            DefinitionsById = BuildDefinitions();
        private static readonly Dictionary<string, Sprite> Fallbacks =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static ResearchIconCatalog3D loaded;

        [SerializeField] private ResearchIconOverride3D[] overrides =
            Array.Empty<ResearchIconOverride3D>();

        public Sprite ResolveIcon(string researchId)
        {
            if (!DefinitionsById.TryGetValue(
                    researchId ?? string.Empty,
                    out ResearchDefinition definition))
                return null;
            for (var index = 0;
                 overrides != null && index < overrides.Length;
                 index++)
            {
                ResearchIconOverride3D candidate = overrides[index];
                if (candidate != null && candidate.Sprite != null &&
                    string.Equals(candidate.ResearchId, researchId,
                        StringComparison.Ordinal))
                    return candidate.Sprite;
            }
            return ResolveFallback(definition);
        }

        public void ConfigureOverrides(ResearchIconOverride3D[] values)
        {
            overrides = values == null
                ? Array.Empty<ResearchIconOverride3D>()
                : (ResearchIconOverride3D[])values.Clone();
        }

        public bool TryValidate(out string error)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 overrides != null && index < overrides.Length;
                 index++)
            {
                ResearchIconOverride3D candidate = overrides[index];
                if (candidate == null || candidate.Sprite == null ||
                    !DefinitionsById.ContainsKey(
                        candidate.ResearchId ?? string.Empty))
                {
                    error = "Research icon overrides require a registered " +
                        "research ID and a Sprite.";
                    return false;
                }
                if (!seen.Add(candidate.ResearchId))
                {
                    error = "Duplicate research icon override: " +
                        candidate.ResearchId;
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        public static Sprite Resolve(string researchId)
        {
            if (!DefinitionsById.TryGetValue(
                    researchId ?? string.Empty,
                    out ResearchDefinition definition))
            {
                return null;
            }
            if (loaded == null)
                loaded = Resources.Load<ResearchIconCatalog3D>(ResourcesPath);
            return loaded == null
                ? ResolveFallback(definition)
                : loaded.ResolveIcon(researchId);
        }

        private static Dictionary<string, ResearchDefinition> BuildDefinitions()
        {
            var values = new Dictionary<string, ResearchDefinition>(
                ResearchCatalog.All.Length,
                StringComparer.Ordinal);
            foreach (ResearchDefinition definition in ResearchCatalog.All)
                values.Add(definition.Id.Value, definition);
            return values;
        }

        private static Sprite ResolveFallback(ResearchDefinition definition)
        {
            string id = definition.Id.Value;
            if (Fallbacks.TryGetValue(id, out Sprite sprite) && sprite != null)
                return sprite;

            var texture = new Texture2D(
                FallbackSize,
                FallbackSize,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "ResearchIconFallbackTexture." + id,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[FallbackSize * FallbackSize];
            Color32 fill = RouteColor(definition.Route);
            Color32 edge = new Color32(
                (byte)Mathf.RoundToInt(fill.r * .3f),
                (byte)Mathf.RoundToInt(fill.g * .3f),
                (byte)Mathf.RoundToInt(fill.b * .3f),
                255);
            float tierInset = definition.Tier * .045f;
            for (var y = 0; y < FallbackSize; y++)
            for (var x = 0; x < FallbackSize; x++)
            {
                float nx = (x + .5f) / FallbackSize * 2f - 1f;
                float ny = (y + .5f) / FallbackSize * 2f - 1f;
                float radius = Mathf.Abs(nx) + Mathf.Abs(ny);
                if (radius > .72f - tierInset) continue;
                pixels[y * FallbackSize + x] =
                    radius > .62f - tierInset ? edge : fill;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, FallbackSize, FallbackSize),
                Vector2.one * .5f,
                FallbackSize,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = definition.IconId + ".fallback";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Fallbacks[id] = sprite;
            return sprite;
        }

        private static Color32 RouteColor(DevelopmentRoute route)
        {
            switch (route)
            {
                case DevelopmentRoute.Technology:
                    return new Color32(75, 187, 226, 255);
                case DevelopmentRoute.Cultivation:
                    return new Color32(221, 177, 66, 255);
                case DevelopmentRoute.Biological:
                    return new Color32(99, 194, 92, 255);
                case DevelopmentRoute.Psionics:
                    return new Color32(181, 101, 222, 255);
                case DevelopmentRoute.Bridge:
                    return new Color32(225, 129, 75, 255);
                default:
                    return new Color32(174, 183, 191, 255);
            }
        }
    }
}
