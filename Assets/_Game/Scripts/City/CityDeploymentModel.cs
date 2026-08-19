using System;

namespace WasteCity.City
{
    public enum CityMode { Mobile, Deploying, Fortress, Packing }

    public sealed class CityDeploymentModel
    {
        private const float CompletionEpsilonSeconds = 0.00001f;
        private readonly float deployDuration;
        private readonly float packDuration;
        private float remaining;
        public CityMode Mode { get; private set; } = CityMode.Mobile;
        public float Remaining => remaining;
        public float Progress => Mode == CityMode.Deploying ? 1f - remaining / deployDuration : Mode == CityMode.Packing ? 1f - remaining / packDuration : 1f;
        public event Action<CityMode> Changed;

        public CityDeploymentModel(float deployDuration, float packDuration)
        { this.deployDuration = Math.Max(0.1f, deployDuration); this.packDuration = Math.Max(0.1f, packDuration); }
        public bool Toggle()
        {
            if (Mode == CityMode.Mobile)
            {
                ChangeMode(CityMode.Deploying, deployDuration);
                return true;
            }
            if (Mode == CityMode.Deploying)
            {
                ChangeMode(CityMode.Mobile, 0f);
                return true;
            }
            if (Mode == CityMode.Fortress)
            {
                ChangeMode(CityMode.Packing, packDuration);
                return true;
            }
            if (Mode == CityMode.Packing)
            {
                ChangeMode(CityMode.Fortress, 0f);
                return true;
            }
            return false;
        }
        public void Tick(float delta)
        {
            if (Mode != CityMode.Deploying && Mode != CityMode.Packing) return;
            remaining -= Math.Max(0f, delta);
            if (remaining > CompletionEpsilonSeconds) return;
            ChangeMode(
                Mode == CityMode.Deploying
                    ? CityMode.Fortress
                    : CityMode.Mobile,
                0f);
        }
        public void Restore(CityMode mode,float remainingSeconds){Mode=Enum.IsDefined(typeof(CityMode),mode)?mode:CityMode.Mobile;remaining=(Mode==CityMode.Deploying||Mode==CityMode.Packing)?Math.Max(.001f,remainingSeconds):0f;Changed?.Invoke(Mode);}

        private void ChangeMode(CityMode mode, float remainingSeconds)
        {
            Mode = mode;
            remaining = Math.Max(0f, remainingSeconds);
            Changed?.Invoke(Mode);
        }
    }
}
