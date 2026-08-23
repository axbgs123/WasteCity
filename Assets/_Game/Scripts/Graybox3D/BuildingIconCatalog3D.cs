using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Content;

namespace WasteCity.Graybox3D
{
    [Serializable]
    public sealed class BuildingIconOverride3D
    {
        [SerializeField] private string buildingId;
        [SerializeField] private Sprite sprite;

        public BuildingIconOverride3D(string buildingId, Sprite sprite)
        {
            this.buildingId = buildingId;
            this.sprite = sprite;
        }

        public string BuildingId => buildingId;
        public Sprite Sprite => sprite;
    }

    [CreateAssetMenu(
        fileName = "BuildingIconCatalog3D",
        menuName = "WasteCity/Presentation/Building Icon Catalog 3D")]
    public sealed class BuildingIconCatalog3D : ScriptableObject
    {
        public const string ResourcesPath =
            "Production2D/BuildingIconCatalog3D";

        private const int FallbackSize = 64;
        private static readonly Dictionary<string, BuildingDefinition>
            Definitions = BuildDefinitions();
        private static readonly Dictionary<string, Sprite> Fallbacks =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static BuildingIconCatalog3D loaded;

        [SerializeField] private BuildingIconOverride3D[] overrides =
            Array.Empty<BuildingIconOverride3D>();

        public Sprite ResolveIcon(string buildingId)
        {
            if (!Definitions.ContainsKey(buildingId ?? string.Empty))
                return null;
            for (var index = 0;
                 overrides != null && index < overrides.Length;
                 index++)
            {
                BuildingIconOverride3D candidate = overrides[index];
                if (candidate != null && candidate.Sprite != null &&
                    string.Equals(candidate.BuildingId, buildingId,
                        StringComparison.Ordinal))
                    return candidate.Sprite;
            }
            return ResolveFallback(buildingId);
        }

        public void ConfigureOverrides(BuildingIconOverride3D[] values)
        {
            overrides = values == null
                ? Array.Empty<BuildingIconOverride3D>()
                : (BuildingIconOverride3D[])values.Clone();
        }

        public bool TryValidate(out string error)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 overrides != null && index < overrides.Length;
                 index++)
            {
                BuildingIconOverride3D candidate = overrides[index];
                if (candidate == null || candidate.Sprite == null ||
                    !Definitions.ContainsKey(
                        candidate.BuildingId ?? string.Empty))
                {
                    error = "Building icon overrides require a registered " +
                        "building ID and a Sprite.";
                    return false;
                }
                if (!seen.Add(candidate.BuildingId))
                {
                    error = "Duplicate building icon override: " +
                        candidate.BuildingId;
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        public static Sprite Resolve(string buildingId)
        {
            if (loaded == null)
                loaded = Resources.Load<BuildingIconCatalog3D>(ResourcesPath);
            return loaded == null
                ? ResolveFallback(buildingId)
                : loaded.ResolveIcon(buildingId);
        }

        public static Sprite ResolveFallback(string buildingId)
        {
            if (!Definitions.TryGetValue(
                    buildingId ?? string.Empty,
                    out BuildingDefinition definition))
                return null;
            if (Fallbacks.TryGetValue(buildingId, out Sprite cached))
                return cached;

            var texture = new Texture2D(
                FallbackSize,
                FallbackSize,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "BuildingIconFallbackTexture." + buildingId,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32 fill = RouteColor(
                RouteContentDisplayCatalog.BuildingRoute(definition));
            Color32 edge = new Color32(
                (byte)Mathf.RoundToInt(fill.r * .28f),
                (byte)Mathf.RoundToInt(fill.g * .28f),
                (byte)Mathf.RoundToInt(fill.b * .28f),
                byte.MaxValue);
            var pixels = new Color32[FallbackSize * FallbackSize];
            int roof = 42 + Math.Min(8, definition.Height * 2);
            int left = 10;
            int right = 53;
            for (var y = 10; y <= roof; y++)
            for (var x = left; x <= right; x++)
            {
                bool inside = y <= 32
                    ? x >= left && x <= right
                    : x >= left + (y - 32) / 2 &&
                      x <= right - (y - 32) / 2;
                if (!inside) continue;
                bool boundary = x == left || x == right || y == 10 ||
                    y == roof || x == left + (y - 32) / 2 ||
                    x == right - (y - 32) / 2;
                pixels[y * FallbackSize + x] = boundary ? edge : fill;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, FallbackSize, FallbackSize),
                Vector2.one * .5f,
                FallbackSize,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = "BuildingIconFallback." + buildingId;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Fallbacks.Add(buildingId, sprite);
            return sprite;
        }

        private static Dictionary<string, BuildingDefinition>
            BuildDefinitions()
        {
            var result = new Dictionary<string, BuildingDefinition>(
                BuildingCatalog.All.Length,
                StringComparer.Ordinal);
            foreach (BuildingDefinition definition in BuildingCatalog.All)
                result.Add(definition.Id.Value, definition);
            return result;
        }

        private static Color32 RouteColor(ContentRoute route)
        {
            switch (route)
            {
                case ContentRoute.Technology:
                    return new Color32(65, 170, 222, 255);
                case ContentRoute.Cultivation:
                    return new Color32(81, 194, 137, 255);
                case ContentRoute.BiologicalAscension:
                    return new Color32(173, 190, 70, 255);
                case ContentRoute.Psionics:
                    return new Color32(155, 102, 221, 255);
                default:
                    return new Color32(190, 132, 78, 255);
            }
        }
    }
}
