using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;

namespace WasteCity.Progression
{
    public enum FormalAttentionRepeatPolicy
    {
        OncePerSession,
        OncePerStableEvent,
    }

    public sealed class FormalAttentionReasonDefinition
    {
        internal FormalAttentionReasonDefinition(
            string id,
            int delta,
            FormalAttentionRepeatPolicy repeatPolicy,
            string localizationKey)
        {
            Id = new StableId(id);
            Delta = delta;
            RepeatPolicy = repeatPolicy;
            LocalizationKey = string.IsNullOrWhiteSpace(localizationKey)
                ? throw new ArgumentException(
                    "A formal attention reason requires a localization key.",
                    nameof(localizationKey))
                : localizationKey;
        }

        public StableId Id { get; }
        public int Delta { get; }
        public FormalAttentionRepeatPolicy RepeatPolicy { get; }
        public string LocalizationKey { get; }
    }

    public static class FormalAttentionCatalog
    {
        public const int InitialValue = 10;
        public const int MinimumValue = 0;
        public const int MaximumValue = 100;
        public const int HistoryCapacity = 128;
        public const int RecentReasonCapacity = 3;

        private static readonly ReadOnlyCollection<int> thresholds =
            Array.AsReadOnly(new[] { 30, 60, 90 });

        private static readonly ReadOnlyCollection<
            FormalAttentionReasonDefinition> all =
            Array.AsReadOnly(new[]
            {
                Once("core.attention.fate.first-activation", 5),
                Once("core.attention.scan.safe-mining-zone", 2),
                Once("core.attention.scan.crystal-rift", 5),
                Once("core.attention.city.first-deployment", 5),
                Once("core.attention.building.first-mining-station", 2),
                Once("core.attention.building.first-smelter", 3),
                Once("core.attention.building.first-assembler", 4),
                Event("core.attention.building.machine-gun-turret", 5),
                Once("core.attention.research.automated-machinery", 3),
                Once("core.attention.research.precision-assembly", 4),
                Once("core.attention.research.automated-defense", 5),
                Once("core.attention.research.reinforced-structures", 5),
                Once("core.attention.research.legacy-analysis", 12),
                Event("core.attention.rescue.ruins", 2),
                Once("core.attention.rescue.cen-jin", 5),
                Once(
                    "core.attention.combat.first-directed-attack-defeated",
                    8),
                Event("core.attention.fate.rewind-anchor-used", 12),
                Event("core.attention.fate.void-debt-periodic", 1),
                Once("core.attention.fate.pocket-universe-activated", 4),
                Once("core.attention.escape.locked-region", -8),
                Event("core.attention.ruins.optional-interference", -5),
                Event("core.attention.civilization.advanced", 25),
            });

        private static readonly IReadOnlyDictionary<string,
            FormalAttentionReasonDefinition> byId = BuildLookup();

        public static IReadOnlyList<int> Thresholds => thresholds;
        public static IReadOnlyList<FormalAttentionReasonDefinition> All => all;

        public static FormalAttentionReasonDefinition Find(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                byId.TryGetValue(
                    id,
                    out FormalAttentionReasonDefinition definition)
                ? definition
                : null;
        }

        private static FormalAttentionReasonDefinition Once(
            string id,
            int delta)
        {
            return Define(
                id,
                delta,
                FormalAttentionRepeatPolicy.OncePerSession);
        }

        private static FormalAttentionReasonDefinition Event(
            string id,
            int delta)
        {
            return Define(
                id,
                delta,
                FormalAttentionRepeatPolicy.OncePerStableEvent);
        }

        private static FormalAttentionReasonDefinition Define(
            string id,
            int delta,
            FormalAttentionRepeatPolicy repeatPolicy)
        {
            const string idPrefix = "core.attention.";
            string suffix = id.Substring(idPrefix.Length).Replace('.', '-');
            return new FormalAttentionReasonDefinition(
                id,
                delta,
                repeatPolicy,
                "attention.reason." + suffix);
        }

        private static IReadOnlyDictionary<string,
            FormalAttentionReasonDefinition> BuildLookup()
        {
            var lookup = new Dictionary<string,
                FormalAttentionReasonDefinition>(
                all.Count,
                StringComparer.Ordinal);
            for (var index = 0; index < all.Count; index++)
            {
                FormalAttentionReasonDefinition definition = all[index];
                lookup.Add(definition.Id.Value, definition);
            }
            return new ReadOnlyDictionary<string,
                FormalAttentionReasonDefinition>(lookup);
        }
    }
}
