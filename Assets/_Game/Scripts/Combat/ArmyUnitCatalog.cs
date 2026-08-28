using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WasteCity.Economy;

namespace WasteCity.Combat
{
    public sealed class ArmyUnitDefinition
    {
        internal ArmyUnitDefinition(
            string id,
            string chineseName,
            string sourceBuildingId,
            ResourceAmount[] manufactureCosts,
            float manufactureSeconds,
            int capacityPerBuilding,
            ResourceAmount[] maintenanceCosts,
            float maintenanceSeconds,
            int maximumHealth,
            int damage,
            DamageType damageType,
            ArmorType armor,
            float moveSpeed)
        {
            Id = id;
            ChineseName = chineseName;
            SourceBuildingId = sourceBuildingId;
            ManufactureCosts = Freeze(manufactureCosts);
            ManufactureSeconds = manufactureSeconds;
            CapacityPerBuilding = capacityPerBuilding;
            MaintenanceCosts = Freeze(maintenanceCosts);
            MaintenanceSeconds = maintenanceSeconds;
            MaximumHealth = maximumHealth;
            Damage = damage;
            DamageType = damageType;
            Armor = armor;
            MoveSpeed = moveSpeed;
        }

        public string Id { get; }
        public string ChineseName { get; }
        public string SourceBuildingId { get; }
        public IReadOnlyList<ResourceAmount> ManufactureCosts { get; }
        public float ManufactureSeconds { get; }
        public int CapacityPerBuilding { get; }
        public IReadOnlyList<ResourceAmount> MaintenanceCosts { get; }
        public float MaintenanceSeconds { get; }
        public int MaximumHealth { get; }
        public int Damage { get; }
        public DamageType DamageType { get; }
        public ArmorType Armor { get; }
        public float MoveSpeed { get; }

        private static IReadOnlyList<ResourceAmount> Freeze(
            ResourceAmount[] values)
        {
            return Array.AsReadOnly(
                values == null
                    ? Array.Empty<ResourceAmount>()
                    : (ResourceAmount[])values.Clone());
        }
    }

    public static class ArmyUnitCatalog
    {
        public const string CombatPuppetId =
            "cultivation.unit.combat-puppet";
        public const string BredBehemothId =
            "biological.unit.bred-behemoth";
        public const string PsionicMechId =
            "fusion.unit.psionic-mech";
        public const string BioMechanicalBehemothId =
            "fusion.unit.bio-mechanical-behemoth";

        public static readonly ArmyUnitDefinition CombatPuppet = Unit(
            CombatPuppetId,
            "战斗傀儡",
            "cultivation.building.puppet-workshop",
            Costs((ResourceIds.Alloy, 1), (ResourceIds.SpiritIron, 1)),
            20f,
            3,
            Costs((ResourceIds.EnergyCrystal, 1)),
            60f,
            100,
            12,
            DamageType.TrueEssence,
            ArmorType.Light,
            2.5f);

        public static readonly ArmyUnitDefinition BredBehemoth = Unit(
            BredBehemothId,
            "培育巨兽",
            "biological.building.behemoth-pen",
            Costs(
                (ResourceIds.BoneSteel, 2),
                (ResourceIds.BiomassConcentrate, 3)),
            35f,
            1,
            Costs((ResourceIds.Biomass, 1)),
            45f,
            320,
            24,
            DamageType.Biological,
            ArmorType.BiologicalShell,
            1.2f);

        public static readonly ArmyUnitDefinition PsionicMech = Unit(
            PsionicMechId,
            "灵能机甲",
            "bridge.building.psionic-mech-factory",
            Costs(
                (ResourceIds.ControlChip, 2),
                (ResourceIds.PsionicAmplifier, 1),
                (ResourceIds.EnergyCell, 1)),
            30f,
            2,
            Costs((ResourceIds.EnergyCell, 1)),
            60f,
            220,
            20,
            DamageType.Psionic,
            ArmorType.PsionicShield,
            1.8f);

        public static readonly ArmyUnitDefinition BioMechanicalBehemoth = Unit(
            BioMechanicalBehemothId,
            "半机械巨兽",
            "bridge.building.bio-hangar",
            Costs(
                (ResourceIds.BiologicalWeapon, 2),
                (ResourceIds.MechanicalComponent, 2),
                (ResourceIds.ActiveBiomass, 1)),
            45f,
            1,
            Costs(
                (ResourceIds.Biomass, 1),
                (ResourceIds.EnergyCell, 1)),
            60f,
            380,
            28,
            DamageType.Biological,
            ArmorType.Heavy,
            1.1f);

        private static readonly ReadOnlyCollection<ArmyUnitDefinition> all =
            Array.AsReadOnly(new[]
            {
                CombatPuppet,
                BredBehemoth,
                PsionicMech,
                BioMechanicalBehemoth,
            });
        private static readonly IReadOnlyDictionary<string, ArmyUnitDefinition>
            byId = BuildIndex();

        public static IReadOnlyList<ArmyUnitDefinition> All => all;

        public static ArmyUnitDefinition Find(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   byId.TryGetValue(id, out ArmyUnitDefinition value)
                ? value
                : null;
        }

        private static IReadOnlyDictionary<string, ArmyUnitDefinition>
            BuildIndex()
        {
            var result = new Dictionary<string, ArmyUnitDefinition>(
                StringComparer.Ordinal);
            for (var index = 0; index < all.Count; index++)
                result.Add(all[index].Id, all[index]);
            return new ReadOnlyDictionary<string, ArmyUnitDefinition>(result);
        }

        private static ArmyUnitDefinition Unit(
            string id,
            string chineseName,
            string sourceBuildingId,
            ResourceAmount[] manufactureCosts,
            float manufactureSeconds,
            int capacityPerBuilding,
            ResourceAmount[] maintenanceCosts,
            float maintenanceSeconds,
            int maximumHealth,
            int damage,
            DamageType damageType,
            ArmorType armor,
            float moveSpeed)
        {
            return new ArmyUnitDefinition(
                id,
                chineseName,
                sourceBuildingId,
                manufactureCosts,
                manufactureSeconds,
                capacityPerBuilding,
                maintenanceCosts,
                maintenanceSeconds,
                maximumHealth,
                damage,
                damageType,
                armor,
                moveSpeed);
        }

        private static ResourceAmount[] Costs(
            params (string id, int amount)[] values)
        {
            var result = new ResourceAmount[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                result[index] = new ResourceAmount(
                    values[index].id,
                    values[index].amount);
            }
            return result;
        }
    }
}
