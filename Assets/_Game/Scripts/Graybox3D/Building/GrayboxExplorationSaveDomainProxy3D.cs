using System;
using WasteCity.Graybox3D.Exploration;
using WasteCity.Persistence.ThreeD;

namespace WasteCity.Graybox3D.Building
{
    public sealed class GrayboxExplorationSaveDomainProxy3D :
        IFormalThreeDExplorationSaveDomain
    {
        private readonly GrayboxExplorationController3D controller;
        private readonly Func<string> sessionIdProvider;
        private readonly Func<double> ruleTimeSecondsProvider;

        public GrayboxExplorationSaveDomainProxy3D(
            GrayboxExplorationController3D controller,
            Func<string> sessionIdProvider,
            Func<double> ruleTimeSecondsProvider)
        {
            this.controller = controller ??
                throw new ArgumentNullException(nameof(controller));
            this.sessionIdProvider = sessionIdProvider ??
                throw new ArgumentNullException(nameof(sessionIdProvider));
            this.ruleTimeSecondsProvider = ruleTimeSecondsProvider ??
                throw new ArgumentNullException(
                    nameof(ruleTimeSecondsProvider));
        }

        public GrayboxFormalSaveDomainId3D DomainId =>
            GrayboxFormalSaveDomainId3D.Exploration;

        public bool TryCapture(
            FormalThreeDSaveData destination,
            out string error)
        {
            return TryCreateAdapter(out GrayboxExplorationLeaderOutpostSaveAdapter3D adapter,
                    out error) &&
                adapter.TryCapture(destination, out error);
        }

        public bool TryApply(
            FormalThreeDSaveData source,
            out string error)
        {
            return TryCreateAdapter(out GrayboxExplorationLeaderOutpostSaveAdapter3D adapter,
                    out error) &&
                adapter.TryApply(source, out error);
        }

        private bool TryCreateAdapter(
            out GrayboxExplorationLeaderOutpostSaveAdapter3D adapter,
            out string error)
        {
            adapter = null;
            string sessionId = sessionIdProvider();
            if (!controller.IsInitialized ||
                string.IsNullOrWhiteSpace(sessionId))
            {
                error = "探索存档域尚未绑定正式会话";
                return false;
            }

            adapter = new GrayboxExplorationLeaderOutpostSaveAdapter3D(
                controller.Exploration,
                controller.LeaderControl,
                controller.ManualGather,
                controller.CenJinDistress,
                controller.OutpostAlerts,
                sessionId,
                ruleTimeSecondsProvider);
            error = string.Empty;
            return true;
        }
    }
}
