using System;
using System.Collections.Generic;

namespace WasteCity.World
{
    public sealed class RescueSite
    {
        public int X { get; } public int Y { get; } public bool Completed { get; private set; }
        public RescueSite(int x, int y) { X = x; Y = y; }
        public bool Complete() { if (Completed) return false; Completed = true; return true; }
        public void Restore(bool completed) => Completed = completed;
    }
    public sealed class RescueSiteModel
    {
        public IReadOnlyList<RescueSite> Sites { get; }
        public RescueSiteModel(int width, int height, WorldSeed seed, int count = 5)
        {
            var sites = new List<RescueSite>(); var occupied = new HashSet<int>();
            for (int i = 0; sites.Count < Math.Max(1, count) && i < 1000; i++)
            {
                int x = seed.Sample(i, 31, 551) % Math.Max(1, width); int y = seed.Sample(i, 47, 552) % Math.Max(1, height); int key = y * width + x;
                if (occupied.Add(key)) sites.Add(new RescueSite(x, y));
            }
            Sites = sites;
        }
        public bool[] Capture() { var result = new bool[Sites.Count]; for (int i = 0; i < result.Length; i++) result[i] = Sites[i].Completed; return result; }
        public void Restore(bool[] values) { if (values == null) return; for (int i = 0; i < Sites.Count && i < values.Length; i++) Sites[i].Restore(values[i]); }
        public int FindFirstIncomplete(Func<RescueSite,bool> predicate){for(int i=0;i<Sites.Count;i++)if(!Sites[i].Completed&&(predicate==null||predicate(Sites[i])))return i;return -1;}
    }
    public static class RescueRules{public static int BiomassCost(bool immediate,bool remoteCommunication)=>immediate?5:remoteCommunication?0:2;}
}
