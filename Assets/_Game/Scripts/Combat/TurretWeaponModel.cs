using System;
using WasteCity.Economy;

namespace WasteCity.Combat
{
    public sealed class TurretWeaponModel
    {
        private readonly float damagePerSecond;
        private readonly float ammoDuration;
        private readonly DamageType damageType;
        private readonly string consumableId;
        private float ammoRemaining;
        private float damageRemainder;
        public bool OutOfAmmo { get; private set; }
        public TurretWeaponModel(float dps, float ammoDuration,DamageType damage=DamageType.Physical,string consumable=ResourceIds.Ammunition) { damagePerSecond = Math.Max(0f, dps); this.ammoDuration = Math.Max(.1f, ammoDuration);damageType=damage;consumableId=consumable; }
        public int Tick(float delta, ResourceInventory inventory, HealthModel target, ArmorType armor,float damageMultiplier=1f)
        {
            if (target == null || target.IsDead || delta <= 0f) return 0;
            if (ammoRemaining <= 0f)
            {
                if (!inventory.TrySpend(consumableId, 1)) { OutOfAmmo = true; return 0; }
                ammoRemaining = ammoDuration; OutOfAmmo = false;
            }
            float active = Math.Min(delta, ammoRemaining); ammoRemaining -= active; damageRemainder += damagePerSecond * active*Math.Max(0f,damageMultiplier);
            int raw = (int)damageRemainder; if (raw <= 0) return 0; damageRemainder -= raw;
            return target.Apply(raw, damageType, armor);
        }
    }
}
