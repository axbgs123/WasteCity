using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.Economy;
using WasteCity.Research;

namespace WasteCity.Graybox3D
{
    public enum Production2DVisualClass
    {
        Item,
        Technology,
        Building,
        Ui,
        Character,
        WorldMarker,
    }

    public enum Production2DRecipeBadgeRule
    {
        Machine,
        Manual,
    }

    [Serializable]
    public sealed class Production2DVisualEntry3D
    {
        [SerializeField] private Production2DVisualClass visualClass;
        [SerializeField] private string contentId;
        [SerializeField] private string variant;
        [SerializeField] private Sprite sprite;

        public Production2DVisualEntry3D(
            Production2DVisualClass visualClass,
            string contentId,
            string variant,
            Sprite sprite)
        {
            this.visualClass = visualClass;
            this.contentId = contentId;
            this.variant = variant;
            this.sprite = sprite;
        }

        public Production2DVisualClass VisualClass => visualClass;
        public string ContentId => contentId;
        public string Variant => variant;
        public Sprite Sprite => sprite;
    }

    [Serializable]
    public sealed class Production2DRecipeVisualEntry3D
    {
        [SerializeField] private string recipeId;
        [SerializeField] private string primaryOutputContentId;
        [SerializeField] private bool usesBoundResourceVisual;
        [SerializeField] private Production2DRecipeBadgeRule badgeRule;
        [SerializeField] private Sprite primaryOutputSprite;

        public Production2DRecipeVisualEntry3D(
            string recipeId,
            string primaryOutputContentId,
            bool usesBoundResourceVisual,
            Production2DRecipeBadgeRule badgeRule,
            Sprite primaryOutputSprite)
        {
            this.recipeId = recipeId;
            this.primaryOutputContentId = primaryOutputContentId;
            this.usesBoundResourceVisual = usesBoundResourceVisual;
            this.badgeRule = badgeRule;
            this.primaryOutputSprite = primaryOutputSprite;
        }

        public string RecipeId => recipeId;
        public string PrimaryOutputContentId => primaryOutputContentId;
        public bool UsesBoundResourceVisual => usesBoundResourceVisual;
        public Production2DRecipeBadgeRule BadgeRule => badgeRule;
        public Sprite PrimaryOutputSprite => primaryOutputSprite;
    }

