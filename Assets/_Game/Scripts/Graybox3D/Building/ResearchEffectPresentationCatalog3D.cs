using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using WasteCity.Research;

namespace WasteCity.Graybox3D.Building
{
    /// <summary>
    /// Immutable localized projection of one formal research effect. Numeric
    /// truth remains in ResearchEffectCatalog; this type only formats it for
    /// the research tree and development surfaces.
    /// </summary>
    public sealed class ResearchEffectLinePresentation3D
    {
        internal ResearchEffectLinePresentation3D(
            string tag,
            string summary,
            string scope,
            string stacking,
            string activation,
            bool isApplied,
            bool isPreviewOnly)
        {
            Tag = tag ?? string.Empty;
            Summary = summary ?? string.Empty;
            Scope = scope ?? string.Empty;
            Stacking = stacking ?? string.Empty;
            Activation = activation ?? string.Empty;
            IsApplied = isApplied;
            IsPreviewOnly = isPreviewOnly;
        }

        public string Tag { get; }
        public string Summary { get; }
        public string Scope { get; }
        public string Stacking { get; }
        public string Activation { get; }
        public bool IsApplied { get; }
        public bool IsPreviewOnly { get; }
    }

    public static class ResearchEffectPresentationCatalog3D
    {
        public static IReadOnlyList<ResearchEffectLinePresentation3D> Resolve(
            ResearchDefinition definition,
            bool completed)
        {
            if (definition == null)
                return Array.Empty<ResearchEffectLinePresentation3D>();

            IReadOnlyList<ResearchEffectDefinition> effects =
                ResearchEffectCatalog.ForResearch(definition.Id.Value);
            if (effects.Count == 0)
                return Array.Empty<ResearchEffectLinePresentation3D>();

            var result = new ResearchEffectLinePresentation3D[effects.Count];
            for (var index = 0; index < effects.Count; index++)
            {
                ResearchEffectDefinition effect = effects[index];
                bool previewOnly = effect.Activation ==
                        ResearchEffectActivation.Preview ||
                    !effect.IsExecutable &&
                    effect.Kind != ResearchEffectKind.UnlockContent ||
                    !completed && definition.ReleaseState ==
                        ResearchReleaseState.PreviewOnly;
                bool applied = completed && !previewOnly;
                result[index] = new ResearchEffectLinePresentation3D(
                    Tag(effect.Kind),
                    Summary(effect),
                    "范围：" + effect.Scope,
                    "叠加：" + effect.Stacking,
                    previewOnly
                        ? "仅预览，效果待接入"
                        : applied
                            ? "已生效"
                            : "研究完成后生效",
                    applied,
                    previewOnly);
            }
            return new ReadOnlyCollection<ResearchEffectLinePresentation3D>(
                result);
        }

        private static string Tag(ResearchEffectKind kind)
        {
            return kind == ResearchEffectKind.UnlockContent
                ? "[解锁]"
                : "[被动]";
        }

        private static string Summary(ResearchEffectDefinition effect)
        {
            if (effect.Kind == ResearchEffectKind.UnlockContent)
            {
                return string.IsNullOrWhiteSpace(effect.Description)
                    ? effect.DisplayName
                    : effect.Description;
            }

            return effect.DisplayName + " " +
                FormatValue(effect.BeforeValue, effect.Unit) + " → " +
                FormatValue(effect.AfterValue, effect.Unit);
        }

        private static string FormatValue(float value, string unit)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) +
                (unit ?? string.Empty);
        }
    }
}
