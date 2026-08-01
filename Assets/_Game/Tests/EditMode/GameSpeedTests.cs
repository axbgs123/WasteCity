using NUnit.Framework;
using WasteCity.Core;

namespace WasteCity.Tests
{
    public sealed class GameSpeedTests
    {
        [Test] public void PauseRestoresPreviousSpeed() { var model = new GameSpeedModel(); model.Set(2f); model.TogglePause(); Assert.That(model.Speed, Is.Zero); model.TogglePause(); Assert.That(model.Speed, Is.EqualTo(2f)); }
        [Test] public void SpeedIsClampedToSupportedRange() { var model = new GameSpeedModel(); model.Set(5f); Assert.That(model.Speed, Is.EqualTo(2f)); model.Set(-1f); Assert.That(model.Speed, Is.Zero); }
    }
}
