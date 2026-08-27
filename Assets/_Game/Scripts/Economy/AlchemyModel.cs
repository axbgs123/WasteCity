using System.Collections.Generic;
using System.Linq;
using WasteCity.Combat;

namespace WasteCity.Economy
{
    public static class ElixirUseModel
    {
        public static bool TryUse(ResourceInventory inventory,HealthModel city,IEnumerable<HealthModel> buildings)
        {
            return TryUse(
                inventory,
                city,
                buildings,
                fleshElixirUnlocked: false,
                mutationSamplePercent: 100,
                out _);
        }

        public static bool TryUse(
            ResourceInventory inventory,
            HealthModel city,
            IEnumerable<HealthModel> buildings,
            bool fleshElixirUnlocked,
            int mutationSamplePercent,
            out int backlashDamage)
        {
            backlashDamage = 0;
            if(inventory==null||city==null)return false;var targets=buildings?.Where(value=>value!=null).ToArray()??System.Array.Empty<HealthModel>();
            if(city.Current>=city.Maximum&&targets.All(value=>value.Current>=value.Maximum))return false;if(!inventory.TrySpend(ResourceIds.Elixir,1))return false;
            int multiplier = fleshElixirUnlocked ? 3 : 1;
            city.Heal(250 * multiplier);foreach(var health in targets)health.Heal(100 * multiplier);
            int sample = System.Math.Max(0, System.Math.Min(99, mutationSamplePercent));
            if(fleshElixirUnlocked && sample < 20)
                backlashDamage = city.ApplyTrueDamage(150);
            return true;
        }
    }
}
