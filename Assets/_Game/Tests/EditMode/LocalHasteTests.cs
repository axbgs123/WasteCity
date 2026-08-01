using NUnit.Framework;
using WasteCity.Legacy;

namespace WasteCity.Tests
{
    public sealed class LocalHasteTests
    {
        [Test] public void ActiveHasteUsesFiveTimesMultiplierAndConsumesPool(){var m=new LocalHasteModel();m.SetActive(true);m.Tick(10,1);Assert.That(m.Multiplier,Is.EqualTo(5));Assert.That(m.Remaining,Is.EqualTo(50));}
        [Test] public void HasteStopsWhenDailyPoolIsEmpty(){var m=new LocalHasteModel();m.SetActive(true);m.Tick(60,1);Assert.That(m.Active,Is.False);Assert.That(m.Multiplier,Is.EqualTo(1));Assert.That(m.Remaining,Is.Zero);}
        [Test] public void NewDayRefillsPool(){var m=new LocalHasteModel();m.SetActive(true);m.Tick(40,1);m.Tick(0,2);Assert.That(m.Remaining,Is.EqualTo(60));Assert.That(m.PoolDay,Is.EqualTo(2));}
        [Test] public void HasteStateCanBeRestored(){var m=new LocalHasteModel();m.Restore(4,12.5f,true);Assert.That(m.PoolDay,Is.EqualTo(4));Assert.That(m.Remaining,Is.EqualTo(12.5f));Assert.That(m.Active,Is.True);}
    }
}
