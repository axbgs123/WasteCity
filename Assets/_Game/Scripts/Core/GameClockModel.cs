using System;

namespace WasteCity.Core
{
    public sealed class GameClockModel
    {
        public float SecondsPerDay { get; }
        public int Day { get; private set; } = 1;
        public float SecondsIntoDay { get; private set; }
        public GameClockModel(float secondsPerDay = 600f) => SecondsPerDay = Math.Max(10f, secondsPerDay);
        public void Tick(float delta)
        {
            SecondsIntoDay += Math.Max(0f, delta);
            while (SecondsIntoDay >= SecondsPerDay) { SecondsIntoDay -= SecondsPerDay; Day++; }
        }
        public void Restore(int day, float secondsIntoDay) { Day = Math.Max(1, day); SecondsIntoDay = Math.Max(0f, Math.Min(SecondsPerDay - .001f, secondsIntoDay)); }
    }
}
