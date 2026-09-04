using System;

namespace WasteCity.World.Exploration
{
    [Flags]
    public enum OutpostOperationalIssue
    {
        None = 0,
        CommunicationLost = 1 << 0,
        SupplyInterrupted = 1 << 1,
        MaintenanceInterrupted = 1 << 2,
    }

    public enum OutpostOperationalStatus
    {
        Normal = 0,
        Limited = 1,
        Disconnected = 2,
    }

    /// <summary>
    /// Read-only projection of settlement-owned operational link truth.
    /// It deliberately does not store inventory, transport, or map state.
    /// </summary>
    public sealed class OutpostOperationalState
    {
        public OutpostOperationalState(
            string settlementId,
            bool communicationActive,
            bool supplied,
            bool maintained)
        {
            if (string.IsNullOrWhiteSpace(settlementId))
                throw new ArgumentException(
                    "A stable settlement ID is required.",
                    nameof(settlementId));
            SettlementId = settlementId;
            IsCommunicationActive = communicationActive;
            IsSupplied = supplied;
            IsMaintained = maintained;
        }

        public string SettlementId { get; }
        public bool IsCommunicationActive { get; }
        public bool IsSupplied { get; }
        public bool IsMaintained { get; }

        public bool CanIssueRemoteCommands => IsCommunicationActive;

        public bool CanOperateAutonomously => IsSupplied && IsMaintained;

        public OutpostOperationalStatus OverallStatus
        {
            get
            {
                if (!IsCommunicationActive)
                    return OutpostOperationalStatus.Disconnected;
                if (!IsSupplied || !IsMaintained)
                    return OutpostOperationalStatus.Limited;
                return OutpostOperationalStatus.Normal;
            }
        }

        public OutpostOperationalIssue Issues
        {
            get
            {
                var result = OutpostOperationalIssue.None;
                if (!IsCommunicationActive)
                    result |= OutpostOperationalIssue.CommunicationLost;
                if (!IsSupplied)
                    result |= OutpostOperationalIssue.SupplyInterrupted;
                if (!IsMaintained)
                    result |= OutpostOperationalIssue.MaintenanceInterrupted;
                return result;
            }
        }
    }
}
