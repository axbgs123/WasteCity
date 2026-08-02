using NUnit.Framework;
using WasteCity.Narrative;

namespace WasteCity.Tests
{
    public sealed class GuidanceFlowTests
    {
        [Test] public void MainObjectivesAdvanceInFormalOrder(){var model=new GuidanceFlowModel();model.SignalMoved();Assert.That(model.Stage,Is.EqualTo(GuidanceStage.Discovery));model.SignalFortress();model.SignalMiningBuilt();model.SignalTurretBuilt();model.SignalWaveCompleted(30);Assert.That(model.Stage,Is.EqualTo(GuidanceStage.PressureTest));model.SignalWaveCompleted(60);Assert.That(model.Stage,Is.EqualTo(GuidanceStage.Broodmother));model.SignalBossDefeated();Assert.That(model.Stage,Is.EqualTo(GuidanceStage.Complete));}
        [Test] public void OutOfOrderSignalsCannotSkipObjectives(){var model=new GuidanceFlowModel();model.SignalTurretBuilt();model.SignalBossDefeated();Assert.That(model.Stage,Is.EqualTo(GuidanceStage.Awakening));}
        [Test] public void GuidanceStageRoundTrips(){var model=new GuidanceFlowModel();model.Restore((int)GuidanceStage.ProductionChain);Assert.That(model.Stage,Is.EqualTo(GuidanceStage.ProductionChain));Assert.That(model.Objective,Does.Contain("机枪塔"));}
    }
}
