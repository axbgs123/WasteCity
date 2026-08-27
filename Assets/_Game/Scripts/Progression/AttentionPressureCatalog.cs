using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;

namespace WasteCity.Progression
{
    public sealed class AttentionPressureDefinition
    {
        internal AttentionPressureDefinition(
            int order,
            int threshold,
            string thresholdId,
            string encounterId,
            string displayName,
            float warningSeconds)
        {
            Order = order;
            Threshold = threshold;
            ThresholdId = new StableId(thresholdId);
            EncounterId = new StableId(encounterId);
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException(nameof(displayName))
                : displayName;
            WarningSeconds = warningSeconds;
        }

        public int Order { get; }
        public int Threshold { get; }
        public StableId ThresholdId { get; }
        public StableId EncounterId { get; }
        public string DisplayName { get; }
        public float WarningSeconds { get; }
    }

    public static class AttentionPressureCatalog
    {
        public const int QueueCapacity = 3;

        private static readonly ReadOnlyCollection<
            AttentionPressureDefinition> all = Array.AsReadOnly(new[]
            {
                new AttentionPressureDefinition(
                    0,
                    30,
                    "core.attention-threshold.echo",
                    "core.attention-encounter.directional-attack",
                    "定向攻击",
                    60f),
                new AttentionPressureDefinition(
                    1,
                    60,
                    "core.attention-threshold.high-risk",
                    "core.attention-encounter.high-risk-attack",
                    "高风险攻击",
                    75f),
                new AttentionPressureDefinition(
                    2,
                    90,
                    "core.attention-threshold.locked",
                    "core.attention-encounter.crystalline-broodmother",
                    "晶壳母体",
                    90f),
            });

        private static readonly IReadOnlyDictionary<int,
            AttentionPressureDefinition> byThreshold = BuildLookup();

        public static IReadOnlyList<AttentionPressureDefinition> All => all;

        public static AttentionPressureDefinition FindByThreshold(
            int threshold)
        {
            return byThreshold.TryGetValue(
                threshold,
                out AttentionPressureDefinition definition)
                ? definition
                : null;
        }

        private static IReadOnlyDictionary<int,
            AttentionPressureDefinition> BuildLookup()
        {
            var lookup = new Dictionary<int, AttentionPressureDefinition>(
                all.Count);
            for (var index = 0; index < all.Count; index++)
                lookup.Add(all[index].Threshold, all[index]);
            return new ReadOnlyDictionary<int,
                AttentionPressureDefinition>(lookup);
        }
    }
}
