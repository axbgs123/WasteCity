using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Combat
{
    public sealed class CrystalBroodmotherReinforcementDefinition
    {
        public CrystalBroodmotherReinforcementDefinition(
            EnemyArchetype archetype,
            int count)
        {
            Archetype = archetype;
            Count = Math.Max(1, count);
        }

        public EnemyArchetype Archetype { get; }
        public int Count { get; }
    }

    public sealed class CrystalBroodmotherPhaseDefinition
    {
        private readonly ReadOnlyCollection<
            CrystalBroodmotherReinforcementDefinition> reinforcements;

        public CrystalBroodmotherPhaseDefinition(
            string stablePhaseId,
            float healthRatioThreshold,
            params CrystalBroodmotherReinforcementDefinition[] reinforcements)
        {
            StablePhaseId = stablePhaseId;
            HealthRatioThreshold = healthRatioThreshold;
            this.reinforcements = Array.AsReadOnly(reinforcements == null
                ? Array.Empty<CrystalBroodmotherReinforcementDefinition>()
                : (CrystalBroodmotherReinforcementDefinition[])
                    reinforcements.Clone());
        }

        public string StablePhaseId { get; }
        public float HealthRatioThreshold { get; }
        public IReadOnlyList<CrystalBroodmotherReinforcementDefinition>
            Reinforcements => reinforcements;
    }

    public static class CrystalBroodmotherCatalog
    {
        public static EnemyDefinition Definition =>
            EnemyCatalog.CrystalBroodmother;
        public static string StableArchetypeId => Definition.Id.Value;
        public static int MaximumHealth => Definition.MaximumHealth;
        public static float MovementSpeedCellsPerSecond => Definition.MoveSpeed;
        public static float DamagePerSecond => Definition.DamagePerSecond;
        public static float AttackRangeCells => Definition.AttackRange;
        public const float FixedStepSeconds = .1f;

        private static readonly ReadOnlyCollection<
            CrystalBroodmotherPhaseDefinition> phases =
            Array.AsReadOnly(new[]
            {
                new CrystalBroodmotherPhaseDefinition(
                    "phase-70",
                    .7f,
                    new CrystalBroodmotherReinforcementDefinition(
                        EnemyArchetype.CrystalBeast,
                        4)),
                new CrystalBroodmotherPhaseDefinition(
                    "phase-35",
                    .35f,
                    new CrystalBroodmotherReinforcementDefinition(
                        EnemyArchetype.Gnawer,
                        6),
                    new CrystalBroodmotherReinforcementDefinition(
                        EnemyArchetype.Howler,
                        2)),
            });

        public static IReadOnlyList<CrystalBroodmotherPhaseDefinition> Phases =>
            phases;
    }
}
