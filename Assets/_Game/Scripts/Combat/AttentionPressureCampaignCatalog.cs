using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WasteCity.Combat
{
    public static class AttentionPressureCampaignCatalog
    {
        public static readonly SingleCityDefenseCampaignDefinition Directional =
            Define("core.attention-encounter.directional-attack", 60f,
                new[] { CampaignSpawnDirection.East, CampaignSpawnDirection.North },
                new WaveEntry(EnemyArchetype.Gnawer, 18),
                new WaveEntry(EnemyArchetype.CrystalBeast, 4));

        public static readonly SingleCityDefenseCampaignDefinition HighRisk =
            Define("core.attention-encounter.high-risk-attack", 75f,
                new[] { CampaignSpawnDirection.East, CampaignSpawnDirection.North,
                    CampaignSpawnDirection.South, CampaignSpawnDirection.West },
                new WaveEntry(EnemyArchetype.Gnawer, 24),
                new WaveEntry(EnemyArchetype.CrystalBeast, 6),
                new WaveEntry(EnemyArchetype.Howler, 4),
                new WaveEntry(EnemyArchetype.Burrower, 1));

        public static readonly SingleCityDefenseCampaignDefinition Boss =
            Define("core.attention-encounter.crystalline-broodmother", 5f,
                new[] { CampaignSpawnDirection.East },
                new WaveEntry(EnemyArchetype.CrystalBroodmother, 1));

        private static readonly ReadOnlyCollection<
            SingleCityDefenseCampaignDefinition> all = Array.AsReadOnly(
                new[] { Directional, HighRisk, Boss });

        public static IReadOnlyList<SingleCityDefenseCampaignDefinition> All => all;

        public static SingleCityDefenseCampaignDefinition Find(string id)
        {
            for (var index = 0; index < all.Count; index++)
                if (string.Equals(all[index].Id, id, StringComparison.Ordinal))
                    return all[index];
            return null;
        }

        private static SingleCityDefenseCampaignDefinition Define(
            string id, float spawnSeconds,
            CampaignSpawnDirection[] directions, params WaveEntry[] entries)
        {
            return new SingleCityDefenseCampaignDefinition(id,
                new CampaignWaveDefinition(1, 0f, spawnSeconds,
                    directions, entries));
        }
    }
}
