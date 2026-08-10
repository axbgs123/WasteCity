using System;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.ArtIntegration3D
{
    public sealed class FirstArtTerrainRenderer3D : MonoBehaviour,
        IGrayboxTerrainPresentation3D
    {
        private static readonly int BaseColorArrayId =
            Shader.PropertyToID("_BaseColorArray");
        private static readonly int NormalArrayId =
            Shader.PropertyToID("_NormalArray");
        private static readonly int MaskArrayId =
            Shader.PropertyToID("_MaskArray");
        private static readonly int HeightArrayId =
            Shader.PropertyToID("_HeightArray");
        private static readonly int ControlAId =
            Shader.PropertyToID("_ControlA");
        private static readonly int ControlBId =
            Shader.PropertyToID("_ControlB");
        private static readonly int WorldOriginId =
            Shader.PropertyToID("_WorldOriginXZ");
        private static readonly int WorldSizeId =
            Shader.PropertyToID("_WorldSizeXZ");
        private static readonly int CellsPerTextureId =
            Shader.PropertyToID("_CellsPerTexture");
        private static readonly int HeightBlendStrengthId =
            Shader.PropertyToID("_HeightBlendStrength");
        private static readonly int WaterVelocityAId =
            Shader.PropertyToID("_WaterVelocityA");
        private static readonly int WaterVelocityBId =
            Shader.PropertyToID("_WaterVelocityB");

        [SerializeField] private FirstArtTerrainProfile3D profile;

        private GrayboxWorldView3D retainedWorldView;
        private GameObject runtimeSurface;
        private Mesh ownedMesh;
        private MeshRenderer surfaceRenderer;
        private FirstArtTerrainControlMap3D controlMaps;
        private MaterialPropertyBlock propertyBlock;

        public FirstArtTerrainProfile3D Profile => profile;
        public bool IsPresented =>
            runtimeSurface != null &&
            ownedMesh != null &&
            surfaceRenderer != null &&
            controlMaps != null;
        public MeshRenderer SurfaceRenderer => surfaceRenderer;
        public FirstArtTerrainControlMap3D ControlMaps => controlMaps;

        public void Configure(FirstArtTerrainProfile3D profile)
        {
            this.profile = profile;
        }

        public bool TryPresent(GrayboxWorldView3D worldView)
        {
            ClearPresentation();
            retainedWorldView = worldView;

            Mesh localMesh = null;
            FirstArtTerrainControlMap3D localControlMaps = null;
            GameObject localSurface = null;
            try
            {
                ValidateSource(worldView);
                WorldMapModel model = worldView.Model;
                localMesh = FirstArtTerrainMeshBuilder3D.Build(
                    model.Width,
                    model.Height);
                localControlMaps =
                    FirstArtTerrainControlMapGenerator3D.Generate(
                        model,
                        profile);

                localSurface = new GameObject("RuntimeSurface");
                localSurface.transform.SetParent(transform, false);
                MeshFilter filter = localSurface.AddComponent<MeshFilter>();
                MeshRenderer renderer =
                    localSurface.AddComponent<MeshRenderer>();
                filter.sharedMesh = localMesh;
                renderer.sharedMaterial = profile.Material;

                ApplyProperties(
                    renderer,
                    localMesh,
                    localControlMaps,
                    model);
                VerifyLocalPresentation(
                    filter,
                    renderer,
                    localMesh,
                    localControlMaps);

                runtimeSurface = localSurface;
                ownedMesh = localMesh;
                surfaceRenderer = renderer;
                controlMaps = localControlMaps;
                localSurface = null;
                localMesh = null;
                localControlMaps = null;

                worldView.SetSurfaceFallbackVisible(false);
                return true;
            }
            catch (Exception exception)
            {
                ClearPresentation();
                DestroyOwned(localSurface);
                localControlMaps?.Dispose();
                DestroyOwned(localMesh);
                propertyBlock?.Clear();
                if (worldView != null)
                    worldView.SetSurfaceFallbackVisible(true);
                Debug.LogError(
                    "First-art terrain presentation failed: " +
                    exception.Message,
                    this);
                return false;
            }
        }

        public void ClearPresentation()
        {
            if (retainedWorldView != null)
                retainedWorldView.SetSurfaceFallbackVisible(true);

            if (propertyBlock != null)
            {
                propertyBlock.Clear();
                if (surfaceRenderer != null)
                    surfaceRenderer.SetPropertyBlock(propertyBlock);
            }

            GameObject surfaceToDestroy = runtimeSurface;
            Mesh meshToDestroy = ownedMesh;
            FirstArtTerrainControlMap3D mapsToDispose = controlMaps;
            runtimeSurface = null;
            ownedMesh = null;
            surfaceRenderer = null;
            controlMaps = null;

            DestroyOwned(surfaceToDestroy);
            mapsToDispose?.Dispose();
            DestroyOwned(meshToDestroy);
        }

        private void ValidateSource(GrayboxWorldView3D worldView)
        {
            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    "Presenter component must be active and enabled.");
            }
            if (profile == null)
                throw new InvalidOperationException("Terrain profile is required.");
            if (!profile.TryValidate(out string profileError))
                throw new InvalidOperationException(profileError);
            if (worldView == null)
                throw new ArgumentNullException(nameof(worldView));
            if (worldView.Model == null || worldView.Coordinates == null)
            {
                throw new InvalidOperationException(
                    "Graybox world model and coordinates are required.");
            }
        }

        private void ApplyProperties(
            MeshRenderer renderer,
            Mesh mesh,
            FirstArtTerrainControlMap3D maps,
            WorldMapModel model)
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
            propertyBlock.Clear();
            propertyBlock.SetTexture(BaseColorArrayId, profile.BaseColorArray);
            propertyBlock.SetTexture(NormalArrayId, profile.NormalArray);
            propertyBlock.SetTexture(MaskArrayId, profile.MaskArray);
            propertyBlock.SetTexture(HeightArrayId, profile.HeightArray);
            propertyBlock.SetTexture(ControlAId, maps.ControlA);
            propertyBlock.SetTexture(ControlBId, maps.ControlB);
            propertyBlock.SetVector(
                WorldOriginId,
                new Vector4(
                    mesh.bounds.min.x,
                    mesh.bounds.min.z,
                    0f,
                    0f));
            propertyBlock.SetVector(
                WorldSizeId,
                new Vector4(model.Width, model.Height, 0f, 0f));
            propertyBlock.SetFloat(
                CellsPerTextureId,
                profile.CellsPerTexture);
            propertyBlock.SetFloat(
                HeightBlendStrengthId,
                profile.HeightBlendStrength);
            propertyBlock.SetVector(
                WaterVelocityAId,
                new Vector4(
                    profile.WaterNormalVelocityA.x,
                    profile.WaterNormalVelocityA.y,
                    0f,
                    0f));
            propertyBlock.SetVector(
                WaterVelocityBId,
                new Vector4(
                    profile.WaterNormalVelocityB.x,
                    profile.WaterNormalVelocityB.y,
                    0f,
                    0f));
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void VerifyLocalPresentation(
            MeshFilter filter,
            MeshRenderer renderer,
            Mesh mesh,
            FirstArtTerrainControlMap3D maps)
        {
            renderer.GetPropertyBlock(propertyBlock);
            if (filter == null || renderer == null ||
                filter.sharedMesh != mesh ||
                renderer.sharedMaterial != profile.Material ||
                maps == null || maps.ControlA == null || maps.ControlB == null ||
                propertyBlock.GetTexture(BaseColorArrayId) !=
                    profile.BaseColorArray ||
                propertyBlock.GetTexture(ControlAId) != maps.ControlA ||
                propertyBlock.GetTexture(ControlBId) != maps.ControlB)
            {
                throw new InvalidOperationException(
                    "Formal terrain renderer references were not applied.");
            }
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

        private void OnEnable()
        {
            if (retainedWorldView != null &&
                retainedWorldView.Model != null &&
                retainedWorldView.Coordinates != null)
                TryPresent(retainedWorldView);
        }

        private void OnDisable()
        {
            ClearPresentation();
        }

        private void OnDestroy()
        {
            ClearPresentation();
            retainedWorldView = null;
            propertyBlock = null;
        }
    }
}
