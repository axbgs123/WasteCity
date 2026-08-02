using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Combat;

namespace WasteCity.Economy
{
    public sealed class ElixirController : MonoBehaviour
    {
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private HealthComponent cityHealth;
        public string LastResult { get; private set; }
        private void Update()
        {
            if(Keyboard.current==null||!Keyboard.current.cKey.wasPressedThisFrame)return;
            var targets=UnityEngine.Object.FindObjectsOfType<BuildingRuntime>().Where(value=>value.Construction.IsComplete&&Vector2.Distance(value.transform.position,city.transform.position)<=8f).Select(value=>value.Health.Value);
            LastResult=ElixirUseModel.TryUse(economy.Inventory,cityHealth.Value,targets)?"服用灵丹：核心 +250，附近建筑 +100":"无法服用灵丹：库存为空";
        }
        private void OnGUI()
        {
            int stock=economy==null?0:economy.Inventory.Get(ResourceIds.Elixir);if(stock<=0&&string.IsNullOrEmpty(LastResult))return;
            GUI.Box(new Rect(18,407,430,48),$"灵丹 {stock} · [C] 应急治疗\n{LastResult}");
        }
    }
}
