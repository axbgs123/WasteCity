using WasteCity.City;

namespace WasteCity.World
{
    public enum CameraFollowMode
    {
        Following = 0,
        Free = 1
    }

    public sealed class CameraFollowModel
    {
        public CameraFollowMode Mode { get; private set; } =
            CameraFollowMode.Following;

        public DirectControlTarget Target { get; private set; } =
            DirectControlTarget.City;

        public void BeginFreeDrag()
        {
            Mode = CameraFollowMode.Free;
        }

        public void EndFreeDrag()
        {
        }

        public bool ObserveTarget(
            DirectControlTarget requestedTarget,
            bool leaderTargetAvailable)
        {
            DirectControlTarget effectiveTarget =
                requestedTarget == DirectControlTarget.Leader &&
                leaderTargetAvailable
                    ? DirectControlTarget.Leader
                    : DirectControlTarget.City;

            if (effectiveTarget == Target)
                return false;

            Target = effectiveTarget;
            Mode = CameraFollowMode.Following;
            return true;
        }

        public void ReturnToTarget()
        {
            Mode = CameraFollowMode.Following;
        }
    }
}
