using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WasteCity.Combat
{
    [Serializable] public sealed class WaveDirectorSnapshot
    {
        public int currentTrigger=-1,phase,nextSpawn,defeated;
        public float warningRemaining,spawnClock;
        public int[] pendingTriggers,scheduledTriggers;
    }
    public enum WavePhase { Idle, Warning, Spawning, Active }

    internal sealed class WaveDirectorPersistenceState
    {
        private readonly ReadOnlyCollection<int> pendingTriggers;
        private readonly ReadOnlyCollection<int> scheduledTriggers;

        public WaveDirectorPersistenceState(
            int currentTrigger,
            WavePhase phase,
            int nextSpawn,
            int defeated,
            float warningRemaining,
            float spawnClock,
            IEnumerable<int> pendingTriggers,
            IEnumerable<int> scheduledTriggers)
        {
            CurrentTrigger = currentTrigger;
            Phase = phase;
            NextSpawn = nextSpawn;
            Defeated = defeated;
            WarningRemaining = warningRemaining;
            SpawnClock = spawnClock;
            this.pendingTriggers = Array.AsReadOnly(
                (pendingTriggers ?? Enumerable.Empty<int>()).ToArray());
            this.scheduledTriggers = Array.AsReadOnly(
                (scheduledTriggers ?? Enumerable.Empty<int>()).ToArray());
        }

        public int CurrentTrigger { get; }
        public WavePhase Phase { get; }
        public int NextSpawn { get; }
        public int Defeated { get; }
        public float WarningRemaining { get; }
        public float SpawnClock { get; }
        public IReadOnlyList<int> PendingTriggers => pendingTriggers;
        public IReadOnlyList<int> ScheduledTriggers => scheduledTriggers;
    }

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
        public static readonly WaveDefinition PostAdvance=new WaveDefinition(120,45,90,new WaveEntry(EnemyArchetype.Gnawer,18),new WaveEntry(EnemyArchetype.CrystalBeast,7),new WaveEntry(EnemyArchetype.Howler,5),new WaveEntry(EnemyArchetype.Burrower,2));
        public static readonly WaveDefinition[] All={Tutorial,Directed,HighRisk,Boss,PostAdvance};
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
        public float SpawnClock => spawnClock;
        public int ScheduledWaveCount => scheduled.Count;
        public float WarningMultiplier { get; private set; } = 1f;

        public void SetWarningMultiplier(float multiplier) => WarningMultiplier = Math.Max(1f, multiplier);

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

        public WaveDirectorSnapshot Capture()=>new WaveDirectorSnapshot{currentTrigger=Current?.Trigger??-1,phase=(int)Phase,nextSpawn=nextSpawn,defeated=defeated,warningRemaining=WarningRemaining,spawnClock=spawnClock,pendingTriggers=pending.Select(value=>value.Trigger).ToArray(),scheduledTriggers=scheduled.ToArray()};

        internal bool TryRestoreForPersistence(
            WaveDirectorPersistenceState state,
            out string error)
        {
            if (!TryValidatePersistenceState(
                state,
                out WaveDefinition current,
                out WaveDefinition[] restoredPending,
                out HashSet<int> restoredScheduled,
                out error))
            {
                return false;
            }

            pending.Clear();
            for (int index = 0; index < restoredPending.Length; index++)
                pending.Enqueue(restoredPending[index]);
            scheduled.Clear();
            foreach (int trigger in restoredScheduled)
                scheduled.Add(trigger);
            Current = current;
            sequence = current == null
                ? new List<EnemyArchetype>()
                : Interleave(current.Entries);
            nextSpawn = state.NextSpawn;
            defeated = state.Defeated;
            WarningRemaining = state.WarningRemaining;
            spawnClock = state.SpawnClock;
            Phase = state.Phase;
            return true;
        }

        public void Restore(WaveDirectorSnapshot snapshot)
        {
            pending.Clear();scheduled.Clear();Current=null;sequence.Clear();nextSpawn=defeated=0;spawnClock=WarningRemaining=0;Phase=WavePhase.Idle;
            if(snapshot==null)return;
            if(snapshot.scheduledTriggers!=null)foreach(int trigger in snapshot.scheduledTriggers)if(WaveCatalog.ForTrigger(trigger)!=null)scheduled.Add(trigger);
            if(snapshot.pendingTriggers!=null)foreach(int trigger in snapshot.pendingTriggers){var definition=WaveCatalog.ForTrigger(trigger);if(definition!=null)pending.Enqueue(definition);}
            Current=WaveCatalog.ForTrigger(snapshot.currentTrigger);
            if(Current==null){BeginNext();return;}
            scheduled.Add(Current.Trigger);sequence=Interleave(Current.Entries);nextSpawn=Math.Max(0,Math.Min(sequence.Count,snapshot.nextSpawn));defeated=Math.Max(0,Math.Min(Current.TotalCount,snapshot.defeated));
            spawnClock=Math.Max(0,snapshot.spawnClock);WarningRemaining=Math.Max(0,snapshot.warningRemaining);Phase=Enum.IsDefined(typeof(WavePhase),snapshot.phase)?(WavePhase)snapshot.phase:WavePhase.Warning;
        }

        private void BeginNext()
        {
            if(pending.Count==0)return;Current=pending.Dequeue();sequence=Interleave(Current.Entries);nextSpawn=defeated=0;spawnClock=0;WarningRemaining=Current.WarningSeconds*WarningMultiplier;Phase=WarningRemaining>0?WavePhase.Warning:WavePhase.Spawning;
        }

        private static List<EnemyArchetype> Interleave(IReadOnlyList<WaveEntry> entries)
        {
            var result=new List<EnemyArchetype>();var remaining=entries.Select(value=>value.Count).ToArray();bool added;
            do{added=false;for(int i=0;i<entries.Count;i++)if(remaining[i]-->0){result.Add(entries[i].Archetype);added=true;}}while(added);return result;
        }

        private static bool TryValidatePersistenceState(
            WaveDirectorPersistenceState state,
            out WaveDefinition current,
            out WaveDefinition[] restoredPending,
            out HashSet<int> restoredScheduled,
            out string error)
        {
            current = null;
            restoredPending = null;
            restoredScheduled = null;
            error = null;
            if (state == null)
                return Fail("Wave persistence state is required.", out error);
            if (!Enum.IsDefined(typeof(WavePhase), state.Phase))
                return Fail("Wave phase is invalid.", out error);
            if (!IsFiniteNonNegative(state.WarningRemaining) ||
                !IsFiniteNonNegative(state.SpawnClock))
            {
                return Fail("Wave clocks must be finite and non-negative.", out error);
            }
            if (state.NextSpawn < 0 || state.Defeated < 0)
                return Fail("Wave counters cannot be negative.", out error);

            var scheduledSet = new HashSet<int>();
            for (int index = 0; index < state.ScheduledTriggers.Count; index++)
            {
                int trigger = state.ScheduledTriggers[index];
                if (WaveCatalog.ForTrigger(trigger) == null)
                    return Fail("A scheduled wave trigger is unknown.", out error);
                if (!scheduledSet.Add(trigger))
                    return Fail("Scheduled wave triggers must be unique.", out error);
            }

            var pendingSet = new HashSet<int>();
            var pendingDefinitions =
                new WaveDefinition[state.PendingTriggers.Count];
            for (int index = 0; index < state.PendingTriggers.Count; index++)
            {
                int trigger = state.PendingTriggers[index];
                WaveDefinition definition = WaveCatalog.ForTrigger(trigger);
                if (definition == null || !scheduledSet.Contains(trigger))
                    return Fail("A pending wave is not scheduled.", out error);
                if (!pendingSet.Add(trigger))
                    return Fail("Pending wave triggers must be unique.", out error);
                pendingDefinitions[index] = definition;
            }

            if (state.CurrentTrigger < 0)
            {
                if (state.CurrentTrigger != -1 || state.Phase != WavePhase.Idle ||
                    state.PendingTriggers.Count != 0 ||
                    state.WarningRemaining != 0f)
                {
                    return Fail("Idle wave state is inconsistent.", out error);
                }
            }
            else
            {
                current = WaveCatalog.ForTrigger(state.CurrentTrigger);
                if (current == null || !scheduledSet.Contains(state.CurrentTrigger))
                    return Fail("Current wave is not scheduled.", out error);
                if (pendingSet.Contains(state.CurrentTrigger))
                    return Fail("Current wave cannot also be pending.", out error);
                if (state.NextSpawn > current.TotalCount ||
                    state.Defeated > state.NextSpawn)
                {
                    return Fail("Wave counters exceed the current wave.", out error);
                }

                float cadence = current.SpawnSeconds /
                    Math.Max(1, current.TotalCount);
                switch (state.Phase)
                {
                    case WavePhase.Warning:
                        if (state.WarningRemaining <= 0f ||
                            state.SpawnClock != 0f || state.NextSpawn != 0 ||
                            state.Defeated != 0)
                        {
                            return Fail("Warning wave state is inconsistent.", out error);
                        }
                        break;
                    case WavePhase.Spawning:
                        if (state.WarningRemaining != 0f ||
                            state.NextSpawn >= current.TotalCount ||
                            state.SpawnClock >= cadence)
                        {
                            return Fail("Spawning wave state is inconsistent.", out error);
                        }
                        break;
                    case WavePhase.Active:
                        if (state.WarningRemaining != 0f ||
                            state.NextSpawn != current.TotalCount)
                        {
                            return Fail("Active wave state is inconsistent.", out error);
                        }
                        break;
                    default:
                        return Fail("A current wave cannot be idle.", out error);
                }
            }

            restoredPending = pendingDefinitions;
            restoredScheduled = scheduledSet;
            return true;
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) &&
                value >= 0f;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