    /// <summary>
    /// IDEA-0016 presentation-only root for formal 2D visuals. It owns stable
    /// visual keys and references, never gameplay names, values or state.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Production2DVisualCatalog3D",
        menuName = "WasteCity/Presentation/Production 2D Visual Catalog 3D")]
    public sealed class Production2DVisualCatalog3D : ScriptableObject
    {
        public const string DefaultVariant = "default";
        public const string ResourcesPath =
            "Production2D/Production2DVisualCatalog3D";

        private static readonly Dictionary<string, Sprite> ClassFallbacks =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private static ResearchIconCatalog3D researchFallback;
        private static Production2DVisualCatalog3D loaded;

        [SerializeField] private Production2DVisualEntry3D[] entries =
            Array.Empty<Production2DVisualEntry3D>();
        [SerializeField] private Production2DRecipeVisualEntry3D[] recipes =
            Array.Empty<Production2DRecipeVisualEntry3D>();

        public IReadOnlyList<Production2DVisualEntry3D> Entries =>
            entries ?? Array.Empty<Production2DVisualEntry3D>();
        public IReadOnlyList<Production2DRecipeVisualEntry3D> RecipeEntries =>
            recipes ?? Array.Empty<Production2DRecipeVisualEntry3D>();

        public void Configure(
            Production2DVisualEntry3D[] visualEntries,
            Production2DRecipeVisualEntry3D[] recipeEntries)
        {
            entries = visualEntries == null
                ? Array.Empty<Production2DVisualEntry3D>()
                : (Production2DVisualEntry3D[])visualEntries.Clone();
            recipes = recipeEntries == null
                ? Array.Empty<Production2DRecipeVisualEntry3D>()
                : (Production2DRecipeVisualEntry3D[])recipeEntries.Clone();
        }

        public Sprite ResolveIcon(
            Production2DVisualClass visualClass,
            string contentId,
            string variant = DefaultVariant)
        {
            for (var index = 0;
                 entries != null && index < entries.Length;
                 index++)
            {
                Production2DVisualEntry3D candidate = entries[index];
                if (candidate != null && candidate.Sprite != null &&
                    candidate.VisualClass == visualClass &&
                    string.Equals(candidate.ContentId, contentId,
                        StringComparison.Ordinal) &&
                    string.Equals(candidate.Variant, variant,
                        StringComparison.Ordinal))
                    return candidate.Sprite;
            }

            return ResolveCategoryFallback(visualClass, contentId, variant);
        }

        public bool TryResolveRecipeVisual(
            string recipeId,
            string boundResourceId,
            out Sprite primaryOutput,
            out Production2DRecipeBadgeRule badgeRule)
        {
            for (var index = 0;
                 recipes != null && index < recipes.Length;
                 index++)
            {
                Production2DRecipeVisualEntry3D candidate = recipes[index];
                if (candidate == null || !string.Equals(
                        candidate.RecipeId,
                        recipeId,
                        StringComparison.Ordinal))
                    continue;
                badgeRule = candidate.BadgeRule;
                primaryOutput = candidate.UsesBoundResourceVisual
                    ? ResolveIcon(
                        Production2DVisualClass.Item,
                        boundResourceId)
                    : candidate.PrimaryOutputSprite;
                return primaryOutput != null;
            }

            return TryResolveRecipeFallback(
                recipeId,
                boundResourceId,
                out primaryOutput,
                out badgeRule);
        }

        public bool TryValidate(out string error)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 entries != null && index < entries.Length;
                 index++)
            {
                Production2DVisualEntry3D candidate = entries[index];
                if (candidate == null || candidate.Sprite == null ||
                    string.IsNullOrWhiteSpace(candidate.ContentId) ||
                    string.IsNullOrWhiteSpace(candidate.Variant))
                {
                    error = "Production 2D visual entries require a key and Sprite.";
                    return false;
                }
                string key = Key(
                    candidate.VisualClass,
                    candidate.ContentId,
                    candidate.Variant);
                if (!keys.Add(key))
                {
                    error = "Duplicate production 2D visual key: " + key;
                    return false;
                }
            }

            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0;
                 recipes != null && index < recipes.Length;
                 index++)
            {
                Production2DRecipeVisualEntry3D candidate = recipes[index];
                bool validOutput = candidate != null &&
                    (candidate.UsesBoundResourceVisual
                        ? string.IsNullOrEmpty(candidate.PrimaryOutputContentId) &&
                          candidate.PrimaryOutputSprite == null
                        : !string.IsNullOrWhiteSpace(
                              candidate.PrimaryOutputContentId) &&
                          candidate.PrimaryOutputSprite != null);
                if (!validOutput || string.IsNullOrWhiteSpace(
                        candidate.RecipeId) ||
                    !recipeIds.Add(candidate.RecipeId))
                {
                    error = "Production 2D recipe visuals require unique " +
                        "recipe IDs and one explicit output visual rule.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public static Sprite Resolve(
            Production2DVisualClass visualClass,
            string contentId,
            string variant = DefaultVariant)
        {
            if (loaded == null)
                loaded = Resources.Load<Production2DVisualCatalog3D>(
                    ResourcesPath);
            return ResolveFromCatalogOrFallback(
                loaded,
                visualClass,
                contentId,
                variant);
        }

        public static Sprite ResolveFromCatalogOrFallback(
            Production2DVisualCatalog3D catalog,
            Production2DVisualClass visualClass,
            string contentId,
            string variant = DefaultVariant)
        {
            return catalog == null
                ? ResolveCategoryFallback(visualClass, contentId, variant)
                : catalog.ResolveIcon(visualClass, contentId, variant);
        }

        private static Sprite ResolveCategoryFallback(
            Production2DVisualClass visualClass,
            string contentId,
            string variant)
        {
            if (!string.Equals(variant, DefaultVariant,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(contentId))
                return null;
            switch (visualClass)
            {
                case Production2DVisualClass.Item:
                    return ResourceIconCatalog3D.Resolve(contentId) ??
                        CreateClassFallback(visualClass, contentId);
                case Production2DVisualClass.Technology:
                    ResearchDefinition research = ResearchCatalog.Find(contentId);
                    if (research != null)
                    {
                        if (researchFallback == null)
                        {
                            researchFallback =
                                CreateInstance<ResearchIconCatalog3D>();
                            researchFallback.hideFlags =
                                HideFlags.HideAndDontSave;
                        }
                        return researchFallback.ResolveIcon(contentId);
                    }
                    return CreateClassFallback(visualClass, contentId);
                case Production2DVisualClass.Building:
                    return BuildingIconCatalog3D.ResolveFallback(contentId) ??
                        CreateClassFallback(visualClass, contentId);
                case Production2DVisualClass.Ui:
                case Production2DVisualClass.Character:
                case Production2DVisualClass.WorldMarker:
                    return CreateClassFallback(visualClass, contentId);
                default:
                    return null;
            }
        }

        private static bool TryResolveRecipeFallback(
            string recipeId,
            string boundResourceId,
            out Sprite primaryOutput,
            out Production2DRecipeBadgeRule badgeRule)
        {
            primaryOutput = null;
            badgeRule = Production2DRecipeBadgeRule.Machine;
            if (!ResourceRecipeCatalog.TryGet(
                    recipeId,
                    out ResourceRecipeDefinition definition))
                return false;
            badgeRule = definition.Kind == ResourceRecipeKind.ManualCrafting
                ? Production2DRecipeBadgeRule.Manual
                : Production2DRecipeBadgeRule.Machine;
            string resourceId = definition.UsesBoundResourceNode
                ? boundResourceId
                : definition.Outputs.Count > 0
                    ? definition.Outputs[0].ResourceId
                    : null;
            primaryOutput = ResolveCategoryFallback(
                Production2DVisualClass.Item,
                resourceId,
                DefaultVariant);
            return primaryOutput != null;
        }

        private static Sprite CreateClassFallback(
            Production2DVisualClass visualClass,
            string contentId)
        {
            string key = visualClass + ":" + contentId;
            if (ClassFallbacks.TryGetValue(key, out Sprite cached))
                return cached;
            const int size = 64;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                false)
            {
                name = "Production2DFallbackTexture." + key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32 fill = ClassColor(visualClass);
            Color32 edge = new Color32(
                (byte)(fill.r / 3),
                (byte)(fill.g / 3),
                (byte)(fill.b / 3),
                byte.MaxValue);
            var pixels = new Color32[size * size];
            for (var y = 10; y < 54; y++)
            for (var x = 10; x < 54; x++)
            {
                bool boundary = x < 14 || x >= 50 || y < 14 || y >= 50;
                pixels[y * size + x] = boundary ? edge : fill;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                Vector2.one * .5f,
                size,
                0u,
                SpriteMeshType.FullRect);
            sprite.name = "Production2DFallback." + key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ClassFallbacks.Add(key, sprite);
            return sprite;
        }

        private static Color32 ClassColor(Production2DVisualClass visualClass)
        {
            switch (visualClass)
            {
                case Production2DVisualClass.Character:
                    return new Color32(89, 177, 187, 255);
                case Production2DVisualClass.WorldMarker:
                    return new Color32(195, 139, 72, 255);
                default:
                    return new Color32(102, 132, 145, 255);
            }
        }

        private static string Key(
            Production2DVisualClass visualClass,
            string contentId,
            string variant)
        {
            return visualClass + "|" + contentId + "|" + variant;
        }
    }
}
