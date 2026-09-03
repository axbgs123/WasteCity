using System;
using Unity.Profiling;
using UnityEngine;
using WasteCity.Research;

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
        private Func<float> civilizationEfficiencySource;
        private Func<float> localHasteMultiplierSource;
        private ResearchModel researchEffectSource;
        private int researchEffectCompletedCount = -1;
        private ResearchEffectSnapshot researchEffects =
            ResearchEffectResolver.Resolve(Array.Empty<string>());

        public GrayboxProductionClock3D Clock { get; } =
            new GrayboxProductionClock3D();
        public GrayboxProductionCommandFacade3D Commands => Clock.Commands;
        public ProductionObservabilitySnapshot Snapshot => Clock.Snapshot;
        public ulong Revision => Clock.Revision;
        public bool IsPersistencePaused =>
            persistencePauseSource != null && persistencePauseSource();
        public bool IsConfigured =>
            session != null && city != null && worldView != null;
        public ResearchEffectSnapshot ResearchEffects => researchEffects;

        public void ConfigurePersistencePauseSource(Func<bool> pauseSource)
        {
            persistencePauseSource = pauseSource;
        }

        public void ConfigureCivilizationEfficiencySource(
            Func<float> source)
        {
            civilizationEfficiencySource = source;
        }

        public void ConfigureLocalHasteMultiplier(Func<float> source)
        {
            localHasteMultiplierSource = source;
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
            RefreshResearchModifier(force: true);
        }

        public bool TryRebuildAfterPersistenceRestore(out string error)
        {
            RefreshResearchModifier(force: true);
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
                EffectiveLogisticsRadius(),
                worldView.Model,
                session.CityStorage);
            Clock.Runtime.Synchronize(
                session.Instances,
                city.Mode,
                cityX,
                cityY,
                EffectiveLogisticsRadius(),
                session.CityStorage);
            Clock.PublishObservabilityIfChanged();
            error = string.Empty;
            return true;
        }

        public bool Tick(float ruleDeltaSeconds, bool paused)
        {
            using (TickMarker.Auto())
            {
                RefreshResearchModifier(force: false);
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
                    ruleDeltaSeconds * Mathf.Max(
                        0f,
                        civilizationEfficiencySource?.Invoke() ?? 1f) *
                    Mathf.Max(0f, localHasteMultiplierSource?.Invoke() ?? 1f),
                    paused || IsPersistencePaused,
                    session.Instances,
                    city.Mode,
                    cityX,
                    cityY,
                    EffectiveLogisticsRadius(),
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

        private void RefreshResearchModifier(bool force)
        {
            ResearchModel current = session == null
                ? null
                : session.Research;
            int completedCount = current?.CompletedCount ?? 0;
            if (!force && ReferenceEquals(current, researchEffectSource) &&
                completedCount == researchEffectCompletedCount)
            {
                return;
            }

            researchEffectSource = current;
            researchEffectCompletedCount = completedCount;
            researchEffects = ResearchEffectResolver.Resolve(current == null
                ? Array.Empty<string>()
                : current.CaptureCompleted());
            Clock.ConfigureResearchModifier(
                new FormalProductionResearchModifierAdapter(researchEffects));
        }

        private int EffectiveLogisticsRadius()
        {
            return researchEffects.ResolveLogisticsRange(
                session?.GroundBuildRadius ?? 0);
        }
    }
}
