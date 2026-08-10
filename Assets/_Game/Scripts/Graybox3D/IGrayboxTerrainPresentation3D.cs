namespace WasteCity.Graybox3D
{
    public interface IGrayboxTerrainPresentation3D
    {
        bool TryPresent(GrayboxWorldView3D worldView);
        void ClearPresentation();
    }
}
