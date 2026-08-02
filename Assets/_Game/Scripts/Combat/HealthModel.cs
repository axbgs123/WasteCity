using System;

namespace WasteCity.Combat
{
    public sealed class HealthModel
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public int Shield { get; private set; }
        public bool IsDead => Current <= 0;
        private int physicalDamagePercentOverride = -1;
        public event Action<int> Damaged;
        public event Action Died;
        public HealthModel(int maximum) { Maximum = Math.Max(1, maximum); Current = Maximum; }
        public int Apply(int rawDamage, DamageType type, ArmorType armor)
        {
            if (IsDead) return 0; int incoming=type==DamageType.Physical&&physicalDamagePercentOverride>=0?Math.Max(0,rawDamage)*physicalDamagePercentOverride/100:DamageMatrix.Apply(rawDamage,type,armor);
            int absorbed=Math.Min(Shield,incoming);Shield-=absorbed;int amount=Math.Min(Current,incoming-absorbed);
            if(amount>0){Current-=amount;Damaged?.Invoke(amount);if(IsDead)Died?.Invoke();}return absorbed+amount;
        }
        public void Restore(int current) => Current = Math.Max(1, Math.Min(Maximum, current));
        public void Restore(int current,int shield){Restore(current);Shield=Math.Max(0,shield);}
        public int GrantShield(int amount){int accepted=Math.Max(0,amount);Shield+=accepted;return accepted;}
        public int GrantShield(int amount,int maximumShield){int accepted=Math.Min(Math.Max(0,amount),Math.Max(0,maximumShield)-Shield);if(accepted<=0)return 0;Shield+=accepted;return accepted;}
        public void SetPhysicalDamagePercent(int percent)=>physicalDamagePercentOverride=percent<0?-1:Math.Min(100,percent);
        public int Heal(int amount) { if (amount <= 0 || IsDead) return 0; int accepted = Math.Min(amount, Maximum - Current); Current += accepted; return accepted; }
        public void SetMaximum(int maximum,bool preserveMissingHealth=false){int next=Math.Max(1,maximum);int missing=Maximum-Current;Maximum=next;Current=preserveMissingHealth?Math.Max(1,next-missing):Math.Min(Current,next);}
    }
}
