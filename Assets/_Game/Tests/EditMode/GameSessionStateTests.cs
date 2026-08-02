using NUnit.Framework;
using WasteCity.Core;

namespace WasteCity.Tests
{
    public sealed class GameSessionStateTests
    {
        [Test] public void PauseTogglesOnlyDuringLivePlay(){var model=new GameSessionStateModel();Assert.That(model.TogglePause(),Is.True);Assert.That(model.State,Is.EqualTo(GameSessionState.Paused));model.TogglePause();Assert.That(model.State,Is.EqualTo(GameSessionState.Playing));}
        [Test] public void DefeatIsIdempotentAndCannotPause(){var model=new GameSessionStateModel();Assert.That(model.Defeat(),Is.True);Assert.That(model.Defeat(),Is.False);Assert.That(model.TogglePause(),Is.False);Assert.That(model.State,Is.EqualTo(GameSessionState.Defeated));}
        [Test] public void RetryReturnsSessionToPlaying(){var model=new GameSessionStateModel();model.Defeat();model.ResumeAfterRetry();Assert.That(model.State,Is.EqualTo(GameSessionState.Playing));}
    }
}
