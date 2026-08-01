using NUnit.Framework;
using WasteCity.Core;
using WasteCity.Legacy;
using WasteCity.World;

namespace WasteCity.Tests
{
    public sealed class GameClockAndForesightTests
    {
        [Test] public void ClockAdvancesDaysAndCarriesRemainder(){var clock=new GameClockModel(100);clock.Tick(250);Assert.That(clock.Day,Is.EqualTo(3));Assert.That(clock.SecondsIntoDay,Is.EqualTo(50));}
        [Test] public void ClockStateCanBeRestored(){var clock=new GameClockModel(100);clock.Restore(8,42);Assert.That(clock.Day,Is.EqualTo(8));Assert.That(clock.SecondsIntoDay,Is.EqualTo(42));}
        [Test] public void ForesightFlashesOnlyOncePerDay(){var model=new ForesightFlashModel(new WorldSeed(8128),600);float at=model.ScheduledSecond(1);Assert.That(model.TryFlash(1,at-.1f),Is.False);Assert.That(model.TryFlash(1,at+.1f),Is.True);Assert.That(model.TryFlash(1,599),Is.False);Assert.That(model.TryFlash(2,model.ScheduledSecond(2)+.1f),Is.True);}
        [Test] public void ForesightScheduleIsDeterministicAndInsideDay(){var a=new ForesightFlashModel(new WorldSeed(9),600);var b=new ForesightFlashModel(new WorldSeed(9),600);Assert.That(a.ScheduledSecond(3),Is.EqualTo(b.ScheduledSecond(3)));Assert.That(a.ScheduledSecond(3),Is.InRange(90f,510f));}
    }
}
