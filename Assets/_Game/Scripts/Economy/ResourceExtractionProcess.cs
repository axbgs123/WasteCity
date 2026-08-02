using System;
using WasteCity.World;

namespace WasteCity.Economy
{
    public enum ExtractionStatus { Running, NoNode, Depleted, OutputFull }

    public sealed class ResourceExtractionProcess
    {
        private readonly float duration;
        private float progress;
        public float Progress => progress;
        public float ProgressNormalized => Math.Min(1f, progress / duration);
        public ExtractionStatus Status { get; private set; } = ExtractionStatus.NoNode;

        public ResourceExtractionProcess(float durationSeconds = 3f) => duration = Math.Max(.1f, durationSeconds);

        public int Tick(float delta, WorldMapModel world, int x, int y, ResourceInventory inventory, int units = 1)
        {
            if (world == null || x < 0 || y < 0 || x >= world.Width || y >= world.Height || !world.Get(x, y).HasResource)
            { Status = ExtractionStatus.NoNode; return 0; }
            WorldCell cell = world.Get(x, y);
            if (cell.ResourceAmount <= 0) { Status = ExtractionStatus.Depleted; return 0; }
            if (inventory.Get(cell.ResourceId) >= inventory.CapacityPerResource) { Status = ExtractionStatus.OutputFull; return 0; }
            Status = ExtractionStatus.Running;
            progress += Math.Max(0f, delta) * Math.Max(1, units);
            int cycles = 0;
            while (progress >= duration)
            {
                cell = world.Get(x, y);
                if (cell.ResourceAmount <= 0) { Status = ExtractionStatus.Depleted; break; }
                if (inventory.Get(cell.ResourceId) >= inventory.CapacityPerResource) { Status = ExtractionStatus.OutputFull; break; }
                int harvested = world.Harvest(x, y, 1, out string resourceId);
                if (harvested <= 0) { Status = ExtractionStatus.Depleted; break; }
                inventory.Add(resourceId, harvested); progress -= duration; cycles++;
            }
            return cycles;
        }

        public void Restore(float savedProgress) => progress = Math.Max(0f, Math.Min(duration, savedProgress));
    }
}
