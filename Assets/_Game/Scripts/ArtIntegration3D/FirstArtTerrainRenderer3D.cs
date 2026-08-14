using System;
using System.Collections.Generic;
using UnityEngine;
using WasteCity.Graybox3D;
using WasteCity.World;

namespace WasteCity.ArtIntegration3D
{
    public enum FirstArtRuinsCliffPresentationStatus3D
    {
        NotConfigured = 0,
        Presented = 1,
        Fallback = 2,
    }

    public sealed class FirstArtTerrainRenderer3D : MonoBehaviour,
        IGrayboxTerrainPresentation3D,
        IGrayboxTerrainPresentationAttempt3D,
        IGrayboxTerrainPresentationSource3D
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
        [SerializeField] private FirstArtRuinsCliffProfile3D geometryProfile;

        private GrayboxWorldView3D retainedWorldView;
        private GameObject runtimeSurface;
        private Mesh ownedMesh;
        private MeshRenderer surfaceRenderer;
        private FirstArtTerrainControlMap3D controlMaps;
        private MaterialPropertyBlock propertyBlock;
        private GameObject runtimeGeometry;
        private FirstArtRuinsCliffCategoryGeometry3D ruinsGeometry;
        private FirstArtRuinsCliffCategoryGeometry3D cliffGeometry;

        public FirstArtTerrainProfile3D Profile => profile;
        public FirstArtRuinsCliffProfile3D GeometryProfile => geometryProfile;
        public bool IsPresented =>
            runtimeSurface != null &&
            ownedMesh != null &&
            surfaceRenderer != null &&
            controlMaps != null;
        public MeshRenderer SurfaceRenderer => surfaceRenderer;
        public FirstArtTerrainControlMap3D ControlMaps => controlMaps;
        public string LastPresentationError { get; private set; }
        public FirstArtRuinsCliffPresentationStatus3D RuinsStatus { get; private set; }
        public FirstArtRuinsCliffPresentationStatus3D CliffStatus { get; private set; }
        public string RuinsError { get; private set; }
        public string CliffError { get; private set; }

        public void Configure(FirstArtTerrainProfile3D profile)
        {
            Configure(profile, null);
        }

        public void Configure(
            FirstArtTerrainProfile3D profile,
            FirstArtRuinsCliffProfile3D geometryProfile)
        {
            if (ReferenceEquals(this.profile, profile) &&
                ReferenceEquals(this.geometryProfile, geometryProfile))
                return;

            ClearPresentation();
            this.profile = profile;
            this.geometryProfile = geometryProfile;
            LastPresentationError = null;
            ResetFamilyResults();
        }

        public bool TryPresent(GrayboxWorldView3D worldView)
        {
            return TryPresent(worldView, true);
        }

