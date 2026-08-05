using UnityEngine;
using WasteCity.City;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxCameraController3D : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private Transform cameraRig;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxLeaderController3D leader;
        [SerializeField]
        private GrayboxDirectControlCoordinator directControl;
        [SerializeField] private GrayboxGroundProjector groundProjector;

        private readonly CameraFollowModel followModel =
            new CameraFollowModel();
        private bool freeDragActive;
        private Vector3 dragAnchor;

        public CameraFollowMode Mode => followModel.Mode;
        public DirectControlTarget CurrentTarget =>
            followModel.Target;
        public bool ReferencesReady =>
            controlledCamera != null &&
            cameraRig != null &&
            city != null &&
            directControl != null &&
            groundProjector != null;

        public void Configure(
            Camera camera,
            Transform cameraRig,
            GrayboxMobileCityController3D city,
            GrayboxLeaderController3D leader,
            GrayboxDirectControlCoordinator directControl,
            GrayboxGroundProjector groundProjector)
        {
            controlledCamera = camera;
            this.cameraRig = cameraRig;
            this.city = city;
            this.leader = leader;
            this.directControl = directControl;
            this.groundProjector = groundProjector;
        }

        public void BeginFreeDrag(Vector2 screenPosition)
        {
            if (groundProjector == null ||
                !groundProjector.TryProjectToPlane(
                    screenPosition,
                    out dragAnchor))
                return;

            freeDragActive = true;
            followModel.BeginFreeDrag();
        }

        public void ContinueFreeDrag(Vector2 screenPosition)
        {
            if (!freeDragActive ||
                followModel.Mode != CameraFollowMode.Free ||
                cameraRig == null ||
                groundProjector == null ||
                !groundProjector.TryProjectToPlane(
                    screenPosition,
                    out Vector3 current))
                return;

            Vector3 position = cameraRig.position;
            position.x += dragAnchor.x - current.x;
            position.z += dragAnchor.z - current.z;
            cameraRig.position = position;
        }

        public void EndFreeDrag()
        {
            freeDragActive = false;
            followModel.EndFreeDrag();
        }

        public void ReturnToTarget()
        {
            freeDragActive = false;
            ObserveCurrentTarget();
            followModel.ReturnToTarget();
            SnapRigToEffectiveTarget();
        }

        public void TickCamera()
        {
            bool targetChanged = ObserveCurrentTarget();
            if (targetChanged ||
                followModel.Mode == CameraFollowMode.Following)
                SnapRigToEffectiveTarget();
        }

        private void LateUpdate()
        {
            TickCamera();
        }

        private bool ObserveCurrentTarget()
        {
            directControl?.Refresh();
            DirectControlTarget requested =
                directControl?.ControlTarget ??
                DirectControlTarget.City;
            return followModel.ObserveTarget(
                requested,
                IsLeaderTargetAvailable());
        }

        private bool IsLeaderTargetAvailable()
        {
            return leader != null && leader.Model.Recruited;
        }

        private void SnapRigToEffectiveTarget()
        {
            if (cameraRig == null)
                return;

            Transform target =
                followModel.Target ==
                    DirectControlTarget.Leader &&
                IsLeaderTargetAvailable()
                    ? leader.transform
                    : city == null
                        ? null
                        : city.transform;
            if (target == null)
                return;

            Vector3 position = cameraRig.position;
            position.x = target.position.x;
            position.z = target.position.z;
            cameraRig.position = position;
        }
    }
}
