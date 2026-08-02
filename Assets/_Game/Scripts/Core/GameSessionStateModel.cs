namespace WasteCity.Core
{
    public enum GameSessionState { Playing,Paused,Defeated }
    public sealed class GameSessionStateModel
    {
        public GameSessionState State { get; private set; }=GameSessionState.Playing;
        public bool TogglePause(){if(State==GameSessionState.Defeated)return false;State=State==GameSessionState.Paused?GameSessionState.Playing:GameSessionState.Paused;return true;}
        public bool Defeat(){if(State==GameSessionState.Defeated)return false;State=GameSessionState.Defeated;return true;}
        public void ResumeAfterRetry()=>State=GameSessionState.Playing;
    }
}
