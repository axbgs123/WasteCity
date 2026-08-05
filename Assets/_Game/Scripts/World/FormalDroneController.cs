using System.Collections.Generic;
using UnityEngine;
using WasteCity.Building;
using WasteCity.City;
using WasteCity.Presentation;
using WasteCity.Research;

namespace WasteCity.World
{
    public sealed class FormalDroneController : MonoBehaviour
    {
        private sealed class ActiveDrone
        {
            public GameObject Visual { get; }
            public ScoutDronePatrolModel Patrol { get; } = new ScoutDronePatrolModel();

            public ActiveDrone(GameObject visual) => Visual = visual;
        }

        [SerializeField] private ResearchController research;
        [SerializeField] private Transform city;
        [SerializeField] private PlaceholderWorldView world;
        [SerializeField] private PlaceholderBuildingController buildings;

        private static Sprite square;
        private readonly List<ActiveDrone> drones = new List<ActiveDrone>();
        private readonly List<DroneBayState> bayStates = new List<DroneBayState>();

        public int ActiveDroneCount => drones.Count;
        public bool HasRequiredReferences => research != null && city != null && world != null && buildings != null;

        public void Configure(
            ResearchController researchController,
            Transform cityTransform,
            PlaceholderWorldView worldView,
            PlaceholderBuildingController buildingController)
        {
            research = researchController;
            city = cityTransform;
            world = worldView;
            buildings = buildingController;
        }

        public GameObject DroneAt(int index) => index >= 0 && index < drones.Count ? drones[index].Visual : null;

        private void Update()
        {
            RefreshDeployment();
            TickDrones(Time.deltaTime);
        }

        public void RefreshDeployment()
        {
            bayStates.Clear();
            foreach (BuildingRuntime runtime in Object.FindObjectsOfType<BuildingRuntime>())
                if (runtime.Definition != null && runtime.Definition.Id.Value == BuildingCatalog.AutomatedRepairBay.Id.Value)
                    bayStates.Add(new DroneBayState(runtime.Construction.IsComplete, runtime.IsOperational));

            int targetCount = ScoutDroneDeploymentRules.ActiveCount(
                research != null && research.HasUnmannedSystems,
                bayStates);

            while (drones.Count < targetCount) drones.Add(new ActiveDrone(CreateDrone(drones.Count)));
            while (drones.Count > targetCount)
            {
                int last = drones.Count - 1;
                if (drones[last].Visual != null) Destroy(drones[last].Visual);
                drones.RemoveAt(last);
            }
        }

        private void TickDrones(float deltaSeconds)
        {
            if (city == null) return;
            for (int index = 0; index < drones.Count; index++)
            {
                ActiveDrone drone = drones[index];
                bool reveal = drone.Patrol.Tick(deltaSeconds);
                ScoutDronePosition position = drone.Patrol.Position(
                    city.position.x,
                    city.position.y,
                    index,
                    drones.Count);
                drone.Visual.transform.position = new Vector3(position.X, position.Y, -1.2f);
                if (reveal && world != null) world.RevealAroundWorld(drone.Visual.transform.position, 2);
            }
        }

        private GameObject CreateDrone(int index)
        {
            if (square == null)
                square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1f);
            var item = new GameObject($"TechnologyScoutDrone_{index}");
            item.transform.SetParent(transform, true);
            item.transform.localScale = new Vector3(.5f, .32f, 1f);
            var renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(.2f, .9f, 1f);
            renderer.sortingOrder = 12;
            VisualSlot.Attach(item, "technology.unit.scout-drone", renderer, renderer.color);
            return item;
        }

        private void OnDestroy()
        {
            foreach (ActiveDrone drone in drones)
                if (drone.Visual != null)
                    Destroy(drone.Visual);
            drones.Clear();
        }
    }
}
