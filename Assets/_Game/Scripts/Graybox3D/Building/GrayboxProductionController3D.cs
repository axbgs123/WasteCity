using System;
using UnityEngine;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProductionController3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxWorldView3D worldView;

        public GrayboxProductionClock3D Clock { get; } =
            new GrayboxProductionClock3D();
        public GrayboxProductionCommandFacade3D Commands => Clock.Commands;
        public ProductionObservabilitySnapshot Snapshot => Clock.Snapshot;
        public ulong Revision => Clock.Revision;
        public bool IsConfigured =>
            session != null && city != null && worldView != null;

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxMobileCityController3D city,
            GrayboxWorldView3D worldView)
        {
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.city = city ??
                throw new ArgumentNullException(nameof(city));
            this.worldView = worldView ??
                throw new ArgumentNullException(nameof(worldView));
        }

        public bool Tick(float deltaSeconds, bool paused)
        {
            if (!IsConfigured ||
                session.Inventory == null ||
                session.Instances == null ||
                worldView.Model == null ||
                worldView.Coordinates == null ||
                !worldView.Coordinates.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY))
            {
                return false;
            }

            Clock.Tick(
                deltaSeconds,
                paused,
                session.Instances,
                city.Mode,
                cityX,
                cityY,
                session.GroundBuildRadius,
                worldView.Model,
                session.Inventory);
            return true;
        }

        private void Update()
        {
            Tick(
                Time.deltaTime,
                Time.timeScale <= 0f);
        }
    }
}