        public bool TryPresent(
            GrayboxWorldView3D worldView,
            bool logFailure)
        {
            ClearPresentation();
            LastPresentationError = null;
            ResetFamilyResults();
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

                worldView.AttachTerrainPresentation(this);
                worldView.SetSurfaceFallbackVisible(false);
                if (geometryProfile != null)
                    PresentGeometryCategories(worldView, logFailure);
                return true;
            }
            catch (Exception exception)
            {
                ClearPresentation();
                DestroyOwned(localSurface);
                localControlMaps?.Dispose();
                DestroyOwned(localMesh);
                propertyBlock?.Clear();
                LastPresentationError = exception.Message;
                ResetFamilyResults();
                if (logFailure)
                {
                    Debug.LogError(
                        "First-art terrain presentation failed: " +
                        exception.Message,
                        this);
                }
                return false;
            }
        }

        public void ClearPresentation()
        {
            if (retainedWorldView != null)
            {
                bool ownsFallback =
                    retainedWorldView.IsTerrainPresentationActive(this);
                bool hasCompetingPresentation =
                    retainedWorldView.HasActiveTerrainPresentation &&
                    !ownsFallback;
                if (ownsFallback)
                {
                    retainedWorldView.TrySetSurfaceFallbackVisible(
                        SurfaceStableId(FirstArtRuinsCliffFamily3D.Ruins),
                        true,
                        out _);
                    retainedWorldView.TrySetSurfaceFallbackVisible(
                        SurfaceStableId(FirstArtRuinsCliffFamily3D.Cliff),
                        true,
                        out _);
                }
                if (ownsFallback)
                    retainedWorldView.DetachTerrainPresentation(this);
                if (!hasCompetingPresentation)
                    retainedWorldView.SetSurfaceFallbackVisible(true);
            }

            FirstArtRuinsCliffCategoryGeometry3D ruinsToDispose = ruinsGeometry;
            FirstArtRuinsCliffCategoryGeometry3D cliffToDispose = cliffGeometry;
            GameObject geometryRootToDestroy = runtimeGeometry;
            ruinsGeometry = null;
            cliffGeometry = null;
            runtimeGeometry = null;
            ruinsToDispose?.Dispose();
            cliffToDispose?.Dispose();
            DestroyOwned(geometryRootToDestroy);

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
            ResetFamilyResults();
        }

        public void ReleasePresentationSource()
        {
            try
            {
                ClearPresentation();
            }
            finally
            {
                retainedWorldView = null;
            }
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

        private void PresentGeometryCategories(
            GrayboxWorldView3D worldView,
            bool logFailure)
        {
            IReadOnlyList<FirstArtRuinsCliffPlacement3D> projected;
            try
            {
                projected = FirstArtRuinsCliffLayout3D.Project(
                    worldView.Model,
                    worldView.Coordinates);
            }
            catch (Exception exception)
            {
                SetCategoryFailure(
                    worldView,
                    FirstArtRuinsCliffFamily3D.Ruins,
                    exception.Message,
                    logFailure);
                SetCategoryFailure(
                    worldView,
                    FirstArtRuinsCliffFamily3D.Cliff,
                    exception.Message,
                    logFailure);
                return;
            }

            var ruins = new List<FirstArtRuinsCliffPlacement3D>();
            var cliffs = new List<FirstArtRuinsCliffPlacement3D>();
            for (int index = 0; index < projected.Count; index++)
            {
                FirstArtRuinsCliffPlacement3D placement = projected[index];
                if (placement.Family == FirstArtRuinsCliffFamily3D.Ruins)
                    ruins.Add(placement);
                else
                    cliffs.Add(placement);
            }

            if (projected.Count > 0)
            {
                runtimeGeometry = new GameObject("RuntimeGeometry");
                runtimeGeometry.transform.SetParent(transform, false);
            }

            PresentGeometryCategory(
                worldView,
                FirstArtRuinsCliffFamily3D.Ruins,
                ruins,
                logFailure);
            PresentGeometryCategory(
                worldView,
                FirstArtRuinsCliffFamily3D.Cliff,
                cliffs,
                logFailure);

            if (ruinsGeometry == null && cliffGeometry == null)
            {
                GameObject emptyRoot = runtimeGeometry;
                runtimeGeometry = null;
                DestroyOwned(emptyRoot);
            }
        }

        private void PresentGeometryCategory(
            GrayboxWorldView3D worldView,
            FirstArtRuinsCliffFamily3D family,
            IReadOnlyList<FirstArtRuinsCliffPlacement3D> placements,
            bool logFailure)
        {
            string stableId = SurfaceStableId(family);
            if (placements.Count == 0)
            {
                if (!worldView.TrySetSurfaceFallbackVisible(
                        stableId,
                        false,
                        out string visibilityError))
                {
                    SetCategoryFailure(worldView, family, visibilityError, logFailure);
                    return;
                }
                SetCategorySuccess(family, null);
                return;
            }

            FirstArtRuinsCliffCategoryGeometry3D localGeometry = null;
            string error = null;
            try
            {
                if (runtimeGeometry == null)
                    throw new InvalidOperationException("Runtime geometry root is missing.");
                if (!FirstArtRuinsCliffGeometry3D.TryBuild(
                        geometryProfile,
                        placements,
                        runtimeGeometry.transform,
                        out localGeometry,
                        out error))
                {
                    SetCategoryFailure(worldView, family, error, logFailure);
                    return;
                }
                VerifyCategoryGeometry(localGeometry, family);
                if (!worldView.TrySetSurfaceFallbackVisible(
                        stableId,
                        false,
                        out error))
                    throw new InvalidOperationException(error);

                SetCategorySuccess(family, localGeometry);
                localGeometry = null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                SetCategoryFailure(worldView, family, error, logFailure);
            }
            finally
            {
                localGeometry?.Dispose();
            }
        }

        private static void VerifyCategoryGeometry(
            FirstArtRuinsCliffCategoryGeometry3D geometry,
            FirstArtRuinsCliffFamily3D family)
        {
            int expectedSlots = family == FirstArtRuinsCliffFamily3D.Ruins
                ? 8
                : 5;
            if (geometry == null ||
                geometry.Family != family ||
                geometry.GameObject == null ||
                geometry.Mesh == null)
                throw new InvalidOperationException("Category geometry ownership is incomplete.");
            MeshFilter filter = geometry.GameObject.GetComponent<MeshFilter>();
            MeshRenderer renderer = geometry.GameObject.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null ||
                filter.sharedMesh != geometry.Mesh ||
                geometry.Mesh.subMeshCount != expectedSlots ||
                renderer.sharedMaterials.Length != expectedSlots)
            {
                throw new InvalidOperationException(
                    "Category geometry renderer references are incomplete.");
            }
        }

        private void SetCategorySuccess(
            FirstArtRuinsCliffFamily3D family,
            FirstArtRuinsCliffCategoryGeometry3D geometry)
        {
            if (family == FirstArtRuinsCliffFamily3D.Ruins)
            {
                ruinsGeometry = geometry;
                RuinsStatus = FirstArtRuinsCliffPresentationStatus3D.Presented;
                RuinsError = null;
            }
            else
            {
                cliffGeometry = geometry;
                CliffStatus = FirstArtRuinsCliffPresentationStatus3D.Presented;
                CliffError = null;
            }
        }

        private void SetCategoryFailure(
            GrayboxWorldView3D worldView,
            FirstArtRuinsCliffFamily3D family,
            string error,
            bool logFailure)
        {
            string stableId = SurfaceStableId(family);
            if (!worldView.TrySetSurfaceFallbackVisible(
                    stableId,
                    true,
                    out string visibilityError))
            {
                error = string.IsNullOrEmpty(error)
                    ? visibilityError
                    : error + " " + visibilityError;
            }
            error = string.IsNullOrEmpty(error)
                ? "Unknown category presentation failure."
                : error;
            if (family == FirstArtRuinsCliffFamily3D.Ruins)
            {
                RuinsStatus = FirstArtRuinsCliffPresentationStatus3D.Fallback;
                RuinsError = error;
            }
            else
            {
                CliffStatus = FirstArtRuinsCliffPresentationStatus3D.Fallback;
                CliffError = error;
            }
            if (logFailure)
            {
                Debug.LogError(
                    "First-art " + family +
                    " geometry presentation failed: " + error,
                    this);
            }
        }

        private static string SurfaceStableId(FirstArtRuinsCliffFamily3D family)
        {
            return family == FirstArtRuinsCliffFamily3D.Ruins
                ? FirstArtRuinsCliffCatalog3D.Entries[0].SurfaceStableId
                : FirstArtRuinsCliffCatalog3D.Entries[
                    FirstArtRuinsCliffCatalog3D.RuinsEntryCount].SurfaceStableId;
        }

        private void ResetFamilyResults()
        {
            FirstArtRuinsCliffPresentationStatus3D resetStatus =
                geometryProfile == null
                    ? FirstArtRuinsCliffPresentationStatus3D.NotConfigured
                    : FirstArtRuinsCliffPresentationStatus3D.Fallback;
            RuinsStatus = resetStatus;
            CliffStatus = resetStatus;
            RuinsError = null;
            CliffError = null;
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
