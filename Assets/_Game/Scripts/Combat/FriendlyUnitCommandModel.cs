using System;

namespace WasteCity.Combat
{
    public enum FriendlyUnitKind
    {
        Puppet,
        Behemoth,
        Controlled
    }

    public readonly struct FriendlyRallyPoint
    {
        public FriendlyRallyPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    public sealed class FriendlyUnitCommandModel
    {
        private float rallyX;
        private float rallyY;

        public bool HasFixedRally { get; private set; }
        public int PuppetLosses { get; private set; }
        public int BehemothLosses { get; private set; }
        public int ControlledLosses { get; private set; }
        public int TotalLosses => PuppetLosses + BehemothLosses + ControlledLosses;

        public FriendlyRallyPoint ResolveRally(float cityX, float cityY)
        {
            return HasFixedRally
                ? new FriendlyRallyPoint(rallyX, rallyY)
                : new FriendlyRallyPoint(cityX, cityY);
        }

        public void SetRally(float x, float y)
        {
            rallyX = x;
            rallyY = y;
            HasFixedRally = true;
        }

        public void ClearRally()
        {
            HasFixedRally = false;
        }

        public void RecordLoss(FriendlyUnitKind kind)
        {
            switch (kind)
            {
                case FriendlyUnitKind.Puppet:
                    PuppetLosses++;
                    break;
                case FriendlyUnitKind.Behemoth:
                    BehemothLosses++;
                    break;
                case FriendlyUnitKind.Controlled:
                    ControlledLosses++;
                    break;
            }
        }

        public void Restore(bool hasFixedRally, float x, float y, int puppetLosses, int behemothLosses, int controlledLosses)
        {
            HasFixedRally = hasFixedRally;
            rallyX = x;
            rallyY = y;
            PuppetLosses = Math.Max(0, puppetLosses);
            BehemothLosses = Math.Max(0, behemothLosses);
            ControlledLosses = Math.Max(0, controlledLosses);
        }
    }
}
