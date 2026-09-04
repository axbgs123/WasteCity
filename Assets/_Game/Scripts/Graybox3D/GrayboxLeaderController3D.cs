using System;
using UnityEngine;
using WasteCity.City;
using WasteCity.Leader;
using WasteCity.Leader.Exploration;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxLeaderController3D : MonoBehaviour
    {
        private static readonly Vector2 CityDockOffset =
            new Vector2(1.8f, 1.2f);

        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private GrayboxWorldView3D worldView;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField]
        private bool developmentFixtureRecruited;

        private Vector2 manualInput;
        private Func<LeaderIntent> aiIntentProvider;

        public LeaderModel Model { get; } = new LeaderModel();
        public bool DevelopmentFixtureRecruited =>
            developmentFixtureRecruited;

        public void Configure(
            GrayboxWorldView3D worldView,
            GrayboxMobileCityController3D city,
            bool developmentFixtureRecruited)
        {
            this.worldView = worldView;
            this.city = city;
            this.developmentFixtureRecruited =
                developmentFixtureRecruited;
            ApplyDevelopmentFixture();
        }

        private void Awake()
        {
            ApplyDevelopmentFixture();
        }

        private void ApplyDevelopmentFixture()
        {
            if (!developmentFixtureRecruited)
                return;

            Model.Restore(
                true,
                false,
                0f,
                0f,
                0f);
        }

        public void ApplyManualInput(Vector2 input)
        {
            manualInput = Vector2.ClampMagnitude(input, 1f);
        }

        public void ConfigureAiIntentProvider(Func<LeaderIntent> provider)
        {
            aiIntentProvider = provider;
        }

        public void TickControl(
            DirectControlTarget target,
            float deltaTime)
        {
            if (target != DirectControlTarget.Leader)
            {
                manualInput = Vector2.zero;
                TickAiControl(deltaTime);
                return;
            }

            if (manualInput.sqrMagnitude <= 0f ||
                worldView == null ||
                worldView.Model == null ||
                worldView.Coordinates == null)
                return;

            Vector2 plane =
                worldView.Coordinates.WorldToPlane(
                    transform.position);
            Vector2 candidate =
                plane +
                manualInput.normalized *
                moveSpeed *
                Mathf.Max(0f, deltaTime);
            TryCommitPlanePosition(candidate);
        }

        private void TickAiControl(float deltaTime)
        {
            if (aiIntentProvider == null || worldView?.Coordinates == null ||
                worldView.Model == null)
                return;
            LeaderIntent intent = aiIntentProvider();
            if (intent.Kind == LeaderIntentKind.None ||
                intent.Kind == LeaderIntentKind.HoldPosition)
                return;
            Vector2 current = worldView.Coordinates.WorldToPlane(
                transform.position);
            Vector2 target = new Vector2(intent.TargetX, intent.TargetY);
            Vector2 delta = target - current;
            if (delta.sqrMagnitude <=
                LeaderAiRules.DockArrivalDistance *
                LeaderAiRules.DockArrivalDistance)
                return;
            float step = moveSpeed * Mathf.Max(0f, deltaTime);
            if (step <= 0f) return;
            Vector2 candidate = current +
                delta.normalized * Mathf.Min(step, delta.magnitude);
            TryCommitPlanePosition(candidate);
        }

        private bool TryCommitPlanePosition(Vector2 candidate)
        {
            Vector3 candidateWorld = worldView.Coordinates.PlaneToWorld(
                candidate,
                transform.position.y);
            if (!worldView.TryWorldToCell(
                    candidateWorld,
                    out int cellX,
                    out int cellY) ||
                !CityTerrainRules.IsPassable(
                    worldView.Model.Get(cellX, cellY)))
                return false;
            transform.position = candidateWorld;
            return true;
        }

        public void SnapToCityDock()
        {
            if (city == null)
                return;

            Vector2 cityPlane =
                new Vector2(
                    city.transform.position.x,
                    city.transform.position.z);
            Vector2 dockPlane = cityPlane + CityDockOffset;
            transform.position =
                new Vector3(
                    dockPlane.x,
                    transform.position.y,
                    dockPlane.y);
        }

        public Vector2 ResolveCityDockPlane()
        {
            return city == null
                ? new Vector2(transform.position.x, transform.position.z)
                : new Vector2(
                    city.transform.position.x,
                    city.transform.position.z) + CityDockOffset;
        }
    }
}
