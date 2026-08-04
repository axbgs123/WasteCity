using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Population;
using WasteCity.Progression;
using WasteCity.Presentation;
using System;
using WasteCity.Research;

namespace WasteCity.World
{
    public sealed class RescueSiteController : MonoBehaviour
    {
        [SerializeField] private PlaceholderWorldView world;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private FormalPopulationController population;
        [SerializeField] private FormalProgressionController progression;
        [SerializeField] private ResearchController research;
        public RescueSiteModel Model { get; private set; }
        public string LastResult { get; private set; }
        public event Action<int,bool> Rescued;
        private SpriteRenderer[] markers;
        private static Sprite square;
        private void Start() => TryInitialize();
        private void TryInitialize()
        {
            if (Model != null || world.Model == null) return;
            Model = new RescueSiteModel(world.Model.Width, world.Model.Height, new WorldSeed(8128)); markers = new SpriteRenderer[Model.Sites.Count];
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            for (int i = 0; i < markers.Length; i++) { var site = Model.Sites[i]; var item = new GameObject($"RescueRuinPlaceholder_{i}"); item.transform.SetParent(transform); item.transform.position = WorldPosition(site); item.transform.localScale = Vector3.one * .65f; var r = item.AddComponent<SpriteRenderer>(); r.sprite = square; r.color = Color.white; r.sortingOrder = 4; VisualSlot.Attach(item,"core.world.rescue-ruin",r,r.color); markers[i] = r; }
        }
        private void Update()
        {
            if (Model == null) { TryInitialize(); return; } int nearby = -1;
            for (int i = 0; i < Model.Sites.Count; i++) { var site = Model.Sites[i]; markers[i].enabled = world.Model.IsRevealed(site.X, site.Y) && !site.Completed; if (!site.Completed && Vector2.Distance(city.transform.position, WorldPosition(site)) <= 1.5f) nearby = i; }
            if (nearby >= 0 && city.Deployment.Mode == CityMode.Fortress && Keyboard.current != null)
            {if(Keyboard.current.nKey.wasPressedThisFrame)TryRescue(nearby,true);else if(Keyboard.current.mKey.wasPressedThisFrame)TryRescue(nearby,false);}
            if(research!=null&&research.HasConsciousnessNetwork&&Keyboard.current!=null&&Keyboard.current.jKey.wasPressedThisFrame){int remote=Model.FindFirstIncomplete(site=>world.Model.IsRevealed(site.X,site.Y));if(remote>=0)TryRescue(remote,false,true);else LastResult="远程通讯：没有已发现的待救援信号";}
        }
        public bool TryRescue(int index,bool immediate=true,bool remote=false)
        {
            int cost=RescueRules.BiomassCost(immediate,remote);if (index < 0 || index >= Model.Sites.Count || Model.Sites[index].Completed || !economy.Inventory.TrySpend(ResourceIds.Biomass, cost)) return false;
            Model.Sites[index].Complete(); population.AddPeople(20); progression.Observation.Add("救援废墟幸存者", 2f); LastResult = $"{(immediate?"立即":"延迟")}救援成功：人口 +20 · 生物质 -{cost} · 异常观测值 +2";Rescued?.Invoke(index,immediate);return true;
        }
        public bool[] Capture() => Model?.Capture();
        public void Restore(bool[] values) => Model?.Restore(values);
        private Vector3 WorldPosition(RescueSite site) => new Vector3(site.X - world.Model.Width * .5f, site.Y - world.Model.Height * .5f, -1f);
        private void OnGUI() { if (Model == null) return; foreach (var site in Model.Sites) if (!site.Completed && Vector2.Distance(city.transform.position, WorldPosition(site)) <= 1.5f) GUI.Box(new Rect(18, Screen.height - 132f, 620f, 48f), "救援遗迹：[N] 立即救援（生物质5，健康） / [M] 延迟救援（生物质2，受伤）"); if(research!=null&&research.HasConsciousnessNetwork&&Model.FindFirstIncomplete(site=>world.Model.IsRevealed(site.X,site.Y))>=0)GUI.Box(new Rect(18,Screen.height-185f,620f,44f),"意识网络：[J] 免费远程处理已发现求救（按延迟救援结算）");if (!string.IsNullOrEmpty(LastResult)) GUI.Box(new Rect(18, 245f, 500f, 45f), LastResult); }
    }
}
