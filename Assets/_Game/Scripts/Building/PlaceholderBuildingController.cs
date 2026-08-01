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
using WasteCity.Legacy;
using WasteCity.Presentation;
using WasteCity.Research;

namespace WasteCity.Building
{
    public sealed class PlaceholderBuildingController : MonoBehaviour
    {
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderWorldView world;
        [SerializeField] private FormalPopulationController population;
        [SerializeField] private ResearchController research;
        private const int GridWidth=32,GridHeight=24;
        private BuildingGrid grid = new BuildingGrid(GridWidth, GridHeight);
        private readonly LogisticsNetworkModel logistics = new LogisticsNetworkModel();
        private bool active;
        private int selected;
        private static Sprite square;
        private readonly Dictionary<BuildingRuntime, PlacedBuilding> placements = new Dictionary<BuildingRuntime, PlacedBuilding>();
        [SerializeField] private LocalHasteController localTime;
        public int PlacedCount => grid.Count;
        public bool HasLocalTimeSource => localTime != null;
        public int DisconnectedCount => placements.Keys.Count(runtime=>runtime.Construction.IsComplete&&!runtime.HasLogistics);
        public event Action<BuildingDefinition> BuildingPlaced;
        public event Action<BuildingDefinition> BuildingRemoved;
        public string LastAction { get; private set; }
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
            if (Mouse.current != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) TryRepairAtMouse();
            if (city.Deployment.Mode != CityMode.Fortress) active = false;
        }
        private void PlaceAtMouse()
        {
            if(!CanBuild(BuildingCatalog.All[selected],out _))return;
            Vector2 worldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            int gridX = Mathf.FloorToInt(worldPosition.x - city.transform.position.x + 8f);
            int gridY = Mathf.FloorToInt(worldPosition.y - city.transform.position.y + 6f);
            int mapX = Mathf.FloorToInt(worldPosition.x + world.Model.Width * 0.5f); int mapY = Mathf.FloorToInt(worldPosition.y + world.Model.Height * 0.5f);
            bool resource = mapX >= 0 && mapY >= 0 && mapX < world.Model.Width && mapY < world.Model.Height && world.Model.Get(mapX, mapY).HasResource;
            if (!grid.TryPlace(BuildingCatalog.All[selected], gridX, gridY, economy.Inventory, resource, out var placed)) return;
            CreateRuntime(placed);
        }
        private BuildingRuntime CreateRuntime(PlacedBuilding placed, int health = -1, float remaining = -1f, float repairRemaining = 0f)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var item = new GameObject($"Placeholder_{placed.Definition.Name}"); item.transform.SetParent(transform);
            item.transform.position = new Vector3(city.transform.position.x - 8f + placed.X + placed.Definition.Width * 0.5f, city.transform.position.y - 6f + placed.Y + placed.Definition.Height * 0.5f, -1f);
            item.transform.localScale = new Vector3(placed.Definition.Width * 0.9f, placed.Definition.Height * 0.9f, 1f);
            var renderer = item.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sortingOrder = 8; renderer.color = ColorFor(placed.Definition.Id.Value);
            VisualSlot.Attach(item, placed.Definition.Id.Value, renderer, renderer.color);
            item.AddComponent<HealthComponent>(); var runtime = item.AddComponent<BuildingRuntime>(); runtime.Configure(placed.Definition, economy, population, population, localTime, city);
            placements[runtime] = placed;
            runtime.Completed += OnCompleted; runtime.Removed += OnRemoved;
            if (health >= 0) runtime.RestoreState(health, remaining, repairRemaining);
            RefreshLogistics();
            return runtime;
        }
        private static Color ColorFor(string id) => id.Contains("mining") ? Color.yellow : id.Contains("housing") ? Color.green : id.Contains("warehouse") ? Color.cyan : id.Contains("wall") ? Color.gray : id.Contains("research") ? Color.magenta : id.Contains("smelter") ? new Color(.8f,.3f,.1f) : id.Contains("assembler") ? Color.blue : Color.white;
        private void OnCompleted(BuildingRuntime runtime)
        {
            RefreshLogistics();
            if (runtime.Definition.Id.Value == "core.building.machine-gun-turret") runtime.gameObject.AddComponent<PlaceholderTurret>().Configure(economy, runtime, localTime);
            BuildingPlaced?.Invoke(runtime.Definition);
        }
        private void OnRemoved(BuildingRuntime runtime) { if (placements.TryGetValue(runtime, out var placed)) { grid.Remove(placed); placements.Remove(runtime); } if (runtime.Construction != null && runtime.Construction.IsComplete) BuildingRemoved?.Invoke(runtime.Definition); RefreshLogistics(); }
        public BuildingSnapshot[] CaptureSnapshots() => placements.Select(pair => new BuildingSnapshot { definitionId = pair.Key.Definition.Id.Value, x = pair.Value.X, y = pair.Value.Y, health = pair.Key.Health.Value.Current, constructionRemaining = pair.Key.Construction.Remaining, repairRemaining = pair.Key.Repair?.Remaining ?? 0f }).ToArray();
        public void SetLocalTimeSource(LocalHasteController value) { localTime = value; foreach (var runtime in placements.Keys) runtime.SetLocalTimeSource(value); }
        public BuildingRuntime FindNearest(Vector2 point, float radius) { BuildingRuntime nearest=null;float best=radius*radius;foreach(var runtime in placements.Keys){float sqr=((Vector2)runtime.transform.position-point).sqrMagnitude;if(sqr<=best){best=sqr;nearest=runtime;}}return nearest; }
        public BuildingRuntime FindAtGrid(int x, int y) { foreach(var pair in placements)if(pair.Value.X==x&&pair.Value.Y==y)return pair.Key;return null; }
        public bool TryGetGrid(BuildingRuntime runtime, out int x, out int y) { if(runtime!=null&&placements.TryGetValue(runtime,out var placed)){x=placed.X;y=placed.Y;return true;}x=y=-1;return false; }
        public int CompletedCount(string id)=>placements.Keys.Count(runtime=>runtime.Construction.IsComplete&&runtime.Definition.Id.Value==id);
        public bool CanBuild(BuildingDefinition definition,out string reason)=>BuildingUnlockModel.IsUnlocked(definition,population.Model.Current,id=>research.Model.IsCompleted(new WasteCity.Content.StableId(id)),CompletedCount,out reason);
        public void WorldToGrid(Vector2 point, out int x, out int y) { x=Mathf.FloorToInt(point.x-city.transform.position.x+8f);y=Mathf.FloorToInt(point.y-city.transform.position.y+6f); }
        public SpatialTemplateEntry[] CaptureTemplate(int originX, int originY) => placements.Where(pair => pair.Value.X >= originX && pair.Value.Y >= originY && pair.Value.X + pair.Value.Definition.Width <= originX + 3 && pair.Value.Y + pair.Value.Definition.Height <= originY + 3).Select(pair => new SpatialTemplateEntry { definitionId = pair.Value.Definition.Id.Value, dx = pair.Value.X - originX, dy = pair.Value.Y - originY }).ToArray();
        public bool TryStampTemplate(IReadOnlyList<SpatialTemplateEntry> entries, int originX, int originY)
        {
            if (city.Deployment.Mode != CityMode.Fortress || entries == null || entries.Count == 0) return false; var pending=new List<(BuildingDefinition definition,int x,int y)>();var costs=new Dictionary<string,int>();
            foreach(var entry in entries){var definition=BuildingCatalog.All.FirstOrDefault(value=>value.Id.Value==entry.definitionId);int x=originX+entry.dx,y=originY+entry.dy;if(definition==null||!grid.CanPlace(definition,x,y)||!TemplateResourceCondition(definition,x,y))return false;pending.Add((definition,x,y));costs[definition.CostId]=costs.TryGetValue(definition.CostId,out int cost)?cost+definition.Cost:definition.Cost;}
            for(int i=0;i<pending.Count;i++)for(int j=i+1;j<pending.Count;j++)if(Overlaps(pending[i],pending[j]))return false;
            foreach(var cost in costs)if(!economy.Inventory.CanSpend(cost.Key,cost.Value))return false;foreach(var cost in costs)economy.Inventory.TrySpend(cost.Key,cost.Value);
            foreach(var value in pending)if(grid.TryRestore(value.definition,value.x,value.y,out var placed))CreateRuntime(placed);return true;
        }
        private bool TemplateResourceCondition(BuildingDefinition definition,int x,int y){if(!definition.RequiresResourceNode)return true;Vector2 point=new Vector2(city.transform.position.x-8f+x+definition.Width*.5f,city.transform.position.y-6f+y+definition.Height*.5f);int mapX=Mathf.FloorToInt(point.x+world.Model.Width*.5f),mapY=Mathf.FloorToInt(point.y+world.Model.Height*.5f);return mapX>=0&&mapY>=0&&mapX<world.Model.Width&&mapY<world.Model.Height&&world.Model.Get(mapX,mapY).HasResource;}
        private static bool Overlaps((BuildingDefinition definition,int x,int y) a,(BuildingDefinition definition,int x,int y) b)=>a.x<b.x+b.definition.Width&&a.x+a.definition.Width>b.x&&a.y<b.y+b.definition.Height&&a.y+a.definition.Height>b.y;
        public void RestoreSnapshots(BuildingSnapshot[] snapshots)
        {
            foreach (var runtime in placements.Keys.ToArray()) { runtime.PrepareForRestore(); Destroy(runtime.gameObject); }
            placements.Clear(); grid = new BuildingGrid(GridWidth, GridHeight);
            if (snapshots == null) return;
            foreach (var snapshot in snapshots)
            {
                var definition = BuildingCatalog.All.FirstOrDefault(value => value.Id.Value == snapshot.definitionId);
                if (definition != null && grid.TryRestore(definition, snapshot.x, snapshot.y, out var placed)) CreateRuntime(placed, snapshot.health, snapshot.constructionRemaining, snapshot.repairRemaining);
            }
        }
        private void TryRepairAtMouse()
        {
            Vector2 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()); BuildingRuntime nearest = null; float best = 2.25f;
            foreach (var runtime in placements.Keys) { float sqr = ((Vector2)runtime.transform.position - point).sqrMagnitude; if (sqr < best) { best = sqr; nearest = runtime; } }
            if (nearest == null) { LastAction = "维修：请将鼠标指向受损建筑"; return; }
            LastAction = nearest.TryStartRepair() ? $"维修开始：{nearest.Definition.Name} · 生物质 -1" : "维修无法开始：建筑未受损、施工中、已在维修或生物质不足";
        }
        private void RefreshLogistics()
        {
            var points=placements.Where(pair=>pair.Key.Construction!=null&&pair.Key.Construction.IsComplete).Select(pair=>new LogisticsPoint(pair.Key.GetInstanceID().ToString(),pair.Value.X+pair.Value.Definition.Width/2,pair.Value.Y+pair.Value.Definition.Height/2)).ToArray();logistics.Rebuild(points);
            foreach(var pair in placements)pair.Key.SetLogistics(pair.Key.Construction!=null&&pair.Key.Construction.IsComplete&&logistics.IsConnected(pair.Key.GetInstanceID().ToString()));
        }
        private void OnGUI()
        {
            if (active){CanBuild(BuildingCatalog.All[selected],out string lockReason);GUI.Box(new Rect(18, Screen.height - 72f, 850f, 52f), $"建造：1采矿 2住房 3仓库 4墙 5研究 6冶炼 7装配 8机枪塔 · 当前 {BuildingCatalog.All[selected].Name}{(lockReason==null?" · 已解锁":$" · 锁定：{lockReason}")} · 左键放置");}
            if (!string.IsNullOrEmpty(LastAction)) GUI.Box(new Rect(18, Screen.height - 185f, 620f, 45f), LastAction);
            if(DisconnectedCount>0)GUI.Box(new Rect(18,Screen.height-292f,520f,42f),$"物流警告：{DisconnectedCount} 座建筑断网，效果和库存访问暂停");
        }
    }
    [Serializable]
    public sealed class BuildingSnapshot { public string definitionId; public int x, y, health; public float constructionRemaining, repairRemaining; }
}
