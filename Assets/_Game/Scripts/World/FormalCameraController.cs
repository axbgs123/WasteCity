using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;
using WasteCity.Leader;

namespace WasteCity.World
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class FormalCameraController : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private FormalLeaderController leader;
        [SerializeField] private Transform leaderTarget;

        private readonly CameraFollowModel model = new CameraFollowModel();

        public CameraFollowMode Mode => model.Mode;
        public DirectControlTarget CurrentTarget => model.Target;
        public bool ReferencesReady =>
            controlledCamera != null &&
            city != null &&
            leader != null &&
            leaderTarget != null;

        private void Awake()
        {
            if (controlledCamera == null)
                controlledCamera = GetComponent<Camera>();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.middleButton.wasPressedThisFrame)
                    BeginFreeDrag();

                if (mouse.middleButton.isPressed)
                    ApplyPointerDelta(
                        mouse.delta.ReadValue(),
                        Screen.height);

                if (mouse.middleButton.wasReleasedThisFrame)
                    EndFreeDrag();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null &&
                keyboard.homeKey.wasPressedThisFrame)
                ReturnToTarget();
        }

        private void LateUpdate()
        {
            TickCamera();
        }

        public void Configure(
            Camera camera,
            PlaceholderMobileCity cityController,
            FormalLeaderController leaderController,
            Transform leaderFollowTarget)
        {
            controlledCamera = camera;
            city = cityController;
            leader = leaderController;
            leaderTarget = leaderFollowTarget;
        }

        public void BeginFreeDrag()
        {
            model.BeginFreeDrag();
        }

        public void EndFreeDrag()
        {
            model.EndFreeDrag();
        }

        public void ApplyPointerDelta(
            Vector2 screenDelta,
            float screenHeight)
        {
            if (model.Mode != CameraFollowMode.Free ||
                controlledCamera == null ||
                screenHeight <= 0f ||
                screenDelta.sqrMagnitude <= 0f)
                return;

            float worldUnitsPerPixel =
                2f * controlledCamera.orthographicSize / screenHeight;
            Vector3 position = controlledCamera.transform.position;
            position.x -= screenDelta.x * worldUnitsPerPixel;
            position.y -= screenDelta.y * worldUnitsPerPixel;
            controlledCamera.transform.position = position;
        }

        public void ReturnToTarget()
        {
            ObserveCurrentTarget();
            model.ReturnToTarget();
            SnapToCurrentTarget();
        }

        public void TickCamera()
        {
            ObserveCurrentTarget();
            if (model.Mode == CameraFollowMode.Following)
                SnapToCurrentTarget();
        }

        private void ObserveCurrentTarget()
        {
            DirectControlTarget requestedTarget =
                leader == null
                    ? DirectControlTarget.City
                    : leader.ControlTarget;
            model.ObserveTarget(
                requestedTarget,
                IsLeaderTargetAvailable());
        }

        private bool IsLeaderTargetAvailable()
        {
            return leader != null &&
                   leaderTarget != null &&
                   leaderTarget.gameObject.activeInHierarchy;
        }

        private void SnapToCurrentTarget()
        {
            if (controlledCamera == null)
                return;

            Transform target = model.Target == DirectControlTarget.Leader &&
                               IsLeaderTargetAvailable()
                ? leaderTarget
                : city == null
                    ? null
                    : city.transform;
            if (target == null)
                return;

            Vector3 position = controlledCamera.transform.position;
            position.x = target.position.x;
            position.y = target.position.y;
            controlledCamera.transform.position = position;
        }
    }
}
