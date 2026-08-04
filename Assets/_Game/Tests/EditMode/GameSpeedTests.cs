using NUnit.Framework;
using WasteCity.Core;

namespace WasteCity.Tests
{
    public sealed class GameSpeedTests
    {
        [Test] public void PauseRestoresPreviousSpeed() { var model = new GameSpeedModel(); model.Set(2f); model.TogglePause(); Assert.That(model.Speed, Is.Zero); model.TogglePause(); Assert.That(model.Speed, Is.EqualTo(2f)); }
        [Test] public void SpeedIsClampedToSupportedRange() { var model = new GameSpeedModel(); model.Set(5f); Assert.That(model.Speed, Is.EqualTo(2f)); model.Set(-1f); Assert.That(model.Speed, Is.Zero); }
        [Test]
        public void IndependentPauseReasonsMustAllClearBeforeTimeResumes()
        {
            var model = new GameSpeedModel();
            model.SetPaused(GamePauseReason.Title, true);
            model.SetPaused(GamePauseReason.Advancement, true);

            model.SetPaused(GamePauseReason.Title, false);

            Assert.That(model.Speed, Is.Zero);
            model.SetPaused(GamePauseReason.Advancement, false);
            Assert.That(model.Speed, Is.EqualTo(1f));
        }
    }
}
