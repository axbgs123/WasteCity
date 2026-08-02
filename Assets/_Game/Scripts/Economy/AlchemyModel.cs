using System.Collections.Generic;
using System.Linq;
using WasteCity.Combat;

namespace WasteCity.Economy
{
    public static class ElixirUseModel
    {
        public static bool TryUse(ResourceInventory inventory,HealthModel city,IEnumerable<HealthModel> buildings)
        {
            if(inventory==null||city==null)return false;var targets=buildings?.Where(value=>value!=null).ToArray()??System.Array.Empty<HealthModel>();
            if(city.Current>=city.Maximum&&targets.All(value=>value.Current>=value.Maximum))return false;if(!inventory.TrySpend(ResourceIds.Elixir,1))return false;
            city.Heal(250);foreach(var health in targets)health.Heal(100);return true;
        }
    }
}
