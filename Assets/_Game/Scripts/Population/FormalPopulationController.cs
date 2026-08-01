using UnityEngine;

namespace WasteCity.Population
{
    public sealed class FormalPopulationController : MonoBehaviour
    {
        public PopulationModel Model { get; private set; }
        private void Awake() => Model = new PopulationModel();
        public void AddPeople(int amount) => Model.AddPeople(amount);
        public void AddCapacity(int amount) => Model.AddCapacity(amount);
    }
}
