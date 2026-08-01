using WasteCity.World;

namespace WasteCity.Legacy
{
    public sealed class ForesightFlashModel
    {
        private readonly WorldSeed seed;
        private readonly float secondsPerDay;
        private int flashedDay;
        public ForesightFlashModel(WorldSeed seed, float secondsPerDay) { this.seed = seed; this.secondsPerDay = secondsPerDay; }
        public float ScheduledSecond(int day) => secondsPerDay * (.15f + (seed.Sample(day, 67, 911) % 7000) / 10000f);
        public bool TryFlash(int day, float secondsIntoDay)
        { if (day <= flashedDay || secondsIntoDay < ScheduledSecond(day)) return false; flashedDay = day; return true; }
        public void Restore(int lastFlashedDay) => flashedDay = lastFlashedDay;
        public int LastFlashedDay => flashedDay;
    }
}
