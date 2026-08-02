using NUnit.Framework;
using WasteCity.Core;

namespace WasteCity.Tests
{
    public sealed class SessionStatisticsTests
    {
        [Test] public void StatisticsAccumulateOnlyValidValues(){var m=new SessionStatisticsModel();m.Tick(12.5f,44);m.AddKill();m.AddProduction(3);m.AddBuildingLoss();m.AddRescue(false);m.MarkRetreat();Assert.That(m.ElapsedSeconds,Is.EqualTo(12.5f));Assert.That(m.HighestObservation,Is.EqualTo(44));Assert.That(m.Kills,Is.EqualTo(1));Assert.That(m.ProductionCycles,Is.EqualTo(3));Assert.That(m.DelayedRescues,Is.EqualTo(1));Assert.That(m.RetreatedDuringBoss,Is.True);}
        [Test] public void HighestObservationNeverMovesBackward(){var m=new SessionStatisticsModel();m.Tick(1,60);m.Tick(1,20);Assert.That(m.HighestObservation,Is.EqualTo(60));}
        [Test] public void StatisticsCanBeRestored(){var m=new SessionStatisticsModel();m.Restore(90,12,88,31,2,3,1,true);Assert.That(m.ElapsedSeconds,Is.EqualTo(90));Assert.That(m.Kills,Is.EqualTo(12));Assert.That(m.BuildingLosses,Is.EqualTo(2));Assert.That(m.Rescues,Is.EqualTo(3));}
    }
}
