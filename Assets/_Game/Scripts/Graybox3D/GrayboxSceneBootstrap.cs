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

        public bool IsInitialized { get; private set; }
        public WorldMapModel World { get; private set; }

        public void Configure(
            GrayboxUrpScope renderScope,
            GrayboxWorldView3D worldView)
        {
            this.renderScope = renderScope;
            this.worldView = worldView;
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
            IsInitialized = true;
            return true;
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
