using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Economy
{
    public sealed class FormalEconomyController : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private PlaceholderWorldView world;
        public ResourceInventory Inventory { get; private set; }
        public string LastHarvestedId { get; private set; }
        public int LastHarvestedAmount { get; private set; }
        private void Awake() => Inventory = new ResourceInventory(150);
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) HarvestCurrentCell();
        }
        public int HarvestCurrentCell()
        {
            if (city.Deployment.Mode != CityMode.Fortress || world.Model == null) return 0;
            int x = Mathf.FloorToInt(city.transform.position.x + world.Model.Width * 0.5f);
            int y = Mathf.FloorToInt(city.transform.position.y + world.Model.Height * 0.5f);
            int extracted = world.Model.Harvest(x, y, 10, out string id);
            if (extracted <= 0) return 0;
            int accepted = Inventory.Add(id, extracted); LastHarvestedId = id; LastHarvestedAmount = accepted; return accepted;
        }
    }
}
