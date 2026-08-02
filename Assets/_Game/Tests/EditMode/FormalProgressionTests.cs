using NUnit.Framework;
using WasteCity.Progression;
using WasteCity.Research;

namespace WasteCity.Tests
{
    public sealed class FormalProgressionTests
    {
        [Test] public void ObservationThresholdsAreIdempotentAndCapped(){var m=new ObservationModel();int hits=0;m.ThresholdReached+=_=>hits++;m.Add("a",35);m.Add("b",5);m.Add("c",100);Assert.That(hits,Is.EqualTo(3));Assert.That(m.Value,Is.EqualTo(100));Assert.That(m.RecentReasons.Count,Is.EqualTo(3));}
        [Test] public void RestoredObservationDoesNotRetriggerReachedThresholds(){var m=new ObservationModel();int hits=0;m.ThresholdReached+=_=>hits++;m.Restore(61);m.Add("after-load",1);Assert.That(hits,Is.Zero);m.Add("boss",40);Assert.That(hits,Is.EqualTo(1));}
        [Test] public void EraRequiresTrackAndResearchDepth(){var m=new EraTrackModel();m.Add(DevelopmentRoute.Technology,40);Assert.That(m.TryTrigger(DevelopmentRoute.Technology,5),Is.False);Assert.That(m.TryTrigger(DevelopmentRoute.Technology,6),Is.True);Assert.That(m.Get(DevelopmentRoute.Technology),Is.EqualTo(50));}
        [Test] public void CivilizationAdvanceIsActiveAndConditional(){var m=new CivilizationModel();Assert.That(m.TryAdvance(0,1),Is.False);Assert.That(m.TryAdvance(1,1),Is.True);Assert.That(m.Level,Is.EqualTo(2));}
    }
}
