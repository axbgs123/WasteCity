using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Content;

namespace WasteCity.Leader.CivilizationExpansion
{
    public sealed class CharacterDefinition
    {
        internal CharacterDefinition(
            string id,
            string displayName,
            string specialization,
            int prestige,
            string routeInclinationId,
            string visualId,
            int maximumHealth,
            int initialLoyalty,
            string[] initialEquipmentIds)
        {
            Id = new StableId(id);
            DisplayName = Require(displayName, nameof(displayName));
            Specialization = Require(specialization, nameof(specialization));
            if (prestige < 0 || prestige > 100)
                throw new ArgumentOutOfRangeException(nameof(prestige));
            Prestige = prestige;
            RouteInclinationId = new StableId(routeInclinationId);
            VisualId = new StableId(visualId);
            MaximumHealth = Math.Max(1, maximumHealth);
            InitialLoyalty = ClampPercent(initialLoyalty);
            string[] equipment = initialEquipmentIds == null
                ? Array.Empty<string>()
                : (string[])initialEquipmentIds.Clone();
            for (var index = 0; index < equipment.Length; index++)
                _ = new StableId(equipment[index]);
            InitialEquipmentIds = Array.AsReadOnly(equipment);
        }

        public StableId Id { get; }
        public string DisplayName { get; }
        public string Specialization { get; }
        public int Prestige { get; }
        public StableId RouteInclinationId { get; }
        public StableId VisualId { get; }
        public int MaximumHealth { get; }
        public int InitialLoyalty { get; }
        public IReadOnlyList<string> InitialEquipmentIds { get; }

        private static string Require(string value, string name)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("角色定义文本不能为空", name)
                : value;
        }

        private static int ClampPercent(int value)
        {
            return Math.Max(0, Math.Min(100, value));
        }
    }

    public static class CharacterCatalog
    {
        public const string MainCityId = "core.city.000001";
        public const string CenJinId = "core.character.cen-jin";
        public const string LinXiId = "core.character.lin-xi";
        public const string HanGuId = "core.character.han-gu";

        public static CharacterDefinition CenJin { get; } = Define(
            CenJinId,
            "岑烬",
            "工程/维修",
            70,
            "core.route.technology",
            "art.character.cen-jin",
            100,
            80,
            "core.equipment.field-tool");

        public static CharacterDefinition LinXi { get; } = Define(
            LinXiId,
            "林溪",
            "研究/管理",
            55,
            "core.route.psionics",
            "art.character.lin-xi",
            90,
            75,
            "core.equipment.medical-kit");

        public static CharacterDefinition HanGu { get; } = Define(
            HanGuId,
            "韩骨",
            "军事/远征",
            65,
            "core.route.biological",
            "art.character.han-gu",
            120,
            55,
            "core.equipment.guard-rifle");

        private static readonly ReadOnlyCollection<CharacterDefinition> all =
            Array.AsReadOnly(new[] { CenJin, LinXi, HanGu });

        private static readonly IReadOnlyDictionary<string, CharacterDefinition>
            byId = BuildLookup();

        public static IReadOnlyList<CharacterDefinition> All => all;

        public static CharacterDefinition Find(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && byId.TryGetValue(
                id,
                out CharacterDefinition definition)
                    ? definition
                    : null;
        }

        private static CharacterDefinition Define(
            string id,
            string displayName,
            string specialization,
            int prestige,
            string routeInclinationId,
            string visualId,
            int maximumHealth,
            int initialLoyalty,
            params string[] equipmentIds)
        {
            return new CharacterDefinition(
                id,
                displayName,
                specialization,
                prestige,
                routeInclinationId,
                visualId,
                maximumHealth,
                initialLoyalty,
                equipmentIds);
        }

        private static IReadOnlyDictionary<string, CharacterDefinition>
            BuildLookup()
        {
            var result = new Dictionary<string, CharacterDefinition>(
                StringComparer.Ordinal);
            for (var index = 0; index < all.Count; index++)
            {
                CharacterDefinition definition = all[index];
                if (!result.TryAdd(definition.Id.Value, definition))
                    throw new InvalidOperationException("重复角色 ID");
            }
            return new ReadOnlyDictionary<string, CharacterDefinition>(result);
        }
    }
}
