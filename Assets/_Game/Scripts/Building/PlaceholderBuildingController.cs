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
using WasteCity.Leader;
using WasteCity.Progression;

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
        [SerializeField] private FormalLeaderController leader;
        [SerializeField] private FormalTechnologyRouteController technology;
        [SerializeField] private FormalProgressionController progression;
        public int PlacedCount => grid.Count;
        public bool HasLocalTimeSource => localTime != null;
        public int DisconnectedCount => placements.Keys.Count(runtime=>runtime.Construction.IsComplete&&!runtime.HasLogistics);
        public ResearchController Research => research;
        public event Action<BuildingDefinition> BuildingPlaced;
        public event Action<BuildingDefinition> BuildingRemoved;
        public string LastAction { get; private set; }
        private void Update()
        {
            if(research!=null&&logistics.SetRange(RouteTechnologyEffects.LogisticsRange(research.HasFormationReinforcement,research.HasOrbitalSupply)))RefreshLogistics();
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
                if(active&&Keyboard.current.leftArrowKey.wasPressedThisFrame)selected=(selected-1+BuildingCatalog.BuildMenu.Length)%BuildingCatalog.BuildMenu.Length;
                if(active&&Keyboard.current.rightArrowKey.wasPressedThisFrame)selected=(selected+1)%BuildingCatalog.BuildMenu.Length;
                if (Keyboard.current.vKey.wasPressedThisFrame) TryUpgradeAtMouse();
            }
            if (active && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) PlaceAtMouse();
            if (Mouse.current != null && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) TryRepairAtMouse();
            if (city.Deployment.Mode != CityMode.Fortress) active = false;
        }
        private void PlaceAtMouse()
        {
            if(!CanBuild(BuildingCatalog.BuildMenu[selected],out _))return;
            Vector2 worldPosition = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
            int gridX = Mathf.FloorToInt(worldPosition.x - city.transform.position.x + 8f);
            int gridY = Mathf.FloorToInt(worldPosition.y - city.transform.position.y + 6f);
            int mapX = Mathf.FloorToInt(worldPosition.x + world.Model.Width * 0.5f); int mapY = Mathf.FloorToInt(worldPosition.y + world.Model.Height * 0.5f);
            bool resource = CoversRequiredResource(BuildingCatalog.BuildMenu[selected], mapX, mapY);
            if (!grid.TryPlace(BuildingCatalog.BuildMenu[selected], gridX, gridY, economy.Inventory, resource, out var placed)) return;
            CreateRuntime(placed);
        }
        private BuildingRuntime CreateRuntime(PlacedBuilding placed, int health = -1, float remaining = -1f, float repairRemaining = 0f, int shield = 0)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1f);
            var item = new GameObject($"Placeholder_{placed.Definition.Name}"); item.transform.SetParent(transform);
            item.transform.position = new Vector3(city.transform.position.x - 8f + placed.X + placed.Definition.Width * 0.5f, city.transform.position.y - 6f + placed.Y + placed.Definition.Height * 0.5f, -1f);
            item.transform.localScale = new Vector3(placed.Definition.Width * 0.9f, placed.Definition.Height * 0.9f, 1f);
            var renderer = item.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.sortingOrder = 8; renderer.color = ColorFor(placed.Definition.Id.Value);
            VisualSlot.Attach(item, placed.Definition.Id.Value, renderer, renderer.color);
            item.AddComponent<HealthComponent>(); var runtime = item.AddComponent<BuildingRuntime>(); runtime.Configure(placed.Definition, economy, population, population, localTime, city, research);
            placements[runtime] = placed;
            runtime.Completed += OnCompleted; runtime.Removed += OnRemoved;
            if (health >= 0) runtime.RestoreState(health, remaining, repairRemaining, shield);
            RefreshLogistics();
            return runtime;
        }
        private static Color ColorFor(string id) => id.StartsWith("cultivation.")?new Color(.3f,.85f,1f):id.StartsWith("biological.")?new Color(.35f,.9f,.25f):id.StartsWith("psionics.")?new Color(.75f,.25f,1f):id.Contains("mining") ? Color.yellow : id.Contains("housing") ? Color.green : id.Contains("warehouse") ? Color.cyan : id.Contains("wall") ? Color.gray : id.Contains("research") ? Color.magenta : id.Contains("smelter") ? new Color(.8f,.3f,.1f) : id.Contains("assembler") ? Color.blue : Color.white;
        private void OnCompleted(BuildingRuntime runtime)
        {
            RefreshLogistics();
            if (DefenseTowerCatalog.For(runtime.Definition.Id.Value)!=null) runtime.gameObject.AddComponent<PlaceholderTurret>().Configure(economy, runtime, localTime,TurretModifier,research);
            if(runtime.Definition.Id.Value==BuildingCatalog.ShieldGenerator.Id.Value)runtime.gameObject.AddComponent<PlaceholderShieldGenerator>().Configure(runtime);
            if(runtime.Definition.Id.Value==BuildingCatalog.AutomatedRepairBay.Id.Value)runtime.gameObject.AddComponent<PlaceholderAutomatedRepairBay>().Configure(runtime);
            BuildingPlaced?.Invoke(runtime.Definition);
        }
        private void OnRemoved(BuildingRuntime runtime) { if (placements.TryGetValue(runtime, out var placed)) { grid.Remove(placed); placements.Remove(runtime); } if (runtime.Construction != null && runtime.Construction.IsComplete) BuildingRemoved?.Invoke(runtime.Definition); RefreshLogistics(); }
        public BuildingSnapshot[] CaptureSnapshots() => placements.Select(pair => new BuildingSnapshot { definitionId = pair.Key.Definition.Id.Value, x = pair.Value.X, y = pair.Value.Y, health = pair.Key.Health.Value.Current, shield = pair.Key.Health.Value.Shield, constructionRemaining = pair.Key.Construction.Remaining, repairRemaining = pair.Key.Repair?.Remaining ?? 0f }).ToArray();
        public void SetLocalTimeSource(LocalHasteController value) { localTime = value; foreach (var runtime in placements.Keys) runtime.SetLocalTimeSource(value); }
        private ITurretCombatModifierSource TurretModifier=>technology!=null?technology:leader;
        public void SetTurretFireRateSource(FormalLeaderController value){leader=value;foreach(var turret in UnityEngine.Object.FindObjectsOfType<PlaceholderTurret>())turret.SetCombatModifierSource(TurretModifier);}
        public void SetTurretCombatModifierSource(FormalTechnologyRouteController value){technology=value;foreach(var turret in UnityEngine.Object.FindObjectsOfType<PlaceholderTurret>())turret.SetCombatModifierSource(TurretModifier);}
        public BuildingRuntime FindNearest(Vector2 point, float radius) { BuildingRuntime nearest=null;float best=radius*radius;foreach(var runtime in placements.Keys){float sqr=((Vector2)runtime.transform.position-point).sqrMagnitude;if(sqr<=best){best=sqr;nearest=runtime;}}return nearest; }
        public BuildingRuntime FindAtGrid(int x, int y) { foreach(var pair in placements)if(pair.Value.X==x&&pair.Value.Y==y)return pair.Key;return null; }
        public bool TryGetGrid(BuildingRuntime runtime, out int x, out int y) { if(runtime!=null&&placements.TryGetValue(runtime,out var placed)){x=placed.X;y=placed.Y;return true;}x=y=-1;return false; }
        public bool TryGetWorldCell(BuildingRuntime runtime, out int x, out int y)
        {
            x = y = -1; if (runtime == null || world.Model == null || !placements.ContainsKey(runtime)) return false;
            x = Mathf.FloorToInt(runtime.transform.position.x + world.Model.Width * .5f);
            y = Mathf.FloorToInt(runtime.transform.position.y + world.Model.Height * .5f);
            return x >= 0 && y >= 0 && x < world.Model.Width && y < world.Model.Height;
        }
        public int CompletedCount(string id)=>placements.Keys.Count(runtime=>runtime.Construction.IsComplete&&(runtime.Definition.Id.Value==id||(id=="core.building.machine-gun-turret"&&runtime.Definition.Id.Value=="core.building.heavy-machine-gun-turret")));
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
        private bool TemplateResourceCondition(BuildingDefinition definition,int x,int y){if(!definition.RequiresResourceNode)return true;Vector2 point=new Vector2(city.transform.position.x-8f+x+definition.Width*.5f,city.transform.position.y-6f+y+definition.Height*.5f);int mapX=Mathf.FloorToInt(point.x+world.Model.Width*.5f),mapY=Mathf.FloorToInt(point.y+world.Model.Height*.5f);return CoversRequiredResource(definition,mapX,mapY);}
        private bool CoversRequiredResource(BuildingDefinition definition,int mapX,int mapY)
        {
            if(!definition.RequiresResourceNode)return true;
            if(mapX<0||mapY<0||mapX>=world.Model.Width||mapY>=world.Model.Height)return false;
            WorldCell cell=world.Model.Get(mapX,mapY);
            return definition.Id.Value=="core.building.mining-station"?cell.ResourceId==ResourceIds.Iron:cell.HasResource;
        }
        private static bool Overlaps((BuildingDefinition definition,int x,int y) a,(BuildingDefinition definition,int x,int y) b)=>a.x<b.x+b.definition.Width&&a.x+a.definition.Width>b.x&&a.y<b.y+b.definition.Height&&a.y+a.definition.Height>b.y;
        public void RestoreSnapshots(BuildingSnapshot[] snapshots)
        {
            foreach (var runtime in placements.Keys.ToArray()) { runtime.PrepareForRestore(); Destroy(runtime.gameObject); }
            placements.Clear(); grid = new BuildingGrid(GridWidth, GridHeight);
            if (snapshots == null) return;
            foreach (var snapshot in snapshots)
            {
                var definition = BuildingCatalog.All.FirstOrDefault(value => value.Id.Value == snapshot.definitionId);
                if (definition != null && grid.TryRestore(definition, snapshot.x, snapshot.y, out var placed)) CreateRuntime(placed, snapshot.health, snapshot.constructionRemaining, snapshot.repairRemaining, snapshot.shield);
            }
        }
        private void TryRepairAtMouse()
        {
            Vector2 point = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()); BuildingRuntime nearest = null; float best = 2.25f;
            foreach (var runtime in placements.Keys) { float sqr = ((Vector2)runtime.transform.position - point).sqrMagnitude; if (sqr < best) { best = sqr; nearest = runtime; } }
            if (nearest == null) { LastAction = "维修：请将鼠标指向受损建筑"; return; }
            LastAction = nearest.TryStartRepair() ? $"维修开始：{nearest.Definition.Name} · 生物质 -1" : "维修无法开始：建筑未受损、施工中、已在维修或生物质不足";
        }
        private void TryUpgradeAtMouse()
        {
            if(Mouse.current==null||city.Deployment.Mode!=CityMode.Fortress)return;Vector2 point=Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());BuildingRuntime runtime=FindNearest(point,1.5f);if(runtime==null||!runtime.Construction.IsComplete){LastAction="升级：请指向已完成建筑";return;}bool alloyArmor=research.Model.IsCompleted(new WasteCity.Content.StableId("core.research.alloy-armor"));var recipe=BuildingUpgradeCatalog.For(runtime.Definition,progression.Civilization.Level,alloyArmor);if(recipe==null){LastAction="升级锁定：需要二级文明与合金装甲科技，或该建筑没有后续型号";return;}if(!placements.TryGetValue(runtime,out var placed)||!grid.TryUpgrade(placed,recipe.Target,economy.Inventory,recipe.CostId,recipe.Cost,out var upgraded)){LastAction=$"升级失败：需要 {recipe.Cost} 合金";return;}runtime.PrepareForUpgrade();placements.Remove(runtime);Destroy(runtime.gameObject);CreateRuntime(upgraded);LastAction=$"升级施工：{recipe.Target.Name} · {recipe.CostId} -{recipe.Cost}";
        }
        private void RefreshLogistics()
        {
            var points=placements.Where(pair=>pair.Key.Construction!=null&&pair.Key.Construction.IsComplete).Select(pair=>new LogisticsPoint(pair.Key.GetInstanceID().ToString(),pair.Value.X+pair.Value.Definition.Width/2,pair.Value.Y+pair.Value.Definition.Height/2)).ToArray();logistics.Rebuild(points);
            foreach(var pair in placements)pair.Key.SetLogistics(pair.Key.Construction!=null&&pair.Key.Construction.IsComplete&&logistics.IsConnected(pair.Key.GetInstanceID().ToString()));
        }
        private void OnGUI()
        {
            if (active){var definition=BuildingCatalog.BuildMenu[selected];CanBuild(definition,out string lockReason);GUI.Box(new Rect(18, Screen.height - 72f, 930f, 52f), $"建造：数字1-8基础建筑 · ←/→ 全建筑 · 当前 {selected+1}/{BuildingCatalog.BuildMenu.Length} {definition.Name}{(lockReason==null?" · 已解锁":$" · 锁定：{lockReason}")} · 左键放置 · [V] 升级");}
            if (!string.IsNullOrEmpty(LastAction)) GUI.Box(new Rect(18, Screen.height - 185f, 620f, 45f), LastAction);
            if(DisconnectedCount>0)GUI.Box(new Rect(18,Screen.height-292f,520f,42f),$"物流警告：{DisconnectedCount} 座建筑断网，效果和库存访问暂停");
        }
    }
    [Serializable]
    public sealed class BuildingSnapshot { public string definitionId; public int x, y, health, shield; public float constructionRemaining, repairRemaining; }
}
