using UnityEngine;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    [DefaultExecutionOrder(-9000)]
    public sealed class GrayboxSceneBootstrap : MonoBehaviour
    {
        public const int WorldSeedValue = GrayboxWorldLayout3D.DefaultSeed;
        public const int WorldWidth = GrayboxWorldLayout3D.WorldWidth;
        public const int WorldHeight = GrayboxWorldLayout3D.WorldHeight;

        [SerializeField] private GrayboxUrpScope renderScope;
        [SerializeField] private GrayboxWorldView3D worldView;
        [SerializeField] private MonoBehaviour terrainPresentationBehaviour;

        public bool IsInitialized { get; private set; }
        public WorldMapModel World { get; private set; }

        public void Configure(
            GrayboxUrpScope renderScope,
            GrayboxWorldView3D worldView)
        {
            Configure(renderScope, worldView, null);
        }

        public void Configure(
            GrayboxUrpScope renderScope,
            GrayboxWorldView3D worldView,
            MonoBehaviour terrainPresentationBehaviour)
        {
            if (ReferenceEquals(this.renderScope, renderScope) &&
                ReferenceEquals(this.worldView, worldView) &&
                ReferenceEquals(
                    this.terrainPresentationBehaviour,
                    terrainPresentationBehaviour))
                return;

            ClearConfiguredPresentation();
            this.renderScope = renderScope;
            this.worldView = worldView;
            this.terrainPresentationBehaviour =
                terrainPresentationBehaviour;
            World = null;
            IsInitialized = false;
        }

        public bool Initialize()
        {
            if (IsInitialized)
                return true;
            if (renderScope == null ||
                worldView == null ||
                !renderScope.IsApplied)
                return false;

            World = GrayboxWorldLayout3D.CreateDefault();
            worldView.Generate(World);
            TryPresentTerrain();
            IsInitialized = true;
            return true;
        }

        private void TryPresentTerrain()
        {
            if (terrainPresentationBehaviour == null)
                return;

            var presentation = terrainPresentationBehaviour as
                IGrayboxTerrainPresentation3D;
            if (presentation == null)
            {
                worldView.SetSurfaceFallbackVisible(true);
                Debug.LogError(
                    "Graybox terrain presentation behaviour does not " +
                    "implement IGrayboxTerrainPresentation3D; surface " +
                    "fallback restored.",
                    this);
                return;
            }

            string presentationFailure;
            try
            {
                bool presented;
                var diagnosticAttempt = presentation as
                    IGrayboxTerrainPresentationAttempt3D;
                if (diagnosticAttempt != null)
                {
                    presented = diagnosticAttempt.TryPresent(
                        worldView,
                        false);
                }
                else
                {
                    presented = presentation.TryPresent(worldView);
                }

                if (presented)
                    return;

                presentationFailure =
                    diagnosticAttempt != null &&
                    !string.IsNullOrEmpty(
                        diagnosticAttempt.LastPresentationError)
                        ? "terrain presentation failed: " +
                          diagnosticAttempt.LastPresentationError
                        : "terrain presentation returned false";
            }
            catch (System.Exception exception)
            {
                presentationFailure =
                    "terrain presentation failed: " + exception.Message;
            }

            string cleanupFailure = ClearPresentationBestEffort(
                presentation,
                worldView);
            string message = "Graybox " + presentationFailure;
            if (!string.IsNullOrEmpty(cleanupFailure))
                message += "; cleanup failed: " + cleanupFailure;
            Debug.LogError(
                message + "; surface fallback restored.",
                this);
        }

        private void ClearConfiguredPresentation()
        {
            var presentation = terrainPresentationBehaviour as
                IGrayboxTerrainPresentation3D;
            if (presentation == null && worldView == null)
                return;

            string cleanupFailure = ClearPresentationBestEffort(
                presentation,
                worldView,
                true);
            if (!string.IsNullOrEmpty(cleanupFailure))
            {
                Debug.LogError(
                    "Graybox terrain presentation reconfiguration " +
                    "cleanup failed: " + cleanupFailure +
                    "; surface fallback restored.",
                    this);
            }
        }

        private static string ClearPresentationBestEffort(
            IGrayboxTerrainPresentation3D presentation,
            GrayboxWorldView3D configuredWorldView,
            bool releaseSource = false)
        {
            string cleanupFailure = null;
            if (presentation != null)
            {
                try
                {
                    var source = presentation as
                        IGrayboxTerrainPresentationSource3D;
                    if (releaseSource && source != null)
                        source.ReleasePresentationSource();
                    else
                        presentation.ClearPresentation();
                }
                catch (System.Exception exception)
                {
                    cleanupFailure = exception.Message;
                }
            }

            if (configuredWorldView != null)
            {
                configuredWorldView.DetachTerrainPresentation(presentation);
                configuredWorldView.SetSurfaceFallbackVisible(true);
            }
            return cleanupFailure;
        }

        private void Start()
        {
            if (!Initialize())
                Debug.LogError(
                    "Graybox 3D initialization requires an active URP " +
                    "scope and configured world view.",
                    this);
        }
    }
}
