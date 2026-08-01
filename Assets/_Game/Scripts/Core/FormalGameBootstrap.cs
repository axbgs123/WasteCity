using UnityEngine;
using WasteCity.World;

namespace WasteCity.Core
{
    public sealed class FormalGameBootstrap : MonoBehaviour
    {
        [SerializeField] private int worldSeed = 8128;
        [SerializeField] private PlaceholderWorldView worldView;
        public int WorldSeed => worldSeed;
        private void Start() => worldView.Generate(new WorldSeed(worldSeed));
    }
}
