using UnityEngine;
using WasteCity.Building;

namespace WasteCity.Population
{
    public sealed class FormalPopulationController : MonoBehaviour, IProductivitySource
    {
        public PopulationModel Model { get; private set; } = new PopulationModel();
        public float ConstructionMultiplier => Model == null ? 1f : Model.ProductivityMultiplier;
        public void AddPeople(int amount) => Model.AddPeople(amount);
        public void AddCapacity(int amount) => Model.AddCapacity(amount);
        public void Restore(int current, int capacity) => Model.Restore(current, capacity);
    }
}
