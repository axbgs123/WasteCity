using System;
using UnityEngine;
using WasteCity.Leader.Exploration;

namespace WasteCity.Graybox3D.Exploration
{
    [DisallowMultipleComponent]
    public sealed class GrayboxCenJinDistressPresenter3D : MonoBehaviour
    {
        private const float MarkerHeight = 2.4f;
        private const float TargetVisibleWidth = 1.35f;

        private Transform markerRoot;
        private PlanarCoordinateMapper3D coordinates;

        public GameObject MarkerObject { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        public void Configure(
            Transform root,
            PlanarCoordinateMapper3D mapper,
            Sprite sprite)
        {
            Configure(root, mapper, sprite, new Rect(0f, 0f, 1f, 1f));
        }

        public void Configure(
            Transform root,
            PlanarCoordinateMapper3D mapper,
            Sprite sprite,
            Rect visibleBounds)
        {
            markerRoot = root ?? throw new ArgumentNullException(nameof(root));
            coordinates = mapper ??
                throw new ArgumentNullException(nameof(mapper));
            EnsureMarker();
            Renderer.sprite = sprite;
            float scale = Production2DVisualScalePolicy3D
                .ResolveSpriteWorldScale(
                    sprite,
                    visibleBounds,
                    TargetVisibleWidth);
            MarkerObject.transform.localScale = Vector3.one * scale;
            if (!coordinates.TryCellToWorld(
                    LeaderInteractionCatalog.CenJinDistressCellX,
                    LeaderInteractionCatalog.CenJinDistressCellY,
                    MarkerHeight,
                    out Vector3 position))
            {
                throw new InvalidOperationException(
                    "岑烬求救点超出正式地图范围");
            }
            MarkerObject.transform.position = position;
            MarkerObject.SetActive(false);
        }

        public void Apply(CenJinDistressState state)
        {
            EnsureMarker();
            bool active = state == CenJinDistressState.Discovered ||
                state == CenJinDistressState.Rescuing;
            MarkerObject.SetActive(active);
            if (!active) return;
            Renderer.color = state == CenJinDistressState.Rescuing
                ? new Color(1f, .82f, .34f, 1f)
                : new Color(1f, .48f, .30f, 1f);
            FaceCamera();
        }

        private void LateUpdate()
        {
            if (MarkerObject != null && MarkerObject.activeSelf)
                FaceCamera();
        }

        private void FaceCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 direction =
                camera.transform.position - MarkerObject.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= .000001f) return;
            MarkerObject.transform.rotation = Quaternion.LookRotation(
                direction.normalized,
                Vector3.up);
        }

        private void EnsureMarker()
        {
            if (MarkerObject != null) return;
            if (markerRoot == null)
                markerRoot = transform;
            MarkerObject = new GameObject("Exploration.CenJinDistressMarker");
            MarkerObject.transform.SetParent(markerRoot, false);
            Renderer = MarkerObject.AddComponent<SpriteRenderer>();
            Renderer.sortingOrder = 26;
        }

        private void OnDestroy()
        {
            if (MarkerObject == null) return;
            if (Application.isPlaying) Destroy(MarkerObject);
            else DestroyImmediate(MarkerObject);
            MarkerObject = null;
            Renderer = null;
            coordinates = null;
            markerRoot = null;
        }
    }
}
