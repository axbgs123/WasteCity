using System;
using System.Globalization;
using WasteCity.Persistence;

namespace WasteCity.City
{
    public enum CityMode { Mobile, Deploying, Fortress, Packing }

    public sealed class CityDeploymentModel
    {
        private const float CompletionEpsilonSeconds = 0.00001f;
        private readonly float deployDuration;
        private readonly float packDuration;
        private float remaining;
        private long nextCheckpointOrdinal = 1;
        private CityMode transitionReturnMode = CityMode.Mobile;
        public CityMode Mode { get; private set; } = CityMode.Mobile;
        public float Remaining => remaining;
        public CityMode TransitionReturnMode => transitionReturnMode;
        public float Progress => Mode == CityMode.Deploying ? 1f - remaining / deployDuration : Mode == CityMode.Packing ? 1f - remaining / packDuration : 1f;
        public event Action<CityMode> Changed;
        public event Action<string, string> CheckpointCommitted;

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
            CityMode completedTransition = Mode;
            CityMode stableMode = completedTransition == CityMode.Deploying
                ? CityMode.Fortress
                : CityMode.Mobile;
            ChangeMode(stableMode, 0f);
            string reasonId = stableMode == CityMode.Fortress
                ? FormalSaveCheckpointReasonIds.FirstDeploymentComplete
                : FormalSaveCheckpointReasonIds.PackingComplete;
            string stableEventId = "city-transition." +
                nextCheckpointOrdinal.ToString(
                    "D6",
                    CultureInfo.InvariantCulture) + "." +
                stableMode.ToString().ToLowerInvariant();
            unchecked { nextCheckpointOrdinal++; }
            CheckpointCommitted?.Invoke(reasonId, stableEventId);
        }
        public void Restore(CityMode mode,float remainingSeconds){Mode=Enum.IsDefined(typeof(CityMode),mode)?mode:CityMode.Mobile;transitionReturnMode=GetTransitionReturnMode(Mode);remaining=(Mode==CityMode.Deploying||Mode==CityMode.Packing)?Math.Max(.001f,remainingSeconds):0f;Changed?.Invoke(Mode);}

        public bool TryRestore(
            CityMode mode,
            CityMode returnMode,
            float remainingSeconds,
            out string error)
        {
            if (!Enum.IsDefined(typeof(CityMode), mode) ||
                !Enum.IsDefined(typeof(CityMode), returnMode))
            {
                error = "城市模式或转换返回模式无效";
                return false;
            }

            CityMode expectedReturnMode = GetTransitionReturnMode(mode);
            if (returnMode != expectedReturnMode)
            {
                error = "转换返回模式与当前城市模式不一致";
                return false;
            }

            if (float.IsNaN(remainingSeconds) ||
                float.IsInfinity(remainingSeconds) ||
                remainingSeconds < 0f)
            {
                error = "转换剩余时间必须是非负有限数";
                return false;
            }

            bool isTransition = mode == CityMode.Deploying || mode == CityMode.Packing;
            if (!isTransition && remainingSeconds != 0f)
            {
                error = "稳定城市模式不能保留转换剩余时间";
                return false;
            }

            float maximumDuration = mode == CityMode.Deploying
                ? deployDuration
                : packDuration;
            if (isTransition && remainingSeconds > maximumDuration)
            {
                error = "转换剩余时间超过正式配置时长";
                return false;
            }

            Mode = mode;
            transitionReturnMode = returnMode;
            remaining = remainingSeconds;
            error = string.Empty;
            Changed?.Invoke(Mode);
            return true;
        }

        private void ChangeMode(CityMode mode, float remainingSeconds)
        {
            Mode = mode;
            transitionReturnMode = GetTransitionReturnMode(mode);
            remaining = Math.Max(0f, remainingSeconds);
            Changed?.Invoke(Mode);
        }

        private static CityMode GetTransitionReturnMode(CityMode mode)
        {
            return mode == CityMode.Fortress || mode == CityMode.Packing
                ? CityMode.Fortress
                : CityMode.Mobile;
        }
    }
}
