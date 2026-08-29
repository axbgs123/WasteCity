using UnityEngine;

namespace WasteCity.Graybox3D
{
    public static class FormalInnerCityPresentationPolicy3D
    {
        public static Vector3 CellCenterLocal(int x, int y, float localY)
        {
            if (!Contains(x, y))
                return new Vector3(float.NaN, float.NaN, float.NaN);
            return FootprintCenterLocal(x, y, 1, 1, localY);
        }

        public static Vector3 FootprintCenterLocal(
            int x,
            int y,
            int width,
            int height,
            float localY)
        {
            Vector2 anchor = new Vector2(
                -FormalWorldPresentationScaleProfile3D.InnerGridWidthCells *
                    FormalWorldPresentationScaleProfile3D.InnerCellSize * .5f,
                -FormalWorldPresentationScaleProfile3D.InnerGridHeightCells *
                    FormalWorldPresentationScaleProfile3D.InnerCellSize * .5f);
            return new Vector3(
                anchor.x + (x + width * .5f) *
                    FormalWorldPresentationScaleProfile3D.InnerCellSize,
                localY,
                anchor.y + (y + height * .5f) *
                    FormalWorldPresentationScaleProfile3D.InnerCellSize);
        }

        public static bool TryProjectWorldPoint(
            Vector3 cityPosition,
            Quaternion cityRotation,
            Vector3 worldPoint,
            float localY,
            out int x,
            out int y,
            out Vector3 centerWorld)
        {
            Vector3 local = Quaternion.Inverse(cityRotation) *
                (worldPoint - cityPosition);
            float anchorX =
                -FormalWorldPresentationScaleProfile3D.InnerGridWidthCells *
                FormalWorldPresentationScaleProfile3D.InnerCellSize * .5f;
            float anchorY =
                -FormalWorldPresentationScaleProfile3D.InnerGridHeightCells *
                FormalWorldPresentationScaleProfile3D.InnerCellSize * .5f;
            x = Mathf.FloorToInt((local.x - anchorX) /
                FormalWorldPresentationScaleProfile3D.InnerCellSize);
            y = Mathf.FloorToInt((local.z - anchorY) /
                FormalWorldPresentationScaleProfile3D.InnerCellSize);
            if (!Contains(x, y))
            {
                centerWorld = default;
                return false;
            }
            centerWorld = cityPosition + cityRotation *
                CellCenterLocal(x, y, localY);
            return true;
        }

        public static void OrientVerticalBillboard(
            Transform target,
            Vector3 cameraPosition)
        {
            if (target == null) return;
            Vector3 direction = cameraPosition - target.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= .000001f) return;
            target.rotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        }

        private static bool Contains(int x, int y)
        {
            return x >= 0 && y >= 0 &&
                x < FormalWorldPresentationScaleProfile3D.InnerGridWidthCells &&
                y < FormalWorldPresentationScaleProfile3D.InnerGridHeightCells;
        }
    }
}
