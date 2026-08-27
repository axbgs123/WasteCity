using System;
using WasteCity.Combat;
using WasteCity.Defense;
using WasteCity.Progression;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxAttentionPressureDefenseController3D :
        IDisposable
    {
        private readonly AttentionPressureRuntime pressure;
        private readonly GrayboxDefenseRuntime3D defense;

        public GrayboxAttentionPressureDefenseController3D(
            AttentionPressureRuntime pressure,
            GrayboxDefenseRuntime3D defense)
        {
            this.pressure = pressure ??
                throw new ArgumentNullException(nameof(pressure));
            this.defense = defense ??
                throw new ArgumentNullException(nameof(defense));
            defense.PressureCampaignTerminalCommitted += HandleTerminal;
        }

        public AttentionPressureCommand LastCompletionCommand { get; private set; }

        public event Action<AttentionPressureCommand> EncounterStarted;
        public event Action<AttentionPressureCommand> EncounterCompleted;

        public bool TryHandle(
            AttentionPressureCommand command,
            out string error)
        {
            if (command == null || command.Kind !=
                    AttentionPressureCommandKind.StartEncounterRequested)
            {
                error = "压力命令不是遭遇启动请求";
                return false;
            }
            SingleCityDefenseCampaignDefinition definition =
                AttentionPressureCampaignCatalog.Find(command.EncounterId);
            if (!defense.TryStartPressure(definition, out error)) return false;
            Publish(EncounterStarted, command);
            return true;
        }

        public void Dispose()
        {
            defense.PressureCampaignTerminalCommitted -= HandleTerminal;
            EncounterStarted = null;
            EncounterCompleted = null;
        }

        private void HandleTerminal(
            string encounterId,
            SingleCityDefenseCampaignResult result)
        {
            if (result != SingleCityDefenseCampaignResult.Victory) return;
            if (pressure.TryCompleteActive(
                    encounterId,
                    out AttentionPressureCommand command,
                    out _))
            {
                LastCompletionCommand = command;
                Publish(EncounterCompleted, command);
            }
        }

        private static void Publish(
            Action<AttentionPressureCommand> handlers,
            AttentionPressureCommand command)
        {
            if (handlers == null) return;
            Delegate[] subscribers = handlers.GetInvocationList();
            for (var index = 0; index < subscribers.Length; index++)
            {
                try
                {
                    ((Action<AttentionPressureCommand>)subscribers[index])(
                        command);
                }
                catch
                {
                    // Observability cannot roll back committed combat truth.
                }
            }
        }
    }
}
