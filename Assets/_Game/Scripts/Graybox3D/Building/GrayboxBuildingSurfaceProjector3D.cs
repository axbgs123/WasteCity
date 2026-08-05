using System;
using UnityEngine;
using WasteCity.Building;

namespace WasteCity.Graybox3D.Building
{
    public readonly struct BuildingSurfaceHit
    {
        public bool IsValid { get; }
        public BuildingSite Site { get; }
        public int X { get; }
        public int Y { get; }
        public Vector3 WorldPoint { get; }
        public string SurfaceLabel { get; }

        public static BuildingSurfaceHit Invalid => default;

        public BuildingSurfaceHit(
            bool isValid,
            BuildingSite site,
            int x,
            int y,
            Vector3 worldPoint,
            string surfaceLabel)
        {
            IsValid = isValid;
            Site = site;
            X = x;
            Y = y;
            WorldPoint = worldPoint;
            SurfaceLabel = surfaceLabel;
        }
    }

    public sealed class GrayboxBuildingSurfaceProjector3D : MonoBehaviour
    {
        private const float InnerAnchorX = -1.28f;
        private const float InnerAnchorZ = -.96f;
        private const float InnerCellSize = .32f;
        private const int InnerWidth = 8;
        private const int InnerHeight = 6;
        private const float PreviewLift = .01f;

        [SerializeField] private Camera controlledCamera;
        [SerializeField] private GrayboxWorldView3D worldView;
        [SerializeField] private GrayboxMobileCityController3D city;
        [SerializeField] private Collider innerCitySurface;

        public void Configure(
            Camera controlledCamera,
            GrayboxWorldView3D worldView,
            GrayboxMobileCityController3D city,
            Collider innerCitySurface)
        {
            this.controlledCamera = controlledCamera;
            this.worldView = worldView;
            this.city = city;
            this.innerCitySurface = innerCitySurface;
        }

        public bool TryProject(
            Vector2 screenPosition,
            out BuildingSurfaceHit hit)
        {
            hit = BuildingSurfaceHit.Invalid;
            if (controlledCamera == null ||
                worldView == null ||
                worldView.Coordinates == null ||
                city == null ||
                innerCitySurface == null)
                return false;

            Ray ray = controlledCamera.ScreenPointToRay(screenPosition);
            if (innerCitySurface.enabled &&
                innerCitySurface.Raycast(
                    ray,
                    out RaycastHit platformHit,
                    float.PositiveInfinity))
                return TryCreateInnerHit(platformHit.point, out hit);

            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float distance) ||
                distance < 0f)
                return false;

            Vector3 groundPoint = ray.GetPoint(distance);
            if (!worldView.Coordinates.TryWorldToCell(
                    groundPoint,
                    out int x,
                    out int y) ||
                !worldView.Coordinates.TryCellToWorld(
                    x,
                    y,
                    0f,
                    out Vector3 snapped))
                return false;

            hit = new BuildingSurfaceHit(
                true,
                BuildingSite.Ground,
                x,
                y,
                snapped,
                "外城");
            return true;
        }

        private bool TryCreateInnerHit(
            Vector3 platformPoint,
            out BuildingSurfaceHit hit)
        {
            hit = BuildingSurfaceHit.Invalid;
            Vector3 local = city.transform.InverseTransformPoint(platformPoint);
            int x = Mathf.FloorToInt(
                (local.x - InnerAnchorX) / InnerCellSize);
            int y = Mathf.FloorToInt(
                (local.z - InnerAnchorZ) / InnerCellSize);
            if (x < 0 || y < 0 || x >= InnerWidth || y >= InnerHeight)
                return false;

            Vector3 centerLocal = new Vector3(
                InnerAnchorX + (x + .5f) * InnerCellSize,
                local.y + PreviewLift,
                InnerAnchorZ + (y + .5f) * InnerCellSize);
            hit = new BuildingSurfaceHit(
                true,
                BuildingSite.InnerCity,
                x,
                y,
                city.transform.TransformPoint(centerLocal),
                "内城");
            return true;
        }
    }
}
