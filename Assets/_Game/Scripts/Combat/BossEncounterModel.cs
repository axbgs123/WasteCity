using System;
using System.Collections.Generic;

namespace WasteCity.Combat
{
    public enum BossPhase { One=1,Two=2,Three=3 }
    public enum BossActionType { SummonGnawers,GroundSlamWarning,GroundSlam,GrantShield,SummonHowlers,CrystalHazard,ChargeWarning,Charge }
    public readonly struct BossAction
    {
        public BossActionType Type { get; }
        public int Amount { get; }
        public float Duration { get; }
        public BossAction(BossActionType type,int amount=0,float duration=0){Type=type;Amount=amount;Duration=duration;}
    }
    [Serializable] public sealed class BossEncounterSnapshot { public int phase=1;public float summonTimer=12,primaryTimer=10;public bool warned; }

    public sealed class BossEncounterModel
    {
        private float summonTimer=12f,primaryTimer=10f;
        private bool warned;
        public BossPhase Phase { get; private set; }=BossPhase.One;
        public float SpeedMultiplier=>Phase==BossPhase.Three?1.4f:1f;
        public void Tick(float delta,float healthRatio,List<BossAction> output)
        {
            if(output==null)return;float step=Math.Max(0,delta);healthRatio=Math.Max(0,Math.Min(1,healthRatio));
            if(Phase==BossPhase.One&&healthRatio<=.7f){Phase=BossPhase.Two;summonTimer=float.MaxValue;primaryTimer=14;warned=false;output.Add(new BossAction(BossActionType.GrantShield,600));output.Add(new BossAction(BossActionType.SummonHowlers,3));}
            if(Phase==BossPhase.Two&&healthRatio<=.4f){Phase=BossPhase.Three;summonTimer=10;primaryTimer=15;warned=false;}
            if(Phase==BossPhase.One)TickPhase(step,12,2,10,1.5f,BossActionType.GroundSlamWarning,BossActionType.GroundSlam,60,output);
            else if(Phase==BossPhase.Two){primaryTimer-=step;if(primaryTimer<=0){output.Add(new BossAction(BossActionType.CrystalHazard,0,6));primaryTimer+=14;}}
            else TickPhase(step,10,3,15,2,BossActionType.ChargeWarning,BossActionType.Charge,0,output);
        }
        private void TickPhase(float delta,float summonInterval,int summonCount,float primaryInterval,float warning,BossActionType warningType,BossActionType actionType,int amount,List<BossAction> output)
        {
            summonTimer-=delta;while(summonTimer<=0){output.Add(new BossAction(BossActionType.SummonGnawers,summonCount));summonTimer+=summonInterval;}
            primaryTimer-=delta;if(!warned&&primaryTimer<=warning){warned=true;output.Add(new BossAction(warningType,0,warning));}
            if(primaryTimer<=0){output.Add(new BossAction(actionType,amount));primaryTimer+=primaryInterval;warned=false;}
        }
        public BossEncounterSnapshot Capture()=>new BossEncounterSnapshot{phase=(int)Phase,summonTimer=summonTimer,primaryTimer=primaryTimer,warned=warned};
        public void Restore(BossEncounterSnapshot snapshot){if(snapshot==null)return;Phase=Enum.IsDefined(typeof(BossPhase),snapshot.phase)?(BossPhase)snapshot.phase:BossPhase.One;summonTimer=Math.Max(0,snapshot.summonTimer);primaryTimer=Math.Max(0,snapshot.primaryTimer);warned=snapshot.warned;}
    }
}
