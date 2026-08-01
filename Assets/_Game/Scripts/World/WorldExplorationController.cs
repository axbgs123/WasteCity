using UnityEngine;
using WasteCity.City;

namespace WasteCity.World
{
    public sealed class WorldExplorationController : MonoBehaviour
    {
        [SerializeField] private PlaceholderWorldView world;
        [SerializeField] private PlaceholderMobileCity city;
        [SerializeField] private int revealRadius = 5;
        private void Update() => world.RevealAroundWorld(city.transform.position, revealRadius);
    }
}
