using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;
using System;
using WasteCity.Combat;
using WasteCity.Population;
using System.Collections.Generic;
using System.Linq;

namespace WasteCity.Building
{
    public sealed class PlaceholderBuildingController : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderWorldView world;
        [SerializeField] private FormalPopulationController population;
        private BuildingGrid grid = new BuildingGrid(16, 12);
        private bool active;
        private int selected;
        private static Sprite square;
        private readonly Dictionary<BuildingRuntime, PlacedBuilding> placements = new Dictionary<BuildingRuntime, PlacedBuilding>();
        public int PlacedCount => grid.Count;
        public event Action<BuildingDefinition> BuildingPlaced;
        public event Action<BuildingDefinition> BuildingRemoved;
        private void Update()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.bKey.wasPressedThisFrame) active = city.Deployment.Mode == CityMode.Fortress && !active;
                if (Keyboard.current.digit1Key.wasPressedThisFrame) selected = 0;
                if (Keyboard.current.digit2Key.wasPressedThisFrame) selected = 1;
                if (Keyboard.current.digit3Key.wasPressedThisFrame) selected = 2;
                if (Keyboard.current.digit4Key.wasPressedThisFrame) selected = 3;
                if (Keyboard.current.digit5Key.wasPressedThisFrame) selected = 4;
                if (Keyboard.current.digit6Key.wasPressedThisFrame) selected = 5;
                if (Keyboard.current.digit7Key.wasPressedThisFrame) selected = 6;
                if (Keyboard.current.digit8Key.wasPressedThisFrame) selected = 7;
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
            CreateRuntime(placed);
        }
        private BuildingRuntime CreateRuntime(PlacedBuilding placed, int health = -1, float remaining = -1f)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var item = new GameObject($"Placeholder_{placed.Definition.Name}"); item.transform.SetParent(transform);
            item.transform.position = new Vector3(city.transform.position.x - 8f + placed.X + placed.Definition.Width * 0.5f, city.transform.position.y - 6f + placed.Y + placed.Definition.Height * 0.5f, -1f);
            item.transform.localScale = new Vector3(placed.Definition.Width * 0.9f, placed.Definition.Height * 0.9f, 1f);
            var renderer = item.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sortingOrder = 8; renderer.color = ColorFor(placed.Definition.Id.Value);
            item.AddComponent<HealthComponent>(); var runtime = item.AddComponent<BuildingRuntime>(); runtime.Configure(placed.Definition, economy, population, population);
            placements[runtime] = placed;
            runtime.Completed += OnCompleted; runtime.Removed += OnRemoved;
            if (health >= 0) runtime.RestoreState(health, remaining);
            return runtime;
        }
        private static Color ColorFor(string id) => id.Contains("mining") ? Color.yellow : id.Contains("housing") ? Color.green : id.Contains("warehouse") ? Color.cyan : id.Contains("wall") ? Color.gray : id.Contains("research") ? Color.magenta : id.Contains("smelter") ? new Color(.8f,.3f,.1f) : id.Contains("assembler") ? Color.blue : Color.white;
        private void OnCompleted(BuildingRuntime runtime)
        {
            if (runtime.Definition.Id.Value == "core.building.machine-gun-turret") runtime.gameObject.AddComponent<PlaceholderTurret>().Configure(economy);
            BuildingPlaced?.Invoke(runtime.Definition);
        }
        private void OnRemoved(BuildingRuntime runtime) { if (placements.TryGetValue(runtime, out var placed)) { grid.Remove(placed); placements.Remove(runtime); } if (runtime.Construction != null && runtime.Construction.IsComplete) BuildingRemoved?.Invoke(runtime.Definition); }
        public BuildingSnapshot[] CaptureSnapshots() => placements.Select(pair => new BuildingSnapshot { definitionId = pair.Key.Definition.Id.Value, x = pair.Value.X, y = pair.Value.Y, health = pair.Key.Health.Value.Current, constructionRemaining = pair.Key.Construction.Remaining }).ToArray();
        public void RestoreSnapshots(BuildingSnapshot[] snapshots)
        {
            foreach (var runtime in placements.Keys.ToArray()) { runtime.PrepareForRestore(); Destroy(runtime.gameObject); }
            placements.Clear(); grid = new BuildingGrid(16, 12);
            if (snapshots == null) return;
            foreach (var snapshot in snapshots)
            {
                var definition = BuildingCatalog.All.FirstOrDefault(value => value.Id.Value == snapshot.definitionId);
                if (definition != null && grid.TryRestore(definition, snapshot.x, snapshot.y, out var placed)) CreateRuntime(placed, snapshot.health, snapshot.constructionRemaining);
            }
        }
        private void OnGUI()
        {
            if (active) GUI.Box(new Rect(18, Screen.height - 72f, 780f, 52f), $"建造：1采矿 2住房 3仓库 4墙 5研究 6冶炼 7装配 8机枪塔 · 当前 {BuildingCatalog.All[selected].Name} · 左键放置");
        }
    }
    [Serializable]
    public sealed class BuildingSnapshot { public string definitionId; public int x, y, health; public float constructionRemaining; }
}
