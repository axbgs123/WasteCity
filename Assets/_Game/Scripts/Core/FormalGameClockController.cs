using UnityEngine;

namespace WasteCity.Core
{
    public sealed class FormalGameClockController : MonoBehaviour
    {
        [SerializeField] private float secondsPerDay = 600f;
        public GameClockModel Model { get; private set; }
        private void Awake() => Model = new GameClockModel(secondsPerDay);
        private void Update() => Model.Tick(Time.deltaTime);
    }
}
