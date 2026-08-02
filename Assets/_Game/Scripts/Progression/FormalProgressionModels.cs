using System;
using System.Collections.Generic;
using WasteCity.Research;

namespace WasteCity.Progression
{
    public sealed class ObservationModel
    {
        private readonly Queue<string> reasons = new Queue<string>();
        private readonly HashSet<int> reached = new HashSet<int>();
        public float Value { get; private set; }
        public IReadOnlyCollection<string> RecentReasons => reasons;
        public event Action<int> ThresholdReached;
        public void Add(string reason, float amount)
        {
            if (amount <= 0f) return; Value = Math.Min(100f, Value + amount);
            reasons.Enqueue(reason); while (reasons.Count > 3) reasons.Dequeue();
            foreach (int threshold in new[] { 30, 60, 90 }) if (Value >= threshold && reached.Add(threshold)) ThresholdReached?.Invoke(threshold);
        }
        public void Restore(float value) { Value = Math.Max(0f, Math.Min(100f, value)); reached.Clear();foreach(int threshold in new[]{30,60,90})if(Value>=threshold)reached.Add(threshold); }
    }

    public sealed class EraTrackModel
    {
        private readonly int[] values = new int[4];
        private readonly bool[] triggered = new bool[4];
        public int Get(DevelopmentRoute route) => values[(int)route];
        public void Add(DevelopmentRoute route, int amount) => values[(int)route] = Math.Min(100, Math.Max(0, values[(int)route] + amount));
        public bool TryTrigger(DevelopmentRoute route, int researchedNodes)
        {
            int index = (int)route; if (triggered[index] || values[index] < 40 || researchedNodes < 6) return false;
            triggered[index] = true; values[index] = Math.Min(100, values[index] + 10); return true;
        }
    }

    public sealed class CivilizationModel
    {
        public int Level { get; private set; } = 1;
        public bool TryAdvance(int completedResearch, int buildings)
        {
            if (Level >= 10 || completedResearch < Level || buildings < Level) return false;
            Level++; return true;
        }
        public bool TryAdvanceFormal(bool requirementsMet){if(!requirementsMet||Level!=1)return false;Level=2;return true;}
        public void Restore(int level) => Level = Math.Max(1, Math.Min(10, level));
    }

    public static class CivilizationAdvanceRequirements
    {
        public static bool Meets(bool legacyAnalysis,int turretCount,bool bossDefeated,bool productionRunning)=>legacyAnalysis&&turretCount>=2&&bossDefeated&&productionRunning;
    }
}
