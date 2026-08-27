using NUnit.Framework;
using UnityEngine;
using WasteCity.Core;

namespace WasteCity.Tests
{
    public sealed class GameSpeedTests
    {
        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
        }

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

        [TestCase(GamePauseReason.User)]
        [TestCase(GamePauseReason.Title)]
        [TestCase(GamePauseReason.Session)]
        [TestCase(GamePauseReason.Defeat)]
        [TestCase(GamePauseReason.SystemMenu)]
        [TestCase(GamePauseReason.CampaignVictory)]
        public void SpeedIgnoringAdvancementPreservesRequestedSpeedOnlyWhenAlone(
            GamePauseReason foreignReason)
        {
            var model = new GameSpeedModel();
            model.Set(2f);
            model.SetPaused(GamePauseReason.Advancement, true);

            Assert.That(
                model.SpeedIgnoring(GamePauseReason.Advancement),
                Is.EqualTo(2f));

            model.SetPaused(foreignReason, true);
            Assert.That(
                model.SpeedIgnoring(GamePauseReason.Advancement),
                Is.Zero,
                "Ignoring Advancement must never bypass another pause.");
        }

        [TestCase(GamePauseReason.User)]
        [TestCase(GamePauseReason.Title)]
        [TestCase(GamePauseReason.Session)]
        [TestCase(GamePauseReason.Defeat)]
        [TestCase(GamePauseReason.Advancement)]
        [TestCase(GamePauseReason.CampaignVictory)]
        public void IDEA0007_SystemMenuPauseReasonIsIndependent(
            GamePauseReason foreignReason)
        {
            var model = new GameSpeedModel();
            model.Set(2f);
            model.SetPaused(foreignReason, true);
            model.SetPaused(GamePauseReason.SystemMenu, true);

            model.SetPaused(GamePauseReason.SystemMenu, false);

            Assert.That(model.IsPaused(GamePauseReason.SystemMenu), Is.False);
            Assert.That(model.IsPaused(foreignReason), Is.True);
            Assert.That(model.RequestedSpeed, Is.EqualTo(2f));
            Assert.That(model.Speed, Is.Zero);
            model.SetPaused(foreignReason, false);
            Assert.That(model.Speed, Is.EqualTo(2f));
        }
    }
}
