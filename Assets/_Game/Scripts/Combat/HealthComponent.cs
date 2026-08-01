using UnityEngine;

namespace WasteCity.Combat
{
    public sealed class HealthComponent : MonoBehaviour
    {
        [SerializeField] private int maximum = 100;
        [SerializeField] private ArmorType armor = ArmorType.Light;
        public HealthModel Value { get; private set; }
        public ArmorType Armor => armor;
        private void Awake() => Value = new HealthModel(maximum);
        public void Configure(int max, ArmorType armorType) { maximum = max; armor = armorType; Value = new HealthModel(max); }
    }
}
