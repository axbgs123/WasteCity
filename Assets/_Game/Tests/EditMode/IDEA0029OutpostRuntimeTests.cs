using NUnit.Framework;
using WasteCity.World.Exploration;

namespace WasteCity.Tests
{
    public sealed class IDEA0029OutpostRuntimeTests
    {
        [Test]
        public void CommunicationLossBlocksRemoteCommandsButNotLocalAutonomy()
        {
            var state = new OutpostOperationalState(
                "core.outpost.000001",
                communicationActive: false,
                supplied: true,
                maintained: true);

            Assert.That(state.CanIssueRemoteCommands, Is.False);
            Assert.That(state.CanOperateAutonomously, Is.True);
            Assert.That(state.OverallStatus,
                Is.EqualTo(OutpostOperationalStatus.Disconnected));
            Assert.That(state.Issues,
                Is.EqualTo(OutpostOperationalIssue.CommunicationLost));
        }

        [Test]
        public void SupplyAndMaintenanceAreIndependentOperationalIssues()
        {
            var supply = new OutpostOperationalState(
                "core.outpost.000001", true, false, true);
            var maintenance = new OutpostOperationalState(
                "core.outpost.000001", true, true, false);
            var combined = new OutpostOperationalState(
                "core.outpost.000001", false, false, false);

            Assert.That(supply.CanIssueRemoteCommands, Is.True);
            Assert.That(supply.CanOperateAutonomously, Is.False);
            Assert.That(supply.OverallStatus,
                Is.EqualTo(OutpostOperationalStatus.Limited));
            Assert.That(supply.Issues,
                Is.EqualTo(OutpostOperationalIssue.SupplyInterrupted));
            Assert.That(maintenance.Issues,
                Is.EqualTo(OutpostOperationalIssue.MaintenanceInterrupted));
            Assert.That(combined.Issues, Is.EqualTo(
                OutpostOperationalIssue.CommunicationLost |
                OutpostOperationalIssue.SupplyInterrupted |
                OutpostOperationalIssue.MaintenanceInterrupted));
            Assert.That(combined.OverallStatus,
                Is.EqualTo(OutpostOperationalStatus.Disconnected),
                "Communication loss has priority over limited links.");
        }

        [Test]
        public void OperationalProjectionRequiresStableSettlementIdentity()
        {
            Assert.That(
                () => new OutpostOperationalState(null, true, true, true),
                Throws.ArgumentException);
            Assert.That(
                () => new OutpostOperationalState(" ", true, true, true),
                Throws.ArgumentException);
        }
    }
}
