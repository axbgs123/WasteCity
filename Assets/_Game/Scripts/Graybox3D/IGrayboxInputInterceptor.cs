namespace WasteCity.Graybox3D
{
    public interface IGrayboxDeploymentRequest
    {
        WasteCity.City.CityMode Mode { get; }
        bool TryToggleDeployment(out string failureReason);
    }

    public readonly struct GrayboxInputSuppression
    {
        public bool Move { get; }
        public bool Deployment { get; }
        public bool Destination { get; }
        public bool CameraDrag { get; }
        public bool Home { get; }
        public bool Zoom { get; }

        public GrayboxInputSuppression(
            bool move,
            bool deployment,
            bool destination,
            bool cameraDrag,
            bool home,
            bool zoom = false)
        {
            Move = move;
            Deployment = deployment;
            Destination = destination;
            CameraDrag = cameraDrag;
            Home = home;
            Zoom = zoom;
        }
    }

    public interface IGrayboxInputInterceptor
    {
        GrayboxInputSuppression ProcessCurrentInput();
    }
}
