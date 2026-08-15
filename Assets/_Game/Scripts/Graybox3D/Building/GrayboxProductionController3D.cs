using System;
using UnityEngine;
using WasteCity.Core;
using WasteCity.Graybox3D.Production;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxProductionController3D : MonoBehaviour
    {
        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxWorldView3D worldView;
        [SerializeField]
        private GrayboxMobileCityController3D cityController;
        [SerializeField] private MonoBehaviour ruleTimeSource;

        private readonly GrayboxProductionSimulation3D simulation =
            new GrayboxProductionSimulation3D();
        private GameSpeedModel speed;

        public void Configure(
            GrayboxBuildingSession3D session,
            GrayboxWorldView3D worldView,
            GrayboxMobileCityController3D cityController,
            GameSpeedModel speed)
        {
            this.session = session ??
                throw new ArgumentNullException(nameof(session));
            this.worldView = worldView ??
                throw new ArgumentNullException(nameof(worldView));
            this.cityController = cityController ??
                throw new ArgumentNullException(nameof(cityController));
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
            ruleTimeSource = null;
        }

        public float TickProduction(float unscaledDeltaTime)
        {
            if (session == null ||
                worldView == null ||
                cityController == null ||
                worldView.Model == null ||
                worldView.Coordinates == null)
                return 0f;

            float ruleSpeed = ruleTimeSource is
                IGrayboxProductionRuleTimeSource3D source
                    ? source.ProductionRuleSpeed
                    : speed != null
                        ? speed.Speed
                        : 0f;
            float ruleDelta = Mathf.Max(0f, unscaledDeltaTime) *
                              Mathf.Max(0f, ruleSpeed);
            if (ruleDelta <= 0f) return 0f;
            if (!worldView.Coordinates.TryWorldToCell(
                    cityController.transform.position,
                    out int cityX,
                    out int cityY))
                return 0f;

            session.SynchronizeProductionRuntime(
                worldView.Model,
                cityController.Mode,
                cityX,
                cityY);
            simulation.Tick(
                ruleDelta,
                cityController.Mode,
                worldView.Model,
                session.Inventory,
                session.ProductionStates);
            return ruleDelta;
        }

        private void Update()
        {
            TickProduction(Time.unscaledDeltaTime);
        }
    }
}
