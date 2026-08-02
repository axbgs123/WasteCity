using System;

namespace WasteCity.Core
{
    public sealed class SessionStatisticsModel
    {
        public float ElapsedSeconds { get; private set; }
        public int Kills { get; private set; }
        public float HighestObservation { get; private set; }
        public int ProductionCycles { get; private set; }
        public int BuildingLosses { get; private set; }
        public int Rescues { get; private set; }
        public int DelayedRescues { get; private set; }
        public bool RetreatedDuringBoss { get; private set; }
        public void Tick(float delta,float observation){ElapsedSeconds+=Math.Max(0f,delta);HighestObservation=Math.Max(HighestObservation,observation);}
        public void AddKill()=>Kills++;
        public void AddProduction(int cycles)=>ProductionCycles+=Math.Max(0,cycles);
        public void AddBuildingLoss()=>BuildingLosses++;
        public void AddRescue(bool immediate){Rescues++;if(!immediate)DelayedRescues++;}
        public void MarkRetreat()=>RetreatedDuringBoss=true;
        public void Restore(float elapsed,int kills,float highest,int production,int losses,int rescues,int delayed,bool retreated){ElapsedSeconds=Math.Max(0,elapsed);Kills=Math.Max(0,kills);HighestObservation=Math.Max(0,highest);ProductionCycles=Math.Max(0,production);BuildingLosses=Math.Max(0,losses);Rescues=Math.Max(0,rescues);DelayedRescues=Math.Max(0,delayed);RetreatedDuringBoss=retreated;}
    }
}
