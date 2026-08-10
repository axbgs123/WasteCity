using UnityEngine;
using WasteCity.World;

namespace WasteCity.Graybox3D
{
    [DefaultExecutionOrder(-9000)]
    public sealed class GrayboxSceneBootstrap : MonoBehaviour
    {
        public const int WorldSeedValue = 8128;
        public const int WorldWidth = 32;
        public const int WorldHeight = 24;

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
            this.renderScope = renderScope;
            this.worldView = worldView;
            this.terrainPresentationBehaviour =
                terrainPresentationBehaviour;
        }

        public bool Initialize()
        {
            if (IsInitialized)
                return true;
            if (renderScope == null ||
                worldView == null ||
                !renderScope.IsApplied)
                return false;

            World = new WorldMapModel(
                WorldWidth,
                WorldHeight,
                new WorldSeed(WorldSeedValue));
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

            try
            {
                if (presentation.TryPresent(worldView))
                    return;

                worldView.SetSurfaceFallbackVisible(true);
                Debug.LogError(
                    "Graybox terrain presentation returned false; " +
                    "surface fallback restored.",
                    this);
            }
            catch (System.Exception exception)
            {
                worldView.SetSurfaceFallbackVisible(true);
                Debug.LogError(
                    "Graybox terrain presentation failed: " +
                    exception.Message + "; surface fallback restored.",
                    this);
            }
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
