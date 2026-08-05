namespace WasteCity.Graybox3D
{
    public readonly struct GrayboxInputSuppression
    {
        public bool Move { get; }
        public bool Deployment { get; }
        public bool Destination { get; }
        public bool CameraDrag { get; }
        public bool Home { get; }

        public GrayboxInputSuppression(
            bool move,
            bool deployment,
            bool destination,
            bool cameraDrag,
            bool home)
        {
            Move = move;
            Deployment = deployment;
            Destination = destination;
            CameraDrag = cameraDrag;
            Home = home;
        }
    }

    public interface IGrayboxInputInterceptor
    {
        GrayboxInputSuppression ProcessCurrentInput();
    }
}
