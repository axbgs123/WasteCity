namespace WasteCity.Graybox3D
{
    public interface IGrayboxTerrainPresentation3D
    {
        bool TryPresent(GrayboxWorldView3D worldView);
        void ClearPresentation();
    }

    public interface IGrayboxTerrainPresentationAttempt3D
    {
        string LastPresentationError { get; }
        bool TryPresent(GrayboxWorldView3D worldView, bool logFailure);
    }

    public interface IGrayboxTerrainPresentationSource3D
    {
        void ReleasePresentationSource();
    }
}
