using System;
using System.Collections.Generic;
using System.Linq;

namespace WasteCity.Combat
{
    public enum WavePhase { Idle, Warning, Spawning, Active }

    public readonly struct WaveEntry
    {
        public EnemyArchetype Archetype { get; }
        public int Count { get; }
        public WaveEntry(EnemyArchetype archetype,int count){Archetype=archetype;Count=Math.Max(0,count);}
    }

    public sealed class WaveDefinition
    {
        public int Trigger { get; }
        public float WarningSeconds { get; }
        public float SpawnSeconds { get; }
        public IReadOnlyList<WaveEntry> Entries { get; }
        public int TotalCount => Entries.Sum(value=>value.Count);
        public WaveDefinition(int trigger,float warning,float spawn,params WaveEntry[] entries){Trigger=trigger;WarningSeconds=Math.Max(0,warning);SpawnSeconds=Math.Max(.1f,spawn);Entries=entries??Array.Empty<WaveEntry>();}
    }

    public static class WaveCatalog
    {
        public static readonly WaveDefinition Tutorial=new WaveDefinition(0,15,40,new WaveEntry(EnemyArchetype.Gnawer,8));
        public static readonly WaveDefinition Directed=new WaveDefinition(30,60,60,new WaveEntry(EnemyArchetype.Gnawer,18),new WaveEntry(EnemyArchetype.CrystalBeast,4));
        public static readonly WaveDefinition HighRisk=new WaveDefinition(60,75,75,new WaveEntry(EnemyArchetype.Gnawer,24),new WaveEntry(EnemyArchetype.CrystalBeast,6),new WaveEntry(EnemyArchetype.Howler,4),new WaveEntry(EnemyArchetype.Burrower,1));
        public static readonly WaveDefinition Boss=new WaveDefinition(90,90,5,new WaveEntry(EnemyArchetype.CrystalBroodmother,1));
        public static readonly WaveDefinition[] All={Tutorial,Directed,HighRisk,Boss};
        public static WaveDefinition ForTrigger(int trigger)=>All.FirstOrDefault(value=>value.Trigger==trigger);
    }

    public sealed class WaveDirectorModel
    {
        private readonly Queue<WaveDefinition> pending=new Queue<WaveDefinition>();
        private readonly HashSet<int> scheduled=new HashSet<int>();
        private List<EnemyArchetype> sequence=new List<EnemyArchetype>();
        private int nextSpawn,defeated;
        private float spawnClock;
        public WaveDefinition Current { get; private set; }
        public WavePhase Phase { get; private set; }
        public float WarningRemaining { get; private set; }
        public int SpawnedCount => nextSpawn;
        public int DefeatedCount => defeated;
        public int PendingWaveCount => pending.Count;

        public bool Schedule(int trigger)
        {
            WaveDefinition definition=WaveCatalog.ForTrigger(trigger);if(definition==null||!scheduled.Add(trigger))return false;
            pending.Enqueue(definition);if(Current==null)BeginNext();return true;
        }

        public void Tick(float delta,List<EnemyArchetype> output)
        {
            if(Current==null||output==null)return;float remaining=Math.Max(0,delta);
            if(Phase==WavePhase.Warning)
            {
                float consumed=Math.Min(WarningRemaining,remaining);WarningRemaining-=consumed;remaining-=consumed;
                if(WarningRemaining<=0)Phase=WavePhase.Spawning;
            }
            if(Phase!=WavePhase.Spawning)return;
            float cadence=Current.SpawnSeconds/Math.Max(1,sequence.Count);spawnClock+=remaining;
            while(nextSpawn<sequence.Count&&spawnClock>=cadence){spawnClock-=cadence;output.Add(sequence[nextSpawn++]);}
            if(nextSpawn>=sequence.Count)Phase=WavePhase.Active;
        }

        public bool RegisterDefeat(int trigger)
        {
            if(Current==null||Current.Trigger!=trigger)return false;defeated++;
            if(Phase!=WavePhase.Active||defeated<Math.Max(1,(int)Math.Ceiling(Current.TotalCount*.9f)))return false;
            Current=null;Phase=WavePhase.Idle;BeginNext();return true;
        }

        private void BeginNext()
        {
            if(pending.Count==0)return;Current=pending.Dequeue();sequence=Interleave(Current.Entries);nextSpawn=defeated=0;spawnClock=0;WarningRemaining=Current.WarningSeconds;Phase=WarningRemaining>0?WavePhase.Warning:WavePhase.Spawning;
        }

        private static List<EnemyArchetype> Interleave(IReadOnlyList<WaveEntry> entries)
        {
            var result=new List<EnemyArchetype>();var remaining=entries.Select(value=>value.Count).ToArray();bool added;
            do{added=false;for(int i=0;i<entries.Count;i++)if(remaining[i]-->0){result.Add(entries[i].Archetype);added=true;}}while(added);return result;
        }
    }
}
