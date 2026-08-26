using System;
using System.Collections.Generic;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
    public readonly struct ProductionStatisticsDelta
    {
        public static readonly ProductionStatisticsDelta Empty =
            new ProductionStatisticsDelta(0, 0f, 0f);

        public ProductionStatisticsDelta(
            int completedProductionBatchCount,
            float productionActiveProgressSeconds,
            float productionEligibleSeconds)
        {
            CompletedProductionBatchCount = Math.Max(
                0,
                completedProductionBatchCount);
            ProductionActiveProgressSeconds = Math.Max(
                0f,
                productionActiveProgressSeconds);
            ProductionEligibleSeconds = Math.Max(
                ProductionActiveProgressSeconds,
                productionEligibleSeconds);
        }

        public int CompletedProductionBatchCount { get; }
        public float ProductionActiveProgressSeconds { get; }
        public float ProductionEligibleSeconds { get; }
        public bool IsEmpty => CompletedProductionBatchCount == 0 &&
            ProductionActiveProgressSeconds == 0f &&
            ProductionEligibleSeconds == 0f;
    }

    public sealed class GrayboxProductionClock3D
    {
        public const float StepSeconds = .1f;
        private const float StepEpsilon = .00001f;

        private readonly FormalProductionSimulation simulation =
            new FormalProductionSimulation();
        private readonly ResourceCapacityPolicy cityCapacity =
            new ResourceCapacityPolicy();
        private float accumulatorSeconds;
        private ProductionObservabilitySnapshot snapshot =
            ProductionObservabilitySnapshot.Empty;
        private WorldMapModel latestWorld;
        private CityResourceStorageModel latestCityStorage;
        private ulong publishedContentHash;
        private bool hasPublishedContentHash;
        private readonly List<ProductionStateMeasurement> stepMeasurements =
            new List<ProductionStateMeasurement>();
        private IFormalProductionOutputModifier outputModifier;

        public GrayboxProductionRuntime3D Runtime { get; } =
            new GrayboxProductionRuntime3D();
        public GrayboxProductionCommandFacade3D Commands { get; }
        public ProductionObservabilitySnapshot Snapshot => snapshot;
        public ulong Revision => snapshot.Revision;
        public uint ObservabilityCaptureCount { get; private set; }
        public float AccumulatorSeconds => accumulatorSeconds;
        public ulong StatisticsRevision { get; private set; }
        public ProductionStatisticsDelta LastStatisticsDelta { get; private set; } =
            ProductionStatisticsDelta.Empty;
        public Exception LastBatchNotificationFailure { get; private set; }
        internal WorldMapModel LatestWorld => latestWorld;
        internal CityResourceStorageModel LatestCityStorage =>
            latestCityStorage;

        public GrayboxProductionClock3D()
        {
            Commands = new GrayboxProductionCommandFacade3D(this);
        }

        public event Action<BuildingProductionState, ulong>
            ProductionBatchesCompleted;

        public void ConfigureOutputModifier(
            IFormalProductionOutputModifier modifier)
        {
            outputModifier = modifier;
        }

        public void Tick(
            float deltaSeconds,
            bool paused,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            WorldMapModel world,
            ResourceInventory cityInventory)
        {
            LastStatisticsDelta = ProductionStatisticsDelta.Empty;
            if (paused || cityInventory == null)
                return;

            latestWorld = world;
            latestCityStorage = null;
            accumulatorSeconds += Math.Max(0f, deltaSeconds);
            bool stepped = false;
            int completedProductionBatchCount = 0;
            float productionActiveProgressSeconds = 0f;
            float productionEligibleSeconds = 0f;
            while (accumulatorSeconds + StepEpsilon >= StepSeconds)
            {
                Runtime.Synchronize(
                    instances,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius);
                CaptureStepMeasurements(
                    ref productionEligibleSeconds);
                simulation.Tick(
                    Runtime.RunnableStates,
                    StepSeconds,
                    world,
                    cityInventory,
                    cityCapacity,
                    Runtime.ActiveWarehouseCount,
                    globallyPaused: false,
                    outputModifier: outputModifier);
                CompleteStepMeasurements(
                    ref completedProductionBatchCount,
                    ref productionActiveProgressSeconds);
                accumulatorSeconds -= StepSeconds;
                if (accumulatorSeconds < StepEpsilon)
                    accumulatorSeconds = 0f;
                stepped = true;
            }
            PublishStatisticsDelta(new ProductionStatisticsDelta(
                completedProductionBatchCount,
                productionActiveProgressSeconds,
                productionEligibleSeconds));
            if (stepped)
                PublishObservabilityIfChanged();
        }

        public void Tick(
            float deltaSeconds,
            bool paused,
            IReadOnlyList<GrayboxBuildingInstance3D> instances,
            CityMode cityMode,
            int cityX,
            int cityY,
            int groundRadius,
            WorldMapModel world,
            CityResourceStorageModel cityStorage)
        {
            LastStatisticsDelta = ProductionStatisticsDelta.Empty;
            if (paused || cityStorage == null) return;

            latestWorld = world;
            latestCityStorage = cityStorage;
            accumulatorSeconds += Math.Max(0f, deltaSeconds);
            bool stepped = false;
            int completedProductionBatchCount = 0;
            float productionActiveProgressSeconds = 0f;
            float productionEligibleSeconds = 0f;
            while (accumulatorSeconds + StepEpsilon >= StepSeconds)
            {
                Runtime.Synchronize(
                    instances,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius,
                    cityStorage);
                CaptureStepMeasurements(
                    ref productionEligibleSeconds);
                simulation.Tick(
                    Runtime.RunnableStates,
                    StepSeconds,
                    world,
                    cityStorage,
                    globallyPaused: false,
                    outputModifier: outputModifier);
                CompleteStepMeasurements(
                    ref completedProductionBatchCount,
                    ref productionActiveProgressSeconds);
                accumulatorSeconds -= StepSeconds;
                if (accumulatorSeconds < StepEpsilon)
                    accumulatorSeconds = 0f;
                stepped = true;
            }
            PublishStatisticsDelta(new ProductionStatisticsDelta(
                completedProductionBatchCount,
                productionActiveProgressSeconds,
                productionEligibleSeconds));
            if (stepped) PublishObservabilityIfChanged();
        }

        private void PublishStatisticsDelta(ProductionStatisticsDelta delta)
        {
            LastStatisticsDelta = delta;
            if (delta.IsEmpty) return;
            unchecked { StatisticsRevision++; }
        }

        private void CaptureStepMeasurements(
            ref float productionEligibleSeconds)
        {
            stepMeasurements.Clear();
            IReadOnlyList<BuildingProductionState> runnableStates =
                Runtime.RunnableStates;
            for (var index = 0; index < runnableStates.Count; index++)
            {
                BuildingProductionState state = runnableStates[index];
                if (state == null || state.IsPlayerPaused) continue;
                productionEligibleSeconds += StepSeconds;
                stepMeasurements.Add(new ProductionStateMeasurement(
                    state,
                    state.CompletionRevision,
                    state.ProgressSeconds));
            }
        }

        private void CompleteStepMeasurements(
            ref int completedProductionBatchCount,
            ref float productionActiveProgressSeconds)
        {
            for (var index = 0; index < stepMeasurements.Count; index++)
            {
                ProductionStateMeasurement measurement =
                    stepMeasurements[index];
                ulong completedCycles =
                    measurement.State.CompletionRevision -
                    measurement.CompletionRevision;
                completedProductionBatchCount += (int)completedCycles;
                if (completedCycles > 0)
                    PublishBatchesCompleted(
                        measurement.State,
                        completedCycles);
                float activeSeconds =
                    (completedCycles * measurement.State.Definition.DurationSeconds) +
                    measurement.State.ProgressSeconds -
                    measurement.ProgressSeconds;
                productionActiveProgressSeconds += Math.Max(
                    0f,
                    Math.Min(StepSeconds, activeSeconds));
            }
        }

        private void PublishBatchesCompleted(
            BuildingProductionState state,
            ulong completedCycles)
        {
            Action<BuildingProductionState, ulong> handlers =
                ProductionBatchesCompleted;
            if (handlers == null) return;
            Delegate[] subscribers = handlers.GetInvocationList();
            for (var index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<BuildingProductionState, ulong>)
                        subscribers[index])(state, completedCycles);
                }
                catch (Exception exception)
                {
                    LastBatchNotificationFailure = exception;
                }
            }
        }

        internal void PublishObservabilityIfChanged()
        {
            ulong contentHash = Runtime.ComputeObservabilityContentHash(
                latestWorld);
            if (latestCityStorage != null)
            {
                unchecked
                {
                    contentHash ^= latestCityStorage.Revision;
                    contentHash *= 1099511628211ul;
                }
            }
            if (hasPublishedContentHash &&
                contentHash == publishedContentHash)
            {
                return;
            }

            snapshot = Runtime.CaptureObservability(
                Revision + 1,
                latestWorld);
            publishedContentHash = contentHash;
            hasPublishedContentHash = true;
            unchecked { ObservabilityCaptureCount++; }
        }

        private readonly struct ProductionStateMeasurement
        {
            public ProductionStateMeasurement(
                BuildingProductionState state,
                ulong completionRevision,
                float progressSeconds)
            {
                State = state;
                CompletionRevision = completionRevision;
                ProgressSeconds = progressSeconds;
            }

            public BuildingProductionState State { get; }
            public ulong CompletionRevision { get; }
            public float ProgressSeconds { get; }
        }
    }
}
