using System;

namespace WasteCity.Building
{
    public interface IProductivitySource { float ConstructionMultiplier { get; } }
    public sealed class ConstructionProgress
    {
        public float BaseDuration { get; }
        public float Remaining { get; private set; }
        public bool IsComplete => Remaining <= 0f;
        public float Normalized => BaseDuration <= 0f ? 1f : Math.Max(0f, Math.Min(1f, 1f - Remaining / BaseDuration));
        public ConstructionProgress(float baseDuration) { BaseDuration = Math.Max(.1f, baseDuration); Remaining = BaseDuration; }
        public bool Tick(float delta, float productivity)
        {
            if (IsComplete || delta <= 0f) return false;
            Remaining = Math.Max(0f, Remaining - delta * Math.Max(0f, productivity));
            return IsComplete;
        }
        public void Restore(float remaining) => Remaining = Math.Max(0f, Math.Min(BaseDuration, remaining));
    }
}
