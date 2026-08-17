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

        public GrayboxProductionRuntime3D Runtime { get; } =
            new GrayboxProductionRuntime3D();
        public float AccumulatorSeconds => accumulatorSeconds;

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

            accumulatorSeconds += Math.Max(0f, deltaSeconds);
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
            }
        }
    }
}
