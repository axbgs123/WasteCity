using System;
using WasteCity.Core;
using WasteCity.Defense;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxCampaignTerminalSpeedGate3D
    {
        private readonly GameSpeedModel speed;
        private SingleCityDefenseCampaignPhase phase =
            SingleCityDefenseCampaignPhase.Idle;
        private bool ownsVictoryPause;
        private bool ownsDefeatPause;
        private bool victoryContinued;

        public GrayboxCampaignTerminalSpeedGate3D(GameSpeedModel speed)
        {
            this.speed = speed ??
                throw new ArgumentNullException(nameof(speed));
        }

        public bool CanContinueSandbox =>
            phase == SingleCityDefenseCampaignPhase.Victory &&
            !victoryContinued;
        public bool BlocksRuleProgress =>
            phase == SingleCityDefenseCampaignPhase.Defeat ||
            (phase == SingleCityDefenseCampaignPhase.Victory &&
             !victoryContinued);

        public void Synchronize(
            SingleCityDefenseCampaignSnapshot snapshot)
        {
            SingleCityDefenseCampaignPhase nextPhase = snapshot?.Phase ??
                SingleCityDefenseCampaignPhase.Idle;
            if (nextPhase != SingleCityDefenseCampaignPhase.Victory)
                victoryContinued = false;

            switch (nextPhase)
            {
                case SingleCityDefenseCampaignPhase.Victory:
                    ReleaseDefeatPause();
                    if (!victoryContinued)
                        AcquireVictoryPause();
                    break;
                case SingleCityDefenseCampaignPhase.Defeat:
                    ReleaseVictoryPause();
                    AcquireDefeatPause();
                    break;
                default:
                    ReleaseVictoryPause();
                    ReleaseDefeatPause();
                    break;
            }
            phase = nextPhase;
        }

        public bool TryContinueSandbox()
        {
            if (!CanContinueSandbox) return false;

            victoryContinued = true;
            speed.Set(speed.LastNonZeroSpeed);
            speed.SetPaused(GamePauseReason.User, false);
            ReleaseVictoryPause();
            return true;
        }

        private void AcquireVictoryPause()
        {
            if (ownsVictoryPause ||
                speed.IsPaused(GamePauseReason.Advancement))
            {
                return;
            }
            speed.SetPaused(GamePauseReason.Advancement, true);
            ownsVictoryPause = true;
        }

        private void AcquireDefeatPause()
        {
            if (ownsDefeatPause || speed.IsPaused(GamePauseReason.Defeat))
                return;
            speed.SetPaused(GamePauseReason.Defeat, true);
            ownsDefeatPause = true;
        }

        private void ReleaseVictoryPause()
        {
            if (!ownsVictoryPause) return;
            speed.SetPaused(GamePauseReason.Advancement, false);
            ownsVictoryPause = false;
        }

        private void ReleaseDefeatPause()
        {
            if (!ownsDefeatPause) return;
            speed.SetPaused(GamePauseReason.Defeat, false);
            ownsDefeatPause = false;
        }
    }
}
