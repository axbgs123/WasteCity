using UnityEngine;

namespace WasteCity.Graybox3D
{
    public enum ResourceNodeMarkerLod3D
    {
        Near,
        Mid,
        Far
    }

    [CreateAssetMenu(
        fileName = "FormalMapNavigationProfile3D",
        menuName = "WasteCity/Presentation/Formal Map Navigation 3D")]
    public sealed class FormalMapNavigationProfile3D : ScriptableObject
    {
        public const float DefaultMinimumOrthographicSize = 8f;
        public const float DefaultOrthographicSize = 13f;
        public const float DefaultMaximumOrthographicSize = 26f;
        public const float DefaultZoomSensitivity = 1f / 120f;
        public const float DefaultNearMarkerMaximumSize = 15f;
        public const float DefaultMidMarkerMaximumSize = 21f;
        public const string ResourcesPath =
            "Presentation/FormalMapNavigationProfile3D";

        [SerializeField]
        private float minimumOrthographicSize =
            DefaultMinimumOrthographicSize;
        [SerializeField]
        private float defaultOrthographicSize =
            DefaultOrthographicSize;
        [SerializeField]
        private float maximumOrthographicSize =
            DefaultMaximumOrthographicSize;
        [SerializeField]
        private float zoomSensitivity = DefaultZoomSensitivity;
        [SerializeField]
        private float nearMarkerMaximumSize =
            DefaultNearMarkerMaximumSize;
        [SerializeField]
        private float midMarkerMaximumSize =
            DefaultMidMarkerMaximumSize;

        public float MinimumOrthographicSize => minimumOrthographicSize;
        public float DefaultSize => defaultOrthographicSize;
        public float MaximumOrthographicSize => maximumOrthographicSize;
        public float ZoomSensitivity => zoomSensitivity;
        public float NearMarkerMaximumSize => nearMarkerMaximumSize;
        public float MidMarkerMaximumSize => midMarkerMaximumSize;

        public void Configure(
            float minimumOrthographicSize,
            float defaultOrthographicSize,
            float maximumOrthographicSize,
            float zoomSensitivity,
            float nearMarkerMaximumSize,
            float midMarkerMaximumSize)
        {
            this.minimumOrthographicSize = minimumOrthographicSize;
            this.defaultOrthographicSize = defaultOrthographicSize;
            this.maximumOrthographicSize = maximumOrthographicSize;
            this.zoomSensitivity = zoomSensitivity;
            this.nearMarkerMaximumSize = nearMarkerMaximumSize;
            this.midMarkerMaximumSize = midMarkerMaximumSize;
        }

        public bool TryValidate(out string error)
        {
            if (!IsFinitePositive(minimumOrthographicSize) ||
                !IsFinitePositive(defaultOrthographicSize) ||
                !IsFinitePositive(maximumOrthographicSize) ||
                minimumOrthographicSize > defaultOrthographicSize ||
                defaultOrthographicSize > maximumOrthographicSize)
            {
                error = "Camera sizes must be finite, positive, and ordered.";
                return false;
            }
            if (!IsFinitePositive(zoomSensitivity))
            {
                error = "Zoom sensitivity must be finite and positive.";
                return false;
            }
            if (!IsFinitePositive(nearMarkerMaximumSize) ||
                !IsFinitePositive(midMarkerMaximumSize) ||
                nearMarkerMaximumSize >= midMarkerMaximumSize ||
                nearMarkerMaximumSize < minimumOrthographicSize ||
                midMarkerMaximumSize > maximumOrthographicSize)
            {
                error = "Marker LOD sizes must be finite and ordered " +
                    "inside the camera zoom range.";
                return false;
            }
            error = string.Empty;
            return true;
        }

        public float ResolveOrthographicSize(
            float currentSize,
            float scrollDeltaY)
        {
            if (!IsFinite(currentSize) || !IsFinite(scrollDeltaY))
                return Mathf.Clamp(
                    defaultOrthographicSize,
                    minimumOrthographicSize,
                    maximumOrthographicSize);
            return Mathf.Clamp(
                currentSize - scrollDeltaY * zoomSensitivity,
                minimumOrthographicSize,
                maximumOrthographicSize);
        }

        public ResourceNodeMarkerLod3D ResolveMarkerLod(
            float orthographicSize)
        {
            if (orthographicSize <= nearMarkerMaximumSize)
                return ResourceNodeMarkerLod3D.Near;
            if (orthographicSize <= midMarkerMaximumSize)
                return ResourceNodeMarkerLod3D.Mid;
            return ResourceNodeMarkerLod3D.Far;
        }

        private static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
