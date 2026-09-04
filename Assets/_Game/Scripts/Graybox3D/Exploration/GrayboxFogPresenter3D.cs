using System;
using UnityEngine;
using WasteCity.World.Exploration;

namespace WasteCity.Graybox3D.Exploration
{
    public sealed class GrayboxFogPresenter3D : MonoBehaviour
    {
        private const float OverlayHeight = 4f;

        [SerializeField] private Transform fogRoot;
        [SerializeField] private Material fogMaterialTemplate;

        private PlanarCoordinateMapper3D coordinates;
        private GameObject overlayObject;
        private Mesh overlayMesh;
        private Material overlayMaterial;
        private Texture2D maskTexture;
        private WorldVisibilityState[] presentedStates =
            Array.Empty<WorldVisibilityState>();
        private Color32[] maskColors = Array.Empty<Color32>();
        private ulong appliedRevision;
        private bool hasAppliedRevision;

        public GameObject OverlayObject => overlayObject;
        public Mesh OverlayMesh => overlayMesh;
        public Texture2D MaskTexture => maskTexture;
        public int PersistentObjectCount => overlayObject == null ? 0 : 1;
        public int LastDirtyCellCount { get; private set; }
        public int MaskApplyCount { get; private set; }

        public void Configure(Transform fogRoot, Material materialTemplate)
        {
            if (fogRoot == null)
                throw new ArgumentNullException(nameof(fogRoot));
            if (materialTemplate == null)
                throw new ArgumentNullException(nameof(materialTemplate));
            ClearPresentation();
            this.fogRoot = fogRoot;
            fogMaterialTemplate = materialTemplate;
        }

