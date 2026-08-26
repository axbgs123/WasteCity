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
            string localizationKey,
            string displayName)
        {
            Id = new StableId(id);
            Delta = delta;
            RepeatPolicy = repeatPolicy;
            LocalizationKey = string.IsNullOrWhiteSpace(localizationKey)
                ? throw new ArgumentException(
                    "A formal attention reason requires a localization key.",
                    nameof(localizationKey))
                : localizationKey;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException(
                    "A formal attention reason requires a display name.",
                    nameof(displayName))
                : displayName;
        }

        public StableId Id { get; }
        public int Delta { get; }
        public FormalAttentionRepeatPolicy RepeatPolicy { get; }
        public string LocalizationKey { get; }
        public string DisplayName { get; }
    }

    public sealed class FormalAttentionStageDefinition
    {
        internal FormalAttentionStageDefinition(
            int minimumInclusive,
            int maximumInclusive,
            string displayName,
            string localizationKey)
        {
            if (minimumInclusive < FormalAttentionCatalog.MinimumValue ||
                maximumInclusive > FormalAttentionCatalog.MaximumValue ||
                minimumInclusive > maximumInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumInclusive));
            }
            MinimumInclusive = minimumInclusive;
            MaximumInclusive = maximumInclusive;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? throw new ArgumentException(
                    "An attention stage requires a display name.",
                    nameof(displayName))
                : displayName;
            LocalizationKey = string.IsNullOrWhiteSpace(localizationKey)
                ? throw new ArgumentException(
                    "An attention stage requires a localization key.",
                    nameof(localizationKey))
                : localizationKey;
        }

        public int MinimumInclusive { get; }
        public int MaximumInclusive { get; }
        public string DisplayName { get; }
        public string LocalizationKey { get; }

        internal bool Contains(int value)
        {
            return value >= MinimumInclusive && value <= MaximumInclusive;
        }
    }

    public static class FormalAttentionCatalog
    {
        public const int InitialValue = 10;
        public const int MinimumValue = 0;
        public const int MaximumValue = 100;
        public const int HistoryCapacity = 128;
        public const int RecentReasonCapacity = 3;
        public const string UnknownReasonDisplayName = "未知历史原因";

        private static readonly ReadOnlyCollection<int> thresholds =
            Array.AsReadOnly(new[] { 30, 60, 90 });

        private static readonly ReadOnlyCollection<
            FormalAttentionStageDefinition> stages =
            Array.AsReadOnly(new[]
            {
                new FormalAttentionStageDefinition(
                    0,
                    29,
                    "未锁定",
                    "attention.stage.unlocked"),
                new FormalAttentionStageDefinition(
                    30,
                    59,
                    "异常回波",
                    "attention.stage.echo"),
                new FormalAttentionStageDefinition(
                    60,
                    89,
                    "定向观测",
                    "attention.stage.directed"),
                new FormalAttentionStageDefinition(
                    90,
                    100,
                    "坐标锁定",
                    "attention.stage.locked"),
            });

        private static readonly ReadOnlyCollection<
            FormalAttentionReasonDefinition> all =
            Array.AsReadOnly(new[]
            {
                Once("core.attention.fate.first-activation", 5, "选择命轨"),
                Once("core.attention.scan.safe-mining-zone", 2, "扫描安全矿区"),
                Once("core.attention.scan.crystal-rift", 5, "扫描结晶裂谷"),
                Once("core.attention.city.first-deployment", 5, "城市首次展开"),
                Once("core.attention.building.first-mining-station", 2,
                    "首座采矿站完工"),
                Once("core.attention.building.first-smelter", 3,
                    "首座冶炼厂完工"),
                Once("core.attention.building.first-assembler", 4,
                    "首座装配厂完工"),
                Event("core.attention.building.machine-gun-turret", 5,
                    "机枪塔完工"),
                Once("core.attention.research.automated-machinery", 3,
                    "完成基础冶金"),
                Once("core.attention.research.precision-assembly", 4,
                    "完成精密装配"),
                Once("core.attention.research.automated-defense", 5,
                    "完成自动防御架构"),
                Once("core.attention.research.reinforced-structures", 5,
                    "完成加固结构"),
                Once("core.attention.research.legacy-analysis", 12,
                    "完成遗产解析"),
                Event("core.attention.rescue.ruins", 2, "废墟救援"),
                Once("core.attention.rescue.cen-jin", 5, "营救岑烬"),
                Once(
                    "core.attention.combat.first-directed-attack-defeated",
                    8,
                    "首次击退定向攻击"),
                Event("core.attention.fate.rewind-anchor-used", 12,
                    "使用回溯锚点"),
                Event("core.attention.fate.void-debt-periodic", 1,
                    "虚空债结算"),
                Once("core.attention.fate.pocket-universe-activated", 4,
                    "袖珍宇宙旗舰启动"),
                Once("core.attention.escape.locked-region", -8,
                    "离开锁定观测区域"),
                Event("core.attention.ruins.optional-interference", -5,
                    "完成可选干扰遗迹"),
                Event("core.attention.civilization.advanced", 25, "文明升阶"),
            });

        private static readonly IReadOnlyDictionary<string,
            FormalAttentionReasonDefinition> byId = BuildLookup();

        public static IReadOnlyList<int> Thresholds => thresholds;
        public static IReadOnlyList<FormalAttentionStageDefinition> Stages =>
            stages;
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

        public static string DisplayNameForReason(string id)
        {
            FormalAttentionReasonDefinition definition = Find(id);
            return definition?.DisplayName ?? UnknownReasonDisplayName;
        }

        public static FormalAttentionStageDefinition StageFor(int value)
        {
            for (var index = 0; index < stages.Count; index++)
                if (stages[index].Contains(value)) return stages[index];
            return null;
        }

        public static bool TryGetNextUnreachedThreshold(
            int value,
            IReadOnlyList<int> reachedThresholds,
            out int threshold,
            out int distance)
        {
            threshold = 0;
            distance = 0;
            if (value < MinimumValue || value > MaximumValue) return false;
            for (var index = 0; index < thresholds.Count; index++)
            {
                int candidate = thresholds[index];
                if (candidate <= value ||
                    Contains(reachedThresholds, candidate))
                {
                    continue;
                }
                threshold = candidate;
                distance = candidate - value;
                return true;
            }
            return false;
        }

        private static FormalAttentionReasonDefinition Once(
            string id,
            int delta,
            string displayName)
        {
            return Define(
                id,
                delta,
                FormalAttentionRepeatPolicy.OncePerSession,
                displayName);
        }

        private static FormalAttentionReasonDefinition Event(
            string id,
            int delta,
            string displayName)
        {
            return Define(
                id,
                delta,
                FormalAttentionRepeatPolicy.OncePerStableEvent,
                displayName);
        }

        private static FormalAttentionReasonDefinition Define(
            string id,
            int delta,
            FormalAttentionRepeatPolicy repeatPolicy,
            string displayName)
        {
            const string idPrefix = "core.attention.";
            string suffix = id.Substring(idPrefix.Length).Replace('.', '-');
            return new FormalAttentionReasonDefinition(
                id,
                delta,
                repeatPolicy,
                "attention.reason." + suffix,
                displayName);
        }

        private static bool Contains(
            IReadOnlyList<int> values,
            int candidate)
        {
            if (values == null) return false;
            for (var index = 0; index < values.Count; index++)
                if (values[index] == candidate) return true;
            return false;
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
