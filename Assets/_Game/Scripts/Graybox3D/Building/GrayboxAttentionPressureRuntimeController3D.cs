using System;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxAttentionPressureRuntimeController3D :
        IDisposable
    {
        private readonly FormalAttentionRuntime attention;
        private readonly AttentionPressureRuntime pressure;
        private readonly GrayboxAttentionPressureDefenseController3D defense;
        private bool bound;

        public GrayboxAttentionPressureRuntimeController3D(
            FormalAttentionRuntime attention,
            AttentionPressureRuntime pressure,
            GrayboxAttentionPressureDefenseController3D defense)
        {
            this.attention = attention ??
                throw new ArgumentNullException(nameof(attention));
            this.pressure = pressure ??
                throw new ArgumentNullException(nameof(pressure));
            this.defense = defense ??
                throw new ArgumentNullException(nameof(defense));
        }

        public Exception LastWarningNotificationFailure { get; private set; }

        public event Action<AttentionPressureCommand> WarningStarted;

        public void Bind()
        {
            if (bound) return;
            bound = true;
            attention.ThresholdReached += HandleThresholdReached;
            FormalAttentionSnapshot snapshot = attention.Capture();
            for (var index = 0;
                 index < snapshot.ReachedThresholds.Count;
                 index++)
            {
                pressure.TryQueueThreshold(
                    snapshot.ReachedThresholds[index],
                    out _);
            }
        }

        public bool Tick(
            float ruleDeltaSeconds,
            bool mainCampaignActive,
            bool tutorialCompleted,
            bool firstMachineGunCompleted,
            out string error)
        {
            AttentionPressureSnapshot before = pressure.Capture();
            if (!pressure.Tick(
                    ruleDeltaSeconds,
                    mainCampaignActive,
                    tutorialCompleted,
                    firstMachineGunCompleted,
                    out AttentionPressureCommand command,
                    out error))
            {
                return false;
            }

            if (command.Kind == AttentionPressureCommandKind.WarningStarted)
            {
                PublishWarning(command);
                error = string.Empty;
                return true;
            }
            if (command.Kind !=
                    AttentionPressureCommandKind.StartEncounterRequested)
            {
                error = string.Empty;
                return true;
            }
            if (defense.TryHandle(command, out error)) return true;

            string dispatchError = error;
            if (!pressure.TryRestore(before, out string rollbackError))
            {
                error = dispatchError + "；压力状态回滚失败：" + rollbackError;
                return false;
            }
            error = dispatchError;
            return false;
        }

        public void Dispose()
        {
            if (!bound) return;
            attention.ThresholdReached -= HandleThresholdReached;
            bound = false;
            WarningStarted = null;
        }

        private void HandleThresholdReached(int threshold)
        {
            pressure.TryQueueThreshold(threshold, out _);
        }

        private void PublishWarning(AttentionPressureCommand command)
        {
            Action<AttentionPressureCommand> handlers = WarningStarted;
            if (handlers == null) return;
            Delegate[] subscribers = handlers.GetInvocationList();
            for (var index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<AttentionPressureCommand>)subscribers[index])(
                        command);
                }
                catch (Exception exception)
                {
                    LastWarningNotificationFailure = exception;
                }
            }
        }
    }
}