        public void Generate(PlanarCoordinateMapper3D mapper)
        {
            if (mapper == null)
                throw new ArgumentNullException(nameof(mapper));
            if (fogRoot == null || fogMaterialTemplate == null)
            {
                throw new InvalidOperationException(
                    "Configure the fog presenter before generation.");
            }

            ClearPresentation();
            coordinates = mapper;
            int cellCount = mapper.Width * mapper.Height;
            presentedStates = new WorldVisibilityState[cellCount];
            maskColors = new Color32[cellCount];
            Color32 hidden = GrayboxFogVisualPolicy3D.Resolve(
                WorldVisibilityState.Hidden);
            for (var index = 0; index < cellCount; index++)
            {
                presentedStates[index] = WorldVisibilityState.Hidden;
                maskColors[index] = hidden;
            }

            maskTexture = new Texture2D(
                mapper.Width,
                mapper.Height,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "IDEA0029 Fog Mask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            maskTexture.SetPixels32(maskColors);
            maskTexture.Apply(false, false);

            overlayObject = new GameObject("IDEA0029 Fog Overlay");
            overlayObject.transform.SetParent(fogRoot, false);
            var filter = overlayObject.AddComponent<MeshFilter>();
            var renderer = overlayObject.AddComponent<MeshRenderer>();
            overlayMesh = CreateOverlayMesh(mapper.Width, mapper.Height);
            filter.sharedMesh = overlayMesh;
            overlayMaterial = new Material(fogMaterialTemplate)
            {
                name = "IDEA0029 Fog Overlay Runtime",
            };
            BindMaskTexture(overlayMaterial, maskTexture);
            renderer.sharedMaterial = overlayMaterial;
            LastDirtyCellCount = 0;
            MaskApplyCount = 0;
            appliedRevision = 0;
            hasAppliedRevision = false;
        }

        public bool ApplyVisibility(WorldVisibilityRuntime visibility)
        {
            if (visibility == null)
                throw new ArgumentNullException(nameof(visibility));
            EnsureGenerated();
            if (visibility.Width != coordinates.Width ||
                visibility.Height != coordinates.Height)
            {
                throw new ArgumentException(
                    "Visibility dimensions must match the generated fog mask.",
                    nameof(visibility));
            }
            if (hasAppliedRevision &&
                appliedRevision == visibility.Revision)
            {
                LastDirtyCellCount = 0;
                return false;
            }

            int dirtyCount = 0;
            for (var y = 0; y < coordinates.Height; y++)
            for (var x = 0; x < coordinates.Width; x++)
            {
                int index = y * coordinates.Width + x;
                WorldVisibilityState state = visibility.GetState(x, y);
                if (presentedStates[index] == state)
                    continue;
                presentedStates[index] = state;
                Color32 color = GrayboxFogVisualPolicy3D.Resolve(state);
                maskColors[index] = color;
                maskTexture.SetPixel(x, y, color);
                dirtyCount++;
            }

            hasAppliedRevision = true;
            appliedRevision = visibility.Revision;
            LastDirtyCellCount = dirtyCount;
            if (dirtyCount == 0)
                return false;
            maskTexture.Apply(false, false);
            MaskApplyCount++;
            return true;
        }

        public bool ApplyVisibility(WorldExplorationRuntime exploration)
        {
            if (exploration == null)
                throw new ArgumentNullException(nameof(exploration));
            EnsureGenerated();
            if (exploration.Width != coordinates.Width ||
                exploration.Height != coordinates.Height)
            {
                throw new ArgumentException(
                    "Exploration dimensions must match the generated fog mask.",
                    nameof(exploration));
            }
            if (hasAppliedRevision &&
                appliedRevision == exploration.VisibilityRevision)
            {
                LastDirtyCellCount = 0;
                return false;
            }

            int dirtyCount = 0;
            for (var y = 0; y < coordinates.Height; y++)
            for (var x = 0; x < coordinates.Width; x++)
            {
                int index = y * coordinates.Width + x;
                WorldVisibilityState state = exploration.GetState(x, y);
                if (presentedStates[index] == state)
                    continue;
                presentedStates[index] = state;
                Color32 color = GrayboxFogVisualPolicy3D.Resolve(state);
                maskColors[index] = color;
                maskTexture.SetPixel(x, y, color);
                dirtyCount++;
            }

            hasAppliedRevision = true;
            appliedRevision = exploration.VisibilityRevision;
            LastDirtyCellCount = dirtyCount;
            if (dirtyCount == 0)
                return false;
            maskTexture.Apply(false, false);
            MaskApplyCount++;
            return true;
        }

        public WorldVisibilityState GetPresentedState(int x, int y)
        {
            ValidateCell(x, y);
            return presentedStates[y * coordinates.Width + x];
        }

        public Color32 GetMaskColor(int x, int y)
        {
            ValidateCell(x, y);
            return maskColors[y * coordinates.Width + x];
        }

        public void ClearPresentation()
        {
            DestroyOwned(overlayObject);
            DestroyOwned(overlayMesh);
            DestroyOwned(overlayMaterial);
            DestroyOwned(maskTexture);
            overlayObject = null;
            overlayMesh = null;
            overlayMaterial = null;
            maskTexture = null;
            coordinates = null;
            presentedStates = Array.Empty<WorldVisibilityState>();
            maskColors = Array.Empty<Color32>();
            LastDirtyCellCount = 0;
            MaskApplyCount = 0;
            appliedRevision = 0;
            hasAppliedRevision = false;
        }

        private static Mesh CreateOverlayMesh(int width, int height)
        {
            float minimumX = -width * .5f - .5f;
            float maximumX = width * .5f - .5f;
            float minimumZ = -height * .5f - .5f;
            float maximumZ = height * .5f - .5f;
            var mesh = new Mesh { name = "IDEA0029 Fog Overlay Mesh" };
            mesh.vertices = new[]
            {
                new Vector3(minimumX, OverlayHeight, minimumZ),
                new Vector3(maximumX, OverlayHeight, minimumZ),
                new Vector3(maximumX, OverlayHeight, maximumZ),
                new Vector3(minimumX, OverlayHeight, maximumZ),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BindMaskTexture(
            Material material,
            Texture texture)
        {
            if (material.HasProperty("_FogMask"))
                material.SetTexture("_FogMask", texture);
            else if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            else if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
        }

        private void EnsureGenerated()
        {
            if (coordinates == null || maskTexture == null ||
                overlayObject == null || overlayMesh == null)
            {
                throw new InvalidOperationException(
                    "Generate the fog presenter before applying visibility.");
            }
        }

        private void ValidateCell(int x, int y)
        {
            EnsureGenerated();
            if (!coordinates.ContainsCell(x, y))
                throw new ArgumentOutOfRangeException(nameof(x));
        }

        private static void DestroyOwned(UnityEngine.Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }

        private void OnDestroy()
        {
            ClearPresentation();
        }
    }
}
