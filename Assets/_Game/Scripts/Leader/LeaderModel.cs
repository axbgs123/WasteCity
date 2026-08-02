using System;

namespace WasteCity.Leader
{
    public enum OverloadPhase { Ready, Boosting, Lockout, Cooldown }

    public sealed class LeaderOverloadModel
    {
        private readonly Func<float> boostMultiplier;
        public float CooldownRemaining { get; private set; }
        public float BoostRemaining { get; private set; }
        public float LockoutRemaining { get; private set; }
        public OverloadPhase Phase=>BoostRemaining>0f?OverloadPhase.Boosting:LockoutRemaining>0f?OverloadPhase.Lockout:CooldownRemaining>0f?OverloadPhase.Cooldown:OverloadPhase.Ready;
        public float FireRateMultiplier=>Phase==OverloadPhase.Boosting?boostMultiplier():Phase==OverloadPhase.Lockout?0f:1f;
        public LeaderOverloadModel(Func<float> multiplier)=>boostMultiplier=multiplier;
        public bool TryActivate(){if(CooldownRemaining>0f)return false;CooldownRemaining=30f;BoostRemaining=5f;LockoutRemaining=0f;return true;}
        public void Tick(float delta)
        {
            delta=Math.Max(0f,delta);CooldownRemaining=Math.Max(0f,CooldownRemaining-delta);
            if(BoostRemaining>0f){float used=Math.Min(delta,BoostRemaining);BoostRemaining-=used;delta-=used;if(BoostRemaining<=0f)LockoutRemaining=3f;}
            if(delta>0f&&LockoutRemaining>0f)LockoutRemaining=Math.Max(0f,LockoutRemaining-delta);
        }
        public void Restore(float cooldown,float boost,float lockout){CooldownRemaining=Math.Max(0f,cooldown);BoostRemaining=Math.Max(0f,boost);LockoutRemaining=Math.Max(0f,lockout);}
    }

    public sealed class LeaderModel
    {
        public bool Recruited { get; private set; }
        public bool Injured { get; private set; }
        public float AssemblerEfficiency=>Recruited?1.25f:1f;
        public LeaderOverloadModel Overload { get; }
        public LeaderModel(){Overload=new LeaderOverloadModel(()=>Injured?1.35f:1.75f);}
        public bool Recruit(bool immediate){if(Recruited)return false;Recruited=true;Injured=!immediate;return true;}
        public void Tick(float delta){if(Recruited)Overload.Tick(delta);}
        public void Restore(bool recruited,bool injured,float cooldown,float boost,float lockout){Recruited=recruited;Injured=injured;Overload.Restore(cooldown,boost,lockout);}
    }
}
