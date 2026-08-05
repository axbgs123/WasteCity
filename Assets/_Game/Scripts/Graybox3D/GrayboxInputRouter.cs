using UnityEngine;
using UnityEngine.InputSystem;
using WasteCity.City;

namespace WasteCity.Graybox3D
{
    public readonly struct GrayboxInputFrame
    {
        public Vector2 Move { get; }
        public Vector2 PointerPosition { get; }
        public bool ToggleDeploymentPressed { get; }
        public bool DestinationPressed { get; }
        public bool MiddlePressed { get; }
        public bool MiddleHeld { get; }
        public bool MiddleReleased { get; }
        public bool HomePressed { get; }

        public GrayboxInputFrame(
            Vector2 move,
            Vector2 pointerPosition,
            bool toggleDeploymentPressed,
            bool destinationPressed,
            bool middlePressed,
            bool middleHeld,
            bool middleReleased,
            bool homePressed)
        {
            Move = move;
            PointerPosition = pointerPosition;
            ToggleDeploymentPressed =
                toggleDeploymentPressed;
            DestinationPressed = destinationPressed;
            MiddlePressed = middlePressed;
            MiddleHeld = middleHeld;
            MiddleReleased = middleReleased;
            HomePressed = homePressed;
        }
    }

    public sealed class GrayboxInputRouter : MonoBehaviour
    {
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private GrayboxLeaderController3D leader;
        [SerializeField]
        private GrayboxDirectControlCoordinator directControl;
        [SerializeField] private GrayboxGroundProjector groundProjector;
        [SerializeField]
        private GrayboxCameraController3D cameraController;

        public void Configure(
            GrayboxMobileCityController3D city,
            GrayboxLeaderController3D leader,
            GrayboxDirectControlCoordinator directControl,
            GrayboxGroundProjector groundProjector,
            GrayboxCameraController3D cameraController)
        {
            this.city = city;
            this.leader = leader;
            this.directControl = directControl;
            this.groundProjector = groundProjector;
            this.cameraController = cameraController;
        }

        public GrayboxInputFrame ReadCurrentFrame()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            float horizontal = 0f;
            float vertical = 0f;
            bool toggleDeploymentPressed = false;
            bool homePressed = false;
            if (keyboard != null)
            {
                horizontal =
                    (keyboard.dKey.isPressed ? 1f : 0f) -
                    (keyboard.aKey.isPressed ? 1f : 0f);
                vertical =
                    (keyboard.wKey.isPressed ? 1f : 0f) -
                    (keyboard.sKey.isPressed ? 1f : 0f);
                toggleDeploymentPressed =
                    keyboard.fKey.wasPressedThisFrame;
                homePressed =
                    keyboard.homeKey.wasPressedThisFrame;
            }

            Vector2 pointerPosition = Vector2.zero;
            bool destinationPressed = false;
            bool middlePressed = false;
            bool middleHeld = false;
            bool middleReleased = false;
            if (mouse != null)
            {
                pointerPosition = mouse.position.ReadValue();
                destinationPressed =
                    mouse.rightButton.wasPressedThisFrame;
                middlePressed =
                    mouse.middleButton.wasPressedThisFrame;
                middleHeld = mouse.middleButton.isPressed;
                middleReleased =
                    mouse.middleButton.wasReleasedThisFrame;
            }

            return new GrayboxInputFrame(
                new Vector2(horizontal, vertical),
                pointerPosition,
                toggleDeploymentPressed,
                destinationPressed,
                middlePressed,
                middleHeld,
                middleReleased,
                homePressed);
        }

        public void ProcessFrame(GrayboxInputFrame frame)
        {
            ProcessCameraInput(frame);
            if (Time.timeScale <= 0f)
                return;

            directControl?.Refresh();
            DirectControlTarget target =
                directControl?.ControlTarget ??
                DirectControlTarget.City;
            if (target == DirectControlTarget.Leader &&
                leader != null)
            {
                city?.ApplyManualInput(Vector2.zero);
                leader.ApplyManualInput(frame.Move);
            }
            else
            {
                leader?.ApplyManualInput(Vector2.zero);
                city?.ApplyManualInput(frame.Move);
            }

            if (frame.ToggleDeploymentPressed)
                city?.TryToggleDeployment(out _);

            if (frame.DestinationPressed &&
                city != null &&
                city.Mode == CityMode.Mobile &&
                groundProjector != null &&
                groundProjector.TryProjectToCell(
                    frame.PointerPosition,
                    out _,
                    out int cellX,
                    out int cellY))
            {
                city.TrySetDestinationCell(
                    cellX,
                    cellY,
                    out _);
            }
        }

        public void TickGameplay(float deltaTime)
        {
            if (Time.timeScale <= 0f || leader == null)
                return;

            directControl?.Refresh();
            DirectControlTarget target =
                directControl?.ControlTarget ??
                DirectControlTarget.City;
            leader.TickControl(
                target,
                Mathf.Max(0f, deltaTime));
        }

        private void Update()
        {
            ProcessFrame(ReadCurrentFrame());
            TickGameplay(Time.deltaTime);
        }

        private void ProcessCameraInput(GrayboxInputFrame frame)
        {
            if (cameraController == null)
                return;

            if (frame.MiddlePressed)
            {
                cameraController.BeginFreeDrag(
                    frame.PointerPosition);
            }
            else if (frame.MiddleHeld)
            {
                cameraController.ContinueFreeDrag(
                    frame.PointerPosition);
            }

            if (frame.MiddleReleased)
                cameraController.EndFreeDrag();
            if (frame.HomePressed)
                cameraController.ReturnToTarget();
        }
    }
}
