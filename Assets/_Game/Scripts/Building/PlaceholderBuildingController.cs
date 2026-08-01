using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Building
{
    public sealed class PlaceholderBuildingController : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderWorldView world;
        private BuildingGrid grid = new BuildingGrid(16, 12);
        private bool active;
        private int selected;
        private static Sprite square;
        public int PlacedCount => grid.Count;
        private void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.bKey.wasPressedThisFrame) active = city.Deployment.Mode == CityMode.Fortress && !active;
                if (Keyboard.current.digit1Key.wasPressedThisFrame) selected = 0;
                if (Keyboard.current.digit2Key.wasPressedThisFrame) selected = 1;
                if (Keyboard.current.digit3Key.wasPressedThisFrame) selected = 2;
                if (Keyboard.current.digit4Key.wasPressedThisFrame) selected = 3;
            }
            if (active && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) PlaceAtMouse();
            if (city.Deployment.Mode != CityMode.Fortress) active = false;
        }
        private void PlaceAtMouse()
        {
            Vector2 worldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            int gridX = Mathf.FloorToInt(worldPosition.x - city.transform.position.x + 8f);
            int gridY = Mathf.FloorToInt(worldPosition.y - city.transform.position.y + 6f);
            int mapX = Mathf.FloorToInt(worldPosition.x + world.Model.Width * 0.5f); int mapY = Mathf.FloorToInt(worldPosition.y + world.Model.Height * 0.5f);
            bool resource = mapX >= 0 && mapY >= 0 && mapX < world.Model.Width && mapY < world.Model.Height && world.Model.Get(mapX, mapY).HasResource;
            if (!grid.TryPlace(BuildingCatalog.All[selected], gridX, gridY, economy.Inventory, resource, out var placed)) return;
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var item = new GameObject($"Placeholder_{placed.Definition.Name}"); item.transform.SetParent(transform);
            item.transform.position = new Vector3(city.transform.position.x - 8f + placed.X + placed.Definition.Width * 0.5f, city.transform.position.y - 6f + placed.Y + placed.Definition.Height * 0.5f, -1f);
            item.transform.localScale = new Vector3(placed.Definition.Width * 0.9f, placed.Definition.Height * 0.9f, 1f);
            var renderer = item.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sortingOrder = 8; renderer.color = selected == 0 ? Color.yellow : selected == 1 ? Color.cyan : selected == 2 ? Color.gray : Color.magenta;
        }
        private void OnGUI()
        {
            if (active) GUI.Box(new Rect(18, Screen.height - 72f, 520f, 52f), $"建造占位模式：1采矿站 2仓库 3城墙 4研究站 · 当前 {BuildingCatalog.All[selected].Name} · 左键放置");
        }
    }
}
