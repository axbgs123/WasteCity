using System;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.Presentation;
using WasteCity.Research;
using UnityEngine.InputSystem;

namespace WasteCity.Combat
{
    [Serializable]
    public sealed class FriendlyUnitSnapshot
    {
        public float x, y;
        public int health;
    }

    public sealed class FormalFriendlyUnitController : MonoBehaviour
    {
        [SerializeField] private FormalEconomyController economy;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private PlaceholderBuildingController buildings;
        [SerializeField] private ResearchController research;
        private readonly PuppetFabricationModel fabrication = new PuppetFabricationModel();
        private readonly BehemothBreedingModel breeding = new BehemothBreedingModel();
        private static Sprite square;
        private int spawnSequence;
        private GameObject rallyMarker;
        public PuppetFabricationModel Fabrication => fabrication;
        public BehemothBreedingModel Breeding => breeding;
        public FriendlyUnitCommandModel Commands { get; } = new FriendlyUnitCommandModel();
        public int PuppetCount => FindObjectsOfType<PlaceholderPuppet>().Length;
        public int BehemothCount => FindObjectsOfType<PlaceholderBehemoth>().Length;
        public GameObject RallyMarker => rallyMarker;

        private void Update()
        {
            HandleRallyInput();
            if (city == null || buildings == null || economy == null || !city.LongWorkAllowed) return;
            int workshops = buildings.CompletedCount(BuildingCatalog.PuppetWorkshop.Id.Value);
            int produced = fabrication.Tick(Time.deltaTime, workshops, PuppetCount, economy.Inventory);
            for (int i = 0; i < produced; i++) SpawnPuppet();
            int pens = buildings.CompletedCount(BuildingCatalog.BehemothPen.Id.Value);
            int bred = breeding.Tick(Time.deltaTime, pens, BehemothCount, economy.Inventory);
            for (int i = 0; i < bred; i++) SpawnBehemoth();
        }

        private void HandleRallyInput()
        {
            if (Keyboard.current == null || !Keyboard.current.yKey.wasPressedThisFrame) return;
            if (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)
            {
                FollowCity();
                return;
            }
            if (Mouse.current == null || Camera.main == null) return;
            SetRallyPoint(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()));
        }

        public void SetRallyPoint(Vector2 point)
        {
            Commands.SetRally(point.x, point.y);
            EnsureRallyMarker();
            rallyMarker.transform.position = point;
            rallyMarker.SetActive(true);
        }

        public void FollowCity()
        {
            Commands.ClearRally();
            if (rallyMarker != null) rallyMarker.SetActive(false);
        }

        public void RestoreCommandState(bool hasFixedRally, float x, float y, int puppetLosses, int behemothLosses, int controlledLosses)
        {
            Commands.Restore(hasFixedRally, x, y, puppetLosses, behemothLosses, controlledLosses);
            if (hasFixedRally)
            {
                EnsureRallyMarker();
                rallyMarker.transform.position = new Vector2(x, y);
                rallyMarker.SetActive(true);
            }
            else if (rallyMarker != null)
            {
                rallyMarker.SetActive(false);
            }
        }

        private void EnsureRallyMarker()
        {
            if (rallyMarker != null) return;
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            rallyMarker = new GameObject("Placeholder_RallyPoint");
            rallyMarker.transform.localScale = Vector3.one * .55f;
            rallyMarker.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var renderer = rallyMarker.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(1f, .82f, .18f, .9f);
            renderer.sortingOrder = 12;
            VisualSlot.Attach(rallyMarker, "core.command.rally-point", renderer, renderer.color);
        }

        private PlaceholderPuppet SpawnPuppet(Vector2? restoredPosition = null, int restoredHealth = -1)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            float angle = spawnSequence++ * 137.5f * Mathf.Deg2Rad;
            Vector2 position = restoredPosition ?? ((Vector2)city.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 2.5f);
            var item = new GameObject("Placeholder_Puppet");
            item.transform.position = position;
            item.transform.localScale = new Vector3(.7f, .7f, 1f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = square; renderer.color = new Color(.3f, .85f, 1f); renderer.sortingOrder = 11;
            VisualSlot.Attach(item, "cultivation.unit.puppet", renderer, renderer.color);
            item.AddComponent<HealthComponent>();
            var puppet = item.AddComponent<PlaceholderPuppet>();
            puppet.Configure(city.transform, restoredHealth, research, Commands, economy?.Inventory);
            return puppet;
        }

