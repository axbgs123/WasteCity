using System;
using Unity.Profiling;
using UnityEngine;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxFormalMixedProfilerHeartbeat3D : MonoBehaviour
    {
        private static readonly ProfilerMarker FrameMarker =
            new ProfilerMarker("WasteCity.Formal.MixedWorkload.Frame");

        private Action pulseAction;
        private bool pulseRequested;
        private bool pulseExecuting;

        public int PulseCompletionCount { get; private set; }

        public void Configure(Action pulseAction)
        {
            this.pulseAction = pulseAction ??
                throw new ArgumentNullException(nameof(pulseAction));
            ResetPulseTracking();
        }

        public void ResetPulseTracking()
        {
            if (pulseExecuting)
                throw new InvalidOperationException(
                    "Cannot reset the formal Profiler heartbeat during a pulse.");
            pulseRequested = false;
            PulseCompletionCount = 0;
        }

        public void RequestPulse()
        {
            if (pulseAction == null)
                throw new InvalidOperationException(
                    "Formal Profiler heartbeat is not configured.");
            if (PulseCompletionCount == 0)
                pulseRequested = true;
        }

        private void Update()
        {
            using (FrameMarker.Auto())
            {
                if (!pulseRequested || pulseExecuting ||
                    PulseCompletionCount != 0)
                    return;
                pulseRequested = false;
                pulseExecuting = true;
                try
                {
                    pulseAction();
                    PulseCompletionCount++;
                }
                finally
                {
                    pulseExecuting = false;
                }
            }
        }

        private void OnDisable()
        {
            ClearPulse();
        }

        private void OnDestroy()
        {
            ClearPulse();
        }

        private void ClearPulse()
        {
            pulseAction = null;
            pulseRequested = false;
        }
    }

    public sealed class GrayboxProductionController3D : MonoBehaviour
    {
        private static readonly ProfilerMarker TickMarker =
            new ProfilerMarker("WasteCity.Formal.Production.Tick");

        [SerializeField] private GrayboxBuildingSession3D session;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxWorldView3D worldView;

        private Func<bool> persistencePauseSource;

        public GrayboxProductionClock3D Clock { get; } =
            new GrayboxProductionClock3D();
        public GrayboxProductionCommandFacade3D Commands => Clock.Commands;
        public ProductionObservabilitySnapshot Snapshot => Clock.Snapshot;
        public ulong Revision => Clock.Revision;
        public bool IsPersistencePaused =>
            persistencePauseSource != null && persistencePauseSource();
        public bool IsConfigured =>
            session != null && city != null && worldView != null;

        public void ConfigurePersistencePauseSource(Func<bool> pauseSource)
        {
            persistencePauseSource = pauseSource;
        }

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

        public bool TryRebuildAfterPersistenceRestore(out string error)
        {
            if (!IsConfigured ||
                session.Inventory == null ||
                session.CityStorage == null ||
                session.Instances == null ||
                worldView.Model == null ||
                worldView.Coordinates == null ||
                !worldView.Coordinates.TryWorldToCell(
                    city.transform.position,
                    out int cityX,
                    out int cityY))
            {
                error = "正式生产运行时尚未完成恢复后重建配置";
                return false;
            }

            Clock.Tick(
                0f,
                paused: false,
                session.Instances,
                city.Mode,
                cityX,
                cityY,
                session.GroundBuildRadius,
                worldView.Model,
                session.CityStorage);
            Clock.Runtime.Synchronize(
                session.Instances,
                city.Mode,
                cityX,
                cityY,
                session.GroundBuildRadius,
                session.CityStorage);
            Clock.PublishObservabilityIfChanged();
            error = string.Empty;
            return true;
        }

        public bool Tick(float ruleDeltaSeconds, bool paused)
        {
            using (TickMarker.Auto())
            {
                if (!IsConfigured ||
                    session.Inventory == null ||
                    session.CityStorage == null ||
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
                    ruleDeltaSeconds,
                    paused || IsPersistencePaused,
                    session.Instances,
                    city.Mode,
                    cityX,
                    cityY,
                    session.GroundBuildRadius,
                    worldView.Model,
                    session.CityStorage);
                return true;
            }
        }

        private void Update()
        {
            float ruleDeltaSeconds = session == null
                ? 0f
                : session.ResolveRuleDelta(Time.unscaledDeltaTime);
            Tick(
                ruleDeltaSeconds,
                paused: ruleDeltaSeconds <= 0f);
        }
    }
}
