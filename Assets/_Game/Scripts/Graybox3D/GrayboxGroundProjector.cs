using UnityEngine;

namespace WasteCity.Graybox3D
{
    public sealed class GrayboxGroundProjector : MonoBehaviour
    {
        [SerializeField] private Camera controlledCamera;
        [SerializeField] private GrayboxWorldView3D worldView;
        private PlanarCoordinateMapper3D injectedCoordinates;

        public void Configure(
            Camera camera,
            PlanarCoordinateMapper3D coordinates)
        {
            controlledCamera = camera;
            worldView = null;
            injectedCoordinates = coordinates;
        }

        public void Configure(
            Camera camera,
            GrayboxWorldView3D worldView)
        {
            controlledCamera = camera;
            this.worldView = worldView;
            injectedCoordinates = null;
        }

        public bool TryProjectToPlane(
            Vector2 screenPosition,
            out Vector3 worldPoint)
        {
            worldPoint = default;
            if (controlledCamera == null)
                return false;

            Ray ray =
                controlledCamera.ScreenPointToRay(screenPosition);
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float distance) ||
                distance < 0f)
                return false;

            worldPoint = ray.GetPoint(distance);
            worldPoint.y = 0f;
            return true;
        }

        public bool TryProjectToCell(
            Vector2 screenPosition,
            out Vector3 worldPoint,
            out int cellX,
            out int cellY)
        {
            PlanarCoordinateMapper3D coordinates =
                injectedCoordinates ?? worldView?.Coordinates;
            if (!TryProjectToPlane(
                    screenPosition,
                    out worldPoint) ||
                coordinates == null ||
                !coordinates.TryWorldToCell(
                    worldPoint,
                    out cellX,
                    out cellY))
            {
                cellX = -1;
                cellY = -1;
                return false;
            }

            return true;
        }
    }
}
