using System;
using WasteCity.City;
using WasteCity.Leader.CivilizationExpansion;

namespace WasteCity.Leader.Exploration
{
    public enum LeaderControlMode
    {
        AI = 0,
        Manual = 1,
    }

    public enum LeaderControlBlockReason
    {
        None = 0,
        CityNotFortress = 1,
        NotRecruited = 2,
        LeaderNotActive = 3,
        ModalBlocked = 4,
    }

    public readonly struct LeaderControlResolution
    {
        internal LeaderControlResolution(
            LeaderControlMode requestedMode,
            LeaderControlMode actualMode,
            DirectControlTarget controlTarget,
            LeaderControlBlockReason blockReason)
        {
            RequestedMode = requestedMode;
            ActualMode = actualMode;
            ControlTarget = controlTarget;
            BlockReason = blockReason;
        }

        public LeaderControlMode RequestedMode { get; }
        public LeaderControlMode ActualMode { get; }
        public DirectControlTarget ControlTarget { get; }
        public LeaderControlBlockReason BlockReason { get; }
        public bool CanManuallyControl =>
            ActualMode == LeaderControlMode.Manual &&
            ControlTarget == DirectControlTarget.Leader;
    }

    public sealed class LeaderControlRuntime
    {
        public LeaderControlMode RequestedMode { get; private set; } =
            LeaderControlMode.AI;
        public ulong Revision { get; private set; }

        public bool TryRequest(LeaderControlMode mode, out string error)
        {
            if (!Enum.IsDefined(typeof(LeaderControlMode), mode))
            {
                error = "领袖控制模式无效";
                return false;
            }
            if (RequestedMode == mode)
            {
                error = string.Empty;
                return true;
            }

            RequestedMode = mode;
            unchecked { Revision++; }
            error = string.Empty;
            return true;
        }

        public bool TryRestore(LeaderControlMode mode, out string error)
        {
            if (!Enum.IsDefined(typeof(LeaderControlMode), mode))
            {
                error = "领袖控制存档模式无效";
                return false;
            }
            if (RequestedMode != mode)
            {
                RequestedMode = mode;
                unchecked { Revision++; }
            }
            error = string.Empty;
            return true;
        }

        public LeaderControlResolution Resolve(
            CityMode cityMode,
            bool leaderRecruited,
            CharacterLifeState leaderState,
            bool modalBlocksWorldInteraction)
        {
            LeaderControlBlockReason reason = ResolveBlockReason(
                cityMode,
                leaderRecruited,
                leaderState,
                modalBlocksWorldInteraction);
            bool manual = RequestedMode == LeaderControlMode.Manual &&
                reason == LeaderControlBlockReason.None;
            return new LeaderControlResolution(
                RequestedMode,
                manual ? LeaderControlMode.Manual : LeaderControlMode.AI,
                manual
                    ? DirectControlTarget.Leader
                    : DirectControlTarget.City,
                RequestedMode == LeaderControlMode.AI
                    ? LeaderControlBlockReason.None
                    : reason);
        }

        private static LeaderControlBlockReason ResolveBlockReason(
            CityMode cityMode,
            bool leaderRecruited,
            CharacterLifeState leaderState,
            bool modalBlocksWorldInteraction)
        {
            if (!leaderRecruited)
                return LeaderControlBlockReason.NotRecruited;
            if (leaderState != CharacterLifeState.Active)
                return LeaderControlBlockReason.LeaderNotActive;
            if (cityMode != CityMode.Fortress)
                return LeaderControlBlockReason.CityNotFortress;
            return modalBlocksWorldInteraction
                ? LeaderControlBlockReason.ModalBlocked
                : LeaderControlBlockReason.None;
        }
    }
}
