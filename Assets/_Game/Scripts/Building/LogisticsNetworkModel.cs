using System;
using System.Collections.Generic;

namespace WasteCity.Building
{
    public readonly struct LogisticsPoint
    {
        public string Key { get; } public int X { get; } public int Y { get; }
        public LogisticsPoint(string key,int x,int y){Key=key;X=x;Y=y;}
    }
    public sealed class LogisticsNetworkModel
    {
        private readonly int range;
        private readonly int coreX,coreY;
        private readonly HashSet<string> connected=new HashSet<string>();
        public LogisticsNetworkModel(int coreX=8,int coreY=6,int range=8){this.coreX=coreX;this.coreY=coreY;this.range=Math.Max(1,range);}
        public void Rebuild(IReadOnlyList<LogisticsPoint> points)
        {
            connected.Clear();if(points==null)return;var frontier=new Queue<int>();
            for(int i=0;i<points.Count;i++)if(InRange(coreX,coreY,points[i].X,points[i].Y)){connected.Add(points[i].Key);frontier.Enqueue(i);}
            while(frontier.Count>0){var source=points[frontier.Dequeue()];for(int i=0;i<points.Count;i++)if(!connected.Contains(points[i].Key)&&InRange(source.X,source.Y,points[i].X,points[i].Y)){connected.Add(points[i].Key);frontier.Enqueue(i);}}
        }
        public bool IsConnected(string key)=>!string.IsNullOrEmpty(key)&&connected.Contains(key);
        private bool InRange(int ax,int ay,int bx,int by)=>Math.Max(Math.Abs(ax-bx),Math.Abs(ay-by))<=range;
    }
}
