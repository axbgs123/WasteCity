using System;
using System.Collections.Generic;

namespace WasteCity.Combat
{
    public enum CampaignSpawnDirection { East, North, South, West }

    public sealed class CampaignWaveDefinition
    {
        private readonly int totalCount;

        public int Number { get; }
        public float WarningSeconds { get; }
        public float SpawnSeconds { get; }
        public IReadOnlyList<WaveEntry> Entries { get; }
        public IReadOnlyList<CampaignSpawnDirection> Directions { get; }
        public int TotalCount => totalCount;

        public CampaignWaveDefinition(int number,float warningSeconds,float spawnSeconds,CampaignSpawnDirection[] directions,params WaveEntry[] entries)
        {
            Number=number;
            WarningSeconds=warningSeconds;
            SpawnSeconds=spawnSeconds;
            Directions=Array.AsReadOnly(directions==null?Array.Empty<CampaignSpawnDirection>():(CampaignSpawnDirection[])directions.Clone());
            Entries=Array.AsReadOnly(entries==null?Array.Empty<WaveEntry>():(WaveEntry[])entries.Clone());
            for(var index=0;index<Entries.Count;index++)totalCount+=Entries[index].Count;
        }
    }

    public static class CampaignWaveCatalog
    {
        public const string Id="campaign.single-city-defense.v1";

        private static readonly CampaignWaveDefinition[] Waves=
        {
            new CampaignWaveDefinition(1,15,40,new[]{CampaignSpawnDirection.East},new WaveEntry(EnemyArchetype.Gnawer,8)),
            new CampaignWaveDefinition(2,20,45,new[]{CampaignSpawnDirection.East},new WaveEntry(EnemyArchetype.Gnawer,10)),
            new CampaignWaveDefinition(3,20,50,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.North},new WaveEntry(EnemyArchetype.Gnawer,12),new WaveEntry(EnemyArchetype.CrystalBeast,2)),
            new CampaignWaveDefinition(4,25,50,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.North},new WaveEntry(EnemyArchetype.Gnawer,14),new WaveEntry(EnemyArchetype.CrystalBeast,3)),
            new CampaignWaveDefinition(5,25,55,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.South},new WaveEntry(EnemyArchetype.Gnawer,16),new WaveEntry(EnemyArchetype.CrystalBeast,4),new WaveEntry(EnemyArchetype.Howler,2)),
            new CampaignWaveDefinition(6,30,55,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.North,CampaignSpawnDirection.South},new WaveEntry(EnemyArchetype.Gnawer,18),new WaveEntry(EnemyArchetype.CrystalBeast,5),new WaveEntry(EnemyArchetype.Howler,3)),
            new CampaignWaveDefinition(7,30,60,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.North,CampaignSpawnDirection.South,CampaignSpawnDirection.West},new WaveEntry(EnemyArchetype.Gnawer,20),new WaveEntry(EnemyArchetype.CrystalBeast,6),new WaveEntry(EnemyArchetype.Howler,4)),
            new CampaignWaveDefinition(8,35,60,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.North,CampaignSpawnDirection.South,CampaignSpawnDirection.West},new WaveEntry(EnemyArchetype.Gnawer,22),new WaveEntry(EnemyArchetype.CrystalBeast,8),new WaveEntry(EnemyArchetype.Howler,5)),
            new CampaignWaveDefinition(9,40,65,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.North,CampaignSpawnDirection.South,CampaignSpawnDirection.West},new WaveEntry(EnemyArchetype.Gnawer,24),new WaveEntry(EnemyArchetype.CrystalBeast,9),new WaveEntry(EnemyArchetype.Howler,7)),
            new CampaignWaveDefinition(10,45,75,new[]{CampaignSpawnDirection.East,CampaignSpawnDirection.North,CampaignSpawnDirection.South,CampaignSpawnDirection.West},new WaveEntry(EnemyArchetype.Gnawer,28),new WaveEntry(EnemyArchetype.CrystalBeast,10),new WaveEntry(EnemyArchetype.Howler,8))
        };

        private static readonly IReadOnlyList<CampaignWaveDefinition>
            ReadOnlyWaves=Array.AsReadOnly(Waves);

        public static IReadOnlyList<CampaignWaveDefinition> All => ReadOnlyWaves;
    }
}
