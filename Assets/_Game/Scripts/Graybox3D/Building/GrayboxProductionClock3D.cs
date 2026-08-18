using System;
using System.Collections.Generic;
using WasteCity.City;
using WasteCity.Economy;
using WasteCity.World;

namespace WasteCity.Graybox3D.Building
{
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

        public GrayboxProductionRuntime3D Runtime { get; } =
            new GrayboxProductionRuntime3D();
        public GrayboxProductionCommandFacade3D Commands { get; }
        public ProductionObservabilitySnapshot Snapshot => snapshot;
        public ulong Revision => snapshot.Revision;
        public uint ObservabilityCaptureCount { get; private set; }
        public float AccumulatorSeconds => accumulatorSeconds;
        internal WorldMapModel LatestWorld => latestWorld;
        internal CityResourceStorageModel LatestCityStorage =>
            latestCityStorage;

        public GrayboxProductionClock3D()
        {
            Commands = new GrayboxProductionCommandFacade3D(this);
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
            if (paused || cityInventory == null)
                return;

            latestWorld = world;
            latestCityStorage = null;
            accumulatorSeconds += Math.Max(0f, deltaSeconds);
            bool stepped = false;
            while (accumulatorSeconds + StepEpsilon >= StepSeconds)
            {
                Runtime.Synchronize(
                    instances,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius);
                simulation.Tick(
                    Runtime.RunnableStates,
                    StepSeconds,
                    world,
                    cityInventory,
                    cityCapacity,
                    Runtime.ActiveWarehouseCount,
                    globallyPaused: false);
                accumulatorSeconds -= StepSeconds;
                if (accumulatorSeconds < StepEpsilon)
                    accumulatorSeconds = 0f;
                stepped = true;
            }
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
            if (paused || cityStorage == null) return;

            latestWorld = world;
            latestCityStorage = cityStorage;
            accumulatorSeconds += Math.Max(0f, deltaSeconds);
            bool stepped = false;
            while (accumulatorSeconds + StepEpsilon >= StepSeconds)
            {
                Runtime.Synchronize(
                    instances,
                    cityMode,
                    cityX,
                    cityY,
                    groundRadius,
                    cityStorage);
                simulation.Tick(
                    Runtime.RunnableStates,
                    StepSeconds,
                    world,
                    cityStorage,
                    globallyPaused: false);
                accumulatorSeconds -= StepSeconds;
                if (accumulatorSeconds < StepEpsilon)
                    accumulatorSeconds = 0f;
                stepped = true;
            }
            if (stepped) PublishObservabilityIfChanged();
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
    }
}