        private PlaceholderBehemoth SpawnBehemoth(Vector2? restoredPosition = null, int restoredHealth = -1)
        {
            if (square == null) square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            float angle = spawnSequence++ * 137.5f * Mathf.Deg2Rad;
            Vector2 position = restoredPosition ?? ((Vector2)city.transform.position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3.2f);
            var item = new GameObject("Placeholder_Behemoth"); item.transform.position = position; item.transform.localScale = new Vector3(1.45f, 1.1f, 1f);
            var renderer = item.AddComponent<SpriteRenderer>(); renderer.sprite = square; renderer.color = new Color(.62f, .2f, .3f); renderer.sortingOrder = 11;
            VisualSlot.Attach(item, "biological.unit.behemoth", renderer, renderer.color);
            item.AddComponent<HealthComponent>(); var behemoth = item.AddComponent<PlaceholderBehemoth>(); behemoth.Configure(city.transform, research, restoredHealth, Commands); return behemoth;
        }

        public FriendlyUnitSnapshot[] Capture()
        {
            var values = FindObjectsOfType<PlaceholderPuppet>();
            var result = new FriendlyUnitSnapshot[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = new FriendlyUnitSnapshot { x = values[i].transform.position.x, y = values[i].transform.position.y, health = values[i].Health.Value.Current };
            return result;
        }

        public void Restore(float progress, FriendlyUnitSnapshot[] snapshots)
        {
            fabrication.Restore(progress);
            foreach (var existing in FindObjectsOfType<PlaceholderPuppet>()) Destroy(existing.gameObject);
            spawnSequence = 0;
            if (snapshots == null) return;
            foreach (var value in snapshots) SpawnPuppet(new Vector2(value.x, value.y), value.health);
        }

        public FriendlyUnitSnapshot[] CaptureBehemoths()
        {
            var values = FindObjectsOfType<PlaceholderBehemoth>(); var result = new FriendlyUnitSnapshot[values.Length];
            for (int i = 0; i < values.Length; i++) result[i] = new FriendlyUnitSnapshot { x = values[i].transform.position.x, y = values[i].transform.position.y, health = values[i].Health.Value.Current };
            return result;
        }

        public void RestoreBehemoths(float progress, FriendlyUnitSnapshot[] snapshots)
        {
            breeding.Restore(progress); foreach (var existing in FindObjectsOfType<PlaceholderBehemoth>()) Destroy(existing.gameObject);
            if (snapshots == null) return; foreach (var value in snapshots) SpawnBehemoth(new Vector2(value.x, value.y), value.health);
        }

        private void OnGUI()
        {
            int workshops = buildings == null ? 0 : buildings.CompletedCount(BuildingCatalog.PuppetWorkshop.Id.Value);
            int pens = buildings == null ? 0 : buildings.CompletedCount(BuildingCatalog.BehemothPen.Id.Value);
            if (workshops > 0) GUI.Box(new Rect(Screen.width - 230f, 174f, 215f, 52f), $"傀儡 {PuppetCount}/{fabrication.Capacity(workshops)}\n制造进度 {fabrication.Progress:0.0}/{PuppetFabricationModel.SecondsPerUnit:0}s");
            if (pens > 0) GUI.Box(new Rect(Screen.width - 230f, 232f, 215f, 52f), $"巨兽 {BehemothCount}/{breeding.Capacity(pens)}\n培育进度 {breeding.Progress:0.0}/{BehemothBreedingModel.SecondsPerUnit:0}s");
            string rally = Commands.HasFixedRally ? "固定集结点" : "跟随移动城市";
            GUI.Box(new Rect(Screen.width - 230f, 290f, 215f, 68f), $"友军命令：{rally}\n[Y] 鼠标处集结 · [Shift+Y] 随城\n损失 {Commands.TotalLosses}");
        }
    }
}
