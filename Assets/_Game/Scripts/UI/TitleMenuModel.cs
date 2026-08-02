namespace WasteCity.UI
{
    public enum TitleMenuState { Main, Help, Started }
    public sealed class TitleMenuModel
    {
        public TitleMenuState State { get; private set; }
        public bool StartNew(){if(State==TitleMenuState.Started)return false;State=TitleMenuState.Started;return true;}
        public bool Continue(bool hasSave){if(State==TitleMenuState.Started||!hasSave)return false;State=TitleMenuState.Started;return true;}
        public bool OpenHelp(){if(State!=TitleMenuState.Main)return false;State=TitleMenuState.Help;return true;}
        public bool Back(){if(State!=TitleMenuState.Help)return false;State=TitleMenuState.Main;return true;}
    }
}
