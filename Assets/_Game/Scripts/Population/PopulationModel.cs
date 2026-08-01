using System;

namespace WasteCity.Population
{
    public sealed class PopulationModel
    {
        public int Current { get; private set; }
        public int Capacity { get; private set; }
        public int EffectiveWorkers => Math.Min(Current, Capacity);
        public int Waiting => Math.Max(0, Current - Capacity);
        public float ProductivityMultiplier => Math.Min(2.5f, .5f + EffectiveWorkers * .005f);
        public PopulationModel(int current = 100, int capacity = 150)
        {
            Current = Math.Max(0, current); Capacity = Math.Max(0, capacity);
        }
        public void AddPeople(int amount) => Current = Math.Max(0, Current + amount);
        public void AddCapacity(int amount) => Capacity = Math.Max(0, Capacity + amount);
    }
}
